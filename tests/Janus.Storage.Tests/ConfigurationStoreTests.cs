using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Storage.Migrations;
using Janus.Storage.Settings;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
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
            Result<TimeSpan> before = await new ConfigurationStore(writing, new DataConnections(writing)).WriteAsync(
                Catalogue.AbuseNonexistentWindow,
                TimeSpan.FromMinutes(30),
                TestContext.Current.CancellationToken);

            Assert.Equal(Catalogue.AbuseNonexistentWindow.Default, Value(before));
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();
        Result<TimeSpan> read = await new ConfigurationStore(reading, new DataConnections(reading)).ReadAsync(
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

        Result<int> read = await new ConfigurationStore(reading, new DataConnections(reading)).ReadAsync(
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

        Result<bool> written = await new ConfigurationStore(writing, new DataConnections(writing)).WriteAsync(
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

        Result<TimeSpan> written = await new ConfigurationStore(writing, new DataConnections(writing)).WriteAsync(
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
        var store = new ConfigurationStore(context, new DataConnections(context));

        Result<PolicyOverride> absent = await store.ReadAsync(
            Catalogue.OrganizationPolicy,
            organization.ToString(),
            TestContext.Current.CancellationToken);

        Assert.Equal(Catalogue.OrganizationPolicy.Default, Value(absent));

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
    /// the next read on a context that knew nothing of the write.
    /// </summary>
    [Fact]
    public async Task OPS_CFG_008_AC1_AWrittenMemberOfAFamilyIsInForceForTheNextReadAsync()
    {
        var organization = OrganizationId.New(TimeProvider.System);
        PolicyOverride changed = PolicyOverride.None with { SelfServiceRecovery = false };

        await using (StoreContext writing = database.Context())
        {
            Result written = await new ConfigurationStore(writing, new DataConnections(writing)).WriteAsync(
                Catalogue.OrganizationPolicy,
                organization.ToString(),
                changed,
                TestContext.Current.CancellationToken);

            Assert.True(written.Match(() => true, _ => false));
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();
        Result<PolicyOverride> read = await new ConfigurationStore(reading, new DataConnections(reading)).ReadAsync(
            Catalogue.OrganizationPolicy,
            organization.ToString(),
            TestContext.Current.CancellationToken);

        Assert.False(Value(read)?.SelfServiceRecovery);
    }

    /// <summary>
    /// A member's value that would not read back as one the family admits is refused
    /// rather than stored, so no row the next read faults on is ever written.
    /// </summary>
    [Fact]
    public async Task WriteAsync_AMemberValueTheFamilyDoesNotAdmit_IsRefusedAsync()
    {
        await using StoreContext writing = database.Context();

        Result written = await new ConfigurationStore(writing, new DataConnections(writing)).WriteAsync(
            Catalogue.HostCategoryRetention,
            "ledgers",
            TimeSpan.FromDays(-1),
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.ConfigurationValueNotAllowed, written.Match<ErrorCode?>(() => null, failure => failure.Code));
        Assert.Empty(writing.ChangeTracker.Entries<SettingRecord>());
    }

    /// <summary>
    /// CONV-DESIGN-007 AC4, OPS-CFG-008 and `10` section 4: the row carries the key's
    /// written form, so a row that does not parse is a fault, for a key that exists
    /// once, for a member of a family and among the members written: the read throws,
    /// naming the key and never the stored text, and no default stands in for the value.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_007_AC4_AStoredValueThatDoesNotReadIsAFaultAsync()
    {
        await using StoreContext context = database.Context();

        const string organization = "unreadable";
        const string stored = "a quarter of an hour";

        SettingRecord[] written =
        [
            new() { Key = Catalogue.LinkMagicLifetime.Key, Value = stored },
            new() { Key = Catalogue.OrganizationPolicy.For(organization), Value = stored },
        ];

        context.Settings.AddRange(written);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var store = new ConfigurationStore(context, new DataConnections(context));

        try
        {
            InvalidOperationException single = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await store.ReadAsync(
                    Catalogue.LinkMagicLifetime,
                    TestContext.Current.CancellationToken));

            InvalidOperationException member = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await store.ReadAsync(
                    Catalogue.OrganizationPolicy,
                    organization,
                    TestContext.Current.CancellationToken));

            InvalidOperationException members = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await store.ReadWrittenAsync(
                    Catalogue.OrganizationPolicy,
                    TestContext.Current.CancellationToken));

            Assert.Contains(Catalogue.LinkMagicLifetime.Key.ToString(), single.Message, StringComparison.Ordinal);
            Assert.Contains(written[1].Key.ToString(), member.Message, StringComparison.Ordinal);
            Assert.Contains(written[1].Key.ToString(), members.Message, StringComparison.Ordinal);
            Assert.All(
                new[] { single, member, members },
                fault => Assert.DoesNotContain(stored, fault.Message, StringComparison.Ordinal));
        }
        finally
        {
            // The rows are the class's database, which the other cases read too, so
            // what this one wrote goes out with it.
            context.Settings.RemoveRange(written);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
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

    /// <summary>
    /// Chapter 10 section 5a (D-235): the migration deletes the gates the system policy
    /// and an organization's override still name for a retired step-up action, keeps
    /// every other gate, and leaves a row naming none, and the other keys under the
    /// prefix, as they were.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_STEP_001_TheMigrationRemovesTheGatesOfARetiredActionAsync()
    {
        string moved = await database.CreateDatabaseAsync("retired_gates");
        string removing;

        await using (StoreContext migrating = DatabaseFixture.Context(moved))
        {
            string[] declared = [.. migrating.GetService<IMigrationsAssembly>().Migrations.Keys.Order(StringComparer.Ordinal)];

            removing = declared.Single(migration =>
                migration.EndsWith("_" + nameof(RemoveTheGatesOfARetiredAction), StringComparison.Ordinal));

            await migrating.GetService<IMigrator>().MigrateAsync(
                declared[Array.IndexOf(declared, removing) - 1],
                TestContext.Current.CancellationToken);
        }

        string system = Catalogue.PolicyDefault.Write(Catalogue.PolicyDefault.Default);
        string stated = Catalogue.OrganizationPolicy.Write(PolicyOverride.None with
        {
            Gates = new Dictionary<StepUpAction, Gate>
            {
                [StepUpAction.FactorRemove] = new(GateLevel.Aal2, PhishingResistant: true, TimeSpan.FromMinutes(5)),
            },
        });

        var values = new
        {
            system = Catalogue.PolicyDefault.Key.ToString(),
            systemValue = Retiring(system),
            organization = Catalogue.OrganizationPolicy.For(Guid.CreateVersion7().ToString()).ToString(),
            organizationValue = Retiring(stated),
            untouched = Catalogue.OrganizationPolicy.For(Guid.CreateVersion7().ToString()).ToString(),
            untouchedValue = stated,
            grace = Catalogue.PolicyEnforcementGrace.Key.ToString(),
        };

        await using var connection = new NpgsqlConnection(moved);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await connection.ExecuteAsync(
            """
            INSERT INTO identity.settings (key, value)
            VALUES (@system, @systemValue), (@organization, @organizationValue), (@untouched, @untouchedValue),
                (@grace, 'PT1H');
            """,
            values);

        await using (StoreContext migrating = DatabaseFixture.Context(moved))
        {
            await migrating.GetService<IMigrator>().MigrateAsync(removing, TestContext.Current.CancellationToken);
        }

        async Task<string> ValueAsync(string key) =>
            await connection.QuerySingleAsync<string>(
                "SELECT value FROM identity.settings WHERE key = @key",
                new { key });

        Assert.Equal(Gates(system), Gates(await ValueAsync(values.system)));
        Assert.Equal(Gates(stated), Gates(await ValueAsync(values.organization)));
        Assert.Equal(stated, await ValueAsync(values.untouched));
        Assert.Equal("PT1H", await ValueAsync(values.grace));
    }

    // A written policy with the gate of the retired alerting:destinations added, as a
    // deployment written before the action was retired holds it.
    private static string Retiring(string written)
    {
        JsonObject policy = JsonNode.Parse(written)!.AsObject();

        if (policy["gates"] is not JsonObject gates)
        {
            gates = [];
            policy["gates"] = gates;
        }

        gates["alerting:destinations"] = new JsonObject
        {
            ["level"] = "aal2",
            ["phishingResistant"] = false,
            ["maxAge"] = "PT15M",
        };

        return policy.ToJsonString();
    }

    // The gates a written policy holds, each as its name and its three values, read
    // whatever order the stored object keeps its fields in.
    private static string[] Gates(string written) =>
        [.. JsonNode.Parse(written)!["gates"]!.AsObject()
            .Select(gate => gate.Key + " " + gate.Value!["level"] + " " + gate.Value["phishingResistant"] + " " + gate.Value["maxAge"])
            .Order(StringComparer.Ordinal)];
}
