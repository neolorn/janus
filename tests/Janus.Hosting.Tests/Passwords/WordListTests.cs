using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Hosting.Passwords;
using Janus.Hosting.Tests.Bff;
using Xunit;

namespace Janus.Hosting.Tests.Passwords;

/// <summary>
/// The word lists the dictionary rejection source reads: the English and Arabic lists
/// the package carries and the words a host declares beside them (AUTH-PASS-004).
/// </summary>
[Trait("kind", "unit")]
public sealed class WordListTests
{
    /// <summary>
    /// AUTH-PASS-004 AC8: the English list the package carries holds at least 10,000
    /// entries, each a single lower-case word of four letters or more.
    /// </summary>
    [Fact]
    public void AUTH_PASS_004_AC8_TheEnglishListHoldsTenThousandLowerCaseWords()
    {
        string[] words = Shipped(WordList.EnglishResource);

        Assert.True(words.Length >= 10_000, $"The English list holds {words.Length} words.");
        Assert.All(words, word => Assert.Matches("^[a-z]{4,}$", word));
    }

    /// <summary>
    /// AUTH-PASS-004, D-169: the English list's source is the 3esl list byte for byte as
    /// the 12dicts 6.0.2 package publishes it; only the build's filter acts on it.
    /// </summary>
    [Fact]
    public void AUTH_PASS_004_TheEnglishSourceIsTheListAsItsPackagePublishesIt()
    {
        byte[] vendored = File.ReadAllBytes(
            Path.Combine(Repository.Root(), "src", "Janus.Hosting", "Passwords", "3esl.txt"));

        Assert.Equal(
            "EB70E6169534511CAFF9E06971B3FA71981A83F0355B29532B710EA5A9DF253B",
            Convert.ToHexString(SHA256.HashData(vendored)));
    }

    /// <summary>
    /// AUTH-PASS-004: the Arabic list carries Arabizi forms written with each of the
    /// digits 2, 3, 5 and 7, and the variants its rules generate beside the entries they
    /// are generated from.
    /// </summary>
    [Fact]
    public void AUTH_PASS_004_TheArabicListCarriesArabiziFormsAndTheirVariants()
    {
        string[] words = Shipped(WordList.ArabicResource);

        Assert.All(words, word => Assert.Matches("^[a-z2357]{4,}$", word));
        Assert.Contains("mo2men", words);
        Assert.Contains("3omar", words);
        Assert.Contains("5aled", words);
        Assert.Contains("7abibi", words);
        Assert.Contains("habibi", words);
        Assert.Contains("3afrita", words);
        Assert.Contains("3afritah", words);
        Assert.Contains("afrita", words);
    }

    /// <summary>
    /// AUTH-PASS-004: an Arabizi form is matched as it is written, digits kept, by the
    /// Arabic list and not by the English one.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_PASS_004_AnArabiziFormIsMatchedByTheArabicListAsync()
    {
        const string password = "zq 7abibi zq";

        var english = new WordList(
            DictionaryWords.Default,
            resource => resource is WordList.EnglishResource ? Opened(resource) : Written(string.Empty));

        Assert.True(await MatchesAsync(new WordList(DictionaryWords.Default), password));
        Assert.False(await MatchesAsync(english, password));
    }

    /// <summary>
    /// AUTH-PASS-004: every character counts toward the four a match needs, digits
    /// included, so a declared word of three characters matches nothing and one of three
    /// letters and a digit matches.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_PASS_004_DigitsCountTowardTheFourCharactersAMatchNeedsAsync()
    {
        var words = new WordList(DictionaryWords.Of(["qx7", "qz7w"]), _ => Written(string.Empty));

        Assert.False(await MatchesAsync(words, "vv qx7 vv"));
        Assert.True(await MatchesAsync(words, "vv qz7w vv"));
    }

    /// <summary>
    /// AUTH-PASS-004, LIB-HOST-001: a word the host declares is matched beside the shipped
    /// lists, whatever its case.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_PASS_004_AWordTheHostDeclaresIsMatchedAsync()
    {
        const string password = "vv zqwvx vv";

        Assert.False(await MatchesAsync(new WordList(DictionaryWords.Default), password));
        Assert.True(await MatchesAsync(new WordList(DictionaryWords.Of([" ZqWvX "])), password));
    }

    /// <summary>
    /// AUTH-PASS-004: a list the package cannot open answers the screening failure, never
    /// a password that matched nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_PASS_004_AListThePackageCannotOpenAnswersTheScreeningFailureAsync()
    {
        var words = new WordList(
            DictionaryWords.Default,
            resource => resource is WordList.ArabicResource ? null : Opened(resource));

        Result<bool> matched = await words.MatchesAsync("vv zqwvx vv", TestContext.Current.CancellationToken);

        Assert.Equal(
            ErrorCodes.ScreeningUnavailable,
            matched.Match(_ => throw new Xunit.Sdk.XunitException("The list answered."), error => error.Code));
    }

    private static Stream? Opened(string resource) =>
        typeof(WordList).Assembly.GetManifestResourceStream(resource);

    private static MemoryStream Written(string contents) =>
        new(Encoding.UTF8.GetBytes(contents));

    private static string[] Shipped(string resource)
    {
        using Stream list = Opened(resource)
            ?? throw new InvalidOperationException("The package carries no list " + resource + ".");
        using var reading = new StreamReader(list);

        return reading.ReadToEnd().Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
    }

    private static async Task<bool> MatchesAsync(WordList words, string password) =>
        (await words.MatchesAsync(password, TestContext.Current.CancellationToken)).Match(
            matched => matched,
            error => throw new Xunit.Sdk.XunitException("The lists answered " + error.Code + "."));
}
