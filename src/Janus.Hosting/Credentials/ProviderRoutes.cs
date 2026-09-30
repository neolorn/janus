using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    private static readonly JsonSerializerOptions Spelled =
        new() { Converters = { new JsonStringEnumConverter() } };

    /// <summary>
    /// Each provider by the segment its routes carry.
    /// </summary>
    public static FrozenDictionary<string, Factor> Named { get; } =
        new Dictionary<string, Factor>(StringComparer.Ordinal)
        {
            ["google"] = Factor.Google,
            ["apple"] = Factor.Apple,
        }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    /// A provider's name, as the factor catalogue spells it: the name its routes carry,
    /// its credential is read from the secret source by, and its refusals name it by.
    /// </summary>
    /// <param name="provider">Which factor the declaration names.</param>
    /// <returns>The name.</returns>
    public static string NameOf(Factor provider) =>
        JsonSerializer.SerializeToElement(provider, Spelled).GetString()!;
}
