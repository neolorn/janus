using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Privacy.Bases;

/// <summary>
/// Writes the declared lawful bases into their table as the application starts.
/// </summary>
/// <param name="bases">Where the table is.</param>
/// <param name="declaration">What the host declared.</param>
/// <param name="work">The one transaction the write runs in.</param>
/// <remarks>
/// Implements PRIV-BASIS-001 and CONV-ENUM-001. The code and the records of processing
/// read the declaration; the table is the declared list at rest, written whole in one
/// transaction that holds the table, so of two starts the later one's list stands.
/// </remarks>
internal sealed class LawfulBasisSeed(
    ILawfulBasisStore bases,
    AuthorizationDeclaration declaration,
    IUnitOfWork work)
{
    /// <summary>
    /// Leaves the table holding exactly the declared bases.
    /// </summary>
    /// <param name="cancellationToken">Abandons the start.</param>
    /// <returns>The work of writing them.</returns>
    /// <exception cref="InvalidOperationException">
    /// The transaction could not begin or commit, which is a fault naming the code.
    /// </exception>
    public async ValueTask SeededAsync(CancellationToken cancellationToken)
    {
        (await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Switch(() => { }, error => throw new InvalidOperationException(error.Code.ToString()));

        await bases.ReplaceAsync(declaration.LawfulBases, cancellationToken).ConfigureAwait(false);

        (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Switch(() => { }, error => throw new InvalidOperationException(error.Code.ToString()));
    }
}
