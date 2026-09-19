using System;
using System.Globalization;

namespace Janus.Core;

/// <summary>
/// The identifier of one browser the account knows: a trusted device or one
/// remembered by the new-device check.
/// </summary>
/// <param name="Value">The identifier as the database and the wire carry it.</param>
/// <remarks>
/// Implements CONV-DESIGN-004, AUTH-FACT-015 and AUTH-FACT-016. Every identifier but
/// the subject's is a version 7 value, so rows written together sit together in the
/// index.
/// </remarks>
public readonly record struct DeviceId(Guid Value)
{
    /// <summary>
    /// Issues an identifier for one browser.
    /// </summary>
    /// <param name="time">The clock the deployment runs on.</param>
    /// <returns>An identifier ordered by the instant it was issued.</returns>
    /// <exception cref="ArgumentNullException">The clock is absent.</exception>
    public static DeviceId New(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);

        return new DeviceId(Guid.CreateVersion7(time.GetUtcNow()));
    }

    /// <inheritdoc/>
    public override string ToString() => Value.ToString("D", CultureInfo.InvariantCulture);
}
