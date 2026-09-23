using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Janus.Authentication.Configuration;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Storage.Authentication.Configuration;
using Janus.Storage.Identity.Audit;
using Xunit;
using Catalogue = Janus.Core.Configuration.Settings;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// What a runtime configuration change leaves in the trail, and the two ways the
/// chapter asks for it back (OPS-CFG-005).
/// </summary>
/// <remarks>
/// The port implementation is tested against the real database (D-156). One database
/// serves the class, so each test writes a key and an actor of its own.
/// </remarks>
[Trait("kind", "integration")]
public sealed class ConfigurationAuditTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly Deployment _deployment = new(database);

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    /// <summary>
    /// OPS-CFG-005 AC1: the record carries the values before and after, the direction,
    /// the reason and who made it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task OPS_CFG_005_AC1_TheRecordCarriesBeforeAndAfterAsync()
    {
        SubjectId actor = await _deployment.AccountAsync(DateTimeOffset.UtcNow);
        var written = new ConfigurationChange(
            Catalogue.SessionAal2Inactivity.Key,
            "PT1H",
            "PT2H",
            Loosening: true,
            "a support window",
            actor,
            DateTimeOffset.UtcNow);

        await RecordedAsync(written);

        ConfigurationChange read = Assert.Single(await OfActorAsync(actor));

        Assert.Equal(written.Key, read.Key);
        Assert.Equal("PT1H", read.Before);
        Assert.Equal("PT2H", read.After);
        Assert.True(read.Loosening);
        Assert.Equal("a support window", read.Reason);
        Assert.Equal(actor, read.Actor);
    }

    /// <summary>
    /// OPS-CFG-005 AC2: the records read back by setting, and a change to another
    /// setting is not among them.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task OPS_CFG_005_AC2_TheRecordsAreQueryableBySettingAsync()
    {
        SubjectId actor = await _deployment.AccountAsync(DateTimeOffset.UtcNow);

        await RecordedAsync(Changed(Catalogue.PrivacyExportRateLimit.Key, "3", "2", actor));
        await RecordedAsync(Changed(Catalogue.SessionStepUpRecency.Key, "PT15M", "PT5M", actor));

        ConfigurationChange read = Assert.Single(await OfSettingAsync(Catalogue.SessionStepUpRecency.Key));

        Assert.Equal(Catalogue.SessionStepUpRecency.Key, read.Key);
        Assert.Equal("PT5M", read.After);
    }

    /// <summary>
    /// OPS-CFG-005 AC2: the records read back by actor, and another actor's change is
    /// not among them.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task OPS_CFG_005_AC2_TheRecordsAreQueryableByActorAsync()
    {
        SubjectId one = await _deployment.AccountAsync(DateTimeOffset.UtcNow);
        SubjectId other = await _deployment.AccountAsync(DateTimeOffset.UtcNow);

        await RecordedAsync(Changed(Catalogue.PrivacyExportRateLimit.Key, "3", "2", one));
        await RecordedAsync(Changed(Catalogue.PrivacyExportRateLimit.Key, "2", "1", other));

        ConfigurationChange read = Assert.Single(await OfActorAsync(other));

        Assert.Equal("1", read.After);
        Assert.Equal(other, read.Actor);
    }

    private static ConfigurationChange Changed(
        ConfigurationKey key,
        string before,
        string after,
        SubjectId actor) =>
        new(key, before, after, Loosening: true, "a support window", actor, DateTimeOffset.UtcNow);

    private ConfigurationAudit Audit(StoreContext context) =>
        new(context, new AuditStore(context, _deployment.Keys, _deployment.Randomness), TimeProvider.System);

    private async Task RecordedAsync(ConfigurationChange change)
    {
        await using StoreContext writing = database.Context();

        await Audit(writing).ChangedAsync(change, TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<IReadOnlyList<ConfigurationChange>> OfSettingAsync(ConfigurationKey key)
    {
        await using StoreContext reading = database.Context();

        return await Audit(reading).OfSettingAsync(key, TestContext.Current.CancellationToken);
    }

    private async Task<IReadOnlyList<ConfigurationChange>> OfActorAsync(SubjectId actor)
    {
        await using StoreContext reading = database.Context();

        return await Audit(reading).OfActorAsync(actor, TestContext.Current.CancellationToken);
    }
}
