using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Janus.Core.Configuration;

/// <summary>
/// A setting whose value is a list of text: relying party origins, alert destination
/// lists, and the other bracketed lists of chapter 10 section 4.
/// </summary>
/// <remarks>Implements chapter 10 section 4 value types, LIB-HOST-001.</remarks>
public sealed class TextListSetting : Setting<IReadOnlyList<string>>
{
    internal TextListSetting(
        string key,
        SettingScope scope,
        IReadOnlyList<string> fallback,
        int minimum = 0)
        : base(key, scope, SettingDirection.AnyChange, required: false, fallback) =>
        Minimum = minimum;

    internal TextListSetting(string key, SettingScope scope, int minimum)
        : base(key, scope, SettingDirection.AnyChange, required: true, fallback: []) =>
        Minimum = minimum;

    /// <summary>
    /// The shortest list the key admits.
    /// </summary>
    public int Minimum { get; }

    /// <inheritdoc />
    public override Result<IReadOnlyList<string>> Accept(IReadOnlyList<string> value) =>
        value is null || value.Count < Minimum || value.Any(string.IsNullOrWhiteSpace)
            ? Result.Failure<IReadOnlyList<string>>(
                Refused(ErrorCodes.ConfigurationValueNotAllowed, "minimum", Minimum.ToString(CultureInfo.InvariantCulture)))
            : Result.Success(value);
}
