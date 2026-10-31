using System;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Authentication.Maintenance;

/// <summary>
/// Where the records the key-encryption key's cryptoperiod is measured from are read.
/// </summary>
/// <remarks>Implements DR-009a and CONV-DESIGN-003.</remarks>
internal interface ICryptoperiodStore
{
    /// <summary>
    /// The latest rotation of the key-encryption key the audit trail records as
    /// completed (<c>ops.keyrotation.completed</c> of that kind); a rotation of the
    /// fingerprint key is not one.
    /// </summary>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The version it introduced and when it completed, or nothing where none has.</returns>
    ValueTask<(int Version, DateTimeOffset CompletedAt)?> KeyEncryptionKeyRotatedAsync(CancellationToken cancellationToken);

    /// <summary>
    /// When bootstrap created the deployment's first organization, as its
    /// <c>identity.organization.created</c> record says.
    /// </summary>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The instant, or nothing where the deployment was never bootstrapped.</returns>
    ValueTask<DateTimeOffset?> BootstrappedAsync(CancellationToken cancellationToken);
}
