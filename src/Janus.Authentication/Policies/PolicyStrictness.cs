using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using Janus.Core;

namespace Janus.Authentication.Policies;

/// <summary>
/// How two policies combine. An organization may tighten a field and may not loosen
/// one below the system default, and a principal holding several memberships follows
/// the strictest of their organizations' policies.
/// </summary>
/// <remarks>Implements AUTH-PRIN-002 and AUTH-STEP-002a.</remarks>
internal static class PolicyStrictness
{
    /// <summary>
    /// The policy an organization's overrides produce over the system policy. A field
    /// the organization left absent inherits, and one that would loosen is ignored, so
    /// a value written before the system default rose cannot take effect.
    /// </summary>
    /// <param name="system">The system policy.</param>
    /// <param name="overrides">What the organization overrides.</param>
    /// <returns>The organization's policy.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static Policy Tighten(Policy system, PolicyOverride overrides)
    {
        ArgumentNullException.ThrowIfNull(system);
        ArgumentNullException.ThrowIfNull(overrides);

        return Strictest(
            system,
            new Policy(
                overrides.RequiredAssurance ?? system.RequiredAssurance,
                overrides.LoginFactors ?? system.LoginFactors,
                overrides.Gates ?? system.Gates,
                overrides.CredentialRedundancy ?? system.CredentialRedundancy,
                overrides.SelfServiceRecovery ?? system.SelfServiceRecovery,
                overrides.EmailDomains ?? system.EmailDomains));
    }

    /// <summary>
    /// The stricter of two policies, field by field.
    /// </summary>
    /// <param name="first">One policy.</param>
    /// <param name="second">The other.</param>
    /// <returns>The policy that grants least.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static Policy Strictest(Policy first, Policy second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        return new Policy(
            first.RequiredAssurance > second.RequiredAssurance
                ? first.RequiredAssurance
                : second.RequiredAssurance,
            first.LoginFactors.Intersect(second.LoginFactors).ToFrozenSet(),
            Strictest(first.Gates, second.Gates),
            first.CredentialRedundancy is CredentialRedundancy.Enforced
                || second.CredentialRedundancy is CredentialRedundancy.Enforced
                ? CredentialRedundancy.Enforced
                : CredentialRedundancy.Advisory,
            first.SelfServiceRecovery && second.SelfServiceRecovery,
            Locked(first.EmailDomains, second.EmailDomains));
    }

    /// <summary>
    /// How strict a gate's level is. A stated tier of one factor asks least; the
    /// account's reachable assurance asks at least that and never less, because it
    /// has that tier as its floor; a stated two factors asks most, since it holds an
    /// account that reaches only one factor at enrolment.
    /// </summary>
    /// <param name="level">The level.</param>
    /// <returns>Its rank, higher being stricter.</returns>
    public static int Rank(GateLevel level) => level switch
    {
        GateLevel.Aal1 => 0,
        GateLevel.Reachable => 1,
        _ => 2,
    };

    // A locked list admits only what it names, so any lock beats no lock and two
    // locks admit only what both name.
    private static IReadOnlyList<string> Locked(
        IReadOnlyList<string> first,
        IReadOnlyList<string> second)
    {
        if (first.Count == 0)
        {
            return second;
        }

        return second.Count == 0 ? first : [.. first.Intersect(second, StringComparer.Ordinal)];
    }

    private static FrozenDictionary<StepUpAction, Gate> Strictest(
        IReadOnlyDictionary<StepUpAction, Gate> first,
        IReadOnlyDictionary<StepUpAction, Gate> second) =>
        Enum.GetValues<StepUpAction>()
            .ToFrozenDictionary(
                action => action,
                action => Strictest(first[action], second[action]));

    private static Gate Strictest(Gate first, Gate second) =>
        new(
            Rank(first.Level) >= Rank(second.Level) ? first.Level : second.Level,
            first.PhishingResistant || second.PhishingResistant,
            first.MaximumAge <= second.MaximumAge ? first.MaximumAge : second.MaximumAge);
}
