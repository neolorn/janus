using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication;
using Janus.Authentication.Factors;
using Janus.Authentication.Invitations;
using Janus.Authentication.Mailboxes;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Identity.Accounts;
using Janus.Identity.Audit;
using Janus.Identity.Identifiers;
using Janus.Identity.Preferences;
using Janus.Identity.Profiles;
using Janus.Privacy.Erasures;
using Janus.Privacy.SubjectKeys;
using Janus.Storage.Authentication.Accounts;
using Janus.Storage.Authentication.Factors;
using Janus.Storage.Authentication.Invitations;
using Janus.Storage.Authentication.Mailboxes;
using Janus.Storage.Authentication.Sessions;
using Janus.Storage.Identity.Accounts;
using Janus.Storage.Identity.Audit;
using Janus.Storage.Identity.Identifiers;
using Janus.Storage.Identity.Preferences;
using Janus.Storage.Identity.Profiles;
using Janus.Storage.Privacy.Erasures;
using Janus.Storage.Privacy.Outbox;
using Janus.Storage.Privacy.SubjectKeys;
using Janus.Storage.Settings;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests;

/// <summary>
/// The erasure: one transaction that leaves a subject's fields unrecoverable and every
/// row where it was (PRIV-RIGHT-005, PRIV-RIGHT-005a, IDN-LIFE-003a, IDN-LIFE-003b,
/// IDN-LIFE-014, IDN-ACCT-002, IDN-PRIN-003).
/// </summary>
[Trait("kind", "integration")]
public sealed class SubjectEraserTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new(database);

    private SubjectEraser Eraser(StoreContext context) => new(
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

        await using StoreContext reading = database.Context();
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

        await using (StoreContext erasing = database.Context())
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

        await using StoreContext reading = database.Context();
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
    /// IDN-LIFE-003a AC5: the state change, the key's destruction and the fingerprints'
    /// neutralisation commit together or not at all. The erasure that commits leaves an
    /// erased subject whose key is gone and whose address belongs to nobody; the one
    /// that rolls back leaves a live subject whose key and address read as before.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_003a_AC5_TheStateTheKeyAndTheFingerprintsCommitTogetherAsync()
    {
        (SubjectId erased, string gone) = await AddressedAsync();
        (SubjectId kept, string held) = await AddressedAsync();

        await EraseAsync(erased, ErasureReason.ErasureRequest);

        await using (StoreContext erasing = database.Context())
        await using (var work = new UnitOfWork(erasing))
        {
            await work.BeginAsync(TestContext.Current.CancellationToken);
            await Eraser(erasing).EraseAsync(
                kept,
                ErasureReason.ErasureRequest,
                Noon,
                TestContext.Current.CancellationToken);
            await erasing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();

        Assert.Equal(
            (AccountState.Deleted, false, (SubjectId?)null),
            await StandingAsync(reading, erased, gone));
        Assert.Equal(
            (AccountState.Deleting, true, (SubjectId?)kept),
            await StandingAsync(reading, kept, held));
        Assert.Equal(
            held,
            Assert.Single((await Identifiers(reading).FindBySubjectAsync(
                kept,
                TestContext.Current.CancellationToken)).All).Entered);
    }

    /// <summary>
    /// PRIV-RIGHT-005 AC1, AC6, IDN-LIFE-014 AC1: after the erasure no personal field
    /// is recoverable. The declared profile values go with the key.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_005_AC6_NoPersonalFieldIsRecoverableAfterErasureAsync()
    {
        SubjectId subject = await DeletingAccountAsync();

        await using (StoreContext writing = database.Context())
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

        await using StoreContext reading = database.Context();

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

        await using (StoreContext writing = database.Context())
        {
            await new ProfilePhotoStore(writing, _deployment.Keys, _deployment.Randomness)
                .RecordAsync(ProfilePhoto.Of(subject, image, Noon), TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await EraseAsync(subject, ErasureReason.ErasureRequest);

        await using StoreContext reading = database.Context();

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

        await using (StoreContext writing = database.Context())
        {
            await new ProfilePhotoStore(writing, _deployment.Keys, _deployment.Randomness)
                .RecordAsync(ProfilePhoto.Of(subject, image, Noon), TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        byte[] before = await StoredImageAsync(subject);

        await EraseAsync(subject, ErasureReason.ErasureRequest);

        await using StoreContext reading = database.Context();

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
                "SELECT count(*) FROM identity.accounts WHERE subject = @subject",
                new { subject = subject.Value }));

        Assert.Equal(
            1,
            await connection.ExecuteScalarAsync<int>(
                "SELECT count(*) FROM identity.subject_keys WHERE subject = @subject",
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

        await using StoreContext erasing = database.Context();

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

        await using (StoreContext progressing = database.Context())
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

        await using StoreContext reading = database.Context();
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
                "UPDATE identity.erasures SET status = 'half-done' WHERE subject = @subject",
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
                + "WHERE table_schema = 'identity' AND table_name = 'accounts'");

        Assert.All(columns, column =>
        {
            Assert.DoesNotContain("erasure", column, StringComparison.Ordinal);
            Assert.DoesNotContain("attempt", column, StringComparison.Ordinal);
            Assert.DoesNotContain("subscriber", column, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// IDN-LIFE-014 AC1: after the deletion nothing personal reads back. The rows are
    /// where they were and the key that made sense of them is gone, so every read of
    /// a personal attribute fails rather than returning a value.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_014_AC1_NoPersonalAttributeIsRetrievableAfterDeletionAsync()
    {
        SubjectId subject = await DeletingAccountAsync();
        byte[] image = new byte[256];
        _deployment.Randomness.GetBytes(image);

        await using (StoreContext writing = database.Context())
        {
            Assert.True(LegalName.TryParse("Ahmed Hassan", out LegalName legal));

            var profile = Profile.Empty(subject);
            profile.SetLegalName(legal);
            profile.RecordDateOfBirth(new DateOnly(1990, 4, 17));

            await new ProfileStore(writing, _deployment.Keys, _deployment.Randomness)
                .RecordAsync(profile, TestContext.Current.CancellationToken);

            await new ProfilePhotoStore(writing, _deployment.Keys, _deployment.Randomness)
                .RecordAsync(ProfilePhoto.Of(subject, image, Noon), TestContext.Current.CancellationToken);

            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await EraseAsync(subject, ErasureReason.ErasureRequest);

        await using StoreContext reading = database.Context();

        await Assert.ThrowsAsync<CryptographicException>(async () =>
            await new ProfileStore(reading, _deployment.Keys, _deployment.Randomness)
                .FindBySubjectAsync(subject, TestContext.Current.CancellationToken));

        await Assert.ThrowsAsync<CryptographicException>(async () =>
            await new ProfilePhotoStore(reading, _deployment.Keys, _deployment.Randomness)
                .FindBySubjectAsync(subject, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// PRIV-RIGHT-005 AC1, AC5: a record kept under its own lawful basis is outside
    /// the erasure. The library writes in its own schema alone, so the row, its
    /// count and its total read after the erasure exactly as before it.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_005_AC1_RecordsKeptUnderTheirOwnBasisAreUntouchedAsync()
    {
        SubjectId subject = await DeletingAccountAsync();

        await BusinessRecordsAsync();

        await using (NpgsqlConnection writing = await database.OpenAsync())
        {
            await writing.ExecuteAsync(
                "INSERT INTO host.records (id, subject, locality, amount) "
                    + "VALUES (@id, @subject, 'Al Malaz', 249.50)",
                new { id = Guid.CreateVersion7(), subject = subject.Value });
        }

        await EraseAsync(subject, ErasureReason.ErasureRequest);

        await using NpgsqlConnection connection = await database.OpenAsync();

        Assert.Equal(
            1,
            await connection.ExecuteScalarAsync<int>(
                "SELECT count(*) FROM host.records WHERE subject = @subject",
                new { subject = subject.Value }));

        Assert.Equal(
            249.50m,
            await connection.ExecuteScalarAsync<decimal>(
                "SELECT sum(amount) FROM host.records WHERE subject = @subject",
                new { subject = subject.Value }));

        Assert.Equal(
            "Al Malaz",
            await connection.ExecuteScalarAsync<string>(
                "SELECT locality FROM host.records WHERE subject = @subject",
                new { subject = subject.Value }));
    }

    /// <summary>
    /// PRIV-RIGHT-005 AC5: an aggregate over the non-encrypted columns of a host
    /// record is counted over the host's own rows, and an erasure changes neither how
    /// many there are nor what they add up to.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_005_AC5_AggregatesOverPlainColumnsAreUnchangedByAnErasureAsync()
    {
        SubjectId going = await DeletingAccountAsync();
        SubjectId staying = await _deployment.AccountAsync(Noon);

        await BusinessRecordsAsync();

        await using (NpgsqlConnection writing = await database.OpenAsync())
        {
            await writing.ExecuteAsync(
                "INSERT INTO host.records (id, subject, locality, amount) VALUES "
                    + "(@first, @going, 'Al Malaz', 100.00), "
                    + "(@second, @staying, 'Al Olaya', 50.25)",
                new
                {
                    first = Guid.CreateVersion7(),
                    second = Guid.CreateVersion7(),
                    going = going.Value,
                    staying = staying.Value,
                });
        }

        await using NpgsqlConnection connection = await database.OpenAsync();

        int before = await connection.ExecuteScalarAsync<int>("SELECT count(*) FROM host.records");
        decimal total = await connection.ExecuteScalarAsync<decimal>(
            "SELECT sum(amount) FROM host.records");

        await EraseAsync(going, ErasureReason.ErasureRequest);

        Assert.Equal(
            before,
            await connection.ExecuteScalarAsync<int>("SELECT count(*) FROM host.records"));

        Assert.Equal(
            total,
            await connection.ExecuteScalarAsync<decimal>("SELECT sum(amount) FROM host.records"));
    }

    /// <summary>
    /// PRIV-RIGHT-005 AC2: the trail is not personal data under the subject's key,
    /// so what happened and when it happened still read after the erasure.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_005_AC2_TheAuditTrailSurvivesTheErasureAsync()
    {
        SubjectId subject = await DeletingAccountAsync();
        var deactivated = AuditAction.Parse("identity.account.deactivated");
        DateTimeOffset occurred = Noon.AddHours(-2);

        await using (StoreContext writing = database.Context())
        {
            await new AuditStore(writing, _deployment.Keys, _deployment.Randomness).AppendAsync(
                AuditRecord.Of(
                    new AuditRecordId(Guid.CreateVersion7()),
                    AuditCategory.Security,
                    deactivated,
                    occurred,
                    subject,
                    subject,
                    organization: null),
                TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await EraseAsync(subject, ErasureReason.ErasureRequest);

        await using NpgsqlConnection connection = await database.OpenAsync();

        Assert.Equal(
            deactivated.ToString(),
            await connection.ExecuteScalarAsync<string>(
                "SELECT action FROM identity.audit_records WHERE effective_subject = @subject",
                new { subject = subject.Value }));
    }

    /// <summary>
    /// PRIV-RIGHT-005 AC3: the account row outlives the erasure, so the identifier it
    /// was known by is never handed to a later account.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_005_AC3_TheIdentifierIsNeverReissuedAsync()
    {
        SubjectId subject = await DeletingAccountAsync();

        await EraseAsync(subject, ErasureReason.ErasureRequest);

        await using NpgsqlConnection connection = await database.OpenAsync();

        PostgresException refusal = await Assert.ThrowsAsync<PostgresException>(async () =>
            await connection.ExecuteAsync(
                "INSERT INTO identity.accounts (subject, state, created_at) "
                    + "VALUES (@subject, 'active', @at)",
                new { subject = subject.Value, at = Noon }));

        Assert.Equal("23505", refusal.SqlState);
    }

    /// <summary>
    /// PRIV-RIGHT-005 AC7: the name the erased account went by is held for
    /// <c>retention.consent</c> and is claimable the moment that has run out.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_005_AC7_TheUsernameIsHeldAndThenClaimableAsync()
    {
        SubjectId subject = await DeletingAccountAsync();
        string chosen = "kestrel" + Guid.NewGuid().ToString("N")[..8];

        Assert.True(Username.TryParse(chosen, out Username username));

        await using (StoreContext writing = database.Context())
        {
            IdentifierStore store = Identifiers(writing);
            IdentifierSet set = await store.FindBySubjectAsync(
                subject,
                TestContext.Current.CancellationToken);

            set.Add(
                Identifier.Username(IdentifierId.New(TimeProvider.System), subject, username, Noon),
                maximum: 5);

            await store.RecordAsync(set, TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await EraseAsync(subject, ErasureReason.ErasureRequest);

        TimeSpan held = Janus.Core.Configuration.Settings.RetentionConsent.Default;

        await using StoreContext reading = database.Context();
        IdentifierStore identifiers = Identifiers(reading);

        Assert.True(await identifiers.IsHeldAsync(
            username.Value,
            Noon + held - TimeSpan.FromDays(1),
            TestContext.Current.CancellationToken));

        Assert.False(await identifiers.IsHeldAsync(
            username.Value,
            Noon + held,
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// IDN-ORG-005 AC1, AC2: an organization's erasure runs over its members, and
    /// afterwards the audit records naming the organization are still there to be
    /// queried, with the personal details they carried no longer readable.
    /// </summary>
    [Fact]
    public async Task IDN_ORG_005_AC1_TheOrganizationsTrailSurvivesItsErasureAsync()
    {
        SubjectId member = await DeletingAccountAsync();
        var organization = new OrganizationId(Guid.CreateVersion7());
        var suspended = AuditAction.Parse("identity.account.suspended");

        await using (StoreContext writing = database.Context())
        {
            await new AuditStore(writing, _deployment.Keys, _deployment.Randomness).AppendAsync(
                AuditRecord.Of(
                    new AuditRecordId(Guid.CreateVersion7()),
                    AuditCategory.Security,
                    suspended,
                    Noon.AddHours(-1),
                    member,
                    member,
                    organization,
                    personalDetails: new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                    {
                        ["reason"] = JsonSerializer.SerializeToElement("ahmed@example.com"),
                    }),
                TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await EraseAsync(member, ErasureReason.OrganizationErasure);

        await using NpgsqlConnection connection = await database.OpenAsync();

        Assert.Equal(
            1,
            await connection.ExecuteScalarAsync<int>(
                "SELECT count(*) FROM identity.audit_records WHERE organization = @organization",
                new { organization = organization.Value }));

        await using StoreContext reading = database.Context();

        AuditRecord read = Assert.Single(
            await new AuditStore(reading, _deployment.Keys, _deployment.Randomness)
                .FindBySubjectAsync(member, TestContext.Current.CancellationToken));

        Assert.Equal(organization, read.Organization);
        Assert.Equal(suspended, read.Action);
        Assert.Empty(read.PersonalDetails);
    }

    /// <summary>
    /// REG-ACCT-001 AC3: one erasure leaves every field the table marks Key
    /// unreadable, group by group, and the two preference fields that are not under
    /// the key are retained exactly as chapter 04 says they are.
    /// </summary>
    [Fact]
    public async Task REG_ACCT_001_AC3_EveryKeyFieldIsUnreadableAndTheRestIsAsChapterFourSaysAsync()
    {
        SubjectId subject = await DeletingAccountAsync();

        Assert.True(LegalName.TryParse("Ahmed Hassan", out LegalName legal));
        Assert.True(EmailAddress.TryParse("groups@example.com", out EmailAddress address));

        await using (StoreContext writing = database.Context())
        {
            var profile = Profile.Empty(subject);
            profile.SetLegalName(legal);

            await new ProfileStore(writing, _deployment.Keys, _deployment.Randomness)
                .RecordAsync(profile, TestContext.Current.CancellationToken);

            IdentifierStore identifiers = Identifiers(writing);
            IdentifierSet set = await identifiers.FindBySubjectAsync(
                subject,
                TestContext.Current.CancellationToken);

            set.Add(
                Identifier.Email(
                    IdentifierId.New(TimeProvider.System),
                    subject,
                    address,
                    "groups@example.com",
                    Noon),
                maximum: 5);

            await identifiers.RecordAsync(set, TestContext.Current.CancellationToken);

            var preferences = PreferenceSet.Empty(subject);
            preferences.SetLanguage("ar-EG");
            preferences.SetTimeZone("Africa/Cairo");

            await new PreferenceStore(writing, _deployment.Keys, _deployment.Randomness)
                .RecordAsync(preferences, TestContext.Current.CancellationToken);

            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await EraseAsync(subject, ErasureReason.ErasureRequest);

        await using StoreContext reading = database.Context();

        await Assert.ThrowsAsync<CryptographicException>(async () =>
            await new ProfileStore(reading, _deployment.Keys, _deployment.Randomness)
                .FindBySubjectAsync(subject, TestContext.Current.CancellationToken));

        await Assert.ThrowsAsync<CryptographicException>(async () =>
            await Identifiers(reading).FindBySubjectAsync(
                subject,
                TestContext.Current.CancellationToken));

        PreferenceSet kept = await new PreferenceStore(reading, _deployment.Keys, _deployment.Randomness)
            .FindBySubjectAsync(subject, TestContext.Current.CancellationToken);

        Assert.Equal("ar-EG", kept.Language);
        Assert.Equal("Africa/Cairo", kept.TimeZone);
        Assert.Empty(kept.Values);
    }

    /// <summary>
    /// PRIV-RIGHT-005b AC5: the photo is in the database beside everything else held
    /// under the subject key, so one erasure reaches all of it at once and no store
    /// is left readable after another has been cleared.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_005b_AC5_OneErasureReachesThePhotoAndEveryOtherStoreAsync()
    {
        SubjectId subject = await DeletingAccountAsync();
        byte[] image = new byte[256];
        _deployment.Randomness.GetBytes(image);

        await using (StoreContext writing = database.Context())
        {
            Assert.True(LegalName.TryParse("Ahmed Hassan", out LegalName legal));

            var profile = Profile.Empty(subject);
            profile.SetLegalName(legal);

            await new ProfileStore(writing, _deployment.Keys, _deployment.Randomness)
                .RecordAsync(profile, TestContext.Current.CancellationToken);

            await new ProfilePhotoStore(writing, _deployment.Keys, _deployment.Randomness)
                .RecordAsync(ProfilePhoto.Of(subject, image, Noon), TestContext.Current.CancellationToken);

            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await EraseAsync(subject, ErasureReason.ErasureRequest);

        await using StoreContext reading = database.Context();

        await Assert.ThrowsAsync<CryptographicException>(async () =>
            await new ProfilePhotoStore(reading, _deployment.Keys, _deployment.Randomness)
                .FindBySubjectAsync(subject, TestContext.Current.CancellationToken));

        await Assert.ThrowsAsync<CryptographicException>(async () =>
            await new ProfileStore(reading, _deployment.Keys, _deployment.Randomness)
                .FindBySubjectAsync(subject, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// PRIV-RIGHT-005c and REG-MAIL-003: the address of a mailbox the subject held is
    /// theirs, so its fingerprint is neutralised with the rest and the row stays where
    /// it is, is no longer read, and leaves the address free for a later invitation.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_005c_TheAddressOfAMailboxGoesWithItsHolderAsync()
    {
        SubjectId subject = await DeletingAccountAsync();
        var mailbox = Mailbox.Reserved("erased@example.test", Noon);

        mailbox.Hold(subject);

        await using (StoreContext writing = database.Context())
        {
            await Mailboxes(writing).AddAsync(mailbox, TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await EraseAsync(subject, ErasureReason.ErasureRequest);

        await using StoreContext reading = database.Context();

        MailboxRecord row = await reading.Mailboxes
            .SingleAsync(held => held.Id == mailbox.Id.Value, TestContext.Current.CancellationToken);

        Assert.True(Fingerprint.IsNeutralised(row.Fingerprint));
        Assert.Empty(await Mailboxes(reading).AllAsync(TestContext.Current.CancellationToken));

        await Mailboxes(reading).AddAsync(
            Mailbox.Reserved("erased@example.test", Noon),
            TestContext.Current.CancellationToken);
        await reading.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// IDN-LIFE-012a and PRIV-RIGHT-005c: the provider's subject identifier a linked
    /// identity is found by is the holder's, so its fingerprint is neutralised with the
    /// rest, the provider's events find the account no longer, and the identity can be
    /// linked afresh.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_012a_TheProvidersSubjectOfALinkedIdentityGoesWithItsHolderAsync()
    {
        string providerSubject = Guid.NewGuid().ToString("N");
        SubjectId subject = await DeletingAccountAsync();

        Assert.True(CredentialLabel.TryParse("Linked", out CredentialLabel label));

        var linked = Authenticator.Linked(
            AuthenticatorId.New(TimeProvider.System),
            subject,
            Factor.Google,
            label,
            Noon);

        await using (StoreContext writing = database.Context())
        {
            await Authenticators(writing).LinkAsync(linked, providerSubject, TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await EraseAsync(subject, ErasureReason.ErasureRequest);

        await using StoreContext reading = database.Context();

        AuthenticatorRecord row = await reading.Authenticators
            .SingleAsync(held => held.Id == linked.Id, TestContext.Current.CancellationToken);

        Assert.True(Fingerprint.IsNeutralised(row.ProviderSubject!));
        Assert.Null(await Authenticators(reading).ByProviderAsync(
            Factor.Google,
            providerSubject,
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// PRIV-RIGHT-005a: what an invitation attached to the subject binds is forgotten
    /// with the rest of their fields, while the row still names who invited into what;
    /// an invitation attached to nobody keeps what it binds until it is used or expires.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_005a_WhatAnAttachedInvitationBindsGoesWithTheSubjectAsync()
    {
        SubjectId subject = await DeletingAccountAsync();
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        SubjectId inviter = await _deployment.AccountAsync(Noon);
        Invitation attached = Invited(organization, inviter);
        Invitation standing = Invited(organization, inviter);

        attached.AttachTo(subject, Noon);

        await using (StoreContext writing = database.Context())
        {
            await Invitations(writing).AddAsync(attached, TestContext.Current.CancellationToken);
            await Invitations(writing).AddAsync(standing, TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await EraseAsync(subject, ErasureReason.ErasureRequest);

        await using StoreContext reading = database.Context();

        InvitationRecord forgotten = await reading.Invitations
            .SingleAsync(row => row.Id == attached.Id, TestContext.Current.CancellationToken);
        InvitationRecord kept = await reading.Invitations
            .SingleAsync(row => row.Id == standing.Id, TestContext.Current.CancellationToken);

        Assert.Equal((null, null, null), (forgotten.EncryptedIdentifiers, forgotten.WrappedKey, forgotten.KeyVersion));
        Assert.Equal((subject, inviter, organization), (forgotten.Invitee, forgotten.Inviter, forgotten.Organization));
        Assert.NotNull(kept.EncryptedIdentifiers);
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    /// <summary>
    /// PRIV-RIGHT-005 AC4: the account directory the photo endpoint reads through
    /// answers an erased subject as it answers one that never set a photo, so the row
    /// that stays where it is is never met as a failure on bytes nothing can read
    /// (D-157).
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_005_AC4_TheDirectoryShowsAnErasedSubjectAsOneWithNoPhotoAsync()
    {
        SubjectId subject = await DeletingAccountAsync();
        byte[] image = new byte[256];
        _deployment.Randomness.GetBytes(image);

        await using (StoreContext writing = database.Context())
        {
            await new ProfilePhotoStore(writing, _deployment.Keys, _deployment.Randomness)
                .RecordAsync(ProfilePhoto.Of(subject, image, Noon), TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (StoreContext shown = database.Context())
        {
            Assert.Equal(
                image,
                (await Directory(shown).PhotoAsync(subject, TestContext.Current.CancellationToken)).ToArray());
        }

        await EraseAsync(subject, ErasureReason.ErasureRequest);

        await using StoreContext reading = database.Context();

        Assert.True(await reading.ProfilePhotos
            .AnyAsync(photo => photo.Subject == subject, TestContext.Current.CancellationToken));

        Assert.True(
            (await Directory(reading).PhotoAsync(subject, TestContext.Current.CancellationToken)).IsEmpty);
    }

    private AccountDirectory Directory(StoreContext context) => new(
        context,
        new AccountStore(context),
        new ProfileStore(context, _deployment.Keys, _deployment.Randomness),
        new ProfilePhotoStore(context, _deployment.Keys, _deployment.Randomness),
        new SubjectKeyStore(context, _deployment.Keys, _deployment.Randomness),
        new PreferenceStore(context, _deployment.Keys, _deployment.Randomness),
        PreferenceDeclarations.None,
        new OutboxStore(context, new FixedTime(Noon)));

    private static ErasureStore Store(StoreContext context) => new(context);

    private AuthenticatorStore Authenticators(StoreContext context) =>
        new(context, _deployment.Keys, _deployment.Randomness, Deployment.FingerprintKey);

    private IdentifierStore Identifiers(StoreContext context) =>
        new(context, _deployment.Keys, Deployment.FingerprintKey, _deployment.Randomness);

    private static Invitation Invited(OrganizationId organization, SubjectId inviter) =>
        Invitation.Issued(
            InvitationId.New(TimeProvider.System),
            organization,
            inviter,
            new InvitedIdentifiers("invited@example.test", Phone: null, CorporateEmail: null),
            [],
            [],
            mailbox: null,
            OpaqueToken.Of(Guid.NewGuid().ToString("N")).Fingerprint(),
            Noon,
            TimeSpan.FromDays(7));

    private InvitationStore Invitations(StoreContext context) =>
        new(context, _deployment.Keys, _deployment.Randomness);

    private MailboxStore Mailboxes(StoreContext context) =>
        new(context, _deployment.Keys, Deployment.FingerprintKey, _deployment.Randomness);

    // The deployment's own records, which the library neither maps nor writes: a
    // record names its subject and outlives the subject's erasure (PRIV-RIGHT-005).
    private async ValueTask BusinessRecordsAsync()
    {
        await using NpgsqlConnection connection = await database.OpenAsync();

        await connection.ExecuteAsync(
            """
            CREATE SCHEMA IF NOT EXISTS host;
            CREATE TABLE IF NOT EXISTS host.records (
                id uuid PRIMARY KEY,
                subject uuid NOT NULL,
                locality text NOT NULL,
                amount numeric(10, 2) NOT NULL);
            """);
    }

    /// <summary>
    /// AUTH-SESS-010 AC3: the subject's sessions end before anything of theirs is
    /// made unreadable, so no request arrives on one afterwards.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_010_AC3_DeletionEndsSessionsBeforeThePersonalDataGoesAsync()
    {
        SubjectId subject = await DeletingAccountAsync();

        await using (StoreContext writing = database.Context())
        {
            await new SessionStore(writing, _deployment.Keys, _deployment.Randomness).AddAsync(
                Session.Begin(
                    SessionId.New(TimeProvider.System),
                    subject,
                    new Assurance(AssuranceLevel.Aal1, PhishingResistant: false),
                    new SessionOrigin("198.51.100.7", new DeviceDescription("Firefox", "Linux")),
                    Noon,
                    TimeSpan.FromDays(1),
                    TimeSpan.FromDays(30),
                    satisfiesEveryGate: false),
                OpaqueToken.Draw(_deployment.Randomness).Fingerprint(),
                OpaqueToken.Draw(_deployment.Randomness).Fingerprint(),
                TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (StoreContext erasing = database.Context())
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

        await using StoreContext reading = database.Context();
        SessionRecord ended = await reading.Sessions
            .SingleAsync(session => session.Subject == subject, TestContext.Current.CancellationToken);

        Assert.Equal(Noon.AddHours(1), ended.EndedAt);
    }

    private async ValueTask<SubjectId> DeletingAccountAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await using StoreContext deleting = database.Context();
        AccountRecord record = await deleting.Accounts
            .SingleAsync(row => row.Subject == subject, TestContext.Current.CancellationToken);
        record.State = AccountState.Deleting;
        record.DeletingBy = DeletionOrigin.Self;
        record.DeletingSince = Noon;
        await deleting.SaveChangesAsync(TestContext.Current.CancellationToken);

        return subject;
    }

    // An account on its way out that holds one verified email, and that email.
    private async ValueTask<(SubjectId Subject, string Address)> AddressedAsync()
    {
        SubjectId subject = await DeletingAccountAsync();
        string entered = "erasing-" + Guid.NewGuid().ToString("N")[..12] + "@example.test";

        Assert.True(EmailAddress.TryParse(entered, out EmailAddress address));

        await using StoreContext writing = database.Context();
        IdentifierStore store = Identifiers(writing);
        IdentifierSet set = await store.FindBySubjectAsync(subject, TestContext.Current.CancellationToken);
        var email = Identifier.Email(IdentifierId.New(TimeProvider.System), subject, address, entered, Noon);

        set.Add(email, maximum: 5);
        set.Verify(email.Id, Noon);

        await store.RecordAsync(set, TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (subject, address.Value);
    }

    // The account's state, whether its key still unwraps, and who owns the address.
    private async ValueTask<(AccountState State, bool Readable, SubjectId? Owner)> StandingAsync(
        StoreContext reading,
        SubjectId subject,
        string address)
    {
        AccountRecord account = await reading.Accounts
            .SingleAsync(row => row.Subject == subject, TestContext.Current.CancellationToken);
        SubjectKeyRecord key = await reading.SubjectKeys
            .SingleAsync(row => row.Subject == subject, TestContext.Current.CancellationToken);

        return (
            account.State,
            key.FormatMarker == PersonalDataFormat.Marker,
            await Identifiers(reading).FindOwnerAsync(
                IdentifierKind.Email,
                address,
                TestContext.Current.CancellationToken));
    }

    private async ValueTask<byte[]> StoredImageAsync(SubjectId subject)
    {
        await using StoreContext reading = database.Context();

        ProfilePhotoRecord record = await reading.ProfilePhotos
            .SingleAsync(photo => photo.Subject == subject, TestContext.Current.CancellationToken);

        return record.Image ?? [];
    }

    private async ValueTask EraseAsync(SubjectId subject, ErasureReason reason)
    {
        await using StoreContext erasing = database.Context();
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

    /// <summary>
    /// IDN-LIFE-014 AC2: the trail survives the deletion. What happened and when it
    /// happened are columns of the row, so they read as they did; only what the
    /// subject's key protected is gone.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_014_AC2_TheTrailStillShowsWhatHappenedAndWhenAsync()
    {
        SubjectId subject = await DeletingAccountAsync();
        var suspended = AuditAction.Parse("identity.account.suspended");
        DateTimeOffset occurred = Noon.AddHours(-3);

        await using (StoreContext writing = database.Context())
        {
            await new AuditStore(writing, _deployment.Keys, _deployment.Randomness).AppendAsync(
                AuditRecord.Of(
                    new AuditRecordId(Guid.CreateVersion7()),
                    AuditCategory.Security,
                    suspended,
                    occurred,
                    subject,
                    subject,
                    organization: null),
                TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await EraseAsync(subject, ErasureReason.ErasureRequest);

        await using NpgsqlConnection connection = await database.OpenAsync();

        string? action = await connection.ExecuteScalarAsync<string>(
            "SELECT action FROM identity.audit_records WHERE effective_subject = @subject",
            new { subject = subject.Value });

        DateTime at = await connection.ExecuteScalarAsync<DateTime>(
            "SELECT occurred_at FROM identity.audit_records WHERE effective_subject = @subject",
            new { subject = subject.Value });

        Assert.Equal(suspended.ToString(), action);
        Assert.Equal(
            occurred.UtcDateTime,
            DateTime.SpecifyKind(at, DateTimeKind.Utc),
            TimeSpan.FromMilliseconds(1));
    }

    /// <summary>
    /// IDN-LIFE-014 AC3: the account row outlives its own erasure, so the identifier
    /// it was known by is held against every later account and never handed out a
    /// second time.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_014_AC3_TheSubjectIdentifierIsNotReissuedAsync()
    {
        SubjectId subject = await DeletingAccountAsync();

        await EraseAsync(subject, ErasureReason.ErasureRequest);

        await using NpgsqlConnection connection = await database.OpenAsync();

        PostgresException refusal = await Assert.ThrowsAsync<PostgresException>(async () =>
            await connection.ExecuteAsync(
                "INSERT INTO identity.accounts (subject, state, created_at) "
                    + "VALUES (@subject, 'active', @at)",
                new { subject = subject.Value, at = Noon }));

        Assert.Equal("23505", refusal.SqlState);
    }
}
