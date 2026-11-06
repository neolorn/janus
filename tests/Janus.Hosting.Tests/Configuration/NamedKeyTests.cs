using System;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Factors;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Tests.Authorization;
using Janus.Privacy.SubjectKeys;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Janus.Hosting.Tests.Configuration;

/// <summary>
/// A runtime key the deployment names, never written, read and given its first value
/// through the configuration administration (OPS-CFG-008, D-277).
/// </summary>
/// <param name="host">The deployment the cases run against, which never named <c>service.name</c>.</param>
[Trait("kind", "integration")]
public sealed class NamedKeyTests(HostFixture host) : IClassFixture<HostFixture>
{
    private const string Named = "Example Bank";

    private const string Reason = "The deployment is named for its authenticator apps.";

    /// <summary>
    /// OPS-CFG-008 (D-277): on PostgreSQL, <c>service.name</c>, which the deployment never
    /// wrote, reads with <c>value</c> and <c>default</c> null; its first value is written
    /// as a loosening with its reason and an audit entry naming no value before it; it
    /// then reads as that value, and an authenticator app's enrolment carries it in its
    /// address.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task OPS_CFG_008_AKeyTheDeploymentNamesReadsNullAndTakesItsFirstValueAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        (AccessContext administrator, SessionId session) = await AdministratorAsync();

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        IConfigurationAdministration configuration = scope.ServiceProvider.GetRequiredService<IConfigurationAdministration>();

        ConfiguredSetting unwritten = (await configuration.ReadAsync(administrator, Settings.ServiceName.Key, cancellationToken))
            .Match(read => read, error => throw new InvalidOperationException(error.Code.ToString()));

        Assert.Equal(JsonValueKind.Null, unwritten.Value.ValueKind);
        Assert.Null(unwritten.Default);
        Assert.False(unwritten.Protected);
        Assert.Equal(SettingDirection.AnyChange, unwritten.Direction);

        Result changed = await configuration.ChangeAsync(
            administrator,
            session,
            Settings.ServiceName.Key,
            JsonSerializer.SerializeToElement(Named),
            Reason,
            cancellationToken);

        Assert.Null(changed.Match<ErrorCode?>(() => null, error => error.Code));

        ConfiguredSetting written = (await configuration.ReadAsync(administrator, Settings.ServiceName.Key, cancellationToken))
            .Match(read => read, error => throw new InvalidOperationException(error.Code.ToString()));

        Assert.Equal(Named, written.Value.GetString());
        Assert.Null(written.Default);
        Assert.Equal(
            (JsonValueKind.Null, Named, true, Reason),
            await RecordedAsync(administrator.Acting!.Value));

        GeneratorEnrolment shown = await scope.ServiceProvider.GetRequiredService<TotpService>().ShownAsync(
            AuthenticatorId.New(TimeProvider.System),
            RandomNumberGenerator.GetBytes(20),
            "person@example.test",
            cancellationToken);

        Assert.StartsWith("otpauth://totp/Example%20Bank:person%40example.test?", shown.Address, StringComparison.Ordinal);
        Assert.Contains("&issuer=Example%20Bank&", shown.Address, StringComparison.Ordinal);
    }

    // The configuration change the trail holds for the key, by the account given: what
    // it says the key was before, what it became, whether it loosened and why.
    private async Task<(JsonValueKind Before, string? After, bool Loosening, string? Reason)> RecordedAsync(SubjectId acting)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        (string Before, string? After, bool Loosening, string? Reason) row = await connection
            .QuerySingleAsync<(string Before, string? After, bool Loosening, string? Reason)>(new CommandDefinition(
                """
                SELECT jsonb_typeof(details->'before'), details->>'after', (details->>'loosening')::boolean,
                       details->>'reason'
                FROM identity.audit_records
                WHERE action = 'ops.configuration.changed' AND acting_subject = @acting
                  AND details->>'key' = 'service.name';
                """,
                new { acting = acting.Value },
                cancellationToken: TestContext.Current.CancellationToken));

        return (row.Before == "null" ? JsonValueKind.Null : JsonValueKind.Undefined, row.After, row.Loosening, row.Reason);
    }

    // A member of the administrative organization holding the permissions a loosening of
    // a runtime key asks there, and a session of its own that proved a phishing-resistant
    // second factor just now, which the config:loosen gate asks.
    private async Task<(AccessContext Administrator, SessionId Session)> AdministratorAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Authorization.Deployment(host);

        _ = await deployment.BeginAsync([], cancellationToken);

        SubjectId account = await deployment.AccountAsync(cancellationToken);
        OrganizationId administrative = await deployment.AdministrativeAsync(cancellationToken);
        RoleName administering = await deployment.RoleAsync(
            [Permissions.ConfigurationRead, Permissions.ConfigurationManage, Permissions.SystemAdminister],
            cancellationToken);

        await deployment.MemberAsync(account, administrative, cancellationToken);
        await deployment.GrantAsync(
            GrantSubject.Of(account), administering, null, false, null, administrative, cancellationToken);

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        IServiceProvider services = scope.ServiceProvider;

        var session = Session.Begin(
            SessionId.New(TimeProvider.System),
            account,
            new Assurance(AssuranceLevel.Aal2, PhishingResistant: true),
            new SessionOrigin("198.51.100.7", new DeviceDescription("Firefox", "Linux")),
            services.GetRequiredService<TimeProvider>().GetUtcNow(),
            TimeSpan.FromDays(7),
            TimeSpan.FromDays(30),
            breakGlassReason: null);

        IUnitOfWork work = services.GetRequiredService<IUnitOfWork>();

        await work.BeginAsync(cancellationToken);

        // A session is kept under its person's key, which an account written directly
        // does not have until its first session asks for it.
        ISubjectKeyStore keys = services.GetRequiredService<ISubjectKeyStore>();

        if (await keys.FindBySubjectAsync(account, cancellationToken) is null)
        {
            await keys.CreateAsync(account, cancellationToken);
        }

        await services.GetRequiredService<ISessionStore>().AddAsync(
            session,
            RandomNumberGenerator.GetBytes(32),
            RandomNumberGenerator.GetBytes(32),
            cancellationToken);
        await work.CommitAsync(cancellationToken);

        return (AccessContext.Of(account), session.Id);
    }
}
