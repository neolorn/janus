using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;

namespace Janus.Core;

/// <summary>
/// The two policies chapter 10 section 4.1a states: the system policy every principal
/// with no membership resolves to, and the one the administrative organization is
/// created with at bootstrap.
/// </summary>
/// <remarks>Implements chapter 10 section 4.1a, AUTH-PRIN-002, AUTH-STEP-002a.</remarks>
public static class Policies
{
    // The maximum age of both default gates is session.stepup.recency (section 4.1a),
    // so the value sits here once and the catalogue reads it for that key.
    internal static TimeSpan StepUpRecency { get; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// The system policy, at the safe end of every field: one factor, the catalogue
    /// less the four that are off by default, a gate at the account's reachable
    /// assurance, a second credential offered rather than required, self-service
    /// recovery available, and no domain lock.
    /// </summary>
    public static Policy SystemDefault { get; } = new(
        AssuranceLevel.Aal1,
        new[]
        {
            Factor.Password,
            Factor.Passkey,
            Factor.Google,
            Factor.Apple,
            Factor.Totp,
            Factor.SecurityKey,
            Factor.RecoveryCodes,
        }.ToFrozenSet(),
        GatesAt(GateLevel.Reachable, phishingResistant: false),
        CredentialRedundancy.Advisory,
        SelfServiceRecovery: true,
        []);

    /// <summary>
    /// The administrative organization's policy at bootstrap: two factors, passkeys
    /// only, every gate at AAL2 and phishing-resistant, a second credential required,
    /// and no self-service recovery.
    /// </summary>
    public static Policy AdministrativeOrganization { get; } = new(
        AssuranceLevel.Aal2,
        new[] { Factor.Passkey }.ToFrozenSet(),
        GatesAt(GateLevel.Aal2, phishingResistant: true),
        CredentialRedundancy.Enforced,
        SelfServiceRecovery: false,
        []);

    // Section 4.1a states one gate for every action of section 5a rather than a gate
    // per action.
    private static FrozenDictionary<StepUpAction, Gate> GatesAt(GateLevel level, bool phishingResistant)
    {
        var gate = new Gate(level, phishingResistant, StepUpRecency);

        return Enum.GetValues<StepUpAction>().ToFrozenDictionary(action => action, _ => gate);
    }
}
