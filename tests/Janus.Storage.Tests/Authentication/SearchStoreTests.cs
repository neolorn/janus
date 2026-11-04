using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Search;
using Janus.Core;
using Janus.Identity.Accounts;
using Janus.Identity.Identifiers;
using Janus.Identity.Organizations;
using Janus.Identity.Profiles;
using Janus.Storage.Authentication.Search;
using Janus.Storage.Identity.Accounts;
using Janus.Storage.Identity.Identifiers;
using Janus.Storage.Identity.Organizations;
using Janus.Storage.Identity.Profiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// What the staff search reads from the database and writes to it: the subjects it
/// indexes, its per-minute counts, and the channel each change is announced on
/// (IDN-SRCH-001 to IDN-SRCH-003, D-256).
/// </summary>
/// <param name="database">The database the class shares.</param>
[Trait("kind", "integration")]
public sealed class SearchStoreTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Far = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan Near = TimeSpan.FromMilliseconds(500);

    private readonly Deployment _deployment = new(database);

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    /// <summary>
    /// IDN-SRCH-001 AC4: an account is read with its display name and its verified email,
    /// and an email it has not verified is not read for the search.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_SRCH_001_AC4_AnUnverifiedIdentifierIsNotReadForTheSearchAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        string verified = Fresh("verified");
        string unverified = Fresh("unverified");

        await NameAsync(subject, "Ahmed Hassan");
        await IdentifyAsync(subject, verified, verify: true);
        await IdentifyAsync(subject, unverified, verify: false);

        SearchSubject? read = await FindAsync(subject);

        Assert.NotNull(read);
        Assert.Equal(SubjectKind.Account, read.Kind);
        Assert.Equal("Ahmed Hassan", read.DisplayName);
        Assert.True(EmailAddress.TryParse(verified, out EmailAddress canonical));
        Assert.Equal([canonical.Value], read.Emails);
        Assert.Empty(read.Phones);
    }

    /// <summary>
    /// IDN-SRCH-002 AC2: an account is read in the organizations it is a current member
    /// of, and not in one whose membership ended.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_SRCH_002_AC2_AFormerMemberIsReadInNoneOfItsFormerOrganizationsAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        OrganizationId current = await OrganizationAsync("Current");
        OrganizationId former = await OrganizationAsync("Former");

        await PlaceAsync(subject, current, until: null);
        await PlaceAsync(subject, former, until: Noon.AddHours(1));

        SearchSubject? read = await FindAsync(subject);

        Assert.Equal([current], read?.Organizations);
    }

    /// <summary>
    /// IDN-SRCH-002 AC3, IDN-SRCH-003 AC4: an erased account and one in a takedown's
    /// window are not read for the index, and their names are not answered.
    /// </summary>
    /// <param name="state">The account's state.</param>
    /// <param name="origin">What began its deletion, where something did.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(AccountState.Deleted, null)]
    [InlineData(AccountState.Deleting, DeletionOrigin.Takedown)]
    public async Task IDN_SRCH_002_AC3_AnErasedOrTakenDownAccountIsNotReadAsync(AccountState state, DeletionOrigin? origin)
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        SubjectId kept = await _deployment.AccountAsync(Noon);

        await NameAsync(subject, "Ahmed Hassan");
        await StandAsync(subject, state, origin);

        await using StoreContext reading = database.Context();
        var store = new SearchSubjectStore(reading, _deployment.Ring);

        Assert.Null(await store.FindAsync(subject, TestContext.Current.CancellationToken));
        Assert.Equal(
            [kept],
            (await store.NamesAsync([subject, kept], TestContext.Current.CancellationToken)).Keys);
    }

    /// <summary>
    /// IDN-SRCH-002 AC7, AC11: the count is held in the table, so two processes count one
    /// window; after searches in two minutes it holds one row for the caller, the later
    /// minute's, with its count.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_SRCH_002_AC11_TheWindowsHoldTheLaterMinuteAloneAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        DateTimeOffset first = Noon;
        DateTimeOffset second = Noon.AddMinutes(1);

        Assert.Equal(1, await CountAsync(subject, first));
        Assert.Equal(2, await CountAsync(subject, first));
        Assert.Equal(1, await CountAsync(subject, second));
        Assert.Equal(2, await CountAsync(subject, second));

        await using NpgsqlConnection connection = await OpenAsync();

        IEnumerable<(DateTimeOffset WindowStart, int Count)> held = await connection
            .QueryAsync<(DateTimeOffset WindowStart, int Count)>(new CommandDefinition(
                "SELECT window_start, count FROM identity.search_windows WHERE subject = @subject;",
                new { subject = subject.Value },
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal([(second, 2)], held);
    }

    /// <summary>
    /// IDN-SRCH-003 AC3: a change announced inside a transaction is heard once the
    /// transaction commits, and one rolled back is never heard; the notification carries
    /// the subject identifier and nothing else.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_SRCH_003_AC3_TheNotificationCarriesTheSubjectAloneOnCommitAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var signals = new SearchSignals(database.ConnectionString);
        var heard = new List<SubjectId>();
        var committed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        SubjectId rolledBack = Subjects.New();
        SubjectId announced = Subjects.New();
        string? payload = null;

        using var listening = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        await using NpgsqlConnection raw = await OpenAsync();

        raw.Notification += (_, notification) => payload = notification.Payload;
        _ = await raw.ExecuteAsync(new CommandDefinition("LISTEN " + SearchChannel.Name + ";", cancellationToken: cancellationToken));

        Task listened = await signals.ListenAsync(
            subject =>
            {
                lock (heard)
                {
                    heard.Add(subject);
                }

                if (subject == announced)
                {
                    committed.TrySetResult();
                }
            },
            listening.Token);

        Assert.False(listened.IsCompleted);

        await RaiseAsync(rolledBack, commit: false);
        await RaiseAsync(announced, commit: true);
        await committed.Task.WaitAsync(Far, cancellationToken);
        await raw.WaitAsync(Near, cancellationToken);

        await listening.CancelAsync();

        lock (heard)
        {
            Assert.Equal([announced], heard);
        }

        Assert.Equal(announced.Value, Guid.Parse(payload!));
    }

    // The tests of a class share one database, so each names its own values.
    private static string Fresh(string person) => person + "." + Guid.NewGuid().ToString("N") + "@Example.COM";

    private async Task<SearchSubject?> FindAsync(SubjectId subject)
    {
        await using StoreContext reading = database.Context();

        return await new SearchSubjectStore(reading, _deployment.Ring)
            .FindAsync(subject, TestContext.Current.CancellationToken);
    }

    private async Task<int> CountAsync(SubjectId subject, DateTimeOffset window)
    {
        await using StoreContext counting = database.Context();

        return await new SearchWindows(new DataConnections(counting))
            .CountAsync(subject, window, TestContext.Current.CancellationToken);
    }

    private async Task NameAsync(SubjectId subject, string name)
    {
        Assert.True(DisplayName.TryParse(name, out DisplayName shown));

        await using StoreContext writing = database.Context();
        var store = new ProfileStore(writing, _deployment.Ring, _deployment.Randomness);
        Profile profile = await store.FindBySubjectAsync(subject, TestContext.Current.CancellationToken);

        profile.SetDisplayName(shown);

        await store.RecordAsync(profile, TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task IdentifyAsync(SubjectId subject, string entered, bool verify)
    {
        Assert.True(EmailAddress.TryParse(entered, out EmailAddress address));

        var id = IdentifierId.New(TimeProvider.System);

        await using StoreContext writing = database.Context();
        var store = new IdentifierStore(writing, _deployment.Ring, _deployment.Randomness);
        IdentifierSet set = await store.FindBySubjectAsync(subject, TestContext.Current.CancellationToken);

        set.Add(Identifier.Email(id, subject, address, entered, Noon), maximum: 5);

        if (verify)
        {
            set.Verify(id, Noon);
        }

        await store.RecordAsync(set, TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<OrganizationId> OrganizationAsync(string name)
    {
        var id = new OrganizationId(Guid.CreateVersion7());

        await using StoreContext writing = database.Context();
        await new OrganizationStore(writing).CreateAsync(
            Organization.Create(id, name + " " + Guid.NewGuid().ToString("N"), Noon),
            TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);

        return id;
    }

    private async Task PlaceAsync(SubjectId subject, OrganizationId organization, DateTimeOffset? until)
    {
        Membership membership = Membership
            .Create(new MembershipId(Guid.CreateVersion7()), subject, organization, [], multiple: true, Noon)
            .Match(made => made, error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

        if (until is { } ended)
        {
            membership.End(ended);
        }

        await using StoreContext writing = database.Context();
        await new MembershipStore(writing).CreateAsync(membership, TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task StandAsync(SubjectId subject, AccountState state, DeletionOrigin? origin)
    {
        await using StoreContext writing = database.Context();
        AccountRecord record = await writing.Accounts
            .SingleAsync(row => row.Subject == subject, TestContext.Current.CancellationToken);

        record.State = state;
        record.DeletingBy = origin;
        record.DeletingSince = origin is null ? null : Noon;

        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task RaiseAsync(SubjectId subject, bool commit)
    {
        await using StoreContext context = database.Context();
        await using IDbContextTransaction transaction = await context.Database
            .BeginTransactionAsync(TestContext.Current.CancellationToken);

        await SearchChannel.RaiseAsync(context, subject, TestContext.Current.CancellationToken);

        if (commit)
        {
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }
        else
        {
            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
        }
    }

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(database.ConnectionString);

        await connection.OpenAsync(TestContext.Current.CancellationToken);

        return connection;
    }
}
