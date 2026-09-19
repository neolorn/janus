using Janus.Core;

namespace Janus.Authentication.Factors;

/// <summary>
/// What a factor may do. Every rule in the library reads these and never which entry
/// of the catalogue it was handed, so a factor added to the catalogue is a row and not
/// an edit to a rule.
/// </summary>
/// <param name="CanBePrimary">Whether it may begin an authentication.</param>
/// <param name="CanBeSecondFactor">Whether it may satisfy a second-factor requirement.</param>
/// <param name="IsPhishingResistant">Whether it resists credential relay.</param>
/// <param name="AssuranceLevel">
/// The tier this factor's own contribution amounts to: what a primary reaches on its
/// own, and what a second factor lifts a sign-in to beside one.
/// </param>
/// <param name="VerificationOnly">
/// Whether it proves control of a channel and never authenticates.
/// </param>
/// <remarks>
/// Implements AUTH-FACT-001 and chapter 10 section 5.3. There is no property for
/// whether a factor may satisfy step-up: that follows from the tier and the
/// phishing-resistance against the gate (AUTH-STEP-002), and a second axis would let
/// two rules disagree about the same factor.
/// </remarks>
internal sealed record FactorProperties(
    bool CanBePrimary,
    bool CanBeSecondFactor,
    bool IsPhishingResistant,
    AssuranceLevel AssuranceLevel,
    bool VerificationOnly);
