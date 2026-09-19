using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// The shape of the solution: which project may depend on which, what a project file
/// may declare, and which packages the build may resolve
/// (CONV-LAYOUT-001, CONV-LAYOUT-002, CONV-LAYOUT-003, CONV-SETUP-001, CONV-SETUP-002,
/// CONV-DESIGN-003, CONV-DESIGN-004, CONV-DESIGN-008, CONV-CODE-008, LIB-PKG-001,
/// LIB-PKG-002, OPS-DATA-001, OPS-DATA-002).
/// </summary>
[Trait("kind", "contract")]
public sealed class LibraryStructureTests
{
    private static readonly Dictionary<string, string[]> Dependencies = new(StringComparer.Ordinal)
    {
        ["Janus.Core"] = [],
        ["Janus.Identity"] = ["Janus.Core"],
        ["Janus.Authentication"] = ["Janus.Core"],
        ["Janus.Authorization"] = ["Janus.Core"],
        ["Janus.Privacy"] = ["Janus.Core"],
        ["Janus.Storage"] = ["Janus.Authentication", "Janus.Authorization", "Janus.Core", "Janus.Identity", "Janus.Privacy"],
        ["Janus.Hosting"] = ["Janus.Authentication", "Janus.Authorization", "Janus.Core", "Janus.Identity", "Janus.Privacy", "Janus.Storage"],
        ["Janus.Conformance"] = ["Janus.Core", "Janus.Hosting"],
        ["Janus.Analyzers"] = [],
        ["Janus.UnicodeTables"] = [],
        ["Janus.Cli"] = ["Janus.Authentication", "Janus.Core", "Janus.Identity", "Janus.Storage"],
    };

    // CONV-LAYOUT-001: the library is under src and the generators are under tools,
    // outside the package. Both are held to the same conventions.
    private static readonly string[] Roots = ["src", "tools"];

    private static readonly string[] RawSql =
    [
        "FromSqlRaw",
        "FromSqlInterpolated",
        "ExecuteSqlRaw",
        "ExecuteSqlRawAsync",
        "ExecuteSqlInterpolated",
        "ExecuteSqlInterpolatedAsync",
        "SqlQueryRaw",
    ];

    // OPS-DATA-002: the accessor is the one place a connection comes from. These are
    // the ways a caller could get another one instead.
    private static readonly string[] DirectConnections =
    [
        "GetDbConnection",
        "new NpgsqlConnection",
        "OpenConnectionAsync",
        "OpenConnection(",
    ];

    private static readonly string[] Areas =
    [
        "Janus.Identity",
        "Janus.Authentication",
        "Janus.Authorization",
        "Janus.Privacy",
    ];

    // CONV-DESIGN-003: the tools an area must not reach for, whatever it wanted them
    // for. Persistence is the port's business and the port is an interface.
    private static readonly string[] Persistence =
    [
        "Microsoft.EntityFrameworkCore",
        "Microsoft.EntityFrameworkCore.Design",
        "Npgsql.EntityFrameworkCore.PostgreSQL",
        "Dapper",
    ];

    // CONV-DESIGN-004: a property a caller can assign is a property the type's own
    // methods no longer govern.
    private static readonly Regex Setter = new(
        @"\{\s*get;\s*(internal\s+)?set;",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    // CONV-DESIGN-004 AC2: a parameter of the underlying type where the library has a
    // type of its own for the thing.
    private static readonly Regex Untyped = new(
        @"[(,]\s*(Guid\s+[a-z]|string\s+(subject|organization|email|phone|username|address))",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    private static readonly string[] AllowedPackages =
    [
        "Dapper",
        "Fido2",
        "Konscious.Security.Cryptography.Argon2",
        "Microsoft.AspNetCore.Authentication.OpenIdConnect",
        "Microsoft.CodeAnalysis.Analyzers",
        "Microsoft.CodeAnalysis.CSharp",
        "Microsoft.CodeAnalysis.PublicApiAnalyzers",
        "Microsoft.EntityFrameworkCore",
        "Microsoft.EntityFrameworkCore.Design",
        "Microsoft.Testing.Platform",
        "MinVer",
        "Npgsql.EntityFrameworkCore.PostgreSQL",
        "OpenIddict.AspNetCore",
        "OpenIddict.Server",
        "OpenIddict.Validation",
        "Otp.NET",
        "StackExchange.Redis",
        "Testcontainers.PostgreSql",
        "Testcontainers.Redis",
        "xunit.v3",
    ];

    private static readonly string[] InheritedProperties =
    [
        "AnalysisLevel",
        "Deterministic",
        "EnforceCodeStyleInBuild",
        "GenerateDocumentationFile",
        "ImplicitUsings",
        "Nullable",
        "TargetFramework",
        "TargetFrameworks",
        "TreatWarningsAsErrors",
    ];

    /// <summary>
    /// CONV-LAYOUT-001 AC3: every project's library dependencies are exactly the ones
    /// the table gives, so a dependency pointing outward does not build.
    /// </summary>
    [Fact]
    public void CONV_LAYOUT_001_AC3_DependenciesAreExactlyTheOnesTheTableGives()
    {
        foreach ((string project, string[] expected) in Dependencies)
        {
            Assert.Equal(expected, LibraryReferences(project));
        }
    }

    /// <summary>
    /// CONV-LAYOUT-001 AC2 and LIB-PKG-002 AC1: the contracts compile with no database
    /// dependency, and with no package at all.
    /// </summary>
    [Fact]
    public void CONV_LAYOUT_001_AC2_CoreCarriesNoPackage() =>
        Assert.Empty(PackageReferences("Janus.Core"));

    /// <summary>
    /// LIB-PKG-001 AC2: no area reaches into another area.
    /// </summary>
    [Fact]
    public void LIB_PKG_001_AC2_NoAreaDependsOnAnotherArea()
    {
        foreach (string area in Areas)
        {
            Assert.Empty(LibraryReferences(area).Intersect(Areas, StringComparer.Ordinal));
        }
    }

    /// <summary>
    /// CONV-LAYOUT-002 AC1: the only grants of internal visibility are the ones the
    /// item permits.
    /// </summary>
    [Fact]
    public void CONV_LAYOUT_002_AC1_InternalsAreVisibleOnlyWhereThePermittedGrantsSay()
    {
        foreach (string project in Dependencies.Keys)
        {
            Assert.Equal(PermittedGrants(project), Grants(project));
        }
    }

    /// <summary>
    /// CONV-LAYOUT-003 AC1: a file's namespace matches its folder path.
    /// </summary>
    [Fact]
    public void CONV_LAYOUT_003_AC1_EveryNamespaceMatchesItsFolder()
    {
        foreach (string file in Sources())
        {
            string expected = Path
                .GetRelativePath(RootOf(file), Path.GetDirectoryName(file)!)
                .Replace(Path.DirectorySeparatorChar, '.');

            Assert.Contains(
                "namespace " + expected + ";",
                File.ReadAllText(file),
                StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// OPS-DATA-001 AC1: hand-written SQL goes through Dapper, so the raw-SQL facility
    /// of the other tool is not a second way of doing the same job.
    /// </summary>
    [Fact]
    public void OPS_DATA_001_AC1_NoFileUsesEfCoresRawSqlExecution()
    {
        IEnumerable<string> reaching = Sources()
            .Where(file => RawSql.Any(call =>
                File.ReadAllText(file).Contains(call, StringComparison.Ordinal)));

        Assert.Empty(reaching);
    }

    /// <summary>
    /// OPS-DATA-002 AC2: nothing but the accessor retrieves a connection, so a
    /// hand-written query can never run outside the operation's transaction.
    /// </summary>
    [Fact]
    public void OPS_DATA_002_AC2_NoFileButTheAccessorRetrievesAConnection()
    {
        IEnumerable<string> reaching = Sources()
            .Where(file => !string.Equals(
                Path.GetFileName(file),
                "DataConnections.cs",
                StringComparison.Ordinal))
            .Where(file => DirectConnections.Any(call =>
                File.ReadAllText(file).Contains(call, StringComparison.Ordinal)));

        Assert.Empty(reaching);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC1: no area reaches a database tool, so persistence can only
    /// leave an area through a port.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_003_AC1_NoAreaReferencesADatabasePackage()
    {
        foreach (string area in Areas)
        {
            Assert.Empty(PackageReferences(area).Intersect(Persistence, StringComparer.Ordinal));
        }
    }

    /// <summary>
    /// CONV-DESIGN-003 AC2: no port hands a query out of the area for someone else to
    /// finish, so what a port returns is what the caller gets. A queryable the host
    /// passes in from its own context is the opposite motion and is what a permission
    /// filter is built from (AUTHZ-GATE-002, D-159), so the rule is read over the port
    /// declarations, which is where a port's methods are.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_003_AC2_NoPortMethodReturnsAQueryable()
    {
        IEnumerable<string> handing = SourcesOf(Areas)
            .Select(file => new { File = file, Text = File.ReadAllText(file) })
            .Where(source => source.Text.Contains("internal interface", StringComparison.Ordinal))
            .Where(source => source.Text.Contains("IQueryable", StringComparison.Ordinal))
            .Select(source => source.File);

        Assert.Empty(handing);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC4: the field cipher is reached from a port implementation and
    /// from nowhere else, so no caller above the port holds a personal field's
    /// ciphertext and no caller below it holds the plaintext.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_003_AC4_OnlyAPortImplementationReachesTheFieldCipher()
    {
        IEnumerable<string> reaching = Sources()
            .Where(file => !IsCipherOrStore(file))
            .Where(file => File.ReadAllText(file).Contains("PersonalFieldCipher", StringComparison.Ordinal));

        Assert.Empty(reaching);
    }

    /// <summary>
    /// IDN-ATTR-003 AC2: the photo is in a table of its own and only its own port
    /// reaches it, so no ordinary read of an account carries image bytes.
    /// </summary>
    [Fact]
    public void IDN_ATTR_003_AC2_OnlyThePhotosOwnPortReachesTheImageTable()
    {
        IEnumerable<string> reaching = Sources()
            .Where(file => !Path.GetFileNameWithoutExtension(file).StartsWith(
                "ProfilePhoto",
                StringComparison.Ordinal))
            .Where(file => !Path.GetFileName(file).Equals("JanusDbContext.cs", StringComparison.Ordinal))
            .Where(file => File.ReadAllText(file).Contains(".ProfilePhotos", StringComparison.Ordinal));

        Assert.Empty(reaching);
    }

    /// <summary>
    /// CONV-DESIGN-004 AC1: no domain type exposes a setter, so its state changes only
    /// through the methods named for the actions that change it. The persistence
    /// records of `Janus.Storage` are columns rather than domain types and are what EF
    /// Core assigns (CONV-DESIGN-003).
    /// </summary>
    [Fact]
    public void CONV_DESIGN_004_AC1_NoDomainTypeExposesAPropertySetter()
    {
        IEnumerable<string> assignable = SourcesOf(Areas)
            .Where(file => Setter.IsMatch(File.ReadAllText(file)));

        Assert.Empty(assignable);
    }

    /// <summary>
    /// CONV-DESIGN-004 AC2: nothing outside the type that gives a value its rules takes
    /// that value as the type it is stored in.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_004_AC2_NoMethodTakesAValueAsItsUnderlyingType()
    {
        IEnumerable<string> taking = SourcesOf([.. Areas, "Janus.Storage"])
            .Where(file => Untyped.IsMatch(File.ReadAllText(file)));

        Assert.Empty(taking);
    }

    /// <summary>
    /// CONV-SETUP-001 AC1: no project file sets a target framework or any inherited
    /// property, except the analyser project, which must target netstandard2.0.
    /// </summary>
    [Fact]
    public void CONV_SETUP_001_AC1_NoProjectOverridesTheInheritedProperties()
    {
        foreach (string project in Projects().Where(project => !IsTheAnalyserProject(project)))
        {
            Assert.Empty(XDocument
                .Parse(File.ReadAllText(project))
                .Descendants("PropertyGroup")
                .Elements()
                .Select(element => element.Name.LocalName)
                .Intersect(InheritedProperties, StringComparer.Ordinal));
        }
    }

    /// <summary>
    /// CONV-SETUP-002 AC1: no project file carries a package version.
    /// </summary>
    [Fact]
    public void CONV_SETUP_002_AC1_NoProjectFileCarriesAPackageVersion()
    {
        foreach (string project in Projects())
        {
            Assert.DoesNotContain(
                XDocument.Parse(File.ReadAllText(project)).Descendants("PackageReference"),
                reference => reference.Attribute("Version") is not null);
        }
    }

    /// <summary>
    /// CONV-DESIGN-008 AC1: the set of package identifiers the build may resolve is
    /// exactly the table's.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_008_AC1_ThePackageSetIsExactlyTheAllowList()
    {
        string[] declared = XDocument
            .Parse(Repository.ReadText("Directory.Packages.props"))
            .Descendants("PackageVersion")
            .Select(package => package.Attribute("Include")!.Value)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(AllowedPackages, declared);
    }

    /// <summary>
    /// CONV-CODE-008 AC2: every source project reads the analysers as analysers, and
    /// none of them links the analyser assembly.
    /// </summary>
    [Fact]
    public void CONV_CODE_008_AC2_TheAnalyserProjectIsReferencedAsAnAnalyser()
    {
        foreach (string project in Projects().Where(project => !IsTheAnalyserProject(project)))
        {
            XElement reference = Assert.Single(
                XDocument.Parse(File.ReadAllText(project)).Descendants("ProjectReference"),
                candidate => candidate.Attribute("Include")!.Value.EndsWith("Janus.Analyzers.csproj", StringComparison.Ordinal));

            Assert.Equal("Analyzer", reference.Attribute("OutputItemType")?.Value);
            Assert.Equal("false", reference.Attribute("ReferenceOutputAssembly")?.Value);
        }
    }

    /// <summary>
    /// The project a reference names. A project file writes its includes with the
    /// separator of the machine that wrote them, which is not the separator of the
    /// machine reading them.
    /// </summary>
    private static string Referenced(string include) =>
        Path.GetFileNameWithoutExtension(include.Replace('\\', '/'));

    private static bool IsTheAnalyserProject(string project) =>
        string.Equals(Path.GetFileNameWithoutExtension(project), "Janus.Analyzers", StringComparison.Ordinal);

    private static IEnumerable<string> Projects() =>
        Roots.SelectMany(root =>
            Directory.EnumerateFiles(Path.Combine(Repository.Root, root), "*.csproj", SearchOption.AllDirectories));

    // The cipher itself and the port implementations that run it. A store is named for
    // what it stores, so the suffix is the whole rule.
    private static bool IsCipherOrStore(string file) =>
        Path.GetFileName(file) is "PersonalFieldCipher.cs" or "PersonalFieldLocation.cs"
            || Path.GetFileNameWithoutExtension(file).EndsWith("Store", StringComparison.Ordinal);

    private static IEnumerable<string> SourcesOf(string[] projects) =>
        projects
            .Select(project => Path.Combine(Repository.Root, "src", project) + Path.DirectorySeparatorChar)
            .SelectMany(folder => Sources().Where(file =>
                file.StartsWith(folder, StringComparison.Ordinal)));

    private static IEnumerable<string> Sources() =>
        Roots
            .SelectMany(root =>
                Directory.EnumerateFiles(Path.Combine(Repository.Root, root), "*.cs", SearchOption.AllDirectories))
            .Where(file => !file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal));

    private static string RootOf(string file) =>
        Roots
            .Select(root => Path.Combine(Repository.Root, root))
            .Single(root => file.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal));

    private static bool IsLibrary(string project) =>
        string.Equals(RootOf(Project(project)), Path.Combine(Repository.Root, "src"), StringComparison.Ordinal);

    private static string Project(string project) =>
        Projects().Single(file =>
            string.Equals(Path.GetFileNameWithoutExtension(file), project, StringComparison.Ordinal));

    private static string[] LibraryReferences(string project) =>
        XDocument
            .Parse(File.ReadAllText(Project(project)))
            .Descendants("ProjectReference")
            .Where(reference => reference.Attribute("OutputItemType") is null)
            .Select(reference => Referenced(reference.Attribute("Include")!.Value))
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static string[] PackageReferences(string project) =>
        XDocument
            .Parse(File.ReadAllText(Project(project)))
            .Descendants("PackageReference")
            .Select(reference => reference.Attribute("Include")!.Value)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static string[] Grants(string project) =>
        XDocument
            .Parse(File.ReadAllText(Project(project)))
            .Descendants("InternalsVisibleTo")
            .Select(grant => grant.Attribute("Include")!.Value)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static string[] PermittedGrants(string project)
    {
        var permitted = new List<string>();

        // CONV-LAYOUT-002 permits a source project other than Core, whose surface is
        // public already, to open its internals to its own test project (CONV-TEST-001).
        // The generator of CONV-LAYOUT-001 is not a source project: it lives outside the
        // package and the gate that regenerates its output is what covers it.
        if (!string.Equals(project, "Janus.Core", StringComparison.Ordinal) && IsLibrary(project))
        {
            permitted.Add(project + ".Tests");
        }

        // An area opens its internals to Janus.Storage for the port implementations and
        // to Janus.Storage.Tests, where a port implementation is tested against the
        // aggregate it translates (D-156), and to the two projects that register it.
        if (Areas.Contains(project, StringComparer.Ordinal))
        {
            permitted.AddRange(
                ["Janus.Storage", "Janus.Storage.Tests", "Janus.Hosting", "Janus.Cli"]);
        }

        if (string.Equals(project, "Janus.Core", StringComparison.Ordinal)
            || string.Equals(project, "Janus.Storage", StringComparison.Ordinal))
        {
            permitted.AddRange(["Janus.Hosting", "Janus.Cli"]);
        }

        return [.. permitted.Order(StringComparer.Ordinal)];
    }
}
