using System;
using System.Linq;
using System.Threading.Tasks;
using Janus.Authentication.Tests;
using Janus.Authorization.Gate;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// The record of a refusal: written, counted against its actor and, where the count
/// passes the threshold, raised as a spike, in one unit of work of its own
/// (AUTHZ-CONCEAL-004, D-183).
/// </summary>
[Trait("kind", "unit")]
public sealed class DenialRecordingTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly SubjectId Clerk = new(Guid.Parse("11111111-1111-4111-8111-111111111111"));

    private readonly ConfigurationInMemory _configuration = new();
    private readonly AccessAuditInMemory _audit = new();
    private readonly AccessAlertsInMemory _alerts = new();
    private readonly UnitOfWorkInMemory _work = new();

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _work.DisposeAsync();

    private DenialRecording Recording =>
        new(_work, _audit, new DenialSpikes(_audit, _configuration, _alerts));

    /// <summary>
    /// AUTHZ-CONCEAL-004: a refusal is written in a unit of work that holds its actor's
    /// refusals before it writes, and commits; one that takes the actor's count past
    /// <c>alerting.denials.threshold</c> raises <c>denial-spike</c> naming the actor
    /// inside that unit of work, before it commits.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_CONCEAL_004_ARefusalIsRecordedCountedAndRaisedInOneUnitOfWorkAsync()
    {
        int threshold = Settings.AlertingDenialsThreshold.Default;

        for (int each = 0; each < threshold; each++)
        {
            await Recording.RecordAsync(Denial(), TestContext.Current.CancellationToken);
        }

        Assert.Empty(_alerts.Raised);
        Assert.Equal((threshold, 0), (_work.OutermostCommitted, _work.RolledBack));

        _audit.Opened = () => _work.Open;

        await Recording.RecordAsync(Denial(), TestContext.Current.CancellationToken);

        Assert.Equal([(AlertCondition.DenialSpike, Clerk.ToString())], _alerts.Raised);
        Assert.Equal((threshold + 1, 0), (_work.OutermostCommitted, _work.RolledBack));
        Assert.Equal(["held:True", "recorded:True", "counted:True"], _audit.Asked.TakeLast(3));
        Assert.False(_work.Open);
    }

    /// <summary>
    /// AUTHZ-CONCEAL-004, CONV-DESIGN-003: a spike that cannot be raised fails the record
    /// of the refusal that reached it: the unit of work rolls back and the failure is a
    /// fault naming its code.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_CONCEAL_004_ASpikeThatCannotBeRaisedFailsTheRecordAsync()
    {
        for (int each = 0; each < Settings.AlertingDenialsThreshold.Default; each++)
        {
            await Recording.RecordAsync(Denial(), TestContext.Current.CancellationToken);
        }

        _work.Reset();
        _alerts.Refuses = Error.From(ErrorCodes.SystemFault);

        InvalidOperationException fault = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await Recording.RecordAsync(Denial(), TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.SystemFault.ToString(), fault.Message);
        Assert.Equal((0, 1), (_work.OutermostCommitted, _work.RolledBack));
        Assert.False(_work.Open);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC7: where the unit of work does not begin, or does not commit,
    /// the record throws a fault naming the failure's code.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC7_AUnitOfWorkThatFailsIsAFaultNamingItsCodeAsync()
    {
        _work.RefusesBegin = Error.From(ErrorCodes.SystemFault);

        InvalidOperationException notBegun = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await Recording.RecordAsync(Denial(), TestContext.Current.CancellationToken));

        _work.RefusesCommit = Error.From(ErrorCodes.SystemFault);

        InvalidOperationException notCommitted = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await Recording.RecordAsync(Denial(), TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.SystemFault.ToString(), notBegun.Message);
        Assert.Equal(ErrorCodes.SystemFault.ToString(), notCommitted.Message);
        Assert.False(_work.Open);
    }

    private static DeniedAccess Denial() =>
        new(
            AuditRecordId.New(TimeProvider.System),
            Clerk,
            Clerk,
            Principal: null,
            PrincipalReason: null,
            BreakGlassReason: null,
            Organization: null,
            Permissions.GrantRead,
            ResourceType.Parse("organization"),
            Noon,
            Grant: null);
}
