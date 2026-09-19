namespace Janus.Authentication.Sending;

/// <summary>
/// What one restriction counts a send under: the restriction's name and the plain
/// value its key resolved to. The value never reaches a record; the ledger stores its
/// keyed hash (AUTH-ABUSE-004).
/// </summary>
/// <param name="Restriction">The restriction's name.</param>
/// <param name="Value">The address, account, source or host value.</param>
/// <remarks>Implements AUTH-ABUSE-004 and chapter 10 section 5.14.</remarks>
internal readonly record struct RestrictionKey(string Restriction, string Value);
