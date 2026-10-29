using System;
using Janus.Authentication.Records;
using Janus.Core;

namespace Janus.Storage.Authentication.Records;

/// <summary>
/// The <c>collection_sessions</c> row.
/// </summary>
/// <remarks>
/// Implements REC-COL-001 and OPS-SEC-001. What the steps stage is one encrypted
/// document under a key the row carries, so removing the row removes both the staged
/// contacts and the only key that reads them.
/// </remarks>
internal sealed class CollectionSessionRecord
{
    /// <summary>The <c>id</c> column, which is this table's key.</summary>
    public CollectionSessionId Id { get; set; }

    /// <summary>The <c>expires_at</c> column, which the sweep reads.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// The <c>wrapped_key</c> column: the row's data key, wrapped under the deployment's
    /// data key.
    /// </summary>
    public byte[] WrappedKey { get; set; } = [];

    /// <summary>The <c>enc_session</c> column: everything the steps staged.</summary>
    [NeverLogged]
    public byte[] Session { get; set; } = [];
}
