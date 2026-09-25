using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Tests.Oidc;
using Janus.Core;
using Janus.Storage.Authentication.Oidc;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;

namespace Janus.Hosting.Tests.Oidc;

/// <summary>
/// The registered clients as the protocol server reads them, over the registry the
/// deployment's own fake holds.
/// </summary>
/// <param name="clients">Where the deployment put its clients.</param>
internal sealed class OidcApplicationStoreInMemory(OidcClientStoreInMemory clients)
    : IOpenIddictApplicationStore<OidcClientRecord>
{
    private IQueryable<OidcClientRecord> Applications =>
        clients.Registered
            .Select(held => new OidcClientRecord
            {
                ClientId = held.Client.ClientId,
                Name = held.Client.Name,
                Kind = held.Client.Kind,
                Redirect = held.Client.Redirect,
                Secret = held.Secret,
                PreviousSecret = held.Previous,
                PreviousSecretUntil = held.PreviousUntil,
                Scopes = [.. held.Client.Scopes],
            })
            .AsQueryable();

    /// <inheritdoc/>
    public ValueTask<long> CountAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(Applications.LongCount());

    /// <inheritdoc/>
    public ValueTask<long> CountAsync<TResult>(
        Func<IQueryable<OidcClientRecord>, IQueryable<TResult>> query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return ValueTask.FromResult(query(Applications).LongCount());
    }

    /// <inheritdoc/>
    public ValueTask CreateAsync(OidcClientRecord application, CancellationToken cancellationToken) =>
        throw Unwritten();

    /// <inheritdoc/>
    public ValueTask DeleteAsync(OidcClientRecord application, CancellationToken cancellationToken) =>
        throw Unwritten();

    /// <inheritdoc/>
    public ValueTask<OidcClientRecord?> FindByClientIdAsync(
        string identifier,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(
            Applications.FirstOrDefault(client => client.ClientId == identifier));

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
        Applications.Where(client => client.Redirect == uri).ToAsyncEnumerable();

    /// <inheritdoc/>
    public ValueTask<string?> GetApplicationTypeAsync(
        OidcClientRecord application,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<string?>(OpenIddictConstants.ApplicationTypes.Web);

    /// <inheritdoc/>
    public ValueTask<TResult?> GetAsync<TState, TResult>(
        Func<IQueryable<OidcClientRecord>, TState, IQueryable<TResult>> query,
        TState state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return ValueTask.FromResult(query(Applications, state).FirstOrDefault());
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

        return ValueTask.FromResult<string?>(
            application.Secret is { Length: > 0 } fingerprint
                ? Convert.ToBase64String(fingerprint)
                : null);
    }

    /// <inheritdoc/>
    public ValueTask<string?> GetClientTypeAsync(
        OidcClientRecord application,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<string?>(OpenIddictConstants.ClientTypes.Confidential);

    /// <inheritdoc/>
    public ValueTask<string?> GetConsentTypeAsync(
        OidcClientRecord application,
        CancellationToken cancellationToken) =>
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
        ValueTask.FromResult<JsonWebKeySet?>(null);

    /// <inheritdoc/>
    public ValueTask<ImmutableArray<string>> GetPermissionsAsync(
        OidcClientRecord application,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(application);

        ImmutableArray<string>.Builder permitted = ImmutableArray.CreateBuilder<string>();

        permitted.Add(OpenIddictConstants.Permissions.Endpoints.PushedAuthorization);
        permitted.Add(OpenIddictConstants.Permissions.Endpoints.Authorization);
        permitted.Add(OpenIddictConstants.Permissions.Endpoints.Token);
        permitted.Add(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
        permitted.Add(OpenIddictConstants.Permissions.ResponseTypes.Code);

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

        return ValueTask.FromResult(ImmutableArray.Create(application.Redirect));
    }

    /// <inheritdoc/>
    public ValueTask<ImmutableArray<string>> GetRequirementsAsync(
        OidcClientRecord application,
        CancellationToken cancellationToken) =>
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
        IQueryable<OidcClientRecord> listed = Applications.OrderBy(client => client.ClientId);

        if (offset is int skipped)
        {
            listed = listed.Skip(skipped);
        }

        if (count is int taken)
        {
            listed = listed.Take(taken);
        }

        return listed.ToAsyncEnumerable();
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<TResult> ListAsync<TState, TResult>(
        Func<IQueryable<OidcClientRecord>, TState, IQueryable<TResult>> query,
        TState state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return query(Applications, state).ToAsyncEnumerable();
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
