using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Janus.Core.Configuration;

/// <summary>
/// How a configuration value is written in the settings table, which is the form the
/// wire contract writes it in: the name a member of a stated set carries there, and
/// a JSON array or object for a value with parts.
/// </summary>
/// <remarks>
/// Implements OPS-CFG-008 and chapter 10 section 4 value types. The table is read by
/// an operator as well as by the library, so a stored value reads as the chapter
/// writes it rather than as a number.
/// </remarks>
internal static class SettingText
{
    private static readonly JsonSerializerOptions Written = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>
    /// The written form of a value of a stated set.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The written form.</returns>
    public static string Of(object value) => value switch
    {
        string text => text,
        Enum member => JsonSerializer.SerializeToElement(member, member.GetType(), Written).GetString()!,
        IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    /// <summary>
    /// Reads a member of a stated set from its written form.
    /// </summary>
    /// <typeparam name="TEnum">The set.</typeparam>
    /// <param name="text">The written form.</param>
    /// <param name="member">The member, where the form names one.</param>
    /// <returns>Whether the form names a member.</returns>
    public static bool TryRead<TEnum>(string text, out TEnum member)
        where TEnum : struct, Enum
    {
        foreach (TEnum candidate in Enum.GetValues<TEnum>())
        {
            if (string.Equals(Of(candidate), text, StringComparison.Ordinal))
            {
                member = candidate;
                return true;
            }
        }

        member = default;
        return false;
    }

    /// <summary>
    /// The written form of a list, which the settings table holds as a JSON array.
    /// </summary>
    /// <typeparam name="TMember">The type of the members.</typeparam>
    /// <param name="values">The members, in order.</param>
    /// <returns>The written form.</returns>
    public static string OfList<TMember>(IEnumerable<TMember> values) =>
        JsonSerializer.Serialize(values, Written);

    /// <summary>
    /// Reads a list from the written form.
    /// </summary>
    /// <typeparam name="TMember">The type of the members.</typeparam>
    /// <param name="stored">The stored text.</param>
    /// <param name="malformed">The failure a text that is not a list of them carries.</param>
    /// <returns>The members, or the failure.</returns>
    public static Result<IReadOnlyList<TMember>> List<TMember>(string stored, Error malformed)
    {
        try
        {
            return JsonSerializer.Deserialize<TMember[]>(stored, Written) is { } values
                ? Result.Success<IReadOnlyList<TMember>>(values)
                : Result.Failure<IReadOnlyList<TMember>>(malformed);
        }
        catch (JsonException)
        {
            return Result.Failure<IReadOnlyList<TMember>>(malformed);
        }
    }

    /// <summary>
    /// Reads an object from the written form.
    /// </summary>
    /// <typeparam name="TShape">The shape the key is written in.</typeparam>
    /// <param name="stored">The stored text.</param>
    /// <param name="malformed">The failure a text that is not of the shape carries.</param>
    /// <returns>The object, or the failure.</returns>
    public static Result<TShape> Shape<TShape>(string stored, Error malformed)
    {
        try
        {
            return JsonSerializer.Deserialize<TShape>(stored, Written) is { } value
                ? Result.Success(value)
                : Result.Failure<TShape>(malformed);
        }
        catch (JsonException)
        {
            return Result.Failure<TShape>(malformed);
        }
    }

    /// <summary>
    /// The written form of an object.
    /// </summary>
    /// <typeparam name="TShape">The shape the key is written in.</typeparam>
    /// <param name="value">The object.</param>
    /// <returns>The written form.</returns>
    public static string OfShape<TShape>(TShape value) => JsonSerializer.Serialize(value, Written);
}
