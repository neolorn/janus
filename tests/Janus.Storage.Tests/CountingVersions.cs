using System;
using System.Collections;
using System.Collections.Generic;

namespace Janus.Storage.Tests;

/// <summary>
/// The key-encryption key versions a deployment holds, counting how often one is taken
/// out. An unwrap takes one out, so the count is the number of unwraps
/// (PRIV-RIGHT-005a AC12).
/// </summary>
/// <param name="versions">The versions held.</param>
internal sealed class CountingVersions(IReadOnlyDictionary<int, ReadOnlyMemory<byte>> versions)
    : IReadOnlyDictionary<int, ReadOnlyMemory<byte>>
{
    /// <summary>
    /// How often a version has been taken out.
    /// </summary>
    public int Reads { get; private set; }

    /// <inheritdoc/>
    public IEnumerable<int> Keys => versions.Keys;

    /// <inheritdoc/>
    public IEnumerable<ReadOnlyMemory<byte>> Values => versions.Values;

    /// <inheritdoc/>
    public int Count => versions.Count;

    /// <inheritdoc/>
    public ReadOnlyMemory<byte> this[int key]
    {
        get
        {
            Reads++;

            return versions[key];
        }
    }

    /// <inheritdoc/>
    public bool ContainsKey(int key) => versions.ContainsKey(key);

    /// <inheritdoc/>
    public bool TryGetValue(int key, out ReadOnlyMemory<byte> value)
    {
        Reads++;

        return versions.TryGetValue(key, out value);
    }

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<int, ReadOnlyMemory<byte>>> GetEnumerator() => versions.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
