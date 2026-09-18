using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Janus.Core.Configuration;

/// <summary>
/// A setting whose value is a list of whole numbers in the order the chapter states,
/// where some members are held in place: the COSE algorithms a relying party accepts.
/// </summary>
/// <remarks>Implements chapter 10 section 4 value types, AUTH-FACT-014.</remarks>
public sealed class IntegerListSetting : Setting<IReadOnlyList<int>>
{
    internal IntegerListSetting(
        string key,
        SettingScope scope,
        SettingDirection loosening,
        IReadOnlyList<int> fallback,
        IReadOnlySet<int> unremovable)
        : base(key, scope, loosening, required: false, fallback) => Unremovable = unremovable;

    /// <summary>
    /// The members the chapter holds in place, which a change cannot drop.
    /// </summary>
    public IReadOnlySet<int> Unremovable { get; }

    /// <inheritdoc />
    public override Result<IReadOnlyList<int>> Accept(IReadOnlyList<int> value) =>
        value is not null && Unremovable.All(value.Contains)
            ? Result.Success(value)
            : Result.Failure<IReadOnlyList<int>>(
                Refused(
                    ErrorCodes.ConfigurationValueNotAllowed,
                    "unremovable",
                    string.Join(", ", Unremovable.Select(member => member.ToString(CultureInfo.InvariantCulture)))));
}
