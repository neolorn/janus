using System;
using Janus.Core;

namespace Janus.Hosting.Accounts;

/// <summary>
/// What a host needs to write to a person, as
/// <c>GET /admin/accounts/{subject}/contact</c> answers it.
/// </summary>
/// <param name="Kind">Whether the subject is an account or a person record.</param>
/// <param name="DisplayName">The name the person is shown by.</param>
/// <param name="Email">The primary verified email address, or null.</param>
/// <param name="Phone">The primary verified phone number, or null.</param>
/// <param name="Language">The language the person reads.</param>
/// <param name="TimeZone">The zone their times are rendered in.</param>
/// <param name="State">Where the account stands.</param>
/// <remarks>Implements IDN-READ-003, REC-ADM-001 and chapter 09 section 8a.</remarks>
internal sealed record ContactView(
    SubjectKind Kind,
    string? DisplayName,
    string? Email,
    string? Phone,
    string? Language,
    string? TimeZone,
    AccountState State)
{
    /// <summary>
    /// Reads the contact details.
    /// </summary>
    /// <param name="contact">The contact details.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The contact details are absent.</exception>
    public static ContactView Of(ContactDetail contact)
    {
        ArgumentNullException.ThrowIfNull(contact);

        return new ContactView(
            contact.Kind,
            contact.DisplayName,
            contact.Email,
            contact.Phone,
            contact.Language,
            contact.TimeZone,
            contact.State);
    }
}
