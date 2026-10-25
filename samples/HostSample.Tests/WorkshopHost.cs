using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace HostSample.Tests;

/// <summary>
/// A deployment of the kite workshop as its tests stand one up: an empty database
/// prepared by the library's own commands, the workshop's tables beside the library's,
/// the application started on a web server in memory with mail and SMS taken in memory,
/// and its people signed in through the library's own endpoints: the first
/// administrator, who publishes the notice and the terms and opens the organization
/// that runs the workshop, and a technician of that organization, who opens it.
/// </summary>
/// <remarks>
/// Implements LIB-TEST-004. One container per test class, torn down with the class.
/// </remarks>
public sealed class WorkshopHost : IAsyncLifetime
{
    // The back channel the application reaches its provider on (BFF-SESS-006).
    private const string SignOnChannel = "identity-signon";

    private const string FirstAdministrator = "administrator@kites.example.test";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    private readonly ServerInMemory _server = new();

    // Where the keys and each software passkey's credential are drawn from.
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    private string _connectionString = string.Empty;

    private WebApplication? _application;

    private TestPeople? _people;

    private TestSession? _administrator;

    private TestSession? _technician;

    private int _customers;

    /// <summary>
    /// The origin the workshop answers at.
    /// </summary>
    public static Uri Origin { get; } = new("https://kites.example.test");

    /// <summary>
    /// The legal document the workshop publishes as its terms of service.
    /// </summary>
    public static DocumentName Terms { get; } = DocumentName.Parse("terms");

    /// <summary>
    /// The deployment's clock, which a test moves forward.
    /// </summary>
    internal ShiftedClock Clock { get; } = new();

    /// <summary>
    /// What went out by mail.
    /// </summary>
    public InMemoryMailTransport Mail { get; } = new();

    /// <summary>
    /// What went out by SMS.
    /// </summary>
    public InMemorySmsTransport Sms { get; } = new();

    /// <summary>
    /// The first administrator.
    /// </summary>
    public TestSession Administrator => _administrator ?? throw new InvalidOperationException("The host has not started.");

    /// <summary>
    /// A technician of the organization that runs the workshop.
    /// </summary>
    public TestSession Technician => _technician ?? throw new InvalidOperationException("The host has not started.");

    /// <summary>
    /// The organization that runs the workshop.
    /// </summary>
    public OrganizationId Organization { get; private set; }

    /// <summary>
    /// The workshop the technician opened.
    /// </summary>
    public string Workshop { get; private set; } = string.Empty;

    /// <summary>
    /// The workshop's job, as the application registers it.
    /// </summary>
    public FollowUpCalls FollowUpCalls => Application.Services.GetRequiredService<FollowUpCalls>();

    private TestPeople People => _people ?? throw new InvalidOperationException("The host has not started.");

    private WebApplication Application => _application ?? throw new InvalidOperationException("The host has not started.");

    /// <summary>
    /// A browser that holds no session, as a visitor's does.
    /// </summary>
    /// <returns>The browser.</returns>
    public HttpClient Visitor()
    {
        HttpMessageHandler? channel = _server.Channel();

        try
        {
            var visitor = new HttpClient(channel, disposeHandler: true)
            {
                BaseAddress = new Uri(WorkshopApplication.Address(Origin).AbsoluteUri + "/"),
            };

            channel = null;

            return visitor;
        }
        finally
        {
            channel?.Dispose();
        }
    }

    /// <summary>
    /// Registers a customer of the workshop through the registration endpoints, with a
    /// phone of their own, which the deployment requires of every account, and a
    /// password, and signs the customer in.
    /// </summary>
    /// <param name="cancellationToken">Abandons the registration.</param>
    /// <returns>The customer's session.</returns>
    public async Task<TestSession> CustomerAsync(CancellationToken cancellationToken)
    {
        int customer = Interlocked.Increment(ref _customers);

        if (!EmailAddress.TryParse("customer." + customer + "@kites.example.test", out EmailAddress email))
        {
            throw new InvalidOperationException("The customer's address does not read as one.");
        }

        return await People.CustomerAsync(
            email,
            Phone("+2010020" + customer.ToString("D5", CultureInfo.InvariantCulture)),
            Convert.ToHexString(RandomNumberGenerator.GetBytes(16)),
            cancellationToken);
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
    /// Asks the gate whether a context may act under a permission across an organization,
    /// in a scope of the application's own and outside any request, as a host's
    /// background work asks it.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="permission">What it would do.</param>
    /// <param name="organization">The organization it would do it in.</param>
    /// <param name="cancellationToken">Abandons the question.</param>
    /// <returns>The gate's answer.</returns>
    public async Task<Result> RequireAsync(
        AccessContext context,
        Permission permission,
        OrganizationId organization,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = Application.Services.CreateAsyncScope();

        return await scope.ServiceProvider
            .GetRequiredService<IAccessGate>()
            .RequireAsync(context, permission, organization, cancellationToken);
    }

    /// <summary>
    /// Has the technician open a workshop of the organization's.
    /// </summary>
    /// <param name="name">The workshop's name.</param>
    /// <param name="capacity">How many kites it holds for repair at once.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The workshop's identifier.</returns>
    public async Task<string> OpenAsync(string name, int capacity, CancellationToken cancellationToken)
    {
        Answered opened = await Exchanges.SendAsync(
            Technician,
            HttpMethod.Post,
            "workshops",
            new { organization = Organization.Value, name, capacity },
            cancellationToken);

        return opened.Status is HttpStatusCode.Created
            ? opened.Body!.Value.GetProperty("id").GetString()!
            : throw new InvalidOperationException("POST workshops answered " + (int)opened.Status + " " + opened.Code + ".");
    }

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync(CancellationToken.None);

        // OPS-DB-001: the database is created under ICU before the first migration runs.
        await using (var server = new NpgsqlConnection(_container.GetConnectionString()))
        {
            await server.OpenAsync(CancellationToken.None);

            await using NpgsqlCommand created = server.CreateCommand();

            created.CommandText = "CREATE DATABASE workshop TEMPLATE template0 "
                + "LOCALE_PROVIDER icu ICU_LOCALE 'und' LC_COLLATE 'C' LC_CTYPE 'C'";

            _ = await created.ExecuteNonQueryAsync(CancellationToken.None);
        }

        _connectionString = new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = "workshop" }
            .ConnectionString;

        // OPS-MIG-003a: the scheduled maintenance runs under the maintenance role.
        var secrets = new FixedSecretSource(_randomness)
        {
            MaintenanceCredential = Encoding.UTF8.GetBytes(
                new NpgsqlConnectionStringBuilder(_connectionString) { Options = "-c role=identity_maintenance" }.ConnectionString),
        };

        TestDeployment deployment = await TestDeployment.PrepareAsync(
            WorkshopApplication.Address(Origin),
            new SignOnClient(WorkshopApplication.Client),
            secrets,
            _connectionString,
            _connectionString,
            Bootstrapped(),
            CancellationToken.None);

        await using (DbConnection connection = await ConnectAsync(CancellationToken.None))
        {
            await using DbCommand created = connection.CreateCommand();

            created.CommandText = WorkshopSchema.Creation;

            _ = await created.ExecuteNonQueryAsync(CancellationToken.None);
        }

        _application = Built(secrets);

        await _application.StartAsync(CancellationToken.None);

        if (!EmailAddress.TryParse(FirstAdministrator, out EmailAddress administrator))
        {
            throw new InvalidOperationException("The first administrator's address does not read as one.");
        }

        _people = new TestPeople(
            deployment.Address,
            new SignOnClient(WorkshopApplication.Client),
            Terms,
            _server.Channel,
            Mail,
            Sms,
            _randomness);
        _administrator = await _people.AdministratorAsync(deployment.EnrolmentLink, administrator, CancellationToken.None);

        await PublishedAsync("admin/notices", "The workshop's privacy notice.");
        await PublishedAsync("admin/documents/" + Terms + "/versions", "The workshop's terms of service.");

        Answered organization = await Exchanges.SteppedAsync(
            Administrator,
            HttpMethod.Post,
            "admin/organizations",
            new { name = "Harbour kite repairs", reason = "The organization that runs the workshop." },
            CancellationToken.None);

        Organization = organization.Status is HttpStatusCode.Created
            ? OrganizationId.Parse(organization.Body!.Value.GetProperty("id").GetString()!, provider: null)
            : throw new InvalidOperationException("POST admin/organizations answered " + (int)organization.Status + " " + organization.Code + ".");

        if (!EmailAddress.TryParse("technician@kites.example.test", out EmailAddress technician))
        {
            throw new InvalidOperationException("The technician's address does not read as one.");
        }

        _technician = await _people.StaffMemberAsync(
            Administrator,
            Organization,
            technician,
            Phone("+201001000001"),
            [RoleName.Parse(WorkshopRoles.Technician)],
            CancellationToken.None);

        Workshop = await OpenAsync("Harbour kite repairs, the quay", capacity: 20, CancellationToken.None);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _technician?.Dispose();
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

    // The application as the workshop composes it, with what this deployment supplies
    // of its own: the web server, the clock, the secret source, the transports and the
    // stand-ins for the optional environment seams (LIB-TEST-003).
    private WebApplication Built(FixedSecretSource secrets)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();

        builder.Logging.ClearProviders();

        _ = builder.Services.AddSingleton<IServer>(_server);
        _ = builder.Services.AddSingleton<TimeProvider>(Clock);
        _ = builder.Services.AddSingleton<ISecretSource>(secrets);
        _ = builder.Services.AddSingleton<IMailTransport>(Mail);
        _ = builder.Services.AddSingleton<ISmsTransport>(Sms);
        _ = builder.Services.AddSingleton<IClockReference>(new FixedClockReference());
        _ = builder.Services.AddSingleton<ICertificateRenewal>(new SucceededCertificateRenewal());
        _ = builder.Services.AddSingleton<ILocationSource>(new EmptyLocationSource(Clock));
        _ = builder.Services.AddSingleton<IDatacenterRangeSource>(new EmptyDatacenterRangeSource(Clock));

        WorkshopApplication.Register(builder.Services, Origin, _connectionString);

        _ = builder.Services.AddHttpClient(SignOnChannel).ConfigurePrimaryHttpMessageHandler(_server.Channel);

        WebApplication application = builder.Build();

        WorkshopApplication.Mount(application);

        return application;
    }

    // REG-IDENT-001: the phone a person registers with, since registration.phone keeps
    // its default, required.
    private static PhoneNumber Phone(string entered) =>
        PhoneNumber.TryParse(entered, out PhoneNumber phone)
            ? phone
            : throw new InvalidOperationException("The phone " + entered + " does not read as one.");

    // OPS-BOOT-001: the values that name the deployment, each as its key admits it.
    private static List<string> Bootstrapped()
    {
        List<string> arguments =
        [
            "--organization", "Kite workshop administration",
            "--email", FirstAdministrator,
            "--phone", "+201000000103",
            "--dateofbirth", "1990-01-01",
        ];

        foreach ((ConfigurationKey key, string value) in new Dictionary<ConfigurationKey, string>
        {
            [Settings.WebAuthnOrigins.Key] = Settings.WebAuthnOrigins.Write([Origin.GetLeftPart(UriPartial.Authority)]),
            [Settings.HostingLocation.Key] = Settings.HostingLocation.Write(HostingLocation.Inside),
            [Settings.HostingEnvironment.Key] = Settings.HostingEnvironment.Write("a rented virtual machine"),
            [Settings.AlertingEmailDestinations.Key] = Settings.AlertingEmailDestinations.Write(["operator@kites.example.test"]),
            [Settings.AlertingSmsDestinations.Key] = Settings.AlertingSmsDestinations.Write(["+201000000101"]),
            [Settings.AlertingOwnerEmail.Key] = Settings.AlertingOwnerEmail.Write("owner@kites.example.test"),
            [Settings.AlertingOwnerSms.Key] = Settings.AlertingOwnerSms.Write("+201000000102"),
            [Settings.AbuseSmsBalanceFloor.Key] = Settings.AbuseSmsBalanceFloor.Write(100m),
            [Settings.LegalGoverningLanguage.Key] = Settings.LegalGoverningLanguage.Write("en"),
            [Settings.PrivacyCalendarTimeZone.Key] = Settings.PrivacyCalendarTimeZone.Write("UTC"),
            [Settings.NotificationLanguages.Key] = Settings.NotificationLanguages.Write(["en"]),
            [Settings.NotificationEmailSendingDomain.Key] = Settings.NotificationEmailSendingDomain.Write("mail.kites.example.test"),
        })
        {
            arguments.AddRange(["--" + key, value]);
        }

        return arguments;
    }

    // A version of a legal document, published by the first administrator.
    private async Task PublishedAsync(string route, string text)
    {
        Answered published = await Exchanges.SteppedAsync(
            Administrator,
            HttpMethod.Post,
            route,
            new { text, material = false },
            CancellationToken.None);

        if (published.Status is not HttpStatusCode.OK)
        {
            throw new InvalidOperationException("POST " + route + " answered " + (int)published.Status + " " + published.Code + ".");
        }
    }
}
