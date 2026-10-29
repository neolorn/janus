namespace Janus.Hosting.Records;

/// <summary>
/// What a staff member's correction of a person record carries, as the request reads.
/// </summary>
/// <param name="Name">The name it takes, or nothing to leave it as it is.</param>
/// <param name="Language">The language it takes, or nothing to leave it as it is.</param>
/// <param name="Email">The email it holds, or nothing to leave it as it is.</param>
/// <param name="Phone">The phone it holds, or nothing to leave it as it is.</param>
/// <param name="Reason">Why it is corrected.</param>
/// <remarks>Implements chapter 09 section 8a and REC-ADM-002.</remarks>
internal sealed record RecordCorrectionBody(
    string? Name,
    string? Language,
    string? Email,
    string? Phone,
    string? Reason);
