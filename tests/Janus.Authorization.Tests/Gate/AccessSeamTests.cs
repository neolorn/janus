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
    /// AUTHZ-SEAM-001 AC2: nothing outside the gate builds a permission query, so no
    /// call site can come to ask a question the gate would answer differently.
    /// </summary>
    [Fact]
    public void AUTHZ_SEAM_001_AC2_NoCallSiteBuildsAPermissionQuery()
    {
        foreach (string file in Sources())
        {
            if (string.Equals(
                Path.GetFileName(Path.GetDirectoryName(file)),
                "Gate",
                StringComparison.Ordinal))
            {
                continue;
            }

            string text = File.ReadAllText(file);

            Assert.DoesNotContain(nameof(PermissionRule), text, StringComparison.Ordinal);
            Assert.DoesNotContain("janus.effective_grants", text, StringComparison.Ordinal);
        }
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
                Path.Combine("Janus.Authorization", "Gate", "AccessGate.cs"),
                Path.Combine("Janus.Hosting", "HostingRegistration.cs"),
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
            .Where(name => name.StartsWith("Janus.", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal);

        Assert.Equal(["Janus.Core"], referenced);
    }

    /// <summary>
    /// AUTHZ-GATE-006 AC2: a restriction is read in one place and refused in one
    /// place, so no feature can come to enforce it differently or to forget it.
    /// </summary>
    [Fact]
    public void AUTHZ_GATE_006_AC2_TheRestrictionIsReadAndRefusedInOnePlace()
    {
        Assert.Equal(
            [
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
