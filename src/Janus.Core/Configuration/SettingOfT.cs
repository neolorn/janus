using System;

namespace Janus.Core.Configuration;

/// <summary>
/// A setting of a given type, which knows the value a deployment that names none gets
/// and refuses a value its constraints do not admit.
/// </summary>
/// <typeparam name="TValue">The type of the setting's value.</typeparam>
/// <remarks>
/// Implements OPS-CFG-003: a value outside a constraint is rejected at validation,
/// never silently clamped.
/// </remarks>
public abstract class Setting<TValue> : Setting
{
    private readonly TValue _fallback;

    private protected Setting(
        string key,
        SettingScope scope,
        SettingDirection loosening,
        bool required,
        TValue fallback)
        : base(key, scope, loosening, required) => _fallback = fallback;

    /// <summary>
    /// The value a deployment that names none gets, at the safe end of the setting's
    /// range (P-001).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The setting is one of the keys the deployment has to name (LIB-HOST-001), so
    /// there is no default to fall back to.
    /// </exception>
    public TValue Default => IsRequired
        ? throw new InvalidOperationException("The deployment names " + Key + "; it has no default.")
        : _fallback;

    /// <summary>
    /// Reads a value the deployment named, refusing one the constraints do not admit
    /// rather than clamping it.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The value, or the failure naming the constraint it missed.</returns>
    public abstract Result<TValue> Accept(TValue value);
}
