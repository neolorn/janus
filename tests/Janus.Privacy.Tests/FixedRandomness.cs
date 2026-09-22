using System;
using System.Security.Cryptography;

namespace Janus.Privacy.Tests;

/// <summary>
/// A randomness source handing out bytes a test chose, so the jitter a delay is
/// multiplied by is the one the test means.
/// </summary>
/// <param name="bytes">The bytes every draw returns, repeated to fill the buffer.</param>
internal sealed class FixedRandomness(params byte[] bytes) : RandomNumberGenerator
{
    /// <inheritdoc/>
    public override void GetBytes(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        for (int index = 0; index < data.Length; index++)
        {
            data[index] = bytes[index % bytes.Length];
        }
    }
}
