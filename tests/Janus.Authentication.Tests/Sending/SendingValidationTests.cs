using System;
using System.Threading.Tasks;
using Janus.Authentication.Sending;
using Janus.Authentication.Tests.Alerting;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Sending;

/// <summary>
/// What is checked once, before anything is served: a handler for the messages and one
/// for the alerts, every host key supplied, and no endpoint reached over plain HTTP
/// (INT-SMS-006, OPS-ALERT-003, INT-GEN-001, LIB-HOST-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class SendingValidationTests
{
    private readonly ConfigurationInMemory _configuration = new();
    private readonly NotificationHandlerInMemory _notificationHandler = new();
    private readonly AlertHandlerInMemory _alertHandler = new();

    private RestrictionKeySuppliers _suppliers = RestrictionKeySuppliers.None;

    private SendingValidation Validation => new(_configuration, _suppliers, _notificationHandler, _alertHandler);

    /// <summary>
    /// INT-SMS-006 AC2: a deployment that registered no notification handler does not
    /// start, the refusal naming the handler.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task INT_SMS_006_AC2_ADeploymentWithNoNotificationHandlerIsRefusedByTheSendingCheckAsync()
    {
        Error refusal = (await new SendingValidation(_configuration, _suppliers, notificationHandler: null, _alertHandler)
                .ValidateAsync(TestContext.Current.CancellationToken))
            .Match(() => throw new Xunit.Sdk.XunitException("Startup was not refused."), error => error);

        Assert.Equal(ErrorCodes.StartupDeclarationMissing, refusal.Code);
        Assert.Equal("notificationHandler", refusal.Details["key"].GetString());
    }

    /// <summary>
    /// OPS-ALERT-003 AC4: a deployment that registered no alert handler does not start,
    /// the refusal naming the handler.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task OPS_ALERT_003_AC4_ADeploymentWithNoAlertHandlerIsRefusedByTheSendingCheckAsync()
    {
        Error refusal = (await new SendingValidation(_configuration, _suppliers, _notificationHandler, alertHandler: null)
                .ValidateAsync(TestContext.Current.CancellationToken))
            .Match(() => throw new Xunit.Sdk.XunitException("Startup was not refused."), error => error);

        Assert.Equal(ErrorCodes.StartupDeclarationMissing, refusal.Code);
        Assert.Equal("alertHandler", refusal.Details["key"].GetString());
    }

    /// <summary>
    /// INT-GEN-001 AC1: an endpoint the library calls reached over plain HTTP stops the
    /// deployment with the error the chapter names for it.
    /// </summary>
    /// <param name="key">The endpoint that is not over TLS.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [InlineData("integration.mailserver.endpoint")]
    [InlineData("password.blocklist.selfhosted.address")]
    public async Task INT_GEN_001_AC1_APlaintextEndpointStopsStartupAsync(string key)
    {
        Error refusal = await PlaintextAsync(key);

        Assert.Equal(ErrorCodes.EndpointInsecure, refusal.Code);
    }

    /// <summary>
    /// INT-GEN-001 AC2: the failure names the key the endpoint was read from, which
    /// is at once the setting and the integration it belongs to, and the other
    /// integration's key, over TLS, is not the one named.
    /// </summary>
    /// <param name="key">The endpoint that is not over TLS.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [InlineData("integration.mailserver.endpoint")]
    [InlineData("password.blocklist.selfhosted.address")]
    public async Task INT_GEN_001_AC2_TheFailureNamesTheIntegrationAndTheSettingAsync(string key)
    {
        Error refusal = await PlaintextAsync(key);

        Assert.Equal(key, refusal.Details["key"].GetString());
    }

    /// <summary>
    /// INT-GEN-001 AC1 and AC2: the mail server adapter's endpoint named over plain HTTP
    /// stops the deployment, and the failure names its key; over TLS it starts.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task INT_GEN_001_AC1_APlaintextMailServerEndpointStopsStartupAsync()
    {
        _configuration.Set(Settings.IntegrationMailServerEndpoint, "http://mailserver.example.test");

        Error refusal = await RefusedAsync();

        Assert.Equal(ErrorCodes.EndpointInsecure, refusal.Code);
        Assert.Equal("integration.mailserver.endpoint", refusal.Details["key"].GetString());

        _configuration.Set(Settings.IntegrationMailServerEndpoint, "https://mailserver.example.test");

        await PassedAsync();
    }

    /// <summary>
    /// INF-TLS-004 AC1: a self-hosted password corpus named over plain HTTP stops the
    /// deployment, and the failure names the key it was read from.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task INF_TLS_004_AC1_APlaintextCorpusAddressStopsStartupAsync()
    {
        _configuration.Set(Settings.PasswordBlocklistSelfHostedAddress, "http://corpus.example.test/range");

        Error refusal = await RefusedAsync();

        Assert.Equal(ErrorCodes.EndpointInsecure, refusal.Code);
        Assert.Equal("password.blocklist.selfhosted.address", refusal.Details["key"].GetString());

        _configuration.Set(Settings.PasswordBlocklistSelfHostedAddress, "https://corpus.example.test/range");

        await PassedAsync();
    }

    /// <summary>
    /// INT-GEN-001 AC1: a deployment whose endpoints use TLS starts, and so does one
    /// that names neither because it uses integrations of its own.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task INT_GEN_001_AC1_EveryEndpointOverTlsStartsAsync()
    {
        await PassedAsync();

        _configuration.Set(Settings.IntegrationMailServerEndpoint, "https://mailserver.example.test");
        _configuration.Set(Settings.PasswordBlocklistSelfHostedAddress, "https://corpus.example.test/range");

        await PassedAsync();
    }

    /// <summary>
    /// LIB-HOST-001: a restriction naming a host key no supplier answers for stops
    /// the deployment, naming the supplier it wanted.
    /// </summary>
    [Fact]
    public async Task ValidateAsync_ARestrictionWithNoSupplier_StopsStartupAsync()
    {
        _configuration.Set(
            Settings.Restrictions,
            [
                new Restriction(
                    "tenant.sends",
                    RestrictionKeyKind.Host,
                    "tenant",
                    RestrictionPurpose.Any,
                    [new Bucket(1, TimeSpan.FromHours(1), BucketWindow.Sliding)]),
            ]);

        Error refusal = await RefusedAsync();

        Assert.Equal(ErrorCodes.StartupDeclarationMissing, refusal.Code);
        Assert.Equal("tenant", refusal.Details["supplier"].GetString());

        _suppliers = RestrictionKeySuppliers.Of(
        [
            new RestrictionKeySupplier("tenant", (_, _) => ValueTask.FromResult("acme")),
        ]);

        await PassedAsync();
    }

    // Both endpoints are named over TLS, and then the one given over plain HTTP, so
    // the refusal can only be about that one.
    private async Task<Error> PlaintextAsync(string key)
    {
        _configuration.Set(Settings.IntegrationMailServerEndpoint, "https://mailserver.example.test");
        _configuration.Set(Settings.PasswordBlocklistSelfHostedAddress, "https://corpus.example.test/range");
        _configuration.Set(
            key.Contains("mailserver", StringComparison.Ordinal)
                ? Settings.IntegrationMailServerEndpoint
                : Settings.PasswordBlocklistSelfHostedAddress,
            "http://plain.example.test");

        return await RefusedAsync();
    }

    private async Task PassedAsync() =>
        (await Validation.ValidateAsync(TestContext.Current.CancellationToken)).Switch(
            () => { },
            error => throw new Xunit.Sdk.XunitException($"Startup was refused: {error.Code}."));

    private async Task<Error> RefusedAsync() =>
        (await Validation.ValidateAsync(TestContext.Current.CancellationToken)).Match(
            () => throw new Xunit.Sdk.XunitException("Startup was not refused."),
            error => error);
}
