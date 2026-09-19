using System;
using System.Security.Cryptography;

namespace Janus.Core.Tests;

/// <summary>
/// A randomness source that hands out bytes a test chose, so that what a generator
/// derives from them can be read off the result.
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
