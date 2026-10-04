using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Gate;
using Janus.Core;

namespace Janus.Authorization.Tests.Gate;

/// <summary>
/// Which accounts of a deployment are under a processing restriction, held in memory.
/// </summary>
/// <remarks>
/// CONV-TEST-004: a fake that answers from what it holds, so a test that restricts an
/// account sees what the gate would see.
/// </remarks>
internal sealed class RestrictionsInMemory : ISubjectRestrictions
{
    private readonly HashSet<SubjectId> _restricted = [];
    private readonly List<SubjectId> _held = [];

    /// <summary>
    /// Gets or sets a value indicating whether a transaction is open, in which an
    /// account's row can be held.
    /// </summary>
    public bool InTransaction { get; set; }

    /// <summary>
    /// Gets the accounts whose row was held, in the order they were.
    /// </summary>
    public IReadOnlyList<SubjectId> Held => _held;

    /// <summary>
    /// Restricts an account's processing.
    /// </summary>
    /// <param name="subject">The account.</param>
    public void Restrict(SubjectId subject) => _restricted.Add(subject);

    /// <inheritdoc/>
    public ValueTask<bool> IsRestrictedAsync(SubjectId subject, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_restricted.Contains(subject));

    /// <inheritdoc/>
    public ValueTask<bool?> HoldAsync(SubjectId subject, CancellationToken cancellationToken)
    {
        if (!InTransaction)
        {
            return ValueTask.FromResult<bool?>(null);
        }

        _held.Add(subject);

        return ValueTask.FromResult<bool?>(_restricted.Contains(subject));
    }
}
