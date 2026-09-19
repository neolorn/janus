using System.Collections.Generic;
using System.Linq;

namespace Janus.Core.Configuration;

/// <summary>
/// A setting whose value is a set of text the library does not draw from a stated
/// vocabulary: the domains registered with a private relay, and the other sets of
/// chapter 10 section 4 whose members the deployment names.
/// </summary>
/// <remarks>Implements chapter 10 section 4 value types, INT-MAIL-011.</remarks>
public sealed class TextSetSetting : Setting<IReadOnlySet<string>>
{
    internal TextSetSetting(string key, SettingScope scope, IReadOnlySet<string> fallback)
        : base(key, scope, SettingDirection.AnyChange, required: false, fallback)
    {
    }

    /// <inheritdoc />
    public override Result<IReadOnlySet<string>> Accept(IReadOnlySet<string> value) =>
        value is null || value.Any(string.IsNullOrWhiteSpace)
            ? Result.Failure<IReadOnlySet<string>>(
                Refused(ErrorCodes.ConfigurationValueNotAllowed, "allowed", "a set of values"))
            : Result.Success(value);
}
