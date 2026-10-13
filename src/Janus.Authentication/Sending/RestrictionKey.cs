using Janus.Core;

namespace Janus.Authentication.Sending;

/// <summary>
/// What one restriction counts a send under: the restriction's name and the plain
/// value its key resolved to. The value never reaches a record; the ledger stores its
/// keyed hash (AUTH-ABUSE-004).
/// </summary>
/// <param name="Restriction">The restriction's name.</param>
/// <param name="Kind">
/// What the restriction counts against, which decides where the ledger keeps the key.
/// </param>
/// <param name="Value">The address, account, source or host value.</param>
/// <remarks>Implements AUTH-ABUSE-004, PRIV-RET-005 and chapter 10 section 5.14.</remarks>
internal readonly record struct RestrictionKey(string Restriction, RestrictionKeyKind Kind, string Value);
