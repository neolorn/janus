using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Identity.Identifiers;
using Janus.Privacy.SubjectKeys;
using Janus.Storage.Identity.Accounts;
using Janus.Storage.Identity.Identifiers;
using Janus.Storage.Privacy.SubjectKeys;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Janus.Storage.Tests;

/// <summary>
/// An account's identifiers as the <c>identifiers</c> and
/// <c>identifier_backup_settings</c> rows carry them (IDN-ACCT-004, IDN-ACCT-006,
/// REG-IDENT-002, PRIV-RIGHT-005a, PRIV-RIGHT-005c).
/// </summary>
/// <remarks>
/// The port implementation is tested against the aggregate it translates, with the real
/// database (D-156). Each test draws its own subject and its own key-encryption key, so
/// what one test writes is unreadable to another.
/// </remarks>
[Trait("kind", "integration")]
public sealed class IdentifierStoreTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private static readonly byte[] Elsewhere =
        Encoding.UTF8.GetBytes("the fingerprint key of another deployment");

    private readonly Deployment _deployment = new(database);

    // One live fingerprint of a kind exists across the deployment (REG-SESS-005), so no
    // two tests of this class may enter the same address.
    private readonly string _entered = Fresh("Ahmed");

    private string Canonical => Canonicalised(_entered);

    /// <summary>
    /// IDN-ACCT-004 AC2: the row keeps the form the person entered as well as the form
    /// it is compared under, and a read gives back both, neither having become the
    /// other.
    /// </summary>
    [Fact]
    public async Task IDN_ACCT_004_AC2_TheFormThePersonEnteredIsWhatComesBackAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        IdentifierId id = await WriteAsync(subject, _entered);

        await using StoreContext reading = database.Context();
        IdentifierSet set = await Store(reading).FindBySubjectAsync(
            subject,
            TestContext.Current.CancellationToken);

        Identifier read = Assert.Single(set.All);

        Assert.Equal(id, read.Id);
        Assert.Equal(_entered, read.Entered);
        Assert.Equal(Canonical, read.Canonical);
        Assert.NotEqual(_entered, read.Canonical);
        Assert.Equal(IdentifierKind.Email, read.Kind);
        Assert.Equal(Noon, read.AddedAt);
        Assert.False(read.IsVerified);
        Assert.False(read.IsPrimary);
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC8: what the two columns hold is the scheme's own shape, and
    /// neither form of the identifier appears in them, so a dump without the
    /// key-encryption key yields nothing.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_005a_AC8_NoFormOfAnIdentifierIsInTheDatabaseInPlainAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        IdentifierId id = await WriteAsync(subject, _entered);

        IdentifierRecord stored = await StoredAsync(id);

        Assert.Equal(PersonalDataFormat.Marker, stored.Entered[0]);
        Assert.Equal(PersonalDataFormat.Marker, stored.Canonical[0]);

        Assert.Equal(-1, stored.Entered.AsSpan().IndexOf(Encoding.UTF8.GetBytes(_entered)));
        Assert.Equal(-1, stored.Entered.AsSpan().IndexOf(Encoding.UTF8.GetBytes(Canonical)));
        Assert.Equal(-1, stored.Canonical.AsSpan().IndexOf(Encoding.UTF8.GetBytes(Canonical)));
    }

    /// <summary>
    /// PRIV-RIGHT-005c AC4: the Unicode version the canonical form was computed under is
    /// stored beside the fingerprint, so a later version re-derives rather than
    /// mismatching silently.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_005c_AC4_TheCanonicalisationVersionIsStoredBesideTheFingerprintAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        IdentifierId id = await WriteAsync(subject, _entered);

        IdentifierRecord stored = await StoredAsync(id);

        Assert.Equal(CanonicalForm.UnicodeVersion, stored.CanonicalisationVersion);
        Assert.Equal(Fingerprint.Compute(Encoding.UTF8.GetBytes(Canonical), Deployment.FingerprintKey), stored.Fingerprint);
    }

    /// <summary>
    /// IDN-ACCT-006 AC1: an address entered in another casing resolves to the account
    /// that already holds it, so a second registration finds an owner rather than
    /// creating a second account.
    /// </summary>
    [Fact]
    public async Task IDN_ACCT_006_AC1_AnAddressEnteredInAnotherCaseFindsTheAccountHoldingItAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        await WriteAsync(subject, _entered);

        Assert.True(EmailAddress.TryParse(_entered.ToUpperInvariant(), out EmailAddress again));

        await using StoreContext reading = database.Context();

        Assert.Equal(
            subject,
            await Store(reading).FindOwnerAsync(
                IdentifierKind.Email,
                again.Value,
                TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// PRIV-RIGHT-005c AC1: what the lookup matches on is the keyed fingerprint, so the
    /// same value under another deployment's fingerprint key finds nobody.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_005c_AC1_DuplicateDetectionMatchesOnTheKeyedFingerprintAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        await WriteAsync(subject, _entered);

        await using StoreContext reading = database.Context();
        var elsewhere = new IdentifierStore(
            reading,
            _deployment.Keys,
            Elsewhere,
            _deployment.Randomness);

        Assert.Null(await elsewhere.FindOwnerAsync(
            IdentifierKind.Email,
            Canonical,
            TestContext.Current.CancellationToken));

        Assert.Equal(
            subject,
            await Store(reading).FindOwnerAsync(
                IdentifierKind.Email,
                Canonical,
                TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// REG-IDENT-006 AC2: the value a removal gave up belongs to nobody and is still
    /// out of reach, matched on the keyed fingerprint, until the moment the undo stops
    /// working.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_IDENT_006_AC2_AGivenUpValueIsOutOfReachUntilTheUndoLapsesAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        string given = Fresh("Hana");

        IdentifierId first = await WriteAsync(subject, _entered);
        IdentifierId second = await WriteAsync(subject, given);
        DateTimeOffset lapses = Noon.AddHours(72);

        await using (StoreContext giving = database.Context())
        {
            IdentifierStore store = Store(giving);
            IdentifierSet set = await store.FindBySubjectAsync(
                subject,
                TestContext.Current.CancellationToken);

            // The first is verified before the second, so the primary of the kind is the
            // one that stays and the second is free to leave.
            set.Verify(first, Noon.AddHours(1));
            set.Verify(second, Noon.AddHours(1));

            await store.RecordRemovalAsync(
                IdentifierRemoval.Of(set.Remove(second), Noon.AddHours(2), lapses, [7, 3, 9]),
                TestContext.Current.CancellationToken);

            await store.RecordAsync(set, TestContext.Current.CancellationToken);
            await giving.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();
        IdentifierStore held = Store(reading);

        Assert.Null(await held.FindOwnerAsync(
            IdentifierKind.Email,
            Canonicalised(given),
            TestContext.Current.CancellationToken));

        Assert.True(await held.IsReservedAsync(
            IdentifierKind.Email,
            Canonicalised(given),
            lapses.AddSeconds(-1),
            TestContext.Current.CancellationToken));

        Assert.False(await held.IsReservedAsync(
            IdentifierKind.Email,
            Canonicalised(given),
            lapses,
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC12: reading an account's identifiers takes the subject's data
    /// key out once, however many encrypted columns the read decrypts.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_005a_AC12_ReadingAnAccountsIdentifiersUnwrapsItsKeyOnceAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        await WriteAsync(subject, _entered);
        await WriteAsync(subject, Fresh("Basma"));
        await WriteAsync(subject, Fresh("Cyrus"));

        var counting = new CountingVersions(_deployment.Versions);

        await using StoreContext reading = database.Context();
        var store = new IdentifierStore(
            reading,
            new KeyEncryptionKeys(1, counting),
            Deployment.FingerprintKey,
            _deployment.Randomness);

        IdentifierSet set = await store.FindBySubjectAsync(subject, TestContext.Current.CancellationToken);

        Assert.Equal(3, set.All.Count);
        Assert.Equal(1, counting.Reads);
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC9: once the subject's key is erased, the identifiers it wrote
    /// are unreadable, and the read says so rather than yielding whatever the bytes
    /// happen to be.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_005a_AC9_TheIdentifiersOfAnErasedSubjectAreNotReadableAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        await WriteAsync(subject, _entered);
        await _deployment.EraseAsync(subject);

        await using StoreContext reading = database.Context();

        await Assert.ThrowsAsync<CryptographicException>(async () =>
            await Store(reading).FindBySubjectAsync(subject, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// PRIV-RIGHT-005c AC5: the row persists after erasure and the neutralised
    /// fingerprint matches no lookup, so the identifier belongs to nobody.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_005c_AC5_ANeutralisedFingerprintBelongsToNobodyAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        IdentifierId id = await WriteAsync(subject, _entered);

        await using (StoreContext erasing = database.Context())
        {
            IdentifierRecord row = await erasing.Identifiers
                .SingleAsync(held => held.Id == id, TestContext.Current.CancellationToken);
            row.Fingerprint = Fingerprint.Neutralised();

            await erasing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();

        Assert.Null(await Store(reading).FindOwnerAsync(
            IdentifierKind.Email,
            Canonical,
            TestContext.Current.CancellationToken));

        Assert.True(await reading.Identifiers
            .AnyAsync(held => held.Id == id, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// A verification and the primary role the account moved with it reach the rows.
    /// </summary>
    [Fact]
    public async Task RecordAsync_AVerificationAndThePrimaryRole_ReachTheRowsAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        IdentifierId id = await WriteAsync(subject, _entered);

        await using (StoreContext verifying = database.Context())
        {
            IdentifierStore store = Store(verifying);
            IdentifierSet set = await store.FindBySubjectAsync(
                subject,
                TestContext.Current.CancellationToken);

            set.Verify(id, Noon.AddHours(1));

            await store.RecordAsync(set, TestContext.Current.CancellationToken);
            await verifying.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();
        IdentifierSet read = await Store(reading).FindBySubjectAsync(
            subject,
            TestContext.Current.CancellationToken);

        Identifier verified = Assert.Single(read.All);

        Assert.Equal(Noon.AddHours(1), verified.VerifiedAt);
        Assert.True(verified.IsPrimary);
    }

    /// <summary>
    /// REG-MAIL-001: the personal email a membership keeps reaches its row and reads
    /// back kept, and the security-notice set reaches it at the primary-only setting.
    /// </summary>
    [Fact]
    public async Task REG_MAIL_001_ThePersonalEmailAMembershipKeepsReadsBackKeptAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        IdentifierId personal = await WriteAsync(subject, _entered);
        IdentifierId corporate = await WriteAsync(subject, Fresh("Corporate"));

        await RecordAsync(subject, set =>
        {
            set.Verify(personal, Noon);
            set.Verify(corporate, Noon);
            set.MakePrimary(corporate);
            set.KeepPersonal(personal);
            set.Backup(IdentifierKind.Email).UsePrimaryOnly();
        });

        await using StoreContext reading = database.Context();
        IdentifierSet read = await Store(reading).FindBySubjectAsync(
            subject,
            TestContext.Current.CancellationToken);

        Assert.True(read.Find(personal)!.IsPersonal);
        Assert.False(read.Find(corporate)!.IsPersonal);
        Assert.Equal(
            [personal, corporate],
            read.SecurityNoticeSet(IdentifierKind.Email).Select(identifier => identifier.Id));
        Assert.True((await StoredAsync(personal)).IsPersonal);
    }

    /// <summary>
    /// A value the account changed replaces both stored forms and the fingerprint, so
    /// the old value belongs to nobody and the new one belongs to the account.
    /// </summary>
    [Fact]
    public async Task RecordAsync_AChangedValue_ReplacesBothFormsAndTheFingerprintAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        await WriteAsync(subject, _entered);

        string moved = Fresh("Hana");

        await using (StoreContext changing = database.Context())
        {
            IdentifierStore store = Store(changing);
            IdentifierSet set = await store.FindBySubjectAsync(
                subject,
                TestContext.Current.CancellationToken);

            Assert.True(EmailAddress.TryParse(moved, out EmailAddress parsed));
            set.All[0].Change(moved, parsed.Value);

            await store.RecordAsync(set, TestContext.Current.CancellationToken);
            await changing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();
        IdentifierStore finding = Store(reading);

        Identifier read = Assert.Single(
            (await finding.FindBySubjectAsync(subject, TestContext.Current.CancellationToken)).All);

        Assert.Equal(moved, read.Entered);
        Assert.Equal(Canonicalised(moved), read.Canonical);

        Assert.Null(await finding.FindOwnerAsync(
            IdentifierKind.Email,
            Canonical,
            TestContext.Current.CancellationToken));

        Assert.Equal(
            subject,
            await finding.FindOwnerAsync(
                IdentifierKind.Email,
                Canonicalised(moved),
                TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// An identifier whose value the account did not touch keeps the bytes it was
    /// written with, so a save draws no new initialisation vector for a column nothing
    /// changed.
    /// </summary>
    [Fact]
    public async Task RecordAsync_AnIdentifierThatDidNotChange_LeavesItsColumnsAsTheyWereAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        IdentifierId id = await WriteAsync(subject, _entered);

        IdentifierRecord before = await StoredAsync(id);

        await using (StoreContext verifying = database.Context())
        {
            IdentifierStore store = Store(verifying);
            IdentifierSet set = await store.FindBySubjectAsync(
                subject,
                TestContext.Current.CancellationToken);

            set.Verify(id, Noon.AddHours(1));

            await store.RecordAsync(set, TestContext.Current.CancellationToken);
            await verifying.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        IdentifierRecord after = await StoredAsync(id);

        Assert.Equal(before.Entered, after.Entered);
        Assert.Equal(before.Canonical, after.Canonical);
        Assert.Equal(before.Fingerprint, after.Fingerprint);
    }

    /// <summary>
    /// A kind the account settled on something other than the default carries a row,
    /// and one it returned to the default gives that row up, the default being what a
    /// kind with no row reads as.
    /// </summary>
    [Fact]
    public async Task RecordAsync_ABackupSettingAndItsReturnToTheDefault_WriteAndGiveUpTheRowAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        await WriteAsync(subject, _entered);
        IdentifierId second = await WriteAsync(subject, Fresh("Basma"));

        await RecordAsync(subject, set => set.Backup(IdentifierKind.Email).UseNamed(second));

        await using (StoreContext reading = database.Context())
        {
            IdentifierSet set = await Store(reading).FindBySubjectAsync(
                subject,
                TestContext.Current.CancellationToken);

            BackupSetting setting = set.Backup(IdentifierKind.Email);

            Assert.Equal(BackupChoice.Named, setting.Rule);
            Assert.Equal(second, setting.Named);
        }

        await RecordAsync(subject, set => set.Backup(IdentifierKind.Email).UsePrimaryOnly());

        await using (StoreContext reading = database.Context())
        {
            IdentifierSet set = await Store(reading).FindBySubjectAsync(
                subject,
                TestContext.Current.CancellationToken);

            Assert.Equal(BackupChoice.PrimaryOnly, set.Backup(IdentifierKind.Email).Rule);
        }

        await RecordAsync(subject, set => set.Backup(IdentifierKind.Email).UseEveryVerified());

        await using (StoreContext reading = database.Context())
        {
            Assert.False(await reading.BackupSettings
                .AnyAsync(settled => settled.Subject == subject, TestContext.Current.CancellationToken));

            IdentifierSet set = await Store(reading).FindBySubjectAsync(
                subject,
                TestContext.Current.CancellationToken);

            Assert.Equal(BackupChoice.AllVerified, set.Backup(IdentifierKind.Email).Rule);
        }
    }

    /// <summary>
    /// A subject with no key is a fault in the caller: the key is written in the
    /// transaction that creates the subject, so nothing is written under one that does
    /// not exist.
    /// </summary>
    [Fact]
    public async Task RecordAsync_ASubjectWithNoKey_ThrowsAsync()
    {
        SubjectId subject = Subjects.New();

        await using StoreContext context = database.Context();

        Assert.True(EmailAddress.TryParse(_entered, out EmailAddress address));

        var set = IdentifierSet.Of(
            subject,
            [Identifier.Email(IdentifierId.New(TimeProvider.System), subject, address, _entered, Noon)],
            []);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Store(context).RecordAsync(set, TestContext.Current.CancellationToken));
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    private IdentifierStore Store(StoreContext context) =>
        new(context, _deployment.Keys, Deployment.FingerprintKey, _deployment.Randomness);

    private static string Fresh(string person) =>
        person + "." + Guid.NewGuid().ToString("N") + "@Example.COM";

    private static string Canonicalised(string entered)
    {
        Assert.True(EmailAddress.TryParse(entered, out EmailAddress address));

        return address.Value;
    }

    private async Task<IdentifierId> WriteAsync(SubjectId subject, string entered)
    {
        Assert.True(EmailAddress.TryParse(entered, out EmailAddress address));

        var id = IdentifierId.New(TimeProvider.System);

        await using StoreContext context = database.Context();
        IdentifierStore store = Store(context);

        IdentifierSet set = await store.FindBySubjectAsync(subject, TestContext.Current.CancellationToken);
        set.Add(Identifier.Email(id, subject, address, entered, Noon), maximum: 5);

        await store.RecordAsync(set, TestContext.Current.CancellationToken);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return id;
    }

    private async Task RecordAsync(SubjectId subject, Action<IdentifierSet> change)
    {
        await using StoreContext context = database.Context();
        IdentifierStore store = Store(context);

        IdentifierSet set = await store.FindBySubjectAsync(subject, TestContext.Current.CancellationToken);
        change(set);

        await store.RecordAsync(set, TestContext.Current.CancellationToken);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task EraseAsync(SubjectId subject)
    {
        await using StoreContext context = database.Context();

        SubjectKeyRecord key = await context.SubjectKeys
            .SingleAsync(held => held.Subject == subject, TestContext.Current.CancellationToken);

        key.FormatMarker = PersonalDataFormat.ErasedMarker;
        key.WrappedKey = new byte[PersonalDataFormat.DataKeyLength];

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<IdentifierRecord> StoredAsync(IdentifierId id)
    {
        await using StoreContext context = database.Context();

        return await context.Identifiers
            .SingleAsync(held => held.Id == id, TestContext.Current.CancellationToken);
    }
}
