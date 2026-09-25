using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Privacy.SubjectKeys;
using Janus.Storage.Authentication.Sending;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// What the outbox keeps between the transaction that undertook a message and the
/// transport that carries it (D-022, IDN-PRIN-003).
/// </summary>
/// <remarks>
/// One database serves the class, so each test writes a message of its own.
/// </remarks>
[Trait("kind", "integration")]
public sealed class SendOutboxTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new(database);

    /// <summary>
    /// D-022: the message read back is the message undertaken, so a publisher sends
    /// what the transaction meant to send and not a shape of it.
    /// </summary>
    [Fact]
    public async Task D_022_TheMessageReadsBackAsItWasUndertakenAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        SendDelivery undertaken = Delivery("first@example.test", subject);

        await WrittenAsync(undertaken);

        await using StoreContext reading = database.Context();

        SendDelivery held = await Outbox(reading)
            .FindAsync(undertaken.Id, TestContext.Current.CancellationToken)
            ?? throw new Xunit.Sdk.XunitException("The message was not written.");

        Assert.Equal(undertaken.Id, held.Id);
        Assert.Equal(Noon, held.RecordedAt);
        Assert.Equal(SendKind.Email, held.Requested.Kind);
        Assert.Equal("first@example.test", held.Requested.Destination.Canonical);
        Assert.Equal(MessageKind.SecondStepCode, held.Requested.Message);
        Assert.Equal(RestrictionPurpose.SignIn, held.Requested.Purpose);
        Assert.Equal("198.51.100.7", held.Requested.Source);
        Assert.Equal("ar", held.Requested.Language);
        Assert.Equal(subject, held.Requested.Subject);
        Assert.Equal("482913", held.Requested.Values["code"]);
    }

    /// <summary>
    /// IDN-ATTR-001: a message undertaken with no language of the recipient's known
    /// reads back with none, so it is still carried in every declared language.
    /// </summary>
    [Fact]
    public async Task IDN_ATTR_001_AMessageInEveryLanguageReadsBackWithNoneAsync()
    {
        SendDelivery undertaken = Delivery("every@example.test", subject: null, language: null);

        await WrittenAsync(undertaken);

        await using StoreContext reading = database.Context();

        SendDelivery held = await Outbox(reading)
            .FindAsync(undertaken.Id, TestContext.Current.CancellationToken)
            ?? throw new Xunit.Sdk.XunitException("The message was not written.");

        Assert.Null(held.Requested.Language);
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC8: the message is held under the row's own key, so a dump of
    /// the table without the key-encryption key yields no destination.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_005a_AC8_TheTableYieldsNoDestinationInPlainAsync()
    {
        const string address = "second@example.test";

        SendDelivery undertaken = Delivery(address, subject: null);

        await WrittenAsync(undertaken);

        await using StoreContext reading = database.Context();

        SendDeliveryRecord stored = await reading.SendOutbox
            .SingleAsync(held => held.Id == undertaken.Id, TestContext.Current.CancellationToken);

        Assert.Null(stored.Subject);
        Assert.Equal(PersonalDataFormat.Marker, stored.Message[0]);
        Assert.Equal(-1, stored.Message.AsSpan().IndexOf(Encoding.UTF8.GetBytes(address)));
        Assert.Equal(-1, stored.Message.AsSpan().IndexOf(Encoding.UTF8.GetBytes("482913")));
    }

    /// <summary>
    /// IDN-PRIN-003: a message a transport has taken is removed, so the outbox holds
    /// what is outstanding and no record of where anybody was written to.
    /// </summary>
    [Fact]
    public async Task IDN_PRIN_003_AMessageTakenLeavesNoRowAsync()
    {
        SendDelivery undertaken = Delivery("third@example.test", subject: null);

        await WrittenAsync(undertaken);

        await using (StoreContext removing = database.Context())
        {
            await Outbox(removing).RemoveAsync(undertaken.Id, TestContext.Current.CancellationToken);
            await removing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();

        Assert.Null(await Outbox(reading).FindAsync(undertaken.Id, TestContext.Current.CancellationToken));
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    private static SendDelivery Delivery(string address, SubjectId? subject, string? language = "ar")
    {
        if (!EmailAddress.TryParse(address, out EmailAddress destination))
        {
            throw new Xunit.Sdk.XunitException("The address does not parse.");
        }

        return SendDelivery.Of(
            new SendRequest(
                SendDestination.Of(destination),
                MessageKind.SecondStepCode,
                RestrictionPurpose.SignIn,
                "198.51.100.7",
                language)
            {
                Subject = subject,
                Values = new Dictionary<string, string>(StringComparer.Ordinal) { ["code"] = "482913" },
            },
            Noon);
    }

    private async Task WrittenAsync(SendDelivery delivery)
    {
        await using StoreContext writing = database.Context();

        await Outbox(writing).AddAsync(delivery, TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private SendDeliveryStore Outbox(StoreContext context) =>
        new(context, _deployment.Keys, _deployment.Randomness);
}
