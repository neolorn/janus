using System;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// The outcome of an operation that carries a value (CONV-DESIGN-005, CONV-ERR-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class ResultOfTTests
{
    /// <summary>
    /// A successful outcome hands the value to the success branch.
    /// </summary>
    [Fact]
    public void Match_Success_RunsTheSuccessBranchWithTheValue()
    {
        var outcome = Result.Success(42);

        string branch = outcome.Match(value => value.ToString(System.Globalization.CultureInfo.InvariantCulture), failure => failure.Code.ToString());

        Assert.Equal("42", branch);
    }

    /// <summary>
    /// A failed outcome runs the failure branch and hands it the failure.
    /// </summary>
    [Fact]
    public void Match_Failure_RunsTheFailureBranchWithTheFailure()
    {
        var outcome = Result.Failure<int>(Error.From(ErrorCodes.ConfigurationPolicyBelowSystem));

        string branch = outcome.Match(value => "success", failure => failure.Code.ToString());

        Assert.Equal("config.policy.belowsystem", branch);
    }

    /// <summary>
    /// A successful outcome hands the value to the success action.
    /// </summary>
    [Fact]
    public void Switch_Success_RunsTheSuccessBranchWithTheValue()
    {
        var outcome = Result.Success("value");
        string ran = string.Empty;

        outcome.Switch(value => ran = value, failure => ran = failure.Code.ToString());

        Assert.Equal("value", ran);
    }

    /// <summary>
    /// A failed outcome runs the failure action and hands it the failure.
    /// </summary>
    [Fact]
    public void Switch_Failure_RunsTheFailureBranchWithTheFailure()
    {
        var outcome = Result.Failure<string>(Error.From(ErrorCodes.ConfigurationChangeStepUpRequired));
        string ran = string.Empty;

        outcome.Switch(value => ran = value, failure => ran = failure.Code.ToString());

        Assert.Equal("config.change.stepuprequired", ran);
    }

    /// <summary>
    /// An outcome that was never set throws rather than answering, for the reason the
    /// valueless outcome gives.
    /// </summary>
    [Fact]
    public void Match_OutcomeNeverSet_Throws()
    {
        var outcome = default(Result<string>);

        Assert.Throws<InvalidOperationException>(
            () => outcome.Match(value => value, failure => failure.Code.ToString()));
    }

    /// <summary>
    /// A failure without a failure to carry is a fault.
    /// </summary>
    [Fact]
    public void Failure_AbsentFailure_Refused() =>
        Assert.Throws<ArgumentNullException>(() => Result.Failure<string>(null!));
}
