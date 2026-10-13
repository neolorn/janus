using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Registration;

/// <summary>
/// What registration asks of the account directory: whether a value is already
/// somebody's or held out of reach for an undo, the language its holder reads, and the
/// one write that turns a finished registration into an account.
/// </summary>
/// <remarks>
/// Implements REG-SESS-001, REG-SESS-005, REG-SESS-007, REG-IDENT-006 and
/// CONV-DESIGN-003. The answer to the first two never reaches the person registering:
/// it decides only whether a code is sent and whether the owner is told.
/// </remarks>
internal interface IRegistrationDirectory
{
    /// <summary>
    /// The account a value already belongs to, where one does.
    /// </summary>
    /// <param name="kind">Which kind the value is.</param>
    /// <param name="canonical">The value in its canonical form.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The account holding it, or nothing.</returns>
    ValueTask<SubjectId?> OwnerAsync(
        IdentifierKind kind,
        string canonical,
        CancellationToken cancellationToken);

    /// <summary>
    /// Whether a value is held out of reach by a removal whose undo has not run out
    /// (REG-IDENT-006), which registration answers as a value somebody holds.
    /// </summary>
    /// <param name="kind">Which kind the value is.</param>
    /// <param name="canonical">The value in its canonical form.</param>
    /// <param name="now">The instant the undo window is judged at.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether the value is out of reach.</returns>
    ValueTask<bool> IsReservedAsync(
        IdentifierKind kind,
        string canonical,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Takes the lock on each value the registration is about to write to its account,
    /// held until the operation's transaction ends, so whether each is held or reserved
    /// is judged and the account written with no other transaction taking or reserving
    /// one in between (CONV-DESIGN-003, REG-SESS-005).
    /// </summary>
    /// <param name="values">The values, each with its kind and its canonical form.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of taking the locks.</returns>
    /// <exception cref="InvalidOperationException">No transaction is open.</exception>
    ValueTask LockValuesAsync(
        IReadOnlyList<(IdentifierKind Kind, string Canonical)> values,
        CancellationToken cancellationToken);

    /// <summary>
    /// The language an account settled on, which is what its holder is told in.
    /// </summary>
    /// <param name="subject">The account.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The language, or nothing where it settled none.</returns>
    ValueTask<string?> LanguageAsync(SubjectId subject, CancellationToken cancellationToken);

    /// <summary>
    /// Writes the account, its identifiers and what the person answered on the way
    /// in. The caller opens and commits the transaction it runs in.
    /// </summary>
    /// <param name="account">What the finished registration holds.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of creating it.</returns>
    ValueTask CreateAsync(NewAccount account, CancellationToken cancellationToken);
}
