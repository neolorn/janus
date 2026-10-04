using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Janus.Authentication.Factors;
using Xunit;

namespace Janus.Authentication.Tests.Factors;

/// <summary>
/// The Public Suffix List the package carries and how it reads a host (AUTH-FACT-010,
/// AUTH-FACT-012).
/// </summary>
[Trait("kind", "unit")]
public sealed class PublicSuffixListTests
{
    // The pin of AUTH-FACT-010: every rule of the shipped list the conversion sets
    // aside, as the list writes it and in the list's order. It is reviewed and updated
    // with the list at each release (CONV-VCS-005), and the list shipped sets none aside.
    private static readonly string[] SetAsidePin = [];

    /// <summary>
    /// AUTH-FACT-010: the list travels unmodified, its header and its date included, with
    /// both its ICANN and its private sections.
    /// </summary>
    [Fact]
    public void AUTH_FACT_010_TheShippedListIsUnmodifiedWithItsHeaderAndDate()
    {
        using Stream shipped = typeof(PublicSuffixList).Assembly.GetManifestResourceStream(PublicSuffixList.Resource)
            ?? throw new InvalidOperationException("The package carries no Public Suffix List.");
        using var copy = new MemoryStream();

        shipped.CopyTo(copy);

        byte[] vendored = File.ReadAllBytes(
            Path.Combine(Root(), "src", "Janus.Authentication", "Factors", "public_suffix_list.dat"));
        string text = Encoding.UTF8.GetString(vendored);

        Assert.Equal(vendored, copy.ToArray());
        Assert.StartsWith(
            "// This Source Code Form is subject to the terms of the Mozilla Public",
            text,
            StringComparison.Ordinal);
        Assert.Matches(@"(?m)^// VERSION: \d{4}-\d{2}-\d{2}_", text);
        Assert.Contains("// ===BEGIN ICANN DOMAINS===", text, StringComparison.Ordinal);
        Assert.Contains("// ===BEGIN PRIVATE DOMAINS===", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// AUTH-FACT-012 AC2: the label counted is the one immediately before the public
    /// suffix, however many labels the suffix has, and in the private section too.
    /// </summary>
    [Fact]
    public void AUTH_FACT_012_AC2_TheLabelIsTheOneBeforeThePublicSuffix()
    {
        Assert.Equal("shop", PublicSuffixList.Shipped.Label("shop.com"));
        Assert.Equal("shop", PublicSuffixList.Shipped.Label("www.shop.co.uk"));
        Assert.Equal("someone", PublicSuffixList.Shipped.Label("someone.github.io"));
    }

    /// <summary>
    /// AUTH-FACT-010: a wildcard rule takes a label more as the suffix, and an exception
    /// rule gives one back, as the list's own algorithm reads them.
    /// </summary>
    [Fact]
    public void AUTH_FACT_010_WildcardAndExceptionRulesAreRead()
    {
        Assert.Equal("shop", PublicSuffixList.Shipped.Label("shop.anywhere.kawasaki.jp"));
        Assert.Equal("city", PublicSuffixList.Shipped.Label("www.city.kawasaki.jp"));
        Assert.True(PublicSuffixList.Shipped.IsSuffix("anywhere.kawasaki.jp"));
        Assert.False(PublicSuffixList.Shipped.IsSuffix("city.kawasaki.jp"));
    }

    /// <summary>
    /// AUTH-FACT-010: a name no rule covers has its last label as its suffix, and the
    /// comparison ignores case.
    /// </summary>
    [Fact]
    public void AUTH_FACT_010_ANameNoRuleCoversEndsAtItsLastLabel()
    {
        Assert.True(PublicSuffixList.Shipped.IsSuffix("unlisted"));
        Assert.False(PublicSuffixList.Shipped.IsSuffix("shop.unlisted"));
        Assert.True(PublicSuffixList.Shipped.IsSuffix("CO.UK"));
    }

    /// <summary>
    /// AUTH-FACT-010: a rule the list publishes in Unicode is read in its ASCII form, so
    /// a host is judged the same whichever form it is written in.
    /// </summary>
    [Fact]
    public void AUTH_FACT_010_ARuleIsReadInItsAsciiForm()
    {
        Assert.True(PublicSuffixList.Shipped.IsSuffix("公司.cn"));
        Assert.True(PublicSuffixList.Shipped.IsSuffix("xn--55qx5d.cn"));
        Assert.Equal("shop", PublicSuffixList.Shipped.Label("shop.公司.cn"));
        Assert.Equal("shop", PublicSuffixList.Shipped.Label("shop.xn--55qx5d.cn"));
    }

    /// <summary>
    /// AUTH-FACT-010: a host's ASCII form is the library's own, nontransitional and in
    /// lower case, whatever the machine would have made of the name.
    /// </summary>
    [Fact]
    public void AUTH_FACT_010_AHostIsReadInTheLibrarysOwnAsciiForm()
    {
        Assert.Equal("xn--fa-hia", PublicSuffixList.Shipped.Label("www.faß.de"));
        Assert.Equal("xn--bcher-kva", PublicSuffixList.Shipped.Label("BÜCHER.example"));
        Assert.Equal("shop", PublicSuffixList.Shipped.Label("SHOP.COM."));
    }

    /// <summary>
    /// AUTH-FACT-010: a host the conversion refuses under the checks of REG-DOM-001 has
    /// no ASCII form to compare, and the list says so before it is asked about it; one it
    /// admits is given in the form the list compares.
    /// </summary>
    [Fact]
    public void AUTH_FACT_010_AHostTheConversionRefusesDoesNotRead()
    {
        Assert.True(PublicSuffixList.TryAscii("Shop.BÜCHER.example.", out string ascii));
        Assert.Equal("shop.xn--bcher-kva.example", ascii);
        Assert.False(PublicSuffixList.TryAscii("my_shop.example.com", out _));
        Assert.False(PublicSuffixList.TryAscii("ab--c.example.com", out _));
        Assert.False(PublicSuffixList.TryAscii("-shop.example.com", out _));
        Assert.False(PublicSuffixList.TryAscii("shop..example.com", out _));
        Assert.Throws<ArgumentException>(() => PublicSuffixList.Shipped.IsSuffix("my_shop.example.com"));
        Assert.Throws<ArgumentException>(() => PublicSuffixList.Shipped.Label("my_shop.example.com"));
    }

    /// <summary>
    /// AUTH-FACT-010: a rule whose label the conversion refuses is set aside when the
    /// list is read, whichever kind of rule it is, and the list holds each as it was
    /// written.
    /// </summary>
    [Fact]
    public void AUTH_FACT_010_ARuleTheConversionRefusesIsSetAsideAndNamed()
    {
        using var list = new MemoryStream(Encoding.UTF8.GetBytes(string.Join(
            '\n',
            "// a comment",
            "example",
            "held_back.example",
            "*.ab--c.example",
            "!-kept.wild.example",
            "*.wild.example",
            "公司.example",
            string.Empty)));

        var read = PublicSuffixList.Read(list);

        Assert.Equal(["held_back.example", "*.ab--c.example", "!-kept.wild.example"], read.SetAside);
        Assert.True(read.IsSuffix("example"));
        Assert.True(read.IsSuffix("any.wild.example"));
        Assert.True(read.IsSuffix("xn--55qx5d.example"));
        Assert.False(read.IsSuffix("any.c.example"));
        Assert.Equal("shop", read.Label("shop.example"));
    }

    /// <summary>
    /// CONV-VCS-005 AC4: the pin of the suffix rules set aside matches the list drawn
    /// for the release, rule for rule and by name.
    /// </summary>
    [Fact]
    public void CONV_VCS_005_AC4_ThePinMatchesTheRulesTheShippedListSetsAside() =>
        Assert.Equal(SetAsidePin, PublicSuffixList.Shipped.SetAside);

    /// <summary>
    /// AUTH-FACT-010 AC4: a list that sets aside one rule more than a pin holds, or one
    /// fewer, or another rule in the place of one, does not match that pin.
    /// </summary>
    [Fact]
    public void AUTH_FACT_010_AC4_AListThatSetsAsideOneRuleMoreOrOneFewerDoesNotMatchThePin()
    {
        string[] pin = ["held_back.example", "*.ab--c.example"];

        Assert.Equal(pin, SetAsideOf("example", "held_back.example", "*.ab--c.example"));
        Assert.NotEqual(pin, SetAsideOf("example", "held_back.example", "*.ab--c.example", "!-kept.example"));
        Assert.NotEqual(pin, SetAsideOf("example", "held_back.example"));
        Assert.NotEqual(pin, SetAsideOf("example", "held_back.example", "*.ab--d.example"));
        Assert.NotEqual(SetAsidePin, SetAsideOf("example", "held_back.example"));
    }

    /// <summary>
    /// AUTH-FACT-010: no source of the library takes an ASCII form from the machine's
    /// mapping, so a registrable domain is the same on every machine.
    /// </summary>
    [Fact]
    public void AUTH_FACT_010_NoSourceTakesAnAsciiFormFromTheMachine()
    {
        string machine = "Idn" + "Mapping";
        string built = Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar;

        string[] naming = [.. Directory
            .EnumerateFiles(Path.Combine(Root(), "src"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains(built, StringComparison.Ordinal))
            .Where(file => Regex.IsMatch(File.ReadAllText(file), @"\b" + machine + @"\b"))];

        Assert.Empty(naming);
    }

    private static IReadOnlyList<string> SetAsideOf(params string[] rules)
    {
        using var list = new MemoryStream(Encoding.UTF8.GetBytes(string.Join('\n', rules) + "\n"));

        return PublicSuffixList.Read(list).SetAside;
    }

    private static string Root()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);

        while (at is not null && !Directory.Exists(Path.Combine(at.FullName, "src")))
        {
            at = at.Parent;
        }

        return at!.FullName;
    }
}
