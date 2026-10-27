using System;
using Janus.Core;

namespace Janus.Hosting.Accounts;

/// <summary>
/// One email address or phone number of another person's account.
/// </summary>
/// <param name="Value">The form the person entered.</param>
/// <param name="Verified">Whether it counts.</param>
/// <param name="Primary">Whether it is the primary of its kind.</param>
/// <remarks>Implements IDN-READ-003.</remarks>
internal sealed record HeldIdentifierView(string Value, bool Verified, bool Primary)
{
    /// <summary>
    /// Reads one.
    /// </summary>
    /// <param name="identifier">The identifier.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The identifier is absent.</exception>
    public static HeldIdentifierView Of(AccountViewIdentifier identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);

        return new HeldIdentifierView(identifier.Value, identifier.Verified, identifier.Primary);
    }
}
