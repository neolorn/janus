using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Privacy.SubjectKeys;
using Janus.Storage.Authentication.Sending;
using Microsoft.EntityFrameworkCore;
using Npgsql;
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

    private static readonly TimeSpan Held = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

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
    /// IDN-PRIN-003: a message the handler has taken is removed under the claim its
    /// attempt held, so the outbox holds what is outstanding and no record of where
    /// anybody was written to.
    /// </summary>
    [Fact]
    public async Task IDN_PRIN_003_AMessageTakenLeavesNoRowAsync()
    {
        SendDelivery undertaken = Delivery("third@example.test", subject: null);

        await WrittenAsync(undertaken);

        SendClaim claim = await ClaimedAsync(undertaken.Id, Noon)
            ?? throw new Xunit.Sdk.XunitException("The message was not claimed.");

        await using (StoreContext removing = database.Context())
        {
            Assert.True(await Outbox(removing).RemoveAsync(claim, TestContext.Current.CancellationToken));
        }

        await using StoreContext reading = database.Context();

        Assert.Null(await Outbox(reading).FindAsync(undertaken.Id, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// D-022, AUTH-ABUSE-004: what a failed attempt made of a message reads back as it
    /// was recorded, so a retry waits as long as the schedule said, and the message
    /// reads back with the reference drawn for it at its admission.
    /// </summary>
    [Fact]
    public async Task D_022_AnAttemptReadsBackAsItWasRecordedAsync()
    {
        SendDelivery undertaken = Delivery("fourth@example.test", subject: null, language: null);
        DateTimeOffset attempted = Noon.AddSeconds(5);

        await WrittenAsync(undertaken);

        SendClaim claim = await ClaimedAsync(undertaken.Id, Noon)
            ?? throw new Xunit.Sdk.XunitException("The message was not claimed.");

        SendDelivery refused = undertaken.Refused(attempted, TimeSpan.FromSeconds(30), 2.0m, jitter: 0.5);

        await using (StoreContext recording = database.Context())
        {
            Assert.True(await Outbox(recording).RecordAsync(refused, claim, TestContext.Current.CancellationToken));
        }

        await using StoreContext reading = database.Context();

        SendDelivery held = await Outbox(reading)
            .FindAsync(undertaken.Id, TestContext.Current.CancellationToken)
            ?? throw new Xunit.Sdk.XunitException("The message was not written.");

        Assert.Equal(1, held.Attempts);
        Assert.Equal(attempted.AddSeconds(15), held.NextAttemptAt);
        Assert.Null(held.Requested.Language);
        Assert.Equal(undertaken.Reference.Value, held.Reference.Value);
        Assert.Equal(undertaken.Reference.Value, held.Admitted.Reference.Value);
    }

    /// <summary>
    /// D-022, INF-BG-001: the publisher reads a message once its next attempt is due
    /// and not before.
    /// </summary>
    [Fact]
    public async Task D_022_OnlyAMessageWhoseAttemptIsDueIsReadAsync()
    {
        SendDelivery due = Delivery("fifth@example.test", subject: null);
        SendDelivery waiting = Delivery("sixth@example.test", subject: null, held: Held);

        await WrittenAsync(due);
        await WrittenAsync(waiting);

        await using StoreContext reading = database.Context();

        IReadOnlyList<SendDeliveryId> read = await Outbox(reading)
            .DueAsync(Noon.AddSeconds(1), count: 100, TestContext.Current.CancellationToken);

        Assert.Contains(due.Id, read);
        Assert.DoesNotContain(waiting.Id, read);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC9: a claim succeeds only where the row's next attempt is due, as
    /// well as unclaimed or timed out, so a row another pass released and rescheduled is
    /// not taken early, and is taken once its instant has come.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC9_ARowReleasedAndRescheduledIsNotClaimedBeforeItIsDueAsync()
    {
        SendDelivery undertaken = Delivery("eleventh@example.test", subject: null);

        await WrittenAsync(undertaken);

        SendClaim claim = await ClaimedAsync(undertaken.Id, Noon)
            ?? throw new Xunit.Sdk.XunitException("The message was not claimed.");

        SendDelivery refused = undertaken.Refused(Noon, TimeSpan.FromMinutes(10), 2.0m, jitter: 1.0);

        await using (StoreContext recording = database.Context())
        {
            Assert.True(await Outbox(recording).RecordAsync(refused, claim, TestContext.Current.CancellationToken));
        }

        Assert.Equal(Noon.AddMinutes(10), refused.NextAttemptAt);
        Assert.Null(await ClaimedAsync(undertaken.Id, Noon.AddMinutes(10) - TimeSpan.FromSeconds(1)));
        Assert.NotNull(await ClaimedAsync(undertaken.Id, Noon.AddMinutes(10)));
    }

    /// <summary>
    /// CONV-DESIGN-003 AC9, INF-BG-001 AC4: a row is claimed by one conditional update,
    /// so of two attempts that reach it at once one takes it; while the claim stands no
    /// pass reads the row as due and no other attempt takes it; and once the claim has
    /// timed out the next attempt takes it over.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC9_ARowIsClaimedByOneAttemptUntilItsClaimTimesOutAsync()
    {
        SendDelivery undertaken = Delivery("seventh@example.test", subject: null);

        await WrittenAsync(undertaken);

        SendClaim?[] claims = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => ClaimedAsync(undertaken.Id, Noon)));

        SendClaim claim = Assert.Single(claims, one => one is not null)!.Value;

        Assert.Equal(Noon + Timeout, claim.Until);
        Assert.Null(await ClaimedAsync(undertaken.Id, Noon + Timeout - TimeSpan.FromSeconds(1)));

        await using (StoreContext reading = database.Context())
        {
            Assert.DoesNotContain(
                undertaken.Id,
                await Outbox(reading).DueAsync(Noon.AddSeconds(1), count: 100, TestContext.Current.CancellationToken));
            Assert.Contains(
                undertaken.Id,
                await Outbox(reading).DueAsync(Noon + Timeout, count: 100, TestContext.Current.CancellationToken));
        }

        Assert.NotNull(await ClaimedAsync(undertaken.Id, Noon + Timeout));
    }

    /// <summary>
    /// CONV-DESIGN-003 AC9: an attempt's outcome is written by one statement conditional
    /// on its claim, so an attempt whose claim was taken over changes nothing: the row
    /// stands as the attempt that holds it left it.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC9_AnOutcomeWhoseClaimWasTakenOverChangesNothingAsync()
    {
        SendDelivery undertaken = Delivery("eighth@example.test", subject: null);

        await WrittenAsync(undertaken);

        SendClaim abandoned = await ClaimedAsync(undertaken.Id, Noon)
            ?? throw new Xunit.Sdk.XunitException("The message was not claimed.");
        SendClaim taken = await ClaimedAsync(undertaken.Id, Noon + Timeout)
            ?? throw new Xunit.Sdk.XunitException("The claim was not taken over.");

        await using (StoreContext late = database.Context())
        {
            Assert.False(await Outbox(late).RecordAsync(
                undertaken.Refused(Noon, TimeSpan.FromSeconds(30), 2.0m, jitter: 1),
                abandoned,
                TestContext.Current.CancellationToken));
            Assert.False(await Outbox(late).RemoveAsync(abandoned, TestContext.Current.CancellationToken));
        }

        await using (StoreContext reading = database.Context())
        {
            SendDelivery held = await Outbox(reading)
                .FindAsync(undertaken.Id, TestContext.Current.CancellationToken)
                ?? throw new Xunit.Sdk.XunitException("The message is gone.");

            Assert.Equal(0, held.Attempts);
        }

        await using StoreContext settling = database.Context();

        Assert.True(await Outbox(settling).RemoveAsync(taken, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-ABUSE-004, OPS-MIG-005: a row written before a send carried its reference
    /// holds none in its content. It reads with the reference made from its own
    /// identifier, the same at every read, and the statement the migration runs over
    /// such rows writes that reference's hash, as the ledger keeps one.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_ARowWrittenBeforeItCarriedAReferenceReadsWithOneOfItsOwnAsync()
    {
        var id = SendDeliveryId.Of(Noon);

        await using (StoreContext writing = database.Context())
        {
            byte[] deploymentKey = await _deployment.DataKey(writing)
                .UnwrappedAsync(TestContext.Current.CancellationToken);
            byte[] dataKey = PersonalFieldCipher.NewDataKey(_deployment.Randomness);

            writing.SendOutbox.Add(new SendDeliveryRecord
            {
                Id = id,
                RecordedAt = Noon,
                NextAttemptAt = Noon,
                WrappedKey = PersonalFieldCipher.Wrap(dataKey, deploymentKey),
                Message = PersonalFieldCipher.Encrypt(
                    dataKey,
                    new PersonalFieldLocation(new SubjectId(id.Value), "send_outbox", "enc_message"),
                    Encoding.UTF8.GetBytes(
                        """
                        {"kind":"email","destination":"before@example.test","message":"security-notice",
                         "purpose":"notification","source":null,"language":"en","subject":null,"values":{}}
                        """),
                    _deployment.Randomness),
            });

            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (NpgsqlConnection connection = await database.OpenAsync())
        {
            await connection.ExecuteAsync(
                """
                UPDATE identity.send_outbox
                SET reference = sha256(convert_to(translate(encode(uuid_send(id), 'base64'), '+/=', '-_'), 'UTF8'))
                WHERE id = @id;
                """,
                new { id = id.Value });
        }

        await using StoreContext reading = database.Context();

        SendDelivery first = await Outbox(reading).FindAsync(id, TestContext.Current.CancellationToken)
            ?? throw new Xunit.Sdk.XunitException("The row was not read.");
        SendDelivery again = await Outbox(reading).FindAsync(id, TestContext.Current.CancellationToken)
            ?? throw new Xunit.Sdk.XunitException("The row was not read.");

        SendDeliveryRecord stored = await reading.SendOutbox
            .AsNoTracking()
            .SingleAsync(row => row.Id == id, TestContext.Current.CancellationToken);

        Assert.Equal("before@example.test", first.Requested.Destination.Canonical);
        Assert.Equal(SendDeliveryStore.Unreferenced(id), first.Reference.Value);
        Assert.Equal(first.Reference.Value, again.Reference.Value);
        Assert.Equal(SendReferences.Of(first.Reference), stored.Reference);
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC18: a message that names no subject is bound to its own row, so
    /// its value and wrapped key moved onto another such row do not open there.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_005a_AC18_AMessageNamingNoSubjectDoesNotOpenOnAnotherRowAsync()
    {
        SendDelivery moved = Delivery("moved@example.test", subject: null);
        SendDelivery other = Delivery("other@example.test", subject: null);

        await WrittenAsync(moved);
        await WrittenAsync(other);

        await using (NpgsqlConnection connection = await database.OpenAsync())
        {
            await connection.ExecuteAsync(
                """
                UPDATE identity.send_outbox AS other
                SET wrapped_key = moved.wrapped_key, enc_message = moved.enc_message
                FROM identity.send_outbox AS moved
                WHERE other.id = @other AND moved.id = @moved;
                """,
                new { other = other.Id.Value, moved = moved.Id.Value });
        }

        await using StoreContext reading = database.Context();

        Assert.Equal(
            "moved@example.test",
            (await Outbox(reading).FindAsync(moved.Id, TestContext.Current.CancellationToken))?.Requested.Destination.Canonical);
        await Assert.ThrowsAnyAsync<CryptographicException>(async () =>
            await Outbox(reading).FindAsync(other.Id, TestContext.Current.CancellationToken));
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    private SendDelivery Delivery(
        string address,
        SubjectId? subject,
        string? language = "ar",
        TimeSpan? held = null)
    {
        if (!EmailAddress.TryParse(address, out EmailAddress destination))
        {
            throw new Xunit.Sdk.XunitException("The address does not parse.");
        }

        return SendDelivery.Of(
            new OutboundMessage(
                SendDestination.Of(destination),
                MessageKind.SecondStepCode,
                RestrictionPurpose.SignIn,
                "198.51.100.7",
                language)
            {
                Subject = subject,
                Values = new Dictionary<string, string>(StringComparer.Ordinal) { ["code"] = "482913" },
            },
            SendReference.Draw(_deployment.Randomness),
            Noon) with
        {
            NextAttemptAt = Noon + (held ?? TimeSpan.Zero),
        };
    }

    private async Task WrittenAsync(SendDelivery delivery)
    {
        await using StoreContext writing = database.Context();

        await Outbox(writing).AddAsync(delivery, TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // A claim as an attempt takes it: one conditional update, committed on its own.
    private async Task<SendClaim?> ClaimedAsync(SendDeliveryId delivery, DateTimeOffset now)
    {
        await using StoreContext claiming = database.Context();

        return await Outbox(claiming).ClaimAsync(delivery, now, Timeout, TestContext.Current.CancellationToken);
    }

    private SendDeliveryStore Outbox(StoreContext context) =>
        new(context, _deployment.DataKey(context), _deployment.Randomness);
}
