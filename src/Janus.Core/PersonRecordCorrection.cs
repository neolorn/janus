namespace Janus.Core;

/// <summary>
/// What staff correct in a person record on the person's word, each member left out
/// left as it is.
/// </summary>
/// <param name="Name">The name, or nothing to leave it.</param>
/// <param name="Language">The language, one of <c>notification.languages</c>, or nothing to leave it.</param>
/// <param name="Email">The email, or nothing to leave it.</param>
/// <param name="Phone">The phone, or nothing to leave it.</param>
/// <param name="Reason">Why, in the words of the staff member.</param>
/// <remarks>Implements REC-ADM-002.</remarks>
public sealed record PersonRecordCorrection(
    string? Name,
    string? Language,
    string? Email,
    string? Phone,
    string Reason);
