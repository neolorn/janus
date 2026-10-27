namespace Janus.Core;

/// <summary>
/// What a host needs to write to a person.
/// </summary>
/// <param name="DisplayName">The name the person is shown by.</param>
/// <param name="Email">The primary verified email address, or nothing.</param>
/// <param name="Phone">The primary verified phone number, or nothing.</param>
/// <param name="Language">The language the person reads, as a BCP 47 tag.</param>
/// <param name="TimeZone">The zone their times are rendered in, as an IANA identifier.</param>
/// <param name="State">Where the account stands.</param>
/// <remarks>Implements IDN-READ-003.</remarks>
public sealed record ContactDetail(
    string? DisplayName,
    string? Email,
    string? Phone,
    string? Language,
    string? TimeZone,
    AccountState State);
