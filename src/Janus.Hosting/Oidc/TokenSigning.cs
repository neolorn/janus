using System;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace Janus.Hosting.Oidc;

/// <summary>
/// What the server protects a token with: the encryption credential its options hold,
/// and the signing credential the credential source holds now.
/// </summary>
/// <param name="source">The signing keys.</param>
/// <param name="configuration">Where the cadence and the access-token lifetime are read.</param>
/// <remarks>
/// Implements AUTH-KEY-001 and CONV-CODE-007. It stands in the place of the server's own
/// step, which takes the signing credential from the options and is removed, so no token
/// is signed by a key the set does not hold as current and a rotation needs no restart
/// (AUTH-KEY-001 AC1, AC5). The encryption credential is taken from the options as the
/// server's own step takes it.
/// </remarks>
internal sealed class TokenSigning(SigningCredentialSource source, IConfigurationStore configuration)
    : IOpenIddictServerHandler<OpenIddictServerEvents.GenerateTokenContext>
{
    /// <summary>
    /// Where the handler sits: the place of the server's own step it replaces.
    /// </summary>
    public static int Order => OpenIddictServerHandlers.Protection.AttachSecurityCredentials.Descriptor.Order;

    /// <inheritdoc/>
    public async ValueTask HandleAsync(OpenIddictServerEvents.GenerateTokenContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.SecurityTokenHandler = context.Options.JsonWebTokenHandler;

        context.EncryptionCredentials = context.TokenType switch
        {
            // AUTH-OIDC-004: a relying party validates the access token offline, and an
            // identity token is never encrypted.
            OpenIddictConstants.TokenTypeIdentifiers.AccessToken
                when context.Options.DisableAccessTokenEncryption => null,
            OpenIddictConstants.TokenTypeIdentifiers.IdentityToken => null,
            _ => context.Options.EncryptionCredentials[0],
        };

        Error? failure = null;

        SigningCredentials credentials = (await source
                .SigningAsync(
                    configuration,
                    context.TokenType is OpenIddictConstants.TokenTypeIdentifiers.AccessToken,
                    context.CancellationToken)
                .ConfigureAwait(false))
            .Match(signing => signing, error => Withheld(error, ref failure));

        if (failure is not null)
        {
            throw new InvalidOperationException("The deployment's signing keys could not be read: " + failure.Code);
        }

        context.SigningCredentials = credentials;
    }

    private static SigningCredentials Withheld(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }
}
