using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Erasures;

namespace Janus.Privacy.Requests;

/// <summary>
/// The account states the privacy area moves an account into: restricted while a
/// dispute is decided, deleting when an erasure request is fulfilled, and taken down
/// until the takedown's window ends or it is reversed.
/// </summary>
/// <remarks>
/// Implements PRIV-RIGHT-004, IDN-LIFE-003, IDN-LIFE-014, AUTH-SESS-010 and
/// CONV-DESIGN-003.
/// Restriction suspends action and never visibility, so nothing here hides, moves or
/// deletes a record.
/// </remarks>
internal interface IAccountStates
{
    /// <summary>
    /// Restricts one account and ends every session of it in the same transaction; one
    /// suspended or in its deletion window holds the restriction, comes back restricted
    /// and ends nothing, having no session to end.
    /// </summary>
    /// <param name="subject">Whose.</param>
    /// <param name="at">When, which is when its sessions end.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Whether the restriction is new: one already restricted or held needs nothing,
    /// and an erased account cannot be restricted.
    /// </returns>
    ValueTask<bool> RestrictAsync(SubjectId subject, DateTimeOffset at, CancellationToken cancellationToken);

    /// <summary>
    /// Starts the deletion grace window on an account, for an erasure request a human
    /// confirmed and fulfilled: an active or restricted account enters it, and a
    /// suspended one enters it holding the suspension, which a cancellation returns.
    /// </summary>
    /// <param name="subject">Whose.</param>
    /// <param name="origin">What started it.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether the state changed.</returns>
    ValueTask<bool> BeginDeletionAsync(
        SubjectId subject,
        DeletionOrigin origin,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// The accounts whose deletion grace window began on or before an instant and
    /// which nothing has cancelled.
    /// </summary>
    /// <param name="before">The instant the window must have begun by.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Them, oldest window first.</returns>
    ValueTask<IReadOnlyList<PendingDeletion>> DeletingSinceAsync(
        DateTimeOffset before,
        CancellationToken cancellationToken);

    /// <summary>
    /// Where an account stands.
    /// </summary>
    /// <param name="subject">Whose.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Its standing, or nothing where no such account exists.</returns>
    ValueTask<AccountStanding?> StandingAsync(SubjectId subject, CancellationToken cancellationToken);

    /// <summary>
    /// Takes an account down: it passes through suspension into the takedown's window,
    /// and every session of it ends in the same transaction.
    /// </summary>
    /// <param name="subject">Whose.</param>
    /// <param name="at">When the takedown was triggered.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Whether the state changed: an account already taken down or deleted cannot be
    /// taken down.
    /// </returns>
    ValueTask<bool> TakeDownAsync(
        SubjectId subject,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reverses a takedown and restores the state the account held at the trigger,
    /// judging the window under a lock on the account.
    /// </summary>
    /// <param name="subject">Whose.</param>
    /// <param name="now">The instant of the reversal.</param>
    /// <param name="windows">The lengths the takedown's window is measured by.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Whether the state changed: only an account deleting by a takedown whose window
    /// is still open is reversed.
    /// </returns>
    ValueTask<bool> ReverseTakedownAsync(
        SubjectId subject,
        DateTimeOffset now,
        DeletionWindows windows,
        CancellationToken cancellationToken);
}
