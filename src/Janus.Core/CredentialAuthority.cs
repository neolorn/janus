namespace Janus.Core;

/// <summary>
/// What a credential operation is done under: the account's own session, or the
/// enrolment session an approved recovery opened.
/// </summary>
/// <remarks>
/// Implements AUTH-RECOV-002, D-147 and D-148. The two are not interchangeable. A
/// session belongs to somebody who is signed in and every gate applies to it; an
/// enrolment session belongs to somebody who is not and cannot be, so the gates it
/// could never pass are the ones the approver already stood in for, and it reaches
/// the credential endpoints of one account and nothing else.
/// </remarks>
public sealed record CredentialAuthority
{
    private CredentialAuthority(
        AccessContext? context,
        SessionId? session,
        EnrolmentSessionId? enrolment)
    {
        Context = context;
        Session = session;
        Enrolment = enrolment;
    }

    /// <summary>
    /// Who is asking, where a signed-in person is.
    /// </summary>
    public AccessContext? Context { get; }

    /// <summary>
    /// The session the request arrived on, where it arrived on one.
    /// </summary>
    public SessionId? Session { get; }

    /// <summary>
    /// The enrolment session the request arrived on, where it arrived on one.
    /// </summary>
    public EnrolmentSessionId? Enrolment { get; }

    /// <summary>
    /// The authority of somebody signed in.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="session">The session the request arrived on.</param>
    /// <returns>The authority.</returns>
    public static CredentialAuthority Of(AccessContext context, SessionId session) =>
        new(context, session, enrolment: null);

    /// <summary>
    /// The authority of an enrolment session.
    /// </summary>
    /// <param name="enrolment">Which session.</param>
    /// <returns>The authority.</returns>
    public static CredentialAuthority Of(EnrolmentSessionId enrolment) =>
        new(context: null, session: null, enrolment);
}
