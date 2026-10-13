using System;
using System.Globalization;

namespace Janus.Core;

/// <summary>
/// The identifier of one mailbox the library provisions.
/// </summary>
/// <param name="Value">The identifier as the database and the mail server carry it.</param>
/// <remarks>
/// Implements CONV-DESIGN-004, INT-MAIL-001 and INT-MAIL-006. It is what an alert names
/// in place of the address, which is personal data and stays out of the alert, and what
/// a push carries to the mail server, which writes it on the account it creates and
/// reads it back before it acts on one it finds.
/// </remarks>
public readonly record struct MailboxId(Guid Value)
{
    /// <summary>
    /// Issues an identifier for a mailbox reserved at one instant.
    /// </summary>
    /// <param name="reservedAt">When it was reserved.</param>
    /// <returns>An identifier ordered by that instant.</returns>
    public static MailboxId Of(DateTimeOffset reservedAt) =>
        new(Guid.CreateVersion7(reservedAt));

    /// <inheritdoc/>
    public override string ToString() => Value.ToString("D", CultureInfo.InvariantCulture);
}
