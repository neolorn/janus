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
/// the build may resolve, how a secret is compared, what an exception may carry, how a
/// catch block ends, and which kind each test class is
/// (CONV-LAYOUT-001, CONV-LAYOUT-002, CONV-LAYOUT-003, CONV-SETUP-001, CONV-SETUP-002,
/// CONV-SETUP-004, CONV-DESIGN-001, CONV-DESIGN-003, CONV-DESIGN-004, CONV-DESIGN-007,
/// CONV-DESIGN-008, CONV-CODE-007, CONV-CODE-008, CONV-ERR-001, CONV-ERR-003,
/// CONV-TEST-002, CONV-VCS-005, LIB-API-002, LIB-PKG-001, LIB-PKG-002, OPS-DATA-001,
/// OPS-DATA-002, OPS-DATA-003).
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

    // CONV-CODE-007 AC1: a member chain as an operand of a comparison writes it, each
    // step carrying the arguments of a call or an index where it has them. It starts
    // where a name starts, never inside one.
    private const string Chain =
        @"(?<!\w)[A-Za-z_]\w*(?:\([^()]*\)|\[[^\]]*\])?(?:\s*[?!]?\.\s*[A-Za-z_]\w*(?:\([^()]*\)|\[[^\]]*\])?)*";

    // CONV-CODE-007 AC1: a literal as an operand of a comparison writes it: text, a
    // character or a number.
    private const string Literals = @"(?:""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)*'|(?<!\w)\d\w*)";

    // CONV-CODE-007 AC1: one operand of a comparison.
    private const string Operand = "(?:" + Literals + "|" + Chain + ")";

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
    // the ways a caller could get another one instead: the context's own, one a data
    // source or a factory opens, or one constructed, which names its type whether the
    // type follows `new` or the declaration it is constructed into.
    private static readonly Regex DirectConnection = new(
        @"\b(GetDbConnection|OpenConnection(Async)?|CreateConnection)\s*\(|\bNpgsql(Connection|DataSource)\b",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    // OPS-DATA-003: the database features direct ADO.NET use is reserved for, in the
    // item's words.
    private static readonly string[] DirectFeatures =
    [
        "binary bulk copy",
        "advisory locks",
        "listen/notify",
        "server-side cursors",
    ];

    // OPS-DATA-003: going below Dapper and EF Core to ADO.NET is naming a provider's
    // connection, command, batch, reader, data source or copy, or the provider-neutral
    // command, batch or reader, or calling what makes or runs a command or starts a copy.
    private static readonly Regex DirectAdoNet = new(
        @"\b(Npgsql(Connection|Command|Batch|DataReader|DataSource|BinaryImporter|BinaryExporter)|Db(Command|Batch|DataReader))\b"
            + @"|\b(CreateCommand|ExecuteNonQuery(Async)?|Begin(Binary|Text|RawBinary)(Import|Export|Copy))\s*\(",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

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

    // CONV-CODE-007 AC1: the ways a comparison is written that are not constant-time:
    // the equality operators, an instance's equality or sequence comparison, and the
    // static comparisons of text, objects and sequences.
    private static readonly Regex[] Comparisons =
    [
        new(
            "(?<left>" + Operand + @")\s*[!=]=\s*(?<right>" + Operand + ")",
            RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(5)),
        new(
            "(?<left>" + Chain + @")\s*\.\s*(?:Equals|SequenceEqual|SequenceCompareTo|CompareTo)\s*\(\s*(?<right>" + Operand + ")",
            RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(5)),
        new(
            @"(?<![\w.])(?:(?:string|object|MemoryExtensions|Enumerable|StringComparer\s*\.\s*\w+|EqualityComparer<[^>]+>\s*\.\s*Default)\s*\.\s*)?"
                + @"(?:Equals|Compare|CompareOrdinal|SequenceEqual)\s*\(\s*(?<left>" + Operand + @")\s*,\s*(?<right>" + Operand + ")",
            RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(5)),
    ];

    // CONV-CODE-007 AC1: an operand written as a literal, which is compiled in and
    // secret to nobody.
    private static readonly Regex Literal = new(
        "^" + Literals + "$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    // CONV-CODE-007 AC1: a name a step of a member chain is called by or handed.
    private static readonly Regex Identifier = new(
        @"[A-Za-z_]\w*",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    // CONV-CODE-007 AC1: the words of an identifier, split where its case changes.
    private static readonly Regex Word = new(
        @"[A-Z]?[a-z]+|[A-Z]+(?![a-z])|\d+",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    // CONV-CODE-007 AC1: a constant a file declares, which is compiled in and secret to
    // nobody.
    private static readonly Regex Constant = new(
        @"\bconst\s+[\w<>\[\]?,\s]+?\s(\w+)\s*=",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    // CONV-CODE-007 AC1: the context a store queries the database through.
    private static readonly Regex StoreContext = new(
        @"\bStoreContext\s+context\b",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    // CONV-CODE-007 AC1: an operator of EF Core that sends a query to the database,
    // which then compares in the database and not in the library.
    private static readonly Regex Queried = new(
        @"\b(?:First|FirstOrDefault|Single|SingleOrDefault|Any|All|Count|LongCount|ToList|ToArray|ToDictionary|ExecuteUpdate|ExecuteDelete)Async\s*\(",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    // CONV-CODE-007 AC1: what brings a query's rows into the process, after which a
    // comparison is the library's own again.
    private static readonly Regex Materialised = new(
        @"\b(?:AsEnumerable|AsAsyncEnumerable|To(?:List|Array|Dictionary|HashSet)(?:Async)?)\b",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    // CONV-CODE-007 AC1: the words a hash, a token, a code or another secret that is
    // compared is named by. A word counts when it starts with one of them, so a hashed
    // value and a set of codes count too.
    private static readonly string[] Secrets =
    [
        "challenge",
        "code",
        "digest",
        "fingerprint",
        "hash",
        "nonce",
        "otp",
        "passcode",
        "password",
        "secret",
        "signature",
        "token",
        "totp",
        "verifier",
    ];

    // CONV-CODE-007 AC1: the words for a message authentication code, which count only
    // whole, since a machine is not one.
    private static readonly string[] Macs = ["hmac", "mac"];

    // CONV-CODE-007 AC1: the steps that carry a value on without naming it anew, so the
    // value of a token and the span of a hash are still the token and the hash.
    private static readonly string[] Carriers = ["AsMemory", "AsSpan", "Memory", "Span", "ToArray", "ToString", "Value"];

    // CONV-CODE-007 AC1: the encodings that carry the value they are handed into text or
    // bytes, so the text of a hash is still the hash.
    private static readonly string[] Encoders =
    [
        "DecodeFromChars",
        "DecodeFromUtf8",
        "EncodeToString",
        "EncodeToUtf8",
        "FromBase64String",
        "FromHexString",
        "GetBytes",
        "GetString",
        "ToBase64String",
        "ToHexString",
        "ToHexStringLower",
        "ToString",
    ];

    // CONV-CODE-007 AC1: the catalogues of values the library publishes, whose members
    // are the same for every caller and secret to nobody.
    private static readonly string[] Catalogues = ["ErrorCodes", "FactorCatalogue"];

    // CONV-ERR-001 AC1: a code of the catalogue as the catalogue declares it, by the
    // member's name and the code's spelling.
    private static readonly Regex Coded = new(
        @"public\s+static\s+ErrorCode\s+(?<member>\w+)\s*\{\s*get;\s*\}\s*=\s*ErrorCode\.Parse\(\s*""(?<code>[^""]+)""\s*\)",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    // CONV-ERR-001 AC1: where an exception is raised or made, a throw or the
    // construction of an exception, to the end of its statement.
    private static readonly Regex Raised = new(
        @"\bthrow\b[^;]*;|\bnew\s+[\w.]*Exception\s*\([^;]*;",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    // CONV-ERR-001 AC1: an exception whose type is itself a refusal of access.
    private static readonly Regex Refusing = new(
        @"\b\w*(?:Unauthori[sz]ed|Forbidden|Denied|Denial|Authentication|Authorization|Security|Credential)\w*Exception\b",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    // CONV-ERR-001 AC1: the families of the catalogue a denial is spelled in, the
    // authentication and the authorization refusals.
    private static readonly string[] Refusals = ["auth.", "authz."];

    // CONV-ERR-003 AC2: the head of a catch clause, up to the brace that opens its block.
    private static readonly Regex Catch = new(
        @"\bcatch\b\s*(?:\([^)]*\))?\s*(?:when\s*\((?:[^()]|\([^()]*\))*\))?\s*\{",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    // CONV-ERR-003 AC2: a last statement that ends the operation the exception
    // interrupted: it throws, or it returns a failure.
    private static readonly Regex Ending = new(
        @"^(?:throw\b|return\b[\s\S]*\bFailure\b)",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    // CONV-ERR-003 AC2: a last statement that answers the request with a refusal, which
    // ends the operation when nothing runs after it.
    private static readonly Regex Answering = new(
        @"^await\s+Refusal\s*\.\s*WriteAsync\s*\(",
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
    /// hand-written query can never run outside the operation's transaction. The one
    /// other connection is the one that listens for registration signals, a
    /// listen/notify use of OPS-DATA-003: it runs no query, holds no operation's
    /// transaction, and nothing above the port it implements reaches it.
    /// </summary>
    [Fact]
    public void OPS_DATA_002_AC2_OnlyTheAccessorAndTheListenerRetrieveAConnection() =>
        Assert.Equal(
            ["DataConnections.cs", "RegistrationSignals.cs"],
            Sources()
                .Where(file => DirectConnection.IsMatch(File.ReadAllText(file)))
                .Select(Path.GetFileName)
                .Select(name => name!)
                .Order(StringComparer.Ordinal));

    /// <summary>
    /// OPS-DATA-003 AC1: every line of the library that goes below Dapper and EF Core to
    /// ADO.NET has, in the comment nearest above it, the database feature that took it
    /// there, one of the four the item reserves direct use for.
    /// </summary>
    [Fact]
    public void OPS_DATA_003_AC1_EveryDirectUseNamesTheFeatureRequiringIt() =>
        Assert.Empty(Sources()
            .Where(file => file.StartsWith(
                Path.Combine(Repository.Root, "src") + Path.DirectorySeparatorChar,
                StringComparison.Ordinal))
            .SelectMany(file => Unnamed(File.ReadAllLines(file))
                .Select(line => Path.GetFileName(file) + ":" + line)));

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
    /// CONV-CODE-007 AC1: no file of the library compares a value named for a hash, a
    /// token, a code or another secret by an equality operator, an equality or sequence
    /// method or a comparison of text, so each such comparison it makes is
    /// FixedTimeEquals or a wrapper of it. A comparison with a literal, a constant or a
    /// member of a catalogue the library publishes compares with nothing secret, and an
    /// equality inside a query a store sends to the database is a lookup by the keyed
    /// fingerprint the store is handed, which the library does not compare itself.
    /// </summary>
    [Fact]
    public void CONV_CODE_007_AC1_NoHashTokenOrCodeIsComparedButInConstantTime()
    {
        IEnumerable<string> compared = Sources()
            .Where(file => file.StartsWith(
                Path.Combine(Repository.Root, "src") + Path.DirectorySeparatorChar,
                StringComparison.Ordinal))
            .SelectMany(file => Unguarded(file).Select(line => Path.GetFileName(file) + ":" + line));

        Assert.Empty(compared);
    }

    /// <summary>
    /// CONV-ERR-001 AC1: no file of the library raises or makes an exception that
    /// carries an authentication or authorization code of the catalogue, or whose type
    /// is itself a refusal of access, so every denial reaches its caller as a result.
    /// </summary>
    [Fact]
    public void CONV_ERR_001_AC1_NoDenialIsSignalledByAnException()
    {
        string[] denials =
        [
            .. Coded
                .Matches(Repository.ReadText("src/Janus.Core/ErrorCodes.cs"))
                .Where(code => Refusals.Any(family =>
                    code.Groups["code"].Value.StartsWith(family, StringComparison.Ordinal)))
                .Select(code => code.Groups["member"].Value),
        ];

        var naming = new Regex(
            @"\bErrorCodes\s*\.\s*(?:" + string.Join('|', denials) + @")\b",
            RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(5));

        IEnumerable<string> thrown = Sources()
            .Where(file => file.StartsWith(
                Path.Combine(Repository.Root, "src") + Path.DirectorySeparatorChar,
                StringComparison.Ordinal))
            .SelectMany(file =>
            {
                string code = Comment.Replace(File.ReadAllText(file), string.Empty);

                return Raised
                    .Matches(code)
                    .Where(raised => naming.IsMatch(raised.Value) || Refusing.IsMatch(raised.Value))
                    .Select(raised => Path.GetFileName(file) + ":" + LineOf(code, raised.Index));
            });

        Assert.NotEmpty(denials);
        Assert.Empty(thrown);
    }

    /// <summary>
    /// CONV-ERR-003 AC2: no catch block of the library carries on after the exception,
    /// logging or not, since every path of the library is a security path: each one
    /// throws, returns a failure, or answers the request with a refusal as the last act
    /// of the block that holds it.
    /// </summary>
    [Fact]
    public void CONV_ERR_003_AC2_NoCatchOfTheLibraryCarriesOnAfterTheException()
    {
        (string Site, bool Ended)[] caught =
        [
            .. Sources()
                .Where(file => file.StartsWith(
                    Path.Combine(Repository.Root, "src") + Path.DirectorySeparatorChar,
                    StringComparison.Ordinal))
                .SelectMany(file => Catches(file).Select(found => (Path.GetFileName(file) + ":" + found.Line, found.Ended))),
        ];

        Assert.NotEmpty(caught);
        Assert.Empty(caught.Where(found => !found.Ended).Select(found => found.Site));
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

    // The numbers of the lines that use ADO.NET directly and whose nearest comment above
    // names no feature of OPS-DATA-003. The comment is read whole, its documentation
    // lines among it, so a phrase it wraps across two lines is still one phrase.
    private static IEnumerable<int> Unnamed(string[] lines)
    {
        for (int at = 0; at < lines.Length; at++)
        {
            if (IsComment(lines[at]) || !DirectAdoNet.IsMatch(lines[at]))
            {
                continue;
            }

            int last = at - 1;

            while (last >= 0 && !IsComment(lines[last]))
            {
                last--;
            }

            int first = last;

            while (first > 0 && IsComment(lines[first - 1]))
            {
                first--;
            }

            string said = last < 0
                ? string.Empty
                : string.Join(' ', lines[first..(last + 1)].Select(line => line.TrimStart().TrimStart('/').Trim()));

            if (!DirectFeatures.Any(feature => said.Contains(feature, StringComparison.OrdinalIgnoreCase)))
            {
                yield return at + 1;
            }
        }
    }

    private static bool IsComment(string line) => line.TrimStart().StartsWith("//", StringComparison.Ordinal);

    // CONV-CODE-007 AC1: the lines of a file where a value named for a secret is
    // compared other than in constant time, neither operand being a constant and the
    // comparison not being one a store's query sends to the database.
    private static IEnumerable<int> Unguarded(string file)
    {
        string code = Comment.Replace(File.ReadAllText(file), string.Empty);
        HashSet<string> constants = [.. Constant.Matches(code).Select(match => match.Groups[1].Value)];
        bool store = StoreContext.IsMatch(code);

        return Comparisons
            .SelectMany(comparison => comparison.Matches(code))
            .Where(match => IsSecret(match.Groups["left"].Value) || IsSecret(match.Groups["right"].Value))
            .Where(match => !IsConstant(match.Groups["left"].Value, constants)
                && !IsConstant(match.Groups["right"].Value, constants))
            .Where(match => !(store && IsQueried(code, match)))
            .Select(match => LineOf(code, match.Index))
            .Distinct()
            .Order();
    }

    // Whether an operand names a secret: the step that last names it does, or that step
    // encodes a value it is handed that does.
    private static bool IsSecret(string operand)
    {
        string[] steps = Steps(operand);
        int named = steps.Length - 1;

        while (named > 0
            && Carriers.Contains(NameOf(steps[named]), StringComparer.Ordinal)
            && (!steps[named].Contains('(', StringComparison.Ordinal) || steps[named].EndsWith("()", StringComparison.Ordinal)))
        {
            named--;
        }

        return Encoders.Contains(NameOf(steps[named]), StringComparer.Ordinal)
            ? Identifier.Matches(steps[named]).Any(identifier => NamesASecret(identifier.Value))
            : NamesASecret(NameOf(steps[named]));
    }

    // Whether an identifier is named for a secret. A cancellation token is not one.
    private static bool NamesASecret(string identifier)
    {
        string[] words = [.. Word.Matches(identifier).Select(word => word.Value)];

        return !words.Contains("cancellation", StringComparer.OrdinalIgnoreCase)
            && words.Any(word => Macs.Contains(word, StringComparer.OrdinalIgnoreCase)
                || Secrets.Any(secret => word.StartsWith(secret, StringComparison.OrdinalIgnoreCase)));
    }

    // Whether an operand is a value the library compiles in or publishes.
    private static bool IsConstant(string operand, HashSet<string> constants)
    {
        string[] steps = Steps(operand);

        return Literal.IsMatch(operand)
            || operand is "null" or "default" or "true" or "false"
            || Catalogues.Contains(NameOf(steps[0]), StringComparer.Ordinal)
            || (steps.Length == 1 && constants.Contains(steps[0]));
    }

    // The steps of a member chain, split at the dots that stand outside its arguments.
    private static string[] Steps(string operand)
    {
        var steps = new List<string>();
        int depth = 0;
        int started = 0;

        for (int at = 0; at < operand.Length; at++)
        {
            if (operand[at] is '(' or '[')
            {
                depth++;
            }
            else if (operand[at] is ')' or ']')
            {
                depth--;
            }
            else if (operand[at] == '.' && depth == 0)
            {
                steps.Add(operand[started..at].Trim().TrimEnd('?', '!'));
                started = at + 1;
            }
        }

        steps.Add(operand[started..].Trim());

        return [.. steps];
    }

    // The name of a step, without its arguments.
    private static string NameOf(string step) => step.Split('(', '[')[0].Trim();

    // Whether a comparison stands inside a statement that sends a query to the
    // database, with nothing before it in the statement bringing the rows into the
    // process first.
    private static bool IsQueried(string code, Match comparison)
    {
        int start = code.LastIndexOfAny([';', '{', '}'], comparison.Index) + 1;
        int end = code.IndexOf(';', comparison.Index + comparison.Length);

        return Queried.IsMatch(code[start..(end < 0 ? code.Length : end)])
            && !Materialised.IsMatch(code[start..comparison.Index]);
    }

    // CONV-ERR-003 AC2: every catch block of a file, by its line, and whether it ends
    // the operation: its last statement throws or returns a failure, or it writes a
    // refusal as the answer and the block that holds it closes after it, so nothing
    // runs after the exception as though it had not been thrown.
    private static IEnumerable<(int Line, bool Ended)> Catches(string file)
    {
        string code = Comment.Replace(File.ReadAllText(file), string.Empty);

        foreach (Match head in Catch.Matches(code))
        {
            int opened = head.Index + head.Length;
            int closed = Closing(code, opened);
            string last = LastStatement(code[opened..closed]);

            yield return (
                LineOf(code, head.Index),
                Ending.IsMatch(last)
                    || (Answering.IsMatch(last) && code[(closed + 1)..].TrimStart().StartsWith('}')));
        }
    }

    // The index of the brace that closes a block, from just inside the brace that
    // opens it.
    private static int Closing(string code, int opened)
    {
        int depth = 1;

        for (int at = opened; at < code.Length; at++)
        {
            if (code[at] is '"' or '\'')
            {
                at = Unquoted(code, at);
            }
            else if (code[at] == '{')
            {
                depth++;
            }
            else if (code[at] == '}' && --depth == 0)
            {
                return at;
            }
        }

        throw new InvalidOperationException("A block opened at " + opened + " is never closed.");
    }

    // The last statement of a block's body: what follows the last semicolon or closed
    // block before it at the body's own depth.
    private static string LastStatement(string body)
    {
        int depth = 0;
        int started = 0;
        int ended = 0;

        for (int at = 0; at < body.Length; at++)
        {
            char character = body[at];

            if (character is '"' or '\'')
            {
                at = Unquoted(body, at);
            }
            else if (character is '(' or '[' or '{')
            {
                depth++;
            }
            else if (character is ')' or ']' or '}')
            {
                depth--;
            }

            if ((character == ';' && depth == 0) || (character == '}' && depth == 0))
            {
                started = ended;
                ended = at + 1;
            }
        }

        return body[started..ended].Trim();
    }

    // The index of the quote that closes the literal a quote opens.
    private static int Unquoted(string code, int opened)
    {
        for (int at = opened + 1; at < code.Length; at++)
        {
            if (code[at] == '\\')
            {
                at++;
            }
            else if (code[at] == code[opened])
            {
                return at;
            }
        }

        return code.Length;
    }

    // The line an index of a file's text falls on, counting from one.
    private static int LineOf(string code, int index) => code[..index].Count(character => character == '\n') + 1;

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
