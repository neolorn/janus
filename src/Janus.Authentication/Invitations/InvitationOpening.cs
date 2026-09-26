using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Invitations;

/// <summary>
/// Opening an invitation while signed in: the invitation a link opens attaches to the
/// signed-in person's account, which then reaches the membership step without a
/// registration.
/// </summary>
/// <param name="invitations">Where invitations are kept.</param>
/// <param name="work">The one transaction the opening runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements REG-INV-002 and IDN-LIFE-009a. The registration a signed-in browser asks
/// for is where a link is opened (REG-SESS-002), so its refusal and the opening are one
/// operation.
/// </remarks>
internal sealed class InvitationOpening(
    IInvitationStore invitations,
    IUnitOfWork work,
    TimeProvider time)
{
    /// <summary>
    /// Attaches the invitation a link opens to the signed-in person's account. The link
    /// is spent.
    /// </summary>
    /// <param name="context">Who is signed in.</param>
    /// <param name="token">The token the link carried.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, or the refusal: <c>identity.invitation.expired</c> where the token opens
    /// no invitation.
    /// </returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public async ValueTask<Result> OpenAsync(
        AccessContext context,
        [NeverLogged] string token,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(token);

        if (context.Effective is not SubjectId invitee)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        DateTimeOffset now = time.GetUtcNow();

        // IDN-LIFE-009a AC2: the link is single use, so the account that presses it is
        // the one it attaches to, and every token that opens nothing is answered alike.
        Invitation? invitation = await invitations
            .FindByTokenAsync(OpaqueToken.Of(token).Fingerprint(), cancellationToken)
            .ConfigureAwait(false);

        if (invitation is null || !invitation.Opens(now))
        {
            return Result.Failure(Error.From(ErrorCodes.InvitationExpired));
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        invitation.AttachTo(invitee, now);

        await invitations.RecordAsync(invitation, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
