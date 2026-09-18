namespace Janus.Core.Configuration;

/// <summary>
/// Whether the application may change a setting.
/// </summary>
/// <remarks>Implements OPS-CFG-001, OPS-CFG-004, chapter 10 section 4.</remarks>
public enum SettingScope
{
    /// <summary>
    /// Changeable at runtime through the management application, which is the default
    /// for every key the specification does not protect.
    /// </summary>
    Runtime = 0,

    /// <summary>
    /// Not changeable through the application: a command on the server or a
    /// redeployment, so that turning it off cannot hide the person turning it off.
    /// </summary>
    Protected = 1,
}
