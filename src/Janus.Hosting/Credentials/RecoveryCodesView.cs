using System;
using System.Collections.Generic;
using Janus.Core;

namespace Janus.Hosting.Credentials;

/// <summary>
/// A generated set of recovery codes, returned once and never read back.
/// </summary>
/// <param name="Codes">The codes.</param>
/// <param name="GeneratedAt">When the set was made.</param>
/// <remarks>Implements AUTH-FACT-008 and AUTH-FACT-009.</remarks>
[NeverLogged]
internal sealed record RecoveryCodesView(IReadOnlyList<string> Codes, DateTimeOffset GeneratedAt)
{
    /// <summary>
    /// Reads a generated set.
    /// </summary>
    /// <param name="generated">The set.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The set is absent.</exception>
    public static RecoveryCodesView Of(GeneratedRecoveryCodes generated)
    {
        ArgumentNullException.ThrowIfNull(generated);

        return new RecoveryCodesView(generated.Codes, generated.GeneratedAt);
    }
}
