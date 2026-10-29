using System;
using Janus.Core;

namespace Janus.Hosting.Accounts;

/// <summary>
/// Whether a password is set, as <c>GET /account</c> and the enrolment document answer
/// it.
/// </summary>
/// <param name="Set">Whether a password is set.</param>
/// <param name="ChangedAt">When it was last set; null where none is.</param>
/// <remarks>Implements chapter 09 <c>GET /account</c> and <c>GET /enrol</c>, FE-SEC-001 and D-224.</remarks>
internal sealed record PasswordView(bool Set, DateTimeOffset? ChangedAt)
{
    /// <summary>
    /// The view of a status.
    /// </summary>
    /// <param name="status">The status.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The status is absent.</exception>
    public static PasswordView Of(PasswordStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        return new PasswordView(status.Set, status.ChangedAt);
    }
}
