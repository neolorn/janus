namespace Janus.Hosting.Recovery;

/// <summary>
/// What a cancellation presents where it is not made from a session of the account.
/// </summary>
/// <param name="Token">
/// The token every notification carried, or nothing where the account's own session
/// is cancelling.
/// </param>
/// <remarks>Implements AUTH-RECOV-007 and D-141.</remarks>
internal sealed record CancelLossRequest(string? Token);
