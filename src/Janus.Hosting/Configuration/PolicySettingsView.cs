using System;
using System.Collections.Generic;
using System.Linq;
using Janus.Core;

namespace Janus.Hosting.Configuration;

/// <summary>
/// The part of the policy in force a frontend renders from, and nothing else of it.
/// </summary>
/// <param name="RequiredAssurance">The assurance the policy requires.</param>
/// <param name="LoginFactors">The factors it admits.</param>
/// <param name="Photos">Whether it admits a profile photo.</param>
/// <param name="GrammaticalGender">Whether it admits the grammatical gender (D-236).</param>
/// <remarks>Implements AUTH-PRIN-002, D-224 and D-236.</remarks>
internal sealed record PolicySettingsView(
    AssuranceLevel RequiredAssurance,
    IReadOnlyList<Factor> LoginFactors,
    bool Photos,
    bool GrammaticalGender)
{
    /// <summary>
    /// Reads a policy.
    /// </summary>
    /// <param name="policy">The policy.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The policy is absent.</exception>
    public static PolicySettingsView Of(Policy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        return new PolicySettingsView(
            policy.RequiredAssurance,
            [.. policy.LoginFactors.Order()],
            policy.Photos,
            policy.GrammaticalGender);
    }
}
