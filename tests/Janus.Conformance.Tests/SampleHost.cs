using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting;
using Janus.Hosting.Bff;
using Janus.Storage.Tests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace Janus.Conformance.Tests;

/// <summary>
/// A deployment of the library as the smallest host stands one up: three kinds of
/// thing of its own and two facts about them it derives roles from, the values
/// LIB-HOST-001 requires and no other, mail and SMS taken in memory, and the library
/// mounted under a path of the host's choosing.
/// </summary>
/// <remarks>
/// Implements LIB-TEST-001, LIB-API-002 AC2 and LIB-HOST-001. Everything here is
/// written against the public surface alone, as a host outside the repository writes
/// it. The deployment is bootstrapped and its client registered by the commands an
/// operator runs, and it is started as a web server starts it, the checks that run
/// before anything is served included. One container per test class, torn down with
/// the class.
/// </remarks>
public sealed class SampleHost : IAsyncLifetime
{
    /// <summary>
    /// Where the host mounts the library.
    /// </summary>
    public const string Prefix = "/identity";

    /// <summary>
    /// The relationship the keeper role is derived from.
    /// </summary>
    public const string Keeper = "keeper";

    /// <summary>
    /// The relationship the steward role is derived from.
    /// </summary>
    public const string Steward = "steward";

    // The one purpose the host processes its records for, and what it holds of whom.
    private const string Keeping = "keeping records";

    private const string Records = "records";

    private const string Members = "members";

    // The client the provider's refusals are asked of, and this application's own.
    private const string RelyingParty = "sample-relying-party";

    private const string Application = "sample-application";

    private readonly DatabaseFixture _database = new();
    private readonly ServerInMemory _server = new();

    // The deployment's keys and the two secrets it is handed, as a secrets manager
    // holds them; cleared when the deployment stops.
    private readonly byte[] _encryption = RandomNumberGenerator.GetBytes(32);
    private readonly byte[] _fingerprint = RandomNumberGenerator.GetBytes(FingerprintKeys.MinimumLength);
    private readonly byte[] _signOn = RandomNumberGenerator.GetBytes(32);
    private readonly byte[] _secret = Encoding.UTF8.GetBytes(Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32)));

    private WebApplication? _application;

    /// <summary>
    /// The origin the host answers at.
    /// </summary>
    public static Uri Origin { get; } = new("https://sample.example.test");

    /// <summary>
    /// The provider's issuer, which is the origin with the prefix.
    /// </summary>
    public static Uri Issuer { get; } = new(Origin, Prefix);

    /// <summary>
    /// The host's outermost kind of thing.
    /// </summary>
    public static ResourceType ShelfType { get; } = ResourceType.Parse("shelf");

    /// <summary>
    /// The host's kind of thing kept on a shelf.
    /// </summary>
    public static ResourceType BinderType { get; } = ResourceType.Parse("binder");

    /// <summary>
    /// The host's kind of thing filed in a binder.
    /// </summary>
    public static ResourceType SheetType { get; } = ResourceType.Parse("sheet");

    /// <summary>
    /// Reading a shelf.
    /// </summary>
    public static Permission ReadShelf { get; } = Permission.Parse("shelf:read");

    /// <summary>
    /// Reading a binder.
    /// </summary>
    public static Permission ReadBinder { get; } = Permission.Parse("binder:read");

    /// <summary>
    /// Reading a sheet.
    /// </summary>
    public static Permission ReadSheet { get; } = Permission.Parse("sheet:read");

    /// <summary>
    /// The container the library's services are resolved from.
    /// </summary>
    public IServiceProvider Services => _application?.Services
        ?? throw new InvalidOperationException("The host has not started.");

    /// <summary>
    /// Where the host's relying party is registered to be returned to.
    /// </summary>
    public static Uri Destination { get; } = new("https://relying.example.test/callback");

    /// <summary>
    /// The client the provider's registry holds for the host's relying party, with the
    /// secret it presents.
    /// </summary>
    public ConformanceClient Registered => new(RelyingParty, _secret, Destination);

    /// <summary>
    /// What went out by mail.
    /// </summary>
    internal MailTransportInMemory Mail { get; } = new();

    /// <summary>
    /// What went out by SMS.
    /// </summary>
    internal SmsTransportInMemory Sms { get; } = new();

    /// <summary>
    /// What the host declares about its own domain.
    /// </summary>
    /// <returns>The declaration.</returns>
    public static AuthorizationDeclaration Declaration()
    {
        var declaring = new AuthorizationDeclarationBuilder();

        foreach (LawfulBasisDeclaration basis in LawfulBases.Default)
        {
            _ = declaring.LawfulBasis(basis);
        }

        return declaring
            .RetentionFloor(Records, TimeSpan.FromDays(730))
            .Permission(ReadShelf.ToString())
            .Permission(ReadBinder.ToString())
            .Permission(ReadSheet.ToString())
            .Relationship<ShelfKeeper>(
                Keeper,
                ShelfType.ToString(),
                "sample.keepers",
                row => row.Keeper,
                "keeper",
                row => row.ShelfId,
                "shelf_id")
            .Relationship<BinderSteward>(
                Steward,
                BinderType.ToString(),
                "sample.stewards",
                row => row.Steward,
                "steward",
                row => row.BinderId,
                "binder_id")
            .Resource<Shelf>(ShelfType.ToString(), type => type
                .BelongsToOrganization()
                .Derivation(Keeper, Keeper, materialised: true)
                .Purpose(Keeping, "contractual-obligation", data: [Records], subjects: [Members]))
            .Resource<Binder>(BinderType.ToString(), type => type
                .ContainedIn(ShelfType.ToString())
                .Derivation(Steward, Steward)
                .Purpose(Keeping, "contractual-obligation", data: [Records], subjects: [Members]))
            .Resource<Sheet>(SheetType.ToString(), type => type
                .ContainedIn(BinderType.ToString())
                .Purpose(Keeping, "contractual-obligation", data: [Records], subjects: [Members]))
            .Build();
    }

    /// <summary>
    /// A client whose requests the host's web server takes.
    /// </summary>
    /// <returns>The client.</returns>
    public HttpClient Client() => _server.Client(Origin);

    /// <summary>
    /// Opens a connection to the deployment's database, as the host's own code opens
    /// one.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The open connection.</returns>
    public async ValueTask<DbConnection> ConnectAsync(CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(_database.ConnectionString);

        await connection.OpenAsync(cancellationToken);

        return connection;
    }

    /// <summary>
    /// Opens the host's own context.
    /// </summary>
    /// <returns>The context.</returns>
    internal SampleContext Context() =>
        new(new DbContextOptionsBuilder<SampleContext>().UseNpgsql(_database.ConnectionString).Options);

    /// <summary>
    /// Opens the host's context with an entity mapped that nothing declares.
    /// </summary>
    /// <returns>The context.</returns>
    internal StrayContext Stray() =>
        new(new DbContextOptionsBuilder<StrayContext>().UseNpgsql(_database.ConnectionString).Options);

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        await _database.InitializeAsync();

        // OPS-BOOT-001: the operator stands the deployment up once, naming the values
        // that name it, before the application first starts.
        await RunAsync(Bootstrapped(), Keys(), CancellationToken.None);

        await using (DbConnection connection = await ConnectAsync(CancellationToken.None))
        {
            // The host's own tables, with the index each fact a role is derived from
            // is read through (AUTHZ-DERIVE-004).
            _ = await connection.ExecuteAsync(
                """
                CREATE SCHEMA sample;
                CREATE TABLE sample.shelves (
                    id text PRIMARY KEY,
                    organization uuid NOT NULL);
                CREATE TABLE sample.binders (
                    id text PRIMARY KEY,
                    shelf_id text NOT NULL REFERENCES sample.shelves (id));
                CREATE TABLE sample.sheets (
                    id text PRIMARY KEY,
                    binder_id text NOT NULL REFERENCES sample.binders (id));
                CREATE TABLE sample.keepers (
                    shelf_id text NOT NULL REFERENCES sample.shelves (id),
                    keeper uuid NOT NULL,
                    PRIMARY KEY (shelf_id, keeper));
                CREATE INDEX ix_keepers_keeper ON sample.keepers (keeper);
                CREATE TABLE sample.stewards (
                    binder_id text NOT NULL REFERENCES sample.binders (id),
                    steward uuid NOT NULL,
                    PRIMARY KEY (binder_id, steward));
                CREATE INDEX ix_stewards_steward ON sample.stewards (steward);
                """);
        }

        // AUTH-OIDC-001: the relying party's client is registered from the server.
        JsonObject presenting = Keys();

        presenting["clientSecret"] = Convert.ToBase64String(_secret);

        await RunAsync(
            [
                "register-client",
                "--client", RelyingParty,
                "--name", "Sample relying party",
                "--kind", "protocol",
                "--redirect", Destination.AbsoluteUri,
                "--scopes", "openid",
            ],
            presenting,
            CancellationToken.None);

        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();

        builder.Logging.ClearProviders();

        builder.Services.AddSingleton<IServer>(_server);
        builder.Services.AddSingleton<IMailTransport>(Mail);
        builder.Services.AddSingleton<ISmsTransport>(Sms);

        // LIB-HOST-001: the frontend's pages, the host's sign-in screen and the
        // provider, and which client of the provider this application is.
        builder.Services.AddSingleton(new PasskeyAddresses(
            new Uri(Origin, "/account/password").AbsoluteUri,
            new Uri(Origin, "/account/passkeys/new").AbsoluteUri,
            new Uri(Origin, "/account/passkeys").AbsoluteUri));
        builder.Services.AddSingleton(new AuthenticationAddresses(
            new Uri(Origin, "/signin").AbsoluteUri,
            Issuer.AbsoluteUri));
        builder.Services.AddSingleton(new SignOnClient(Application));

        builder.Services.AddJanus(
            _database.ConnectionString,
            new KeyEncryptionKeys(1, new Dictionary<int, ReadOnlyMemory<byte>> { [1] = _encryption }),
            new FingerprintKeys(1, new Dictionary<int, ReadOnlyMemory<byte>> { [1] = _fingerprint }),
            _signOn,
            Encoding.UTF8.GetBytes(Maintenance()),
            Declaration(),
            ApplicationKind.Public);

        _application = builder.Build();

        // LIB-HOST-003: the library under the host's own prefix, the two documents of
        // REG-PM-001 at the site's root.
        _ = ((IApplicationBuilder)_application).Map(new PathString(Prefix), Mounted);
        _ = ((IApplicationBuilder)_application).UseRouting();
        _ = _application.MapIdentityWellKnown();
        _ = ((IApplicationBuilder)_application).UseEndpoints(_ => { });

        await _application.StartAsync(CancellationToken.None);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_application is not null)
        {
            await _application.StopAsync(CancellationToken.None);
            await _application.DisposeAsync();
        }

        _server.Dispose();

        await _database.DisposeAsync();

        CryptographicOperations.ZeroMemory(_encryption);
        CryptographicOperations.ZeroMemory(_fingerprint);
        CryptographicOperations.ZeroMemory(_signOn);
        CryptographicOperations.ZeroMemory(_secret);

        GC.SuppressFinalize(this);
    }

    // BFF-ORDER-001: the machine profile, then the browser profile, then the endpoints.
    private static void Mounted(IApplicationBuilder mount)
    {
        _ = mount.UseRouting();
        _ = mount.UseMachineProfile();
        _ = mount.UseBrowserProfile();
        _ = mount.UseEndpoints(endpoints => endpoints.MapIdentityEndpoints());
    }

    // LIB-HOST-001: the values that name the deployment, each as its key admits it.
    private static List<string> Bootstrapped()
    {
        List<string> arguments =
        [
            "bootstrap",
            "--organization", "Sample administration",
            "--email", "administrator@sample.example.test",
            "--phone", "+201000000003",
            "--dateofbirth", "1990-01-01",
        ];

        foreach ((ConfigurationKey key, string value) in new Dictionary<ConfigurationKey, string>
        {
            [Settings.WebAuthnOrigins.Key] = Settings.WebAuthnOrigins.Write([Origin.GetLeftPart(UriPartial.Authority)]),
            [Settings.HostingLocation.Key] = Settings.HostingLocation.Write(HostingLocation.Inside),
            [Settings.HostingEnvironment.Key] = Settings.HostingEnvironment.Write("a rented virtual machine"),
            [Settings.AlertingEmailDestinations.Key] = Settings.AlertingEmailDestinations.Write(["operator@sample.example.test"]),
            [Settings.AlertingSmsDestinations.Key] = Settings.AlertingSmsDestinations.Write(["+201000000001"]),
            [Settings.AlertingOwnerEmail.Key] = Settings.AlertingOwnerEmail.Write("owner@sample.example.test"),
            [Settings.AlertingOwnerSms.Key] = Settings.AlertingOwnerSms.Write("+201000000002"),
            [Settings.AbuseSmsBalanceFloor.Key] = Settings.AbuseSmsBalanceFloor.Write(100m),
            [Settings.LegalGoverningLanguage.Key] = Settings.LegalGoverningLanguage.Write("en"),
            [Settings.PrivacyCalendarTimeZone.Key] = Settings.PrivacyCalendarTimeZone.Write("UTC"),
            [Settings.NotificationLanguages.Key] = Settings.NotificationLanguages.Write(["en"]),
            [Settings.NotificationEmailSendingDomain.Key] = Settings.NotificationEmailSendingDomain.Write("mail.sample.example.test"),
        })
        {
            arguments.AddRange(["--" + key, value]);
        }

        return arguments;
    }

    // Where the command-line application is started from: the host the tests run
    // under where the platform names it, and the one on the path otherwise.
    private static string Dotnet() =>
        Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } named ? named : "dotnet";

    // The command-line application as its own build laid it out, with the dependencies
    // resolved for it alone.
    private static string CommandLineApplication() =>
        Path.Combine(
            typeof(SampleHost).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .Single(attribute => attribute.Key == "CommandLineApplicationDirectory").Value!,
            "Janus.Cli.dll");

    // OPS-MIG-003a: the credential the scheduled maintenance runs under, holding the
    // maintenance role's rights and no path to the application's.
    private string Maintenance() =>
        new NpgsqlConnectionStringBuilder(_database.ConnectionString) { Options = "-c role=identity_maintenance" }
            .ConnectionString;

    // The document the operator pipes from the secrets manager.
    private JsonObject Keys() =>
        new()
        {
            ["connection"] = _database.ConnectionString,
            ["keyEncryptionKeys"] = new JsonObject
            {
                ["current"] = 1,
                ["versions"] = new JsonObject { ["1"] = Convert.ToBase64String(_encryption) },
            },
            ["fingerprintKeys"] = new JsonObject
            {
                ["current"] = 1,
                ["versions"] = new JsonObject { ["1"] = Convert.ToBase64String(_fingerprint) },
            },
        };

    // One run of the command-line application at the server, the key document piped
    // to it.
    private static async Task RunAsync(
        List<string> arguments,
        JsonObject keys,
        CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(Dotnet())
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        start.ArgumentList.Add("exec");
        start.ArgumentList.Add(CommandLineApplication());

        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process run = Process.Start(start)
            ?? throw new InvalidOperationException("The command-line application did not start.");

        Task<string> output = run.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> error = run.StandardError.ReadToEndAsync(cancellationToken);

        await run.StandardInput.WriteAsync(keys.ToJsonString());
        run.StandardInput.Close();

        await run.WaitForExitAsync(cancellationToken);

        _ = await output;

        if (run.ExitCode != 0)
        {
            throw new InvalidOperationException(arguments[0] + " was refused: " + await error);
        }
    }
}
