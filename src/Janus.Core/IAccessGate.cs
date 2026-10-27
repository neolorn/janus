using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// The one place a permission is evaluated. A check and a list filter are the same
/// rule asked two ways, so a list screen cannot come to show what a check would
/// refuse.
/// </summary>
/// <remarks>
/// Implements AUTHZ-SEAM-001, AUTHZ-PRIN-001, AUTHZ-PRIN-002, AUTHZ-GATE-001,
/// AUTHZ-GATE-002, AUTHZ-GATE-004, AUTHZ-GATE-005, AUTHZ-GATE-007, OPS-ALERT-006 and
/// LIB-SEAM-001.
/// Every evaluation is scoped to the organization owning the record, resolved from the
/// record and never from a session (AUTHZ-SCOPE-001), and every path fails closed
/// (AUTHZ-PRIN-003). A permission the host declares with the action <c>export</c> is an
/// export operation: while <c>exfiltration.export.stepuprequired</c> is on it asks for
/// step-up whether or not the host bound it to a gate, and each check, filter or
/// fragment that admits it counts against the actor's
/// <c>exfiltration.export.ratelimit</c> for the hour and is recorded on its own. A
/// resource type or a permission the model does not declare, named at any member that
/// takes one, is the calling code's fault: it raises
/// <see cref="InvalidOperationException"/> before anything is read or recorded
/// (AUTHZ-PRIN-003 AC1, CONV-ERR-001 AC3).
/// </remarks>
public interface IAccessGate
{
    /// <summary>
    /// Whether the caller may do this to this record.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="permission">What they are asking to do.</param>
    /// <param name="resource">Which record.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Nothing, or <c>authz.denied</c> carrying the correlation identifier the audit
    /// trail records it under. What the caller is answered is the type's declared
    /// concealment behaviour: on a disclosing type the refusal says the record exists
    /// and is forbidden; on a concealing one the browser profile answers the request as
    /// a record that does not exist, <c>authz.resource.notfound</c> under the same
    /// identifier, whatever the endpoint goes on to write.
    /// </returns>
    ValueTask<Result> RequireAsync(
        AccessContext context,
        Permission permission,
        ResourceReference resource,
        CancellationToken cancellationToken);

    /// <summary>
    /// Whether the caller may do this to this record, on a type whose access follows
    /// in part from a fact in the host's own data.
    /// </summary>
    /// <typeparam name="TResource">The host's row.</typeparam>
    /// <param name="context">Who is asking.</param>
    /// <param name="permission">What they are asking to do.</param>
    /// <param name="resource">Which record.</param>
    /// <param name="sources">
    /// The same contract tables and relationship rows the filter takes, from the host's
    /// own context.
    /// </param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The same answer as the overload without sources, with every declared derivation
    /// evaluated beside the stored grants.
    /// </returns>
    /// <remarks>
    /// AUTHZ-DERIVE-001, AUTHZ-PRIN-001 AC2, D-161: a type a derivation reaches, on
    /// itself or through a container, is asked through this overload, whatever the role
    /// the derivation confers allows; the one without sources is refused with
    /// <c>authz.derivation.sourcesmissing</c>, so no path answers from stored grants
    /// alone.
    /// </remarks>
    ValueTask<Result> RequireAsync<TResource>(
        AccessContext context,
        Permission permission,
        ResourceReference resource,
        FilterSources<TResource> sources,
        CancellationToken cancellationToken);

    /// <summary>
    /// Whether the caller may do this at all, where what is being asked for is not
    /// tied to one record.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="permission">What they are asking to do.</param>
    /// <param name="organization">The organization they are asking within.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Nothing, or <c>authz.denied</c>. Nothing is being concealed when an
    /// administrative operation is called without the permission, so the caller answers
    /// that the operation is forbidden (AUTHZ-CONCEAL-005).
    /// </returns>
    ValueTask<Result> RequireAsync(
        AccessContext context,
        Permission permission,
        OrganizationId organization,
        CancellationToken cancellationToken);

    /// <summary>
    /// The same rule as a predicate over the host's own rows, for the host to apply to
    /// its own query.
    /// </summary>
    /// <typeparam name="TResource">The host's row.</typeparam>
    /// <param name="context">Who is asking.</param>
    /// <param name="permission">What they are asking to do.</param>
    /// <param name="type">The kind of thing the rows are.</param>
    /// <param name="organization">The organization the listing is within.</param>
    /// <param name="sources">The contract tables and the identifier selector.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The predicate, which enumerates nothing and returns no record; or, asked in this
    /// order before any rule is handed out: <c>authz.denied</c> carrying the correlation
    /// identifier the audit trail records it under, for a host job's context asked while
    /// a request is served, or for a permission or an organization its declaration does
    /// not reach, or while its organization's deletion is requested (IDN-PRIN-001,
    /// IDN-ORG-003); <c>authz.restricted</c>, recorded nowhere, where the account is
    /// restricted and the permission is modifying, a reading permission being handed its
    /// rule (AUTHZ-GATE-006, D-209); <c>auth.stepup.required</c> where the action is bound
    /// to a step-up gate the session has not met; or <c>auth.throttled</c> carrying
    /// <c>retryAt</c> where the action is an export the actor has no place left for this
    /// hour.
    /// </returns>
    ValueTask<Result<Expression<Func<TResource, bool>>>> FilterAsync<TResource>(
        AccessContext context,
        Permission permission,
        ResourceType type,
        OrganizationId organization,
        FilterSources<TResource> sources,
        CancellationToken cancellationToken);

    /// <summary>
    /// The same rule as a fragment a hand-written query composes into its <c>WHERE</c>
    /// clause. The fragment is PostgreSQL, as AUTHZ-GATE-003 and LIB-API-004 fix it.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="permission">What they are asking to do.</param>
    /// <param name="type">The kind of thing the rows are.</param>
    /// <param name="organization">The organization the listing is within.</param>
    /// <param name="rowAlias">The alias the query gives the row.</param>
    /// <param name="column">The column of that row holding the record's identifier.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The fragment and its parameters; or the refusals of
    /// <see cref="FilterAsync{TResource}"/>, in its order and on the same grounds:
    /// <c>authz.denied</c> carrying the correlation identifier for a host job's context
    /// the list refuses, <c>authz.restricted</c>, recorded nowhere, for a restricted
    /// account and a modifying permission, <c>auth.stepup.required</c> where the action
    /// is bound to a step-up gate the session has not met, or <c>auth.throttled</c>
    /// carrying <c>retryAt</c> where the action is an export the actor has no place left
    /// for this hour.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// The row alias or the column is not an identifier, which is what would let a value
    /// into the fragment's text.
    /// </exception>
    ValueTask<Result<SqlFilter>> FragmentAsync(
        AccessContext context,
        Permission permission,
        ResourceType type,
        OrganizationId organization,
        string rowAlias,
        string column,
        CancellationToken cancellationToken);

    /// <summary>
    /// Why access was granted or refused, naming the grant that decided or stating
    /// that none matched.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="permission">What they are asking to do.</param>
    /// <param name="resource">Which record.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The explanation, or <c>authz.denied</c> where the type conceals its records and
    /// the caller is asking about their own denial: an explanation saying that no grant
    /// matched would disclose that the record exists (AUTHZ-GATE-004).
    /// </returns>
    ValueTask<Result<AccessExplanation>> ExplainAsync(
        AccessContext context,
        Permission permission,
        ResourceReference resource,
        CancellationToken cancellationToken);

    /// <summary>
    /// Why access was granted or refused, on a type whose access follows in part from a
    /// fact in the host's own data.
    /// </summary>
    /// <typeparam name="TResource">The host's row.</typeparam>
    /// <param name="context">Who is asking.</param>
    /// <param name="permission">What they are asking to do.</param>
    /// <param name="resource">Which record.</param>
    /// <param name="sources">
    /// The same contract tables and relationship rows the check and the page take, from
    /// the host's own context.
    /// </param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The same explanation as the overload without sources, with every declared
    /// derivation evaluated beside the stored grants. A grant a fact produced carries
    /// no identifier and names itself as derived (AUTHZ-DERIVE-005, chapter 10 section
    /// 5.6).
    /// </returns>
    /// <remarks>
    /// AUTHZ-GATE-004, AUTHZ-DERIVE-001, D-162: a type a derivation reaches is
    /// explained through this overload, and the one without sources is refused with
    /// <c>authz.derivation.sourcesmissing</c> rather than saying that no grant matched
    /// for a record the filter admits.
    /// </remarks>
    ValueTask<Result<AccessExplanation>> ExplainAsync<TResource>(
        AccessContext context,
        Permission permission,
        ResourceReference resource,
        FilterSources<TResource> sources,
        CancellationToken cancellationToken);

    /// <summary>
    /// Who can access a record, and through which grant or container, for a caller
    /// holding <c>grant:read</c> in the organization the record sits in.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="resource">
    /// Which record, or the organization itself under the type <c>organization</c>.
    /// </param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The live stored and materialised grants on the record, on what contains it and
    /// on its whole organization, and the grants every derivation reaching the record
    /// produces, each evaluated over the rows the relationship source the host declared
    /// answers (LIB-HOST-001), in one statement in the host's context. A grant a fact
    /// produced carries no identifier and names itself as derived. Evaluation stops at
    /// <c>authz.reverselookup.budget</c>, and what it did not reach is named. Or
    /// <c>api.request.malformed</c> naming <c>resourceType</c> where the type is not
    /// declared, or <c>resourceId</c> where the type is <c>organization</c> and the
    /// identifier is not a UUID; <c>authz.denied</c> without the permission, and for a
    /// record the registry does not hold, which belongs to no organization and is
    /// refused exactly as a caller without the permission is refused, recorded against
    /// no organization and counted; and <c>authz.derivation.sourcesmissing</c>, asked
    /// after the permission, where a derivation the host's rows decide reaches the type
    /// and no source is declared for its relationship, since the stored grants alone
    /// are not the answer.
    /// </returns>
    /// <remarks>
    /// AUTHZ-DERIVE-007 AC1 and AC2, AUTHZ-DERIVE-005 AC6: stored and derived grants are
    /// reported apart, and where the derivations make the answer unbounded the answer
    /// says so rather than being partial in silence.
    /// </remarks>
    ValueTask<Result<ResourceAccess>> WhoCanAccessAsync(
        AccessContext context,
        ResourceReference resource,
        CancellationToken cancellationToken);

    /// <summary>
    /// Who can access a record, on a type whose access follows in part from a fact in
    /// the host's own data.
    /// </summary>
    /// <typeparam name="TResource">The host's row.</typeparam>
    /// <param name="context">Who is asking.</param>
    /// <param name="resource">Which record.</param>
    /// <param name="sources">
    /// The relationship rows the derivations are evaluated over, from the host's own
    /// context.
    /// </param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The same answer and the same refusals as the overload without sources, with
    /// every declared derivation evaluated over the rows handed in, for the record and
    /// what contains it, whether or not a source is declared for its relationship.
    /// </returns>
    /// <remarks>
    /// AUTHZ-DERIVE-007 AC1 and AC2: stored and derived grants are reported apart, and
    /// where the derivations make the answer unbounded the answer says so rather than
    /// being partial in silence.
    /// </remarks>
    ValueTask<Result<ResourceAccess>> WhoCanAccessAsync<TResource>(
        AccessContext context,
        ResourceReference resource,
        FilterSources<TResource> sources,
        CancellationToken cancellationToken);

    /// <summary>
    /// Which accounts may exercise a permission on a record, or across an organization,
    /// as the single check would admit each of them.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="permission">What the accounts would be doing.</param>
    /// <param name="resource">
    /// Which record, or the organization itself under the type <c>organization</c>.
    /// </param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The accounts, in state active or, for a reading permission, restricted, that the
    /// record's stored and materialised grants, its live derivations evaluated over the
    /// relationship sources the host declared (LIB-HOST-001), its containers and its
    /// organization admit, directly or through a group, a deny defeating them, and in the
    /// administrative organization only while each is a current member; for the
    /// organization, those its organization-wide grants admit. None where the
    /// permission's purpose asks a consent the record's data subject has not given.
    /// Evaluation stops at <c>authz.reverselookup.budget</c>, and what it did not reach
    /// is named. Or <c>authz.denied</c> for a person without <c>grant:read</c> in the
    /// organization the record belongs to, for a job whose declaration does not name the
    /// permission or the organization, for a job's context asked while a request is
    /// served, and for a record the registry does not hold, or an organization
    /// identifier that is not one; and <c>authz.derivation.sourcesmissing</c> where a
    /// derivation the host's rows decide reaches the type and no source is declared for
    /// its relationship.
    /// </returns>
    /// <remarks>
    /// AUTHZ-GATE-007 (D-218): a step-up is a session's and not an account's, so none is
    /// judged; nothing is recorded or counted for the accounts judged, only a refusal of
    /// the one asking.
    /// </remarks>
    ValueTask<Result<AdmittedAccounts>> WhoMayAsync(
        AccessContext context,
        Permission permission,
        ResourceReference resource,
        CancellationToken cancellationToken);

    /// <summary>
    /// The refusal a correlation identifier stands for, for a support role holding
    /// <c>audit:read</c> in the administrative organization.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="correlation">The identifier the refusal was answered with.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The explanation the refusal was recorded with, or <c>authz.denied</c> where the
    /// caller does not hold <c>audit:read</c> there or the identifier stands for no
    /// refusal. It names the permission and the principal and nothing about the record
    /// (AUTHZ-CONCEAL-004).
    /// </returns>
    ValueTask<Result<AccessExplanation>> ResolveAsync(
        AccessContext context,
        AuditRecordId correlation,
        CancellationToken cancellationToken);

    /// <summary>
    /// The refusal a correlation identifier stands for, for the principal it refused,
    /// on a type whose refusal discloses that the operation is forbidden.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="correlation">The identifier the refusal was answered with.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The explanation the refusal was recorded with, or <c>authz.denied</c> where the
    /// identifier stands for no refusal of this principal or for one on a type whose
    /// refusal answers as a record that does not exist (AUTHZ-GATE-004).
    /// </returns>
    ValueTask<Result<AccessExplanation>> ResolveOwnAsync(
        AccessContext context,
        AuditRecordId correlation,
        CancellationToken cancellationToken);

    /// <summary>
    /// What the caller may do to each of these records, computed for the whole page at
    /// once rather than a query per row.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="type">
    /// The kind of thing the records are, or <c>organization</c>, which no host declares,
    /// for what the caller may do across the whole of each organization named.
    /// </param>
    /// <param name="resources">The records of the page, or the organizations' identifiers.</param>
    /// <param name="permissions">
    /// The permissions the caller's surface offers on them, each one the host declared as
    /// a capability of the type, or, of an organization, any declared permission, the
    /// library's included; naming any other is the calling code's fault, and raises
    /// (BFF-CAP-002).
    /// </param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>One capability per record, in the order the records were given.</returns>
    /// <remarks>
    /// BFF-CAP-002 (D-211): an organization's capabilities are what its organization-wide
    /// check admits, through a grant naming no record, a deny there defeating it, every
    /// permission judged in one statement. No refusal is recorded or counted, and what a
    /// capability still requires never holds a consent, since the check names no data
    /// subject; consent is asked of each record. A host job's context is refused as for
    /// any page.
    /// </remarks>
    ValueTask<Result<IReadOnlyList<Capability>>> CapabilitiesAsync(
        AccessContext context,
        ResourceType type,
        IReadOnlyList<ResourceId> resources,
        IReadOnlyList<Permission> permissions,
        CancellationToken cancellationToken);

    /// <summary>
    /// What the caller may do to each of these records, on a type whose access follows
    /// in part from a fact in the host's own data.
    /// </summary>
    /// <typeparam name="TResource">The host's row.</typeparam>
    /// <param name="context">Who is asking.</param>
    /// <param name="type">The kind of thing the records are.</param>
    /// <param name="resources">The records of the page.</param>
    /// <param name="permissions">
    /// The permissions the caller's surface offers on them, each one the host declared as
    /// a capability of the type; naming any other is the calling code's fault, and raises
    /// (BFF-CAP-002).
    /// </param>
    /// <param name="sources">
    /// The same contract tables and relationship rows the filter takes, from the host's
    /// own context.
    /// </param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// One capability per record, in the order the records were given, with every
    /// declared derivation evaluated beside the stored grants.
    /// </returns>
    /// <remarks>
    /// AUTHZ-DERIVE-001, AUTHZ-GATE-005 AC1, D-162: the derivations cost one further
    /// query for the whole page, carrying one clause each, whatever the page's size and
    /// however many permissions are asked for; the overload without sources is refused
    /// on a type a derivation reaches, whatever it confers.
    /// </remarks>
    ValueTask<Result<IReadOnlyList<Capability>>> CapabilitiesAsync<TResource>(
        AccessContext context,
        ResourceType type,
        IReadOnlyList<ResourceId> resources,
        IReadOnlyList<Permission> permissions,
        FilterSources<TResource> sources,
        CancellationToken cancellationToken);
}
