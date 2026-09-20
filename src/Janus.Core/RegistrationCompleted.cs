namespace Janus.Core;

/// <summary>
/// What the terms step leaves behind: an account, and the session the person is signed
/// in on.
/// </summary>
/// <param name="Subject">The account that now exists.</param>
/// <param name="Session">The session it is signed in on.</param>
/// <param name="Landing">
/// The address registered for the client that began the registration, which is where
/// the person is returned (REG-SESS-008, API-REDIR-002). Empty where the registry held
/// no such client, which is the deployment's own default and never an address the
/// request asked for.
/// </param>
/// <remarks>Implements REG-SESS-007, REG-SESS-008, REG-SESS-001 and API-REDIR-002.</remarks>
public sealed record RegistrationCompleted(
    SubjectId Subject,
    SessionId Session,
    string Landing);
