using System;
using Janus.Core;

namespace Janus.Storage.Authentication.Credentials;

/// <summary>
/// The <c>pending_enrolments</c> row: one second step the library texts, staged and
/// not yet confirmed.
/// </summary>
/// <remarks>
/// Implements AUTH-FACT-002. The number it would be bound to and the code texted to
/// that number are held under the account's key, so a dump of the table yields neither
/// and an erasure leaves both unreadable.
/// </remarks>
internal sealed class PendingEnrolmentRecord
{
    /// <summary>The <c>subject</c> column.</summary>
    public SubjectId Subject { get; set; }

    /// <summary>The <c>factor</c> column: which catalogue entry it would enrol.</summary>
    public Factor Factor { get; set; }

    /// <summary>The <c>label</c> column: what the person would call the credential.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>The <c>enc_number</c> column: the number it would be bound to.</summary>
    [NeverLogged]
    public byte[] Number { get; set; } = [];

    /// <summary>
    /// The <c>send</c> column: what names the text that carried the code
    /// (AUTH-ABUSE-004).
    /// </summary>
    [NeverLogged]
    public byte[] Send { get; set; } = [];

    /// <summary>The <c>enc_code</c> column: the code the text carried.</summary>
    [NeverLogged]
    public byte[] Code { get; set; } = [];

    /// <summary>The <c>issued_at</c> column.</summary>
    public DateTimeOffset IssuedAt { get; set; }

    /// <summary>The <c>expires_at</c> column.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>The <c>wrong_attempts</c> column.</summary>
    public int WrongAttempts { get; set; }
}
