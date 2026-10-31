using System;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// The criteria that are about an operator-facing document rather than a behaviour:
/// the documents state the rule the item states (PRIV-BREACH-001, PRIV-BREACH-003,
/// INT-MAIL-005). A test is what keeps a later edit from quietly taking the rule out.
/// </summary>
[Trait("kind", "unit")]
public sealed class ProcedureDocumentTests
{
    private const string Runbook = "docs/spec/11-runbook.md";

    private const string Risks = "docs/spec/13-risk-register.md";

    /// <summary>
    /// PRIV-BREACH-001 AC3: the runbook does not let the clock wait for the conclusion
    /// that an incident is notifiable, so no procedure defers its start.
    /// </summary>
    [Fact]
    public void PRIV_BREACH_001_AC3_NoProcedureDefersTheClockPendingAssessment() =>
        Assert.Contains(
            "Not deferrable pending investigation",
            Repository.ReadText(Runbook),
            StringComparison.Ordinal);

    /// <summary>
    /// PRIV-BREACH-001 AC2: the clock the operator runs starts at detection, and the
    /// procedure has them record the timeline, so what was known when is tracked
    /// rather than reconstructed afterwards.
    /// </summary>
    [Fact]
    public void PRIV_BREACH_001_AC2_TheClockStartsAtDetectionAndTheTimelineIsRecorded()
    {
        string runbook = Repository.ReadText(Runbook);

        Assert.Contains("Clock starts at detection", runbook, StringComparison.Ordinal);
        Assert.Contains("Record the timeline", runbook, StringComparison.Ordinal);

        Assert.Contains(
            "detection is the moment of the credible indication",
            runbook,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// PRIV-BREACH-003 AC1: the procedure the operator follows says the notice to
    /// subjects answers to no preference.
    /// </summary>
    [Fact]
    public void PRIV_BREACH_003_AC1_TheProcedureStatesTheNoticeIsNonSuppressible()
    {
        string runbook = Repository.ReadText(Runbook).ReplaceLineEndings("\n");

        Assert.Contains("**Non-suppressible**", runbook, StringComparison.Ordinal);
        Assert.Contains("no preference\n   silences it", runbook, StringComparison.Ordinal);
    }

    /// <summary>
    /// INT-MAIL-005 AC2: the risk register records, as R-A02, that staff mail is
    /// reached with an app password and not a phishing-resistant factor.
    /// </summary>
    [Fact]
    public void INT_MAIL_005_AC2_TheLimitationIsRecordedAsRA02()
    {
        string register = Repository.ReadText(Risks);

        Assert.Contains(
            "### R-A02 · Staff mail is reachable without a phishing-resistant factor",
            register,
            StringComparison.Ordinal);
        Assert.Contains("INT-MAIL-005", register, StringComparison.Ordinal);
        Assert.Contains("App passwords", register, StringComparison.Ordinal);
    }
}
