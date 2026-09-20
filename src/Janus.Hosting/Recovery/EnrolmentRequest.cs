namespace Janus.Hosting.Recovery;

/// <summary>
/// The enrolment link an approver sent.
/// </summary>
/// <param name="Token">The token the message carried.</param>
/// <remarks>
/// Implements AUTH-RECOV-002 and D-147. This is not the self-service recovery link
/// and the two are never interchangeable.
/// </remarks>
internal sealed record EnrolmentRequest(string? Token);
