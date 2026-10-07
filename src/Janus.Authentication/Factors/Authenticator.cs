using System;
using System.Linq;
using Janus.Core;

namespace Janus.Authentication.Factors;

/// <summary>
/// One enrolled credential of an account: what it is, what the person calls it, where
/// it stands, and the material it proves itself with.
/// </summary>
/// <remarks>
/// Implements AUTH-FACT-001, AUTH-FACT-007, AUTH-FACT-013 and AUTH-RECOV-007. A
/// reported-lost credential is suspended rather than removed, so an attacker who
/// reported it cannot strip a factor from an account in one step.
/// </remarks>
internal sealed class Authenticator
{
    private Authenticator(
        AuthenticatorId id,
        SubjectId subject,
        Factor factor,
        CredentialLabel label,
        DateTimeOffset at,
        bool confirmed,
        TotpMaterial? totp,
        WebAuthnMaterial? webAuthn)
    {
        Id = id;
        Subject = subject;
        Factor = factor;
        Label = label;
        AddedAt = at;
        Confirmed = confirmed;
        Totp = totp;
        WebAuthn = webAuthn;
    }

    /// <summary>Which credential.</summary>
    public AuthenticatorId Id { get; }

    /// <summary>Whose it is.</summary>
    public SubjectId Subject { get; }

    /// <summary>Which catalogue entry it is an instance of.</summary>
    public Factor Factor { get; }

    /// <summary>What the person calls it.</summary>
    public CredentialLabel Label { get; private set; }

    /// <summary>Where it stands.</summary>
    public AuthenticatorState State { get; private set; }

    /// <summary>When it was enrolled.</summary>
    public DateTimeOffset AddedAt { get; }

    /// <summary>When it was last presented, and nothing where it never has been.</summary>
    public DateTimeOffset? LastUsedAt { get; private set; }

    /// <summary>
    /// When a suspended credential is invalidated, and nothing where it is not
    /// suspended.
    /// </summary>
    public DateTimeOffset? InvalidatesAt { get; private set; }

    /// <summary>
    /// Whether the enrolment was confirmed by presenting the credential once, which
    /// an abandoned enrolment never is.
    /// </summary>
    public bool Confirmed { get; private set; }

    /// <summary>The shared secret, where it is a code generator.</summary>
    public TotpMaterial? Totp { get; private set; }

    /// <summary>The key material, where it is a WebAuthn credential.</summary>
    public WebAuthnMaterial? WebAuthn { get; private set; }

    /// <summary>
    /// The number it is bound to, canonically, where it is a second step the library
    /// texts: the number its code confirmed it on, to which every later code goes, and
    /// nothing once that number has left the account (AUTH-FACT-002).
    /// </summary>
    [NeverLogged]
    public string? Number { get; private set; }

    /// <summary>
    /// Whether the person chose this one to be offered first at a second step. Where
    /// the account has chosen none, the most recently enrolled second factor is
    /// offered first and no credential carries the mark (IDN-ATTR-008).
    /// </summary>
    public bool IsPreferred { get; private set; }

    /// <summary>
    /// Whether it may be presented at this instant, which a credential awaiting its
    /// confirming code and a suspended one may not.
    /// </summary>
    public bool IsUsable => State is AuthenticatorState.Active && Confirmed;

    /// <summary>
    /// Whether a loss report, or a removal that would lower the account's reachable
    /// assurance, suspended it: suspended with the instant at which it is invalidated.
    /// Presented anyway, it is judged as an active one would be and refused as
    /// suspended only where what it presented verifies (AUTH-RECOV-007).
    /// </summary>
    public bool IsAwaitingInvalidation =>
        State is AuthenticatorState.Suspended && InvalidatesAt is not null && Confirmed;

    /// <summary>
    /// A code generator, enrolled but not yet confirmed: one valid code has to be
    /// presented before it becomes usable (AUTH-FACT-007).
    /// </summary>
    /// <param name="id">The identifier issued for it.</param>
    /// <param name="subject">Whose it is.</param>
    /// <param name="label">What the person calls it.</param>
    /// <param name="secret">The shared secret.</param>
    /// <param name="at">When it was enrolled.</param>
    /// <returns>The credential.</returns>
    public static Authenticator EnrollingTotp(
        AuthenticatorId id,
        SubjectId subject,
        CredentialLabel label,
        [NeverLogged] ReadOnlyMemory<byte> secret,
        DateTimeOffset at) =>
        new(
            id,
            subject,
            Factor.Totp,
            label,
            at,
            confirmed: false,
            new TotpMaterial(secret, ConsumedStep: null),
            webAuthn: null);

    /// <summary>
    /// A WebAuthn credential, which is confirmed by the ceremony that created it.
    /// </summary>
    /// <param name="id">The identifier issued for it.</param>
    /// <param name="subject">Whose it is.</param>
    /// <param name="factor">Which catalogue entry the ceremony produced.</param>
    /// <param name="label">What the person calls it.</param>
    /// <param name="material">The key material the ceremony returned.</param>
    /// <param name="at">When it was enrolled.</param>
    /// <returns>The credential.</returns>
    /// <exception cref="ArgumentNullException">The material is absent.</exception>
    public static Authenticator WebAuthnCredential(
        AuthenticatorId id,
        SubjectId subject,
        Factor factor,
        CredentialLabel label,
        WebAuthnMaterial material,
        DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(material);

        return new Authenticator(
            id,
            subject,
            factor,
            label,
            at,
            confirmed: true,
            totp: null,
            material);
    }

    /// <summary>
    /// An identity at a social provider, linked to the account: the sign-in at the
    /// provider proved it, so it is confirmed as it is linked (IDN-LIFE-012).
    /// </summary>
    /// <param name="id">The identifier issued for it.</param>
    /// <param name="subject">Whose it is.</param>
    /// <param name="provider">Which social provider.</param>
    /// <param name="label">What the person calls it.</param>
    /// <param name="at">When it was linked.</param>
    /// <returns>The credential.</returns>
    /// <exception cref="ArgumentException">The factor is not a social provider.</exception>
    public static Authenticator Linked(
        AuthenticatorId id,
        SubjectId subject,
        Factor provider,
        CredentialLabel label,
        DateTimeOffset at)
    {
        if (FactorCatalogue.Of(provider).AssuranceLevel is not AssuranceLevel.Delegated)
        {
            throw new ArgumentException("Only a social provider is linked.", nameof(provider));
        }

        return new Authenticator(
            id,
            subject,
            provider,
            label,
            at,
            confirmed: true,
            totp: null,
            webAuthn: null);
    }

    /// <summary>
    /// Whether an entry's credential is bound to a number: a second step the library
    /// texts, which goes to the number it was confirmed on and to no other
    /// (AUTH-FACT-002).
    /// </summary>
    /// <param name="factor">The entry.</param>
    /// <returns>Whether its credential is bound to a number.</returns>
    public static bool IsBoundToNumber(Factor factor) =>
        FactorCatalogue.Of(factor).CanBeSecondFactor
        && FactorCatalogue.Sent.Any(sent => sent.Key.Channel is IdentifierKind.Phone && sent.Value == factor);

    /// <summary>
    /// A second step the library texts, confirmed by the code texted to the number it
    /// is bound to from then on (AUTH-FACT-002).
    /// </summary>
    /// <param name="id">The identifier issued for it.</param>
    /// <param name="subject">Whose it is.</param>
    /// <param name="factor">Which catalogue entry the code confirmed.</param>
    /// <param name="label">What the person calls it.</param>
    /// <param name="number">The number the code was texted to, canonically.</param>
    /// <param name="at">When it was confirmed.</param>
    /// <returns>The credential.</returns>
    /// <exception cref="ArgumentException">
    /// The entry's credential is bound to no number, or the number is absent.
    /// </exception>
    public static Authenticator Bound(
        AuthenticatorId id,
        SubjectId subject,
        Factor factor,
        CredentialLabel label,
        [NeverLogged] string number,
        DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrEmpty(number);

        if (!IsBoundToNumber(factor))
        {
            throw new ArgumentException("Only a second step the library texts is bound to a number.", nameof(factor));
        }

        return new Authenticator(
            id,
            subject,
            factor,
            label,
            at,
            confirmed: true,
            totp: null,
            webAuthn: null)
        {
            Number = number,
        };
    }

    /// <summary>
    /// The credential as it already stands, which is the store's translation of a row
    /// and no change to it.
    /// </summary>
    /// <param name="id">Which credential.</param>
    /// <param name="subject">Whose it is.</param>
    /// <param name="factor">Which catalogue entry it is an instance of.</param>
    /// <param name="label">What the person calls it.</param>
    /// <param name="state">Where it stands.</param>
    /// <param name="addedAt">When it was enrolled.</param>
    /// <param name="lastUsedAt">When it was last presented.</param>
    /// <param name="invalidatesAt">When a suspended one is invalidated.</param>
    /// <param name="confirmed">Whether the enrolment was confirmed.</param>
    /// <param name="totp">The shared secret, where it is a code generator.</param>
    /// <param name="webAuthn">The key material, where it holds a key.</param>
    /// <param name="isPreferred">Whether the person chose it to be offered first.</param>
    /// <param name="number">The number it is bound to, where it is bound to one.</param>
    /// <returns>The credential.</returns>
    public static Authenticator Existing(
        AuthenticatorId id,
        SubjectId subject,
        Factor factor,
        CredentialLabel label,
        AuthenticatorState state,
        DateTimeOffset addedAt,
        DateTimeOffset? lastUsedAt,
        DateTimeOffset? invalidatesAt,
        bool confirmed,
        TotpMaterial? totp,
        WebAuthnMaterial? webAuthn,
        bool isPreferred = false,
        [NeverLogged] string? number = null) =>
        new(id, subject, factor, label, addedAt, confirmed, totp, webAuthn)
        {
            State = state,
            LastUsedAt = lastUsedAt,
            InvalidatesAt = invalidatesAt,
            IsPreferred = isPreferred,
            Number = number,
        };

    /// <summary>
    /// The enrolment was confirmed by presenting the credential once.
    /// </summary>
    /// <param name="at">When.</param>
    public void Confirm(DateTimeOffset at)
    {
        Confirmed = true;
        LastUsedAt = at;
    }

    /// <summary>
    /// The credential was presented and accepted.
    /// </summary>
    /// <param name="at">When.</param>
    public void Used(DateTimeOffset at) => LastUsedAt = at;

    /// <summary>
    /// The code generator consumed a time step, which it will not accept again.
    /// </summary>
    /// <param name="step">The step the code belonged to.</param>
    /// <exception cref="InvalidOperationException">The credential generates no codes.</exception>
    public void Consumed(long step) =>
        Totp = Totp is null
            ? throw new InvalidOperationException("The credential generates no codes.")
            : Totp with { ConsumedStep = step };

    /// <summary>
    /// The signature counter the authenticator reported, which is recorded so that one
    /// that does not advance is caught.
    /// </summary>
    /// <param name="counter">What the authenticator reported.</param>
    /// <exception cref="InvalidOperationException">The credential holds no key.</exception>
    public void Counted(uint counter) =>
        WebAuthn = WebAuthn is null
            ? throw new InvalidOperationException("The credential holds no key.")
            : WebAuthn with { Counter = counter };

    /// <summary>
    /// The backup state the authenticator reported with an accepted assertion, which is
    /// recorded so that a credential that became synced, or stopped being, is told
    /// apart afterwards (AUTH-FACT-013, AUTH-FACT-014).
    /// </summary>
    /// <param name="backedUp">Whether the credential is backed up now.</param>
    /// <exception cref="InvalidOperationException">The credential holds no key.</exception>
    public void BackedUp(bool backedUp) =>
        WebAuthn = WebAuthn is null
            ? throw new InvalidOperationException("The credential holds no key.")
            : WebAuthn with { BackupState = backedUp };

    /// <summary>
    /// The person renamed it.
    /// </summary>
    /// <param name="label">What they call it now.</param>
    public void Rename(CredentialLabel label) => Label = label;

    /// <summary>
    /// The person chose this one to be offered first at a second step, or chose
    /// another and this one gave the mark up (IDN-ATTR-008).
    /// </summary>
    /// <param name="preferred">Whether it is the one chosen.</param>
    public void Prefer(bool preferred) => IsPreferred = preferred;

    /// <summary>
    /// The credential was reported lost: it is refused from now and gone when the
    /// notified window ends.
    /// </summary>
    /// <param name="invalidatesAt">When the window ends.</param>
    public void Suspend(DateTimeOffset invalidatesAt)
    {
        if (State is AuthenticatorState.Active)
        {
            State = AuthenticatorState.Suspended;
            InvalidatesAt = invalidatesAt;
        }
    }

    /// <summary>
    /// Whether it is a second step the library texts whose number is no longer the
    /// account's: the number it was bound to left the account, or it was never bound to
    /// one. No code is texted for it and no cancel returns it (AUTH-FACT-002,
    /// AUTH-RECOV-007).
    /// </summary>
    public bool IsUnbound => IsBoundToNumber(Factor) && Number is null;

    /// <summary>
    /// The number it is bound to left the account, so it is bound to none from now; an
    /// undo that restores the number does not bind it again (AUTH-FACT-002).
    /// </summary>
    public void Unbind() => Number = null;

    /// <summary>
    /// Whether a social provider's security event holds it: suspended with no instant
    /// at which it is invalidated, until the person signs in by another factor
    /// (IDN-LIFE-012a). A reported loss always carries that instant, so the two are
    /// never mistaken for each other.
    /// </summary>
    public bool IsHeldByProvider => State is AuthenticatorState.Suspended && InvalidatesAt is null;

    /// <summary>
    /// The provider said the identity behind it was compromised, disabled or signed
    /// out everywhere: it is refused from now until the person signs in by another
    /// factor, and nothing invalidates it meanwhile (IDN-LIFE-012a).
    /// </summary>
    public void Hold()
    {
        if (State is AuthenticatorState.Active)
        {
            State = AuthenticatorState.Suspended;
        }
    }

    /// <summary>
    /// The report against it was cancelled, or the person signed in by another factor
    /// while a provider's event held it, so it is usable again from this instant
    /// (AUTH-RECOV-007, IDN-LIFE-012a).
    /// </summary>
    public void Restore()
    {
        if (State is AuthenticatorState.Suspended)
        {
            State = AuthenticatorState.Active;
            InvalidatesAt = null;
        }
    }

    /// <summary>
    /// The window ended, or the account was recovered.
    /// </summary>
    public void Invalidate()
    {
        State = AuthenticatorState.Invalidated;
        InvalidatesAt = null;
    }
}
