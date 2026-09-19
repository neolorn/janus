using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
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

    private readonly IReadOnlyList<RelationshipDeclaration> _derivations;
    private readonly string[] _permissions;
    private readonly ResourceType? _type;
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
    /// <param name="derivations">
    /// The relationships whose derivations confer one of the permissions on records of
    /// the type, each rendered beside the grants (AUTHZ-DERIVE-002).
    /// </param>
    /// <exception cref="ArgumentNullException">The permissions or the subjects are absent.</exception>
    public PermissionRule(
        IReadOnlyList<Permission> permissions,
        ResourceType type,
        OrganizationId organization,
        SubjectSet subjects,
        DateTimeOffset at,
        IReadOnlyList<RelationshipDeclaration>? derivations = null)
        : this(permissions, organization, subjects, at)
    {
        _type = type;
        _derivations = derivations ?? [];
    }

    /// <summary>
    /// The same rule asked of the organization itself, which is the third scope a
    /// grant may name (AUTHZ-GRANT-001).
    /// </summary>
    /// <param name="permissions">What is being asked for.</param>
    /// <param name="organization">The organization the evaluation is scoped to.</param>
    /// <param name="subjects">Who holds grants for the principal.</param>
    /// <param name="at">The instant liveness is read at.</param>
    /// <exception cref="ArgumentNullException">The permissions or the subjects are absent.</exception>
    public PermissionRule(
        IReadOnlyList<Permission> permissions,
        OrganizationId organization,
        SubjectSet subjects,
        DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        ArgumentNullException.ThrowIfNull(subjects);

        _permissions = [.. permissions.Select(permission => permission.ToString())];
        _organization = organization;
        _subjects = subjects;
        _at = at;
        _derivations = [];
    }

    // Every rendering but the organization-wide one is about records of one type, and
    // the type is what the ancestry is read by.
    private ResourceType Type => _type
        ?? throw new InvalidOperationException(
            "The rule was built for the organization itself and names no resource type.");

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
        string type = Type.ToString();
        Guid organization = _organization.Value;
        Guid[] accounts = _subjects.Accounts;
        Guid[] groups = _subjects.Groups;
        DateTimeOffset at = _at;

        Expression<Func<string, bool>> allowed = identifier =>
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
                        && entry.AncestorId == grant.ResourceId)));

        Expression<Func<string, bool>> denied = identifier =>
            grants.Any(grant =>
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

        ParameterExpression named = allowed.Parameters[0];
        Expression reaches = allowed.Body;

        // AUTHZ-DERIVE-001, AUTHZ-DERIVE-002 (D-160): a grant or a relationship row for
        // one of the principal's subjects, on the record or on something above it, and
        // a deny defeats either.
        foreach (RelationshipDeclaration relationship in _derivations)
        {
            reaches = Expression.OrElse(reaches, Derived(relationship, sources, ancestry, named));
        }

        Expression body = Expression.AndAlso(
            reaches,
            Expression.Not(new Substitution(denied.Parameters[0], named).Visit(denied.Body)));

        return Expression.Lambda<Func<TResource, bool>>(
            new Substitution(named, sources.Identifier.Body).Visit(body),
            sources.Identifier.Parameters[0]);
    }

    /// <summary>
    /// The records of a page that a derivation admits, as a query over the host's own
    /// relations for the host's context to run.
    /// </summary>
    /// <typeparam name="TResource">The host's row.</typeparam>
    /// <param name="sources">The contract tables and the rows of each relationship.</param>
    /// <param name="resources">The records being asked about.</param>
    /// <returns>
    /// The query, or nothing where the rule follows from no derivation and the stored
    /// grants are the whole of the answer.
    /// </returns>
    /// <exception cref="ArgumentNullException">The sources or the records are absent.</exception>
    /// <remarks>
    /// AUTHZ-PRIN-001, D-161: this is the one rule's derived clause read the other way
    /// round, from the record to the relationship rather than from the row, so that a
    /// check and a page decide what the filter decides.
    /// </remarks>
    public IQueryable<string>? ToAdmitted<TResource>(
        FilterSources<TResource> sources,
        IReadOnlyList<ResourceId> resources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(resources);

        if (_derivations.Count == 0 || resources.Count == 0)
        {
            return null;
        }

        string[] page = [.. resources.Select(resource => resource.ToString())];
        IQueryable<AncestryEntry> ancestry = sources.Ancestry;
        string type = Type.ToString();
        IQueryable<string>? admitted = null;

        foreach (RelationshipDeclaration relationship in _derivations)
        {
            IQueryable<string> one = Admits(sources, ancestry, type, page, relationship);

            admitted = admitted is null ? one : admitted.Union(one);
        }

        return admitted?.Distinct();
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
            ((EXISTS (
                SELECT 1
                FROM janus.effective_grants AS {Prefix}allow
                WHERE {Prefix}allow.deny = false
                  AND {Matches(Prefix + "allow", row)}
            ){Derived(row)}) AND NOT EXISTS (
                SELECT 1
                FROM janus.effective_grants AS {Prefix}deny
                WHERE {Prefix}deny.deny = true
                  AND {Matches(Prefix + "deny", row)}
            ))
            """);

        return new SqlFilter(text, FragmentParameters());
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
    /// The grants the rule matches on the organization itself, the one that decides
    /// first. Nothing is inherited here: a grant naming a record confers nothing over
    /// the organization that record sits in.
    /// </summary>
    /// <returns>The statement and its parameters.</returns>
    public SqlFilter ToOrganizationCandidates() => new(
        string.Create(
            CultureInfo.InvariantCulture,
            $"""
            SELECT {Prefix}grant.grant_id AS "{nameof(CandidateGrant.Grant)}",
                   {Prefix}grant.kind AS "{nameof(CandidateGrant.Kind)}",
                   {Prefix}grant.subject_type AS "{nameof(CandidateGrant.SubjectType)}",
                   {Prefix}grant.subject_id AS "{nameof(CandidateGrant.SubjectId)}",
                   {Prefix}grant.role AS "{nameof(CandidateGrant.Role)}",
                   {Prefix}grant.deny AS "{nameof(CandidateGrant.Deny)}",
                   CAST(NULL AS text) AS "{nameof(CandidateGrant.AncestorType)}",
                   CAST(NULL AS text) AS "{nameof(CandidateGrant.AncestorId)}"
            FROM janus.effective_grants AS {Prefix}grant
            WHERE {MatchesOrganization(Prefix + "grant")}
            ORDER BY {Prefix}grant.deny DESC, {Prefix}grant.grant_id ASC;
            """),
        OrganizationParameters());

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

    // AUTHZ-DERIVE-001 (D-160): the rows are the host's, and what the predicate over
    // one of them asks is that its holder is one of the principal's subjects and that
    // the record it names is this one or one above it.
    private Expression Derived<TResource>(
        RelationshipDeclaration relationship,
        FilterSources<TResource> sources,
        IQueryable<AncestryEntry> ancestry,
        ParameterExpression named)
    {
        if (!sources.Relationships.TryGetValue(relationship.Name, out RelationshipRows? rows))
        {
            throw new InvalidOperationException(string.Create(
                CultureInfo.InvariantCulture,
                $"A derivation follows from the relationship '{relationship.Name}', whose rows the filter was not given."));
        }

        string type = Type.ToString();
        string on = relationship.On.ToString();

        Expression<Func<string, string, bool>> above = (resource, identifier) =>
            ancestry.Any(entry =>
                entry.ResourceType == type
                && entry.ResourceId == identifier
                && entry.AncestorType == on
                && entry.AncestorId == resource);

        ParameterExpression row = relationship.Holder.Parameters[0];

        Expression held = new Substitution(
                above.Parameters[0],
                new Substitution(relationship.Resource.Parameters[0], row)
                    .Visit(relationship.Resource.Body))
            .Visit(above.Body);

        return rows.Any(Expression.Lambda(
            Expression.AndAlso(
                Holds(relationship.Holder.Body),
                new Substitution(above.Parameters[1], named).Visit(held)),
            row));
    }

    // The same clause as Derived, asked from the ancestry rather than from the host's
    // row: the records of the page with a container the relationship names, held by one
    // of the principal's subjects. One query per derivation, whatever the page's size
    // (AUTHZ-GATE-005 AC1).
    private IQueryable<string> Admits<TResource>(
        FilterSources<TResource> sources,
        IQueryable<AncestryEntry> ancestry,
        string type,
        string[] page,
        RelationshipDeclaration relationship)
    {
        if (!sources.Relationships.TryGetValue(relationship.Name, out RelationshipRows? rows))
        {
            throw new InvalidOperationException(string.Create(
                CultureInfo.InvariantCulture,
                $"A derivation follows from the relationship '{relationship.Name}', whose rows the filter was not given."));
        }

        string on = relationship.On.ToString();

        Expression<Func<AncestryEntry, bool>> scoped = entry =>
            entry.ResourceType == type
            && page.Contains(entry.ResourceId)
            && entry.AncestorType == on;

        ParameterExpression above = scoped.Parameters[0];
        ParameterExpression row = relationship.Holder.Parameters[0];

        Expression names = Expression.Equal(
            new Substitution(relationship.Resource.Parameters[0], row)
                .Visit(relationship.Resource.Body),
            Expression.Property(above, nameof(AncestryEntry.AncestorId)));

        Expression held = rows.Any(Expression.Lambda(
            Expression.AndAlso(Holds(relationship.Holder.Body), names),
            row));

        return ancestry
            .Where(Expression.Lambda<Func<AncestryEntry, bool>>(
                Expression.AndAlso(scoped.Body, held),
                above))
            .Select(entry => entry.ResourceId);
    }

    // Whether the row is held by one of the principal's subjects. The column is the
    // host's, mapped by the host's own model, so the set stands against it as the
    // subject identifiers the host's model reads there.
    private Expression Holds(Expression holder)
    {
        SubjectId[] subjects = [.. _subjects.Accounts.Select(account => new SubjectId(account))];
        Expression<Func<SubjectId, bool>> held = candidate => subjects.Contains(candidate);

        return new Substitution(held.Parameters[0], holder).Visit(held.Body);
    }

    // The same rule over the host's own relations, one clause per derivation. The
    // relation and its two columns are the declaration's; everything else is a
    // parameter (AUTHZ-GATE-002 AC3).
    private string Derived(string row)
    {
        var text = new StringBuilder();

        for (int index = 0; index < _derivations.Count; index++)
        {
            RelationshipDeclaration relationship = _derivations[index];
            string held = Alias(index);

            text.Append(string.Create(
                CultureInfo.InvariantCulture,
                $"""
                 OR EXISTS (
                    SELECT 1
                    FROM {Relation(relationship.Relation)} AS {held}
                    WHERE {held}.{Identifier(relationship.HolderColumn, nameof(relationship.HolderColumn))} = ANY(@{Prefix}accounts)
                      AND EXISTS (
                          SELECT 1
                          FROM janus.ancestry AS {held}_above
                          WHERE {held}_above.resource_type = @{Prefix}type
                            AND {held}_above.resource_id = {row}
                            AND {held}_above.ancestor_type = @{held}_on
                            AND {held}_above.ancestor_id = {held}.{Identifier(relationship.ResourceColumn, nameof(relationship.ResourceColumn))}))
                """));
        }

        return text.ToString();
    }

    private static string Alias(int index) =>
        Prefix + "derived" + index.ToString(CultureInfo.InvariantCulture);

    // A relation may be schema-qualified, and every part of it is held to what an
    // unquoted identifier may be.
    private static string Relation(string relation) =>
        string.Join('.', relation.Split('.').Select(part => Identifier(part, nameof(relation))));

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
        {Live(grant)}
                  AND ({grant}.resource_type IS NULL OR EXISTS (
                      SELECT 1
                      FROM janus.ancestry AS {grant}_above
                      WHERE {grant}_above.resource_type = @{Prefix}type
                        AND {grant}_above.resource_id = {row}
                        AND {grant}_above.ancestor_type = {grant}.resource_type
                        AND {grant}_above.ancestor_id = {grant}.resource_id))
        """);

    // The same predicate asked of the organization itself, which AUTHZ-GRANT-001 makes
    // a scope a grant may name: only a grant naming no record reaches it, and a grant
    // on one record confers nothing over the organization it sits in.
    private static string MatchesOrganization(string grant) => string.Create(
        CultureInfo.InvariantCulture,
        $"""
        {Live(grant)}
                  AND {grant}.resource_type IS NULL
        """);

    // What every rendering asks of a grant before it asks what the grant reaches.
    private static string Live(string grant) => string.Create(
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
        """);

    private Dictionary<string, object> Parameters() => new(StringComparer.Ordinal)
    {
        [Prefix + "permissions"] = _permissions,
        [Prefix + "type"] = Type.ToString(),
        [Prefix + "organization"] = _organization.Value,
        [Prefix + "accounts"] = _subjects.Accounts,
        [Prefix + "groups"] = _subjects.Groups,
        [Prefix + "at"] = _at,
    };

    // The fragment is the one rendering that reaches the host's own relations, so it
    // is the one that names the type each derivation is declared on.
    private Dictionary<string, object> FragmentParameters()
    {
        Dictionary<string, object> parameters = Parameters();

        for (int index = 0; index < _derivations.Count; index++)
        {
            parameters[Alias(index) + "_on"] = _derivations[index].On.ToString();
        }

        return parameters;
    }

    // The organization-wide rendering reads no ancestry, so it carries no resource
    // type: the statement would hold a parameter it never names.
    private Dictionary<string, object> OrganizationParameters() => new(StringComparer.Ordinal)
    {
        [Prefix + "permissions"] = _permissions,
        [Prefix + "organization"] = _organization.Value,
        [Prefix + "accounts"] = _subjects.Accounts,
        [Prefix + "groups"] = _subjects.Groups,
        [Prefix + "at"] = _at,
    };
}
