namespace Janus.Core;

/// <summary>
/// The access of one request: who is asking, and the session the request carries.
/// </summary>
/// <param name="Context">
/// The context the library's own endpoints pass for the same request, its break-glass
/// reason included where the session is a break-glass session or one another
/// application opened from it.
/// </param>
/// <param name="Session">
/// The session the library's own endpoints pass to an operation for the same request.
/// </param>
/// <remarks>
/// Implements BFF-HOST-001. It carries no assurance: a host never judges assurance, and
/// the gate it asks reads the session record when it judges, as it does for the
/// library's own endpoints.
/// </remarks>
public sealed record RequestAccess(AccessContext Context, SessionId Session);
