# Decisions taken in the owner's absence

Every question that would have stopped a run under the original Tier 2 and Tier 3 rules,
decided under D-161 instead. One entry per decision, newest last. Each stands on its own:
nothing here needs a phase report to be read beside it.

The rule applied throughout: the reading most consistent with the chapters and the
decision log; fail closed where readings differ on what is allowed; the smaller public
surface; never a new package. Tier 3 questions take the strictest reading, the one that
grants least, keeps most or refuses, and are marked as such.

Nothing under `docs/spec/` or `docs/decision-log.md` was edited. Each entry states the
chapter text that should change so the owner can reconcile them.

---

## 1. The guide copy of the instruction file is committed

**Phase 2 · 2026-09-19 · Tier 3 · the working guide's sections 8 and 9, D-161**

*The question.* D-161's update replaces the copy of the working guide under
`docs/guide/` and the copy at the repository root, and says the root copy stays excluded
through `.git/info/exclude`, which reads as committing the guide copy. The exclusion in
the clone was the bare file name, which reaches a file of that name at any depth, so
both copies were excluded. Section 5 of the working guide names the other instruction
files in its own text, and sections 8 and 9 forbid letting a tool, model or vendor name
into the repository or its history in absolute terms.

*The readings.*

1. Keep both copies excluded. Nothing of the tooling enters the history, and the guide
   copy can be committed later at no cost once its section 5 is reworded.
2. Commit the guide copy and narrow the exclusion to the root, as the parenthetical
   says, accepting the two names in section 5.

*Chosen: 2.* `docs/decision-log.md`, which the owner wrote and committed at phase 0,
already carries the sentence naming the instruction files at the repository root
(D-149's "Also produced, outside the specification"). The names are therefore already in
the history by the owner's own hand, and the strictest reading of sections 8 and 9,
which bind what this work writes, cannot undo that or make a second occurrence a new
disclosure. D-161 propagates to the guide copy by name, which only a tracked file can
mean. `.git/info/exclude` now holds the root file alone, so the root instruction file
stays excluded and the other instruction files stay excluded at any depth.

*Tests that pin it.* None: this is a repository arrangement, not behaviour. The
exclusion is visible in `git status`, which shows neither root instruction file.

*Chapter text that should change.* The working guide's section 5, second paragraph:
the instruction files excluded are the ones at the repository root, and the versioned
copy under `docs/guide/` is part of the repository. Section 8's ban stands for
everything written from here on.

---

## 2. What makes a path need the host's rows is a derivation that confers what is asked

**Phase 2 · 2026-09-19 · Tier 2 · AUTHZ-DERIVE-001, D-161 decision 1**

*The question.* D-161 refuses a call without sources "on a type that declares a
derivation". A type declares derivations through its containment as well as its own
declaration, and a derivation confers a role, which allows some permissions and not
others. A deployment that has not written the conferred role at all has no derived grant
to miss. Taken literally, every check on such a type would be refused even where the
derivation could confer nothing.

*The readings.*

1. Refuse whenever the type or a container of it declares a non-materialised derivation,
   whatever the derivation confers.
2. Refuse when a non-materialised derivation reaching the type confers a role that
   allows one of the permissions being asked for.

*Chosen: 2.* This is the condition under which the stored grants are not the whole of
the answer, which is what the refusal exists to prevent (AUTHZ-PRIN-001 AC2). It is the
same predicate the filter already evaluates: `Derivations.ReachingAsync` is what decides
which relationships enter the rule, so the refusal and the rule cannot come apart.
Reading 1 refuses calls whose answer would have been complete, which is not failing
closed but failing loudly for nothing. A materialised derivation is left out of both,
because its grants are rows and are read as rows.

*Tests that pin it.*
`GateBehaviourTests.AUTHZ_PRIN_001_AC2_ACheckWithoutTheHostsRowsIsAFaultAsync`,
`GateBehaviourTests.AUTHZ_PRIN_001_AC2_ACheckNoDerivationReachesNeedsNoRowsAsync`.

*Chapter text that should change.* `03` AUTHZ-DERIVE-001, the D-161 paragraph: "on a
type a non-materialised derivation reaches, where the role it confers allows the
permission being asked for, a call without sources is refused with
`authz.derivation.sourcesmissing`".

---

## 3. The explanation is refused on a derived type rather than answered from grants alone

**Phase 2 · 2026-09-19 · Tier 2 · AUTHZ-GATE-004, AUTHZ-DERIVE-001, D-161 decision 1**

*The question.* D-161 names `RequireAsync` and `CapabilitiesAsync` as the two operations
that take the host's rows, and says the point of it is that "no path answers from stored
grants alone". `ExplainAsync` is a third path evaluated over the library's own
connection. On a type a derivation reaches it would state that no grant matched for a
record the filter admits.

*The readings.*

1. Leave `ExplainAsync` as it is. D-161 names two operations and adding a third is
   widening the decision.
2. Give `ExplainAsync` a sources overload as well, and have it name the derivation that
   decided.
3. Refuse `ExplainAsync` with the same fault on a type a derivation reaches, taking no
   new overload.

*Chosen: 3.* Reading 1 leaves a path answering from stored grants alone, which the
chapter sentence forbids. Reading 2 needs a shape for a grant that has no row: an
explanation names a grant by `GrantId`, and a derived grant has none, so it would need a
new public shape and a new sentence in AUTHZ-GATE-004 about what that shape carries;
that is the larger surface and the decision D-161 did not take. Reading 3 refuses rather
than answering wrongly, adds no public surface, and leaves the shape to whoever decides
what a derived grant looks like in an explanation. Concealment is still read first, so a
concealing type answers one way whatever else is true of it (AUTHZ-CONCEAL-003).

*Tests that pin it.*
`GateBehaviourTests.AUTHZ_PRIN_001_AC2_ACheckWithoutTheHostsRowsIsAFaultAsync` asks the
explanation for a disclosing type a derivation reaches and expects the fault.

*Chapter text that should change.* `03` AUTHZ-GATE-004: an explanation is refused with
`authz.derivation.sourcesmissing` on a type a derivation reaches, until a shape exists
for a grant with no row.

---

## 4. A capability page costs one query per permission a derivation confers

**Phase 2 · 2026-09-19 · Tier 2 · AUTHZ-GATE-005 AC1, D-161 decision 1**

*The question.* AC1 requires a page of fifty records to return capabilities "without
additional queries". A derivation confers a role, and what one role allows is not what
another allows, so a single query cannot say which of several permissions a derived
grant confers on which record.

*The readings.*

1. One query for the page and one further query for every permission a derivation
   confers, none of them growing with the page.
2. One query for the page and one per record, which is the N+1 the item exists to
   prevent.
3. Evaluate no derivation on the page, which is what the item's own reason forbids: a
   page that omits what a derived grant confers hides a control the person may use.

*Chosen: 1.* AC1's reason is the N+1 ("computing them per row in separate calls produces
N+1 queries"), so what it forbids is a cost that grows with the page. One further query
per permission is constant in the page's size and is the fewest queries that can answer
correctly, each of them one statement over the host's own relations.

*Tests that pin it.*
`GateBehaviourTests.AUTHZ_GATE_005_AC1_APageOfFiftyIsAnsweredWithoutAQueryPerRecordAsync`,
`GateBehaviourTests.AUTHZ_GATE_005_AC2_ADerivedGrantReachesTheCapabilityPageAsync`.

*Chapter text that should change.* `03` AUTHZ-GATE-005 AC1: "A list of 50 records
returns capabilities in one query for the stored grants and one further query per
permission a derivation confers, none of them per record."

---

## 5. The host's query is read where the rule is, not across a port

**Phase 2 · 2026-09-19 · Tier 2 · CONV-DESIGN-003, CONV-LAYOUT-001, LIB-HOST-002**

*The question.* The derived clause on the check and the page paths is a query over the
rows the host supplied, and something must read it. `Janus.Authorization` depends on
`Janus.Core` alone and holds no relational package; the awaiting extension methods are
EF Core's and live in `Janus.Storage`.

*The readings.*

1. Declare a port in `Janus.Authorization` taking the composed query and implement it in
   `Janus.Storage`. CONV-DESIGN-003 forbids an `IQueryable` crossing a port, so the rule
   or the test enforcing it would have to be narrowed to "no port returns one".
2. Reference `Microsoft.EntityFrameworkCore` from `Janus.Authorization`, which
   CONV-LAYOUT-001 gives to `Janus.Storage` alone.
3. Read the query where it is composed, through `IAsyncEnumerable<T>`, which the base
   class library declares and which the host's provider implements.

*Chosen: 3.* It adds no port, no package and no abstraction, and leaves CONV-DESIGN-003
and CONV-LAYOUT-001 exactly as they stand. The query is the host's, composed from the
rows the host supplied and carrying the host's own provider, so reading it issues nothing
of the library's own against a host table (LIB-HOST-002) and takes none of the library's
connections. A queryable that is not asynchronously enumerable raises, naming what the
host must supply, rather than being read synchronously.

*Tests that pin it.* `LibraryStructureTests.CONV_DESIGN_003_AC2_NoPortMethodReturnsAQueryable`
stands unchanged and passes; the derived paths are exercised by `TruthTableTests` and the
derived tests of `GateBehaviourTests`.

*Chapter text that should change.* None. This entry records a design that was chosen to
avoid changing one.

---

## 6. A refresh takes the host's rows and the context, as every other operation does

**Phase 2 · 2026-09-19 · Tier 2 · AUTHZ-DERIVE-005, D-161 decision 2**

*The question.* D-161 gives the signature as
`IDerivationMaterialiser.RefreshAsync(derivationName, resourceId)`. Recomputing the
grants means reading the rows of the relationship for that record, and LIB-HOST-002
forbids the library from querying a host table, so something has to carry the rows.
Every grant also records who granted it and why (AUTHZ-GRANT-002), and `granted_by` is
not nullable.

*The readings.*

1. Take the two arguments literally and find the rows another way: a port the host
   implements, or a queryable registered at startup. Both are a second shape for what
   `FilterSources` already is, and the first is a port the library calls into.
2. Take the same host-supplied sources object decision 1 gives the check and the page,
   and the `AccessContext` every other operation of the library takes, so the refresh is
   `RefreshAsync(context, derivation, resource, sources, cancellationToken)`.

*Chosen: 2.* Decision 1 settled that the rows a derivation is evaluated over are
supplied by the host as `FilterSources`; a second mechanism for the same rows would be
the larger surface and could come apart from the first. The context is not a widening
either: AUTHZ-GATE-001 AC2 has every operation name who is asking, and the grants the
refresh writes record that subject. A context naming no subject is a fault, because the
row it would write cannot be recorded against anybody. The resource is a `ResourceId`
and not a reference, as D-161 has it: the type is the relationship's own.

*Tests that pin it.*
`MaterialisationTests.AUTHZ_DERIVE_005_AC1_TheRowsTheRefreshWroteConferTheRoleAsync`,
`MaterialisationTests.AUTHZ_DERIVE_005_AC3_ARefreshRolledBackLeavesNoGrantAsync`.

*Chapter text that should change.* `03` AUTHZ-DERIVE-005, the Values paragraph:
"`IDerivationMaterialiser.RefreshAsync(context, derivationName, resourceId, sources)`,
which the host calls from the operation that changes the relationship, inside the same
unit of work, with the same sources object the filter takes."

---

## 7. A materialised derivation writes one grant per record of the type it is declared on

**Phase 2 · 2026-09-19 · Tier 2 · AUTHZ-DERIVE-005 AC1, AUTHZ-DERIVE-002**

*The question.* A derivation is declared on a resource type and follows a relationship
that is about a resource type, and the two need not be the same one. Evaluated, it
reaches records of the declared type that sit at or under the record the relationship
names. Precomputed "as ordinary grants", what record does the grant sit on?

*The readings.*

1. One grant on the record the relationship names. Cheapest, and exact where the
   derivation is declared on the type that relationship is about. Where it is declared
   on a narrower type, the grant reaches records of every other type under that record
   as well, which is more than the derivation confers.
2. One grant on every registered record of the declared type that the relationship's
   record reaches. Exact in both cases, and where the two types are the same it is the
   one grant of reading 1, because a record is its own ancestor at depth zero.
3. Refuse at startup to materialise a derivation declared on a narrower type than its
   relationship, and take reading 1 for the rest.

*Chosen: 2.* Reading 1 confers more than evaluating the derivation would, which is not
failing closed. Reading 3 fails closed but refuses a configuration the evaluated path
supports today, and needs a startup code `10` does not carry. Reading 2 confers exactly
what the derivation confers in both shapes, needs no new code and no refusal, and the
rows it writes are as many as the optimisation ladder says materialisation costs
(AUTHZ-DERIVE-006: a synchronisation problem). Inheritance then carries each grant
exactly as far as it carries the derivation, which is AUTHZ-DERIVE-002.

*Tests that pin it.*
`TruthTableTests.AUTHZ_TEST_001_AC3_EveryCaseDecidesTheSameWayMaterialisedAsync`, which
runs the whole table against the same deployment with the derivation materialised,
including the case whose fact sits on a container above the record.

*Chapter text that should change.* `03` AUTHZ-DERIVE-005: "the grants are written on
each registered record of the type the derivation is declared on that the record the
relationship names reaches".

---

## 8. What AUTHZ-DERIVE-005 AC3 is proved by

**Phase 2 · 2026-09-19 · Tier 2 · AUTHZ-DERIVE-005 AC3, D-161 decision 2**

*The question.* AC3 reads "Refresh occurs in the same transaction as the change that
triggers it, or drift is detected and reported." The change that triggers it is a write
to a host table, which the library neither makes nor can enlist: the host's context and
the library's hold their own connections. The sweep D-161 describes cannot supply the
host's rows by itself either, for the same reason the check cannot.

*The readings.*

1. Prove the first clause literally, which needs the library to take part in the host's
   own transaction: a public seam onto the library's connection, or an ambient
   transaction the host's context enlists in. Both are a new public mechanism, and the
   second is a distributed transaction in all but name.
2. Prove what the library can hold itself to: the refresh writes inside the caller's
   unit of work, so a unit of work that is not committed leaves no grant; and a refresh
   run after the relation changed reports the difference and corrects it in the same
   run. The host putting its own write in that unit of work is the host's part, and the
   contract states it.

*Chosen: 2.* AC3 is a disjunction and both of its clauses are now tested. The library
cannot write the host's row (LIB-HOST-002), so "the same transaction" can only mean the
unit of work the host runs the operation in, which is what `IUnitOfWork` already is.
Reading 1 would add a public mechanism for something the host already controls.

*What is left for phase 9.* The sweep itself: something has to run the refresh every
`derivation.materialised.driftcheck` and raise the degradation condition with the
derivation's name in `details`. It needs the host's rows, so it is a job the host hands
its own sources to, and background jobs, named principals and the alert conditions are
phases 4 and 9 of the implementation plan. The setting exists and the refresh reports
the drift; no criterion of AUTHZ-DERIVE-005 waits on it.

*Tests that pin it.*
`MaterialisationTests.AUTHZ_DERIVE_005_AC3_ARefreshRolledBackLeavesNoGrantAsync`,
`MaterialisationTests.AUTHZ_DERIVE_005_AC3_ARefreshFindsTheDriftAndCorrectsItAsync`.

*Chapter text that should change.* `03` AUTHZ-DERIVE-005 AC3: "Refresh occurs in the
unit of work the host runs the change in, or the next refresh detects the difference,
reports it and corrects it."

---

## 9. Reverse lookup over derivations is built in phase 8 and nothing of it now

**Phase 2 · 2026-09-19 · Tier 2 · AUTHZ-DERIVE-007, D-161 decision 3**

*The question.* Phase 2 builds "`03` all", and AUTHZ-DERIVE-007 is in `03`. D-161
decision 3 says the reverse lookup over derivations is the phase 8 view and that nothing
of it is built now. This entry records the one item of `03` that phase 2 leaves unbuilt,
because the ledger has to stand alone.

*The readings.* None: the owner settled it in D-161.

*Chosen.* Nothing of the reverse lookup over derived grants is built in phase 2. The
Values paragraph on AUTHZ-DERIVE-007 states how the view will answer, and the view is
phase 8. `authz.reverselookup.budget` exists as a setting from phase 0, which is what
the migration trigger of AUTHZ-SEAM-001 is measured against.

*Tests that pin it.* None of the view. The item's own criteria are the cache criteria,
and those are tested: nothing is keyed on a subject-action-resource triple, a revocation
takes effect on the next request, and an expired grant confers nothing without a sweep.

*Chapter text that should change.* None.
