using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Hosting.Callbacks;

/// <summary>
/// One of the host's callbacks whose provider cannot sign what it sends. What it says is
/// a hint, and the machine profile has the host confirm it with the provider before the
/// host's route is reached.
/// </summary>
/// <remarks>
/// Implements BFF-MACH-003 and INT-GEN-003. The host's route mounted for it is reached
/// only by a request within the rate limit and the published ranges, carrying a
/// correlation reference issued for this callback through
/// <see cref="ICallbackReferences"/>, which the provider's own API then confirms. A
/// failure either method returns refuses the delivery.
/// </remarks>
public interface IUnsignedCallback
{
    /// <summary>
    /// What the callback is counted, issued references for and recorded under; unique
    /// among the host's callbacks.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The address ranges the provider publishes for its callbacks; empty where it
    /// publishes none.
    /// </summary>
    IReadOnlyCollection<IPNetwork> Sources { get; }

    /// <summary>
    /// Reads the correlation reference the delivery carries.
    /// </summary>
    /// <param name="delivery">The delivery.</param>
    /// <returns>The reference, or a failure where it carries none.</returns>
    Result<string> ReferenceOf(CallbackDelivery delivery);

    /// <summary>
    /// Asks the provider's API whether what the delivery says is so. Nothing the
    /// delivery says is taken as a fact until this succeeds.
    /// </summary>
    /// <param name="delivery">The delivery, whose reference was issued for this callback.</param>
    /// <param name="cancellationToken">Abandons the question.</param>
    /// <returns>Success where the provider confirms it; a failure otherwise.</returns>
    ValueTask<Result> ConfirmAsync(CallbackDelivery delivery, CancellationToken cancellationToken);
}
