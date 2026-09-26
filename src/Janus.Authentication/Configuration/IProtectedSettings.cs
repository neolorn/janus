using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core.Configuration;

namespace Janus.Authentication.Configuration;

/// <summary>
/// Where the value of a protected key is written: by bootstrap or by a command on the
/// server, and never through the application's configuration store.
/// </summary>
/// <remarks>
/// Implements OPS-CFG-004 and D-071. Each write is saved in the caller's transaction as
/// it is made, so later reads in the transaction see it.
/// </remarks>
internal interface IProtectedSettings
{
    /// <summary>
    /// Writes one value in the form the settings table holds it.
    /// </summary>
    /// <param name="key">Which key.</param>
    /// <param name="written">Its value, written.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The text the key held before, or nothing where no value stood.</returns>
    ValueTask<string?> WriteAsync(ConfigurationKey key, string written, CancellationToken cancellationToken);

    /// <summary>
    /// The keys the settings table holds a value for.
    /// </summary>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The keys.</returns>
    ValueTask<IReadOnlySet<ConfigurationKey>> HeldAsync(CancellationToken cancellationToken);
}
