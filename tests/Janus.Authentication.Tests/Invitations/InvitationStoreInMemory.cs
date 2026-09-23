using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Invitations;
using Janus.Authentication.Mailboxes;
using Janus.Core;

namespace Janus.Authentication.Tests.Invitations;

/// <summary>
/// The invitations held in memory, as the rows hold them.
/// </summary>
/// <remarks>
/// CONV-TEST-004: a fake that keeps what it is given and refuses what the table
/// refuses, so a second invitation standing over one mailbox fails here as it fails
/// against the partial unique index.
/// </remarks>
internal sealed class InvitationStoreInMemory : IInvitationStore
{
    /// <summary>
    /// Every invitation, oldest first.
    /// </summary>
    public List<Invitation> Held { get; } = [];

    /// <inheritdoc/>
    public ValueTask<Invitation?> FindAsync(InvitationId id, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Held.FirstOrDefault(invitation => invitation.Id == id));

    /// <inheritdoc/>
    public ValueTask<Invitation?> FindByTokenAsync(byte[] token, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Held.FirstOrDefault(invitation => invitation.Token.AsSpan().SequenceEqual(token)));

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<Invitation>> ReservingAsync(
        MailboxId mailbox,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<Invitation>>(
            [.. Held.Where(invitation => invitation.Mailbox == mailbox && invitation.Stands)]);

    /// <inheritdoc/>
    public ValueTask AddAsync(Invitation invitation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invitation);

        if (invitation.Mailbox is MailboxId mailbox
            && Held.Any(held => held.Mailbox == mailbox && held.Stands))
        {
            throw new InvalidOperationException("An invitation already stands over the mailbox.");
        }

        Held.Add(invitation);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RecordAsync(Invitation invitation, CancellationToken cancellationToken)
    {
        if (!Held.Contains(invitation))
        {
            throw new InvalidOperationException("The invitation has no row to carry the change.");
        }

        return ValueTask.CompletedTask;
    }
}
