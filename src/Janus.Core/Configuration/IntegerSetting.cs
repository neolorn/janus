using System.Globalization;

namespace Janus.Core.Configuration;

/// <summary>
/// A setting whose value is a whole number: a count, a length in bytes, a number of
/// working days, or any other bare number chapter 10 section 4 writes without a
/// decimal point.
/// </summary>
/// <remarks>Implements chapter 10 section 4 value types.</remarks>
public sealed class IntegerSetting : BoundedSetting<int>
{
    internal IntegerSetting(
        string key,
        SettingScope scope,
        int fallback,
        int? floor = null,
        int? ceiling = null,
        SettingDirection? loosening = null)
        : base(key, scope, required: false, fallback, floor, ceiling, loosening)
    {
    }

    private protected override string Render(int value) =>
        value.ToString(CultureInfo.InvariantCulture);
}
