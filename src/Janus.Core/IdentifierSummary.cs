namespace Janus.Core;

/// <summary>
/// One identifier of an account, as the account page reads it.
/// </summary>
/// <param name="Id">Which identifier, as the identifier endpoints name it.</param>
/// <param name="Value">The form the person entered, which is what is shown back.</param>
/// <param name="Verified">Whether it counts.</param>
/// <param name="Primary">Whether it is the primary of its kind.</param>
/// <param name="Locked">Whether it is fixed against change.</param>
/// <remarks>Implements REG-ACCT-001 and REG-IDENT-002.</remarks>
public sealed record IdentifierSummary(
    IdentifierId Id,
    string Value,
    bool Verified,
    bool Primary,
    bool Locked);
