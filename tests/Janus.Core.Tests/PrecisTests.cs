using System;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// The two PRECIS profiles an identifier must satisfy to be accepted: UsernameCaseMapped
/// for a username (RFC 8265), Nickname for a display name (RFC 8266). Covers
/// IDN-ACCT-004 and REG-PROF-001.
/// </summary>
[Trait("kind", "unit")]
public sealed class PrecisTests
{
    /// <summary>
    /// The legal userparts of RFC 8265 section 3.6 are accepted, and the two rules that
    /// change a value, case mapping and Form C, are the only things that change it.
    /// </summary>
    /// <param name="entered">The username as it was entered.</param>
    /// <param name="expected">The username in the profile's form.</param>
    [Theory]
    [InlineData("juliet@example.com", "juliet@example.com")]
    [InlineData("fussball", "fussball")]
    [InlineData("fu\u00DFball", "fu\u00DFball")]
    [InlineData("\u03C0", "\u03C0")]
    [InlineData("\u03A3", "\u03C3")]
    [InlineData("\u03C3", "\u03C3")]
    [InlineData("\u03C2", "\u03C2")]
    public void IDN_ACCT_004_AC1_UsernameProfileAcceptsALegalUserpart(string entered, string expected)
    {
        Assert.True(Precis.TryEnforceUsername(entered, out string enforced));
        Assert.Equal(expected, enforced);
    }

    /// <summary>
    /// The strings of RFC 8265 section 3.6 that violate the userpart rules are refused:
    /// a space, an empty value, a code point of the letter-number category and one of
    /// the symbol category are none of them in the IdentifierClass.
    /// </summary>
    /// <param name="entered">The username as it was entered.</param>
    [Theory]
    [InlineData("foo bar")]
    [InlineData("")]
    [InlineData("henry\u2163")]
    [InlineData("\u221E")]
    public void IDN_ACCT_004_AC1_UsernameProfileRefusesAnIllegalUserpart(string entered)
    {
        Assert.False(Precis.TryEnforceUsername(entered, out string enforced));
        Assert.Equal(string.Empty, enforced);
    }

    /// <summary>
    /// IDN-ACCT-004 AC4: the width mapping rule of the username profile takes a
    /// fullwidth or halfwidth code point to its decomposition, so the two spellings of
    /// one username are one username.
    /// </summary>
    [Fact]
    public void IDN_ACCT_004_AC4_UsernameProfileMapsFullwidthAndHalfwidthCodePoints()
    {
        Assert.True(Precis.TryEnforceUsername("\uFF41hmed", out string fullwidth));
        Assert.True(Precis.TryEnforceUsername("\uFF71hmed", out string halfwidth));
        Assert.Equal("ahmed", fullwidth);
        Assert.Equal("\u30A2hmed", halfwidth);
    }

    /// <summary>
    /// The normalization rule of the username profile is Form C, so a username entered
    /// decomposed is stored composed.
    /// </summary>
    [Fact]
    public void IDN_ACCT_004_AC1_UsernameProfileNormalizesToFormC()
    {
        Assert.True(Precis.TryEnforceUsername("a\u0301hmed", out string enforced));
        Assert.Equal("\u00E1hmed", enforced);
    }

    /// <summary>
    /// The directionality rule is the Bidi Rule of RFC 5893: a value holding
    /// right-to-left code points begins and ends with a strong right-to-left or numeric
    /// code point and mixes neither numeral system.
    /// </summary>
    /// <param name="entered">The username as it was entered.</param>
    /// <param name="conforms">Whether the Bidi Rule is satisfied.</param>
    [Theory]
    [InlineData("\u0645\u062D\u0645\u062F", true)]
    [InlineData("\u0645\u062D\u0645\u062F\u0662", true)]
    [InlineData("\u0645\u062D\u0645\u062F2", true)]
    [InlineData("\u0645\u062D\u0645\u062Fa", false)]
    [InlineData("a\u0645\u062D\u0645\u062F", false)]
    [InlineData("ahmed", true)]
    public void IDN_ACCT_004_AC1_UsernameProfileAppliesTheBidiRule(string entered, bool conforms) =>
        Assert.Equal(conforms, Precis.TryEnforceUsername(entered, out _));

    /// <summary>
    /// REG-PROF-001: the legal nicknames of RFC 8266 section 3 are accepted. Enforcement
    /// leaves case alone, so only the additional mapping rule and Form KC change a value.
    /// </summary>
    /// <param name="entered">The display name as it was entered.</param>
    /// <param name="expected">The display name in the profile's form.</param>
    [Theory]
    [InlineData("Foo", "Foo")]
    [InlineData("foo", "foo")]
    [InlineData("Foo Bar", "Foo Bar")]
    [InlineData("foo bar", "foo bar")]
    [InlineData("\u03A3", "\u03A3")]
    [InlineData("\u03C3", "\u03C3")]
    [InlineData("\u03C2", "\u03C2")]
    [InlineData("\u03D4", "\u03AB")]
    [InlineData("\u221E", "\u221E")]
    [InlineData("Richard \u2163", "Richard IV")]
    public void REG_PROF_001_NicknameProfileAcceptsALegalNickname(string entered, string expected)
    {
        Assert.True(Precis.TryEnforceNickname(entered, out string enforced));
        Assert.Equal(expected, enforced);
    }

    /// <summary>
    /// REG-PROF-001: the additional mapping rule of the nickname profile maps a space of
    /// any kind to an ASCII space, drops the spaces at either end and makes one space of
    /// a run inside.
    /// </summary>
    /// <param name="entered">The display name as it was entered.</param>
    [Theory]
    [InlineData("  Richard  \u2163  ")]
    [InlineData("\u00A0Richard\u2002\u2003\u2163\u3000")]
    public void REG_PROF_001_NicknameProfileSettlesTheSpaces(string entered)
    {
        Assert.True(Precis.TryEnforceNickname(entered, out string enforced));
        Assert.Equal("Richard IV", enforced);
    }

    /// <summary>
    /// REG-PROF-001: a display name is drawn from the FreeformClass, which admits a
    /// symbol and a letter-number that a username may not carry, and refuses a control
    /// and a value that is nothing but spaces.
    /// </summary>
    /// <param name="entered">The display name as it was entered.</param>
    /// <param name="conforms">Whether the profile is satisfied.</param>
    [Theory]
    [InlineData("\u221E", true)]
    [InlineData("henry\u2163", true)]
    [InlineData("Ahmed \u0645\u062D\u0645\u062F", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("Ahmed\u0007", false)]
    [InlineData("Ahmed\u200B", false)]
    public void REG_PROF_001_NicknameProfileDecidesOnTheFreeformClass(string entered, bool conforms) =>
        Assert.Equal(conforms, Precis.TryEnforceNickname(entered, out _));

    /// <summary>
    /// An absent value is a programming fault, not an empty username.
    /// </summary>
    [Fact]
    public void TryEnforceUsername_AbsentValue_Throws() =>
        Assert.Throws<ArgumentNullException>(() => Precis.TryEnforceUsername(null!, out _));

    /// <summary>
    /// An absent value is a programming fault, not an empty display name.
    /// </summary>
    [Fact]
    public void TryEnforceNickname_AbsentValue_Throws() =>
        Assert.Throws<ArgumentNullException>(() => Precis.TryEnforceNickname(null!, out _));
}
