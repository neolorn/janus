using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Grants;
using Janus.Core;

namespace Janus.Authorization.Tests.Grants;

/// <summary>
/// Which subjects an account bears, as a test deployment needs it: every subject the
/// deployment names is an account's, save those a test declares no account bears, a
/// person record's among them.
/// </summary>
/// <remarks>
/// CONV-TEST-004: a fake holding the one fact the port answers, not a recorder of calls.
/// </remarks>
internal sealed class AccountSubjectsInMemory : IAccountSubjects
{
    private readonly HashSet<SubjectId> _unborne = [];

    /// <summary>
    /// Declares that no account bears a subject.
    /// </summary>
    /// <param name="subject">The subject.</param>
    public void Unborne(SubjectId subject) => _unborne.Add(subject);

    /// <inheritdoc/>
    public ValueTask<bool> BearsAsync(SubjectId subject, CancellationToken cancellationToken) =>
        ValueTask.FromResult(!_unborne.Contains(subject));
}
