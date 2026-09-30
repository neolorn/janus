using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Core;
using Janus.Hosting.Credentials;
using Microsoft.Extensions.Hosting;

namespace Janus.Hosting;

/// <summary>
/// Reads the secrets the deployment needs through the host's secret source into the key
/// ring before the web server starts, and clears the ring once everything after it has
/// stopped.
/// </summary>
/// <param name="ring">The key ring it fills and clears.</param>
/// <param name="providers">The social providers the deployment declares.</param>
/// <param name="source">The host's secret source, or nothing where it registered none.</param>
/// <remarks>
/// Implements CONV-DESIGN-007, CONV-CODE-007, IDN-LIFE-012 and D-171. It stands at the
/// head of the hosted services, so it starts before every other and stops after every
/// other, the background worker and the web server among them. A credential is judged
/// usable here, where the deployment can still be stopped, and not at the first exchange
/// that would present it.
/// </remarks>
internal sealed class KeyRingService(
    KeyRing ring,
    IEnumerable<SocialProvider> providers,
    ISecretSource? source = null) : IHostedService
{
    /// <inheritdoc/>
    /// <exception cref="StartupException">A secret cannot be read, or is unusable.</exception>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // LIB-HOST-001: a declaration that is not a social provider, or one declared
        // twice, is refused by name after this; the credential read is the one of each
        // social provider declared.
        foreach (Factor provider in providers
            .Select(declared => declared.Provider)
            .Where(provider => FactorCatalogue.Of(provider).AssuranceLevel is AssuranceLevel.Delegated)
            .Distinct())
        {
            string name = ProviderRoutes.NameOf(provider);
            Error unavailable = KeyRing.Unavailable(KeyRing.Named(name));
            Result<ProviderCredential> read = source is null
                ? Result.Failure<ProviderCredential>(unavailable)
                : await source.ReadProviderCredentialAsync(name, cancellationToken).ConfigureAwait(false);

            ProviderCredential credential = read.Match(
                answered => Usable(answered, unavailable).Match(() => answered, error => Refused(error)),
                _ => Refused(unavailable));

            ring.Hold(name, credential);
        }

        ring.Fill();
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        ring.Clear();

        return Task.CompletedTask;
    }

    private static ProviderCredential Refused(Error failure) =>
        throw new StartupException(
            "A social provider's credential cannot be read from the secret source, or is not one the library can present.",
            failure);

    // IDN-LIFE-012: a static secret is presented as it is, so an empty one is none; a
    // signing credential names who signs and with which key, and the key is the P-256
    // private key the client secret is signed with.
    private static Result Usable(ProviderCredential credential, Error unavailable)
    {
        if (credential.Material.IsEmpty)
        {
            return Result.Failure(unavailable);
        }

        if (!credential.IsSigned)
        {
            return Result.Success();
        }

        if (string.IsNullOrWhiteSpace(credential.Issuer) || string.IsNullOrWhiteSpace(credential.KeyId))
        {
            return Result.Failure(unavailable);
        }

        using var key = ECDsa.Create();

        try
        {
            key.ImportPkcs8PrivateKey(credential.Material.Span, out int read);

            return read == credential.Material.Length
                && key.ExportParameters(includePrivateParameters: false).Curve.Oid.Value
                    == ECCurve.NamedCurves.nistP256.Oid.Value
                ? Result.Success()
                : Result.Failure(unavailable);
        }
        catch (CryptographicException)
        {
            return Result.Failure(unavailable);
        }
    }
}
