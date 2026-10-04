using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Privacy.Bases;

/// <summary>
/// The table of lawful bases, which holds the list the host declared and nothing else.
/// </summary>
/// <remarks>Implements PRIV-BASIS-001 and CONV-DESIGN-003.</remarks>
internal interface ILawfulBasisStore
{
    /// <summary>
    /// Holds the table against every other writer for the length of the transaction,
    /// writes each declared basis by its key, and removes every row whose key the
    /// declaration does not hold.
    /// </summary>
    /// <param name="declared">The list the host declared.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of writing it.</returns>
    ValueTask ReplaceAsync(IReadOnlyList<LawfulBasisDeclaration> declared, CancellationToken cancellationToken);
}
