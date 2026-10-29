using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Volumes;
using Xunit;

namespace Janus.Hosting.Tests.Mailboxes;

/// <summary>
/// A real mail server of the release the shipped adapter is written for, its image named
/// by digest, configured without its setup wizard the way its documentation for that
/// release states: the data store's settings written as its configuration file, the rest
/// loaded through its JMAP management interface in recovery mode, and the server started
/// again in normal mode over the same data.
/// </summary>
/// <remarks>
/// Implements CONV-TEST-002, and INT-MAIL-001 AC7 for criteria 3 to 6 (D-224). Every
/// secret is drawn when the server is made and lives no longer than it: the recovery
/// administrator, the server's certificate and its key, and the management key the
/// server generates. The server is reached over TLS trusting that certificate alone, its
/// name checked, as a deployment reaches it. One server per test class.
/// </remarks>
public sealed class StalwartServer : IAsyncLifetime
{
    /// <summary>
    /// The image of release 1.0.1, the newest of 1.0 when round H3 began, by its digest
    /// on the registry the release publishes it to.
    /// </summary>
    public const string Image =
        "stalwartlabs/stalwart@sha256:b9da4df8b4efc06bc926dd6e990bdeb0ad7b9261a7c3893d74b0461a00bb389b";

    /// <summary>
    /// The domain every mailbox here is in.
    /// </summary>
    public const string Domain = "example.test";

    private const int Https = 443;

    private const int Recovery = 8080;

    private const string Administrator = "admin";

    private const string ConfigurationFile = "/etc/stalwart/config.json";

    private const string DataDirectory = "/var/lib/stalwart";

    private static readonly byte[] DataStore = Encoding.UTF8.GetBytes("""{"@type":"RocksDb","path":"/var/lib/stalwart/"}""");

    // The release the server names as it starts.
    private static readonly Regex Version = new(
        @"version(?:\x1b\[[0-9;]*m)?\s*=\s*""(?<release>[0-9]+\.[0-9]+\.[0-9]+)""",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    private readonly IVolume _data = new VolumeBuilder()
        .WithName("janus-mailserver-" + Guid.NewGuid().ToString("N"))
        .Build();

    private IContainer? _server;

    private X509Certificate2? _certificate;

    private byte[] _managementKey = [];

    /// <summary>
    /// Where the server's HTTPS listener is reached, the value of
    /// <c>integration.mailserver.endpoint</c>.
    /// </summary>
    public Uri Endpoint { get; private set; } = new("https://localhost/");

    /// <summary>
    /// The release the server named as it started.
    /// </summary>
    public string Release { get; private set; } = string.Empty;

    /// <summary>
    /// The identifier of the domain every mailbox here is in.
    /// </summary>
    public string DomainId { get; private set; } = string.Empty;

    /// <summary>
    /// The management key the server generated, as the host's secret source holds it.
    /// </summary>
    /// <returns>A copy of the key.</returns>
    public byte[] ManagementKey() => (byte[])_managementKey.Clone();

    /// <summary>
    /// A connection that trusts the server's certificate and nothing else, every other
    /// check of it made.
    /// </summary>
    /// <returns>The handler.</returns>
    public HttpMessageHandler Trusting()
    {
        X509Certificate2 trusted = _certificate
            ?? throw new InvalidOperationException("The server has not started.");
        var chain = new X509ChainPolicy { TrustMode = X509ChainTrustMode.CustomRootTrust };

        chain.CustomTrustStore.Add(trusted);

        return new SocketsHttpHandler
        {
            SslOptions = new SslClientAuthenticationOptions { CertificateChainPolicy = chain },
        };
    }

    /// <summary>
    /// Makes one JMAP request with the management key, as an operator's tool does, for
    /// what a test sets up or reads at the server around the adapter.
    /// </summary>
    /// <param name="calls">The method calls, each named by its position.</param>
    /// <returns>The arguments of each answer, in the order of the calls.</returns>
    public async Task<IReadOnlyList<JsonElement>> CallAsync(params JsonNode[] calls)
    {
        using HttpMessageHandler trusting = Trusting();
        using var requests = new HttpClient(trusting, disposeHandler: false);

        return await CalledAsync(
            requests,
            new Uri(Endpoint, "jmap"),
            new AuthenticationHeaderValue("Bearer", Encoding.UTF8.GetString(_managementKey)),
            calls);
    }

    /// <summary>
    /// The account under a name in the domain, as the server holds it, or nothing.
    /// </summary>
    /// <param name="name">The local part.</param>
    /// <returns>The account's name, description and permissions, with its identifier.</returns>
    public async Task<JsonElement?> AccountAsync(string name)
    {
        IReadOnlyList<JsonElement> answered = await CallAsync(
            Call("x:Account/query", new JsonObject
            {
                ["filter"] = new JsonObject { ["name"] = name, ["domainId"] = DomainId },
            }),
            Call("x:Account/get", new JsonObject
            {
                ["#ids"] = new JsonObject { ["resultOf"] = "0", ["name"] = "x:Account/query", ["path"] = "/ids" },
                ["properties"] = new JsonArray("name", "description", "permissions"),
            }));

        JsonElement? found = null;

        foreach (JsonElement account in answered[1].GetProperty("list").EnumerateArray())
        {
            if (account.GetProperty("name").GetString() == name)
            {
                Assert.Null(found);
                found = account;
            }
        }

        return found;
    }

    /// <summary>
    /// Creates a user account in the domain at the server, as an operator does by hand.
    /// </summary>
    /// <param name="name">The local part.</param>
    /// <param name="description">What the account's description carries, if anything.</param>
    /// <param name="permissions">How the account holds its permissions.</param>
    /// <returns>The account's identifier.</returns>
    public async Task<string> CreateAsync(string name, string? description, JsonObject permissions)
    {
        IReadOnlyList<JsonElement> answered = await CallAsync(Call("x:Account/set", new JsonObject
        {
            ["create"] = new JsonObject
            {
                ["account"] = new JsonObject
                {
                    ["@type"] = "User",
                    ["name"] = name,
                    ["domainId"] = DomainId,
                    ["description"] = description,
                    ["credentials"] = new JsonObject(),
                    ["permissions"] = permissions,
                },
            },
        }));

        return Created(answered[0], "account");
    }

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string password = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

        await _data.CreateAsync(cancellationToken);

        // The documentation's declarative deployment: the data store's settings in the
        // configuration file, and the server started in recovery mode, where only the
        // management interface runs, with a recovery administrator.
        IContainer recovering = Started()
            .WithEnvironment("STALWART_RECOVERY_MODE", "1")
            .WithEnvironment("STALWART_RECOVERY_ADMIN", Administrator + ":" + password)
            .WithPortBinding(Recovery, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged(Listening("http-recovery")))
            .Build();

        await using (recovering)
        {
            await recovering.StartAsync(cancellationToken);

            (string output, string errors) = await recovering.GetLogsAsync(ct: cancellationToken);

            Release = Version.Match(output + errors) is { Success: true } named
                ? named.Groups["release"].Value
                : throw new InvalidOperationException("The server named no release as it started.");

            _certificate = Certificate(recovering.Hostname, out string privateKey);

            await ConfiguredAsync(
                new Uri(string.Create(
                    CultureInfo.InvariantCulture,
                    $"http://{recovering.Hostname}:{recovering.GetMappedPublicPort(Recovery)}/jmap")),
                new AuthenticationHeaderValue(
                    "Basic",
                    Convert.ToBase64String(Encoding.UTF8.GetBytes(Administrator + ":" + password))),
                privateKey);

            await recovering.StopAsync(cancellationToken);
        }

        // Started again without the recovery variables, it serves the listeners its
        // configuration holds, the certificate's on HTTPS among them.
        _server = Started()
            .WithPortBinding(Https, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged(Listening("https")))
            .Build();

        await _server.StartAsync(cancellationToken);

        Endpoint = new Uri(string.Create(
            CultureInfo.InvariantCulture,
            $"https://{_server.Hostname}:{_server.GetMappedPublicPort(Https)}/"));
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        CryptographicOperations.ZeroMemory(_managementKey);

        if (_server is not null)
        {
            await _server.DisposeAsync();
        }

        await _data.DisposeAsync();
        _certificate?.Dispose();
    }

    /// <summary>
    /// One method call of a request, named by its position among the calls.
    /// </summary>
    /// <param name="method">The method.</param>
    /// <param name="arguments">Its arguments.</param>
    /// <returns>The call, to be named as it is placed.</returns>
    public static JsonNode Call(string method, JsonObject arguments) => new JsonArray(method, arguments);

    // The line the server logs once a listener is bound, coloured or not.
    private static Regex Listening(string listener) =>
        new(
            @"network\.listen-start\).*""" + Regex.Escape(listener) + @"""",
            RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(5));

    // The image with the data store's settings as its configuration file, over the data
    // the server keeps across its two starts.
    private ContainerBuilder Started() => new ContainerBuilder(Image)
        .WithResourceMapping(DataStore, ConfigurationFile)
        .WithVolumeMount(_data, DataDirectory);

    // A self-signed certificate for the names the server is reached by, the key used for
    // nothing but this server.
    private static X509Certificate2 Certificate(string hostname, out string privateKey)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256);
        var names = new SubjectAlternativeNameBuilder();

        names.AddDnsName("localhost");
        names.AddIpAddress(IPAddress.Loopback);

        if (IPAddress.TryParse(hostname, out IPAddress? address))
        {
            if (!IPAddress.IsLoopback(address))
            {
                names.AddIpAddress(address);
            }
        }
        else if (hostname != "localhost")
        {
            names.AddDnsName(hostname);
        }

        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            [new Oid("1.3.6.1.5.5.7.3.1")],
            critical: false));
        request.CertificateExtensions.Add(names.Build());

        DateTimeOffset now = DateTimeOffset.UtcNow;

        using X509Certificate2 issued = request.CreateSelfSigned(now.AddMinutes(-5), now.AddDays(1));

        privateKey = key.ExportPkcs8PrivateKeyPem();

        return X509CertificateLoader.LoadCertificate(issued.RawData);
    }

    private static string Created(JsonElement answer, string key) =>
        answer.TryGetProperty("created", out JsonElement created)
        && created.TryGetProperty(key, out JsonElement entry)
        && entry.TryGetProperty("id", out JsonElement id)
            ? id.GetString()!
            : throw new InvalidOperationException("The server created nothing: " + answer.GetRawText());

    private static async Task<IReadOnlyList<JsonElement>> CalledAsync(
        HttpClient requests,
        Uri endpoint,
        AuthenticationHeaderValue credential,
        JsonNode[] calls)
    {
        var methodCalls = new JsonArray();

        for (int index = 0; index < calls.Length; index++)
        {
            JsonArray call = calls[index].AsArray();

            methodCalls.Add(new JsonArray(
                (string)call[0]!,
                call[1]!.DeepClone(),
                index.ToString(CultureInfo.InvariantCulture)));
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(
                new JsonObject
                {
                    ["using"] = new JsonArray("urn:ietf:params:jmap:core", "urn:stalwart:jmap"),
                    ["methodCalls"] = methodCalls,
                }.ToJsonString(),
                Encoding.UTF8,
                MediaTypeNames.Application.Json),
        };

        message.Headers.Authorization = credential;

        using HttpResponseMessage response = await requests.SendAsync(message, TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("The server answered " + (int)response.StatusCode + ": " + body);
        }

        using var parsed = JsonDocument.Parse(body);
        var answered = new List<JsonElement>(calls.Length);

        foreach (JsonElement answer in parsed.RootElement.GetProperty("methodResponses").EnumerateArray())
        {
            if (answer[0].GetString() == "error")
            {
                throw new InvalidOperationException("The server refused a call: " + answer.GetRawText());
            }

            answered.Add(answer[1].Clone());
        }

        return answered;
    }

    // What the documentation's plan loads before the server goes live: its certificate,
    // the domain, the server's identity, a log the start can be waited on by, and an
    // administrator whose key is the deployment's management key.
    private async Task ConfiguredAsync(Uri endpoint, AuthenticationHeaderValue administrator, string privateKey)
    {
        using var requests = new HttpClient();

        IReadOnlyList<JsonElement> configured = await CalledAsync(
            requests,
            endpoint,
            administrator,
            [
                Call("x:Certificate/set", new JsonObject
                {
                    ["create"] = new JsonObject
                    {
                        ["certificate"] = new JsonObject
                        {
                            ["certificate"] = new JsonObject { ["@type"] = "Text", ["value"] = _certificate!.ExportCertificatePem() },
                            ["privateKey"] = new JsonObject { ["@type"] = "Text", ["secret"] = privateKey },
                        },
                    },
                }),
                Call("x:Domain/set", new JsonObject
                {
                    ["create"] = new JsonObject { ["domain"] = new JsonObject { ["name"] = Domain } },
                }),
                Call("x:SystemSettings/set", new JsonObject
                {
                    ["update"] = new JsonObject
                    {
                        ["singleton"] = new JsonObject
                        {
                            ["defaultHostname"] = "mail." + Domain,
                            ["defaultDomainId"] = "#domain",
                            ["defaultCertificateId"] = "#certificate",
                        },
                    },
                }),
                Call("x:Tracer/set", new JsonObject
                {
                    ["create"] = new JsonObject
                    {
                        ["log"] = new JsonObject { ["@type"] = "Stdout", ["buffered"] = false, ["level"] = "info" },
                    },
                }),
            ]);

        DomainId = Created(configured[1], "domain");

        if (!configured[2].TryGetProperty("updated", out JsonElement updated) || !updated.TryGetProperty("singleton", out _))
        {
            throw new InvalidOperationException("The server kept its settings: " + configured[2].GetRawText());
        }

        _ = Created(configured[0], "certificate");
        _ = Created(configured[3], "log");

        IReadOnlyList<JsonElement> provisioning = await CalledAsync(
            requests,
            endpoint,
            administrator,
            [
                Call("x:Account/set", new JsonObject
                {
                    ["create"] = new JsonObject
                    {
                        ["operator"] = new JsonObject
                        {
                            ["@type"] = "User",
                            ["name"] = "provisioning",
                            ["domainId"] = DomainId,
                            ["credentials"] = new JsonObject(),
                            ["roles"] = new JsonObject { ["@type"] = "Admin" },
                        },
                    },
                }),
            ]);

        IReadOnlyList<JsonElement> keyed = await CalledAsync(
            requests,
            endpoint,
            administrator,
            [
                Call("x:ApiKey/set", new JsonObject
                {
                    ["accountId"] = Created(provisioning[0], "operator"),
                    ["create"] = new JsonObject
                    {
                        ["key"] = new JsonObject
                        {
                            ["description"] = "management",
                            ["permissions"] = new JsonObject { ["@type"] = "Inherit" },
                            ["allowedIps"] = new JsonObject(),
                        },
                    },
                }),
            ]);

        _managementKey = Encoding.UTF8.GetBytes(
            keyed[0].GetProperty("created").GetProperty("key").GetProperty("secret").GetString()
            ?? throw new InvalidOperationException("The server generated no management key."));
    }
}
