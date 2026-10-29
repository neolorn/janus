namespace Janus.Hosting.Records;

/// <summary>
/// Why staff act on a person record where the row takes nothing but the reason.
/// </summary>
/// <param name="Reason">The written reason, which is recorded.</param>
/// <remarks>Implements chapter 09 section 8a, REC-ADM-002 and D-223.</remarks>
internal sealed record RecordReasonBody(string? Reason);
