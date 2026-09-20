using System;
using Janus.Core;

namespace Janus.Hosting.Accounts;

/// <summary>
/// One identifier the account holds.
/// </summary>
/// <param name="Id">What the identifier endpoints name it by.</param>
/// <param name="Value">The value, canonicalised.</param>
/// <param name="Verified">Whether it has been proved.</param>
/// <param name="Primary">Whether it is the primary of its kind.</param>
/// <param name="Locked">Whether a provider supplied it and it cannot be changed.</param>
/// <remarks>Implements REG-ACCT-001 and REG-IDENT-002.</remarks>
internal sealed record IdentifierView(
    string Id,
    string Value,
    bool Verified,
    bool Primary,
    bool Locked)
{
    /// <summary>
    /// Reads one identifier.
    /// </summary>
    /// <param name="identifier">The identifier.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The identifier is absent.</exception>
    public static IdentifierView Of(IdentifierSummary identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);

        return new IdentifierView(
            identifier.Id.ToString(),
            identifier.Value,
            identifier.Verified,
            identifier.Primary,
            identifier.Locked);
    }
}
