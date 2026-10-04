using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Organizations;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Organizations;

/// <summary>
/// The pass that checks every listed domain again, as the unit of work sees it ended
/// (CONV-DESIGN-003).
/// </summary>
[Trait("kind", "unit")]
public sealed class DomainReverificationTests : IAsyncDisposable
{
    private static readonly AccessContext Sweeper = AccessContext.Of(
        SystemPrincipal.ForDeployment("domain-reverification", "REG-DOM-001", SystemOperation.ExpirySweep));

    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly OrganizationId Locked = new(Guid.NewGuid());

    private readonly DomainStoreInMemory _domains = new();
    private readonly DnsResolverInMemory _dns = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly EventsInMemory _events = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    private DomainReverification Reverification => new(_domains, _dns, _configuration, _events, _work, _clock);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _work.DisposeAsync();
        _randomness.Dispose();
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5: a failed check whose alert the channels do not take rolls
    /// the unit of work back, so the pass stops with nothing committed.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_AFailedCheckWhoseAlertIsNotRaisedIsRolledBackAsync()
    {
        var listed = LockedDomain.Listed(Locked, "example.test", _randomness, Noon);
        listed.Checked(passed: true, Noon);
        await _domains.AddAsync(listed, TestContext.Current.CancellationToken);
        _clock.Advance(Settings.DomainReverifyInterval.Default + TimeSpan.FromMinutes(1));
        _events.Refusal = Error.From(ErrorCodes.Denied);

        Result<int> swept = await Reverification.SweepAsync(Sweeper, TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.Denied, swept.Match(_ => (Error?)null, error => error)?.Code);
        Assert.False(_work.Open);
        Assert.Equal(0, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
    }
}
