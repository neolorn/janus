using System;
using Janus.Authentication.Factors;
using Janus.Core;

namespace Janus.Authentication.Credentials;

/// <summary>
/// A second step the library texts, staged and not yet confirmed: whose it would be,
/// what the person would call it, the number it would be bound to and the code texted
/// to that number.
/// </summary>
/// <remarks>
/// Implements AUTH-FACT-002 and AUTH-FACT-004. It is no credential until the code is
/// presented: nothing of the account reads it as one, a new one replaces it, and it
/// ends with the number it is bound to. Its code is held to the lifetime and the tries
/// of an authentication code, and a wrong try counts against the code alone.
/// </remarks>
internal sealed class PendingEnrolment
{
    private PendingEnrolment(
        SubjectId subject,
        Factor factor,
        CredentialLabel label,
        [NeverLogged] string number,
        [NeverLogged] byte[] send,
        [NeverLogged] byte[] code,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt)
    {
        Subject = subject;
        Factor = factor;
        Label = label;
        Number = number;
        Send = send;
        Code = code;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
    }

    /// <summary>Whose enrolment.</summary>
    public SubjectId Subject { get; }

    /// <summary>Which catalogue entry it would enrol.</summary>
    public Factor Factor { get; }

    /// <summary>What the person would call the credential.</summary>
    public CredentialLabel Label { get; }

    /// <summary>The number the credential would be bound to, canonically.</summary>
    [NeverLogged]
    public string Number { get; }

    /// <summary>
    /// What names the text that carried the code, which is what releases the one count
    /// that text took once its code is presented right (AUTH-ABUSE-004).
    /// </summary>
    [NeverLogged]
    public byte[] Send { get; }

    /// <summary>The code the text carried, held as it is compared.</summary>
    [NeverLogged]
    public byte[] Code { get; }

    /// <summary>When the code went out.</summary>
    public DateTimeOffset IssuedAt { get; }

    /// <summary>When the code stops working.</summary>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>How many wrong codes have been typed against it.</summary>
    public int WrongAttempts { get; private set; }

    /// <summary>
    /// Stages one.
    /// </summary>
    /// <param name="token">The secret the send of the text is named by.</param>
    /// <param name="subject">Whose enrolment.</param>
    /// <param name="factor">Which catalogue entry it would enrol.</param>
    /// <param name="label">What the person would call the credential.</param>
    /// <param name="number">The number it would be bound to, canonically.</param>
    /// <param name="code">The code texted to that number.</param>
    /// <param name="at">Now.</param>
    /// <param name="lifetime">How long the code works for.</param>
    /// <returns>The pending enrolment.</returns>
    /// <exception cref="ArgumentException">
    /// The credential of the entry is bound to no number, or the number is absent.
    /// </exception>
    public static PendingEnrolment Stage(
        OpaqueToken token,
        SubjectId subject,
        Factor factor,
        CredentialLabel label,
        [NeverLogged] string number,
        [NeverLogged] string code,
        DateTimeOffset at,
        TimeSpan lifetime)
    {
        ArgumentException.ThrowIfNullOrEmpty(number);

        if (!Authenticator.IsBoundToNumber(factor))
        {
            throw new ArgumentException("Only a second step the library texts is enrolled by a code.", nameof(factor));
        }

        return new PendingEnrolment(
            subject,
            factor,
            label,
            number,
            token.Fingerprint(),
            VerificationCode.Held(code),
            at,
            at + lifetime);
    }

    /// <summary>
    /// The pending enrolment as the store holds it.
    /// </summary>
    /// <param name="subject">Whose enrolment.</param>
    /// <param name="factor">Which catalogue entry it would enrol.</param>
    /// <param name="label">What the person would call the credential.</param>
    /// <param name="number">The number it would be bound to, canonically.</param>
    /// <param name="send">What names the text that carried the code.</param>
    /// <param name="code">The code it carries.</param>
    /// <param name="issuedAt">When the code went out.</param>
    /// <param name="expiresAt">When the code stops working.</param>
    /// <param name="wrongAttempts">Wrong codes typed against it.</param>
    /// <returns>The pending enrolment.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static PendingEnrolment Existing(
        SubjectId subject,
        Factor factor,
        CredentialLabel label,
        [NeverLogged] string number,
        [NeverLogged] byte[] send,
        [NeverLogged] byte[] code,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        int wrongAttempts)
    {
        ArgumentNullException.ThrowIfNull(number);
        ArgumentNullException.ThrowIfNull(send);
        ArgumentNullException.ThrowIfNull(code);

        return new PendingEnrolment(subject, factor, label, number, send, code, issuedAt, expiresAt)
        {
            WrongAttempts = wrongAttempts,
        };
    }

    /// <summary>
    /// Whether the code has stopped working.
    /// </summary>
    /// <param name="now">Now.</param>
    /// <returns>Whether it has expired.</returns>
    public bool HasExpired(DateTimeOffset now) => now >= ExpiresAt;

    /// <summary>
    /// Whether this is the code the text carried.
    /// </summary>
    /// <param name="entered">What was typed.</param>
    /// <returns>Whether it matches.</returns>
    public bool Matches([NeverLogged] string entered) => VerificationCode.Matches(Code, entered);

    /// <summary>
    /// A wrong code was typed against it.
    /// </summary>
    public void Missed() => WrongAttempts++;
}
