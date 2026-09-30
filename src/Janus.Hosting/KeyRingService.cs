using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Credentials;
using Janus.Hosting.Mailboxes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Janus.Hosting;

/// <summary>
/// Reads the secrets the deployment needs through the host's secret source into the key
/// ring, and chooses the mail server in use, before the web server starts; clears the
/// ring once everything has stopped.
/// </summary>
/// <param name="ring">The key ring it fills and clears.</param>
/// <param name="inUse">The mail server in use, which it chooses.</param>
/// <param name="adapter">The library's mail-server adapter, chosen where the host registered no mail server and the endpoint is set.</param>
/// <param name="scopes">Where the scope the endpoint is read in comes from.</param>
/// <param name="providers">The social providers the deployment declares.</param>
/// <param name="host">The host's own mail server, or nothing where it registered none.</param>
/// <param name="source">The host's secret source, or nothing where it registered none.</param>
/// <remarks>
/// Implements CONV-DESIGN-007, CONV-CODE-007, IDN-LIFE-012, LIB-HOST-001, D-171, D-176
/// and D-180. Every secret is read through the host's secret source, so a start whose
/// host declared none is refused by the declaration's name before any secret is read.
/// The start fills the ring in steps: every secret but the mail server's as the start begins, ahead
/// of every hosted service; then, in its own place among them, once the settings table
/// is readable, the choice of the mail server in use and, where the adapter is chosen,
/// the mail server's key. It
/// clears the ring once every hosted service has stopped, the background worker and the
/// web server among them. A credential is judged usable here, where the deployment can
/// still be stopped, and not at the first exchange that would present it. The host's mail
/// server, where it registered one, is the one in use for the life of the process.
/// </remarks>
internal sealed class KeyRingService(
    KeyRing ring,
    MailServerInUse inUse,
    JmapMailServer adapter,
    IServiceScopeFactory scopes,
    IEnumerable<SocialProvider> providers,
    IMailServer? host,
    ISecretSource? source) : IHostedLifecycleService
{
    // LIB-HOST-001, D-180: the secret source spelled as every other declaration is.
    private const string SecretSource = "secretSource";

    /// <inheritdoc/>
    /// <exception cref="StartupException">
    /// The host declared no secret source, or a secret cannot be read, or is unusable.
    /// </exception>
    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        ISecretSource declared = Declared();

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
            Result<ProviderCredential> read = await declared
                .ReadProviderCredentialAsync(name, cancellationToken)
                .ConfigureAwait(false);

            ProviderCredential credential = read.Match(
                answered => Usable(answered, unavailable).Match(() => answered, error => Refused(error)),
                _ => Refused(unavailable));

            ring.Hold(name, credential);
        }

        ring.Fill();
    }

    /// <inheritdoc/>
    /// <exception cref="StartupException">
    /// The adapter is chosen and the mail server's key cannot be read, or its endpoint is
    /// not an absolute https address.
    /// </exception>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // CONV-DESIGN-007 AC5: the host's mail server where it registered one, the
        // adapter's key then not read.
        if (host is not null)
        {
            ring.Completed();
            inUse.Choose(host);

            return;
        }

        string endpoint;

        await using (AsyncServiceScope scope = scopes.CreateAsyncScope())
        {
            endpoint = (await scope.ServiceProvider
                    .GetRequiredService<IConfigurationStore>()
                    .ReadAsync(Settings.IntegrationMailServerEndpoint, cancellationToken)
                    .ConfigureAwait(false))
                .Match(value => value, error => throw new InvalidOperationException(error.Code.ToString()));
        }

        if (endpoint.Length is 0)
        {
            ring.Completed();
            inUse.Choose(server: null);

            return;
        }

        // INT-GEN-001: the sending check refused a plaintext endpoint before this; one
        // written since is refused here all the same.
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? reached)
            || !string.Equals(reached.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
        {
            throw new StartupException(
                "The mail server's endpoint is not an absolute https address.",
                Error.From(
                    ErrorCodes.EndpointInsecure,
                    "key",
                    JsonSerializer.SerializeToElement(Settings.IntegrationMailServerEndpoint.Key.ToString())));
        }

        // LIB-HOST-001: the adapter's key, read where the adapter is chosen and nowhere
        // else; one the source cannot answer, or answers empty, stops the start.
        Error unavailable = KeyRing.Unavailable(KeyRing.MailServerSecret);
        Result<ReadOnlyMemory<byte>> read = await Declared()
            .ReadMailServerSecretAsync(cancellationToken)
            .ConfigureAwait(false);

        ring.HoldMailServerSecret(read.Match(
            secret => secret.IsEmpty ? Unread(unavailable) : secret,
            _ => Unread(unavailable)));
        ring.Completed();

        adapter.Reach(reached);
        inUse.Choose(adapter);
    }

    /// <inheritdoc/>
    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task StoppedAsync(CancellationToken cancellationToken)
    {
        ring.Clear();

        return Task.CompletedTask;
    }

    private static ReadOnlyMemory<byte> Unread(Error failure) =>
        throw new StartupException("The mail server's key cannot be read from the secret source.", failure);

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

    private ISecretSource Declared() =>
        source ?? throw new StartupException(
            "The deployment declares no secret source to read its secrets from.",
            Error.From(ErrorCodes.StartupDeclarationMissing, "key", JsonSerializer.SerializeToElement(SecretSource)));
}
