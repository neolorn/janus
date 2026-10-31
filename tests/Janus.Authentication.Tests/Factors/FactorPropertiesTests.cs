using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Security.Cryptography;
using Janus.Authentication.Factors;
using Janus.Authentication.Sessions;
using Janus.Core;
using Xunit;

namespace Janus.Authentication.Tests.Factors;

/// <summary>
/// What the properties a factor is registered with decide, where no rule knows the
/// factor (LIB-EXT-002).
/// </summary>
[Trait("kind", "unit")]
public sealed class FactorPropertiesTests : IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    /// <inheritdoc/>
    public void Dispose() => _randomness.Dispose();

    /// <summary>
    /// LIB-EXT-002 AC1: a factor no entry of the catalogue describes, registered as
    /// resisting relay, reaches a phishing-resistant session by the arithmetic as it
    /// stands, and that session meets a gate that asks for phishing resistance by the
    /// rule as it stands. Registered as not resisting it, the same factor leaves the
    /// gate unmet.
    /// </summary>
    /// <param name="resistant">Whether the factor is registered as resisting relay.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LIB_EXT_002_AC1_ANewFactorRegisteredAsPhishingResistantMeetsTheGatesThatAskIt(bool resistant)
    {
        var registered = new FactorProperties(
            CanBePrimary: true,
            CanBeSecondFactor: true,
            IsPhishingResistant: resistant,
            AssuranceLevel.Aal2,
            VerificationOnly: false,
            SignInOnly: false,
            IsWebAuthn: false,
            IsDiscoverable: false,
            Channel: null,
            Restricted: false,
            SingleUse: false,
            OperatedDomains: FrozenSet<string>.Empty,
            OperatesHostedDomain: false);
        var gate = new Gate(GateLevel.Aal2, PhishingResistant: true, TimeSpan.FromMinutes(15));
        HashSet<Factor> held = [Factor.Passkey];

        Assurance? reached = Assurance.Reached([registered]);
        StepUpChallenge asked = StepUp.On(
            Session.Begin(
                SessionId.New(TimeProvider.System),
                SubjectId.New(_randomness),
                reached ?? default,
                new SessionOrigin("198.51.100.7", new DeviceDescription("Firefox", "Linux")),
                Noon,
                TimeSpan.FromDays(1),
                TimeSpan.FromDays(30),
                breakGlassReason: null),
            gate,
            new HeldFactors(held, held, null),
            Noon + TimeSpan.FromMinutes(1));

        Assert.Equal(new Assurance(AssuranceLevel.Aal2, resistant), reached);
        Assert.Equal(resistant, asked.Outcome is StepUpOutcome.Satisfied);
    }
}
