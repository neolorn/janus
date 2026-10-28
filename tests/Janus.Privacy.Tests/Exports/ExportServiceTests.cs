using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Privacy.Consents;
using Janus.Privacy.Exports;
using Janus.Privacy.Tests.Consents;
using Xunit;

namespace Janus.Privacy.Tests.Exports;

/// <summary>
/// The one export routine: what it asks of the session before it assembles anything,
/// and what it counts against the rate limit (PRIV-RIGHT-003).
/// </summary>
[Trait("kind", "unit")]
public sealed class ExportServiceTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

    private static readonly SubjectId Ahmed =
        new(Guid.Parse("11111111-1111-4111-8111-111111111111"));

    private static readonly SessionId Browser =
        new(Guid.Parse("33333333-3333-4333-8333-333333333333"));

    private readonly ExportSourceInMemory _source = new();
    private readonly ExportLedgerInMemory _ledger = new();
    private readonly ConsentStoreInMemory _consents = new();
    private readonly StepUpGateInMemory _stepUp = new();
    private readonly PrivacyAuditInMemory _audit = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly List<IExportContributor> _contributors = [];
    private readonly ExportNameRuleInMemory _names = new();
    private readonly FaultEntryInMemory _faults = new();

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _work.DisposeAsync();

    private ExportService Exports => new(
        _source,
        _contributors,
        _names,
        _faults,
        _ledger,
        _consents,
        _stepUp,
        _audit,
        _configuration,
        _work,
        _clock);

    /// <summary>
    /// PRIV-RIGHT-003: the export is gated at the account's own assurance, so the
    /// gate is asked for <c>privacy:export</c> before anything is read.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_003_TheGateIsAskedBeforeAnythingIsReadAsync()
    {
        _stepUp.Closed = Error.From(ErrorCodes.StepUpRequired);

        Assert.Equal(
            ErrorCodes.StepUpRequired,
            Refused(await Exports.AssembleAsync(
                AccessContext.Of(Ahmed),
                Browser,
                TestContext.Current.CancellationToken)).Code);

        Assert.Equal(
            (Ahmed, Browser, StepUpAction.PrivacyExport),
            Assert.Single(_stepUp.Asked));

        Assert.Equal(0, _source.Reads);
        Assert.Empty(_ledger.Taken);
    }

    /// <summary>
    /// PRIV-RIGHT-003: what the areas hold and what the subject has decided are one
    /// assembly, so both reach the export in one read.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_003_TheAreasAndTheDecisionsReachTheExportTogetherAsync()
    {
        _source.Holds(Ahmed, new ExportSection(
            "identifiers",
            [new ExportRecord(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["kind"] = "Email",
                ["value"] = "ahmed@example.test",
            })]));

        _consents.Keep(
            Ahmed,
            new ConsentRecord(
                "recommendations",
                "privacy-notice",
                "2026-09-01",
                ConsentMechanism.Dashboard,
                ConsentKind.Ordinary,
                Noon,
                WithdrawnAt: null,
                SupersededAt: null));

        _ = await _consents.AddAsync(
            Ahmed,
            new ObjectionRecord(
                "marketing",
                "privacy-notice",
                "2026-09-01",
                ConsentMechanism.Dashboard,
                Noon,
                WithdrawnAt: null),
            TestContext.Current.CancellationToken);

        SubjectExport export = Assembled(await Exports.AssembleAsync(
            AccessContext.Of(Ahmed),
            Browser,
            TestContext.Current.CancellationToken));

        Assert.Equal(Ahmed, export.Subject);
        Assert.Equal(Noon, export.AssembledAt);
        Assert.Equal(
            ["identifiers", "consents", "objections"],
            export.Sections.Select(section => section.Name));

        Assert.Equal(
            "ahmed@example.test",
            Assert.Single(export.Sections[0].Records).Values["value"]);

        Assert.Equal(
            "recommendations",
            Assert.Single(export.Sections[1].Records).Values["purpose"]);

        Assert.Equal(
            "marketing",
            Assert.Single(export.Sections[2].Records).Values["purpose"]);
    }

    /// <summary>
    /// D-086: the rate limit is what stands between a borrowed session and a complete
    /// copy of a person's data, so the export after the last one allowed is refused.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_003_TheExportAfterTheLastOneAllowedIsRefusedAsync()
    {
        _configuration.Set(Settings.PrivacyExportRateLimit, 2);

        for (int taken = 0; taken < 2; taken++)
        {
            _ = Assembled(await Exports.AssembleAsync(
                AccessContext.Of(Ahmed),
                Browser,
                TestContext.Current.CancellationToken));

            _clock.Advance(TimeSpan.FromHours(1));
        }

        Error refused = Refused(await Exports.AssembleAsync(
            AccessContext.Of(Ahmed),
            Browser,
            TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.Throttled, refused.Code);
        Assert.Equal(
            Noon + ExportService.Window,
            refused.Details["retryAt"].Deserialize<DateTimeOffset>());

        Assert.Equal(2, _ledger.Taken.Count);
    }

    /// <summary>
    /// D-086, CONV-DESIGN-003: an export counted while this one waited for the
    /// subject's exports is counted, so the export past the limit is refused and tells
    /// nobody.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_003_AnExportCountedMeanwhileIsCountedAsync()
    {
        _configuration.Set(Settings.PrivacyExportRateLimit, 1);

        _ledger.Holding = () =>
            _ = _ledger.RecordAsync(Ahmed, Noon, TestContext.Current.CancellationToken).AsTask();

        Error refused = Refused(await Exports.AssembleAsync(
            AccessContext.Of(Ahmed),
            Browser,
            TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.Throttled, refused.Code);
        Assert.Single(_ledger.Taken);
        Assert.False(_work.Open);
        Assert.Equal(0, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
    }

    /// <summary>
    /// D-086: the window rolls, so an export that has fallen out of it no longer
    /// counts, and the instant the refusal named is the instant it falls out.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_003_AnExportThatHasFallenOutOfTheWindowNoLongerCountsAsync()
    {
        _configuration.Set(Settings.PrivacyExportRateLimit, 1);

        _ = Assembled(await Exports.AssembleAsync(
            AccessContext.Of(Ahmed),
            Browser,
            TestContext.Current.CancellationToken));

        _clock.Advance(ExportService.Window - TimeSpan.FromSeconds(1));

        Error refused = Refused(await Exports.AssembleAsync(
            AccessContext.Of(Ahmed),
            Browser,
            TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.Throttled, refused.Code);

        _clock.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(
            Noon + ExportService.Window,
            refused.Details["retryAt"].Deserialize<DateTimeOffset>());

        _ = Assembled(await Exports.AssembleAsync(
            AccessContext.Of(Ahmed),
            Browser,
            TestContext.Current.CancellationToken));

        Assert.Equal(2, _ledger.Taken.Count);
    }

    /// <summary>
    /// D-086: the limit is one account's, so one account spending it leaves another
    /// account's untouched.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_003_OneAccountSpendingTheLimitLeavesAnothersUntouchedAsync()
    {
        var noura = new SubjectId(Guid.Parse("22222222-2222-4222-8222-222222222222"));

        _configuration.Set(Settings.PrivacyExportRateLimit, 1);

        _ = Assembled(await Exports.AssembleAsync(
            AccessContext.Of(Ahmed),
            Browser,
            TestContext.Current.CancellationToken));

        _ = Assembled(await Exports.AssembleAsync(
            AccessContext.Of(noura),
            Browser,
            TestContext.Current.CancellationToken));

        Assert.Equal(
            ErrorCodes.Throttled,
            Refused(await Exports.AssembleAsync(
                AccessContext.Of(Ahmed),
                Browser,
                TestContext.Current.CancellationToken)).Code);
    }

    /// <summary>
    /// PRIV-RET-004: the audit entry says what was assembled and never what was in
    /// it, so nothing of the person's own values reaches the trail.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_003_TheAuditEntrySaysWhatWasAssembledAndNotWhatWasInItAsync()
    {
        _source.Holds(Ahmed, new ExportSection(
            "profile",
            [new ExportRecord(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["legalName"] = "Ahmed Mostafa",
            })]));

        _ = Assembled(await Exports.AssembleAsync(
            AccessContext.Of(Ahmed),
            Browser,
            TestContext.Current.CancellationToken));

        PrivacyAuditEntry entry = Assert.Single(_audit.Entries);

        Assert.Equal("privacy.export.assembled", entry.Action.ToString());
        Assert.Equal(Ahmed, entry.Subject);
        Assert.Equal(
            ["profile", "consents", "objections"],
            entry.Details["sections"].Deserialize<string[]>() ?? []);
        Assert.DoesNotContain("Ahmed Mostafa", JsonSerializer.Serialize(entry.Details), StringComparison.Ordinal);
    }

    /// <summary>
    /// PRIV-RIGHT-003: an export is the subject's own, so a context carrying nobody
    /// gets nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_003_AContextCarryingNobodyGetsNothingAsync() =>
        Assert.Equal(
            ErrorCodes.Denied,
            Refused(await Exports.AssembleAsync(
                AccessContext.Of(SystemPrincipal.ForDeployment(
                    "records",
                    "Generating the records of processing.",
                    SystemOperation.RecordsOfProcessing)),
                Browser,
                TestContext.Current.CancellationToken)).Code);

    /// <summary>
    /// PRIV-RIGHT-003 AC5: each contributor's sections follow the library's, in the order
    /// of the contributors' names whatever order they were registered in, and every
    /// contributor is asked before the unit of work that counts the export begins.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_003_AC5_EachContributorsSectionsFollowTheLibrarysInNameOrderAsync()
    {
        var open = new List<bool>();
        ExportContributorInMemory tickets = Contributor("tickets", "support-tickets", "ticket-notes");
        ExportContributorInMemory orders = Contributor("orders", "orders");

        tickets.Meanwhile = () => open.Add(_work.Open);
        orders.Meanwhile = () => open.Add(_work.Open);
        _contributors.Add(tickets);
        _contributors.Add(orders);

        SubjectExport export = Assembled(await Exports.AssembleAsync(
            AccessContext.Of(Ahmed),
            Browser,
            TestContext.Current.CancellationToken));

        Assert.Equal(
            ["consents", "objections", "orders", "support-tickets", "ticket-notes"],
            export.Sections.Select(section => section.Name));
        Assert.Equal("orders", Assert.Single(export.Sections[2].Records).Values["held-in"]);
        Assert.Equal([Ahmed], orders.Asked);
        Assert.Equal([Ahmed], tickets.Asked);
        Assert.Equal([false, false], open);
        Assert.Equal((Ahmed, Noon), Assert.Single(_ledger.Taken));
    }

    /// <summary>
    /// PRIV-RIGHT-003 AC5: a contributor that answers a failure fails the export: nothing
    /// is returned, the refusal is a fault naming the contributor, no unit of work
    /// begins and the export is not counted.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_003_AC5_AFailingContributorFailsTheExportUncountedAsync()
    {
        ExportContributorInMemory orders = Contributor("orders", "orders");

        orders.Fails = Error.From(ErrorCodes.Denied);
        _contributors.Add(orders);
        _contributors.Add(Contributor("tickets", "support-tickets"));

        AssertFaulted(
            Refused(await Exports.AssembleAsync(
                AccessContext.Of(Ahmed),
                Browser,
                TestContext.Current.CancellationToken)),
            "orders");
    }

    /// <summary>
    /// PRIV-RIGHT-003 AC5: a contributor that throws fails the export as one that
    /// answered a failure does, and the person may ask again, since nothing was counted.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_003_AC5_AFaultingContributorFailsTheExportUncountedAsync()
    {
        _configuration.Set(Settings.PrivacyExportRateLimit, 1);

        ExportContributorInMemory tickets = Contributor("tickets", "support-tickets");

        tickets.Faults = true;
        _contributors.Add(tickets);

        AssertFaulted(
            Refused(await Exports.AssembleAsync(
                AccessContext.Of(Ahmed),
                Browser,
                TestContext.Current.CancellationToken)),
            "tickets");

        tickets.Faults = false;

        _ = Assembled(await Exports.AssembleAsync(
            AccessContext.Of(Ahmed),
            Browser,
            TestContext.Current.CancellationToken));

        Assert.Single(_ledger.Taken);
    }

    /// <summary>
    /// PRIV-RIGHT-003 AC5 (D-210, D-211): what a contributor threw is kept by the fault
    /// entry, once and naming the contributor, and the error the export answers names
    /// the contributor and holds no fault; a contributor that answered a failure threw
    /// nothing, and nothing is kept.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_003_AC5_WhatAContributorThrewIsKeptByTheFaultEntryAndNotInTheErrorAsync()
    {
        ExportContributorInMemory orders = Contributor("orders", "orders");
        ExportContributorInMemory tickets = Contributor("tickets", "support-tickets");

        orders.Fails = Error.From(ErrorCodes.Denied);
        _contributors.Add(orders);

        AssertFaulted(
            Refused(await Exports.AssembleAsync(
                AccessContext.Of(Ahmed),
                Browser,
                TestContext.Current.CancellationToken)),
            "orders");
        Assert.Empty(_faults.Kept);

        orders.Fails = null;
        tickets.Faults = true;
        _contributors.Add(tickets);

        AssertFaulted(
            Refused(await Exports.AssembleAsync(
                AccessContext.Of(Ahmed),
                Browser,
                TestContext.Current.CancellationToken)),
            "tickets");

        (string contributor, Exception fault) = Assert.Single(_faults.Kept);

        Assert.Equal("tickets", contributor);
        Assert.IsType<InvalidOperationException>(fault);
    }

    /// <summary>
    /// PRIV-RIGHT-003 (D-203): a section named as a library section, or as another
    /// contributor's, is a fault of the contributor that answered it second, and fails
    /// the export uncounted.
    /// </summary>
    /// <param name="taken">The name the second contributor answers a section under.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("consents")]
    [InlineData("identifiers")]
    [InlineData("orders")]
    public async Task PRIV_RIGHT_003_AC5_ASectionUnderANameAlreadyTakenFailsTheExportAsync(string taken)
    {
        _source.Holds(Ahmed, new ExportSection("identifiers", []));
        _contributors.Add(Contributor("orders", "orders"));
        _contributors.Add(Contributor("tickets", taken));

        AssertFaulted(
            Refused(await Exports.AssembleAsync(
                AccessContext.Of(Ahmed),
                Browser,
                TestContext.Current.CancellationToken)),
            "tickets");
    }

    /// <summary>
    /// PRIV-RIGHT-003 AC7 (D-206): a section's name is distinct from every other section
    /// of the export, so a contributor answering two sections under one name is a fault
    /// as one answering a library section's name is, and fails the export uncounted.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_003_AC7_TwoSectionsOfOneAnswerUnderOneNameFailTheExportAsync()
    {
        _contributors.Add(Contributor("orders", "orders"));
        _contributors.Add(Contributor("tickets", "support-tickets", "support-tickets"));

        AssertFaulted(
            Refused(await Exports.AssembleAsync(
                AccessContext.Of(Ahmed),
                Browser,
                TestContext.Current.CancellationToken)),
            "tickets");
    }

    /// <summary>
    /// PRIV-RIGHT-003 AC7 (D-206): the assembly asks the rule of every section and value
    /// name a contributor answers, and of none of the library's, before it takes the
    /// answer: every name is asked before the unit of work that counts the export begins.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_003_AC7_EveryNameAContributorAnswersIsJudgedBeforeItIsTakenAsync()
    {
        var open = new List<bool>();

        _source.Holds(Ahmed, new ExportSection(
            "identifiers",
            [new ExportRecord(new Dictionary<string, string>(StringComparer.Ordinal) { ["kind"] = "Email" })]));
        _contributors.Add(Contributor("tickets", "support-tickets", "ticket-notes"));
        _contributors.Add(Contributor("orders", "orders"));
        _names.Meanwhile = () => open.Add(_work.Open);

        SubjectExport export = Assembled(await Exports.AssembleAsync(
            AccessContext.Of(Ahmed),
            Browser,
            TestContext.Current.CancellationToken));

        Assert.Equal(
            ["orders", "held-in", "support-tickets", "held-in", "ticket-notes", "held-in"],
            _names.Asked);
        Assert.Equal([false, false, false, false, false, false], open);
        Assert.Equal(6, export.Sections.Count);
    }

    /// <summary>
    /// PRIV-RIGHT-003 AC7 (D-206): a section or a value named outside the rule is a fault
    /// of the contributor that answered it, failing the export uncounted as one that
    /// faults does, whatever the contributors before it answered.
    /// </summary>
    /// <param name="refused">The name the rule refuses.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("ticket-notes")]
    [InlineData("ticket-number")]
    public async Task PRIV_RIGHT_003_AC7_ANameOutsideTheRuleFailsTheExportAsync(string refused)
    {
        _contributors.Add(Contributor("orders", "orders"));
        _contributors.Add(new ExportContributorInMemory("tickets")
        {
            Sections =
            [
                new ExportSection(
                    "ticket-notes",
                    [new ExportRecord(new Dictionary<string, string>(StringComparer.Ordinal) { ["ticket-number"] = "t-1" })]),
            ],
        });
        _names.Refused.Add(refused);

        AssertFaulted(
            Refused(await Exports.AssembleAsync(
                AccessContext.Of(Ahmed),
                Browser,
                TestContext.Current.CancellationToken)),
            "tickets");
    }

    /// <summary>
    /// PRIV-RIGHT-003 AC5: of exports asked at once past the limit, the one counted while
    /// this one's contributors answered is counted against it, so this one is refused as
    /// it would be had it been asked after, and returns nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_003_AC5_AnExportCountedWhileTheContributorsAnsweredIsCountedAsync()
    {
        _configuration.Set(Settings.PrivacyExportRateLimit, 1);

        ExportContributorInMemory orders = Contributor("orders", "orders");

        orders.Meanwhile = () =>
            _ = _ledger.RecordAsync(Ahmed, Noon, TestContext.Current.CancellationToken).AsTask();
        _contributors.Add(orders);

        Error refused = Refused(await Exports.AssembleAsync(
            AccessContext.Of(Ahmed),
            Browser,
            TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.Throttled, refused.Code);
        Assert.Equal(
            Noon + ExportService.Window,
            refused.Details["retryAt"].Deserialize<DateTimeOffset>());
        Assert.Single(_ledger.Taken);
        Assert.Equal(0, _work.Committed);
        Assert.False(_work.Open);
    }

    private static SubjectExport Assembled(Result<SubjectExport> outcome) =>
        outcome.Match(
            export => export,
            error => throw new InvalidOperationException(error.Code.ToString()));

    /// <summary>
    /// REC-ACC-002: a person record's export is PRIV-RIGHT-003's routine for the record,
    /// its own sections then each contributor's, counted for the record and asking no
    /// step-up, since the code that reached it is fresh.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ACC_002_ARecordsExportAsksNoStepUpAndIsCountedForTheRecordAsync()
    {
        _source.Holds(Ahmed, new ExportSection("record", []));
        _contributors.Add(Contributor("orders", "orders"));

        SubjectExport export = Assembled(await Exports.AssembleRecordAsync(Ahmed, TestContext.Current.CancellationToken));

        Assert.Equal(
            ["record", "consents", "objections", "orders"],
            export.Sections.Select(section => section.Name));
        Assert.Empty(_stepUp.Asked);
        Assert.Equal((Ahmed, Noon), Assert.Single(_ledger.Taken));
    }

    /// <summary>
    /// REC-ACC-002 (chapter 10 section 5.46, D-221): a contributor's section for a person
    /// record under a name of an account's export, or under <c>record</c> or
    /// <c>records</c>, fails the record's export uncounted.
    /// </summary>
    /// <param name="taken">The name the contributor answers a section under.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("record")]
    [InlineData("records")]
    [InlineData("credentials")]
    public async Task REC_ACC_002_ARecordsExportRefusesAContributorTakingALibraryNameAsync(string taken)
    {
        _source.Holds(Ahmed, new ExportSection("record", []));
        _contributors.Add(Contributor("tickets", taken));

        AssertFaulted(
            Refused(await Exports.AssembleRecordAsync(Ahmed, TestContext.Current.CancellationToken)),
            "tickets");
    }

    private static ExportContributorInMemory Contributor(string name, params string[] sections) =>
        new(name)
        {
            Sections =
            [
                .. sections.Select(section => new ExportSection(
                    section,
                    [new ExportRecord(new Dictionary<string, string>(StringComparer.Ordinal) { ["held-in"] = section })])),
            ],
        };

    private static Error Refused(Result<SubjectExport> outcome) =>
        outcome.Match(
            _ => throw new InvalidOperationException("The export was assembled."),
            error => error);

    private void AssertFaulted(Error refused, string contributor)
    {
        Assert.Equal(ErrorCodes.SystemFault, refused.Code);
        Assert.Equal("contributor", Assert.Single(refused.Details).Key);
        Assert.Equal(contributor, refused.Details["contributor"].GetString());
        Assert.Empty(_ledger.Taken);
        Assert.Empty(_audit.Entries);
        Assert.Equal(0, _work.Opened);
    }
}
