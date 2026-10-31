using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Maintenance;

namespace Janus.Authentication.Tests.Maintenance;

/// <summary>
/// The completed rotations and the bootstrap the audit trail would record, held in
/// memory.
/// </summary>
internal sealed class CryptoperiodStoreInMemory : ICryptoperiodStore
{
    /// <summary>
    /// Every completed rotation, of either key, as its <c>ops.keyrotation.completed</c>
    /// record states it: the kind as chapter 10 section 5.41 spells it.
    /// </summary>
    public List<(string Kind, int Version, DateTimeOffset CompletedAt)> Rotations { get; } = [];

    /// <summary>
    /// When bootstrap created the first organization, where it has.
    /// </summary>
    public DateTimeOffset? Bootstrapped { get; set; }

    /// <inheritdoc/>
    public ValueTask<(int Version, DateTimeOffset CompletedAt)?> KeyEncryptionKeyRotatedAsync(
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(
            Rotations
                .Where(rotation => rotation.Kind == "key-encryption-key")
                .OrderByDescending(rotation => rotation.CompletedAt)
                .Select(rotation => ((int Version, DateTimeOffset CompletedAt)?)(rotation.Version, rotation.CompletedAt))
                .FirstOrDefault());

    /// <inheritdoc/>
    public ValueTask<DateTimeOffset?> BootstrappedAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(Bootstrapped);
}
