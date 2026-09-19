using System;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// Mixed-script detection within a word, as UTS #39 section 5.1 defines it
/// (IDN-ACCT-005).
/// </summary>
[Trait("kind", "unit")]
public sealed class ScriptMixingTests
{
    /// <summary>
    /// IDN-ACCT-005 AC1: a name whose first letter is Cyrillic and whose remaining
    /// letters are Latin mixes scripts inside one word.
    /// </summary>
    [Fact]
    public void IDN_ACCT_005_AC1_AWordMixingCyrillicAndLatinIsRejected() =>
        Assert.False(ScriptMixing.IsSingleScriptPerWord("\u0410hmed"));

    /// <summary>
    /// IDN-ACCT-005 AC1: the same name written wholly in Cyrillic or wholly in Latin is
    /// one script and is accepted, so the rule catches the mixture and not the script.
    /// </summary>
    /// <param name="value">The value.</param>
    [Theory]
    [InlineData("\u0410\u0445\u043C\u0435\u0434")]
    [InlineData("Ahmed")]
    public void IDN_ACCT_005_AC1_AWordOfOneScriptIsAccepted(string value) =>
        Assert.True(ScriptMixing.IsSingleScriptPerWord(value));

    /// <summary>
    /// IDN-ACCT-005 AC2: two words of different scripts are two words, each of one
    /// script, and are accepted together.
    /// </summary>
    [Fact]
    public void IDN_ACCT_005_AC2_SeparateWordsOfDifferentScriptsAreAccepted() =>
        Assert.True(ScriptMixing.IsSingleScriptPerWord("\u0623\u062D\u0645\u062F Ahmed"));

    /// <summary>
    /// IDN-ACCT-005 AC4: an apostrophe, a hyphen and a digit are of script Common, which
    /// is ignored when deciding, so none of them makes a word mixed.
    /// </summary>
    /// <param name="value">The value.</param>
    [Theory]
    [InlineData("O'Brien")]
    [InlineData("O\u2019Brien")]
    [InlineData("Jean-Luc")]
    [InlineData("\u0645\u062D\u0645\u062F2")]
    [InlineData("Ahmed2")]
    public void IDN_ACCT_005_AC4_CommonScriptCodePointsAreNeverAForeignScript(string value) =>
        Assert.True(ScriptMixing.IsSingleScriptPerWord(value));

    /// <summary>
    /// A combining mark is of script Inherited, which takes the script of what it
    /// follows rather than being a script of its own.
    /// </summary>
    [Fact]
    public void IDN_ACCT_005_AC4_InheritedScriptCodePointsAreNeverAForeignScript() =>
        Assert.True(ScriptMixing.IsSingleScriptPerWord("Jose\u0301"));

    /// <summary>
    /// Japanese writes Han, Hiragana and Katakana together, and UTS #39 resolves that
    /// combination to a single script rather than a mixture.
    /// </summary>
    /// <param name="value">The value.</param>
    [Theory]
    [InlineData("\u6F22\u5B57\u3072\u3089\u304C\u306A")]
    [InlineData("\u30AB\u30BF\u30AB\u30CA\u6F22\u5B57")]
    [InlineData("\uD55C\uAE00\u6F22\u5B57")]
    public void IDN_ACCT_005_AC1_AugmentedScriptSetsResolveTheEastAsianCombinations(string value) =>
        Assert.True(ScriptMixing.IsSingleScriptPerWord(value));

    /// <summary>
    /// IDN-ACCT-005 AC1: the mixture is caught wherever it is, so a word that is mixed
    /// makes the whole value mixed even when every other word is not.
    /// </summary>
    [Fact]
    public void IDN_ACCT_005_AC1_OneMixedWordRejectsTheWholeValue() =>
        Assert.False(ScriptMixing.IsSingleScriptPerWord("Ahmed \u0410hmed"));

    /// <summary>
    /// An empty value has no word that mixes scripts; length is not this rule's
    /// business.
    /// </summary>
    [Fact]
    public void IsSingleScriptPerWord_EmptyValue_Accepted() =>
        Assert.True(ScriptMixing.IsSingleScriptPerWord(string.Empty));

    /// <summary>
    /// An absent value is a programming fault, not an empty identifier.
    /// </summary>
    [Fact]
    public void IsSingleScriptPerWord_AbsentValue_Throws() =>
        Assert.Throws<ArgumentNullException>(() => ScriptMixing.IsSingleScriptPerWord(null!));
}
