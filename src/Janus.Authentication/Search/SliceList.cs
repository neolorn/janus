using System;

namespace Janus.Authentication.Search;

/// <summary>
/// The entries whose values hold one slice, kept in name order with spare room, so a
/// search reads them in the order it answers and stops at its limit.
/// </summary>
/// <remarks>
/// Implements IDN-SRCH-003 (D-256). A list holds entry numbers and no text. The spare
/// room is a hundredth of the list, and at least one place, so an entry added after the
/// build is placed without making the list again.
/// </remarks>
internal sealed class SliceList
{
    private const int SpareDivisor = 100;

    private int[] _entries = new int[1];

    /// <summary>
    /// How many entries the list holds.
    /// </summary>
    public int Count { get; private set; }

    /// <summary>
    /// The entries, in name order, lent for the length of the read.
    /// </summary>
    public ReadOnlySpan<int> Entries => _entries.AsSpan(0, Count);

    /// <summary>
    /// Adds an entry that comes after every entry the list holds, as a build adds them.
    /// </summary>
    /// <param name="entry">The entry.</param>
    public void Append(int entry)
    {
        if (Count == _entries.Length)
        {
            Array.Resize(ref _entries, _entries.Length * 2);
        }

        _entries[Count++] = entry;
    }

    /// <summary>
    /// Places an entry where the name order puts it.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="order">The name order of two entries.</param>
    public void Insert(int entry, Comparison<int> order)
    {
        int place = Place(entry, order);

        if (Count == _entries.Length)
        {
            Array.Resize(ref _entries, Count + 1 + Spare(Count + 1));
        }

        Array.Copy(_entries, place, _entries, place + 1, Count - place);
        _entries[place] = entry;
        Count++;
    }

    /// <summary>
    /// Takes an entry out.
    /// </summary>
    /// <param name="entry">The entry, whose values are still readable.</param>
    /// <param name="order">The name order of two entries.</param>
    public void Remove(int entry, Comparison<int> order)
    {
        int place = Place(entry, order);

        if (place < Count && _entries[place] == entry)
        {
            Array.Copy(_entries, place + 1, _entries, place, Count - place - 1);
            Count--;
        }
    }

    /// <summary>
    /// Leaves the list with its spare room and no more, once a build has added every
    /// entry.
    /// </summary>
    public void Settle() => Array.Resize(ref _entries, Count + Spare(Count));

    private static int Spare(int count) => Math.Max(1, count / SpareDivisor);

    // The first place whose entry does not come before the one given.
    private int Place(int entry, Comparison<int> order)
    {
        int low = 0;
        int high = Count;

        while (low < high)
        {
            int middle = low + ((high - low) / 2);

            if (order(_entries[middle], entry) < 0)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }
}
