using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication;
using Janus.Authentication.Invitations;
using Janus.Authentication.Mailboxes;
using Janus.Core;
using Janus.Storage.Authentication.Invitations;
using Janus.Storage.Authentication.Mailboxes;
using Janus.Storage.Authorization.Roles;
using Janus.Storage.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
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
    /// REG-INV-002: an account reads the standing invitation whose link it opened
    /// last; a revoked one is not read, and an account nothing is attached to reads
    /// nothing.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_INV_002_AnAccountReadsTheInvitationItOpenedLastAsync()
    {
        Invitation first = await IssuedAsync("first@example.test");
        Invitation second = await IssuedAsync("second@example.test");
        Invitation revoked = await IssuedAsync("revoked@example.test");
        SubjectId invitee = await _deployment.AccountAsync(Noon);
        SubjectId nobody = await _deployment.AccountAsync(Noon);

        await ChangeAsync(first.Id, held => held.AttachTo(invitee, Noon.AddHours(1)));
        await ChangeAsync(second.Id, held => held.AttachTo(invitee, Noon.AddHours(2)));
        await ChangeAsync(revoked.Id, held => held.AttachTo(invitee, Noon.AddHours(3)));
        await ChangeAsync(revoked.Id, held => held.Revoke(Noon.AddHours(4)));

        await using StoreContext reading = database.Context();

        Assert.Equal(
            second.Id,
            (await Store(reading).AttachedToAsync(invitee, TestContext.Current.CancellationToken))?.Id);
        Assert.Null(await Store(reading).AttachedToAsync(nobody, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// REG-INV-001: a revoked invitation forgets what it bound: the document is gone
    /// from the row and the key that read it is erased, and what stays is who invited
    /// into what.
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
        Assert.Equal(new byte[32], row.WrappedKey);
        Assert.Null(read.Identifiers);
        Assert.Equal(Noon.AddHours(1), read.RevokedAt);
        Assert.Equal(issued.Inviter, read.Inviter);
        Assert.Equal(issued.Organization, read.Organization);
    }

    /// <summary>
    /// PRIV-RIGHT-005a and REG-MAIL-001: the sweep forgets what an invitation bound once
    /// it has expired unused, and nothing sooner; the row still names who invited into
    /// what, keeps its token's fingerprint and its mailbox, and a second sweep finds
    /// nothing more.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_INV_001_AnExpiredInvitationForgetsWhatItBoundWhenSweptAsync()
    {
        Invitation issued = await IssuedAsync("lapsing@example.test", Fresh("lapsing"));

        Assert.Equal(0, await SweptAsync(issued.ExpiresAt.AddSeconds(-1)));
        Assert.NotNull((await RowAsync(issued.Id)).EncryptedIdentifiers);
        Assert.True(await SweptAsync(issued.ExpiresAt) >= 1);

        InvitationRecord row = await RowAsync(issued.Id);

        Assert.Null(row.EncryptedIdentifiers);
        Assert.Equal(new byte[32], row.WrappedKey);
        Assert.Equal(issued.Token, row.Token);
        Assert.Equal(issued.Mailbox?.Value, row.Mailbox);
        Assert.Equal((issued.Inviter, issued.Organization), (row.Inviter, row.Organization));
        Assert.Equal(0, await SweptAsync(issued.ExpiresAt.AddDays(1)));
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC14: an invitation revoked, acknowledged or swept after expiry
    /// holds no identifier and, in place of its wrapped key, the 32 zero bytes of an
    /// erased key; one that stands keeps what it binds under its key until then.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_005a_AC14_AnInvitationRevokedAcknowledgedOrSweptHoldsNoIdentifierAndTheErasedKeyAsync()
    {
        SubjectId invitee = await _deployment.AccountAsync(Noon);
        Invitation revoked = await IssuedAsync(Fresh("revoked"));
        Invitation acknowledged = await IssuedAsync(Fresh("acknowledged"));
        Invitation swept = await IssuedAsync(Fresh("swept"));

        await ChangeAsync(revoked.Id, held => held.Revoke(Noon.AddHours(1)));
        await ChangeAsync(acknowledged.Id, held => held.AttachTo(invitee, Noon.AddHours(1)));
        await ChangeAsync(acknowledged.Id, held => held.Acknowledge(Noon.AddHours(2)));

        InvitationRecord standing = await RowAsync(swept.Id);

        Assert.NotNull(standing.EncryptedIdentifiers);
        Assert.False(PersonalFieldCipher.IsErased(standing.WrappedKey));

        Assert.True(await SweptAsync(swept.ExpiresAt) >= 1);

        Assert.All(
            [await RowAsync(revoked.Id), await RowAsync(acknowledged.Id), await RowAsync(swept.Id)],
            row =>
            {
                Assert.Null(row.EncryptedIdentifiers);
                Assert.Equal(new byte[32], row.WrappedKey);
            });

        await using StoreContext reading = database.Context();

        Assert.Null((await Store(reading).FindAsync(swept.Id, TestContext.Current.CancellationToken))?.Identifiers);
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC14 (D-187): an invitation forgotten while a forgotten key was
    /// stored as nothing holds the erased key once the migration has run, a standing one
    /// keeps its key, and no row holds an absent key after it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_005a_AC14_AnInvitationForgottenBeforeTheErasedKeyHoldsItOnceMigratedAsync()
    {
        string moved = await database.CreateDatabaseAsync("invitation_key");
        var forgotten = Guid.CreateVersion7();
        var standing = Guid.CreateVersion7();
        string erasing;

        await using (StoreContext migrating = DatabaseFixture.Context(moved))
        {
            string[] declared = [.. migrating.GetService<IMigrationsAssembly>().Migrations.Keys.Order(StringComparer.Ordinal)];

            erasing = declared.Single(migration =>
                migration.EndsWith("_" + nameof(HoldAnInvitationsErasedKey), StringComparison.Ordinal));

            await migrating.GetService<IMigrator>().MigrateAsync(
                declared[Array.IndexOf(declared, erasing) - 1],
                TestContext.Current.CancellationToken);
        }

        await using var connection = new NpgsqlConnection(moved);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var values = new
        {
            forgotten,
            standing,
            inviter = Subjects.New().Value,
            organization = Guid.CreateVersion7(),
            at = Noon,
        };

        await connection.ExecuteAsync(
            """
            INSERT INTO identity.accounts (subject, state, created_at)
            VALUES (@inviter, 'active', @at);
            INSERT INTO identity.organizations (id, name, canonical_name, created_at)
            VALUES (@organization, 'Invitation keys', 'invitation keys', @at);
            INSERT INTO identity.invitations
                (id, organization, inviter, token, wrapped_key, enc_identifiers, roles, documents,
                 issued_at, expires_at, revoked_at)
            VALUES
                (@forgotten, @organization, @inviter, '\x01', NULL, NULL, '{}', '[]', @at, @at, @at),
                (@standing, @organization, @inviter, '\x02', '\x0a0b', '\x0c', '{}', '[]', @at, @at, NULL);
            """,
            values);

        await using (StoreContext migrating = DatabaseFixture.Context(moved))
        {
            await migrating.GetService<IMigrator>().MigrateAsync(erasing, TestContext.Current.CancellationToken);
        }

        Assert.Equal(
            new byte[32],
            await connection.QuerySingleAsync<byte[]>(
                "SELECT wrapped_key FROM identity.invitations WHERE id = @forgotten", values));
        Assert.Equal(
            [0x0a, 0x0b],
            await connection.QuerySingleAsync<byte[]>(
                "SELECT wrapped_key FROM identity.invitations WHERE id = @standing", values));
        await Assert.ThrowsAsync<PostgresException>(() => connection.ExecuteAsync(
            "UPDATE identity.invitations SET wrapped_key = NULL, enc_identifiers = NULL WHERE id = @standing",
            values));
        await Assert.ThrowsAsync<PostgresException>(() => connection.ExecuteAsync(
            "UPDATE identity.invitations SET enc_identifiers = NULL WHERE id = @standing",
            values));
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
    /// REG-MAIL-001 and INT-MAIL-006 AC7, D-178: a mailbox is found by its address while
    /// it stands for it and by its identifier whatever state it is in; a released one is
    /// found by its identifier alone.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_MAIL_001_AMailboxIsFoundByItsAddressAsync()
    {
        var reserved = Mailbox.Reserved(Parsed("found@example.test"), Noon);
        var released = Mailbox.Reserved(Parsed("given-up@example.test"), Noon);

        released.Release(Noon.AddHours(1));

        await using (StoreContext writing = database.Context())
        {
            await Mailboxes(writing).AddAsync(reserved, TestContext.Current.CancellationToken);
            await Mailboxes(writing).AddAsync(released, TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();

        Mailbox? byAddress = await Mailboxes(reading).FindAsync(Parsed("found@example.test"), TestContext.Current.CancellationToken);
        Mailbox? byId = await Mailboxes(reading).FindAsync(released.Id, TestContext.Current.CancellationToken);

        Assert.Equal(reserved.Id, byAddress?.Id);
        Assert.Equal("given-up@example.test", byId?.Address.Value);
        Assert.Equal(Noon.AddHours(1), byId?.RemovalOwedAt);
        Assert.Null(await Mailboxes(reading).FindAsync(Parsed("given-up@example.test"), TestContext.Current.CancellationToken));
        Assert.Null(await Mailboxes(reading).FindAsync(Parsed("absent@example.test"), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC18 and D-166 (234): what an invitation binds is bound to the
    /// invitation itself, so its value and wrapped key moved onto another invitation's
    /// row do not open there, although every invitation's key is wrapped under the one
    /// deployment key.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_005a_AC18_AnInvitationsValueDoesNotOpenOnAnotherRowAsync()
    {
        Invitation moved = await IssuedAsync("moved@example.test");
        Invitation other = await IssuedAsync("other@example.test");

        await using (NpgsqlConnection connection = await database.OpenAsync())
        {
            await connection.ExecuteAsync(
                """
                UPDATE identity.invitations AS other
                SET wrapped_key = moved.wrapped_key, enc_identifiers = moved.enc_identifiers
                FROM identity.invitations AS moved
                WHERE other.id = @other AND moved.id = @moved;
                """,
                new { other = other.Id.Value, moved = moved.Id.Value });
        }

        await using StoreContext reading = database.Context();

        Assert.Equal(
            moved.Identifiers,
            (await Store(reading).FindAsync(moved.Id, TestContext.Current.CancellationToken))?.Identifiers);
        await Assert.ThrowsAnyAsync<CryptographicException>(async () =>
            await Store(reading).FindAsync(other.Id, TestContext.Current.CancellationToken));
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

    private static string Fresh(string name) => name + "." + Guid.NewGuid().ToString("N") + "@example.test";

    /// <summary>
    /// AUTHZ-GRANT-004 AC6 and REG-INV-001: a role a standing invitation names is named,
    /// the invitation expired or not; one only a revoked or an acknowledged invitation
    /// names is not, since neither will grant it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_004_ARoleAStandingInvitationNamesIsNamedAsync()
    {
        string unique = Guid.NewGuid().ToString("N")[..8];
        var standing = RoleName.Parse("standing-" + unique);
        var revoked = RoleName.Parse("revoked-" + unique);
        var acknowledged = RoleName.Parse("acknowledged-" + unique);
        SubjectId invitee = await _deployment.AccountAsync(Noon);

        Invitation expired = await IssuedAsync("standing@example.test", roles: [standing]);
        Invitation taken = await IssuedAsync("revoked@example.test", roles: [revoked]);
        Invitation used = await IssuedAsync("acknowledged@example.test", roles: [acknowledged]);

        await ChangeAsync(taken.Id, held => held.Revoke(Noon.AddHours(1)));
        await ChangeAsync(used.Id, held => held.AttachTo(invitee, Noon.AddHours(1)));
        await ChangeAsync(used.Id, held => held.Acknowledge(Noon.AddHours(2)));

        await using StoreContext reading = database.Context();
        var references = new RoleReferences(reading);

        Assert.True(expired.HasExpired(Noon.AddDays(8)));
        Assert.True(await references.NamedAsync(standing, TestContext.Current.CancellationToken));
        Assert.False(await references.NamedAsync(revoked, TestContext.Current.CancellationToken));
        Assert.False(await references.NamedAsync(acknowledged, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// IDN-LIFE-009a AC2, CONV-DESIGN-003 AC6: two accounts pressing one link at once
    /// each judge it on the invitation under its lock, so it attaches to one of them.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_LIFE_009a_AC2_TwoPressesAtOnceAttachTheInvitationOnceAsync()
    {
        Invitation issued = await IssuedAsync("pressed@example.test");
        SubjectId first = await _deployment.AccountAsync(Noon);
        SubjectId second = await _deployment.AccountAsync(Noon);

        bool[] opened = await Task.WhenAll(OpenedAsync(issued.Id, first), OpenedAsync(issued.Id, second));

        await using StoreContext reading = database.Context();
        Invitation read = (await Store(reading).FindAsync(issued.Id, TestContext.Current.CancellationToken))!;

        Assert.Equal(1, opened.Count(answer => answer));
        Assert.Equal(opened[0] ? first : second, read.Invitee);
    }

    private InvitationStore Store(StoreContext context) =>
        new(context, _deployment.DataKey(context), _deployment.Randomness);

    // Each press is its own request, judging the link on the invitation as read before
    // its transaction and again under the lock, as the opening does.
    private async Task<bool> OpenedAsync(InvitationId id, SubjectId invitee)
    {
        await using StoreContext context = database.Context();
        await using var work = new UnitOfWork(context);
        InvitationStore store = Store(context);

        _ = await store.FindAsync(id, TestContext.Current.CancellationToken);

        Assert.True((await work.BeginAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));

        Invitation held = (await store.FindForUpdateAsync(id, TestContext.Current.CancellationToken))!;
        bool opens = held.Opens(Noon.AddHours(1));

        if (opens)
        {
            held.AttachTo(invitee, Noon.AddHours(1));

            await store.RecordAsync(held, TestContext.Current.CancellationToken);
        }

        Assert.True((await work.CommitAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));

        return opens;
    }

    private async Task<int> SweptAsync(DateTimeOffset now)
    {
        await using StoreContext writing = database.Context();

        return await Store(writing).SweepAsync(now, TestContext.Current.CancellationToken);
    }

    private async Task<InvitationRecord> RowAsync(InvitationId id)
    {
        await using StoreContext reading = database.Context();

        return await reading.Invitations
            .SingleAsync(invitation => invitation.Id == id, TestContext.Current.CancellationToken);
    }

    private MailboxStore Mailboxes(StoreContext context) =>
        new(context, _deployment.DataKey(context), _deployment.Ring, _deployment.Randomness);

    private async Task<Invitation> IssuedAsync(
        string email,
        string? corporate = null,
        RoleName[]? roles = null)
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        SubjectId inviter = await _deployment.AccountAsync(Noon);
        Mailbox? mailbox = corporate is null ? null : Mailbox.Reserved(Parsed(corporate), Noon);

        var invitation = Invitation.Issued(
            InvitationId.New(TimeProvider.System),
            organization,
            inviter,
            new InvitedIdentifiers(email, "+441632960011", corporate),
            roles ?? [RoleName.Parse("clerk"), RoleName.Parse("auditor")],
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

    private async Task ChangeAsync(InvitationId id, Action<Invitation> change)
    {
        await using StoreContext writing = database.Context();

        Invitation held = (await Store(writing).FindAsync(id, TestContext.Current.CancellationToken))!;

        change(held);

        await Store(writing).RecordAsync(held, TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task AddAsync(Invitation invitation)
    {
        await using StoreContext writing = database.Context();

        await Store(writing).AddAsync(invitation, TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static EmailAddress Parsed(string value)
    {
        Assert.True(EmailAddress.TryParse(value, out EmailAddress address));

        return address;
    }
}
