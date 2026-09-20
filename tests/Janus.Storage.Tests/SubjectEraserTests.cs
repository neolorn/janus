using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication;
using Janus.Authentication.Factors;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Identity.Accounts;
using Janus.Identity.Profiles;
using Janus.Privacy.Erasures;
using Janus.Privacy.SubjectKeys;
using Janus.Storage.Authentication.Sessions;
using Janus.Storage.Identity.Accounts;
using Janus.Storage.Identity.Profiles;
using Janus.Storage.Privacy.Erasures;
using Janus.Storage.Privacy.SubjectKeys;
using Janus.Storage.Settings;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests;

/// <summary>
/// The erasure: one transaction that leaves a subject's fields unrecoverable and every
/// row where it was (PRIV-RIGHT-005, PRIV-RIGHT-005a, IDN-LIFE-003b, IDN-LIFE-014,
/// IDN-ACCT-002, IDN-PRIN-003).
/// </summary>
[Trait("kind", "integration")]
public sealed class SubjectEraserTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new(database);

    private SubjectEraser Eraser(JanusDbContext context) => new(
        context,
        new SessionStore(context, _deployment.Keys, _deployment.Randomness),
        new ConfigurationStore(context));

    /// <summary>
    /// IDN-LIFE-003b AC4, PRIV-RIGHT-005a: the erasure commits as one thing. Afterwards
    /// the account is <c>deleted</c>, the wrapped key is the irreversible value and the
    /// erasures row is there.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_003b_AC4_TheErasureAndItsRowCommitTogetherAsync()
    {
        SubjectId subject = await DeletingAccountAsync();

        await EraseAsync(subject, ErasureReason.ErasureRequest);

        await using JanusDbContext reading = database.Context();
        AccountRecord account = await reading.Accounts
            .SingleAsync(row => row.Subject == subject, TestContext.Current.CancellationToken);
        Erasure erasure = Assert.IsType<Erasure>(
            await Store(reading).FindBySubjectAsync(subject, TestContext.Current.CancellationToken));

        Assert.Equal(AccountState.Deleted, account.State);
        Assert.Equal(ErasureReason.ErasureRequest, erasure.Reason);
        Assert.Equal(ErasureStatus.AwaitingSubscribers, erasure.Status);
        Assert.Equal(0, erasure.Attempts);
        Assert.Equal(Noon, erasure.RequestedAt);
    }

    /// <summary>
    /// IDN-LIFE-003b AC4: a transaction that does not commit leaves no row and erases
    /// nothing. There is no third state in which the row is absent and the key is gone.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_003b_AC4_ATransactionThatRollsBackErasesNothingAsync()
    {
        SubjectId subject = await DeletingAccountAsync();

        await using (JanusDbContext erasing = database.Context())
        await using (var work = new UnitOfWork(erasing))
        {
            await work.BeginAsync(TestContext.Current.CancellationToken);
            await Eraser(erasing).EraseAsync(
                subject,
                ErasureReason.ErasureRequest,
                Noon,
                TestContext.Current.CancellationToken);
            await erasing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using JanusDbContext reading = database.Context();
        AccountRecord account = await reading.Accounts
            .SingleAsync(row => row.Subject == subject, TestContext.Current.CancellationToken);

        Assert.Null(await Store(reading).FindBySubjectAsync(
            subject,
            TestContext.Current.CancellationToken));
        Assert.Equal(AccountState.Deleting, account.State);

        SubjectKeyRecord key = await reading.SubjectKeys
            .SingleAsync(row => row.Subject == subject, TestContext.Current.CancellationToken);

        Assert.Equal(PersonalDataFormat.Marker, key.FormatMarker);
    }

    /// <summary>
    /// PRIV-RIGHT-005 AC1, AC6, IDN-LIFE-014 AC1: after the erasure no personal field
    /// is recoverable. The declared profile values go with the key.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_005_AC6_NoPersonalFieldIsRecoverableAfterErasureAsync()
    {
        SubjectId subject = await DeletingAccountAsync();

        await using (JanusDbContext writing = database.Context())
        {
            Assert.True(LegalName.TryParse("Ahmed Hassan", out LegalName legal));

            var profile = Profile.Empty(subject);
            profile.SetLegalName(legal);
            profile.RecordDateOfBirth(new DateOnly(1990, 4, 17));

            await new ProfileStore(writing, _deployment.Keys, _deployment.Randomness)
                .RecordAsync(profile, TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await EraseAsync(subject, ErasureReason.ErasureRequest);

        await using JanusDbContext reading = database.Context();

        await Assert.ThrowsAsync<CryptographicException>(async () =>
            await new ProfileStore(reading, _deployment.Keys, _deployment.Randomness)
                .FindBySubjectAsync(subject, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// IDN-ATTR-003 AC1: erasing the account leaves the photo unreadable in the same
    /// transaction, because the image is held under the key that transaction destroys.
    /// The row stays where it is (IDN-PRIN-003).
    /// </summary>
    [Fact]
    public async Task IDN_ATTR_003_AC1_ThePhotoIsUnreadableInTheSameTransactionAsync()
    {
        SubjectId subject = await DeletingAccountAsync();
        byte[] image = new byte[256];
        _deployment.Randomness.GetBytes(image);

        await using (JanusDbContext writing = database.Context())
        {
            await new ProfilePhotoStore(writing, _deployment.Keys, _deployment.Randomness)
                .RecordAsync(ProfilePhoto.Of(subject, image, Noon), TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await EraseAsync(subject, ErasureReason.ErasureRequest);

        await using JanusDbContext reading = database.Context();

        Assert.True(await reading.ProfilePhotos
            .AnyAsync(photo => photo.Subject == subject, TestContext.Current.CancellationToken));

        await Assert.ThrowsAsync<CryptographicException>(async () =>
            await new ProfilePhotoStore(reading, _deployment.Keys, _deployment.Randomness)
                .FindBySubjectAsync(subject, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// PRIV-RIGHT-005 AC4: the photo is rendered unreadable by the same operation. Its
    /// bytes are encrypted under the key that operation destroys, so the ciphertext the
    /// row still holds is the ciphertext it held before, and nothing reads it.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_005_AC4_ThePhotoIsRenderedUnreadableByTheSameOperationAsync()
    {
        SubjectId subject = await DeletingAccountAsync();
        byte[] image = new byte[256];
        _deployment.Randomness.GetBytes(image);

        await using (JanusDbContext writing = database.Context())
        {
            await new ProfilePhotoStore(writing, _deployment.Keys, _deployment.Randomness)
                .RecordAsync(ProfilePhoto.Of(subject, image, Noon), TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        byte[] before = await StoredImageAsync(subject);

        await EraseAsync(subject, ErasureReason.ErasureRequest);

        await using JanusDbContext reading = database.Context();

        Assert.Equal(before, await StoredImageAsync(subject));
        await Assert.ThrowsAsync<CryptographicException>(async () =>
            await new ProfilePhotoStore(reading, _deployment.Keys, _deployment.Randomness)
                .FindBySubjectAsync(subject, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// IDN-ACCT-002 AC3, IDN-PRIN-003 AC2, AC3: the subject identifier still resolves
    /// after the erasure, the identifier rows are still there, and an audit record that
    /// names the subject still resolves to it.
    /// </summary>
    [Fact]
    public async Task IDN_ACCT_002_AC3_TheSubjectStillResolvesAfterItsErasureAsync()
    {
        SubjectId subject = await DeletingAccountAsync();

        await EraseAsync(subject, ErasureReason.ErasureRequest);

        await using NpgsqlConnection connection = await database.OpenAsync();

        Assert.Equal(
            1,
            await connection.ExecuteScalarAsync<int>(
                "SELECT count(*) FROM janus.accounts WHERE subject = @subject",
                new { subject = subject.Value }));

        Assert.Equal(
            1,
            await connection.ExecuteScalarAsync<int>(
                "SELECT count(*) FROM janus.subject_keys WHERE subject = @subject",
                new { subject = subject.Value }));
    }

    /// <summary>
    /// A subject is erased once. A second erasure would move the instant it happened
    /// and count its attempts from nothing.
    /// </summary>
    [Fact]
    public async Task EraseAsync_ASubjectAlreadyErased_ThrowsAsync()
    {
        SubjectId subject = await DeletingAccountAsync();

        await EraseAsync(subject, ErasureReason.ErasureRequest);

        await using JanusDbContext erasing = database.Context();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Eraser(erasing).EraseAsync(
                subject,
                ErasureReason.ErasureRequest,
                Noon,
                TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// IDN-LIFE-003b AC2: every erasure whose host-side work is outstanding comes back
    /// in one query, whether it is awaiting subscribers or has failed, and a completed
    /// one does not.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_003b_AC2_EveryIncompleteErasureComesBackInOneQueryAsync()
    {
        SubjectId awaiting = await DeletingAccountAsync();
        SubjectId failed = await DeletingAccountAsync();
        SubjectId complete = await DeletingAccountAsync();

        await EraseAsync(awaiting, ErasureReason.ErasureRequest);
        await EraseAsync(failed, ErasureReason.MinorTakedown);
        await EraseAsync(complete, ErasureReason.OrganizationErasure);

        await using (JanusDbContext progressing = database.Context())
        {
            ErasureStore store = Store(progressing);

            Erasure one = Assert.IsType<Erasure>(
                await store.FindBySubjectAsync(failed, TestContext.Current.CancellationToken));
            one.RecordAttempt();
            one.Fail();
            await store.RecordAsync(one, TestContext.Current.CancellationToken);

            Erasure two = Assert.IsType<Erasure>(
                await store.FindBySubjectAsync(complete, TestContext.Current.CancellationToken));
            two.Complete();
            await store.RecordAsync(two, TestContext.Current.CancellationToken);

            await progressing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using JanusDbContext reading = database.Context();
        IReadOnlyList<Erasure> outstanding = await Store(reading).FindIncompleteAsync(
            TestContext.Current.CancellationToken);

        List<SubjectId> subjects = [];

        foreach (Erasure erasure in outstanding)
        {
            subjects.Add(erasure.Subject);
        }

        Assert.Contains(awaiting, subjects);
        Assert.Contains(failed, subjects);
        Assert.DoesNotContain(complete, subjects);
    }

    /// <summary>
    /// IDN-LIFE-003b AC3: a status outside the three the chapter names is refused by
    /// the database.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_003b_AC3_AnUnrecognisedStatusIsRefusedByTheDatabaseAsync()
    {
        SubjectId subject = await DeletingAccountAsync();
        await EraseAsync(subject, ErasureReason.ErasureRequest);

        await using NpgsqlConnection connection = await database.OpenAsync();

        PostgresException refusal = await Assert.ThrowsAsync<PostgresException>(async () =>
            await connection.ExecuteAsync(
                "UPDATE janus.erasures SET status = 'half-done' WHERE subject = @subject",
                new { subject = subject.Value }));

        Assert.Equal("ck_erasures_status", refusal.ConstraintName);
    }

    /// <summary>
    /// IDN-LIFE-003b AC1: erasure progress is on the erasures row and on no column of
    /// the account.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_003b_AC1_NoColumnOfTheAccountCarriesErasureProgressAsync()
    {
        await using NpgsqlConnection connection = await database.OpenAsync();

        IEnumerable<string> columns = await connection.QueryAsync<string>(
            "SELECT column_name FROM information_schema.columns "
                + "WHERE table_schema = 'janus' AND table_name = 'accounts'");

        Assert.All(columns, column =>
        {
            Assert.DoesNotContain("erasure", column, StringComparison.Ordinal);
            Assert.DoesNotContain("attempt", column, StringComparison.Ordinal);
            Assert.DoesNotContain("subscriber", column, StringComparison.Ordinal);
        });
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    private static ErasureStore Store(JanusDbContext context) => new(context);

    /// <summary>
    /// AUTH-SESS-010 AC3: the subject's sessions end before anything of theirs is
    /// made unreadable, so no request arrives on one afterwards.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_010_AC3_DeletionEndsSessionsBeforeThePersonalDataGoesAsync()
    {
        SubjectId subject = await DeletingAccountAsync();

        await using (JanusDbContext writing = database.Context())
        {
            await new SessionStore(writing, _deployment.Keys, _deployment.Randomness).AddAsync(
                Session.Begin(
                    SessionId.New(TimeProvider.System),
                    subject,
                    new Assurance(AssuranceLevel.Aal1, PhishingResistant: false),
                    new SessionOrigin(
                        "198.51.100.7",
                        new DeviceDescription("Firefox", "Linux"),
                        null),
                    Noon,
                    TimeSpan.FromDays(1),
                    TimeSpan.FromDays(30),
                    satisfiesEveryGate: false),
                OpaqueToken.Draw(_deployment.Randomness).Fingerprint(),
                OpaqueToken.Draw(_deployment.Randomness).Fingerprint(),
                TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (JanusDbContext erasing = database.Context())
        await using (var work = new UnitOfWork(erasing))
        {
            await work.BeginAsync(TestContext.Current.CancellationToken);
            await Eraser(erasing).EraseAsync(
                subject,
                ErasureReason.ErasureRequest,
                Noon.AddHours(1),
                TestContext.Current.CancellationToken);
            await erasing.SaveChangesAsync(TestContext.Current.CancellationToken);
            await work.CommitAsync(TestContext.Current.CancellationToken);
        }

        await using JanusDbContext reading = database.Context();
        SessionRecord ended = await reading.Sessions
            .SingleAsync(session => session.Subject == subject, TestContext.Current.CancellationToken);

        Assert.Equal(Noon.AddHours(1), ended.EndedAt);
    }

    private async ValueTask<SubjectId> DeletingAccountAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await using JanusDbContext deleting = database.Context();
        AccountRecord record = await deleting.Accounts
            .SingleAsync(row => row.Subject == subject, TestContext.Current.CancellationToken);
        record.State = AccountState.Deleting;
        record.DeletingBy = DeletionOrigin.Self;
        record.DeletingSince = Noon;
        await deleting.SaveChangesAsync(TestContext.Current.CancellationToken);

        return subject;
    }

    private async ValueTask<byte[]> StoredImageAsync(SubjectId subject)
    {
        await using JanusDbContext reading = database.Context();

        ProfilePhotoRecord record = await reading.ProfilePhotos
            .SingleAsync(photo => photo.Subject == subject, TestContext.Current.CancellationToken);

        return record.Image ?? [];
    }

    private async ValueTask EraseAsync(SubjectId subject, ErasureReason reason)
    {
        await using JanusDbContext erasing = database.Context();
        await using var work = new UnitOfWork(erasing);

        await work.BeginAsync(TestContext.Current.CancellationToken);
        await Eraser(erasing).EraseAsync(
            subject,
            reason,
            Noon,
            TestContext.Current.CancellationToken);
        await erasing.SaveChangesAsync(TestContext.Current.CancellationToken);
        await work.CommitAsync(TestContext.Current.CancellationToken);
    }
}
