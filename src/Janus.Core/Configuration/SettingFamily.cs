namespace Janus.Core.Configuration;

/// <summary>
/// A key of chapter 10 section 4 that exists once per organization identifier or once
/// per host-declared category, rather than once for the deployment.
/// </summary>
/// <remarks>Implements chapter 10 section 4, D-151. The catalogue is <see cref="Settings"/>.</remarks>
public abstract class SettingFamily
{
    private protected SettingFamily(string prefix, SettingScope scope, SettingDirection loosening)
    {
        Prefix = prefix;
        Scope = scope;
        Loosening = loosening;
    }

    /// <summary>
    /// The part of the key that is the same for every member of the family.
    /// </summary>
    public string Prefix { get; }

    /// <summary>
    /// Whether the application may change a member of the family.
    /// </summary>
    public SettingScope Scope { get; }

    /// <summary>
    /// Which way a change to a member loosens the deployment.
    /// </summary>
    public SettingDirection Loosening { get; }

    /// <summary>
    /// The key of one member.
    /// </summary>
    /// <param name="parameter">The organization identifier or the declared category.</param>
    /// <returns>The key.</returns>
    public ConfigurationKey For(string parameter) => ConfigurationKey.Parse(Prefix + "." + parameter);
}
