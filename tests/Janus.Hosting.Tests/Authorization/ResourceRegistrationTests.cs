using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// The host's records as the host tells the library about them, through the one seam
/// it has for that, and the inheritance the gate then reads from them
/// (AUTHZ-INHERIT-001, AUTHZ-INHERIT-002, AUTHZ-SCOPE-001, AUTHZ-MODEL-003).
/// </summary>
[Trait("kind", "integration")]
public sealed class ResourceRegistrationTests(HostFixture host) : IClassFixture<HostFixture>
{
    private static readonly ResourceType Document = ResourceType.Parse("document");
    private static readonly ResourceType Note = ResourceType.Parse("note");
    private static readonly ResourceType Report = ResourceType.Parse("report");
    private static readonly ResourceType Workspace = ResourceType.Parse("workspace");

    /// <summary>
    /// AUTHZ-INHERIT-002 AC1, AUTHZ-INHERIT-001: a record the host registers in a
    /// container is reached by a grant on the container from the moment it is
    /// registered.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_INHERIT_002_AC1_ARecordTheHostRegistersInheritsFromItsContainerAsync()
    {
        Case written = await BeginAsync();
        ResourceReference workspace = Reference(Workspace);
        ResourceReference document = Reference(Document);

        Ok(await RegisteredAsync(new ResourceRegistration(workspace, written.Organization, null, null)));
        Ok(await RegisteredAsync(new ResourceRegistration(document, written.Organization, workspace, null)));

        Assert.False(await ChecksAsync(written.Account, document));

        _ = await written.Deployment.GrantAsync(
            GrantSubject.Of(written.Account),
            written.Role,
            workspace,
            false,
            null,
            null,
            TestContext.Current.CancellationToken);

        Assert.True(await ChecksAsync(written.Account, document));
        Assert.Equal(2, await AncestryOfAsync(document));
    }

    /// <summary>
    /// AUTHZ-INHERIT-002 AC1: a host that opened the unit of work and let it go without
    /// committing leaves neither the record nor its ancestry.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_INHERIT_002_AC1_AHostsRollbackLeavesNeitherTheRecordNorItsAncestryAsync()
    {
        Case written = await BeginAsync();
        ResourceReference workspace = Reference(Workspace);

        await using (AsyncServiceScope scope = host.Services.CreateAsyncScope())
        {
            IUnitOfWork work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await work.BeginAsync(TestContext.Current.CancellationToken);

            Ok(await scope.ServiceProvider.GetRequiredService<IResources>().RegisterAsync(
                new ResourceRegistration(workspace, written.Organization, null, null),
                TestContext.Current.CancellationToken));
        }

        Assert.Equal(0, await RowsOfAsync(workspace));
        Assert.Equal(0, await AncestryOfAsync(workspace));
    }

    /// <summary>
    /// AUTHZ-INHERIT-002: a record the host moves to another container is reached from
    /// the new one and no longer from the old.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_INHERIT_002_AMovedRecordInheritsFromItsNewContainerAloneAsync()
    {
        Case written = await BeginAsync();
        ResourceReference before = Reference(Workspace);
        ResourceReference after = Reference(Workspace);
        ResourceReference document = Reference(Document);

        Ok(await RegisteredManyAsync(
        [
            new ResourceRegistration(before, written.Organization, null, null),
            new ResourceRegistration(after, written.Organization, null, null),
            new ResourceRegistration(document, written.Organization, before, null),
        ]));

        _ = await written.Deployment.GrantAsync(
            GrantSubject.Of(written.Account),
            written.Role,
            before,
            false,
            null,
            null,
            TestContext.Current.CancellationToken);

        Assert.True(await ChecksAsync(written.Account, document));

        Ok(await MovedAsync(document, after));

        Assert.False(await ChecksAsync(written.Account, document));

        _ = await written.Deployment.GrantAsync(
            GrantSubject.Of(written.Account),
            written.Role,
            after,
            false,
            null,
            null,
            TestContext.Current.CancellationToken);

        Assert.True(await ChecksAsync(written.Account, document));
    }

    /// <summary>
    /// AUTHZ-INHERIT-002: a bulk registration places each container before its
    /// contents, and one whose contents come first records nothing at all.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_INHERIT_002_ABulkRegistrationTakesContainersBeforeContentsAsync()
    {
        Case written = await BeginAsync();
        ResourceReference workspace = Reference(Workspace);
        ResourceReference document = Reference(Document);
        ResourceReference early = Reference(Document);
        ResourceReference late = Reference(Workspace);

        Ok(await RegisteredManyAsync(
        [
            new ResourceRegistration(workspace, written.Organization, null, null),
            new ResourceRegistration(document, written.Organization, workspace, null),
        ]));

        Error refused = Refused(await RegisteredManyAsync(
        [
            new ResourceRegistration(early, written.Organization, late, null),
            new ResourceRegistration(late, written.Organization, null, null),
        ]));

        Assert.Equal(2, await AncestryOfAsync(document));
        Assert.Equal("containedIn", Member(refused));
        Assert.Equal(0, await RowsOfAsync(early));
        Assert.Equal(0, await RowsOfAsync(late));
    }

    /// <summary>
    /// AUTHZ-SCOPE-001: a record is never placed in a container of another
    /// organization, so no grant there reaches it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_SCOPE_001_ARecordIsNotPlacedInAnotherOrganizationsContainerAsync()
    {
        Case ours = await BeginAsync();
        Case theirs = await BeginAsync();
        ResourceReference workspace = Reference(Workspace);
        ResourceReference document = Reference(Document);
        ResourceReference elsewhere = Reference(Workspace);

        Ok(await RegisteredManyAsync(
        [
            new ResourceRegistration(workspace, ours.Organization, null, null),
            new ResourceRegistration(elsewhere, theirs.Organization, null, null),
            new ResourceRegistration(document, ours.Organization, workspace, null),
        ]));

        Assert.Equal(
            "containedIn",
            Member(Refused(await RegisteredAsync(
                new ResourceRegistration(Reference(Document), ours.Organization, elsewhere, null)))));
        Assert.Equal("containedIn", Member(Refused(await MovedAsync(document, elsewhere))));
    }

    /// <summary>
    /// AUTHZ-MODEL-003: a record is placed only where its type is declared contained,
    /// and outside every container only where its type belongs to the organization.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_MODEL_003_ARecordSitsOnlyWhereItsTypeIsDeclaredToAsync()
    {
        Case written = await BeginAsync();
        ResourceReference workspace = Reference(Workspace);
        ResourceReference note = Reference(Note);

        Ok(await RegisteredManyAsync(
        [
            new ResourceRegistration(workspace, written.Organization, null, null),
            new ResourceRegistration(note, written.Organization, workspace, null),
            new ResourceRegistration(Reference(Report), written.Organization, null, null),
        ]));

        Assert.Equal(
            "containedIn",
            Member(Refused(await RegisteredAsync(
                new ResourceRegistration(Reference(Document), written.Organization, note, null)))));
        Assert.Equal(
            "containedIn",
            Member(Refused(await RegisteredAsync(
                new ResourceRegistration(Reference(Document), written.Organization, null, null)))));
        Assert.Equal(
            "containedIn",
            Member(Refused(await RegisteredAsync(
                new ResourceRegistration(Reference(Workspace), written.Organization, workspace, null)))));
        Assert.Equal("containedIn", Member(Refused(await MovedAsync(note, null))));
    }

    /// <summary>
    /// AUTHZ-MODEL-001, LIB-HOST-002: a type the declaration does not name, a record
    /// registered twice, and a move of one never registered are each refused.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_MODEL_001_OnlyADeclaredTypeIsRegisteredAndOnlyOnceAsync()
    {
        Case written = await BeginAsync();
        ResourceReference workspace = Reference(Workspace);

        Ok(await RegisteredAsync(new ResourceRegistration(workspace, written.Organization, null, null)));

        Assert.Equal(
            "resourceType",
            Member(Refused(await RegisteredAsync(new ResourceRegistration(
                Reference(ResourceType.Parse("journey")),
                written.Organization,
                null,
                null)))));
        Assert.Equal(
            "resourceId",
            Member(Refused(await RegisteredAsync(
                new ResourceRegistration(workspace, written.Organization, null, null)))));
        Assert.Equal("resourceId", Member(Refused(await MovedAsync(Reference(Document), workspace))));
    }

    private static ResourceReference Reference(ResourceType type) =>
        new(type, ResourceId.Parse(Guid.NewGuid().ToString()));

    private static void Ok(Result outcome) =>
        outcome.Switch(() => { }, error => Assert.Fail(error.Code.ToString()));

    private static Error Refused(Result outcome) =>
        outcome.Match<Error>(() => throw new InvalidOperationException("The call was not refused."), error => error);

    private static string? Member(Error refused)
    {
        Assert.Equal(ErrorCodes.RequestMalformed, refused.Code);

        return refused.Details["member"].GetString();
    }

    private async Task<Case> BeginAsync()
    {
        var deployment = new Deployment(host);

        RoleName role = await deployment.BeginAsync([HostPermissions.Read], TestContext.Current.CancellationToken);
        SubjectId account = await deployment.AccountAsync(TestContext.Current.CancellationToken);

        return new Case(deployment, deployment.Organization, account, role);
    }

    private async Task<Result> RegisteredAsync(ResourceRegistration registration)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<IResources>()
            .RegisterAsync(registration, TestContext.Current.CancellationToken);
    }

    private async Task<Result> RegisteredManyAsync(IReadOnlyList<ResourceRegistration> registrations)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<IResources>()
            .RegisterManyAsync(registrations, TestContext.Current.CancellationToken);
    }

    private async Task<Result> MovedAsync(ResourceReference resource, ResourceReference? containedIn)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<IResources>()
            .MoveAsync(resource, containedIn, TestContext.Current.CancellationToken);
    }

    private async Task<bool> ChecksAsync(SubjectId account, ResourceReference resource)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await using HostContext reading = host.Context();

        Result outcome = await scope.ServiceProvider.GetRequiredService<IAccessGate>()
            .RequireAsync(
                AccessContext.Of(account),
                HostPermissions.Read,
                resource,
                new FilterSources<HostDocument>(reading.Ancestry, reading.Grants, document => document.Id)
                    .Relationship("reviewer", reading.Reviewers),
                TestContext.Current.CancellationToken);

        return outcome.Match(() => true, _ => false);
    }

    private async Task<int> RowsOfAsync(ResourceReference resource)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.QuerySingleAsync<int>(
            "SELECT count(*)::int FROM identity.resources WHERE resource_type = @type AND resource_id = @id;",
            new { type = resource.Type.ToString(), id = resource.Id.ToString() });
    }

    private async Task<int> AncestryOfAsync(ResourceReference resource)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.QuerySingleAsync<int>(
            "SELECT count(*)::int FROM identity.ancestry WHERE resource_type = @type AND resource_id = @id;",
            new { type = resource.Type.ToString(), id = resource.Id.ToString() });
    }

    // One case's organization, the account asked about, and the role its grants name.
    private sealed record Case(Deployment Deployment, OrganizationId Organization, SubjectId Account, RoleName Role);
}
