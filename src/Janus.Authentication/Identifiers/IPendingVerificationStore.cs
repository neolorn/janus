using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Identifiers;

/// <summary>
/// Where the verifications a live account has outstanding are read and written.
/// </summary>
/// <remarks>
/// Implements REG-IDENT-004, REG-IDENT-007 and CONV-DESIGN-003. One verification is
/// outstanding per identifier at a time: a second add sends again on the same one, and
/// a second replace is refused while one is staged.
/// </remarks>
internal interface IPendingVerificationStore
{
    /// <summary>
    /// The verification outstanding against an identifier.
    /// </summary>
    /// <param name="identifier">Which identifier.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The verification, or nothing where none is outstanding.</returns>
    ValueTask<PendingVerification?> FindAsync(
        IdentifierId identifier,
        CancellationToken cancellationToken);

    /// <summary>
    /// The verification outstanding against an identifier, read under a lock on its row
    /// held until the operation's transaction ends, so a code, a link or a confirmation
    /// judged on it cannot race another (CONV-DESIGN-003).
    /// </summary>
    /// <param name="identifier">Which identifier.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The verification as committed when the lock was taken, or nothing.</returns>
    /// <exception cref="System.InvalidOperationException">No transaction is open.</exception>
    ValueTask<PendingVerification?> FindForUpdateAsync(
        IdentifierId identifier,
        CancellationToken cancellationToken);

    /// <summary>
    /// The verification a link answers to, whichever of its two links it is.
    /// </summary>
    /// <param name="fingerprint">The fingerprint of the token the link carried.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The verification, or nothing where none answers to it.</returns>
    ValueTask<PendingVerification?> FindByLinkAsync(
        byte[] fingerprint,
        CancellationToken cancellationToken);

    /// <summary>
    /// The adds an account has pending, which it lists as unverified identifiers and
    /// counts toward each kind's maximum until they verify (REG-IDENT-004).
    /// </summary>
    /// <param name="subject">Whose adds.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The pending adds, the earliest staged first; no replace is among them.</returns>
    ValueTask<IReadOnlyList<PendingVerification>> AddsOfAsync(
        SubjectId subject,
        CancellationToken cancellationToken);

    /// <summary>
    /// Stages a verification.
    /// </summary>
    /// <param name="pending">The verification.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of staging it.</returns>
    ValueTask AddAsync(PendingVerification pending, CancellationToken cancellationToken);

    /// <summary>
    /// Carries a verification as it now stands onto its row.
    /// </summary>
    /// <param name="pending">The verification.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask RecordAsync(PendingVerification pending, CancellationToken cancellationToken);

    /// <summary>
    /// Ends a verification, whether because it completed or because it was abandoned.
    /// </summary>
    /// <param name="identifier">Which identifier's verification.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of ending it.</returns>
    ValueTask RemoveAsync(IdentifierId identifier, CancellationToken cancellationToken);

    /// <summary>
    /// Ends every verification that no verification-code record holds any longer: one
    /// whose code and, for a replace, whose confirmation are each spent or past their
    /// lifetime. An add goes with nothing else to remove, and a replace leaves the
    /// identifier as it stood (REG-IDENT-004, REG-IDENT-007, OPS-OBS-003).
    /// </summary>
    /// <param name="now">The instant a record's lifetime is judged at.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>How many were ended.</returns>
    ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
