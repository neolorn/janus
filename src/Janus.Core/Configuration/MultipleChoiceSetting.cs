using System;
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
        IReadOnlySet<TValue> fallback,
        IReadOnlySet<TValue> allowed,
        IReadOnlySet<TValue> unremovable,
        int minimum = 0,
        SettingDirection? loosening = null)
        : base(key, scope, loosening ?? SettingDirection.AnyChange, required: false, fallback)
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
    /// <remarks>
    /// A set loosens by what it gained or lost, never by a comparison of the two: a key
    /// that loosens upward loosens where a member was added, and one that loosens
    /// downward where a member was dropped (OPS-CFG-002, chapter 10 section 4).
    /// </remarks>
    /// <exception cref="ArgumentNullException">Either set is absent.</exception>
    public override bool Loosens(IReadOnlySet<TValue> before, IReadOnlySet<TValue> after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        return Loosening switch
        {
            SettingDirection.Increase => after.Any(one => !before.Contains(one)),
            SettingDirection.Decrease => before.Any(one => !after.Contains(one)),
            _ => !after.SetEquals(before),
        };
    }

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

    /// <inheritdoc />
    private protected override Result<IReadOnlySet<TValue>> Parse(string stored) =>
        SettingText
            .List<string>(stored, Malformed())
            .Match(Members, Result.Failure<IReadOnlySet<TValue>>);

    /// <inheritdoc />
    private protected override string Render(IReadOnlySet<TValue> value) =>
        SettingText.OfList(value.Select(member => SettingText.Of(member)));

    private Result<IReadOnlySet<TValue>> Members(IReadOnlyList<string> written)
    {
        var read = new HashSet<TValue>();

        foreach (string name in written)
        {
            bool found = false;

            foreach (TValue candidate in Allowed)
            {
                if (string.Equals(SettingText.Of(candidate), name, StringComparison.Ordinal))
                {
                    read.Add(candidate);
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                return Result.Failure<IReadOnlySet<TValue>>(Malformed());
            }
        }

        return Result.Success<IReadOnlySet<TValue>>(read);
    }

    private Error Malformed() =>
        Refused(ErrorCodes.ConfigurationValueNotAllowed, "allowed", string.Join(", ", Allowed));
}
