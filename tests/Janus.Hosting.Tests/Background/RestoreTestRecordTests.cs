using System;
using System.Linq;
using System.Threading.Tasks;
using Janus.Authentication.Tests;
using Janus.Core;
using Janus.Hosting.Background;
using Janus.Hosting.Tests.Bff;
using Janus.Privacy.Tests.SubjectKeys;
using Xunit;

namespace Janus.Hosting.Tests.Background;

/// <summary>
/// How the restore test ends the unit of work it records a run in: the record and the
/// alert of a failed run commit together, or neither does (DR-007, CONV-DESIGN-003).
/// </summary>
[Trait("kind", "unit")]
public sealed class RestoreTestRecordTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    private readonly ConfigurationInMemory _configuration = new();
    private readonly Janus.Privacy.Tests.PrivacyAuditInMemory _audit = new();
    private readonly EventsInMemory _alerts = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);

    private RestoreTest Test =>
        new(
            instance: null,
            new KeyRingInMemory(),
            _configuration,
            _audit,
            _alerts,
            _work,
            _clock,
            new LogInMemory<RestoreTest>());

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _work.DisposeAsync();

    /// <summary>
    /// DR-007 AC3, CONV-DESIGN-003: a run that restored nothing is recorded and raised
    /// in one unit of work, committed once.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task DR_007_AC3_AFailedRunIsRecordedAndRaisedInOneUnitOfWorkAsync()
    {
        Result ran = await RunAsync();

        Assert.Null(ran.Match(() => default(ErrorCode?), error => error.Code));
        Assert.Single(_audit.Entries);
        Assert.Equal(AlertCondition.RestoreTestFailed, Assert.Single(_alerts.Of<AlertRaised>()).Condition);
        Assert.False(_work.Open);
        Assert.Equal(1, _work.Committed);
        Assert.Equal(0, _work.RolledBack);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5: a failed run whose alert cannot be raised is refused after
    /// its unit of work began, and rolls it back, so the run is not recorded without its
    /// alert.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_ARunWhoseAlertCannotBeRaisedRollsBackAsync()
    {
        _alerts.Refusal = Error.From(ErrorCodes.SystemFault);

        Result ran = await RunAsync();

        Assert.Equal(ErrorCodes.SystemFault, ran.Match(() => default(ErrorCode?), error => error.Code));
        Assert.False(_work.Open);
        Assert.Equal(0, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
    }

    // One run of the test as its job's principal.
    private ValueTask<Result> RunAsync() =>
        Test.RunAsync(
            AccessContext.Of(BackgroundJobs.All.Single(job => job.Name == "restore-test").Principal),
            TestContext.Current.CancellationToken);
}
