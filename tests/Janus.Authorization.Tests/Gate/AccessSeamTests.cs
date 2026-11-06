using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Janus.Authorization.Gate;
using Janus.Core;
using Xunit;

namespace Janus.Authorization.Tests.Gate;

/// <summary>
/// That there is one evaluation path and no way past it
/// (AUTHZ-SEAM-001, LIB-SEAM-001, LIB-SEAM-002, LIB-HOST-004, AUTHZ-GATE-001,
/// AUTHZ-PRIN-003, AUTHZ-IMP-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class AccessSeamTests
{
    // The head every library assembly's name carries, read from the core's
    // namespace so no string spells the product name (CONV-NAME-001).
    private static readonly string Library = typeof(Result).Namespace!.Split('.')[0] + ".";

    /// <summary>
    /// AUTHZ-SEAM-001 AC1: one type stands behind the interface, so there is one
    /// evaluation path and nothing else answering the same question.
    /// </summary>
    [Fact]
    public void AUTHZ_SEAM_001_AC1_OneTypeStandsBehindTheInterface()
    {
        Type[] implementations =
        [
            .. typeof(AccessGate).Assembly
                .GetTypes()
                .Where(type => type.IsClass && typeof(IAccessGate).IsAssignableFrom(type)),
        ];

        Assert.Equal([typeof(AccessGate)], implementations);
    }

    /// <summary>
    /// AUTHZ-SEAM-001 AC2 (D-269): nothing outside the gate builds a permission query, over
    /// the host's rows or over the contract relations. Across every source project of the
    /// library and the sample host, no file names <c>identity.ancestry</c>,
    /// <c>identity.effective_grants</c> or <c>identity.consented_resources</c>, or reads the
    /// <see cref="AncestryEntry"/>, <see cref="EffectiveGrant"/> or
    /// <see cref="ConsentedResource"/> sets, save where the gate renders, the relations are
    /// kept, mapped or counted as the library's own, or the sets are handed to the gate.
    /// </summary>
    [Fact]
    public void AUTHZ_SEAM_001_AC2_NoCallSiteBuildsAPermissionQuery()
    {
        string[] read =
        [
            .. Tree("src")
                .Concat(Tree(Path.Combine("samples", "HostSample")))
                .Where(file => !Generated(file) && ReadsAContractRelation(Placed(file), File.ReadAllText(file))),
        ];

        string[] ruled =
        [
            .. Sources().Where(file =>
                !string.Equals(Path.GetFileName(Path.GetDirectoryName(file)), "Gate", StringComparison.Ordinal)
                && File.ReadAllText(file).Contains(nameof(PermissionRule), StringComparison.Ordinal)),
        ];

        Assert.Empty(read);
        Assert.Empty(ruled);
    }

    /// <summary>
    /// AUTHZ-SEAM-001 AC2 (D-269): the check finds a source outside those places that reads
    /// one of the relations, by its name in SQL, by the record its set is mapped through,
    /// or through a set the sample host maps and reads outside what it hands the gate.
    /// </summary>
    [Fact]
    public void AUTHZ_SEAM_001_AC2_AReadOutsideThosePlacesIsFound()
    {
        Assert.True(ReadsAContractRelation(
            "src/Janus.Identity/Accounts/Holders.cs",
            "SELECT 1 FROM identity.effective_grants AS held WHERE held.subject_id = @subject"));
        Assert.True(ReadsAContractRelation(
            "src/Janus.Privacy/Consents/Readers.cs",
            "context.Set<ConsentedResource>().Any(row => row.Purpose == purpose)"));
        Assert.True(ReadsAContractRelation(
            "samples/HostSample/WorkshopEndpoints.cs",
            "bool held = await context.Grants.AnyAsync(grant => grant.SubjectId == person);"));
        Assert.True(ReadsAContractRelation(
            "samples/HostSample/WorkshopContext.cs",
            "public DbSet<EffectiveGrant> Grants => Set<EffectiveGrant>();\nint count = Grants.Count();"));
        Assert.False(ReadsAContractRelation(
            "src/Janus.Authorization/Gate/PermissionRule.cs",
            "FROM identity.effective_grants AS grant"));
    }

    /// <summary>
    /// AUTHZ-PRIN-003 AC3: no path turns a failure into an allow, there being nothing
    /// in the area that catches one.
    /// </summary>
    [Fact]
    public void AUTHZ_PRIN_003_AC3_NoPathTurnsAFailureIntoAnAllow()
    {
        foreach (string file in Sources())
        {
            Assert.DoesNotMatch("\\bcatch\\b", File.ReadAllText(file));
        }
    }

    /// <summary>
    /// AUTHZ-GATE-001 AC2: every operation of the gate is asked by somebody, so
    /// background work reaches data as a named principal or not at all.
    /// </summary>
    [Fact]
    public void AUTHZ_GATE_001_AC2_EveryOperationNamesWhoIsAsking()
    {
        Assert.All(
            typeof(IAccessGate).GetMethods(),
            method => Assert.Equal(
                typeof(AccessContext),
                method.GetParameters()[0].ParameterType));

        Assert.Throws<ArgumentNullException>(() => AccessContext.Of(principal: null!));
    }

    /// <summary>
    /// AUTHZ-GATE-001 AC1: the library hands out no set of its own rows, so the only
    /// way to what it holds is the gate.
    /// </summary>
    [Fact]
    public void AUTHZ_GATE_001_AC1_NoPublicSurfaceHandsOutASetOfRows()
    {
        Assert.All(
            typeof(IAccessGate).Assembly.GetExportedTypes()
                // What a host passed the gate is the host's own, and handing it back is
                // not the library handing out rows of its own (LIB-HOST-002).
                .Where(type => type != typeof(FilterSources<>))
                .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance)),
            method => Assert.DoesNotContain(
                "IQueryable",
                method.ReturnType.Name,
                StringComparison.Ordinal));
    }

    /// <summary>
    /// AUTHZ-IMP-001 AC1: both identities are on every context, one principal standing
    /// for both where nobody is acting for anybody.
    /// </summary>
    [Fact]
    public void AUTHZ_IMP_001_AC1_BothFieldsArePresentOnEveryAccessContext()
    {
        using var randomness = RandomNumberGenerator.Create();

        var subject = SubjectId.New(randomness);
        var context = AccessContext.Of(subject);

        Assert.Equal(subject, context.Acting);
        Assert.Equal(subject, context.Effective);

        Assert.All(
            typeof(AccessContext).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.Name is nameof(AccessContext.Acting)
                    or nameof(AccessContext.Effective)),
            property => Assert.NotNull(property.GetMethod));
    }

    /// <summary>
    /// AUTHZ-IMP-001 AC3: the two identities are carried and never read as differing,
    /// impersonation being a seam and not a feature (LIB-SEAM-002).
    /// </summary>
    [Fact]
    public void AUTHZ_IMP_001_AC3_NoFeatureReadsTheTwoIdentitiesAsDiffering()
    {
        foreach (string file in Sources())
        {
            string text = File.ReadAllText(file);

            Assert.DoesNotMatch(Compared("Acting"), text);
            Assert.DoesNotMatch(Compared("Effective"), text);
        }
    }

    /// <summary>
    /// AUTHZ-IMP-001 AC6 (D-202): no public member makes a context whose two identities
    /// differ. A context is made by its own type alone, which nothing derives from and no
    /// public constructor opens, and its public makers are the person's own and the
    /// system principal's, each with one identity standing for both.
    /// </summary>
    [Fact]
    public void AUTHZ_IMP_001_AC6_NoPublicMemberMakesAContextWhoseIdentitiesDiffer()
    {
        using var randomness = RandomNumberGenerator.Create();

        var subject = SubjectId.New(randomness);
        var person = AccessContext.Of(subject);
        var principal = AccessContext.Of(
            SystemPrincipal.ForDeployment("expiry-sweep", "AUTHZ-IMP-001", SystemOperation.ExpirySweep));
        string[] makers =
        [
            .. typeof(AccessContext)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(method => method.ReturnType == typeof(AccessContext))
                .Select(method => method.Name + "(" + string.Join(", ", method.GetParameters().Select(parameter => parameter.ParameterType.Name)) + ")")
                .Order(StringComparer.Ordinal),
        ];

        Assert.True(typeof(AccessContext).IsSealed);
        Assert.Empty(typeof(AccessContext).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Equal(["Of(SubjectId)", "Of(SystemPrincipal)"], makers);
        Assert.Equal((subject, subject), (person.Acting!.Value, person.Effective!.Value));
        Assert.Null(principal.Acting);
        Assert.Null(principal.Effective);
    }

    /// <summary>
    /// LIB-SEAM-001 AC1: what evaluates a permission is named in one place, so putting
    /// something else behind the interface is one line and no call site.
    /// </summary>
    [Fact]
    public void LIB_SEAM_001_AC1_ReplacingWhatEvaluatesIsOneChange()
    {
        IEnumerable<string> naming = Tree("src")
            .Where(file => Regex.IsMatch(
                File.ReadAllText(file),
                "\\b" + nameof(AccessGate) + "\\b",
                RegexOptions.None,
                TimeSpan.FromSeconds(1)))
            .Select(Relative)
            .Order(StringComparer.Ordinal);

        Assert.Equal(
            [
                Path.Combine("Janus.Authorization", "AuthorizationRegistration.cs"),
                Path.Combine("Janus.Authorization", "Gate", "AccessGate.cs"),
            ],
            naming);
    }

    /// <summary>
    /// LIB-SEAM-002 AC2: no feature anywhere in the library reads the acting and the
    /// effective identity as differing, impersonation being a reserved seam.
    /// </summary>
    [Fact]
    public void LIB_SEAM_002_AC2_NoFeatureReadsActingAndEffectiveAsDiffering()
    {
        foreach (string file in Tree("src"))
        {
            string text = File.ReadAllText(file);

            Assert.DoesNotMatch(Compared("Acting"), text);
            Assert.DoesNotMatch(Compared("Effective"), text);
        }
    }

    /// <summary>
    /// LIB-HOST-004 AC1: the area stands on the contract alone, so a host that consumes
    /// authorization and no authentication has everything the gate needs; the suites
    /// that run it are registered the same way.
    /// </summary>
    [Fact]
    public void LIB_HOST_004_AC1_AuthorizationAloneCompilesAndRuns()
    {
        IEnumerable<string> referenced = typeof(AccessGate).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => name.StartsWith(Library, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal);

        Assert.Equal(["Janus.Core"], referenced);
    }

    /// <summary>
    /// AUTHZ-GATE-006 AC2: a restriction is read in one place and refused in one place
    /// for each kind of acting subject, an account's by the gate and a person record's by
    /// the record's rights, so no feature can come to enforce it differently or to forget
    /// it.
    /// </summary>
    [Fact]
    public void AUTHZ_GATE_006_AC2_TheRestrictionIsReadAndRefusedInOnePlace()
    {
        Assert.Equal(
            [
                Path.Combine("Janus.Authentication", "Records", "RecordRights.cs"),
                Path.Combine("Janus.Authorization", "Gate", "AccessGate.cs"),
            ],
            Naming("Error.From(ErrorCodes.Restricted)"));

        Assert.Equal(
            [
                Path.Combine("Janus.Authorization", "Gate", "ISubjectRestrictions.cs"),
                Path.Combine("Janus.Authorization", "Gate", "SubjectSets.cs"),
                Path.Combine("Janus.Storage", "Authorization", "Gate", "SubjectRestrictions.cs"),
            ],
            Naming("IsRestrictedAsync"));
    }

    // The files of the library naming something, as the repository lays them out.
    private static string[] Naming(string what) =>
    [
        .. Tree("src")
            .Where(file => File.ReadAllText(file).Contains(what, StringComparison.Ordinal))
            .Select(Relative)
            .Order(StringComparer.Ordinal),
    ];

    // AUTHZ-SEAM-001 AC2 (D-269): where a contract relation may be named or its set read,
    // each a file or a folder, with what it does there.
    private static readonly (string Place, string Why)[] Kept =
    [
        ("src/Janus.Authorization/Gate/", "the gate renders the rule over them"),
        ("src/Janus.Core/AncestryEntry.cs", "the record the closure is mapped through"),
        ("src/Janus.Core/EffectiveGrant.cs", "the record the view is mapped through"),
        ("src/Janus.Core/ConsentedResource.cs", "the record the view is mapped through"),
        ("src/Janus.Core/FilterSources.cs", "the sets as the host hands them to the gate"),
        ("src/Janus.Storage/Migrations/", "the relations kept as the library's own"),
        ("src/Janus.Storage/Authorization/", "the closure kept with the registrations"),
        ("src/Janus.Hosting/AuthorizationTables.cs", "the relations mapped into the host's context"),
        ("src/Janus.Hosting/Authorization/RelationshipSources.cs", "the host's rows read for the gate"),
        ("src/Janus.Hosting/DeclarationCoverage.cs", "the relations counted as policed (AUTHZ-GATE-001 AC3)"),
        ("src/Janus.Conformance/ConformanceSuite.cs", "the relations counted as policed (AUTHZ-GATE-001 AC3)"),
        ("samples/HostSample/WorkshopContext.cs", "the sample host maps the sets and hands them to the gate"),
    ];

    // A contract relation by its name in SQL, or the record a host maps it through.
    private static readonly Regex Relation = new(
        "identity\\.(ancestry|effective_grants|consented_resources)\\b|\\b(AncestryEntry|EffectiveGrant|ConsentedResource)\\b",
        RegexOptions.None,
        TimeSpan.FromSeconds(1));

    // A set of the host's context a contract relation is mapped through, and its name.
    private static readonly Regex MappedSet = new(
        "DbSet<(?:AncestryEntry|EffectiveGrant|ConsentedResource)>\\s+(\\w+)",
        RegexOptions.None,
        TimeSpan.FromSeconds(1));

    // The names the sample host gives the sets it maps the contract relations through.
    private static readonly Lazy<string[]> SampleSets = new(() =>
    [
        .. Tree(Path.Combine("samples", "HostSample"))
            .Where(file => !Generated(file))
            .SelectMany(file => MappedSet.Matches(File.ReadAllText(file)))
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal),
    ]);

    // Whether a source reads a contract relation outside the places that may: by its name
    // or its record anywhere else, or, in the sample host, through a set it maps, read
    // anywhere but where it is declared and handed to the gate.
    private static bool ReadsAContractRelation(string place, string text)
    {
        if (!Kept.Any(kept => place.StartsWith(kept.Place, StringComparison.Ordinal)) && Relation.IsMatch(text))
        {
            return true;
        }

        return place.StartsWith("samples/", StringComparison.Ordinal)
            && text.Split('\n').Any(line =>
                !MappedSet.IsMatch(line)
                && !line.Contains("new FilterSources<", StringComparison.Ordinal)
                && SampleSets.Value.Any(set => Regex.IsMatch(line, "\\b" + set + "\\b", RegexOptions.None, TimeSpan.FromSeconds(1))));
    }

    // What the build writes under a project's bin and obj folders, which nobody authored.
    private static bool Generated(string file) =>
        Placed(file).Split('/').Any(part => part is "bin" or "obj");

    // A file's place in the repository, its folders joined by a forward slash.
    private static string Placed(string file) =>
        Path.GetRelativePath(Root(), file).Replace(Path.DirectorySeparatorChar, '/');

    // One identity compared with another, written as the comparison would be.
    private static string Compared(string identity) => identity + "\\s*[!=]=\\s*";

    private static string[] Sources() => Tree(Path.Combine("src", "Janus.Authorization"));

    private static string[] Tree(string under) => Directory.GetFiles(
        Path.Combine(Root(), under),
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
