namespace Janus.Hosting.Privacy;

/// <summary>
/// What a subject submits for themselves.
/// </summary>
/// <param name="Type">What is asked for: <c>restriction</c> or <c>rectification</c>.</param>
/// <param name="Detail">What they wrote.</param>
/// <remarks>
/// Implements chapter 09 section 7 and PRIV-RIGHT-001. Erasure is not a type here: a
/// signed-in customer exercises it with account deletion.
/// </remarks>
internal sealed record PrivacyRequestBody(string? Type, string? Detail);
