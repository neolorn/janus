using System;
using System.Collections.Generic;
using Janus.Core;

namespace Janus.Authentication.Policies;

/// <summary>
/// The run-up an account gets when the policy over it is raised, and what happens at
/// a sign-in before and after it ends.
/// </summary>
/// <remarks>
/// Implements AUTH-FACT-017 and AUTH-SESS-009. A run-up of nothing is the safe
/// default: an organization that raises its floor gets the floor at once unless it
/// asked for time to announce the change.
/// </remarks>
internal static class PolicyGrace
{
    /// <summary>
    /// What a change to a policy raised, which is nothing where it lowered or left
    /// both fields alone.
    /// </summary>
    /// <param name="before">The policy as it stood.</param>
    /// <param name="after">The policy as it now stands.</param>
    /// <param name="at">When it changed.</param>
    /// <returns>The requirements raised, in the order the chapter states them.</returns>
    /// <exception cref="ArgumentNullException">A policy is absent.</exception>
    public static IReadOnlyList<PolicyRaise> Raised(Policy before, Policy after, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        List<PolicyRaise> raised = [];

        if (after.RequiredAssurance > before.RequiredAssurance)
        {
            raised.Add(new PolicyRaise(
                PolicyField.RequiredAssurance,
                Name(after.RequiredAssurance),
                at));
        }

        if (after.CredentialRedundancy > before.CredentialRedundancy)
        {
            raised.Add(new PolicyRaise(
                PolicyField.CredentialRedundancy,
                Name(after.CredentialRedundancy),
                at));
        }

        return raised;
    }

    /// <summary>
    /// What a raised requirement holds over a sign-in: nothing where the account
    /// meets it, the requirement and its deadline where it does not.
    /// </summary>
    /// <param name="raise">What was raised, and when.</param>
    /// <param name="grace">The run-up the deployment allows.</param>
    /// <param name="complies">Whether the account already meets the requirement.</param>
    /// <param name="registered">When the account came into being.</param>
    /// <param name="now">The instant of the sign-in.</param>
    /// <returns>
    /// What the sign-in is told, or nothing where the account meets the requirement.
    /// </returns>
    /// <exception cref="ArgumentNullException">The raise is absent.</exception>
    public static PolicyHold? On(
        PolicyRaise raise,
        TimeSpan grace,
        bool complies,
        DateTimeOffset registered,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(raise);

        if (complies)
        {
            return null;
        }

        // The run-up exists so that people already under a policy can enrol on their
        // own schedule. An account created after the change was created under the
        // new requirement and is held at its first sign-in (AUTH-FACT-017).
        DateTimeOffset deadline = registered > raise.At ? raise.At : raise.At + grace;

        return new PolicyHold(
            new PolicyRequirement(raise.Field, raise.Value, deadline),
            now >= deadline);
    }

    private static string Name(AssuranceLevel level) => level switch
    {
        AssuranceLevel.Aal1 => "aal1",
        AssuranceLevel.Aal2 => "aal2",
        AssuranceLevel.Aal3 => "aal3",
        AssuranceLevel.Delegated => "delegated",
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, "The tier is not one of chapter 10 section 5.4."),
    };

    private static string Name(CredentialRedundancy redundancy) =>
        redundancy is Core.CredentialRedundancy.Enforced ? "enforced" : "advisory";
}
