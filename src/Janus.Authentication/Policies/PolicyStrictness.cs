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
    /// a value written before the system default rose cannot take effect. The gates are
    /// overridden action by action: an action the organization does not name keeps the
    /// system's gate.
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
                Overridden(system.Gates, overrides.Gates),
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
    /// The first field an organization's overrides state looser than the system
    /// policy, which an organization may not do.
    /// </summary>
    /// <param name="system">The system policy.</param>
    /// <param name="overrides">What the organization would override.</param>
    /// <returns>The field's name as chapter 10 section 4.1a writes it, or nothing.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static string? BelowSystem(Policy system, PolicyOverride overrides)
    {
        ArgumentNullException.ThrowIfNull(system);
        ArgumentNullException.ThrowIfNull(overrides);

        if (overrides.RequiredAssurance is { } floor && floor < system.RequiredAssurance)
        {
            return "requiredAssurance";
        }

        if (overrides.LoginFactors is { } factors && !factors.IsSubsetOf(system.LoginFactors))
        {
            return "loginFactors";
        }

        if (overrides.Gates is { } gates
            && gates.Any(stated => Strictest(system.Gates[stated.Key], stated.Value) != stated.Value))
        {
            return "gates";
        }

        if (overrides.CredentialRedundancy is CredentialRedundancy.Advisory
            && system.CredentialRedundancy is CredentialRedundancy.Enforced)
        {
            return "credentialRedundancy";
        }

        return overrides.SelfServiceRecovery is true && !system.SelfServiceRecovery
            ? "selfServiceRecovery"
            : null;
    }

    /// <summary>
    /// Whether one policy grants anything another did not: a lower floor, another
    /// factor, a gate that asks less, redundancy advised where it was enforced,
    /// recovery offered where it was withdrawn, or a domain lock that admits more.
    /// </summary>
    /// <param name="before">What was in force.</param>
    /// <param name="after">What would be.</param>
    /// <returns>Whether the change is a loosening (OPS-CFG-002).</returns>
    /// <exception cref="ArgumentNullException">A policy is absent.</exception>
    public static bool Loosens(Policy before, Policy after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        Policy stricter = Strictest(before, after);

        return stricter.RequiredAssurance != after.RequiredAssurance
            || !stricter.LoginFactors.SetEquals(after.LoginFactors)
            || Weakened(stricter, after).Length > 0
            || stricter.CredentialRedundancy != after.CredentialRedundancy
            || stricter.SelfServiceRecovery != after.SelfServiceRecovery
            || !Admitting(stricter.EmailDomains).SetEquals(after.EmailDomains);
    }

    /// <summary>
    /// The step-up actions whose gate one policy asks less of than another: a lower
    /// level, phishing resistance no longer asked, or a longer maximum age.
    /// </summary>
    /// <param name="before">What was in force.</param>
    /// <param name="after">What would be.</param>
    /// <returns>The actions in their declared order, none where no gate asks less.</returns>
    /// <exception cref="ArgumentNullException">A policy is absent.</exception>
    public static IReadOnlyList<StepUpAction> WeakenedGates(Policy before, Policy after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        return Weakened(Strictest(before, after), after);
    }

    /// <summary>
    /// What of a resolved policy is the organization's own: a field, or a gate by
    /// action, the organization states whose value in force is the one it stated, or
    /// is not the system's.
    /// </summary>
    /// <param name="system">The system policy.</param>
    /// <param name="resolved">What the organization's members resolve to.</param>
    /// <param name="stated">What the organization overrides.</param>
    /// <returns>
    /// The value in force of each such field and gate; a field the system policy has
    /// since overtaken is absent, since the value in force is then the system's.
    /// </returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static PolicyOverride InForce(Policy system, Policy resolved, PolicyOverride stated)
    {
        ArgumentNullException.ThrowIfNull(system);
        ArgumentNullException.ThrowIfNull(resolved);
        ArgumentNullException.ThrowIfNull(stated);

        var gates = (stated.Gates ?? FrozenDictionary<StepUpAction, Gate>.Empty)
            .Where(bound => resolved.Gates[bound.Key] == bound.Value
                || resolved.Gates[bound.Key] != system.Gates[bound.Key])
            .ToFrozenDictionary(bound => bound.Key, bound => resolved.Gates[bound.Key]);

        return new PolicyOverride(
            stated.RequiredAssurance is { } floor
                && (floor == resolved.RequiredAssurance || resolved.RequiredAssurance != system.RequiredAssurance)
                ? resolved.RequiredAssurance
                : null,
            stated.LoginFactors is { } factors
                && (factors.SetEquals(resolved.LoginFactors) || !system.LoginFactors.SetEquals(resolved.LoginFactors))
                ? resolved.LoginFactors
                : null,
            gates.Count > 0 ? gates : null,
            stated.CredentialRedundancy is { } redundancy
                && (redundancy == resolved.CredentialRedundancy
                    || resolved.CredentialRedundancy != system.CredentialRedundancy)
                ? resolved.CredentialRedundancy
                : null,
            stated.SelfServiceRecovery is { } recovery
                && (recovery == resolved.SelfServiceRecovery
                    || resolved.SelfServiceRecovery != system.SelfServiceRecovery)
                ? resolved.SelfServiceRecovery
                : null,
            stated.EmailDomains is { } domains
                && (Admitting(domains).SetEquals(resolved.EmailDomains)
                    || !Admitting(system.EmailDomains).SetEquals(resolved.EmailDomains))
                ? resolved.EmailDomains
                : null);
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

    // A gate of the policy in force that the stricter of the two asks more of.
    private static StepUpAction[] Weakened(Policy stricter, Policy after) =>
        [.. Enum.GetValues<StepUpAction>().Where(action => stricter.Gates[action] != after.Gates[action])];

    // Chapter 10 section 4.1a: an organization stores only what it overrides, so its
    // gates name the actions it tightens and no others.
    private static FrozenDictionary<StepUpAction, Gate> Overridden(
        IReadOnlyDictionary<StepUpAction, Gate> system,
        IReadOnlyDictionary<StepUpAction, Gate>? overrides) =>
        Enum.GetValues<StepUpAction>()
            .ToFrozenDictionary(
                action => action,
                action => overrides is not null && overrides.TryGetValue(action, out Gate? stated)
                    ? stated
                    : system[action]);

    // Which domains a lock names, whatever order it names them in.
    private static HashSet<string> Admitting(IEnumerable<string> domains) => new(domains, StringComparer.Ordinal);

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
