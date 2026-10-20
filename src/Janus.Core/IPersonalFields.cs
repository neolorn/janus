using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// The cipher of a host's own declared fields: each subject's key lent for the length of
/// one use, through which the host encrypts what it writes and reads what it holds.
/// </summary>
/// <remarks>
/// Implements PRIV-RIGHT-005a, LIB-API-001 and LIB-API-005 (D-203). A seam, as
/// <see cref="IResources"/> is: it joins the host's work, has no endpoint and takes no
/// access context, since who may see a record is the host's own question to the gate.
/// The subject is the value of the field's declared subject column, which the host
/// passes. The keys are read in one statement and each is unwrapped once; every key is
/// cleared when the use returns, which is synchronous, so no key outlives the call.
/// Erasing the subject's key makes every value written through this unreadable, in the
/// host's tables as in the library's.
/// </remarks>
public interface IPersonalFields
{
    /// <summary>
    /// Lends one subject's key to a use.
    /// </summary>
    /// <typeparam name="TValue">What the use answers.</typeparam>
    /// <param name="subject">The subject the fields are held under.</param>
    /// <param name="use">What is done with the subject's fields, which keeps nothing of them once it returns.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// What the use answered, or <c>api.request.invalid</c> naming <c>subject</c> where
    /// the deployment holds no key for the subject.
    /// </returns>
    ValueTask<Result<TValue>> WithSubjectAsync<TValue>(
        SubjectId subject,
        Func<SubjectFields, Result<TValue>> use,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lends the keys of a page of subjects to one use.
    /// </summary>
    /// <typeparam name="TValue">What the use answers.</typeparam>
    /// <param name="subjects">The subjects the page's fields are held under.</param>
    /// <param name="use">
    /// What is done with each subject's fields, by subject, which keeps nothing of them
    /// once it returns.
    /// </param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// What the use answered, or <c>api.request.invalid</c> naming <c>subject</c> where
    /// the deployment holds no key for one of the subjects, in which case the use is not
    /// called.
    /// </returns>
    ValueTask<Result<TValue>> WithSubjectsAsync<TValue>(
        IReadOnlyList<SubjectId> subjects,
        Func<IReadOnlyDictionary<SubjectId, SubjectFields>, Result<TValue>> use,
        CancellationToken cancellationToken);
}
