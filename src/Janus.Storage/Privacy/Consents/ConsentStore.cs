using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Consents;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Privacy.Consents;

/// <summary>
/// The consent and objection records, over the <c>consents</c> and
/// <c>objections</c> tables.
/// </summary>
/// <param name="context">The context the operation's reads and writes run on.</param>
/// <remarks>
/// Implements PRIV-CONS-001, PRIV-RIGHT-001a and CONV-DESIGN-003. Nothing here
/// deletes: a withdrawal writes a timestamp onto the row that is there, because the
/// record is the evidence the law asks for.
/// </remarks>
internal sealed class ConsentStore(JanusDbContext context) : IConsentStore
{
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
                NoticeVersion = consent.NoticeVersion,
                Mechanism = consent.Mechanism,
                Kind = consent.Kind,
                GrantedAt = consent.GrantedAt,
                WithdrawnAt = consent.WithdrawnAt,
                SupersededAt = consent.SupersededAt,
            });

            return;
        }

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
                NoticeVersion = objection.NoticeVersion,
                Mechanism = objection.Mechanism,
                RecordedAt = objection.RecordedAt,
                WithdrawnAt = objection.WithdrawnAt,
            });

            return;
        }

        held.NoticeVersion = objection.NoticeVersion;
        held.Mechanism = objection.Mechanism;
        held.RecordedAt = objection.RecordedAt;
        held.WithdrawnAt = objection.WithdrawnAt;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<HeldConsent>> LiveAgainstAnotherAsync(
        string noticeVersion,
        CancellationToken cancellationToken) =>
    [
        .. (await context.Consents
                .Where(consent => consent.WithdrawnAt == null
                    && consent.SupersededAt == null
                    && consent.NoticeVersion != noticeVersion)
                .OrderBy(consent => consent.GrantedAt)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .Select(consent => new HeldConsent(consent.Subject, Read(consent))),
    ];

    private static ConsentRecord Read(ConsentRecordRow row) =>
        new(
            row.Purpose,
            row.NoticeVersion,
            row.Mechanism,
            row.Kind,
            row.GrantedAt,
            row.WithdrawnAt,
            row.SupersededAt);

    private static ObjectionRecord Read(ObjectionRecordRow row) =>
        new(row.Purpose, row.NoticeVersion, row.Mechanism, row.RecordedAt, row.WithdrawnAt);
}
