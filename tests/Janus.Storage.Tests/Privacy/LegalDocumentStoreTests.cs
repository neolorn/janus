using System;
using System.Linq;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Storage.Privacy.Documents;
using Xunit;

namespace Janus.Storage.Tests.Privacy;

/// <summary>
/// What the tables hold of a legal document: one governing text a version, the
/// translations attached to it, and the cookies a version of the notice lists
/// (PRIV-CONS-005, PRIV-CONS-006, PRIV-CONS-006a).
/// </summary>
[Trait("kind", "integration")]
public sealed class LegalDocumentStoreTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// PRIV-CONS-005 AC1: a version comes back carrying the one language it binds in
    /// and the text that binds.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_CONS_005_AC1_AVersionComesBackWithItsGoverningLanguageAsync()
    {
        string notice = Fresh();

        await WritingAsync(async store => await store.AddAsync(
            new DocumentVersion(notice, "1", "ar", "النص", [], Noon),
            TestContext.Current.CancellationToken));

        await using StoreContext reading = database.Context();

        DocumentVersion held = await new LegalDocumentStore(reading)
            .FindAsync(notice, "1", TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException("The version was not written.");

        Assert.Equal("ar", held.GoverningLanguage);
        Assert.Equal("النص", held.Text);
        Assert.Empty(held.Translations);
    }

    /// <summary>
    /// PRIV-CONS-005 AC2, PRIV-CONS-006 AC1: a translation attaches to a published
    /// version and the table still holds one version of that document.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_CONS_006_AC1_ATranslationAttachesToTheVersionItCorrectsAsync()
    {
        string notice = Fresh();

        await WritingAsync(async store => await store.AddAsync(
            new DocumentVersion(notice, "1", "ar", "النص", [], Noon),
            TestContext.Current.CancellationToken));
        await WritingAsync(async store => await store.TranslateAsync(
            notice,
            "1",
            new DocumentTranslation("en", "The text"),
            TestContext.Current.CancellationToken));
        await WritingAsync(async store => await store.TranslateAsync(
            notice,
            "1",
            new DocumentTranslation("en", "The corrected text"),
            TestContext.Current.CancellationToken));

        await using StoreContext reading = database.Context();
        var store = new LegalDocumentStore(reading);

        Assert.Equal(1, await store.CountAsync(notice, TestContext.Current.CancellationToken));

        DocumentVersion held = await store.CurrentAsync(notice, TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException("The version was not written.");

        Assert.Equal("1", held.Version);
        Assert.Equal("The corrected text", Assert.Single(held.Translations).Text);
    }

    /// <summary>
    /// PRIV-CONS-006 AC1: the current version of a document is the last published,
    /// and an earlier one still reads as it read.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_CONS_006_AC1_TheCurrentVersionIsTheLastPublishedAsync()
    {
        string notice = Fresh();

        await WritingAsync(async store => await store.AddAsync(
            new DocumentVersion(notice, "1", "ar", "النص الأول", [], Noon),
            TestContext.Current.CancellationToken));
        await WritingAsync(async store => await store.AddAsync(
            new DocumentVersion(
                notice,
                "2",
                "ar",
                "النص الثاني",
                [new DocumentTranslation("en", "The text")],
                Noon.AddDays(30)),
            TestContext.Current.CancellationToken));

        await using StoreContext reading = database.Context();
        var store = new LegalDocumentStore(reading);

        DocumentVersion current = await store.CurrentAsync(notice, TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException("The version was not written.");
        DocumentVersion first = await store.FindAsync(notice, "1", TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException("The version was not written.");

        Assert.Equal("2", current.Version);
        Assert.Equal(["en"], current.Translations.Select(translation => translation.Language));
        Assert.Equal("النص الأول", first.Text);
        Assert.Empty(first.Translations);
    }

    /// <summary>
    /// PRIV-CONS-006a AC1 (D-203): the cookies a version lists are kept with it and read
    /// back with it by name, each with its purpose, lifetime and readability by a third
    /// party, translated or not; a version listing none reads with none.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_CONS_006a_AC1_AVersionKeepsTheCookiesItListsAsync()
    {
        string notice = Fresh();
        NoticeCookie session = new("__Host-identity-session", null, TimeSpan.FromDays(365), false);
        NoticeCookie preauthentication = new("__Host-identity-preauth", null, null, false);
        NoticeCookie picks = new("host-picks", "recommendations", TimeSpan.FromDays(90), true);

        await WritingAsync(async store => await store.AddAsync(
            new DocumentVersion(notice, "1", "ar", "النص الأول", [], Noon),
            TestContext.Current.CancellationToken));
        await WritingAsync(async store => await store.AddAsync(
            new DocumentVersion(
                notice,
                "2",
                "ar",
                "النص الثاني",
                [new DocumentTranslation("en", "The text"), new DocumentTranslation("fr", "Le texte")],
                Noon.AddDays(30))
            {
                Cookies = [picks, session, preauthentication],
            },
            TestContext.Current.CancellationToken));

        await using StoreContext reading = database.Context();
        var store = new LegalDocumentStore(reading);

        DocumentVersion current = await store.CurrentAsync(notice, TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException("The version was not written.");
        DocumentVersion first = await store.FindAsync(notice, "1", TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException("The version was not written.");

        Assert.Equal([preauthentication, session, picks], current.Cookies);
        Assert.Equal(["en", "fr"], current.Translations.Select(translation => translation.Language));
        Assert.Empty(first.Cookies);
    }

    /// <summary>
    /// PRIV-CONS-006: a translation names a version, so one that was never published
    /// takes none.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_CONS_006_AC1_AnUnpublishedVersionTakesNoTranslationAsync()
    {
        string notice = Fresh();

        await using StoreContext writing = database.Context();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await new LegalDocumentStore(writing).TranslateAsync(
                notice,
                "1",
                new DocumentTranslation("en", "The text"),
                TestContext.Current.CancellationToken));
    }

    // The tests of a class share one database, so each names its own document.
    private static string Fresh() => "notice-" + Guid.NewGuid().ToString("N");

    private async Task WritingAsync(Func<LegalDocumentStore, Task> write)
    {
        await using StoreContext writing = database.Context();

        await write(new LegalDocumentStore(writing));
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
