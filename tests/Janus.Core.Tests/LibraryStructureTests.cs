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
/// and the style configuration may declare, what a folder may be named, which packages
/// the build may resolve, and which kind each test class is
/// (CONV-LAYOUT-001, CONV-LAYOUT-002, CONV-LAYOUT-003, CONV-SETUP-001, CONV-SETUP-002,
/// CONV-SETUP-004, CONV-DESIGN-001, CONV-DESIGN-003, CONV-DESIGN-004, CONV-DESIGN-007,
/// CONV-DESIGN-008, CONV-CODE-008, CONV-TEST-002, CONV-VCS-005, LIB-API-002,
/// LIB-PKG-001, LIB-PKG-002, OPS-DATA-001, OPS-DATA-002).
/// </summary>
[Trait("kind", "contract")]
public sealed class LibraryStructureTests
{
    // LIB-API-002 AC2: the project the sample host is written in.
    private const string Sample = "Janus.Conformance.Tests";

    // CONV-TEST-002 AC1: what makes an instance of one of the types named, which is
    // where a container would start: asking the framework for it as a fixture, or
    // constructing it. A call to one of its static members makes nothing.
    private const string Creates =
        @"\bI(?:Class|Collection)Fixture<\s*(?:NAMES)\s*>|\bAssemblyFixture\(\s*typeof\(\s*(?:NAMES)\s*\)\)|\bnew\s+(?:NAMES)\b|\b(?:NAMES)\??\s+\w+\s*=\s*new\s*\(";

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
        @"[(,]\s*(Guid\s+[a-z]|string\s+(subject|organization|email|phone|username|address)\b)",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    // CONV-DESIGN-004 AC2: a member that implements another package's interface takes
    // what that interface declares, which no type of the library's can change.
    // OpenIddict's stores name the OIDC subject as text.
    private static readonly Regex Foreign = new(
        @":\s*IOpenIddict\w+Store<",
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

    // CONV-SETUP-004 AC2: the folders every project lives in. With the root above them
    // they are every directory the build reads a configuration from.
    private static readonly string[] Built = ["src", "tests", "tools"];

    // CONV-SETUP-004 AC2: what configures a rule when a project, props or targets file
    // declares it, as a property, an item or an item's metadata: a rule's severity or
    // suppression, a rule set, a further configuration file, whether the analysers run,
    // or a mode or a level for one category of rules.
    private static readonly Regex RuleConfiguration = new(
        @"^(NoWarn|WarningsAsErrors|WarningsNotAsErrors|CodeAnalysisRuleSet|GlobalAnalyzerConfigFiles|EditorConfigFiles|RunAnalyzers\w*|Analysis(Mode\w*|Level\w+))$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    // CONV-SETUP-004 AC3: a severity the .editorconfig sets, through a rule's own key,
    // for a category or every rule, for a naming rule, or after a style option's value.
    private static readonly Regex Severity = new(
        @"^(?:dotnet_diagnostic\.(?<rule>[^.\s]+)|(?<rule>(?:dotnet_analyzer_diagnostic|dotnet_naming_rule)(?:\.[^.\s]+)?))\.severity\s*=\s*(?<severity>\w+)|^(?<rule>\w+)\s*=\s*[^:\s]+:(?<severity>\w+)",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    // CONV-VCS-005 AC2: the properties through which a project would state a version
    // of its own where MinVer derives it from the tag.
    private static readonly string[] VersionProperties =
    [
        "AssemblyVersion",
        "FileVersion",
        "InformationalVersion",
        "MinVerVersionOverride",
        "PackageVersion",
        "Version",
        "VersionPrefix",
        "VersionSuffix",
    ];

    // CONV-DESIGN-001: the names of horizontal layers. A feature folder is named for
    // the feature, and none of these is one.
    private static readonly string[] TechnicalRoles =
    [
        "Abstractions",
        "Common",
        "Contracts",
        "Controllers",
        "Dtos",
        "Entities",
        "Enums",
        "Exceptions",
        "Extensions",
        "Factories",
        "Handlers",
        "Helpers",
        "Infrastructure",
        "Interfaces",
        "Managers",
        "Mappers",
        "Mappings",
        "Models",
        "Repositories",
        "Services",
        "Shared",
        "Utilities",
        "Utils",
        "Validators",
    ];

    // CONV-DESIGN-007 AC2: the wall clock read in place of the TimeProvider a service
    // is given, and the one random source that is not the RandomNumberGenerator.
    private static readonly Regex Ambient = new(
        @"\bDateTime(Offset)?\s*\.\s*(Now|UtcNow|Today)\b|\bRandom\b",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    // CONV-TEST-002: the four kinds the chapter keeps apart.
    private static readonly string[] Kinds = ["conformance", "contract", "integration", "unit"];

    // CONV-TEST-002: the kind a test class carries.
    private static readonly Regex Kind = new(
        @"\[Trait\(""kind"",\s*""(\w+)""\)\]",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    // CONV-TEST-002: what makes a type a test class.
    private static readonly Regex Test = new(
        @"\[(Fact|Theory)\b",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    // CONV-TEST-002 AC3: the kind a pipeline job runs.
    private static readonly Regex Filtered = new(
        @"--filter-trait\s+""kind=(\w+)""",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    // CONV-TEST-002 AC1: a reach into the container package, by a using directive or a
    // qualified name. The package's identifier inside a string names it and uses nothing.
    private static readonly Regex Container = new(
        @"(?<![\w"".])(?:DotNet\.)?Testcontainers\.\w",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    // A comment to the end of its line, which names what it likes and uses nothing.
    private static readonly Regex Comment = new(
        @"//.*",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

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
    /// LIB-API-002 AC2: the sample host lives in the conformance package's test project,
    /// and no project of the library opens its internals to it, so what the sample
    /// compiles against is the public surface alone.
    /// </summary>
    [Fact]
    public void LIB_API_002_AC2_NoProjectOpensItsInternalsToTheSampleHost()
    {
        foreach (string project in Dependencies.Keys)
        {
            Assert.DoesNotContain(Sample, Grants(project));
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
    /// CONV-DESIGN-001 AC1: no folder of an area, at any depth, is a horizontal layer,
    /// so each is a feature holding its types, its service and its port together.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_001_AC1_NoFolderOfAnAreaIsNamedForATechnicalRole()
    {
        IEnumerable<string> horizontal = Areas
            .SelectMany(area => Directory.EnumerateDirectories(
                Path.Combine(Repository.Root, "src", area),
                "*",
                SearchOption.AllDirectories))
            .Where(folder => !IsBuildOutput(folder))
            .Where(folder => TechnicalRoles.Contains(Path.GetFileName(folder), StringComparer.OrdinalIgnoreCase));

        Assert.Empty(horizontal);
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
    /// OPS-CFG-002, OPS-CFG-005: one operation writes a runtime setting, so a change
    /// that went round it would be a change nobody was told of and nobody had to answer
    /// for. Nothing else in the library calls the store's write, and the settings table
    /// is written by the store and by the writer of protected keys alone, which bootstrap
    /// and the change from the server reach, each recording what it sets (OPS-BOOT-001,
    /// OPS-CFG-004, entries 315 and 319).
    /// </summary>
    [Fact]
    public void OPS_CFG_002_OnlyTheConfigurationAdministrationWritesARuntimeSetting()
    {
        IEnumerable<string> writing = Sources()
            .Where(file => !Path.GetFileNameWithoutExtension(file).Equals(
                "ConfigurationAdministration",
                StringComparison.Ordinal))
            .Select(file => (File: file, Text: File.ReadAllText(file)))
            .Where(one => Written(one.Text))
            .Select(one => one.File);

        Assert.Empty(writing);
        Assert.Equal(
            ["ConfigurationStore.cs", "ProtectedSettings.cs"],
            Named(text => Regex.IsMatch(text, @"\bSettings\s*\.\s*Add\(", RegexOptions.None, TimeSpan.FromSeconds(5))));
    }

    /// <summary>
    /// OPS-CFG-004 AC1 and AC2, D-071: a protected key is written by bootstrap and by the
    /// change from the server alone, and the change from the server is run by the command
    /// line alone, so no endpoint of the management application and no job of the worker
    /// reaches either.
    /// </summary>
    [Fact]
    public void OPS_CFG_004_AC2_OnlyTheCommandLineWritesAProtectedKey()
    {
        Assert.Equal(
            ["DeploymentSeed.cs", "IProtectedSettings.cs", "ProtectedConfiguration.cs", "ProtectedSettings.cs", "StorageRegistration.cs"],
            Named(text => Regex.IsMatch(text, @"\bIProtectedSettings\b", RegexOptions.None, TimeSpan.FromSeconds(5))));
        Assert.Equal(
            ["ConfigureCommand.cs", "ProtectedConfiguration.cs"],
            Named(text => Regex.IsMatch(text, @"\bProtectedConfiguration\b", RegexOptions.None, TimeSpan.FromSeconds(5))));
    }

    /// <summary>
    /// DR-009a AC5: the key-encryption key is rotated through OPS-SEC-003 and by no other
    /// path. The principal a rotation runs under is made by the rotations alone, and
    /// nothing the host mounts, the worker runs or the conformance suite drives names a
    /// rotation at all.
    /// </summary>
    [Fact]
    public void DR_009a_AC5_NoPathButTheCommandRotatesTheKey()
    {
        Assert.Equal(
            ["FingerprintKeyRotation.cs", "KeyRotation.cs"],
            Named(text => Regex.IsMatch(text, @"\bSystemOperation\.KeyRotation\b", RegexOptions.None, TimeSpan.FromSeconds(5))));
        Assert.DoesNotContain(
            Sources(),
            file => ((string[])["Janus.Hosting", "Janus.Conformance"]).Any(project => file.StartsWith(
                    Path.Combine(Repository.Root, "src", project) + Path.DirectorySeparatorChar,
                    StringComparison.Ordinal))
                && Regex.IsMatch(
                    File.ReadAllText(file),
                    @"\b(KeyRotation|FingerprintKeyRotation|IKeyRotationStore|RotateKeyEncryptionKeyCommand)\b",
                    RegexOptions.None,
                    TimeSpan.FromSeconds(5)));
    }

    /// <summary>
    /// DR-006a AC2: no erasure procedure attempts to modify a backup. The library takes
    /// and writes no backup, and the one port through which it reaches one is the restore
    /// test's, which restores a copy into a throwaway instance; nothing that erases names
    /// it.
    /// </summary>
    [Fact]
    public void DR_006a_AC2_NoErasureProcedureReachesABackup()
    {
        Assert.Equal(
            ["HostingRegistration.cs", "IRestoreTestInstance.cs", "RestoreTest.cs"],
            Named(text => Regex.IsMatch(text, @"\bIRestoreTestInstance\b", RegexOptions.None, TimeSpan.FromSeconds(5))));
        Assert.Empty(Named(text => Regex.IsMatch(
            text,
            @"\bpg_(dump|dumpall|restore|basebackup)\b",
            RegexOptions.None,
            TimeSpan.FromSeconds(5))));
    }

    /// <summary>
    /// OPS-SEC-003 AC1 and AC6, DR-009a AC5: the rotations of the key-encryption key and
    /// of the fingerprint key are run by the command line and by nothing else, so no
    /// endpoint of the management application, no job of the worker and no host reaches
    /// either. The fingerprint rotation reads the batch size of the other and keeps its
    /// progress in the same table.
    /// </summary>
    [Fact]
    public void OPS_SEC_003_AC1_OnlyTheCommandLineRunsTheRotation()
    {
        Assert.Equal(
            ["FingerprintKeyRotation.cs", "KeyRotation.cs", "RotateKeyEncryptionKeyCommand.cs"],
            Named(text => Regex.IsMatch(text, @"(?<!SystemOperation\.)\bKeyRotation\b(?!\s*=\s*\d)", RegexOptions.None, TimeSpan.FromSeconds(5))));
        Assert.Equal(
            [
                "FingerprintKeyRotation.cs",
                "IKeyRotationStore.cs",
                "KeyRotation.cs",
                "KeyRotationStore.cs",
                "RotateFingerprintKeyCommand.cs",
                "RotateKeyEncryptionKeyCommand.cs",
            ],
            Named(text => Regex.IsMatch(text, @"\bIKeyRotationStore\b", RegexOptions.None, TimeSpan.FromSeconds(5))));
        Assert.Equal(
            [
                "FingerprintKeyRotation.cs",
                "FingerprintRotationStore.cs",
                "IFingerprintRotationStore.cs",
                "RotateFingerprintKeyCommand.cs",
            ],
            Named(text => Regex.IsMatch(
                text,
                @"\b(FingerprintKeyRotation|IFingerprintRotationStore)\b",
                RegexOptions.None,
                TimeSpan.FromSeconds(5))));
    }

    /// <summary>
    /// IDN-LIFE-009a AC1: a membership is made in one place, the attachment of an
    /// acknowledged invitation, and the acknowledgement is the one thing that runs it,
    /// beside bootstrap, which makes the first administrator's (OPS-BOOT-001,
    /// INT-MAIL-006 AC1a, entry 313); a grant, or any other path, makes none.
    /// </summary>
    [Fact]
    public void IDN_LIFE_009a_AC1_OnlyAnAcknowledgedInvitationMakesAMembership()
    {
        string[] making =
        [
            .. Named(text =>
                Regex.IsMatch(text, @"\bMembership\s*\.\s*Create\(", RegexOptions.None, TimeSpan.FromSeconds(5))
                || Called(text, "IMembershipStore", "CreateAsync")),
        ];

        Assert.Equal(["MembershipAttachment.cs"], making);
        Assert.Equal(
            [
                "DeploymentBootstrap.cs",
                "IMembershipAttachment.cs",
                "InvitationAcknowledgement.cs",
                "MembershipAttachment.cs",
                "StorageRegistration.cs",
            ],
            Named(text => text.Contains("MembershipAttachment", StringComparison.Ordinal)));
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
            .Where(file => !Path.GetFileName(file).Equals("StoreContext.cs", StringComparison.Ordinal))
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
            .Select(file => (File: file, Text: File.ReadAllText(file)))
            .Where(one => Untyped.IsMatch(one.Text) && !Foreign.IsMatch(one.Text))
            .Select(one => one.File);

        Assert.Empty(taking);
    }

    /// <summary>
    /// CONV-DESIGN-007 AC2: nothing outside the tests reads the wall clock or makes a
    /// <c>Random</c>, so time comes from the injected TimeProvider and randomness from
    /// the injected RandomNumberGenerator.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_007_AC2_NoFileOutsideTheTestsReadsTheClockOrMakesARandom()
    {
        IEnumerable<string> reading = Sources()
            .Where(file => Ambient.IsMatch(File.ReadAllText(file)));

        Assert.Empty(reading);
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
    /// CONV-SETUP-004 AC2: the .editorconfig at the root is the one file that configures
    /// a rule, and no project, props or targets file configures one beside it.
    /// </summary>
    [Fact]
    public void CONV_SETUP_004_AC2_TheEditorconfigIsTheOnlyPlaceARuleIsConfigured()
    {
        string[] configurations =
        [
            .. BuildTree()
                .Where(file => Path.GetExtension(file) is ".editorconfig" or ".globalconfig" or ".ruleset")
                .Select(file => Path.GetRelativePath(Repository.Root, file)),
        ];
        IEnumerable<string> configuring = BuildFiles()
            .Where(file => XDocument
                .Parse(File.ReadAllText(file))
                .Descendants()
                .SelectMany(element => element
                    .Attributes()
                    .Select(attribute => attribute.Name.LocalName)
                    .Prepend(element.Name.LocalName))
                .Any(RuleConfiguration.IsMatch));

        Assert.Equal([".editorconfig"], configurations);
        Assert.Empty(configuring);
    }

    /// <summary>
    /// CONV-SETUP-004 AC3, the configuration: the .editorconfig sets below error the five
    /// rules of the table, each where the table scopes it, and no other rule.
    /// </summary>
    [Fact]
    public void CONV_SETUP_004_AC3_NoRuleButTheFiveOfTheTableIsSetBelowError() =>
        Assert.Equal(
            [
                "*.cs CA1062 none",
                "src/**.cs CA1812 none",
                "src/{Janus.Core,Janus.Hosting,Janus.Conformance}/**.cs CA1515 none",
                "{tests,src/Janus.Hosting,src/Janus.Cli}/**.cs CA2007 none",
                "tests/**.cs CA1707 none",
                "tests/**.cs CA1515 none",
            ],
            BelowError(Repository.ReadText(".editorconfig")));

    /// <summary>
    /// CONV-VCS-005 AC2: no project, props or targets file states a version, and the
    /// version comes from MinVer, which every project inherits.
    /// </summary>
    [Fact]
    public void CONV_VCS_005_AC2_NoProjectFileCarriesAVersion()
    {
        IEnumerable<string> versioned = BuildFiles()
            .Where(file => XDocument
                .Parse(File.ReadAllText(file))
                .Descendants("PropertyGroup")
                .Elements()
                .Any(property => VersionProperties.Contains(property.Name.LocalName, StringComparer.Ordinal)));

        Assert.Contains(
            XDocument.Parse(Repository.ReadText("Directory.Build.props")).Descendants("PackageReference"),
            reference => string.Equals(reference.Attribute("Include")?.Value, "MinVer", StringComparison.Ordinal));
        Assert.Empty(versioned);
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
    /// CONV-TEST-002 AC1: no test class of the unit or contract kind takes as a fixture,
    /// or constructs, a type that starts a container, itself or through a type it makes,
    /// so both kinds run where no container can.
    /// </summary>
    [Fact]
    public void CONV_TEST_002_AC1_NoUnitOrContractClassUsesAContainer()
    {
        string[] starting = Starting();
        Regex starts = Creating(starting);

        IEnumerable<string> contained = TestClasses()
            .Where(test => test.Kinds.Any(kind => kind is "unit" or "contract"))
            .Where(test => starts.IsMatch(test.Code))
            .Select(test => test.Name);

        Assert.NotEmpty(starting);
        Assert.Empty(contained);
    }

    /// <summary>
    /// CONV-TEST-002 AC3: every test class carries one kind of the four, and a job of
    /// the pipeline runs that kind on its own, so no class goes unrun for carrying no
    /// kind and none runs with two.
    /// </summary>
    [Fact]
    public void CONV_TEST_002_AC3_EveryTestClassCarriesOneKindThatAJobRunsOnItsOwn()
    {
        string[] run =
        [
            .. Filtered
                .Matches(Repository.ReadText(".github/workflows/gates.yml"))
                .Select(match => match.Groups[1].Value),
        ];

        IEnumerable<string> unrun = TestClasses()
            .Where(test => test.Kinds.Length != 1 || !run.Contains(test.Kinds[0], StringComparer.Ordinal))
            .Select(test => test.Name);

        Assert.Subset(Kinds.ToHashSet(StringComparer.Ordinal), run.ToHashSet(StringComparer.Ordinal));
        Assert.Empty(unrun);
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

    private static bool Written(string text) => Called(text, "IConfigurationStore", "WriteAsync");

    // Whichever name a class gives the port it holds, a call through it is that name
    // followed by the port's method.
    private static bool Called(string text, string port, string method) =>
        Regex.Matches(text, port + @"\s+(\w+)", RegexOptions.None, TimeSpan.FromSeconds(5))
            .Select(match => match.Groups[1].Value)
            .Any(held => Regex.IsMatch(
                text,
                @"\b" + Regex.Escape(held) + @"\s*\." + method + @"\(",
                RegexOptions.None,
                TimeSpan.FromSeconds(5)));

    // The files of the library whose text answers yes, by name.
    private static string[] Named(Func<string, bool> answers) =>
    [
        .. Sources()
            .Where(file => file.StartsWith(
                Path.Combine(Repository.Root, "src") + Path.DirectorySeparatorChar,
                StringComparison.Ordinal))
            .Where(file => answers(File.ReadAllText(file)))
            .Select(Path.GetFileName)
            .Select(name => name!)
            .Order(StringComparer.Ordinal),
    ];

    private static IEnumerable<string> Sources() =>
        Roots
            .SelectMany(root =>
                Directory.EnumerateFiles(Path.Combine(Repository.Root, root), "*.cs", SearchOption.AllDirectories))
            .Where(file => !IsBuildOutput(file));

    // A path under a folder a build writes, which holds nothing anyone committed.
    private static bool IsBuildOutput(string path) =>
        Path.GetRelativePath(Repository.Root, path)
            .Split(Path.DirectorySeparatorChar)
            .Any(segment => segment is "bin" or "obj");

    // Every file of the build: the root's own files and everything under the folders
    // the projects live in.
    private static IEnumerable<string> BuildTree() =>
        Directory
            .EnumerateFiles(Repository.Root)
            .Concat(Built.SelectMany(folder =>
                Directory.EnumerateFiles(Path.Combine(Repository.Root, folder), "*", SearchOption.AllDirectories)))
            .Where(file => !IsBuildOutput(file));

    private static IEnumerable<string> BuildFiles() =>
        BuildTree().Where(file => Path.GetExtension(file) is ".csproj" or ".props" or ".targets");

    // What an .editorconfig sets below error, each as its section, the rule and the
    // severity, in the order the file sets them.
    private static string[] BelowError(string configuration)
    {
        var below = new List<string>();
        string section = string.Empty;

        foreach (string line in configuration.ReplaceLineEndings("\n").Split('\n').Select(entry => entry.Trim()))
        {
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line[1..^1];
            }
            else if (Severity.Match(line) is { Success: true } setting
                && !string.Equals(setting.Groups["severity"].Value, "error", StringComparison.OrdinalIgnoreCase))
            {
                below.Add(section + " " + setting.Groups["rule"].Value + " " + setting.Groups["severity"].Value);
            }
        }

        return [.. below];
    }

    // The source files of the test projects, as code with the comments taken out.
    private static IEnumerable<(string File, string Code)> TestSources() =>
        Directory
            .EnumerateFiles(Path.Combine(Repository.Root, "tests"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !IsBuildOutput(file))
            .Select(file => (file, Comment.Replace(File.ReadAllText(file), string.Empty)));

    // The type a file declares: its name up to the first dot, which is the whole name
    // but for the further files of a partial type.
    private static string TypeOf(string file) => Path.GetFileName(file).Split('.')[0];

    // Every test class, named by its path under the root, with the kinds that all of
    // its files give it and the code of all of them.
    private static IEnumerable<(string Name, string[] Kinds, string Code)> TestClasses() =>
        TestSources()
            .GroupBy(source => Path.Combine(Path.GetDirectoryName(source.File)!, TypeOf(source.File)))
            .Where(type => type.Any(source => Test.IsMatch(source.Code)))
            .Select(type => (
                Path.GetRelativePath(Repository.Root, type.Key),
                type.SelectMany(source => Kind.Matches(source.Code).Select(match => match.Groups[1].Value)).ToArray(),
                string.Join('\n', type.Select(source => source.Code))));

    // The types of the test projects that start a container: those that reach the
    // container package, then every type that makes an instance of one of those, until
    // no more are reached. A type is known by its name, which is how the code making
    // one reads.
    private static string[] Starting()
    {
        ILookup<string, string> code = TestSources()
            .ToLookup(source => TypeOf(source.File), source => source.Code, StringComparer.Ordinal);
        HashSet<string> starting = [.. code.Where(type => type.Any(Container.IsMatch)).Select(type => type.Key)];
        string[] reached = [.. starting];

        while (reached.Length > 0)
        {
            Regex creates = Creating(reached);

            reached = [.. code.Where(type => !starting.Contains(type.Key) && type.Any(creates.IsMatch)).Select(type => type.Key)];
            starting.UnionWith(reached);
        }

        return [.. starting];
    }

    private static Regex Creating(string[] types) =>
        new(
            Creates.Replace("NAMES", string.Join('|', types.Select(Regex.Escape)), StringComparison.Ordinal),
            RegexOptions.None,
            TimeSpan.FromSeconds(5));

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
        // package and the gate that regenerates its output is what covers it. The
        // conformance package's test project holds the sample host, which reads no
        // internal type (LIB-API-002 AC2), so that one grant is not made.
        if (!string.Equals(project, "Janus.Core", StringComparison.Ordinal)
            && IsLibrary(project)
            && !string.Equals(project + ".Tests", Sample, StringComparison.Ordinal))
        {
            permitted.Add(project + ".Tests");
        }

        // An area opens its internals to Janus.Storage for the port implementations and
        // to Janus.Storage.Tests, where a port implementation is tested against the
        // aggregate it translates (D-156), and to the two projects that register it.
        // Janus.Hosting implements ports of its own, so its test project reads the same
        // internals for the same reason.
        if (Areas.Contains(project, StringComparer.Ordinal))
        {
            permitted.AddRange([
                "Janus.Storage",
                "Janus.Storage.Tests",
                "Janus.Hosting",
                "Janus.Hosting.Tests",
                "Janus.Cli",
            ]);
        }

        if (string.Equals(project, "Janus.Core", StringComparison.Ordinal)
            || string.Equals(project, "Janus.Storage", StringComparison.Ordinal))
        {
            permitted.AddRange(["Janus.Hosting", "Janus.Cli"]);
        }

        // Janus.Storage holds the rows the protocol server keeps its own records in,
        // so the project that stands the server up reads them and its test project
        // stands the same server up over fakes of them (AUTH-OIDC-001, D-162).
        if (string.Equals(project, "Janus.Storage", StringComparison.Ordinal))
        {
            permitted.Add("Janus.Hosting.Tests");
        }

        return [.. permitted.Order(StringComparer.Ordinal)];
    }
}
