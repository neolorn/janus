using System;

namespace Janus.Core.Configuration;

/// <summary>
/// A setting on an ordered scale, with the floor and the ceiling chapter 10 section 4
/// declares for it. A value below the floor or above the ceiling is refused.
/// </summary>
/// <typeparam name="TValue">The type of the setting's value.</typeparam>
/// <remarks>Implements OPS-CFG-003, chapter 10 section 4.</remarks>
public abstract class BoundedSetting<TValue> : Setting<TValue>
    where TValue : struct, IComparable<TValue>
{
    private protected BoundedSetting(
        string key,
        SettingScope scope,
        bool required,
        TValue fallback,
        TValue? floor,
        TValue? ceiling,
        SettingDirection? loosening)
        : base(key, scope, loosening ?? DirectionFrom(floor, ceiling), required, fallback)
    {
        Floor = floor;
        Ceiling = ceiling;
    }

    /// <summary>
    /// The enforced minimum, where the chapter declares one.
    /// </summary>
    public TValue? Floor { get; }

    /// <summary>
    /// The enforced maximum, where the chapter declares one.
    /// </summary>
    public TValue? Ceiling { get; }

    /// <inheritdoc />
    public override Result<TValue> Accept(TValue value)
    {
        if (Floor is TValue floor && value.CompareTo(floor) < 0)
        {
            return Result.Failure<TValue>(
                Refused(ErrorCodes.ConfigurationValueBelowFloor, "floor", Render(floor)));
        }

        if (Ceiling is TValue ceiling && value.CompareTo(ceiling) > 0)
        {
            return Result.Failure<TValue>(
                Refused(ErrorCodes.ConfigurationValueAboveCeiling, "ceiling", Render(ceiling)));
        }

        return Result.Success(value);
    }
}
