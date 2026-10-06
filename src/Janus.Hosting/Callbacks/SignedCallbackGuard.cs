using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Callbacks;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Janus.Hosting.Callbacks;

/// <summary>
/// The machine profile's checks for one of the host's signed callbacks, run before the
/// host's route.
/// </summary>
/// <param name="callback">The callback, as the host declared it.</param>
/// <remarks>
/// Implements BFF-MACH-002, INT-GEN-003 and entry 276. The signature is verified over
/// the raw bytes before anything parses them, compared in fixed time against the one
/// each live secret gives, and a delivery of an event already carried is acknowledged
/// without reaching the route. The claim is committed before the route runs, settled
/// once the route answers 2xx and given back where it does not or throws, so the
/// provider's next delivery is carried. A delivery meeting a claim still unsettled is
/// answered <c>integration.callback.inprogress</c> until the claim has stood for
/// <c>integration.callback.claimtimeout</c>, when the delivery takes it over.
/// </remarks>
internal sealed class SignedCallbackGuard(ISignedCallback callback) : IMiddleware
{
    // BFF-MACH-002: five minutes or less where the scheme carries an instant.
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(5);

    // BFF-MACH-002: both secrets verify for 24 hours after a rotation.
    private static readonly TimeSpan Overlap = TimeSpan.FromHours(24);

    /// <summary>
    /// Runs the checks.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="next">The host's route, and whatever the host mounted before it.</param>
    /// <returns>The work of carrying or refusing it.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        CancellationToken cancellationToken = context.RequestAborted;
        CallbackAdmission admission = context.RequestServices.GetRequiredService<CallbackAdmission>();
        IUnitOfWork work = context.RequestServices.GetRequiredService<IUnitOfWork>();
        TimeProvider time = context.RequestServices.GetRequiredService<TimeProvider>();
        ILogger log = context.RequestServices.GetRequiredService<ILogger<SignedCallbackGuard>>();

        (await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Switch(_ => { }, error => throw new InvalidOperationException(error.Code.ToString()));

        if (!await CallbackIntake
                .AdmittedAsync(context, callback.Name, callback.Sources, admission, work, log, cancellationToken)
                .ConfigureAwait(false))
        {
            return;
        }

        CallbackDelivery delivery = await CallbackIntake.ReadAsync(context, cancellationToken).ConfigureAwait(false);

        CallbackCheck? check = await RefusalAsync(delivery, time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        string? identifier = check is null
            ? callback.EventOf(delivery).Match<string?>(value => value, _ => null)
            : null;

        if (identifier is null)
        {
            await CallbackIntake
                .RefusedAsync(context, callback.Name, check ?? CallbackCheck.Event, admission, work, log, cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        DateTimeOffset claimedAt = time.GetUtcNow();
        CallbackClaim claim = (await admission
                .ClaimAsync(callback.Name, identifier, claimedAt, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => throw new InvalidOperationException(error.Code.ToString()));

        (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Switch(() => { }, error => throw new InvalidOperationException(error.Code.ToString()));

        if (claim is CallbackClaim.Settled)
        {
            CallbackLog.Repeated(log, callback.Name, context.TraceIdentifier);
            context.Response.StatusCode = StatusCodes.Status200OK;

            return;
        }

        // Not a rejection: the provider delivers it again, and the delivery is carried
        // once the claim settles or is given back, or has stood past the timeout.
        if (claim is CallbackClaim.InProgress)
        {
            CallbackLog.InProgress(log, callback.Name, context.TraceIdentifier);
            await Refusal.WriteAsync(context, ErrorCodes.CallbackInProgress, cancellationToken).ConfigureAwait(false);

            return;
        }

        bool carried = false;

        try
        {
            await next(context).ConfigureAwait(false);

            carried = context.Response.StatusCode is >= 200 and < 300;
        }
        finally
        {
            await ConcludedAsync(context, identifier, claimedAt, carried, CancellationToken.None).ConfigureAwait(false);
        }
    }

    // Which check the delivery fails, or nothing where its signature holds. Every
    // presented signature is compared against every live secret, whichever matches
    // first, so how long the answer takes says nothing of which did. Secrets the
    // secrets manager cannot give verify nothing.
    private async ValueTask<CallbackCheck?> RefusalAsync(
        CallbackDelivery delivery,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        CallbackSignature? presented = callback.Presented(delivery).Match<CallbackSignature?>(
            value => value,
            _ => null);

        if (presented is null || presented.Presented.Count == 0)
        {
            return CallbackCheck.Signature;
        }

        if (presented.SignedAt is DateTimeOffset signedAt && (now - signedAt).Duration() > Window)
        {
            return CallbackCheck.Window;
        }

        Result<CallbackSecrets> read = await callback.ReadSecretsAsync(cancellationToken).ConfigureAwait(false);

        if (read.Match<CallbackSecrets?>(value => value, _ => null) is not CallbackSecrets secrets)
        {
            return CallbackCheck.Verification;
        }

        bool previousLive = secrets.Previous is not null && now < secrets.CurrentSince + Overlap;
        bool matched = false;

        foreach (ReadOnlyMemory<byte> signature in presented.Presented)
        {
            matched |= Matches(signature.Span, presented.Message.Span, secrets.Current.Span);

            if (previousLive)
            {
                matched |= Matches(signature.Span, presented.Message.Span, secrets.Previous!.Value.Span);
            }
        }

        return matched ? null : CallbackCheck.Verification;
    }

    private bool Matches(ReadOnlySpan<byte> signature, ReadOnlySpan<byte> message, ReadOnlySpan<byte> secret)
    {
        byte[] expected = CryptographicOperations.HmacData(callback.Algorithm, secret, message);

        try
        {
            return CryptographicOperations.FixedTimeEquals(expected, signature);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(expected);
        }
    }

    // The claim settled where the route carried the delivery and given back where it
    // did not or threw. In a scope of its own, so that nothing the route left open in
    // the request's transaction holds the claim back; and under a token the request's
    // abandonment does not cancel, so an abandoned delivery concludes its claim too.
    private async ValueTask ConcludedAsync(
        HttpContext context,
        string identifier,
        DateTimeOffset claimedAt,
        bool carried,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = context.RequestServices
            .GetRequiredService<IServiceScopeFactory>()
            .CreateAsyncScope();

        IUnitOfWork work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        CallbackAdmission admission = scope.ServiceProvider.GetRequiredService<CallbackAdmission>();

        (await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Switch(_ => { }, error => throw new InvalidOperationException(error.Code.ToString()));

        if (carried)
        {
            await admission.SettleAsync(callback.Name, identifier, claimedAt, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await admission.ReleaseAsync(callback.Name, identifier, claimedAt, cancellationToken).ConfigureAwait(false);
        }

        (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Switch(() => { }, error => throw new InvalidOperationException(error.Code.ToString()));
    }
}
