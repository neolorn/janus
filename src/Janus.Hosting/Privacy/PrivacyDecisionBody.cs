namespace Janus.Hosting.Privacy;

/// <summary>
/// Why a request was refused.
/// </summary>
/// <param name="Reason">The written reason, which is recorded.</param>
/// <remarks>Implements chapter 09 section 8a and PRIV-RIGHT-001.</remarks>
internal sealed record PrivacyDecisionBody(string? Reason);
