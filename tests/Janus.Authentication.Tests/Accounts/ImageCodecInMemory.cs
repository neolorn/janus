using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Tests.Accounts;

/// <summary>
/// An image codec that reads nothing: it answers what a test told it to answer, and
/// records what it was handed, which is what the library's side of IDN-ATTR-004 is.
/// </summary>
internal sealed class ImageCodecInMemory
{
    private const string Mark = "jpeg:";

    private bool _refuses;

    /// <summary>
    /// What was handed to it to re-encode.
    /// </summary>
    public ReadOnlyMemory<byte> Given { get; private set; }

    /// <summary>
    /// The longest side it was told to hold the image to.
    /// </summary>
    public int Dimension { get; private set; }

    /// <summary>
    /// How many uploads reached it.
    /// </summary>
    public int Reads { get; private set; }

    /// <summary>
    /// The codec as a deployment declares it.
    /// </summary>
    public ImageCodec Declared => new(ReencodeAsync);

    /// <summary>
    /// What this codec answers for an upload it accepts.
    /// </summary>
    /// <param name="upload">The uploaded bytes.</param>
    /// <returns>The bytes it re-encodes them to.</returns>
    public static ReadOnlyMemory<byte> Reencoded(string upload) =>
        Encoding.ASCII.GetBytes(Mark + upload);

    /// <summary>
    /// Makes it refuse whatever arrives, as it does for bytes that are no image of a
    /// kind it reads.
    /// </summary>
    public void Refuses() => _refuses = true;

    private ValueTask<ReadOnlyMemory<byte>?> ReencodeAsync(
        ReadOnlyMemory<byte> upload,
        int dimension,
        CancellationToken cancellationToken)
    {
        Given = upload;
        Dimension = dimension;
        Reads++;

        return ValueTask.FromResult<ReadOnlyMemory<byte>?>(
            _refuses
                ? null
                : Reencoded(Encoding.ASCII.GetString(upload.Span)));
    }
}
