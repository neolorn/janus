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
    Justification = "Chapter 10 section 4.1a names it the policy object, and System.Security.Policy is a .NET Framework namespace that does not exist on this framework.")]
public sealed record Policy(
    AssuranceLevel RequiredAssurance,
    IReadOnlySet<Factor> LoginFactors,
    IReadOnlyDictionary<StepUpAction, Gate> Gates,
    CredentialRedundancy CredentialRedundancy,
    bool SelfServiceRecovery,
    IReadOnlyList<string> EmailDomains);
