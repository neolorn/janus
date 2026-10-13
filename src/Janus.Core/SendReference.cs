using System;
using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace Janus.Core;

/// <summary>
/// The correlation reference one send carries to the gateway and back: 128 random
/// bits in base64url, which the library keeps as its hash, and in the encrypted content
/// of the outbox row of the message it was drawn for until that row is removed.
/// </summary>
/// <remarks>
/// Implements AUTH-ABUSE-007, INT-GEN-003 and INT-SMS-005. A callback is hostile
/// input, so the reference is unguessable and a dump of the table yields none.
/// </remarks>
[NeverLogged]
public readonly record struct SendReference
{
    private const int Length = 16;

    private readonly string? _value;

    private SendReference(string value) => _value = value;

    /// <summary>
    /// The reference as the gateway sees it.
    /// </summary>
    public string Value => _value ?? throw new InvalidOperationException("The reference was never drawn.");

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
    /// Reads a reference as the outbox row of the message it was drawn for carries it.
    /// </summary>
    /// <param name="value">The reference as text.</param>
    /// <param name="reference">The reference, where the text is one.</param>
    /// <returns>Whether the text is 128 bits in base64url.</returns>
    /// <remarks>
    /// Implements AUTH-ABUSE-004: the reference drawn at a send's admission travels with
    /// its row until the row is removed, and is read back from it at each attempt.
    /// </remarks>
    public static bool TryParse([NotNullWhen(true)][NeverLogged] string? value, out SendReference reference)
    {
        Span<byte> drawn = stackalloc byte[Length];

        bool read = value is not null
            && Base64Url.TryDecodeFromChars(value, drawn, out int written)
            && written == Length;

        reference = read ? new SendReference(value!) : default;

        return read;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
