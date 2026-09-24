using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Callbacks;
using Janus.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Janus.Hosting.Callbacks;

/// <summary>
/// The machine profile's checks for one of the host's unsigned callbacks, run before
/// the host's route.
/// </summary>
/// <param name="callback">The callback, as the host declared it.</param>
/// <remarks>
/// Implements BFF-MACH-003, INT-GEN-003 and entry 276. What the callback says is a hint:
/// it reaches the route only once it carries a reference issued for this callback and
/// the provider's own API has confirmed it. A guessed reference and an unconfirmed hint
/// are refused, recorded and counted towards the alert.
/// </remarks>
internal sealed class UnsignedCallbackGuard(IUnsignedCallback callback) : IMiddleware
{
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
        CallbackReferences references = context.RequestServices.GetRequiredService<CallbackReferences>();
        IUnitOfWork work = context.RequestServices.GetRequiredService<IUnitOfWork>();
        ILogger log = context.RequestServices.GetRequiredService<ILogger<UnsignedCallbackGuard>>();

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        if (!await CallbackIntake
                .AdmittedAsync(context, callback.Name, callback.Sources, admission, work, log, cancellationToken)
                .ConfigureAwait(false))
        {
            return;
        }

        CallbackDelivery delivery = await CallbackIntake.ReadAsync(context, cancellationToken).ConfigureAwait(false);
        string? reference = callback.ReferenceOf(delivery).Match<string?>(value => value, _ => null);

        if (!await references
                .RecognisesAsync(callback.Name, reference, cancellationToken)
                .ConfigureAwait(false))
        {
            await CallbackIntake
                .RefusedAsync(context, callback.Name, CallbackCheck.Reference, admission, work, log, cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        Result confirmed = await callback.ConfirmAsync(delivery, cancellationToken).ConfigureAwait(false);

        if (!confirmed.Match(() => true, _ => false))
        {
            await work.BeginAsync(cancellationToken).ConfigureAwait(false);
            await CallbackIntake
                .RefusedAsync(context, callback.Name, CallbackCheck.Confirmation, admission, work, log, cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        await next(context).ConfigureAwait(false);
    }
}
