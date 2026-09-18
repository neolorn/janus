using System;
using System.Collections.Generic;

namespace Janus.Core.Configuration;

/// <summary>
/// A setting whose value is a list of dates: the public holidays staff maintain as
/// they are announced. Its safe default is empty, because an unlisted holiday counts
/// as a working day and makes a deadline earlier, which is always compliant.
/// </summary>
/// <remarks>Implements chapter 10 section 4 value types, PRIV-RIGHT-002.</remarks>
public sealed class DateListSetting : Setting<IReadOnlyList<DateOnly>>
{
    internal DateListSetting(string key, SettingScope scope, SettingDirection loosening)
        : base(key, scope, loosening, required: false, fallback: [])
    {
    }

    /// <inheritdoc />
    public override Result<IReadOnlyList<DateOnly>> Accept(IReadOnlyList<DateOnly> value) =>
        value is null
            ? Result.Failure<IReadOnlyList<DateOnly>>(
                Refused(ErrorCodes.ConfigurationValueNotAllowed, "allowed", "a list of dates"))
            : Result.Success(value);
}
