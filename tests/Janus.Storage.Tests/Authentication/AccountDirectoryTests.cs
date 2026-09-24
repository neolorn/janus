using System;
using System.Linq;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Identity.Accounts;
using Janus.Identity.Preferences;
using Janus.Privacy.Outbox;
using Janus.Storage.Authentication.Accounts;
using Janus.Storage.Identity.Accounts;
using Janus.Storage.Identity.Preferences;
using Janus.Storage.Identity.Profiles;
using Janus.Storage.Privacy.Outbox;
using Janus.Storage.Privacy.SubjectKeys;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// The account directory's writes that reach beyond the account row (PRIV-RIGHT-004).
/// </summary>
[Trait("kind", "integration")]
public sealed class AccountDirectoryTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new(database);

    /// <summary>
    /// PRIV-RIGHT-004 AC2: lifting a restriction makes the account active and writes the
    /// delivery that tells every subject-event handler it is lifted, in one transaction.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_004_AC2_TheLiftTellsTheSubscribersAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await using (StoreContext restricting = database.Context())
        {
            var accounts = new AccountStore(restricting);
            Account account = Assert.IsType<Account>(
                await accounts.FindBySubjectAsync(subject, TestContext.Current.CancellationToken));

            account.Restrict();
            await accounts.RecordTransitionAsync(account, TestContext.Current.CancellationToken);
            await restricting.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (StoreContext lifting = database.Context())
        {
            await using var work = new UnitOfWork(lifting);

            await work.BeginAsync(TestContext.Current.CancellationToken);
            await Directory(lifting).LiftRestrictionAsync(
                subject,
                Noon.AddDays(1),
                TestContext.Current.CancellationToken);
            await work.CommitAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();

        DeliveryRecord told = await reading.Outbox
            .SingleAsync(delivery => delivery.Subject == subject, TestContext.Current.CancellationToken);

        Assert.Equal(
            AccountState.Active,
            await reading.Accounts
                .Where(account => account.Subject == subject)
                .Select(account => account.State)
                .SingleAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            (SubjectEventKind.RestrictionChanged, false, Noon.AddDays(1)),
            (told.Kind, told.Restricted, told.RaisedAt));
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    private AccountDirectory Directory(StoreContext context) => new(
        new AccountStore(context),
        new ProfileStore(context, _deployment.Keys, _deployment.Randomness),
        new ProfilePhotoStore(context, _deployment.Keys, _deployment.Randomness),
        new SubjectKeyStore(context, _deployment.Keys, _deployment.Randomness),
        new PreferenceStore(context, _deployment.Keys, _deployment.Randomness),
        PreferenceDeclarations.None,
        new OutboxStore(context, new FixedTime(Noon)));
}
