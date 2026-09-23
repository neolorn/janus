using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Invitations;
using Janus.Authentication.Mailboxes;
using Janus.Core;
using Janus.Storage.Authentication.Invitations;
using Janus.Storage.Authentication.Mailboxes;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// Invitations as the database keeps them: what they bind under a key of their own
/// until it is forgotten, and one invitation standing over a mailbox
/// (IDN-LIFE-009a, REG-INV-001, REG-MAIL-001).
/// </summary>
/// <remarks>The port implementations are tested against the real database (D-156).</remarks>
[Trait("kind", "integration")]
public sealed class InvitationStoreTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new(database);

    /// <summary>
    /// REG-INV-001 and PRIV-RIGHT-005a: an invitation reads back as it was issued, what
    /// it binds is not in the row as it was entered, and the link's token is held only
    /// as its fingerprint.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_INV_001_AnInvitationReadsBackAsItWasIssuedAsync()
    {
        Invitation issued = await IssuedAsync("read@example.test");

        await using StoreContext reading = database.Context();

        Invitation read = (await Store(reading).FindAsync(issued.Id, TestContext.Current.CancellationToken))!;
        InvitationRecord row = await reading.Invitations
            .SingleAsync(invitation => invitation.Id == issued.Id, TestContext.Current.CancellationToken);

        Assert.Equal(issued.Identifiers, read.Identifiers);
        Assert.Equal(issued.Roles, read.Roles);
        Assert.Equal(issued.Documents, read.Documents);
        Assert.Equal(issued.Mailbox, read.Mailbox);
        Assert.Equal(issued.ExpiresAt, read.ExpiresAt);
        Assert.Equal(issued.Token, read.Token);
        Assert.DoesNotContain(
            "read@example.test",
            Encoding.UTF8.GetString(row.EncryptedIdentifiers!),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// IDN-LIFE-009a: the invitation a link's token opens is found by what is stored
    /// against the token, and a token nobody issued finds nothing.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_LIFE_009a_AnInvitationIsFoundByItsTokenAsync()
    {
        Invitation issued = await IssuedAsync("token@example.test");

        await using StoreContext reading = database.Context();

        Assert.Equal(
            issued.Id,
            (await Store(reading).FindByTokenAsync(issued.Token, TestContext.Current.CancellationToken))?.Id);
        Assert.Null(await Store(reading).FindByTokenAsync(
            OpaqueToken.Of("nobody issued this").Fingerprint(),
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// REG-INV-001: a revoked invitation forgets what it bound: the document and the
    /// key that read it are gone from the row, and what stays is who invited into what.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_INV_001_ARevokedInvitationForgetsWhatItBoundAsync()
    {
        Invitation issued = await IssuedAsync("forgotten@example.test");

        await using (StoreContext writing = database.Context())
        {
            Invitation held = (await Store(writing).FindAsync(issued.Id, TestContext.Current.CancellationToken))!;

            held.Revoke(Noon.AddHours(1));

            await Store(writing).RecordAsync(held, TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();

        InvitationRecord row = await reading.Invitations
            .SingleAsync(invitation => invitation.Id == issued.Id, TestContext.Current.CancellationToken);
        Invitation read = (await Store(reading).FindAsync(issued.Id, TestContext.Current.CancellationToken))!;

        Assert.Null(row.EncryptedIdentifiers);
        Assert.Null(row.WrappedKey);
        Assert.Null(row.KeyVersion);
        Assert.Null(read.Identifiers);
        Assert.Equal(Noon.AddHours(1), read.RevokedAt);
        Assert.Equal(issued.Inviter, read.Inviter);
        Assert.Equal(issued.Organization, read.Organization);
    }

    /// <summary>
    /// REG-MAIL-001: one invitation at most stands over a mailbox's reservation, which
    /// the partial unique index holds whatever the service does; once it is revoked,
    /// the address is invited again.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_MAIL_001_OneInvitationStandsOverAMailboxAsync()
    {
        Invitation first = await IssuedAsync("personal@example.test", "reserved@example.test");
        MailboxId mailbox = first.Mailbox!.Value;

        await Assert.ThrowsAsync<DbUpdateException>(() => AddAsync(Issue(first.Organization, first.Inviter, mailbox)));

        await using (StoreContext writing = database.Context())
        {
            Assert.Single(await Store(writing).ReservingAsync(mailbox, TestContext.Current.CancellationToken));

            Invitation held = (await Store(writing).FindAsync(first.Id, TestContext.Current.CancellationToken))!;

            held.Revoke(Noon.AddHours(1));

            await Store(writing).RecordAsync(held, TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Invitation second = Issue(first.Organization, first.Inviter, mailbox);

        await AddAsync(second);

        await using StoreContext reading = database.Context();

        Assert.Equal(
            second.Id,
            Assert.Single(await Store(reading).ReservingAsync(mailbox, TestContext.Current.CancellationToken)).Id);
    }

    /// <summary>
    /// REG-MAIL-001: a mailbox is found by its address and by its identifier, whatever
    /// state it is in.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_MAIL_001_AMailboxIsFoundByItsAddressAsync()
    {
        var reserved = Mailbox.Reserved("found@example.test", Noon);

        reserved.Release(Noon.AddHours(1));

        await using (StoreContext writing = database.Context())
        {
            await Mailboxes(writing).AddAsync(reserved, TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();

        Mailbox? byAddress = await Mailboxes(reading).FindAsync("found@example.test", TestContext.Current.CancellationToken);
        Mailbox? byId = await Mailboxes(reading).FindAsync(reserved.Id, TestContext.Current.CancellationToken);

        Assert.Equal(reserved.Id, byAddress?.Id);
        Assert.Equal(Noon.AddHours(1), byAddress?.ReleasedAt);
        Assert.Equal("found@example.test", byId?.Address);
        Assert.Null(await Mailboxes(reading).FindAsync("absent@example.test", TestContext.Current.CancellationToken));
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    private static Invitation Issue(OrganizationId organization, SubjectId inviter, MailboxId? mailbox) =>
        Invitation.Issued(
            InvitationId.New(TimeProvider.System),
            organization,
            inviter,
            new InvitedIdentifiers("again@example.test", Phone: null, CorporateEmail: null),
            roles: [],
            documents: [],
            mailbox,
            OpaqueToken.Of(Guid.NewGuid().ToString("N")).Fingerprint(),
            Noon,
            TimeSpan.FromDays(7));

    private InvitationStore Store(StoreContext context) =>
        new(context, _deployment.Keys, _deployment.Randomness);

    private MailboxStore Mailboxes(StoreContext context) =>
        new(context, _deployment.Keys, Deployment.FingerprintKey, _deployment.Randomness);

    private async Task<Invitation> IssuedAsync(string email, string? corporate = null)
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        SubjectId inviter = await _deployment.AccountAsync(Noon);
        Mailbox? mailbox = corporate is null ? null : Mailbox.Reserved(corporate, Noon);

        var invitation = Invitation.Issued(
            InvitationId.New(TimeProvider.System),
            organization,
            inviter,
            new InvitedIdentifiers(email, "+441632960011", corporate),
            [RoleName.Parse("clerk"), RoleName.Parse("auditor")],
            [new InvitationDocument("staff-handbook", "3")],
            mailbox?.Id,
            OpaqueToken.Of(Guid.NewGuid().ToString("N")).Fingerprint(),
            Noon,
            TimeSpan.FromDays(7));

        await using StoreContext writing = database.Context();

        if (mailbox is not null)
        {
            await Mailboxes(writing).AddAsync(mailbox, TestContext.Current.CancellationToken);
        }

        await Store(writing).AddAsync(invitation, TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);

        return invitation;
    }

    private async Task AddAsync(Invitation invitation)
    {
        await using StoreContext writing = database.Context();

        await Store(writing).AddAsync(invitation, TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
