using Janus.Core;

namespace Janus.Authentication.Identifiers;

/// <summary>
/// What one kind's backup setting stands at.
/// </summary>
/// <param name="Kind">The kind it governs.</param>
/// <param name="Choice">What it adds to the primary.</param>
/// <param name="Named">The identifier it names, where it names one.</param>
/// <remarks>Implements REG-IDENT-002 and CONV-LAYOUT-001.</remarks>
internal sealed record HeldBackup(IdentifierKind Kind, BackupChoice Choice, IdentifierId? Named);
