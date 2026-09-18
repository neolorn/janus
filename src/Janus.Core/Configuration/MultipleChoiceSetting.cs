using System.Collections.Generic;
using System.Linq;

namespace Janus.Core.Configuration;

/// <summary>
/// A setting whose value is a set drawn from a stated set, where the chapter may hold
/// some members in place and require the set to keep at least one.
/// </summary>
/// <typeparam name="TValue">The type of the stated values.</typeparam>
/// <remarks>Implements chapter 10 section 4 value types, OPS-CFG-003.</remarks>
public sealed class MultipleChoiceSetting<TValue> : Setting<IReadOnlySet<TValue>>
    where TValue : notnull
{
    internal MultipleChoiceSetting(
        string key,
        SettingScope scope,
        SettingDirection loosening,
        IReadOnlySet<TValue> fallback,
        IReadOnlySet<TValue> allowed,
        IReadOnlySet<TValue> unremovable,
        int minimum = 0)
        : base(key, scope, loosening, required: false, fallback)
    {
        Allowed = allowed;
        Unremovable = unremovable;
        Minimum = minimum;
    }

    /// <summary>
    /// The values the key admits.
    /// </summary>
    public IReadOnlySet<TValue> Allowed { get; }

    /// <summary>
    /// The members the chapter holds in place, which a change cannot drop.
    /// </summary>
    public IReadOnlySet<TValue> Unremovable { get; }

    /// <summary>
    /// The smallest set the key admits.
    /// </summary>
    public int Minimum { get; }

    /// <inheritdoc />
    public override Result<IReadOnlySet<TValue>> Accept(IReadOnlySet<TValue> value)
    {
        if (value is null || !value.All(Allowed.Contains) || value.Count < Minimum)
        {
            return Result.Failure<IReadOnlySet<TValue>>(
                Refused(ErrorCodes.ConfigurationValueNotAllowed, "allowed", string.Join(", ", Allowed)));
        }

        return Unremovable.All(value.Contains)
            ? Result.Success(value)
            : Result.Failure<IReadOnlySet<TValue>>(
                Refused(ErrorCodes.ConfigurationValueNotAllowed, "unremovable", string.Join(", ", Unremovable)));
    }
}
