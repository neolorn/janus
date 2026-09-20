using Janus.Core;

namespace Janus.Authentication.Identifiers;

/// <summary>
/// One identifier of a live account, as the operations that act on the account read
/// it.
/// </summary>
/// <param name="Id">Which identifier, as the endpoints name it.</param>
/// <param name="Kind">Which of the three kinds it is.</param>
/// <param name="Entered">The form the person entered, which is what is shown back.</param>
/// <param name="Canonical">The form it is compared and sent to under.</param>
/// <param name="IsVerified">Whether it counts.</param>
/// <param name="IsPrimary">Whether it is the primary of its kind.</param>
/// <param name="IsLocked">Whether it is fixed against change.</param>
/// <remarks>Implements REG-IDENT-002 and CONV-LAYOUT-001.</remarks>
internal sealed record HeldIdentifier(
    IdentifierId Id,
    IdentifierKind Kind,
    string Entered,
    string Canonical,
    bool IsVerified,
    bool IsPrimary,
    bool IsLocked);
