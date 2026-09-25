namespace Janus.Core.Configuration;

/// <summary>
/// A setting whose value is free text the library does not constrain further: a
/// hosting location, an alert destination, a governing language.
/// </summary>
/// <remarks>Implements chapter 10 section 4 value types, LIB-HOST-001.</remarks>
public sealed class TextSetting : Setting<string>
{
    internal TextSetting(string key, SettingScope scope, string fallback)
        : base(key, scope, SettingDirection.AnyChange, required: false, fallback)
    {
    }

    internal TextSetting(string key, SettingScope scope)
        : base(key, scope, SettingDirection.AnyChange, required: true, fallback: string.Empty)
    {
    }

    /// <inheritdoc />
    public override Result<string> Accept(string value) => string.IsNullOrWhiteSpace(value)
        ? Result.Failure<string>(Refused(ErrorCodes.ConfigurationValueNotAllowed, "allowed", "a value"))
        : Result.Success(value);

    /// <inheritdoc />
    /// <inheritdoc />
    private protected override bool Textual => true;

    private protected override Result<string> Parse(string stored) => Result.Success(stored);

    /// <inheritdoc />
    private protected override string Render(string value) => value;
}
