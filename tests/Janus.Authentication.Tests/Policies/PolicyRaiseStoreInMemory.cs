using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Policies;
using Janus.Core;

namespace Janus.Authentication.Tests.Policies;

/// <summary>
/// The requirements each scope's policy has raised, held in memory. One raise stands
/// per scope per field, as the unique indexes hold it in the database.
/// </summary>
internal sealed class PolicyRaiseStoreInMemory : IPolicyRaiseStore
{
    private readonly Dictionary<(OrganizationId? Scope, PolicyField Field), PolicyRaise> _raises = [];

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<PolicyRaise>> OfAsync(
        OrganizationId? organization,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<PolicyRaise>>(
            [.. _raises
                .Where(entry => entry.Key.Scope == organization)
                .Select(entry => entry.Value)
                .OrderBy(raise => raise.At)]);

    /// <inheritdoc/>
    public ValueTask RecordAsync(
        OrganizationId? organization,
        IReadOnlyCollection<PolicyRaise> raised,
        CancellationToken cancellationToken)
    {
        foreach (PolicyRaise raise in raised)
        {
            _raises[(organization, raise.Field)] = raise;
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RemoveAsync(
        OrganizationId? organization,
        PolicyField field,
        CancellationToken cancellationToken)
    {
        _raises.Remove((organization, field));

        return ValueTask.CompletedTask;
    }
}
