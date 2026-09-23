using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Storage.Authentication.Oidc;
using OpenIddict.Abstractions;

namespace Janus.Hosting.Tests.Oidc;

/// <summary>
/// The grants the clients hold, held in memory.
/// </summary>
/// <param name="tokens">What hangs from a grant, which decides when one may be pruned.</param>
internal sealed class OidcAuthorizationStoreInMemory(OidcTokenStoreInMemory tokens)
    : IOpenIddictAuthorizationStore<OidcAuthorizationRecord>
{
    private readonly List<OidcAuthorizationRecord> _authorizations = [];

    /// <summary>
    /// The grants as they stand.
    /// </summary>
    public IReadOnlyList<OidcAuthorizationRecord> All => _authorizations;

    /// <inheritdoc/>
    public ValueTask<long> CountAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult((long)_authorizations.Count);

    /// <inheritdoc/>
    public ValueTask<long> CountAsync<TResult>(
        Func<IQueryable<OidcAuthorizationRecord>, IQueryable<TResult>> query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return ValueTask.FromResult(query(_authorizations.AsQueryable()).LongCount());
    }

    /// <inheritdoc/>
    public ValueTask CreateAsync(
        OidcAuthorizationRecord authorization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        authorization.ConcurrencyToken = Guid.CreateVersion7();

        _authorizations.Add(authorization);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask DeleteAsync(
        OidcAuthorizationRecord authorization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        _ = _authorizations.Remove(authorization);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<OidcAuthorizationRecord> FindAsync(
        string? subject,
        string? client,
        string? status,
        string? type,
        ImmutableArray<string>? scopes,
        CancellationToken cancellationToken)
    {
        IEnumerable<OidcAuthorizationRecord> found = _authorizations;

        if (subject is not null)
        {
            if (OidcSubjects.Find(subject) is not SubjectId account)
            {
                return AsyncEnumerable.Empty<OidcAuthorizationRecord>();
            }

            found = found.Where(authorization => authorization.Subject == account);
        }

        if (client is not null)
        {
            found = found.Where(authorization =>
                string.Equals(authorization.ApplicationId, client, StringComparison.Ordinal));
        }

        if (status is not null)
        {
            found = found.Where(authorization =>
                string.Equals(authorization.Status, status, StringComparison.Ordinal));
        }

        if (type is not null)
        {
            found = found.Where(authorization =>
                string.Equals(authorization.Type, type, StringComparison.Ordinal));
        }

        if (scopes is ImmutableArray<string> asked)
        {
            found = found.Where(authorization =>
                asked.All(scope => authorization.Scopes.Contains(scope, StringComparer.Ordinal)));
        }

        return found.ToAsyncEnumerable();
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<OidcAuthorizationRecord> FindByApplicationIdAsync(
        string identifier,
        CancellationToken cancellationToken) =>
        _authorizations
            .Where(authorization =>
                string.Equals(authorization.ApplicationId, identifier, StringComparison.Ordinal))
            .ToAsyncEnumerable();

    /// <inheritdoc/>
    public ValueTask<OidcAuthorizationRecord?> FindByIdAsync(
        string identifier,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(
            Guid.TryParse(identifier, out Guid id)
                ? _authorizations.Find(authorization => authorization.Id == id)
                : null);

    /// <inheritdoc/>
    public IAsyncEnumerable<OidcAuthorizationRecord> FindBySubjectAsync(
        string subject,
        CancellationToken cancellationToken) =>
        OidcSubjects.Find(subject) is SubjectId account
            ? _authorizations
                .Where(authorization => authorization.Subject == account)
                .ToAsyncEnumerable()
            : AsyncEnumerable.Empty<OidcAuthorizationRecord>();

    /// <inheritdoc/>
    public ValueTask<string?> GetApplicationIdAsync(
        OidcAuthorizationRecord authorization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        return ValueTask.FromResult<string?>(authorization.ApplicationId);
    }

    /// <inheritdoc/>
    public ValueTask<TResult?> GetAsync<TState, TResult>(
        Func<IQueryable<OidcAuthorizationRecord>, TState, IQueryable<TResult>> query,
        TState state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return ValueTask.FromResult(query(_authorizations.AsQueryable(), state).FirstOrDefault());
    }

    /// <inheritdoc/>
    public ValueTask<DateTimeOffset?> GetCreationDateAsync(
        OidcAuthorizationRecord authorization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        return ValueTask.FromResult(authorization.CreatedAt);
    }

    /// <inheritdoc/>
    public ValueTask<string?> GetIdAsync(
        OidcAuthorizationRecord authorization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        return ValueTask.FromResult<string?>(
            authorization.Id.ToString("D", CultureInfo.InvariantCulture));
    }

    /// <inheritdoc/>
    public ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(
        OidcAuthorizationRecord authorization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        return ValueTask.FromResult(OidcProperties.Read(authorization.Properties));
    }

    /// <inheritdoc/>
    public ValueTask<ImmutableArray<string>> GetScopesAsync(
        OidcAuthorizationRecord authorization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        return ValueTask.FromResult(ImmutableArray.Create(authorization.Scopes));
    }

    /// <inheritdoc/>
    public ValueTask<string?> GetStatusAsync(
        OidcAuthorizationRecord authorization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        return ValueTask.FromResult<string?>(authorization.Status);
    }

    /// <inheritdoc/>
    public ValueTask<string?> GetSubjectAsync(
        OidcAuthorizationRecord authorization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        return ValueTask.FromResult<string?>(authorization.Subject.ToString());
    }

    /// <inheritdoc/>
    public ValueTask<string?> GetTypeAsync(
        OidcAuthorizationRecord authorization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        return ValueTask.FromResult<string?>(authorization.Type);
    }

    /// <inheritdoc/>
    public ValueTask<OidcAuthorizationRecord> InstantiateAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(new OidcAuthorizationRecord { Id = Guid.CreateVersion7() });

    /// <inheritdoc/>
    public IAsyncEnumerable<OidcAuthorizationRecord> ListAsync(
        int? count,
        int? offset,
        CancellationToken cancellationToken)
    {
        IEnumerable<OidcAuthorizationRecord> listed =
            _authorizations.OrderBy(authorization => authorization.Id);

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
        Func<IQueryable<OidcAuthorizationRecord>, TState, IQueryable<TResult>> query,
        TState state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return query(_authorizations.AsQueryable(), state).ToAsyncEnumerable();
    }

    /// <inheritdoc/>
    public ValueTask<long> PruneAsync(DateTimeOffset threshold, CancellationToken cancellationToken) =>
        ValueTask.FromResult((long)_authorizations.RemoveAll(authorization =>
            authorization.CreatedAt < threshold
            && (!string.Equals(
                    authorization.Status,
                    OpenIddictConstants.Statuses.Valid,
                    StringComparison.Ordinal)
                || (string.Equals(
                        authorization.Type,
                        OpenIddictConstants.AuthorizationTypes.AdHoc,
                        StringComparison.Ordinal)
                    && !tokens.All.Any(token => token.AuthorizationId == authorization.Id)))));

    /// <inheritdoc/>
    public ValueTask<long> RevokeAsync(
        string? subject,
        string? client,
        string? status,
        string? type,
        CancellationToken cancellationToken)
    {
        IEnumerable<OidcAuthorizationRecord> reached = _authorizations;

        if (subject is not null)
        {
            if (OidcSubjects.Find(subject) is not SubjectId account)
            {
                return ValueTask.FromResult(0L);
            }

            reached = reached.Where(authorization => authorization.Subject == account);
        }

        if (client is not null)
        {
            reached = reached.Where(authorization =>
                string.Equals(authorization.ApplicationId, client, StringComparison.Ordinal));
        }

        if (status is not null)
        {
            reached = reached.Where(authorization =>
                string.Equals(authorization.Status, status, StringComparison.Ordinal));
        }

        if (type is not null)
        {
            reached = reached.Where(authorization =>
                string.Equals(authorization.Type, type, StringComparison.Ordinal));
        }

        return ValueTask.FromResult(Revoked(reached));
    }

    /// <inheritdoc/>
    public ValueTask<long> RevokeByApplicationIdAsync(
        string identifier,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(
            Revoked(_authorizations.Where(authorization =>
                string.Equals(authorization.ApplicationId, identifier, StringComparison.Ordinal))));

    /// <inheritdoc/>
    public ValueTask<long> RevokeBySubjectAsync(
        string subject,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(
            OidcSubjects.Find(subject) is SubjectId account
                ? Revoked(_authorizations.Where(authorization => authorization.Subject == account))
                : 0L);

    /// <inheritdoc/>
    public ValueTask SetApplicationIdAsync(
        OidcAuthorizationRecord authorization,
        string? identifier,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        authorization.ApplicationId = identifier ?? string.Empty;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask SetCreationDateAsync(
        OidcAuthorizationRecord authorization,
        DateTimeOffset? date,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        authorization.CreatedAt = date;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask SetPropertiesAsync(
        OidcAuthorizationRecord authorization,
        ImmutableDictionary<string, JsonElement> properties,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        authorization.Properties = OidcProperties.Write(properties);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask SetScopesAsync(
        OidcAuthorizationRecord authorization,
        ImmutableArray<string> scopes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        authorization.Scopes = [.. scopes];

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask SetStatusAsync(
        OidcAuthorizationRecord authorization,
        string? status,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        authorization.Status = status ?? string.Empty;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask SetSubjectAsync(
        OidcAuthorizationRecord authorization,
        string? subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        authorization.Subject = OidcSubjects.Of(subject);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask SetTypeAsync(
        OidcAuthorizationRecord authorization,
        string? type,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        authorization.Type = type ?? string.Empty;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask UpdateAsync(
        OidcAuthorizationRecord authorization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        authorization.ConcurrencyToken = Guid.CreateVersion7();

        return ValueTask.CompletedTask;
    }

    private static long Revoked(IEnumerable<OidcAuthorizationRecord> reached)
    {
        long taken = 0;

        foreach (OidcAuthorizationRecord authorization in reached.ToList())
        {
            if (!string.Equals(
                authorization.Status,
                OpenIddictConstants.Statuses.Revoked,
                StringComparison.Ordinal))
            {
                authorization.Status = OpenIddictConstants.Statuses.Revoked;
                taken++;
            }
        }

        return taken;
    }
}
