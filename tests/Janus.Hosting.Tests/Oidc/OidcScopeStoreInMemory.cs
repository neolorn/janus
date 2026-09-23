using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Storage.Authentication.Oidc;
using OpenIddict.Abstractions;

namespace Janus.Hosting.Tests.Oidc;

/// <summary>
/// The scopes a deployment registered beyond the ones the provider is built with,
/// held in memory.
/// </summary>
internal sealed class OidcScopeStoreInMemory : IOpenIddictScopeStore<OidcScopeRecord>
{
    private readonly List<OidcScopeRecord> _scopes = [];

    /// <summary>
    /// Registers a scope the deployment declared.
    /// </summary>
    /// <param name="name">What a request asks for it by.</param>
    /// <param name="resources">What a token covering it is good for.</param>
    public void Declares(string name, params string[] resources) =>
        _scopes.Add(new OidcScopeRecord
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            Resources = resources,
        });

    /// <inheritdoc/>
    public ValueTask<long> CountAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult((long)_scopes.Count);

    /// <inheritdoc/>
    public ValueTask<long> CountAsync<TResult>(
        Func<IQueryable<OidcScopeRecord>, IQueryable<TResult>> query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return ValueTask.FromResult(query(_scopes.AsQueryable()).LongCount());
    }

    /// <inheritdoc/>
    public ValueTask CreateAsync(OidcScopeRecord scope, CancellationToken cancellationToken) =>
        throw Unwritten();

    /// <inheritdoc/>
    public ValueTask DeleteAsync(OidcScopeRecord scope, CancellationToken cancellationToken) =>
        throw Unwritten();

    /// <inheritdoc/>
    public ValueTask<OidcScopeRecord?> FindByIdAsync(
        string identifier,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(
            Guid.TryParse(identifier, out Guid id)
                ? _scopes.Find(scope => scope.Id == id)
                : null);

    /// <inheritdoc/>
    public ValueTask<OidcScopeRecord?> FindByNameAsync(
        string name,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(
            _scopes.Find(scope => string.Equals(scope.Name, name, StringComparison.Ordinal)));

    /// <inheritdoc/>
    public IAsyncEnumerable<OidcScopeRecord> FindByNamesAsync(
        ImmutableArray<string> names,
        CancellationToken cancellationToken)
    {
        string[] asked = [.. names];

        return _scopes
            .Where(scope => asked.Contains(scope.Name, StringComparer.Ordinal))
            .ToAsyncEnumerable();
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<OidcScopeRecord> FindByResourceAsync(
        string resource,
        CancellationToken cancellationToken) =>
        _scopes
            .Where(scope => scope.Resources.Contains(resource, StringComparer.Ordinal))
            .ToAsyncEnumerable();

    /// <inheritdoc/>
    public ValueTask<TResult?> GetAsync<TState, TResult>(
        Func<IQueryable<OidcScopeRecord>, TState, IQueryable<TResult>> query,
        TState state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return ValueTask.FromResult(query(_scopes.AsQueryable(), state).FirstOrDefault());
    }

    /// <inheritdoc/>
    public ValueTask<string?> GetDescriptionAsync(
        OidcScopeRecord scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return ValueTask.FromResult(scope.Description);
    }

    /// <inheritdoc/>
    public ValueTask<ImmutableDictionary<CultureInfo, string>> GetDescriptionsAsync(
        OidcScopeRecord scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return ValueTask.FromResult(OidcProperties.ReadByCulture(scope.Descriptions));
    }

    /// <inheritdoc/>
    public ValueTask<string?> GetDisplayNameAsync(
        OidcScopeRecord scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return ValueTask.FromResult(scope.DisplayName);
    }

    /// <inheritdoc/>
    public ValueTask<ImmutableDictionary<CultureInfo, string>> GetDisplayNamesAsync(
        OidcScopeRecord scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return ValueTask.FromResult(OidcProperties.ReadByCulture(scope.DisplayNames));
    }

    /// <inheritdoc/>
    public ValueTask<string?> GetIdAsync(OidcScopeRecord scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return ValueTask.FromResult<string?>(scope.Id.ToString("D", CultureInfo.InvariantCulture));
    }

    /// <inheritdoc/>
    public ValueTask<string?> GetNameAsync(OidcScopeRecord scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return ValueTask.FromResult<string?>(scope.Name);
    }

    /// <inheritdoc/>
    public ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(
        OidcScopeRecord scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return ValueTask.FromResult(OidcProperties.Read(scope.Properties));
    }

    /// <inheritdoc/>
    public ValueTask<ImmutableArray<string>> GetResourcesAsync(
        OidcScopeRecord scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return ValueTask.FromResult(ImmutableArray.Create(scope.Resources));
    }

    /// <inheritdoc/>
    public ValueTask<OidcScopeRecord> InstantiateAsync(CancellationToken cancellationToken) =>
        throw Unwritten();

    /// <inheritdoc/>
    public IAsyncEnumerable<OidcScopeRecord> ListAsync(
        int? count,
        int? offset,
        CancellationToken cancellationToken)
    {
        IEnumerable<OidcScopeRecord> listed = _scopes.OrderBy(scope => scope.Name, StringComparer.Ordinal);

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
        Func<IQueryable<OidcScopeRecord>, TState, IQueryable<TResult>> query,
        TState state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return query(_scopes.AsQueryable(), state).ToAsyncEnumerable();
    }

    /// <inheritdoc/>
    public ValueTask SetDescriptionAsync(
        OidcScopeRecord scope,
        string? description,
        CancellationToken cancellationToken) =>
        throw Unwritten();

    /// <inheritdoc/>
    public ValueTask SetDescriptionsAsync(
        OidcScopeRecord scope,
        ImmutableDictionary<CultureInfo, string> descriptions,
        CancellationToken cancellationToken) =>
        throw Unwritten();

    /// <inheritdoc/>
    public ValueTask SetDisplayNameAsync(
        OidcScopeRecord scope,
        string? name,
        CancellationToken cancellationToken) =>
        throw Unwritten();

    /// <inheritdoc/>
    public ValueTask SetDisplayNamesAsync(
        OidcScopeRecord scope,
        ImmutableDictionary<CultureInfo, string> names,
        CancellationToken cancellationToken) =>
        throw Unwritten();

    /// <inheritdoc/>
    public ValueTask SetNameAsync(
        OidcScopeRecord scope,
        string? name,
        CancellationToken cancellationToken) =>
        throw Unwritten();

    /// <inheritdoc/>
    public ValueTask SetPropertiesAsync(
        OidcScopeRecord scope,
        ImmutableDictionary<string, JsonElement> properties,
        CancellationToken cancellationToken) =>
        throw Unwritten();

    /// <inheritdoc/>
    public ValueTask SetResourcesAsync(
        OidcScopeRecord scope,
        ImmutableArray<string> resources,
        CancellationToken cancellationToken) =>
        throw Unwritten();

    /// <inheritdoc/>
    public ValueTask UpdateAsync(OidcScopeRecord scope, CancellationToken cancellationToken) =>
        throw Unwritten();

    private static NotSupportedException Unwritten() =>
        new("The scope registry is the deployment's and no request writes it.");
}
