using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Janus.Authorization.Grants;
using Janus.Authorization.Roles;
using Janus.Core;
using Janus.Identity.Organizations;
using Janus.Storage.Authentication.Invitations;
using Janus.Storage.Authorization.Grants;
using Janus.Storage.Authorization.Roles;
using Janus.Storage.Identity.Organizations;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// The membership an acknowledged invitation attaches, over the rows (REG-INV-001,
/// IDN-LIFE-009a, IDN-MEM-002).
/// </summary>
[Trait("kind", "integration")]
public sealed class MembershipAttachmentTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    private static readonly RoleName Clerk = RoleName.Parse("clerk");

    private readonly Deployment _deployment = new(database);

    /// <summary>
    /// REG-INV-001 AC3: the membership reads back carrying the documents at the
    /// versions acknowledged and when; each role is granted once across the
    /// organization, as given by who invited them, however often the invitation names
    /// it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_INV_001_AC3_TheMembershipCarriesTheAcknowledgementAndTheGrantsAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        SubjectId inviter = await _deployment.AccountAsync(Noon);
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        InvitationDocument[] documents = [new("staff-handbook", "3"), new("conduct", "1")];

        await RoleAsync();

        MembershipId attached = await AttachAsync(
            subject,
            organization,
            documents,
            [Clerk, Clerk],
            inviter,
            multiple: false);

        await using StoreContext reading = database.Context();

        Membership membership = Assert.Single(await new MembershipStore(reading).FindBySubjectAsync(
            subject,
            TestContext.Current.CancellationToken));
        Grant grant = Assert.Single(await new GrantStore(reading, new DataConnections(reading)).HeldByAsync(
            [GrantSubject.Of(subject)],
            organization,
            Noon,
            TestContext.Current.CancellationToken));

        Assert.Equal(attached, membership.Id);
        Assert.Equal(documents, membership.Acknowledgement?.Documents);
        Assert.Equal(Noon, membership.Acknowledgement?.At);
        Assert.Equal((Clerk, GrantKind.Stored, false), (grant.Role, grant.Kind, grant.Deny));
        Assert.Equal((inviter, Noon, "invitation:reason"), (grant.GrantedBy, grant.GrantedAt, grant.Reason));
        Assert.Null(grant.ResourceType);
        Assert.Null(grant.ExpiresAt);
    }

    /// <summary>
    /// REG-INV-001: a role the account already holds across the organization is not
    /// granted a second time; the grant it held stands as it was.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_INV_001_AGrantTheAccountHoldsIsNotWrittenAgainAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);

        await RoleAsync();

        GrantId held = await GrantedAsync(subject, organization);

        _ = await AttachAsync(subject, organization, [], [Clerk], subject, multiple: false);

        await using StoreContext reading = database.Context();

        Grant grant = Assert.Single(await new GrantStore(reading, new DataConnections(reading)).HeldByAsync(
            [GrantSubject.Of(subject)],
            organization,
            Noon,
            TestContext.Current.CancellationToken));

        Assert.Equal((held, "held before"), (grant.Id, grant.Reason));
    }

    /// <summary>
    /// IDN-MEM-002: an account that may hold no further membership is refused with the
    /// code, and nothing is written.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_MEM_002_AnAccountThatMayHoldNoMoreIsRefusedAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        OrganizationId first = await _deployment.OrganizationAsync(Noon);
        OrganizationId second = await _deployment.OrganizationAsync(Noon);

        _ = await AttachAsync(subject, first, [], [], subject, multiple: false);

        await using StoreContext writing = database.Context();

        Result<MembershipId> refused = await Attachment(writing).AttachAsync(
            subject,
            second,
            [],
            [],
            subject,
            "invitation:reason",
            multiple: false,
            Noon,
            TestContext.Current.CancellationToken);

        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            ErrorCodes.MembershipLimitReached,
            refused.Match(_ => throw new Xunit.Sdk.XunitException("The membership attached."), error => error.Code));
        Assert.Equal(
            1,
            await writing.Memberships.CountAsync(row => row.Subject == subject, TestContext.Current.CancellationToken));
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    private static MembershipAttachment Attachment(StoreContext context) =>
        new(new MembershipStore(context), new GrantStore(context, new DataConnections(context)), TimeProvider.System);

    private async Task<MembershipId> AttachAsync(
        SubjectId subject,
        OrganizationId organization,
        IReadOnlyList<InvitationDocument> documents,
        IReadOnlyList<RoleName> roles,
        SubjectId inviter,
        bool multiple)
    {
        await using StoreContext writing = database.Context();

        MembershipId attached = (await Attachment(writing).AttachAsync(
                subject,
                organization,
                documents,
                roles,
                inviter,
                "invitation:reason",
                multiple,
                Noon,
                TestContext.Current.CancellationToken))
            .Match(made => made, error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);

        return attached;
    }

    private async Task<GrantId> GrantedAsync(SubjectId subject, OrganizationId organization)
    {
        Grant grant = Grant.Create(
                GrantId.New(TimeProvider.System),
                GrantSubject.Of(subject),
                Clerk,
                organization,
                on: null,
                deny: false,
                GrantKind.Stored,
                expiresAt: null,
                subject,
                Noon.AddDays(-1),
                "held before")
            .Match(
                written => written,
                error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

        await using StoreContext writing = database.Context();

        await new GrantStore(writing, new DataConnections(writing))
            .CreateAsync(grant, TestContext.Current.CancellationToken);

        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);

        return grant.Id;
    }

    private async Task RoleAsync()
    {
        await using StoreContext writing = database.Context();

        if (!await writing.Roles.AnyAsync(row => row.Name == Clerk, TestContext.Current.CancellationToken))
        {
            await new RoleStore(writing).CreateAsync(
                Role.Of(Clerk, [Permissions.GrantRead]),
                TestContext.Current.CancellationToken);

            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }
}
