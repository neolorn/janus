using System;
using Janus.Core;

namespace Janus.Hosting.Registration;

/// <summary>
/// One staged identifier, as the state document shows it.
/// </summary>
/// <param name="Id">What the verification and change endpoints name it by.</param>
/// <param name="Kind">Which kind it is.</param>
/// <param name="Value">The value, canonicalised.</param>
/// <param name="Verified">Whether it has been proved.</param>
/// <param name="Locked">Whether a provider supplied it and it cannot be changed.</param>
/// <remarks>Implements REG-SESS-002 and REG-IDENT-010.</remarks>
internal sealed record StagedIdentifierView(
    string Id,
    IdentifierKind Kind,
    string Value,
    bool Verified,
    bool Locked)
{
    /// <summary>
    /// Reads one staged identifier.
    /// </summary>
    /// <param name="staged">The identifier.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The identifier is absent.</exception>
    public static StagedIdentifierView Of(StagedIdentifier staged)
    {
        ArgumentNullException.ThrowIfNull(staged);

        return new StagedIdentifierView(
            staged.Id.ToString(),
            staged.Kind,
            staged.Value,
            staged.Verified,
            staged.Locked);
    }
}
