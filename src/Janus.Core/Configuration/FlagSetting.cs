namespace Janus.Core.Configuration;

/// <summary>
/// A setting that is on or off. Every flag a deployment does not set is on the safe
/// side of its range (P-001), so turning one off is the loosening.
/// </summary>
/// <remarks>Implements chapter 10 section 4 value types, OPS-CFG-002.</remarks>
public sealed class FlagSetting : Setting<bool>
{
    internal FlagSetting(string key, SettingScope scope, SettingDirection loosening, bool fallback)
        : base(key, scope, loosening, required: false, fallback)
    {
    }

    /// <inheritdoc />
    public override Result<bool> Accept(bool value) => Result.Success(value);
}
