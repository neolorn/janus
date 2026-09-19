using System;
using System.Globalization;
using System.Security.Cryptography;

namespace Janus.Core;

/// <summary>
/// The stable opaque identifier of an account, generated at creation and never
/// reused. Audit records, grants and tokens reference it, which is what lets erasure
/// anonymise a record rather than destroy it.
/// </summary>
/// <param name="Value">The identifier as the database and the wire carry it.</param>
/// <remarks>
/// Implements IDN-ACCT-002 and CONV-DESIGN-004. This is the OIDC <c>sub</c>, so it is
/// a version 4 value: a version 7 value would carry the account's creation instant and
/// the identifier would no longer be opaque.
/// </remarks>
public readonly record struct SubjectId(Guid Value)
{
    /// <summary>
    /// Issues an identifier for a new account.
    /// </summary>
    /// <param name="randomness">The randomness the deployment runs on.</param>
    /// <returns>An identifier derived from nothing but those bytes.</returns>
    /// <exception cref="ArgumentNullException">The source is absent.</exception>
    public static SubjectId New(RandomNumberGenerator randomness)
    {
        ArgumentNullException.ThrowIfNull(randomness);

        Span<byte> bytes = stackalloc byte[16];
        randomness.GetBytes(bytes);

        // RFC 9562 section 5.4: four bits name the version, two the variant, and the
        // remaining 122 are the random ones just drawn.
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x40);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        return new SubjectId(new Guid(bytes, bigEndian: true));
    }

    /// <inheritdoc/>
    public override string ToString() => Value.ToString("D", CultureInfo.InvariantCulture);
}
