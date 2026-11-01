using System;
using System.Linq;
using System.Threading.Tasks;
using Janus.Authentication.Events;
using Janus.Authentication.Invitations;
using Janus.Core;
using Janus.Storage.Authentication.Events;
using Janus.Storage.Authentication.Invitations;
using Janus.Storage.Authorization.Grants;
using Janus.Storage.Identity.Accounts;
using Janus.Storage.Identity.Audit;
using Janus.Storage.Identity.Organizations;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// The membership an administrator ends, over the rows (IDN-MEM-001, REG-MAIL-003).
/// </summary>
[Trait("kind", "integration")]
public sealed class MembershipEndingTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new(database);

    /// <summary>
    /// IDN-MEM-001 AC1 and AC2: the current membership of the organization ends and its
    /// row stays, carrying when it began and when it ended; a second end, or an end in
    /// an organization the account never belonged to, finds nothing; and the account
    /// may be invited into the organization again.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_MEM_001_AC2_TheMembershipEndsAndItsRecordStaysAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        OrganizationId elsewhere = await _deployment.OrganizationAsync(Noon);
        MembershipId attached = await AttachAsync(subject, organization, Noon);

        Assert.Equal(attached, await EndAsync(subject, organization, Noon.AddDays(1)));
        Assert.Null(await EndAsync(subject, organization, Noon.AddDays(2)));
        Assert.Null(await EndAsync(subject, elsewhere, Noon.AddDays(2)));

        MembershipId again = await AttachAsync(subject, organization, Noon.AddDays(3));

        await using StoreContext reading = database.Context();

        var held = (await new MembershipStore(reading).FindBySubjectAsync(
                subject,
                TestContext.Current.CancellationToken))
            .ToDictionary(membership => membership.Id);

        Assert.Equal(2, held.Count);
        Assert.Equal((Noon, Noon.AddDays(1)), (held[attached].CreatedAt, held[attached].EndedAt));
        Assert.True(held[again].IsCurrent);
    }

    /// <summary>
    /// REG-MAIL-003 AC3: ending a membership writes the membership and nothing of the
    /// account: its state, and who or what placed it there, read the same afterwards.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_MAIL_003_AC3_EndingTheMembershipLeavesTheAccountsStateAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);

        _ = await AttachAsync(subject, organization, Noon);

        (AccountState, SuspensionOrigin?, bool, DeletionOrigin?) before = await StandingAsync(subject);

        Assert.NotNull(await EndAsync(subject, organization, Noon.AddDays(1)));
        Assert.Equal(before, await StandingAsync(subject));
    }

    /// <summary>
    /// IDN-MEM-001, CONV-DESIGN-003 AC6: two ends of one membership at once each read
    /// it under its row's lock, so it ends once and the second finds nothing to end.
    /// </summary>
    [Fact]
    public async Task IDN_MEM_001_TwoEndsAtOnceEndTheMembershipOnceAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);

        _ = await AttachAsync(subject, organization, Noon);

        MembershipId?[] ended = await Task.WhenAll(
            EndAsync(subject, organization, Noon.AddDays(1)),
            EndAsync(subject, organization, Noon.AddDays(1)));

        Assert.Single(ended, end => end is not null);
    }

    /// <summary>
    /// IDN-MEM-001 (D-240): an end set and ahead is brought forward under the row's lock:
    /// the membership ends at the instant given and the end it replaced is returned; one
    /// with no end set, one whose end is not after the instant, and one already ended
    /// each find nothing.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_MEM_001_AnEndSetAndAheadIsBroughtForwardAsync()
    {
        SubjectId member = await _deployment.AccountAsync(Noon);
        SubjectId unscheduled = await _deployment.AccountAsync(Noon);
        SubjectId passed = await _deployment.AccountAsync(Noon);
        SubjectId setter = await _deployment.AccountAsync(Noon);
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        MembershipId attached = await AttachAsync(member, organization, Noon);

        _ = await AttachAsync(unscheduled, organization, Noon);
        _ = await AttachAsync(passed, organization, Noon);
        _ = await SetEndAsync(member, organization, Noon.AddDays(30), setter);
        _ = await SetEndAsync(passed, organization, Noon.AddDays(1), setter);

        Assert.Null(await BringForwardAsync(unscheduled, organization, Noon.AddDays(2)));
        Assert.Null(await BringForwardAsync(passed, organization, Noon.AddDays(2)));

        (MembershipEndDue Ended, DateTimeOffset Replaced)? brought =
            await BringForwardAsync(member, organization, Noon.AddDays(2));

        Assert.NotNull(brought);
        Assert.Equal((attached, member, organization, setter), (brought.Value.Ended.Membership, brought.Value.Ended.Member, brought.Value.Ended.Organization, brought.Value.Ended.SetBy));
        Assert.Equal(Noon.AddDays(30), brought.Value.Replaced);
        Assert.Null(await BringForwardAsync(member, organization, Noon.AddDays(3)));

        await using StoreContext reading = database.Context();

        Janus.Identity.Organizations.Membership ended = Assert.Single(await new MembershipStore(reading).FindBySubjectAsync(
            member,
            TestContext.Current.CancellationToken));

        Assert.Equal(Noon.AddDays(2), ended.EndedAt);
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    private async Task<(AccountState, SuspensionOrigin?, bool, DeletionOrigin?)> StandingAsync(SubjectId subject)
    {
        await using StoreContext reading = database.Context();

        AccountRecord account = await reading.Accounts
            .SingleAsync(account => account.Subject == subject, TestContext.Current.CancellationToken);

        return (account.State, account.SuspendedBy, account.RestrictionHeld, account.DeletingBy);
    }

    private async Task<MembershipId> AttachAsync(SubjectId subject, OrganizationId organization, DateTimeOffset at)
    {
        await using StoreContext writing = database.Context();
        await using UnitOfWork work = new(writing);

        await work.BeginAsync(TestContext.Current.CancellationToken);

        MembershipId attached = (await new MembershipAttachment(
                    new MembershipStore(writing),
                    new GrantStore(writing, new DataConnections(writing)),
                    new GrantAudit(
                        new AuditStore(writing, new DataConnections(writing), _deployment.Ring, _deployment.Randomness),
                        TimeProvider.System),
                    new EventOutbox(new PendingEvents(writing), work),
                    new DataConnections(writing),
                    TimeProvider.System)
                .AttachAsync(
                    subject,
                    organization,
                    invitation: null,
                    [],
                    [],
                    subject,
                    "invitation:reason",
                    AccessContext.Of(subject),
                    multiple: false,
                    grantEnd: null,
                    at,
                    TestContext.Current.CancellationToken))
            .Match(made => made, error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

        await work.CommitAsync(TestContext.Current.CancellationToken);

        return attached;
    }

    private async Task<MembershipId?> SetEndAsync(
        SubjectId subject,
        OrganizationId organization,
        DateTimeOffset endsAt,
        SubjectId setBy)
    {
        await using StoreContext writing = database.Context();
        await using UnitOfWork work = new(writing);

        Assert.True((await work.BeginAsync(TestContext.Current.CancellationToken)).Match(_ => true, _ => false));

        MembershipId? set = await new MembershipEnding(new MembershipStore(writing))
            .SetEndAsync(subject, organization, endsAt, setBy, "The contract ends.", TestContext.Current.CancellationToken);

        Assert.True((await work.CommitAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));

        return set;
    }

    private async Task<(MembershipEndDue Ended, DateTimeOffset Replaced)?> BringForwardAsync(
        SubjectId subject,
        OrganizationId organization,
        DateTimeOffset at)
    {
        await using StoreContext writing = database.Context();
        await using UnitOfWork work = new(writing);

        Assert.True((await work.BeginAsync(TestContext.Current.CancellationToken)).Match(_ => true, _ => false));

        (MembershipEndDue Ended, DateTimeOffset Replaced)? brought = await new MembershipEnding(new MembershipStore(writing))
            .BringForwardAsync(subject, organization, at, TestContext.Current.CancellationToken);

        Assert.True((await work.CommitAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));

        return brought;
    }

    private async Task<MembershipId?> EndAsync(SubjectId subject, OrganizationId organization, DateTimeOffset at)
    {
        await using StoreContext writing = database.Context();
        await using UnitOfWork work = new(writing);
        var ending = new MembershipEnding(new MembershipStore(writing));

        // The end is read before the transaction and ended inside it, as the operation does.
        _ = await ending.FindAsync(subject, organization, TestContext.Current.CancellationToken);

        Assert.True((await work.BeginAsync(TestContext.Current.CancellationToken)).Match(_ => true, _ => false));

        EndedMembership? ended = await ending.EndAsync(subject, organization, at, TestContext.Current.CancellationToken);

        Assert.True((await work.CommitAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));

        return ended?.Membership;
    }
}
