using Janus.Core;

namespace Janus.Storage.Identity.Profiles;

/// <summary>
/// The <c>profiles</c> row.
/// </summary>
/// <remarks>
/// Implements IDN-ATTR-007, PRIV-RIGHT-005a and CONV-DESIGN-003. Every field is
/// ciphertext under the subject's own key, so erasure of that key is what removes the
/// profile and no column of this table is ever queried by its value.
/// </remarks>
internal sealed class ProfileRecord
{
    /// <summary>
    /// The subject column, which is this table's key and the column every encrypted
    /// column names as its subject.
    /// </summary>
    public SubjectId Subject { get; set; }

    /// <summary>
    /// The <c>enc_display_name</c> column.
    /// </summary>
    public byte[]? DisplayName { get; set; }

    /// <summary>
    /// The <c>enc_legal_name</c> column.
    /// </summary>
    public byte[]? LegalName { get; set; }

    /// <summary>
    /// The <c>enc_date_of_birth</c> column.
    /// </summary>
    public byte[]? DateOfBirth { get; set; }
}
