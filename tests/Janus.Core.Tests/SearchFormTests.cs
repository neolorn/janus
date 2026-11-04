using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// The one form the staff search holds a name in and reads a query in (IDN-SRCH-001,
/// D-256).
/// </summary>
[Trait("kind", "unit")]
public sealed class SearchFormTests
{
    /// <summary>
    /// IDN-SRCH-001 AC1: a name written with teh marbuta or heh, alef maksura or yeh, an
    /// alef with a hamza or a madda or the bare alef, with harakat or a tatweel or
    /// without, reads as one form.
    /// </summary>
    /// <param name="written">The name as one person wrote it.</param>
    /// <param name="other">The name as another wrote it.</param>
    [Theory]
    [InlineData("فاطمة", "فاطمه")]
    [InlineData("مصطفى", "مصطفي")]
    [InlineData("أحمد", "احمد")]
    [InlineData("إسلام", "اسلام")]
    [InlineData("آمنة", "امنه")]
    [InlineData("ٱلزهراء", "الزهراء")]
    [InlineData("مُحَمَّد", "محمد")]
    [InlineData("محـــمد", "محمد")]
    [InlineData("عليٰ", "علي")]
    public void IDN_SRCH_001_AC1_ArabicSpellingVariantsReadAsOneForm(string written, string other) =>
        Assert.Equal(SearchForm.Of(other), SearchForm.Of(written));

    /// <summary>
    /// Arabic-Indic and Eastern Arabic-Indic digits read as the ASCII digits.
    /// </summary>
    /// <param name="written">The digits as written.</param>
    /// <param name="expected">The form they read as.</param>
    [Theory]
    [InlineData("٠١٢٣٤٥٦٧٨٩", "0123456789")]
    [InlineData("۰۱۲۳۴۵۶۷۸۹", "0123456789")]
    public void Of_ArabicIndicDigits_ReadAsAsciiDigits(string written, string expected) =>
        Assert.Equal(expected, SearchForm.Of(written));

    /// <summary>
    /// Latin letters are lowercased and lose their combining marks, and a compatibility
    /// form reads as the letters it stands for.
    /// </summary>
    /// <param name="written">The name as written.</param>
    /// <param name="expected">The form it reads as.</param>
    [Theory]
    [InlineData("Émile ÇELİK", "emile celik")]
    [InlineData("José Nuñez", "jose nunez")]
    [InlineData("ＡＨＭＥＤ", "ahmed")]
    [InlineData("ﬁrst", "first")]
    public void Of_LatinLetters_AreLowercasedWithoutTheirMarks(string written, string expected) =>
        Assert.Equal(expected, SearchForm.Of(written));

    /// <summary>
    /// A mark on a letter of another script is kept, and the letter is not lowercased.
    /// </summary>
    [Fact]
    public void Of_ALetterOfAnotherScript_KeepsItsMarkAndItsCase() =>
        Assert.Equal("Ὀδυσσεύς", SearchForm.Of("Ὀδυσσεύς"));

    /// <summary>
    /// Every run of whitespace and punctuation is one space, and none is left at either
    /// end.
    /// </summary>
    /// <param name="written">The text as written.</param>
    /// <param name="expected">The form it reads as.</param>
    [Theory]
    [InlineData("  ahmed--ali,\t hassan. ", "ahmed ali hassan")]
    [InlineData("o'brien", "o brien")]
    [InlineData("محمد، علي", "محمد علي")]
    public void Of_RunsOfWhitespaceAndPunctuation_AreOneSpace(string written, string expected) =>
        Assert.Equal(expected, SearchForm.Of(written));

    /// <summary>
    /// A text of whitespace, punctuation and marks alone reads as nothing.
    /// </summary>
    /// <param name="written">The text as written.</param>
    [Theory]
    [InlineData("")]
    [InlineData(" .,;- ")]
    [InlineData("ـــ ًٌ")]
    public void Of_NothingSearched_ReadsAsNothing(string written) =>
        Assert.Equal(string.Empty, SearchForm.Of(written));
}
