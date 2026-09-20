namespace Janus.Hosting.Recovery;

/// <summary>
/// One identifier a recovery link is asked for, as the person entered it.
/// </summary>
/// <param name="Identifier">The email or the phone.</param>
/// <remarks>
/// Implements AUTH-RECOV-005 and AUTH-ABUSE-003. The kind is detected from the value,
/// so nothing here says which kind was meant.
/// </remarks>
internal sealed record RecoveryRequest(string? Identifier);
