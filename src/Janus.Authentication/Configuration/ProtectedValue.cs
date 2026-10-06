using System.Threading;
using System.Threading.Tasks;
using Janus.Core.Configuration;

namespace Janus.Authentication.Configuration;

/// <summary>
/// A value named on the server for a protected key, read as its key admits it.
/// </summary>
/// <remarks>Implements OPS-CFG-004 and chapter 10 section 4.8.</remarks>
internal abstract class ProtectedValue
{
    /// <summary>
    /// The key the value is named for.
    /// </summary>
    public abstract ConfigurationKey Key { get; }

    /// <summary>
    /// The value in the form the settings table holds it.
    /// </summary>
    public abstract string Written { get; }

    /// <summary>
    /// Whether putting the value in force loosens the deployment against the value in
    /// force now. A value set where none stood loosens nothing, as a value bootstrap sets
    /// does not; one whose direction cannot be read from what stands is a loosening
    /// (D-079b).
    /// </summary>
    /// <param name="configuration">Where the value in force is read.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>Whether the change loosens.</returns>
    public abstract ValueTask<bool> LoosensAsync(IConfigurationStore configuration, CancellationToken cancellationToken);

    /// <summary>
    /// What the key was before the change, as the record carries it: the written form
    /// of the value in force, which is the default where no row stood, and nothing only
    /// for a key the deployment names that has no row (OPS-CFG-005, D-166).
    /// </summary>
    /// <param name="configuration">Where the value in force is read.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The written value in force, or nothing.</returns>
    public abstract ValueTask<string?> BeforeAsync(IConfigurationStore configuration, CancellationToken cancellationToken);
}
