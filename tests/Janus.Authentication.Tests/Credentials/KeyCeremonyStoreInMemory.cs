using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Credentials;
using Janus.Core;

namespace Janus.Authentication.Tests.Credentials;

/// <summary>
/// The creation ceremonies accounts have open, held in memory. One stands per
/// account, as the primary key holds it.
/// </summary>
internal sealed class KeyCeremonyStoreInMemory : IKeyCeremonyStore
{
    private readonly Dictionary<SubjectId, KeyCeremony> _open = [];

    /// <inheritdoc/>
    public ValueTask<KeyCeremony?> FindAsync(
        SubjectId subject,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(_open.GetValueOrDefault(subject));

    /// <inheritdoc/>
    public ValueTask ReplaceAsync(KeyCeremony ceremony, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ceremony);

        _open[ceremony.Subject] = ceremony;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RemoveAsync(SubjectId subject, CancellationToken cancellationToken)
    {
        _ = _open.Remove(subject);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        SubjectId[] gone = [.. _open
            .Where(ceremony => ceremony.Value.ExpiresAt <= now)
            .Select(ceremony => ceremony.Key)];

        foreach (SubjectId subject in gone)
        {
            _ = _open.Remove(subject);
        }

        return ValueTask.FromResult(gone.Length);
    }
}
