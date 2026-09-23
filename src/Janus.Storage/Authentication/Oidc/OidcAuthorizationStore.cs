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
/// The grants the clients hold, over the <c>oidc_authorizations</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <remarks>
/// Implements AUTH-OIDC-001, AUTH-OIDC-003 and CONV-DESIGN-003. A grant is the thing a
/// reuse revokes, so the row carries a token a write must hold to succeed: two uses of
/// one grant cannot both change it, and the second is refused rather than lost.
/// </remarks>
internal sealed class OidcAuthorizationStore(JanusDbContext context)
    : IOpenIddictAuthorizationStore<OidcAuthorizationRecord>
{
    /// <inheritdoc/>
    public async ValueTask<long> CountAsync(CancellationToken cancellationToken) =>
        await context.OidcAuthorizations.LongCountAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<long> CountAsync<TResult>(
        Func<IQueryable<OidcAuthorizationRecord>, IQueryable<TResult>> query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return await query(context.OidcAuthorizations)
            .LongCountAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask CreateAsync(
        OidcAuthorizationRecord authorization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        authorization.ConcurrencyToken = Guid.CreateVersion7();

        _ = await context.OidcAuthorizations.AddAsync(authorization, cancellationToken)
            .ConfigureAwait(false);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask DeleteAsync(
        OidcAuthorizationRecord authorization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        _ = context.OidcAuthorizations.Remove(authorization);

        await SavedAsync(cancellationToken).ConfigureAwait(false);
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
        IQueryable<OidcAuthorizationRecord> found = context.OidcAuthorizations;

        if (subject is not null)
        {
            if (!Guid.TryParse(subject, out Guid held))
            {
                return AsyncEnumerable.Empty<OidcAuthorizationRecord>();
            }

            var account = new SubjectId(held);
            found = found.Where(authorization => authorization.Subject == account);
        }

        if (client is not null)
        {
            found = found.Where(authorization => authorization.ApplicationId == client);
        }

        if (status is not null)
        {
            found = found.Where(authorization => authorization.Status == status);
        }

        if (type is not null)
        {
            found = found.Where(authorization => authorization.Type == type);
        }

        if (scopes is ImmutableArray<string> asked)
        {
            string[] wanted = [.. asked];
            found = found.Where(authorization => wanted.All(scope => authorization.Scopes.Contains(scope)));
        }

        return found.AsAsyncEnumerable();
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<OidcAuthorizationRecord> FindByApplicationIdAsync(
        string identifier,
        CancellationToken cancellationToken) =>
        context.OidcAuthorizations
            .Where(authorization => authorization.ApplicationId == identifier)
            .AsAsyncEnumerable();

    /// <inheritdoc/>
    public async ValueTask<OidcAuthorizationRecord?> FindByIdAsync(
        string identifier,
        CancellationToken cancellationToken) =>
        Guid.TryParse(identifier, out Guid id)
            ? await context.OidcAuthorizations
                .FirstOrDefaultAsync(authorization => authorization.Id == id, cancellationToken)
                .ConfigureAwait(false)
            : null;

    /// <inheritdoc/>
    public IAsyncEnumerable<OidcAuthorizationRecord> FindBySubjectAsync(
        string subject,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(subject, out Guid held))
        {
            return AsyncEnumerable.Empty<OidcAuthorizationRecord>();
        }

        var account = new SubjectId(held);

        return context.OidcAuthorizations
            .Where(authorization => authorization.Subject == account)
            .AsAsyncEnumerable();
    }

    /// <inheritdoc/>
    public ValueTask<string?> GetApplicationIdAsync(
        OidcAuthorizationRecord authorization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        return ValueTask.FromResult<string?>(authorization.ApplicationId);
    }

    /// <inheritdoc/>
    public async ValueTask<TResult?> GetAsync<TState, TResult>(
        Func<IQueryable<OidcAuthorizationRecord>, TState, IQueryable<TResult>> query,
        TState state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return await query(context.OidcAuthorizations, state)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
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
        IQueryable<OidcAuthorizationRecord> listed =
            context.OidcAuthorizations.OrderBy(authorization => authorization.Id);

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
        Func<IQueryable<OidcAuthorizationRecord>, TState, IQueryable<TResult>> query,
        TState state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return query(context.OidcAuthorizations, state).AsAsyncEnumerable();
    }

    /// <inheritdoc/>
    public async ValueTask<long> PruneAsync(
        DateTimeOffset threshold,
        CancellationToken cancellationToken) =>
        // AUTH-KEY-003: a grant goes when it can no longer be acted on, which is when
        // it is no longer valid or when it stood for one exchange that left nothing
        // behind.
        await context.OidcAuthorizations
            .Where(authorization =>
                authorization.CreatedAt < threshold
                && (authorization.Status != OpenIddictConstants.Statuses.Valid
                    || (authorization.Type == OpenIddictConstants.AuthorizationTypes.AdHoc
                        && !context.OidcTokens.Any(token => token.AuthorizationId == authorization.Id))))
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
        IQueryable<OidcAuthorizationRecord> reached = context.OidcAuthorizations;

        if (subject is not null)
        {
            if (!Guid.TryParse(subject, out Guid held))
            {
                return 0;
            }

            var account = new SubjectId(held);
            reached = reached.Where(authorization => authorization.Subject == account);
        }

        if (client is not null)
        {
            reached = reached.Where(authorization => authorization.ApplicationId == client);
        }

        if (status is not null)
        {
            reached = reached.Where(authorization => authorization.Status == status);
        }

        if (type is not null)
        {
            reached = reached.Where(authorization => authorization.Type == type);
        }

        return await RevokedAsync(reached, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<long> RevokeByApplicationIdAsync(
        string identifier,
        CancellationToken cancellationToken) =>
        await RevokedAsync(
                context.OidcAuthorizations.Where(authorization =>
                    authorization.ApplicationId == identifier),
                cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<long> RevokeBySubjectAsync(
        string subject,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(subject, out Guid held))
        {
            return 0;
        }

        var account = new SubjectId(held);

        return await RevokedAsync(
                context.OidcAuthorizations.Where(authorization => authorization.Subject == account),
                cancellationToken)
            .ConfigureAwait(false);
    }

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
    public async ValueTask UpdateAsync(
        OidcAuthorizationRecord authorization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        authorization.ConcurrencyToken = Guid.CreateVersion7();

        await SavedAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<long> RevokedAsync(
        IQueryable<OidcAuthorizationRecord> reached,
        CancellationToken cancellationToken) =>
        await reached
            .Where(authorization => authorization.Status != OpenIddictConstants.Statuses.Revoked)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    authorization => authorization.Status,
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
                "The grant was changed by another operation.",
                failure);
        }
    }
}
