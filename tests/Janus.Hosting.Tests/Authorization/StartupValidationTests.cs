using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Tests.Sending;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Xunit;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// What the checks that read the database decide over a deployment, and where they run
/// (AUTHZ-MODEL-004, AUTHZ-DERIVE-004).
/// </summary>
/// <param name="host">The deployment the checks read.</param>
[Trait("kind", "integration")]
public sealed class StartupValidationTests(HostFixture host) : IClassFixture<HostFixture>
{
    /// <summary>
    /// AUTHZ-MODEL-004, AUTHZ-DERIVE-004 AC1: the deployment's roles allow what it
    /// declares and the columns its derivation names are indexed, so it starts.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task StartAsync_TheDeploymentAsItStands_StartsAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using IHost deployment = Deployed();

        await deployment.StartAsync(cancellationToken);
        await deployment.StopAsync(cancellationToken);
    }

    /// <summary>
    /// AUTHZ-MODEL-004 AC1, AC2: a role written into the deployment allowing a
    /// permission the model does not declare stops the deployment as it starts, with
    /// its own code, which the declaration alone could never decide.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_MODEL_004_AC1_AStoredRoleAllowingAnUndeclaredPermissionIsRefusedAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await WriteAsync(
            """
            INSERT INTO janus.roles (name) VALUES ('forger');
            INSERT INTO janus.role_permissions (role, permission)
            VALUES ('forger', 'document:forge');
            """,
            cancellationToken);

        try
        {
            using IHost deployment = Deployed();

            StartupException refused = await Assert.ThrowsAsync<StartupException>(
                async () => await deployment.StartAsync(cancellationToken));

            Assert.Equal(ErrorCodes.StartupUndeclaredPermission, refused.Failure?.Code);
        }
        finally
        {
            await WriteAsync("DELETE FROM janus.roles WHERE name = 'forger';", cancellationToken);
        }
    }

    /// <summary>
    /// AUTH-ABUSE-005 AC3 and LIB-HOST-001: a deployment that has declared no
    /// message catalogue can answer in no language, and is stopped as it starts
    /// rather than at the first message a person waits for.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_ABUSE_005_AC3_ADeploymentThatDeclaredNoMessagesIsRefusedAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using IHost deployment = Deployed(catalogue: false);

        StartupException refused = await Assert.ThrowsAsync<StartupException>(
            async () => await deployment.StartAsync(cancellationToken));

        Assert.Equal(ErrorCodes.StartupDeclarationMissing, refused.Failure?.Code);
    }

    /// <summary>
    /// PRIV-RIGHT-005b AC3: the deployment declares its documents sensitive, so a
    /// deployment that registered nothing to do the host-side work for them is
    /// stopped as it starts rather than at the erasure that would reach no one.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_005b_AC3_ADeploymentWithNoHandlerForItsSensitiveTypeIsRefusedAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using IHost deployment = Deployed(handlers: false);

        StartupException refused = await Assert.ThrowsAsync<StartupException>(
            async () => await deployment.StartAsync(cancellationToken));

        Assert.Equal(ErrorCodes.StartupDeclarationMissing, refused.Failure?.Code);
        Assert.Equal("document", refused.Failure?.Details["handler"].GetString());
    }

    /// <summary>
    /// AUTHZ-MODEL-004 AC2: the web server is a hosted service of the host's, and
    /// hosted services start in the order they were registered, so the checks that read
    /// the database stand at the head of the collection and no request is served
    /// before them (D-160).
    /// </summary>
    [Fact]
    public void AUTHZ_MODEL_004_AC2_TheChecksStartBeforeEverythingElseRegistered()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IHostedService>(new ServerStandIn());

        Declared(services);

        ServiceDescriptor first = services.First(
            service => service.ServiceType == typeof(IHostedService));

        Assert.Equal(0, services.IndexOf(first));
        Assert.Equal(typeof(ModelValidationService), first.ImplementationType);
    }

    private IHost Deployed(bool catalogue = true, bool handlers = true) => new HostBuilder()
        .ConfigureServices(services => Declared(services, catalogue, handlers))
        .Build();

    // The library registered over this deployment, as the host's own code registers
    // it, with the messages the deployment has written (LIB-HOST-001).
    private IServiceCollection Declared(
        IServiceCollection services,
        bool catalogue = true,
        bool handlers = true)
    {
        if (catalogue)
        {
            services.AddSingleton<IMessageTemplates>(new MessageTemplatesInMemory());
        }

        if (handlers)
        {
            services.AddSingleton<ISubjectEventSubscriber>(new HostSubjectEvents());
        }

        return services.AddJanus(
            host.ConnectionString,
            new KeyEncryptionKeys(1, new Dictionary<int, ReadOnlyMemory<byte>> { [1] = new byte[32] }),
            new byte[32],
            HostFixture.Declaration(),
            JanusApplication.Public);
    }

    private async Task WriteAsync(string statement, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        await connection.ExecuteAsync(new CommandDefinition(
            statement,
            cancellationToken: cancellationToken));
    }

    // A hosted service of the host's own, registered before the library is, standing
    // for the web server the deployment starts.
    private sealed class ServerStandIn : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
