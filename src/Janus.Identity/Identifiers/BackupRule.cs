namespace Janus.Identity.Identifiers;

/// <summary>
/// What a kind's backup setting adds to the primary to make the security-notice set.
/// </summary>
/// <remarks>Implements REG-IDENT-002 and chapter 10 section 5.17.</remarks>
internal enum BackupRule
{
    /// <summary>
    /// Every verified identifier of the kind. The default.
    /// </summary>
    AllVerified = 0,

    /// <summary>
    /// The primary alone.
    /// </summary>
    PrimaryOnly = 1,

    /// <summary>
    /// The primary and one named verified identifier.
    /// </summary>
    Named = 2,
}
