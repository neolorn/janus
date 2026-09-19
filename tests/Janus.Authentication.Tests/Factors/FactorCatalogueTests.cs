using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Janus.Authentication.Factors;
using Janus.Core;
using Xunit;

namespace Janus.Authentication.Tests.Factors;

/// <summary>
/// The catalogue is the one place a factor is named (AUTH-FACT-001, AUTH-FACT-002).
/// </summary>
[Trait("kind", "unit")]
public sealed class FactorCatalogueTests
{
    /// <summary>
    /// AUTH-FACT-002: every entry of the catalogue carries what it may do, so a rule
    /// handed an entry always has properties to read.
    /// </summary>
    [Fact]
    public void AUTH_FACT_002_EveryEntryOfTheCatalogueIsRegistered() =>
        Assert.Equal(Enum.GetValues<Factor>().Order(), FactorCatalogue.Entries.Keys.Order());

    /// <summary>
    /// AUTH-FACT-001 AC1: the one conditional in the library that reads a factor by
    /// name is the policy object's refusal of the emergency credential, which chapter
    /// 10 section 4.1a states by name; no rule about what a factor may do reads one.
    /// </summary>
    [Fact]
    public void AUTH_FACT_001_AC1_NoConditionalTestsForAFactorByName() =>
        Assert.Equal([Path.Combine("Janus.Core", "Policy.cs")], BranchingOnAFactor());

    // The files of the library in which a catalogue entry is named inside a construct
    // that chooses between two paths, as the repository lays them out.
    private static string[] BranchingOnAFactor() =>
    [
        .. Tree()
            .Where(file => File.ReadLines(file).Any(Chooses))
            .Select(Relative)
            .Order(StringComparer.Ordinal),
    ];

    // A construct that chooses between two paths.
    private static readonly string[] Branches =
    [
        "if (", "case ", "switch", " is ", "==", "!=", "?",
        "Contains(", "Any(", "All(", "Where(", "Exists(",
    ];

    private static bool Chooses(string line) =>
        line.Contains("Factor.", StringComparison.Ordinal)
        && Array.Exists(Branches, branch => line.Contains(branch, StringComparison.Ordinal));

    private static string[] Tree() => Directory.GetFiles(
        Path.Combine(Root(), "src"),
        "*.cs",
        SearchOption.AllDirectories);

    private static string Relative(string file) =>
        Path.GetRelativePath(Path.Combine(Root(), "src"), file);

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
