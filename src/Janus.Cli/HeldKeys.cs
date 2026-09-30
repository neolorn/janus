using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Janus.Core;

namespace Janus.Cli;

/// <summary>
/// The key ring a command reads its keys into, and every key decoded on the way, each
/// cleared when the command ends however it ends.
/// </summary>
/// <remarks>
/// Implements OPS-SEC-001, CONV-LOG-003 and CONV-CODE-007. A key lives in memory for as
/// long as the command that needs it and no longer; the runtime moving or collecting an
/// array is no reason to leave its content behind. A key decoded from the document is
/// cleared as soon as the ring holds its own copy, and the ring is cleared when the
/// command ends, after which a read of it throws.
/// </remarks>
internal sealed class HeldKeys : IDisposable
{
    private readonly List<byte[]> _held = [];

    /// <summary>
    /// The key ring the command's keys are read into and borrowed from.
    /// </summary>
    public KeyRing Ring { get; } = new();

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
    /// Clears every key decoded so far; the ring keeps the copies it holds.
    /// </summary>
    public void Release()
    {
        foreach (byte[] key in _held)
        {
            CryptographicOperations.ZeroMemory(key);
        }

        _held.Clear();
    }

    /// <summary>
    /// Clears every key decoded and the key ring.
    /// </summary>
    public void Dispose()
    {
        Release();
        Ring.Clear();
    }
}
