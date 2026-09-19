using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Janus.Authorization.Gate;
using Janus.Core;
using Xunit;

namespace Janus.Authorization.Tests.Gate;

/// <summary>
/// What the one rule renders, read as text rather than run
/// (AUTHZ-GATE-002, AUTHZ-GATE-003, AUTHZ-PRIN-001, AUTHZ-PRIN-002, AUTHZ-GATE-005,
/// LIB-HOST-002).
/// </summary>
[Trait("kind", "unit")]
public sealed class PermissionRuleTests
{
    private static readonly ResourceType Document = ResourceType.Parse("document");

    // The clause that decides, in the words every rendering carries it in.
    private static readonly string[] Deciding =
    [
        ".permission = ANY(@janus_authz_permissions)",
        ".organization = @janus_authz_organization",
        ".revoked_at IS NULL",
        ".expires_at > @janus_authz_at",
        ".subject_id = ANY(@janus_authz_accounts)",
        ".subject_id = ANY(@janus_authz_groups)",
    ];

    /// <summary>
    /// AUTHZ-GATE-005 AC1, AUTHZ-PRIN-002 AC1: a page of records is one statement that
    /// names the whole page by parameter, so fifty records cost what one costs and
    /// nothing is asked per row.
    /// </summary>
    [Fact]
    public void AUTHZ_GATE_005_AC1_APageIsOneStatementNamingEveryRecordByParameter()
    {
        PermissionRule rule = Rule();

        SqlFilter page = rule.ToPage();

        Assert.Equal(1, page.Text.Count(character => character == ';'));
        Assert.Contains(
            "unnest(CAST(@janus_authz_page_ids AS text[]))",
            page.Text,
            StringComparison.Ordinal);
        Assert.DoesNotContain("UNION", page.Text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// AUTHZ-PRIN-002 AC2: no rendering asks the database which records are permitted,
    /// so nothing is read that is not displayed.
    /// </summary>
    [Fact]
    public void AUTHZ_PRIN_002_AC2_NoRenderingSelectsTheRecordsAPrincipalMayReach()
    {
        PermissionRule rule = Rule();

        foreach (string text in Renderings(rule))
        {
            Assert.DoesNotContain("SELECT resource_id", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("DISTINCT", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// AUTHZ-INHERIT-002 AC4: inheritance is read from the closure, so no rendering
    /// walks the tree at request time.
    /// </summary>
    [Fact]
    public void AUTHZ_INHERIT_002_AC4_NoRenderingWalksTheTreeAtRequestTime()
    {
        PermissionRule rule = Rule();

        foreach (string text in Renderings(rule))
        {
            Assert.DoesNotContain("RECURSIVE", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("janus.ancestry", text, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// AUTHZ-GRANT-001 AC2: the organization is the third scope a grant may name, and
    /// asking about it reads no ancestry, a grant on one record reaching nothing above
    /// it.
    /// </summary>
    [Fact]
    public void AUTHZ_GRANT_001_AC2_TheOrganizationRenderingReadsNoAncestry()
    {
        var subjects = SubjectSet.Of(Subject(), [], 0, restricted: false);
        var rule = new PermissionRule(
            [Permission.Parse("audit:read")],
            new OrganizationId(Guid.NewGuid()),
            subjects,
            DateTimeOffset.UnixEpoch);

        SqlFilter candidates = rule.ToOrganizationCandidates();

        Assert.DoesNotContain("janus.ancestry", candidates.Text, StringComparison.Ordinal);
        Assert.Contains("resource_type IS NULL", candidates.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("janus_authz_type", candidates.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("janus_authz_type", candidates.Parameters.Keys);
    }

    /// <summary>
    /// AUTHZ-GATE-002 AC1: the predicate the three renderings ask is written once, so
    /// every one of them carries the same set of values.
    /// </summary>
    [Fact]
    public void AUTHZ_GATE_002_AC1_EveryRenderingCarriesTheSameValues()
    {
        PermissionRule rule = Rule();

        IReadOnlyDictionary<string, object> fragment = rule.ToFragment("janus_authz_row", "id").Parameters;

        Assert.Equal(fragment.Keys.Order(StringComparer.Ordinal), rule.ToCandidates().Parameters.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(fragment.Keys.Order(StringComparer.Ordinal), rule.ToPage().Parameters.Keys.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// AUTHZ-PRIN-001 AC2: the clause that decides is written once and carried by every
    /// rendering, and no other type in the area renders one, so no permission logic is
    /// reachable by only one of the two paths.
    /// </summary>
    [Fact]
    public void AUTHZ_PRIN_001_AC2_NoPredicateIsReachableByOnlyOneOfThePaths()
    {
        PermissionRule rule = Rule();

        foreach (string text in Renderings(rule))
        {
            Assert.All(Deciding, clause => Assert.Contains(clause, text, StringComparison.Ordinal));
        }

        Assert.All(
            typeof(PermissionRule).Assembly.GetTypes()
                .Where(type => type != typeof(PermissionRule))
                .SelectMany(type => type.GetMethods(
                    BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.Instance
                    | BindingFlags.Static
                    | BindingFlags.DeclaredOnly)),
            method => Assert.NotEqual(typeof(SqlFilter), method.ReturnType));
    }

    /// <summary>
    /// LIB-HOST-002 AC1: every relation a rendering reads is the library's own, the
    /// host's row reaching it only as the alias the caller passed in.
    /// </summary>
    [Fact]
    public void LIB_HOST_002_AC1_NoRenderingReadsATableTheHostOwns()
    {
        PermissionRule rule = Rule();

        foreach (string text in Renderings(rule))
        {
            Assert.All(
                Read().Matches(text).Select(match => match.Groups[1].Value),
                relation => Assert.True(
                    relation.StartsWith("janus.", StringComparison.Ordinal)
                        || string.Equals(relation, "unnest", StringComparison.Ordinal),
                    relation));
        }

        Assert.Contains("janus_authz_row.id", rule.ToFragment("janus_authz_row", "id").Text, StringComparison.Ordinal);
    }

    // What a rendering reads from, in the two words a statement names it by.
    private static Regex Read() => new(
        @"(?:FROM|JOIN)\s+([A-Za-z_][A-Za-z0-9_.]*)",
        RegexOptions.None,
        TimeSpan.FromSeconds(1));

    private static string[] Renderings(PermissionRule rule) =>
    [
        rule.ToFragment("janus_authz_row", "id").Text,
        rule.ToCandidates().Text,
        rule.ToPage().Text,
    ];

    private static PermissionRule Rule()
    {
        var subjects = SubjectSet.Of(
            Subject(),
            [GroupId.New(TimeProvider.System)],
            3,
            restricted: false);

        return new PermissionRule(
            [Permission.Parse("document:read"), Permission.Parse("document:edit")],
            Document,
            new OrganizationId(Guid.NewGuid()),
            subjects,
            DateTimeOffset.UnixEpoch);
    }

    private static SubjectId Subject()
    {
        using var randomness = RandomNumberGenerator.Create();

        return SubjectId.New(randomness);
    }
}
