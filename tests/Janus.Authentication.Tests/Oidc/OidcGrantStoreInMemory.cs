using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;
using Janus.Core;

namespace Janus.Authentication.Tests.Oidc;

/// <summary>
/// The provider's grants revoked, held in memory, each with whether a unit of work was
/// open when it was revoked; a revocation inside one that does not commit is forgotten,
/// as the database forgets it. A grant held records the account it was issued to.
/// </summary>
/// <param name="work">The unit of work the operations under test run in.</param>
internal sealed class OidcGrantStoreInMemory(UnitOfWorkInMemory work) : IOidcGrantStore
{
    private readonly List<(string Grant, bool InsideUnitOfWork)> _revoked = [];

    private readonly Dictionary<string, SubjectId> _held = new(StringComparer.Ordinal);

    /// <summary>
    /// The grants revoked, oldest first.
    /// </summary>
    public IReadOnlyList<(string Grant, bool InsideUnitOfWork)> Revoked => _revoked;

    /// <summary>
    /// Holds a grant issued to an account.
    /// </summary>
    /// <param name="grant">The provider's identifier of the grant.</param>
    /// <param name="subject">The account it records.</param>
    public void Hold(string grant, SubjectId subject) => _held[grant] = subject;

    /// <inheritdoc/>
    public ValueTask<SubjectId?> SubjectAsync(string grant, CancellationToken cancellationToken) =>
        ValueTask.FromResult<SubjectId?>(_held.TryGetValue(grant, out SubjectId subject) ? subject : null);

    /// <inheritdoc/>
    public ValueTask RevokeAsync(string grant, CancellationToken cancellationToken)
    {
        int stood = _revoked.Count;

        work.Undoing(() => _revoked.RemoveRange(stood, _revoked.Count - stood));
        _revoked.Add((grant, work.Open));

        return ValueTask.CompletedTask;
    }
}
