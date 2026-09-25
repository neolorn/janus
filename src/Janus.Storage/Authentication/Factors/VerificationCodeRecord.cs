using System;
using Janus.Core;

namespace Janus.Storage.Authentication.Factors;

/// <summary>
/// The <c>verification_codes</c> row: one code outstanding.
/// </summary>
/// <remarks>
/// Implements AUTH-FACT-004 AC2. The table is the codes' own, apart from the
/// credentials a sign-in is answered with, and the row names no person: what the code
/// was issued against is what finds it again.
/// </remarks>
internal sealed class VerificationCodeRecord
{
    /// <summary>The <c>holder</c> column: what the code was issued against.</summary>
    public byte[] Holder { get; set; } = [];

    /// <summary>The <c>code</c> column: the digits, as they are compared.</summary>
    [NeverLogged]
    public byte[] Code { get; set; } = [];

    /// <summary>The <c>issued_at</c> column.</summary>
    public DateTimeOffset IssuedAt { get; set; }

    /// <summary>The <c>expires_at</c> column, which the sweep reads.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>The <c>attempts</c> column: the wrong tries entered against it.</summary>
    public int Attempts { get; set; }
}
