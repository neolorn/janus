using System;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// The E.164 form of a telephone number (IDN-ACCT-004, REG-IDENT-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class PhoneNumberTests
{
    /// <summary>
    /// IDN-ACCT-004 AC5: a number entered in digits of any script stores as the E.164
    /// value its ASCII-digit form stores as.
    /// </summary>
    /// <param name="entered">The number as it was entered.</param>
    [Theory]
    [InlineData("+201001234567")]
    [InlineData("201001234567")]
    [InlineData("+\u0662\u0660\u0661\u0660\u0660\u0661\u0662\u0663\u0664\u0665\u0666\u0667")]
    [InlineData("+\u06F2\u06F0\u06F1\u06F0\u06F0\u06F1\u06F2\u06F3\u06F4\u06F5\u06F6\u06F7")]
    [InlineData("+\uFF12\uFF10\uFF11\uFF10\uFF10\uFF11\uFF12\uFF13\uFF14\uFF15\uFF16\uFF17")]
    public void IDN_ACCT_004_AC5_OneNumberWhateverScriptItsDigitsWereEnteredIn(string entered)
    {
        Assert.True(PhoneNumber.TryParse(entered, out PhoneNumber number));
        Assert.Equal("+201001234567", number.Value);
    }

    /// <summary>
    /// REG-IDENT-001: E.164 carries at most fifteen digits.
    /// </summary>
    [Fact]
    public void REG_IDENT_001_ANumberIsAtMostFifteenDigits()
    {
        Assert.True(PhoneNumber.TryParse("+" + new string('2', PhoneNumber.MaximumDigits), out _));
        Assert.False(PhoneNumber.TryParse("+" + new string('2', PhoneNumber.MaximumDigits + 1), out _));
    }

    /// <summary>
    /// REG-IDENT-001: the stored form is E.164 and nothing else, so the separators a
    /// person may type are not part of a number.
    /// </summary>
    /// <param name="entered">The value as it was entered.</param>
    [Theory]
    [InlineData("")]
    [InlineData("+")]
    [InlineData("+20 100 123 4567")]
    [InlineData("+20-100-1234567")]
    [InlineData("+20(100)1234567")]
    [InlineData("+20100123456x7")]
    [InlineData("++201001234567")]
    public void REG_IDENT_001_NothingButDigitsIsANumber(string entered) =>
        Assert.False(PhoneNumber.TryParse(entered, out _));

    /// <summary>
    /// An unset value reads as an empty string rather than throwing.
    /// </summary>
    [Fact]
    public void Value_UnsetNumber_IsEmpty()
    {
        Assert.Equal(string.Empty, default(PhoneNumber).Value);
        Assert.Equal(string.Empty, default(PhoneNumber).ToString());
    }

    /// <summary>
    /// An absent value is a programming fault, not a malformed number.
    /// </summary>
    [Fact]
    public void TryParse_AbsentValue_Throws() =>
        Assert.Throws<ArgumentNullException>(() => PhoneNumber.TryParse(null!, out _));
}
