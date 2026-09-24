using System;
using Janus.Core;
using Janus.Storage.Authentication.Oidc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace Janus.Hosting.Oidc;

/// <summary>
/// How the OpenID Connect server is put together: the endpoints it answers, the flows
/// it admits, the tables it keeps its own records in, and the few places the library
/// speaks for itself.
/// </summary>
/// <remarks>
/// Implements AUTH-OIDC-001, AUTH-OIDC-002, AUTH-OIDC-003, AUTH-OIDC-004,
/// AUTH-SESS-012, AUTH-KEY-001, API-REDIR-001 and CONV-DESIGN-008. The protocol is the
/// server's: it validates the clients, issues the codes and the tokens, and writes the
/// refusals. What the library adds is what no protocol server can know, which is the
/// session record every token is minted from, and what only this deployment declares,
/// which is the kind of client and the one destination a code returns to. The records
/// the server keeps are in the library's own tables over the one context, and no store
/// package of the server's own is referenced (CONV-DESIGN-008).
/// </remarks>
internal static class OidcRegistration
{
    /// <summary>The route the authorization request arrives at.</summary>
    public const string Authorization = "oidc/authorize";

    /// <summary>The route the authorization request is pushed to beforehand.</summary>
    public const string PushedAuthorization = "oidc/par";

    /// <summary>The route the token request arrives at.</summary>
    public const string Token = "oidc/token";

    /// <summary>The route the userinfo request arrives at.</summary>
    public const string UserInfo = "oidc/userinfo";

    /// <summary>The route the key set is served from.</summary>
    public const string KeySet = "oidc/jwks";

    /// <summary>The route the discovery document is served from.</summary>
    public const string Configuration = ".well-known/openid-configuration";

    /// <summary>How long the reference to a pushed request can be presented.</summary>
    public static readonly TimeSpan PushedRequestLifetime = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Adds the server, the stores it keeps its records in, and the library's own
    /// handlers.
    /// </summary>
    /// <param name="services">The host's services.</param>
    /// <param name="keyEncryptionKeys">
    /// The versions the deployment holds, which the codes and the refresh tokens are
    /// encrypted under.
    /// </param>
    /// <returns>The collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException">The collection is absent.</exception>
    public static IServiceCollection AddOidc(
        this IServiceCollection services,
        KeyEncryptionKeys keyEncryptionKeys)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<SigningCredentialSource>();

        _ = services.AddOpenIddict()
            .AddCore(options =>
            {
                // CONV-DESIGN-008: the four stores are hand-written in Janus.Storage
                // over the one context, so the entities the server keeps its records
                // as are the library's rows.
                _ = options.SetDefaultApplicationEntity<OidcClientRecord>();
                _ = options.SetDefaultAuthorizationEntity<OidcAuthorizationRecord>();
                _ = options.SetDefaultScopeEntity<OidcScopeRecord>();
                _ = options.SetDefaultTokenEntity<OidcTokenRecord>();

                // AUTH-OIDC-001 AC2, CONV-SEC-002: the secret is judged against the
                // fingerprint the registry holds, in constant time.
                _ = options.ReplaceApplicationManager<OidcClientRecord, ClientSecrets>();

                // CONV-DESIGN-003: an entity read here is tracked by the request's own
                // context, so none of them outlives the request that read it.
                _ = options.DisableEntityCaching();
            })
            .AddServer(options =>
            {
                _ = options
                    .SetAuthorizationEndpointUris(Authorization)
                    .SetPushedAuthorizationEndpointUris(PushedAuthorization)
                    .SetTokenEndpointUris(Token)
                    .SetUserInfoEndpointUris(UserInfo)
                    .SetJsonWebKeySetEndpointUris(KeySet)
                    .SetConfigurationEndpointUris(Configuration);

                // AUTH-OIDC-001: the code flow with proof key and the refresh flow, and
                // nothing else. No implicit flow, no password grant, no device flow.
                _ = options.AllowAuthorizationCodeFlow().AllowRefreshTokenFlow();
                _ = options.RequireProofKeyForCodeExchange();

                // AUTH-OIDC-006 AC2: every authorization request is pushed over the
                // back channel first, so the browser carries a reference and none of
                // the parameters, and the reference lapses a minute after it is issued.
                _ = options.RequirePushedAuthorizationRequests();
                _ = options.Configure(server => server.RequestTokenLifetime = PushedRequestLifetime);
                _ = options.RegisterScopes(
                    OpenIddictConstants.Scopes.OpenId,
                    OpenIddictConstants.Scopes.Email,
                    OpenIddictConstants.Scopes.Profile,
                    OpenIddictConstants.Scopes.OfflineAccess);

                // AUTH-OIDC-004: a relying party validates the access token offline
                // against the published key set, which it cannot do if it is encrypted.
                _ = options.DisableAccessTokenEncryption();

                // AUTH-KEY-002: the codes and the refresh tokens are encrypted under a
                // key derived from the deployment's own key-encryption key, so every
                // instance reads what any other wrote and a restart loses nothing.
                foreach (SymmetricSecurityKey key in TokenProtection.Keys(keyEncryptionKeys))
                {
                    _ = options.AddEncryptionKey(key);
                }

                _ = options.AddEventHandler<OpenIddictServerEvents.ValidatePushedAuthorizationRequestContext>(
                    handler => handler
                        .UseScopedHandler<RegisteredDestination>()
                        .SetOrder(RegisteredDestination.Order));
                _ = options.AddEventHandler<OpenIddictServerEvents.HandleAuthorizationRequestContext>(
                    handler => handler.UseScopedHandler<AuthorizationIssue>());
                _ = options.AddEventHandler<OpenIddictServerEvents.ApplyAuthorizationResponseContext>(
                    handler => handler.UseScopedHandler<PushedRequestSpent>());
                _ = options.AddEventHandler<OpenIddictServerEvents.HandleTokenRequestContext>(
                    handler => handler.UseScopedHandler<TokenIssue>());
                _ = options.AddEventHandler<OpenIddictServerEvents.HandleUserInfoRequestContext>(
                    handler => handler.UseScopedHandler<ClaimsAnswer>());
                _ = options.AddEventHandler<OpenIddictServerEvents.HandleJsonWebKeySetRequestContext>(
                    handler => handler.UseScopedHandler<KeySetAnswer>());
                _ = options.AddEventHandler<OpenIddictServerEvents.GenerateTokenContext>(
                    handler => handler.UseScopedHandler<TokenSigning>().SetOrder(TokenSigning.Order));
                _ = options.AddEventHandler<OpenIddictServerEvents.ValidateTokenContext>(
                    handler => handler
                        .UseScopedHandler<TokenValidationKeys>()
                        .SetOrder(TokenValidationKeys.Order));
                _ = options.AddEventHandler<OpenIddictServerEvents.ValidateTokenContext>(
                    handler => handler.UseScopedHandler<TokenReuse>().SetOrder(TokenReuse.Order));

                _ = options.UseAspNetCore();
            });

        // AUTH-KEY-001: the server is put together with the key the store held at
        // startup, and every token afterwards is signed with the key the store holds
        // when the request arrives, so a rotation needs no restart.
        _ = services.AddOptions<OpenIddictServerOptions>()
            .Configure<SigningCredentialSource>(
                (options, source) => options.SigningCredentials.Add(source.Current));

        return services;
    }
}
