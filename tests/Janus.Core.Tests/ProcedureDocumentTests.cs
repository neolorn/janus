using System;
using System.IO;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// The criteria that are about an operator-facing document rather than a behaviour:
/// the procedure exists, and the documents state the rule the item states
/// (PRIV-BREACH-001, PRIV-MINOR-002). A test is what keeps a later edit from quietly
/// taking the rule out.
/// </summary>
[Trait("kind", "unit")]
public sealed class ProcedureDocumentTests
{
    private const string Runbook = "docs/spec/11-runbook.md";

    private const string Takedown = "docs/spec/14-takedown-procedure.md";

    /// <summary>
    /// PRIV-BREACH-001 AC4: both operator documents state the clock the item states,
    /// with both deadlines and the instant they run from.
    /// </summary>
    [Fact]
    public void PRIV_BREACH_001_AC4_TheOperatorDocumentsStateTheDetectionClock()
    {
        string runbook = Repository.ReadText(Runbook);

        Assert.Contains("Clock starts at detection", runbook, StringComparison.Ordinal);
        Assert.Contains("72 hours to the regulator", runbook, StringComparison.Ordinal);
        Assert.Contains("three days to affected", runbook, StringComparison.Ordinal);

        Assert.Contains(
            "breach clock starts at detection",
            Repository.ReadText(Takedown),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// PRIV-BREACH-001 AC3: neither document lets the clock wait for the conclusion
    /// that an incident is notifiable, so no procedure defers its start.
    /// </summary>
    [Fact]
    public void PRIV_BREACH_001_AC3_NoProcedureDefersTheClockPendingAssessment()
    {
        Assert.Contains(
            "Not deferrable pending investigation",
            Repository.ReadText(Runbook),
            StringComparison.Ordinal);

        Assert.Contains(
            "not deferrable\npending investigation",
            Repository.ReadText(Takedown).ReplaceLineEndings("\n"),
            StringComparison.Ordinal);
    }

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
    /// PRIV-MINOR-002 AC1: the takedown procedure exists as the item names it.
    /// </summary>
    [Fact]
    public void PRIV_MINOR_002_AC1_TheTakedownProcedureExists()
    {
        Assert.True(File.Exists(Path.Combine(Repository.Root, Takedown)));
        Assert.NotEmpty(Repository.ReadText(Takedown));
    }
}
