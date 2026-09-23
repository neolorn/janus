using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;

namespace Janus.Storage.Authentication.Oidc;

/// <summary>
/// The scopes the deployment registered, as the protocol server reads them.
/// </summary>
/// <param name="context">The context the operation's reads are taken on.</param>
/// <remarks>
/// Implements AUTH-OIDC-001, CONV-CONTENT-001 and CONV-DESIGN-003. The scopes the
/// provider is built with are declared where the server is put together; this table is
/// what a deployment adds beyond them, and it adds them itself, so every write through
/// this interface is refused. The wording columns are read and handed on untouched: no
/// sentence in them is the library's.
/// </remarks>
internal sealed class OidcScopeStore(JanusDbContext context)
    : IOpenIddictScopeStore<OidcScopeRecord>
{
    /// <inheritdoc/>
    public async ValueTask<long> CountAsync(CancellationToken cancellationToken) =>
        await context.OidcScopes.LongCountAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<long> CountAsync<TResult>(
        Func<IQueryable<OidcScopeRecord>, IQueryable<TResult>> query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return await query(context.OidcScopes).LongCountAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public ValueTask CreateAsync(OidcScopeRecord scope, CancellationToken cancellationToken) =>
        throw Unwritten();

    /// <inheritdoc/>
    public ValueTask DeleteAsync(OidcScopeRecord scope, CancellationToken cancellationToken) =>
        throw Unwritten();

    /// <inheritdoc/>
    public async ValueTask<OidcScopeRecord?> FindByIdAsync(
        string identifier,
        CancellationToken cancellationToken) =>
        Guid.TryParse(identifier, out Guid id)
            ? await context.OidcScopes
                .FirstOrDefaultAsync(scope => scope.Id == id, cancellationToken)
                .ConfigureAwait(false)
            : null;

    /// <inheritdoc/>
    public async ValueTask<OidcScopeRecord?> FindByNameAsync(
        string name,
        CancellationToken cancellationToken) =>
        await context.OidcScopes
            .FirstOrDefaultAsync(scope => scope.Name == name, cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public IAsyncEnumerable<OidcScopeRecord> FindByNamesAsync(
        ImmutableArray<string> names,
        CancellationToken cancellationToken)
    {
        string[] asked = [.. names];

        return context.OidcScopes.Where(scope => asked.Contains(scope.Name)).AsAsyncEnumerable();
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<OidcScopeRecord> FindByResourceAsync(
        string resource,
        CancellationToken cancellationToken) =>
        context.OidcScopes.Where(scope => scope.Resources.Contains(resource)).AsAsyncEnumerable();

    /// <inheritdoc/>
    public async ValueTask<TResult?> GetAsync<TState, TResult>(
        Func<IQueryable<OidcScopeRecord>, TState, IQueryable<TResult>> query,
        TState state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return await query(context.OidcScopes, state)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
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
        IQueryable<OidcScopeRecord> listed = context.OidcScopes.OrderBy(scope => scope.Name);

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
        Func<IQueryable<OidcScopeRecord>, TState, IQueryable<TResult>> query,
        TState state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return query(context.OidcScopes, state).AsAsyncEnumerable();
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
