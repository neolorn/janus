using System;
using System.Buffers.Text;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Callbacks;
using Janus.Authentication.Credentials;
using Janus.Core;
using Janus.Hosting.Bff;
using Janus.Hosting.Callbacks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Janus.Hosting.Credentials;

/// <summary>
/// One social provider's security event as it arrives: counted, verified against the
/// provider's published keys, read, carried once and answered the way the provider
/// expects.
/// </summary>
/// <param name="keys">What verifies an event against its provider's keys.</param>
/// <param name="events">What carries a verified event.</param>
/// <param name="admission">What counts callbacks and refusals.</param>
/// <param name="work">The one transaction the event runs in.</param>
/// <param name="log">Where a refusal is recorded.</param>
/// <remarks>
/// Implements IDN-LIFE-012a, INT-GEN-003, LIB-API-003, BFF-MACH-001 and BFF-MACH-003.
/// Google delivers the event as the request's body (RFC 8935) and is answered 202, and
/// a token that fails validation 400 with the code RFC 8935 section 2.4 gives the first
/// failure; Apple wraps it in a JSON object under <c>payload</c>, is answered 200, and
/// is refused as every rejected callback is. A provider document that cannot be read
/// refuses nothing on either route: the delivery is answered as a fault, with nothing
/// claimed, recorded or changed, so the provider may deliver it again. The providers
/// publish no ranges their deliveries come from, so no source is refused for where it
/// is. Nothing an event says is believed before its signature holds; what an unverified
/// one names is used only to record the refusal against the account it names.
/// </remarks>
internal sealed class ProviderEventIntake(
    ProviderKeys keys,
    ProviderEvents events,
    CallbackAdmission admission,
    IUnitOfWork work,
    ILogger<ProviderEventIntake> log)
{
    // RFC 8935 section 2.4: the codes a refused Security Event Token is answered with.
    private const string InvalidRequest = "invalid_request";

    private const string InvalidKey = "invalid_key";

    private const string InvalidIssuer = "invalid_issuer";

    private const string InvalidAudience = "invalid_audience";

    // RFC 8935 section 2.3: the language the refusal's description is declared in.
    private const string DescriptionLanguage = "en";

    private static readonly IReadOnlyCollection<IPNetwork> Anywhere = [];

    // How each provider delivers its events and expects to be answered.
    private static readonly FrozenDictionary<Factor, Delivery> Deliveries = new Dictionary<Factor, Delivery>
    {
        [Factor.Google] = new(Enveloped: false, Pushed: true, StatusCodes.Status202Accepted, Google),
        [Factor.Apple] = new(Enveloped: true, Pushed: false, StatusCodes.Status200OK, Apple),
    }.ToFrozenDictionary();

    /// <summary>
    /// The status a provider's delivery is answered with once it is taken, which is the
    /// provider's own convention and part of the endpoint's contract (LIB-API-001).
    /// </summary>
    /// <param name="provider">Which social provider's route.</param>
    /// <returns>The status.</returns>
    public static int Taken(Factor provider) => Deliveries[provider].Answer;

    /// <summary>
    /// Whether a provider delivers as RFC 8935 does, and so is refused in that
    /// standard's shape.
    /// </summary>
    /// <param name="provider">Which social provider's route.</param>
    /// <returns>Whether it pushes its events.</returns>
    public static bool Pushes(Factor provider) => Deliveries[provider].Pushed;

    /// <summary>
    /// Takes one delivery.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="provider">Which social provider's route it arrived on.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of carrying or refusing it.</returns>
    /// <exception cref="ArgumentNullException">The request is absent.</exception>
    public async Task TakeAsync(HttpContext context, Factor provider, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        string callback = ProviderEvents.CallbackOf(provider);
        Delivery delivered = Deliveries[provider];

        (await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Switch(() => { }, error => throw new InvalidOperationException(error.Code.ToString()));

        if (!await CallbackIntake
                .AdmittedAsync(context, callback, Anywhere, admission, work, log, cancellationToken)
                .ConfigureAwait(false))
        {
            return;
        }

        CallbackDelivery delivery = await CallbackIntake.ReadAsync(context, cancellationToken).ConfigureAwait(false);

        if (Token(delivered, delivery.Body.Span) is not string token)
        {
            await RefusedAsync(context, delivered, callback, CallbackCheck.Signature, InvalidRequest, cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        // IDN-LIFE-012a AC7, chapter 09 section 10: a token that cannot be read as a
        // Security Event Token, or carries no jti, is the first failure, refused
        // before the provider is looked for and before anything is claimed.
        if (Notice(delivered, token) is not { EventId.Length: > 0 } notice)
        {
            await RefusedAsync(context, delivered, callback, CallbackCheck.Event, InvalidRequest, cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        ProviderEventVerification verification = await keys
            .VerifiedAsync(provider, token, cancellationToken)
            .ConfigureAwait(false);

        if (verification is ProviderEventVerification.Unreadable)
        {
            // IDN-LIFE-012a AC8: a document that cannot be read refuses nothing. The
            // delivery is a fault, nothing of it is kept, and the provider may deliver
            // it again.
            await work.RollbackAsync().ConfigureAwait(false);

            await Refusal
                .WriteAsync(
                    context,
                    Error.From(ErrorCodes.SystemFault, "callback", JsonSerializer.SerializeToElement(callback)),
                    cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        if (verification is not ProviderEventVerification.Verified)
        {
            // IDN-LIFE-012a AC1: an unsigned event changes nothing and is audited as
            // rejected against the account it names, where it names one.
            await events
                .RejectedAsync(provider, notice.Subject, notice.Type, cancellationToken)
                .ConfigureAwait(false);
            await RefusedAsync(
                    context,
                    delivered,
                    callback,
                    CallbackCheck.Verification,
                    verification switch
                    {
                        ProviderEventVerification.Key => InvalidKey,
                        ProviderEventVerification.Audience => InvalidAudience,
                        ProviderEventVerification.Lifetime => InvalidRequest,
                        _ => InvalidIssuer,
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        Result<bool> taken = await events
            .TakeAsync(notice, RequestOrigin.Source(context.Request), cancellationToken)
            .ConfigureAwait(false);

        if (taken.Match(_ => (Error?)null, error => error) is Error failed)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            await Refusal.WriteAsync(context, failed, cancellationToken).ConfigureAwait(false);

            return;
        }

        (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Switch(() => { }, error => throw new InvalidOperationException(error.Code.ToString()));

        if (!taken.Match(carried => carried, _ => false))
        {
            CallbackLog.Repeated(log, callback, context.TraceIdentifier);
        }

        context.Response.StatusCode = delivered.Answer;
    }

    // The token as each provider delivers it; a body that carries none carries no
    // signature either.
    private static string? Token(Delivery delivered, ReadOnlySpan<byte> body)
    {
        if (!delivered.Enveloped)
        {
            string whole = Encoding.UTF8.GetString(body).Trim();

            return whole.Length > 0 ? whole : null;
        }

        using JsonDocument? document = Parsed(body.ToArray()).Match<JsonDocument?>(read => read, _ => null);

        return document is null ? null : Text(document.RootElement, "payload");
    }

    // What an event says, read from its claims: its identifier, its one event, and the
    // identity and address that event concerns, or nothing where the token does not
    // read as a Security Event Token. It judges nothing; the caller either verifies the
    // token before it acts on it or uses what it names only to record the refusal.
    private static ProviderNotice? Notice(Delivery delivered, [NeverLogged] string token)
    {
        string[] parts = token.Split('.');

        if (parts.Length != 3 || !Base64Url.IsValid(parts[1]))
        {
            return null;
        }

        using JsonDocument? document = Parsed(Base64Url.DecodeFromChars(parts[1]))
            .Match<JsonDocument?>(read => read, _ => null);

        if (document is null
            || document.RootElement.ValueKind is not JsonValueKind.Object
            || !document.RootElement.TryGetProperty("events", out JsonElement carried))
        {
            return null;
        }

        string eventId = Text(document.RootElement, "jti") ?? string.Empty;

        return delivered.Read(eventId, carried);
    }

    // RISC and the OAuth event types: one event a token, keyed by its type, naming the
    // identity by the provider's issuer and subject.
    private static ProviderNotice? Google(string eventId, JsonElement carried)
    {
        if (carried.ValueKind is not JsonValueKind.Object)
        {
            return null;
        }

        JsonProperty[] named = [.. carried.EnumerateObject()];

        if (named.Length != 1 || named[0].Name.Length is 0)
        {
            return null;
        }

        string? subject = named[0].Value.ValueKind is JsonValueKind.Object
            && named[0].Value.TryGetProperty("subject", out JsonElement identity)
            && Text(identity, "subject_type") is "iss-sub"
                ? Text(identity, "sub")
                : null;

        return new ProviderNotice(Factor.Google, eventId, named[0].Name, subject, Address: null);
    }

    // Sign in with Apple: one event a token, whose claim Apple sends as an object or as
    // the text of one.
    private static ProviderNotice? Apple(string eventId, JsonElement carried)
    {
        if (carried.ValueKind is JsonValueKind.Object)
        {
            return AppleEvent(eventId, carried);
        }

        if (carried.ValueKind is not JsonValueKind.String)
        {
            return null;
        }

        using JsonDocument? document = Parsed(Encoding.UTF8.GetBytes(carried.GetString()!))
            .Match<JsonDocument?>(read => read, _ => null);

        return document is null ? null : AppleEvent(eventId, document.RootElement);
    }

    private static ProviderNotice? AppleEvent(string eventId, JsonElement one)
    {
        if (Text(one, "type") is not string type)
        {
            return null;
        }

        return new ProviderNotice(Factor.Apple, eventId, type, Text(one, "sub"), Text(one, "email"));
    }

    // What arrived is the provider's or anyone's, so text that is not JSON is a
    // refusal rather than a fault.
    private static Result<JsonDocument> Parsed(byte[] text)
    {
        try
        {
            return Result.Success(JsonDocument.Parse(text));
        }
        catch (JsonException)
        {
            return Result.Failure<JsonDocument>(Error.From(ErrorCodes.CallbackRejected));
        }
    }

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind is JsonValueKind.Object
        && element.TryGetProperty(name, out JsonElement value)
        && value.ValueKind is JsonValueKind.String
        && value.GetString() is { Length: > 0 } text
            ? text
            : null;

    // Counts and records one refusal as every callback's is, and answers it the way the
    // provider expects: in the shape RFC 8935 section 2.3 fixes on the route of a
    // provider that delivers as that standard does, the description carrying the code
    // again and never a sentence (LIB-API-003), and as a rejected callback otherwise. A
    // failure that kept the refusal from being counted is answered as itself.
    private async ValueTask RefusedAsync(
        HttpContext context,
        Delivery delivered,
        string callback,
        CallbackCheck check,
        string err,
        CancellationToken cancellationToken)
    {
        Error refused = await CallbackIntake
            .CountedAsync(context, callback, check, admission, work, log, cancellationToken)
            .ConfigureAwait(false);

        if (!delivered.Pushed || refused.Code != ErrorCodes.CallbackRejected)
        {
            await Refusal.WriteAsync(context, refused, cancellationToken).ConfigureAwait(false);

            return;
        }

        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Response.Headers.ContentLanguage = DescriptionLanguage;

        await context.Response
            .WriteAsJsonAsync(
                new SecurityEventError(err, err),
                CredentialsJson.Default.SecurityEventError,
                "application/json",
                cancellationToken)
            .ConfigureAwait(false);
    }

    // Whether the token arrives inside a JSON object under `payload` or as the whole
    // body, whether the provider delivers as RFC 8935 does and is refused in that
    // standard's shape, the status a carried event is answered with, and what reads its
    // one event.
    private sealed record Delivery(
        bool Enveloped,
        bool Pushed,
        int Answer,
        Func<string, JsonElement, ProviderNotice?> Read);
}
