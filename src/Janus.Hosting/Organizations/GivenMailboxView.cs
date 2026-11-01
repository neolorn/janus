using System;
using Janus.Core;

namespace Janus.Hosting.Organizations;

/// <summary>
/// A mailbox given to a current member, as the response carries it.
/// </summary>
/// <param name="Id">The mailbox.</param>
/// <param name="Address">Its address, in its canonical form.</param>
/// <param name="State">The state it is owed, chapter 10 section 5.45.</param>
/// <remarks>Implements chapter 09 section 8a and REG-MAIL-001 (D-253).</remarks>
internal sealed record GivenMailboxView(Guid Id, string Address, MailboxState State)
{
    /// <summary>
    /// The view of one mailbox given.
    /// </summary>
    /// <param name="given">The mailbox.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The mailbox is absent.</exception>
    public static GivenMailboxView Of(GivenMailbox given)
    {
        ArgumentNullException.ThrowIfNull(given);

        return new(given.Id.Value, given.Address.Value, given.State);
    }
}
