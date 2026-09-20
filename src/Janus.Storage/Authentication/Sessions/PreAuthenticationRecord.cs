using System;
using Janus.Core;

namespace Janus.Storage.Authentication.Sessions;

/// <summary>
/// The <c>preauthentication_sessions</c> row: what a browser carries before it holds
/// a session.
/// </summary>
/// <remarks>
/// Implements BFF-CSRF-005a and BFF-CSRF-005b. The row holds what each of the two
/// tokens fingerprints to and nothing the browser holds; it names no subject, because
/// there is none to name.
/// </remarks>
internal sealed class PreAuthenticationRecord
{
    /// <summary>The <c>fingerprint</c> column, which is this table's key.</summary>
    public byte[] Fingerprint { get; set; } = [];

    /// <summary>The <c>csrf_fingerprint</c> column.</summary>
    public byte[] CsrfFingerprint { get; set; } = [];

    /// <summary>The <c>created_at</c> column.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>The <c>expires_at</c> column, which the sweep reads.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>The <c>registration</c> column, where the browser has one in flight.</summary>
    public RegistrationSessionId? Registration { get; set; }

    /// <summary>The <c>enrolment</c> column, where the browser has one in flight.</summary>
    public EnrolmentSessionId? Enrolment { get; set; }
}
