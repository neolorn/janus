using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Sending;
using Janus.Core;
using Xunit;

namespace Janus.Authentication.Tests.Sending;

/// <summary>
/// The host's report of a message's outcome: what a failure releases, what a delivered
/// <c>secondfactor</c> text releases, and that nothing else moves (INT-SMS-005,
/// AUTH-ABUSE-004, AUTH-ABUSE-007, D-225).
/// </summary>
[Trait("kind", "unit")]
public sealed class SendOutcomesTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly RestrictionKey Destination =
        new("sms.destination", RestrictionKeyKind.Destination, "+201001234567");

    private static readonly RestrictionKey Source = new("sms.source", RestrictionKeyKind.Source, "198.51.100.7");

    private static readonly RestrictionKey Resend = new("sms.resend", RestrictionKeyKind.Destination, "+201001234567");

    private readonly SendLedgerInMemory _ledger = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    private SendOutcomes Outcomes => new(_ledger, _work);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _work.DisposeAsync();
        _randomness.Dispose();
    }

    /// <summary>
    /// INT-SMS-005 AC1, AUTH-ABUSE-007: a report that a message was delivered marks
    /// nothing verified and, for a send that is not of purpose <c>secondfactor</c>,
    /// releases nothing: every count stands.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_SMS_005_AC1_AReportOfDeliveryMarksNothingAndReleasesNothingAsync()
    {
        SendReference reference = await SentAsync(secondFactor: false);

        Assert.True(await ReportedAsync(reference, delivered: true));

        Assert.Single(_ledger.Sends(Destination));
        Assert.Single(_ledger.Sends(Source));
        Assert.Single(_ledger.Sends(Resend));
        Assert.Equal(1, _work.OutermostCommitted);
    }

    /// <summary>
    /// INT-SMS-005 AC2, AUTH-ABUSE-004: a report that a message failed releases its
    /// count from every restriction not marked <c>resend</c> and its one count from each
    /// marked <c>resend</c>.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_SMS_005_AC2_AFailureReportReleasesEveryCountAsync()
    {
        SendReference reference = await SentAsync(secondFactor: false);

        Assert.True(await ReportedAsync(reference, delivered: false));

        Assert.Empty(_ledger.Sends(Destination));
        Assert.Empty(_ledger.Sends(Source));
        Assert.Empty(_ledger.Sends(Resend));
    }

    /// <summary>
    /// INT-SMS-005 AC2, D-197: a report that a <c>secondfactor</c> text was delivered
    /// releases its count from each restriction marked <c>resend</c> and changes nothing
    /// else.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_SMS_005_AC2_ADeliveredSecondFactorTextReleasesOnlyItsResendCountAsync()
    {
        SendReference reference = await SentAsync(secondFactor: true);

        Assert.True(await ReportedAsync(reference, delivered: true));

        Assert.Single(_ledger.Sends(Destination));
        Assert.Single(_ledger.Sends(Source));
        Assert.Empty(_ledger.Sends(Resend));
    }

    /// <summary>
    /// INT-SMS-005 AC3: a report naming no message of the library's is answered false
    /// and changes nothing, whatever it says became of the message.
    /// </summary>
    /// <param name="delivered">What the report says.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task INT_SMS_005_AC3_AReportNamingNoMessageChangesNothingAsync(bool delivered)
    {
        _ = await SentAsync(secondFactor: true);

        Assert.False(await ReportedAsync(SendReference.Draw(_randomness), delivered));

        Assert.Single(_ledger.Sends(Destination));
        Assert.Single(_ledger.Sends(Source));
        Assert.Single(_ledger.Sends(Resend));
    }

    /// <summary>
    /// INT-SMS-005 AC3 (D-227): a repeated report changes nothing and is still answered
    /// true, the message being one the library handed over. A failure releases the
    /// message's counts once, a second delivery report of a <c>secondfactor</c> text
    /// releases nothing more, and a reference never issued is answered false.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_SMS_005_AC3_ARepeatedReportChangesNothingAsync()
    {
        SendReference failed = await SentAsync(secondFactor: false);
        SendReference delivered = await SentAsync(secondFactor: true);

        Assert.True(await ReportedAsync(failed, delivered: false));
        Assert.True(await ReportedAsync(failed, delivered: false));
        Assert.True(await ReportedAsync(failed, delivered: true));
        Assert.True(await ReportedAsync(delivered, delivered: true));
        Assert.True(await ReportedAsync(delivered, delivered: true));
        Assert.False(await ReportedAsync(SendReference.Draw(_randomness), delivered: false));

        Assert.Single(_ledger.Sends(Destination));
        Assert.Single(_ledger.Sends(Source));
        Assert.Empty(_ledger.Sends(Resend));
    }

    /// <summary>
    /// CONV-DESIGN-003 AC8: a report is an outermost unit of work, so one made inside a
    /// unit of work already open is a fault and changes nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC8_AReportInsideAnOpenUnitOfWorkIsAFaultAsync()
    {
        SendReference reference = await SentAsync(secondFactor: false);
        _ = await _work.BeginAsync(TestContext.Current.CancellationToken);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Outcomes.ReportAsync(reference, delivered: false, TestContext.Current.CancellationToken));

        Assert.Equal(0, _work.OutermostCommitted);
        Assert.Single(_ledger.Sends(Destination));
    }

    private async Task<bool> ReportedAsync(SendReference reference, bool delivered) =>
        (await Outcomes.ReportAsync(reference, delivered, TestContext.Current.CancellationToken))
            .Match(
                named => named,
                error => throw new Xunit.Sdk.XunitException("The report failed: " + error.Code + "."));

    // What the ledger holds once one text has been handed over: the two keys it counts
    // against, and its send's one count against a restriction marked resend.
    private async Task<SendReference> SentAsync(bool secondFactor)
    {
        var reference = SendReference.Draw(_randomness);

        await _ledger.RecordAsync(
            [
                new CountedMessage(
                    SendReferences.Of(reference),
                    new SendPlan(
                        [new SendCount(Destination, TimeSpan.FromHours(24)), new SendCount(Source, TimeSpan.FromHours(24))],
                        [])),
            ],
            new SendPlan([new SendCount(Resend, TimeSpan.FromHours(24))], []),
            secondFactor,
            Noon,
            TestContext.Current.CancellationToken);

        return reference;
    }
}
