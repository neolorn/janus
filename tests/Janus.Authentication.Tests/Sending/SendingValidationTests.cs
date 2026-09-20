using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Sending;

/// <summary>
/// What is checked once, before anything is served: every message in every
/// configured language, every text message inside its budget, every host key
/// supplied, and no endpoint reached over plain HTTP (AUTH-ABUSE-005, INT-SMS-003,
/// INT-GEN-001, LIB-HOST-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class SendingValidationTests
{
    private static readonly string[] Languages = ["en", "ar"];

    private readonly ConfigurationInMemory _configuration = new();
    private readonly MessageTemplatesInMemory _templates = new();

    private RestrictionKeySuppliers _suppliers = RestrictionKeySuppliers.None;
    private IntegrationEndpoints _endpoints = IntegrationEndpoints.None;

    /// <summary>
    /// A deployment answering in two languages.
    /// </summary>
    public SendingValidationTests() =>
        _configuration.Set(Settings.NotificationLanguages, Languages);

    private SendingValidation Validation =>
        new(_configuration, _templates, _suppliers, _endpoints);

    /// <summary>
    /// AUTH-ABUSE-005 AC3 and INT-SMS-003 AC1: a text message over its language's
    /// budget stops the deployment, naming the key it is over on.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_005_AC3_AnOverBudgetTextMessageStopsStartupAsync()
    {
        _templates.Set(
            MessageKind.VerificationCode,
            SendKind.Sms,
            "ar",
            new MessageTemplate(null, new string('م', 71)));

        Error refusal = await RefusedAsync();

        Assert.Equal(ErrorCodes.ConfigurationValueNotAllowed, refusal.Code);
        Assert.Equal("verification-code.sms.ar", refusal.Details["key"].GetString());
        Assert.Equal(70, refusal.Details["allowed"].GetInt32());
    }

    /// <summary>
    /// AUTH-ABUSE-005 AC2 and INT-SMS-003 AC2: the check covers every message on
    /// every channel it goes out on, in every configured language.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_005_AC2_EveryMessageIsCheckedInEveryLanguageAsync()
    {
        await PassedAsync();

        IEnumerable<(MessageKind Message, SendKind Kind, string Language)> wanted =
            from message in MessageChannels.Messages
            from kind in MessageChannels.Of(message)
            from language in Languages
            select (message, kind, language);

        Assert.Equal([.. wanted.Order()], [.. _templates.Asked.Order()]);
    }

    /// <summary>
    /// INT-SMS-003 AC1: the Latin budget is enforced as the Arabic one is, so a
    /// message one character over 160 stops the deployment rather than costing two
    /// messages for every send.
    /// </summary>
    [Fact]
    public async Task INT_SMS_003_AC1_ALatinMessageOverItsBudgetStopsStartupAsync()
    {
        _templates.Set(
            MessageKind.VerificationCode,
            SendKind.Sms,
            "en",
            new MessageTemplate(null, new string('a', 161)));

        Error refusal = await RefusedAsync();

        Assert.Equal(ErrorCodes.ConfigurationValueNotAllowed, refusal.Code);
        Assert.Equal("verification-code.sms.en", refusal.Details["key"].GetString());
        Assert.Equal(160, refusal.Details["allowed"].GetInt32());
    }

    /// <summary>
    /// INT-SMS-003 AC2: every message that goes out by text is measured in every
    /// configured language, so no language is budgeted by the one it was written in.
    /// </summary>
    [Fact]
    public async Task INT_SMS_003_AC2_EveryTextMessageIsMeasuredInEveryLanguageAsync()
    {
        await PassedAsync();

        IEnumerable<(MessageKind Message, SendKind Kind, string Language)> texted =
            from message in MessageChannels.Messages
            where MessageChannels.Of(message).Contains(SendKind.Sms)
            from language in Languages
            select (message, SendKind.Sms, language);

        Assert.Equal(
            [.. texted.Order()],
            [.. _templates.Asked.Where(asked => asked.Kind is SendKind.Sms).Order()]);
    }

    /// <summary>
    /// AUTH-ABUSE-005 AC3: a language the catalogue cannot answer in stops the
    /// deployment, rather than the send a person is waiting for.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_005_AC3_ALanguageTheCatalogueCannotAnswerInStopsStartupAsync()
    {
        _templates.Complete = false;

        foreach (MessageKind message in MessageChannels.Messages)
        {
            foreach (SendKind kind in MessageChannels.Of(message))
            {
                _templates.Set(message, kind, "en", new MessageTemplate("subject", "text"));
                _templates.Set(message, kind, "ar", new MessageTemplate("subject", "text"));
            }
        }

        await PassedAsync();

        _configuration.Set(Settings.NotificationLanguages, ["en", "ar", "fr"]);

        Error refusal = await RefusedAsync();

        Assert.Equal(ErrorCodes.StartupDeclarationMissing, refusal.Code);
        Assert.EndsWith(".fr", refusal.Details["key"].GetString()!, StringComparison.Ordinal);
    }

    /// <summary>
    /// INT-GEN-001 AC1 and AC2: an endpoint reached over plain HTTP stops the
    /// deployment, and the failure names which integration and which setting.
    /// </summary>
    [Fact]
    public async Task INT_GEN_001_AC1_APlaintextEndpointStopsStartupAsync()
    {
        _endpoints = IntegrationEndpoints.Of(
        [
            new IntegrationEndpoint("sms", "sms.gateway.baseurl", new Uri("https://sms.example.test")),
            new IntegrationEndpoint("shipping", "shipping.baseurl", new Uri("http://ship.example.test")),
        ]);

        Error refusal = await RefusedAsync();

        Assert.Equal(ErrorCodes.EndpointInsecure, refusal.Code);
        Assert.Equal("shipping", refusal.Details["integration"].GetString());
        Assert.Equal("shipping.baseurl", refusal.Details["key"].GetString());
    }

    /// <summary>
    /// INT-GEN-001 AC1: a deployment whose endpoints all use TLS starts.
    /// </summary>
    [Fact]
    public async Task INT_GEN_001_AC1_EveryEndpointOverTlsStartsAsync()
    {
        _endpoints = IntegrationEndpoints.Of(
        [
            new IntegrationEndpoint("sms", "sms.gateway.baseurl", new Uri("https://sms.example.test")),
        ]);

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

    private async Task PassedAsync() =>
        (await Validation.ValidateAsync(TestContext.Current.CancellationToken)).Switch(
            () => { },
            error => throw new Xunit.Sdk.XunitException($"Startup was refused: {error.Code}."));

    private async Task<Error> RefusedAsync() =>
        (await Validation.ValidateAsync(TestContext.Current.CancellationToken)).Match(
            () => throw new Xunit.Sdk.XunitException("Startup was not refused."),
            error => error);
}
