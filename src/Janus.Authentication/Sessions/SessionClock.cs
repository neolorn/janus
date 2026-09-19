using System;
using Janus.Core;

namespace Janus.Authentication.Sessions;

/// <summary>
/// Whether a session is still live, and what an expired one asks for.
/// </summary>
/// <remarks>
/// Implements AUTH-SESS-005. Lifetimes are derived from the assurance the principal's
/// policy requires, never from the level a particular sign-in happened to reach: a
/// customer who signs in with a passkey keeps the customer lifetimes.
/// </remarks>
internal static class SessionClock
{
    /// <summary>
    /// What the session asks for at this instant.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="now">The instant.</param>
    /// <param name="required">The assurance the principal's policy requires.</param>
    /// <returns>
    /// Nothing where the session is still live, or what it asks for where it is not.
    /// </returns>
    /// <exception cref="ArgumentNullException">The session is absent.</exception>
    public static ReauthenticationKind? Expired(
        Session session,
        DateTimeOffset now,
        AssuranceLevel required)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.EndedAt is not null || now >= session.AbsoluteExpiry)
        {
            return ReauthenticationKind.Full;
        }

        if (now < session.IdleExpiry)
        {
            return null;
        }

        // Inside the absolute window, a policy that requires AAL2 takes one factor
        // bound to the session secret the browser still holds. The allowance is
        // written for a short gap, which is why it does not reach a ninety-day one.
        return required >= AssuranceLevel.Aal2
            ? ReauthenticationKind.SingleFactor
            : ReauthenticationKind.Full;
    }
}
