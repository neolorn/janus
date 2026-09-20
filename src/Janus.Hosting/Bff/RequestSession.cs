using System;
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
    /// Who is asking, or nothing where nobody is.
    /// </summary>
    public AccessContext? Context =>
        Live is null ? null : AccessContext.Of(Live.Subject);

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
    /// Records what the browser carried before it held a session.
    /// </summary>
    /// <param name="contact">The first contact.</param>
    /// <exception cref="ArgumentNullException">The first contact is absent.</exception>
    public void Resolved(PreAuthentication contact)
    {
        ArgumentNullException.ThrowIfNull(contact);

        FirstContact = contact;
    }
}
