using System;
using System.Linq;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Identity.Accounts;
using Janus.Identity.Audit;
using Janus.Identity.Preferences;
using Janus.Privacy.Outbox;
using Janus.Privacy.Requests;
using Janus.Storage.Authentication.Accounts;
using Janus.Storage.Identity.Accounts;
using Janus.Storage.Identity.Audit;
using Janus.Storage.Identity.Preferences;
using Janus.Storage.Identity.Profiles;
using Janus.Storage.Privacy.Outbox;
using Janus.Storage.Privacy.Requests;
using Janus.Storage.Privacy.SubjectKeys;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// The account directory's reads and writes that reach beyond the account row, and
/// what the account audit writes for an administrator (PRIV-RIGHT-004, IDN-LIFE-003).
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

    /// <summary>
    /// IDN-LIFE-003: the erasure request a window is recorded against is the one whose
    /// fulfilment began it, not an open one or a later one, and a subject with none has
    /// nothing to record against.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_LIFE_003_TheErasureRequestBehindTheWindowIsFoundAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        QueuedRequest behind = Requested(subject, PrivacyRequestType.Erasure);
        QueuedRequest open = Requested(subject, PrivacyRequestType.Erasure);
        QueuedRequest restriction = Requested(subject, PrivacyRequestType.Restriction);

        await using (StoreContext writing = database.Context())
        {
            var requests = new PrivacyRequestStore(writing);

            await requests.AddAsync(behind, TestContext.Current.CancellationToken);
            await requests.AddAsync(open, TestContext.Current.CancellationToken);
            await requests.AddAsync(restriction, TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);

            behind.Fulfil(Noon.AddHours(1));
            restriction.Fulfil(Noon.AddHours(2));
            await requests.RecordAsync(behind, TestContext.Current.CancellationToken);
            await requests.RecordAsync(restriction, TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();

        Assert.Equal(
            behind.Id,
            await Directory(reading).ErasureRequestAsync(subject, Noon.AddHours(3), TestContext.Current.CancellationToken));
        Assert.Null(await Directory(reading).ErasureRequestAsync(
            subject,
            Noon,
            TestContext.Current.CancellationToken));
        Assert.Null(await Directory(reading).ErasureRequestAsync(
            Subjects.New(),
            Noon.AddHours(3),
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// IDN-LIFE-003 and IDN-AUD-001: a cancellation on the subject's behalf is a
    /// security record naming the administrator, the subject and the request it is
    /// recorded against.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_LIFE_003_TheCancellationIsRecordedAgainstTheRequestAsync()
    {
        SubjectId administrator = await _deployment.AccountAsync(Noon);
        SubjectId subject = await _deployment.AccountAsync(Noon);
        var request = PrivacyRequestId.Of(Noon);

        await using (StoreContext writing = database.Context())
        {
            await new AccountAudit(new AuditStore(writing, new DataConnections(writing), _deployment.Keys, _deployment.Randomness), TimeProvider.System)
                .CancelledOnBehalfAsync(administrator, subject, request, Noon, TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();

        AuditRecord read = Assert.Single(
            await new AuditStore(reading, new DataConnections(reading), _deployment.Keys, _deployment.Randomness)
                .FindBySubjectAsync(subject, TestContext.Current.CancellationToken));

        Assert.Equal(
            (AuditCategory.Security, AuditActions.DeletionCancelled, administrator, subject),
            (read.Category, read.Action, read.ActingSubject, read.EffectiveSubject));
        Assert.Equal(request.ToString(), read.Details["request"].GetString());
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    private static QueuedRequest Requested(SubjectId subject, PrivacyRequestType type) =>
        QueuedRequest.Entered(
            new PrivacyRequestEntry(
                subject,
                type,
                "please act on this",
                new DateOnly(2026, 9, 23),
                "letter",
                "national identity card seen"),
            Noon,
            new Deadline(Noon.AddDays(30), Noon.AddDays(20), Noon.AddDays(25)));

    private AccountDirectory Directory(StoreContext context) => new(
        context,
        new AccountStore(context),
        new ProfileStore(context, _deployment.Keys, _deployment.Randomness),
        new ProfilePhotoStore(context, _deployment.Keys, _deployment.Randomness),
        new SubjectKeyStore(context, _deployment.Keys, _deployment.Randomness),
        new PreferenceStore(context, _deployment.Keys, _deployment.Randomness),
        PreferenceDeclarations.None,
        new OutboxStore(context, new FixedTime(Noon)));
}
