using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Janus.Authentication.Mailboxes;
using Janus.Core;
using Janus.Storage.Authentication.Mailboxes;
using Janus.Storage.Identity.Organizations;
using Microsoft.EntityFrameworkCore;
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
    /// INT-MAIL-006a: a holder stands while the account is active and a membership of
    /// the administrative organization is current, and not otherwise.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task INT_MAIL_006a_AHolderStandsOnlyWhileActiveAndAMemberAsync()
    {
        OrganizationId administrative = await AdministrativeAsync();
        SubjectId holder = await _deployment.AccountAsync(Noon);
        Guid membership = await MemberAsync(holder, administrative);
        var mailbox = Mailbox.Reserved("stands@example.test", Noon);

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
        var mailbox = Mailbox.Reserved("elsewhere@example.test", Noon);

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
        var mailbox = Mailbox.Reserved("held@example.test", Noon);

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
        var mailbox = Mailbox.Reserved("kept@example.test", Noon);

        await WrittenAsync(store => store.AddAsync(mailbox, TestContext.Current.CancellationToken));

        MailboxPush push = mailbox.Due(stands: false, Noon)!;

        _ = mailbox.Refused(Noon, TimeSpan.FromSeconds(30), 2.0m, maximum: 10, jitter: 1);

        await WrittenAsync(store => store.RecordAsync(mailbox, TestContext.Current.CancellationToken));

        Mailbox read = (await ReadAsync()).Single(standing => standing.Mailbox.Id == mailbox.Id).Mailbox;

        Assert.Equal(push.Key, read.PendingKey);
        Assert.Equal(MailboxState.Disabled, read.Pending);
        Assert.Equal(1, read.Attempts);
        Assert.Equal(Noon.AddSeconds(30), read.NextAttemptAt);

        read.Confirmed();
        read.Release(Noon);
        _ = read.Due(stands: false, Noon);
        read.Confirmed();

        await WrittenAsync(store => store.RecordAsync(read, TestContext.Current.CancellationToken));

        Assert.DoesNotContain(await ReadAsync(), standing => standing.Mailbox.Id == mailbox.Id);
    }

    /// <summary>
    /// INT-MAIL-006 and REG-MAIL-003: an address is one mailbox for good, which the
    /// unique index holds whatever the service does.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task INT_MAIL_006_AnAddressIsOneMailboxAsync()
    {
        await WrittenAsync(store => store.AddAsync(
            Mailbox.Reserved("once@example.test", Noon),
            TestContext.Current.CancellationToken));

        await Assert.ThrowsAsync<DbUpdateException>(() => WrittenAsync(store => store.AddAsync(
            Mailbox.Reserved("once@example.test", Noon),
            TestContext.Current.CancellationToken)));
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
        new(context, _deployment.Keys, Deployment.FingerprintKey, _deployment.Randomness);

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
}
