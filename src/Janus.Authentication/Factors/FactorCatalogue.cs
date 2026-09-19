using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using Janus.Core;

namespace Janus.Authentication.Factors;

/// <summary>
/// The catalogue: every entry with what it may do. This is the one place a factor is
/// named; every rule reads the properties an entry carries.
/// </summary>
/// <remarks>
/// Implements AUTH-FACT-002. Which entries a principal may sign in with is the
/// policy's login factors and never a property here, so an entry that is off for one
/// organization and on for another is one catalogue row either way.
/// </remarks>
internal static class FactorCatalogue
{
    /// <summary>
    /// Every entry, by the identifier requests, credential records and a policy's
    /// login factors use.
    /// </summary>
    public static FrozenDictionary<Factor, FactorProperties> Entries { get; } =
        new Dictionary<Factor, FactorProperties>
        {
            [Factor.Password] = Primary(AssuranceLevel.Aal1, phishingResistant: false),
            [Factor.Passkey] = Primary(
                AssuranceLevel.Aal2,
                phishingResistant: true,
                webAuthn: true,
                discoverable: true),
            [Factor.EmailLink] = AtSignIn(AssuranceLevel.Aal1),
            [Factor.EmailCode] = AtSignIn(AssuranceLevel.Aal1),
            [Factor.PhoneLink] = AtSignIn(AssuranceLevel.Aal1),
            [Factor.Google] = Primary(AssuranceLevel.Delegated, phishingResistant: false),
            [Factor.Apple] = Primary(AssuranceLevel.Delegated, phishingResistant: false),
            [Factor.Totp] = Second(phishingResistant: false),
            [Factor.SecurityKey] = Second(phishingResistant: true, webAuthn: true),
            [Factor.PhoneCode] = Second(phishingResistant: false),
            [Factor.RecoveryCodes] = Second(phishingResistant: false, singleUse: true),
            [Factor.BreakGlass] = Primary(AssuranceLevel.Aal1, phishingResistant: false),
        }.ToFrozenDictionary();

    /// <summary>
    /// The entry a ceremony creating a discoverable credential produces, which is the
    /// one an upgrade moves a second-factor credential to (AUTH-FACT-002b).
    /// </summary>
    public static Factor Discoverable { get; } =
        Entries.First(entry => entry.Value.IsWebAuthn && entry.Value.IsDiscoverable).Key;

    /// <summary>
    /// What an entry may do.
    /// </summary>
    /// <param name="factor">The entry.</param>
    /// <returns>Its properties.</returns>
    /// <exception cref="KeyNotFoundException">The value is not a catalogue entry.</exception>
    public static FactorProperties Of(Factor factor) => Entries[factor];

    // An entry that begins an authentication and is never a second step.
    private static FactorProperties Primary(
        AssuranceLevel level,
        bool phishingResistant,
        bool webAuthn = false,
        bool discoverable = false) =>
        new(
            CanBePrimary: true,
            CanBeSecondFactor: false,
            phishingResistant,
            level,
            VerificationOnly: false,
            SignInOnly: false,
            webAuthn,
            discoverable,
            SingleUse: false);

    // An entry whose contribution is to a sign-in and to nothing afterwards: the
    // mailbox or the number behind it is also the recovery channel, so counting it
    // later would make one compromise both steps (AUTH-FACT-003).
    private static FactorProperties AtSignIn(AssuranceLevel level) =>
        new(
            CanBePrimary: true,
            CanBeSecondFactor: false,
            IsPhishingResistant: false,
            level,
            VerificationOnly: false,
            SignInOnly: true,
            IsWebAuthn: false,
            IsDiscoverable: false,
            SingleUse: false);

    // An entry that is never a first step and lifts a sign-in beside one.
    private static FactorProperties Second(
        bool phishingResistant,
        bool webAuthn = false,
        bool singleUse = false) =>
        new(
            CanBePrimary: false,
            CanBeSecondFactor: true,
            phishingResistant,
            AssuranceLevel.Aal2,
            VerificationOnly: false,
            SignInOnly: false,
            webAuthn,
            IsDiscoverable: false,
            singleUse);
}
