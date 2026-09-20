namespace Janus.Core;

/// <summary>
/// What one kind's backup setting stands at, which is what the security-notice set
/// holds beyond the primary.
/// </summary>
/// <param name="Kind">The kind it governs.</param>
/// <param name="Choice">What it adds to the primary.</param>
/// <param name="Named">The identifier it names, where it names one.</param>
/// <remarks>Implements REG-IDENT-002 and chapter 10 section 5.17.</remarks>
public sealed record BackupSelection(IdentifierKind Kind, BackupChoice Choice, IdentifierId? Named);
