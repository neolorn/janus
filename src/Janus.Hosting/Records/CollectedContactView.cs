using System;
using Janus.Authentication.Records;
using Janus.Core;

namespace Janus.Hosting.Records;

/// <summary>
/// A staged contact, as the collection session's state shows it.
/// </summary>
/// <param name="Kind">An email or a phone.</param>
/// <param name="Value">Its canonical value.</param>
/// <param name="Verified">Whether its code verified it.</param>
/// <remarks>Implements REC-COL-001, chapter 09 section 7a.</remarks>
internal sealed record CollectedContactView(IdentifierKind Kind, string Value, bool Verified)
{
    /// <summary>
    /// Shows a staged contact.
    /// </summary>
    /// <param name="contact">The contact.</param>
    /// <returns>Its view.</returns>
    /// <exception cref="ArgumentNullException">The contact is absent.</exception>
    public static CollectedContactView Of(CollectedContact contact)
    {
        ArgumentNullException.ThrowIfNull(contact);

        return new CollectedContactView(contact.Kind, contact.Value, contact.IsVerified);
    }
}
