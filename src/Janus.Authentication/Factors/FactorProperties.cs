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
/// <param name="SignInOnly">
/// Whether its contribution is to a sign-in and to nothing afterwards: it is never a
/// second step, satisfies no gate and restores no lapsed session.
/// </param>
/// <remarks>
/// Implements AUTH-FACT-001, AUTH-FACT-003 and chapter 10 section 5.3. How strong a
/// factor is has one axis, the tier beside phishing-resistance, so no second axis can
/// let two rules disagree about the same factor; what the tier cannot say is when a
/// contribution counts, which is what <paramref name="SignInOnly"/> carries.
/// </remarks>
internal sealed record FactorProperties(
    bool CanBePrimary,
    bool CanBeSecondFactor,
    bool IsPhishingResistant,
    AssuranceLevel AssuranceLevel,
    bool VerificationOnly,
    bool SignInOnly);
