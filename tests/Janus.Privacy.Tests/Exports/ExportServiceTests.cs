using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Privacy.Consents;
using Janus.Privacy.Exports;
using Janus.Privacy.Outbox;
using Janus.Privacy.Tests.Consents;
using Janus.Privacy.Tests.Outbox;
using Xunit;

namespace Janus.Privacy.Tests.Exports;

/// <summary>
/// The one export routine: what it asks of the session before it assembles anything,
/// what it counts against the rate limit, and what it puts on the outbox
/// (PRIV-RIGHT-003).
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
    private readonly OutboxStoreInMemory _outbox = new();
    private readonly PrivacyAuditInMemory _audit = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _work.DisposeAsync();

    private ExportService Exports => new(
        _source,
        _ledger,
        _consents,
        _stepUp,
        _outbox,
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

        await _consents.RecordAsync(
            Ahmed,
            new ConsentRecord(
                "recommendations",
                "2026-09-01",
                ConsentMechanism.Dashboard,
                ConsentKind.Ordinary,
                Noon,
                WithdrawnAt: null,
                SupersededAt: null),
            TestContext.Current.CancellationToken);

        await _consents.RecordAsync(
            Ahmed,
            new ObjectionRecord(
                "marketing",
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
    /// PRIV-RIGHT-005b: the host holds the half the library cannot produce, so every
    /// assembled export tells the subscribers to produce it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_003_EveryAssembledExportTellsTheSubscribersAsync()
    {
        _ = Assembled(await Exports.AssembleAsync(
            AccessContext.Of(Ahmed),
            Browser,
            TestContext.Current.CancellationToken));

        Delivery raised = Assert.Single(_outbox.Deliveries);

        Assert.Equal(Ahmed, raised.Subject);
        Assert.Equal(SubjectEventKind.ExportRequested, raised.Kind);
        Assert.Equal(Noon, raised.RaisedAt);

        Assert.Equal((Ahmed, Noon), Assert.Single(_ledger.Taken));
        Assert.Equal(1, _work.Committed);
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

    private static SubjectExport Assembled(Result<SubjectExport> outcome) =>
        outcome.Match(
            export => export,
            error => throw new InvalidOperationException(error.Code.ToString()));

    private static Error Refused(Result<SubjectExport> outcome) =>
        outcome.Match(
            _ => throw new InvalidOperationException("The export was assembled."),
            error => error);
}
