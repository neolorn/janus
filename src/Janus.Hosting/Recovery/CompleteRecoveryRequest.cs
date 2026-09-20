namespace Janus.Hosting.Recovery;

/// <summary>
/// The recovery link and the password it sets.
/// </summary>
/// <param name="Token">The token the message carried.</param>
/// <param name="Password">The new password.</param>
/// <remarks>Implements AUTH-RECOV-005 and D-147.</remarks>
internal sealed record CompleteRecoveryRequest(string? Token, string? Password);
