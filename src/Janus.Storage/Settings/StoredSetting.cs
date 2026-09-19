using System;
using Janus.Core.Configuration;

namespace Janus.Storage.Settings;

/// <summary>
/// The value in force for one runtime-changeable configuration key. A key the
/// deployment has never changed has no row and reads as the default the catalogue
/// gives it.
/// </summary>
/// <remarks>
/// Implements OPS-CFG-008. The settings live in the library's own schema so that a
/// change made through the management application needs no restart. Neither bootstrap
/// value is ever a row here: the database connection and the secrets-manager
/// credential are injected at deployment, and the two keys fetched from the secrets
/// manager at startup are read before this table can be.
/// </remarks>
internal sealed class StoredSetting
{
    private StoredSetting(ConfigurationKey key, string value)
    {
        Key = key;
        Value = value;
    }

    /// <summary>
    /// The key the row carries the value of.
    /// </summary>
    public ConfigurationKey Key { get; }

    /// <summary>
    /// The value in force, written as the key's type writes it.
    /// </summary>
    public string Value { get; private set; }

    /// <summary>
    /// Records the value a deployment has put in force for a key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    /// <returns>The setting as it is stored.</returns>
    /// <exception cref="ArgumentNullException">The value is absent.</exception>
    public static StoredSetting InForce(ConfigurationKey key, string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return new StoredSetting(key, value);
    }

    /// <summary>
    /// Puts another value in force for the key. What may change, in which direction and
    /// under what step-up is OPS-CFG-002's business, not the row's.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <exception cref="ArgumentNullException">The value is absent.</exception>
    public void Change(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        Value = value;
    }
}
