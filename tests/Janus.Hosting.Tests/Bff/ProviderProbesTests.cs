using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Janus.Hosting.Tests.Bff;

/// <summary>
/// The conformance suite's provider probes as this application's half of the sign-on
/// makes them: as its own client, on its own connection, with the secret the registry
/// holds at each request (LIB-TEST-001 AC4, BFF-SESS-006, D-172).
/// </summary>
[Trait("kind", "unit")]
public sealed class ProviderProbesTests
{
    private const string Client = "this-application";

    private const string Return = "https://identity.example.test/auth/signon/return";

    private const string Secret = "a-secret-the-deployment-set";

    /// <summary>
    /// LIB-TEST-001 AC4: the probes are asked at the endpoints the document names, as the
    /// application's own client, on the sign-on's connection; each that authenticates
    /// presents the registry's secret, the one that asks about a client that does not
    /// authenticate presents none, and the provider refuses them all.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_TEST_001_AC4_TheProbesAskAsTheSignOnClientWithTheRegistrysSecretAsync()
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        IReadOnlyList<Error> findings = await ProbedAsync(deployment);

        Dictionary<string, StringValues>[] carried = [.. deployment.Provider.Carried.Skip(1).Select(QueryHelpers.ParseQuery)];

        Assert.Empty(findings.Select(Described));
        Assert.Equal("/.well-known/openid-configuration", deployment.Provider.Asked[0].AbsolutePath);
        Assert.Equal(15, carried.Length);
        Assert.All(carried, fields => Assert.Equal(Client, fields["client_id"].ToString()));
        Assert.All(carried[..^1], fields => Assert.Equal(Secret, fields["client_secret"].ToString()));
        Assert.False(carried[^1].ContainsKey("client_secret"));
        Assert.Equal(["/oidc/par", "/oidc/token"], deployment.Provider.Asked.Skip(1).Select(asked => asked.AbsolutePath).Distinct());
        Assert.Equal(Client, Assert.Single(await deployment.Clients.AllAsync(TestContext.Current.CancellationToken)).ClientId);
    }

    /// <summary>
    /// LIB-TEST-001 AC4, OPS-SEC-002: nothing of the secret is kept between requests, so
    /// a secret replaced since the last run is the one the next run presents.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_TEST_001_AC4_ASecretReplacedSinceTheLastRunIsTheOnePresentedAsync()
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        _ = await ProbedAsync(deployment);

        int asked = deployment.Provider.Carried.Count;

        deployment.Clock.Advance(
            (await deployment.Configuration.ReadAsync(Settings.TokenSigningRotation, TestContext.Current.CancellationToken))
                .Match(read => read, error => throw new InvalidOperationException(error.Code.ToString())));

        IReadOnlyList<Error> findings = await ProbedAsync(deployment);

        string[] presented =
        [
            .. deployment.Provider.Carried
                .Skip(asked)
                .Select(QueryHelpers.ParseQuery)
                .Where(fields => fields.ContainsKey("client_secret"))
                .Select(fields => fields["client_secret"].ToString())
                .Distinct(),
        ];

        Assert.Empty(findings.Select(Described));
        Assert.Equal(1, deployment.Clients.Replacements);
        Assert.Equal(
            Encoding.UTF8.GetString(deployment.Clients.Registered.Single().Secret),
            Assert.Single(presented));
        Assert.NotEqual(Secret, presented[0]);
    }

    /// <summary>
    /// LIB-TEST-001 AC4: where this application's client is not registered, the probes are
    /// not made and the refusal is handed back, since no client is registered for them.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_TEST_001_AC4_AnUnregisteredSignOnClientAsksNothingAsync()
    {
        await using var deployment = new Deployment();
        await using AsyncServiceScope scope = deployment.Scope();

        Result<IReadOnlyList<Error>> probed = await scope.ServiceProvider.GetRequiredService<IProviderProbes>()
            .RunAsync(Asking(), TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.SystemFault, probed.Match<ErrorCode?>(_ => null, error => error.Code));
        Assert.Empty(deployment.Provider.Asked);
    }

    // A finding as its details read, so one reported is named in the failure.
    private static string Described(Error finding) =>
        string.Join(", ", finding.Details.Select(detail => detail.Key + "=" + detail.Value.GetRawText()));

    private static async Task RegisteredAsync(Deployment deployment) =>
        await deployment.Clients.AddAsync(
            new OidcClient(
                Client,
                Client,
                OidcClientKind.BrowserApplication,
                Return,
                ["openid"]),
            Encoding.UTF8.GetBytes(Secret),
            deployment.Clock.GetUtcNow(),
            TestContext.Current.CancellationToken);

    // Who asks: the person running the suite, whom the probes meet no gate for and read
    // nothing of.
    private static AccessContext Asking()
    {
        using var randomness = RandomNumberGenerator.Create();

        return AccessContext.Of(SubjectId.New(randomness));
    }

    private static async Task<IReadOnlyList<Error>> ProbedAsync(Deployment deployment)
    {
        await using AsyncServiceScope scope = deployment.Scope();

        return (await scope.ServiceProvider.GetRequiredService<IProviderProbes>()
                .RunAsync(Asking(), TestContext.Current.CancellationToken))
            .Match(findings => findings, error => throw new InvalidOperationException(error.Code.ToString()));
    }
}
