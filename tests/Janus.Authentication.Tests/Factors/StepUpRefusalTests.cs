using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;
using Janus.Authentication.Factors;
using Janus.Authentication.Sessions;
using Janus.Core;
using Xunit;

namespace Janus.Authentication.Tests.Factors;

/// <summary>
/// What the refusal of an unmet gate carries of the gate's three values (BFF-STEP-001,
/// chapter 09 <c>POST /auth/step-up</c>, chapter 10 <c>auth.stepup.required</c>).
/// </summary>
[Trait("kind", "unit")]
public sealed class StepUpRefusalTests : IDisposable
{
    private static readonly DateTimeOffset Noon =
        new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Recency = TimeSpan.FromMinutes(15);

    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    /// <inheritdoc/>
    public void Dispose() => _randomness.Dispose();

    /// <summary>
    /// BFF-STEP-001 AC1, AUTH-STEP-007: a gate whose level is <c>delegated</c> asks no
    /// maximum age, so the refusal of one, which only a session downgraded since
    /// meets, carries <c>maxAge</c> null beside the level and the phishing-resistance
    /// requirement.
    /// </summary>
    [Fact]
    public void BFF_STEP_001_AC1_TheRefusalOfAGateThatAsksNoMaximumAgeCarriesNone()
    {
        Session downgraded = Signed(AssuranceLevel.Delegated);

        downgraded.Downgrade(Noon);

        JsonElement required = StepUpRefusal
            .Of(StepUp.ToEnrol(
                downgraded,
                new Gate(GateLevel.Reachable, PhishingResistant: false, Recency),
                Held(Factor.Google),
                Factor.Password,
                Noon + TimeSpan.FromMinutes(1)))
            .Details["required"];

        Assert.Equal("delegated", required.GetProperty("level").GetString());
        Assert.False(required.GetProperty("phishingResistant").GetBoolean());
        Assert.Equal(JsonValueKind.Null, required.GetProperty("maxAge").ValueKind);
    }

    /// <summary>
    /// BFF-STEP-001 AC1: the refusal of a gate at any level above <c>delegated</c>
    /// carries the gate's maximum age in whole seconds.
    /// </summary>
    [Fact]
    public void BFF_STEP_001_AC1_TheRefusalOfAGateThatAsksAMaximumAgeCarriesItInWholeSeconds()
    {
        JsonElement required = StepUpRefusal
            .Of(StepUp.On(
                Signed(AssuranceLevel.Aal1),
                new Gate(GateLevel.Aal1, PhishingResistant: false, Recency),
                Held(Factor.Password),
                Noon + Recency + TimeSpan.FromSeconds(1)))
            .Details["required"];

        Assert.Equal("aal1", required.GetProperty("level").GetString());
        Assert.Equal(900, required.GetProperty("maxAge").GetInt64());
    }

    private static HeldFactors Held(params Factor[] factors)
    {
        HashSet<Factor> held = [.. factors];

        return new HeldFactors(held, held, null);
    }

    private Session Signed(AssuranceLevel reached) =>
        Session.Begin(
            SessionId.New(TimeProvider.System),
            SubjectId.New(_randomness),
            new Assurance(reached, PhishingResistant: false),
            new SessionOrigin("198.51.100.7", new DeviceDescription("Firefox", "Linux")),
            Noon,
            TimeSpan.FromDays(1),
            TimeSpan.FromDays(30),
            breakGlassReason: null);
}
