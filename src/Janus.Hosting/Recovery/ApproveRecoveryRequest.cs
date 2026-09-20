namespace Janus.Hosting.Recovery;

/// <summary>
/// One approver standing behind one account's re-enrolment.
/// </summary>
/// <param name="Subject">Whose account.</param>
/// <param name="Reason">The written reason, which is recorded.</param>
/// <param name="ChannelUsed">
/// The channel the person was confirmed on, which SHALL be one the account already
/// holds (AUTH-RECOV-003).
/// </param>
/// <remarks>Implements AUTH-RECOV-002 and AUTH-RECOV-003.</remarks>
internal sealed record ApproveRecoveryRequest(string? Subject, string? Reason, string? ChannelUsed);
