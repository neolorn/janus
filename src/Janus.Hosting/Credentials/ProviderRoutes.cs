using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using Janus.Core;

namespace Janus.Hosting.Credentials;

/// <summary>
/// The social providers, as the library's routes name them.
/// </summary>
/// <remarks>
/// Implements IDN-LIFE-012, IDN-LIFE-012a AC3 and REG-IDENT-008. A route exists for each
/// provider the factor catalogue names as social and for no other, so a path naming
/// any other provider is not the library's.
/// </remarks>
internal static class ProviderRoutes
{
    /// <summary>
    /// Each provider by the segment its routes carry.
    /// </summary>
    public static FrozenDictionary<string, Factor> Named { get; } =
        new Dictionary<string, Factor>(StringComparer.Ordinal)
        {
            ["google"] = Factor.Google,
            ["apple"] = Factor.Apple,
        }.ToFrozenDictionary(StringComparer.Ordinal);
}
