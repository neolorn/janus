using System;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Privacy.Erasures;
using Xunit;

namespace Janus.Privacy.Tests.Erasures;

/// <summary>
/// One pass over the organization deletion windows: which organizations the clock has
/// reached, what the erasure announces, and what a cancellation inside the window
/// leaves for the pass to find (IDN-ORG-003).
/// </summary>
[Trait("kind", "unit")]
public sealed class OrganizationErasureSweepTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    private static readonly OrganizationId Acme =
        new(Guid.Parse("33333333-3333-4333-8333-333333333333"));

    private static readonly OrganizationId Beta =
        new(Guid.Parse("33333333-3333-4333-8333-333333333334"));

    private readonly OrganizationStatesInMemory _organizations = new();
    private readonly EventsInMemory _events = new();
    private readonly PrivacyAuditInMemory _audit = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _work.DisposeAsync();

    private OrganizationErasureSweep Sweep =>
        new(_organizations, _events, _audit, _configuration, _work, _clock);

    /// <summary>
    /// IDN-ORG-003 AC3: the erasure executes when the window elapses and not before,
    /// so an organization one minute short of its end is left standing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ORG_003_AC3_TheErasureDoesNotExecuteBeforeTheWindowElapsesAsync()
    {
        TimeSpan grace = Settings.OrganizationDeletionGrace.Default;

        _organizations.Deletes(Acme, Noon - grace + TimeSpan.FromMinutes(1));

        Assert.Equal(0, await ErasedAsync());
        Assert.Empty(_organizations.Erased);

        _clock.Advance(TimeSpan.FromMinutes(1));

        Assert.Equal(1, await ErasedAsync());
        Assert.Equal(Acme, Assert.Single(_organizations.Erased));
    }

    /// <summary>
    /// IDN-ORG-003 AC2: a window cancelled inside itself is not reached by the pass,
    /// whatever the clock has done since.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ORG_003_AC2_ACancelledWindowIsNotReachedByThePassAsync()
    {
        _organizations.Deletes(Acme, Noon);
        _organizations.Deletes(Beta, Noon);
        _organizations.Cancels(Beta);

        _clock.Advance(Settings.OrganizationDeletionGrace.Default);

        Assert.Equal(1, await ErasedAsync());
        Assert.Equal(Acme, Assert.Single(_organizations.Erased));
    }

    /// <summary>
    /// IDN-ORG-003: the erasure is announced once it has committed, naming the
    /// organization, how many memberships it ended and nobody at all.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ORG_003_TheErasureIsAnnouncedWithWhatItEndedAsync()
    {
        _organizations.Deletes(Acme, Noon, members: 3);

        _clock.Advance(Settings.OrganizationDeletionGrace.Default);

        _ = await ErasedAsync();

        OrganizationErased announced = Assert.Single(_events.Of<OrganizationErased>());

        Assert.Equal(Acme, announced.Organization);
        Assert.Equal(3, announced.MembershipsEnded);
        Assert.Equal(_clock.GetUtcNow(), announced.RaisedAt);
        Assert.Null(announced.Subject);
        Assert.Null(announced.Actor);

        // The erasure is one transaction of its own, so a deployment that falls over
        // mid-pass has erased whole organizations and begun none.
        Assert.Equal(1, _work.Committed);
    }

    /// <summary>
    /// IDN-ORG-003: the erasure is written down where it happened, naming the
    /// organization and what the window it ended began at.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ORG_003_TheErasureIsWrittenDownAsync()
    {
        _organizations.Deletes(Acme, Noon, members: 2);

        _clock.Advance(Settings.OrganizationDeletionGrace.Default);

        _ = await ErasedAsync();

        PrivacyAuditEntry written = Assert.Single(_audit.Entries);

        Assert.Equal(AuditActions.OrganizationErased, written.Action);
        Assert.Null(written.Acting);
        Assert.Null(written.Subject);
        Assert.Equal(Acme.Value, written.Details["organization"].GetGuid());
        Assert.Equal(2, written.Details["membershipsEnded"].GetInt32());
    }

    /// <summary>
    /// IDN-ORG-003: the erasure has committed by the time it is announced, so a
    /// consumer that will not take the announcement stops the pass with its refusal
    /// rather than leaving an organization half erased.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ORG_003_APassWhoseAnnouncementIsRefusedAnswersWithTheRefusalAsync()
    {
        _organizations.Deletes(Acme, Noon);
        _organizations.Deletes(Beta, Noon.AddMinutes(1));

        _clock.Advance(Settings.OrganizationDeletionGrace.Default + TimeSpan.FromMinutes(1));

        _events.Refusal = Error.From(ErrorCodes.SystemFault);

        Result<int> swept = await Sweep.SweepAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            ErrorCodes.SystemFault,
            swept.Match(_ => null, error => (ErrorCode?)error.Code));

        Assert.Equal(Acme, Assert.Single(_organizations.Erased));
    }

    private async ValueTask<int> ErasedAsync() =>
        (await Sweep.SweepAsync(TestContext.Current.CancellationToken))
            .Match(count => count, error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));
}
