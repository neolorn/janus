using System.Globalization;

namespace Janus.Core.Configuration;

/// <summary>
/// A setting whose value is a number chapter 10 section 4 writes with a decimal
/// point, such as the multiplier a progressive delay grows by.
/// </summary>
/// <remarks>Implements chapter 10 section 4 value types.</remarks>
public sealed class DecimalSetting : BoundedSetting<decimal>
{
    internal DecimalSetting(
        string key,
        SettingScope scope,
        SettingDirection loosening,
        decimal fallback,
        decimal? floor = null,
        decimal? ceiling = null)
        : base(key, scope, loosening, required: false, fallback, floor, ceiling)
    {
    }

    internal DecimalSetting(string key, SettingScope scope, SettingDirection loosening)
        : base(key, scope, loosening, required: true, fallback: default, floor: null, ceiling: null)
    {
    }

    private protected override string Render(decimal value) =>
        value.ToString(CultureInfo.InvariantCulture);
}
