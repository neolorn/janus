using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Mailboxes;
using Janus.Core;
using Janus.Storage.Authentication.Mailboxes;
using Janus.Storage.Authentication.Sessions;
using Janus.Storage.Identity.Accounts;
using Janus.Storage.Identity.Organizations;
using Janus.Storage.Privacy.Erasures;
using Janus.Storage.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// The mailboxes the library provisions, as the database keeps them: one row per
/// address, and whether a holder stands read from the account and its memberships in
/// the same query (INT-MAIL-006, INT-MAIL-006a, INT-MAIL-007).
/// </summary>
/// <remarks>
/// The port implementations are tested against the real database (D-156). One database
/// serves the class and the store reads every row, which only the deployment that wrote
/// it can decrypt, so each test begins with no mailboxes and shares the one
/// administrative organization the schema admits.
/// </remarks>
[Trait("kind", "integration")]
public sealed class MailboxStoreTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new(database);

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        await using StoreContext context = database.Context();

        _ = await context.Mailboxes.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _deployment.Dispose();

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// INT-MAIL-006a: a holder stands while the account is active or restricted and a
    /// membership of the administrative organization is current, and not otherwise.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task INT_MAIL_006a_AHolderStandsOnlyWhileActiveAndAMemberAsync()
    {
        OrganizationId administrative = await AdministrativeAsync();
        SubjectId holder = await _deployment.AccountAsync(Noon);
        Guid membership = await MemberAsync(holder, administrative);
        var mailbox = Mailbox.Reserved(Parsed("stands@example.test"), Noon);

        mailbox.Hold(holder);
        await WrittenAsync(store => store.AddAsync(mailbox, TestContext.Current.CancellationToken));

        Assert.True(await StandsAsync(mailbox.Id));

        await ChangedAsync(context => context.Accounts
            .Where(account => account.Subject == holder)
            .ExecuteUpdateAsync(
                account => account.SetProperty(row => row.State, AccountState.Suspended),
                TestContext.Current.CancellationToken));

        Assert.False(await StandsAsync(mailbox.Id));

        await ChangedAsync(context => context.Accounts
            .Where(account => account.Subject == holder)
            .ExecuteUpdateAsync(
                account => account.SetProperty(row => row.State, AccountState.Active),
                TestContext.Current.CancellationToken));
        await ChangedAsync(context => context.Memberships
            .Where(row => row.Id == new MembershipId(membership))
            .ExecuteUpdateAsync(
                row => row.SetProperty(ended => ended.EndedAt, Noon.AddDays(1)),
                TestContext.Current.CancellationToken));

        Assert.False(await StandsAsync(mailbox.Id));
    }

    /// <summary>
    /// IDN-ACCT-007 AC2, INT-MAIL-006a (D-166): a restriction the person asked for does
    /// not cut them off from their mail, so a restricted holder stands and its mailbox
    /// stays owed enabled.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_ACCT_007_AC2_ARestrictedHolderStandsAsync()
    {
        OrganizationId administrative = await AdministrativeAsync();
        SubjectId holder = await _deployment.AccountAsync(Noon);
        _ = await MemberAsync(holder, administrative);
        var mailbox = Mailbox.Reserved(Parsed("restricted@example.test"), Noon);

        mailbox.Hold(holder);
        await WrittenAsync(store => store.AddAsync(mailbox, TestContext.Current.CancellationToken));
        await ChangedAsync(context => context.Accounts
            .Where(account => account.Subject == holder)
            .ExecuteUpdateAsync(
                account => account.SetProperty(row => row.State, AccountState.Restricted),
                TestContext.Current.CancellationToken));

        Assert.True(await StandsAsync(mailbox.Id));
    }

    /// <summary>
    /// INT-MAIL-006: a membership of any other organization does not make a holder
    /// stand, because mailboxes are the administrative organization's alone.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task INT_MAIL_006_AMembershipElsewhereDoesNotStandAsync()
    {
        OrganizationId customer = await _deployment.OrganizationAsync(Noon);
        SubjectId holder = await _deployment.AccountAsync(Noon);
        _ = await MemberAsync(holder, customer);
        var mailbox = Mailbox.Reserved(Parsed("elsewhere@example.test"), Noon);

        mailbox.Hold(holder);
        await WrittenAsync(store => store.AddAsync(mailbox, TestContext.Current.CancellationToken));

        Assert.False(await StandsAsync(mailbox.Id));
    }

    /// <summary>
    /// REG-MAIL-003: the mailbox an account holds is read by its holder until it is
    /// retired, and a later holder of the same address reads it as theirs.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_MAIL_003_TheMailboxAnAccountHoldsIsReadUntilRetiredAsync()
    {
        SubjectId holder = await _deployment.AccountAsync(Noon);
        SubjectId later = await _deployment.AccountAsync(Noon);
        var mailbox = Mailbox.Reserved(Parsed("held@example.test"), Noon);

        mailbox.Hold(holder);
        await WrittenAsync(store => store.AddAsync(mailbox, TestContext.Current.CancellationToken));

        Assert.Equal(mailbox.Id, (await HeldByAsync(holder))?.Id);
        Assert.Null(await HeldByAsync(later));

        mailbox.Retire(Noon.AddDays(1));
        await WrittenAsync(store => store.RecordAsync(mailbox, TestContext.Current.CancellationToken));

        Assert.Null(await HeldByAsync(holder));

        mailbox.Reserve();
        mailbox.Hold(later);
        await WrittenAsync(store => store.RecordAsync(mailbox, TestContext.Current.CancellationToken));

        Assert.Null(await HeldByAsync(holder));
        Assert.Equal(mailbox.Id, (await HeldByAsync(later))?.Id);
    }

    /// <summary>
    /// INT-MAIL-007: the outstanding push and its key are carried onto the row and read
    /// back, so a retry after a restart is made under the same key; a removal the server
    /// has confirmed is no longer read.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task INT_MAIL_007_AnOutstandingPushKeepsItsKeyAsync()
    {
        var mailbox = Mailbox.Reserved(Parsed("kept@example.test"), Noon);

        await WrittenAsync(store => store.AddAsync(mailbox, TestContext.Current.CancellationToken));

        MailboxPush push = mailbox.Due(stands: false, Noon)!;

        mailbox.Attempting();
        _ = mailbox.Refused(Noon, TimeSpan.FromSeconds(30), 2.0m, maximum: 10, jitter: 1);

        await WrittenAsync(store => store.RecordAsync(mailbox, TestContext.Current.CancellationToken));

        Mailbox read = (await ReadAsync()).Single(standing => standing.Mailbox.Id == mailbox.Id).Mailbox;

        Assert.Equal(push.Key, read.PendingKey);
        Assert.Equal(MailboxState.Disabled, read.Pending);
        Assert.Equal(1, read.Attempts);
        Assert.Equal(Noon.AddSeconds(30), read.NextAttemptAt);
        Assert.True(read.Attempted);

        read.Confirmed();
        read.Release(Noon);
        _ = read.Due(stands: false, Noon);
        read.Confirmed();

        await WrittenAsync(store => store.RecordAsync(read, TestContext.Current.CancellationToken));

        Assert.DoesNotContain(await ReadAsync(), standing => standing.Mailbox.Id == mailbox.Id);
    }

    /// <summary>
    /// INT-MAIL-006 and REG-MAIL-003: one mailbox stands for an address at a time, which
    /// the unique index holds whatever the service does.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task INT_MAIL_006_AnAddressIsOneMailboxAsync()
    {
        await WrittenAsync(store => store.AddAsync(
            Mailbox.Reserved(Parsed("once@example.test"), Noon),
            TestContext.Current.CancellationToken));

        await Assert.ThrowsAsync<DbUpdateException>(() => WrittenAsync(store => store.AddAsync(
            Mailbox.Reserved(Parsed("once@example.test"), Noon),
            TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC18: an address nobody holds is bound to its own mailbox, so its
    /// value and wrapped key moved onto another unheld mailbox's row do not open there.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_005a_AC18_AnUnheldAddressDoesNotOpenOnAnotherRowAsync()
    {
        var moved = Mailbox.Reserved(Parsed("moved@example.test"), Noon);
        var other = Mailbox.Reserved(Parsed("other@example.test"), Noon);

        await WrittenAsync(store => store.AddAsync(moved, TestContext.Current.CancellationToken));
        await WrittenAsync(store => store.AddAsync(other, TestContext.Current.CancellationToken));

        await using (NpgsqlConnection connection = await database.OpenAsync())
        {
            await connection.ExecuteAsync(
                """
                UPDATE identity.mailboxes AS other
                SET wrapped_key = moved.wrapped_key, enc_canonical = moved.enc_canonical
                FROM identity.mailboxes AS moved
                WHERE other.id = @other AND moved.id = @moved;
                """,
                new { other = other.Id.Value, moved = moved.Id.Value });
        }

        await using StoreContext reading = database.Context();

        Assert.Equal(
            moved.Address,
            (await Store(reading).FindAsync(moved.Id, TestContext.Current.CancellationToken))?.Address);
        await Assert.ThrowsAnyAsync<CryptographicException>(async () =>
            await Store(reading).FindAsync(other.Id, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// INT-MAIL-007 AC7, D-177: whether a row written before the mark was ever pushed
    /// cannot be told, so the migration counts every such row attempted and its removal
    /// is sent; the column then takes no default, so a row written after carries what
    /// the publisher records.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task INT_MAIL_007_AC7_AMailboxWrittenBeforeTheMarkCountsAsAttemptedAsync()
    {
        string migrated = await database.CreateDatabaseAsync("mailbox_attempted");

        await MigrateAsync(migrated, "20260929173000_HoldClientSecretsWrapped");

        await using (var connection = new NpgsqlConnection(migrated))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await connection.ExecuteAsync(
                """
                INSERT INTO identity.mailboxes
                    (id, fingerprint, canonicalisation_version, enc_canonical, wrapped_key, reserved_at, attempts, fingerprint_version)
                VALUES
                    (gen_random_uuid(), '\x01', '16.0.0', '\x02', '\x03', now(), 0, 1);
                """);
        }

        await MigrateAsync(migrated, "20260930043033_RecordWhetherAMailboxPushWasAttempted");

        await using var reading = new NpgsqlConnection(migrated);
        await reading.OpenAsync(TestContext.Current.CancellationToken);

        Assert.Equal([true], await reading.QueryAsync<bool>("SELECT attempted FROM identity.mailboxes"));
        Assert.Null(await reading.ExecuteScalarAsync<string?>(
            """
            SELECT column_default FROM information_schema.columns
            WHERE table_schema = 'identity' AND table_name = 'mailboxes' AND column_name = 'attempted'
            """));
    }

    /// <summary>
    /// INT-MAIL-006 AC7 and REG-MAIL-003 AC7, D-178: under <c>replace</c> the old row
    /// records the instant, is owed <c>removed</c> whatever its holder's state and keeps
    /// its fingerprint, and the address finds the new row, which the unique index admits
    /// beside it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task INT_MAIL_006_AC7_AReplacedMailboxStandsAsideForItsSuccessorAsync()
    {
        (Mailbox replaced, Mailbox successor, byte[] kept) = await ReplacedAsync("replaced@example.test");

        await using StoreContext reading = database.Context();

        Mailbox? found = await Store(reading).FindAsync(Parsed("replaced@example.test"), TestContext.Current.CancellationToken);
        Mailbox old = (await Store(reading).FindAsync(replaced.Id, TestContext.Current.CancellationToken))!;

        Assert.Equal(successor.Id, found?.Id);
        Assert.Equal(Noon.AddDays(2), old.RemovalOwedAt);
        Assert.Equal(MailboxState.Removed, old.Owed(stands: true));
        Assert.Equal(kept, await FingerprintAsync(replaced.Id));
        Assert.Equal(
            [replaced.Id, successor.Id],
            (await Store(reading).AllAsync(TestContext.Current.CancellationToken)).Select(standing => standing.Mailbox.Id));
    }

    /// <summary>
    /// REG-MAIL-003 AC7 and INT-MAIL-007 AC8, D-178: erasing the replaced mailbox's last
    /// holder before its removal is confirmed neutralises its fingerprint, so it is read
    /// no more and its push ends unsent, while the new mailbox still stands for the
    /// address.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_MAIL_003_AC7_ErasingTheReplacedHolderEndsItsRemovalUnsentAsync()
    {
        (Mailbox replaced, Mailbox successor, _) = await ReplacedAsync("erased-holder@example.test");

        await ChangedAsync(context => context.Accounts
            .Where(account => account.Subject == replaced.Holder)
            .ExecuteUpdateAsync(
                account => account
                    .SetProperty(row => row.State, AccountState.Deleting)
                    .SetProperty(row => row.DeletingBy, DeletionOrigin.Self)
                    .SetProperty(row => row.DeletingSince, Noon),
                TestContext.Current.CancellationToken));

        await using (StoreContext erasing = database.Context())
        await using (var work = new UnitOfWork(erasing))
        {
            await work.BeginAsync(TestContext.Current.CancellationToken);
            await new SubjectEraser(
                    erasing,
                    new SessionStore(erasing, _deployment.Ring, _deployment.Randomness),
                    new ConfigurationStore(erasing, new DataConnections(erasing)),
                    new DataConnections(erasing))
                .EraseAsync(replaced.Holder!.Value, ErasureReason.ErasureRequest, Noon.AddDays(3), TestContext.Current.CancellationToken);
            await erasing.SaveChangesAsync(TestContext.Current.CancellationToken);
            await work.CommitAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();

        Assert.True(Janus.Storage.Fingerprint.IsNeutralised(await FingerprintAsync(replaced.Id)));
        Assert.Null(await Store(reading).FindAsync(replaced.Id, TestContext.Current.CancellationToken));
        Assert.Equal(
            [successor.Id],
            (await Store(reading).AllAsync(TestContext.Current.CancellationToken)).Select(standing => standing.Mailbox.Id));
        Assert.Equal(
            successor.Id,
            (await Store(reading).FindAsync(Parsed("erased-holder@example.test"), TestContext.Current.CancellationToken))?.Id);
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC19, D-178: a reservation nobody held, released with its
    /// invitation, holds no readable address and no live fingerprint once the server has
    /// confirmed its removal, and its row remains.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_005a_AC19_AReleasedMailboxForgetsItsAddressOnceRemovedAsync()
    {
        var released = Mailbox.Reserved(Parsed("forgotten@example.test"), Noon);

        await WrittenAsync(store => store.AddAsync(released, TestContext.Current.CancellationToken));

        _ = released.Due(stands: false, Noon);
        released.Attempting();
        released.Confirmed();
        released.Release(Noon.AddHours(1));
        _ = released.Due(stands: false, Noon.AddHours(1));
        released.Attempting();

        await WrittenAsync(store => store.RecordAsync(released, TestContext.Current.CancellationToken));

        byte[] live = await FingerprintAsync(released.Id);

        Assert.False(Janus.Storage.Fingerprint.IsNeutralised(live));

        released.Confirmed();
        await WrittenAsync(store => store.RecordAsync(released, TestContext.Current.CancellationToken));

        await using NpgsqlConnection connection = await database.OpenAsync();

        (byte[] Fingerprint, byte[] WrappedKey) row = await connection.QuerySingleAsync<(byte[], byte[])>(
            "SELECT fingerprint, wrapped_key FROM identity.mailboxes WHERE id = @id",
            new { id = released.Id.Value });

        Assert.True(Janus.Storage.Fingerprint.IsNeutralised(row.Fingerprint));
        Assert.Equal(32, row.WrappedKey.Length);
        Assert.All(row.WrappedKey, value => Assert.Equal(0, value));

        await using StoreContext reading = database.Context();

        Assert.Null(await Store(reading).FindAsync(released.Id, TestContext.Current.CancellationToken));
        Assert.Empty(await Store(reading).AllAsync(TestContext.Current.CancellationToken));
    }

    // A mailbox held and retired, then replaced by a new reservation at its address, as
    // an invitation naming replace leaves them, and the old row's fingerprint beforehand.
    private async Task<(Mailbox Replaced, Mailbox Successor, byte[] Kept)> ReplacedAsync(string address)
    {
        SubjectId holder = await _deployment.AccountAsync(Noon);
        var replaced = Mailbox.Reserved(Parsed(address), Noon);

        replaced.Hold(holder);
        replaced.Retire(Noon.AddDays(1));
        await WrittenAsync(store => store.AddAsync(replaced, TestContext.Current.CancellationToken));

        byte[] kept = await FingerprintAsync(replaced.Id);
        var successor = Mailbox.Reserved(Parsed(address), Noon.AddDays(2));

        replaced.Replace(Noon.AddDays(2));
        await WrittenAsync(async store =>
        {
            await store.RecordAsync(replaced, TestContext.Current.CancellationToken);
            await store.AddAsync(successor, TestContext.Current.CancellationToken);
        });

        return (replaced, successor, kept);
    }

    private async Task<byte[]> FingerprintAsync(MailboxId mailbox)
    {
        await using NpgsqlConnection connection = await database.OpenAsync();

        return await connection.QuerySingleAsync<byte[]>(
            "SELECT fingerprint FROM identity.mailboxes WHERE id = @id",
            new { id = mailbox.Value });
    }

    private static async Task MigrateAsync(string connectionString, string target)
    {
        await using StoreContext context = DatabaseFixture.Context(connectionString);
        await context.GetService<IMigrator>().MigrateAsync(target, TestContext.Current.CancellationToken);
    }

    private async Task<OrganizationId> AdministrativeAsync()
    {
        await using StoreContext context = database.Context();

        if (await context.Organizations
                .SingleOrDefaultAsync(organization => organization.IsAdministrative, TestContext.Current.CancellationToken)
            is OrganizationRecord held)
        {
            return held.Id;
        }

        var administrative = new OrganizationId(Guid.NewGuid());

        context.Organizations.Add(new OrganizationRecord
        {
            Id = administrative,
            Name = "Administrative",
            CreatedAt = Noon,
            IsAdministrative = true,
        });

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return administrative;
    }

    private async Task<Guid> MemberAsync(SubjectId subject, OrganizationId organization)
    {
        var id = Guid.NewGuid();

        await using StoreContext context = database.Context();

        context.Memberships.Add(new MembershipRecord
        {
            Id = new MembershipId(id),
            Subject = subject,
            Organization = organization,
            CreatedAt = Noon,
        });

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return id;
    }

    private MailboxStore Store(StoreContext context) =>
        new(context, _deployment.DataKey(context), _deployment.Ring, _deployment.Randomness);

    private async Task<Mailbox?> HeldByAsync(SubjectId holder)
    {
        await using StoreContext reading = database.Context();

        return await Store(reading).HeldByAsync(holder, TestContext.Current.CancellationToken);
    }

    private async Task<bool> StandsAsync(MailboxId mailbox) =>
        (await ReadAsync()).Single(standing => standing.Mailbox.Id == mailbox).Stands;

    private async Task<IReadOnlyList<MailboxStanding>> ReadAsync()
    {
        await using StoreContext reading = database.Context();

        return await Store(reading).AllAsync(TestContext.Current.CancellationToken);
    }

    private async Task WrittenAsync(Func<MailboxStore, ValueTask> write)
    {
        await using StoreContext writing = database.Context();

        await write(Store(writing));
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task ChangedAsync(Func<StoreContext, Task<int>> change)
    {
        await using StoreContext context = database.Context();

        _ = await change(context);
    }

    private static EmailAddress Parsed(string value)
    {
        Assert.True(EmailAddress.TryParse(value, out EmailAddress address));

        return address;
    }
}
