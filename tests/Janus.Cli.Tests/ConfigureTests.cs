using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using Janus.Core.Configuration;
using Npgsql;
using Xunit;

namespace Janus.Cli.Tests;

/// <summary>
/// What the <c>configure</c> command does to a bootstrapped deployment: a protected key
/// changes from the server only, with a reason, written down under the command's
/// principal and raised, and never into a deployment that would not start
/// (OPS-CFG-004, D-071, entry 319).
/// </summary>
[Trait("kind", "integration")]
public sealed class ConfigureTests(BootstrappedDeployment deployment) : IClassFixture<BootstrappedDeployment>
{
    private const string Reason = "The provider's own limits now apply.";

    /// <summary>
    /// OPS-CFG-004 AC2 and OPS-ALERT-001: a protected key the application refuses is
    /// changed from the server, written down under the command's principal with the
    /// values, the direction and the reason, and raised as a High condition.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_CFG_004_AC2_AProtectedKeyIsChangedFromTheServerWrittenDownAndRaisedAsync()
    {
        string key = Settings.AbuseThrottleEnabled.Key.ToString();

        Invocation run = await ConfiguredAsync("--" + key, "false", "--reason", Reason);

        await using NpgsqlConnection connection = await deployment.OpenAsync();

        (string? Before, string After, bool Loosening, string Reason, string ReasonOf) recorded = await connection
            .QuerySingleAsync<(string?, string, bool, string, string)>(
                """
                SELECT details->>'before', details->>'after', (details->>'loosening')::boolean,
                       details->>'reason', principal_reason
                FROM identity.audit_records
                WHERE action = 'ops.configuration.changed' AND principal = 'configure'
                  AND details->>'key' = @Key
                """,
                new { Key = key });

        Assert.Equal(0, run.ExitCode);
        Assert.Equal("""{"changed":["abuse.throttle.enabled"]}""", run.Output.Trim());
        Assert.Equal(Settings.AbuseThrottleEnabled.Write(false), await ValueAsync(connection, key));
        Assert.Equal((null, Settings.AbuseThrottleEnabled.Write(false), true, Reason, "OPS-CFG-004"), recorded);
        Assert.Equal(["protected-setting-changed"], await RaisedAsync(connection, key));
    }

    /// <summary>
    /// OPS-CFG-004 and OPS-ALERT-001: the governing language changes from the server
    /// under its own Normal condition as well as the High one every protected key raises.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_CFG_004_TheGoverningLanguageIsRaisedUnderItsOwnConditionAsync()
    {
        string key = Settings.LegalGoverningLanguage.Key.ToString();

        Invocation run = await ConfiguredAsync("--" + key, "en", "--reason", "The contracts are signed in English.");

        await using NpgsqlConnection connection = await deployment.OpenAsync();

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(Settings.LegalGoverningLanguage.Write("en"), await ValueAsync(connection, key));
        Assert.Equal(
            ["governing-language-changed", "protected-setting-changed"],
            (await RaisedAsync(connection, key)).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// OPS-CFG-004 and chapter 10 section 4.8: an organization's step-up enforcement is
    /// switched from the server by its member of the protected family, and a member for
    /// an organization the deployment does not hold is refused.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_CFG_004_AnOrganizationsStepUpEnforcementIsSwitchedFromTheServerAsync()
    {
        await using NpgsqlConnection connection = await deployment.OpenAsync();

        string organization = await connection.ExecuteScalarAsync<string>(
            "SELECT id::text FROM identity.organizations WHERE administrative") ?? string.Empty;
        string key = Settings.OrganizationStepUpEnforcement.For(organization).ToString();
        string unheld = Settings.OrganizationStepUpEnforcement.For(Guid.NewGuid().ToString()).ToString();

        Invocation run = await ConfiguredAsync("--" + key, "false", "--reason", "An incident on the gate.");
        Invocation refused = await ConfiguredAsync("--" + unheld, "false", "--reason", "An incident on the gate.");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(Settings.OrganizationStepUpEnforcement.Write(false), await ValueAsync(connection, key));
        Assert.True(await connection.ExecuteScalarAsync<bool>(
            """
            SELECT (details->>'loosening')::boolean FROM identity.audit_records
            WHERE action = 'ops.configuration.changed' AND principal = 'configure' AND details->>'key' = @Key
            """,
            new { Key = key }));
        Assert.Equal(1, refused.ExitCode);
        Assert.Equal(("config.value.notallowed", unheld, "organization"), Refusal(refused, "key", "field"));
        Assert.Null(await ValueAsync(connection, unheld));
    }

    /// <summary>
    /// OPS-CFG-004 and OPS-CFG-002: a key the application may change is changed through
    /// the application, where its direction prices it, and the command refuses it by name.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_CFG_004_AKeyTheApplicationChangesIsRefusedAsync()
    {
        string key = Settings.PasswordMaximum.Key.ToString();

        Invocation run = await ConfiguredAsync("--" + key, "200", "--reason", Reason);

        await using NpgsqlConnection connection = await deployment.OpenAsync();

        Assert.Equal(1, run.ExitCode);
        Assert.Empty(run.Output);
        Assert.Equal(("api.request.malformed", "--" + key), Refusal(run, "member"));
        Assert.Null(await ValueAsync(connection, key));
    }

    /// <summary>
    /// OPS-CFG-004 and OPS-CFG-005: a change from the server carries a reason, or nothing
    /// changes.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_CFG_004_AChangeWithoutAReasonIsRefusedAsync()
    {
        string key = Settings.ExfiltrationExportAuditing.Key.ToString();

        Invocation run = await ConfiguredAsync("--" + key, "false");
        Invocation blank = await ConfiguredAsync("--" + key, "false", "--reason", " ");

        await using NpgsqlConnection connection = await deployment.OpenAsync();

        Assert.Equal(1, run.ExitCode);
        Assert.Equal("auth.restriction.reasonrequired", Refusal(run));
        Assert.Equal("auth.restriction.reasonrequired", Refusal(blank));
        Assert.Null(await ValueAsync(connection, key));
        Assert.Empty(await RaisedAsync(connection, key));
    }

    /// <summary>
    /// OPS-CFG-004 and OPS-CFG-003: a value its key does not admit is refused with the
    /// key's own code before the database is reached.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_CFG_004_AValueItsKeyDoesNotAdmitIsRefusedAsync()
    {
        string key = Settings.TokenSigningAlgorithm.Key.ToString();

        Invocation run = await ConfiguredAsync("--" + key, "RS256", "--reason", Reason);

        await using NpgsqlConnection connection = await deployment.OpenAsync();

        Assert.Equal(1, run.ExitCode);
        Assert.Equal("config.value.notallowed", Refusal(run));
        Assert.Null(await ValueAsync(connection, key));
    }

    /// <summary>
    /// OPS-CFG-004 and LIB-HOST-001: a change that would leave the deployment unable to
    /// start is refused by the rule the host's start applies, and nothing of it stays.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_CFG_004_AChangeThatLeavesTheDeploymentUnableToStartIsRefusedAsync()
    {
        string key = Settings.HostingLocation.Key.ToString();

        Invocation run = await ConfiguredAsync("--" + key, "outside", "--reason", "The provider moved the region.");

        await using NpgsqlConnection connection = await deployment.OpenAsync();

        Assert.Equal(1, run.ExitCode);
        Assert.Equal(
            ("model.startup.declarationmissing", Settings.HostingCrossBorderBasis.Key.ToString()),
            Refusal(run, "key"));
        Assert.Equal(Settings.HostingLocation.Write(HostingLocation.Inside), await ValueAsync(connection, key));
        Assert.Empty(await RaisedAsync(connection, key));
        Assert.Equal(0, await connection.ExecuteScalarAsync<long>(
            """
            SELECT count(*) FROM identity.audit_records
            WHERE principal = 'configure' AND details->>'key' = @Key
            """,
            new { Key = key }));
    }

    private static async Task<string?> ValueAsync(NpgsqlConnection connection, string key) =>
        await connection.ExecuteScalarAsync<string?>(
            "SELECT value FROM identity.settings WHERE key = @Key",
            new { Key = key });

    private static async Task<IReadOnlyList<string>> RaisedAsync(NpgsqlConnection connection, string key) =>
        [.. await connection.QueryAsync<string>(
            "SELECT condition FROM identity.raised_alerts WHERE details->>'key' = @Key",
            new { Key = key })];

    private static string? Refusal(Invocation run)
    {
        using var refusal = JsonDocument.Parse(run.Error);

        return refusal.RootElement.GetProperty("code").GetString();
    }

    private static (string?, string?) Refusal(Invocation run, string member)
    {
        using var refusal = JsonDocument.Parse(run.Error);

        return (
            refusal.RootElement.GetProperty("code").GetString(),
            refusal.RootElement.GetProperty("details").GetProperty(member).GetString());
    }

    private static (string?, string?, string?) Refusal(Invocation run, string first, string second)
    {
        using var refusal = JsonDocument.Parse(run.Error);
        JsonElement details = refusal.RootElement.GetProperty("details");

        return (
            refusal.RootElement.GetProperty("code").GetString(),
            details.GetProperty(first).GetString(),
            details.GetProperty(second).GetString());
    }

    private Task<Invocation> ConfiguredAsync(params string[] arguments) =>
        Invocation.PipedAsync(["configure", .. arguments], Invocation.Keys(deployment.ConnectionString));
}
