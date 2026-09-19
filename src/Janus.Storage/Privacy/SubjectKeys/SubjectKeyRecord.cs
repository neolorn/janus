using Janus.Core;

namespace Janus.Storage.Privacy.SubjectKeys;

/// <summary>
/// The <c>subject_keys</c> row.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-003. The wrapped key is not a personal field and does not go
/// through the field cipher: it is what the field cipher is unwrapped from.
/// </remarks>
internal sealed class SubjectKeyRecord
{
    /// <summary>
    /// The subject column, which is this table's key.
    /// </summary>
    public SubjectId Subject { get; set; }

    /// <summary>
    /// The <c>format_marker</c> column.
    /// </summary>
    public byte FormatMarker { get; set; }

    /// <summary>
    /// The <c>key_version</c> column.
    /// </summary>
    public int KeyVersion { get; set; }

    /// <summary>
    /// The <c>wrapped_key</c> column.
    /// </summary>
    public byte[] WrappedKey { get; set; } = [];
}
