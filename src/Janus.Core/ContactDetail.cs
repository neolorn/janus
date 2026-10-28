namespace Janus.Core;

/// <summary>
/// What a host needs to write to a person, an account's holder or a person record's.
/// </summary>
/// <param name="Kind">Whether the subject is an account or a person record.</param>
/// <param name="DisplayName">The name the person is shown by, a record's name.</param>
/// <param name="Email">
/// The primary verified email address, a record's email as held, or nothing.
/// </param>
/// <param name="Phone">
/// The primary verified phone number, a record's phone as held, or nothing.
/// </param>
/// <param name="Language">The language the person reads, as a BCP 47 tag.</param>
/// <param name="TimeZone">
/// The zone their times are rendered in, as an IANA identifier; none for a record.
/// </param>
/// <param name="State">Where the account or the record stands.</param>
/// <remarks>Implements IDN-READ-003 and REC-ADM-001.</remarks>
public sealed record ContactDetail(
    SubjectKind Kind,
    string? DisplayName,
    string? Email,
    string? Phone,
    string? Language,
    string? TimeZone,
    AccountState State);
