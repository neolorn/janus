using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.Extensions.DependencyInjection;

namespace HostSample;

/// <summary>
/// The workshop's own work when the library raises an event about a customer: an
/// erasure deletes the grip fittings held for the customer, the record of their health
/// the workshop has no reason to keep. Their repairs are kept, and the contact name each
/// holds under the customer's key no longer decrypts once the key is erased
/// (PRIV-RIGHT-005b).
/// </summary>
/// <param name="scopes">Where each event's scope is made.</param>
internal sealed class WorkshopSubjectEvents(IServiceScopeFactory scopes) : ISubjectEventSubscriber
{
    /// <inheritdoc/>
    public string Name => "workshop";

    /// <inheritdoc/>
    public bool Required => true;

    /// <inheritdoc/>
    public IReadOnlyCollection<ResourceType> Covers { get; } = [WorkshopModel.FittingType];

    /// <inheritdoc/>
    public async ValueTask<Result> HandleAsync(SubjectEvent raised, CancellationToken cancellationToken)
    {
        if (raised is not ErasureRequested { Subject: SubjectId customer })
        {
            return Result.Success();
        }

        return await CustomerFittings.DeleteAsync(scopes, customer, cancellationToken).ConfigureAwait(false);
    }
}
