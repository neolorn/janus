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
        SettingDirection loosening,
        TimeSpan fallback,
        TimeSpan? floor = null,
        TimeSpan? ceiling = null)
        : base(key, scope, loosening, required: false, fallback, floor, ceiling)
    {
    }

    private protected override string Render(TimeSpan value) => XmlConvert.ToString(value);
}
