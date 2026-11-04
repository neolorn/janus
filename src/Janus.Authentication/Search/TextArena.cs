using System;

namespace Janus.Authentication.Search;

/// <summary>
/// The index's own character buffer, which every indexed value is copied into and
/// read from, so that what the index holds is in memory it can overwrite.
/// </summary>
/// <param name="capacity">How many characters the buffer is first made for.</param>
/// <remarks>
/// Implements IDN-SRCH-003 (D-256). The buffer is pinned, as the key ring's arrays are
/// (CONV-CODE-007), so a compacting collection never moves it and leaves no copy of a
/// value behind; a value released is overwritten with zeros where it lay, and a buffer
/// outgrown is overwritten before it is let go.
/// </remarks>
internal sealed class TextArena(int capacity)
{
    private char[] _text = GC.AllocateArray<char>(Math.Max(capacity, 1), pinned: true);

    private int _length;

    /// <summary>
    /// How many characters the buffer holds, released ones included.
    /// </summary>
    public int Length => _length;

    /// <summary>
    /// How many of those characters were released and now hold zeros.
    /// </summary>
    public int Released { get; private set; }

    /// <summary>
    /// Copies a value in.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>Where it lies.</returns>
    public TextRange Add(ReadOnlySpan<char> value)
    {
        if (_length + value.Length > _text.Length)
        {
            Grow(_length + value.Length);
        }

        var range = new TextRange(_length, value.Length);
        value.CopyTo(_text.AsSpan(_length));
        _length += value.Length;

        return range;
    }

    /// <summary>
    /// Reads a value back where it lies.
    /// </summary>
    /// <param name="range">Where it lies.</param>
    /// <returns>The value, lent for the length of the read.</returns>
    public ReadOnlySpan<char> Read(TextRange range) => _text.AsSpan(range.Start, range.Length);

    /// <summary>
    /// Overwrites a value with zeros where it lies.
    /// </summary>
    /// <param name="range">Where it lies.</param>
    public void Release(TextRange range)
    {
        _text.AsSpan(range.Start, range.Length).Clear();
        Released += range.Length;
    }

    /// <summary>
    /// Overwrites every value the buffer holds with zeros.
    /// </summary>
    public void Clear()
    {
        _text.AsSpan().Clear();
        Released = _length;
    }

    private void Grow(int needed)
    {
        char[] grown = GC.AllocateArray<char>(Math.Max(needed, _text.Length * 2), pinned: true);
        _text.AsSpan(0, _length).CopyTo(grown);
        _text.AsSpan().Clear();
        _text = grown;
    }
}
