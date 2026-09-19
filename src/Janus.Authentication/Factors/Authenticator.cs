using System;
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
    /// Whether it may be presented at this instant, which a credential awaiting its
    /// confirming code and a suspended one may not.
    /// </summary>
    public bool IsUsable => State is AuthenticatorState.Active && Confirmed;

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
        ReadOnlyMemory<byte> secret,
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
        WebAuthnMaterial? webAuthn) =>
        new(id, subject, factor, label, addedAt, confirmed, totp, webAuthn)
        {
            State = state,
            LastUsedAt = lastUsedAt,
            InvalidatesAt = invalidatesAt,
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
    /// moving backwards is caught.
    /// </summary>
    /// <param name="counter">What the authenticator reported.</param>
    /// <exception cref="InvalidOperationException">The credential holds no key.</exception>
    public void Counted(uint counter) =>
        WebAuthn = WebAuthn is null
            ? throw new InvalidOperationException("The credential holds no key.")
            : WebAuthn with { Counter = counter };

    /// <summary>
    /// The person renamed it.
    /// </summary>
    /// <param name="label">What they call it now.</param>
    public void Rename(CredentialLabel label) => Label = label;

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
    /// The window ended, or the account was recovered.
    /// </summary>
    public void Invalidate()
    {
        State = AuthenticatorState.Invalidated;
        InvalidatesAt = null;
    }
}
