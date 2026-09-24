using System;
using Janus.Core;

namespace Janus.Hosting.Accounts;

/// <summary>
/// One mail app password as the mail server holds it, as the account application lists
/// it.
/// </summary>
/// <param name="Id">What the revocation endpoint names it by.</param>
/// <param name="Label">What the person called it.</param>
/// <param name="CreatedAt">When the server generated it.</param>
/// <param name="ExpiresAt">When it stops working, where one was set.</param>
/// <remarks>Implements REG-MAIL-002 and INT-MAIL-010 AC2.</remarks>
internal sealed record AppPasswordView(
    string Id,
    string Label,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt)
{
    /// <summary>
    /// Reads one app password.
    /// </summary>
    /// <param name="password">The app password.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The app password is absent.</exception>
    public static AppPasswordView Of(AppPassword password)
    {
        ArgumentNullException.ThrowIfNull(password);

        return new AppPasswordView(password.Id, password.Label, password.CreatedAt, password.ExpiresAt);
    }
}
