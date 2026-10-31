using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Core;
using Xunit;

namespace Janus.Testing.Tests;

/// <summary>
/// The in-memory notification handler: every message kept, readable by destination and
/// by kind with its values, and a hand-over failed only where the test says so
/// (LIB-TEST-003, D-225).
/// </summary>
[Trait("kind", "unit")]
public sealed class NotificationsTests
{
    private static readonly EmailAddress Customer = Email("customer@example.test");

    private static readonly EmailAddress Staff = Email("staff@example.test");

    private static readonly PhoneNumber CustomerPhone = Phone("+201000000011");

    private static readonly RandomNumberGenerator Randomness = RandomNumberGenerator.Create();

    /// <summary>
    /// LIB-TEST-003: the handler keeps every message it takes, each read by the address
    /// or the number it is for and by its kind, in the order taken, with its values.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_TEST_003_TheHandlerKeepsEveryMessageByDestinationAndKindAsync()
    {
        var notifications = new InMemoryNotifications();

        Result[] sent =
        [
            await notifications.SendAsync(Mailed(Customer, MessageKind.VerificationCode, "111111"), TestContext.Current.CancellationToken),
            await notifications.SendAsync(Mailed(Staff, MessageKind.VerificationCode, "222222"), TestContext.Current.CancellationToken),
            await notifications.SendAsync(Texted(CustomerPhone, MessageKind.SecondStepCode, "333333"), TestContext.Current.CancellationToken),
            await notifications.SendAsync(Mailed(Customer, MessageKind.SignInCode, "444444"), TestContext.Current.CancellationToken),
        ];

        Assert.All(sent, result => Assert.True(Succeeded(result)));
        Assert.Equal(["111111", "444444"], notifications.SentTo(Customer).Select(request => request.Values["code"]));
        Assert.Equal(["222222"], notifications.SentTo(Staff).Select(request => request.Values["code"]));
        Assert.Equal(["333333"], notifications.SentTo(CustomerPhone).Select(request => request.Values["code"]));
        Assert.Equal(["111111", "222222"], notifications.Of(MessageKind.VerificationCode).Select(request => request.Values["code"]));
        Assert.Equal(4, notifications.Taken.Count);
    }

    /// <summary>
    /// LIB-TEST-003: a hand-over fails only for the destination the test names, from
    /// then until the test takes it back, and a failed hand-over is not kept.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_TEST_003_TheHandlerFailsAHandOverOnlyWhereTheTestSaysSoAsync()
    {
        var notifications = new InMemoryNotifications();

        notifications.FailHandOversTo(Customer);
        notifications.FailHandOversTo(CustomerPhone);

        Result failed = await notifications.SendAsync(Mailed(Customer, MessageKind.SignInCode, "111111"), TestContext.Current.CancellationToken);
        Result texted = await notifications.SendAsync(Texted(CustomerPhone, MessageKind.SecondStepCode, "222222"), TestContext.Current.CancellationToken);
        Result elsewhere = await notifications.SendAsync(Mailed(Staff, MessageKind.SignInCode, "333333"), TestContext.Current.CancellationToken);

        notifications.AcceptHandOversTo(Customer);
        notifications.AcceptHandOversTo(CustomerPhone);

        Result again = await notifications.SendAsync(Mailed(Customer, MessageKind.SignInCode, "444444"), TestContext.Current.CancellationToken);

        Assert.False(Succeeded(failed));
        Assert.False(Succeeded(texted));
        Assert.True(Succeeded(elsewhere));
        Assert.True(Succeeded(again));
        Assert.Equal(["444444"], notifications.SentTo(Customer).Select(request => request.Values["code"]));
        Assert.Empty(notifications.SentTo(CustomerPhone));
    }

    private static bool Succeeded(Result result) => result.Match(() => true, _ => false);

    private static SendRequest Mailed(EmailAddress destination, MessageKind kind, string code) =>
        Request(SendKind.Email, destination.Value, kind, code);

    private static SendRequest Texted(PhoneNumber destination, MessageKind kind, string code) =>
        Request(SendKind.Sms, destination.Value, kind, code);

    private static SendRequest Request(SendKind channel, string destination, MessageKind kind, string code) =>
        new(
            SendReference.Draw(Randomness),
            kind,
            channel,
            Subject: null,
            destination,
            ["en"],
            new Dictionary<string, string>(StringComparer.Ordinal) { ["code"] = code });

    private static EmailAddress Email(string entered) =>
        EmailAddress.TryParse(entered, out EmailAddress address) ? address : throw new ArgumentException(entered, nameof(entered));

    private static PhoneNumber Phone(string entered) =>
        PhoneNumber.TryParse(entered, out PhoneNumber number) ? number : throw new ArgumentException(entered, nameof(entered));
}
