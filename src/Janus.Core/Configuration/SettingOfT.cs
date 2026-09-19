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

    /// <summary>
    /// Reads the value from the text the settings table holds for the key.
    /// </summary>
    /// <param name="stored">The stored text.</param>
    /// <returns>
    /// The value, or the failure naming what the text misses: a form the key does not
    /// write, or a value a tightened constraint no longer admits.
    /// </returns>
    /// <remarks>Implements OPS-CFG-008, OPS-CFG-003.</remarks>
    public Result<TValue> Read(string stored) =>
        Parse(stored).Match(Accept, Result.Failure<TValue>);

    /// <summary>
    /// Writes the value as the settings table holds it.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The text to store.</returns>
    /// <remarks>Implements OPS-CFG-008.</remarks>
    public string Write(TValue value) => Render(value);

    /// <summary>
    /// Reads the key's own written form, before the constraints are applied to it.
    /// </summary>
    /// <param name="stored">The stored text.</param>
    /// <returns>The value, or the failure where the text is not of the key's type.</returns>
    private protected abstract Result<TValue> Parse(string stored);

    /// <summary>
    /// The value as the management application, the audit record and the settings
    /// table write it.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The written form.</returns>
    private protected abstract string Render(TValue value);

    /// <summary>
    /// The failure a text of the wrong form carries.
    /// </summary>
    /// <param name="expected">The form the key writes, as the chapter writes it.</param>
    /// <returns>The failure.</returns>
    private protected Result<TValue> NotOfTheType(string expected) =>
        Result.Failure<TValue>(Refused(ErrorCodes.ConfigurationValueNotAllowed, "allowed", expected));

    /// <summary>
    /// Reads a value the deployment named at startup, where a refused value is a fault
    /// the operator fixes rather than an outcome a caller handles.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The value.</returns>
    /// <exception cref="StartupException">The constraints do not admit the value.</exception>
    /// <remarks>Implements OPS-CFG-003, CONV-ERR-001.</remarks>
    public TValue AcceptAtStartup(TValue value) =>
        Accept(value).Match(
            accepted => accepted,
            failure => throw new StartupException(
                "The value named for " + Key + " is outside what the key admits.",
                failure));
}
