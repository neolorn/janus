using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Oidc;

/// <summary>
/// How a client comes to be in the provider's registry: registered by the deployment,
/// from the server, and changed the same way.
/// </summary>
/// <param name="clients">The registry.</param>
/// <param name="audit">Where a registration is recorded.</param>
/// <param name="work">The transaction the registration and its record commit in.</param>
/// <param name="randomness">What a new client's secret is drawn from.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements AUTH-OIDC-001, API-REDIR-001, OPS-SEC-001 and OPS-SEC-002, as D-166 (340)
/// settles them. No person chooses a secret: a first registration draws one and holds
/// it wrapped, and registering a client the registry holds changes its name, kind,
/// destination and scopes and leaves its secret as it stands. The secret rotates
/// without a person (<see cref="RegisteredSecrets"/>).
/// </remarks>
internal sealed class ClientRegistry(
    IOidcClientStore clients,
    IOidcAudit audit,
    IUnitOfWork work,
    RandomNumberGenerator randomness,
    TimeProvider time)
{
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
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Nothing, or the failure naming the member that is not one a client can be
    /// registered with.
    /// </returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public async ValueTask<Result> RegisterAsync(OidcClient client, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);

        if (Unregistrable(client) is string member)
        {
            return Result.Failure(Error.From(
                ErrorCodes.RequestMalformed,
                "member",
                JsonSerializer.SerializeToElement(member)));
        }

        DateTimeOffset now = time.GetUtcNow();

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        bool changed = await clients.FindAsync(client.ClientId, cancellationToken).ConfigureAwait(false) is not null;

        if (changed)
        {
            await clients.RecordAsync(client, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            byte[] secret = RegisteredSecrets.Draw(randomness);

            try
            {
                await clients.AddAsync(client, secret, now, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(secret);
            }
        }

        await audit
            .RegisteredAsync(Principal, client.ClientId, client.Kind, changed, now, cancellationToken)
            .ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

        return Result.Success();
    }

    // The member a client cannot be registered with, or nothing where each can be.
    private static string? Unregistrable(OidcClient client) =>
        Blank(client.ClientId) ? "client"
        : string.IsNullOrWhiteSpace(client.Name) ? "name"
        : !RedirectValidation.Origin(client.Redirect) ? "redirect"
        : client.Scopes.Count is 0 || client.Scopes.Any(Blank) ? "scopes"
        : null;

    private static bool Blank(string value) =>
        value.Length is 0 || value.Any(char.IsWhiteSpace);
}
