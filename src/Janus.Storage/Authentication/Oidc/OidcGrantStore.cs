using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;

namespace Janus.Storage.Authentication.Oidc;

/// <summary>
/// The provider's grants and the tokens issued under them, revoked over the
/// <c>oidc_authorizations</c> and <c>oidc_tokens</c> tables.
/// </summary>
/// <param name="context">The context whose transaction the statements run in.</param>
/// <remarks>
/// Implements AUTH-OIDC-003 and CONV-DESIGN-003 (D-207). Each revocation is one update
/// of the rows not yet revoked, run in the unit of work the caller began, so a fault or
/// a failed commit leaves every row as it stood.
/// </remarks>
internal sealed class OidcGrantStore(StoreContext context) : IOidcGrantStore
{
    /// <inheritdoc/>
    public async ValueTask RevokeAsync(string grant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(grant);

        if (!Guid.TryParse(grant, out Guid issued))
        {
            return;
        }

        _ = await context.OidcAuthorizations
            .Where(authorization =>
                authorization.Id == issued
                && authorization.Status != OpenIddictConstants.Statuses.Revoked)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    authorization => authorization.Status,
                    OpenIddictConstants.Statuses.Revoked),
                cancellationToken)
            .ConfigureAwait(false);

        _ = await context.OidcTokens
            .Where(token =>
                token.AuthorizationId == issued
                && token.Status != OpenIddictConstants.Statuses.Revoked)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    token => token.Status,
                    OpenIddictConstants.Statuses.Revoked),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
