using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Core;
using Janus.Storage.Authentication.Factors;
using Janus.Storage.Settings;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// The verification codes over the <c>verification_codes</c> table, where two tries
/// made at once meet on the row they decide on (AUTH-FACT-004).
/// </summary>
[Trait("kind", "integration")]
public sealed class VerificationCodesTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    /// <summary>
    /// AUTH-FACT-004 AC3 and AC4: ten wrong codes presented at once against one code,
    /// with <c>code.verification.attempts</c> at its default of 5, are counted as ten
    /// tries made one after another: five are refused as wrong, the five after the cap as
    /// expired, and the right code after them is refused.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_004_AC3_ConcurrentWrongTriesAreAllCountedAsync()
    {
        byte[] holder = Holder();
        string right = await IssuedAsync(holder);

        ErrorCode?[] answers = await Task.WhenAll(
            Enumerable.Range(0, 10).Select(_ => PresentedAsync(holder, Wrong(right))));

        Assert.Equal(5, answers.Count(answer => answer == ErrorCodes.CodeInvalid));
        Assert.Equal(5, answers.Count(answer => answer == ErrorCodes.CodeExpired));
        Assert.Equal(ErrorCodes.CodeExpired, await PresentedAsync(holder, right));
    }

    /// <summary>
    /// AUTH-FACT-004 AC4: the right code presented twice at once answers once, and the
    /// other presentation is refused as a code already spent.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_004_AC3_TwoConcurrentRightTriesSucceedOnceAsync()
    {
        byte[] holder = Holder();
        string right = await IssuedAsync(holder);

        ErrorCode?[] answers = await Task.WhenAll(PresentedAsync(holder, right), PresentedAsync(holder, right));

        Assert.Equal(1, answers.Count(answer => answer is null));
        Assert.Equal(1, answers.Count(answer => answer == ErrorCodes.CodeExpired));
    }

    /// <inheritdoc/>
    public void Dispose() => _randomness.Dispose();

    // Any six digits other than the ones outstanding.
    private static string Wrong(string right) =>
        string.Equals(right, "000000", StringComparison.Ordinal) ? "111111" : "000000";

    // A holder no other test issues against.
    private static byte[] Holder() => RandomNumberGenerator.GetBytes(32);

    private async Task<string> IssuedAsync(byte[] holder)
    {
        await using StoreContext context = database.Context();
        await using var work = new UnitOfWork(context);

        return (await Codes(context, work).IssueAsync(holder, TestContext.Current.CancellationToken))
            .Match(code => code, error => throw new Xunit.Sdk.XunitException("The code was not issued: " + error.Code));
    }

    // Each presentation is its own request: its own context, connection and transaction,
    // which its caller begins and commits as the operation presenting a code does.
    private async Task<ErrorCode?> PresentedAsync(byte[] holder, string entered)
    {
        await using StoreContext context = database.Context();
        await using var work = new UnitOfWork(context);

        (await work.BeginAsync(TestContext.Current.CancellationToken))
            .Switch(() => { }, error => throw new Xunit.Sdk.XunitException("No unit of work began: " + error.Code));

        Result presented = await Codes(context, work)
            .PresentAsync(holder, entered, TestContext.Current.CancellationToken);

        (await work.CommitAsync(TestContext.Current.CancellationToken))
            .Switch(() => { }, error => throw new Xunit.Sdk.XunitException("Nothing was committed: " + error.Code));

        return presented.Match<ErrorCode?>(() => null, error => error.Code);
    }

    private VerificationCodes Codes(StoreContext context, UnitOfWork work)
    {
        var connections = new DataConnections(context);

        return new VerificationCodes(
            new VerificationCodeStore(context, connections),
            new ConfigurationStore(context, connections),
            work,
            TimeProvider.System,
            _randomness);
    }
}
