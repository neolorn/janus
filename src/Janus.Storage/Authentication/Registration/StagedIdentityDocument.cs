using System;

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
/// <param name="Code">The code outstanding, which a link opened elsewhere shows.</param>
/// <param name="CodeExpiresAt">When that code stops being accepted.</param>
/// <param name="Link">The fingerprint of the link token outstanding.</param>
/// <param name="WrongAttempts">How many wrong codes have been presented.</param>
/// <param name="CodeSpent">Whether the code has been invalidated.</param>
/// <param name="VerifiedAt">When it was confirmed, where it has been.</param>
/// <remarks>Implements REG-SESS-003 and REG-SESS-004.</remarks>
internal sealed record StagedIdentityDocument(
    Guid Id,
    string Kind,
    string Entered,
    string Canonical,
    bool IsLocked,
    bool IsExtra,
    byte[]? Code,
    DateTimeOffset? CodeExpiresAt,
    byte[]? Link,
    int WrongAttempts,
    bool CodeSpent,
    DateTimeOffset? VerifiedAt);
