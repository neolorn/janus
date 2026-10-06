using System;
using System.Collections.Generic;
using System.Linq;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Core.Tests.Configuration;

/// <summary>
/// The named restriction set as a value of its key: which sets it admits, held to the
/// rule a name that fills a message place is held to (INT-SMS-003, AUTH-ABUSE-004),
/// and how a restriction's channel reads, writes and loosens (chapter 10 section 5.15a).
/// </summary>
[Trait("kind", "unit")]
public sealed class RestrictionSetSettingTests
{
    /// <summary>
    /// INT-SMS-003 AC3: a restriction whose name breaks the rule is refused where it is
    /// written, with <c>config.value.notallowed</c> naming the key.
    /// </summary>
    /// <param name="name">A name outside the rule.</param>
    [Theory]
    [InlineData("")]
    [InlineData("Sms.destination")]
    [InlineData("sms..destination")]
    [InlineData(".sms")]
    [InlineData("sms.")]
    [InlineData("sms destination")]
    [InlineData("sms/destination")]
    [InlineData("séance")]
    [InlineData("sms.-destination")]
    public void INT_SMS_003_ARestrictionNameOutsideTheRuleIsRefused(string name)
    {
        Result<IReadOnlyList<Restriction>> accepted = Settings.Restrictions.Accept([Named(name)]);

        Error refused = accepted.Match(
            _ => throw new Xunit.Sdk.XunitException("The set was admitted."),
            error => error);

        Assert.Equal(ErrorCodes.ConfigurationValueNotAllowed, refused.Code);
        Assert.Equal("restrictions", refused.Details["key"].GetString());
    }

    /// <summary>
    /// INT-SMS-003 AC3: a name longer than 64 characters is refused, and one of 64 is
    /// admitted.
    /// </summary>
    [Fact]
    public void INT_SMS_003_ARestrictionNameIsAtMostSixtyFourCharacters()
    {
        Assert.True(Admitted(new string('a', 64)));
        Assert.False(Admitted(new string('a', 65)));
    }

    /// <summary>
    /// INT-SMS-003: lower-case letters and digits separated by single <c>.</c>,
    /// <c>-</c> or <c>_</c> are admitted.
    /// </summary>
    /// <param name="name">A name inside the rule.</param>
    [Theory]
    [InlineData("a")]
    [InlineData("7")]
    [InlineData("sms.destination")]
    [InlineData("host-key_2.limit")]
    public void INT_SMS_003_ARestrictionNameInsideTheRuleIsAdmitted(string name) =>
        Assert.True(Admitted(name));

    /// <summary>
    /// INT-SMS-003: the shipped restriction names satisfy the rule, so the default set
    /// is a value its own key admits.
    /// </summary>
    [Fact]
    public void INT_SMS_003_TheShippedRestrictionNamesKeepTheRule()
    {
        Assert.All(Settings.Restrictions.Default, restriction => Assert.True(PlaceName.Holds(restriction.Name)));
        Assert.True(Settings.Restrictions.Accept(Settings.Restrictions.Default).Match(_ => true, _ => false));
    }

    /// <summary>
    /// AUTH-ABUSE-004 and chapter 10 section 5.15a: the shipped restrictions carry the
    /// channel each is named for, and a restriction written with its channel reads back
    /// with it.
    /// </summary>
    [Fact]
    public void AUTH_ABUSE_004_TheShippedRestrictionsCarryTheirChannels()
    {
        Dictionary<string, RestrictionChannel> shipped = [];

        foreach (Restriction restriction in Settings.Restrictions.Default)
        {
            shipped[restriction.Name] = restriction.Channel;
        }

        Assert.Equal(RestrictionChannel.Sms, shipped["sms.destination"]);
        Assert.Equal(RestrictionChannel.Sms, shipped["sms.source"]);
        Assert.Equal(RestrictionChannel.Email, shipped["email.destination"]);
        Assert.Equal(RestrictionChannel.Any, shipped["notification.destination"]);

        IReadOnlyList<Restriction> read = Settings.Restrictions
            .Read(Settings.Restrictions.Write(Settings.Restrictions.Default))
            .Match(value => value, error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

        Assert.Equal(Settings.Restrictions.Default, read, RestrictionEquals);
    }

    /// <summary>
    /// AUTH-ABUSE-004 and chapter 10 section 5.15a: a restriction stored before it could
    /// name a channel reads as <c>any</c>.
    /// </summary>
    [Fact]
    public void AUTH_ABUSE_004_AStoredRestrictionWithoutAChannelReadsAsAny()
    {
        const string stored =
            "[{\"name\":\"old.destination\",\"key\":\"destination\",\"purpose\":\"any\","
            + "\"buckets\":[{\"max\":3,\"interval\":\"PT24H\",\"window\":\"sliding\"}]}]";

        Restriction read = Assert.Single(Settings.Restrictions
            .Read(stored)
            .Match(value => value, error => throw new Xunit.Sdk.XunitException(error.Code.ToString())));

        Assert.Equal(RestrictionChannel.Any, read.Channel);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC3 and chapter 10 section 4: a channel changed to anything but
    /// <c>any</c> is a loosening, and one changed to <c>any</c> or left as it stood is not.
    /// </summary>
    /// <param name="before">The channel that stood.</param>
    /// <param name="after">The channel that replaces it.</param>
    /// <param name="loosening">Whether the change loosens.</param>
    [Theory]
    [InlineData(RestrictionChannel.Any, RestrictionChannel.Sms, true)]
    [InlineData(RestrictionChannel.Any, RestrictionChannel.Email, true)]
    [InlineData(RestrictionChannel.Sms, RestrictionChannel.Email, true)]
    [InlineData(RestrictionChannel.Email, RestrictionChannel.Sms, true)]
    [InlineData(RestrictionChannel.Sms, RestrictionChannel.Any, false)]
    [InlineData(RestrictionChannel.Email, RestrictionChannel.Any, false)]
    [InlineData(RestrictionChannel.Sms, RestrictionChannel.Sms, false)]
    [InlineData(RestrictionChannel.Any, RestrictionChannel.Any, false)]
    public void AUTH_ABUSE_004_AC3_NarrowingAChannelIsALoosening(
        RestrictionChannel before,
        RestrictionChannel after,
        bool loosening) =>
        Assert.Equal(
            loosening,
            RestrictionSetSetting.Loosens(
                Named("sms.destination") with { Channel = before },
                Named("sms.destination") with { Channel = after }));

    // A record's equality compares its list of buckets by reference, so two readings of
    // one set are compared member by member.
    private static bool RestrictionEquals(Restriction left, Restriction right) =>
        left.Name == right.Name
        && left.Key == right.Key
        && left.HostKeyName == right.HostKeyName
        && left.Purpose == right.Purpose
        && left.Channel == right.Channel
        && left.Buckets.SequenceEqual(right.Buckets);

    private static bool Admitted(string name) =>
        Settings.Restrictions.Accept([Named(name)]).Match(_ => true, _ => false);

    private static Restriction Named(string name) =>
        new(
            name,
            RestrictionKeyKind.Destination,
            null,
            RestrictionPurpose.Any,
            [new Bucket(1, TimeSpan.FromHours(1), BucketWindow.Sliding)]);
}
