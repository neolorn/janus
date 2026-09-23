using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;

namespace Janus.Storage.Authentication.Oidc;

/// <summary>
/// The JSON columns the provider's rows carry, read and written in one place.
/// </summary>
/// <remarks>
/// Implements AUTH-OIDC-001 and CONV-DESIGN-003. A column that holds a document is
/// still a column: what goes into it is serialized here and nowhere else, so the two
/// directions cannot drift apart.
/// </remarks>
internal static class OidcProperties
{
    /// <summary>What an absent document is.</summary>
    public const string Empty = "{}";

    /// <summary>
    /// The properties a column holds.
    /// </summary>
    /// <param name="document">The column.</param>
    /// <returns>The properties, empty where the column holds none.</returns>
    public static ImmutableDictionary<string, JsonElement> Read(string document) =>
        document is not { Length: > 0 }
            ? ImmutableDictionary<string, JsonElement>.Empty
            : JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(document)
                ?.ToImmutableDictionary(StringComparer.Ordinal)
                ?? ImmutableDictionary<string, JsonElement>.Empty;

    /// <summary>
    /// The column those properties make.
    /// </summary>
    /// <param name="properties">The properties.</param>
    /// <returns>The column.</returns>
    public static string Write(ImmutableDictionary<string, JsonElement> properties) =>
        properties is null || properties.IsEmpty ? Empty : JsonSerializer.Serialize(properties);

    /// <summary>
    /// The wording a column holds, by culture.
    /// </summary>
    /// <param name="document">The column.</param>
    /// <returns>The wording, empty where the column holds none.</returns>
    public static ImmutableDictionary<CultureInfo, string> ReadByCulture(string document)
    {
        if (document is not { Length: > 0 })
        {
            return ImmutableDictionary<CultureInfo, string>.Empty;
        }

        Dictionary<string, string>? written =
            JsonSerializer.Deserialize<Dictionary<string, string>>(document);

        if (written is null)
        {
            return ImmutableDictionary<CultureInfo, string>.Empty;
        }

        ImmutableDictionary<CultureInfo, string>.Builder read =
            ImmutableDictionary.CreateBuilder<CultureInfo, string>();

        foreach (KeyValuePair<string, string> pair in written)
        {
            read[CultureInfo.GetCultureInfo(pair.Key)] = pair.Value;
        }

        return read.ToImmutable();
    }

    /// <summary>
    /// The column that wording makes.
    /// </summary>
    /// <param name="wording">The wording, by culture.</param>
    /// <returns>The column.</returns>
    public static string WriteByCulture(ImmutableDictionary<CultureInfo, string> wording)
    {
        if (wording is null || wording.IsEmpty)
        {
            return Empty;
        }

        Dictionary<string, string> written = new(StringComparer.Ordinal);

        foreach (KeyValuePair<CultureInfo, string> pair in wording)
        {
            written[pair.Key.Name] = pair.Value;
        }

        return JsonSerializer.Serialize(written);
    }
}
