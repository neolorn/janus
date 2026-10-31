namespace Janus.Hosting.Authentication;

/// <summary>
/// The sign-in the new-device check held, for which a new code is asked.
/// </summary>
/// <param name="ChallengeId">The handle the held sign-in carries.</param>
/// <remarks>Implements AUTH-FACT-016 and D-224 item 18.</remarks>
internal sealed record ResendDeviceRequest(string? ChallengeId);
