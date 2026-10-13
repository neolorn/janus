using System;
using Janus.Core;

namespace Janus.Storage.Authentication.Registration;

/// <summary>
/// One staged identifier, as it is written into the session's encrypted column.
/// </summary>
/// <param name="Id">The identifier issued for it.</param>
/// <param name="Kind">Whether it is an email or a phone.</param>
/// <param name="Entered">The form the person entered.</param>
/// <param name="Canonical">The form it is compared and sent to under.</param>
/// <param name="IsLocked">Whether it is fixed against change.</param>
/// <param name="IsExtra">Whether it was added at the confirm step.</param>
/// <param name="Link">The fingerprint of the link token outstanding.</param>
/// <param name="VerifiedAt">When it was confirmed, where it has been.</param>
/// <remarks>Implements REG-SESS-003 and REG-SESS-004.</remarks>
internal sealed record StagedIdentityDocument(
    Guid Id,
    string Kind,
    string Entered,
    string Canonical,
    bool IsLocked,
    bool IsExtra,
    [property: NeverLogged] byte[]? Link,
    DateTimeOffset? VerifiedAt);
