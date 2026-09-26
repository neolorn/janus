using System;
using System.Collections.Generic;
using System.Text.Json;

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
    private readonly SettingForm<TValue> _form;

    internal SettingFamily(string prefix, SettingScope scope, SettingForm<TValue> form, TValue fallback)
        : base(prefix, scope, DirectionFrom(fallback))
    {
        _fallback = fallback;
        _form = form;
        HasDefault = true;
    }

    internal SettingFamily(string prefix, SettingScope scope, SettingForm<TValue> form)
        : base(prefix, scope, SettingDirection.AnyChange)
    {
        _fallback = default!;
        _form = form;
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

    /// <summary>
    /// Reads a member's value from the text the settings table holds for it.
    /// </summary>
    /// <param name="parameter">The organization identifier or the declared category.</param>
    /// <param name="stored">The stored text.</param>
    /// <returns>The value, or the failure naming what the text misses.</returns>
    /// <remarks>Implements OPS-CFG-008.</remarks>
    public Result<TValue> Read(string parameter, string stored) =>
        _form.Parse(stored, Malformed(parameter));

    /// <summary>
    /// Writes a member's value as the settings table holds it.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The text to store.</returns>
    /// <remarks>Implements OPS-CFG-008.</remarks>
    public string Write(TValue value) => _form.Render(value);

    /// <summary>
    /// Whether changing a member's value from one to another loosens the deployment, by
    /// the rule <see cref="Setting{TValue}.Loosens"/> applies to a key that exists once.
    /// </summary>
    /// <param name="before">The value in force.</param>
    /// <param name="after">The value it would become.</param>
    /// <returns>Whether the change loosens.</returns>
    /// <remarks>Implements OPS-CFG-002 and the chapter 10 section 4 direction paragraph.</remarks>
    public bool Loosens(TValue before, TValue after) => Loosening switch
    {
        SettingDirection.Increase => Comparer<TValue>.Default.Compare(after, before) > 0,
        SettingDirection.Decrease => Comparer<TValue>.Default.Compare(after, before) < 0,
        _ => !EqualityComparer<TValue>.Default.Equals(before, after),
    };

    /// <summary>
    /// Hands the family, typed, to an operation that works on any family.
    /// </summary>
    /// <typeparam name="TResult">What the operation answers.</typeparam>
    /// <param name="operation">The operation.</param>
    /// <returns>What it answered.</returns>
    /// <remarks>Implements CONV-CODE-004: a generic serves where reflection would.</remarks>
    internal sealed override TResult Apply<TResult>(ISettingFamilyOperation<TResult> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return operation.On(this);
    }

    private Error Malformed(string parameter) => new(
        ErrorCodes.ConfigurationValueNotAllowed,
        new Dictionary<string, JsonElement>(capacity: 2, StringComparer.Ordinal)
        {
            ["key"] = JsonSerializer.SerializeToElement(For(parameter).ToString()),
            ["allowed"] = JsonSerializer.SerializeToElement(_form.Expected),
        });

    // The chapter 10 section 4 direction rule as it reaches a family: a boolean
    // loosens away from its default, and a family of any other type states no
    // direction, so every change to a member carries the friction of a loosening
    // (D-079b, D-152).
    private static SettingDirection DirectionFrom(TValue fallback) => fallback switch
    {
        bool flag => flag ? SettingDirection.Decrease : SettingDirection.Increase,
        _ => SettingDirection.AnyChange,
    };
}
