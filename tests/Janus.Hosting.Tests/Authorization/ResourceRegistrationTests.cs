using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
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
/// (AUTHZ-INHERIT-001, AUTHZ-INHERIT-002, AUTHZ-SCOPE-001, AUTHZ-MODEL-003,
/// IDN-LIFE-002a).
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
        Ok(await RegisteredAsync(new ResourceRegistration(document, written.Organization, workspace, written.Account)));

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
            new ResourceRegistration(document, written.Organization, before, written.Account),
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
            new ResourceRegistration(document, written.Organization, workspace, written.Account),
        ]));

        Error refused = Refused(await RegisteredManyAsync(
        [
            new ResourceRegistration(early, written.Organization, late, written.Account),
            new ResourceRegistration(late, written.Organization, null, null),
        ]));

        Assert.Equal(2, await AncestryOfAsync(document));
        Assert.Equal("containedIn", Invalid(refused));
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
            new ResourceRegistration(document, ours.Organization, workspace, ours.Account),
        ]));

        Assert.Equal(
            "containedIn",
            Invalid(Refused(await RegisteredAsync(
                new ResourceRegistration(Reference(Document), ours.Organization, elsewhere, ours.Account)))));
        Assert.Equal("containedIn", Invalid(Refused(await MovedAsync(document, elsewhere))));
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
            Invalid(Refused(await RegisteredAsync(
                new ResourceRegistration(Reference(Document), written.Organization, note, written.Account)))));
        Assert.Equal(
            "containedIn",
            Invalid(Refused(await RegisteredAsync(
                new ResourceRegistration(Reference(Document), written.Organization, null, written.Account)))));
        Assert.Equal(
            "containedIn",
            Invalid(Refused(await RegisteredAsync(
                new ResourceRegistration(Reference(Workspace), written.Organization, workspace, null)))));
        Assert.Equal("containedIn", Invalid(Refused(await MovedAsync(note, null))));
    }

    /// <summary>
    /// AUTHZ-INHERIT-002, D-166: a type outside the vocabulary the model fixes is a
    /// request that cannot be read, at a registration and at a move; every refusal on
    /// what a registration means names its member as invalid; and a batch refused for
    /// its last record writes none of those before it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_INHERIT_002_ARefusalOnMeaningIsInvalidAndARefusedBatchWritesNothingAsync()
    {
        Case ours = await BeginAsync();
        Case theirs = await BeginAsync();
        ResourceReference workspace = Reference(Workspace);
        ResourceReference elsewhere = Reference(Workspace);
        ResourceReference first = Reference(Document);
        var undeclared = ResourceType.Parse("journey");

        Ok(await RegisteredManyAsync(
        [
            new ResourceRegistration(workspace, ours.Organization, null, null),
            new ResourceRegistration(elsewhere, theirs.Organization, null, null),
        ]));

        Assert.Equal(
            "resourceType",
            Malformed(Refused(await RegisteredAsync(
                new ResourceRegistration(Reference(undeclared), ours.Organization, null, null)))));
        Assert.Equal("resourceType", Malformed(Refused(await MovedAsync(Reference(undeclared), workspace))));
        Assert.Equal(
            "resourceId",
            Invalid(Refused(await RegisteredAsync(
                new ResourceRegistration(workspace, ours.Organization, null, null)))));
        Assert.Equal("resourceId", Invalid(Refused(await MovedAsync(Reference(Document), workspace))));
        Assert.Equal(
            "containedIn",
            Invalid(Refused(await RegisteredAsync(
                new ResourceRegistration(Reference(Document), ours.Organization, Reference(Workspace), ours.Account)))));
        Assert.Equal(
            "containedIn",
            Invalid(Refused(await RegisteredAsync(
                new ResourceRegistration(Reference(Document), ours.Organization, elsewhere, ours.Account)))));
        Assert.Equal(
            "containedIn",
            Invalid(Refused(await RegisteredAsync(
                new ResourceRegistration(Reference(Document), ours.Organization, null, ours.Account)))));
        Assert.Equal(
            "subject",
            Invalid(Refused(await RegisteredManyAsync(
            [
                new ResourceRegistration(first, ours.Organization, workspace, ours.Account),
                new ResourceRegistration(Reference(Document), ours.Organization, workspace, null),
            ]))));
        Assert.Equal(0, await RowsOfAsync(first));
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
            Malformed(Refused(await RegisteredAsync(new ResourceRegistration(
                Reference(ResourceType.Parse("journey")),
                written.Organization,
                null,
                null)))));
        Assert.Equal(
            "resourceId",
            Invalid(Refused(await RegisteredAsync(
                new ResourceRegistration(workspace, written.Organization, null, null)))));
        Assert.Equal("resourceId", Invalid(Refused(await MovedAsync(Reference(Document), workspace))));
    }

    /// <summary>
    /// IDN-LIFE-002a AC1: a record of a sensitive type is registered only for a subject
    /// holding an account, so a record naming nobody, one naming a subject the library
    /// holds no account for, and one naming an account being deleted are each refused,
    /// and a type that is not sensitive needs no subject.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_LIFE_002a_AC1_ASensitiveRecordIsRegisteredOnlyForAnAccountHolderAsync()
    {
        Case written = await BeginAsync();
        ResourceReference workspace = Reference(Workspace);
        SubjectId leaving = await written.Deployment.AccountAsync(TestContext.Current.CancellationToken);
        SubjectId nobody;

        using (var randomness = RandomNumberGenerator.Create())
        {
            nobody = SubjectId.New(randomness);
        }

        await DeletingAsync(leaving);

        Ok(await RegisteredAsync(new ResourceRegistration(workspace, written.Organization, null, null)));

        Assert.Equal(
            "subject",
            Invalid(Refused(await RegisteredAsync(
                new ResourceRegistration(Reference(Document), written.Organization, workspace, null)))));
        Assert.Equal(
            "subject",
            Invalid(Refused(await RegisteredAsync(
                new ResourceRegistration(Reference(Document), written.Organization, workspace, nobody)))));
        Assert.Equal(
            "subject",
            Invalid(Refused(await RegisteredManyAsync(
            [
                new ResourceRegistration(Reference(Document), written.Organization, workspace, written.Account),
                new ResourceRegistration(Reference(Document), written.Organization, workspace, leaving),
            ]))));

        ResourceReference held = Reference(Document);

        Ok(await RegisteredAsync(new ResourceRegistration(held, written.Organization, workspace, written.Account)));

        Assert.Equal(1, await RowsOfAsync(held));
    }

    /// <summary>
    /// REC-AGE-001 AC3, REC-COL-003 (D-221): a record staff entered without a date of
    /// birth holds no answer, and no sensitive record registers under it; one entered
    /// with the person's own answer does.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REC_AGE_001_AC3_ASensitiveRecordRegistersUnderAnEntryOnlyWithThePersonsAnswerAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Case written = await BeginAsync();
        ResourceReference workspace = Reference(Workspace);
        RoleName entering = await written.Deployment.RoleAsync([Permissions.RecordCreate], cancellationToken);

        _ = await written.Deployment.GrantAsync(
            GrantSubject.Of(written.Account), entering, null, false, null, written.Organization, cancellationToken);

        Ok(await RegisteredAsync(new ResourceRegistration(workspace, written.Organization, null, null)));

        SubjectId unanswered = await EnteredAsync(written, dateOfBirth: null);
        SubjectId answered = await EnteredAsync(written, new DateOnly(1990, 1, 1));

        Assert.Equal(
            "subject",
            Invalid(Refused(await RegisteredAsync(
                new ResourceRegistration(Reference(Document), written.Organization, workspace, unanswered)))));
        Ok(await RegisteredAsync(
            new ResourceRegistration(Reference(Document), written.Organization, workspace, answered)));
    }

    /// <summary>
    /// AUTHZ-INHERIT-002 AC5, AUTHZ-GRANT-003 AC6 (D-203, D-206): a record that holds a
    /// registered record is refused with <c>authz.resource.inuse</c>, and one not
    /// registered with <c>api.request.invalid</c> naming <c>resource</c>, each changing
    /// nothing. A removal leaves no row and no ancestry row of the record, and revokes
    /// every live grant naming it with the nil subject as revoker and the reason
    /// <c>AUTHZ-INHERIT-002</c>, each recorded <c>authz.grant.revoked</c> naming its
    /// holder as subject and announced as <c>GrantChanged</c> <c>revoked</c> (D-218); a grant
    /// on its container stands, and the record registered again inherits nothing it held.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_INHERIT_002_AC5_ARemovedRegistrationLeavesNoAncestryAndNoLiveGrantAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Case written = await BeginAsync();
        SubjectId other = await written.Deployment.AccountAsync(cancellationToken);
        ResourceReference workspace = Reference(Workspace);
        ResourceReference document = Reference(Document);

        Ok(await RegisteredAsync(new ResourceRegistration(workspace, written.Organization, null, null)));
        Ok(await RegisteredAsync(new ResourceRegistration(document, written.Organization, workspace, written.Account)));

        GrantId onContainer = await written.Deployment.GrantAsync(
            GrantSubject.Of(written.Account), written.Role, workspace, false, null, null, cancellationToken);
        GrantId allowed = await written.Deployment.GrantAsync(
            GrantSubject.Of(other), written.Role, document, false, null, null, cancellationToken);
        GrantId denied = await written.Deployment.GrantAsync(
            GrantSubject.Of(written.Account), written.Role, document, true, null, null, cancellationToken);

        Error inUse = Refused(await RemovedAsync(workspace));
        Error unregistered = Refused(await RemovedAsync(Reference(Document)));

        Assert.Equal(ErrorCodes.ResourceInUse, inUse.Code);
        Assert.Equal("resource", Invalid(unregistered));
        Assert.Equal(1, await RowsOfAsync(workspace));
        Assert.Equal(2, await AncestryOfAsync(document));
        Assert.Equal(0, await RevokedAsync(allowed) + await RevokedAsync(denied));

        Ok(await RemovedAsync(document));

        Assert.Equal(0, await RowsOfAsync(document));
        Assert.Equal(0, await AncestryOfAsync(document));
        Assert.Equal(0, await AncestorRowsOfAsync(document));
        Assert.Equal(1, await RevokedAsync(allowed));
        Assert.Equal(1, await RevokedAsync(denied));
        Assert.Equal(0, await RevokedAsync(onContainer));
        Assert.Equal(1, await RevocationsRecordedAsync(allowed, other));
        Assert.Equal(1, await RevocationsRecordedAsync(denied, written.Account));
        Assert.Equal(1, await RevocationsAnnouncedAsync(allowed));
        Assert.Equal(1, await RevocationsAnnouncedAsync(denied));
        Assert.Equal(0, await RevocationsAnnouncedAsync(onContainer));

        Ok(await RegisteredAsync(new ResourceRegistration(document, written.Organization, workspace, written.Account)));

        Assert.False(await ChecksAsync(other, document));
        Assert.True(await ChecksAsync(written.Account, document));
        Assert.Equal(ErrorCodes.ResourceInUse, Refused(await RemovedAsync(workspace)).Code);
    }

    private static ResourceReference Reference(ResourceType type) =>
        new(type, ResourceId.Parse(Guid.NewGuid().ToString()));

    private static void Ok(Result outcome) =>
        outcome.Switch(() => { }, error => Assert.Fail(error.Code.ToString()));

    private static Error Refused(Result outcome) =>
        outcome.Match<Error>(() => throw new InvalidOperationException("The call was not refused."), error => error);

    private static string? Malformed(Error refused)
    {
        Assert.Equal(ErrorCodes.RequestMalformed, refused.Code);

        return refused.Details["member"].GetString();
    }

    private static string? Invalid(Error refused)
    {
        Assert.Equal(ErrorCodes.RequestInvalid, refused.Code);

        return refused.Details["member"].GetString();
    }

    private async Task<Case> BeginAsync()
    {
        var deployment = new Deployment(host);

        RoleName role = await deployment.BeginAsync([HostPermissions.Read], TestContext.Current.CancellationToken);
        SubjectId account = await deployment.AccountAsync(TestContext.Current.CancellationToken);

        return new Case(deployment, deployment.Organization, account, role);
    }

    // A record staff entered in the case's organization by the case's account.
    private async Task<SubjectId> EnteredAsync(Case written, DateOnly? dateOfBirth)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        return (await scope.ServiceProvider.GetRequiredService<IPersonRecords>().EnterAsync(
                AccessContext.Of(written.Account),
                new PersonRecordEntry(
                    written.Organization,
                    "en",
                    Name: null,
                    Email: null,
                    "+441632960031",
                    Consents: [],
                    dateOfBirth,
                    Reference: null),
                TestContext.Current.CancellationToken))
            .Match(subject => subject, error => throw new InvalidOperationException(error.Code.ToString()));
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

    private async Task<Result> RemovedAsync(ResourceReference resource)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<IResources>()
            .RemoveAsync(resource, TestContext.Current.CancellationToken);
    }

    // A grant revoked by the removal of the record it names, as AUTHZ-GRANT-003 records it.
    private async Task<int> RevokedAsync(GrantId grant)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.QuerySingleAsync<int>(
            """
            SELECT count(*)::int FROM identity.grants
            WHERE id = @id AND revoked_at IS NOT NULL
              AND revoked_by = '00000000-0000-0000-0000-000000000000'
              AND revocation_reason = 'AUTHZ-INHERIT-002';
            """,
            new { id = grant.Value });
    }

    private async Task<int> RevocationsRecordedAsync(GrantId grant, SubjectId holder)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.QuerySingleAsync<int>(
            """
            SELECT count(*)::int FROM identity.audit_records
            WHERE action = 'authz.grant.revoked'
              AND acting_subject = '00000000-0000-0000-0000-000000000000'
              AND principal IS NULL
              AND details ->> 'grant' = CAST(@id AS text)
              AND details ->> 'reason' = 'AUTHZ-INHERIT-002'
              AND details ->> 'role' IS NOT NULL
              AND subject = @holder;
            """,
            new { id = grant.Value, holder = holder.Value });
    }

    // The announcements that the grant was revoked, as the rows of the events table,
    // naming no actor since the host removed the record (chapter 10 section 5b, D-218).
    private async Task<int> RevocationsAnnouncedAsync(GrantId grant)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.QuerySingleAsync<int>(
            """
            SELECT count(*)::int FROM identity.events
            WHERE kind = 'GrantChanged' AND payload->>'Change' = 'revoked'
              AND payload->>'Actor' IS NULL
              AND payload->>'IdempotencyKey' LIKE @key;
            """,
            new { key = "grant-revoked:" + grant.Value + "@%" });
    }

    private async Task<int> AncestorRowsOfAsync(ResourceReference resource)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.QuerySingleAsync<int>(
            "SELECT count(*)::int FROM identity.ancestry WHERE ancestor_type = @type AND ancestor_id = @id;",
            new { type = resource.Type.ToString(), id = resource.Id.ToString() });
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
                new FilterSources<HostDocument>(reading.Ancestry, reading.Grants, reading.Consented, document => document.Id)
                    .Relationship("reviewer", reading.Reviewers),
                TestContext.Current.CancellationToken);

        return outcome.Match(() => true, _ => false);
    }

    // The account's deletion has begun, which leaves no right to hang a record off.
    private async Task DeletingAsync(SubjectId subject)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        _ = await connection.ExecuteAsync(
            """
            UPDATE identity.accounts
            SET state = 'deleting', deleting_by = 'self', deleting_since = now()
            WHERE subject = @subject;
            """,
            new { subject = subject.Value });
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
