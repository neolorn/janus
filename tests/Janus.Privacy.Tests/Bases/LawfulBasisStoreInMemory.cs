using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Bases;

namespace Janus.Privacy.Tests.Bases;

/// <summary>
/// The table of lawful bases, in a list.
/// </summary>
internal sealed class LawfulBasisStoreInMemory : ILawfulBasisStore
{
    /// <summary>
    /// What the table holds.
    /// </summary>
    public IReadOnlyList<LawfulBasisDeclaration> Held { get; private set; } = [];

    /// <summary>
    /// How many times the table was written whole.
    /// </summary>
    public int Replaced { get; private set; }

    /// <inheritdoc/>
    public ValueTask ReplaceAsync(IReadOnlyList<LawfulBasisDeclaration> declared, CancellationToken cancellationToken)
    {
        Held = [.. declared];
        Replaced++;

        return ValueTask.CompletedTask;
    }
}
