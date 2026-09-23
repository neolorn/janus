using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core.Configuration;
using Microsoft.AspNetCore.Http;

namespace Janus.Hosting.Accounts;

/// <summary>
/// The bytes a photo upload carried, read from the request body and bounded before
/// they are held.
/// </summary>
/// <remarks>
/// Implements IDN-ATTR-004 and API-CONV-002. The body is the image itself: what it is
/// is decided by reading it and never by what the request called it, so the endpoint
/// takes the bytes and says nothing about them. The ceiling of <c>photo.maxbytes</c>
/// bounds what is held, because a value above the ceiling cannot be configured and a
/// body longer than it is longer than the key allows whatever the key is set to.
/// </remarks>
internal static class UploadedImage
{
    private const int Chunk = 8192;

    private static readonly int Most = Settings.PhotoMaxBytes.Ceiling ?? 0;

    /// <summary>
    /// Reads the body, stopping once more has arrived than any setting could admit.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The bytes the body carried, as far as they are read.</returns>
    /// <exception cref="ArgumentNullException">The request is absent.</exception>
    public static async ValueTask<ReadOnlyMemory<byte>> ReadAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var held = new ArrayBufferWriter<byte>(Chunk);

        while (held.WrittenCount <= Most)
        {
            int taken = await request.Body
                .ReadAsync(held.GetMemory(Chunk), cancellationToken)
                .ConfigureAwait(false);

            if (taken is 0)
            {
                break;
            }

            held.Advance(taken);
        }

        return held.WrittenMemory;
    }
}
