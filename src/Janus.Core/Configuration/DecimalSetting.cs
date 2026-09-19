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
        decimal fallback,
        decimal? floor = null,
        decimal? ceiling = null,
        SettingDirection? loosening = null)
        : base(key, scope, required: false, fallback, floor, ceiling, loosening)
    {
    }

    internal DecimalSetting(string key, SettingScope scope)
        : base(
            key,
            scope,
            required: true,
            fallback: default,
            floor: null,
            ceiling: null,
            SettingDirection.AnyChange)
    {
    }

    private protected override string Render(decimal value) =>
        value.ToString(CultureInfo.InvariantCulture);

    /// <inheritdoc />
    private protected override Result<decimal> Parse(string stored) =>
        decimal.TryParse(stored, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal number)
            ? Result.Success(number)
            : NotOfTheType("a number");
}
