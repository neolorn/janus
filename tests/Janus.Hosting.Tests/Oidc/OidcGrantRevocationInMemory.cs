using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;
using Janus.Storage.Authentication.Oidc;
using OpenIddict.Abstractions;

namespace Janus.Hosting.Tests.Oidc;

/// <summary>
/// The provider's grants and their tokens revoked over the stores held in memory, as
/// the library's own statements revoke them over the tables.
/// </summary>
/// <param name="grants">The grants the clients hold.</param>
/// <param name="tokens">The tokens issued under them.</param>
internal sealed class OidcGrantRevocationInMemory(
    OidcAuthorizationStoreInMemory grants,
    OidcTokenStoreInMemory tokens) : IOidcGrantStore
{
    /// <inheritdoc/>
    public ValueTask RevokeAsync(string grant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(grant);

        if (Guid.TryParse(grant, out Guid issued))
        {
            foreach (OidcAuthorizationRecord authorization in grants.All.Where(each => each.Id == issued))
            {
                authorization.Status = OpenIddictConstants.Statuses.Revoked;
            }

            foreach (OidcTokenRecord token in tokens.All.Where(each => each.AuthorizationId == issued))
            {
                token.Status = OpenIddictConstants.Statuses.Revoked;
            }
        }

        return ValueTask.CompletedTask;
    }
}
