using System;
using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace Janus.Core;

/// <summary>
/// The correlation reference one send carries to the host and back: 128 random
/// bits in base64url, which the library keeps as its hash, and in the encrypted content
/// of the outbox row of the message it was drawn for until that row is removed.
/// </summary>
/// <remarks>
/// Implements INT-SMS-005, INT-SMS-006 and INT-GEN-003. It is the same at every
/// attempt, so it is the host's idempotency key, and the host's report of an outcome
/// names it, so the reference is unguessable and a dump of the table yields none.
/// </remarks>
[NeverLogged]
public readonly record struct SendReference
{
    private const int Length = 16;

    // The 22 characters 128 bits take in base64url, unpadded, as a reference is drawn.
    private const int Encoded = 22;

    private static readonly SearchValues<char> Alphabet =
        SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_");

    private readonly string? _value;

    private SendReference(string value) => _value = value;

    /// <summary>
    /// The reference as the host sees it.
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
    /// <returns>
    /// Whether the text is 128 bits in base64url, unpadded; any other text, whatever it
    /// holds, is read as no reference and is never a fault.
    /// </returns>
    /// <remarks>
    /// Implements AUTH-ABUSE-004 and INT-SMS-005 AC3: the reference drawn at a send's
    /// admission travels with its row until the row is removed, and is read back from it
    /// at each attempt; a host's report naming text that is no reference changes nothing.
    /// </remarks>
    public static bool TryParse([NotNullWhen(true)][NeverLogged] string? value, out SendReference reference)
    {
        bool read = value is { Length: Encoded }
            && !value.AsSpan().ContainsAnyExcept(Alphabet)
            && Base64Url.IsValid(value, out int decoded)
            && decoded == Length;

        reference = read ? new SendReference(value!) : default;

        return read;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
