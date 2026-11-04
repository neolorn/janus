using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Janus.Core;

namespace Janus.Authentication.Search;

/// <summary>
/// The staff search's index, held in this process's memory and written nowhere: the
/// names, emails and phones of the subjects it holds, cut into slices whose lists are
/// kept in name order, so a search reads its candidates in the order it answers and
/// stops at its limit's verified hits.
/// </summary>
/// <remarks>
/// Implements IDN-SRCH-001 and IDN-SRCH-003 (D-256). Every value is copied into the
/// index's own buffers, and a subject that leaves has its values and every slice no
/// other subject holds overwritten with zeros at once. Readers search side by side and
/// one writer changes the index at a time. Each subject that leaves leaves its place
/// behind, and once those places pass a tenth of the index it is fragmented, to be
/// rebuilt and swapped in.
/// </remarks>
internal sealed class SearchIndex : IDisposable
{
    // A word of this many characters or more matches a value containing it; a shorter
    // one matches a name word it begins.
    private const int ContainedLength = 3;

    // A word of this many characters or more also matches a name word one edit away.
    private const int NearLength = 4;

    // The kind of a slice, in the bits above its three characters.
    private const ulong Trigram = 1UL << 48;

    private const ulong FirstLetter = 2UL << 48;

    private const ulong FirstTwoLetters = 3UL << 48;

    private const char Separator = ' ';

    private readonly ReaderWriterLockSlim _guard = new(LockRecursionPolicy.NoRecursion);

    private readonly TextArena _text;

    private readonly SliceTable _slices = new();

    private readonly NameWords _words = new();

    private readonly List<IndexEntry> _entries = [];

    private readonly Dictionary<SubjectId, int> _bySubject = [];

    private readonly Comparison<int> _order;

    private ulong[] _scratch = GC.AllocateArray<ulong>(256, pinned: true);

    private int _removed;

    private SearchIndex(int characters)
    {
        _text = new TextArena(characters);
        _order = Order;
    }

    /// <summary>
    /// How many subjects the index holds.
    /// </summary>
    public int Count
    {
        get
        {
            _guard.EnterReadLock();

            try
            {
                return _bySubject.Count;
            }
            finally
            {
                _guard.ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Whether the places subjects left behind have passed a tenth of the index.
    /// </summary>
    public bool Fragmented
    {
        get
        {
            _guard.EnterReadLock();

            try
            {
                return _removed * 10 > _entries.Count;
            }
            finally
            {
                _guard.ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Builds an index over the subjects given.
    /// </summary>
    /// <param name="subjects">The subjects, each once.</param>
    /// <returns>The index.</returns>
    /// <exception cref="ArgumentNullException">The subjects are absent.</exception>
    /// <exception cref="ArgumentException">A subject is given twice.</exception>
    public static SearchIndex Build(IReadOnlyCollection<IndexedSubject> subjects)
    {
        ArgumentNullException.ThrowIfNull(subjects);

        var index = new SearchIndex(subjects.Sum(Characters));

        // Added in name order, each entry comes after every one its lists already hold.
        foreach (IndexedSubject subject in subjects
                     .OrderBy(subject => string.IsNullOrEmpty(subject.Name))
                     .ThenBy(subject => subject.Name, StringComparer.Ordinal)
                     .ThenBy(subject => subject.Subject.Value))
        {
            if (index._bySubject.ContainsKey(subject.Subject))
            {
                throw new ArgumentException("A subject is given twice.", nameof(subjects));
            }

            index.Add(subject, appended: true);
        }

        index._slices.Settle();
        index._words.Settle();

        return index;
    }

    /// <summary>
    /// Holds a subject as it now stands, replacing what the index held of it.
    /// </summary>
    /// <param name="subject">The subject.</param>
    /// <exception cref="ArgumentNullException">The subject is absent.</exception>
    public void Put(IndexedSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        _guard.EnterWriteLock();

        try
        {
            if (_bySubject.TryGetValue(subject.Subject, out int held))
            {
                Take(held);
            }

            Add(subject, appended: false);
        }
        finally
        {
            _guard.ExitWriteLock();
        }
    }

    /// <summary>
    /// Takes a subject out, overwriting its values and every slice no other subject
    /// holds with zeros.
    /// </summary>
    /// <param name="subject">The subject.</param>
    public void Remove(SubjectId subject)
    {
        _guard.EnterWriteLock();

        try
        {
            if (_bySubject.TryGetValue(subject, out int held))
            {
                Take(held);
            }
        }
        finally
        {
            _guard.ExitWriteLock();
        }
    }

    /// <summary>
    /// Finds the subjects every query word matches one of whose fields, in name order.
    /// </summary>
    /// <param name="words">The normalised query's words.</param>
    /// <param name="scope">Which subjects may be answered.</param>
    /// <param name="limit">At most how many.</param>
    /// <returns>The subjects found, sorted by normalised name, then by subject.</returns>
    /// <exception cref="ArgumentNullException">The words are absent.</exception>
    /// <exception cref="ArgumentException">There is no word, or a word is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The limit is below one.</exception>
    public IReadOnlyList<IndexHit> Find(IReadOnlyList<string> words, IndexScope scope, int limit)
    {
        ArgumentNullException.ThrowIfNull(words);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);

        if (words.Count == 0 || words.Any(string.IsNullOrEmpty))
        {
            throw new ArgumentException("A search has at least one word, and no word is empty.", nameof(words));
        }

        _guard.EnterReadLock();

        try
        {
            List<SliceList>? candidates = null;

            foreach (string word in words)
            {
                List<SliceList> lists = Candidates(word);

                if (lists.Count == 0)
                {
                    return [];
                }

                if (candidates is null || lists.Sum(list => list.Count) < candidates.Sum(list => list.Count))
                {
                    candidates = lists;
                }
            }

            return Verified(candidates!, words, scope, limit);
        }
        finally
        {
            _guard.ExitReadLock();
        }
    }

    /// <summary>
    /// Overwrites every value and slice the index holds with zeros, as one swapped out
    /// for a rebuilt index is; a search that still reads it finds nothing.
    /// </summary>
    public void Clear()
    {
        _guard.EnterWriteLock();

        try
        {
            _text.Clear();
            _slices.Clear();
            _words.Clear();
            _scratch.AsSpan().Clear();
            _entries.Clear();
            _bySubject.Clear();
        }
        finally
        {
            _guard.ExitWriteLock();
        }
    }

    /// <summary>
    /// Overwrites the index, as the process stops, and lets its guard go.
    /// </summary>
    public void Dispose()
    {
        Clear();
        _guard.Dispose();
    }

    private static int Characters(IndexedSubject subject) =>
        (subject.Name?.Length ?? 0) + subject.Emails.Sum(email => email.Length) + subject.Phones.Sum(phone => phone.Length);

    private static ulong Slice(ulong kind, char first, char second, char third) =>
        kind | ((ulong)first << 32) | ((ulong)second << 16) | third;

    private static SearchMatch? Matched(bool name, bool email, bool phone) =>
        name ? SearchMatch.Name : email ? SearchMatch.Email : phone ? SearchMatch.Phone : null;

    private static bool Visible(IndexEntry entry, IndexScope scope) =>
        (entry.Kind == SubjectKind.Account ? scope.Accounts : scope.Records)
            && (scope.Organization is not OrganizationId organization || entry.Organizations.Contains(organization));

    // The lists whose entries are the only subjects a word can match, each in name
    // order: the rarest of its slices for a value containing it, or the first letters
    // of a name word it begins, and the words one edit away.
    private List<SliceList> Candidates(string word)
    {
        var lists = new List<SliceList>();

        if (word.Length >= ContainedLength)
        {
            SliceList? rarest = null;

            for (int at = 0; at + ContainedLength <= word.Length; at++)
            {
                SliceList? list = _slices.Find(Slice(Trigram, word[at], word[at + 1], word[at + 2]));

                if (list is null)
                {
                    rarest = null;

                    break;
                }

                if (rarest is null || list.Count < rarest.Count)
                {
                    rarest = list;
                }
            }

            if (rarest is not null)
            {
                lists.Add(rarest);
            }
        }
        else if (_slices.Find(word.Length == 1
                     ? Slice(FirstLetter, word[0], '\0', '\0')
                     : Slice(FirstTwoLetters, word[0], word[1], '\0')) is SliceList begun)
        {
            lists.Add(begun);
        }

        if (word.Length >= NearLength)
        {
            _words.Near(word, lists);
        }

        return lists;
    }

    // Reads the candidates in name order, each once, and keeps those every word
    // matches until the limit is reached.
    private List<IndexHit> Verified(List<SliceList> candidates, IReadOnlyList<string> words, IndexScope scope, int limit)
    {
        var hits = new List<IndexHit>(Math.Min(limit, 64));
        int[] cursors = new int[candidates.Count];

        while (hits.Count < limit && Next(candidates, cursors) is int next)
        {
            IndexEntry entry = _entries[next];

            if (Visible(entry, scope) && Matches(entry, words) is SearchMatch matched)
            {
                hits.Add(new IndexHit(entry.Subject, entry.Kind, matched));
            }
        }

        return hits;
    }

    // The first entry in name order at the heads of the lists, each list holding it
    // moved past it.
    private int? Next(List<SliceList> lists, int[] cursors)
    {
        int? first = null;

        for (int list = 0; list < lists.Count; list++)
        {
            ReadOnlySpan<int> entries = lists[list].Entries;

            if (cursors[list] < entries.Length && (first is not int held || _order(entries[cursors[list]], held) < 0))
            {
                first = entries[cursors[list]];
            }
        }

        if (first is int found)
        {
            for (int list = 0; list < lists.Count; list++)
            {
                ReadOnlySpan<int> entries = lists[list].Entries;

                if (cursors[list] < entries.Length && entries[cursors[list]] == found)
                {
                    cursors[list]++;
                }
            }
        }

        return first;
    }

    // Which field matched first where every word matches one of the entry's fields.
    private SearchMatch? Matches(IndexEntry entry, IReadOnlyList<string> words)
    {
        bool name = false;
        bool email = false;
        bool phone = false;

        foreach (string word in words)
        {
            bool inName = InName(entry, word);
            bool inEmail = word.Length >= ContainedLength && Contains(entry.Emails, word);
            bool inPhone = word.Length >= ContainedLength && Contains(entry.Phones, word);

            if (!inName && !inEmail && !inPhone)
            {
                return null;
            }

            name |= inName;
            email |= inEmail;
            phone |= inPhone;
        }

        return Matched(name, email, phone);
    }

    private bool InName(IndexEntry entry, string word)
    {
        if (entry.Name is not TextRange name)
        {
            return false;
        }

        if (word.Length >= ContainedLength && _text.Read(name).Contains(word, StringComparison.Ordinal))
        {
            return true;
        }

        foreach (TextRange range in entry.Words)
        {
            ReadOnlySpan<char> held = _text.Read(range);

            if ((word.Length < ContainedLength && held.StartsWith(word, StringComparison.Ordinal))
                || (word.Length >= NearLength && EditDistance.WithinOne(held, word)))
            {
                return true;
            }
        }

        return false;
    }

    private bool Contains(IReadOnlyList<TextRange> values, string word)
    {
        foreach (TextRange value in values)
        {
            if (_text.Read(value).Contains(word, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    // Name order: by normalised name, a subject with none last, then by subject.
    private int Order(int left, int right)
    {
        if (left == right)
        {
            return 0;
        }

        IndexEntry first = _entries[left];
        IndexEntry second = _entries[right];

        if (first.Name.HasValue != second.Name.HasValue)
        {
            return first.Name.HasValue ? -1 : 1;
        }

        if (first.Name is TextRange one && second.Name is TextRange other)
        {
            int named = _text.Read(one).SequenceCompareTo(_text.Read(other));

            if (named != 0)
            {
                return named;
            }
        }

        return first.Subject.Value.CompareTo(second.Subject.Value);
    }

    private void Add(IndexedSubject subject, bool appended)
    {
        TextRange? name = string.IsNullOrEmpty(subject.Name) ? null : _text.Add(subject.Name);
        var entry = new IndexEntry(
            subject.Subject,
            subject.Kind,
            name,
            name is TextRange held ? DistinctWords(held) : [],
            [.. subject.Emails.Select(email => _text.Add(email))],
            [.. subject.Phones.Select(phone => _text.Add(phone))],
            [.. subject.Organizations]);

        int number = _entries.Count;
        _entries.Add(entry);
        _bySubject[entry.Subject] = number;

        int count = Slices(entry);

        foreach (ulong slice in _scratch.AsSpan(0, count))
        {
            Place(_slices.Held(slice), number, appended);
        }

        _scratch.AsSpan(0, count).Clear();

        foreach (TextRange word in entry.Words)
        {
            Place(_words.Held(_text.Read(word)), number, appended);
        }
    }

    private void Place(SliceList list, int number, bool appended)
    {
        if (appended)
        {
            list.Append(number);
        }
        else
        {
            list.Insert(number, _order);
        }
    }

    // Takes an entry out of every list while its values can still be read, then
    // overwrites the values, and each slice and word no entry holds any more.
    private void Take(int number)
    {
        IndexEntry entry = _entries[number];
        int count = Slices(entry);

        foreach (ulong slice in _scratch.AsSpan(0, count))
        {
            if (_slices.Find(slice) is SliceList list)
            {
                list.Remove(number, _order);

                if (list.Count == 0)
                {
                    _slices.Remove(slice);
                }
            }
        }

        _scratch.AsSpan(0, count).Clear();

        foreach (TextRange word in entry.Words)
        {
            ReadOnlySpan<char> held = _text.Read(word);
            _words.Find(held)?.Remove(number, _order);
            _words.Release(held);
        }

        if (entry.Name is TextRange name)
        {
            _text.Release(name);
        }

        foreach (TextRange value in entry.Emails.Concat(entry.Phones))
        {
            _text.Release(value);
        }

        entry.Remove();
        _bySubject.Remove(entry.Subject);
        _removed++;
    }

    // The distinct slices of an entry's values, sorted, in the scratch buffer: every
    // three characters of a value that hold no separator, and the first letter and
    // the first two of each name word.
    private int Slices(IndexEntry entry)
    {
        int count = 0;

        if (entry.Name is TextRange name)
        {
            count = Trigrams(_text.Read(name), count);
        }

        foreach (TextRange value in entry.Emails.Concat(entry.Phones))
        {
            count = Trigrams(_text.Read(value), count);
        }

        foreach (TextRange range in entry.Words)
        {
            ReadOnlySpan<char> word = _text.Read(range);
            count = Scratched(Slice(FirstLetter, word[0], '\0', '\0'), count);

            if (word.Length > 1)
            {
                count = Scratched(Slice(FirstTwoLetters, word[0], word[1], '\0'), count);
            }
        }

        Span<ulong> slices = _scratch.AsSpan(0, count);
        slices.Sort();

        int distinct = 0;

        for (int at = 0; at < count; at++)
        {
            if (distinct == 0 || slices[distinct - 1] != slices[at])
            {
                slices[distinct++] = slices[at];
            }
        }

        slices[distinct..].Clear();

        return distinct;
    }

    private int Trigrams(ReadOnlySpan<char> value, int count)
    {
        for (int at = 0; at + ContainedLength <= value.Length; at++)
        {
            ReadOnlySpan<char> slice = value.Slice(at, ContainedLength);

            if (!slice.Contains(Separator))
            {
                count = Scratched(Slice(Trigram, slice[0], slice[1], slice[2]), count);
            }
        }

        return count;
    }

    private int Scratched(ulong slice, int count)
    {
        if (count == _scratch.Length)
        {
            ulong[] grown = GC.AllocateArray<ulong>(_scratch.Length * 2, pinned: true);
            _scratch.AsSpan().CopyTo(grown);
            _scratch.AsSpan().Clear();
            _scratch = grown;
        }

        _scratch[count] = slice;

        return count + 1;
    }

    private List<TextRange> DistinctWords(TextRange name)
    {
        var words = new List<TextRange>();
        ReadOnlySpan<char> text = _text.Read(name);
        int start = 0;

        while (start < text.Length)
        {
            int length = text[start..].IndexOf(Separator);
            length = length < 0 ? text.Length - start : length;

            if (length > 0 && !Held(words, text.Slice(start, length)))
            {
                words.Add(new TextRange(name.Start + start, length));
            }

            start += length + 1;
        }

        return words;
    }

    private bool Held(List<TextRange> words, ReadOnlySpan<char> word)
    {
        foreach (TextRange held in words)
        {
            if (_text.Read(held).SequenceEqual(word))
            {
                return true;
            }
        }

        return false;
    }
}
