using Janus.Core;

namespace Janus.Storage.Identity.Identifiers;

/// <summary>
/// The <c>identifier_backup_settings</c> row: what a kind's setting adds to the primary
/// to make the security-notice set.
/// </summary>
/// <remarks>
/// Implements REG-IDENT-002, chapter 10 section 5.17 and CONV-DESIGN-003. The setting
/// is one of two words or the identifier of one named verified identifier, so exactly
/// one of the two columns carries a value.
/// </remarks>
internal sealed class BackupSettingRecord
{
    /// <summary>
    /// The setting that reaches every verified identifier of the kind, which is the
    /// default.
    /// </summary>
    public const string AllVerified = "all-verified";

    /// <summary>
    /// The setting that reaches the primary alone.
    /// </summary>
    public const string PrimaryOnly = "primary-only";

    /// <summary>
    /// The subject column, the first half of this table's key.
    /// </summary>
    public SubjectId Subject { get; set; }

    /// <summary>
    /// The <c>kind</c> column, the second half of this table's key.
    /// </summary>
    public IdentifierKind Kind { get; set; }

    /// <summary>
    /// The <c>rule</c> column, where the setting is one of the two words.
    /// </summary>
    public string? Rule { get; set; }

    /// <summary>
    /// The <c>named</c> column, where the setting is one named verified identifier.
    /// </summary>
    public IdentifierId? Named { get; set; }
}
