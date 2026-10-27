namespace Janus.Core;

/// <summary>
/// Who is asking in the request being served, as the browser profile established it,
/// for a host's own endpoint to pass to every operation and gate it asks for the person.
/// </summary>
/// <remarks>
/// Implements BFF-HOST-001 and LIB-API-001. What it answers is what session resolution
/// established for the request, read and never looked up again, so it is the same
/// context and session the library's own endpoints pass for the same request. A host
/// endpoint keeps neither past the request it read them for, so a break-glass session's
/// reason never reaches later work.
/// </remarks>
public interface IRequestAccess
{
    /// <summary>
    /// The access of the request being served.
    /// </summary>
    /// <returns>
    /// The access, or the failure <c>auth.session.expired</c> where the request carries no
    /// live sign-in session (none presented, one that has ended, or only a
    /// pre-authentication, registration or enrolment session), and outside the browser
    /// profile or outside a request.
    /// </returns>
    Result<RequestAccess> Current();
}
