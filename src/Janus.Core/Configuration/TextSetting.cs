namespace Janus.Core.Configuration;

/// <summary>
/// A setting whose value is free text the library does not constrain further: a
/// hosting location, an alert destination, a governing language.
/// </summary>
/// <remarks>Implements chapter 10 section 4 value types, LIB-HOST-001.</remarks>
public sealed class TextSetting : Setting<string>
{
    internal TextSetting(string key, SettingScope scope, SettingDirection loosening, string fallback)
        : base(key, scope, loosening, required: false, fallback)
    {
    }

    internal TextSetting(string key, SettingScope scope, SettingDirection loosening)
        : base(key, scope, loosening, required: true, fallback: string.Empty)
    {
    }

    /// <inheritdoc />
    public override Result<string> Accept(string value) => string.IsNullOrWhiteSpace(value)
        ? Result.Failure<string>(Refused(ErrorCodes.ConfigurationValueNotAllowed, "allowed", "a value"))
        : Result.Success(value);
}
