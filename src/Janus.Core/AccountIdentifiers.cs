using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// The identifier group of an account.
/// </summary>
/// <param name="Emails">The addresses the account holds.</param>
/// <param name="Phones">The numbers the account holds.</param>
/// <param name="Username">
/// The username, absent while <c>identifiers.username.enabled</c> is off and where
/// the account has chosen none.
/// </param>
/// <param name="Backup">The backup setting of each kind.</param>
/// <remarks>Implements REG-ACCT-001, REG-IDENT-001 and REG-IDENT-002.</remarks>
public sealed record AccountIdentifiers(
    IReadOnlyList<IdentifierSummary> Emails,
    IReadOnlyList<IdentifierSummary> Phones,
    string? Username,
    IReadOnlyList<BackupSelection> Backup);
