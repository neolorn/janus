using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Gate;
using Janus.Core;
using Janus.Storage.Privacy.Consents;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authorization.Gate;

/// <summary>
/// What a subject consented to for one purpose, over the <c>consents</c> table.
/// </summary>
/// <param name="context">The context the row is read on.</param>
/// <remarks>
/// Implements PRIV-SENS-002, PRIV-CONS-001, AUTHZ-GATE-005 and CONV-DESIGN-003. A
/// subject holds a record a grant, so the one the gate evaluates is the record that
/// stands for the purpose: the live one, or the latest where none is live, which says
/// whether the consent was taken back or superseded. It reads that and nothing else.
/// </remarks>
internal sealed class RecordedConsents(StoreContext context) : IRecordedConsents
{
    /// <inheritdoc/>
    public async ValueTask<ConsentRecord?> OfAsync(
        SubjectId subject,
        string purpose,
        CancellationToken cancellationToken) =>
        await Standing(context.Consents
                .AsNoTracking()
                .Where(consent => consent.Subject == subject && consent.Purpose == purpose))
            .Select(consent => new ConsentRecord(
                consent.Purpose,
                consent.Document,
                consent.NoticeVersion,
                consent.Mechanism,
                consent.Kind,
                consent.GrantedAt,
                consent.WithdrawnAt,
                consent.SupersededAt))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyDictionary<SubjectId, IReadOnlyList<ConsentRecord>>> OfAsync(
        IReadOnlyCollection<SubjectId> subjects,
        IReadOnlyCollection<string> purposes,
        CancellationToken cancellationToken)
    {
        var held = await Standing(context.Consents
                .AsNoTracking()
                .Where(consent => subjects.Contains(consent.Subject) && purposes.Contains(consent.Purpose)))
            .Select(consent => new
            {
                consent.Subject,
                Record = new ConsentRecord(
                    consent.Purpose,
                    consent.Document,
                    consent.NoticeVersion,
                    consent.Mechanism,
                    consent.Kind,
                    consent.GrantedAt,
                    consent.WithdrawnAt,
                    consent.SupersededAt),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return held
            .GroupBy(consent => consent.Subject)
            .ToDictionary(
                subject => subject.Key,
                IReadOnlyList<ConsentRecord> (subject) =>
                [
                    .. subject
                        .GroupBy(consent => consent.Record.Purpose)
                        .Select(purpose => purpose.First().Record),
                ]);
    }

    // The record that stands comes first: the live one, then the latest.
    private static IOrderedQueryable<ConsentRecordRow> Standing(IQueryable<ConsentRecordRow> consents) =>
        consents
            .OrderByDescending(consent => consent.WithdrawnAt == null && consent.SupersededAt == null)
            .ThenByDescending(consent => consent.GrantedAt)
            .ThenByDescending(consent => consent.Id);
}
