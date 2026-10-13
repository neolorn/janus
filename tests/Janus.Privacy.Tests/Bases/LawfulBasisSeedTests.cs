using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Bases;
using Xunit;

namespace Janus.Privacy.Tests.Bases;

/// <summary>
/// The write of the declared lawful bases at the start (PRIV-BASIS-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class LawfulBasisSeedTests : IAsyncDisposable
{
    private readonly LawfulBasisStoreInMemory _bases = new();
    private readonly UnitOfWorkInMemory _work = new();

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _work.DisposeAsync();

    /// <summary>
    /// PRIV-BASIS-001 AC6: the start writes the declared list whole, each basis with its
    /// label and flags, in one transaction it commits.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_BASIS_001_AC6_TheStartWritesTheDeclaredListInOneTransactionAsync()
    {
        AuthorizationDeclaration declaration = Declaration.Declared().Build();

        await new LawfulBasisSeed(_bases, declaration, _work).SeededAsync(CancellationToken.None);

        Assert.Equal(declaration.LawfulBases, _bases.Held);
        Assert.Equal(1, _bases.Replaced);
        Assert.Equal((1, 1, 0), (_work.Opened, _work.Committed, _work.RolledBack));
    }

    /// <summary>
    /// CONV-DESIGN-003 AC7: the start returns no result, so a transaction that will not
    /// begin, or will not commit, is thrown as a fault naming the failure's code.
    /// </summary>
    /// <param name="atCommit">Whether the commit, and not the beginning, fails.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CONV_DESIGN_003_AC7_ATransactionThatFailsIsAFaultNamingItsCodeAsync(bool atCommit)
    {
        if (atCommit)
        {
            _work.RefusesCommit = Error.From(ErrorCodes.SystemFault);
        }
        else
        {
            _work.RefusesBegin = Error.From(ErrorCodes.SystemFault);
        }

        InvalidOperationException fault = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await new LawfulBasisSeed(_bases, Declaration.Declared().Build(), _work)
                .SeededAsync(CancellationToken.None));

        Assert.Equal(ErrorCodes.SystemFault.ToString(), fault.Message);
        Assert.Equal(atCommit ? 1 : 0, _bases.Replaced);
    }
}
