using Janus.Core.Configuration;

namespace Janus.Storage.Settings;

/// <summary>
/// The <c>settings</c> row: the value in force for one runtime-changeable
/// configuration key. A key the deployment has never changed has no row and reads as
/// the default the catalogue gives it.
/// </summary>
/// <remarks>
/// Implements OPS-CFG-008 and CONV-DESIGN-003. Neither bootstrap value is ever a row
/// here: the database connection and the secrets-manager credential are injected at
/// deployment, and the two keys fetched from the secrets manager at startup are read
/// before this table can be.
/// </remarks>
internal sealed class SettingRecord
{
    /// <summary>
    /// The <c>key</c> column, which is this table's key.
    /// </summary>
    public ConfigurationKey Key { get; set; }

    /// <summary>
    /// The <c>value</c> column, written as the key's type writes it.
    /// </summary>
    public string Value { get; set; } = string.Empty;
}
