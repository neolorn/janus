using System.Collections.Generic;
using System.Linq;

namespace Janus.Core.Configuration;

/// <summary>
/// The named restriction set governing every send: the one key of chapter 10 section
/// 4.5 whose value is a list of restrictions.
/// </summary>
/// <remarks>Implements chapter 10 sections 4.5 and 5.14 to 5.16, AUTH-ABUSE-004.</remarks>
public sealed class RestrictionSetSetting : Setting<IReadOnlyList<Restriction>>
{
    internal RestrictionSetSetting(
        string key,
        SettingScope scope,
        SettingDirection loosening,
        IReadOnlyList<Restriction> fallback)
        : base(key, scope, loosening, required: false, fallback)
    {
    }

    /// <inheritdoc />
    public override Result<IReadOnlyList<Restriction>> Accept(IReadOnlyList<Restriction> value) =>
        value is null || value.Any(restriction => restriction is null || restriction.Buckets.Count == 0)
            ? Result.Failure<IReadOnlyList<Restriction>>(
                Refused(ErrorCodes.ConfigurationValueNotAllowed, "allowed", "restrictions, each with at least one bucket"))
            : Result.Success(value);
}
