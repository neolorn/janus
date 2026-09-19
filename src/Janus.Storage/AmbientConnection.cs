using System.Data.Common;

namespace Janus.Storage;

/// <summary>
/// An open connection with the operation's transaction already attached.
/// </summary>
/// <param name="Connection">The connection the context is using.</param>
/// <param name="Transaction">The operation's transaction, where one is open.</param>
/// <remarks>Implements OPS-DATA-002.</remarks>
internal readonly record struct AmbientConnection(DbConnection Connection, DbTransaction? Transaction);
