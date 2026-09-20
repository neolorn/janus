using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Privacy.Requests;

/// <summary>
/// The account states the privacy area moves an account into: restricted while a
/// dispute is decided, and deleting when an erasure request is fulfilled.
/// </summary>
/// <remarks>
/// Implements PRIV-RIGHT-004, IDN-LIFE-003, IDN-LIFE-014 and CONV-DESIGN-003.
/// Restriction suspends action and never visibility, so nothing here hides, moves or
/// deletes a record.
/// </remarks>
internal interface IAccountStates
{
    /// <summary>
    /// Restricts one account.
    /// </summary>
    /// <param name="subject">Whose.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Whether the state changed: an account that is not active cannot be restricted,
    /// and one already restricted needs nothing.
    /// </returns>
    ValueTask<bool> RestrictAsync(SubjectId subject, CancellationToken cancellationToken);

    /// <summary>
    /// Starts the deletion grace window on an account, for an erasure request a human
    /// confirmed and fulfilled.
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
}
