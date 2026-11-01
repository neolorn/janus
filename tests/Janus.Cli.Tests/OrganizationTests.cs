using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Janus.Core.Configuration;
using Npgsql;
using Xunit;

namespace Janus.Cli.Tests;

/// <summary>
/// What the <c>organization</c> command does to a bootstrapped deployment: a business
/// organization set up from the server, its policy written from a file with the policy
/// route's checks and codes, each change written down under the command's principal, and
/// nothing of a refused run left behind (OPS-BOOT-001, IDN-ORG-002, D-248).
/// </summary>
[Trait("kind", "integration")]
public sealed class OrganizationTests(BootstrappedDeployment deployment) : IClassFixture<BootstrappedDeployment>, IDisposable
{
    private const string Reason = "Production is set up.";

    // A policy that asks more than the system's on every field it states.
    private const string Stricter =
        """
        {
          "requiredAssurance": "aal2",
          "loginFactors": ["passkey", "password", "totp"],
          "credentialRedundancy": "enforced",
          "selfServiceRecovery": false,
          "grammaticalGender": true
        }
        """;

    private readonly string _policy = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    /// <inheritdoc/>
    public void Dispose() => File.Delete(_policy);

    /// <summary>
    /// OPS-BOOT-001 AC10 (D-248): where no organization holds the name, the command
    /// creates one, writes its policy key as the route that creates one does and then the
    /// policy from the file, and writes <c>organization.multiplememberships</c>; each
    /// change and the creation are recorded under the principal <c>organization</c> with
    /// the reason given, the creation is announced, no grant and no membership is
    /// written, and the one line on standard output is the organization's identifier.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_BOOT_001_AC10_TheCommandCreatesTheOrganizationFromTheServerAsync()
    {
        const string name = "Northwind Clinics";

        await File.WriteAllTextAsync(_policy, Stricter, TestContext.Current.CancellationToken);

        Invocation run = await OrganizationAsync("--name", name, "--policy", _policy, "--reason", Reason, "--multiplememberships", "true");

        Reported.Success(run);

        OrganizationId created = Created(run);
        string policy = Settings.OrganizationPolicy.For(created.ToString()).ToString();

        await using NpgsqlConnection connection = await deployment.OpenAsync();

        IReadOnlyList<(string Action, string? Key, string? Reason, string? PrincipalReason)> recorded = [.. await connection
            .QueryAsync<(string, string?, string?, string?)>(
                """
                SELECT action, details->>'key', details->>'reason', principal_reason
                FROM identity.audit_records
                WHERE principal = 'organization' AND (organization = @Organization OR details->>'key' = ANY(@Keys))
                ORDER BY occurred_at, id
                """,
                new { Organization = created.Value, Keys = new[] { policy, Settings.OrganizationMultipleMemberships.Key.ToString() } })];

        Assert.Equal(name, await connection.ExecuteScalarAsync<string>(
            "SELECT name FROM identity.organizations WHERE id = @Id",
            new { Id = created.Value }));
        Assert.Equal("true", await ValueAsync(connection, Settings.OrganizationMultipleMemberships.Key.ToString()));
        Assert.Equal(
            [
                ("identity.organization.created", null),
                ("ops.configuration.changed", Settings.OrganizationMultipleMemberships.Key.ToString()),
                ("ops.configuration.changed", policy),
                ("ops.configuration.changed", policy),
            ],
            recorded
                .Select(row => (row.Action, row.Key))
                .OrderBy(row => row.Action, StringComparer.Ordinal)
                .ThenBy(row => row.Key ?? string.Empty, StringComparer.Ordinal));
        Assert.All(recorded, row => Assert.Equal((Reason, "OPS-BOOT-001"), (row.Reason, row.PrincipalReason)));
        Assert.Equal(1, await connection.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM identity.events WHERE kind = 'OrganizationCreated' AND payload->'Organization'->>'Value' = @Id",
            new { Id = created.Value.ToString() }));
        Assert.Equal(0, await connection.ExecuteScalarAsync<long>(
            """
            SELECT (SELECT count(*) FROM identity.grants WHERE organization = @Id)
                 + (SELECT count(*) FROM identity.memberships WHERE organization = @Id)
            """,
            new { Id = created.Value }));
    }

    /// <summary>
    /// IDN-ORG-002 AC4 (D-248): the policy the command writes from the file is stored as
    /// the policy route stores the same fields.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ORG_002_AC4_ThePolicyIsStoredAsThePolicyRouteStoresItAsync()
    {
        await File.WriteAllTextAsync(_policy, Stricter, TestContext.Current.CancellationToken);

        Invocation run = await OrganizationAsync("--name", "Contoso Pharmacies", "--policy", _policy, "--reason", Reason);

        Reported.Success(run);

        OrganizationId created = Created(run);
        string fields = JsonNode.Parse(Stricter)!.ToJsonString();

        await using NpgsqlConnection connection = await deployment.OpenAsync();

        Assert.Equal(
            Settings.OrganizationPolicy.Write(
                Settings.OrganizationPolicy.Read(created.ToString(), fields)
                    .Match(value => value, error => throw new InvalidOperationException(error.Code.ToString()))),
            await ValueAsync(connection, Settings.OrganizationPolicy.For(created.ToString()).ToString()));
    }

    /// <summary>
    /// OPS-BOOT-001 AC11 (D-248): run again with the same arguments the command changes
    /// and records nothing and prints the same identifier; run with another file it
    /// writes and records the policy alone.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_BOOT_001_AC11_RunAgainTheCommandWritesOnlyWhatDiffersAsync()
    {
        string[] arguments = ["--name", "Fabrikam Labs", "--policy", _policy, "--reason", Reason, "--multiplememberships", "false"];

        await File.WriteAllTextAsync(_policy, Stricter, TestContext.Current.CancellationToken);

        OrganizationId first = Created(await OrganizationAsync(arguments));

        await using NpgsqlConnection connection = await deployment.OpenAsync();

        long before = await RecordedAsync(connection);

        Invocation again = await OrganizationAsync(arguments);

        Reported.Success(again);
        Assert.Equal(first, Created(again));
        Assert.Equal(before, await RecordedAsync(connection));

        await File.WriteAllTextAsync(_policy, """{ "requiredAssurance": "aal2" }""", TestContext.Current.CancellationToken);

        Invocation changed = await OrganizationAsync(arguments);

        Reported.Success(changed);
        Assert.Equal(first, Created(changed));
        Assert.Equal(before + 1, await RecordedAsync(connection));
        Assert.Equal(
            Settings.OrganizationPolicy.For(first.ToString()).ToString(),
            await connection.ExecuteScalarAsync<string>(
                """
                SELECT details->>'key' FROM identity.audit_records
                WHERE principal = 'organization' ORDER BY occurred_at DESC, id DESC LIMIT 1
                """));
    }

    /// <summary>
    /// OPS-BOOT-001 AC12 (D-248): where two organizations hold the name, the command is
    /// refused with <c>api.request.malformed</c> naming <c>name</c> and writes nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_BOOT_001_AC12_ANameTwoOrganizationsHoldIsRefusedAsync()
    {
        await File.WriteAllTextAsync(_policy, Stricter, TestContext.Current.CancellationToken);

        OrganizationId first = Created(await OrganizationAsync("--name", "Tailspin Clinics", "--policy", _policy, "--reason", Reason));

        await using NpgsqlConnection connection = await deployment.OpenAsync();

        // The policy route creates organizations by name with no check of the name, so
        // two may share one.
        await connection.ExecuteAsync(
            """
            INSERT INTO identity.organizations (id, name, canonical_name, created_at)
            SELECT gen_random_uuid(), name, canonical_name, created_at FROM identity.organizations WHERE id = @Id
            """,
            new { Id = first.Value });

        long before = await RecordedAsync(connection);

        await File.WriteAllTextAsync(_policy, """{ "requiredAssurance": "aal2" }""", TestContext.Current.CancellationToken);

        Invocation run = await OrganizationAsync("--name", "Tailspin Clinics", "--policy", _policy, "--reason", Reason);

        Assert.Equal("api.request.malformed", Reported.Refusal(run));
        Assert.Equal("name", Detail(run, "member"));
        Assert.Equal(before, await RecordedAsync(connection));
    }

    /// <summary>
    /// OPS-BOOT-001 AC13 (D-248): on a deployment the migrations raised and bootstrap has
    /// not stood up, the command is refused and creates nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_BOOT_001_AC13_ADeploymentNotBootstrappedIsRefusedAsync()
    {
        string fresh = await deployment.CreateDatabaseAsync("not_bootstrapped");

        Reported.Success(await Invocation.PipedAsync(["migrate"], new JsonObject { ["connection"] = fresh }));

        await File.WriteAllTextAsync(_policy, Stricter, TestContext.Current.CancellationToken);

        Invocation run = await Invocation.PipedAsync(
            ["organization", "--name", "Northwind Clinics", "--policy", _policy, "--reason", Reason],
            Invocation.Keys(fresh));

        await using var connection = new NpgsqlConnection(fresh);

        Assert.Equal("authz.denied", Reported.Refusal(run));
        Assert.Equal(0, await connection.ExecuteScalarAsync<long>("SELECT count(*) FROM identity.organizations"));
        Assert.Equal(0, await connection.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM identity.audit_records WHERE principal = 'organization'"));
    }

    /// <summary>
    /// OPS-BOOT-001 AC13 and IDN-ORG-002 AC4 (D-248): a policy the policy route refuses
    /// is refused with that route's code once the database is read, a field looser than
    /// the system's among them, and a reason missing or empty with
    /// <c>config.change.reasonrequired</c>; nothing of a refused run is written.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_BOOT_001_AC13_ARefusedRunWritesNothingAsync()
    {
        const string name = "Wingtip Clinics";

        await using NpgsqlConnection connection = await deployment.OpenAsync();

        long before = await RecordedAsync(connection);

        await File.WriteAllTextAsync(_policy, """{ "loginFactors": ["password", "emailLink"] }""", TestContext.Current.CancellationToken);

        Invocation looser = await OrganizationAsync("--name", name, "--policy", _policy, "--reason", Reason);

        await File.WriteAllTextAsync(_policy, Stricter, TestContext.Current.CancellationToken);

        Invocation unexplained = await OrganizationAsync("--name", name, "--policy", _policy);
        Invocation blank = await OrganizationAsync("--name", name, "--policy", _policy, "--reason", "  ");

        Assert.Equal("config.policy.belowsystem", Reported.Refusal(looser));
        Assert.Equal("loginFactors", Detail(looser, "field"));
        Assert.Equal("config.change.reasonrequired", Reported.Refusal(unexplained));
        Assert.Equal("config.change.reasonrequired", Reported.Refusal(blank));
        Assert.Equal(before, await RecordedAsync(connection));
        Assert.Equal(0, await connection.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM identity.organizations WHERE name = @Name",
            new { Name = name }));
    }

    private static OrganizationId Created(Invocation run)
    {
        string[] lines = run.Output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        Assert.Single(lines);
        Assert.True(OrganizationId.TryParse(lines[0], provider: null, out OrganizationId created));

        return created;
    }

    private static async Task<string?> ValueAsync(NpgsqlConnection connection, string key) =>
        await connection.ExecuteScalarAsync<string?>(
            "SELECT value FROM identity.settings WHERE key = @Key",
            new { Key = key });

    private static async Task<long> RecordedAsync(NpgsqlConnection connection) =>
        await connection.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM identity.audit_records WHERE principal = 'organization'");

    private static string? Detail(Invocation run, string name)
    {
        using var refusal = JsonDocument.Parse(run.Error);

        return refusal.RootElement.GetProperty("details").GetProperty(name).GetString();
    }

    private Task<Invocation> OrganizationAsync(params string[] arguments) =>
        Invocation.PipedAsync(["organization", .. arguments], Invocation.Keys(deployment.ConnectionString));
}
