using System;
using System.Xml;

namespace Janus.Core.Configuration;

/// <summary>
/// A setting whose value is a length of time, written in ISO 8601.
/// </summary>
/// <remarks>Implements chapter 10 section 4 value types.</remarks>
public sealed class DurationSetting : BoundedSetting<TimeSpan>
{
    internal DurationSetting(
        string key,
        SettingScope scope,
        string fallback,
        string? floor = null,
        string? ceiling = null,
        SettingDirection? loosening = null)
        : base(
            key,
            scope,
            required: false,
            Duration.Parse(fallback),
            floor is null ? null : Duration.Parse(floor),
            ceiling is null ? null : Duration.Parse(ceiling),
            loosening)
    {
    }

    /// <inheritdoc />
    private protected override bool Textual => true;

    private protected override string Render(TimeSpan value) => XmlConvert.ToString(value);

    /// <inheritdoc />
    private protected override Result<TimeSpan> Parse(string stored) =>
        Duration.TryParse(stored, out TimeSpan duration)
            ? Result.Success(duration)
            : NotOfTheType("an ISO 8601 duration");
}
