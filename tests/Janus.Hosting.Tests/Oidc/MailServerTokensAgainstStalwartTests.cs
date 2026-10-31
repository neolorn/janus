using System;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Hosting.Tests.Mailboxes;
using Xunit;

namespace Janus.Hosting.Tests.Oidc;

/// <summary>
/// The provider's access tokens at a real mail server of the release INT-MAIL-001 names,
/// configured as INT-MAIL-004 states against the provider of a test host listening on a
/// real port: the server reads the provider's discovery document and key set itself and
/// decides each token by its own check (INT-MAIL-004, D-224, D-226).
/// </summary>
/// <param name="pair">The provider and the mail server, one pair for the class.</param>
[Trait("kind", "integration")]
public sealed class MailServerTokensAgainstStalwartTests(ProviderAndMailServer pair) : IClassFixture<ProviderAndMailServer>
{
    // The criteria the server decides against the provider (INT-MAIL-004 AC4).
    private static readonly string[] Decided = ["INT_MAIL_004_AC1_", "INT_MAIL_004_AC2_", "INT_MAIL_004_AC3_"];

    /// <summary>
    /// INT-MAIL-004 AC1: the server accepts a person's access token issued to the client
    /// it is configured to accept and carrying the mail scope, having read the provider's
    /// key set and nothing that introspects.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_MAIL_004_AC1_TheServerAcceptsATokenOfTheClientItAcceptsCarryingTheMailScopeAsync()
    {
        string token = await pair.TokenAsync(ProviderAndMailServer.Accepted);
        JsonElement claims = MailServerAdapter.Claims(token);

        HttpStatusCode answered = await pair.Server.PresentedAsync(token);

        Assert.Equal(ProviderAndMailServer.Accepted, claims.GetProperty("aud").GetString());
        Assert.Contains(ProviderAndMailServer.MailScope, claims.GetProperty("scope").GetString()!.Split(' '));
        Assert.Equal(HttpStatusCode.OK, answered);
        Assert.Contains("/oidc/jwks", pair.Listener.Asked);
        Assert.DoesNotContain(pair.Listener.Asked, path => path.Contains("introspect", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// INT-MAIL-004 AC2: the discovery document the server reads at the provider's issuer
    /// over its real port advertises no introspection endpoint, and states the issuer the
    /// server is configured with.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_MAIL_004_AC2_TheDocumentTheServerReadsAdvertisesNoIntrospectionAsync()
    {
        JsonElement document = await pair.DocumentAsync();

        Assert.Equal(pair.Issuer, document.GetProperty("issuer").GetString());
        Assert.False(document.TryGetProperty("introspection_endpoint", out _));
        Assert.DoesNotContain(
            document.EnumerateObject(),
            entry => entry.Name.Contains("introspection", StringComparison.Ordinal));
    }

    /// <summary>
    /// INT-MAIL-004 AC3: a person's access token issued to a client the server is not
    /// configured to accept is refused, though it carries the mail scope and the
    /// provider's signature.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_MAIL_004_AC3_ATokenIssuedToAnotherClientIsRefusedByTheServerAsync()
    {
        string token = await pair.TokenAsync(ProviderAndMailServer.Another);
        JsonElement claims = MailServerAdapter.Claims(token);

        HttpStatusCode answered = await pair.Server.PresentedAsync(token);

        Assert.Equal(ProviderAndMailServer.Another, claims.GetProperty("aud").GetString());
        Assert.Contains(ProviderAndMailServer.MailScope, claims.GetProperty("scope").GetString()!.Split(' '));
        Assert.Equal(HttpStatusCode.Unauthorized, answered);
    }

    /// <summary>
    /// INT-MAIL-004 AC4: criteria 1 to 3 each have their test here, against a server whose
    /// image is named by digest and which started as release 1.0, holding the directory
    /// INT-MAIL-004 states as it reads it back and authenticating by it, the directory
    /// naming the issuer of a provider on a real port.
    /// </summary>
    [Fact]
    public void INT_MAIL_004_AC4_TheCriteriaAreDecidedByTheReleaseConfiguredAsTheItemStates()
    {
        string[] tested =
        [
            .. typeof(MailServerTokensAgainstStalwartTests)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(method => method.GetCustomAttributes<FactAttribute>(inherit: true).Any())
                .Select(method => method.Name),
        ];

        JsonElement directory = Assert.NotNull(pair.Server.Directory);

        Assert.StartsWith("stalwartlabs/stalwart@sha256:", StalwartServer.Image, StringComparison.Ordinal);
        Assert.StartsWith("1.0.", pair.Server.Release, StringComparison.Ordinal);
        Assert.All(
            Decided,
            criterion => Assert.Contains(tested, name => name.StartsWith(criterion, StringComparison.Ordinal)));
        Assert.Equal(new Uri(pair.Issuer).Port, pair.Port);
        Assert.Equal(pair.Issuer, directory.GetProperty("issuerUrl").GetString());
        Assert.Equal(
            [ProviderAndMailServer.Accepted],
            directory.GetProperty("acceptedAudiences").EnumerateObject()
                .Where(audience => audience.Value.GetBoolean())
                .Select(audience => audience.Name));
        Assert.False(directory.GetProperty("skipAudienceValidation").GetBoolean());
        Assert.Equal("none", directory.GetProperty("opaqueTokenValidation").GetString());
        Assert.Equal("reject", directory.GetProperty("unscopedTokens").GetString());
        Assert.Equal("email", directory.GetProperty("claimUsername").GetString());
        Assert.Equal("Oidc", directory.GetProperty("@type").GetString());
        Assert.Equal(directory.GetProperty("id").GetString(), pair.Server.AuthenticatingBy);
    }
}
