using System;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// What a deployment registers to make a profile photo out of an upload: a callback
/// that decides by content whether the bytes are an image it accepts, holds the image
/// to the longest side it is given, and answers the re-encoded JPEG with every
/// metadata segment removed.
/// </summary>
/// <param name="Reencode">
/// The uploaded bytes and the longest side in pixels the stored image is held to,
/// answering the re-encoded image, or nothing where the bytes are not an image the
/// deployment accepts.
/// </param>
/// <remarks>
/// Implements IDN-ATTR-002, IDN-ATTR-004 and LIB-HOST-001. The library ships no image
/// library: which formats are read, and what reading them costs, is the deployment's.
/// What the callback answers is what is stored, so the metadata is stripped once and
/// on the way in. A callback that answers nothing has refused the upload and says no
/// more than that: the code and the words the reader sees are the library's and the
/// frontend's (CONV-CONTENT-001).
/// </remarks>
public sealed record ImageCodec(
    Func<ReadOnlyMemory<byte>, int, CancellationToken, ValueTask<ReadOnlyMemory<byte>?>> Reencode);
