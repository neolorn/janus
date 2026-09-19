using System;
using Janus.Core;

namespace Janus.Storage.Authentication.Sessions;

/// <summary>
/// The <c>sessions</c> row.
/// </summary>
/// <remarks>
/// Implements AUTH-SESS-001, AUTH-SESS-002 and CONV-DESIGN-003. The row holds the
/// properties the authentication reached and never the factors that reached them, and
/// it holds the secret by its fingerprint and never the secret.
/// </remarks>
internal sealed class SessionRecord
{
    /// <summary>The <c>id</c> column, which is this table's key.</summary>
    public SessionId Id { get; set; }

    /// <summary>The <c>spine</c> column, which is the record every session derives from.</summary>
    public SessionId Spine { get; set; }

    /// <summary>The <c>type</c> column.</summary>
    public SessionType Type { get; set; }

    /// <summary>The <c>subject</c> column.</summary>
    public SubjectId Subject { get; set; }

    /// <summary>The <c>secret_fingerprint</c> column.</summary>
    public byte[] SecretFingerprint { get; set; } = [];

    /// <summary>The fingerprint of the synchronizer token bound to the session.</summary>
    public byte[] CsrfFingerprint { get; set; } = [];

    /// <summary>The <c>created_at</c> column.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>The <c>last_seen_at</c> column.</summary>
    public DateTimeOffset LastSeenAt { get; set; }

    /// <summary>The <c>attained</c> column.</summary>
    public AssuranceLevel Attained { get; set; }

    /// <summary>The <c>attained_at</c> column.</summary>
    public DateTimeOffset AttainedAt { get; set; }

    /// <summary>The <c>phishing_resistant</c> column.</summary>
    public bool PhishingResistant { get; set; }

    /// <summary>The <c>phishing_resistant_at</c> column.</summary>
    public DateTimeOffset? PhishingResistantAt { get; set; }

    /// <summary>The <c>origin_browser</c> column.</summary>
    public string OriginBrowser { get; set; } = string.Empty;

    /// <summary>The <c>origin_os</c> column.</summary>
    public string OriginOs { get; set; } = string.Empty;

    /// <summary>
    /// The <c>origin_place</c> column, holding the address the session began at and
    /// the city it resolved to, under the person's key (AUTH-SESS-013, PRIV-RET-002).
    /// </summary>
    public byte[] OriginPlace { get; set; } = [];

    /// <summary>The <c>last_seen_browser</c> column.</summary>
    public string LastSeenBrowser { get; set; } = string.Empty;

    /// <summary>The <c>last_seen_os</c> column.</summary>
    public string LastSeenOs { get; set; } = string.Empty;

    /// <summary>The <c>last_seen_place</c> column, held as the origin's is.</summary>
    public byte[] LastSeenPlace { get; set; } = [];

    /// <summary>The <c>idle_expiry</c> column.</summary>
    public DateTimeOffset IdleExpiry { get; set; }

    /// <summary>The <c>absolute_expiry</c> column.</summary>
    public DateTimeOffset AbsoluteExpiry { get; set; }

    /// <summary>The <c>ended_at</c> column.</summary>
    public DateTimeOffset? EndedAt { get; set; }

    /// <summary>The <c>satisfies_every_gate</c> column.</summary>
    public bool SatisfiesEveryGate { get; set; }
}
