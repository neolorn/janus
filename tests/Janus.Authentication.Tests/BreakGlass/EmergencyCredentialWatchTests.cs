using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Authentication.BreakGlass;
using Janus.Authentication.Passwords;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.BreakGlass;

/// <summary>
/// The alert that stands while no break-glass credential does (OPS-BOOT-001 AC3,
/// OPS-ALERT-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class EmergencyCredentialWatchTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    private readonly BreakGlassStoreInMemory _store = new();
    private readonly EventsInMemory _alerts = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly SubjectId _administrator = new(Guid.CreateVersion7());

    private EmergencyCredentialWatch Watch => new(_store, _alerts, _clock);

    /// <summary>
    /// OPS-BOOT-001 AC3, OPS-ALERT-001 AC1: while no credential has been generated the
    /// absence is raised at High at every pass, with nobody watching, and nothing but
    /// generating one stops it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_BOOT_001_AC3_TheAbsenceIsRaisedUntilACredentialIsGeneratedAsync()
    {
        await WatchedAsync();
        _clock.Advance(TimeSpan.FromHours(1));
        await WatchedAsync();

        Assert.Equal(2, _alerts.Of<AlertRaised>().Count);
        Assert.All(_alerts.Of<AlertRaised>(), raised =>
        {
            Assert.Equal(AlertCondition.NoEmergencyCredential, raised.Condition);
            Assert.Equal(AlertSeverity.High, raised.Severity);
        });

        await _store.AddAsync(Issued(), TestContext.Current.CancellationToken);
        _clock.Advance(TimeSpan.FromHours(1));
        await WatchedAsync();

        Assert.Equal(2, _alerts.Of<AlertRaised>().Count);
    }

    /// <summary>
    /// OPS-BOOT-001 AC3: a credential spent in an emergency leaves none, so the absence
    /// is raised again until a new one is generated.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_BOOT_001_AC3_ASpentCredentialLeavesTheAbsenceRaisedAsync()
    {
        BreakGlassCredential issued = Issued();
        issued.Consume(Noon);

        await _store.AddAsync(issued, TestContext.Current.CancellationToken);
        await WatchedAsync();

        AlertRaised raised = Assert.Single(_alerts.Of<AlertRaised>());

        Assert.Equal(AlertCondition.NoEmergencyCredential, raised.Condition);
    }

    private BreakGlassCredential Issued() =>
        BreakGlassCredential.Issue(
            PasswordHash.Of(
                new Argon2StrengthClass(19456, 2),
                1,
                RandomNumberGenerator.GetBytes(16),
                RandomNumberGenerator.GetBytes(32)),
            _administrator,
            Noon.AddDays(-1));

    private async Task WatchedAsync() =>
        Assert.Null((await Watch.WatchAsync(TestContext.Current.CancellationToken))
            .Match(() => (Error?)null, error => error));
}
