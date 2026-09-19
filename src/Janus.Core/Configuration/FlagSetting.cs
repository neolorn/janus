namespace Janus.Core.Configuration;

/// <summary>
/// A setting that is on or off. Every flag a deployment does not set is on the safe
/// side of its range (P-001), so turning one off is the loosening.
/// </summary>
/// <remarks>Implements chapter 10 section 4 value types, OPS-CFG-002.</remarks>
public sealed class FlagSetting : Setting<bool>
{
    private const string On = "true";
    private const string Off = "false";

    internal FlagSetting(string key, SettingScope scope, bool fallback)
        : base(
            key,
            scope,
            fallback ? SettingDirection.Decrease : SettingDirection.Increase,
            required: false,
            fallback)
    {
    }

    /// <inheritdoc />
    public override Result<bool> Accept(bool value) => Result.Success(value);

    /// <inheritdoc />
    private protected override Result<bool> Parse(string stored) => stored switch
    {
        On => Result.Success(true),
        Off => Result.Success(false),
        _ => NotOfTheType(On + " or " + Off),
    };

    /// <inheritdoc />
    private protected override string Render(bool value) => value ? On : Off;
}
