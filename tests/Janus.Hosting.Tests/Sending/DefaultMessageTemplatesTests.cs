using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Bff;
using Janus.Hosting.Sending;
using Janus.Hosting.Tests.Authorization;
using Janus.Hosting.Tests.Passwords;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Janus.Hosting.Tests.Sending;

/// <summary>
/// The catalogue the library ships, which is what a deployment that registers none of
/// its own sends with (LIB-EXT-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class DefaultMessageTemplatesTests
{
    private const string Connection = "Host=nowhere;Database=identity";

    private static readonly DefaultMessageTemplates Shipped = new();

    /// <summary>
    /// LIB-EXT-001: every message the library sends has words on every channel it goes
    /// out on, in every language the library carries, so a deployment that declares
    /// those languages and nothing else has a complete catalogue.
    /// </summary>
    [Fact]
    public void LIB_EXT_001_EveryMessageIsWordedOnEveryChannelInEveryLanguageCarried() =>
        Assert.All(
            Every(),
            held =>
            {
                MessageTemplate template = Found(held);

                Assert.NotEqual(string.Empty, template.Text);

                if (held.Kind is SendKind.Email)
                {
                    Assert.False(string.IsNullOrEmpty(template.Subject));
                }
                else
                {
                    Assert.Null(template.Subject);
                }
            });

    /// <summary>
    /// AUTH-ABUSE-005: every shipped text message fits one message with every place it
    /// names at its widest, in the alphabet it is written in, so a shipped default
    /// never costs a second message.
    /// </summary>
    [Fact]
    public void AUTH_ABUSE_005_EveryShippedTextMessageFitsOneMessageAtItsWidest() =>
        Assert.All(
            Every().Where(held => held.Kind is SendKind.Sms),
            held => Assert.False(MessageBudget.Exceeds(MessagePlaceholders.Widest(Found(held).Text))));

    /// <summary>
    /// CONV-CONTENT-001: a shipped text leaves a place only for a value the library
    /// puts there, so no default renders with a brace still in it.
    /// </summary>
    [Fact]
    public void CONV_CONTENT_001_EveryPlaceAShippedTextNamesIsOneTheLibraryFills() =>
        Assert.All(
            Every(),
            held =>
            {
                MessageTemplate template = Found(held);

                Assert.All(
                    Places((template.Subject ?? string.Empty) + " " + template.Text),
                    place => Assert.Contains(place, MessagePlaceholders.Widths.Keys));
            });

    /// <summary>
    /// AUTH-ABUSE-003 AC6: the notice that someone tried to register or change to an
    /// address already held points its holder to sign-in and to recovery, on every
    /// channel and in every language carried, and by mail it names both attempts it is
    /// sent for, so a returning customer who forgot the account is not left at a dead end.
    /// </summary>
    [Fact]
    public void AUTH_ABUSE_003_AC6_TheAccountExistsNoticePointsToSignInAndRecovery()
    {
        Dictionary<string, (string SignIn, string Recovery, string Registration, string Change)> words = new(StringComparer.Ordinal)
        {
            ["en"] = ("sign in", "recover", "register", "change"),
            ["ar"] = ("سجل الدخول", "استرد", "حساب جديد", "تغيير"),
        };

        Assert.Equal(DefaultMessageTemplates.Languages.Order(StringComparer.Ordinal), words.Keys.Order(StringComparer.Ordinal));
        Assert.All(
            Every().Where(held => held.Message is MessageKind.AccountExists),
            held =>
            {
                string text = Found(held).Text;
                (string signIn, string recovery, string registration, string change) = words[held.Language];

                Assert.Contains(signIn, text, StringComparison.Ordinal);
                Assert.Contains(recovery, text, StringComparison.Ordinal);

                if (held.Kind is SendKind.Email)
                {
                    Assert.Contains(registration, text, StringComparison.Ordinal);
                    Assert.Contains(change, text, StringComparison.Ordinal);
                }
            });
    }

    /// <summary>
    /// LIB-EXT-001 AC1: a deployment that registers no catalogue gets the shipped one
    /// and starts; nothing about the registration refuses it.
    /// </summary>
    [Fact]
    public void LIB_EXT_001_AC1_ADeploymentThatRegistersNoCatalogueGetsTheShippedOne() =>
        Assert.IsType<DefaultMessageTemplates>(Registered(new ServiceCollection()));

    /// <summary>
    /// LIB-EXT-001 AC2: the catalogue a deployment registers is the one in force, and
    /// replacing it takes no change to the library.
    /// </summary>
    [Fact]
    public void LIB_EXT_001_AC2_TheCatalogueTheDeploymentRegistersIsTheOneInForce()
    {
        var services = new ServiceCollection();

        _ = services.AddSingleton<IMessageTemplates>(new CatalogueOfOne());

        _ = Assert.IsType<CatalogueOfOne>(Registered(services));
    }

    /// <summary>
    /// AUTH-ABUSE-005 AC3: a deployment that registers no catalogue and declares the
    /// languages the library carries passes the startup check, so declaring none is
    /// not itself a refusal.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_005_AC3_ADeploymentOnTheShippedCatalogueStartsAsync() =>
        (await CheckedAsync(DefaultMessageTemplates.Languages)).Switch(
            () => { },
            error => throw new Xunit.Sdk.XunitException(
                $"The shipped catalogue was refused: {error.Code}."));

    /// <summary>
    /// AUTH-ABUSE-005 AC3: a language the shipped catalogue is not written in still
    /// stops the deployment, which is the deployment's to supply and the check's to
    /// refuse.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_005_AC3_ALanguageTheShippedCatalogueLacksStopsStartupAsync()
    {
        Error refusal = (await CheckedAsync([.. DefaultMessageTemplates.Languages, "fr"])).Match(
            () => throw new Xunit.Sdk.XunitException("A language with no words was allowed."),
            error => error);

        Assert.Equal(ErrorCodes.StartupDeclarationMissing, refusal.Code);
        Assert.EndsWith(".fr", refusal.Details["key"].GetString()!, StringComparison.Ordinal);
    }

    private static async Task<Result> CheckedAsync(IReadOnlyList<string> languages)
    {
        var configuration = new ConfigurationInMemory();

        configuration.Set(Settings.NotificationLanguages, languages);

        return await new SendingValidation(
                configuration,
                Shipped,
                RestrictionKeySuppliers.None)
            .ValidateAsync(TestContext.Current.CancellationToken);
    }

    private static IEnumerable<(MessageKind Message, SendKind Kind, string Language)> Every() =>
        from message in MessageChannels.Messages
        from kind in MessageChannels.Of(message)
        from language in DefaultMessageTemplates.Languages
        select (message, kind, language);

    private static MessageTemplate Found((MessageKind Message, SendKind Kind, string Language) held) =>
        Shipped.Find(held.Message, held.Kind, held.Language)
            .Match(
                template => template,
                error => throw new Xunit.Sdk.XunitException(
                    $"The catalogue holds nothing for {held.Message}.{held.Kind}.{held.Language}: {error.Code}."));

    private static IEnumerable<string> Places(string text)
    {
        int opened = text.IndexOf('{', StringComparison.Ordinal);

        while (opened >= 0)
        {
            int closed = text.IndexOf('}', opened);

            if (closed < 0)
            {
                yield break;
            }

            yield return text[(opened + 1)..closed];

            opened = text.IndexOf('{', closed);
        }
    }

    private static IMessageTemplates Registered(IServiceCollection services) =>
        services
            .AddJanus(
                Connection,
                new KeyEncryptionKeys(1, new Dictionary<int, ReadOnlyMemory<byte>> { [1] = new byte[32] }),
                new FingerprintKeys(1, new Dictionary<int, ReadOnlyMemory<byte>> { [1] = new byte[32] }),
                new byte[16],
                Encoding.UTF8.GetBytes(Connection),
                HostFixture.Declaration(),
                ApplicationKind.Public)
            .BuildServiceProvider()
            .GetRequiredService<IMessageTemplates>();

    // A catalogue of the deployment's own, which answers for nothing: what is read of
    // it here is that it is the one the container holds.
    private sealed class CatalogueOfOne : IMessageTemplates
    {
        public Result<MessageTemplate> Find(MessageKind message, SendKind kind, string language) =>
            Result.Failure<MessageTemplate>(Error.From(ErrorCodes.StartupDeclarationMissing));
    }
}
