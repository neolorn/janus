using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using Janus.Authentication.Alerting;
using Janus.Authentication.Factors;
using Janus.Authentication.Sending;
using Janus.Core;
using Xunit;

namespace Janus.Authentication.Tests.Sending;

/// <summary>
/// The width each place a template leaves for the library is measured at, which is
/// what a startup budget check renders a template with (INT-SMS-003, INT-SMS-005a).
/// </summary>
[Trait("kind", "unit")]
public sealed class MessagePlaceholdersTests
{
    /// <summary>
    /// INT-SMS-003 AC1: a place is filled to exactly the width it is defined at, and
    /// with text of the default alphabet, so filling a template does not change the
    /// budget it is measured against.
    /// </summary>
    [Fact]
    public void INT_SMS_003_AC1_APlaceIsFilledToTheWidthItIsDefinedAt()
    {
        foreach (KeyValuePair<string, int> place in MessagePlaceholders.Widths)
        {
            string filled = MessagePlaceholders.Widest("{" + place.Key + "}");

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

        Assert.Equal(MessagePlaceholders.Widths["token"], OpaqueToken.Draw(randomness).Value.Length);
        Assert.Equal(VerificationCode.Digits, MessagePlaceholders.Widths["code"]);

        foreach (AlertCondition condition in Enum.GetValues<AlertCondition>())
        {
            Assert.True(
                Alerts.Key(condition, null).Length <= MessagePlaceholders.Widths["condition"],
                $"The condition {condition} is written wider than a place allows.");
        }

        Assert.Equal(
            MessagePlaceholders.Widths["raisedAt"],
            DateTimeOffset.MaxValue.ToString("O", CultureInfo.InvariantCulture).Length);
    }
}
