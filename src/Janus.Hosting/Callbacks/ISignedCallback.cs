using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Hosting.Callbacks;

/// <summary>
/// One of the host's callbacks whose provider signs what it sends with a shared secret.
/// The host states where the provider's scheme puts the signature and what it signs;
/// the machine profile computes the signature and applies the scheme.
/// </summary>
/// <remarks>
/// Implements BFF-MACH-002 and INT-GEN-003. The host's route mounted for it is reached
/// only by a request within the rate limit and the published ranges, whose signature
/// holds under a live secret, inside the five-minute window where the scheme carries an
/// instant, and whose event has not already been carried. A failure any method returns
/// refuses the delivery.
/// </remarks>
public interface ISignedCallback
{
    /// <summary>
    /// What the callback is counted, claimed and recorded under; unique among the
    /// host's callbacks.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The address ranges the provider publishes for its callbacks; empty where it
    /// publishes none.
    /// </summary>
    IReadOnlyCollection<IPNetwork> Sources { get; }

    /// <summary>
    /// The hash of the keyed-hash message authentication code the provider publishes
    /// its scheme under.
    /// </summary>
    HashAlgorithmName Algorithm { get; }

    /// <summary>
    /// Reads the secrets from the secrets manager.
    /// </summary>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The current secret and the one it replaced.</returns>
    ValueTask<Result<CallbackSecrets>> ReadSecretsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Reads what the delivery presents under the provider's scheme, from its headers
    /// and its raw bytes, without parsing the body.
    /// </summary>
    /// <param name="delivery">The delivery.</param>
    /// <returns>What it presents, or a failure where it carries no signature.</returns>
    Result<CallbackSignature> Presented(CallbackDelivery delivery);

    /// <summary>
    /// Reads the provider's identifier of the event from a delivery whose signature
    /// held.
    /// </summary>
    /// <param name="delivery">The verified delivery.</param>
    /// <returns>
    /// The identifier a repeated delivery carries again, or a failure where it carries
    /// none.
    /// </returns>
    Result<string> EventOf(CallbackDelivery delivery);
}
