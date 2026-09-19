using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using Janus.Core;

namespace Janus.Authorization.Gate;

/// <summary>
/// One permission rule, written once and rendered two ways: a predicate the host
/// composes into a LINQ query, and a fragment a hand-written query composes into its
/// <c>WHERE</c> clause.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GATE-002, AUTHZ-GATE-003, AUTHZ-PRIN-001 and AUTHZ-PRIN-002. Every
/// rendering is built from the one predicate below, so a list screen cannot come to
/// show what a check would refuse. None of them enumerates permitted records: each asks
/// whether a live grant reaches the row in front of it, and a deny defeats an allow.
/// </remarks>
internal sealed class PermissionRule
{
    /// <summary>
    /// The parameter the candidate statement names the record by.
    /// </summary>
    public const string RecordParameter = "janus_authz_record";

    /// <summary>
    /// The parameter a page of records is carried in.
    /// </summary>
    public const string PageParameter = "janus_authz_page_ids";

    /// <summary>
    /// The alias a page of records is read under.
    /// </summary>
    public const string PageAlias = "janus_authz_page";

    /// <summary>
    /// The column of that alias holding a record's identifier.
    /// </summary>
    public const string PageColumn = "id";

    private const string Prefix = "janus_authz_";

    private readonly string[] _permissions;
    private readonly ResourceType _type;
    private readonly OrganizationId _organization;
    private readonly SubjectSet _subjects;
    private readonly DateTimeOffset _at;

    /// <summary>
    /// The rule one principal's evaluation of a set of permissions over one resource
    /// type within one organization runs.
    /// </summary>
    /// <param name="permissions">What is being asked for.</param>
    /// <param name="type">The kind of thing the rows are.</param>
    /// <param name="organization">The organization the evaluation is scoped to.</param>
    /// <param name="subjects">Who holds grants for the principal.</param>
    /// <param name="at">The instant liveness is read at.</param>
    /// <exception cref="ArgumentNullException">The permissions or the subjects are absent.</exception>
    public PermissionRule(
        IReadOnlyList<Permission> permissions,
        ResourceType type,
        OrganizationId organization,
        SubjectSet subjects,
        DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        ArgumentNullException.ThrowIfNull(subjects);

        _permissions = [.. permissions.Select(permission => permission.ToString())];
        _type = type;
        _organization = organization;
        _subjects = subjects;
        _at = at;
    }

    /// <summary>
    /// What the matched grants decide: a deny defeats every allow, and the nearest
    /// allow is the one that explains the outcome.
    /// </summary>
    /// <param name="candidates">The grants the rule matched, the deciding one first.</param>
    /// <returns>The grant that decided, and nothing where none matched.</returns>
    /// <exception cref="ArgumentNullException">The candidates are absent.</exception>
    public static CandidateGrant? Decides(IReadOnlyList<CandidateGrant> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        return candidates.FirstOrDefault(candidate => candidate.Deny)
            ?? (candidates.Count == 0 ? null : candidates[0]);
    }

    /// <summary>
    /// The rule as a predicate over the host's own rows, for the host to apply to its
    /// own query.
    /// </summary>
    /// <typeparam name="TResource">The host's row.</typeparam>
    /// <param name="sources">The contract tables and the identifier selector.</param>
    /// <returns>The predicate.</returns>
    /// <exception cref="ArgumentNullException">The sources are absent.</exception>
    public Expression<Func<TResource, bool>> ToExpression<TResource>(FilterSources<TResource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        IQueryable<AncestryEntry> ancestry = sources.Ancestry;
        IQueryable<EffectiveGrant> grants = sources.Grants;
        string[] permissions = _permissions;
        string type = _type.ToString();
        Guid organization = _organization.Value;
        Guid[] accounts = _subjects.Accounts;
        Guid[] groups = _subjects.Groups;
        DateTimeOffset at = _at;

        Expression<Func<string, bool>> byIdentifier = identifier =>
            grants.Any(grant =>
                !grant.Deny
                && permissions.Contains(grant.Permission)
                && grant.Organization == organization
                && grant.RevokedAt == null
                && (grant.ExpiresAt == null || grant.ExpiresAt > at)
                && ((grant.SubjectType == "user" && accounts.Contains(grant.SubjectId))
                    || (grant.SubjectType == "group" && groups.Contains(grant.SubjectId)))
                && (grant.ResourceType == null
                    || ancestry.Any(entry =>
                        entry.ResourceType == type
                        && entry.ResourceId == identifier
                        && entry.AncestorType == grant.ResourceType
                        && entry.AncestorId == grant.ResourceId)))
            && !grants.Any(grant =>
                grant.Deny
                && permissions.Contains(grant.Permission)
                && grant.Organization == organization
                && grant.RevokedAt == null
                && (grant.ExpiresAt == null || grant.ExpiresAt > at)
                && ((grant.SubjectType == "user" && accounts.Contains(grant.SubjectId))
                    || (grant.SubjectType == "group" && groups.Contains(grant.SubjectId)))
                && (grant.ResourceType == null
                    || ancestry.Any(entry =>
                        entry.ResourceType == type
                        && entry.ResourceId == identifier
                        && entry.AncestorType == grant.ResourceType
                        && entry.AncestorId == grant.ResourceId)));

        Expression body = new Substitution(byIdentifier.Parameters[0], sources.Identifier.Body)
            .Visit(byIdentifier.Body);

        return Expression.Lambda<Func<TResource, bool>>(body, sources.Identifier.Parameters[0]);
    }

    /// <summary>
    /// The rule as a fragment a hand-written query composes into its <c>WHERE</c>
    /// clause, with every value carried as a parameter.
    /// </summary>
    /// <param name="rowAlias">The alias the query gives the row.</param>
    /// <param name="column">The column of that row holding the record's identifier.</param>
    /// <returns>The fragment and its parameters.</returns>
    /// <exception cref="ArgumentException">
    /// The alias or the column is not an unquoted lower-case identifier, which is what
    /// would let a value into the fragment's text.
    /// </exception>
    public SqlFilter ToFragment(string rowAlias, string column)
    {
        string row = Identifier(rowAlias, nameof(rowAlias)) + "." + Identifier(column, nameof(column));

        string text = string.Create(
            CultureInfo.InvariantCulture,
            $"""
            (EXISTS (
                SELECT 1
                FROM janus.effective_grants AS {Prefix}allow
                WHERE {Prefix}allow.deny = false
                  AND {Matches(Prefix + "allow", row)}
            ) AND NOT EXISTS (
                SELECT 1
                FROM janus.effective_grants AS {Prefix}deny
                WHERE {Prefix}deny.deny = true
                  AND {Matches(Prefix + "deny", row)}
            ))
            """);

        return new SqlFilter(text, Parameters());
    }

    /// <summary>
    /// The grants the rule matches on one record, the one that decides first: a deny
    /// before any allow, and the nearest container before a further one.
    /// </summary>
    /// <returns>The statement and its parameters, naming the record by parameter.</returns>
    public SqlFilter ToCandidates() => new(
        string.Create(
            CultureInfo.InvariantCulture,
            $"""
            SELECT {Prefix}grant.grant_id AS "{nameof(CandidateGrant.Grant)}",
                   {Prefix}grant.kind AS "{nameof(CandidateGrant.Kind)}",
                   {Prefix}grant.subject_type AS "{nameof(CandidateGrant.SubjectType)}",
                   {Prefix}grant.subject_id AS "{nameof(CandidateGrant.SubjectId)}",
                   {Prefix}grant.role AS "{nameof(CandidateGrant.Role)}",
                   {Prefix}grant.deny AS "{nameof(CandidateGrant.Deny)}",
                   {Prefix}above.ancestor_type AS "{nameof(CandidateGrant.AncestorType)}",
                   {Prefix}above.ancestor_id AS "{nameof(CandidateGrant.AncestorId)}"
            FROM janus.effective_grants AS {Prefix}grant
            LEFT JOIN janus.ancestry AS {Prefix}above
              ON {Prefix}above.resource_type = @{Prefix}type
             AND {Prefix}above.resource_id = @{RecordParameter}
             AND {Prefix}above.ancestor_type = {Prefix}grant.resource_type
             AND {Prefix}above.ancestor_id = {Prefix}grant.resource_id
            WHERE {Matches(Prefix + "grant", "@" + RecordParameter)}
            ORDER BY {Prefix}grant.deny DESC,
                     COALESCE({Prefix}above.depth, 2147483647) ASC,
                     {Prefix}grant.grant_id ASC;
            """),
        Parameters());

    /// <summary>
    /// What each record of a page confers, for every permission the rule names, in one
    /// query rather than one per row.
    /// </summary>
    /// <returns>The statement and its parameters, naming the page by parameter.</returns>
    public SqlFilter ToPage() => new(
        string.Create(
            CultureInfo.InvariantCulture,
            $"""
            SELECT {PageAlias}.{PageColumn} AS "{nameof(PageCapability.Resource)}",
                   {Prefix}grant.permission AS "{nameof(PageCapability.Permission)}",
                   bool_or({Prefix}grant.deny) AS "{nameof(PageCapability.Denied)}"
            FROM unnest(CAST(@{PageParameter} AS text[])) AS {PageAlias}({PageColumn})
            JOIN janus.effective_grants AS {Prefix}grant
              ON {Matches(Prefix + "grant", PageAlias + "." + PageColumn)}
            GROUP BY {PageAlias}.{PageColumn}, {Prefix}grant.permission;
            """),
        Parameters());

    // A fragment names the caller's row, so the two pieces of it that cannot be
    // parameters are held to what an unquoted identifier may be. Everything else the
    // fragment carries is a parameter (AUTHZ-GATE-002 AC3).
    private static string Identifier(string value, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, name);

        bool admitted = value.Length <= 63
            && char.IsAsciiLetterLower(value[0])
            && value.All(character => char.IsAsciiLetterLower(character)
                || char.IsAsciiDigit(character)
                || character == '_');

        if (!admitted)
        {
            throw new ArgumentException("The value is not an unquoted lower-case identifier.", name);
        }

        return value;
    }

    // The one predicate every rendering is built from: a live grant for one of the
    // permissions, held by one of the principal's subjects, in this organization, on
    // the record itself or on something above it.
    private static string Matches(string grant, string row) => string.Create(
        CultureInfo.InvariantCulture,
        $"""
        {grant}.permission = ANY(@{Prefix}permissions)
                  AND {grant}.organization = @{Prefix}organization
                  AND {grant}.revoked_at IS NULL
                  AND ({grant}.expires_at IS NULL OR {grant}.expires_at > @{Prefix}at)
                  AND (({grant}.subject_type = 'user'
                          AND {grant}.subject_id = ANY(@{Prefix}accounts))
                    OR ({grant}.subject_type = 'group'
                          AND {grant}.subject_id = ANY(@{Prefix}groups)))
                  AND ({grant}.resource_type IS NULL OR EXISTS (
                      SELECT 1
                      FROM janus.ancestry AS {grant}_above
                      WHERE {grant}_above.resource_type = @{Prefix}type
                        AND {grant}_above.resource_id = {row}
                        AND {grant}_above.ancestor_type = {grant}.resource_type
                        AND {grant}_above.ancestor_id = {grant}.resource_id))
        """);

    private Dictionary<string, object> Parameters() => new(StringComparer.Ordinal)
    {
        [Prefix + "permissions"] = _permissions,
        [Prefix + "type"] = _type.ToString(),
        [Prefix + "organization"] = _organization.Value,
        [Prefix + "accounts"] = _subjects.Accounts,
        [Prefix + "groups"] = _subjects.Groups,
        [Prefix + "at"] = _at,
    };

    // The predicate is written once over an identifier and then read over the host's
    // own row, so that one rule serves both renderings rather than two being kept
    // alike by hand (AUTHZ-PRIN-001).
    private sealed class Substitution(ParameterExpression parameter, Expression replacement)
        : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == parameter ? replacement : base.VisitParameter(node);
    }
}
