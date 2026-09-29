using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Janus.Hosting.Tests.Oidc;

/// <summary>
/// A registered client's secret over its life: drawn by the library, replaced at the
/// cadence of the signing keys by the first read that finds it due, taken through the
/// overlap and refused after it, all while the deployment runs (OPS-SEC-002, D-166).
/// </summary>
[Trait("kind", "unit")]
public sealed class ClientSecretLifecycleTests
{
    /// <summary>
    /// OPS-SEC-002 AC1: a secret that has reached the cadence is replaced by the next read
    /// of it, in the running deployment, and the provider takes the new one.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_002_AC1_AClientSecretRotatesAtTheCadenceWithoutRestartAsync()
    {
        await using var deployment = new Deployment();

        await RelyingParty.RegisteredAsync(deployment);

        string before = await CurrentAsync(deployment, TestContext.Current.CancellationToken);

        deployment.Clock.Advance(await SettingAsync(deployment, Settings.TokenSigningRotation) - TimeSpan.FromSeconds(1));

        string due = await CurrentAsync(deployment, TestContext.Current.CancellationToken);

        deployment.Clock.Advance(TimeSpan.FromSeconds(1));

        string after = await CurrentAsync(deployment, TestContext.Current.CancellationToken);

        Assert.Equal(RelyingParty.Secret, before);
        Assert.Equal(before, due);
        Assert.NotEqual(before, after);
        Assert.Equal(43, after.Length);
        Assert.Equal(1, deployment.Clients.Replacements);
        Assert.Equal(after, await CurrentAsync(deployment, TestContext.Current.CancellationToken));
        Assert.Equal(StatusCodes.Status201Created, (await PushedAsync(deployment, after)).Status);
    }

    /// <summary>
    /// OPS-SEC-002 AC2: the replaced secret is taken through the overlap, the access-token
    /// lifetime and five minutes, and refused after it, while the new one is taken
    /// throughout.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_002_AC2_TheReplacedSecretAuthenticatesThroughTheOverlapAndNotAfterAsync()
    {
        await using var deployment = new Deployment();

        await RelyingParty.RegisteredAsync(deployment);

        deployment.Clock.Advance(await SettingAsync(deployment, Settings.TokenSigningRotation));

        string replacing = await CurrentAsync(deployment, TestContext.Current.CancellationToken);
        TimeSpan overlap = await SettingAsync(deployment, Settings.OidcAccessTokenLifetime) + TimeSpan.FromMinutes(5);

        deployment.Clock.Advance(overlap - TimeSpan.FromSeconds(1));

        Answer within = await PushedAsync(deployment, RelyingParty.Secret);

        deployment.Clock.Advance(TimeSpan.FromSeconds(1));

        Answer after = await PushedAsync(deployment, RelyingParty.Secret);
        Answer current = await PushedAsync(deployment, replacing);

        Assert.Equal(StatusCodes.Status201Created, within.Status);
        Assert.Equal(StatusCodes.Status401Unauthorized, after.Status);
        Assert.Equal("invalid_client", after.Text("error"));
        Assert.Equal(StatusCodes.Status201Created, current.Status);
    }

    /// <summary>
    /// OPS-SEC-002, D-166 (340): of two processes that find the secret due together, one
    /// replaces it and the other reads what the first wrote, so both present one secret
    /// and it was replaced once.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_002_TwoProcessesRotatingTogetherLeaveOneSecretAsync()
    {
        await using var deployment = new Deployment();

        await RelyingParty.RegisteredAsync(deployment);

        deployment.Clock.Advance(await SettingAsync(deployment, Settings.TokenSigningRotation));

        string? second = null;

        // The second process reads the secret and replaces it between the first
        // process's read and its replacement.
        deployment.Clients.BeforeReplacement = async cancellationToken =>
            second = await CurrentAsync(deployment, cancellationToken);

        string first = await CurrentAsync(deployment, TestContext.Current.CancellationToken);

        Assert.Equal(second, first);
        Assert.NotEqual(RelyingParty.Secret, first);
        Assert.Equal(1, deployment.Clients.Replacements);
        Assert.Equal(first, await CurrentAsync(deployment, TestContext.Current.CancellationToken));
    }

    // One process's read of the application's secret, in a scope of its own.
    private static async Task<string> CurrentAsync(Deployment deployment, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = deployment.Scope();

        byte[] current = (await scope.ServiceProvider.GetRequiredService<RegisteredSecrets>()
                .CurrentAsync(RelyingParty.Application, cancellationToken))
            .Match(read => read, error => throw new InvalidOperationException(error.Code.ToString()));

        return Encoding.UTF8.GetString(current);
    }

    private static async Task<TimeSpan> SettingAsync(Deployment deployment, DurationSetting setting) =>
        (await deployment.Configuration.ReadAsync(setting, TestContext.Current.CancellationToken))
            .Match(read => read, error => throw new InvalidOperationException(error.Code.ToString()));

    // A request the application pushes, presenting the secret given.
    private static Task<Answer> PushedAsync(Deployment deployment, string secret) =>
        RelyingParty.PushAsync(
            deployment,
            RelyingParty.With(
                RelyingParty.Request(RelyingParty.Application, silent: false, RelyingParty.Destination, "openid"),
                "client_secret",
                secret));
}
