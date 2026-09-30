using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Janus.Authentication.Invitations;
using Janus.Authentication.Organizations;
using Janus.Core;
using Janus.Identity.Audit;
using Janus.Identity.Organizations;
using Janus.Storage.Authentication.Organizations;
using Janus.Storage.Identity.Audit;
using Janus.Storage.Identity.Organizations;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// An organization's lifecycle as the database keeps it, its current members, and what
/// a change leaves in the trail (IDN-ORG-002 to IDN-ORG-004, IDN-AUD-001, REG-MAIL-003).
/// </summary>
/// <remarks>
/// The port implementations are tested against the real database (D-156). One database
/// serves the class, so each test writes an organization and an actor of its own.
/// </remarks>
[Trait("kind", "integration")]
public sealed class OrganizationDirectoryTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new(database);

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    /// <summary>
    /// IDN-ORG-002: an organization created through the directory is found under its
    /// identifier, neither administrative nor deleting.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_ORG_002_AnOrganizationCreatedIsFoundAsync()
    {
        var created = new OrganizationId(Guid.CreateVersion7());
        string name = "Northern branch " + Guid.NewGuid().ToString("N");

        await using (StoreContext writing = database.Context())
        {
            await Directory(writing).CreateAsync(
                created,
                name,
                Noon,
                TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        OrganizationStanding? found = await FoundAsync(created);

        Assert.Equal(new OrganizationStanding(created, name, false, null, null), found);
    }

    /// <summary>
    /// IDN-ORG-003 and IDN-ORG-004: a deletion request is kept with the moment it was
    /// made, and a cancellation clears it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_ORG_004_ARequestIsKeptUntilItIsCancelledAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        DateTimeOffset requestedAt = Noon.AddDays(1);

        await using (StoreContext writing = database.Context())
        {
            Result requested = await Directory(writing).RequestDeletionAsync(
                organization,
                requestedAt,
                TestContext.Current.CancellationToken);

            Assert.Null(requested.Match<Error?>(() => null, error => error));

            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        OrganizationStanding? deleting = await FoundAsync(organization);

        await using (StoreContext writing = database.Context())
        {
            await Directory(writing).CancelDeletionAsync(organization, TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(requestedAt, deleting!.DeletionRequestedAt);
        Assert.Null((await FoundAsync(organization))!.DeletionRequestedAt);
    }

    /// <summary>
    /// IDN-ORG-003 AC1: the members whose sessions a deletion request ends are those
    /// holding a membership of the organization now, each named once; a membership that
    /// has ended names nobody.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_ORG_003_AC1_OnlyCurrentMembersAreNamedAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        SubjectId twice = await _deployment.AccountAsync(Noon);
        SubjectId departed = await _deployment.AccountAsync(Noon);

        await PlaceAsync(twice, organization, until: null);
        await PlaceAsync(twice, organization, until: null);
        await PlaceAsync(departed, organization, Noon.AddDays(1));

        await using StoreContext reading = database.Context();

        Assert.Equal(
            [twice],
            await Directory(reading).MembersAsync(organization, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// IDN-ORG-003 and IDN-AUD-001 AC1: a lifecycle change is recorded under the
    /// organization with the reason, the actor as both identities, as a security event.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_ORG_003_AChangeIsRecordedUnderTheOrganizationAsync()
    {
        SubjectId actor = await _deployment.AccountAsync(Noon);
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);

        await using (StoreContext writing = database.Context())
        {
            await Audit(writing).RecordedAsync(
                AuditActions.OrganizationDeletionRequested,
                organization,
                "Closing the branch.",
                actor,
                breakGlassReason: null,
                Noon,
                TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        AuditRecord read = Assert.Single(await RecordsAsync(actor));

        Assert.Equal(AuditActions.OrganizationDeletionRequested, read.Action);
        Assert.Equal(AuditCategory.Security, read.Category);
        Assert.Equal(actor, read.ActingSubject);
        Assert.Equal(actor, read.EffectiveSubject);
        Assert.Equal(organization, read.Organization);
        Assert.Equal(["reason"], read.Details.Keys.ToArray());
        Assert.Equal("Closing the branch.", read.Details["reason"].GetString());
    }

    /// <summary>
    /// REG-MAIL-003 AC6 and chapter 10 section 5.24: an issue that takes over a former
    /// mailbox is recorded with the invitation, the choice and its reason; one that
    /// names none carries the invitation alone.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_MAIL_003_AC6_ATakeoverIsRecordedWithTheIssueAsync()
    {
        SubjectId actor = await _deployment.AccountAsync(Noon);
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        var replaced = new InvitationId(Guid.CreateVersion7());
        var plain = new InvitationId(Guid.CreateVersion7());

        await using (StoreContext writing = database.Context())
        {
            await Audit(writing).InvitationChangedAsync(
                AuditActions.InvitationIssued,
                organization,
                replaced,
                new MailboxTakeover(FormerMailbox.Replace, "The team keeps the correspondence."),
                actor,
                breakGlassReason: null,
                Noon,
                TestContext.Current.CancellationToken);
            await Audit(writing).InvitationChangedAsync(
                AuditActions.InvitationIssued,
                organization,
                plain,
                takeover: null,
                actor,
                breakGlassReason: null,
                Noon.AddMinutes(1),
                TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        IReadOnlyList<AuditRecord> read = await RecordsAsync(actor);
        AuditRecord taken = Assert.Single(read, record => record.Details["invitation"].GetGuid() == replaced.Value);
        AuditRecord issued = Assert.Single(read, record => record.Details["invitation"].GetGuid() == plain.Value);

        Assert.Equal(["formerMailbox", "invitation", "reason"], taken.Details.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("replace", taken.Details["formerMailbox"].GetString());
        Assert.Equal("The team keeps the correspondence.", taken.Details["reason"].GetString());
        Assert.Equal(organization, taken.Organization);
        Assert.Equal(["invitation"], issued.Details.Keys.ToArray());
    }

    private static OrganizationDirectory Directory(StoreContext context) =>
        new(context, new OrganizationStore(context));

    private OrganizationAudit Audit(StoreContext context) =>
        new(new AuditStore(context, new DataConnections(context), _deployment.Ring, _deployment.Randomness), TimeProvider.System);

    private async Task<OrganizationStanding?> FoundAsync(OrganizationId organization)
    {
        await using StoreContext reading = database.Context();

        return await Directory(reading).FindAsync(organization, TestContext.Current.CancellationToken);
    }

    private async Task<IReadOnlyList<AuditRecord>> RecordsAsync(SubjectId actor)
    {
        await using StoreContext reading = database.Context();

        return await new AuditStore(reading, new DataConnections(reading), _deployment.Ring, _deployment.Randomness)
            .FindBySubjectAsync(actor, TestContext.Current.CancellationToken);
    }

    private async ValueTask PlaceAsync(
        SubjectId subject,
        OrganizationId organization,
        DateTimeOffset? until)
    {
        Membership membership = Membership
            .Create(
                new MembershipId(Guid.CreateVersion7()),
                subject,
                organization,
                [],
                multiple: true,
                Noon)
            .Match(made => made, error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

        if (until is { } ended)
        {
            membership.End(ended);
        }

        await using StoreContext writing = database.Context();
        await new MembershipStore(writing).CreateAsync(
            membership,
            TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
