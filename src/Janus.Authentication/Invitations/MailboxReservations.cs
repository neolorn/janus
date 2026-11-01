using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Mailboxes;
using Janus.Authentication.Organizations;
using Janus.Core;

namespace Janus.Authentication.Invitations;

/// <summary>
/// The rules a mailbox is reserved for an address under, whether an invitation reserves
/// it or a current member is given it, and the writing of the reservation.
/// </summary>
/// <param name="mailboxes">Where the mailboxes are read and written.</param>
/// <param name="invitations">Where the invitations reserving a mailbox are read and withdrawn.</param>
/// <param name="audit">Where an invitation withdrawn for the address is written down.</param>
/// <remarks>
/// Implements REG-MAIL-001 and REG-MAIL-003 (D-178, D-253, D-255). One mailbox stands for
/// an address, and one invitation over it. A mailbox an account holds is taken; an
/// invitation still open over it is taken too, until it is revoked; one that expired
/// unacknowledged is replaced. A mailbox someone has held passes on only under the
/// choice named; an address whose last holder was erased is found no more, so it is one
/// never held, and <c>transfer</c> or <c>replace</c> where no held mailbox stands is
/// refused. <c>adopt</c> reserves a new mailbox whose push may take over the account the
/// mail server holds, and is refused where the library already holds a mailbox at the
/// address.
/// </remarks>
internal sealed class MailboxReservations(
    IMailboxStore mailboxes,
    IInvitationStore invitations,
    IOrganizationAudit audit)
{
    private const string FormerMailboxMember = "formerMailbox";

    /// <summary>
    /// Judges a reservation of an address, writing nothing.
    /// </summary>
    /// <param name="address">The address.</param>
    /// <param name="choice">What the request chose for a mailbox the address had, or nothing.</param>
    /// <param name="member">The request's member the address was named by.</param>
    /// <param name="now">The instant of the request.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The reservation, or the refusal: <c>identity.mailbox.taken</c> naming the member,
    /// <c>identity.invitation.mailboxheld</c>, or <c>api.request.invalid</c> naming
    /// <c>formerMailbox</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public async ValueTask<Result<MailboxReservation>> ReservedAsync(
        EmailAddress address,
        FormerMailbox? choice,
        string member,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(member);

        if (await mailboxes.FindAsync(address, cancellationToken).ConfigureAwait(false) is not Mailbox existing)
        {
            return choice switch
            {
                null => Result.Success(new MailboxReservation(Mailbox.Reserved(address, now), IsNew: true, [], Former: null)),
                FormerMailbox.Adopt => Result.Success(
                    new MailboxReservation(Mailbox.ReservedToAdopt(address, now), IsNew: true, [], Former: null)),
                _ => Result.Failure<MailboxReservation>(Named(ErrorCodes.RequestInvalid, FormerMailboxMember)),
            };
        }

        if (existing.IsHeld)
        {
            return Result.Failure<MailboxReservation>(Named(ErrorCodes.MailboxTaken, member));
        }

        IReadOnlyList<Invitation> standing = await invitations
            .ReservingAsync(existing.Id, cancellationToken)
            .ConfigureAwait(false);

        if (standing.Any(invitation => !invitation.HasExpired(now)))
        {
            return Result.Failure<MailboxReservation>(Named(ErrorCodes.MailboxTaken, member));
        }

        // D-255: the library already holds a mailbox at the address, so there is no
        // account of the mail server's own for the push to take over.
        if (choice is FormerMailbox.Adopt)
        {
            return Result.Failure<MailboxReservation>(Named(ErrorCodes.RequestInvalid, FormerMailboxMember));
        }

        if (!existing.WasHeld)
        {
            return choice is null
                ? Result.Success(new MailboxReservation(existing, IsNew: false, standing, Former: null))
                : Result.Failure<MailboxReservation>(Named(ErrorCodes.RequestInvalid, FormerMailboxMember));
        }

        return choice switch
        {
            null => Result.Failure<MailboxReservation>(Error.From(ErrorCodes.InvitationMailboxHeld)),
            FormerMailbox.Transfer => Result.Success(new MailboxReservation(existing, IsNew: false, standing, Former: null)),
            _ => Result.Success(new MailboxReservation(Mailbox.Reserved(address, now), IsNew: true, standing, Former: existing)),
        };
    }

    /// <summary>
    /// Writes a reservation onto the transaction in progress: the invitations it replaces
    /// withdrawn, the mailbox it replaces stood aside, and the mailbox added or reserved
    /// again.
    /// </summary>
    /// <param name="reservation">The reservation judged.</param>
    /// <param name="acting">Who reserves it.</param>
    /// <param name="breakGlassReason">The break-glass reason the change is made under, or nothing.</param>
    /// <param name="now">The instant of the request.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of writing it.</returns>
    /// <exception cref="ArgumentNullException">The reservation is absent.</exception>
    public async ValueTask ReserveAsync(
        MailboxReservation reservation,
        SubjectId acting,
        string? breakGlassReason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reservation);

        foreach (Invitation replaced in reservation.Replaced)
        {
            await WithdrawAsync(replaced, acting, breakGlassReason, now, cancellationToken).ConfigureAwait(false);
        }

        // REG-MAIL-003, D-178: the old mailbox stands aside, owed its removal, before
        // the new one takes the address.
        if (reservation.Former is Mailbox former)
        {
            former.Replace(now);

            await mailboxes.RecordAsync(former, cancellationToken).ConfigureAwait(false);
        }

        if (reservation.IsNew)
        {
            await mailboxes.AddAsync(reservation.Mailbox, cancellationToken).ConfigureAwait(false);

            return;
        }

        reservation.Mailbox.Reserve();

        await mailboxes.RecordAsync(reservation.Mailbox, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Revokes an invitation and writes the revocation down.
    /// </summary>
    /// <param name="invitation">The invitation.</param>
    /// <param name="acting">Who revokes it.</param>
    /// <param name="breakGlassReason">The break-glass reason the change is made under, or nothing.</param>
    /// <param name="now">The instant of the request.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of revoking it.</returns>
    /// <exception cref="ArgumentNullException">The invitation is absent.</exception>
    public async ValueTask WithdrawAsync(
        Invitation invitation,
        SubjectId acting,
        string? breakGlassReason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invitation);

        invitation.Revoke(now);

        await invitations.RecordAsync(invitation, cancellationToken).ConfigureAwait(false);
        await audit
            .InvitationChangedAsync(
                AuditActions.InvitationRevoked,
                invitation.Organization,
                invitation.Id,
                takeover: null,
                acting,
                breakGlassReason,
                now,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static Error Named(ErrorCode code, string member) =>
        Error.From(code, "member", JsonSerializer.SerializeToElement(member));
}
