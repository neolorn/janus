using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;

namespace Janus.Authentication.Tests.Oidc;

/// <summary>
/// The provider's grants revoked, held in memory, each with whether a unit of work was
/// open when it was revoked; a revocation inside one that does not commit is forgotten,
/// as the database forgets it.
/// </summary>
/// <param name="work">The unit of work the operations under test run in.</param>
internal sealed class OidcGrantStoreInMemory(UnitOfWorkInMemory work) : IOidcGrantStore
{
    private readonly List<(string Grant, bool InsideUnitOfWork)> _revoked = [];

    /// <summary>
    /// The grants revoked, oldest first.
    /// </summary>
    public IReadOnlyList<(string Grant, bool InsideUnitOfWork)> Revoked => _revoked;

    /// <inheritdoc/>
    public ValueTask RevokeAsync(string grant, CancellationToken cancellationToken)
    {
        int stood = _revoked.Count;

        work.Undoing(() => _revoked.RemoveRange(stood, _revoked.Count - stood));
        _revoked.Add((grant, work.Open));

        return ValueTask.CompletedTask;
    }
}
