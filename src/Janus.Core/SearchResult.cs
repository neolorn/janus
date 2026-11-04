namespace Janus.Core;

/// <summary>
/// One subject a staff search found, with its display name and no email or phone value:
/// a host reads its contacts through the contact read.
/// </summary>
/// <param name="Subject">The subject.</param>
/// <param name="Kind">Whether it is an account or a person record.</param>
/// <param name="DisplayName">Its display name, read at the search, or nothing where it has none.</param>
/// <param name="Matched">The field that matched first, in the order name, email, phone.</param>
/// <remarks>Implements IDN-SRCH-001 and LIB-API-005 (D-256).</remarks>
public sealed record SearchResult(
    SubjectId Subject,
    SubjectKind Kind,
    [property: NeverLogged] string? DisplayName,
    SearchMatch Matched);
