using Janus.Core;

namespace Janus.Authentication.Sessions;

/// <summary>
/// A session an end took before its expiry, and whose it was.
/// </summary>
/// <param name="Session">Which session.</param>
/// <param name="Subject">The account it belonged to.</param>
/// <remarks>
/// Implements chapter 10 section 5b (D-218): what an end hands back to be announced.
/// </remarks>
internal sealed record EndedSession(SessionId Session, SubjectId Subject);
