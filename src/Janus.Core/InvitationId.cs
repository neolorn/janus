using System;
using System.Globalization;

namespace Janus.Core;

/// <summary>
/// The identifier of one invitation into an organization.
/// </summary>
/// <param name="Value">The identifier as the database and the wire carry it.</param>
/// <remarks>Implements CONV-DESIGN-004, IDN-LIFE-009a and chapter 09 section 8a.</remarks>
public readonly record struct InvitationId(Guid Value)
{
    /// <summary>
    /// Issues an identifier for one invitation.
    /// </summary>
    /// <param name="time">The clock the deployment runs on.</param>
    /// <returns>An identifier ordered by the instant it was issued.</returns>
    /// <exception cref="ArgumentNullException">The clock is absent.</exception>
    public static InvitationId New(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);

        return new InvitationId(Guid.CreateVersion7(time.GetUtcNow()));
    }

    /// <inheritdoc/>
    public override string ToString() => Value.ToString("D", CultureInfo.InvariantCulture);
}
