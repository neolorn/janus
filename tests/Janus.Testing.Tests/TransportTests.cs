using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Janus.Core;
using Xunit;

namespace Janus.Testing.Tests;

/// <summary>
/// The stand-in transports: every message kept, readable by destination, and a delivery
/// failed only where the test says so (LIB-TEST-003).
/// </summary>
[Trait("kind", "unit")]
public sealed class TransportTests
{
    private static readonly EmailAddress Customer = Email("customer@example.test");

    private static readonly EmailAddress Staff = Email("staff@example.test");

    private static readonly PhoneNumber CustomerPhone = Phone("+201000000011");

    private static readonly PhoneNumber StaffPhone = Phone("+201000000012");

    /// <summary>
    /// LIB-TEST-003: the mail transport keeps every mail it takes, each read by the
    /// address it went to, in the order taken.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_TEST_003_TheMailTransportKeepsEveryMailByDestinationAsync()
    {
        var transport = new InMemoryMailTransport();

        Result[] sent =
        [
            await transport.SendAsync(Mail(Customer, "first"), TestContext.Current.CancellationToken),
            await transport.SendAsync(Mail(Staff, "other"), TestContext.Current.CancellationToken),
            await transport.SendAsync(Mail(Customer, "second"), TestContext.Current.CancellationToken),
        ];

        Assert.All(sent, result => Assert.True(Succeeded(result)));
        Assert.Equal(["first", "second"], transport.SentTo(Customer).Select(mail => mail.Body));
        Assert.Equal(["other"], transport.SentTo(Staff).Select(mail => mail.Body));
    }

    /// <summary>
    /// LIB-TEST-003: a delivery fails only to the destination the test names, from then
    /// until the test takes it back, and a failed delivery is not kept.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_TEST_003_TheMailTransportFailsADeliveryOnlyWhereTheTestSaysSoAsync()
    {
        var transport = new InMemoryMailTransport();

        transport.FailDeliveriesTo(Customer);

        Result failed = await transport.SendAsync(Mail(Customer, "failed"), TestContext.Current.CancellationToken);
        Result elsewhere = await transport.SendAsync(Mail(Staff, "taken"), TestContext.Current.CancellationToken);

        transport.AcceptDeliveriesTo(Customer);

        Result again = await transport.SendAsync(Mail(Customer, "again"), TestContext.Current.CancellationToken);

        Assert.False(Succeeded(failed));
        Assert.True(Succeeded(elsewhere));
        Assert.True(Succeeded(again));
        Assert.Equal(["again"], transport.SentTo(Customer).Select(mail => mail.Body));
    }

    /// <summary>
    /// LIB-TEST-003: the SMS transport keeps every message it takes by destination, fails
    /// a delivery only where the test says so, reports a balance of 99999999999999.9999,
    /// and reads no delivery report.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_TEST_003_TheSmsTransportKeepsEveryMessageByDestinationAsync()
    {
        var transport = new InMemorySmsTransport();

        Result taken = await transport.SendAsync(Sms(CustomerPhone, "code"), TestContext.Current.CancellationToken);

        transport.FailDeliveriesTo(StaffPhone);

        Result failed = await transport.SendAsync(Sms(StaffPhone, "failed"), TestContext.Current.CancellationToken);

        transport.AcceptDeliveriesTo(StaffPhone);

        Result again = await transport.SendAsync(Sms(StaffPhone, "again"), TestContext.Current.CancellationToken);
        Result<decimal> balance = await transport.BalanceAsync(TestContext.Current.CancellationToken);

        Assert.True(Succeeded(taken));
        Assert.False(Succeeded(failed));
        Assert.True(Succeeded(again));
        Assert.Equal(["code"], transport.SentTo(CustomerPhone).Select(message => message.Text));
        Assert.Equal(["again"], transport.SentTo(StaffPhone).Select(message => message.Text));
        Assert.Equal(99999999999999.9999m, balance.Match(value => value, _ => 0m));
        Assert.False(transport.ReadReport(new Dictionary<string, string>(StringComparer.Ordinal)).Match(_ => true, _ => false));
    }

    private static bool Succeeded(Result result) => result.Match(() => true, _ => false);

    private static EmailAddress Email(string entered) =>
        EmailAddress.TryParse(entered, out EmailAddress address) ? address : throw new ArgumentException(entered, nameof(entered));

    private static PhoneNumber Phone(string entered) =>
        PhoneNumber.TryParse(entered, out PhoneNumber number) ? number : throw new ArgumentException(entered, nameof(entered));

    private static MailMessage Mail(EmailAddress destination, string body) =>
        new(destination, "subject", body, Guid.NewGuid().ToString());

    private static SmsMessage Sms(PhoneNumber destination, string text) =>
        new(destination, text, Guid.NewGuid().ToString());
}
