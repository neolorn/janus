using System;
using Janus.Core;

namespace Janus.Authentication.Registration;

/// <summary>
/// One identifier an account holds from the instant it exists.
/// </summary>
/// <param name="Id">The identifier it carries, which the session already issued.</param>
/// <param name="Kind">Whether it is an email or a phone.</param>
/// <param name="Entered">The form the person entered.</param>
/// <param name="Canonical">The form it is compared and looked up under.</param>
/// <param name="Locked">Whether it is fixed against change.</param>
/// <param name="VerifiedAt">When the registration proved it.</param>
/// <remarks>Implements REG-SESS-007, REG-IDENT-002 and REG-IDENT-010.</remarks>
internal sealed record NewIdentifier(
    IdentifierId Id,
    IdentifierKind Kind,
    string Entered,
    string Canonical,
    bool Locked,
    DateTimeOffset VerifiedAt);
