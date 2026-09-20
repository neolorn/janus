namespace Janus.Core;

/// <summary>
/// What the terms step leaves behind: an account, and the session the person is signed
/// in on.
/// </summary>
/// <param name="Subject">The account that now exists.</param>
/// <param name="Session">The session it is signed in on.</param>
/// <remarks>Implements REG-SESS-007 and REG-SESS-001.</remarks>
public sealed record RegistrationCompleted(SubjectId Subject, SessionId Session);
