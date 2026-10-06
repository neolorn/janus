using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using Janus.Authentication.Alerting;
using Janus.Authentication.Factors;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Sending;

/// <summary>
/// The width each place a template leaves for the library is measured at, which is
/// what a startup budget check renders a template with (INT-SMS-003, INT-SMS-005a).
/// </summary>
[Trait("kind", "unit")]
public sealed class MessagePlaceholdersTests
{
    private static readonly MessagePlaceholders Places = new([], [], Landing.Origins);

    /// <summary>
    /// INT-SMS-003 AC1: a place is filled to exactly the width it is defined at, and
    /// with text of the default alphabet, so filling a template does not change the
    /// budget it is measured against.
    /// </summary>
    [Fact]
    public void INT_SMS_003_AC1_APlaceIsFilledToTheWidthItIsDefinedAt()
    {
        foreach (KeyValuePair<string, int> place in Places.Widths)
        {
            string filled = Places.Widest("{" + place.Key + "}");

            Assert.Equal(place.Value, filled.Length);
            Assert.Equal(filled.Length, MessageBudget.Units(filled));
            Assert.Equal(MessageBudget.DefaultAlphabet, MessageBudget.Of(filled));
        }
    }

    /// <summary>
    /// INT-SMS-003 AC1: the values the library draws are no wider than the widths it
    /// measures templates at, so a template that fits at startup fits at a send.
    /// </summary>
    [Fact]
    public void INT_SMS_003_AC1_TheValuesTheLibraryDrawsFitTheWidthsItMeasuresAt()
    {
        using var randomness = RandomNumberGenerator.Create();

        string token = OpaqueToken.Draw(randomness).Value;

        Assert.All(
            Enum.GetValues<LinkKind>(),
            kind => Assert.True(Landing.Links.Of(kind, token).Length <= Places.Widths["link"]));
        Assert.Equal(VerificationCode.Digits, Places.Widths["code"]);

        foreach (AlertCondition condition in Enum.GetValues<AlertCondition>())
        {
            Assert.True(
                Alerts.Key(condition, scope: null, named: null).Length <= Places.Widths["condition"],
                $"The condition {condition} is written wider than a place allows.");
        }

        Assert.Equal(
            Places.Widths["raisedAt"],
            DateTimeOffset.MaxValue.ToString("O", CultureInfo.InvariantCulture).Length);
    }

    /// <summary>
    /// INT-SMS-003, API-LAND-001: <c>link</c> is measured at the longer declared origin,
    /// <c>/link#</c>, the widest kind and a drawn token, one token size serving every
    /// link; no place carries a bare token.
    /// </summary>
    [Fact]
    public void INT_SMS_003_TheLinkIsMeasuredAtItsComposedWidth()
    {
        var landing = new LandingOrigins("https://a.example.test", "https://a-much-longer-origin.example.test");
        int width = new MessagePlaceholders([], [], landing).Widths["link"];

        Assert.Equal(
            landing.Account.Length + "/link#".Length + "identifier-confirm".Length + ".".Length + OpaqueToken.Width,
            width);
        Assert.DoesNotContain("token", Places.Widths.Keys, StringComparer.Ordinal);
    }

    /// <summary>
    /// INT-SMS-003 AC3: the <c>key</c> place is measured at the widest key a change can
    /// name, every key of the catalogue and each family at its widest parameter: an
    /// organization's identifier, or the longest category the host declared.
    /// </summary>
    [Fact]
    public void INT_SMS_003_AC1_TheKeyWidthCoversTheFamilies()
    {
        string organization = Guid.Empty.ToString("D", CultureInfo.InvariantCulture);
        string category = new('c', 80);
        int undeclared = Places.Widths["key"];
        int declared = new MessagePlaceholders([], ["identity", category], Landing.Origins).Widths["key"];

        Assert.All(Settings.All, setting => Assert.True(setting.Key.ToString().Length <= undeclared));
        Assert.All(
            Settings.Families.Where(family => !ReferenceEquals(family, Settings.HostCategoryRetention)),
            family => Assert.True(family.For(organization).ToString().Length <= undeclared));
        Assert.Equal(Settings.HostCategoryRetention.For(category).ToString().Length, declared);
    }

    /// <summary>
    /// INT-SMS-003: the <c>kind</c> place stays 32 wide, and the name of every event the
    /// library raises, each subject event among them, fits it.
    /// </summary>
    [Fact]
    public void INT_SMS_003_EveryEventKindFitsItsPlace()
    {
        Type[] kinds =
        [
            .. typeof(DomainEvent).Assembly.GetTypes()
                .Where(type => !type.IsAbstract && type.IsAssignableTo(typeof(DomainEvent))),
        ];

        Assert.Equal(32, Places.Widths["kind"]);
        Assert.Contains(kinds, type => type.IsAssignableTo(typeof(SubjectEvent)));
        Assert.All(kinds, type => Assert.True(
            type.Name.Length <= Places.Widths["kind"],
            $"The event {type.Name} is wider than its place."));
    }

    /// <summary>
    /// INT-SMS-003: the <c>type</c> and <c>status</c> places are measured at the
    /// spellings chapter 10 section 5.12c gives, which are the ones a send fills them
    /// with, and not at the names of the members.
    /// </summary>
    [Fact]
    public void INT_SMS_003_TypeAndStatusAreMeasuredAtTheirWrittenSpellings()
    {
        Assert.Equal("rectification".Length, Places.Widths["type"]);
        Assert.Equal("deemed-refused-by-lapse".Length, Places.Widths["status"]);
    }
}
