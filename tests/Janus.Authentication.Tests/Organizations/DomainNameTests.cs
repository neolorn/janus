using Janus.Authentication.Organizations;
using Janus.Core;
using Xunit;

namespace Janus.Authentication.Tests.Organizations;

/// <summary>
/// The one form a domain is listed, stored and compared in (REG-DOM-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class DomainNameTests
{
    private const int MaximumLength = 236;

    /// <summary>
    /// REG-DOM-001 AC13: read through the canonical form, a zero width non-joiner is
    /// removed before the conversion could refuse it, and a sharp s reads as two
    /// letters.
    /// </summary>
    /// <param name="entered">The domain as entered.</param>
    /// <param name="expected">The form it reads as.</param>
    [Theory]
    [InlineData("a‌b.example", "ab.example")]
    [InlineData("faß.example", "fass.example")]
    public void REG_DOM_001_AC13_TheCanonicalFormIsAppliedBeforeTheConversion(string entered, string expected)
    {
        bool read = DomainName.TryRead(entered, out string domain);

        Assert.True(read);
        Assert.Equal(expected, domain);
    }

    /// <summary>
    /// REG-DOM-001: a domain entered in its Unicode form, in its ASCII form in any case,
    /// or with a full stop of another script, reads as one lower-case ASCII form.
    /// </summary>
    /// <param name="entered">The domain as entered.</param>
    /// <param name="expected">The form it reads as.</param>
    [Theory]
    [InlineData("Bücher.example", "xn--bcher-kva.example")]
    [InlineData("xn--bcher-KVA.example", "xn--bcher-kva.example")]
    [InlineData(" Example.COM ", "example.com")]
    [InlineData("example。com", "example.com")]
    public void TryRead_ADomainEnteredInAnyOfItsForms_ReadsAsItsAsciiForm(string entered, string expected)
    {
        bool read = DomainName.TryRead(entered, out string domain);

        Assert.True(read);
        Assert.Equal(expected, domain);
    }

    /// <summary>
    /// REG-DOM-001: what the conversion refuses is no domain, and neither is a name of
    /// one label, a name with a trailing dot or nothing at all.
    /// </summary>
    /// <param name="entered">The domain as entered.</param>
    [Theory]
    [InlineData("ab--c.example")]
    [InlineData("xn--a.example")]
    [InlineData("אb.example")]
    [InlineData("1א.example")]
    [InlineData("-a.example")]
    [InlineData("a_b.example")]
    [InlineData("a..example")]
    [InlineData("example")]
    [InlineData("example.com.")]
    [InlineData("")]
    public void TryRead_WhatIsNoDomainALockCanList_IsRefused(string entered)
    {
        bool read = DomainName.TryRead(entered, out string domain);

        Assert.False(read);
        Assert.Equal(string.Empty, domain);
    }

    /// <summary>
    /// REG-DOM-001: the bound of 236 octets is judged on the converted name, so a name
    /// at the bound reads and a name one octet past it does not, though DNS could carry
    /// it.
    /// </summary>
    [Fact]
    public void TryRead_TheLengthBound_IsJudgedOnTheConvertedName()
    {
        string label = new('a', 63);
        string atTheBound = string.Join('.', label, label, label, new string('a', MaximumLength - 192));
        string pastTheBound = atTheBound + "a";

        bool within = DomainName.TryRead(atTheBound, out string domain);
        bool past = DomainName.TryRead(pastTheBound, out _);

        Assert.True(within);
        Assert.Equal(MaximumLength, domain.Length);
        Assert.False(past);
    }

    /// <summary>
    /// REG-DOM-001: an address's domain is read the same way a listed domain is.
    /// </summary>
    [Fact]
    public void TryReadOf_AnAddress_ReadsItsDomainAsAListedDomainIsRead()
    {
        Assert.True(EmailAddress.TryParse("person@Bücher.example", out EmailAddress address));

        bool read = DomainName.TryReadOf(address, out string domain);

        Assert.True(read);
        Assert.Equal("xn--bcher-kva.example", domain);
    }
}
