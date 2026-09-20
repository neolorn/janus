using Janus.Core;

namespace Janus.Hosting.Accounts;

/// <summary>
/// An identifier added to an account that already exists.
/// </summary>
/// <param name="Kind">Which kind it is.</param>
/// <param name="Value">The address or number, as the person entered it.</param>
/// <remarks>Implements REG-IDENT-004.</remarks>
internal sealed record AddIdentifierToAccountRequest(IdentifierKind Kind, string? Value);
