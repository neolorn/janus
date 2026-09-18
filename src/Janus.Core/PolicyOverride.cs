using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// What an organization changes about the system policy. A field the organization
/// leaves absent inherits the system default; an organization may tighten any field
/// and may not loosen one below that default.
/// </summary>
/// <param name="RequiredAssurance">The assurance floor, where the organization raises it.</param>
/// <param name="LoginFactors">The login factors, where the organization narrows them.</param>
/// <param name="Gates">The step-up gates, where the organization raises them.</param>
/// <param name="CredentialRedundancy">The redundancy rule, where the organization enforces it.</param>
/// <param name="SelfServiceRecovery">The recovery rule, where the organization withdraws it.</param>
/// <param name="EmailDomains">The domain lock, written only through the domain operations.</param>
/// <remarks>Implements chapter 10 sections 4.1 and 4.1a, AUTH-PRIN-002, AUTH-STEP-002a.</remarks>
public sealed record PolicyOverride(
    AssuranceLevel? RequiredAssurance,
    IReadOnlySet<Factor>? LoginFactors,
    IReadOnlyDictionary<StepUpAction, Gate>? Gates,
    CredentialRedundancy? CredentialRedundancy,
    bool? SelfServiceRecovery,
    IReadOnlyList<string>? EmailDomains)
{
    /// <summary>
    /// The override an organization is created with: none, so every field inherits
    /// the system policy.
    /// </summary>
    public static PolicyOverride None { get; } = new(null, null, null, null, null, null);
}
