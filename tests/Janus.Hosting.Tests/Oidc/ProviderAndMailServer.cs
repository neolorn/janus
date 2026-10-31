using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;
using Janus.Core;
using Janus.Hosting.Tests.Mailboxes;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Oidc;

/// <summary>
/// The provider of a test host listening on a real port, and a real mail server of the
/// release INT-MAIL-001 names configured as INT-MAIL-004 states against it: an
/// <c>Oidc</c> directory naming the provider's issuer, one client accepted, the
/// provider's certificate trusted by the authority that issued it alone, and a person
/// signed in at the provider (INT-MAIL-004 AC4, D-224, D-226).
/// </summary>
/// <remarks>
/// The deployment's clock reads the time the mail server reads, which checks a token's
/// lifetime by its own. Every key here is drawn when the pair is made and lives no
/// longer than it. One pair per test class.
/// </remarks>
public sealed class ProviderAndMailServer : IAsyncLifetime
{
    /// <summary>
    /// The client the mail server accepts tokens of.
    /// </summary>
    public const string Accepted = "mail-client";

    /// <summary>
    /// A client the mail server does not accept tokens of.
    /// </summary>
    public const string Another = "another-client";

    /// <summary>
    /// The interoperable scope the mail server grants access by.
    /// </summary>
    public const string MailScope = "urn:ietf:params:oauth:scope:mail";

    private const string ProviderHost = "identity.example.test";

    private X509Certificate2? _authority;

    private X509Certificate2? _presented;

    private ProviderListener? _listener;

    private Deployment? _deployment;

    private StalwartServer? _server;

    private Browser? _signedIn;

    /// <summary>
    /// Gets the mail server.
    /// </summary>
    public StalwartServer Server => _server ?? throw new InvalidOperationException("The pair has not started.");

    /// <summary>
    /// Gets the issuer the provider's discovery document states and the mail server is
    /// configured with.
    /// </summary>
    public string Issuer { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the port the provider listens on.
    /// </summary>
    public int Port => Listener.Port;

    /// <summary>
    /// Gets the deployment whose provider listens.
    /// </summary>
    internal Deployment Deployment => _deployment ?? throw new InvalidOperationException("The pair has not started.");

    /// <summary>
    /// Gets the listener the provider is reached at.
    /// </summary>
    internal ProviderListener Listener => _listener ?? throw new InvalidOperationException("The pair has not started.");

    /// <summary>
    /// A person's access token, issued to a client for the mail scope, through the
    /// provider's authorization and exchange as a mail client signing in obtains one.
    /// </summary>
    /// <param name="clientId">The client it is issued to.</param>
    /// <returns>The access token.</returns>
    public async Task<string> TokenAsync(string clientId)
    {
        string authority = Authority();
        var machine = new Machine(Deployment) { Host = authority };
        var browser = new Browser(Deployment) { Host = authority };

        foreach ((string name, string value) in _signedIn!.Cookies)
        {
            browser.Hold(name, value);
        }

        Answer pushed = await machine.PostAsync(
            "/oidc/par",
            RelyingParty.Request(clientId, silent: true, RelyingParty.Destination, "openid email " + MailScope));

        Assert.Equal(StatusCodes.Status201Created, pushed.Status);

        Answer authorized = await browser.SendAsync(
            "GET",
            RelyingParty.Authorization(clientId, pushed.Text("request_uri")),
            origin: null);

        Assert.Equal(StatusCodes.Status302Found, authorized.Status);

        Answer exchanged = await machine.PostAsync(
            "/oidc/token",
            RelyingParty.Code(RelyingParty.Returned(authorized, "code"), clientId));

        Assert.Equal(StatusCodes.Status200OK, exchanged.Status);

        return exchanged.Text("access_token");
    }

    /// <summary>
    /// The provider's discovery document, read over its real port at its issuer, the
    /// provider's certificate trusted by the authority that issued it alone, as the mail
    /// server reads it.
    /// </summary>
    /// <returns>The document.</returns>
    public async Task<JsonElement> DocumentAsync()
    {
        var chain = new X509ChainPolicy
        {
            TrustMode = X509ChainTrustMode.CustomRootTrust,
            // The authority publishes no revocation list; it lives no longer than the pair.
            RevocationMode = X509RevocationMode.NoCheck,
        };

        chain.CustomTrustStore.Add(_authority!);

        using var handler = new SocketsHttpHandler
        {
            SslOptions = new SslClientAuthenticationOptions { CertificateChainPolicy = chain },
            // The provider's host is the machine's, which no resolver here names.
            ConnectCallback = async (_, cancellationToken) =>
            {
                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };

                try
                {
                    await socket.ConnectAsync(IPAddress.Loopback, Port, cancellationToken);

                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            },
        };
        using var requests = new HttpClient(handler);
        string read = await requests.GetStringAsync(
            new Uri(new Uri(Issuer), ".well-known/openid-configuration"),
            TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse(read);

        return document.RootElement.Clone();
    }

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        using var authorityKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        _authority = Authority(authorityKey);
        _presented = Presented(_authority, authorityKey);
        _listener = await ProviderListener.StartedAsync(_presented);

        string provider = new UriBuilder(Uri.UriSchemeHttps, ProviderHost, _listener.Port).Uri.GetLeftPart(UriPartial.Authority);

        _deployment = new Deployment(signIn: new AuthenticationAddresses("https://identity.example.test/signin", provider));
        _deployment.Clock.Advance(DateTimeOffset.UtcNow - _deployment.Clock.GetUtcNow());
        _listener.Serve(_deployment);

        Issuer = (await new Machine(_deployment) { Host = Authority() }
                .GetAsync("/.well-known/openid-configuration", bearer: string.Empty))
            .Text("issuer");

        foreach (string clientId in new[] { Accepted, Another })
        {
            await _deployment.Clients.AddAsync(
                new OidcClient(
                    clientId,
                    clientId,
                    OidcClientKind.Protocol,
                    RelyingParty.Destination,
                    ["openid", "email", MailScope]),
                Encoding.UTF8.GetBytes(RelyingParty.Secret),
                _deployment.Clock.GetUtcNow(),
                TestContext.Current.CancellationToken);
        }

        Flow.Prepare(_deployment);
        _signedIn = await Flow.SignedInAsync(_deployment);

        _server = new StalwartServer(Issuer, _authority, [Accepted]);
        await _server.InitializeAsync();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_server is not null)
        {
            await _server.DisposeAsync();
        }

        if (_listener is not null)
        {
            await _listener.DisposeAsync();
        }

        if (_deployment is not null)
        {
            await _deployment.DisposeAsync();
        }

        _presented?.Dispose();
        _authority?.Dispose();
    }

    // The provider's host and port, as a request addressed to it names them.
    private string Authority() => ProviderHost + ":" + Port.ToString(CultureInfo.InvariantCulture);

    // An authority that issues the provider's certificate and nothing else.
    private static X509Certificate2 Authority(ECDsa key)
    {
        var request = new CertificateRequest("CN=the provider's authority", key, HashAlgorithmName.SHA256);

        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        DateTimeOffset now = DateTimeOffset.UtcNow;

        return request.CreateSelfSigned(now.AddMinutes(-5), now.AddDays(1));
    }

    // The provider's certificate for its host, issued by the authority, with its key, in
    // a form the web server can present on every platform.
    private static X509Certificate2 Presented(X509Certificate2 authority, ECDsa authorityKey)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=" + ProviderHost, key, HashAlgorithmName.SHA256);
        var names = new SubjectAlternativeNameBuilder();

        names.AddDnsName(ProviderHost);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            [new Oid("1.3.6.1.5.5.7.3.1")],
            critical: false));
        request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(authority, true, false));
        request.CertificateExtensions.Add(names.Build());

        DateTimeOffset now = DateTimeOffset.UtcNow;
        byte[] serial = RandomNumberGenerator.GetBytes(16);

        serial[0] &= 0x7F;

        using X509Certificate2 issued = request.Create(
            authority.SubjectName,
            X509SignatureGenerator.CreateForECDsa(authorityKey),
            now.AddMinutes(-5),
            now.AddDays(1),
            serial);
        using X509Certificate2 keyed = issued.CopyWithPrivateKey(key);

        return X509CertificateLoader.LoadPkcs12(keyed.Export(X509ContentType.Pkcs12), password: null);
    }
}
