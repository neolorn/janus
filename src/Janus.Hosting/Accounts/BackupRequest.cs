using Janus.Core;

namespace Janus.Hosting.Accounts;

/// <summary>
/// Which of an account's addresses of one kind a security notice reaches.
/// </summary>
/// <param name="Kind">Which kind the setting is for.</param>
/// <param name="Setting">
/// <c>all-verified</c>, <c>primary-only</c>, or the identifier of one verified
/// address of that kind.
/// </param>
/// <remarks>Implements REG-IDENT-002.</remarks>
internal sealed record BackupRequest(IdentifierKind Kind, string? Setting);
