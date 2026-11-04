using System;
using System.Collections.Generic;

namespace Janus.Authentication.Search;

/// <summary>
/// Every distinct word of the names the index holds, each with the entries whose name
/// carries it, so a query word can be matched against the names one edit away from it.
/// </summary>
/// <remarks>
/// Implements IDN-SRCH-001 and IDN-SRCH-003 (D-256). The words are held in a buffer of
/// the index's own and found through a table of their numbers, which holds no text; a
/// word no entry carries any more is overwritten where it lay. The words are kept by
/// length, so a word one edit away is looked for among the words one character shorter,
/// as long, and one longer, and nowhere else.
/// </remarks>
internal sealed class NameWords
{
    private const int Vacant = -1;

    private const int Left = -2;

    private const int InitialSlots = 1024;

    private readonly TextArena _text = new(InitialSlots * 8);

    private readonly List<TextRange> _ranges = [];

    private readonly List<SliceList> _lists = [];

    private readonly List<List<int>> _byLength = [];

    private int[] _slots = Vacancies(InitialSlots);

    private int _occupied;

    /// <summary>
    /// How many words no entry carries any more, whose places stay until a rebuild.
    /// </summary>
    public int Released { get; private set; }

    /// <summary>
    /// How many words were ever held, released ones included.
    /// </summary>
    public int Count => _ranges.Count;

    /// <summary>
    /// Finds a word's list, adding the word where no entry carries it yet.
    /// </summary>
    /// <param name="word">The word.</param>
    /// <returns>The list of the entries whose name carries it.</returns>
    public SliceList Held(ReadOnlySpan<char> word)
    {
        if (_slots[Slot(word)] is int held && held >= 0)
        {
            return _lists[held];
        }

        if ((_occupied + 1) * 10 > _slots.Length * 7)
        {
            Rehash(_slots.Length * 2);
        }

        int number = _ranges.Count;
        _ranges.Add(_text.Add(word));
        _lists.Add(new SliceList());

        while (_byLength.Count <= word.Length)
        {
            _byLength.Add([]);
        }

        _byLength[word.Length].Add(number);
        Place(word, number);

        return _lists[number];
    }

    /// <summary>
    /// Finds a word's list.
    /// </summary>
    /// <param name="word">The word.</param>
    /// <returns>The list, or nothing where no entry carries the word.</returns>
    public SliceList? Find(ReadOnlySpan<char> word) =>
        _slots[Slot(word)] is int number && number >= 0 ? _lists[number] : null;

    /// <summary>
    /// Overwrites a word no entry carries any more where it lay.
    /// </summary>
    /// <param name="word">The word, read before the entry's own copy is released.</param>
    public void Release(ReadOnlySpan<char> word)
    {
        int slot = Slot(word);
        int number = _slots[slot];

        if (number < 0 || _lists[number].Count > 0)
        {
            return;
        }

        _text.Release(_ranges[number]);
        _ranges[number] = default;
        _slots[slot] = Left;
        Released++;
    }

    /// <summary>
    /// Collects the lists of the words one edit away from a word, or equal to it.
    /// </summary>
    /// <param name="word">The query word.</param>
    /// <param name="found">Where each list is added.</param>
    public void Near(ReadOnlySpan<char> word, List<SliceList> found)
    {
        for (int length = Math.Max(word.Length - 1, 1); length <= word.Length + 1 && length < _byLength.Count; length++)
        {
            foreach (int number in _byLength[length])
            {
                if (_lists[number].Count > 0 && EditDistance.WithinOne(_text.Read(_ranges[number]), word))
                {
                    found.Add(_lists[number]);
                }
            }
        }
    }

    /// <summary>
    /// Leaves every list with its spare room and no more, once a build has added every
    /// entry.
    /// </summary>
    public void Settle()
    {
        foreach (SliceList list in _lists)
        {
            list.Settle();
        }
    }

    /// <summary>
    /// Overwrites every word the table holds.
    /// </summary>
    public void Clear()
    {
        _text.Clear();
        _slots.AsSpan().Fill(Vacant);
        _ranges.Clear();
        _lists.Clear();
        _byLength.Clear();
        _occupied = 0;
    }

    private static int[] Vacancies(int slots)
    {
        int[] vacant = new int[slots];
        vacant.AsSpan().Fill(Vacant);

        return vacant;
    }

    private static int Home(ReadOnlySpan<char> word, int mask) =>
        string.GetHashCode(word, StringComparison.Ordinal) & mask;

    // The slot holding the word, or the vacant slot where its search ended.
    private int Slot(ReadOnlySpan<char> word)
    {
        int mask = _slots.Length - 1;

        for (int slot = Home(word, mask); ; slot = (slot + 1) & mask)
        {
            int number = _slots[slot];

            if (number == Vacant)
            {
                return slot;
            }

            if (number >= 0 && word.SequenceEqual(_text.Read(_ranges[number])))
            {
                return slot;
            }
        }
    }

    private void Place(ReadOnlySpan<char> word, int number)
    {
        int mask = _slots.Length - 1;
        int slot = Home(word, mask);

        while (_slots[slot] != Vacant)
        {
            slot = (slot + 1) & mask;
        }

        _slots[slot] = number;
        _occupied++;
    }

    private void Rehash(int slots)
    {
        _slots = Vacancies(slots);
        _occupied = 0;

        for (int number = 0; number < _ranges.Count; number++)
        {
            if (_ranges[number].Length > 0)
            {
                Place(_text.Read(_ranges[number]), number);
            }
        }
    }
}
