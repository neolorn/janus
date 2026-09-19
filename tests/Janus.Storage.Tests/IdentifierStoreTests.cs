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

    private static readonly byte[] FingerprintKey =
        Encoding.UTF8.GetBytes("the fingerprint key of this deployment");

    private static readonly byte[] Elsewhere =
        Encoding.UTF8.GetBytes("the fingerprint key of another deployment");

    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();
    private readonly Dictionary<int, ReadOnlyMemory<byte>> _versions = [];

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
        SubjectId subject = await AccountAsync();
        IdentifierId id = await WriteAsync(subject, _entered);

        await using JanusDbContext reading = database.Context();
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
        SubjectId subject = await AccountAsync();
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
        SubjectId subject = await AccountAsync();
        IdentifierId id = await WriteAsync(subject, _entered);

        IdentifierRecord stored = await StoredAsync(id);

        Assert.Equal(CanonicalForm.UnicodeVersion, stored.CanonicalisationVersion);
        Assert.Equal(Fingerprint.Compute(Encoding.UTF8.GetBytes(Canonical), FingerprintKey), stored.Fingerprint);
    }

    /// <summary>
    /// IDN-ACCT-006 AC1: an address entered in another casing resolves to the account
    /// that already holds it, so a second registration finds an owner rather than
    /// creating a second account.
    /// </summary>
    [Fact]
    public async Task IDN_ACCT_006_AC1_AnAddressEnteredInAnotherCaseFindsTheAccountHoldingItAsync()
    {
        SubjectId subject = await AccountAsync();
        await WriteAsync(subject, _entered);

        Assert.True(EmailAddress.TryParse(_entered.ToUpperInvariant(), out EmailAddress again));

        await using JanusDbContext reading = database.Context();

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
        SubjectId subject = await AccountAsync();
        await WriteAsync(subject, _entered);

        await using JanusDbContext reading = database.Context();
        var elsewhere = new IdentifierStore(reading, Keys(), Elsewhere, _randomness);

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
    /// PRIV-RIGHT-005a AC12: reading an account's identifiers takes the subject's data
    /// key out once, however many encrypted columns the read decrypts.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_005a_AC12_ReadingAnAccountsIdentifiersUnwrapsItsKeyOnceAsync()
    {
        SubjectId subject = await AccountAsync();
        await WriteAsync(subject, _entered);
        await WriteAsync(subject, Fresh("Basma"));
        await WriteAsync(subject, Fresh("Cyrus"));

        var counting = new CountingVersions(_versions);

        await using JanusDbContext reading = database.Context();
        var store = new IdentifierStore(
            reading,
            new KeyEncryptionKeys(1, counting),
            FingerprintKey,
            _randomness);

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
        SubjectId subject = await AccountAsync();
        await WriteAsync(subject, _entered);
        await EraseAsync(subject);

        await using JanusDbContext reading = database.Context();

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
        SubjectId subject = await AccountAsync();
        IdentifierId id = await WriteAsync(subject, _entered);

        await using (JanusDbContext erasing = database.Context())
        {
            IdentifierRecord row = await erasing.Identifiers
                .SingleAsync(held => held.Id == id, TestContext.Current.CancellationToken);
            row.Fingerprint = Fingerprint.Neutralised();

            await erasing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using JanusDbContext reading = database.Context();

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
        SubjectId subject = await AccountAsync();
        IdentifierId id = await WriteAsync(subject, _entered);

        await using (JanusDbContext verifying = database.Context())
        {
            IdentifierStore store = Store(verifying);
            IdentifierSet set = await store.FindBySubjectAsync(
                subject,
                TestContext.Current.CancellationToken);

            set.Verify(id, Noon.AddHours(1));

            await store.RecordAsync(set, TestContext.Current.CancellationToken);
            await verifying.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using JanusDbContext reading = database.Context();
        IdentifierSet read = await Store(reading).FindBySubjectAsync(
            subject,
            TestContext.Current.CancellationToken);

        Identifier verified = Assert.Single(read.All);

        Assert.Equal(Noon.AddHours(1), verified.VerifiedAt);
        Assert.True(verified.IsPrimary);
    }

    /// <summary>
    /// A value the account changed replaces both stored forms and the fingerprint, so
    /// the old value belongs to nobody and the new one belongs to the account.
    /// </summary>
    [Fact]
    public async Task RecordAsync_AChangedValue_ReplacesBothFormsAndTheFingerprintAsync()
    {
        SubjectId subject = await AccountAsync();
        await WriteAsync(subject, _entered);

        string moved = Fresh("Hana");

        await using (JanusDbContext changing = database.Context())
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

        await using JanusDbContext reading = database.Context();
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
        SubjectId subject = await AccountAsync();
        IdentifierId id = await WriteAsync(subject, _entered);

        IdentifierRecord before = await StoredAsync(id);

        await using (JanusDbContext verifying = database.Context())
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
        SubjectId subject = await AccountAsync();
        await WriteAsync(subject, _entered);
        IdentifierId second = await WriteAsync(subject, Fresh("Basma"));

        await RecordAsync(subject, set => set.Backup(IdentifierKind.Email).UseNamed(second));

        await using (JanusDbContext reading = database.Context())
        {
            IdentifierSet set = await Store(reading).FindBySubjectAsync(
                subject,
                TestContext.Current.CancellationToken);

            BackupSetting setting = set.Backup(IdentifierKind.Email);

            Assert.Equal(BackupRule.Named, setting.Rule);
            Assert.Equal(second, setting.Named);
        }

        await RecordAsync(subject, set => set.Backup(IdentifierKind.Email).UsePrimaryOnly());

        await using (JanusDbContext reading = database.Context())
        {
            IdentifierSet set = await Store(reading).FindBySubjectAsync(
                subject,
                TestContext.Current.CancellationToken);

            Assert.Equal(BackupRule.PrimaryOnly, set.Backup(IdentifierKind.Email).Rule);
        }

        await RecordAsync(subject, set => set.Backup(IdentifierKind.Email).UseEveryVerified());

        await using (JanusDbContext reading = database.Context())
        {
            Assert.False(await reading.BackupSettings
                .AnyAsync(settled => settled.Subject == subject, TestContext.Current.CancellationToken));

            IdentifierSet set = await Store(reading).FindBySubjectAsync(
                subject,
                TestContext.Current.CancellationToken);

            Assert.Equal(BackupRule.AllVerified, set.Backup(IdentifierKind.Email).Rule);
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

        await using JanusDbContext context = database.Context();

        Assert.True(EmailAddress.TryParse(_entered, out EmailAddress address));

        var set = IdentifierSet.Of(
            subject,
            [Identifier.Email(IdentifierId.New(TimeProvider.System), subject, address, _entered, Noon)],
            []);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Store(context).RecordAsync(set, TestContext.Current.CancellationToken));
    }

    /// <inheritdoc/>
    public void Dispose() => _randomness.Dispose();

    private static string Fresh(string person) =>
        person + "." + Guid.NewGuid().ToString("N") + "@Example.COM";

    private static string Canonicalised(string entered)
    {
        Assert.True(EmailAddress.TryParse(entered, out EmailAddress address));

        return address.Value;
    }

    private KeyEncryptionKeys Keys()
    {
        if (_versions.Count == 0)
        {
            byte[] material = new byte[PersonalDataFormat.DataKeyLength];
            _randomness.GetBytes(material);
            _versions[1] = material;
        }

        return new KeyEncryptionKeys(1, _versions);
    }

    private IdentifierStore Store(JanusDbContext context) =>
        new(context, Keys(), FingerprintKey, _randomness);

    private async Task<SubjectId> AccountAsync()
    {
        SubjectId subject = Subjects.New();
        byte[] dataKey = PersonalFieldCipher.NewDataKey(_randomness);

        try
        {
            await using JanusDbContext context = database.Context();

            context.Accounts.Add(new AccountRecord
            {
                Subject = subject,
                CreatedAt = Noon,
                State = AccountState.Active,
            });

            context.SubjectKeys.Add(new SubjectKeyRecord
            {
                Subject = subject,
                FormatMarker = PersonalDataFormat.Marker,
                KeyVersion = 1,
                WrappedKey = PersonalFieldCipher.Wrap(dataKey, Keys().Current.Span),
            });

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }

        return subject;
    }

    private async Task<IdentifierId> WriteAsync(SubjectId subject, string entered)
    {
        Assert.True(EmailAddress.TryParse(entered, out EmailAddress address));

        var id = IdentifierId.New(TimeProvider.System);

        await using JanusDbContext context = database.Context();
        IdentifierStore store = Store(context);

        IdentifierSet set = await store.FindBySubjectAsync(subject, TestContext.Current.CancellationToken);
        set.Add(Identifier.Email(id, subject, address, entered, Noon), maximum: 5);

        await store.RecordAsync(set, TestContext.Current.CancellationToken);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return id;
    }

    private async Task RecordAsync(SubjectId subject, Action<IdentifierSet> change)
    {
        await using JanusDbContext context = database.Context();
        IdentifierStore store = Store(context);

        IdentifierSet set = await store.FindBySubjectAsync(subject, TestContext.Current.CancellationToken);
        change(set);

        await store.RecordAsync(set, TestContext.Current.CancellationToken);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task EraseAsync(SubjectId subject)
    {
        await using JanusDbContext context = database.Context();

        SubjectKeyRecord key = await context.SubjectKeys
            .SingleAsync(held => held.Subject == subject, TestContext.Current.CancellationToken);

        key.FormatMarker = PersonalDataFormat.ErasedMarker;
        key.WrappedKey = new byte[PersonalDataFormat.DataKeyLength];

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<IdentifierRecord> StoredAsync(IdentifierId id)
    {
        await using JanusDbContext context = database.Context();

        return await context.Identifiers
            .SingleAsync(held => held.Id == id, TestContext.Current.CancellationToken);
    }
}
