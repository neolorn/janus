using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// The default lawful-basis declaration, shipped so a deployment in the jurisdiction
/// it belongs to declares it rather than writes it.
/// </summary>
/// <remarks>
/// Implements PRIV-BASIS-001 and chapter 10 section 5.7. The list is closed and the
/// host declares it; a deployment in another jurisdiction declares a different list
/// with its own flags and the library changes not at all. Nothing here is read by
/// name: what a purpose on a basis does is answered by the four properties.
/// </remarks>
public static class LawfulBases
{
    /// <summary>
    /// The six chapter 10 section 5.7 gives, in the order PRIV-BASIS-001 lists them.
    /// </summary>
    public static IReadOnlyList<LawfulBasisDeclaration> Default { get; } =
    [
        new(
            "consent",
            IsConsent: true,
            RequiresWrittenConsentForSensitive: true,
            RequiresAssessment: false,
            IsObjectable: false),
        new(
            "contractual-obligation",
            IsConsent: false,
            RequiresWrittenConsentForSensitive: false,
            RequiresAssessment: false,
            IsObjectable: false),
        new(
            "legal-obligation",
            IsConsent: false,
            RequiresWrittenConsentForSensitive: false,
            RequiresAssessment: false,
            IsObjectable: false),
        new(
            "legitimate-interest",
            IsConsent: false,
            RequiresWrittenConsentForSensitive: false,
            RequiresAssessment: true,
            IsObjectable: true),
        new(
            "legal-right-claim-or-defence",
            IsConsent: false,
            RequiresWrittenConsentForSensitive: false,
            RequiresAssessment: false,
            IsObjectable: false),
        new(
            "court-judgment-or-order",
            IsConsent: false,
            RequiresWrittenConsentForSensitive: false,
            RequiresAssessment: false,
            IsObjectable: false),
    ];
}
