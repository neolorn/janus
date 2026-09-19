using System;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// The canonical form of an email address and the limits it is held to
/// (IDN-ACCT-004, IDN-ACCT-006, REG-IDENT-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class EmailAddressTests
{
    /// <summary>
    /// IDN-ACCT-004 AC1 and AC4: addresses differing only in composition, in width or
    /// in case are one address, so a second registration finds the first account.
    /// </summary>
    /// <param name="entered">The address as it was entered.</param>
    [Theory]
    [InlineData("ahmed@example.com")]
    [InlineData("Ahmed@Example.COM")]
    [InlineData("\uFF41hmed@example.com")]
    [InlineData("ah\u00ADmed@example.com")]
    public void IDN_ACCT_004_AC4_OneAddressWhateverFormItWasEnteredIn(string entered)
    {
        Assert.True(EmailAddress.TryParse(entered, out EmailAddress address));
        Assert.Equal("ahmed@example.com", address.Value);
    }

    /// <summary>
    /// REG-IDENT-001: the address is at most 254 octets and its local part at most 64,
    /// counted in octets so that a non-ASCII address is held to the same limit as an
    /// ASCII one.
    /// </summary>
    /// <param name="entered">The address as it was entered.</param>
    /// <param name="accepted">Whether it is within the limits.</param>
    [Theory]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa@example.com", true)]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa@example.com", false)]
    [InlineData("\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623@example.com", true)]
    [InlineData("\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623\u0623@example.com", false)]
    public void REG_IDENT_001_TheLocalPartIsAtMostSixtyFourOctets(string entered, bool accepted) =>
        Assert.Equal(accepted, EmailAddress.TryParse(entered, out _));

    /// <summary>
    /// REG-IDENT-001: an address is a local part, one at sign and a domain, and carries
    /// no space and nothing unprintable.
    /// </summary>
    /// <param name="entered">The value as it was entered.</param>
    [Theory]
    [InlineData("")]
    [InlineData("ahmed")]
    [InlineData("@example.com")]
    [InlineData("ahmed@")]
    [InlineData("ahmed@@example.com")]
    [InlineData("ah med@example.com")]
    [InlineData("ahmed@exam\u00A0ple.com")]
    [InlineData("ahmed@example.com\u0007")]
    public void REG_IDENT_001_NothingButAnAddressIsAnAddress(string entered) =>
        Assert.False(EmailAddress.TryParse(entered, out _));

    /// <summary>
    /// An address longer than the path RFC 5321 carries is refused whatever its local
    /// part.
    /// </summary>
    [Fact]
    public void REG_IDENT_001_TheWholeAddressIsAtMostTwoHundredAndFiftyFourOctets()
    {
        string domain = new('a', EmailAddress.MaximumOctets - EmailAddress.MaximumLocalPartOctets - 1);

        Assert.True(EmailAddress.TryParse(new string('a', 64) + "@" + domain, out _));
        Assert.False(EmailAddress.TryParse(new string('a', 64) + "@" + domain + "a", out _));
    }

    /// <summary>
    /// An unset value reads as an empty string rather than throwing, as every other
    /// value type in the library does.
    /// </summary>
    [Fact]
    public void Value_UnsetAddress_IsEmpty()
    {
        Assert.Equal(string.Empty, default(EmailAddress).Value);
        Assert.Equal(string.Empty, default(EmailAddress).ToString());
    }

    /// <summary>
    /// An absent value is a programming fault, not a malformed address.
    /// </summary>
    [Fact]
    public void TryParse_AbsentValue_Throws() =>
        Assert.Throws<ArgumentNullException>(() => EmailAddress.TryParse(null!, out _));
}
