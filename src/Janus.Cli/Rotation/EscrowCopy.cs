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
/// was printed. The key passes from the held bytes to the output through a buffer that
/// is cleared once written, never through a string.
/// </remarks>
internal static class EscrowCopy
{
    /// <summary>
    /// Writes the copy of the key-encryption key's current version.
    /// </summary>
    /// <param name="output">Where it is printed.</param>
    /// <param name="keys">The versions, the one copied current.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The work of writing it.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static ValueTask WriteAsync(
        TextWriter output,
        KeyEncryptionKeys keys,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);

        return WrittenAsync(output, "keyEncryptionKeys", keys.CurrentVersion, keys.Current, cancellationToken);
    }

    /// <summary>
    /// Writes the copy of the fingerprint key's current version.
    /// </summary>
    /// <param name="output">Where it is printed.</param>
    /// <param name="keys">The versions, the one copied current.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The work of writing it.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static ValueTask WriteAsync(
        TextWriter output,
        FingerprintKeys keys,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);

        return WrittenAsync(output, "fingerprintKeys", keys.CurrentVersion, keys.Current, cancellationToken);
    }

    private static async ValueTask WrittenAsync(
        TextWriter output,
        string member,
        int currentVersion,
        ReadOnlyMemory<byte> current,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(output);

        string version = currentVersion.ToString(CultureInfo.InvariantCulture);
        char[] written = new char[(current.Length + 2) / 3 * 4];

        try
        {
            Convert.TryToBase64Chars(current.Span, written, out int length);

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
