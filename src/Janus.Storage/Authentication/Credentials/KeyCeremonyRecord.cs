using System;
using Janus.Core;

namespace Janus.Storage.Authentication.Credentials;

/// <summary>
/// The <c>key_ceremonies</c> row: the creation ceremony one account has open.
/// </summary>
/// <remarks>
/// Implements AUTH-FACT-014 and AUTH-STEP-007. Nothing here is a personal field, so
/// no key is unwrapped and none is needed: the row is an account, what it is
/// creating, the value the authenticator signs over, and what it replaces.
/// </remarks>
internal sealed class KeyCeremonyRecord
{
    /// <summary>The <c>subject</c> column: whose account has it open.</summary>
    public SubjectId Subject { get; set; }

    /// <summary>The <c>kind</c> column: which catalogue entry it creates.</summary>
    public Factor Kind { get; set; }

    /// <summary>The <c>challenge</c> column: what the authenticator signs over.</summary>
    public string Challenge { get; set; } = string.Empty;

    /// <summary>The <c>upgrading</c> column: the entry it replaces, where it replaces one.</summary>
    public AuthenticatorId? Upgrading { get; set; }

    /// <summary>The <c>issued_at</c> column.</summary>
    public DateTimeOffset IssuedAt { get; set; }

    /// <summary>The <c>expires_at</c> column, which the sweep reads.</summary>
    public DateTimeOffset ExpiresAt { get; set; }
}
