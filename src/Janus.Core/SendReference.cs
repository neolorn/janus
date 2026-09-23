using System;
using System.Buffers.Text;
using System.Security.Cryptography;

namespace Janus.Core;

/// <summary>
/// The correlation reference one send carries to the gateway and back: 128 random
/// bits in base64url, which the library keeps only as its hash.
/// </summary>
/// <remarks>
/// Implements AUTH-ABUSE-007, INT-GEN-003 and INT-SMS-005. A callback is hostile
/// input, so the reference is unguessable and a dump of the table yields none.
/// </remarks>
public readonly record struct SendReference
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

    /// <inheritdoc />
    public override string ToString() => Value;
}
