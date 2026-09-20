namespace Janus.Hosting.Authentication;

/// <summary>
/// The code that releases a sign-in the new-device check held.
/// </summary>
/// <param name="ChallengeId">The handle the held sign-in carries.</param>
/// <param name="Code">What was typed where the sign-in began.</param>
/// <remarks>Implements AUTH-FACT-016 and AUTH-FACT-004.</remarks>
internal sealed record VerifyDeviceRequest(string? ChallengeId, string? Code);
