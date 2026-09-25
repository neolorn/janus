using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace Janus.Hosting.Oidc;

/// <summary>
/// The pushed request an authorization request was answered for, which is spent
/// whatever the answer was.
/// </summary>
/// <param name="tokens">Where the row the reference stands for is read and spent.</param>
/// <remarks>
/// Implements AUTH-OIDC-006 AC2. The server spends the reference when it issues a code
/// against it; a refusal, and a browser forwarded to sign in, spend it here, so a
/// reference is presented once whatever it was answered with and a copy of it carries
/// nothing afterwards.
/// </remarks>
internal sealed class PushedRequestSpent(IOpenIddictTokenManager tokens)
    : IOpenIddictServerHandler<OpenIddictServerEvents.ApplyAuthorizationResponseContext>
{
    /// <inheritdoc/>
    public ValueTask HandleAsync(OpenIddictServerEvents.ApplyAuthorizationResponseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return SpendAsync(context.Transaction, context.CancellationToken);
    }

    /// <summary>
    /// Spends the pushed request the authorization request was read from, where it was
    /// read from one and the row is still held.
    /// </summary>
    /// <param name="transaction">The authorization request being answered.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of spending it.</returns>
    /// <exception cref="ArgumentNullException">The transaction is absent.</exception>
    public async ValueTask SpendAsync(
        OpenIddictServerTransaction transaction,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        if (transaction.GetProperty<OpenIddictServerEvents.ProcessAuthenticationContext>(
                typeof(OpenIddictServerEvents.ProcessAuthenticationContext).FullName!)
                is not { RequestTokenPrincipal: ClaimsPrincipal pushed }
            || pushed.GetTokenId() is not string identifier
            || await tokens.FindByIdAsync(identifier, cancellationToken).ConfigureAwait(false)
                is not object held)
        {
            return;
        }

        // A reference a code was issued against is already spent, which is the one
        // outcome where this changes nothing.
        _ = await tokens.TryRedeemAsync(held, cancellationToken).ConfigureAwait(false);
    }
}
