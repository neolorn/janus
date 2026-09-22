using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Identity.Accounts;

/// <summary>
/// Where accounts are read and written.
/// </summary>
/// <remarks>Implements IDN-ACCT-001 and CONV-DESIGN-003.</remarks>
internal interface IAccountStore
{
    /// <summary>
    /// Reads the account an identifier was issued to.
    /// </summary>
    /// <param name="subject">Whose account to read.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The account, or nothing where the identifier was never issued.</returns>
    ValueTask<Account?> FindBySubjectAsync(SubjectId subject, CancellationToken cancellationToken);

    /// <summary>
    /// Records a newly created account.
    /// </summary>
    /// <param name="account">The account to record.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask AddAsync(Account account, CancellationToken cancellationToken);

    /// <summary>
    /// Carries a transition the account has made onto the row. Which transitions exist
    /// and which are permitted is the account's business, not the store's.
    /// </summary>
    /// <param name="account">The account as it now stands.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording the transition.</returns>
    /// <exception cref="System.InvalidOperationException">The account has no row.</exception>
    ValueTask RecordTransitionAsync(Account account, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the accounts standing in a deletion grace window that began on or before
    /// an instant.
    /// </summary>
    /// <param name="before">The instant the window must have begun by.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Them, oldest window first.</returns>
    ValueTask<IReadOnlyList<Account>> DeletingSinceAsync(
        DateTimeOffset before,
        CancellationToken cancellationToken);
}
