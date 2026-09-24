using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Janus.Core.Configuration;
using Npgsql;
using Xunit;

namespace Janus.Cli.Tests;

/// <summary>
/// What the <c>bootstrap</c> command leaves in a fresh deployment's database: the first
/// organization and administrator with an enrolment link, the reserved
/// <c>emergency</c> account, the restore test's canary, and no credential of any kind
/// (OPS-BOOT-001, OPS-BOOT-002).
/// </summary>
[Trait("kind", "integration")]
public sealed class BootstrapTests(BootstrappedDeployment deployment) : IClassFixture<BootstrappedDeployment>
{
    private const string Link = Invocation.Origin + "/enrol#token=";

    /// <summary>
    /// OPS-BOOT-001: a fresh deployment is stood up by the command, which answers with
    /// the link and nothing else.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_BOOT_001_AFreshDeploymentIsStoodUpByTheCommandAsync()
    {
        Assert.Equal(0, deployment.First.ExitCode);
        Assert.Empty(deployment.First.Error);

        await using NpgsqlConnection connection = await deployment.OpenAsync();

        IReadOnlyList<string> administrative = [.. await connection.QueryAsync<string>(
            "SELECT name FROM identity.organizations WHERE administrative")];

        Assert.Equal([Invocation.Organization], administrative);
    }

    /// <summary>
    /// OPS-BOOT-001 AC1: running it again while a system administrator exists is refused,
    /// and changes nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_BOOT_001_AC1_RunningItAgainWhileASystemAdministratorExistsIsRefusedAsync()
    {
        await using NpgsqlConnection connection = await deployment.OpenAsync();

        long before = await connection.ExecuteScalarAsync<long>("SELECT count(*) FROM identity.accounts");

        Invocation again = await Invocation.PipedAsync(Invocation.Bootstrap(), Invocation.Keys(deployment.ConnectionString));

        Assert.Equal(1, again.ExitCode);
        Assert.Empty(again.Output);
        Assert.Equal(ErrorCodes.Denied.ToString(), Code(again));
        Assert.Equal(before, await connection.ExecuteScalarAsync<long>("SELECT count(*) FROM identity.accounts"));
        Assert.Equal(1, await connection.ExecuteScalarAsync<long>("SELECT count(*) FROM identity.organizations"));
    }

    /// <summary>
    /// OPS-BOOT-001 AC2: no account bootstrap creates holds a credential of any kind, so
    /// none is known to anybody.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_BOOT_001_AC2_NoAccountHoldsACredentialAsync()
    {
        await using NpgsqlConnection connection = await deployment.OpenAsync();

        foreach (string table in (string[])["passwords", "authenticators", "recovery_codes", "break_glass_credentials"])
        {
            Assert.Equal(0, await connection.ExecuteScalarAsync<long>($"SELECT count(*) FROM identity.{table}"));
        }
    }

    /// <summary>
    /// OPS-BOOT-001 AC3 and D-133: the command emits no break-glass credential, and the
    /// alert that none exists is raised on the alert channels at once.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_BOOT_001_AC3_NoEmergencyCredentialIsIssuedAndItsAbsenceIsRaisedAsync()
    {
        await using NpgsqlConnection connection = await deployment.OpenAsync();

        string[] printed = deployment.First.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.Single(printed);
        Assert.StartsWith(Link, printed[0], StringComparison.Ordinal);
        Assert.Equal(0, await connection.ExecuteScalarAsync<long>("SELECT count(*) FROM identity.break_glass_credentials"));
        Assert.Equal(1, await connection.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM identity.raised_alerts WHERE condition = 'no-emergency-credential'"));
    }

    /// <summary>
    /// OPS-BOOT-001: the printed address opens an enrolment for the first administrator,
    /// for <c>recovery.link.lifetime</c>, and the administrator is a member of the
    /// administrative organization holding the system administrator's role.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_BOOT_001_ThePrintedAddressEnrolsTheFirstAdministratorAsync()
    {
        await using NpgsqlConnection connection = await deployment.OpenAsync();

        string token = deployment.First.Output.Trim()[Link.Length..];

        (Guid subject, string purpose, TimeSpan lifetime) = await connection.QuerySingleAsync<(Guid, string, TimeSpan)>(
            "SELECT subject, purpose, expires_at - issued_at FROM identity.recovery_links WHERE token = @Fingerprint",
            new { Fingerprint = SHA256.HashData(Encoding.UTF8.GetBytes(token)) });

        Assert.Equal("enrolment", purpose);
        Assert.Equal(Settings.RecoveryLinkLifetime.Default, lifetime);
        Assert.Equal(["system-administrator"], await RolesAsync(connection, subject));
        Assert.Equal(3, await connection.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM identity.identifiers WHERE subject = @Subject AND verified_at IS NOT NULL",
            new { Subject = subject }));
    }

    /// <summary>
    /// OPS-BOOT-002 and INT-MAIL-006 AC1b: the reserved account holds the system
    /// administrator's role in the administrative organization and nothing else: no
    /// identifier, no mailbox, no credential.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_BOOT_002_TheEmergencyAccountHoldsTheRoleAndNoWayInAsync()
    {
        await using NpgsqlConnection connection = await deployment.OpenAsync();

        Guid emergency = await connection.QuerySingleAsync<Guid>("SELECT subject FROM identity.accounts WHERE emergency");

        Assert.Equal(["system-administrator"], await RolesAsync(connection, emergency));
        Assert.Equal(0, await connection.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM identity.identifiers WHERE subject = @Subject", new { Subject = emergency }));
        Assert.Equal(0, await connection.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM identity.mailboxes WHERE holder = @Subject", new { Subject = emergency }));
    }

    /// <summary>
    /// INT-MAIL-006 AC1a: the administrator's membership is effective at once and the
    /// mailbox is queued for the provisioning job, not created by bootstrap.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_MAIL_006_AC1a_TheAdministratorsMailboxIsQueuedAsync()
    {
        await using NpgsqlConnection connection = await deployment.OpenAsync();

        IReadOnlyList<(Guid Holder, string? Pushed)> queued = [.. await connection.QueryAsync<(Guid, string?)>(
            "SELECT holder, pushed FROM identity.mailboxes")];

        (Guid holder, string? pushed) = Assert.Single(queued);

        Assert.Null(pushed);
        Assert.Equal(["system-administrator"], await RolesAsync(connection, holder));
    }

    /// <summary>
    /// DR-007 and OPS-BOOT-001: the canary subject is seeded in the administrative
    /// organization with one encrypted field and one verified email, and recorded where
    /// the restore test reads it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task DR_007_TheCanarySubjectIsSeededAsync()
    {
        await using NpgsqlConnection connection = await deployment.OpenAsync();

        var canary = Guid.Parse(await connection.QuerySingleAsync<string>(
            "SELECT value FROM identity.settings WHERE key = @Key",
            new { Key = Settings.BackupRestoreTestCanary.Key.ToString() }));

        Assert.Equal(1, await connection.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM identity.identifiers WHERE subject = @Subject AND kind = 'email' AND verified_at IS NOT NULL",
            new { Subject = canary }));
        Assert.Equal(1, await connection.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM identity.profiles WHERE subject = @Subject AND enc_display_name IS NOT NULL",
            new { Subject = canary }));
        Assert.Equal(1, await connection.ExecuteScalarAsync<long>(
            """
            SELECT count(*) FROM identity.memberships m
            JOIN identity.organizations o ON o.id = m.organization
            WHERE m.subject = @Subject AND o.administrative
            """,
            new { Subject = canary }));
        Assert.Empty(await RolesAsync(connection, canary));
    }

    /// <summary>
    /// OPS-BOOT-001 and chapter 10 section 4.1a: the values the deployment named are its
    /// own, and the administrative organization's policy is the one the table gives it at
    /// bootstrap.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_BOOT_001_TheNamedValuesAndTheAdministrativePolicyAreWrittenAsync()
    {
        await using NpgsqlConnection connection = await deployment.OpenAsync();

        var stored = (await connection.QueryAsync<(string Key, string Value)>(
                "SELECT key, value FROM identity.settings"))
            .ToDictionary(row => row.Key, row => row.Value, StringComparer.Ordinal);

        foreach ((ConfigurationKey key, string value) in Invocation.Named())
        {
            Assert.Equal(value, stored[key.ToString()]);
        }

        Guid organization = await connection.QuerySingleAsync<Guid>(
            "SELECT id FROM identity.organizations WHERE administrative");
        string parameter = organization.ToString("D", System.Globalization.CultureInfo.InvariantCulture);

        PolicyOverride policy = Settings.OrganizationPolicy
            .Read(parameter, stored[Settings.OrganizationPolicy.For(parameter).ToString()])
            .Match(value => value, error => throw new InvalidOperationException(error.Code.ToString()));

        Assert.Equal(AssuranceLevel.Aal2, policy.RequiredAssurance);
        Assert.Equal([Factor.Passkey], policy.LoginFactors!);
        Assert.Equal(CredentialRedundancy.Enforced, policy.CredentialRedundancy);
        Assert.False(policy.SelfServiceRecovery);
        Assert.All(Enum.GetValues<StepUpAction>(), action =>
            Assert.Equal(new Gate(GateLevel.Aal2, true, Settings.SessionStepUpRecency.Default), policy.Gates![action]));
    }

    /// <summary>
    /// Chapter 10 section 3: the three administrative roles are seeded so the system is
    /// usable at once.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_BOOT_001_TheAdministrativeRolesAreSeededAsync()
    {
        await using NpgsqlConnection connection = await deployment.OpenAsync();

        ILookup<string, string> permissions = (await connection.QueryAsync<(string Role, string Permission)>(
                "SELECT role, permission FROM identity.role_permissions"))
            .ToLookup(row => row.Role, row => row.Permission, StringComparer.Ordinal);

        Assert.Equal(
            Permissions.All.Select(permission => permission.ToString()).Order(StringComparer.Ordinal),
            permissions["system-administrator"].Order(StringComparer.Ordinal));
        Assert.Equal(["audit:read", "grant:read", "ropa:read"], permissions["auditor"].Order(StringComparer.Ordinal));
        Assert.Equal(["audit:read", "restriction:grant"], permissions["support"].Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// IDN-PRIN-001 AC4 and OPS-CFG-005: what bootstrap defines and every value it sets
    /// are recorded under its own named principal with its stated reason, since no
    /// person is signed in to answer for them.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_PRIN_001_AC4_WhatBootstrapDefinesAndSetsIsRecordedUnderItsPrincipalAsync()
    {
        await using NpgsqlConnection connection = await deployment.OpenAsync();

        List<(string Action, string? Subject, string? Organization, string? Role)> recorded = [.. await connection
            .QueryAsync<(string Action, string? Subject, string? Organization, string? Role)>(
                """
                SELECT action, acting_subject::text, organization::text, details->>'role'
                FROM identity.audit_records
                WHERE principal = 'bootstrap' AND principal_reason = 'OPS-BOOT-001'
                """)];
        string organization = await connection.ExecuteScalarAsync<string>(
            "SELECT id::text FROM identity.organizations WHERE administrative") ?? string.Empty;

        Assert.Equal(
            [("authz.role.defined", "auditor"), ("authz.role.defined", "support"), ("authz.role.defined", "system-administrator")],
            recorded
                .Where(row => row.Action == "authz.role.defined")
                .Select(row => (row.Action, row.Role ?? string.Empty))
                .Order());
        Assert.Contains(("identity.organization.created", (string?)Unheld, (string?)organization, (string?)null), recorded);
        Assert.All(recorded, row => Assert.Equal(Unheld, row.Subject));

        IEnumerable<string> set = await connection.QueryAsync<string>(
            """
            SELECT details->>'key' FROM identity.audit_records
            WHERE action = 'ops.configuration.changed' AND principal = 'bootstrap'
            """);

        string[] named =
        [
            .. Invocation.Named().Keys.Select(key => key.ToString()),
            Settings.BackupRestoreTestCanary.Key.ToString(),
            Settings.OrganizationPolicy.For(organization).ToString(),
        ];

        Assert.Equal(named.Order(StringComparer.Ordinal), set.Order(StringComparer.Ordinal));
    }

    // The identity a system principal acts under, which no account holds.
    private static string Unheld => Guid.Empty.ToString();

    private static async Task<IReadOnlyList<string>> RolesAsync(NpgsqlConnection connection, Guid subject) =>
        [.. await connection.QueryAsync<string>(
            """
            SELECT g.role FROM identity.grants g
            JOIN identity.organizations o ON o.id = g.organization
            JOIN identity.memberships m ON m.subject = g.subject_id AND m.organization = o.id
            WHERE g.subject_id = @Subject AND o.administrative AND NOT g.deny AND g.revoked_at IS NULL
            """,
            new { Subject = subject })];

    private static string? Code(Invocation run)
    {
        using var refusal = JsonDocument.Parse(run.Error);

        return refusal.RootElement.GetProperty("code").GetString();
    }
}
