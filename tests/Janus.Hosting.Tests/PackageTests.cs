using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Janus.Hosting.Tests.Bff;
using Xunit;

namespace Janus.Hosting.Tests;

/// <summary>
/// What the library's solution packs: the main package carrying the areas, and the four
/// packages that ship beside it (LIB-PKG-001, CONV-LAYOUT-001, D-205).
/// </summary>
[Trait("kind", "contract")]
public sealed class PackageTests(PackedLibrary packed) : IClassFixture<PackedLibrary>
{
    private const string Main = "Janus.Hosting";

    // The seven projects whose assemblies the main package carries.
    private static readonly string[] Carried =
    [
        "Janus.Authentication",
        "Janus.Authorization",
        "Janus.Core",
        "Janus.Hosting",
        "Janus.Identity",
        "Janus.Privacy",
        "Janus.Storage",
    ];

    /// <summary>
    /// LIB-PKG-001 AC4: the main package is packed from the hosting project under its
    /// identifier, its <c>lib/net10.0</c> holds the seven assemblies and no other, with the
    /// documentation of the hosting project and the contracts and nothing else, and it
    /// carries the framework reference.
    /// </summary>
    [Fact]
    public void LIB_PKG_001_AC4_TheMainPackageCarriesTheSevenAssembliesAndTheFramework()
    {
        PackedPackage main = packed.Packages[Main];

        string[] library =
        [
            .. main.Entries
                .Where(entry => entry.StartsWith("lib/", StringComparison.Ordinal))
                .Order(StringComparer.Ordinal),
        ];

        Assert.Equal(
            [
                .. Carried.Select(project => "lib/net10.0/" + project + ".dll")
                    .Concat(["lib/net10.0/Janus.Core.xml", "lib/net10.0/Janus.Hosting.xml"])
                    .Order(StringComparer.Ordinal),
            ],
            library);
        Assert.Equal(["Microsoft.AspNetCore.App"], main.FrameworkReferences);
    }

    /// <summary>
    /// LIB-PKG-001 AC4: the packages the main package declares as dependencies are exactly
    /// the packages its seven projects reference at run time, every reference whose assets
    /// are not all private, at the versions the central package list gives.
    /// </summary>
    [Fact]
    public void LIB_PKG_001_AC4_TheMainPackageDeclaresThePackagesItsProjectsReferenceAtRunTime() =>
        Assert.Equal(RunTimePackages(), packed.Packages[Main].Dependencies.OrderBy(pair => pair.Key, StringComparer.Ordinal));

    /// <summary>
    /// CONV-LAYOUT-001 AC5: the packages the main package declares as dependencies equal
    /// the packages its seven projects reference at run time, so a package an area takes
    /// reaches a host through the main package.
    /// </summary>
    [Fact]
    public void CONV_LAYOUT_001_AC5_TheMainPackagesDependenciesAreItsProjectsRunTimePackages() =>
        Assert.Equal(
            RunTimePackages().Select(pair => pair.Key),
            packed.Packages[Main].Dependencies.Keys.Order(StringComparer.Ordinal));

    /// <summary>
    /// LIB-PKG-001 AC4: the conformance suite depends on the main package at its own
    /// version, and no package declares a dependency on the analysers.
    /// </summary>
    [Fact]
    public void LIB_PKG_001_AC4_TheCompanionsDependOnTheMainPackageAndNoneOnTheAnalysers()
    {
        PackedPackage conformance = packed.Packages["Janus.Conformance"];

        Assert.Equal(conformance.Version, conformance.Dependencies[Main]);
        Assert.Equal(packed.Packages[Main].Version, conformance.Version);
        Assert.All(
            packed.Packages.Values,
            package => Assert.DoesNotContain("Janus.Analyzers", package.Dependencies.Keys));
    }

    /// <summary>
    /// CONV-LAYOUT-001 AC5: only the packable projects pack; the analysers pack as an
    /// analyser with nothing under <c>lib</c>, and the command-line application as a .NET
    /// tool whose command is <c>identity</c>.
    /// </summary>
    [Fact]
    public void CONV_LAYOUT_001_AC5_OnlyThePackableProjectsPack()
    {
        Assert.Equal(
            ["Janus.Analyzers", "Janus.Cli", "Janus.Conformance", Main],
            packed.Packages.Keys.Order(StringComparer.Ordinal));

        Assert.Equal(
            ["analyzers/dotnet/cs/Janus.Analyzers.dll"],
            packed.Packages["Janus.Analyzers"].Entries.Where(entry =>
                entry.StartsWith("analyzers/", StringComparison.Ordinal)
                || entry.StartsWith("lib/", StringComparison.Ordinal)));

        PackedPackage tool = packed.Packages["Janus.Cli"];

        Assert.Contains(
            tool.Manifest.Descendants(),
            element => element.Name.LocalName == "packageType" && element.Attribute("name")?.Value == "DotnetTool");
        Assert.Contains("tools/net10.0/any/DotnetToolSettings.xml", tool.Entries);
        Assert.Contains("tools/net10.0/any/Janus.Cli.dll", tool.Entries);
    }

    // Every package reference of the seven projects, and of the build files they inherit,
    // whose assets are not all private, with the version the central list gives it.
    private static IEnumerable<KeyValuePair<string, string>> RunTimePackages()
    {
        var versions = XDocument
            .Load(Path.Combine(Repository.Root(), "Directory.Packages.props"))
            .Descendants("PackageVersion")
            .ToDictionary(
                package => package.Attribute("Include")!.Value,
                package => package.Attribute("Version")!.Value,
                StringComparer.Ordinal);

        IEnumerable<string> files = Carried
            .Select(project => Path.Combine(Repository.Root(), "src", project, project + ".csproj"))
            .Concat([
                Path.Combine(Repository.Root(), "Directory.Build.props"),
                Path.Combine(Repository.Root(), "src", "Directory.Build.props"),
            ]);

        return files
            .SelectMany(file => XDocument.Load(file).Descendants("PackageReference"))
            .Where(reference => !string.Equals(
                reference.Attribute("PrivateAssets")?.Value,
                "all",
                StringComparison.OrdinalIgnoreCase))
            .Select(reference => reference.Attribute("Include")!.Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(package => KeyValuePair.Create(package, versions[package]));
    }
}
