using System;

namespace Janus.Core.Configuration;

/// <summary>
/// A family of a given type, which knows the value a member that was never written
/// gets, where the chapter states one.
/// </summary>
/// <typeparam name="TValue">The type of a member's value.</typeparam>
/// <remarks>Implements chapter 10 section 4, D-151.</remarks>
public sealed class SettingFamily<TValue> : SettingFamily
{
    private readonly TValue _fallback;

    internal SettingFamily(string prefix, SettingScope scope, SettingDirection loosening, TValue fallback)
        : base(prefix, scope, loosening)
    {
        _fallback = fallback;
        HasDefault = true;
    }

    internal SettingFamily(string prefix, SettingScope scope, SettingDirection loosening)
        : base(prefix, scope, loosening)
    {
        _fallback = default!;
        HasDefault = false;
    }

    /// <summary>
    /// Whether the library has a value for a member the deployment never wrote.
    /// </summary>
    public bool HasDefault { get; }

    /// <summary>
    /// The value a member that was never written gets.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The host declares the value for each member, so the library has none.
    /// </exception>
    public TValue Default => HasDefault
        ? _fallback
        : throw new InvalidOperationException("The host declares each " + Prefix + " value; the library has no default.");
}
