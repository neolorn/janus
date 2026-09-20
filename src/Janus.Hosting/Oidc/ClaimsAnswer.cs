using System;
using System.Threading.Tasks;
using Janus.Core;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace Janus.Hosting.Oidc;

/// <summary>
/// What the userinfo endpoint says about the person a token stands for.
/// </summary>
/// <param name="oidc">Where the claims are read.</param>
/// <remarks>
/// Implements AUTH-OIDC-001 and IDN-ATTR-005. What each scope carries is fixed: no
/// telephone number, legal name, date of birth or photograph is issued here whatever
/// was asked for.
/// </remarks>
internal sealed class ClaimsAnswer(IOidc oidc)
    : IOpenIddictServerHandler<OpenIddictServerEvents.HandleUserInfoRequestContext>
{
    /// <inheritdoc/>
    public async ValueTask HandleAsync(OpenIddictServerEvents.HandleUserInfoRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.AccessTokenPrincipal?.GetClaim(OpenIddictConstants.Claims.Subject) is not string held
            || !Guid.TryParse(held, out Guid value))
        {
            context.Reject(OpenIddictConstants.Errors.InvalidToken, description: null, uri: null);

            return;
        }

        Error? failure = null;

        OidcClaims claims = (await oidc
                .ClaimsAsync(
                    new SubjectId(value),
                    string.Join(' ', context.AccessTokenPrincipal.GetScopes()),
                    context.CancellationToken)
                .ConfigureAwait(false))
            .Match(answer => answer, error => Withheld(error, ref failure));

        if (failure is not null)
        {
            context.Reject(OpenIddictConstants.Errors.InvalidToken, description: null, uri: null);

            return;
        }

        context.Subject = claims.Subject.ToString();
        context.Email = claims.Email;
        context.EmailVerified = claims.EmailVerified;
        context.PreferredUsername = claims.PreferredUsername;

        if (claims.Name is string name)
        {
            context.Claims[OpenIddictConstants.Claims.Name] = name;
        }

        if (claims.Locale is string locale)
        {
            context.Claims[OpenIddictConstants.Claims.Locale] = locale;
        }
    }

    private static OidcClaims Withheld(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }
}
