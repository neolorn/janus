namespace Janus.Hosting.Recovery;

/// <summary>
/// The credential its holder says is gone.
/// </summary>
/// <param name="CredentialId">Which credential.</param>
/// <remarks>Implements AUTH-RECOV-007.</remarks>
internal sealed record ReportLossRequest(string? CredentialId);
