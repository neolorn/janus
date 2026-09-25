using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Unicode;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Oidc;

/// <summary>
/// How a client comes to be in the provider's registry: registered by the deployment,
/// from the server, and changed the same way.
/// </summary>
/// <param name="clients">The registry.</param>
/// <param name="audit">Where a registration is recorded.</param>
/// <param name="configuration">Where the access-token lifetime the overlap follows is read.</param>
/// <param name="work">The transaction the registration and its record commit in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements AUTH-OIDC-001, API-REDIR-001, OPS-SEC-001 and OPS-SEC-002. The registry
/// holds what the secret hashes to and never the secret. A secret that replaces another
/// leaves the one it replaced accepted for the overlap the signing keys keep, the
/// access-token lifetime and five minutes, so an application still presenting it while
/// it takes the new one is not refused; after that only the new one is.
/// </remarks>
internal sealed class ClientRegistry(
    IOidcClientStore clients,
    IOidcAudit audit,
    IConfigurationStore configuration,
    IUnitOfWork work,
    TimeProvider time)
{
    // A secret is held to the length a key of the deployment is: anything shorter is
    // within reach of a search over the registry's hashes.
    private const int ShortestSecret = 32;

    // AUTH-KEY-001: the margin over the access-token lifetime the signing keys keep.
    private static readonly TimeSpan Margin = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The principal a registration from the server is recorded under: the command
    /// cannot know which person at the server runs it.
    /// </summary>
    public static SystemPrincipal Principal { get; } =
        SystemPrincipal.ForDeployment("register-client", "AUTH-OIDC-001", SystemOperation.Configuration);

    /// <summary>
    /// Registers the client, or carries the change to the one the registry holds under
    /// its identifier.
    /// </summary>
    /// <param name="client">The client.</param>
    /// <param name="secret">The secret it presents, as its UTF-8 bytes.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Nothing, or the failure naming the member that is not one a client can be
    /// registered with.
    /// </returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public async ValueTask<Result> RegisterAsync(
        OidcClient client,
        ReadOnlyMemory<byte> secret,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);

        if (Unregistrable(client, secret) is string member)
        {
            return Result.Failure(Error.From(
                ErrorCodes.RequestMalformed,
                "member",
                JsonSerializer.SerializeToElement(member)));
        }

        Error? failure = null;

        TimeSpan lifetime = (await configuration
                .ReadAsync(Settings.OidcAccessTokenLifetime, cancellationToken)
                .ConfigureAwait(false))
            .Match(read => read, error => Withheld<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        DateTimeOffset now = time.GetUtcNow();

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        bool changed = await clients.FindAsync(client.ClientId, cancellationToken).ConfigureAwait(false) is not null;

        await clients
            .RecordAsync(client, SHA256.HashData(secret.Span), now + lifetime + Margin, cancellationToken)
            .ConfigureAwait(false);
        await audit
            .RegisteredAsync(Principal, client.ClientId, client.Kind, changed, now, cancellationToken)
            .ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    // The member a client cannot be registered with, or nothing where each can be.
    private static string? Unregistrable(OidcClient client, ReadOnlyMemory<byte> secret) =>
        Blank(client.ClientId) ? "client"
        : string.IsNullOrWhiteSpace(client.Name) ? "name"
        : !RedirectValidation.Origin(client.Redirect) ? "redirect"
        : client.Scopes.Count is 0 || client.Scopes.Any(Blank) ? "scopes"
        : !Presentable(secret.Span) ? "clientSecret"
        : null;

    private static bool Blank(string value) =>
        value.Length is 0 || value.Any(char.IsWhiteSpace);

    // The secret travels as a form field, so it is text, long enough to be one, and
    // more than white space, which the server never takes as a secret.
    private static bool Presentable(ReadOnlySpan<byte> secret)
    {
        if (secret.Length < ShortestSecret || !Utf8.IsValid(secret))
        {
            return false;
        }

        while (!secret.IsEmpty)
        {
            Rune.DecodeFromUtf8(secret, out Rune rune, out int read);

            if (!Rune.IsWhiteSpace(rune))
            {
                return true;
            }

            secret = secret[read..];
        }

        return false;
    }

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }
}
