using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Authentication.Tests;
using Janus.Authorization.Gate;
using Janus.Authorization.Model;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// What an export operation asks beyond what the grants allow: step-up, a place under
/// the hourly limit, and a row of its own in the audit trail (OPS-ALERT-006, D-045).
/// </summary>
[Trait("kind", "unit")]
public sealed class ExportOperationsTests : IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 24, 9, 0, 0, TimeSpan.Zero);

    private static readonly ResourceType Documents = ResourceType.Parse("document");

    private static readonly AuthorizationModel Model = AuthorizationModel.Of(HostFixture.Declaration());

    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly BulkExportLedgerInMemory _ledger = new();
    private readonly AccessAuditInMemory _audit = new();
    private readonly FixedClock _clock = new(Noon);

    /// <inheritdoc/>
    public void Dispose() => _randomness.Dispose();

    private ExportOperations Exports => new(Model, _ledger, _audit, _configuration, _clock);

    /// <summary>
    /// OPS-ALERT-006 AC1: an export is one of the actions the host declared as one, so
    /// reading a record, however many are read, asks nothing of what an export asks.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_006_AC1_OnlyADeclaredExportIsGatedLimitedAndRecordedAsync()
    {
        var clerk = SubjectId.New(_randomness);

        for (int read = 0; read < 10; read++)
        {
            Assert.Null(Refusal(await AdmitAsync(AccessContext.Of(clerk), HostPermissions.Read)));
        }

        Assert.Null(await Exports.GateOfAsync(HostPermissions.Read, TestContext.Current.CancellationToken));
        Assert.Empty(_ledger.Admitted);
        Assert.Empty(_audit.Exports);

        Assert.Equal(
            HostPermissions.Export.ToString(),
            await Exports.GateOfAsync(HostPermissions.Export, TestContext.Current.CancellationToken));
        Assert.Null(Refusal(await AdmitAsync(AccessContext.Of(clerk), HostPermissions.Export)));
        Assert.Single(_ledger.Admitted);
        Assert.Single(_audit.Exports);
    }

    /// <summary>
    /// OPS-ALERT-006, D-045: an export asks for step-up while the deployment requires
    /// it, which it does unless it says otherwise, and asks for none once it does not.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_006_AnExportAsksForStepUpWhileTheDeploymentRequiresItAsync()
    {
        Assert.Equal(
            HostPermissions.Export.ToString(),
            await Exports.GateOfAsync(HostPermissions.Export, TestContext.Current.CancellationToken));

        _configuration.Set(Settings.ExfiltrationExportStepUpRequired, false);

        Assert.Null(await Exports.GateOfAsync(HostPermissions.Export, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// OPS-ALERT-006, D-045: past the hour's limit an export is refused with the time
    /// the oldest of the hour's exports leaves the window, and admitted again from then.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_006_AnExportPastTheHourlyLimitIsThrottledUntilAPlaceFreesAsync()
    {
        var clerk = SubjectId.New(_randomness);

        for (int export = 0; export < Settings.ExfiltrationExportRateLimit.Default; export++)
        {
            Assert.Null(Refusal(await AdmitAsync(AccessContext.Of(clerk), HostPermissions.Export)));
            _clock.Advance(TimeSpan.FromMinutes(5));
        }

        Error refused = Assert.IsType<Error>(Refusal(await AdmitAsync(AccessContext.Of(clerk), HostPermissions.Export)));

        Assert.Equal(ErrorCodes.Throttled, refused.Code);
        Assert.Equal(Noon.AddHours(1), refused.Details["retryAt"].GetDateTimeOffset());
        Assert.Equal(Settings.ExfiltrationExportRateLimit.Default, _ledger.Admitted.Count);
        Assert.Equal(Settings.ExfiltrationExportRateLimit.Default, _audit.Exports.Count);

        _clock.Advance(Noon.AddHours(1) - _clock.GetUtcNow() + TimeSpan.FromSeconds(1));

        Assert.Null(Refusal(await AdmitAsync(AccessContext.Of(clerk), HostPermissions.Export)));
    }

    /// <summary>
    /// OPS-ALERT-006, D-045: the limit is each actor's own, a system principal's
    /// counted by its name, so one actor's hour spends nothing of another's.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_006_TheLimitIsEachActorsOwnAsync()
    {
        var clerk = SubjectId.New(_randomness);
        var nightly = AccessContext.Of(SystemPrincipal.ForOrganization(
            "nightly-report",
            "the nightly report",
            new OrganizationId(Guid.CreateVersion7())));

        _configuration.Set(Settings.ExfiltrationExportRateLimit, 1);

        Assert.Null(Refusal(await AdmitAsync(AccessContext.Of(clerk), HostPermissions.Export)));
        Assert.Null(Refusal(await AdmitAsync(nightly, HostPermissions.Export)));
        Assert.Null(Refusal(await AdmitAsync(AccessContext.Of(SubjectId.New(_randomness)), HostPermissions.Export)));

        Assert.Equal(
            ErrorCodes.Throttled,
            Refusal(await AdmitAsync(AccessContext.Of(clerk), HostPermissions.Export))?.Code);
        Assert.Equal(
            ErrorCodes.Throttled,
            Refusal(await AdmitAsync(nightly, HostPermissions.Export))?.Code);
    }

    /// <summary>
    /// OPS-ALERT-006, D-045: a deployment that sets the limit to nothing admits no
    /// export, and still says when to try again.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_006_ALimitOfNothingAdmitsNoExportAsync()
    {
        _configuration.Set(Settings.ExfiltrationExportRateLimit, 0);

        Error refused = Assert.IsType<Error>(
            Refusal(await AdmitAsync(AccessContext.Of(SubjectId.New(_randomness)), HostPermissions.Export)));

        Assert.Equal(ErrorCodes.Throttled, refused.Code);
        Assert.Equal(Noon.AddHours(1), refused.Details["retryAt"].GetDateTimeOffset());
        Assert.Empty(_ledger.Admitted);
        Assert.Empty(_audit.Exports);
    }

    /// <summary>
    /// OPS-ALERT-006, D-045: every admitted export is recorded on its own, naming who
    /// exported, what, from where and when.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_006_EachAdmittedExportIsIndividuallyAuditedAsync()
    {
        var clerk = SubjectId.New(_randomness);
        var organization = new OrganizationId(Guid.CreateVersion7());
        var record = ResourceId.Parse("quarterly");

        Assert.Null(Refusal(await Exports.AdmitAsync(
            AccessContext.Of(clerk),
            HostPermissions.Export,
            Documents,
            organization,
            record,
            TestContext.Current.CancellationToken)));
        Assert.Null(Refusal(await AdmitAsync(AccessContext.Of(clerk), HostPermissions.Export)));

        Assert.Equal(2, _audit.Exports.Count);
        Assert.Equal(2, _audit.Exports.Select(export => export.Id).Distinct().Count());

        ExportedAccess first = _audit.Exports[0];

        Assert.Equal(clerk, first.Acting);
        Assert.Equal(clerk, first.Effective);
        Assert.Null(first.Principal);
        Assert.Equal(organization, first.Organization);
        Assert.Equal(HostPermissions.Export, first.Permission);
        Assert.Equal(Documents, first.Type);
        Assert.Equal(record, first.Record);
        Assert.Equal(Noon, first.At);
        Assert.Null(_audit.Exports[1].Record);
    }

    /// <summary>
    /// OPS-ALERT-006 AC2, OPS-CFG-004: the audit is off only where the deployment
    /// turned it off where it is deployed, and even then the export is still counted.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_006_AC2_OnlyTheDeploymentTurnsTheAuditOffAsync()
    {
        _configuration.Set(Settings.ExfiltrationExportAuditing, false);

        Assert.Null(Refusal(await AdmitAsync(AccessContext.Of(SubjectId.New(_randomness)), HostPermissions.Export)));

        Assert.Empty(_audit.Exports);
        Assert.Single(_ledger.Admitted);
        Assert.Equal(SettingScope.Protected, Settings.ExfiltrationExportAuditing.Scope);
    }

    private static Error? Refusal(Result result) => result.Match(() => (Error?)null, error => error);

    private async Task<Result> AdmitAsync(AccessContext context, Permission permission) =>
        await Exports.AdmitAsync(
            context,
            permission,
            Documents,
            organization: null,
            record: null,
            TestContext.Current.CancellationToken);
}
