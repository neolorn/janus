using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Janus.Core;

/// <summary>
/// The one thing a principal resolves to: the assurance floor, the factors a person
/// may sign in with, what each step-up action costs, whether a second credential is
/// required, whether recovery is self-service, and the domains a membership admits.
/// </summary>
/// <param name="RequiredAssurance">
/// The stated floor. Every rule that says "where the policy requires AAL2" reads this
/// field; nothing is inferred from which factors are enabled.
/// </param>
/// <param name="LoginFactors">
/// The catalogue entries a principal may sign in with, primary and second. The
/// emergency credential and the verification code are not entries and never appear
/// here.
/// </param>
/// <param name="Gates">What each step-up action costs.</param>
/// <param name="CredentialRedundancy">
/// Whether a second credential is required after a device-bound enrolment.
/// </param>
/// <param name="SelfServiceRecovery">
/// Whether email recovery and self-service loss reports are available.
/// </param>
/// <param name="EmailDomains">
/// The domains verified by DNS whose addresses members may sign in with. Empty is the
/// domain lock off. Written only through the organization's domain operations.
/// </param>
/// <remarks>
/// Implements chapter 10 section 4.1a, AUTH-PRIN-002, AUTH-STEP-002a. An organization
/// stores only what it overrides and may tighten any field, never loosen below the
/// system default.
/// </remarks>
[SuppressMessage(
    "Naming",
    "CA1724:Type names should not match namespaces",
    Justification = "Chapter 10 section 4.1a fixes the type name, and the System.Security.Policy namespace the rule matches it against is never referenced in this code base.")]
public sealed record Policy(
    AssuranceLevel RequiredAssurance,
    IReadOnlySet<Factor> LoginFactors,
    IReadOnlyDictionary<StepUpAction, Gate> Gates,
    CredentialRedundancy CredentialRedundancy,
    bool SelfServiceRecovery,
    IReadOnlyList<string> EmailDomains)
{
    /// <summary>
    /// The stated floor.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The level is outside the two chapter 10 section 4.1a states for this field.
    /// </exception>
    public AssuranceLevel RequiredAssurance
    {
        get;
        init => field = AStatedFloor(value);
    } = AStatedFloor(RequiredAssurance);

    /// <summary>
    /// The catalogue entries a principal may sign in with, primary and second.
    /// </summary>
    /// <exception cref="ArgumentNullException">The set is absent.</exception>
    /// <exception cref="ArgumentException">
    /// The set holds the emergency credential, which satisfies every gate for the
    /// session's lifetime, so a policy that admitted it as a login factor would turn
    /// the emergency path into an ordinary one (AUTH-FACT-002, AUTH-STEP-004).
    /// </exception>
    public IReadOnlySet<Factor> LoginFactors
    {
        get;
        init => field = WithoutTheEmergencyCredential(value);
    } = WithoutTheEmergencyCredential(LoginFactors);

    /// <summary>
    /// Whether a catalogue entry may stand among a policy's login factors. The
    /// emergency credential may not: it satisfies every gate for the session's
    /// lifetime, so a policy that admitted it would turn the emergency path into an
    /// ordinary one (AUTH-FACT-002, AUTH-STEP-004).
    /// </summary>
    /// <param name="factor">The entry.</param>
    /// <returns>Whether a policy may name it.</returns>
    internal static bool Admits(Factor factor) => factor is not Factor.BreakGlass;

    // Chapter 10 section 4.1a states aal1 or aal2 for this field. A policy asking for
    // aal3 would state a floor the library cannot reach, and one asking for delegated
    // would state a floor below a single factor.
    private static AssuranceLevel AStatedFloor(AssuranceLevel value) =>
        value is AssuranceLevel.Aal1 or AssuranceLevel.Aal2
            ? value
            : throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "A policy requires aal1 or aal2.");

    private static IReadOnlySet<Factor> WithoutTheEmergencyCredential(IReadOnlySet<Factor> value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value.Contains(Factor.BreakGlass)
            ? throw new ArgumentException(
                "The emergency credential is never a login factor.",
                nameof(value))
            : value;
    }
}
