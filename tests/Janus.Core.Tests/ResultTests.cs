using System;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// The outcome of an operation that carries no value (CONV-DESIGN-005, CONV-ERR-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class ResultTests
{
    /// <summary>
    /// A successful outcome runs the success branch.
    /// </summary>
    [Fact]
    public void Match_Success_RunsTheSuccessBranch()
    {
        var outcome = Result.Success();

        string branch = outcome.Match(() => "success", failure => failure.Code.ToString());

        Assert.Equal("success", branch);
    }

    /// <summary>
    /// A failed outcome runs the failure branch and hands it the failure.
    /// </summary>
    [Fact]
    public void Match_Failure_RunsTheFailureBranchWithTheFailure()
    {
        var outcome = Result.Failure(Error.From(ErrorCodes.ConfigurationKeyProtected));

        string branch = outcome.Match(() => "success", failure => failure.Code.ToString());

        Assert.Equal("config.key.protected", branch);
    }

    /// <summary>
    /// A successful outcome runs the success action.
    /// </summary>
    [Fact]
    public void Switch_Success_RunsTheSuccessBranch()
    {
        var outcome = Result.Success();
        string ran = string.Empty;

        outcome.Switch(() => ran = "success", failure => ran = failure.Code.ToString());

        Assert.Equal("success", ran);
    }

    /// <summary>
    /// A failed outcome runs the failure action and hands it the failure.
    /// </summary>
    [Fact]
    public void Switch_Failure_RunsTheFailureBranchWithTheFailure()
    {
        var outcome = Result.Failure(Error.From(ErrorCodes.ConfigurationValueAboveCeiling));
        string ran = string.Empty;

        outcome.Switch(() => ran = "success", failure => ran = failure.Code.ToString());

        Assert.Equal("config.value.aboveceiling", ran);
    }

    /// <summary>
    /// An outcome that was never set is neither a success nor a failure: reading it is
    /// the fail-open case, so it throws rather than answering.
    /// </summary>
    [Fact]
    public void Match_OutcomeNeverSet_Throws()
    {
        var outcome = default(Result);

        Assert.Throws<InvalidOperationException>(
            () => outcome.Match(() => "success", failure => failure.Code.ToString()));
    }

    /// <summary>
    /// A failure without a failure to carry is a fault.
    /// </summary>
    [Fact]
    public void Failure_AbsentFailure_Refused() =>
        Assert.Throws<ArgumentNullException>(() => Result.Failure(null!));

    /// <summary>
    /// Both branches are required; handing one of them absent is a fault.
    /// </summary>
    [Fact]
    public void Match_AbsentBranch_Refused()
    {
        var outcome = Result.Success();

        Assert.Throws<ArgumentNullException>(() => outcome.Match(() => "success", null!));
        Assert.Throws<ArgumentNullException>(() => outcome.Match<string>(null!, failure => "failure"));
    }
}
