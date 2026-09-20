using System;
using System.Globalization;

namespace Janus.Core;

/// <summary>
/// The identifier of one refresh token family: the first token a code exchange issued
/// and every rotation that followed it.
/// </summary>
/// <param name="Value">The identifier as the database carries it.</param>
/// <remarks>
/// Implements CONV-DESIGN-004 and AUTH-OIDC-003. A token presented twice revokes the
/// family it belongs to, which is how a copy in someone else's hands is caught without
/// knowing whose copy was used first.
/// </remarks>
public readonly record struct RefreshFamilyId(Guid Value)
{
    /// <summary>
    /// Issues an identifier for a family a code exchange is opening.
    /// </summary>
    /// <param name="time">The clock the deployment runs on.</param>
    /// <returns>An identifier ordered by the instant it was issued.</returns>
    /// <exception cref="ArgumentNullException">The clock is absent.</exception>
    public static RefreshFamilyId New(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);

        return new RefreshFamilyId(Guid.CreateVersion7(time.GetUtcNow()));
    }

    /// <inheritdoc/>
    public override string ToString() => Value.ToString("D", CultureInfo.InvariantCulture);
}
