using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// The mail app passwords of the signed-in person, managed at the mail server through
/// the library's first-party client for it. The library stores nothing about them.
/// </summary>
/// <remarks>
/// Implements LIB-API-005, REG-MAIL-002 and INT-MAIL-010. Each operation obtains a
/// token for the person from the session named and makes one call to the server with
/// it. Creation and revocation are the <c>mailcredential:create</c> and
/// <c>mailcredential:revoke</c> step-up actions, each notified to the security-notice
/// set and audited; listing is neither. Every operation answers <c>authz.denied</c>
/// where the account holds no enabled mailbox, which includes a deployment that hosts
/// none.
/// </remarks>
public interface IAppPasswords
{
    /// <summary>
    /// What the mail server holds for the person, read from it each time.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="session">The session the request came in under.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The app passwords, or the refusal or the server's failure.</returns>
    ValueTask<Result<IReadOnlyList<AppPassword>>> ListAsync(
        AccessContext context,
        SessionId session,
        CancellationToken cancellationToken);

    /// <summary>
    /// Has the mail server generate an app password for the person.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="session">The session the request came in under.</param>
    /// <param name="label">What the person calls it.</param>
    /// <param name="expiresAt">When it stops working, or nothing for no expiry.</param>
    /// <param name="source">The address the request came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The generated secret with its identifier, returned once; or the refusal:
    /// <c>auth.stepup.required</c>, <c>auth.credential.labelinvalid</c> where the
    /// label is empty or too long; or the server's failure.
    /// </returns>
    ValueTask<Result<IssuedAppPassword>> CreateAsync(
        AccessContext context,
        SessionId session,
        string label,
        DateTimeOffset? expiresAt,
        string source,
        CancellationToken cancellationToken);

    /// <summary>
    /// Has the mail server revoke one of the person's app passwords, so that a client
    /// using it fails on its next connection.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="session">The session the request came in under.</param>
    /// <param name="id">What the server calls it.</param>
    /// <param name="source">The address the request came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, or the refusal: <c>auth.stepup.required</c>,
    /// <c>auth.credential.notfound</c> where the server holds no such app password for
    /// the person; or the server's failure.
    /// </returns>
    ValueTask<Result> RevokeAsync(
        AccessContext context,
        SessionId session,
        string id,
        string source,
        CancellationToken cancellationToken);
}
