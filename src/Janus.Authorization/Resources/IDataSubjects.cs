using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authorization.Resources;

/// <summary>
/// Where the registration of a record reads which subjects a record of a sensitive type
/// may name as its data subject.
/// </summary>
/// <remarks>
/// Implements IDN-LIFE-002a AC1 and REC-AGE-001. Accounts and person records are
/// another area's aggregates and therefore out of this one's reach (LIB-PKG-001), so
/// the one fact the registration evaluates is read through a port of its own
/// (CONV-DESIGN-003).
/// </remarks>
internal interface IDataSubjects
{
    /// <summary>
    /// Which of the subjects stand as the data subject of sensitive data: an account
    /// neither being deleted nor deleted; a person record neither being deleted nor
    /// deleted that holds the age answer; and a person record attached to such an
    /// account, admitted on its account's.
    /// </summary>
    /// <param name="subjects">The subjects asked about.</param>
    /// <param name="groupAnswers">
    /// Whether the age group answers the age question, as it does where the adult
    /// affirmation is off; otherwise only the affirmation does.
    /// </param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Those of them that stand.</returns>
    ValueTask<IReadOnlySet<SubjectId>> StandingAsync(
        IReadOnlyCollection<SubjectId> subjects,
        bool groupAnswers,
        CancellationToken cancellationToken);
}
