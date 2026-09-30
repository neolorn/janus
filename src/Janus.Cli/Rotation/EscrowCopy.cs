using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Cli.Rotation;

/// <summary>
/// The escrow copy of a key's current version, printed for the sealed envelope.
/// </summary>
/// <remarks>
/// Implements DR-009, OPS-SEC-003 AC4 and OPS-SEC-001. The copy is the member of the key
/// document that carries the version, so a restore from the envelope pipes it back as it
/// was printed. The key passes from the key ring to the output through a buffer that is
/// cleared once written, never through a string.
/// </remarks>
internal static class EscrowCopy
{
    /// <summary>
    /// Writes the copy of the key-encryption key's current version.
    /// </summary>
    /// <param name="output">Where it is printed.</param>
    /// <param name="ring">The key ring the current version is borrowed from.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The work of writing it.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static ValueTask WriteKeyEncryptionKeyAsync(
        TextWriter output,
        IKeyRing ring,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ring);

        return WrittenAsync(
            output,
            "keyEncryptionKeys",
            ring.BorrowKeyEncryptionKeys(keys => Encoded(keys.CurrentVersion, keys.Current.Span)),
            cancellationToken);
    }

    /// <summary>
    /// Writes the copy of the fingerprint key's current version.
    /// </summary>
    /// <param name="output">Where it is printed.</param>
    /// <param name="ring">The key ring the current version is borrowed from.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The work of writing it.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static ValueTask WriteFingerprintKeyAsync(
        TextWriter output,
        IKeyRing ring,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ring);

        return WrittenAsync(
            output,
            "fingerprintKeys",
            ring.BorrowFingerprintKeys(keys => Encoded(keys.CurrentVersion, keys.Current.Span)),
            cancellationToken);
    }

    // The version and its key in base64, encoded while the ring lends the key, into a
    // buffer the write clears.
    private static (int Version, char[] Written, int Length) Encoded(int version, ReadOnlySpan<byte> current)
    {
        char[] written = new char[(current.Length + 2) / 3 * 4];

        Convert.TryToBase64Chars(current, written, out int length);

        return (version, written, length);
    }

    private static async ValueTask WrittenAsync(
        TextWriter output,
        string member,
        Result<(int Version, char[] Written, int Length)> encoded,
        CancellationToken cancellationToken)
    {
        (int currentVersion, char[] written, int length) = encoded
            .Match(value => value, error => throw new InvalidOperationException(error.Code.ToString()));

        try
        {
            ArgumentNullException.ThrowIfNull(output);

            string version = currentVersion.ToString(CultureInfo.InvariantCulture);

            await output
                .WriteAsync(
                    ("{\"" + member + "\":{\"current\":" + version + ",\"versions\":{\"" + version + "\":\"").AsMemory(),
                    cancellationToken)
                .ConfigureAwait(false);
            await output.WriteAsync(written.AsMemory(0, length), cancellationToken).ConfigureAwait(false);
            await output.WriteLineAsync("\"}}}".AsMemory(), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(written.AsSpan()));
        }
    }
}
