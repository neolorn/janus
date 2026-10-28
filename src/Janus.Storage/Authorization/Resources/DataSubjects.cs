using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Resources;
using Janus.Core;
using Janus.Storage.Identity.Accounts;
using Janus.Storage.Identity.Records;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authorization.Resources;

/// <summary>
/// Which subjects stand as the data subject of sensitive data, over the
/// <c>accounts</c> and <c>person_records</c> tables.
/// </summary>
/// <param name="context">The context the rows are read on.</param>
/// <remarks>
/// Implements IDN-LIFE-002a AC1, REC-AGE-001 and CONV-DESIGN-003. It reads the subject,
/// the state, the age answer and the attachment and nothing else, in one statement for
/// each table, for every subject asked.
/// </remarks>
internal sealed class DataSubjects(StoreContext context) : IDataSubjects
{
    /// <inheritdoc/>
    public async ValueTask<IReadOnlySet<SubjectId>> StandingAsync(
        IReadOnlyCollection<SubjectId> subjects,
        bool groupAnswers,
        CancellationToken cancellationToken)
    {
        if (subjects.Count is 0)
        {
            return new HashSet<SubjectId>();
        }

        var records = await context.Set<PersonRecordRow>()
            .AsNoTracking()
            .Where(record => subjects.Contains(record.Subject)
                && record.State != PersonRecordState.Deleting
                && record.State != PersonRecordState.Deleted)
            .Select(record => new
            {
                record.Subject,
                record.AttachedTo,
                Answered = record.AdultAffirmed == true || (groupAnswers && record.AgeGroup != null),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        SubjectId[] accounts =
        [
            .. subjects,
            .. records.Select(record => record.AttachedTo).OfType<SubjectId>(),
        ];

        var standing = (await context.Set<AccountRecord>()
                .AsNoTracking()
                .Where(account => accounts.Contains(account.Subject)
                    && account.State != AccountState.Deleting
                    && account.State != AccountState.Deleted)
                .Select(account => account.Subject)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .ToHashSet();

        // REC-AGE-001: a record attached to an account is admitted on its account's
        // standing, and one attached to none on its own age answer.
        var admitted = new HashSet<SubjectId>(standing.Where(subjects.Contains));

        admitted.UnionWith(records
            .Where(record => record.AttachedTo is SubjectId account ? standing.Contains(account) : record.Answered)
            .Select(record => record.Subject));

        return admitted;
    }
}
