using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Mailboxes;
using Janus.Core;
using Xunit;

namespace Janus.Authentication.Tests.Mailboxes;

/// <summary>
/// The daily comparison of the library's mailboxes with the server's: what counts as
/// drift, what does not, and that neither side is touched (INT-MAIL-006, INT-MAIL-006a,
/// INT-MAIL-007).
/// </summary>
[Trait("kind", "unit")]
public sealed class MailboxReconciliationTests : IDisposable
{
    private const string Address = "staff@example.test";

    private static readonly DateTimeOffset Noon = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);

    private readonly MailboxStoreInMemory _mailboxes = new();
    private readonly MailServerInMemory _server = new();
    private readonly EventsInMemory _events = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();
    private readonly SubjectId _holder;

    /// <summary>
    /// A person whose account and membership stand.
    /// </summary>
    public MailboxReconciliationTests()
    {
        _holder = SubjectId.New(_randomness);
        _ = _mailboxes.Standing.Add(_holder);
    }

    /// <inheritdoc/>
    public void Dispose() => _randomness.Dispose();

    /// <summary>
    /// INT-MAIL-007 AC2: a mailbox the server holds in another state than the one it
    /// is owed is reported, alerted, and changed on neither side.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_MAIL_007_AC2_ADriftIsReportedAndNothingIsChangedAsync()
    {
        Mailbox held = await HeldAsync(enabledOnServer: false);

        MailboxDrift drift = await ReconciledAsync();

        Assert.Equal([held.Id], drift.Mailboxes);

        AlertRaised raised = Assert.Single(_events.Of<AlertRaised>());

        Assert.Equal(AlertCondition.Degradation, raised.Condition);
        Assert.Equal(held.Id.ToString(), raised.Details["mailboxes"][0].GetString());
        Assert.False(_server.Hosts(Address));
        Assert.Empty(_server.Received);
        Assert.Equal(0, _mailboxes.Recorded);
    }

    /// <summary>
    /// INT-MAIL-006a AC3: enabled state is compared, not existence alone, so a mailbox
    /// a failed push left serving a suspended holder is drift.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_MAIL_006a_AC3_AnEnabledMailboxOwedDisabledIsDriftAsync()
    {
        Mailbox held = await HeldAsync(enabledOnServer: true);

        _ = _mailboxes.Standing.Remove(_holder);

        MailboxDrift drift = await ReconciledAsync();

        Assert.Equal([held.Id], drift.Mailboxes);
    }

    /// <summary>
    /// INT-MAIL-006 AC1c: a mailbox reserved for an open or expired invitation is
    /// expected disabled, and finding it so is no drift.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_MAIL_006_AC1c_AReservedMailboxIsNoDriftAsync()
    {
        await _mailboxes.AddAsync(Mailbox.Reserved(Address, Noon), TestContext.Current.CancellationToken);
        _server.Set(Address, enabled: false);

        MailboxDrift drift = await ReconciledAsync();

        Assert.True(drift.IsEmpty);
        Assert.Empty(_events.Of<AlertRaised>());
    }

    /// <summary>
    /// INT-MAIL-006 AC1b: an account that holds no mailbox, the reserved emergency
    /// account among them, is nothing to look for; a mailbox the server hosts and the
    /// library never provisioned is drift of the other side.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_MAIL_006_AC1b_OnlyWhatEitherSideHoldsIsComparedAsync()
    {
        MailboxDrift quiet = await ReconciledAsync();

        Assert.True(quiet.IsEmpty);

        _server.Set("stranger@example.test", enabled: true);

        MailboxDrift drift = await ReconciledAsync();

        Assert.Empty(drift.Mailboxes);
        Assert.Equal(1, drift.Unknown);
    }

    /// <summary>
    /// INT-MAIL-007: a mailbox whose removal the server has not carried out is drift,
    /// and one it has is not.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_MAIL_007_AReleasedMailboxIsExpectedGoneAsync()
    {
        var released = Mailbox.Reserved(Address, Noon);

        released.Release(Noon);
        await _mailboxes.AddAsync(released, TestContext.Current.CancellationToken);
        _server.Set(Address, enabled: false);

        Assert.Equal([released.Id], (await ReconciledAsync()).Mailboxes);

        _server.Set(Address, enabled: null);

        Assert.True((await ReconciledAsync()).IsEmpty);
    }

    /// <summary>
    /// OPS-OBS-002: a comparison that could not be made is a degradation of its own,
    /// and the pass answers the failure.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_MAIL_007_AC3_AComparisonThatCouldNotBeMadeIsVisibleAsync()
    {
        _server.Unreachable = true;

        Result<MailboxDrift> outcome = await Reconciliation.ReconcileAsync(TestContext.Current.CancellationToken);

        Assert.False(outcome.Match(_ => true, _ => false));

        AlertRaised raised = Assert.Single(_events.Of<AlertRaised>());

        Assert.False(raised.Details["listed"].GetBoolean());
    }

    private MailboxReconciliation Reconciliation => new(_mailboxes, _server, _events, _clock);

    private async Task<MailboxDrift> ReconciledAsync() =>
        (await Reconciliation.ReconcileAsync(TestContext.Current.CancellationToken))
            .Match(drift => drift, error => throw new InvalidOperationException(error.Code.ToString()));

    private async Task<Mailbox> HeldAsync(bool enabledOnServer)
    {
        var mailbox = Mailbox.Reserved(Address, Noon);

        mailbox.Hold(_holder);
        await _mailboxes.AddAsync(mailbox, TestContext.Current.CancellationToken);
        _server.Set(Address, enabledOnServer);

        return _mailboxes.Held.Single();
    }
}
