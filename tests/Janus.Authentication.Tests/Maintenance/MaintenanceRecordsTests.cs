using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Maintenance;
using Janus.Authentication.Policies;
using Janus.Authentication.Tests.Policies;
using Janus.Core;
using Xunit;

namespace Janus.Authentication.Tests.Maintenance;

/// <summary>
/// The licences and permits and the maintenance log as the management application
/// reads and edits them (OPS-MAINT-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class MaintenanceRecordsTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    private readonly MaintenanceStoreInMemory _store = new();
    private readonly AccessGateInMemory _gate = new();
    private readonly AdministrativeOrganizationInMemory _administrative = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();
    private readonly SubjectId _compliance;
    private readonly SubjectId _other;

    /// <summary>
    /// One person who holds <c>compliance:manage</c> in the administrative organization
    /// and one who does not.
    /// </summary>
    public MaintenanceRecordsTests()
    {
        var administering = OrganizationId.New(_clock);

        _administrative.Organization = administering;
        _compliance = SubjectId.New(_randomness);
        _other = SubjectId.New(_randomness);
        _gate.Grant(_compliance, administering, Permissions.ComplianceManage);
    }

    private MaintenanceRecords Records =>
        new(new AdministrativeScope(_gate, _administrative), _store, _work, _clock);

    /// <summary>
    /// OPS-MAINT-001: the licences and the log answer to <c>compliance:manage</c>, and
    /// a person without it reads and writes nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_MAINT_001_EveryOperationAnswersToComplianceManageAsync()
    {
        var other = AccessContext.Of(_other);

        Assert.Equal(
            ErrorCodes.Denied,
            Refusal(await Records.LicencesAsync(other, TestContext.Current.CancellationToken)));
        Assert.Equal(
            ErrorCodes.Denied,
            Refusal(await Records.ReplaceLicencesAsync(other, [Licence(Noon.AddDays(90))], TestContext.Current.CancellationToken)));
        Assert.Equal(
            ErrorCodes.Denied,
            Refusal(await Records.LogAsync(other, TestContext.Current.CancellationToken)));
        Assert.Equal(
            ErrorCodes.Denied,
            Refusal(await Records.RecordAsync(
                other,
                MaintenanceTask.ApproverReview,
                Noon,
                note: null,
                TestContext.Current.CancellationToken)));

        Assert.Empty(await _store.LicencesAsync(TestContext.Current.CancellationToken));
        Assert.Empty(_store.Log);
    }

    /// <summary>
    /// OPS-MAINT-001 AC1: the expiry dates are stored as given and read back soonest to
    /// lapse first.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_MAINT_001_AC1_TheExpiryDatesAreStoredAndReadBackAsync()
    {
        Licence later = Licence(Noon.AddYears(3));
        Licence sooner = Licence(Noon.AddMonths(4)) with { Kind = LicenceKind.Permit };

        Assert.True((await Records.ReplaceLicencesAsync(
                AccessContext.Of(_compliance),
                [later, sooner],
                TestContext.Current.CancellationToken))
            .Match(() => true, _ => false));

        IReadOnlyList<Licence> read = (await Records.LicencesAsync(
                AccessContext.Of(_compliance),
                TestContext.Current.CancellationToken))
            .Match(held => held, error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

        Assert.Equal([sooner, later], read);
    }

    /// <summary>
    /// OPS-MAINT-001: two licences under one identifier are one record stated twice,
    /// and the list is refused rather than one of them chosen.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_MAINT_001_TwoLicencesUnderOneIdentifierAreRefusedAsync()
    {
        Licence once = Licence(Noon.AddYears(3));

        Assert.Equal(
            ErrorCodes.RequestMalformed,
            Refusal(await Records.ReplaceLicencesAsync(
                AccessContext.Of(_compliance),
                [once, once with { Name = "Restated" }],
                TestContext.Current.CancellationToken)));

        Assert.Empty(await _store.LicencesAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// OPS-MAINT-001 AC3: an entry is dated and carries the person who asked, whoever
    /// the request might have named.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_MAINT_001_AC3_AnEntryIsDatedAndCarriesThePersonAskingAsync()
    {
        DateTimeOffset performedAt = Noon.AddDays(-2);

        MaintenanceEntry recorded = (await Records.RecordAsync(
                AccessContext.Of(_compliance),
                MaintenanceTask.PipelineConsumptionReview,
                performedAt,
                "Within the free allowance.",
                TestContext.Current.CancellationToken))
            .Match(entry => entry, error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

        Assert.Equal(_compliance, recorded.Actor);
        Assert.Equal(performedAt, recorded.PerformedAt);
        Assert.Equal(MaintenanceTask.PipelineConsumptionReview, recorded.Task);
        Assert.Equal([recorded], _store.Log);
        Assert.Equal(1, _work.Committed);
    }

    /// <summary>
    /// OPS-MAINT-001 AC3: a task dated after now has not been performed, and recording
    /// it is refused.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_MAINT_001_AC3_ATaskDatedAfterNowIsRefusedAsync()
    {
        Assert.Equal(
            ErrorCodes.RequestMalformed,
            Refusal(await Records.RecordAsync(
                AccessContext.Of(_compliance),
                MaintenanceTask.RiskTriggerReview,
                Noon.AddMinutes(1),
                note: null,
                TestContext.Current.CancellationToken)));

        Assert.Empty(_store.Log);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _work.DisposeAsync();
        _randomness.Dispose();
    }

    private static Licence Licence(DateTimeOffset expiresAt) =>
        new(new LicenceId(Guid.CreateVersion7(expiresAt)), LicenceKind.Licence, "Operating licence", expiresAt, RenewedAt: null);

    private static ErrorCode Refusal(Result result) =>
        result.Match(
            () => throw new Xunit.Sdk.XunitException("The operation was not refused."),
            error => error.Code);

    private static ErrorCode Refusal<TValue>(Result<TValue> result) =>
        result.Match(
            _ => throw new Xunit.Sdk.XunitException("The operation was not refused."),
            error => error.Code);
}
