using System;
using System.Collections.Generic;
using System.Text.Json;

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
    /// Whether changing the value from one to another loosens the deployment, which is
    /// what decides whether the change costs step-up, a written reason and an audit
    /// entry.
    /// </summary>
    /// <param name="before">The value in force.</param>
    /// <param name="after">The value it would become.</param>
    /// <returns>Whether the change loosens.</returns>
    /// <remarks>
    /// Implements OPS-CFG-002 and the chapter 10 section 4 direction paragraph. A key
    /// that loosens upward loosens on a greater value, one that loosens downward on a
    /// lesser, and one with no direction on any change at all, which is what D-079b
    /// classifies a setting with no direction as.
    /// </remarks>
    public virtual bool Loosens(TValue before, TValue after) => Loosening switch
    {
        SettingDirection.Increase => Comparer<TValue>.Default.Compare(after, before) > 0,
        SettingDirection.Decrease => Comparer<TValue>.Default.Compare(after, before) < 0,
        _ => !EqualityComparer<TValue>.Default.Equals(before, after),
    };

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
    /// Hands the setting, typed, to an operation that works on any key.
    /// </summary>
    /// <typeparam name="TResult">What the operation answers.</typeparam>
    /// <param name="operation">The operation.</param>
    /// <returns>What it answered.</returns>
    /// <remarks>Implements CONV-CODE-004: a generic serves where reflection would.</remarks>
    internal sealed override TResult Apply<TResult>(ISettingOperation<TResult> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return operation.On(this);
    }

    /// <summary>
    /// The value in the key's own type as JSON, as the administration interface writes
    /// it (chapter 09 section 8, chapter 10 section 4 value types).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The JSON value.</returns>
    internal JsonElement Json(TValue value)
    {
        string written = Render(value);

        if (Textual)
        {
            return JsonSerializer.SerializeToElement(written);
        }

        using var document = JsonDocument.Parse(written);

        return document.RootElement.Clone();
    }

    /// <summary>
    /// Reads a JSON value the administration interface received, refusing one of
    /// another JSON type as it refuses one the constraints do not admit.
    /// </summary>
    /// <param name="value">The JSON value.</param>
    /// <returns>The value, or the failure naming the key.</returns>
    /// <remarks>
    /// Implements chapter 10 section 1.5: <c>config.value.notallowed</c> is also the
    /// answer to a value of the wrong type. A key written as text takes a JSON string
    /// and no other key does, so a number is never read as a name.
    /// </remarks>
    internal Result<TValue> Received(JsonElement value) => (Textual, value.ValueKind) switch
    {
        (true, JsonValueKind.String) => Read(value.GetString()!),
        (false, not (JsonValueKind.String or JsonValueKind.Null or JsonValueKind.Undefined)) =>
            Read(value.GetRawText()),
        _ => Result.Failure<TValue>(
            Error.From(
                ErrorCodes.ConfigurationValueNotAllowed,
                "key",
                JsonSerializer.SerializeToElement(Key.ToString()))),
    };

    /// <summary>
    /// Gets a value indicating whether the key writes its value as plain text rather
    /// than as JSON: a string, a duration or a member of an enum.
    /// </summary>
    private protected virtual bool Textual => false;

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
