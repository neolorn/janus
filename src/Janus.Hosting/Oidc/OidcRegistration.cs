using System;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace Janus.Hosting.Oidc;

/// <summary>
/// How the OpenID Connect server is put together: the endpoints it answers, the flows
/// it admits, and the library's own handlers in place of a store of the server's.
/// </summary>
/// <remarks>
/// Implements AUTH-OIDC-001, AUTH-OIDC-002, AUTH-OIDC-004, AUTH-SESS-012 and
/// CONV-DESIGN-008. The protocol, the request shapes and the signatures are the
/// server's; the clients, the codes, the tokens and the keys are the library's, held
/// in its own tables over the one context.
/// </remarks>
internal static class OidcRegistration
{
    /// <summary>The route the authorization request arrives at.</summary>
    public const string Authorization = "oidc/authorize";

    /// <summary>The route the token request arrives at.</summary>
    public const string Token = "oidc/token";

    /// <summary>The route the userinfo request arrives at.</summary>
    public const string UserInfo = "oidc/userinfo";

    /// <summary>The route the key set is served from.</summary>
    public const string KeySet = "oidc/jwks";

    /// <summary>The route the discovery document is served from.</summary>
    public const string Configuration = ".well-known/openid-configuration";

    /// <summary>
    /// Adds the server and the handlers that stand in for its stores.
    /// </summary>
    /// <param name="services">The host's services.</param>
    /// <returns>The collection, for chaining.</returns>
    public static IServiceCollection AddOidc(this IServiceCollection services)
    {
        _ = services.AddOpenIddict().AddServer(options =>
        {
            _ = options
                .SetAuthorizationEndpointUris(Authorization)
                .SetTokenEndpointUris(Token)
                .SetUserInfoEndpointUris(UserInfo)
                .SetJsonWebKeySetEndpointUris(KeySet)
                .SetConfigurationEndpointUris(Configuration);

            // AUTH-OIDC-001: the code flow with proof key and the refresh flow, and
            // nothing else. No implicit flow, no password grant, no device flow.
            _ = options.AllowAuthorizationCodeFlow().AllowRefreshTokenFlow();
            _ = options.RequireProofKeyForCodeExchange();
            _ = options.RegisterScopes(
                OpenIddictConstants.Scopes.OpenId,
                OpenIddictConstants.Scopes.Email,
                OpenIddictConstants.Scopes.Profile,
                OpenIddictConstants.Scopes.OfflineAccess);

            // AUTH-OIDC-004: a relying party validates the access token offline
            // against the published key set, which it cannot do if it is encrypted.
            _ = options.DisableAccessTokenEncryption();

            // CONV-DESIGN-008: no store of the server's own. Every question it would
            // have asked one is answered by a handler below, against the library's
            // tables.
            _ = options.EnableDegradedMode();

            // AUTH-KEY-001: the server will not start without a key of its own, and
            // it protects nothing with these: every token the deployment issues is
            // signed with the key read for that request, the code and the refresh
            // token are the library's own opaque values, and the published set is the
            // deployment's. Neither key leaves the process or is written anywhere.
            _ = options.AddEphemeralEncryptionKey();
            _ = options.AddEphemeralSigningKey();

            _ = options.AddEventHandler<OpenIddictServerEvents.ValidateAuthorizationRequestContext>(
                handler => handler.UseScopedHandler<AuthorizationValidation>());
            _ = options.AddEventHandler<OpenIddictServerEvents.HandleAuthorizationRequestContext>(
                handler => handler.UseScopedHandler<AuthorizationIssue>());
            _ = options.AddEventHandler<OpenIddictServerEvents.ValidateTokenRequestContext>(
                handler => handler.UseScopedHandler<ClientAuthentication>());
            _ = options.AddEventHandler<OpenIddictServerEvents.HandleTokenRequestContext>(
                handler => handler.UseScopedHandler<TokenIssue>());
            _ = options.AddEventHandler<OpenIddictServerEvents.HandleUserInfoRequestContext>(
                handler => handler.UseScopedHandler<ClaimsAnswer>());
            _ = options.AddEventHandler<OpenIddictServerEvents.HandleJsonWebKeySetRequestContext>(
                handler => handler.UseScopedHandler<KeySetAnswer>());
            _ = options.AddEventHandler<OpenIddictServerEvents.GenerateTokenContext>(
                handler => handler.UseScopedHandler<TokenFormat>().SetOrder(TokenFormat.Order));
            _ = options.AddEventHandler<OpenIddictServerEvents.ValidateTokenContext>(
                handler => handler.UseScopedHandler<TokenRecognition>().SetOrder(TokenRecognition.Order));

            _ = options.UseAspNetCore();
        });

        services.TryAddOidcAddresses();

        return services;
    }

    private static void TryAddOidcAddresses(this IServiceCollection services)
    {
        foreach (ServiceDescriptor descriptor in services)
        {
            if (descriptor.ServiceType == typeof(AuthenticationAddresses))
            {
                return;
            }
        }

        services.AddSingleton(AuthenticationAddresses.None);
    }
}
