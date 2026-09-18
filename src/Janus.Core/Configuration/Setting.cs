using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Janus.Core.Configuration;

/// <summary>
/// One key of chapter 10 section 4 as the library holds it: its name, whether the
/// application may change it, which way a change loosens the deployment, and whether
/// the deployment has to name a value.
/// </summary>
/// <remarks>
/// Implements chapter 10 section 4, LIB-API-001, OPS-CFG-001, OPS-CFG-002,
/// OPS-CFG-004, LIB-HOST-001. The catalogue is <see cref="Settings"/>.
/// </remarks>
public abstract class Setting
{
    private protected Setting(string key, SettingScope scope, SettingDirection loosening, bool required)
    {
        Key = ConfigurationKey.Parse(key);
        Scope = scope;
        Loosening = loosening;
        IsRequired = required;
    }

    /// <summary>
    /// The key, as chapter 10 section 4 names it.
    /// </summary>
    public ConfigurationKey Key { get; }

    /// <summary>
    /// Whether the application may change the setting.
    /// </summary>
    public SettingScope Scope { get; }

    /// <summary>
    /// Which way a change loosens the deployment.
    /// </summary>
    public SettingDirection Loosening { get; }

    /// <summary>
    /// Whether the deployment has to name a value: true for the keys of LIB-HOST-001,
    /// which name the deployment and cannot be guessed, and false everywhere else,
    /// where the default sits at the safe end of the range.
    /// </summary>
    public bool IsRequired { get; }

    /// <summary>
    /// The failure a refused value carries: the code, the key, and the constraint it
    /// missed, so the caller is told which end it crossed rather than having the
    /// value clamped under it.
    /// </summary>
    /// <param name="code">The code, from chapter 10 section 1.5.</param>
    /// <param name="constraint">The name of the constraint the value missed.</param>
    /// <param name="expected">The constraint, written as the management application shows it.</param>
    /// <returns>The failure.</returns>
    /// <remarks>Implements OPS-CFG-003.</remarks>
    private protected Error Refused(ErrorCode code, string constraint, string expected) =>
        new(
            code,
            new Dictionary<string, JsonElement>(capacity: 2, StringComparer.Ordinal)
            {
                ["key"] = JsonSerializer.SerializeToElement(Key.ToString()),
                [constraint] = JsonSerializer.SerializeToElement(expected),
            });
}
