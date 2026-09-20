using System;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Janus.Authentication.Sending;

/// <summary>
/// The correlation reference one send carries to the gateway and back: 128 random
/// bits in base64url, stored only as its hash and compared in fixed time.
/// </summary>
/// <remarks>
/// Implements AUTH-ABUSE-007, INT-GEN-003 and INT-SMS-005. A callback is hostile
/// input, so the reference is unguessable and a dump of the table yields none.
/// </remarks>
internal readonly record struct SendReference
{
    private const int Length = 16;

    private SendReference(string value) => Value = value;

    /// <summary>
    /// The reference as the gateway sees it.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Draws a reference no one can guess.
    /// </summary>
    /// <param name="randomness">Where the bits come from.</param>
    /// <returns>The reference.</returns>
    /// <exception cref="ArgumentNullException">The generator is absent.</exception>
    public static SendReference Draw(RandomNumberGenerator randomness)
    {
        ArgumentNullException.ThrowIfNull(randomness);

        byte[] drawn = new byte[Length];
        randomness.GetBytes(drawn);

        return new SendReference(Base64Url.EncodeToString(drawn));
    }

    /// <summary>
    /// What a reference is stored as, for a value that arrived from outside.
    /// </summary>
    /// <param name="presented">What the callback carried.</param>
    /// <returns>Its hash.</returns>
    /// <exception cref="ArgumentNullException">The value is absent.</exception>
    public static byte[] FingerprintOf(string presented)
    {
        ArgumentNullException.ThrowIfNull(presented);

        return SHA256.HashData(Encoding.UTF8.GetBytes(presented));
    }

    /// <summary>
    /// What this reference is stored as.
    /// </summary>
    /// <returns>Its hash.</returns>
    public byte[] Fingerprint() => FingerprintOf(Value);

    /// <inheritdoc />
    public override string ToString() => Value;
}
