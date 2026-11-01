using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Recovery;
using Janus.Core;

namespace Janus.Authentication.Tests.Recovery;

/// <summary>
/// The approver's reach, as a test states it: an approver reaches every account save the
/// ones the test says it may not hand out the roles of.
/// </summary>
internal sealed class ApproverReachInMemory : IApproverReach
{
    private readonly HashSet<(SubjectId Approver, SubjectId Subject)> _beyond = [];

    private readonly List<(SubjectId? Approver, SubjectId Subject)> _asked = [];

    /// <summary>Each approval the reach was asked about, in order.</summary>
    public IReadOnlyList<(SubjectId? Approver, SubjectId Subject)> Asked => _asked;

    /// <summary>
    /// Says the approver may not hand out a role the account holds.
    /// </summary>
    /// <param name="approver">Who approves.</param>
    /// <param name="subject">Whose recovery.</param>
    public void Beyond(SubjectId approver, SubjectId subject) => _beyond.Add((approver, subject));

    /// <inheritdoc/>
    public ValueTask<Error?> RefusedAsync(
        AccessContext context,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        _asked.Add((context.Effective, subject));

        return ValueTask.FromResult<Error?>(
            context.Effective is SubjectId approver && _beyond.Contains((approver, subject))
                ? Error.From(ErrorCodes.RoleNotAssignable)
                : null);
    }
}
