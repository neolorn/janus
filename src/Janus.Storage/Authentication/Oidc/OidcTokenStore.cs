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
using OpenIddict.Abstractions;

namespace Janus.Storage.Authentication.Oidc;

/// <summary>
/// The codes and tokens the provider issued, over the <c>oidc_tokens</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <remarks>
/// Implements AUTH-OIDC-003, AUTH-OIDC-004, AUTH-KEY-003 and CONV-DESIGN-003. A
/// redeemed row is kept rather than removed, because a second presentation of the same
/// value is what a reuse looks like and it has to find something to be caught by. The
/// row carries a token a write must hold to succeed, so two presentations of one token
/// cannot both redeem it.
/// </remarks>
internal sealed class OidcTokenStore(JanusDbContext context)
    : IOpenIddictTokenStore<OidcTokenRecord>
{
    /// <inheritdoc/>
    public async ValueTask<long> CountAsync(CancellationToken cancellationToken) =>
        await context.OidcTokens.LongCountAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<long> CountAsync<TResult>(
        Func<IQueryable<OidcTokenRecord>, IQueryable<TResult>> query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return await query(context.OidcTokens).LongCountAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask CreateAsync(OidcTokenRecord token, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        token.ConcurrencyToken = Guid.CreateVersion7();

        _ = await context.OidcTokens.AddAsync(token, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask DeleteAsync(OidcTokenRecord token, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        _ = context.OidcTokens.Remove(token);

        await SavedAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<OidcTokenRecord> FindAsync(
        string? subject,
        string? client,
        string? status,
        string? type,
        CancellationToken cancellationToken)
    {
        IQueryable<OidcTokenRecord> found = context.OidcTokens;

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
            found = found.Where(token => token.ApplicationId == client);
        }

        if (status is not null)
        {
            found = found.Where(token => token.Status == status);
        }

        if (type is not null)
        {
            found = found.Where(token => token.Type == type);
        }

        return found.AsAsyncEnumerable();
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<OidcTokenRecord> FindByApplicationIdAsync(
        string identifier,
        CancellationToken cancellationToken) =>
        context.OidcTokens.Where(token => token.ApplicationId == identifier).AsAsyncEnumerable();

    /// <inheritdoc/>
    public IAsyncEnumerable<OidcTokenRecord> FindByAuthorizationIdAsync(
        string identifier,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(identifier, out Guid grant))
        {
            return AsyncEnumerable.Empty<OidcTokenRecord>();
        }

        return context.OidcTokens.Where(token => token.AuthorizationId == grant).AsAsyncEnumerable();
    }

    /// <inheritdoc/>
    public async ValueTask<OidcTokenRecord?> FindByIdAsync(
        string identifier,
        CancellationToken cancellationToken) =>
        Guid.TryParse(identifier, out Guid id)
            ? await context.OidcTokens
                .FirstOrDefaultAsync(token => token.Id == id, cancellationToken)
                .ConfigureAwait(false)
            : null;

    /// <inheritdoc/>
    public async ValueTask<OidcTokenRecord?> FindByReferenceIdAsync(
        string identifier,
        CancellationToken cancellationToken) =>
        await context.OidcTokens
            .FirstOrDefaultAsync(token => token.ReferenceId == identifier, cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public IAsyncEnumerable<OidcTokenRecord> FindBySubjectAsync(
        string subject,
        CancellationToken cancellationToken)
    {
        if (OidcSubjects.Find(subject) is not SubjectId account)
        {
            return AsyncEnumerable.Empty<OidcTokenRecord>();
        }

        return context.OidcTokens.Where(token => token.Subject == account).AsAsyncEnumerable();
    }

    /// <inheritdoc/>
    public ValueTask<string?> GetApplicationIdAsync(
        OidcTokenRecord token,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        return ValueTask.FromResult<string?>(token.ApplicationId);
    }

    /// <inheritdoc/>
    public async ValueTask<TResult?> GetAsync<TState, TResult>(
        Func<IQueryable<OidcTokenRecord>, TState, IQueryable<TResult>> query,
        TState state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return await query(context.OidcTokens, state)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
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
    public ValueTask<string?> GetPayloadAsync(
        OidcTokenRecord token,
        CancellationToken cancellationToken)
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
    public ValueTask<string?> GetStatusAsync(
        OidcTokenRecord token,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        return ValueTask.FromResult<string?>(token.Status);
    }

    /// <inheritdoc/>
    public ValueTask<string?> GetSubjectAsync(
        OidcTokenRecord token,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        return ValueTask.FromResult<string?>(token.Subject.ToString());
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
        IQueryable<OidcTokenRecord> listed = context.OidcTokens.OrderBy(token => token.Id);

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
        Func<IQueryable<OidcTokenRecord>, TState, IQueryable<TResult>> query,
        TState state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return query(context.OidcTokens, state).AsAsyncEnumerable();
    }

    /// <inheritdoc/>
    public async ValueTask<long> PruneAsync(
        DateTimeOffset threshold,
        CancellationToken cancellationToken) =>
        // AUTH-KEY-003: a row goes when it can no longer be presented, which is when it
        // stopped being valid or when it expired. A redeemed row inside the window
        // stays, because that is what catches the second presentation.
        await context.OidcTokens
            .Where(token =>
                token.CreatedAt < threshold
                && (token.Status != OpenIddictConstants.Statuses.Valid
                    || (token.ExpiresAt != null && token.ExpiresAt < threshold)))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<long> RevokeAsync(
        string? subject,
        string? client,
        string? status,
        string? type,
        CancellationToken cancellationToken)
    {
        IQueryable<OidcTokenRecord> reached = context.OidcTokens;

        if (subject is not null)
        {
            if (OidcSubjects.Find(subject) is not SubjectId account)
            {
                return 0;
            }

            reached = reached.Where(token => token.Subject == account);
        }

        if (client is not null)
        {
            reached = reached.Where(token => token.ApplicationId == client);
        }

        if (status is not null)
        {
            reached = reached.Where(token => token.Status == status);
        }

        if (type is not null)
        {
            reached = reached.Where(token => token.Type == type);
        }

        return await RevokedAsync(reached, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<long> RevokeByApplicationIdAsync(
        string identifier,
        CancellationToken cancellationToken) =>
        await RevokedAsync(
                context.OidcTokens.Where(token => token.ApplicationId == identifier),
                cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<long> RevokeByAuthorizationIdAsync(
        string identifier,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(identifier, out Guid grant))
        {
            return 0;
        }

        return await RevokedAsync(
                context.OidcTokens.Where(token => token.AuthorizationId == grant),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<long> RevokeBySubjectAsync(
        string subject,
        CancellationToken cancellationToken)
    {
        if (OidcSubjects.Find(subject) is not SubjectId account)
        {
            return 0;
        }

        return await RevokedAsync(
                context.OidcTokens.Where(token => token.Subject == account),
                cancellationToken)
            .ConfigureAwait(false);
    }

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

        token.Subject = OidcSubjects.Of(subject);

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
    public async ValueTask UpdateAsync(OidcTokenRecord token, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        token.ConcurrencyToken = Guid.CreateVersion7();

        await SavedAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<long> RevokedAsync(
        IQueryable<OidcTokenRecord> reached,
        CancellationToken cancellationToken) =>
        await reached
            .Where(token => token.Status != OpenIddictConstants.Statuses.Revoked)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    token => token.Status,
                    OpenIddictConstants.Statuses.Revoked),
                cancellationToken)
            .ConfigureAwait(false);

    private async ValueTask SavedAsync(CancellationToken cancellationToken)
    {
        try
        {
            _ = await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException failure)
        {
            throw new OpenIddictExceptions.ConcurrencyException(
                "The token was changed by another operation.",
                failure);
        }
    }
}
