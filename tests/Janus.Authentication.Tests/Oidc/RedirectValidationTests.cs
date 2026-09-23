using System.Threading.Tasks;
using Janus.Authentication.Oidc;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Oidc;

/// <summary>
/// What the library will forward a browser to, read at startup: the origins the
/// registry holds and the client a destination falls back to (API-REDIR-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class RedirectValidationTests
{
    private const string Application = "web";

    private const string Registered = "https://app.example.test/signin/callback";

    private readonly OidcClientStoreInMemory _clients = new();
    private readonly ConfigurationInMemory _configuration = new();

    /// <summary>
    /// API-REDIR-001 AC3: the list is the origins of the registered clients' return
    /// destinations, and a registry holding nothing but absolute ones starts.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task API_REDIR_001_AC3_ARegistryOfAbsoluteOriginsStartsAsync()
    {
        await RegisterAsync(Application, Registered);
        await RegisterAsync("mail", "https://mail.example.test/oauth", OidcClientKind.Protocol);

        Assert.Null(await RefusalAsync());
    }

    /// <summary>
    /// API-REDIR-001 AC3: an entry that is not an absolute origin stops the
    /// deployment, rather than being carried to the moment a person is forwarded to
    /// it, and the refusal names the client it was read from.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task API_REDIR_001_AC3_AnEntryThatIsNotAnAbsoluteOriginFailsAsync()
    {
        await RegisterAsync(Application, "/signin/callback");

        Error? refused = await RefusalAsync();

        Assert.NotNull(refused);

        Assert.Equal(ErrorCodes.StartupRedirectClient, refused.Code);
        Assert.Equal(Application, refused.Details["client"].GetString());
    }

    /// <summary>
    /// LIB-HOST-001 AC3: the default is a key outside the declarations, so a
    /// deployment that names none starts and falls back to nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_HOST_001_AC3_ADeploymentThatNamesNoDefaultStartsAsync()
    {
        await RegisterAsync(Application, Registered);

        Assert.Null(await RefusalAsync());
    }

    /// <summary>
    /// API-REDIR-001: the default is validated against the registry at startup, so a
    /// key naming a client nothing registered stops the deployment and the refusal
    /// names the key rather than the client.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task API_REDIR_001_ADefaultNamingNoRegisteredClientIsRefusedAsync()
    {
        await RegisterAsync(Application, Registered);
        _configuration.Set(Settings.RedirectDefaultClient, "nobody");

        Error? refused = await RefusalAsync();

        Assert.NotNull(refused);

        Assert.Equal(ErrorCodes.StartupRedirectClient, refused.Code);
        Assert.Equal("redirect.defaultclient", refused.Details["key"].GetString());
    }

    /// <summary>
    /// API-REDIR-001: a protocol client is no place to land a browser, so a default
    /// naming one is refused although the registry holds it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task API_REDIR_001_ADefaultNamingAProtocolClientIsRefusedAsync()
    {
        await RegisterAsync("mail", "https://mail.example.test/oauth", OidcClientKind.Protocol);
        _configuration.Set(Settings.RedirectDefaultClient, "mail");

        Error? refused = await RefusalAsync();

        Assert.NotNull(refused);

        Assert.Equal(ErrorCodes.StartupRedirectClient, refused.Code);
        Assert.Equal("redirect.defaultclient", refused.Details["key"].GetString());
    }

    /// <summary>
    /// API-REDIR-001: the default a deployment named is one of the registry's own
    /// browser applications, which is the arrangement that starts.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task API_REDIR_001_ADefaultNamingARegisteredApplicationStartsAsync()
    {
        await RegisterAsync(Application, Registered);
        _configuration.Set(Settings.RedirectDefaultClient, Application);

        Assert.Null(await RefusalAsync());
    }

    private async Task<Error?> RefusalAsync() =>
        (await new RedirectValidation(_clients, _configuration)
            .ValidateAsync(TestContext.Current.CancellationToken))
        .Match(() => (Error?)null, failure => failure);

    private Task RegisterAsync(
        string clientId,
        string redirect,
        OidcClientKind kind = OidcClientKind.BrowserApplication) =>
        _clients
            .RecordAsync(
                new OidcClient(clientId, clientId, kind, redirect, ["openid"]),
                [1, 2, 3],
                TestContext.Current.CancellationToken)
            .AsTask();
}
