using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Janus.Identity.Organizations;
using Janus.Privacy.Erasures;
using Janus.Storage.Identity.Organizations;
using Janus.Storage.Privacy.Erasures;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests.Privacy;

/// <summary>
/// The erasure at the end of an organization deletion window, over the rows it reaches
/// (IDN-ORG-003, IDN-ORG-005, IDN-PRIN-003).
/// </summary>
[Trait("kind", "integration")]
public sealed class OrganizationStatesTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Window = TimeSpan.FromDays(30);

    private readonly Deployment _deployment = new(database);

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    /// <summary>
    /// IDN-ORG-003: a window that has run out is what the pass reads, and one that
    /// began later and one nobody opened are not.
    /// </summary>
    [Fact]
    public async Task IDN_ORG_003_TheWindowsThatHaveRunOutAreWhatThePassReadsAsync()
    {
        OrganizationId running = await DeletingAsync(Noon);
        OrganizationId standing = await CreateAsync();
        OrganizationId recent = await DeletingAsync(Noon + Window + TimeSpan.FromDays(1));

        await using StoreContext reading = database.Context();

        IReadOnlyList<OrganizationId> reached =
        [
            .. (await States(reading).DeletingSinceAsync(
                    Noon + Window,
                    TestContext.Current.CancellationToken))
                .Select(deletion => deletion.Organization),
        ];

        Assert.Contains(running, reached);
        Assert.DoesNotContain(standing, reached);
        Assert.DoesNotContain(recent, reached);
    }

    /// <summary>
    /// IDN-ORG-003 AC5, IDN-PRIN-003: the erasure leaves the row where it is and the
    /// identifier resolving, with what the organization was called replaced by that
    /// identifier and the instant it executed written down.
    /// </summary>
    [Fact]
    public async Task IDN_ORG_003_AC5_TheRowSurvivesAndTheNameBecomesTheIdentifierAsync()
    {
        OrganizationId organization = await DeletingAsync(Noon);

        _ = await EraseAsync(organization, Noon + Window);

        await using StoreContext reading = database.Context();
        Organization read = Assert.IsType<Organization>(
            await new OrganizationStore(reading).FindAsync(
                organization,
                TestContext.Current.CancellationToken));

        Assert.Equal(Noon + Window, read.ErasedAt);
        Assert.Equal(organization.Value.ToString("D", CultureInfo.InvariantCulture), read.Name);
    }

    /// <summary>
    /// IDN-ORG-003: the erasure ends every current membership of the organization and
    /// answers with the ones it ended, leaving an already ended one where it stands.
    /// </summary>
    [Fact]
    public async Task IDN_ORG_003_TheErasureEndsEveryCurrentMembershipAsync()
    {
        OrganizationId organization = await DeletingAsync(Noon);
        SubjectId first = await _deployment.AccountAsync(Noon);
        SubjectId second = await _deployment.AccountAsync(Noon);
        SubjectId gone = await _deployment.AccountAsync(Noon);

        await PlaceAsync(first, organization, until: null);
        await PlaceAsync(second, organization, until: null);
        await PlaceAsync(gone, organization, Noon.AddDays(1));

        IReadOnlyList<EndedMembership> ended = Assert.IsAssignableFrom<IReadOnlyList<EndedMembership>>(await EraseAsync(organization, Noon + Window));

        Assert.Equal(2, ended.Count);
        Assert.Contains(ended, membership => membership.Subject == first);
        Assert.Contains(ended, membership => membership.Subject == second);

        await using StoreContext reading = database.Context();
        IReadOnlyList<Membership> held = await new MembershipStore(reading)
            .FindByOrganizationAsync(organization, TestContext.Current.CancellationToken);

        Assert.All(held, membership => Assert.False(membership.IsCurrent));
        Assert.Equal(
            Noon.AddDays(1),
            Assert.Single(held, membership => membership.Subject == gone).EndedAt);
    }

    /// <summary>
    /// IDN-ORG-003, D-166 (155): the erasure leaves no domain of the organization
    /// readable. Each becomes the identifier; one still listed is removed at the
    /// erasure, one removed before keeps its instant, and another organization's
    /// domain of the same name is untouched.
    /// </summary>
    [Fact]
    public async Task IDN_ORG_003_TheErasureLeavesNoDomainOfTheOrganizationAsync()
    {
        OrganizationId organization = await DeletingAsync(Noon);
        OrganizationId other = await CreateAsync();

        string listed = "listed-" + Guid.NewGuid().ToString("N") + ".example";
        string dropped = "dropped-" + Guid.NewGuid().ToString("N") + ".example";

        await DomainAsync(organization, listed, removed: null);
        await DomainAsync(organization, dropped, Noon.AddDays(1));
        await DomainAsync(other, listed, removed: null);

        _ = await EraseAsync(organization, Noon + Window);

        await using NpgsqlConnection connection = await database.OpenAsync();

        List<(string Domain, DateTimeOffset? RemovedAt)> erased =
        [
            .. await connection.QueryAsync<(string Domain, DateTimeOffset? RemovedAt)>(new CommandDefinition(
                "SELECT domain, removed_at FROM identity.organization_domains "
                    + "WHERE organization = @organization ORDER BY removed_at",
                new { organization = organization.Value },
                cancellationToken: TestContext.Current.CancellationToken)),
        ];
        string? standing = await connection.ExecuteScalarAsync<string>(new CommandDefinition(
            "SELECT domain FROM identity.organization_domains WHERE organization = @other AND removed_at IS NULL",
            new { other = other.Value },
            cancellationToken: TestContext.Current.CancellationToken));

        string identifier = organization.Value.ToString("D", CultureInfo.InvariantCulture);

        Assert.Equal(
            [(identifier, (DateTimeOffset?)Noon.AddDays(1)), (identifier, Noon + Window)],
            erased);
        Assert.Equal(listed, standing);
    }

    /// <summary>
    /// IDN-ORG-003 AC3: the erasure does not execute before the window elapses, so a
    /// caller that asks for one a day early is answered nothing and writes nothing.
    /// </summary>
    [Fact]
    public async Task IDN_ORG_003_AC3_AnErasureBeforeTheWindowElapsesWritesNothingAsync()
    {
        OrganizationId organization = await DeletingAsync(Noon);

        Assert.Null(await EraseAsync(organization, Noon + Window - TimeSpan.FromDays(1)));

        await using StoreContext reading = database.Context();
        Organization read = Assert.IsType<Organization>(
            await new OrganizationStore(reading).FindAsync(
                organization,
                TestContext.Current.CancellationToken));

        Assert.Null(read.ErasedAt);
    }

    /// <summary>
    /// IDN-ORG-003 AC2, CONV-DESIGN-003 AC6: a cancellation and the erasure at once each
    /// decide on the organization's row under its lock, so either the window was
    /// cancelled and nothing is erased, or the erasure stands with its window.
    /// </summary>
    [Fact]
    public async Task IDN_ORG_003_AC2_ACancellationAndTheErasureAtOnceDoNotBothStandAsync()
    {
        OrganizationId organization = await DeletingAsync(Noon);

        await Task.WhenAll(EraseAsync(organization, Noon + Window).AsTask(), CancelledAsync(organization));

        await using StoreContext reading = database.Context();
        Organization read = Assert.IsType<Organization>(
            await new OrganizationStore(reading).FindAsync(organization, TestContext.Current.CancellationToken));

        Assert.Equal(read.ErasedAt is null, read.DeletionRequestedAt is null);
    }

    private static OrganizationStates States(StoreContext context) =>
        new(context, new OrganizationStore(context), new MembershipStore(context));

    private async ValueTask<IReadOnlyList<EndedMembership>?> EraseAsync(
        OrganizationId organization,
        DateTimeOffset at)
    {
        await using StoreContext writing = database.Context();
        await using var work = new UnitOfWork(writing);
        await work.BeginAsync(TestContext.Current.CancellationToken);

        IReadOnlyList<EndedMembership>? ended = await States(writing).EraseAsync(
            organization,
            at,
            Window,
            TestContext.Current.CancellationToken);

        await work.CommitAsync(TestContext.Current.CancellationToken);

        return ended;
    }

    // The cancellation is its own request, deciding on the row under its lock as the
    // organization service does.
    private async Task CancelledAsync(OrganizationId organization)
    {
        await using StoreContext writing = database.Context();
        await using var work = new UnitOfWork(writing);
        var store = new OrganizationStore(writing);

        Assert.True((await work.BeginAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));

        Organization held = Assert.IsType<Organization>(
            await store.FindForUpdateAsync(organization, TestContext.Current.CancellationToken));

        if (held is { DeletionRequestedAt: not null, ErasedAt: null })
        {
            held.CancelDeletion();

            await store.RecordAsync(held, TestContext.Current.CancellationToken);
        }

        Assert.True((await work.CommitAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));
    }

    private async ValueTask<OrganizationId> CreateAsync()
    {
        var id = new OrganizationId(Guid.CreateVersion7());

        await using StoreContext writing = database.Context();
        await new OrganizationStore(writing).CreateAsync(
            Organization.Create(id, "Acme " + id.Value.ToString("N"), Noon),
            TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);

        return id;
    }

    private async ValueTask DomainAsync(OrganizationId organization, string domain, DateTimeOffset? removed)
    {
        await using NpgsqlConnection connection = await database.OpenAsync();

        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO identity.organization_domains (token, organization, domain, added_at, removed_at) "
                + "VALUES (@token, @organization, @domain, @added, @removed)",
            new
            {
                token = Guid.NewGuid().ToString("N"),
                organization = organization.Value,
                domain,
                added = Noon,
                removed,
            },
            cancellationToken: TestContext.Current.CancellationToken));
    }

    private async ValueTask<OrganizationId> DeletingAsync(DateTimeOffset at)

    {
        OrganizationId id = await CreateAsync();

        await using StoreContext writing = database.Context();
        OrganizationStore store = new(writing);
        Organization organization = Assert.IsType<Organization>(
            await store.FindAsync(id, TestContext.Current.CancellationToken));

        _ = organization.RequestDeletion(at);

        await store.RecordAsync(organization, TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);

        return id;
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

        if (until is DateTimeOffset ended)
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
