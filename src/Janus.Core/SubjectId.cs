using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// The stable opaque identifier of an account, generated at creation and never
/// reused. Audit records, grants and tokens reference it, which is what lets erasure
/// anonymise a record rather than destroy it.
/// </summary>
/// <remarks>
/// Implements IDN-ACCT-002, CONV-DESIGN-004 and PRIV-RIGHT-005a. This is the OIDC
/// <c>sub</c>, so it is a version 4 value: a version 7 value would carry the account's
/// creation instant and the identifier would no longer be opaque. The max UUID of
/// RFC 9562 is the deployment's data key's row of the subject-key table, which no
/// subject is issued, so no subject can be made from it (D-174).
/// </remarks>
public readonly record struct SubjectId
{
    /// <summary>
    /// Reads the identifier of an account.
    /// </summary>
    /// <param name="value">The identifier as the database and the wire carry it.</param>
    /// <exception cref="ArgumentException">The value is the max UUID.</exception>
    /// <remarks>
    /// A stored event reads its subjects back through this constructor, so the refusal
    /// holds for what is read as for what is made.
    /// </remarks>
    [JsonConstructor]
    public SubjectId(Guid value)
    {
        if (value == Guid.AllBitsSet)
        {
            throw new ArgumentException("No subject is issued the max UUID.", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// The identifier as the database and the wire carry it.
    /// </summary>
    public Guid Value { get; }

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
