using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Privacy.Consents;
using Janus.Privacy.Documents;
using Janus.Privacy.Policies;
using Janus.Privacy.Tests.Consents;
using Janus.Privacy.Tests.Outbox;
using Xunit;

namespace Janus.Privacy.Tests.Documents;

/// <summary>
/// The legal documents a deployment publishes: one governing language a version, the
/// translations attached to it, and what a publication without governing text does.
/// </summary>
[Trait("kind", "unit")]
public sealed class LegalDocumentTests : IAsyncDisposable
{
    private static readonly DocumentName Notice = DocumentName.Parse("privacy-notice");

    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private static readonly SubjectId Officer =
        new(Guid.Parse("22222222-2222-4222-8222-222222222222"));

    private static readonly OrganizationId Deployment =
        new(Guid.Parse("33333333-3333-4333-8333-333333333333"));

    private readonly AccessGateInMemory _gate = new();
    private readonly AdministrativeOrganizationInMemory _administrative = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly LegalDocumentStoreInMemory _store = new();
    private readonly PrivacyAuditInMemory _audit = new();
    private readonly PrivacyAlertsInMemory _alerts = new();
    private readonly DeploymentCookiesInMemory _cookies = new();
    private readonly ConsentStoreInMemory _consents = new();
    private readonly EventsInMemory _events = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);

    /// <summary>
    /// An officer of one organization, and a deployment whose governing language is
    /// Arabic.
    /// </summary>
    public LegalDocumentTests()
    {
        _administrative.Organization = Deployment;
        _configuration.Set(Settings.LegalGoverningLanguage, "ar");
    }

    private LegalDocumentService Documents =>
        new LegalDocumentService(
            _store,
            new AdministrativeScope(_gate, _administrative),
            new Supersession(_consents, Declaration.Processing, _events, new OutboxStoreInMemory()),
            _configuration,
            _audit,
            _alerts,
            _cookies,
            _work,
            _clock);

    private static AccessContext Acting => AccessContext.Of(Officer);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _work.DisposeAsync();

    /// <summary>
    /// PRIV-CONS-005 AC1: a version published without naming a governing language
    /// takes the one the deployment configured.
    /// </summary>
    [Fact]
    public async Task PRIV_CONS_005_AC1_AVersionWithoutAGoverningLanguageTakesTheConfiguredOneAsync()
    {
        Permit();

        DocumentVersion published = await PublishAsync(
            new DocumentPublication(Notice, "النص", GoverningLanguage: null, [], Material: false));

        Assert.Equal("ar", published.GoverningLanguage);
    }

    /// <summary>
    /// PRIV-CONS-005 AC1: a version that names a governing language keeps it, and it
    /// is the one language that version binds in however many translations it carries.
    /// </summary>
    [Fact]
    public async Task PRIV_CONS_005_AC1_AVersionCarriesExactlyOneGoverningLanguageAsync()
    {
        Permit();

        DocumentVersion published = await PublishAsync(
            new DocumentPublication(
                Notice,
                "The text",
                "en",
                [new DocumentTranslation("ar", "النص")],
                Material: false));

        Assert.Equal("en", published.GoverningLanguage);
        Assert.Equal("The text", published.Text);
        Assert.Equal(["ar"], published.Translations.Select(translation => translation.Language));
    }

    /// <summary>
    /// PRIV-CONS-005 AC2: a version publishes with no translation attached.
    /// </summary>
    [Fact]
    public async Task PRIV_CONS_005_AC2_AVersionPublishesWithNoTranslationAttachedAsync()
    {
        Permit();

        DocumentVersion published = await PublishAsync(
            new DocumentPublication(Notice, "النص", "ar", [], Material: false));

        Assert.Empty(published.Translations);
    }

    /// <summary>
    /// PRIV-CONS-005 AC2: a translation attaches to a published version without
    /// creating another.
    /// </summary>
    [Fact]
    public async Task PRIV_CONS_005_AC2_ATranslationAttachesWithoutCreatingAVersionAsync()
    {
        Permit();

        DocumentVersion published = await PublishAsync(
            new DocumentPublication(Notice, "النص", "ar", [], Material: false));

        Result attached = await Documents.TranslateAsync(
            Acting,
            Notice,
            published.Version,
            new DocumentTranslation("en", "The text"),
            CancellationToken.None);

        Assert.Null(attached.Match(() => (Error?)null, error => error));
        Assert.Single(_store.Versions);

        DocumentVersion current = await CurrentAsync();

        Assert.Equal(published.Version, current.Version);
        Assert.Equal(["en"], current.Translations.Select(translation => translation.Language));
    }

    /// <summary>
    /// PRIV-CONS-006 AC1: a correction to an attached translation replaces it and
    /// still creates no version.
    /// </summary>
    [Fact]
    public async Task PRIV_CONS_006_AC1_CorrectingATranslationCreatesNoVersionAsync()
    {
        Permit();

        DocumentVersion published = await PublishAsync(
            new DocumentPublication(
                Notice,
                "النص",
                "ar",
                [new DocumentTranslation("en", "The first text")],
                Material: false));

        await Documents.TranslateAsync(
            Acting,
            Notice,
            published.Version,
            new DocumentTranslation("en", "The corrected text"),
            CancellationToken.None);

        DocumentVersion current = await CurrentAsync();

        Assert.Equal(published.Version, current.Version);
        Assert.Equal("The corrected text", Assert.Single(current.Translations).Text);
    }

    /// <summary>
    /// PRIV-CONS-006 AC1: changing the governing text creates a new version, and the
    /// version shown before still resolves to the text it carried.
    /// </summary>
    [Fact]
    public async Task PRIV_CONS_006_AC1_ChangingTheGoverningTextCreatesANewVersionAsync()
    {
        Permit();

        DocumentVersion first = await PublishAsync(
            new DocumentPublication(Notice, "النص الأول", "ar", [], Material: false));
        DocumentVersion second = await PublishAsync(
            new DocumentPublication(Notice, "النص الثاني", "ar", [], Material: true));

        Assert.NotEqual(first.Version, second.Version);

        DocumentVersion held = Read(await Documents.ReadAsync(
            Notice,
            first.Version,
            CancellationToken.None));

        Assert.Equal("النص الأول", held.Text);
    }

    /// <summary>
    /// PRIV-CONS-006 AC3: publishing a version without governing-language text is
    /// refused, and the condition is raised.
    /// </summary>
    [Fact]
    public async Task PRIV_CONS_006_AC3_AVersionWithoutGoverningTextIsRefusedAndRaisedAsync()
    {
        Permit();

        Result<DocumentVersion> refused = await Documents.PublishAsync(
            Acting,
            new DocumentPublication(
                Notice,
                string.Empty,
                "ar",
                [new DocumentTranslation("en", "The text")],
                Material: false),
            CancellationToken.None);

        Assert.Equal(
            ErrorCodes.NoticeGoverningTextMissing,
            refused.Match(_ => default, error => error.Code));
        Assert.Empty(_store.Versions);
        Assert.Equal(AlertCondition.GoverningTextMissing, Assert.Single(_alerts.Raised).Condition);
    }

    /// <summary>
    /// CONV-DESIGN-002, PRIV-CONS-006 AC3: a condition whose row cannot be written is
    /// what the publication is answered with, so no refusal claims a raise that did
    /// not happen.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_002_ARaiseThatCannotBeWrittenIsTheAnswerAsync()
    {
        Permit();

        _alerts.Refusal = Error.From(ErrorCodes.SystemFault);

        Result<DocumentVersion> refused = await Documents.PublishAsync(
            Acting,
            new DocumentPublication(
                Notice,
                string.Empty,
                "ar",
                [new DocumentTranslation("en", "The text")],
                Material: false),
            CancellationToken.None);

        Assert.Equal(ErrorCodes.SystemFault, refused.Match(_ => default, error => error.Code));
        Assert.Empty(_store.Versions);
    }

    /// <summary>
    /// PRIV-CONS-005 AC3: the governing text and every attached translation come back
    /// together, from a read that is told no interface language at all.
    /// </summary>
    [Fact]
    public async Task PRIV_CONS_005_AC3_TheGoverningTextAndItsTranslationsComeBackTogetherAsync()
    {
        Permit();

        await PublishAsync(
            new DocumentPublication(
                Notice,
                "النص",
                "ar",
                [new DocumentTranslation("en", "The text"), new DocumentTranslation("fr", "Le texte")],
                Material: false));

        DocumentVersion current = await CurrentAsync();

        Assert.Equal("ar", current.GoverningLanguage);
        Assert.Equal("النص", current.Text);
        Assert.Equal(
            ["en", "fr"],
            current.Translations.Select(translation => translation.Language).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// PRIV-CONS-005 AC1: a document the deployment never published refuses rather
    /// than answering with a version carrying no governing language.
    /// </summary>
    [Fact]
    public async Task PRIV_CONS_005_AC1_AnUnpublishedDocumentIsRefusedAsync()
    {
        Result<DocumentVersion> refused = await Documents.ReadAsync(
            Notice,
            version: null,
            CancellationToken.None);

        Assert.Equal(
            ErrorCodes.DocumentNotFound,
            refused.Match(_ => default, error => error.Code));
    }

    /// <summary>
    /// LIB-API-005 AC2: publishing is gated, so calling the service in process is no
    /// way round the permission the endpoint applies.
    /// </summary>
    [Fact]
    public async Task LIB_API_005_AC2_PublishingWithoutThePermissionIsRefusedAsync()
    {
        Result<DocumentVersion> refused = await Documents.PublishAsync(
            Acting,
            new DocumentPublication(Notice, "النص", "ar", [], Material: false),
            CancellationToken.None);

        Assert.Equal(ErrorCodes.Denied, refused.Match(_ => default, error => error.Code));
        Assert.Empty(_store.Versions);
    }

    /// <summary>
    /// LIB-API-005 AC2: attaching a translation is gated on the same permission as
    /// publishing the version it attaches to, in process as over HTTP.
    /// </summary>
    [Fact]
    public async Task LIB_API_005_AC2_TranslatingWithoutThePermissionIsRefusedAsync()
    {
        await _store.AddAsync(
            new DocumentVersion(Notice.ToString(), "1", "ar", "النص", [], Noon),
            CancellationToken.None);

        Result refused = await Documents.TranslateAsync(
            Acting,
            Notice,
            "1",
            new DocumentTranslation("en", "The text"),
            CancellationToken.None);

        Assert.Equal(ErrorCodes.Denied, refused.Match(() => default, error => error.Code));
        Assert.Empty(Assert.Single(_store.Versions).Translations);
    }

    /// <summary>
    /// PRIV-CONS-007: what the person publishing answered about materiality is what
    /// the audit record carries, because code does not judge materiality.
    /// </summary>
    [Fact]
    public async Task PRIV_CONS_007_AC1_TheAuditRecordCarriesTheAnswerOnMaterialityAsync()
    {
        Permit();

        await PublishAsync(new DocumentPublication(Notice, "النص", "ar", [], Material: true));

        PrivacyAuditEntry recorded = Assert.Single(_audit.Entries);

        Assert.Equal(Officer, recorded.Acting);
        Assert.True(recorded.Details["material"].GetBoolean());
    }

    /// <summary>
    /// CONV-DESIGN-007: a publication writes its version and its audit record in one
    /// transaction, so neither exists without the other.
    /// </summary>
    [Fact]
    public async Task PRIV_CONS_006_AC1_APublicationWritesAndCommitsOnceAsync()
    {
        Permit();

        await PublishAsync(new DocumentPublication(Notice, "النص", "ar", [], Material: false));

        Assert.Equal(1, _work.Opened);
        Assert.Equal(1, _work.Committed);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5: a material publication whose supersession cannot announce a
    /// consent it ended is refused after its unit of work began, and rolls it back.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_APublicationRefusedAfterItBeganRollsBackAsync()
    {
        Permit();

        _consents.Keep(
            Officer,
            new ConsentRecord(
                "recommendations",
                Notice.ToString(),
                "0",
                ConsentMechanism.Dashboard,
                ConsentKind.Ordinary,
                Noon,
                WithdrawnAt: null,
                SupersededAt: null));

        _events.Refusal = Error.From(ErrorCodes.SystemFault);

        Result<DocumentVersion> refused = await Documents.PublishAsync(
            Acting,
            new DocumentPublication(Notice, "النص", "ar", [], Material: true),
            CancellationToken.None);

        Assert.Equal(ErrorCodes.SystemFault, refused.Match(_ => default(ErrorCode?), error => error.Code));
        Assert.False(_work.Open);
        Assert.Equal(0, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
    }

    /// <summary>
    /// PRIV-CONS-006a AC1 (D-203): a version of the privacy notice keeps the cookies the
    /// deployment sets when it is published, and an earlier version keeps those it was
    /// published with whatever the deployment sets since.
    /// </summary>
    [Fact]
    public async Task PRIV_CONS_006a_AC1_ANoticeVersionKeepsTheCookiesSetWhenItWasPublishedAsync()
    {
        Permit();

        NoticeCookie session = new("__Host-identity-session", null, TimeSpan.FromDays(365), false);
        NoticeCookie picks = new("host-picks", "recommendations", TimeSpan.FromDays(90), true);

        _cookies.Set = [session];

        DocumentVersion first = await PublishAsync(
            new DocumentPublication(Notice, "النص الأول", "ar", [], Material: false));

        _cookies.Set = [session with { Lifetime = TimeSpan.FromDays(30) }, picks];

        DocumentVersion second = await PublishAsync(
            new DocumentPublication(Notice, "النص الثاني", "ar", [], Material: false));

        Assert.Equal([session], first.Cookies);
        Assert.Equal([session], Read(await Documents.ReadAsync(Notice, "1", CancellationToken.None)).Cookies);
        Assert.Equal([session with { Lifetime = TimeSpan.FromDays(30) }, picks], second.Cookies);
        Assert.Equal(second.Cookies, (await CurrentAsync()).Cookies);
    }

    /// <summary>
    /// PRIV-CONS-006a (D-203): the cookies belong to the privacy notice, so a version of
    /// another document keeps none and the cookies are not read for it.
    /// </summary>
    [Fact]
    public async Task PRIV_CONS_006a_AVersionOfAnotherDocumentKeepsNoCookiesAsync()
    {
        Permit();

        _cookies.Set = [new NoticeCookie("__Host-identity-preauth", null, null, false)];

        DocumentVersion published = await PublishAsync(
            new DocumentPublication(DocumentName.Parse("terms"), "النص", "ar", [], Material: false));

        Assert.Empty(published.Cookies);
        Assert.Equal(0, _cookies.Reads);
    }

    /// <summary>
    /// PRIV-CONS-006a (D-203): a notice whose cookies cannot be read is not published
    /// without them; nothing is written and no unit of work begins.
    /// </summary>
    [Fact]
    public async Task PRIV_CONS_006a_ANoticeIsNotPublishedWithoutItsCookiesAsync()
    {
        Permit();

        _cookies.Failure = Error.From(ErrorCodes.SystemFault);

        Result<DocumentVersion> refused = await Documents.PublishAsync(
            Acting,
            new DocumentPublication(Notice, "النص", "ar", [], Material: false),
            CancellationToken.None);

        Assert.Equal(
            ErrorCodes.SystemFault,
            refused.Match(_ => (ErrorCode?)null, error => error.Code));
        Assert.Empty(_store.Versions);
        Assert.Equal(0, _work.Opened);
    }

    /// <summary>
    /// CONV-DESIGN-004 AC3 and INT-SMS-003: a caller in process names a document by its
    /// value type alone, and a name that was never read fails where it is first read:
    /// nothing is published, nothing is raised and no unit of work begins.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_004_AC3_ADocumentNameNeverReadPublishesNothingAsync()
    {
        Permit();

        _ = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await Documents.PublishAsync(
                Acting,
                new DocumentPublication(default, "The text", "en", [], Material: false),
                CancellationToken.None));
        _ = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await Documents.PublishAsync(
                Acting,
                new DocumentPublication(default, string.Empty, "en", [], Material: false),
                CancellationToken.None));

        Assert.Empty(_store.Versions);
        Assert.Empty(_alerts.Raised);
        Assert.Equal(0, _work.Opened);
    }

    private static DocumentVersion Read(Result<DocumentVersion> outcome) =>
        outcome.Match(
            version => version,
            error => throw new InvalidOperationException(error.Code.ToString()));

    private void Permit() => _gate.Grant(Officer, Deployment, Permissions.NoticePublish);

    private async Task<DocumentVersion> PublishAsync(DocumentPublication publication) =>
        Read(await Documents.PublishAsync(Acting, publication, CancellationToken.None));

    private async Task<DocumentVersion> CurrentAsync() =>
        Read(await Documents.ReadAsync(Notice, version: null, CancellationToken.None));
}
