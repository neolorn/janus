using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;

namespace Janus.Storage.Authentication.Oidc;

/// <summary>
/// The registered clients, as the protocol server reads them.
/// </summary>
/// <param name="context">The context the operation's reads are taken on.</param>
/// <remarks>
/// Implements AUTH-OIDC-001, AUTH-OIDC-002, API-REDIR-001 and CONV-DESIGN-003. There is
/// no registration endpoint and no self-service: a row arrives in the table because the
/// deployment put it there, so every write through this interface is refused rather
/// than silently kept in memory. What the protocol asks about a client, the row
/// answers: one exact destination, the proof key, and the grants its kind admits.
/// </remarks>
internal sealed class OidcApplicationStore(JanusDbContext context)
    : IOpenIddictApplicationStore<OidcClientRecord>
{
    /// <inheritdoc/>
    public async ValueTask<long> CountAsync(CancellationToken cancellationToken) =>
        await context.OidcClients.LongCountAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<long> CountAsync<TResult>(
        Func<IQueryable<OidcClientRecord>, IQueryable<TResult>> query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return await query(context.OidcClients).LongCountAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public ValueTask CreateAsync(OidcClientRecord application, CancellationToken cancellationToken) =>
        throw Unwritten();

    /// <inheritdoc/>
    public ValueTask DeleteAsync(OidcClientRecord application, CancellationToken cancellationToken) =>
        throw Unwritten();

    /// <inheritdoc/>
    public async ValueTask<OidcClientRecord?> FindByClientIdAsync(
        string identifier,
        CancellationToken cancellationToken) =>
        await context.OidcClients
            .FirstOrDefaultAsync(client => client.ClientId == identifier, cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public ValueTask<OidcClientRecord?> FindByIdAsync(
        string identifier,
        CancellationToken cancellationToken) =>
        FindByClientIdAsync(identifier, cancellationToken);

    /// <inheritdoc/>
    public IAsyncEnumerable<OidcClientRecord> FindByPostLogoutRedirectUriAsync(
        string uri,
        CancellationToken cancellationToken) =>
        AsyncEnumerable.Empty<OidcClientRecord>();

    /// <inheritdoc/>
    public IAsyncEnumerable<OidcClientRecord> FindByRedirectUriAsync(
        string uri,
        CancellationToken cancellationToken) =>
        context.OidcClients.Where(client => client.Redirect == uri).AsAsyncEnumerable();

    /// <inheritdoc/>
    public ValueTask<string?> GetApplicationTypeAsync(
        OidcClientRecord application,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<string?>(OpenIddictConstants.ApplicationTypes.Web);

    /// <inheritdoc/>
    public async ValueTask<TResult?> GetAsync<TState, TResult>(
        Func<IQueryable<OidcClientRecord>, TState, IQueryable<TResult>> query,
        TState state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return await query(context.OidcClients, state)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public ValueTask<string?> GetClientIdAsync(
        OidcClientRecord application,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(application);

        return ValueTask.FromResult<string?>(application.ClientId);
    }

    /// <inheritdoc/>
    public ValueTask<string?> GetClientSecretAsync(
        OidcClientRecord application,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(application);

        // What the row holds is what the secret hashes to and never the secret, so what
        // leaves here authenticates nothing on its own.
        return ValueTask.FromResult<string?>(
            application.Secret is { Length: > 0 } fingerprint
                ? Convert.ToBase64String(fingerprint)
                : null);
    }

    /// <inheritdoc/>
    public ValueTask<string?> GetClientTypeAsync(
        OidcClientRecord application,
        CancellationToken cancellationToken) =>
        // 09 section 9: both kinds hold a secret. A browser application's own layer is
        // a confidential client of the provider exactly as a protocol client is.
        ValueTask.FromResult<string?>(OpenIddictConstants.ClientTypes.Confidential);

    /// <inheritdoc/>
    public ValueTask<string?> GetConsentTypeAsync(
        OidcClientRecord application,
        CancellationToken cancellationToken) =>
        // AUTH-OIDC-001: consent screens are out of scope, and a first-party client
        // registered by hand is one the deployment has already decided about.
        ValueTask.FromResult<string?>(OpenIddictConstants.ConsentTypes.Implicit);

    /// <inheritdoc/>
    public ValueTask<string?> GetDisplayNameAsync(
        OidcClientRecord application,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(application);

        return ValueTask.FromResult<string?>(application.Name);
    }

    /// <inheritdoc/>
    public ValueTask<ImmutableDictionary<CultureInfo, string>> GetDisplayNamesAsync(
        OidcClientRecord application,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(ImmutableDictionary<CultureInfo, string>.Empty);

    /// <inheritdoc/>
    public ValueTask<string?> GetIdAsync(
        OidcClientRecord application,
        CancellationToken cancellationToken) =>
        GetClientIdAsync(application, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<JsonWebKeySet?> GetJsonWebKeySetAsync(
        OidcClientRecord application,
        CancellationToken cancellationToken) =>
        // AUTH-OIDC-001: a client authenticates with the secret the registry holds, so
        // no client key set is registered and none is read.
        ValueTask.FromResult<JsonWebKeySet?>(null);

    /// <inheritdoc/>
    public ValueTask<ImmutableArray<string>> GetPermissionsAsync(
        OidcClientRecord application,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(application);

        ImmutableArray<string>.Builder permitted = ImmutableArray.CreateBuilder<string>();

        permitted.Add(OpenIddictConstants.Permissions.Endpoints.Authorization);
        permitted.Add(OpenIddictConstants.Permissions.Endpoints.Token);
        permitted.Add(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
        permitted.Add(OpenIddictConstants.Permissions.ResponseTypes.Code);

        // AUTH-OIDC-002 AC2, 09 section 9: a browser application's own layer exchanges
        // one code and holds nothing afterwards, so the grant that would refresh a
        // token is not one it has.
        if (application.Kind is OidcClientKind.Protocol)
        {
            permitted.Add(OpenIddictConstants.Permissions.GrantTypes.RefreshToken);
        }

        foreach (string scope in application.Scopes)
        {
            permitted.Add(OpenIddictConstants.Permissions.Prefixes.Scope + scope);
        }

        return ValueTask.FromResult(permitted.ToImmutable());
    }

    /// <inheritdoc/>
    public ValueTask<ImmutableArray<string>> GetPostLogoutRedirectUrisAsync(
        OidcClientRecord application,
        CancellationToken cancellationToken) =>
        // BFF-SESS-005: a sign-out ends the sessions at the library and forwards
        // nothing, so there is no end-session destination to register.
        ValueTask.FromResult(ImmutableArray<string>.Empty);

    /// <inheritdoc/>
    public ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(
        OidcClientRecord application,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(ImmutableDictionary<string, JsonElement>.Empty);

    /// <inheritdoc/>
    public ValueTask<ImmutableArray<string>> GetRedirectUrisAsync(
        OidcClientRecord application,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(application);

        // API-REDIR-001: one exact destination, matched as one string and never as a
        // prefix or a pattern.
        return ValueTask.FromResult(ImmutableArray.Create(application.Redirect));
    }

    /// <inheritdoc/>
    public ValueTask<ImmutableArray<string>> GetRequirementsAsync(
        OidcClientRecord application,
        CancellationToken cancellationToken) =>
        // AUTH-SESS-012 AC4: every client proves the verifier, so the requirement is
        // the client's own and not only the server's.
        ValueTask.FromResult(
            ImmutableArray.Create(
                OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange));

    /// <inheritdoc/>
    public ValueTask<ImmutableDictionary<string, string>> GetSettingsAsync(
        OidcClientRecord application,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(ImmutableDictionary<string, string>.Empty);

    /// <inheritdoc/>
    public ValueTask<OidcClientRecord> InstantiateAsync(CancellationToken cancellationToken) =>
        throw Unwritten();

    /// <inheritdoc/>
    public IAsyncEnumerable<OidcClientRecord> ListAsync(
        int? count,
        int? offset,
        CancellationToken cancellationToken)
    {
        IQueryable<OidcClientRecord> listed = context.OidcClients.OrderBy(client => client.ClientId);

        if (offset is int skipped)
        {
            listed = listed.Skip(skipped);
        }

        if (count is int taken)
        {
            listed = listed.Take(taken);
        }

        return listed.AsAsyncEnumerable();
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<TResult> ListAsync<TState, TResult>(
        Func<IQueryable<OidcClientRecord>, TState, IQueryable<TResult>> query,
        TState state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return query(context.OidcClients, state).AsAsyncEnumerable();
    }

    /// <inheritdoc/>
    public ValueTask SetApplicationTypeAsync(
        OidcClientRecord application,
        string? type,
        CancellationToken cancellationToken) =>
        throw Unwritten();

    /// <inheritdoc/>
    public ValueTask SetClientIdAsync(
        OidcClientRecord application,
        string? identifier,
        CancellationToken cancellationToken) =>
        throw Unwritten();

    /// <inheritdoc/>
    public ValueTask SetClientSecretAsync(
        OidcClientRecord application,
        string? secret,
        CancellationToken cancellationToken) =>
        throw Unwritten();

    /// <inheritdoc/>
    public ValueTask SetClientTypeAsync(
        OidcClientRecord application,
        string? type,
        CancellationToken cancellationToken) =>
        throw Unwritten();

    /// <inheritdoc/>
    public ValueTask SetConsentTypeAsync(
        OidcClientRecord application,
        string? type,
        CancellationToken cancellationToken) =>
        throw Unwritten();

    /// <inheritdoc/>
    public ValueTask SetDisplayNameAsync(
        OidcClientRecord application,
        string? name,
        CancellationToken cancellationToken) =>
        throw Unwritten();

    /// <inheritdoc/>
    public ValueTask SetDisplayNamesAsync(
        OidcClientRecord application,
        ImmutableDictionary<CultureInfo, string> names,
        CancellationToken cancellationToken) =>
        throw Unwritten();

    /// <inheritdoc/>
    public ValueTask SetJsonWebKeySetAsync(
        OidcClientRecord application,
        JsonWebKeySet? set,
        CancellationToken cancellationToken) =>
        throw Unwritten();

    /// <inheritdoc/>
    public ValueTask SetPermissionsAsync(
        OidcClientRecord application,
        ImmutableArray<string> permissions,
        CancellationToken cancellationToken) =>
        throw Unwritten();

    /// <inheritdoc/>
    public ValueTask SetPostLogoutRedirectUrisAsync(
        OidcClientRecord application,
        ImmutableArray<string> uris,
        CancellationToken cancellationToken) =>
        throw Unwritten();

    /// <inheritdoc/>
    public ValueTask SetPropertiesAsync(
        OidcClientRecord application,
        ImmutableDictionary<string, JsonElement> properties,
        CancellationToken cancellationToken) =>
        throw Unwritten();

    /// <inheritdoc/>
    public ValueTask SetRedirectUrisAsync(
        OidcClientRecord application,
        ImmutableArray<string> uris,
        CancellationToken cancellationToken) =>
        throw Unwritten();

    /// <inheritdoc/>
    public ValueTask SetRequirementsAsync(
        OidcClientRecord application,
        ImmutableArray<string> requirements,
        CancellationToken cancellationToken) =>
        throw Unwritten();

    /// <inheritdoc/>
    public ValueTask SetSettingsAsync(
        OidcClientRecord application,
        ImmutableDictionary<string, string> settings,
        CancellationToken cancellationToken) =>
        throw Unwritten();

    /// <inheritdoc/>
    public ValueTask UpdateAsync(OidcClientRecord application, CancellationToken cancellationToken) =>
        throw Unwritten();

    private static NotSupportedException Unwritten() =>
        new("The client registry is the deployment's and no request writes it.");
}
