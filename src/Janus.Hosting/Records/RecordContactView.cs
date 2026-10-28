using System;
using Janus.Authentication.Records;
using Janus.Core;

namespace Janus.Hosting.Records;

/// <summary>
/// A contact a person record holds, as its read shows it.
/// </summary>
/// <param name="Kind">An email or a phone.</param>
/// <param name="Value">Its canonical value.</param>
/// <param name="VerifiedAt">When a collection last verified it, or nothing where none did.</param>
/// <remarks>Implements REC-ACC-002, chapter 09 section 7a.</remarks>
internal sealed record RecordContactView(IdentifierKind Kind, string Value, DateTimeOffset? VerifiedAt)
{
    /// <summary>
    /// Shows a record's contact.
    /// </summary>
    /// <param name="contact">The contact.</param>
    /// <returns>Its view.</returns>
    /// <exception cref="ArgumentNullException">The contact is absent.</exception>
    public static RecordContactView Of(CollectedContact contact)
    {
        ArgumentNullException.ThrowIfNull(contact);

        return new RecordContactView(contact.Kind, contact.Value, contact.VerifiedAt);
    }
}
