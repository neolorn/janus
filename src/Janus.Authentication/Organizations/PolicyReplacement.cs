using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Janus.Authentication.Policies;
using Janus.Core;

namespace Janus.Authentication.Organizations;

/// <summary>
/// What a replacement of an organization's policy makes of the policy in force: the
/// overrides that stood and those that replace them, the policies the members resolve
/// to before and after, and whether the change loosens.
/// </summary>
/// <param name="Before">The overrides that stood.</param>
/// <param name="After">The overrides that replace them, with the domain lock in force kept.</param>
/// <param name="Was">The policy the organization's members resolved to.</param>
/// <param name="Becomes">The policy they resolve to once the replacement is written.</param>
/// <param name="Loosening">Whether the members resolve to a policy granting more on any field.</param>
/// <remarks>
/// Implements AUTH-STEP-002a, AUTH-SESS-005b, OPS-CFG-002, chapter 09 section 8a and
/// D-248. A replacement is judged by one rule whether it comes through the policy route
/// or from the server; what the caller must hold to make a loosening is the route's own.
/// </remarks>
internal sealed record PolicyReplacement(
    PolicyOverride Before,
    PolicyOverride After,
    Policy Was,
    Policy Becomes,
    bool Loosening)
{
    /// <summary>
    /// Whether the replacement states anything other than what stood, field by field.
    /// </summary>
    public bool Changes =>
        Before.RequiredAssurance != After.RequiredAssurance
        || Before.CredentialRedundancy != After.CredentialRedundancy
        || Before.SelfServiceRecovery != After.SelfServiceRecovery
        || Before.Photos != After.Photos
        || Before.GrammaticalGender != After.GrammaticalGender
        || !Same(Before.LoginFactors, After.LoginFactors)
        || !Same(Before.Gates, After.Gates)
        || !Same(Before.EmailDomains, After.EmailDomains);

    /// <summary>
    /// Judges a replacement against the policy in force.
    /// </summary>
    /// <param name="system">The system policy in force.</param>
    /// <param name="before">The overrides the organization states now.</param>
    /// <param name="replacement">The overrides that would replace them, without the domain lock.</param>
    /// <param name="administrative">Whether the organization is the administrative one.</param>
    /// <returns>
    /// What the replacement makes of the policy, or the refusal: <c>config.policy.belowsystem</c>
    /// naming the first field looser than the system's, <c>config.value.belowfloor</c>
    /// naming <c>requiredAssurance</c> where the administrative organization's members
    /// would resolve below AAL2.
    /// </returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static Result<PolicyReplacement> Of(
        Policy system,
        PolicyOverride before,
        PolicyOverride replacement,
        bool administrative)
    {
        ArgumentNullException.ThrowIfNull(system);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(replacement);

        // Chapter 10 section 4.1a: the domain lock is written only through the domain
        // operations, so a replacement keeps the one in force.
        PolicyOverride after = replacement with { EmailDomains = before.EmailDomains };

        // AUTH-STEP-002a: an organization may tighten any field and may not loosen one
        // below the system policy.
        if (PolicyStrictness.BelowSystem(system, after) is string looser)
        {
            return Result.Failure<PolicyReplacement>(
                Error.From(ErrorCodes.ConfigurationPolicyBelowSystem, "field", JsonSerializer.SerializeToElement(looser)));
        }

        Policy was = PolicyStrictness.Tighten(system, before);
        Policy becomes = PolicyStrictness.Tighten(system, after);

        // AUTH-SESS-005b: the administrative organization is held to a stated floor of
        // AAL2, which no change of its policy takes it below.
        if (administrative && becomes.RequiredAssurance < AssuranceLevel.Aal2)
        {
            return Result.Failure<PolicyReplacement>(
                Error.From(
                    ErrorCodes.ConfigurationValueBelowFloor,
                    "field",
                    JsonSerializer.SerializeToElement("requiredAssurance")));
        }

        return Result.Success(new PolicyReplacement(before, after, was, becomes, PolicyStrictness.Loosens(was, becomes)));
    }

    private static bool Same<TValue>(IReadOnlySet<TValue>? first, IReadOnlySet<TValue>? second) =>
        first is null || second is null ? first is null && second is null : first.SetEquals(second);

    private static bool Same<TKey, TValue>(IReadOnlyDictionary<TKey, TValue>? first, IReadOnlyDictionary<TKey, TValue>? second)
        where TKey : notnull =>
        first is null || second is null
            ? first is null && second is null
            : first.Count == second.Count
                && first.All(entry => second.TryGetValue(entry.Key, out TValue? other) && Equals(entry.Value, other));

    private static bool Same(IReadOnlyList<string>? first, IReadOnlyList<string>? second) =>
        first is null || second is null ? first is null && second is null : first.SequenceEqual(second, StringComparer.Ordinal);
}
