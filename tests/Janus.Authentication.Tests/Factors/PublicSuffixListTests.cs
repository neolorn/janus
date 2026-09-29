using System;
using System.IO;
using System.Text;
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
