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
    /// OPS-CFG-004 AC2, OPS-CFG-005 and OPS-ALERT-001: a protected key the application
    /// refuses is changed from the server, written down under the command's principal
    /// with the values, the direction and the reason, and raised as a High condition
    /// whose <c>AlertRaised</c> event is written with it. No row stood for the key, so
    /// what it was is its default, the value in force.
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
        Assert.Equal(
            (Settings.AbuseThrottleEnabled.Write(true), Settings.AbuseThrottleEnabled.Write(false), true, Reason, "OPS-CFG-004"),
            recorded);
        Assert.Equal(["protected-setting-changed"], await RaisedAsync(connection, key));
        Assert.Equal(["protected-setting-changed"], await AnnouncedAsync(connection, key));
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
    /// OPS-CFG-004: audit logging, token signature verification and step-up enforcement
    /// have no switch, so the command refuses each former switch by name as it refuses
    /// any key that is not protected, and writes nothing.
    /// </summary>
    /// <param name="key">The former switch, a member of the former family included.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("audit.enabled")]
    [InlineData("token.signature.verification")]
    [InlineData("stepup.enforcement.0199a1b2-c3d4-7e5f-8a6b-7c8d9e0f1a2b")]
    public async Task OPS_CFG_004_ARetiredSwitchIsRefusedAsync(string key)
    {
        Invocation run = await ConfiguredAsync("--" + key, "false", "--reason", Reason);

        await using NpgsqlConnection connection = await deployment.OpenAsync();

        Assert.Equal(1, run.ExitCode);
        Assert.Empty(run.Output);
        Assert.Equal(("api.request.malformed", "--" + key), Refusal(run, "member"));
        Assert.Null(await ValueAsync(connection, key));
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
        Assert.Equal("config.change.reasonrequired", Refusal(run));
        Assert.Equal("config.change.reasonrequired", Refusal(blank));
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
    /// AUTH-KEY-001 AC4: <c>token.signing.algorithm</c> admits ES256 alone, so
    /// <c>configure</c> refuses another signing algorithm with
    /// <c>config.value.notallowed</c> and writes nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_KEY_001_AC4_ConfigureRefusesASigningAlgorithmOtherThanES256Async()
    {
        string key = Settings.TokenSigningAlgorithm.Key.ToString();

        Invocation run = await ConfiguredAsync("--" + key, "ES384", "--reason", Reason);

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

    /// <summary>
    /// OPS-CFG-004, AUTH-FACT-010 AC1 and LIB-HOST-001: the checks the host's start runs
    /// over the relying party run over the written values before the commit, so an
    /// identifier that no configured origin sits under is refused with the code the
    /// start gives, and nothing of the change stays.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_CFG_004_ARelyingPartyIdentifierNoOriginSharesIsRefusedAsync()
    {
        string key = Settings.WebAuthnRelyingPartyId.Key.ToString();

        Invocation run = await ConfiguredAsync("--" + key, "elsewhere.example.org", "--reason", Reason);

        await using NpgsqlConnection connection = await deployment.OpenAsync();

        Assert.Equal(1, run.ExitCode);
        Assert.Empty(run.Output);
        Assert.Equal("model.startup.rpid", Refusal(run));
        Assert.Null(await ValueAsync(connection, key));
        Assert.Empty(await RaisedAsync(connection, key));
    }

    /// <summary>
    /// OPS-CFG-005 (D-166, 319): what a key was is the written form of the value in
    /// force, and nothing only for a key the deployment names that has no row: the
    /// first change of the cross-border basis records nothing before it, and the next
    /// records the value the first put in force.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_CFG_005_AChangeRecordsTheValueInForceAsWhatItWasAsync()
    {
        string key = Settings.HostingCrossBorderBasis.Key.ToString();

        await using NpgsqlConnection connection = await deployment.OpenAsync();

        try
        {
            Invocation first = await ConfiguredAsync("--" + key, "standard contractual clauses", "--reason", Reason);
            Invocation second = await ConfiguredAsync("--" + key, "an adequacy decision", "--reason", Reason);

            IReadOnlyList<(string? Before, string After)> recorded = [.. await connection
                .QueryAsync<(string?, string)>(
                    """
                    SELECT details->>'before', details->>'after'
                    FROM identity.audit_records
                    WHERE action = 'ops.configuration.changed' AND principal = 'configure'
                      AND details->>'key' = @Key
                    ORDER BY occurred_at, id
                    """,
                    new { Key = key })];

            Assert.Equal(0, first.ExitCode);
            Assert.Equal(0, second.ExitCode);
            Assert.Equal(
                [(null, "standard contractual clauses"), ("standard contractual clauses", "an adequacy decision")],
                recorded);
        }
        finally
        {
            // The key is required once the location is outside Egypt, which another case
            // of the class relies on finding unnamed.
            await connection.ExecuteAsync("DELETE FROM identity.settings WHERE key = @Key", new { Key = key });
        }
    }

    private static async Task<string?> ValueAsync(NpgsqlConnection connection, string key) =>
        await connection.ExecuteScalarAsync<string?>(
            "SELECT value FROM identity.settings WHERE key = @Key",
            new { Key = key });

    private static async Task<IReadOnlyList<string>> RaisedAsync(NpgsqlConnection connection, string key) =>
        [.. await connection.QueryAsync<string>(
            "SELECT condition FROM identity.raised_alerts WHERE details->>'key' = @Key",
            new { Key = key })];

    private static async Task<IReadOnlyList<string>> AnnouncedAsync(NpgsqlConnection connection, string key) =>
        [.. await connection.QueryAsync<string>(
            """
            SELECT payload->>'Condition' FROM identity.events
            WHERE kind = 'AlertRaised' AND payload->'Details'->>'key' = @Key
            """,
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
