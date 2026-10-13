using System;
using Janus.Authentication;
using Janus.Authentication.Sessions;
using Janus.Core;

namespace Janus.Hosting.Bff;

/// <summary>
/// What the session resolution stage established, for the stages and the endpoint
/// after it.
/// </summary>
/// <remarks>
/// Implements BFF-ORDER-001 stage 5, BFF-CSRF-005a and LIB-API-005. Resolution
/// happens once per request, here, and every later stage reads what it left rather
/// than looking the session up again: two lookups are two answers, and the second
/// one is the one an attacker races. Nothing writes to this except that stage.
/// </remarks>
internal sealed class RequestSession
{
    /// <summary>
    /// The session the request arrived on, or nothing where it carried none.
    /// </summary>
    public Session? Live { get; private set; }

    /// <summary>
    /// What the browser carried before it held a session, or nothing.
    /// </summary>
    public PreAuthentication? FirstContact { get; private set; }

    /// <summary>
    /// The token that first contact answers to, which the sign-on rotates out of once
    /// the session exists (BFF-SESS-006).
    /// </summary>
    public OpaqueToken? FirstContactSecret { get; private set; }

    /// <summary>
    /// The enrolment session the browser carries, where the request reached one of the
    /// routes chapter 09 lists for it at <c>POST /enrol/begin</c>, or nothing: on any
    /// other route the request goes on as one that carried none (BFF-ORDER-001 stage
    /// 5, D-189).
    /// </summary>
    public EnrolmentSessionId? Enrolment { get; private set; }

    /// <summary>
    /// The session the request arrived on, where the stage that requires one let the
    /// request through.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The endpoint was reached without being marked as one that needs a session,
    /// which is a mounting error and not something a caller can produce.
    /// </exception>
    public Session Required =>
        Live ?? throw new InvalidOperationException(
            "The endpoint is not mounted as one that requires a session.");

    /// <summary>
    /// What the cookie resolved to where it named a session that has ended, or
    /// nothing where the browser carried no session cookie at all.
    /// </summary>
    public Error? Expiry { get; private set; }

    /// <summary>
    /// Who is asking, or nothing where nobody is.
    /// </summary>
    public AccessContext? Context =>
        Live is null ? null : Of(Live);

    /// <summary>
    /// Who is asking on the session the request arrived on, where the stage that
    /// requires one let the request through.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The endpoint was reached without being marked as one that needs a session.
    /// </exception>
    public AccessContext Asking => Of(Required);

    /// <summary>
    /// Records the session the request arrived on.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <exception cref="ArgumentNullException">The session is absent.</exception>
    public void Resolved(Session session)
    {
        ArgumentNullException.ThrowIfNull(session);

        Live = session;
    }

    /// <summary>
    /// Records that the cookie named a session that has ended.
    /// </summary>
    /// <param name="refusal">What resolving it answered.</param>
    /// <exception cref="ArgumentNullException">The refusal is absent.</exception>
    public void Ended(Error refusal)
    {
        ArgumentNullException.ThrowIfNull(refusal);

        Expiry = refusal;
    }

    /// <summary>
    /// Records what the browser carried before it held a session, or what it has just
    /// been given where it carried none.
    /// </summary>
    /// <param name="contact">The first contact.</param>
    /// <param name="secret">The token it answers to.</param>
    /// <exception cref="ArgumentNullException">The first contact is absent.</exception>
    public void Resolved(PreAuthentication contact, OpaqueToken secret)
    {
        ArgumentNullException.ThrowIfNull(contact);

        FirstContact = contact;
        FirstContactSecret = secret;
    }

    /// <summary>
    /// Records the enrolment session the browser carries, on a route that resolves one.
    /// </summary>
    /// <param name="enrolment">The enrolment session.</param>
    public void Resolved(EnrolmentSessionId enrolment) => Enrolment = enrolment;

    // OPS-BOOT-002, D-170: a break-glass session hands the reason given at its use to
    // every operation made on it, with the account, and no other session has one.
    private static AccessContext Of(Session session) =>
        session.BreakGlassReason is string reason
            ? AccessContext.InBreakGlass(session.Subject, reason)
            : AccessContext.Of(session.Subject);
}
