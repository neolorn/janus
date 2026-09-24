using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Callbacks;
using Janus.Core;
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
/// without reaching the route. A claim whose delivery the route did not carry through is
/// given back, so the provider's next delivery is carried.
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

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

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

        bool claimed = await admission
            .ClaimAsync(callback.Name, identifier, cancellationToken)
            .ConfigureAwait(false);

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        if (!claimed)
        {
            CallbackLog.Repeated(log, callback.Name, context.TraceIdentifier);
            context.Response.StatusCode = StatusCodes.Status200OK;

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
            if (!carried)
            {
                await ReleasedAsync(context, identifier, CancellationToken.None).ConfigureAwait(false);
            }
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

    // In a scope of its own, so that nothing the route left open in the request's
    // transaction holds the claim back; and under a token the request's abandonment
    // does not cancel, so an abandoned delivery gives its claim back too.
    private async ValueTask ReleasedAsync(HttpContext context, string identifier, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = context.RequestServices
            .GetRequiredService<IServiceScopeFactory>()
            .CreateAsyncScope();

        IUnitOfWork work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await scope.ServiceProvider
            .GetRequiredService<CallbackAdmission>()
            .ReleaseAsync(callback.Name, identifier, cancellationToken)
            .ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
