using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace Janus.Cli;

/// <summary>
/// Every key a command has read, cleared when the command ends however it ends.
/// </summary>
/// <remarks>
/// Implements OPS-SEC-001 and CONV-LOG-003. A key lives in memory for as long as the
/// command that needs it and no longer; the runtime moving or collecting an array is
/// no reason to leave its content behind.
/// </remarks>
internal sealed class HeldKeys : IDisposable
{
    private readonly List<byte[]> _held = [];

    /// <summary>
    /// Takes a key into the set that is cleared.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The key, so it is held as it is read.</returns>
    public byte[] Hold(byte[] key)
    {
        _held.Add(key);

        return key;
    }

    /// <summary>
    /// Clears every key held.
    /// </summary>
    public void Dispose()
    {
        foreach (byte[] key in _held)
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }
}
