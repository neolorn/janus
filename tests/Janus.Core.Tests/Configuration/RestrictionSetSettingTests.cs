using System;
using System.Collections.Generic;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Core.Tests.Configuration;

/// <summary>
/// The named restriction set as a value of its key: which sets it admits, held to the
/// rule a name that fills a message place is held to (INT-SMS-003, AUTH-ABUSE-004).
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
