using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// The mail server that hosts the administrative organization's mailboxes, met through
/// its management interface and nowhere else. The library reads none of the server's
/// storage: what it knows of a mailbox is what it pushed and what a listing answered.
/// </summary>
/// <remarks>
/// Implements INT-MAIL-001, INT-MAIL-003, INT-MAIL-006, INT-MAIL-007, INT-MAIL-008,
/// INT-MAIL-009, INT-MAIL-010 and LIB-EXT-001. Optional: a deployment that registers
/// none hosts no mailbox, and an organization's addresses are then addresses like any
/// other. Outbound delivery is a separate registration, so either can be replaced
/// without the other. The app-password calls carry the person's token and act on the
/// account the server finds in it, never on one the library names.
/// </remarks>
public interface IMailServer
{
    /// <summary>
    /// Brings one mailbox to the state a push names.
    /// </summary>
    /// <param name="push">The mailbox, the state and the key the push is recognised by.</param>
    /// <param name="cancellationToken">Abandons the push.</param>
    /// <returns>
    /// Nothing once the server holds the mailbox in that state, or the failure where it
    /// could not be told. A push whose key the server has already applied changes
    /// nothing and succeeds.
    /// </returns>
    ValueTask<Result> ProvisionAsync(MailboxPush push, CancellationToken cancellationToken);

    /// <summary>
    /// Lists every mailbox the server hosts, which is what reconciliation compares.
    /// </summary>
    /// <param name="cancellationToken">Abandons the listing.</param>
    /// <returns>The mailboxes, or the failure where no listing could be had.</returns>
    ValueTask<Result<IReadOnlyList<HostedMailbox>>> MailboxesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Lists the app passwords the server holds for the person the token was issued to.
    /// </summary>
    /// <param name="accessToken">The person's token.</param>
    /// <param name="cancellationToken">Abandons the listing.</param>
    /// <returns>What the server holds, or the failure where no listing could be had.</returns>
    ValueTask<Result<IReadOnlyList<AppPassword>>> AppPasswordsAsync(
        string accessToken,
        CancellationToken cancellationToken);

    /// <summary>
    /// Has the server generate an app password for the person the token was issued to.
    /// </summary>
    /// <param name="accessToken">The person's token.</param>
    /// <param name="label">What the person calls it.</param>
    /// <param name="expiresAt">When it stops working, or nothing for no expiry.</param>
    /// <param name="cancellationToken">Abandons the call.</param>
    /// <returns>
    /// The secret the server generated, with its identifier, or the failure where the
    /// server generated none.
    /// </returns>
    ValueTask<Result<IssuedAppPassword>> CreateAppPasswordAsync(
        string accessToken,
        string label,
        DateTimeOffset? expiresAt,
        CancellationToken cancellationToken);

    /// <summary>
    /// Has the server revoke one of the app passwords of the person the token was issued
    /// to.
    /// </summary>
    /// <param name="accessToken">The person's token.</param>
    /// <param name="id">What the server calls it.</param>
    /// <param name="cancellationToken">Abandons the call.</param>
    /// <returns>
    /// Nothing once the server holds it no more, <c>auth.credential.notfound</c> where
    /// it holds no such app password for that person, or the failure where it could
    /// not be told.
    /// </returns>
    ValueTask<Result> RevokeAppPasswordAsync(
        string accessToken,
        string id,
        CancellationToken cancellationToken);
}
