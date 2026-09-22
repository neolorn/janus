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
    // What an order's contents would be named by, in the spellings a payload to a
    // provider would use. PRIV-MIN-001 is a search of the codebase, so the search is
    // written down here and run on every build.
    private static readonly string[] OrderContents =
    [
        "productname",
        "producttitle",
        "lineitem",
        "lineitems",
        "cart",
        "cartitems",
        "sku",
        "packagedescription",
        "packagecontents",
    ];

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

    /// <summary>
    /// PRIV-MIN-001 AC1, AC2: nothing of an order's contents is reachable in the
    /// library. It holds no cart, no line and no product, so no field of a payload it
    /// composes could carry a product name to a provider, and what a recipient
    /// receives is the categories the host declares and nothing derived.
    /// </summary>
    [Fact]
    public void PRIV_MIN_001_AC1_NoLibrarySourceNamesAnOrdersContents()
    {
        Assert.Empty(Naming(OrderContents));

        Assert.DoesNotContain(
            typeof(Result).Assembly.GetTypes().SelectMany(type => type.GetProperties()),
            property => OrderContents.Contains(property.Name, StringComparer.OrdinalIgnoreCase));

        Assert.Equal(
            typeof(IReadOnlyList<string>),
            typeof(RecipientDeclaration).GetProperty("DataReceived")!.PropertyType);
    }

    /// <summary>
    /// PRIV-CONS-009 AC1: the method of withdrawal is on the same contract as the
    /// grant and takes the same one purpose, so the screen that obtains a consent can
    /// state how it is taken back without anything further being built.
    /// </summary>
    [Fact]
    public void PRIV_CONS_009_AC1_TheWithdrawalIsOnTheSameContractAsTheGrant()
    {
        MethodInfo granting = typeof(IConsents).GetMethod("GrantAsync")!;
        MethodInfo withdrawing = typeof(IConsents).GetMethod("WithdrawAsync")!;

        Assert.Equal(
            ["context", "purpose", "cancellationToken"],
            withdrawing.GetParameters().Select(parameter => parameter.Name));

        Assert.Equal(
            granting.GetParameters().Length - 1,
            withdrawing.GetParameters().Length);
    }

    /// <summary>
    /// PRIV-RIGHT-006 AC1: nothing the library answers with is a sentence. A refusal
    /// is a code and structured details, so the words a subject reads are written in
    /// their language from the requirement and never translated from a string that
    /// crossed the boundary (LIB-API-003, CONV-CONTENT-001).
    /// </summary>
    [Fact]
    public void PRIV_RIGHT_006_AC1_NoAnswerTheLibraryGivesCarriesASentence()
    {
        Assert.Equal(typeof(ErrorCode), typeof(Error).GetProperty("Code")!.PropertyType);

        Assert.DoesNotContain(
            typeof(Error).GetProperties(),
            property => property.PropertyType == typeof(string));
    }

    /// <summary>
    /// PRIV-CONS-006a AC3: the library sets five cookies, every one of them strictly
    /// necessary to the service, and offers no way to register another. A cookie that
    /// is not necessary cannot be registered without a consent-based purpose because
    /// it cannot be registered at all.
    /// </summary>
    [Fact]
    public void PRIV_CONS_006a_AC3_TheLibrarySetsOnlyTheFiveNecessaryCookies()
    {
        Assert.Equal(
            [
                "__Host-janus-browser",
                "__Host-janus-csrf",
                "__Host-janus-device",
                "__Host-janus-preauth",
                "__Host-janus-session",
            ],
            Cookies());

        Assert.DoesNotContain(
            typeof(Result).Assembly.GetTypes().Where(type => type.IsPublic),
            type => type.Name.Contains("Cookie", StringComparison.Ordinal));
    }

    // Every cookie name the library's source sets, which is the whole of what a
    // browser carries from it.
    private static IReadOnlyList<string> Cookies() =>
    [
        .. Directory
            .EnumerateFiles(Path.Combine(Repository.Root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(File.ReadLines)
            .SelectMany(Named)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal),
    ];

    private static IEnumerable<string> Named(string line)
    {
        const string opening = "\"__Host-";

        for (int at = line.IndexOf(opening, StringComparison.Ordinal); at >= 0;
            at = line.IndexOf(opening, at + 1, StringComparison.Ordinal))
        {
            int closing = line.IndexOf('"', at + 1);

            if (closing > at)
            {
                yield return line[(at + 1)..closing];
            }
        }
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
