using System;

namespace Janus.Authentication.Search;

/// <summary>
/// The slice list of each slice the index holds, found by the slice itself.
/// </summary>
/// <remarks>
/// Implements IDN-SRCH-003 (D-256). A slice is three characters of an indexed value, or
/// the first one or two of a name word, so the table's keys are text: they are held in
/// a pinned array of the index's own, a slice no entry holds any more is overwritten
/// where it lay, and an array outgrown is overwritten before it is let go. The table is
/// open addressed, so no slice is ever copied into a structure the index cannot clear.
/// </remarks>
internal sealed class SliceTable
{
    // A slot that never held a slice, and one whose slice left: neither is a slice,
    // since every slice carries its kind in the bits above its characters.
    private const ulong Vacant = 0;

    private const ulong Left = 1;

    private const int InitialSlots = 1024;

    private ulong[] _slices = GC.AllocateArray<ulong>(InitialSlots, pinned: true);

    private SliceList?[] _lists = new SliceList?[InitialSlots];

    private int _occupied;

    /// <summary>
    /// How many slices the table holds.
    /// </summary>
    public int Count { get; private set; }

    /// <summary>
    /// Finds a slice's list.
    /// </summary>
    /// <param name="slice">The slice.</param>
    /// <returns>Its list, or nothing where no entry holds it.</returns>
    public SliceList? Find(ulong slice)
    {
        int mask = _slices.Length - 1;

        for (int slot = Home(slice, mask); ; slot = (slot + 1) & mask)
        {
            if (_slices[slot] == Vacant)
            {
                return null;
            }

            if (_slices[slot] == slice)
            {
                return _lists[slot];
            }
        }
    }

    /// <summary>
    /// Finds a slice's list, adding an empty one where no entry holds it yet.
    /// </summary>
    /// <param name="slice">The slice.</param>
    /// <returns>Its list.</returns>
    public SliceList Held(ulong slice)
    {
        if (Find(slice) is SliceList found)
        {
            return found;
        }

        if ((_occupied + 1) * 10 > _slices.Length * 7)
        {
            Rehash(Count * 2 > _slices.Length / 2 ? _slices.Length * 2 : _slices.Length);
        }

        var list = new SliceList();
        Place(slice, list);
        Count++;

        return list;
    }

    /// <summary>
    /// Takes out a slice no entry holds any more, overwriting it where it lay.
    /// </summary>
    /// <param name="slice">The slice.</param>
    public void Remove(ulong slice)
    {
        int mask = _slices.Length - 1;

        for (int slot = Home(slice, mask); _slices[slot] != Vacant; slot = (slot + 1) & mask)
        {
            if (_slices[slot] == slice)
            {
                _slices[slot] = Left;
                _lists[slot] = null;
                Count--;

                return;
            }
        }
    }

    /// <summary>
    /// Leaves every list with its spare room and no more, once a build has added every
    /// entry.
    /// </summary>
    public void Settle()
    {
        foreach (SliceList? list in _lists)
        {
            list?.Settle();
        }
    }

    /// <summary>
    /// Overwrites every slice the table holds.
    /// </summary>
    public void Clear()
    {
        _slices.AsSpan().Clear();
        Array.Clear(_lists);
        Count = 0;
        _occupied = 0;
    }

    // Fibonacci hashing spreads slices that share their leading characters.
    private static int Home(ulong slice, int mask) =>
        (int)((slice * 0x9E3779B97F4A7C15UL) >> 32) & mask;

    private void Place(ulong slice, SliceList list)
    {
        int mask = _slices.Length - 1;
        int slot = Home(slice, mask);

        while (_slices[slot] != Vacant)
        {
            slot = (slot + 1) & mask;
        }

        _slices[slot] = slice;
        _lists[slot] = list;
        _occupied++;
    }

    private void Rehash(int slots)
    {
        ulong[] slices = _slices;
        SliceList?[] lists = _lists;

        _slices = GC.AllocateArray<ulong>(slots, pinned: true);
        _lists = new SliceList?[slots];
        _occupied = 0;

        for (int slot = 0; slot < slices.Length; slot++)
        {
            if (lists[slot] is SliceList list)
            {
                Place(slices[slot], list);
            }
        }

        slices.AsSpan().Clear();
    }
}
