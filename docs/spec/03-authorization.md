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

*Source: P-003*

**Acceptance criteria**
1. An unregistered resource type raises rather than permitting.
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

**Values (D-153).** `reason` is required and non-empty on every grant created or revoked;
absent, the request is refused with `authz.grant.reasonrequired`. Free text is 1 to
1024 characters after trimming, the one rule for every free-text field (API-CONV-002).

*Source: D-015*

Expiry supports contractors, trials and temporary escalation. Free to add now; a
migration across every query later.

**Acceptance criteria**
1. An expired grant confers no access without any sweep having run.
2. The audit fields are populated on creation and on revocation.
3. "Who granted this and when" is answerable by query.

---

**AUTHZ-GRANT-004** — Grants and roles SHALL be runtime-changeable without a deploy.

*Source: D-010, D-015*

**Acceptance criteria**
1. Creating a role and assigning permissions to it requires no restart.
2. Granting and revoking take effect on the next request.

---

### 2.2 Groups

**AUTHZ-GROUP-001** — Groups SHALL nest, and membership SHALL be followed
transitively.

*Source: D-015*

**Acceptance criteria**
1. A user in a team inside a department inherits grants held by the department.
2. Nesting depth is not fixed by the schema.

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

*Source: D-015, D-017*

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
the host's query, never as a library-side read.

*Source: D-043*

A stored grant exists because someone wrote it. A derived grant exists because a
fact in the business data is true — "the assigned representative on an account holds
the reader role on that account's orders." It needs no maintenance and cannot drift,
because there is nothing to keep in sync.

This is the same construct Zanzibar calls a computed userset. It is the mechanism
that makes relationship-based access expressible without abandoning the model.

**Acceptance criteria**
1. A derivation is declared alongside containment and sensitivity, not in code.
2. Changing the underlying business data changes access on the next request, with
   no grant written or revoked.
3. Removing the relationship removes the access.

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

*Source: D-043*

Materialisation reintroduces, deliberately and in one controlled place, the
synchronisation problem the design otherwise avoids. It is the last rung of the
optimisation ladder, not the first. It is also what Zanzibar's Leopard index does,
and for the same reason.

**Acceptance criteria**
1. Materialisation is opt-in per derivation.
2. A materialised grant is distinguishable from a written one in storage and in
   explanations.
3. Refresh occurs in the same transaction as the change that triggers it, or drift
   is detected and reported.

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

*Source: D-043, AUTHZ-GATE-004*

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
   the names of the derivations not evaluated (D-153).

---

### 2.5 Organization scoping

**AUTHZ-SCOPE-001** — Every permission evaluation SHALL be scoped to the
organization owning the resource, resolved from the resource and never from the
session.

*Source: D-003, IDN-MEM-003*

**Acceptance criteria**
1. No session field names an organization.
2. A principal with membership in two organizations sees only what each grants,
   without switching.

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

*Source: D-015*

An enumeration would make every new resource type a library change.

**Acceptance criteria**
1. Adding a resource type requires no library modification.

---

**AUTHZ-MODEL-003** — Each resource type declaration SHALL carry: containment,
concealment behaviour, sensitivity, its processing purposes with lawful bases, and —
for each encrypted field — **the column identifying that field's subject**.

| Declared | Drives | Source |
|---|---|---|
| Containment | Ancestry, inheritance | D-015 |
| Concealment | 403 vs 404 | D-016 |
| Sensitivity | Written consent, encryption, retention | D-030 |
| Purposes and lawful bases | Records of processing | D-032, D-036 |
| Derivations | Derived grants | D-043 |
| **Subject column, per encrypted field** | **Which key encrypts it** | **D-099** |

*Source: D-015, D-016, D-030, D-036, D-043*

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

*Source: D-015, D-032, D-043*

Failing at boot rather than at first query. The same principle as the relying party
identifier validation in `02-authentication`.

**Acceptance criteria**
1. Each listed condition produces a distinct named error identifying the offending
   declaration: `model.containment.cycle`, `model.type.noorganizationpath`,
   `model.type.undeclaredreference`, `model.role.undeclaredpermission`,
   `model.derivation.undeclaredreference`, `model.derivation.unindexed` (`10` section
   1.5, D-153).
2. Validation runs before any request is served.
3. A new entity added without a policy fails the build via test, not only at
   startup.

---

**AUTHZ-MODEL-005** — The built model SHALL be serialized to a file at startup, for
committing and reviewing.

*Source: D-015*

Recovers the one real advantage of an external model file — diffability in review —
without giving up compiler-checked property references.

**Acceptance criteria**
1. The serialized output is deterministic across runs with identical configuration.
2. A permission model change produces a reviewable diff.

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

*Source: D-015*

A rule that cannot be forgotten beats a rule that was documented. Endpoint
attributes are insufficient — they cover the endpoints someone remembered to
decorate and miss background jobs, exports and webhooks.

**Acceptance criteria**
1. Raw entity set access is unreachable from the host's service layer.
2. A background job cannot query without a principal.
3. A test enumerates every queryable entity and fails if one lacks a policy.

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
context by `MapJanusAuthorization(ModelBuilder)` in `Janus.Hosting`; the subquery is a
same-context correlated `EXISTS` that EF Core translates. The SQL rendering is the same
`EXISTS` over `janus.ancestry` and `janus.effective_grants`, with the row alias and
column supplied by the caller and the subject set, permission and resource type as
parameters. Neither rendering ever enumerates permitted resources (AUTHZ-PRIN-002).

*Source: D-017*

**Acceptance criteria**
1. Both renderings derive from one rule definition — neither is written separately.
2. Every truth-table case is asserted equal across both renderings.
3. The SQL fragment is parameterised; no value is interpolated into SQL text.

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
{ acting, effective }, grant }` where `grant` is `{ id, kind, subjectType, subjectId,
role, deny, inheritedFrom: { resourceType, resourceId } or null }` for the grant that
decided, or null when none matched.

*Source: P-003*

Costs little and answers "why can't I see this record?" without a debugger. Also the
backing query for a "who can access this?" administrative view.

**Explanations for concealed resource types SHALL NOT be self-service.** For a type
whose denial returns not-found, an explanation saying "no grant matched" discloses
that the record exists — defeating the concealment. Those resolve only for a support
role, from the correlation identifier.

*Source: D-079a*

**Acceptance criteria**
1. A denial explanation names the permission and states no grant matched.
2. An approval explanation names the grant and the container it was inherited from.
3. Self-service explanation is available for non-concealed types only.
4. A correlation identifier from a concealed denial resolves only for a support role.

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

*Source: D-015, D-078*

The frontend must never infer permissions from role names; that is how a button
appears while the endpoint refuses. Computing them per row in separate calls
produces N+1 queries.

**Acceptance criteria**
1. A list of 50 records returns capabilities without additional queries.
2. A capability present with an empty `requires` always succeeds.
3. A capability with `requires` prompts rather than failing silently.
4. No frontend code contains a role name.

---

**AUTHZ-GATE-006** — A processing restriction (`01-identity`, state `restricted`)
SHALL be evaluated wherever the gate is evaluated.

**Values (D-160).** Every permission action is classified **reading** or **modifying**. An
action named `read`, `list` or `export` is reading; every other action is modifying
unless the model builder declares it reading. Under restriction the gate allows the
account's own reading actions and refuses every modifying one with `authz.restricted`.
The library-owned actions of `10` section 2.1 follow the same rule.

*Source: D-037*

**Acceptance criteria**
1. A restricted account's records are readable by that account and not modifiable.
2. Restriction is enforced through the gate, not by scattered checks.

---

## 5. Concealment

**AUTHZ-CONCEAL-001** — Denied access to a specific record SHALL return **not found**
by default. Returning **forbidden** SHALL be opt-in per resource type.

*Source: D-016*

Whatever a developer gets without thinking is what most types will have, so the safe
answer must be the lazy one.

**Acceptance criteria**
1. A type declared without concealment behaviour returns not found on denial.
2. Opting a type into forbidden requires an explicit declaration.

---

**AUTHZ-CONCEAL-002** — A concealment response SHALL be indistinguishable from a
genuine not-found in body, headers, **and timing**.

*Source: D-016*

A concealment response arriving later because it ran a permission check first leaks
the answer regardless of its content.

**Acceptance criteria**
1. Response bodies are byte-identical.
2. Timing distributions for concealed and genuine not-found overlap within noise:
   verified by construction (one code path, identical bytes), asserted by criterion 1,
   named in the report as verified by construction (CONV-TEST-007, D-153).

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

*Source: D-016*

Support can diagnose a legitimate permission problem without the response revealing
anything.

**Acceptance criteria**
1. The identifier resolves to an audit entry naming the permission and principal.
2. The identifier reveals nothing about record existence.

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
live.**

*Source: D-007, D-079*

Caching resolved outcomes leaves stale permissions after a role's action set is
edited, a resource is moved, a grant expires, or an account is restricted — none of
which bump an account's counter. Each is a silent grant of access that no longer
exists. Caching the inputs instead means the counter only needs to track what it
already tracks.

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

*Source: D-014*

Impersonation is out of scope. Two identity fields where one would do is defensible
on its own terms — it makes "who did this" unambiguous — and retrofitting a second
identity into every audit record and permission check later would not be.

**Acceptance criteria**
1. Both fields are present on every access context.
2. Both are written to every audit record.
3. No feature reads them as differing.

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
