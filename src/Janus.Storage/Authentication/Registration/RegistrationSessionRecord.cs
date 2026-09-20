using System;
using Janus.Core;

namespace Janus.Storage.Authentication.Registration;

/// <summary>
/// The <c>registration_sessions</c> row.
/// </summary>
/// <remarks>
/// Implements REG-SESS-001, REG-SESS-002 and OPS-SEC-001. Everything the steps
/// collect is one encrypted document, because no query reads inside a registration
/// and the row is deleted whole: what the session staged has no life after it. The
/// data key is the row's own, so an abandoned registration leaves no key behind
/// either.
/// </remarks>
internal sealed class RegistrationSessionRecord
{
    /// <summary>The <c>id</c> column, which is this table's key.</summary>
    public RegistrationSessionId Id { get; set; }

    /// <summary>
    /// The <c>provisional_subject</c> column, which the encrypted document names as
    /// its subject and which the account carries if the registration completes.
    /// </summary>
    public SubjectId ProvisionalSubject { get; set; }

    /// <summary>The <c>expires_at</c> column, which the sweep reads.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// The <c>key_version</c> column: the key-encryption key version the data key is
    /// wrapped under.
    /// </summary>
    public int KeyVersion { get; set; }

    /// <summary>The <c>wrapped_key</c> column: the row's data key, wrapped.</summary>
    public byte[] WrappedKey { get; set; } = [];

    /// <summary>The <c>enc_session</c> column: everything the steps collected.</summary>
    public byte[] Session { get; set; } = [];
}
