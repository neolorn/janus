using Janus.Core;

namespace Janus.Storage.Identity.Preferences;

/// <summary>
/// The <c>account_preferences</c> row.
/// </summary>
/// <remarks>
/// Implements IDN-ATTR-001, REG-PREF-001, PRIV-RIGHT-005a and CONV-DESIGN-003. The
/// language and the time zone are plaintext because a notification has to reach a
/// person whose personal fields are gone; the declared values are one encrypted
/// document, which is what the cap of <c>preferences.maxsize</c> bounds.
/// </remarks>
internal sealed class PreferenceRecord
{
    /// <summary>
    /// The subject column, which is this table's key and the column the encrypted
    /// column names as its subject.
    /// </summary>
    public SubjectId Subject { get; set; }

    /// <summary>
    /// The <c>language</c> column, a BCP 47 tag.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// The <c>time_zone</c> column, an IANA zone identifier.
    /// </summary>
    public string? TimeZone { get; set; }

    /// <summary>
    /// The <c>enc_values</c> column: the host-declared values as one document.
    /// </summary>
    public byte[]? Values { get; set; }
}
