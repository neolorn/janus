# 03 — Authorization

Grants, roles, the model builder, permission filtering, and the access gate.

**Prerequisite:** `00-overview.md`, sections 1 and 3.2.

**Scope.** This document covers *what a principal may do*. Who exists is
`01-identity`. How they proved who they are is `02-authentication`. What may
lawfully be held is `04-privacy`.

---

## 1. Principles

**AUTHZ-PRIN-001** — Every permission rule SHALL be expressed **once** and SHALL
serve both a single check and a list filter.

*Source: D-015, P-003*

The two questions the system asks are "may this principal do X to Y?" and "which Ys
may this principal do X to?". Where these are written separately they drift, and the
list screen begins showing records the check would refuse — a silent data leak
rather than a crash.

**Acceptance criteria**
1. A test enumerating every truth-table case runs each through both the single check
   and the list filter and asserts identical outcomes.
2. No permission logic exists that is reachable by only one of the two paths.

---

**AUTHZ-PRIN-002** — Permission filtering SHALL occur inside the host's database
query, not by filtering results after retrieval.

*Source: D-015, D-017*

This is what keeps list screens, search and pagination fast, and is the principal
advantage of in-library authorization over an external service.

**Acceptance criteria**
1. A paged list endpoint issues one query; permission filtering is part of its
   `WHERE` clause.
2. Row count returned from the database equals rows displayed — no post-filtering.

---

**AUTHZ-PRIN-003** — Authorization SHALL fail closed.

*Source: P-003, D-166*

**Acceptance criteria**
1. A resource type or a permission the model does not declare, named at any gate entry
   point (the single check, the list filter, the SQL fragment, the capability page, the
   explanation), raises a programming fault before anything is read or recorded, rather
   than permitting.
2. An error resolving a principal denies.
3. No path returns allow on exception.

---

## 2. The permission model

### 2.1 Grants

**AUTHZ-GRANT-001** — Every permission SHALL be expressed as a single sentence:
**[subject] has [role] on [resource]**.

| Element | May be |
|---|---|
| Subject | A user **or** a group |
| Role | A named bundle of actions, stored as data |
| Resource | One item, a container, **or** the whole organization |

A grant SHALL be either **stored** — a row someone wrote — or **derived** — computed
from a relationship in the host's own data (AUTHZ-DERIVE-001). Both express the same
sentence and obey the same rules.

*Source: D-015, D-043*

There are no other kinds of permission. Sharing a record, making someone a branch
manager, granting a group access to a folder — all the same sentence with different
nouns.

**Acceptance criteria**
1. No permission exists that is not expressible as this sentence.
2. A grant with a null resource scopes to the whole organization.
3. The same table serves user grants and group grants.
4. Stored and derived grants are indistinguishable to a caller of the gate.

---

**AUTHZ-GRANT-002** — A deny grant SHALL be the same record with a deny flag, and
SHALL always defeat any allow.

*Source: D-015*

"Everyone in the team except one person" is one deny row. No priority numbers, no
ordering rules.

**Acceptance criteria**
1. A deny on a resource defeats an allow inherited from its container.
2. A deny defeats an organization-wide allow.
3. Removing the deny restores the allow without re-granting it.

---

**AUTHZ-GRANT-003** — Grants SHALL carry an optional expiry, and SHALL record who
granted, when, why, and who revoked.

**Values (D-153, D-166).** `reason` is required on every grant created or revoked and is
held to the free-text rule of API-CONV-002: absent, or blank after trimming, the request
is refused with `authz.grant.reasonrequired` (422).

A grant no person made SHALL record the nil subject as its granter, never its holder:
the grants bootstrap makes carry the reason `OPS-BOOT-001`, and a materialised grant the
drift check writes carries the reason `AUTHZ-DERIVE-005`, its audit record naming the
system principal `derivation-driftcheck` (AUTHZ-DERIVE-005).

*Source: D-015, D-166*

Expiry supports contractors, trials and temporary escalation. Free to add now; a
migration across every query later.

**Acceptance criteria**
1. An expired grant confers no access without any sweep having run.
2. The audit fields are populated on creation and on revocation.
3. "Who granted this and when" is answerable by query.
4. The grants bootstrap makes, and the grants the drift check materialises, record the
   nil subject as their granter, with the reasons `OPS-BOOT-001` and `AUTHZ-DERIVE-005`
   respectively.

---

**AUTHZ-GRANT-004** — Grants and roles SHALL be runtime-changeable without a deploy.

A change to a role SHALL carry a `reason` (API-CONV-002), SHALL be the `grant:manage`
step-up action (`10` section 5a), and SHALL be recorded with the role, its permissions
before and after, the reason and the actor. A change to a role that carries
`system:administer` before or after it SHALL require `system:administer`
(OPS-CFG-007). A change SHALL NOT take a library-owned permission out of a role the
reserved `emergency` account holds, and the `emergency` account's grant of
`system-administrator` SHALL NOT be revoked (OPS-BOOT-002). A role that a grant (live,
expired or revoked), a declared derivation or a standing invitation (neither
acknowledged nor revoked, expired or not) names SHALL NOT be removed
(`authz.role.inuse`).

*Source: D-010, D-015, D-166*

**Acceptance criteria**
1. Creating a role and assigning permissions to it requires no restart.
2. Granting and revoking take effect on the next request.
3. A role change without a reason is refused and changes nothing.
4. The record of a role change carries the permissions before and after.
5. A change taking a library-owned permission out of the role the `emergency` account
   holds, and a revocation of that account's `system-administrator` grant, are refused
   with `authz.denied` and change nothing.
6. Removing a role that a grant, a derivation or a standing invitation names, an
   expired one included, is refused with `authz.role.inuse` and changes nothing.

---

### 2.2 Groups

**AUTHZ-GROUP-001** — Groups SHALL nest, and membership SHALL be followed
transitively.

A group SHALL belong to one organization, and a group SHALL be a member only of a group
of its own organization. Every change to a group (creating it, removing it, adding or
removing a member) SHALL carry a `reason` (API-CONV-002) and SHALL be recorded with the
group, its name, the member where one changed, the reason and the actor, under the
group's organization. A change of members SHALL be the `grant:manage` step-up action
(`10` section 5a), and where the group reaches a grant of a role carrying
`system:administer` it SHALL also require that permission (OPS-CFG-007); creating a
group and removing one SHALL NOT be stepped up. A group that holds a member, belongs to
a group, or was given a grant, live, expired or revoked, SHALL NOT be removed
(`authz.group.inuse`).

*Source: D-015, D-166*

**Acceptance criteria**
1. A user in a team inside a department inherits grants held by the department.
2. Nesting depth is not fixed by the schema.
3. A change to a group without a reason is refused and changes nothing.
4. A change of members is refused without the `grant:manage` step-up; creating a group
   is not stepped up.
5. Adding a member already held, or removing one not held, changes and records nothing.
6. Removing a group anything names is refused with `authz.group.inuse`.

---

**AUTHZ-GROUP-002** — A principal's transitive group set SHALL be resolved **once
per request**, not per permission check.

*Source: D-007*

**Acceptance criteria**
1. Ten permission checks in one request resolve group membership once.
2. The resolved set is cached and invalidated per AUTHZ-CACHE-001.

---

### 2.3 Inheritance

**AUTHZ-INHERIT-001** — Access to a container SHALL confer access to everything
within it, at any depth.

*Source: D-015*

**Acceptance criteria**
1. A grant on a folder confers the same access on a document three levels beneath
   it.
2. Removing the grant removes the inherited access.

---

**AUTHZ-INHERIT-002** — Inheritance SHALL be resolved through a precomputed ancestry
closure, maintained in the **same transaction** as the resource create or move.
Recursive resolution at request time SHALL NOT be used.

**Values (D-166).** The create and move are `IResources.RegisterAsync`,
`RegisterManyAsync` and `MoveAsync` (`Janus.Core`), which the host calls inside its own
unit of work. A registration names the record, its organization, its container and,
where its type declares a subject column for its encrypted fields, its data subject read
from that column (AUTHZ-MODEL-003). A registration or move with a member absent or
unreadable, a type the model does not declare and a record of a sensitive type naming
no subject (IDN-LIFE-002a) included, is refused with 400 `api.request.malformed` naming
the member. One well formed and refused on its meaning is refused with 422
`api.request.invalid` naming the member: the record is already registered; its
container is not of the declared type, is unregistered, belongs to another
organization or is absent where the type requires one; or, for a sensitive type, the
subject it names holds no account, or one `deleting` or `deleted` (IDN-LIFE-002a). A
batch is judged whole before anything is written. `IResources` is a seam, not an
operation: it joins the host's transaction, has no endpoint and takes no access context
(LIB-API-005).
Placing a record, or moving it, confers on it the grants held on its new containers
(AUTHZ-INHERIT-001), so whether a caller may do it is the host's own action, which the
host asks of the gate before it writes.

*Source: D-015, D-017, D-166*

Precomputation is what keeps the rule composable into a query and therefore usable
as a `WHERE` clause. Same-transaction maintenance is why permission data and business
data cannot diverge — the advantage in-library authorization holds over an external
service.

**Acceptance criteria**
1. Creating a resource writes its ancestry in the same transaction; a rollback
   leaves neither.
2. Moving a subtree updates ancestry for every descendant.
3. A bulk import of 10,000 resources produces correct ancestry for all.
4. No permission query contains a recursive common table expression.

---

**AUTHZ-INHERIT-003** — Ancestry maintenance SHALL have dedicated test coverage for
create, move, bulk operations, and subtree moves.

*Source: D-015*

This is the highest-risk area in the whole design: a bug means someone sees a record
they should not, silently. No choice of registry syntax makes it easier or harder.

**Acceptance criteria**
1. Tests cover moving a subtree beneath its own former sibling.
2. Tests cover concurrent moves of overlapping subtrees.
3. A test asserts no orphaned ancestry rows remain after any operation.

---

### 2.4 Derived grants

**AUTHZ-DERIVE-001** — A **derivation** SHALL be declarable in the model builder,
stating that whoever holds a named relationship in the host's own data holds a given
role on a given resource type.

**Values (D-160).** A derivation names a **relationship the host supplies**: in the model
builder, the relationship's name, its resource type, the role it confers, and two
selectors over the host's relationship row (the holder's subject identifier, the
resource identifier), plus the SQL relation name and the two column names for the SQL
rendering. At filter time the host passes an `IQueryable` of that relationship row from
its own context, beside the ancestry and grant sets (AUTHZ-GATE-002); the LINQ rendering
composes it into the same `EXISTS` (a grant **or** a relationship row for one of the
principal's subjects, on the resource or an ancestor), and the SQL rendering names the
declared relation. The library issues no query of its own against a host table
(LIB-HOST-002): the host's context executes the composed query. A single check on a
type with derivations therefore runs as the filter applied to the one resource, through
the host's query, never as a library-side read. Concretely (D-161, D-162):
`RequireAsync`, `CapabilitiesAsync` and `ExplainAsync` take the same host-supplied
sources object the filter takes; on a type that a non-materialised derivation is
declared on or reaches through containment, whatever the role it confers allows, a call
without sources is refused with `authz.derivation.sourcesmissing`, a fault and not a
denial, so no path answers from stored grants alone (AUTHZ-PRIN-001 AC2). With the
sources, a single check composes the stored allow and deny grants and every derivation
reaching the type into one query in the host's context, and reads no grant through the
library's own connection (D-166); the capability page does the same (AUTHZ-GATE-005).

*Source: D-043, D-162, D-166*

A stored grant exists because someone wrote it. A derived grant exists because a
fact in the business data is true: "the assigned representative on an account holds
the reader role on that account's records." Evaluated where it is asked, it needs no
maintenance and cannot drift, because there is nothing to keep in sync; a materialised
derivation is checked for drift daily (AUTHZ-DERIVE-005).

This is the same construct Zanzibar calls a computed userset. It is the mechanism
that makes relationship-based access expressible without abandoning the model.

**Acceptance criteria**
1. A derivation is declared alongside containment and sensitivity, not in code.
2. Changing the underlying business data changes access on the next request, with
   no grant written or revoked.
3. Removing the relationship removes the access.
4. Without the host's sources, a check, a capability page or an explanation on a type
   a non-materialised derivation reaches is refused with
   `authz.derivation.sourcesmissing`, for a permission the conferred role does not allow
   as for one it does; on a type no non-materialised derivation reaches, the stored and
   materialised grants answer.
5. A check with the host's sources on a type a derivation reaches issues one statement
   in the host's context and reads no grant through the library's own connection.

---

**AUTHZ-DERIVE-002** — Derived grants SHALL obey every rule that stored grants obey:
deny defeats them, inheritance applies to them, organization scoping applies, and
they compose into the same filter.

*Source: D-043*

**Acceptance criteria**
1. A deny row defeats a derived grant.
2. A derived grant on a container confers access to its contents.
3. The truth table covers derived cases through both check and filter
   (AUTHZ-PRIN-001).

---

**AUTHZ-DERIVE-003** — Derivations SHALL NOT be used to express what a stored grant
expresses naturally.

*Source: D-043*

The known failure mode in relationship-based systems is expressing every permission
as a relation, producing a model full of contrived relationships that exist only to
satisfy the engine. A derivation is warranted when the permission genuinely follows
from a fact in the domain, not as a way of avoiding a grant row.

**Acceptance criteria**
1. Each declared derivation names the domain relationship it follows from.
2. A derivation whose relationship exists solely to grant permission fails review.

---

**AUTHZ-DERIVE-004** — Every derivation SHALL declare the columns its evaluation
depends on, and those SHALL be indexed.

*Source: D-043*

A derived grant is only as fast as the join it performs into the host's tables.

**Acceptance criteria**
1. A derivation naming an unindexed column fails startup validation.
2. Query plans for derived rules use indexes rather than sequential scans at
   production-scale volume: the AUTHZ-TEST-002 fixture.

---

**AUTHZ-DERIVE-005** — Where a derivation is too costly to evaluate per request, it
MAY be **materialised** — precomputed and stored as ordinary grants, refreshed when
the underlying data changes.

Materialisation SHALL be an explicit, per-derivation choice, and materialised grants
SHALL be marked as such so they are never mistaken for stored grants someone wrote.

**Values (D-161, D-162).** Refresh is the host's call: the library exposes
`IDerivationMaterialiser.RefreshAsync(context, derivation, resource, sources)`, which
takes the access context whose subject every grant it writes records as its granter
(AUTHZ-GRANT-003), the relationship's name, the `ResourceId` of the resource the
relationship's rows are about, and the same host-supplied sources object the filter
takes (AUTHZ-DERIVE-001), and which the host calls from the operation that changes the
relationship. The grants a refresh writes commit with the library's unit of work, and
none exists if that unit of work does not commit. Where the host's write and the refresh
do not both commit, the difference is drift, which the next refresh or the drift check
detects, reports and corrects: the drift check re-evaluates every materialised
derivation every `derivation.materialised.driftcheck` (default `P1D`), and a difference
raises the `degradation` condition with the derivation's name in `details` and is
corrected in the same run. A refresh SHALL write the derivation's grant on each resource of the type the
derivation is declared on whose ancestry includes the resource the relationship's row
names, that resource itself included where it is of that type.

**Values (D-166).** The drift check reads each relationship's rows through the source the
host declares for it (`07` LIB-HOST-001), the source the `GET /admin/access` view reads
(AUTHZ-DERIVE-007). The host SHALL declare that source for every declared derivation,
materialised or not; a derivation whose relationship has none fails startup with
`model.startup.declarationmissing` naming the relationship. The drift check runs as the
system principal `derivation-driftcheck` (`10` section 5.29, IDN-PRIN-001); a grant it
writes records the nil subject as its granter and the reason `AUTHZ-DERIVE-005`, and
its audit record names the principal (AUTHZ-GRANT-003). The refresh meets no gate of
its own: the host calls it from its own gated operation (CONV-DESIGN-002 AC3).

*Source: D-043, D-162, D-166*

Materialisation reintroduces, deliberately and in one controlled place, the
synchronisation problem the design otherwise avoids. It is the last rung of the
optimisation ladder, not the first. It is also what Zanzibar's Leopard index does,
and for the same reason.

**Acceptance criteria**
1. Materialisation is opt-in per derivation.
2. A materialised grant is distinguishable from a written one in storage and in
   explanations.
3. The grants a refresh writes exist only where the library's unit of work commits;
   where the host's write and the refresh do not both commit, the next refresh or the
   drift check detects the difference, reports it and corrects it.
4. The drift check, run over the host's declared relationship sources, corrects a
   materialised grant that no longer matches the host's rows and raises `degradation`
   naming the derivation; a grant it writes names the nil granter with the reason
   `AUTHZ-DERIVE-005`.
5. A model declaring a derivation, materialised or not, whose relationship has no
   declared source fails startup with `model.startup.declarationmissing`.

---

**AUTHZ-DERIVE-006** — The optimisation ladder SHALL be followed in order, and a
step SHALL NOT be skipped without a recorded measurement showing the previous one
insufficient.

| Rung | Technique | Cost |
|---|---|---|
| 1 | Index the columns the derivation depends on | None |
| 2 | Denormalise the derivation's key onto the resource | Small write cost |
| 3 | Cache the resolved subject set per request | None beyond memory |
| 4 | Materialise the derivation | A synchronisation problem |
| 5 | Migrate authorization out of the library | A project |

*Source: D-043*

Recorded as a sequence so it is not reinvented under pressure, with the heaviest
option reached last rather than first.

**Acceptance criteria**
1. A materialisation decision references the measurement that justified it.

---

**AUTHZ-DERIVE-007** — Reverse lookup ("who can access this resource?") over derived
grants SHALL be answered by evaluating declared derivations, and its cost SHALL be
documented as bounded by the number of derivations and the size of the candidate
set.

**Values (D-161).** Reverse lookup is the `GET /admin/access` view of `09` section 8 and is
built in phase 8. It answers stored grants by query, materialised derived grants by
query (they are rows), and unmaterialised derivations by evaluating each declared
derivation over the host-supplied relation for the resource and its ancestors, inside
`authz.reverselookup.budget`; past the budget the response carries `partial: true` and
`unevaluated`. Nothing of it is built in phase 2.

**Values (D-166).** The view reads each relationship's rows through the source the host
declares for it (`07` LIB-HOST-001), the source the drift check of AUTHZ-DERIVE-005
reads. `unevaluated` names the relationships whose derivations were not evaluated; a
grant that confers nothing is not reported. The view is asked under `grant:read` in the
organization the record belongs to (AUTHZ-SCOPE-001); a record the deployment holds no
registration for belongs to no organization and is refused as a caller without
`grant:read` is refused.

*Source: D-043, AUTHZ-GATE-004, D-166*

Stored grants answer this with a query. Derived grants cannot — there is no row to
look up. This is the genuine ceiling of the design, and the reason the migration
trigger exists. Materialisation is the only mitigation, which is precisely why
graph-based systems materialise.

**Acceptance criteria**
1. The administrative "who can access this?" view reports stored and derived grants
   distinctly.
2. Where derivations make reverse lookup unbounded, the view states the limitation
   rather than returning a partial answer silently: when evaluation exceeds
   `authz.reverselookup.budget` the response carries `partial: true` and `unevaluated`,
   the names of the relationships whose derivations were not evaluated (D-153).
3. The view of a record the deployment holds no registration for is refused as the view
   of a registered record is refused to a caller without `grant:read` where it is: 403
   `authz.denied`, with a correlation identifier recorded in the audit trail.

---

### 2.5 Organization scoping

**AUTHZ-SCOPE-001** — Every permission evaluation SHALL be scoped to the
organization owning the resource, resolved from the resource and never from the
session.

An operation whose object is the deployment or an account, rather than a record an
organization owns, SHALL be evaluated in the **administrative organization**
(IDN-ORG-001): runtime configuration and the named restriction set, roles, organization
lifecycle, policy and domains, account lifecycle, session revocation, the takedown,
recovery approval, the privacy request queue and erasures, the audit trail and
explanation resolution, compliance text and records, and the break-glass credential.
Before bootstrap has marked an administrative organization every such operation is
refused. Grants, groups, memberships and invitations are evaluated in the organization
they belong to. A grant in the administrative organization confers only on a principal
holding a current membership of it (IDN-MEM-001). Where the organization is read from
the row a request names, an identifier naming no row belongs to no organization, so no
grant reaches it, and it is refused as a missing permission is (`09` section 8). A path
under `/admin` whose `{id}` names no organization the deployment holds is answered 404
`identity.organization.notfound`, nothing being concealed at that level (`09` section
8a).

*Source: D-003, IDN-MEM-003, D-166*

**Acceptance criteria**
1. No session field names an organization.
2. A principal with membership in two organizations sees only what each grants,
   without switching.
3. A permission held in an organization other than the administrative organization
   authorizes no operation on the deployment or on an account.

---

## 3. The model builder

### 3.1 Form

**AUTHZ-MODEL-001** — The host SHALL declare its resource types and containment
through a fluent, generic builder at startup. The library SHALL NEVER contain a type
name belonging to any business.

*Source: D-015*

**Acceptance criteria**
1. A search of library source finds no host domain type name.
2. Two host projects with entirely different domains use the same library binary.
3. Property references in the declaration are compiler-checked, not strings.

---

**AUTHZ-MODEL-002** — Resource type identifiers SHALL be strings chosen by the host,
never an enumeration shipped by the library.

One name is reserved: `organization` names the whole organization, under which a grant
with no resource is written and asked (AUTHZ-GRANT-001 AC2), and a host resource type
so named fails startup (AUTHZ-MODEL-004).

*Source: D-015, D-166*

An enumeration would make every new resource type a library change.

**Acceptance criteria**
1. Adding a resource type requires no library modification.

---

**AUTHZ-MODEL-003** — Each resource type declaration SHALL carry: containment,
concealment behaviour, sensitivity, its processing purposes, each with its lawful basis
and the data and subject categories it requires (PRIV-PRIN-001), and, for each encrypted
field, **the column identifying that field's subject** and the data category the field
holds.

| Declared | Drives | Source |
|---|---|---|
| Containment | Ancestry, inheritance | D-015 |
| Concealment | 403 vs 404 | D-016 |
| Sensitivity | Written consent, encryption, retention | D-030 |
| Purposes, lawful bases, data and subject categories | Records of processing | D-032, D-036, PRIV-PRIN-001 |
| Derivations | Derived grants | D-043 |
| **Subject column and data category, per encrypted field** | **Which key encrypts it; which purpose holds it (PRIV-PRIN-001 AC2)** | **D-099** |

**Values (D-162, D-166).** The subject column of a type's encrypted fields also names
the record's **data subject**, whose consent the gate reads for a consent-based purpose
(PRIV-SENS-002, AUTHZ-GATE-005). The host supplies that subject when it registers the
record, read from that column (AUTHZ-INHERIT-002), because the library reads no host
table (LIB-HOST-002). A consent-based purpose on a type whose encrypted fields name no
one subject column SHALL fail startup validation with `model.startup.declarationmissing`,
`details.key` naming `<type>.<purpose>`.

*Source: D-015, D-016, D-030, D-036, D-043, D-162, D-166*

One declaration describes the host's domain; permission filtering, error semantics,
and compliance records all derive from it.

**Acceptance criteria**
1. A type declared without a purpose fails startup validation.
2. Changing sensitivity changes consent behaviour without other edits.

---

### 3.2 Validation

**AUTHZ-MODEL-004** — The model SHALL be validated at startup, failing loudly. Checks
that need only the declaration run inside `AddJanus`; checks that read the database
(an undeclared permission granted by a stored role, an unindexed derivation column) run
in a hosted service `Janus.Hosting` registers before the web server, so the process
exits non-zero before a request is served; `Janus.Cli` runs the same validation before
any command (D-160). The
following SHALL fail:

- A containment cycle
- A type referencing an undeclared type
- A type with no path to an organization
- A role granting an undeclared permission
- A purpose declaring legitimate interest without a linked assessment
- A queryable entity with no registered policy
- **A derivation naming an unindexed column** (AUTHZ-DERIVE-004)
- **A derivation referencing an undeclared resource type or relationship**
- An action bound to a purpose no resource type declares (AUTHZ-GATE-005)
- A resource type named `organization`, which the library reserves for the whole
  organization (AUTHZ-MODEL-002)
- A consent-based purpose named for the hosting or its cross-border transfer
  (INT-HOST-002)

*Source: D-015, D-032, D-043, D-162, D-166*

Failing at boot rather than at first query. The same principle as the relying party
identifier validation in `02-authentication`.

**Acceptance criteria**
1. Each listed condition produces a distinct named error identifying the offending
   declaration: `model.containment.cycle`, `model.type.noorganizationpath`,
   `model.type.undeclaredreference` (an action bound to an undeclared purpose included,
   `details.permission` naming the action), `model.role.undeclaredpermission`,
   `model.derivation.undeclaredreference`, `model.derivation.unindexed`,
   `model.type.reserved` (`details.key` naming the type), `model.purpose.hostingconsent`
   (`details.key` naming `<type>.<purpose>`) (`10` section 1.5, D-153).
2. Validation runs before any request is served.
3. A new entity added without a policy fails the build via test, not only at
   startup.

---

**AUTHZ-MODEL-005** — The built model SHALL be serialized to a file at startup, for
committing and reviewing.

The output SHALL also list every right the maintenance credential of OPS-MIG-003a holds,
each as the object it is held on, its kind first, and the right (D-162).

*Source: D-015, D-162, D-166*

Recovers the one real advantage of an external model file — diffability in review —
without giving up compiler-checked property references.

**Acceptance criteria**
1. The serialized output is deterministic across runs with identical configuration.
2. A permission model change produces a reviewable diff.
3. A migration that grants, revokes or widens a right of the maintenance credential
   without the listing changing fails a test.

---

### 3.3 Scope

**AUTHZ-MODEL-006** — The model declaration SHALL NOT be runtime-configurable. Roles and grants remain runtime.

*Source: D-015, D-010*

Adding a resource type means adding a table and code to use it, so a deploy is
occurring regardless. This is the only non-runtime item not on the security list
in `06-operations`.

**Acceptance criteria**
1. No runtime API modifies the model.

---

## 4. The access gate

**AUTHZ-GATE-001** — Application code SHALL NOT reach data except through the gate.
The gate SHALL return an already-filtered query.

*Source: D-015, D-166*

A rule that cannot be forgotten beats a rule that was documented. Endpoint
attributes are insufficient — they cover the endpoints someone remembered to
decorate and miss background jobs, exports and webhooks.

**Acceptance criteria**
1. Raw entity set access is unreachable from the host's service layer.
2. A background job cannot query without a principal.
3. A test enumerates every queryable entity and fails if one lacks a policy; a declared
   resource type, the rows of a declared relationship (the gate's own input,
   AUTHZ-DERIVE-001) and the library's contract tables and views count as registered,
   and an owned type counts with its owner.

---

**AUTHZ-GATE-002** — The gate SHALL render each rule **two ways from one
definition**: an expression composable into LINQ, and a parameterised SQL fragment
for hand-written queries.

**Shape (D-159).** The rule is evaluated in two steps that both renderings share.
First the library resolves the principal's **subject set** (the account, its groups by
the group closure, its organization roles): a small, bounded set, read once per request
from the library's own store. Second, the rendering is a predicate over the host's row
that asks the library's two contract tables whether any grant for that permission
exists for one of those subjects on the resource **or on any of its ancestors**
(AUTHZ-INHERIT-002); a deny grant defeats it. The LINQ rendering is
`Expression<Func<TResource, bool>>` built from the host's resource-identifier selector
and two `IQueryable`s the host supplies **from its own `DbContext`**: `AncestryEntry`
and `EffectiveGrant`, public plain records in `Janus.Core`, mapped into the host's
context by `MapAuthorizationTables(ModelBuilder)` in `Janus.Hosting`; the subquery is a
same-context correlated `EXISTS` that EF Core translates. The SQL rendering is the same
`EXISTS` over `identity.ancestry` and `identity.effective_grants`, with the row alias and
column supplied by the caller and the subject set, permission and resource type as
parameters. Neither rendering ever enumerates permitted resources (AUTHZ-PRIN-002).

**Values (D-166).** For a permission bound to a consent-based purpose (AUTHZ-GATE-005),
both renderings add one condition from the same rule definition: an `EXISTS` over the
library's view `identity.consented_resources` (each registered record whose data
subject, AUTHZ-MODEL-003, holds a consent for a purpose that is neither withdrawn nor
superseded, with the consent's kind) for the row's type and identifier and that purpose,
of kind `written` where the purpose requires written consent. The LINQ rendering reads
the view through a third `IQueryable` the host supplies from its own `DbContext`, mapped
by `MapAuthorizationTables(ModelBuilder)` beside the other two; the SQL rendering names
the view. A list therefore admits no record whose data subject has not consented, or has
withdrawn (PRIV-SENS-002, PRIV-SENS-002a).

*Source: D-017, D-166*

**Acceptance criteria**
1. Both renderings derive from one rule definition — neither is written separately.
2. Every truth-table case is asserted equal across both renderings.
3. The SQL fragment is parameterised; no value is interpolated into SQL text.
4. For a permission bound to a consent-based purpose, both renderings admit only the
   records whose data subject holds a live consent of the required kind for that
   purpose, and a truth-table case bound to such a purpose is asserted equal across
   them.

---

**AUTHZ-GATE-003** — The SQL fragment renderer SHALL be PostgreSQL-specific, and
this SHALL be stated in the public contract.

*Source: D-041, R-19*

**Acceptance criteria**
1. The dialect constraint appears in the library contract document.
2. No claim of dialect portability appears in API documentation.

---

**AUTHZ-GATE-004** — The gate SHALL provide an explanation operation returning **why**
access was granted or denied, naming the matched or missing grant.

**Values (D-153).** The explanation is `{ outcome: allowed · denied, permission, principal:
{ acting, effective, name, reason }, grant }`, `name` and `reason` present only for a
system principal (D-166), where `grant` is `{ id, kind, subjectType, subjectId, role,
deny, inheritedFrom: { resourceType, resourceId } or null }` for the grant that decided,
or null when none matched. A derived grant has no row: it is named `{ id: null, kind:
derived, subjectType: user, subjectId, role, deny: false, inheritedFrom }`, where
`subjectId` is the asking account, `role` the role the derivation confers, and
`inheritedFrom` the resource the relationship's row names, or null where that is the
resource explained (D-162). A matching deny is named before any allow; where several
grants of one kind would decide, the one named is the nearest, on the resource itself
first and then on its containers nearest first, a derived grant placed by the resource
its relationship's row names (D-166).

*Source: P-003, D-162, D-166*

Costs little and answers "why can't I see this record?" without a debugger. Also the
backing query for a "who can access this?" administrative view.

**Explanations for concealed resource types SHALL NOT be self-service.** For a type
whose denial returns not-found, an explanation saying "no grant matched" discloses
that the record exists, defeating the concealment. Those resolve only for a support
role, a holder of `audit:read` in the administrative organization (AUTHZ-SCOPE-001),
from the correlation identifier.

*Source: D-079a, D-166*

**Acceptance criteria**
1. A denial explanation names the permission and states no grant matched.
2. An approval explanation names the grant and the container it was inherited from.
3. Self-service explanation is available for non-concealed types only.
4. A correlation identifier from a concealed denial resolves only for a support role.
5. An explanation asked with the host's sources on a record a derivation admits names a
   grant with a null `id`, kind `derived`, the conferred role and the container the
   relationship names; where a deny matches, the deny is named instead.

---

**AUTHZ-GATE-005** — The API SHALL return **capabilities** alongside each record,
meaning **"permitted by grants, subject to session gates"** — computed in the same
query.

**A capability is not a promise the action will succeed.** An action can still be
refused for step-up, a downgraded session, the subject's processing restriction,
missing or superseded consent, or the caller's account state — none of which the
per-row grant query evaluates.

**Each capability therefore carries what it still requires:**

```json
{ "id": "...", "can": ["read", "edit"], "requires": { "edit": ["stepup"] } }
```

The frontend renders the control and prompts for what is needed, rather than hiding a
control the person is entitled to use or showing one that will fail with no
explanation.

**Values (D-153).** `requires` carries members of the closed set in `10` section 5.20:
`stepup` · `reauthenticate` · `restricted` · `consent` · `accountstate`.

**Values (D-160).** The `stepup` residual comes from the **gate bound to the action**, not
from the permission string: the model builder binds a host-declared action to a step-up
gate name (`10` section 5a or a host-declared gate), and the library-owned actions carry
their bindings in section 5a. `requires` lists `stepup` for an action whose bound gate
the session does not currently satisfy.

**Values (D-162, D-166).** The `consent` residual comes from the **purpose bound to the
action**, as `stepup` comes from its gate: the model builder binds a host-declared action
to the purpose it is done for, beside its step-up gate. Where that purpose rests on
consent, the gate SHALL refuse the action until the record's data subject
(AUTHZ-MODEL-003) holds the consent PRIV-SENS-002 AC1 names, whoever the caller is,
a system or staff principal included (`privacy.consent.required`,
`privacy.consent.superseded`, `privacy.consent.writtenrequired`), and `requires` lists
`consent` for it. The list filter and the SQL fragment apply the same condition
(AUTHZ-GATE-002). An action bound to no purpose, or to one on another basis, asks for no
consent.

**Values (D-162, D-166).** Where the host supplies the sources of AUTHZ-DERIVE-001, the
capabilities of a page SHALL be computed in one query in the host's context: for each
record of the page and each permission asked, the stored allow and deny terms of the
filter and one `EXISTS` per non-materialised derivation reaching the type. What the role
each derivation confers allows is read from the model and mapped in memory; no record
and no permission costs a further query, and no grant is read through the library's own
connection.

*Source: D-015, D-078, D-162, D-166*

The frontend must never infer permissions from role names; that is how a button
appears while the endpoint refuses. Computing them per row in separate calls
produces N+1 queries.

**Acceptance criteria**
1. A list of 50 records returns capabilities without additional queries.
2. A capability present with an empty `requires` always succeeds.
3. A capability with `requires` prompts rather than failing silently.
4. No frontend code contains a role name.
5. A page of 50 records on a type a derivation reaches, asked for three permissions,
   issues one statement in the host's context and reads no grant through the library's
   own connection; a deny on a record defeats a derived grant on the page as it does in
   the check.

---

**AUTHZ-GATE-006** — A processing restriction (`01-identity`, state `restricted`)
SHALL be evaluated wherever the gate is evaluated.

**Values (D-160).** Every permission action is classified **reading** or **modifying**. An
action named `read`, `list` or `export` is reading; every other action is modifying
unless the model builder declares it reading. Under restriction the gate allows the
account's own reading actions and refuses every modifying one with `authz.restricted`.
The library-owned actions of `10` section 2.1 follow the same rule.

**Values (D-166).** Restriction refuses no sign-in: a `restricted` account signs in and
reads (IDN-ACCT-007, "Yes, read only"). The changes the library makes to the account's
own identifiers, credentials, profile and preferences, and an invitation
acknowledgement, are modifying and are refused through the gate with `authz.restricted`
as every other modifying action is; what IDN-ACCT-007 keeps available to a restricted
account is admitted.

*Source: D-037, D-166*

**Acceptance criteria**
1. A restricted account's records are readable by that account and not modifiable.
2. Restriction is enforced through the gate, not by scattered checks.

---

## 5. Concealment

**AUTHZ-CONCEAL-001** — Denied access to a specific record SHALL return **not found**
by default. Returning **forbidden** SHALL be opt-in per resource type.

*Source: D-016, D-166*

Whatever a developer gets without thinking is what most types will have, so the safe
answer must be the lazy one.

A host SHALL ask the gate before it looks a record of a concealing type up, and SHALL
NOT answer an absence of its own: the gate refuses a record the library holds no row for
exactly as one the caller may not see, and the pipeline answers both (BFF-ERR-003). A
refused check is never a probe: what a caller may do on a record is read from the
capability page (API-CAP-001).

**Acceptance criteria**
1. A type declared without concealment behaviour returns not found on denial.
2. Opting a type into forbidden requires an explicit declaration.

---

**AUTHZ-CONCEAL-002** — A concealment response SHALL be indistinguishable from a
genuine not-found in body, headers, **and timing**.

*Source: D-016, D-166*

A concealment response arriving later because it ran a permission check first leaks
the answer regardless of its content.

**Acceptance criteria**
1. Response bodies are byte-identical.
2. Timing distributions for concealed and genuine not-found overlap within noise:
   verified by construction (one code path, identical bytes), asserted by criterion 1,
   named in the report as verified by construction (CONV-TEST-007, D-153).
3. A refusal on a record the library holds no row for and a refusal on a record it
   holds and the caller may not see run the same database statements, differing in
   parameter values only.

---

**AUTHZ-CONCEAL-003** — Concealment behaviour SHALL be uniform within a resource
type. Per-instance variation SHALL NOT occur.

*Source: D-016*

Per-type is fine; per-instance is an oracle.

**Acceptance criteria**
1. Two records of one type produce the same denial shape.

---

**AUTHZ-CONCEAL-004** — A concealment response SHALL carry a correlation identifier
that appears in the audit trail.

Every gate refusal SHALL carry the identifier, whoever asked. A refusal of background
work SHALL be recorded as its other actions are: the nil subject under both identities,
and the system principal's name and stated reason (IDN-PRIN-001 AC4, D-162). The record
of a refusal SHALL be written outside any transaction the caller holds open and
committed at once, so a rollback of the caller's work leaves it standing; an action's
own records stay in its transaction (D-166).

*Source: D-016, D-162, D-166*

Support can diagnose a legitimate permission problem without the response revealing
anything.

**Acceptance criteria**
1. The identifier resolves to an audit entry naming the permission, the principal, and
   the grant that decided (the AUTHZ-GATE-004 values) or none.
2. The identifier reveals nothing about record existence.
3. The identifier of a refusal of a system principal resolves to the principal's name
   and reason.
4. A refusal inside a transaction the caller rolls back is still recorded, resolves by
   its identifier, and counts toward `alerting.denials.threshold`.

---

**AUTHZ-CONCEAL-005** — Authorization failures not tied to a specific record SHALL
return **forbidden**.

*Source: D-016*

Nothing is being concealed when an administrative endpoint is called without the
permission, so forbidden is both accurate and more useful.

**Acceptance criteria**
1. Calling an administrative endpoint without the permission returns forbidden.

---

## 6. Caching and revocation

**AUTHZ-CACHE-001** — What is cached SHALL be the principal's **grant rows and
transitive group set** — never a resolved (principal, action, resource) outcome —
under a key incorporating a per-account version counter bumped in the **same
transaction** as any grant or membership change.

**Role definitions, ancestry, grant expiry and account state SHALL be evaluated
live.** So SHALL an organization's deletion request, which stops the organization's
grants conferring (IDN-ORG-003): it is read through the effective grants view on every
evaluation and bumps no counter (D-166).

*Source: D-007, D-079, D-166*

Caching resolved outcomes leaves stale permissions after a role's action set is
edited, a resource is moved, a grant expires, an account is restricted, or an
organization's deletion is requested — none of which bump an account's counter. Each
is a silent grant of access that no longer exists. Caching the inputs instead means
the counter only needs to track what it already tracks.

*Source: D-007*

A bump orphans the previous entry, so revocation takes effect on the next request.
No expiry to tune, no window in which a removed grant still applies.

**Derived grants SHALL NOT be cached per subject-action-resource triple.** Derived
permissions compose recursively over the host's data, so a naive triple cache cannot
be correctly invalidated — the change that should invalidate it may occur in a table
the cache never observed. Where a derivation is too costly to evaluate per request,
the answer is materialisation (AUTHZ-DERIVE-005), not caching.

**Acceptance criteria**
1. Revoking a grant takes effect on the next request with no wait.
2. A failed grant write leaves the counter unchanged.
3. Changing a group's grants invalidates every transitive member.
4. No cache entry is keyed on a subject-action-resource triple, derived or stored.
5. Editing a role's permissions takes effect immediately, with no counter bump.
6. Moving a resource takes effect immediately.
7. An expired grant confers nothing without any sweep or invalidation.
8. Restricting an account takes effect immediately.
9. An organization's deletion request stops its grants conferring on the next request,
   and its cancellation restores them, with no counter bumped.

---

**AUTHZ-CACHE-002** — Permission resolution SHALL NOT be carried in any token or
cookie.

*Source: D-007*

**Acceptance criteria**
1. No permission or role name appears in a session cookie or token.

---

## 7. Impersonation seam

**AUTHZ-IMP-001** — The access context SHALL carry **acting identity** and
**effective identity** as separate values, identical in every current path. Audit
SHALL record both.

An audit record SHALL also name, where there is one, the data subject it concerns, apart
from both identities: the account an action is taken on is the record's `subject`, never
its effective identity. Every new record carries an effective identity equal to its
acting identity, the nil subject for both beside a system principal. The trail read by
subject (PRIV-BREACH-002) reads the records naming the subject as acting identity or as
`subject`.

*Source: D-014, D-166*

Impersonation is out of scope. Two identity fields where one would do is defensible
on its own terms — it makes "who did this" unambiguous — and retrofitting a second
identity into every audit record and permission check later would not be.

**Acceptance criteria**
1. Both fields are present on every access context.
2. Both are written to every audit record.
3. No feature reads them as differing.
4. An action taken by one account on another, a break-glass session's included, records
   the actor as acting and effective identity and the other account as the record's
   `subject`.

---

## 8. Testing

**AUTHZ-TEST-001** — A truth table SHALL enumerate expected outcomes across
relationship, permission, and condition for each resource type. Changing a policy
SHALL require changing the table first.

*Source: D-015*

The diff in that table is the change under review. It is the difference between an
authorization system that is trusted and one that is feared.

**Acceptance criteria**
1. The table covers direct grants, container inheritance, multi-level inheritance,
   group membership, nested group membership, deny overriding allow, expiry,
   cross-organization isolation, **derived grants**, **deny defeating a derived
   grant**, and **a derived grant on a container reaching its contents**.
2. Every case runs through both the single check and the list filter
   (AUTHZ-PRIN-001).
3. Where a derivation is materialised, the same cases pass identically before and
   after materialisation.

---

**AUTHZ-TEST-002** — Permission query performance SHALL be measured against
realistic data volumes before release.

*Source: D-015*

The nested existence check is where the cost lives; the partial index on live grants
is what keeps it cheap.

**Acceptance criteria**
1. A query plan is captured for the primary list query at production-scale volume: the
   fixture seeds 1,000,000 resources, 1,000,000 grants of which 10% are revoked,
   100,000 principals and 10,000 groups (D-153).
2. The permission predicate uses an index rather than a sequential scan.

---

## 9. Migration seam

**AUTHZ-SEAM-001** — All permission evaluation SHALL pass through a single
interface, so the implementation behind it can be replaced without changing call
sites.

*Source: D-015, D-125*

**Acceptance criteria**
1. Replacing the implementation requires changes in one place.
2. No call site constructs a permission query directly.

**Migration trigger**, recorded rather than left to judgment: when permission queries
are a top-three slow query and the optimisation ladder (AUTHZ-DERIVE-006) is
exhausted — or when reverse lookup over derived grants (AUTHZ-DERIVE-007) for the
administrative "who can access this?" view exceeds **`authz.reverselookup.budget`**
(default **2 seconds**, `10` §4.5a) at production volume after the ladder is
exhausted. The budget is a chosen figure, so the trigger is measurable rather than a
judgment.

The second is the more likely trigger. Forward checks optimise well; reverse lookup
over computed permissions is the problem graph-based systems exist to solve.

---

## 10. Open items

None. Cross-references: principals and organizations in `01-identity`; step-up and
assurance in `02-authentication`; sensitivity, purposes and lawful bases in
`04-privacy`; data access strategy in `06-operations`; the public surface and its
stability guarantees in `07-library-contract`.
