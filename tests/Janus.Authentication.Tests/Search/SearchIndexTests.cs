using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Janus.Authentication.Search;
using Janus.Core;
using Xunit;

namespace Janus.Authentication.Tests.Search;

/// <summary>
/// The staff search's index: how a normalised query word matches a subject's fields,
/// the order and size of what it answers, who it answers, and what is left of a subject
/// that leaves it (IDN-SRCH-001 to IDN-SRCH-003, D-256).
/// </summary>
[Trait("kind", "unit")]
public sealed class SearchIndexTests
{
    private static readonly OrganizationId Clinic = new(Guid.Parse("0192f0a0-0000-7000-8000-000000000001"));

    private static readonly OrganizationId Pharmacy = new(Guid.Parse("0192f0a0-0000-7000-8000-000000000002"));

    private static readonly IndexScope Everyone = new(null, Accounts: true, Records: true);

    private static readonly IndexedSubject Fatma = Account(1, "فاطمه الزهراء", ["fatma.zahraa@example.com"], ["201001234567"], Clinic);

    private static readonly IndexedSubject Ahmed = Account(2, "ahmed hassan", ["a.hassan@example.org"], ["201112223344"], Clinic, Pharmacy);

    private static readonly IndexedSubject Mohamed = Account(3, "mohamed ali", [], ["201234509876"], Pharmacy);

    private static readonly IndexedSubject Unnamed = Account(4, null, ["nameless@example.net"], [], Clinic);

    private static readonly IndexedSubject Guest = Record(5, "ahmed samir", ["samir.guest@example.com"], ["201555000111"], Clinic);

    private static readonly IndexedSubject Customer = Account(6, "ahmed adel", ["adel@example.com"], ["201066667777"]);

    /// <summary>
    /// A word of three letters or more matches a field containing it, in the middle of
    /// a word as at its edges.
    /// </summary>
    /// <param name="word">The query word.</param>
    [Theory]
    [InlineData("has")]
    [InlineData("ssa")]
    [InlineData("san")]
    public void Find_AFragmentOfThreeLetters_FindsTheNameContainingIt(string word)
    {
        using SearchIndex index = Index();

        IReadOnlyList<IndexHit> hits = index.Find([word], Everyone, 20);

        Assert.Equal([Ahmed.Subject], hits.Select(hit => hit.Subject));
        Assert.Equal(SearchMatch.Name, hits[0].Matched);
    }

    /// <summary>
    /// Arabic text, as the normaliser leaves it, is sliced and matched as any other.
    /// </summary>
    [Fact]
    public void Find_AnArabicFragment_FindsTheNameContainingIt()
    {
        using SearchIndex index = Index();

        IReadOnlyList<IndexHit> hits = index.Find(["طمه"], Everyone, 20);

        Assert.Equal([Fatma.Subject], hits.Select(hit => hit.Subject));
    }

    /// <summary>
    /// A word of one or two letters matches a name word it begins, and nothing it only
    /// appears inside.
    /// </summary>
    [Fact]
    public void IDN_SRCH_001_AC2_ATwoLetterWordMatchesOnlyANameWordItBegins()
    {
        using SearchIndex index = Index();

        IReadOnlyList<IndexHit> prefixed = index.Find(["mo"], Everyone, 20);
        IReadOnlyList<IndexHit> inside = index.Find(["ss"], Everyone, 20);
        IReadOnlyList<IndexHit> letter = index.Find(["h"], Everyone, 20);

        Assert.Equal([Mohamed.Subject], prefixed.Select(hit => hit.Subject));
        Assert.Empty(inside);
        Assert.Equal([Ahmed.Subject], letter.Select(hit => hit.Subject));
    }

    /// <summary>
    /// A word of four letters or more also matches a name word one edit away: a letter
    /// left out, one added, or one replaced.
    /// </summary>
    /// <param name="word">The query word, one edit from <c>hassan</c>.</param>
    [Theory]
    [InlineData("hasan")]
    [InlineData("hassann")]
    [InlineData("hossan")]
    public void IDN_SRCH_001_AC3_AWordOfFourOneEditFromANameWordFindsIt(string word)
    {
        using SearchIndex index = Index();

        IReadOnlyList<IndexHit> hits = index.Find([word], Everyone, 20);

        Assert.Equal([Ahmed.Subject], hits.Select(hit => hit.Subject));
    }

    /// <summary>
    /// A word under four letters is held to containment, and a word two edits away
    /// matches nothing.
    /// </summary>
    /// <param name="word">The query word.</param>
    [Theory]
    [InlineData("alx")]
    [InlineData("hosan")]
    public void Find_AWordTooShortOrTooFarFromANameWord_FindsNothing(string word)
    {
        using SearchIndex index = Index();

        IReadOnlyList<IndexHit> hits = index.Find([word], Everyone, 20);

        Assert.Empty(hits);
    }

    /// <summary>
    /// A phone's last digits find it, and the field answered is the phone.
    /// </summary>
    [Fact]
    public void Find_APhonesLastFourDigits_FindsThePhone()
    {
        using SearchIndex index = Index();

        IReadOnlyList<IndexHit> hits = index.Find(["9876"], Everyone, 20);

        Assert.Equal([new IndexHit(Mohamed.Subject, SubjectKind.Account, SearchMatch.Phone)], hits);
    }

    /// <summary>
    /// A fragment of an email finds it, and a subject with no name is found by its
    /// email.
    /// </summary>
    [Fact]
    public void Find_AnEmailFragment_FindsTheEmailOfASubjectWithNoName()
    {
        using SearchIndex index = Index();

        IReadOnlyList<IndexHit> hits = index.Find(["nameless"], Everyone, 20);

        Assert.Equal([new IndexHit(Unnamed.Subject, SubjectKind.Account, SearchMatch.Email)], hits);
    }

    /// <summary>
    /// Every word must match one of the subject's fields, each word any field, and the
    /// field answered is the first matched in the order name, email, phone.
    /// </summary>
    [Fact]
    public void IDN_SRCH_001_AC5_EachWordMustMatchAFieldOfTheSubject()
    {
        using SearchIndex index = Index();

        IReadOnlyList<IndexHit> across = index.Find(["ahmed", "4567"], Everyone, 20);
        IReadOnlyList<IndexHit> named = index.Find(["ahmed", "3344"], Everyone, 20);
        IReadOnlyList<IndexHit> contacts = index.Find(["hassan", "3344"], Everyone, 20);
        IReadOnlyList<IndexHit> emailFirst = index.Find(["org", "3344"], Everyone, 20);

        Assert.Empty(across);
        Assert.Equal([new IndexHit(Ahmed.Subject, SubjectKind.Account, SearchMatch.Name)], named);
        Assert.Equal([new IndexHit(Ahmed.Subject, SubjectKind.Account, SearchMatch.Name)], contacts);
        Assert.Equal([new IndexHit(Ahmed.Subject, SubjectKind.Account, SearchMatch.Email)], emailFirst);
    }

    /// <summary>
    /// What a search answers is sorted by normalised name, then by subject, a subject
    /// with no name last, and is cut at the limit.
    /// </summary>
    [Fact]
    public void Find_ManyMatches_AreSortedByNameWithNoNameLastAndCutAtTheLimit()
    {
        using SearchIndex index = Index();

        IReadOnlyList<IndexHit> all = index.Find(["example"], Everyone, 20);
        IReadOnlyList<IndexHit> two = index.Find(["example"], Everyone, 2);

        Assert.Equal(
            [Customer.Subject, Ahmed.Subject, Guest.Subject, Fatma.Subject, Unnamed.Subject],
            all.Select(hit => hit.Subject));
        Assert.Equal([Customer.Subject, Ahmed.Subject], two.Select(hit => hit.Subject));
    }

    /// <summary>
    /// Two subjects of one name are ordered by subject.
    /// </summary>
    [Fact]
    public void Find_TwoSubjectsOfOneName_AreOrderedBySubject()
    {
        IndexedSubject later = Account(9, "ahmed hassan", [], [], Clinic);
        using var index = SearchIndex.Build([later, Ahmed]);

        IReadOnlyList<IndexHit> hits = index.Find(["hassan"], Everyone, 20);

        Assert.Equal([Ahmed.Subject, later.Subject], hits.Select(hit => hit.Subject));
    }

    /// <summary>
    /// Asked in an organization, the index answers its current members and the records
    /// it holds, each kind only where it is asked for; asked for every subject, it
    /// answers accounts with no membership too.
    /// </summary>
    [Fact]
    public void IDN_SRCH_002_AC2_InAnOrganizationOnlyItsMembersAndRecordsAreAnswered()
    {
        using SearchIndex index = Index();

        IReadOnlyList<IndexHit> clinic = index.Find(["ahmed"], new IndexScope(Clinic, Accounts: true, Records: true), 20);
        IReadOnlyList<IndexHit> accounts = index.Find(["ahmed"], new IndexScope(Clinic, Accounts: true, Records: false), 20);
        IReadOnlyList<IndexHit> records = index.Find(["ahmed"], new IndexScope(Clinic, Accounts: false, Records: true), 20);
        IReadOnlyList<IndexHit> pharmacy = index.Find(["ahmed"], new IndexScope(Pharmacy, Accounts: true, Records: true), 20);
        IReadOnlyList<IndexHit> everyone = index.Find(["ahmed"], Everyone, 20);

        Assert.Equal([Ahmed.Subject, Guest.Subject], clinic.Select(hit => hit.Subject));
        Assert.Equal([Ahmed.Subject], accounts.Select(hit => hit.Subject));
        Assert.Equal([new IndexHit(Guest.Subject, SubjectKind.Record, SearchMatch.Name)], records);
        Assert.Equal([Ahmed.Subject], pharmacy.Select(hit => hit.Subject));
        Assert.Equal([Customer.Subject, Ahmed.Subject, Guest.Subject], everyone.Select(hit => hit.Subject));
    }

    /// <summary>
    /// A subject held after the build takes its place in name order, and one changed is
    /// found by its new values and not its old.
    /// </summary>
    [Fact]
    public void Put_ASubjectAddedOrChangedAfterTheBuild_IsFoundAsItNowStands()
    {
        using SearchIndex index = Index();
        IndexedSubject added = Account(7, "ahmed badr", [], [], Clinic);
        IndexedSubject renamed = Mohamed with { Name = "mahmoud ali" };

        index.Put(added);
        index.Put(renamed);

        Assert.Equal(
            [Customer.Subject, added.Subject, Ahmed.Subject, Guest.Subject],
            index.Find(["ahmed"], Everyone, 20).Select(hit => hit.Subject));
        Assert.Equal([Mohamed.Subject], index.Find(["mahm"], Everyone, 20).Select(hit => hit.Subject));
        Assert.Empty(index.Find(["moha"], Everyone, 20));
        Assert.Equal(6 + 1, index.Count);
    }

    /// <summary>
    /// A subject taken out is found by nothing, and its name, every slice of it no other
    /// subject holds and every word of it no other name carries are zeros in the index's
    /// buffers.
    /// </summary>
    [Fact]
    public void Remove_ASubject_LeavesNothingOfItsTextInTheIndex()
    {
        IndexedSubject erased = Record(8, "zubaydah quorvex", ["quorvex@example.com"], ["209998887771"], Clinic);
        using var index = SearchIndex.Build([Ahmed, erased, Mohamed]);

        index.Remove(erased.Subject);

        Assert.Empty(index.Find(["quorvex"], Everyone, 20));
        Assert.Empty(index.Find(["zubaydah"], Everyone, 20));
        Assert.Empty(index.Find(["887771"], Everyone, 20));
        Assert.DoesNotContain("zubaydah", Buffers(index), StringComparison.Ordinal);
        Assert.DoesNotContain("quorvex", Buffers(index), StringComparison.Ordinal);
        Assert.DoesNotContain("887771", Buffers(index), StringComparison.Ordinal);
        Assert.DoesNotContain(Slice('z', 'u', 'b'), SliceKeys(index));
        Assert.DoesNotContain(Slice('v', 'e', 'x'), SliceKeys(index));
        Assert.Equal(2, index.Count);
    }

    /// <summary>
    /// The index is fragmented once the places its subjects left pass a tenth of it, and
    /// not before.
    /// </summary>
    [Fact]
    public void Fragmented_OnceMoreThanATenthHasLeft_IsTrue()
    {
        IndexedSubject[] subjects = [.. Enumerable.Range(10, 20).Select(number => Account(number, $"member {number}", [], [], Clinic))];
        using var index = SearchIndex.Build(subjects);

        index.Remove(subjects[0].Subject);
        index.Remove(subjects[1].Subject);
        bool atATenth = index.Fragmented;
        index.Remove(subjects[2].Subject);

        Assert.False(atATenth);
        Assert.True(index.Fragmented);
    }

    /// <summary>
    /// A subject given twice to a build is a fault of the calling code.
    /// </summary>
    [Fact]
    public void Build_ASubjectGivenTwice_Throws() =>
        Assert.Throws<ArgumentException>(() => SearchIndex.Build([Ahmed, Ahmed with { Name = "another" }]));

    /// <summary>
    /// A search with no word, or an empty word, is a fault of the calling code, as is a
    /// limit below one.
    /// </summary>
    [Fact]
    public void Find_NoWordOrNoLimit_Throws()
    {
        using SearchIndex index = Index();

        Assert.Throws<ArgumentException>(() => index.Find([], Everyone, 20));
        Assert.Throws<ArgumentException>(() => index.Find([string.Empty], Everyone, 20));
        Assert.Throws<ArgumentOutOfRangeException>(() => index.Find(["ahmed"], Everyone, 0));
    }

    /// <summary>
    /// Readers search while one writer changes the index, and every search answers the
    /// subjects that stand throughout.
    /// </summary>
    /// <returns>The work.</returns>
    [Fact]
    public async Task Find_WhileAWriterChangesTheIndex_AnswersWhatStandsThroughoutAsync()
    {
        using SearchIndex index = Index();
        IndexedSubject[] changing = [.. Enumerable.Range(100, 200).Select(number => Account(number, $"ahmed visitor {number}", [], [], Pharmacy))];

        var writer = Task.Run(
            () =>
            {
                foreach (IndexedSubject subject in changing)
                {
                    index.Put(subject);
                    index.Remove(subject.Subject);
                }
            },
            TestContext.Current.CancellationToken);
        Task<bool>[] readers =
        [
            .. Enumerable.Range(0, 4).Select(_ => Task.Run(
                () => Enumerable.Range(0, 200).All(_ =>
                    index.Find(["hassan"], Everyone, 20).Select(hit => hit.Subject).SequenceEqual([Ahmed.Subject])),
                TestContext.Current.CancellationToken)),
        ];

        await writer;
        bool[] answered = await Task.WhenAll(readers);

        Assert.DoesNotContain(false, answered);
        Assert.Equal(6, index.Count);
    }

    private static SearchIndex Index() => SearchIndex.Build([Fatma, Ahmed, Mohamed, Unnamed, Guest, Customer]);

    private static IndexedSubject Account(
        int number,
        string? name,
        IReadOnlyList<string> emails,
        IReadOnlyList<string> phones,
        params OrganizationId[] organizations) =>
        new(Subject(number), SubjectKind.Account, name, emails, phones, organizations);

    private static IndexedSubject Record(
        int number,
        string name,
        IReadOnlyList<string> emails,
        IReadOnlyList<string> phones,
        OrganizationId organization) =>
        new(Subject(number), SubjectKind.Record, name, emails, phones, [organization]);

    private static SubjectId Subject(int number) =>
        new(Guid.Parse($"4a1c0f00-0000-4000-8000-{number:D12}"));

    private static ulong Slice(char first, char second, char third) =>
        (1UL << 48) | ((ulong)first << 32) | ((ulong)second << 16) | third;

    // The characters of every buffer the index holds text in: its own and its words'.
    private static string Buffers(SearchIndex index) =>
        new string(Characters(Field<TextArena>(index, "_text")))
            + new string(Characters(Field<TextArena>(Field<NameWords>(index, "_words"), "_text")));

    private static char[] Characters(TextArena arena) => Field<char[]>(arena, "_text");

    private static ulong[] SliceKeys(SearchIndex index) => Field<ulong[]>(Field<SliceTable>(index, "_slices"), "_slices");

    private static T Field<T>(object holder, string name) =>
        (T)holder.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(holder)!;
}
