using System;
using System.Threading.Tasks;
using Janus.Authentication.Registration;
using Janus.Core;
using Janus.Storage.Authentication.Registration;
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

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

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
        new(context, new DataConnections(context), _deployment.Keys, _deployment.Randomness);
}
