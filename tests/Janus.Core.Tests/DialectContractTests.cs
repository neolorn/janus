using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// What the documented contract says about the fragment renderer's dialect
/// (AUTHZ-GATE-003, LIB-API-004).
/// </summary>
[Trait("kind", "contract")]
public sealed class DialectContractTests
{
    // A claim of portability, in the words one would be made in. The fragment is
    // PostgreSQL and nothing in the contract may suggest otherwise.
    private static readonly string[] Portability =
    [
        "is portable",
        "portable across",
        "database-agnostic",
        "dialect-agnostic",
        "dialect-independent",
        "any database",
        "any relational",
        "other databases",
        "any SQL",
    ];

    /// <summary>
    /// LIB-API-004 AC1, AUTHZ-GATE-003 AC1: the documentation of the fragment says
    /// which database it is for, so a reader learns it from the contract rather than
    /// from a failure.
    /// </summary>
    [Fact]
    public void LIB_API_004_AC1_TheFragmentIsDocumentedAsPostgreSqlSpecific()
    {
        Assert.Contains(
            "PostgreSQL",
            Documented("T:Janus.Core.SqlFilter"),
            StringComparison.Ordinal);

        Assert.Contains(
            "PostgreSQL",
            Documented("M:Janus.Core.IAccessGate.FragmentAsync(Janus.Core.AccessContext,Janus.Core.Permission,Janus.Core.ResourceType,Janus.Core.OrganizationId,System.String,System.String,System.Threading.CancellationToken)"),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// AUTHZ-GATE-003 AC2, LIB-API-004: nothing in the documented surface claims the
    /// fragment runs anywhere else.
    /// </summary>
    [Fact]
    public void AUTHZ_GATE_003_AC2_NoClaimOfDialectPortabilityIsDocumented()
    {
        string documentation = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Janus.Core.xml"));

        Assert.All(
            Portability,
            claim => Assert.DoesNotContain(claim, documentation, StringComparison.OrdinalIgnoreCase));
    }

    private static string Documented(string member)
    {
        var documentation = XDocument.Parse(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Janus.Core.xml")));

        XElement declared = Assert.Single(
            documentation.Descendants("member"),
            element => string.Equals(
                element.Attribute("name")?.Value,
                member,
                StringComparison.Ordinal));

        return declared.Value;
    }
}
