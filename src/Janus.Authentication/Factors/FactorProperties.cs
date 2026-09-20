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
/// <param name="IsWebAuthn">
/// Whether a credential of this entry is created and presented by a WebAuthn
/// ceremony.
/// </param>
/// <param name="IsDiscoverable">
/// Whether the ceremony that creates it keeps it on the authenticator, which is what
/// lets it be offered without the account being named first (AUTH-FACT-002b).
/// </param>
/// <param name="Channel">
/// The identifier an entry rides, where it rides one: proving control of that
/// identifier is what makes the entry available, and nothing else does. Nothing
/// where the entry rides no identifier.
/// </param>
/// <param name="Restricted">
/// Whether the channel the entry rides is one the standard treats as restricted, so
/// that the limitation is shown before it is enrolled and what the deployment can
/// learn about the number is considered before it is used (AUTH-FACT-002b).
/// </param>
/// <param name="SingleUse">
/// Whether presenting it spends it. What an account can reach counts none of these:
/// a dwindling set of one-use secrets would be spent at every gate that read it as
/// a factor the account holds (AUTH-STEP-006).
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
    bool SignInOnly,
    bool IsWebAuthn,
    bool IsDiscoverable,
    IdentifierKind? Channel,
    bool Restricted,
    bool SingleUse);
