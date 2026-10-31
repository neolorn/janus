using System;
using System.Data.Common;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace HostSample.Tests;

/// <summary>
/// A deployment of the kite workshop with the workshop's own post room and duty board in
/// force, as the workshop composes them: every message the library owes is taken by the
/// post room and every alert by the duty board, and nothing replaces either. Nobody signs
/// in at it, since a sign-in reads a code the post room keeps for the courier; a guest's
/// browser is all its tests use.
/// </summary>
/// <remarks>
/// Implements LIB-TEST-004, INT-SMS-005, INT-SMS-006 and OPS-ALERT-003 (D-225). One
/// container per test class, torn down with the class.
/// </remarks>
public sealed class PostedHost : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    private readonly ServerInMemory _server = new();

    // Where the keys are drawn from.
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    private readonly ShiftedClock _clock = new();

    private string _connectionString = string.Empty;

    private FixedSecretSource? _secrets;

    private WebApplication? _application;

    private TestPeople? _people;

    private Uri? _enrolment;

    private TestSession? _administrator;

    /// <summary>
    /// The workshop's courier, as the application registers it.
    /// </summary>
    public Courier Courier => Application.Services.GetRequiredService<Courier>();

    private WebApplication Application => _application ?? throw new InvalidOperationException("The host has not started.");

    /// <summary>
    /// A guest's browser, which carries the browser profile's CSRF tokens for what a guest
    /// does with no account.
    /// </summary>
    /// <returns>The browser.</returns>
    public TestVisitor Guest() =>
        (_people ?? throw new InvalidOperationException("The host has not started.")).Visitor();

    /// <summary>
    /// The first administrator, enrolled with a passkey through the link the bootstrap
    /// left the first time a test asks, which reads no message to do it.
    /// </summary>
    /// <param name="cancellationToken">Abandons the enrolment.</param>
    /// <returns>The administrator's session.</returns>
    public async Task<TestSession> AdministratorAsync(CancellationToken cancellationToken)
    {
        if (_administrator is TestSession enrolled)
        {
            return enrolled;
        }

        if (!EmailAddress.TryParse(WorkshopHost.FirstAdministrator, out EmailAddress administrator))
        {
            throw new InvalidOperationException("The first administrator's address does not read as one.");
        }

        TestPeople people = _people ?? throw new InvalidOperationException("The host has not started.");

        _administrator = await people.AdministratorAsync(
            _enrolment ?? throw new InvalidOperationException("The host has not started."),
            administrator,
            cancellationToken);

        return _administrator;
    }

    /// <summary>
    /// Opens a connection to the deployment's database, to look at what the workshop
    /// stored as the workshop stored it.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The open connection.</returns>
    public async Task<DbConnection> ConnectAsync(CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(_connectionString);

        await connection.OpenAsync(cancellationToken);

        return connection;
    }

    /// <summary>
    /// The application as the workshop composes it on this deployment, built and not
    /// started, with one of its handlers taken out.
    /// </summary>
    /// <param name="serving">The web server it would be served on.</param>
    /// <param name="withheld">The handler taken out.</param>
    /// <returns>The application.</returns>
    internal WebApplication Without(ServerInMemory serving, Type withheld)
    {
        FixedSecretSource secrets = _secrets ?? throw new InvalidOperationException("The host has not started.");
        WebApplicationBuilder builder = WorkshopHost.Composed(
            serving,
            _server.Channel,
            _clock,
            secrets,
            WorkshopApplication.Client,
            _connectionString);

        _ = builder.Services.RemoveAll(withheld);

        WebApplication application = builder.Build();

        WorkshopApplication.Mount(application);

        return application;
    }

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        _connectionString = await WorkshopHost.DatabaseAsync(_container);

        FixedSecretSource secrets = WorkshopHost.Secrets(_connectionString, _randomness);

        _secrets = secrets;

        TestDeployment deployment = await WorkshopHost.PreparedAsync(_connectionString, secrets);

        _enrolment = deployment.EnrolmentLink;

        _application = WorkshopHost
            .Composed(_server, _server.Channel, _clock, secrets, WorkshopApplication.Client, _connectionString)
            .Build();

        WorkshopApplication.Mount(_application);

        await _application.StartAsync(CancellationToken.None);

        // Nobody signs in, so the people read no message; the handler they are given is
        // registered nowhere.
        _people = new TestPeople(
            deployment.Address,
            new SignOnClient(WorkshopApplication.Client),
            WorkshopHost.Terms,
            _server.Channel,
            new InMemoryNotifications(),
            _randomness);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _administrator?.Dispose();

        if (_application is not null)
        {
            await _application.StopAsync(CancellationToken.None);
            await _application.DisposeAsync();
        }

        _server.Dispose();

        await _container.DisposeAsync();

        _randomness.Dispose();
    }
}
