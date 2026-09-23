namespace Janus.Hosting.Privacy;

/// <summary>
/// Why a takedown is reversed.
/// </summary>
/// <param name="Reason">The written reason, which is recorded.</param>
/// <remarks>Implements chapter 09 section 8a and IDN-LIFE-003.</remarks>
internal sealed record TakedownReversalBody(string? Reason);
