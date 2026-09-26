using System;
using System.Text;
using Janus.Core;

namespace Janus.Hosting.Background;

/// <summary>
/// The database connection the scheduled maintenance runs under.
/// </summary>
/// <param name="material">The connection, as its UTF-8 bytes.</param>
/// <remarks>
/// Implements OPS-MIG-003a, INF-HOST-003 and OPS-SEC-001. It is handed to the library at
/// startup from the secrets manager and is never read from configuration, never written
/// to the database and never answered to a caller: what it exists for is the audit
/// partition job's own connection.
/// </remarks>
[NeverLogged]
internal sealed class MaintenanceCredential(ReadOnlyMemory<byte> material)
{
    /// <summary>
    /// The connection as the database client takes it.
    /// </summary>
    /// <returns>The value.</returns>
    public string Connection() => Encoding.UTF8.GetString(material.Span);
}
