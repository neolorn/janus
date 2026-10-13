using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Janus.Core;
using Xunit;

namespace Janus.Conformance.Tests;

/// <summary>
/// What a row of a host's truth table may state (LIB-TEST-001, chapter 10 section 5.30).
/// </summary>
[Trait("kind", "unit")]
public sealed class TruthTableCaseTests
{
    private static readonly Permission Amend = Permission.Parse("sheet:amend");

    private static readonly JsonSerializerOptions Named = new() { Converters = { new JsonStringEnumConverter() } };

    /// <summary>
    /// LIB-TEST-001 AC2: a step-up case whose scenario leaves the gate unmet and that
    /// states the action allowed is refused where it is made, the refusal naming the
    /// scenario.
    /// </summary>
    /// <param name="scenario">The scenario.</param>
    [Theory]
    [InlineData(TruthTableScenario.StepUpLevelUnmet)]
    [InlineData(TruthTableScenario.StepUpPhishingResistanceUnmet)]
    [InlineData(TruthTableScenario.StepUpAgeUnmet)]
    [InlineData(TruthTableScenario.StepUpInstantFuture)]
    [InlineData(TruthTableScenario.StepUpProviderFailed)]
    [InlineData(TruthTableScenario.StepUpProviderAbsent)]
    public void LIB_TEST_001_AC2_AnUnmetStepUpCaseStatedAllowedIsRefusedNamingTheScenario(TruthTableScenario scenario)
    {
        ArgumentException refused = Assert.Throws<ArgumentException>(
            () => new TruthTableCase(scenario, Amend, Allowed: true));

        Assert.Contains(scenario.ToString(), refused.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// LIB-TEST-001 AC2: the case of a gate met that states the action refused is
    /// refused where it is made, the refusal naming the scenario.
    /// </summary>
    [Fact]
    public void LIB_TEST_001_AC2_AMetStepUpCaseStatedRefusedIsRefusedNamingTheScenario()
    {
        ArgumentException refused = Assert.Throws<ArgumentException>(
            () => new TruthTableCase(TruthTableScenario.StepUpMet, Amend, Allowed: false));

        Assert.Contains(nameof(TruthTableScenario.StepUpMet), refused.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// LIB-TEST-001 AC2, chapter 10 section 5.30: each step-up case states the one
    /// outcome its scenario has, allowed for a gate met and for no other.
    /// </summary>
    /// <param name="scenario">The scenario.</param>
    /// <param name="allowed">The outcome it has.</param>
    [Theory]
    [InlineData(TruthTableScenario.StepUpMet, true)]
    [InlineData(TruthTableScenario.StepUpLevelUnmet, false)]
    [InlineData(TruthTableScenario.StepUpPhishingResistanceUnmet, false)]
    [InlineData(TruthTableScenario.StepUpAgeUnmet, false)]
    [InlineData(TruthTableScenario.StepUpInstantFuture, false)]
    [InlineData(TruthTableScenario.StepUpProviderFailed, false)]
    [InlineData(TruthTableScenario.StepUpProviderAbsent, false)]
    public void LIB_TEST_001_AC2_AStepUpCaseStatesTheOutcomeItsScenarioHas(TruthTableScenario scenario, bool allowed)
    {
        var stated = new TruthTableCase(scenario, Amend, allowed);

        Assert.Equal((scenario, Amend, allowed), (stated.Scenario, stated.Permission, stated.Allowed));
    }

    /// <summary>
    /// AUTHZ-TEST-001: a case of any other scenario states either outcome, since the
    /// table is the host's statement of its policy.
    /// </summary>
    /// <param name="allowed">The outcome stated.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Construct_ACaseOfAnotherScenario_StatesEitherOutcome(bool allowed)
    {
        var stated = new TruthTableCase(TruthTableScenario.GrantOnRecord, Amend, allowed);

        Assert.Equal(allowed, stated.Allowed);
    }

    /// <summary>
    /// Chapter 10 section 5.30: the seven step-up scenarios are written in a finding as
    /// the reference spells them.
    /// </summary>
    /// <param name="scenario">The scenario.</param>
    /// <param name="written">Its name in the reference.</param>
    [Theory]
    [InlineData(TruthTableScenario.StepUpMet, "stepup-met")]
    [InlineData(TruthTableScenario.StepUpLevelUnmet, "stepup-level-unmet")]
    [InlineData(TruthTableScenario.StepUpPhishingResistanceUnmet, "stepup-phishingresistance-unmet")]
    [InlineData(TruthTableScenario.StepUpAgeUnmet, "stepup-age-unmet")]
    [InlineData(TruthTableScenario.StepUpInstantFuture, "stepup-instant-future")]
    [InlineData(TruthTableScenario.StepUpProviderFailed, "stepup-provider-failed")]
    [InlineData(TruthTableScenario.StepUpProviderAbsent, "stepup-provider-absent")]
    public void LIB_TEST_001_AC2_EachStepUpScenarioIsWrittenAsTheReferenceSpellsIt(
        TruthTableScenario scenario,
        string written)
    {
        string? named = JsonSerializer.SerializeToElement(scenario, Named).GetString();

        Assert.Equal(written, named);
    }
}
