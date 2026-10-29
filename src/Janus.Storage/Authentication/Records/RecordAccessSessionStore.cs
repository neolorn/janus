using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Records;
using Janus.Core;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Records;

/// <summary>
/// Record access sessions, over the <c>record_access_sessions</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <remarks>Implements REC-ACC-001 and CONV-DESIGN-003.</remarks>
internal sealed class RecordAccessSessionStore(StoreContext context) : IRecordAccessSessionStore
{
    /// <inheritdoc/>
    public async ValueTask<RecordAccessSession?> FindAsync(
        RecordAccessSessionId id,
        CancellationToken cancellationToken)
    {
        RecordAccessSessionRecord? record = await context.RecordAccessSessions
            .FindAsync([id], cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : Read(record);
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">No transaction is open.</exception>
    public async ValueTask<RecordAccessSession?> FindForUpdateAsync(
        RecordAccessSessionId id,
        CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("A record access session's row is held only inside the operation's transaction.");
        }

        bool tracked = context.RecordAccessSessions.Local.Any(record => record.Id == id);

        RecordAccessSessionRecord? held = (await context.RecordAccessSessions
                .FromSql($"SELECT * FROM identity.record_access_sessions WHERE id = {id.Value} FOR UPDATE")
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .SingleOrDefault();

        // A row the context already tracks was read before the lock, so it is read again:
        // what the decision is made on is the row as it stood when the lock was taken.
        if (held is not null && tracked)
        {
            await context.Entry(held).ReloadAsync(cancellationToken).ConfigureAwait(false);
        }

        return held is null ? null : Read(held);
    }

    /// <inheritdoc/>
    public async ValueTask AddAsync(RecordAccessSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        var record = new RecordAccessSessionRecord
        {
            Id = session.Id,
            Kind = session.Kind,
            Fingerprints = [.. session.Fingerprints.SelectMany(fingerprint => fingerprint)],
            Throttled = session.Throttled,
            CreatedAt = session.CreatedAt,
        };

        Carry(session, record);

        await context.RecordAccessSessions.AddAsync(record, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask RecordAsync(RecordAccessSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        RecordAccessSessionRecord record = await context.RecordAccessSessions
            .FindAsync([session.Id], cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The record access session has no row.");

        Carry(session, record);
    }

    /// <inheritdoc/>
    public async ValueTask RemoveAsync(RecordAccessSessionId id, CancellationToken cancellationToken)
    {
        RecordAccessSessionRecord? record = await context.RecordAccessSessions
            .FindAsync([id], cancellationToken)
            .ConfigureAwait(false);

        if (record is not null)
        {
            context.RecordAccessSessions.Remove(record);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<int> EndReachingAsync(SubjectId record, CancellationToken cancellationToken) =>
        await context.RecordAccessSessions
            .Where(session => session.Reached.Contains(record.Value))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        await context.RecordAccessSessions
            .Where(session => session.ExpiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

    private static void Carry(RecordAccessSession session, RecordAccessSessionRecord record)
    {
        record.OpenedAt = session.OpenedAt;
        record.ExpiresAt = session.ExpiresAt;
        record.Reached = [.. session.Reached.Select(subject => subject.Value)];
    }

    private static RecordAccessSession Read(RecordAccessSessionRecord record) =>
        RecordAccessSession.Existing(
            record.Id,
            record.Kind,
            [.. record.Fingerprints.Chunk(Fingerprint.Length)],
            record.Throttled,
            record.CreatedAt,
            record.OpenedAt,
            record.ExpiresAt,
            record.Reached.Select(subject => new SubjectId(subject)));
}
