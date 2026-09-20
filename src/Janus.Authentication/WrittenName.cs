using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Janus.Authentication;

/// <summary>
/// The written name of a member of one of the fixed enumerations, which is the form
/// chapter 10 gives it and the form that crosses the boundary.
/// </summary>
/// <remarks>
/// Implements CONV-NAME-001 and chapter 10 section 5. The name lives on the member
/// itself, so one place reads it and no table of strings can drift from it.
/// </remarks>
internal static class WrittenName
{
    private static readonly JsonSerializerOptions Written =
        new() { Converters = { new JsonStringEnumConverter() } };

    /// <summary>
    /// What one member is written as.
    /// </summary>
    /// <typeparam name="TMember">The enumeration.</typeparam>
    /// <param name="member">The member.</param>
    /// <returns>Its name.</returns>
    public static string Of<TMember>(TMember member)
        where TMember : struct, Enum =>
        JsonSerializer.Serialize(member, Written).Trim('"');
}
