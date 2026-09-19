using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Janus.Authorization.Gate;
using Janus.Core;
using Xunit;

namespace Janus.Authorization.Tests.Gate;

/// <summary>
/// That there is one evaluation path and no way past it
/// (AUTHZ-SEAM-001, LIB-SEAM-001, AUTHZ-GATE-001, AUTHZ-PRIN-003, AUTHZ-IMP-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class AccessSeamTests
{
    /// <summary>
    /// AUTHZ-SEAM-001 AC1, LIB-SEAM-001 AC1: one type stands behind the interface, so
    /// replacing what evaluates a permission is one change in one place.
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
    /// AUTHZ-GATE-001 AC2, AUTHZ-IMP-001 AC1: every operation of the gate is asked by
    /// somebody, so background work reaches data as a named principal or not at all.
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
    /// AUTHZ-IMP-001 AC3: the two identities are carried and never read as differing,
    /// impersonation being a seam and not a feature (LIB-SEAM-002).
    /// </summary>
    [Fact]
    public void AUTHZ_IMP_001_AC3_NoFeatureReadsTheTwoIdentitiesAsDiffering()
    {
        foreach (string file in Sources())
        {
            string text = File.ReadAllText(file);

            Assert.DoesNotMatch("Acting\\s*[!=]=\\s*", text);
            Assert.DoesNotMatch("Effective\\s*[!=]=\\s*", text);
        }
    }

    private static string[] Sources()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);

        while (at is not null && !Directory.Exists(Path.Combine(at.FullName, "src")))
        {
            at = at.Parent;
        }

        return Directory.GetFiles(
            Path.Combine(at!.FullName, "src", "Janus.Authorization"),
            "*.cs",
            SearchOption.AllDirectories);
    }
}
