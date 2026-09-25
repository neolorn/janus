using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Organizations;
using Janus.Core;
using Janus.Storage.Authentication.Organizations;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// The domains an organization locks its members to, as the database keeps them: one
/// listed row per organization and domain, removed rows kept beside it, and the rows a
/// scheduled check is due for (REG-DOM-001, IDN-ORG-006).
/// </summary>
/// <remarks>
/// The port implementations are tested against the real database (D-156). One database
/// serves the class, so each test writes an organization of its own.
/// </remarks>
[Trait("kind", "integration")]
public sealed class DomainStoreTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private const string Domain = "example.test";

    private static readonly DateTimeOffset Noon = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new(database);

    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    /// <inheritdoc/>
    public void Dispose()
    {
        _deployment.Dispose();
        _randomness.Dispose();
    }

    /// <summary>
    /// REG-DOM-001 and D-153: a verification and a removal are carried onto the row,
    /// and a domain listed anew after its removal is a second row with a token of its
    /// own, the first kept beside it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_DOM_001_ARemovedDomainIsKeptBesideItsSuccessorAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        var first = LockedDomain.Listed(organization, Domain, _randomness, Noon);

        await WrittenAsync(store => store.AddAsync(first, TestContext.Current.CancellationToken));

        first.Checked(passed: true, Noon.AddHours(1));
        first.Remove(Noon.AddHours(2));

        await WrittenAsync(store => store.RecordAsync(first, TestContext.Current.CancellationToken));

        var second = LockedDomain.Listed(organization, Domain, _randomness, Noon.AddHours(3));

        await WrittenAsync(store => store.AddAsync(second, TestContext.Current.CancellationToken));

        IReadOnlyList<LockedDomain> held = await ReadAsync(organization);

        Assert.Equal([first.Token, second.Token], held.Select(domain => domain.Token));
        Assert.NotEqual(first.Token, second.Token);
        Assert.Equal(Noon.AddHours(1), held[0].VerifiedAt);
        Assert.Equal(Noon.AddHours(2), held[0].RemovedAt);
        Assert.True(held[1].IsListed);
        Assert.Null(held[1].VerifiedAt);
    }

    /// <summary>
    /// REG-DOM-001: the partial unique index holds one listed row per organization and
    /// domain, whatever the service does.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_DOM_001_ADomainIsListedOnceAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);

        await WrittenAsync(store => store.AddAsync(
            LockedDomain.Listed(organization, Domain, _randomness, Noon),
            TestContext.Current.CancellationToken));

        await Assert.ThrowsAsync<DbUpdateException>(() => WrittenAsync(store => store.AddAsync(
            LockedDomain.Listed(organization, Domain, _randomness, Noon),
            TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// REG-DOM-001 AC3: a check is due for a listed, verified domain last checked
    /// before the instant, and for no other.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_DOM_001_AC3_OnlyAVerifiedListedDomainIsDueAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        var due = LockedDomain.Listed(organization, "due.test", _randomness, Noon);
        var recent = LockedDomain.Listed(organization, "recent.test", _randomness, Noon);
        var unverified = LockedDomain.Listed(organization, "unverified.test", _randomness, Noon);
        var removed = LockedDomain.Listed(organization, "removed.test", _randomness, Noon);

        due.Checked(passed: true, Noon);
        recent.Checked(passed: true, Noon.AddDays(1));
        unverified.Checked(passed: false, Noon);
        removed.Checked(passed: true, Noon);
        removed.Remove(Noon);

        foreach (LockedDomain domain in new[] { due, recent, unverified, removed })
        {
            await WrittenAsync(store => store.AddAsync(domain, TestContext.Current.CancellationToken));
        }

        await using StoreContext reading = database.Context();

        IReadOnlyList<LockedDomain> found = await new DomainStore(reading)
            .DueAsync(Noon.AddHours(12), TestContext.Current.CancellationToken);

        Assert.Contains(found, domain => domain.Token == due.Token);
        Assert.DoesNotContain(found, domain => domain.Token == recent.Token);
        Assert.DoesNotContain(found, domain => domain.Token == unverified.Token);
        Assert.DoesNotContain(found, domain => domain.Token == removed.Token);
    }

    private async Task WrittenAsync(Func<DomainStore, ValueTask> write)
    {
        await using StoreContext writing = database.Context();

        await write(new DomainStore(writing));
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<IReadOnlyList<LockedDomain>> ReadAsync(OrganizationId organization)
    {
        await using StoreContext reading = database.Context();

        return await new DomainStore(reading).OfAsync(organization, TestContext.Current.CancellationToken);
    }
}
