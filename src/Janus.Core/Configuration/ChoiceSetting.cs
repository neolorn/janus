using System;
using System.Collections.Generic;

namespace Janus.Core.Configuration;

/// <summary>
/// A setting whose value is one of a stated set: the enums of chapter 10 section 4.
/// </summary>
/// <typeparam name="TValue">The type of the stated values.</typeparam>
/// <remarks>Implements chapter 10 section 4 value types, OPS-CFG-003.</remarks>
public sealed class ChoiceSetting<TValue> : Setting<TValue>
    where TValue : notnull
{
    internal ChoiceSetting(
        string key,
        SettingScope scope,
        TValue fallback,
        IReadOnlySet<TValue> allowed,
        SettingDirection? loosening = null)
        : base(key, scope, loosening ?? SettingDirection.AnyChange, required: false, fallback) =>
        Allowed = allowed;

    internal ChoiceSetting(string key, SettingScope scope, IReadOnlySet<TValue> allowed)
        : base(key, scope, SettingDirection.AnyChange, required: true, fallback: default!) =>
        Allowed = allowed;

    /// <summary>
    /// The values the key admits.
    /// </summary>
    public IReadOnlySet<TValue> Allowed { get; }

    /// <inheritdoc />
    public override Result<TValue> Accept(TValue value) => Allowed.Contains(value)
        ? Result.Success(value)
        : Result.Failure<TValue>(
            Refused(ErrorCodes.ConfigurationValueNotAllowed, "allowed", string.Join(", ", Allowed)));

    /// <inheritdoc />
    private protected override Result<TValue> Parse(string stored)
    {
        foreach (TValue candidate in Allowed)
        {
            if (string.Equals(SettingText.Of(candidate), stored, StringComparison.Ordinal))
            {
                return Result.Success(candidate);
            }
        }

        return NotOfTheType(string.Join(", ", Allowed));
    }

    /// <inheritdoc />
    private protected override string Render(TValue value) => SettingText.Of(value);
}
