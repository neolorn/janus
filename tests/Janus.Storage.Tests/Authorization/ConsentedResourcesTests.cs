using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Janus.Authorization.Resources;
using Janus.Core;
using Janus.Privacy.Consents;
using Janus.Storage.Authorization.Resources;
using Janus.Storage.Privacy.Consents;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests.Authorization;

/// <summary>
/// What the view of consented resources holds: each registered record whose data
/// subject holds a consent neither withdrawn nor superseded, with the purpose, the
/// document and the kind of that consent (AUTHZ-GATE-002, PRIV-SENS-002).
/// </summary>
[Trait("kind", "integration")]
public sealed class ConsentedResourcesTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private const string Recommendations = "recommendations";

    private const string Terms = "newsletter-terms";

    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new(database);

    /// <summary>
    /// AUTHZ-GATE-002 AC4: the view holds a row for a record whose data subject holds a
    /// live consent, carrying the purpose, the document and the kind it was recorded
    /// with.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GATE_002_AC4_ARecordOfAConsentingSubjectIsListedWithTheDocumentAndKindAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        ResourceReference document = await RegisteredAsync(subject);

        await GrantedAsync(subject, ConsentKind.Written);

        Assert.Equal(
            [(Recommendations, Terms, "written")],
            await ListedAsync(document));
    }

    /// <summary>
    /// AUTHZ-GATE-002 AC4: a record that names no data subject, and a record whose
    /// data subject holds no consent, have no row.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GATE_002_AC4_ARecordWithoutAConsentingSubjectIsNotListedAsync()
    {
        SubjectId consenting = await _deployment.AccountAsync(Noon);
        SubjectId silent = await _deployment.AccountAsync(Noon);
        ResourceReference unowned = await RegisteredAsync(subject: null);
        ResourceReference unconsented = await RegisteredAsync(silent);

        await GrantedAsync(consenting, ConsentKind.Ordinary);

        Assert.Empty(await ListedAsync(unowned));
        Assert.Empty(await ListedAsync(unconsented));
    }

    /// <summary>
    /// PRIV-SENS-002a AC2: a withdrawal takes the record out of the view, with nothing
    /// else written.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_SENS_002a_AC2_AWithdrawalTakesTheRecordOutOfTheViewAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        ResourceReference document = await RegisteredAsync(subject);

        await GrantedAsync(subject, ConsentKind.Ordinary);

        Assert.True(await WritingAsync(store => store.WithdrawConsentAsync(
            subject,
            Recommendations,
            Noon.AddDays(1),
            TestContext.Current.CancellationToken)));

        Assert.Empty(await ListedAsync(document));
    }

    /// <summary>
    /// PRIV-CONS-007 AC5: a superseded consent takes the record out of the view, and
    /// the consent given after it brings the record back.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_CONS_007_AC5_ASupersededConsentTakesTheRecordOutUntilTheNextGrantAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        ResourceReference document = await RegisteredAsync(subject);

        await GrantedAsync(subject, ConsentKind.Ordinary);

        Assert.True(await WritingAsync(store => store.SupersedeAsync(
            subject,
            Recommendations,
            Noon.AddDays(1),
            TestContext.Current.CancellationToken)));

        IReadOnlyList<(string Purpose, string Document, string Kind)> superseded = await ListedAsync(document);

        await GrantedAsync(subject, ConsentKind.Written);

        Assert.Empty(superseded);
        Assert.Equal(
            [(Recommendations, Terms, "written")],
            await ListedAsync(document));
    }

    /// <inheritdoc />
    public void Dispose() => _deployment.Dispose();

    private async Task<ResourceReference> RegisteredAsync(SubjectId? subject)
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        var reference = new ResourceReference(
            ResourceType.Parse("document"),
            ResourceId.Parse(Guid.NewGuid().ToString()));

        await using StoreContext writing = database.Context();
        await using var transaction = new UnitOfWork(writing);
        await transaction.BeginAsync(TestContext.Current.CancellationToken);

        await new ResourceStore(writing, new DataConnections(writing)).RegisterAsync(
            RegisteredResource.Create(reference, organization, subject, containedIn: null),
            TestContext.Current.CancellationToken);

        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return reference;
    }

    private async Task GrantedAsync(SubjectId subject, ConsentKind kind) =>
        Assert.True(await WritingAsync(store => store.AddAsync(
            subject,
            new ConsentRecord(
                Recommendations,
                Terms,
                "3",
                ConsentMechanism.Dashboard,
                kind,
                Noon,
                WithdrawnAt: null,
                SupersededAt: null),
            TestContext.Current.CancellationToken)));

    private async Task<bool> WritingAsync(Func<ConsentStore, ValueTask<bool>> write)
    {
        await using StoreContext writing = database.Context();

        return await write(new ConsentStore(writing, new DataConnections(writing)));
    }

    private async Task<IReadOnlyList<(string Purpose, string Document, string Kind)>> ListedAsync(
        ResourceReference reference)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        return [.. await connection.QueryAsync<(string, string, string)>(
            """
            SELECT purpose, document, kind
            FROM identity.consented_resources
            WHERE resource_type = @type AND resource_id = @id
            """,
            new { type = reference.Type.ToString(), id = reference.Id.ToString() })];
    }
}
