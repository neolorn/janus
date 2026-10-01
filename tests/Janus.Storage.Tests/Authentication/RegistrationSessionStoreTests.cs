using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Registration;
using Janus.Core;
using Janus.Storage.Authentication.Registration;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// What a registration session stages, as the database keeps it (REG-SESS-001).
/// </summary>
/// <remarks>The port implementations are tested against the real database (D-156).</remarks>
[Trait("kind", "integration")]
public sealed class RegistrationSessionStoreTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new(database);

    /// <summary>
    /// IDN-LIFE-009a and REG-INV-001: a registration an invitation opened keeps the
    /// invitation with everything else it staged, and one opened without keeps none.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_LIFE_009a_ARegistrationKeepsTheInvitationThatOpenedItAsync()
    {
        RegistrationSession invited = Opened();
        RegistrationSession open = Opened();
        var invitation = InvitationId.New(TimeProvider.System);

        invited.Invited(invitation);

        await using (StoreContext writing = database.Context())
        {
            await Store(writing).AddAsync(invited, TestContext.Current.CancellationToken);
            await Store(writing).AddAsync(open, TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();

        Assert.Equal(
            invitation,
            (await Store(reading).FindAsync(invited.Id, TestContext.Current.CancellationToken))!.Invitation);
        Assert.Null((await Store(reading).FindAsync(open.Id, TestContext.Current.CancellationToken))!.Invitation);
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC18: what a session stages belongs to no subject yet and is bound
    /// to the session's own row, so its value and wrapped key moved onto another session's
    /// row do not open there.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_005a_AC18_StagedValuesDoNotOpenOnAnotherRowAsync()
    {
        RegistrationSession moved = Opened();
        RegistrationSession other = Opened();

        await using (StoreContext writing = database.Context())
        {
            await Store(writing).AddAsync(moved, TestContext.Current.CancellationToken);
            await Store(writing).AddAsync(other, TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (NpgsqlConnection connection = await database.OpenAsync())
        {
            await connection.ExecuteAsync(
                """
                UPDATE identity.registration_sessions AS other
                SET wrapped_key = moved.wrapped_key, enc_session = moved.enc_session
                FROM identity.registration_sessions AS moved
                WHERE other.id = @other AND moved.id = @moved;
                """,
                new { other = other.Id.Value, moved = moved.Id.Value });
        }

        await using StoreContext reading = database.Context();

        Assert.Equal(
            moved.Id,
            (await Store(reading).FindAsync(moved.Id, TestContext.Current.CancellationToken))?.Id);
        await Assert.ThrowsAnyAsync<CryptographicException>(async () =>
            await Store(reading).FindAsync(other.Id, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// REG-SESS-003 AC3, CONV-DESIGN-003 AC6: wrong codes presented at once are each
    /// decided on the session's row under its lock, so every one is counted against
    /// the staged identifier and none is written over another.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_SESS_003_AC3_WrongCodesAtOnceAreAllCountedAsync()
    {
        RegistrationSession opened = Opened();
        var staged = IdentifierId.New(TimeProvider.System);

        opened.Stage(StagedIdentity.Of(staged, IdentifierKind.Email, "person@example.test", "person@example.test"));

        await using (StoreContext writing = database.Context())
        {
            await Store(writing).AddAsync(opened, TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await Task.WhenAll(MissedAsync(opened.Id, staged), MissedAsync(opened.Id, staged), MissedAsync(opened.Id, staged));

        await using StoreContext reading = database.Context();
        RegistrationSession read = Assert.IsType<RegistrationSession>(
            await Store(reading).FindAsync(opened.Id, TestContext.Current.CancellationToken));

        Assert.Equal(3, read.Identity(staged)!.WrongAttempts);
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    // Each wrong code is its own request, deciding on the session as the registration
    // service does.
    private async Task MissedAsync(RegistrationSessionId session, IdentifierId staged)
    {
        await using StoreContext context = database.Context();
        await using var work = new UnitOfWork(context);
        RegistrationSessionStore store = Store(context);

        Assert.True((await work.BeginAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));

        RegistrationSession held = Assert.IsType<RegistrationSession>(
            await store.FindForUpdateAsync(session, TestContext.Current.CancellationToken));

        held.Identity(staged)!.Missed(cap: 5);

        await store.RecordAsync(held, TestContext.Current.CancellationToken);

        Assert.True((await work.CommitAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));
    }

    private static RegistrationSession Opened() =>
        RegistrationSession.Open(
            RegistrationSessionId.New(TimeProvider.System),
            Subjects.New(),
            "web",
            "en",
            "198.51.100.7",
            Noon,
            TimeSpan.FromHours(24));

    private RegistrationSessionStore Store(StoreContext context) =>
        new(context, new DataConnections(context), _deployment.DataKey(context), _deployment.Randomness);
}
