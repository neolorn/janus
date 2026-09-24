using System;
using System.Linq;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Storage.Settings;
using Xunit;
using Catalogue = Janus.Core.Configuration.Settings;

namespace Janus.Storage.Tests;

/// <summary>
/// The value in force for a configuration key as the <c>settings</c> row carries it
/// (OPS-CFG-008).
/// </summary>
[Trait("kind", "integration")]
public sealed class ConfigurationStoreTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>
{
    /// <summary>
    /// OPS-CFG-008 AC1: a change is in force for the next read, made on a context
    /// that knew nothing of the write, so nothing is restarted to see it.
    /// </summary>
    [Fact]
    public async Task OPS_CFG_008_AC1_AChangedSettingIsInForceForTheNextReadAsync()
    {
        await using (StoreContext writing = database.Context())
        {
            Result<TimeSpan> before = await new ConfigurationStore(writing).WriteAsync(
                Catalogue.AbuseNonexistentWindow,
                TimeSpan.FromMinutes(30),
                TestContext.Current.CancellationToken);

            Assert.Equal(Catalogue.AbuseNonexistentWindow.Default, Value(before));
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();
        Result<TimeSpan> read = await new ConfigurationStore(reading).ReadAsync(
            Catalogue.AbuseNonexistentWindow,
            TestContext.Current.CancellationToken);

        Assert.Equal(TimeSpan.FromMinutes(30), Value(read));
    }

    /// <summary>
    /// OPS-CFG-008 AC3: neither bootstrap value, nor either key fetched from the
    /// secrets manager at startup, is a key of the catalogue the table holds.
    /// </summary>
    [Fact]
    public void OPS_CFG_008_AC3_NoBootstrapValueIsAKeyOfTheTable()
    {
        string[] bootstrap =
        [
            "database.connection",
            "secrets.credential",
            "key.encryption",
            "fingerprint.key",
        ];

        Assert.All(
            bootstrap,
            name => Assert.DoesNotContain(
                Catalogue.All,
                setting => setting.Key.ToString().Contains(name, StringComparison.Ordinal)));
    }

    /// <summary>
    /// A key the deployment never wrote has no row, and reads as the default the
    /// catalogue gives it.
    /// </summary>
    [Fact]
    public async Task ReadAsync_AKeyNeverWritten_ReadsAsItsDefaultAsync()
    {
        await using StoreContext reading = database.Context();

        Result<int> read = await new ConfigurationStore(reading).ReadAsync(
            Catalogue.AlertingCallbackThreshold,
            TestContext.Current.CancellationToken);

        Assert.Equal(Catalogue.AlertingCallbackThreshold.Default, Value(read));
    }

    /// <summary>
    /// A key the application cannot change is refused whatever the caller asks, and
    /// no row is written for it (OPS-CFG-004).
    /// </summary>
    [Fact]
    public async Task WriteAsync_AProtectedKey_IsRefusedAndWritesNothingAsync()
    {
        await using StoreContext writing = database.Context();

        Result<bool> written = await new ConfigurationStore(writing).WriteAsync(
            Catalogue.AbuseThrottleEnabled,
            false,
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.ConfigurationKeyProtected, Code(written));
        Assert.Empty(writing.ChangeTracker.Entries<SettingRecord>());
    }

    /// <summary>
    /// A value the key does not admit is refused rather than stored, so a tightened
    /// ceiling is never crossed by a write.
    /// </summary>
    [Fact]
    public async Task WriteAsync_AValueAboveTheCeiling_IsRefusedAsync()
    {
        await using StoreContext writing = database.Context();

        Result<TimeSpan> written = await new ConfigurationStore(writing).WriteAsync(
            Catalogue.SessionAal2Absolute,
            TimeSpan.FromHours(48),
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.ConfigurationValueAboveCeiling, Code(written));
        Assert.Empty(writing.ChangeTracker.Entries<SettingRecord>());
    }

    /// <summary>
    /// A member of a family the deployment never wrote reads as the family's own
    /// default, and one it wrote reads back as it was written.
    /// </summary>
    [Fact]
    public async Task ReadAsync_AMemberOfAFamily_ReadsBackAsItWasWrittenAsync()
    {
        var organization = OrganizationId.New(TimeProvider.System);

        await using StoreContext context = database.Context();
        var store = new ConfigurationStore(context);

        Result<PolicyOverride> absent = await store.ReadAsync(
            Catalogue.OrganizationPolicy,
            organization.ToString(),
            TestContext.Current.CancellationToken);

        Assert.Equal(PolicyOverride.None, Value(absent));

        context.Settings.Add(new SettingRecord
        {
            Key = Catalogue.OrganizationPolicy.For(organization.ToString()),
            Value = Catalogue.OrganizationPolicy.Write(
                PolicyOverride.None with { SelfServiceRecovery = false }),
        });

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Result<PolicyOverride> read = await store.ReadAsync(
            Catalogue.OrganizationPolicy,
            organization.ToString(),
            TestContext.Current.CancellationToken);

        Assert.False(Value(read)?.SelfServiceRecovery);
    }

    /// <summary>
    /// OPS-CFG-008 AC1: a member of a family written through the store is in force for
    /// the next read on a context that knew nothing of the write, and the write answers
    /// what was in force before it.
    /// </summary>
    [Fact]
    public async Task OPS_CFG_008_AC1_AWrittenMemberOfAFamilyIsInForceForTheNextReadAsync()
    {
        var organization = OrganizationId.New(TimeProvider.System);
        PolicyOverride changed = PolicyOverride.None with { SelfServiceRecovery = false };

        await using (StoreContext writing = database.Context())
        {
            Result<PolicyOverride> before = await new ConfigurationStore(writing).WriteAsync(
                Catalogue.OrganizationPolicy,
                organization.ToString(),
                changed,
                TestContext.Current.CancellationToken);

            Assert.Equal(PolicyOverride.None, Value(before));
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();
        Result<PolicyOverride> read = await new ConfigurationStore(reading).ReadAsync(
            Catalogue.OrganizationPolicy,
            organization.ToString(),
            TestContext.Current.CancellationToken);

        Assert.False(Value(read)?.SelfServiceRecovery);
    }

    /// <summary>
    /// A family the application cannot change is refused for every member, whatever
    /// the caller asks, and no row is written for it (OPS-CFG-004).
    /// </summary>
    [Fact]
    public async Task WriteAsync_AMemberOfAProtectedFamily_IsRefusedAndWritesNothingAsync()
    {
        await using StoreContext writing = database.Context();

        Result<bool> written = await new ConfigurationStore(writing).WriteAsync(
            Catalogue.OrganizationStepUpEnforcement,
            OrganizationId.New(TimeProvider.System).ToString(),
            false,
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.ConfigurationKeyProtected, Code(written));
        Assert.Empty(writing.ChangeTracker.Entries<SettingRecord>());
    }

    /// <summary>
    /// A member's value that would not read back as one the family admits is refused
    /// rather than stored, so no row the next read faults on is ever written.
    /// </summary>
    [Fact]
    public async Task WriteAsync_AMemberValueTheFamilyDoesNotAdmit_IsRefusedAsync()
    {
        await using StoreContext writing = database.Context();

        Result<TimeSpan> written = await new ConfigurationStore(writing).WriteAsync(
            Catalogue.HostCategoryRetention,
            "ledgers",
            TimeSpan.FromDays(-1),
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.ConfigurationValueNotAllowed, Code(written));
        Assert.Empty(writing.ChangeTracker.Entries<SettingRecord>());
    }

    /// <summary>
    /// `10` section 4: the row carries the key's written form, so a row that does not
    /// parse is a fault and never a default quietly standing in for it.
    /// </summary>
    [Fact]
    public async Task ReadAsync_AStoredValueThatDoesNotParse_IsAFaultAsync()
    {
        await using StoreContext context = database.Context();

        var written = new SettingRecord
        {
            Key = Catalogue.LinkMagicLifetime.Key,
            Value = "a quarter of an hour",
        };

        context.Settings.Add(written);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Result<TimeSpan> read = await new ConfigurationStore(context).ReadAsync(
            Catalogue.LinkMagicLifetime,
            TestContext.Current.CancellationToken);

        // The row is the class's database, which the other cases read too, so what
        // this one wrote goes out with it.
        context.Settings.Remove(written);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.ConfigurationValueNotAllowed, Code(read));
    }

    private static TValue? Value<TValue>(Result<TValue> outcome)
    {
        TValue? held = default;

        outcome.Switch(value => held = value, _ => { });

        return held;
    }

    private static ErrorCode? Code<TValue>(Result<TValue> outcome)
    {
        ErrorCode? code = null;

        outcome.Switch(_ => { }, failure => code = failure.Code);

        return code;
    }
}
