using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Janus.Privacy.Consents;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Privacy.Consents;

/// <summary>
/// The consent and objection records, over the <c>consents</c> and
/// <c>objections</c> tables.
/// </summary>
/// <param name="context">The context the operation's reads and writes run on.</param>
/// <param name="connections">Where the lock statement takes its connection from.</param>
/// <remarks>
/// Implements PRIV-CONS-001, PRIV-RIGHT-001a and CONV-DESIGN-003. Nothing here
/// deletes: a withdrawal writes a timestamp onto the row that is there, because the
/// record is the evidence the law asks for.
/// </remarks>
internal sealed class ConsentStore(StoreContext context, DataConnections connections) : IConsentStore
{
    // D-166 X3: a withdrawal is decided on the record it reads, and a first grant has
    // no row to lock, so the subject's records are held as one for the rest of the
    // transaction; no read takes this lock.
    private const string Hold =
        "SELECT pg_advisory_xact_lock(hashtextextended('identity.consents/' || CAST(@subject AS text), 0));";

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">No transaction is open.</exception>
    public async ValueTask HoldAsync(SubjectId subject, CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("A subject's consents are held only inside the operation's transaction.");
        }

        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        _ = await ambient.Connection
            .ExecuteAsync(new CommandDefinition(
                Hold,
                new { subject = subject.Value },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        // A record the context already tracks was read before the lock, so it is read
        // again: what the change is decided on is the record as committed.
        foreach (ConsentRecordRow row in context.Consents.Local.Where(row => row.Subject == subject).ToList())
        {
            await context.Entry(row).ReloadAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (ObjectionRecordRow row in context.Objections.Local.Where(row => row.Subject == subject).ToList())
        {
            await context.Entry(row).ReloadAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<ConsentRecord>> ConsentsAsync(
        SubjectId subject,
        CancellationToken cancellationToken) =>
    [
        .. (await context.Consents
                .Where(consent => consent.Subject == subject)
                .OrderBy(consent => consent.GrantedAt)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .Select(Read),
    ];

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<ObjectionRecord>> ObjectionsAsync(
        SubjectId subject,
        CancellationToken cancellationToken) =>
    [
        .. (await context.Objections
                .Where(objection => objection.Subject == subject)
                .OrderBy(objection => objection.RecordedAt)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .Select(Read),
    ];

    /// <inheritdoc/>
    public async ValueTask RecordAsync(
        SubjectId subject,
        ConsentRecord consent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consent);

        ConsentRecordRow? held = await context.Consents
            .FindAsync([subject, consent.Purpose], cancellationToken)
            .ConfigureAwait(false);

        if (held is null)
        {
            context.Consents.Add(new ConsentRecordRow
            {
                Subject = subject,
                Purpose = consent.Purpose,
                Document = consent.Document,
                NoticeVersion = consent.NoticeVersion,
                Mechanism = consent.Mechanism,
                Kind = consent.Kind,
                GrantedAt = consent.GrantedAt,
                WithdrawnAt = consent.WithdrawnAt,
                SupersededAt = consent.SupersededAt,
            });

            return;
        }

        held.Document = consent.Document;
        held.NoticeVersion = consent.NoticeVersion;
        held.Mechanism = consent.Mechanism;
        held.Kind = consent.Kind;
        held.GrantedAt = consent.GrantedAt;
        held.WithdrawnAt = consent.WithdrawnAt;
        held.SupersededAt = consent.SupersededAt;
    }

    /// <inheritdoc/>
    public async ValueTask RecordAsync(
        SubjectId subject,
        ObjectionRecord objection,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(objection);

        ObjectionRecordRow? held = await context.Objections
            .FindAsync([subject, objection.Purpose], cancellationToken)
            .ConfigureAwait(false);

        if (held is null)
        {
            context.Objections.Add(new ObjectionRecordRow
            {
                Subject = subject,
                Purpose = objection.Purpose,
                Document = objection.Document,
                NoticeVersion = objection.NoticeVersion,
                Mechanism = objection.Mechanism,
                RecordedAt = objection.RecordedAt,
                WithdrawnAt = objection.WithdrawnAt,
            });

            return;
        }

        held.Document = objection.Document;
        held.NoticeVersion = objection.NoticeVersion;
        held.Mechanism = objection.Mechanism;
        held.RecordedAt = objection.RecordedAt;
        held.WithdrawnAt = objection.WithdrawnAt;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<HeldConsent>> LiveAgainstAnotherAsync(
        IReadOnlyCollection<string> purposes,
        string document,
        string version,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(purposes);

        string[] named = [.. purposes];

        return
        [
            .. (await context.Consents
                    .Where(consent => consent.WithdrawnAt == null
                        && consent.SupersededAt == null
                        && named.Contains(consent.Purpose)
                        && (consent.Document != document || consent.NoticeVersion != version))
                    .OrderBy(consent => consent.GrantedAt)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false))
                .Select(consent => new HeldConsent(consent.Subject, Read(consent))),
        ];
    }

    private static ConsentRecord Read(ConsentRecordRow row) =>
        new(
            row.Purpose,
            row.Document,
            row.NoticeVersion,
            row.Mechanism,
            row.Kind,
            row.GrantedAt,
            row.WithdrawnAt,
            row.SupersededAt);

    private static ObjectionRecord Read(ObjectionRecordRow row) =>
        new(row.Purpose, row.Document, row.NoticeVersion, row.Mechanism, row.RecordedAt, row.WithdrawnAt);
}
