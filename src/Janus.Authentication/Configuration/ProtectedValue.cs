using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
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
    /// The organization whose member of a family the key is, or nothing for a key that
    /// exists once for the deployment.
    /// </summary>
    public abstract OrganizationId? Organization { get; }

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
}
