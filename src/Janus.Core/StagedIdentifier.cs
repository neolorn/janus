namespace Janus.Core;

/// <summary>
/// One identifier a registration session holds, before any account exists to hold it.
/// </summary>
/// <param name="Id">Which one, so that it can be verified, changed or discarded.</param>
/// <param name="Kind">Whether it is an email or a phone.</param>
/// <param name="Value">The form the person entered, which is what is shown back.</param>
/// <param name="Verified">Whether a code or a same-browser link has confirmed it.</param>
/// <param name="Locked">
/// Whether it is fixed: a bound invitation and a provider-operated mailbox both leave
/// nothing to change.
/// </param>
/// <remarks>Implements REG-SESS-001, REG-SESS-004 and REG-IDENT-010.</remarks>
public sealed record StagedIdentifier(
    IdentifierId Id,
    IdentifierKind Kind,
    string Value,
    bool Verified,
    bool Locked);
