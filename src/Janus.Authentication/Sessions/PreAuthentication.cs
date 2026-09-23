using System;
using Janus.Core;

namespace Janus.Authentication.Sessions;

/// <summary>
/// What a browser carries before it holds a session: something for a synchronizer
/// token to be bound to, and the registration or enrolment it has in flight.
/// </summary>
/// <remarks>
/// Implements BFF-CSRF-005a and BFF-CSRF-005b. It carries no identity and grants no
/// access: it exists so that the endpoints reached before a session exists are
/// protected in exactly the way every other endpoint is, rather than exempted. Its
/// key is what the cookie's token fingerprints to, so the row holds nothing the
/// browser holds.
/// </remarks>
internal sealed class PreAuthentication
{
    private PreAuthentication(
        byte[] fingerprint,
        byte[] csrfFingerprint,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        Fingerprint = fingerprint;
        CsrfFingerprint = csrfFingerprint;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    /// <summary>
    /// What the pre-authentication cookie's token fingerprints to.
    /// </summary>
    public byte[] Fingerprint { get; }

    /// <summary>
    /// What the synchronizer token bound to it fingerprints to.
    /// </summary>
    public byte[] CsrfFingerprint { get; private set; }

    /// <summary>
    /// When the browser first arrived.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// When it stops answering, which is <c>registration.session.lifetime</c> after
    /// it was issued.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>
    /// The registration this browser has in flight, where it has one.
    /// </summary>
    public RegistrationSessionId? Registration { get; private set; }

    /// <summary>
    /// The enrolment this browser has in flight, where it has one (AUTH-RECOV-002).
    /// </summary>
    public EnrolmentSessionId? Enrolment { get; private set; }

    /// <summary>
    /// The sign-on this browser has in flight, where it has one (BFF-SESS-006).
    /// </summary>
    public SignOnAttempt? SignOn { get; private set; }

    /// <summary>
    /// Issues one for a browser that carried nothing.
    /// </summary>
    /// <param name="secret">The token the cookie carries.</param>
    /// <param name="csrfToken">The token a state-changing request presents back.</param>
    /// <param name="at">When the browser arrived.</param>
    /// <param name="lifetime">How long it answers for.</param>
    /// <returns>The pre-authentication session.</returns>
    public static PreAuthentication Issue(
        OpaqueToken secret,
        OpaqueToken csrfToken,
        DateTimeOffset at,
        TimeSpan lifetime) =>
        new(secret.Fingerprint(), csrfToken.Fingerprint(), at, at + lifetime);

    /// <summary>
    /// The pre-authentication session as it already stands, which is the store
    /// translating a row and no step the browser took.
    /// </summary>
    /// <param name="fingerprint">What the cookie's token fingerprints to.</param>
    /// <param name="csrfFingerprint">What the bound token fingerprints to.</param>
    /// <param name="createdAt">When the browser arrived.</param>
    /// <param name="expiresAt">When it stops answering.</param>
    /// <param name="registration">The registration in flight, where there is one.</param>
    /// <param name="enrolment">The enrolment in flight, where there is one.</param>
    /// <param name="signOn">The sign-on in flight, where there is one.</param>
    /// <returns>The pre-authentication session.</returns>
    /// <exception cref="ArgumentNullException">Either fingerprint is absent.</exception>
    public static PreAuthentication Existing(
        byte[] fingerprint,
        byte[] csrfFingerprint,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        RegistrationSessionId? registration,
        EnrolmentSessionId? enrolment = null,
        SignOnAttempt? signOn = null)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);
        ArgumentNullException.ThrowIfNull(csrfFingerprint);

        return new PreAuthentication(fingerprint, csrfFingerprint, createdAt, expiresAt)
        {
            Registration = registration,
            Enrolment = enrolment,
            SignOn = signOn,
        };
    }

    /// <summary>
    /// Whether it has stopped answering.
    /// </summary>
    /// <param name="now">The instant to judge at.</param>
    /// <returns>Whether it has.</returns>
    public bool HasExpired(DateTimeOffset now) => now >= ExpiresAt;

    /// <summary>
    /// Records the registration this browser has just started, which is what binds
    /// the two (BFF-CSRF-005b).
    /// </summary>
    /// <param name="registration">Which registration.</param>
    /// <param name="expiresAt">When both stop answering, which is one instant.</param>
    public void Carry(RegistrationSessionId registration, DateTimeOffset expiresAt)
    {
        Registration = registration;
        ExpiresAt = expiresAt;
    }

    /// <summary>
    /// Records the enrolment this browser has just opened, which binds the two as a
    /// registration is bound (AUTH-RECOV-002, BFF-CSRF-005b).
    /// </summary>
    /// <param name="enrolment">Which enrolment.</param>
    /// <param name="expiresAt">When both stop answering, which is one instant.</param>
    public void Carry(EnrolmentSessionId enrolment, DateTimeOffset expiresAt)
    {
        Enrolment = enrolment;
        ExpiresAt = expiresAt;
    }

    /// <summary>
    /// Records the sign-on this browser has just started, which is what binds the
    /// code that comes back to the browser that asked for it (BFF-SESS-006).
    /// </summary>
    /// <param name="attempt">What the return is judged against.</param>
    public void Carry(SignOnAttempt attempt) => SignOn = attempt;

    /// <summary>
    /// Forgets what the browser had in flight, which is what abandoning a
    /// registration or finishing an enrolment leaves behind.
    /// </summary>
    public void Release()
    {
        Registration = null;
        Enrolment = null;
    }

    /// <summary>
    /// Forgets the sign-on, which every return ends with whether it succeeded or not,
    /// so one code answers once and a second return has nothing to be judged against.
    /// </summary>
    public void Abandon() => SignOn = null;
}
