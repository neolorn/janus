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
/// The codes and tokens the provider issued, held in memory.
/// </summary>
internal sealed class OidcTokenStoreInMemory : IOpenIddictTokenStore<OidcTokenRecord>
{
    private readonly List<OidcTokenRecord> _tokens = [];

    /// <summary>
    /// The rows as they stand.
    /// </summary>
    public IReadOnlyList<OidcTokenRecord> All => _tokens;

    /// <inheritdoc/>
    public ValueTask<long> CountAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult((long)_tokens.Count);

    /// <inheritdoc/>
    public ValueTask<long> CountAsync<TResult>(
        Func<IQueryable<OidcTokenRecord>, IQueryable<TResult>> query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return ValueTask.FromResult(query(_tokens.AsQueryable()).LongCount());
    }

    /// <inheritdoc/>
    public ValueTask CreateAsync(OidcTokenRecord token, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        token.ConcurrencyToken = Guid.CreateVersion7();

        _tokens.Add(token);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask DeleteAsync(OidcTokenRecord token, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        _ = _tokens.Remove(token);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<OidcTokenRecord> FindAsync(
        string? subject,
        string? client,
        string? status,
        string? type,
        CancellationToken cancellationToken)
    {
        IEnumerable<OidcTokenRecord> found = _tokens;

        if (subject is not null)
        {
            if (OidcSubjects.Find(subject) is not SubjectId account)
            {
                return AsyncEnumerable.Empty<OidcTokenRecord>();
            }

            found = found.Where(token => token.Subject == account);
        }

        if (client is not null)
        {
            found = found.Where(token =>
                string.Equals(token.ApplicationId, client, StringComparison.Ordinal));
        }

        if (status is not null)
        {
            found = found.Where(token => string.Equals(token.Status, status, StringComparison.Ordinal));
        }

        if (type is not null)
        {
            found = found.Where(token => string.Equals(token.Type, type, StringComparison.Ordinal));
        }

        return found.ToAsyncEnumerable();
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<OidcTokenRecord> FindByApplicationIdAsync(
        string identifier,
        CancellationToken cancellationToken) =>
        _tokens
            .Where(token => string.Equals(token.ApplicationId, identifier, StringComparison.Ordinal))
            .ToAsyncEnumerable();

    /// <inheritdoc/>
    public IAsyncEnumerable<OidcTokenRecord> FindByAuthorizationIdAsync(
        string identifier,
        CancellationToken cancellationToken) =>
        Guid.TryParse(identifier, out Guid grant)
            ? _tokens.Where(token => token.AuthorizationId == grant).ToAsyncEnumerable()
            : AsyncEnumerable.Empty<OidcTokenRecord>();

    /// <inheritdoc/>
    public ValueTask<OidcTokenRecord?> FindByIdAsync(
        string identifier,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(
            Guid.TryParse(identifier, out Guid id)
                ? _tokens.Find(token => token.Id == id)
                : null);

    /// <inheritdoc/>
    public ValueTask<OidcTokenRecord?> FindByReferenceIdAsync(
        string identifier,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(
            _tokens.Find(token =>
                string.Equals(token.ReferenceId, identifier, StringComparison.Ordinal)));

    /// <inheritdoc/>
    public IAsyncEnumerable<OidcTokenRecord> FindBySubjectAsync(
        string subject,
        CancellationToken cancellationToken) =>
        OidcSubjects.Find(subject) is SubjectId account
            ? _tokens.Where(token => token.Subject == account).ToAsyncEnumerable()
            : AsyncEnumerable.Empty<OidcTokenRecord>();

    /// <inheritdoc/>
    public ValueTask<string?> GetApplicationIdAsync(
        OidcTokenRecord token,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        return ValueTask.FromResult<string?>(token.ApplicationId);
    }

    /// <inheritdoc/>
    public ValueTask<TResult?> GetAsync<TState, TResult>(
        Func<IQueryable<OidcTokenRecord>, TState, IQueryable<TResult>> query,
        TState state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return ValueTask.FromResult(query(_tokens.AsQueryable(), state).FirstOrDefault());
    }

    /// <inheritdoc/>
    public ValueTask<string?> GetAuthorizationIdAsync(
        OidcTokenRecord token,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        return ValueTask.FromResult(
            token.AuthorizationId is Guid grant
                ? grant.ToString("D", CultureInfo.InvariantCulture)
                : null);
    }

    /// <inheritdoc/>
    public ValueTask<DateTimeOffset?> GetCreationDateAsync(
        OidcTokenRecord token,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        return ValueTask.FromResult(token.CreatedAt);
    }

    /// <inheritdoc/>
    public ValueTask<DateTimeOffset?> GetExpirationDateAsync(
        OidcTokenRecord token,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        return ValueTask.FromResult(token.ExpiresAt);
    }

    /// <inheritdoc/>
    public ValueTask<string?> GetIdAsync(OidcTokenRecord token, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        return ValueTask.FromResult<string?>(token.Id.ToString("D", CultureInfo.InvariantCulture));
    }

    /// <inheritdoc/>
    public ValueTask<string?> GetPayloadAsync(OidcTokenRecord token, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        return ValueTask.FromResult(token.Payload);
    }

    /// <inheritdoc/>
    public ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(
        OidcTokenRecord token,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        return ValueTask.FromResult(OidcProperties.Read(token.Properties));
    }

    /// <inheritdoc/>
    public ValueTask<DateTimeOffset?> GetRedemptionDateAsync(
        OidcTokenRecord token,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        return ValueTask.FromResult(token.RedeemedAt);
    }

    /// <inheritdoc/>
    public ValueTask<string?> GetReferenceIdAsync(
        OidcTokenRecord token,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        return ValueTask.FromResult(token.ReferenceId);
    }

    /// <inheritdoc/>
    public ValueTask<string?> GetStatusAsync(OidcTokenRecord token, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        return ValueTask.FromResult<string?>(token.Status);
    }

    /// <inheritdoc/>
    public ValueTask<string?> GetSubjectAsync(OidcTokenRecord token, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        return ValueTask.FromResult<string?>(token.Subject?.ToString());
    }

    /// <inheritdoc/>
    public ValueTask<string?> GetTypeAsync(OidcTokenRecord token, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        return ValueTask.FromResult<string?>(token.Type);
    }

    /// <inheritdoc/>
    public ValueTask<OidcTokenRecord> InstantiateAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(new OidcTokenRecord { Id = Guid.CreateVersion7() });

    /// <inheritdoc/>
    public IAsyncEnumerable<OidcTokenRecord> ListAsync(
        int? count,
        int? offset,
        CancellationToken cancellationToken)
    {
        IEnumerable<OidcTokenRecord> listed = _tokens.OrderBy(token => token.Id);

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
        Func<IQueryable<OidcTokenRecord>, TState, IQueryable<TResult>> query,
        TState state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return query(_tokens.AsQueryable(), state).ToAsyncEnumerable();
    }

    /// <inheritdoc/>
    public ValueTask<long> PruneAsync(DateTimeOffset threshold, CancellationToken cancellationToken) =>
        ValueTask.FromResult((long)_tokens.RemoveAll(token =>
            token.CreatedAt < threshold
            && (!string.Equals(token.Status, OpenIddictConstants.Statuses.Valid, StringComparison.Ordinal)
                || (token.ExpiresAt is DateTimeOffset expiry && expiry < threshold))));

    /// <inheritdoc/>
    public ValueTask<long> RevokeAsync(
        string? subject,
        string? client,
        string? status,
        string? type,
        CancellationToken cancellationToken)
    {
        IEnumerable<OidcTokenRecord> reached = _tokens;

        if (subject is not null)
        {
            if (OidcSubjects.Find(subject) is not SubjectId account)
            {
                return ValueTask.FromResult(0L);
            }

            reached = reached.Where(token => token.Subject == account);
        }

        if (client is not null)
        {
            reached = reached.Where(token =>
                string.Equals(token.ApplicationId, client, StringComparison.Ordinal));
        }

        if (status is not null)
        {
            reached = reached.Where(token => string.Equals(token.Status, status, StringComparison.Ordinal));
        }

        if (type is not null)
        {
            reached = reached.Where(token => string.Equals(token.Type, type, StringComparison.Ordinal));
        }

        return ValueTask.FromResult(Revoked(reached));
    }

    /// <inheritdoc/>
    public ValueTask<long> RevokeByApplicationIdAsync(
        string identifier,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(
            Revoked(_tokens.Where(token =>
                string.Equals(token.ApplicationId, identifier, StringComparison.Ordinal))));

    /// <inheritdoc/>
    public ValueTask<long> RevokeByAuthorizationIdAsync(
        string identifier,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(
            Guid.TryParse(identifier, out Guid grant)
                ? Revoked(_tokens.Where(token => token.AuthorizationId == grant))
                : 0L);

    /// <inheritdoc/>
    public ValueTask<long> RevokeBySubjectAsync(string subject, CancellationToken cancellationToken) =>
        ValueTask.FromResult(
            OidcSubjects.Find(subject) is SubjectId account
                ? Revoked(_tokens.Where(token => token.Subject == account))
                : 0L);

    /// <inheritdoc/>
    public ValueTask SetApplicationIdAsync(
        OidcTokenRecord token,
        string? identifier,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        token.ApplicationId = identifier ?? string.Empty;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask SetAuthorizationIdAsync(
        OidcTokenRecord token,
        string? identifier,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        token.AuthorizationId = identifier is { Length: > 0 }
            ? Guid.Parse(identifier, CultureInfo.InvariantCulture)
            : null;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask SetCreationDateAsync(
        OidcTokenRecord token,
        DateTimeOffset? date,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        token.CreatedAt = date;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask SetExpirationDateAsync(
        OidcTokenRecord token,
        DateTimeOffset? date,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        token.ExpiresAt = date;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask SetPayloadAsync(
        OidcTokenRecord token,
        string? payload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        token.Payload = payload;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask SetPropertiesAsync(
        OidcTokenRecord token,
        ImmutableDictionary<string, JsonElement> properties,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        token.Properties = OidcProperties.Write(properties);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask SetRedemptionDateAsync(
        OidcTokenRecord token,
        DateTimeOffset? date,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        token.RedeemedAt = date;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask SetReferenceIdAsync(
        OidcTokenRecord token,
        string? identifier,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        token.ReferenceId = identifier;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask SetStatusAsync(
        OidcTokenRecord token,
        string? status,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        token.Status = status ?? string.Empty;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask SetSubjectAsync(
        OidcTokenRecord token,
        string? subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        // AUTH-OIDC-006 AC2: a pushed request names nobody; anything named is an
        // account of this deployment or is refused.
        token.Subject = subject is null ? null : OidcSubjects.Of(subject);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask SetTypeAsync(
        OidcTokenRecord token,
        string? type,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        token.Type = type ?? string.Empty;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask UpdateAsync(OidcTokenRecord token, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        token.ConcurrencyToken = Guid.CreateVersion7();

        return ValueTask.CompletedTask;
    }

    private static long Revoked(IEnumerable<OidcTokenRecord> reached)
    {
        long taken = 0;

        foreach (OidcTokenRecord token in reached.ToList())
        {
            if (!string.Equals(
                token.Status,
                OpenIddictConstants.Statuses.Revoked,
                StringComparison.Ordinal))
            {
                token.Status = OpenIddictConstants.Statuses.Revoked;
                taken++;
            }
        }

        return taken;
    }
}
