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
/// Implements IDN-LIFE-012a, INT-GEN-003, BFF-MACH-001 and BFF-MACH-003. Google delivers
/// the event as the request's body (RFC 8935) and is answered 202; Apple wraps it in a
/// JSON object under <c>payload</c> and is answered 200. The providers publish no ranges
/// their deliveries come from, so no source is refused for where it is. Nothing an
/// event says is believed before its signature holds; what an unverified one names is
/// used only to record the refusal against the account it names.
/// </remarks>
internal sealed class ProviderEventIntake(
    ProviderKeys keys,
    ProviderEvents events,
    CallbackAdmission admission,
    IUnitOfWork work,
    ILogger<ProviderEventIntake> log)
{
    private static readonly IReadOnlyCollection<IPNetwork> Anywhere = [];

    // How each provider delivers its events and expects to be answered.
    private static readonly FrozenDictionary<Factor, Delivery> Deliveries = new Dictionary<Factor, Delivery>
    {
        [Factor.Google] = new(Enveloped: false, StatusCodes.Status202Accepted, Google),
        [Factor.Apple] = new(Enveloped: true, StatusCodes.Status200OK, Apple),
    }.ToFrozenDictionary();

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

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        if (!await CallbackIntake
                .AdmittedAsync(context, callback, Anywhere, admission, work, log, cancellationToken)
                .ConfigureAwait(false))
        {
            return;
        }

        CallbackDelivery delivery = await CallbackIntake.ReadAsync(context, cancellationToken).ConfigureAwait(false);

        if (Token(delivered, delivery.Body.Span) is not string token)
        {
            await CallbackIntake
                .RefusedAsync(context, callback, CallbackCheck.Signature, admission, work, log, cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        if (!await keys.VerifiesAsync(provider, token, cancellationToken).ConfigureAwait(false))
        {
            // IDN-LIFE-012a AC1: an unsigned event changes nothing and is audited as
            // rejected against the account it names, where it names one.
            ProviderNotice? named = Notice(delivered, token);

            await events
                .RejectedAsync(provider, named?.Subject, named?.Type, cancellationToken)
                .ConfigureAwait(false);
            await CallbackIntake
                .RefusedAsync(context, callback, CallbackCheck.Verification, admission, work, log, cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        if (Notice(delivered, token) is not { EventId.Length: > 0 } notice)
        {
            await CallbackIntake
                .RefusedAsync(context, callback, CallbackCheck.Event, admission, work, log, cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        Result<bool> taken = await events
            .TakeAsync(notice, RequestOrigin.Source(context.Request), cancellationToken)
            .ConfigureAwait(false);

        if (taken.Match(_ => (Error?)null, error => error) is Error failed)
        {
            await Refusal.WriteAsync(context, failed, cancellationToken).ConfigureAwait(false);

            return;
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

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
    // identity and address that event concerns. It judges nothing; the caller has
    // either verified the token or uses what it names only to record the refusal.
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

    // Whether the token arrives inside a JSON object under `payload` or as the whole
    // body, the status a carried event is answered with, and what reads its one event.
    private sealed record Delivery(
        bool Enveloped,
        int Answer,
        Func<string, JsonElement, ProviderNotice?> Read);
}
