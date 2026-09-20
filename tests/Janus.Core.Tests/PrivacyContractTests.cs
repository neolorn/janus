using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// What the privacy contract cannot be made to do: bundle purposes, hold a card
/// number, or name the transfer the deployment rests on a permit for
/// (PRIV-CONS-002, PRIV-CONS-010, PRIV-SENS-004).
/// </summary>
[Trait("kind", "unit")]
public sealed class PrivacyContractTests
{
    // The field names a card would be held under, in the spellings a schema or a
    // request shape would use. PRIV-SENS-004 AC2 is a search of the codebase, so the
    // search is written down here and run on every build.
    private static readonly string[] CardData =
    [
        "cardnumber",
        "cardno",
        "pan",
        "primaryaccountnumber",
        "cvv",
        "cvc",
        "securitycode",
        "expirymonth",
        "expiryyear",
        "cardholder",
    ];

    /// <summary>
    /// PRIV-CONS-002 AC1, AC2: every operation that decides a purpose takes one
    /// purpose, so no record can reference two and no one control can be built that
    /// grants for two.
    /// </summary>
    [Fact]
    public void PRIV_CONS_002_AC1_NoConsentOperationTakesMoreThanOnePurpose()
    {
        IReadOnlyList<ParameterInfo> purposes =
        [
            .. typeof(IConsents)
                .GetMethods()
                .SelectMany(method => method.GetParameters())
                .Where(parameter => parameter.Name is "purpose"),
        ];

        Assert.NotEmpty(purposes);
        Assert.All(purposes, parameter => Assert.Equal(typeof(string), parameter.ParameterType));
        Assert.DoesNotContain(
            typeof(IConsents).GetMethods().SelectMany(method => method.GetParameters()),
            parameter => parameter.ParameterType != typeof(string)
                && typeof(IEnumerable).IsAssignableFrom(parameter.ParameterType));
        Assert.Equal(typeof(string), typeof(ConsentRecord).GetProperty("Purpose")?.PropertyType);
    }

    /// <summary>
    /// PRIV-CONS-010 AC1, AC2: the library declares no purpose at all, so none of its
    /// source names the hosting transfer as one and a withdrawal reaches nothing the
    /// permit stands on.
    /// </summary>
    [Fact]
    public void PRIV_CONS_010_AC1_NoLibrarySourceNamesATransferPurpose()
    {
        string[] transfers = ["cross-border-transfer", "hosting-transfer", "transfer"];

        Assert.Empty(Naming(transfers));
    }

    /// <summary>
    /// PRIV-SENS-004 AC1, AC2: no field of the library holds a card number, its
    /// expiry or its verification value, and a search of the source finds none.
    /// </summary>
    [Fact]
    public void PRIV_SENS_004_AC1_NoLibraryFieldOrSourceNamesCardData()
    {
        Assert.Empty(Naming(CardData));
        Assert.DoesNotContain(
            typeof(Result).Assembly.GetTypes().SelectMany(type => type.GetProperties()),
            property => CardData.Contains(property.Name, StringComparer.OrdinalIgnoreCase));
    }

    // Every source file of the library naming one of the words, in any casing and
    // outside a comment. A word that appears only in prose about what is not held
    // would otherwise fail a search the criterion means literally.
    private static IReadOnlyList<string> Naming(IReadOnlyList<string> words) =>
    [
        .. Directory
            .EnumerateFiles(Path.Combine(Repository.Root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => File.ReadLines(file).Any(line =>
                !line.TrimStart().StartsWith('/')
                && words.Any(word => line.Contains(
                    '"' + word + '"',
                    StringComparison.OrdinalIgnoreCase))))
            .Select(file => Path.GetFileName(file)!)
            .Order(StringComparer.Ordinal),
    ];
}
