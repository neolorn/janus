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

From entry 110 the ledger carries the corrections of D-162, which reviewed the entries
before it. Each names the entry it replaces, what D-162 decided and what was built; the
entry it replaces carries one line saying it is superseded and where the correction is.
D-162 is the specification for every point it settles, so a correction states no
readings of its own. The rows chapter 10 is owed for what those corrections add are
listed once, at the end of this file.

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

**Superseded by D-162.** Applied in entry 112.

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

**Superseded by D-162.** Applied in entry 111.

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

**Superseded by D-162.** Applied in entry 110.

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

---

## 10. The synchronizer token is presented in `X-Janus-Csrf`

**Phase 3 · 2026-09-20 · Tier 2 · BFF-CSRF-001, BFF-CSRF-006, `10` section 5, D-153**

*The question.* BFF-CSRF-001 requires a synchronizer token bound to the server-side
session and validated on every state-changing request. D-153 names the cookie the token
is set in, `__Host-janus-csrf`, and BFF-CSRF-003 names the custom request header whose
presence is required, `X-Janus-Request`. No chapter names the header the token's value
travels back in, and a value that only ever travels in its own cookie is a double-submit
token, which BFF-CSRF-001 rejects for a stateful backend.

*The readings.*

1. Present the value in `X-Janus-Request`, the header BFF-CSRF-003 already names, so the
   boundary has one custom header and the presence check and the value check are the
   same check.
2. Present the value in a second header of its own, leaving `X-Janus-Request` as the
   presence signal BFF-CSRF-003 describes.

*Chosen: 2, `X-Janus-Csrf`.* The two headers answer different questions and fail at
different layers. BFF-CSRF-003's header exists so that a request a form can produce is
refused before anything reads a session; it is checked with no session in hand and its
value is never read. Folding the token into it would make the cheap check depend on the
session lookup, which BFF-ORDER-001 places later, and would leave a request carrying a
valid token for another session indistinguishable from one carrying no header at all.
Both headers are forbidden request headers for a form, so neither can be set
cross-origin without a preflight the browser will not grant.

*Tests that pin it.*
`BrowserProfileTests.BFF_CSRF_001_AC1_AStateChangeWithoutASessionBoundTokenIsRejectedAsync`,
`BrowserProfileTests.BFF_CSRF_003_AC1_ARequestWithoutTheCustomHeaderIsRejectedAsync`,
`BrowserProfileTests.BFF_CSRF_006_AC2_RotationInvalidatesThePreviousTokenAsync`.

*Chapter text that should change.* `17` BFF-CSRF-001 and `10` section 5's header list
should name `X-Janus-Csrf` beside `X-Janus-Request`, and say that the first carries the
token and the second is checked for presence only.

**Superseded by D-163** as to the header names. Applied in entry 166.

---

## 11. A password above the maximum is refused as a value above a ceiling

**Phase 3 · 2026-09-19 · Tier 2 · AUTH-PASS-001 AC3, `10` section 1.2**

*The question.* AUTH-PASS-001 sets `password.maximum` and requires a password beyond it
to be refused. `10` section 1.2 gives `auth.password.tooshort` for the floor and no code
for the ceiling.

*The readings.*

1. Add a code for the ceiling.
2. Use `configuration.value.aboveceiling`, the code the bounded setting already refuses
   an over-ceiling value with.

*Chosen: 2.* Adding a code is a change to the public contract of LIB-API-001 for a case
the chapter did not name, and the existing code says exactly what happened. The smaller
surface wins.

*Tests that pin it.*
`PasswordFloorTests.AUTH_PASS_001_AC3_TheMaximumIsAcceptedAndOneBeyondItIsRefused`.

*Chapter text that should change.* `10` section 1.2 should state which code a password
beyond `password.maximum` carries.

**Superseded by D-162.** Applied in entry 113.

---

## 12. The offline and self-hosted corpora are files the deployment holds

**Phase 3 · 2026-09-19 · Tier 2 · AUTH-PASS-004, INT-PWD-002, INT-PWD-003, D-153**

*The question.* INT-PWD-002 requires an offline list to answer when the range API cannot,
and INT-PWD-003 requires switching to a self-hosted corpus to be a configuration change.
`10` section 4 gives `password.blocklist.source` and `password.blocklist.sources`, which
name which corpus is in force, and no key giving either corpus an address.

*The readings.*

1. Add keys for the two addresses.
2. Take both as files the deployment supplies at a path it already controls, read
   through the same port the range API is read through.

*Chosen: 2.* `10` section 4 is authoritative for the key list, and adding two keys to it
is not a Tier 1 correction. A corpus is a data file like the location database of
INT-GEN-006, and the port that reads it is where a deployment points it. Naming the
self-hosted corpus in `password.blocklist.sources` remains the whole of switching to it,
which is what INT-PWD-003 AC1 states.

*Tests that pin it.*
`PasswordScreeningTests.INT_PWD_003_AC1_SwitchingToTheSelfHostedCorpusIsAConfigurationChangeAsync`,
`ScreeningTests.INT_PWD_002_AC1_WithTheServiceUnreachableTheOfflineListAnswersAsync`.

*Chapter text that should change.* `10` section 4.2 should say where each corpus is
read from, or state that the address is not configuration.

**Superseded by D-162.** Applied in entry 114.

---

## 13. A corpus whose age cannot be read is treated as no corpus

**Phase 3 · 2026-09-19 · Tier 3 · AUTH-PASS-004, `password.blocklist.corpusmaxage`**

*The question.* `10` gives `password.blocklist.corpusmaxage` and AUTH-PASS-004 requires a
password judged against a corpus older than it to be refused rather than accepted
unscreened. Nothing says what happens when a corpus carries no date at all.

*The readings.*

1. Treat an undated corpus as current, since no rule says it is stale.
2. Treat it as no corpus, so the screening falls through to the next source and, with
   none left, refuses.

*Chosen: 2, the strictest reading.* The maximum age exists because a stale corpus
silently accepts passwords it should refuse. A corpus whose age cannot be established
cannot be judged against the bound, and failing closed is what the working guide's
section 4 requires of a security control.

*Tests that pin it.*
`PasswordScreeningTests.INT_PWD_002_AC2_WithBothUnavailableTheOperationFailsAsync`,
`ScreeningTests.INT_PWD_002_AC2_WithNeitherAvailableTheOperationFailsAsync`.

*Chapter text that should change.* AUTH-PASS-004 should say that a corpus whose age
cannot be read is not a corpus.

---

## 14. The application kind is declared at registration, and its zero value is the stricter one

**Phase 3 · 2026-09-20 · Tier 2 · BFF-OWN-001, BFF-CSRF-005**

*The question.* BFF-CSRF-005 requires `SameSite=Strict` for the management application
and `Lax` elsewhere, so something has to tell the library which application it is running
in. BFF-OWN-001 AC1 requires mounting to take no security-relevant configuration.

*The readings.*

1. Take the application kind as an argument to the mount call,
   `UseJanusBrowserProfile(application)`.
2. Take it at registration, on `AddJanus`, so the mount call takes nothing.

*Chosen: 2.* BFF-OWN-001 AC1 is about the mount call, and an argument there is exactly
the configuration it forbids. The value is declared once, beside the connection string
and the keys, where a deployment's other facts are declared. The enum's zero value is
`Management`, the stricter of the two, so a default-constructed or unset value produces
`SameSite=Strict` rather than the looser attribute.

*Tests that pin it.*
`BrowserProfileTests.BFF_OWN_001_AC1_MountingTakesNoSecurityRelevantConfiguration`,
`BrowserCookieTests.BFF_CSRF_005_AC1_TheManagementApplicationsCookieIsStrict`,
`BrowserCookieTests.BFF_CSRF_005_AC3_NoCookieIsIssuedWithSameSiteNone`.

*Chapter text that should change.* `17` BFF-OWN-001 should say where the application
kind is declared, and `07` LIB-HOST-001 should list it among what a host declares.

---

## 15. Every layer of the browser profile answers the one code the chapter gives the layer

**Phase 3 · 2026-09-20 · Tier 2 · BFF-CSRF-001 to BFF-CSRF-004, `10` section 1.2**

*The question.* Four layers refuse a request at the browser boundary: fetch metadata,
the custom request header, the origin, and the synchronizer token. `10` gives the layer
one code, `auth.session.csrfinvalid`.

*The readings.*

1. Give each layer a code of its own, so an operator can see which refused.
2. Answer the one code from all four, and record which layer refused in the log entry
   the layer writes.

*Chosen: 2.* A distinct code tells an attacker which control they have defeated and
which remains, which is the differential response BFF-ABUSE-002 forbids. `10` names one
code for the layer, and adding three is a change to the contract for a benefit the log
already provides to the only party entitled to it.

*Tests that pin it.*
`BrowserProfileTests.BFF_CSRF_002_AC1_ACrossSitePostIsRejectedBeforeAnyEndpointAsync`,
`BrowserProfileTests.BFF_CSRF_003_AC1_ARequestWithoutTheCustomHeaderIsRejectedAsync`,
`BrowserProfileTests.BFF_CSRF_004_AC1_AMismatchedOriginIsRejectedAndLoggedAsync`,
`BrowserProfileTests.BFF_CSRF_001_AC1_AStateChangeWithoutASessionBoundTokenIsRejectedAsync`.

*Chapter text that should change.* `17` section on the CSRF layers should state that all
four answer one code and that the layer is recorded only in the log.

---

## 16. The framework's antiforgery is not used; the primitives under it are

**Phase 3 · 2026-09-20 · Tier 2 · BFF-CSRF-007, BFF-CSRF-001**

*The question.* BFF-CSRF-007 requires the framework's own support to be used where it
exists rather than rebuilt. The framework's antiforgery is a double-submit design: it
issues a cookie and a matching field and compares them, binding to no server-side
session. BFF-CSRF-001 requires a synchronizer token bound to the server-side session and
rejects double submit for a stateful backend.

*The readings.*

1. Use the framework's antiforgery and accept the design it implements.
2. Use the framework's primitives, its randomness and its constant-time comparison, and
   bind the token to the session record as BFF-CSRF-001 requires.

*Chosen: 2.* Where the two items conflict, the one that states the security property
wins; BFF-CSRF-007's purpose is to avoid rebuilding what the platform provides, and the
platform's randomness and comparison are what is reused. Nothing is hand-rolled: the
token is drawn by `RandomNumberGenerator` and compared by
`CryptographicOperations.FixedTimeEquals`, and the binding is a column on the session
row.

*Tests that pin it.*
`BrowserProfileTests.BFF_CSRF_007_AC1_TheTokenIsDrawnAndComparedByTheFramework`,
`SessionStoreTests.BFF_CSRF_001_AC1_TheRowCarriesTheTokenBoundToTheSessionAsync`.

*Chapter text that should change.* `17` BFF-CSRF-007 should say that the framework's
antiforgery is excluded by BFF-CSRF-001 and that its primitives are what the item
requires.

---

## 17. AUTH-FACT-004's criteria are split across three phases

**Phase 3 · 2026-09-20 · Tier 2 · AUTH-FACT-004, D-151**

*The question.* AUTH-FACT-004 states that a verification code is not a credential. `10`
line 302 (D-151) states it is not an entry of the catalogue. Its AC1 is about the
catalogue; AC2 and AC3 are about the code's own storage, its lifetime and its attempt
cap, none of which the factor catalogue holds.

*The readings.*

1. Build a verification-code store in phase 3 so all three criteria are decided here.
2. Decide AC1 here, where the catalogue is, and leave AC2 and AC3 to the flows that
   issue codes.

*Chosen: 2.* A code is issued by registration (REG-SESS-003), by identifier
verification and by recovery, each of which owns the session the code belongs to and the
attempt count against it. Building a store now with no issuer would fix a shape before
the three callers exist, which is the kind of guess the working guide's section 10
forbids.

*Tests that pin it.* `FactorCatalogueTests.AUTH_FACT_004_AC1_AVerificationCodeIsNoCatalogueEntry`.

*Chapter text that should change.* None. The implementation plan should say which phase
owns AUTH-FACT-004 AC2 and AC3.

**Superseded by D-162.** Applied in entry 115.

---

## 18. Nothing of the emitted-event surface is built in phase 3

**Phase 3 · 2026-09-20 · Tier 2 · AUTH-FACT-016 AC5, LIB-API-001, `10` section 5b**

*The question.* AUTH-FACT-016 AC5 requires `DeviceVerified` to be emitted once per
completed new-device check. `10` section 5b lists thirty events and `07` LIB-API-001
makes them part of the contract. No phase of the plan names section 5b, and no port for
emitting an event exists.

*The readings.*

1. Add an event port in phase 3 for the one event this phase raises.
2. Build nothing of it here and record the criterion as waiting.

*Chosen: 2.* An event surface has an idempotency key, a subject reference, the acting
and effective identities and a delivery contract with the outbox (INT-MAIL-007,
INT-MAIL-008), and it is the notification pipeline of phase 4 that first needs all of
them. Inventing a narrower port now for one event would be a second way of doing
something that already has one coming.

*Tests that pin it.* None. The check itself is decided by
`DeviceServiceTests.AUTH_FACT_016_AC1_AnUnseenBrowserHoldsAPasswordOnlySignInAsync` and
`DeviceServiceTests.AUTH_FACT_016_AC2_ARememberedBrowserIsNotHeldAgainAsync`.

*Chapter text that should change.* None. The implementation plan should name the phase
that builds `10` section 5b.

---

## 19. The configuration store's implementation waits for the runtime edits of phase 4

**Phase 3 · 2026-09-20 · Tier 2 · OPS-CFG-008, CONV-DESIGN-007, `10` section 4**

*The question.* Every service of the authentication area reads `IConfigurationStore`.
The interface, the typed settings and the `settings` table exist; no type implements the
interface, so none of the area's services can be registered in `AddJanus`.

*The readings.*

1. Implement it here, which means giving `Setting<TValue>` a way to read a stored string
   back into its value type, a public-surface addition.
2. Leave it to the phase that first edits configuration at runtime, and record that the
   area's services are constructed by their tests and not yet by the container.

*Chosen: 2.* How a stored value is written and read is the same question as how the
management application writes one, which is OPS-CFG-004 and the restriction edits of
phase 4. Deciding it now from one side would fix the serialization of every key in
`10` section 4 before the writer exists. The smaller public surface wins, and nothing
of the area's behaviour depends on the answer: every service takes the port and every
test supplies a fake that honours it.

*Tests that pin it.* None. The area's services are exercised through
`ConfigurationInMemory`, which every test of the area constructs.

*Chapter text that should change.* None. The implementation plan should say which phase
implements the configuration store.

**Superseded by D-162.** Applied in entry 116.

---

## 20. INT-GEN-006 is built with the background jobs, and the session degrades to no location until then

**Phase 3 · 2026-09-20 · Tier 2 · INT-GEN-006, AUTH-SESS-013**

*The question.* AUTH-SESS-013 shows a city on each session in the listing. INT-GEN-006
says the city is resolved from a local IP-to-city database refreshed on a schedule, and
that a stale or missing file degrades to no location. The plan gives phase 4 `05`
INT-GEN-001 to 005 and names INT-GEN-006 in no phase.

*The readings.*

1. Bundle or read a location database in phase 3.
2. Take the place from the caller, hold it under the person's key, show no location
   where none is given, and leave the resolver and its refresh job to the phase that
   builds background jobs.

*Chosen: 2.* The database is a licensed data file the owner has to choose, as the
corpus of INT-PWD-002 is, and its refresh is a background job with a degradation
condition, which is phase 9's subject. The degraded behaviour INT-GEN-006 itself
specifies, no location rather than an external lookup, is what phase 3 does, and no
criterion of AUTH-SESS-013 waits on the resolver.

*Tests that pin it.*
`SessionServiceTests.AUTH_SESS_013_AC2_EachEntryCarriesTimesDeviceAndCityAsync`,
`SessionStoreTests.AUTH_SESS_013_AC4_TheLocationIsNotReadableFromTheTableAsync`,
`SessionStoreTests.AUTH_SESS_013_AC4_TheLocationIsUnreadableAfterErasureAsync`.

*Chapter text that should change.* None. The implementation plan should name the phase
that builds INT-GEN-006.

**Superseded by D-162.** Applied in entry 117.

---

## 21. Two source scans decide the fact their criteria state rather than the words

**Phase 3 · 2026-09-20 · Tier 2 · AUTH-PRIN-001 AC3, AUTH-PRIN-002 AC1**

*The question.* Both criteria are stated over the whole library and are decided by
reading its source. AUTH-PRIN-001 AC3 says no path returns an allow on an exception;
AUTH-PRIN-002 AC1 says no enum, flag or claim distinguishes staff from customers. Read
as a literal word search, the first flags every catch block and the second flags
`Janus.Core/TakedownTrigger.cs`, whose values `StaffReport` and `CustomerReport` are
`10` section 5.12d's own vocabulary for what raised a takedown report.

*The readings.*

1. Keep the literal search and accept that the criteria cannot pass.
2. Scan for the fact each criterion states: a catch that answers with anything but a
   refusal or an exception; a line of code naming a kind of person, with the one file
   whose vocabulary is the chapter's stated as the expected result rather than
   excluded silently.

*Chosen: 2.* A criterion that cannot be satisfied as written is a Tier 3 stop under the
original rule, and the strictest reading here is the one that keeps the check rather
than weakening it: both tests still fail on a real violation. The takedown trigger is
named in the assertion, so adding a second file that distinguishes staff from customers
fails the test.

*Tests that pin it.*
`FailClosedTests.AUTH_PRIN_001_AC3_NoPathReturnsAnAllowOnAnException`,
`FactorCatalogueTests.AUTH_PRIN_002_AC1_NoEnumFlagOrClaimDistinguishesStaffFromCustomers`.

*Chapter text that should change.* AUTH-PRIN-002 AC1 should say that a vocabulary of
`10` naming what raised a report is not a property of a principal.

---

## 22. Sending, restrictions and alerting are built in `Janus.Authentication`

**Phase 4 · 2026-09-20 · Tier 2 · CONV-LAYOUT-001, AUTH-ABUSE-004, OPS-ALERT-001**

*The question.* CONV-LAYOUT-001 fixes the project list and what each holds:
`Janus.Authentication` holds "factors, sessions, flows". The sending path, the named
restrictions and the alert router are none of those words, and registration
(`Janus.Identity`) and the operations code will both send. No project is named for
them, and adding one is a change to the table.

*The readings.*

1. `Janus.Core`, because every area sends. Core holds contracts and no behaviour, so
   this would put the first behaviour there.
2. A new project. CONV-LAYOUT-001 states the list; adding to it is not the implementer's.
3. `Janus.Authentication`, because the items are `02` section 7 and the chapter the
   restrictions are stated in is the authentication chapter.

*Chosen: 3.* The items governing every one of these types (AUTH-ABUSE-001 to 008) are
in `02`, which is `Janus.Authentication`'s chapter, and the alert conditions of `06`
are raised by that code. Core keeps the contracts a host implements
(`IMailTransport`, `ISmsTransport`, `IMessageTemplates`, `Recipients`,
`IntegrationEndpoints`, `RestrictionKeySuppliers`) and the vocabularies, which is what
Core is for.

*The consequence the owner should see.* `Janus.Identity` depends on Core alone, so it
cannot call the sending service. Registration's own sends, in phase 5, reach it either
through a port declared in Core or through `Janus.Hosting`, which references
everything. Phase 5 decides which; nothing in phase 4 forecloses it.

*Tests that pin it.*
`LibraryStructureTests.CONV_LAYOUT_001_AC3_DependenciesAreExactlyTheOnesTheTableGives`.

*Chapter text that should change.* CONV-LAYOUT-001's `Janus.Authentication` row should
read "factors, sessions, flows, and the sending path the flows use".

**Superseded by D-162.** Applied in entry 118.

---

## 23. A transport that would not take a message is a refusal to the caller; the durable retry is the outbox publisher's

**Phase 4 · 2026-09-20 · Tier 2 · AUTH-ABUSE-004, IDN-LIFE-003a, the plan's phase 4 row**

*The question.* The plan's phase 4 row says "notification pipeline with retry". No item
of `02`, `05` or `06` states a retry schedule for a send. The only retry schedule in
the specification is the transactional outbox's (`outbox.poll.interval`,
`outbox.retry.initial`, `outbox.retry.factor`, `outbox.retry.maxattempts`,
IDN-LIFE-003a), whose publisher the plan places with the background jobs in phase 9.

*The readings.*

1. Build a second retry mechanism for sends now, with a schedule no chapter states.
2. Return the transport's refusal to the caller, count nothing against any
   restriction, and leave the durable retry to the one mechanism the specification
   describes.

*Chosen: 2.* A schedule no chapter gives is a decision about behaviour, and a second
retrying mechanism beside the outbox is a second way of doing something that already
has one. AUTH-ABUSE-004 states what a refused transport means for the counting (a
failed delivery does not count), which is built and tested; nothing states that the
library itself re-attempts.

*Tests that pin it.*
`SendingServiceTests.AUTH_ABUSE_004_AC2_ATransportRefusalCountsNothingAsync`.

*Chapter text that should change.* The plan's phase 4 row should say that the retry of
a send is the outbox publisher's, built in phase 9, or an item should state the
schedule.

**Superseded by D-162.** Applied in entry 119.

---

## 24. A refused send carries `retryAt` and nothing else

**Phase 4 · 2026-09-20 · Tier 2 · AUTH-ABUSE-004, CONV-CONTENT-001, LIB-API-003**

*The question.* A send refused by a restriction strands a person who is waiting for a
code, and the remedy the chapter provides is a support grant. Whether the refusal
should carry a route to support (an address, a link, a flag) is not stated; `09` says
the refusal carries `retryAt` in `details`.

*The readings.*

1. Carry a support route in the failure, so the frontend need not know one.
2. Carry `retryAt` alone, as `09` states, and let the frontend write whatever it
   shows.

*Chosen: 2.* CONV-CONTENT-001 puts every user-facing sentence on the frontend, and
LIB-API-003 keeps prose out of a failure. A support route is either a sentence or a
deployment's own address; neither is the library's. What `09` says the refusal carries
is exhaustive as written.

*Tests that pin it.*
`SendingServiceTests.AUTH_ABUSE_004_AC1_AFourthTextMessageInsideADayIsRefusedWithTheLiftAsync`,
`SendingServiceTests.AUTH_ABUSE_002_AC3_ARefusedSendAnswersTheSameForEitherAddressAsync`.

*Chapter text that should change.* None.

---

## 25. A restriction change is a loosening unless every bucket it keeps is at least as strict

**Phase 4 · 2026-09-20 · Tier 2 · AUTH-ABUSE-004 AC3, OPS-CFG-002, OPS-CFG-008**

*The question.* AUTH-ABUSE-004 names four loosenings: a higher max, a shorter interval,
a removed bucket, a deleted restriction. It does not say what a change of key, of host
key name, of purpose, or a replacement of one bucket set by an unrelated one is.

*The readings.*

1. Only the four named changes are loosenings; everything else is a tightening and
   needs no reason and raises no alert.
2. A change is a loosening unless it is provably a tightening: every bucket that stood
   before is still answered by a bucket at least as strict, the key and the host key
   name are unchanged, and the purpose is not narrowed.

*Chosen: 2.* OPS-CFG-002 classifies a change with no direction as a loosening, which is
the specification's own rule for exactly this case, and the strictest reading of
AUTH-ABUSE-004 AC3 is the one that asks for a reason and raises an alert wherever the
direction cannot be shown. Narrowing a purpose (from `any` to `notification`) leaves
sends ungoverned and is a loosening; broadening it governs more and is not.

*Tests that pin it.*
`RestrictionAdministrationTests.AUTH_ABUSE_004_AC3_ALooseningRaisesANormalAlertAsync`,
`RestrictionAdministrationTests.AUTH_ABUSE_004_AC3_ALooseningWithoutAReasonIsRefusedAsync`,
`RestrictionAdministrationTests.OPS_CFG_008_AC4_AnEditIsLiveAuditedWithBothValuesAndAlertedAsync`.

*Chapter text that should change.* AUTH-ABUSE-004 should say that its four are examples
and that any change not provably a tightening is a loosening.

---

## 26. The text-message budget is measured over the template as the catalogue holds it

**Phase 4 · 2026-09-20 · Tier 2 · AUTH-ABUSE-005, INT-SMS-003, INT-SMS-005a**

*The question.* INT-SMS-003 says a test fails if any **rendered** template exceeds its
language's budget. A template is rendered with the values of one send (a code, a name),
which exist only at send time, and INT-SMS-005a says failures surface at startup rather
than at send.

*The readings.*

1. Measure at send time, where the rendered text is known, and refuse or split there.
2. Measure at startup over the template as the catalogue holds it, placeholders and
   all.

*Chosen: 2.* INT-SMS-005a states the invariant as a startup one, and AUTH-ABUSE-005 AC3
says an over-budget message stops the deployment. Measuring at send time would surface
the fault to the person waiting for a code, which is the outcome both items exist to
prevent. The placeholder is counted as written, which states a rule a deployment can
write to.

*Tests that pin it.*
`SendingValidationTests.AUTH_ABUSE_005_AC3_AnOverBudgetTextMessageStopsStartupAsync`,
`SendingValidationTests.INT_SMS_003_AC1_ALatinMessageOverItsBudgetStopsStartupAsync`,
`SendingValidationTests.INT_SMS_003_AC2_EveryTextMessageIsMeasuredInEveryLanguageAsync`.

*Chapter text that should change.* INT-SMS-003 AC1 should say "any template" rather
than "any rendered template", and should state how a placeholder is counted.

**Superseded by D-162.** Applied in entry 120.

---

## 27. A plaintext endpoint is caught through a register the host declares, not a setting for each integration

**Phase 4 · 2026-09-20 · Tier 2 · INT-GEN-001, INT-SMS-006, LIB-HOST-001**

*The question.* INT-GEN-001 says a configuration specifying a plaintext endpoint is
rejected at startup, and its criteria say the error names which integration and which
setting. `10` section 4 carries no key for any integration's base address, and
INT-SMS-006 forbids naming a provider anywhere in the library.

*The readings.*

1. Add a settings key per integration, which would put provider-shaped keys in `10`
   and a list of integrations in the library.
2. Have the host declare its outbound endpoints as a register of integration, key and
   address, and refuse a plaintext one at startup. The key in the error is the name the
   host gives, so the error names the host's own setting.

*Chosen: 2.* It adds no key to `10`, names no provider, and answers both criteria. A
deployment that declares none starts, as LIB-HOST-001 requires of everything outside
the declarations.

*Tests that pin it.*
`SendingValidationTests.INT_GEN_001_AC1_APlaintextEndpointStopsStartupAsync`,
`IntegrationEndpointsTests.INT_GEN_001_AC2_APlaintextAddressIsNamedWithItsIntegrationAndKey`,
`IntegrationBoundaryTests.INT_SMS_006_AC1_NoProviderNameAppearsInTheLibrary`.

*Chapter text that should change.* INT-GEN-001 AC2 should say the error names the
integration and the key the host declared it under.

---

## 28. The restriction administration is built as an operation; its endpoints wait for the phase that mounts the administrative surface

**Phase 4 · 2026-09-20 · Tier 2 · AUTH-ABUSE-004 AC3 and AC4, OPS-CFG-008**

*The question.* AUTH-ABUSE-004 states the restriction edits and the grant as HTTP
endpoints (`GET/PUT/DELETE /admin/restrictions/{name}`,
`POST /admin/restrictions/{name}/grant`). No phase of the plan builds an administrative
HTTP surface, and phase 4 is not a phase that mounts endpoints.

*The readings.*

1. Mount the endpoints here, which would put the first administrative routes in the
   library in a phase whose chapters name none.
2. Build the operation behind them, gated, audited and alerted exactly as the item
   says, and leave the routes to the phase that mounts the administrative surface.

*Chosen: 2.* Everything the criteria decide (the gate, the reason, the audit entry, the
alert, the edit reaching the next send, the credit being spent) is the operation's, and
each is tested against it. A route is a thin call onto the operation and belongs with
the rest of the administrative surface.

*Tests that pin it.* The five `RestrictionAdministrationTests.AUTH_ABUSE_004_AC3_*`
tests and the three `RestrictionAdministrationTests.AUTH_ABUSE_004_AC4_*` tests.

*Chapter text that should change.* The plan should name the phase that mounts the
administrative routes, and AUTH-ABUSE-004's criteria should be readable against the
operation.

---

## 29. The event port publishes without an outcome; the consumer returns one

**Phase 4 · 2026-09-20 · Tier 2 · CONV-DESIGN-005 AC1, LIB-API-001, IDN-LIFE-003a**

*The question.* CONV-DESIGN-005 AC1 says every method on a public contract returns an
outcome. `IEvents.PublishAsync` is called after the transaction the event describes has
committed, so a caller cannot act on a failure: the event is already true.

*The readings.*

1. `PublishAsync` returns an outcome, which every call site must then handle, and the
   only honest handling is to ignore it.
2. `IEvents` is a port, not an operation contract, and is exempt as `ISecretSource` and
   `IUnitOfWork` already are; `IEventConsumer.HandleAsync` returns an outcome, because
   delivery is at least once and a consumer that did not do its work has to say so.

*Chosen: 2.* CONV-DESIGN-005 is about the service contracts of LIB-API-005, the
operations the library performs, and the exemption list already exists with the same
reasoning. The consumer side keeps the outcome, which is where a failure means
something.

*Tests that pin it.*
`ResultContractTests.CONV_DESIGN_005_AC1_EveryContractMethodReturnsAnOutcome`.

*Chapter text that should change.* CONV-DESIGN-005 should name the ports its AC1 does
not reach, or `07` should state that a publication port is not an operation contract.

**Superseded by D-162.** Applied in entry 121.

---

## 30. The message catalogue answers with an outcome, not with a missing template

**Phase 4 · 2026-09-20 · Tier 2 · CONV-DESIGN-005 AC2, AUTH-ABUSE-005, LIB-EXT-001**

*The question.* `IMessageTemplates.Find` is asked for a message in a language. A
deployment whose catalogue has no answer is a startup fault, but the port is also
called at send time.

*The readings.*

1. Return the template or null, and let each caller decide what a missing one means.
2. Return an outcome, so a catalogue that cannot answer says so in one shape that
   startup and the send path both read.

*Chosen: 2.* CONV-DESIGN-005 AC2 forbids reporting "not found" by returning null. The
startup check then names the first message key the catalogue cannot answer, which is
what AUTH-ABUSE-005 AC3 asks for, and a host implementing the port has one way to
refuse.

*Tests that pin it.*
`SendingValidationTests.AUTH_ABUSE_005_AC3_ALanguageTheCatalogueCannotAnswerInStopsStartupAsync`,
`ResultContractTests.CONV_DESIGN_005_AC2_NoContractReturnsNullForNotFound`.

*Chapter text that should change.* None.

---

## 31. A send counter is swept by the time its longest bucket settles at

**Phase 4 · 2026-09-20 · Tier 2 · AUTH-ABUSE-004 AC6, INT-SMS-005**

*The question.* AUTH-ABUSE-004 AC6 says the per-destination record is absent once every
bucket for that key is empty. A record whose key is never sent to again is reached by
nothing: the recording path prunes the key it is writing, and the release path prunes
the key a delivery report names. Neither touches a key nobody mentions again.

*The readings.*

1. Prune only on the paths that already read a key, and accept that a key sent to once
   stands in the table for ever.
2. Keep, on each row, the time at which the last send counted there ages out of the
   longest bucket it was counted against, and delete every row past that time as part
   of the next recording.

*Chosen: 2.* AC6 states the absence as a fact about the table, not about a path, and the
strictest reading is the one under which a dump of the table at any time holds no row
that decides nothing. The column is derived from the restriction the send was counted
against, so it needs no schedule and no background job; the sweep is one indexed delete
before the counters of the next send are read.

*Tests that pin it.*
`SendLedgerTests.AUTH_ABUSE_004_AC6_TheRecordIsGoneOnceItsBucketsAreEmptyAsync`,
`SendLedgerTests.AUTH_ABUSE_004_AC6_TheRecordHoldsAHashAndTimesAndNothingElseAsync`.

*Chapter text that should change.* AUTH-ABUSE-004's last sentence should say when the
record is deleted, not only that it is.

**Superseded by D-162.** Applied in entry 122.

---

## 32. A deployment that declares no message catalogue is refused at startup

**Phase 4 · 2026-09-20 · Tier 2 · AUTH-ABUSE-005 AC3, LIB-HOST-001, CONV-ERR-001**

*The question.* Every message the library sends comes from the host's catalogue. A
deployment that registers none has nothing to send, which is a fault; but a missing
registration surfaces as a container exception when the first service that needs it is
resolved, which names a type and not a key.

*The readings.*

1. Require the catalogue at registration, so the container refuses to build. The
   failure is an exception naming a .NET type, which LIB-HOST-001 AC2 does not accept.
2. Treat the catalogue as absent, and let the startup check refuse with
   `model.startup.declarationmissing` naming the first message key it could not be
   asked for.

*Chosen: 2.* LIB-HOST-001 AC2 says a missing declaration names the key. The deployment
still does not start, so nothing is loosened, and the operator is told what to write
rather than which constructor failed.

*Tests that pin it.*
`StartupValidationTests.AUTH_ABUSE_005_AC3_ADeploymentThatDeclaredNoMessagesIsRefusedAsync`.

*Chapter text that should change.* AUTH-ABUSE-005 AC3 should name the missing
declaration beside the over-budget and the untranslated message.

**Superseded by D-162.** Applied in entry 123.

---

## 33. The authentication services deferred from phase 3 are registered in this phase

**Phase 4 · 2026-09-20 · Tier 2 · LIB-HOST-001, decision 19**

*The question.* Decision 19 held the registration of the phase 3 services because every
one of them reads `IConfigurationStore`, which had no implementation. The store is built
in this phase.

*The readings.*

1. Leave the registration to phase 5, keeping phase 3's area unreachable from a host
   for another phase.
2. Register them now: policies, passwords and their screening, sessions, the factor
   services, recovery codes and the trusted browsers.

*Chosen: 2.* The reason for the deferral is gone. The leaked-password corpus is
registered as a typed client over the host's own base address, and the word list and
the hasher as singletons, which is what their construction costs make them.

*Tests that pin it.* The `Janus.Hosting.Tests` registration and startup suites, which
resolve each service from the host's container.

*Chapter text that should change.* None.

---

## 34. OPS-CFG-008 is completed here except its general audit criterion, which waits with OPS-CFG-002 and OPS-CFG-005

**Phase 4 · 2026-09-20 · Tier 3 · OPS-CFG-002, OPS-CFG-005, OPS-CFG-008**

*The question.* The plan gives the OPS-CFG items of `06` to no phase: phase 4 takes
OPS-ALERT-001 to 004a and phase 9 takes OPS-BOOT, OPS-SEC, OPS-MAINT, OPS-OBS and
OPS-ENV. The runtime settings table those items govern is built in this phase, and one
OPS-CFG-008 criterion (AC4) is about restrictions, which are this phase's subject.
OPS-CFG-002 AC3 and AC4 and OPS-CFG-005 ask for a written reason and an audit entry for
every runtime change with no direction, which includes the alert-destination change of
OPS-ALERT-004a, built here.

*The readings.*

1. Build a general configuration audit now. Every runtime change passes through
   `IConfigurationStore.WriteAsync`, so auditing there means that port takes an actor
   and a reason: a public-surface change in a phase whose chapters do not name the
   items that would justify it.
2. Complete what is restriction-shaped (OPS-CFG-008 AC4: the edit reaches the next send
   with nothing restarted, the entry carries the restriction before and after, and a
   loosening raises the Normal alert) and leave the general mechanism, with OPS-CFG-002
   AC3 and AC4 and OPS-CFG-005, to the phase that builds it.

*Chosen: 2*, with the gap stated rather than papered over. The strictest reading of the
smaller public surface rules out widening `IConfigurationStore` on the strength of items
this phase's chapter list does not carry. **What is therefore not met today:** changing
the alert destinations is stepped up (OPS-ALERT-004a, tested) but takes no written
reason and writes no audit entry, which OPS-CFG-002 AC3 and AC4 require of a change with
no direction. Nothing is loosened by the gap: the change already requires step-up and
already tells every previous destination.

*Tests that pin what is built.*
`RestrictionAdministrationTests.OPS_CFG_008_AC4_AnEditIsLiveAuditedWithBothValuesAndAlertedAsync`,
`ConfigurationStoreTests.OPS_CFG_008_AC1_AChangedSettingIsInForceForTheNextReadAsync`,
`ConfigurationStoreTests.OPS_CFG_008_AC3_NoBootstrapValueIsAKeyOfTheTable`,
`AlertDestinationChangeTests.ChangeAsync_AGateTheSessionDoesNotMeet_ChangesNothingAsync`.

*Chapter text that should change.* The plan should name the phase that builds
OPS-CFG-002, OPS-CFG-005 and the remaining OPS-CFG-008 criteria, and that phase should
add the reason and the audit entry to the alert-destination change.

**Superseded by D-162.** Applied in entry 124.

---

## 35. A given-up identifier stays reserved for the whole undo window

**Phase 5 · 2026-09-20 · Tier 2 · REG-IDENT-006**

*The question.* REG-IDENT-006 gives a removed identifier an undo window and puts the
value out of the account's reach while it runs. It does not say whether another account
may take that value in the meantime.

*The readings.*

1. Release the value at once, so the undo may find it taken.
2. Hold it against every other account until the undo window closes.

*Chosen: 2.* Fail closed: an undo the specification promises cannot be beaten to the
address. The reservation is a unique fingerprint on `identifier_removals`, and
`FindOwnerAsync` still treats the value as unknown everywhere a recovery path reads it
(REG-IDENT-006 AC3).

*Tests that pin it.*
`IdentifierServiceTests.REG_IDENT_006_AC2_TheUndoRestoresInsideTheWindowAndNotAfterAsync`,
`IdentifierServiceTests.REG_IDENT_006_AC3_TheRemovedAddressIsToldWithNoLinkAsync`.

*Chapter text that should change.* REG-IDENT-006 should say that the value is
unavailable to other accounts until the undo window ends.

**Superseded by D-162.** Applied in entry 141.

---

## 36. The username hold after erasure is its own table

**Phase 5 · 2026-09-20 · Tier 2 · REG-IDENT-009, PRIV-RIGHT-005a**

*The question.* REG-IDENT-009 holds a released username against every other account for
the cooling-off period, and holds an erased account's username for the same period. It
does not say where that hold lives once the subject key is destroyed.

*The readings.*

1. Keep the hold on the removal record, beside the other released values.
2. A table of fingerprints alone, each with the instant it is released.

*Chosen: 2.* Erasure destroys the subject key, so a removal record's held columns are
unreadable afterwards and cannot carry the hold. A username is public by nature, so a
fingerprint with a release instant keeps nothing that erasure should have taken.

*Tests that pin it.*
`IdentifierServiceTests.REG_IDENT_009_AC3_AnErasedUsernameIsHeldUntilTheRetentionElapsesAsync`,
`IdentifierServiceTests.REG_IDENT_009_AC2_ASecondChangeInsideTheWindowIsRefusedAsync`.

*Chapter text that should change.* REG-IDENT-009 should say where the hold lives after
erasure.

---

## 37. A pending identifier verification reuses the staged identity of registration

**Phase 5 · 2026-09-20 · Tier 2 · REG-IDENT-004, REG-IDENT-007, REG-SESS-003**

*The question.* An identifier added to a live account waits to be proved exactly as one
staged during registration does. No chapter says whether the live account's waiting
identifier is a second mechanism or the registration one.

*The readings.*

1. A second verification mechanism, written for an account that already exists.
2. Compose the registration one, which already draws the code and the link token and
   counts the attempts.

*Chosen: 2.* One way of doing a thing (CONV-DESIGN). The waiting record adds only what
registration has no use for: which browser asked, whether it is a replacement, and
whether the address being left has confirmed.

*Tests that pin it.*
`IdentifierServiceTests.REG_IDENT_004_AC2_AnAddedIdentifierWaitsUnverifiedAsync`,
`IdentifierServiceTests.REG_IDENT_010_AC2_ChangingAnIdentifierResetsItsVerificationAsync`,
`IdentifierServiceTests.REG_IDENT_007_AC2_WithNoOtherChannelTheOldAddressConfirmsAsync`.

*Chapter text that should change.* None; the chapters do not say which mechanism carries
it.

---

## 38. The preferred second step is stored as a mark and the order derived

**Phase 5 · 2026-09-20 · Tier 2 · IDN-ATTR-008**

*The question.* IDN-ATTR-008 gives an account a preferred second step, makes the most
recently enrolled one preferred until the person chooses, and moves the preference when
the preferred credential is removed. It does not say where the preference is held.

*The readings.*

1. Hold the preferred credential's identifier on the account and maintain it at every
   enrolment and every removal.
2. Hold a mark on the credential and derive the order from it.

*Chosen: 2.* AC1 and AC3 then need no maintenance: an enrolment on an unmarked account
is the most recent second factor and therefore preferred, and removing the marked one
moves the preference by itself. At most one mark per account is a unique filtered index
rather than a rule in code.

*Tests that pin it.*
`AccountServiceTests.IDN_ATTR_008_AC1_TheLatestSecondStepIsPreferredWhileNothingIsMarkedAsync`,
`AccountServiceTests.IDN_ATTR_008_AC2_AMethodTheAccountDoesNotHoldIsRefusedAsync`,
`AccountServiceTests.IDN_ATTR_008_AC3_RemovingThePreferredOneMovesThePreferenceAsync`.

*Chapter text that should change.* None.

---

## 39. Every error code is mapped to one status in one table

**Phase 5 · 2026-09-20 · Tier 2 · API-CONV-003, BFF-ERR-001, BFF-ERR-002**

*The question.* `09` gives a status beside each code at each endpoint. BFF-ERR-001
requires every failure to answer with the status its code carries, and does not say
whether the mapping belongs to the endpoint or to the code.

*The readings.*

1. Each endpoint names the status for each code it can answer with, as `09` writes them
   per endpoint.
2. One table from code to status, used by every endpoint.

*Chosen: 2.* `09` assigns the same status to the same code everywhere it appears, so the
first reading is the same table written many times with many chances to disagree. A code
the table does not name answers as a fault, which discloses nothing the table did not
decide, and a test asserts the table names every code the library raises.

*Tests that pin it.* `ApiStatusTests.Of_ACodeTheLibraryRaises_HasAStatusOfItsOwn`,
`ApiStatusTests.Of_EveryCodeButSessionDeath_AnswersWithSomethingOtherThan401`.

*Chapter text that should change.* `10` section 6 should say that the status is a
property of the code and not of the endpoint, and `10` section 1 should carry the status
for every row.

---

## 40. Six codes the chapters describe but do not name

**Phase 5 · 2026-09-20 · Tier 3 · BFF-ERR-001 AC3, `10` section 1**

*The question.* The library raises `identity.profile.invalid`,
`identity.profile.notaccepted`, `auth.credential.notfound`,
`auth.credential.labelinvalid`, `identity.identifier.invalid` and
`identity.identifier.locked`. Each is a refusal a chapter describes in prose (`09`
`PUT /account/profile` 422 for a display name over 64 bytes and for a field immutable to
the person; `09` `PATCH /account/credentials/{id}` 422 for a label of 0 or over 64
characters; a credential or an identifier the account does not hold), and none has a row
in `10` section 1. BFF-ERR-001 AC3 requires every code to be one `10` names, so the
criterion cannot pass as written.

*The readings.*

1. Answer those refusals with a code `10` does name, which would say something other
   than what happened.
2. Raise the code the chapter describes, and record that `10` has to gain the rows.

*Chosen: 2, the strictest reading: it keeps most.* Nothing is loosened; the refusals are
the ones the chapters describe, and the library's own catalogue documents each with its
meaning and its remediation (CONV-NAME-003).

*Tests that pin it.* `ApiStatusTests.Of_ACodeTheLibraryRaises_HasAStatusOfItsOwn`,
`ErrorCodesTests.CONV_NAME_003_AC2_ChangingACodeFailsTheContractTest`.

*Chapter text that should change.* `10` sections 1.1 and 1.2 should carry a row for each
of the six.

---

## 41. Session resolution refuses a dead session in the pipeline

**Phase 5 · 2026-09-20 · Tier 2 · BFF-ORDER-001 stage 5, API-CONV-003, BFF-ERR-003 AC3**

*The question.* Stage 5 resolves the session cookie. The chapters do not say what the
stage does when the cookie no longer resolves to a live session.

*The readings.*

1. Resolution leaves the request without a session and each endpoint decides.
2. A cookie that no longer resolves is answered 401 at the stage, once.

*Chosen: 2.* BFF-ERR-003 AC3 puts uniformity in the pipeline rather than in endpoint
discipline, and API-CONV-003 reserves 401 for exactly this. A browser that carries
nothing is not refused: what an endpoint requires of a caller stays the endpoint's. The
dead pair is cleared in the same answer so the browser stops presenting it.

*Tests that pin it.*
`BrowserProfileTests.BFF_STEP_001_AC3_AnExpiredSessionIsRefusedWithWhatMustBeRedoneAsync`,
`ApiConventionTests.API_CONV_002_AC1_NoRefusalCarriesASentenceAsync`.

*Chapter text that should change.* `17` should say that stage 5 answers session death
itself.

**Superseded by D-162.** Applied in entry 142.

---

## 42. A session records no location until a local database can resolve one

**Phase 5 · 2026-09-20 · Tier 2 · INT-GEN-006, AUTH-SESS-013**

*The question.* A session entry carries the city it was used from. INT-GEN-006 gives the
city to a local database the plan assigns to no phase.

*The readings.*

1. Hold the session list until the city database exists.
2. Record the address and the device and leave the location absent.

*Chosen: 2.* INT-GEN-006 allows the degradation and AUTH-SESS-013 makes the field
optional, so a deployment without the database shows a session list without a city
rather than no session list at all.

*Tests that pin it.*
`AccountApplicationTests.FE_ACCT_001_AC2_OneSessionIsCurrentAndAnotherIsEndedAsync`,
`SessionServiceTests.AUTH_SESS_013_AC2_EachEntryCarriesTimesDeviceAndCityAsync`.

*Chapter text that should change.* None; the plan should name the phase INT-GEN-006
belongs to.

---

## 43. The browser and the operating system are read from the user agent

**Phase 5 · 2026-09-20 · Tier 2 · AUTH-SESS-013, IDN-ATTR-006**

*The question.* A session entry names the device it was used from. No chapter says how
the browser and the operating system are established.

*The readings.*

1. A package that parses user agents.
2. A short ordered list of tokens read from the header.

*Chosen: 2.* CONV-DESIGN-008 admits no such package, and the item asks for the two coarse
facts a person recognises their own session by, not for a device profile. Anything finer
would be a fingerprint, which IDN-ATTR-006 is written against.

*Tests that pin it.*
`SessionServiceTests.AUTH_SESS_013_AC2_EachEntryCarriesTimesDeviceAndCityAsync`,
`ModelTests.IDN_ATTR_006_AC1_NoSchemaFieldHoldsCoordinates`.

*Chapter text that should change.* None.

---

## 44. The phone step is skipped through a path of its own

**Phase 5 · 2026-09-20 · Tier 2 · REG-SESS-002, `09` section 2**

*The question.* REG-SESS-002 requires step 3 to be skippable where `registration.phone`
is `optional`. `09` section 2 names no path for the skip, and the contract method
(`IRegistration.SkipPhoneAsync`) exists.

*The readings.*

1. Treat an empty `PUT /register/phone` as the skip.
2. A path that says what it does, `POST /register/phone/skip`.

*Chosen: 2.* An empty value would have to be told apart from a malformed one, which is
the oracle API-CONV-005 is written against, and a skip that leaves no staged identifier
is not a stage.

*Tests that pin it.*
`RegistrationServiceTests.REG_SESS_002_AC3_ThePhoneStepIsSkippableOnlyWhereItIsOptionalAsync`,
`RegistrationWizardTests.FE_REG_005_AC1_NoStepIsReachableBeforeItsPredecessorAsync`.

*Chapter text that should change.* The endpoint table of `09` section 2 should carry
`POST /register/phone/skip`, step 3, **204**.

---

## 45. The second-step choice names the next ceremony and is recorded nowhere

**Phase 5 · 2026-09-20 · Tier 2 · REG-SESS-006, `09` section 2**

*The question.* The security step takes a `secondStep` field beside the password. No
chapter says what the server does with the choice.

*The readings.*

1. Record the choice on the registration session and refuse an enrolment that does not
   match it.
2. Set the password and leave the choice to the frontend, which enrols through the
   WebAuthn and TOTP endpoints that accept the registration session.

*Chosen: 2.* REG-SESS-006 makes the step complete when what the policy requires is
enrolled, not when a choice is stored, and a stored choice would be a second gate on an
enrolment the policy already gates. Nothing in `10` holds the field.

*Tests that pin it.*
`RegistrationServiceTests.REG_SESS_006_AC4_ASecondStepBesideAPasswordDrawsRecoveryCodesAsync`,
`RegistrationServiceTests.FE_REG_003_AC4_ASecondStepAfterAPasswordLeavesItStandingAsync`.

*Chapter text that should change.* `09` section 2 should say that `secondStep` selects
the frontend's next screen and that the server records nothing for it.

---

## 46. A signed-in browser that asks to register is answered with the account

**Phase 5 · 2026-09-20 · Tier 2 · REG-SESS-002, API-LAND-001**

*The question.* `09` section 2 says a request to begin a registration from a browser that
already holds a session "is answered with the account landing" and creates no
registration session. It gives neither a status nor a body.

*The readings.*

1. Refuse with a code.
2. Answer **200** with the document of `GET /account`.

*Chosen: 2.* The account landing is the document the account application already has, and
a refusal would make the frontend ask a second time for what it was about to show.

*Tests that pin it.*
`RegistrationFlowTests.BeginAsync_ABrowserAlreadySignedIn_IsAnsweredWithTheAccountAsync`.

*Chapter text that should change.* `09` section 2 should give the status and the body of
that answer.

**Superseded by D-162.** Applied in entry 125.

---

## 47. Every event of the stream carries the state document

**Phase 5 · 2026-09-20 · Tier 3 · REG-SESS-003, FE-VER-001, `09` section 2**

*The question.* `09` section 2 names the stream's events with a payload in parentheses
(`{ id, kind }`, `{ step }`, `{}`), and the sentence that follows them says the data of
every event is the state document, "so polling and streaming are one shape". The two
readings are in the same paragraph.

*The readings.*

1. The payloads the parentheses give.
2. The `GET /register` state as the data of all three.

*Chosen: 2.* It is the reading the item is for: a frontend that falls back to polling then
needs no second reader. `session-ended` carries `{}` because there is no state left to
read.

*Tests that pin it.*
`RegistrationFlowTests.BFF_CSRF_005b_AC3_TheStreamAndThePollCarryTheSameStateAsync`,
`RegistrationWizardTests.FE_REG_005_AC4_TheStateIsReadBackFromTheServerAsync`.

*Chapter text that should change.* `09` section 2 should drop the parenthetical payloads
or say they are the change and not the data.

---

## 48. The stream is produced by reading the state back on an interval

**Phase 5 · 2026-09-20 · Tier 2 · REG-SESS-003, FE-VER-001, CONV-DESIGN-008**

*The question.* The waiting screen follows a stream of server-sent events. No chapter says
what produces them.

*The readings.*

1. Signal the stream in process when a step completes.
2. Read the state back every second and emit what changed.

*Chosen: 2.* The browser that presses a verification link is not promised to reach the
instance the stream is open on, so an in-process signal would strand the waiting screen
behind a load balancer. A database channel would be a dependency CONV-DESIGN-008 does not
admit. One second is short enough that a press feels immediate and long enough that a
waiting screen is not a load generator.

*Tests that pin it.*
`RegistrationFlowTests.BFF_CSRF_005b_AC3_TheStreamAndThePollCarryTheSameStateAsync`,
`RegistrationWizardTests.FE_VER_001_AC2_ThePressAdvancesWhatTheWaitingScreenReadsAsync`.

*Chapter text that should change.* None.

**Superseded by D-162.** Applied in entry 143.

---

## 49. A request the reader cannot parse is answered 400 with no body

**Phase 5 · 2026-09-20 · Tier 3 · API-CONV-002, API-CONV-003**

*The question.* API-CONV-002 AC2 requires every refusal to carry a code and a correlation
identifier. A body the reader cannot parse never reaches an endpoint, and `10` names no
code for a malformed request.

*The readings.*

1. Invent a code for a malformed request and answer the usual body.
2. Answer the status alone.

*Chosen: 2, the strictest reading: it grants least.* A code is wire vocabulary that the
reference chapter owns, and the library does not add to it. API-CONV-002 AC2 therefore
cannot be met at 400 until `10` carries a row for it; nothing is disclosed by the empty
body in the meantime.

*Tests that pin it.*
`ApiConventionTests.MapRegistration_ABodyThatDoesNotParse_AnswersTheStatusAloneAsync`.

*Chapter text that should change.* `10` should carry a code for a malformed request, or
API-CONV-002 should say that 400 carries no body.

**Superseded by D-162.** Applied in entry 126.

---

## 50. The addresses of the frontend's passkey pages are declared by the host

**Phase 5 · 2026-09-20 · Tier 2 · REG-PM-001, LIB-HOST-003**

*The question.* `/.well-known/change-password` and `/.well-known/passkey-endpoints` point
at pages of the frontend. No chapter says how the library learns their addresses.

*The readings.*

1. Derive the addresses from the configured application origin.
2. A declaration the host registers, `PasskeyAddresses`, empty by default.

*Chosen: 2.* The library knows no route of the frontend (CONV-CONTENT-001, LIB-HOST-003),
and a derived address would be a guess at a page that may not exist. A deployment that
declares none serves neither document, which is what REG-PM-001 asks for where the pages
are absent.

*Tests that pin it.*
`WellKnownTests.MapWellKnown_NoDeclaredAddresses_ServesNeitherDocumentAsync`,
`WellKnownTests.REG_PM_001_AC2_TheWellKnownDocumentsAnswerAndTheProbeDoesNotAsync`.

*Chapter text that should change.* REG-PM-001 should name the declaration the host
registers.

**Superseded by D-162.** Applied in entry 127.

---

## 51. The last-of-kind refusal is unreachable where the primary cannot be removed

**Phase 5 · 2026-09-20 · Tier 3 · REG-IDENT-006 AC1, REG-IDENT-002**

*The question.* REG-IDENT-006 AC1 refuses the removal of the last identifier of a kind
the deployment requires. REG-IDENT-002 says the primary is never removed. Where a kind
holds one identifier, that identifier is the primary, and the two items name different
refusals for the same request.

*The readings.*

1. Let the primary of a kind be removed once another verified identifier of that kind
   exists, so that `identity.identifier.lastofkind` is what refuses the last one.
2. Keep REG-IDENT-002's rule without exception, which leaves
   `identity.identifier.primary` as the answer in every case the second clause describes.

*Chosen: 2, the strictest reading: it refuses more.* `identity.identifier.lastofkind` is
raised where a kind's minimum can be unmet without a primary in the way, which is the
registration discard (REG-SESS-004 AC1). The criterion's second clause is therefore proved
there and not at `DELETE /account/identifiers/{id}`.

*Tests that pin it.*
`IdentifierServiceTests.REG_IDENT_006_AC1_RemovalNeedsStepUpAndSparesThePrimaryAsync`,
`RegistrationServiceTests.REG_SESS_004_AC1_AnUnverifiedExtraHoldsTheStepUntilItIsDroppedAsync`.

*Chapter text that should change.* REG-IDENT-006 should say that the primary refusal comes
first and that the last-of-kind refusal answers the registration discard.

---

## 52. The WebAuthn registration ceremony is carried to phase 6 with the rest of `09` section 3

**Phase 5 · 2026-09-20 · Tier 2 · REG-PM-001 AC1, `09` section 4, AUTH-FACT-012**

*The question.* `09` section 4 holds the WebAuthn registration ceremony
(`/auth/webauthn/register/*`). The plan's chapter lists give section 4 to no phase, and
phase 5 owns registration and the well-known documents.

*The readings.*

1. Section 4 belongs to phase 5, because phase 5 owns registration.
2. It belongs to phase 6 with `09` section 3, because the ceremony is a sign-in surface
   built on the factor endpoints that phase builds.

*Chosen: 2.* It keeps most: the creation options are written once, beside the assertion
options they mirror, rather than half now and half later. `/.well-known/webauthn` needs
nothing from the ceremony and is mounted now, so a deployment's allowlist is public from
phase 5 (AUTH-FACT-012). REG-PM-001 AC1 ("No user handle contains personal data") is
proved where the user handle is written, and is therefore carried to phase 6 with it.

*Tests that pin what is built.*
`WellKnownTests.REG_PM_001_AC2_TheWellKnownDocumentsAnswerAndTheProbeDoesNotAsync`.

*Chapter text that should change.* The plan should name the phase that owns `09`
section 4.

---

## 53. API-REDIR-001 has no endpoint in phase 5 to govern

**Phase 5 · 2026-09-20 · Tier 2 · API-REDIR-001, API-REDIR-002**

*The question.* The plan gives API-REDIR-001 to phase 5. API-REDIR-002 exempts
registration from the return address, and no other endpoint of this phase takes one.

*The readings.*

1. Prove the return-address rules against the registration endpoints.
2. Carry all four criteria to phase 6.

*Chosen: 2.* There is nothing in this phase for the rules to refuse. The four criteria are
proved at the sign-in and OIDC surfaces that do take a return address.

*Tests that pin what is built.*
`RegistrationServiceTests.REG_SESS_008_AC2_TheReturnIsDecidedByTheClientCapturedAtTheStartAsync`.

*Chapter text that should change.* None.

---

## 54. The phase's surface is the plan's Builds column, not the whole of `09` section 6

**Phase 5 · 2026-09-20 · Tier 2 · `09` section 6, the plan's phase 5 row**

*The question.* The phase's Chapters column names `09` sections 2 and 6. Section 6 holds
endpoints whose operations other phases build.

*The readings.*

1. The Chapters column makes every endpoint of section 6 this phase's.
2. The Builds column names what the phase builds, and an endpoint of section 6 whose
   operation another phase builds belongs to that phase.

*Chosen: 2.* The Builds column is written endpoint by endpoint ("identifier add, primary,
backup, remove with undo, replace; profile; preferences store; sessions list; credential
list and labels; well-known endpoints"), and the first reading would put half-built
surfaces in this phase: `DELETE /account/credentials/{id}` needs the suspension window of
AUTH-RECOV-007, `/upgrade` needs the ceremony of `09` section 4,
`POST /account/link/{provider}` needs a social consumer no phase builds, and
`/account/photo` needs an imaging capability no permitted package provides. Serving any of
them in part would be a "for now", which the working guide's section 4 forbids.

*Tests that pin what is built.*
`ApiConventionTests.IDN_ACCT_003_AC1_NoSurfaceTakesTwoAccountsAsync`, which reads the
endpoint table the phase mounts.

*Chapter text that should change.* The plan should say that a phase builds the endpoints
of a chapter its Builds column names and no others.

---

## 55. The photo endpoints cannot be built inside the permitted packages

**Phase 5 · 2026-09-20 · Tier 3 · IDN-ATTR-004, IDN-ATTR-002, PRIV-RIGHT-005 AC4, `09` section 6**

*The question.* IDN-ATTR-004 requires an upload to be recognised by content among JPEG,
PNG and WebP, limited by dimension, and re-encoded to JPEG at quality 85 with every
metadata segment removed. The base class library decodes and encodes no image on the
platforms this library targets (`System.Drawing.Common` is Windows-only and unsupported
elsewhere), CONV-DESIGN-008 permits no imaging package, and D-161 forbids adding one.

*The readings.*

1. Accept uploads and store the bytes unvalidated.
2. Serve no photo endpoint until the capability exists.

*Chosen: 2, the strictest reading: it refuses.* An upload the library cannot re-encode is
the exposure IDN-ATTR-004 exists to prevent, and storing the bytes would ship that
exposure under the name of the item that forbids it. `janus.profile_photos` and the
erasure that reaches it stand (IDN-ATTR-003, PRIV-RIGHT-005 AC4, phase 1); nothing writes
to them.

*Tests that pin what is built.*
`SubjectEraserTests.IDN_ATTR_003_AC1_ThePhotoIsUnreadableInTheSameTransactionAsync`,
`SubjectEraserTests.PRIV_RIGHT_005_AC4_ThePhotoIsRenderedUnreadableByTheSameOperationAsync`.

*Chapter text that should change.* CONV-DESIGN-008 needs a row for an imaging package, or
IDN-ATTR-002 to IDN-ATTR-004 need to leave the photo to the host.

**Superseded by D-162.** Applied in entry 144.

---

## 56. The preferred second step is presented by the challenge that phase 6 builds

**Phase 5 · 2026-09-20 · Tier 2 · IDN-ATTR-008 AC4**

*The question.* IDN-ATTR-008 AC4 requires the preferred method to be offered first where a
second step is asked for. Phase 5 builds no endpoint that presents a challenge.

*The readings.*

1. Order the combinations `StepUpChallenge` carries by the preference.
2. Decide AC1 to AC3 here and prove AC4 where a challenge is presented.

*Chosen: 2.* `StepUpChallenge` answers a gate with the combinations of factors that reach
it, not with a list of methods to offer, and the endpoint that presents a challenge is
`09` section 3, which phase 6 builds. Reordering combinations would put a presentation
concern inside a gate decision.

*Tests that pin what is built.*
`AccountServiceTests.IDN_ATTR_008_AC1_TheLatestSecondStepIsPreferredWhileNothingIsMarkedAsync`,
`AccountServiceTests.IDN_ATTR_008_AC3_RemovingThePreferredOneMovesThePreferenceAsync`.

*Chapter text that should change.* None.

---

## 57. Two criteria wait on an enrolment that only a live account can have

**Phase 5 · 2026-09-20 · Tier 2 · AUTH-STEP-007 AC1, AUTH-FACT-002a AC5**

*The question.* AUTH-STEP-007 AC1 requires an enrolment to be notified "on every recorded
channel that is not the enrolling session". The only enrolment phase 5 reaches is the
registration security step, where the account does not yet exist. AUTH-FACT-002a AC5 names
`POST /account/link/{provider}`.

*The readings.*

1. Prove them against the registration security step.
2. Carry them to the phase that builds enrolment on a live account.

*Chosen: 2.* At the security step there is no recorded channel and no other session, so
the criterion cannot be exercised. `MessageKind.CredentialEnrolled` and its routing stand
(phase 4); nothing raises it yet. No social consumer exists in the library at all.

*Tests that pin what is built.*
`RegistrationServiceTests.REG_SESS_006_AC4_ASecondStepBesideAPasswordDrawsRecoveryCodesAsync`,
`AlertRouterTests` and the sending suite of phase 4, which carry `CredentialEnrolled`.

*Chapter text that should change.* None.

---

## 58. The provider runs on OpenIddict in degraded mode

**Phase 6 · 2026-09-20 · Tier 2 · AUTH-OIDC-001, CONV-DESIGN-008**

*The question.* CONV-DESIGN-008 permits OpenIddict. OpenIddict ordinarily owns its own
tables for applications, authorizations and tokens, while chapter 07 already gives the
library tables for the client registry, the one-time codes, the refresh-token families
and the signing keys, and chapter 02 has `IOidc` answer from them. Nothing says which of
the two holds the rows.

*The readings.*

1. Register OpenIddict's Entity Framework stores and let it own the clients and tokens.
2. Enable degraded mode and answer every question the server would have asked a store
   with a handler of the library's own, over the library's tables.

*Chosen: 2.* Chapter 07 names the tables and OPS-MIG-001 describes one migration history;
a second store would be a second source of truth about the same client and a second
history to migrate, and a registry that disagreed with itself could admit a client the
library refuses. The protocol, the request shapes and the signatures stay the server's.

*Tests that pin what is built.*
`OidcServiceTests.AUTH_OIDC_001_AC2_AnUnregisteredClientObtainsNoCodeAsync`,
`OidcFlowTests.AUTH_SESS_012_AC5_TheExchangeIsBackChannelAndNamesTheSessionAsync`.

*Chapter text that should change.* `08` CONV-DESIGN-008 could say that OpenIddict is
taken in degraded mode and that the library holds the rows, so no later run reaches for
its stores.

**Superseded by D-162.** Applied in entry 158.

---

## 59. The server's own keys are ephemeral and protect nothing

**Phase 6 · 2026-09-20 · Tier 2 · AUTH-KEY-001, AUTH-OIDC-004**

*The question.* In degraded mode the server still refuses to start without an encryption
credential and a signing credential of its own, while every token the deployment issues
is signed with the key AUTH-KEY-001 rotates and validated against the set the library
publishes.

*The readings.*

1. Hand the server the deployment's signing key so there is one key and not two.
2. Hand it ephemeral in-process keys, use neither, and sign every token with the key read
   for the request.

*Chosen: 2.* Reading 1 would put the private key in a second component's hands and tie
rotation to a restart, which AUTH-KEY-001 AC1 forbids. The ephemeral pair is created per
process, never written anywhere and never published, and protects nothing: the access
token is signed by the library's handler, the code and the refresh token are the
library's own opaque values, and access-token encryption is off so a relying party can
validate offline (AUTH-OIDC-004 AC3).

*Tests that pin what is built.*
`SigningKeysTests.AUTH_KEY_001_AC1_RotationNeedsNoRestartAndNoPersonAsync`,
`SigningKeysTests.AUTH_KEY_001_AC4_ThePublishedSetCarriesNoPrivateMaterialAsync`,
`OidcFlowTests.AUTH_OIDC_004_AC3_AnOfflineValidatorRefusesTheTokenAtItsExpiryAsync`.

*Chapter text that should change.* None.

**Superseded by D-162.** Applied in entry 158.

---

## 60. What API-REDIR-001's configured list is at each endpoint

**Phase 6 · 2026-09-20 · Tier 2 · API-REDIR-001**

*The question.* API-REDIR-001 requires every redirect target to be matched against a
configured list and replaced by the configured default where it does not match, without
naming the list or the default for either place a redirect is decided.

*The readings.*

1. One list for the whole library, a new configuration key holding it.
2. The list is whatever the endpoint's own chapter already registers: the origins of
   `webauthn.origins` where the library lands a browser after a link, and the client's
   one registered `redirect_uri` at the authorization endpoint.

*Chosen: 2.* Chapter 10 section 4.4 already carries `webauthn.origins` as a protected
setting with at least one entry, and chapter 02 makes the registered destination the only
one a client obtains a code at. A new key would be a second list to keep in step with
both, and REF-001 requires a chapter 10 row for a key that does not exist. The match is
exact: a destination that merely contains a registered one is replaced, so no prefix and
no pattern is read.

*Tests that pin what is built.*
`OidcFlowTests.API_REDIR_001_AC1_AnUnknownDestinationIsReplacedAndLoggedAsync`,
`OidcServiceTests.API_REDIR_001_AC2_ADestinationContainingAKnownOneIsNotAcceptedAsync`.

*Chapter text that should change.* `09` API-REDIR-001 should name the list and the
default for each endpoint that redirects.

**Superseded by D-162.** Applied in entry 145.

---

## 61. Where a browser holding no session is sent

**Phase 6 · 2026-09-20 · Tier 2 · AUTH-SESS-012 AC3, LIB-HOST-003**

*The question.* AUTH-SESS-012 AC3 requires an interactive authorization request from a
browser holding no session to reach the sign-in screen. The library renders no page and
chapter 10 carries no key for a route to one.

*The readings.*

1. Assume a path, such as `/sign-in`, and redirect to it.
2. Let the host declare it, and refuse where it has declared none.

*Chosen: 2.* A path the library assumed would be an assumption about a frontend, which
section 4 of the instruction file forbids and LIB-HOST-003 settles the other way for
every other address. `AuthenticationAddresses` is registered by the host and defaults to
`None`; where it holds nothing the request is refused with `login_required` rather than
forwarded to a route that may not be there, which is the closed answer.

*Tests that pin what is built.*
`OidcFlowTests.AUTH_SESS_012_AC3_AnInteractiveRequestReachesTheSignInScreenAsync`,
`OidcFlowTests.AUTH_SESS_012_AC3_ASilentRequestWithoutASessionSaysSoAsync`.

*Chapter text that should change.* `10` section 5 should carry `AuthenticationAddresses`
beside the other host declarations, or `09` should name the address the authorization
endpoint forwards to.

**Superseded by D-162.** Applied in entry 128.

---

## 62. Which routes the machine profile governs

**Phase 6 · 2026-09-20 · Tier 2 · BFF-MACH-001 AC1**

*The question.* BFF-MACH-001 requires a machine profile that no browser endpoint can be
moved onto. Chapter 09 does not say whether the host chooses which routes it covers.

*The readings.*

1. The host names the routes when it mounts the profile.
2. The library holds a fixed list and the host mounts the profile over it.

*Chosen: 2.* A host that can name the routes can put a browser endpoint on the machine
profile, which is what AC1 forbids, so the smaller surface is also the closed one. The
list is the library's, a gate test reads it against the mounted endpoints, and the host
chooses nothing about what the profile covers.

*Tests that pin what is built.*
`BrowserProfileTests.BFF_MACH_001_AC1_NoBrowserEndpointCanBeMovedOntoTheMachineProfile`,
`OidcFlowTests.BFF_MACH_001_AC2_ACookieOnAMachineRouteIsRefusedAsync`.

*Chapter text that should change.* None.

---

## 63. A deployment that starts without key material refuses to start

**Phase 6 · 2026-09-20 · Tier 2 · AUTH-KEY-002 AC2, OPS-SEC-001**

*The question.* AUTH-KEY-002 AC2 requires startup to fail, named, where key material is
absent. Chapter 10 section 1 carries no code for it, and nothing states how short a
fingerprint key is too short.

*The readings.*

1. Throw with a sentence.
2. Fail with a code of the catalogue's own, naming which key is missing in the structured
   details, and put a floor under the fingerprint key.

*Chosen: 2.* CONV-DESIGN-005 and LIB-API-003 make a code with structured details the way
the library names anything, and a sentence would be wording decided in library code
(CONV-CONTENT-001). `model.startup.kekunavailable` joins the other `model.startup`
faults. The floor is 32 bytes because the fingerprint key computes an HMAC-SHA256 and a
key shorter than the hash is not a key of that construction; a shorter one is refused
rather than padded.

*Tests that pin what is built.*
`KeyMaterialTests.AUTH_KEY_002_AC2_StartupFailsNamedWithoutTheKeyEncryptionKey`,
`KeyMaterialTests.AUTH_KEY_002_AC2_StartupFailsNamedWithoutTheFingerprintKey`,
`KeyMaterialTests.AUTH_KEY_002_AC2_StartupFailsNamedOnAFingerprintKeyShorterThanTheHash`.

*Chapter text that should change.* `10` section 1.2 should carry
`model.startup.kekunavailable`, and `10` section 4 should state the fingerprint key's
minimum length.

---

## 64. Sessions past their absolute expiry are swept

**Phase 6 · 2026-09-20 · Tier 2 · AUTH-KEY-003 AC1**

*The question.* AUTH-KEY-003 AC1 requires what has expired to be removed rather than kept
unreadable. The codes, the refresh tokens and the retired signing keys each sweep; the
session record a token stands on is not named.

*The readings.*

1. Sweep only what AUTH-KEY-003 names.
2. Sweep the session record on its absolute expiry beside them.

*Chosen: 2.* A session past its absolute expiry can be revived by nothing and is read
again by nothing, and it carries the same account identifier the swept rows do, so
keeping it would leave exactly what AC1 says is not left. The sweep reads the absolute
expiry and never idleness, so nothing a request could still revive is taken.

*Tests that pin what is built.*
`SessionStoreTests.AUTH_KEY_003_AC1_TheSweepTakesWhatHasPassedItsAbsoluteExpiryAsync`,
`OidcStoreTests.AUTH_KEY_003_AC1_TheSweepTakesTheCodesThatHaveExpiredAsync`,
`OidcStoreTests.AUTH_KEY_003_AC1_TheSweepTakesTheRefreshTokensThatHaveExpiredAsync`.

*Chapter text that should change.* `02` AUTH-KEY-003 should name the session record among
what the sweep takes.

---

## 65. The integration container's credential is drawn per run

**Phase 6 · 2026-09-20 · Tier 2 · CONV-GATE-002, OPS-SEC-001**

*The question.* The containerised jobs need a database password. A literal in the workflow
is a secret in the repository, which OPS-SEC-001 and the secret-scanning gate both stand
against, and the container accepts connections from nothing but the job that starts it.

*The readings.*

1. A repository secret, added and held by the owner.
2. A value drawn from the run itself, unique per run and per attempt.

*Chosen: 2.* A repository secret would be one more thing the owner maintains for a
database that lives for the length of one job. The run identifier and the attempt number
are unique per run, are a credential to nothing else, and leave nothing to rotate.
Reading 1 stays available if the owner would rather hold it.

*Tests that pin what is built.* None; the workflow is the gate of record.

*Chapter text that should change.* None.

---

## 66. The browser half of BFF-SESS-006 is the deployment's

**Phase 6 · 2026-09-20 · Tier 3 · BFF-SESS-006**

*The question.* BFF-SESS-006 describes a second browser application re-establishing its
session silently against the provider. A backend-for-frontend that does so is an OpenID
Connect client: it needs a client identifier, a secret, and somewhere to keep them.
LIB-HOST-001 names no such declaration, chapter 10 section 4.9 carries no key for one,
and `oidc_clients.secret` holds only what a secret hashes to, so the library could not
read one back even if it held it.

*The readings.*

1. The library ships both halves, adding a client identifier and a secret to what a host
   declares.
2. The library ships the provider side, and the deployment's own backend-for-frontend is
   the client.

*Chosen: 2, the strictest reading.* Adding a secret to the host's declarations would put a
live credential in configuration that chapter 10 does not carry and AUTH-KEY-002 AC1 says
configuration does not hold, and would widen the public surface for a component the
chapters place outside the library. What the library owes BFF-SESS-006 is the silent
re-establishment itself, which it answers: a second application with a live session at the
provider obtains a code without interaction.

*Tests that pin what is built.*
`OidcFlowTests.BFF_SESS_003_AC2_ASecondApplicationReEstablishesSilentlyAsync`,
`OidcServiceTests.AUTH_SESS_012_AC2_ALiveSessionIssuesACodeWithoutInteractionAsync`.

*Chapter text that should change.* `05` BFF-SESS-006 should say which side of the exchange
the library ships.

**Superseded by D-162.** Applied in entry 163.

---

## 67. The restricted channel is a catalogue property

**Phase 6 · 2026-09-20 · Tier 2 · AUTH-FACT-002b AC5**

*The question.* AUTH-FACT-002b AC5 requires the entries a text carries to be marked as
restricted wherever they are offered. Chapter 09's shapes carry factors as bare catalogue
identifiers in the enrolment list, the credential list and the challenge, so there is no
field to put a marker in without adding one to three payloads.

*The readings.*

1. Add a marked field to each of the three shapes.
2. Carry it as a property of the catalogue entry, which every rule already reads.

*Chosen: 2.* Chapter 09 is authoritative for the shapes and gives none of the three a
marker field; AUTH-FACT-001 says every rule reads a factor's properties and never its
name, and `FactorProperties` is where the library states what an entry is. One property
marks the entry once, wherever it is offered, and the frontend writes the sentence
(CONV-CONTENT-001).

*Tests that pin what is built.*
`FactorCatalogueTests.AUTH_FACT_002b_AC5_TheRestrictedEntriesAreTheOnesCarriedByText`.

*Chapter text that should change.* `09` should say how the marker reaches the frontend, or
`02` should say that it is a property of the entry.

---

## 68. What is known about a number is considered and recorded, not refused on

**Phase 6 · 2026-09-20 · Tier 2 · AUTH-FACT-002b AC6**

*The question.* AUTH-FACT-002b AC6 requires what the deployment can learn about a number
to be considered before a restricted entry is used. Neither chapter 02, nor chapter 13's
R-A18, nor chapter 10 describes a refusal or names a code for one.

*The readings.*

1. Refuse the send where the host's callback answers `risk`.
2. Ask, record what was answered, and let the send go.

*Chosen: 2.* A refusal needs a code from chapter 10 section 1 and a status in chapter 09,
and neither exists; inventing both would decide behaviour no chapter describes. The signal
is asked for once the restrictions have let the send through and before a transport takes
it, and the answer is written to the audit trail with the factor and the account, never
the number. A deployment that declares no provider is itself recorded, so the trail says
the question was asked and unanswered rather than saying nothing.

*Tests that pin what is built.*
`SendingServiceTests.AUTH_FACT_002b_AC6_TheSignalIsConsideredBeforeARestrictedFactorGoesAsync`,
`SendingServiceTests.AUTH_FACT_002b_AC6_AnAbsentProviderIsItselfRecordedAsync`,
`SendingServiceTests.AUTH_FACT_002b_AC6_NothingIsConsideredForAMessageThatIsNoFactorAsync`.

*Chapter text that should change.* `02` AUTH-FACT-002b AC6 should say what a deployment
does with the answer, and `10` should carry a code if a refusal is meant.

**Superseded by D-162.** Applied in entry 146.

---

## 69. The upgrade refuses with a code of its own

**Phase 6 · 2026-09-20 · Tier 2 · AUTH-FACT-002b AC3**

*The question.* Chapter 09 gives the credential upgrade a 409 for a credential that is not
a second-factor security key. Chapter 10 section 1.2 carries no code that maps to 409 on
that endpoint, and `auth.factor.rejected` maps to 422.

*The readings.*

1. Answer `auth.factor.rejected` and accept a 422 where the chapter says 409.
2. Add `auth.credential.notupgradable`, mapped to 409.

*Chosen: 2.* Chapter 09 is authoritative for status codes and says 409, so the code that
produces it has to exist; REF-001 requires the chapter 10 row, which the owner adds. A
credential of another account still answers `auth.credential.notfound`, so the new code
says only that a credential the account holds is not one an upgrade applies to, and tells
nobody whose a credential is.

*Tests that pin what is built.*
`CredentialServiceTests.AUTH_FACT_002b_AC3_OnlyASecurityKeyIsUpgradedAsync`,
`WebAuthnServiceTests.UpgradeAsync_ACredentialOfAnotherAccount_IsRefusedAsync`.

*Chapter text that should change.* `10` section 1.2 should carry
`auth.credential.notupgradable` with its 409.

---

## 70. Nothing carries "shown" or "exported" to the library

**Phase 6 · 2026-09-20 · Tier 2 · AUTH-RECOV-006 AC2**

*The question.* AUTH-RECOV-006 AC2 requires the account to record whether a set of recovery
codes was shown and whether it was copied, downloaded or printed. No endpoint in chapter
09 carries either fact to the library, so the operation exists and nothing in production
can reach it.

*The readings.*

1. Add an endpoint.
2. Build the operation, test it directly, and report that the route is missing.

*Chosen: 2.* Adding a route would be deciding chapter 09's surface, which section 3 of the
instruction file puts outside a run. The service records both facts, each way of taking
the codes away sets the export, and the storage column and the audit entry exist, so the
owner adds one route and nothing else changes.

*Tests that pin what is built.*
`RecoveryCodeServiceTests.AUTH_RECOV_006_AC2_EachWayOfTakingTheCodesAwaySetsTheExportAsync`.

*Chapter text that should change.* `09` section 6 should carry a route that records that
the set was shown and whether it was exported.

---

## 71. The other half of AUTH-OIDC-001 AC4 belongs to later phases

**Phase 6 · 2026-09-20 · Tier 2 · AUTH-OIDC-001 AC4**

*The question.* AUTH-OIDC-001 AC4 requires a protocol client's token to be the signed-in
person's and never a shared identity. Two of the ways a deployment could come by a shared
one are the bootstrap registration of OPS-BOOT, which phase 9 builds, and the application
password of chapter 06, which phase 8 builds.

*The readings.*

1. Build enough of both here to prove the criterion end to end.
2. Prove the criterion where the provider decides it, and leave the two later surfaces to
   their phases.

*Chosen: 2.* The criterion is decided at the token endpoint: the subject a token carries is
the session's, the client authenticates as itself and obtains nothing on anyone's behalf,
and no grant type the server admits mints a token without a session. Building part of a
later phase's surface here would put it outside the phase, which section 3 forbids.

*Tests that pin what is built.*
`OidcFlowTests.AUTH_OIDC_001_AC4_TheProtocolClientsTokenIsTheSignedInPersonsAsync`,
`OidcFlowTests.AUTH_OIDC_001_AC3_NoDynamicRegistrationEndpointExistsAsync`.

*Chapter text that should change.* None.

---

## 72. BFF-MACH-001's break-glass and provider callbacks reach past this phase

**Phase 6 · 2026-09-20 · Tier 2 · BFF-MACH-001 AC2, AC3**

*The question.* BFF-MACH-001 AC2 excepts the break-glass path from the machine profile's
refusal of cookies, and AC3 has the profile authenticate the caller by the deployment's
own means. Break-glass is phase 9's, and the application-password callbacks are phase 8's.

*The readings.*

1. Build both now against surfaces that do not exist yet.
2. Build the profile and what it governs, prove the refusal, and leave the exception and
   the callbacks to the phases that build what they stand on.

*Chosen: 2.* The profile refuses a cookie on every machine route it governs, which is what
AC2 states for everything this phase mounts; the exception has nothing to except until
break-glass exists. AC3 is proved at the token endpoint, where the client authenticates
with its registered secret, and widens when phase 8 adds application passwords.

*Tests that pin what is built.*
`OidcFlowTests.BFF_MACH_001_AC2_ACookieOnAMachineRouteIsRefusedAsync`,
`OidcFlowTests.BFF_MACH_001_AC3_TheTokenEndpointAuthenticatesTheClientAsync`.

*Chapter text that should change.* None.

---

## 73. Two contracts reported absence with a null

**Phase 6 · 2026-09-20 · Tier 2 · CONV-DESIGN-005 AC2, AUTH-RECOV-007**

*The question.* `IOidc.FindClientAsync` returned the client or null, and
`ICredentials.RemoveAsync` returned a loss report or null where the removal opened no
window. CONV-DESIGN-005 says a service method never returns null for "not found" and AC2
admits no null-returning lookup on a contract, while the contract test reads the return
type of every contract method.

*The readings.*

1. Narrow the test to lookups, so a null that means something else is admitted.
2. Keep the test as it stands and give both contracts an outcome that carries the answer.

*Chosen: 2.* Narrowing a test to make code pass is the one thing section 4 of the
instruction file rules out, and a lookup is not a distinction a test can draw. The registry
answers `authz.denied` where it holds no client, which is what the issuing path already
answered for the same condition. The removal answers nothing where the credential is gone
and `auth.credential.lastsecondfactor` carrying `invalidatesAt` where the window was
opened, which is what chapter 09 gives the endpoint: a 202 with that code and a body
carrying `invalidatesAt`. The code was in chapter 10 and mapped to 202 in the answer
table, and nothing produced it until now.

*Tests that pin what is built.*
`ResultContractTests.CONV_DESIGN_005_AC1_EveryContractMethodReturnsAnOutcome`,
`ResultContractTests.CONV_DESIGN_005_AC2_NoContractReturnsNullForNotFound`,
`CredentialServiceTests.AUTH_RECOV_007_AC5_RemovingTheLastSecondStepRunsTheWindowAsync`.

*Chapter text that should change.* `10` section 1.2's note on
`auth.credential.lastsecondfactor` reads as a status; it could say that the code travels
on the failure branch and carries `invalidatesAt`.

---

## 74. API-REDIR-002 is built in the phase that first can

**Phase 6 · 2026-09-20 · Tier 2 · API-REDIR-002, REG-SESS-008 AC2**

*The question.* API-REDIR-002 governs registration, which phase 5 built, and requires the
client identifier to be resolved against the registry at capture and the return to be
resolved from that stored reference. The registry did not exist in phase 5, and chapter
09 section 11 is in no phase's chapter list.

*The readings.*

1. Leave it to whichever later phase claims it.
2. Build it here, the first phase in which the registry it needs exists.

*Chosen: 2.* No later phase of Milestone 1 owns registration or the registry, and the
exit gate asks for every criterion of chapters 01 to 10, 17 and 20. The session already
captured the identifier at step 1; what this adds is the resolution against the registry
there, and the address the registry holds for that client carried on what the completion
returns. Chapter 09 gives the completion a bare 201, so nothing about the answer at the
boundary changes.

Where the registry holds no such client the session stores the empty reference and the
completion carries no address: the library declares no default of its own, exactly as it
declares no sign-in address (entry 61), and the deployment's default applies. The
registration is not refused either way, which is what AC2 asks.

*Tests that pin what is built.*
`RegistrationServiceTests.API_REDIR_002_AC1_TheIdentifierIsResolvedWhereItIsCapturedAsync`,
`RegistrationServiceTests.API_REDIR_002_AC2_AnUnrecognisedIdentifierIsTheDefaultAndNoRefusalAsync`,
`RegistrationServiceTests.API_REDIR_002_AC3_NoLaterStepTakesADestination`,
`RegistrationServiceTests.REG_SESS_008_AC2_TheReturnIsDecidedByTheClientCapturedAtTheStartAsync`.

*Chapter text that should change.* `09` section 2 should say what the completion carries
the return in, and `10` should name the default a deployment falls back to, or state that
it is the host's.

---

## 75. A user handle is proved absent rather than made safe

**Phase 6 · 2026-09-20 · Tier 2 · REG-PM-001 AC1**

*The question.* REG-PM-001 AC1 requires that no user handle contain personal data. The
library issues the ceremony a credential is created under and holds no user handle at
all: a credential is found again by its identifier, and the ceremony carries the relying
party, the algorithms, whether the credential is discoverable, and the challenge.

*The readings.*

1. Introduce a user handle the library derives, so there is something to prove safe.
2. Prove that the library issues none, which is what makes the criterion hold.

*Chosen: 2.* Adding a handle would add a public field, a stored column and a second way
to find a credential, none of which any chapter asks for. The ceremony is not a function
of the account at all: the operation that opens it takes no subject, so nothing of the
person can reach it.

*Tests that pin what is built.*
`WebAuthnServiceTests.REG_PM_001_AC1_NoCeremonyCarriesAUserHandleAtAllAsync`.

*Chapter text that should change.* `20` REG-PM-001 AC1 could say that a library issuing
no user handle satisfies it, since a frontend that builds the browser's request decides
what goes in that field.

**Superseded by D-162.** Applied in entry 129.

---

## 76. A prefix moves the provider's endpoints with the rest

**Phase 6 · 2026-09-20 · Tier 2 · API-CONV-001 AC2, LIB-HOST-003 AC2**

*The question.* API-CONV-001 requires the discovery document to reflect the configured
prefix. The provider's endpoints are answered by the server's own middleware against the
request path, not by the routes the library maps, so a host that mounts the library's
endpoints under a route group leaves the provider at the site root and the document with
it.

*The readings.*

1. Map the provider's endpoints among the library's, so a route group moves them.
2. The prefix a host mounts under is a path base, which moves every path the library
   answers, the provider's with them.

*Chosen: 2.* The library writes no absolute path anywhere: every address the document
publishes is built from the request, so under a path base the document reflects the
prefix with no code change, which is what both criteria ask. Reading 1 would mean holding
the provider's routes twice, in the server's configuration and in the library's map, and
the two could disagree. The two documents of REG-PM-001 stay at the site root, where the
standard puts them.

*Tests that pin what is built.*
`OidcFlowTests.API_CONV_001_AC2_TheDocumentCarriesThePrefixTheHostMountedUnderAsync`,
`ApiConventionTests.API_CONV_001_AC1_TheHostMountsTheLibraryWhereItLikesAsync`.

*Chapter text that should change.* `07` LIB-HOST-003 should say that the prefix is the
path base the host mounts under, so a host does not reach for a route group and leave
the provider behind.

---

## 77. A purpose declares the data and subject categories it requires

**Phase 7 · 2026-09-20 · Tier 2 · PRIV-PRIN-001, PRIV-ROPA-001, AUTHZ-MODEL-003**

*The question.* PRIV-PRIN-001 AC1 requires each purpose to name the categories of data
and of subject it requires, and AC2 requires a field held by no declared purpose to fail
validation. Chapter 03's declaration surface carries a purpose's name, basis and
assessment, and nothing that names a category. Nothing in `10` lists a category type
either, so the categories are either a new declared thing or a reading of something that
already exists.

*The readings.*

1. Read the categories off the resource type: the type a purpose is declared on is the
   data, and the subject is whoever the type belongs to. Nothing new is declared.
2. Let a purpose declare its own lists of data and subject categories, which the RoPA
   then prints and which validation reads.

*Chosen: 2.* PRIV-ROPA-001 requires the record of processing to carry the categories per
purpose, and a purpose is declared once and gathered from every type that names it, so
reading 1 would give one purpose as many category sets as it has types and no way to
print the one the register asks for. AC1 says the purpose names them, not the type.
AC2 is satisfied by the refusal that already exists: a resource type declared with no
purpose fails startup (AUTHZ-MODEL-003 AC1), and a field belongs to a type, so a field
held by no declared purpose is a type held by no declared purpose.

*Tests that pin what is built.*
`ProcessingTests.PRIV_PRIN_001_AC1_EachPurposeNamesTheCategoriesItRequires`,
`ProcessingTests.PRIV_PRIN_001_AC2_AFieldHeldByNoDeclaredPurposeFailsValidation`.

*Chapter text that should change.* `03`'s purpose declaration should carry the two
category lists, and `10` should list them with the rest of the declaration surface.
PRIV-PRIN-001 AC2 should say that the refusal is the type-level one AUTHZ-MODEL-003
already states.

---

## 78. The capture path is derived from the basis and the sensitivity

**Phase 7 · 2026-09-20 · Tier 2 · PRIV-BASIS-003, PRIV-SENS-002, AUTHZ-MODEL-003 AC2**

*The question.* PRIV-BASIS-003 forbids the ordinary consent path over sensitive data.
Whether a purpose captures consent the ordinary way or the written way is therefore a
consequence of two things the host already declares, the purpose's basis and the
sensitivity of the type it is declared on, but a host could also state the path itself,
and the two could then disagree.

*The readings.*

1. The host declares the path, and a declaration that states the ordinary path over
   sensitive data fails startup.
2. The library derives the path, and a declaration may only tighten it: a host may ask
   for the written path where the ordinary one would do, never the reverse.

*Chosen: 2.* AUTHZ-MODEL-003 AC2 requires that declaring a type sensitive changes what
its consent-based purposes ask for with nothing else edited, which reading 1 does not
give: it would leave the two statements to be kept in step by hand. Fail closed on the
disagreement, so the derived path is a floor a host may raise and cannot lower. The
startup refusal PRIV-BASIS-003 asks for still exists, for the declaration that tries to
lower it.

*Tests that pin what is built.*
`ProcessingTests.PRIV_BASIS_003_AC1_TheOrdinaryPathOverSensitiveDataFailsValidation`,
`ProcessingTests.AUTHZ_MODEL_003_AC2_DeclaringATypeSensitiveChangesWhatItsConsentAsksFor`.

*Chapter text that should change.* PRIV-BASIS-003 should say that the path is derived
and that a declaration may only tighten it, so a reader does not look for a path field
in `10`.

---

## 79. What the deployment processes is read from Core

**Phase 7 · 2026-09-20 · Tier 2 · CONV-LAYOUT-001, CONV-LAYOUT-002, LIB-API-001**

*The question.* The declared purposes are read by the authorization model, which
validates them, and by the privacy area, which records consent against them. Neither
project may reference the other (CONV-LAYOUT-001), so the type that carries them lives
in one of the two areas and is unreachable from the other, or in Core.

*The readings.*

1. Each area holds its own reading of the declaration, the authorization side for
   validation and the privacy side for the consent kind.
2. `DeclaredProcessing`, `DeclaredPurpose` and `ConsentKind` are Core types, read once
   from the declaration and shared.

*Chosen: 2.* Reading 1 is two implementations of one rule, and the rule is the derivation
of 78: the two would drift and the drift would be a purpose that validates one way and
records another. Core is where the contract types are (LIB-API-001), a purpose is part of
what the host declares, and the privacy dashboard has to print the purposes to the
subject, so they are public either way.

*Tests that pin what is built.*
`ProcessingTests.PRIV_BASIS_001_AC3_APurposeWithoutABasisIsRefusedWhereItIsDeclared`,
`ConsentTests.PRIV_SENS_002a_AC4_APurposeOnAnotherBasisTakesNoConsentAsync`.

*Chapter text that should change.* `10` section 5 should list `DeclaredProcessing`,
`DeclaredPurpose` and `ConsentKind` among the public types, and `07` LIB-API-001 should
name them.

---

## 80. The privacy area raises its alerts through its own port

**Phase 7 · 2026-09-20 · Tier 2 · CONV-DESIGN-003, CONV-LAYOUT-001, OPS-ALERT-001**

*The question.* PRIV-CONS-006 requires the missing-governing-text condition to be raised
on OPS-ALERT-001. The type that builds an alert is internal to the authentication area,
which the privacy area may not reference.

*The readings.*

1. Move the alert builder to Core so every area can raise one.
2. The privacy area declares `IPrivacyAlerts`, its own port, and the hosting project
   implements it over the builder that exists.

*Chosen: 2.* CONV-DESIGN-003 says each area declares the ports it needs and the outer
projects implement them, which is what every other cross-area need in this repository
already does. Reading 1 would widen the public surface for an internal concern.

*Tests that pin what is built.*
`LegalDocumentTests.PRIV_CONS_006_AC3_AVersionWithoutGoverningTextIsRefusedAndRaisedAsync`.

*Chapter text that should change.* None. `08` already settles this; the entry records
that the port was added rather than the builder moved.

---

## 81. A document version is numbered by how many came before it

**Phase 7 · 2026-09-20 · Tier 2 · PRIV-CONS-005, PRIV-CONS-006**

*The question.* A consent record names the version of the notice that was displayed
(PRIV-CONS-001 AC2) and a version is addressable afterwards (PRIV-CONS-006 AC2), so a
version needs an identity. No chapter says what it looks like and `10` carries no
default for it.

*The readings.*

1. The publisher names the version, and the library refuses a name already used.
2. The library numbers it: the count of versions of that document plus one, rendered
   decimal.

*Chosen: 2.* Reading 1 puts a user-facing string in the host's hands and then has the
library compare strings to decide which is current, which PRIV-CONS-006 AC1 needs to be
unambiguous. The ordinal is total, is decided by the library, and answers which version
is current without parsing. The wire carries it as a string, so a host that later wants
its own naming does not break the shape.

*Tests that pin what is built.*
`LegalDocumentTests.PRIV_CONS_006_AC1_ChangingTheGoverningTextCreatesANewVersionAsync`,
`LegalDocumentTests.PRIV_CONS_006_AC1_CorrectingATranslationCreatesNoVersionAsync`.

*Chapter text that should change.* PRIV-CONS-005 should say that the library numbers
versions and that the number is the count of publications of that document.

---

## 82. Supersession is keyed to the privacy notice

**Phase 7 · 2026-09-20 · Tier 2 · PRIV-CONS-001 AC2, PRIV-CONS-007**

*The question.* PRIV-CONS-007 says a material change supersedes every live consent on
the purposes the document covers (`09` section 8a). Nothing declares which purposes a
document covers, and no chapter gives a place to declare it.

*The readings.*

1. Add a declaration: a document names the purposes it covers, and a material revision
   supersedes the consents on those.
2. A consent record names the version of the **privacy notice**, in PRIV-CONS-001 AC2's
   own words, so a material revision of the privacy notice supersedes every live consent
   given against an earlier version of it, and a revision of any other document
   supersedes none.

*Chosen: 2.* PRIV-CONS-001 AC2 names the notice and nothing else, so the record already
carries the one document that governs it and reading 1 would add a second, overlapping
statement of the same thing. The smaller surface: no new declaration, no new public type.
A deployment that wants a consent text to govern a purpose publishes it and revises the
notice that points at it.

*Tests that pin what is built.*
`SupersessionTests.PRIV_CONS_007_AC1_AMaterialChangeIdentifiesWhoMustBeAskedAgainAsync`,
`SupersessionTests.PRIV_CONS_007_AC2_AMaterialRevisionOfAnotherDocumentEndsNoConsentAsync`,
`ConsentStoreTests.PRIV_CONS_007_AC1_OnlyLiveConsentsAgainstAnEarlierVersionAreFoundAsync`.

*Chapter text that should change.* `09` section 8a should say that `material` supersedes
the consents given against an earlier version of the privacy notice, and PRIV-CONS-007
should drop the phrase naming the purposes the document covers.

**Superseded by D-162.** Applied in entry 147.

---

## 83. A refusal `10` gives no code for is refused as denied

**Phase 7 · 2026-09-20 · Tier 3 · REF-001, `10` section 1.4**

*The question.* Three refusals in this phase have no code in `10` section 1.4: reading a
document that was never published, granting or withdrawing consent for a purpose that is
undeclared or rests on another basis, and granting consent when no privacy notice has
been published. Each is a real refusal the library has to make.

*The readings.*

1. Add a code to the catalogue for each and implement it.
2. Refuse with `privacy.denied`, the general refusal, and record that `10` is missing
   three rows.

*Chosen: 2, the strictest reading.* REF-001 makes `10` authoritative for the error
catalogue, and adding a code without changing `10` would put a code on the wire that the
specification does not carry, which a frontend cannot write words for (CONV-CONTENT-001).
Refusing is what both readings agree on; only the name differs, so the general code
grants least and keeps most. The owner adds the rows and the refusals take their names.

*Tests that pin what is built.*
`ConsentTests.PRIV_SENS_002a_AC4_APurposeOnAnotherBasisTakesNoConsentAsync`,
`LegalDocumentEndpointTests.PRIV_CONS_005_AC1_AnUnpublishedDocumentIsRefusedAsync`,
`ConsentEndpointTests.PRIV_CONS_008a_AC3_APurposeOnAnotherBasisTakesNoConsentAsync`.

*Chapter text that should change.* `10` section 1.4 needs three rows: a document version
that does not exist, a purpose that is not the subject's to consent to, and a consent
asked for before any notice was published.

**Superseded by D-162.** Applied in entry 130.

---

## 84. A consent record carries when it was superseded

**Phase 7 · 2026-09-20 · Tier 2 · PRIV-CONS-007 AC4, `09` section 7**

*The question.* `09` section 7 lists a consent record on the wire as `{ purpose,
noticeVersion, mechanism, grantedAt, withdrawnAt }`. PRIV-CONS-007 AC4 requires a
superseded consent to prompt rather than block, which the dashboard cannot do unless it
can tell a superseded consent from a live one.

*The readings.*

1. Keep the listed shape and let the frontend compare the record's `noticeVersion`
   against the current notice.
2. Carry `supersededAt` beside `withdrawnAt`.

*Chosen: 2.* Reading 1 makes every screen re-derive a decision the library already took
and wrote down, and it is wrong whenever a revision was published and called immaterial:
the versions differ and the consent stands. The field is the answer, not a hint.

*Tests that pin what is built.*
`SupersessionTests.PRIV_CONS_007_AC4_ASupersededConsentPromptsRatherThanWithdrawsAsync`,
`ConsentEndpointTests.PRIV_CONS_011_AC1_EveryConsentHeldIsVisibleToItsSubjectAsync`.

*Chapter text that should change.* `09` section 7 should list `supersededAt` in the
consent record.

---

## 85. The consent event names which way the consent changed

**Phase 7 · 2026-09-20 · Tier 2 · PRIV-CONS-008, `10` section 5**

*The question.* `ConsentChanged` is raised on a grant, on a withdrawal and on a
supersession. A subscriber that erases the data held solely for a purpose (PRIV-CONS-008
AC4) must act on one of the three and not the others, and `10` section 5 lists no type
that distinguishes them.

*The readings.*

1. Raise a separate event for each of the three.
2. Carry a `ConsentChange` on the one event: granted, withdrawn, superseded.

*Chosen: 2.* PRIV-CONS-008 names one event, `ConsentChanged`, as what handlers subscribe
to, so three events would break the item's own text. One enum of three values is the
smaller surface and the one a handler can switch on.

*Tests that pin what is built.*
`ConsentTests.PRIV_CONS_008_AC4_WithdrawalAnnouncesTheChangeForThePurposeAsync`,
`SupersessionTests.PRIV_CONS_007_AC4_ASupersededConsentPromptsRatherThanWithdrawsAsync`.

*Chapter text that should change.* `10` section 5 should list `ConsentChange` and the
`ConsentChanged` payload that carries it.

---

## 86. Privacy records are audited under the security category

**Phase 7 · 2026-09-20 · Tier 2 · CONV-LOG-002, `10` section 5**

*The question.* Every consent, objection and publication is written to the audit trail.
`AuditCategory` has no privacy value and `10` lists none.

*The readings.*

1. Add a `Privacy` category.
2. Write them under `Security`, the category the existing gated administrative actions
   use.

*Chosen: 2.* Adding a value to a public enum `10` fixes is a change to the reference, and
the actions are already distinguishable by their action codes
(`privacy.consent.granted` and the rest), which is what a reader filters on. The smaller
surface.

*Tests that pin what is built.*
`ConsentTests.PRIV_CONS_001_AC1_EveryChangeIsAuditedByCodeAsync`,
`LegalDocumentTests.PRIV_CONS_007_AC1_TheAuditRecordCarriesTheAnswerOnMaterialityAsync`.

*Chapter text that should change.* `10` should either add a `privacy` audit category or
say that privacy actions are recorded under `security`.

---

## 87. The dashboard records the mechanism it is

**Phase 7 · 2026-09-20 · Tier 2 · PRIV-CONS-001 AC1, PRIV-CONS-007, `10` section 5.21**

*The question.* `09` section 7 gives the grant and objection endpoints no body, so the
mechanism written on the record is the library's to choose. One of the four values,
`reconsent`, is the prompt raised after a material revision, and the same endpoint serves
it.

*The readings.*

1. Derive it: where the subject holds a superseded consent for that purpose, the grant
   that follows is re-consent.
2. Record `dashboard`, which `10` section 5.21 defines as the subject's own privacy
   pages, and leave `reconsent` to the in-process contract, which takes the mechanism
   from its caller.

*Chosen: 2.* Reading 1 has the library infer which screen the person was on from state in
its own store, and a host may raise its re-consent prompt anywhere or not at all. Which
surface asked is the frontend's fact, and the record should carry what it is told, not a
guess. A host that raises the prompt calls the contract and names `reconsent`.

*Tests that pin what is built.*
`ConsentEndpointTests.PRIV_CONS_011_AC1_EveryConsentHeldIsVisibleToItsSubjectAsync`,
`ConsentTests.PRIV_CONS_001_AC1_TheRecordCarriesTheMechanismAsync`.

*Chapter text that should change.* `09` section 7 should say that the dashboard endpoints
record the `dashboard` mechanism and that `reconsent` is written by a host calling the
contract.

**Superseded by D-162.** Applied in entry 148.

---

## 88. An action is bound to the purpose it is done for

**Phase 7 · 2026-09-20 · Tier 2 · PRIV-SENS-002, PRIV-SENS-002a, AUTHZ-GATE-005, D-160**

*The question.* PRIV-SENS-002 AC1 refuses processing of a sensitive type for a
consent-based purpose without recorded consent, and PRIV-SENS-002a requires the refusal
to reach the purpose and not the record. The gate evaluates a permission, and nothing
tells it which purpose a permission is exercised for.

*The readings.*

1. Add a purpose to the gate's own calls, so every caller states what it is doing.
2. Refuse the action wherever the type carries any consent-based purpose the subject
   has not consented to.
3. The model builder binds an action to the purpose it serves, as D-160 already binds
   an action to a step-up gate, and the gate reads the purpose from the permission.

*Chosen: 3.* Reading 2 is the record gating D-066 replaced: a customer who never wanted
recommendations would stop their own order. Reading 1 changes the gate's four public
signatures for a fact that is fixed at declaration time and never varies per call, and
phase 2 settled those signatures. Reading 3 is the pattern D-160 set for the `stepup`
residual, word for word: the residual comes from what the model binds to the action and
not from the permission string. A binding to a purpose no type declares fails startup,
so the gate never asks about a consent nobody could give.

*Tests that pin what is built.*
`ConsentGateTests.PRIV_SENS_002_AC1_AConsentBasedPurposeWithoutAConsentIsRefusedAsync`,
`ConsentGateTests.PRIV_SENS_002a_AC1_AnotherPurposeOnTheSameRecordIsUntouchedAsync`,
`ConsentGateTests.AUTHZ_GATE_005_AC3_ACapabilityCarriesTheConsentItStillRequiresAsync`.

*Chapter text that should change.* `03` should carry the binding beside the step-up one
of AUTHZ-GATE-005, and `10` should list it with the rest of the declaration surface.

---

## 89. The consent the gate reads is the caller's own

**Phase 7 · 2026-09-20 · Tier 3 · PRIV-SENS-002 AC1, AUTHZ-GATE-005, `10` section 5.20**

*The question.* A consent belongs to a data subject. The gate evaluates a caller. Where
staff act on a customer's record, the two differ, and PRIV-SENS-002 AC1 does not say
whose consent is read.

*The readings.*

1. The record's data subject, resolved from the record the action is on.
2. The caller, as every other residual of `10` section 5.20 is about the caller.

*Chosen: 2, the strictest reading of what is settled.* `10` section 5.20 lists `consent`
beside `stepup`, `reauthenticate`, `restricted` and `accountstate`, and each of those
four is a fact about the caller's own session or account; reading the fifth differently
would make one member of a closed set mean something else. Reading 1 also needs a data
subject on a record, which nothing in `03` declares: the library knows a record's
organization and never its subject. The narrower reading refuses the self-service case,
which is the case PRIV-CONS-011's dashboard is about, and leaves the staff case to the
host, which knows whose record it is. Background work asking as a system principal holds
no account and therefore no consent, so the binding asks nothing of it; the grant it
needs is refused on its own terms (AUTHZ-PRIN-003).

*Tests that pin what is built.*
`ConsentGateTests.PRIV_SENS_002_AC1_AWrittenConsentAdmitsTheActionAsync`,
`ConsentGateTests.PRIV_SENS_002a_AC2_WithdrawingStopsThePurposeOnTheNextRequestAsync`.

*Chapter text that should change.* PRIV-SENS-002 AC1 should say whose consent is read,
and `03` should say what a host does where the caller is not the subject.

**Superseded by D-162.** Applied in entry 133.

---

## 90. A consent control for a purpose taking no consent refuses the terms step

**Phase 7 · 2026-09-20 · Tier 2 · REG-SESS-007 AC1, PRIV-CONS-001, PRIV-CONS-008a**

*The question.* The terms step takes one boolean per consent control. REG-SESS-007 AC1
says no consent control blocks registration. A control naming a purpose the deployment
takes no consent for cannot produce a record, and the item does not say what happens.

*The readings.*

1. Record what can be recorded, ignore the rest, and complete the registration.
2. Refuse the step, since a control that should not exist is a malformed request rather
   than a consent decision.

*Chosen: 2.* AC1 is about a control left **unticked**, which writes nothing and stops
nothing, and that is what is built and tested. A tick the library cannot honour is
different: reading 1 would tell the person they had consented and keep no record of it,
which is the one outcome PRIV-CONS-001 exists to prevent. Fail closed. Nothing of the
registration is written, because the step is the one transaction of REG-SESS-001.

*Tests that pin what is built.*
`RegistrationServiceTests.PRIV_CONS_003_AC1_NoConsentIsRecordedForAControlLeftUntickedAsync`,
`RegistrationServiceTests.PRIV_CONS_001_AC1_AControlForAPurposeTakingNoConsentIsRefusedAsync`.

*Chapter text that should change.* `09` section 2's row for `POST /register/terms`
should carry the refusal, and `10` section 1.4 the code it answers with (entry 83).

---

## 91. The sweep that fires a deadline is built here and scheduled in phase 9

**Phase 7 · 2026-09-20 · Tier 2 · PRIV-RIGHT-002 AC2, the plan's phase 7 and 9 rows**

*The question.* PRIV-RIGHT-002 AC2 requires the two deadline alerts to fire "without
human monitoring", which needs something to run on a timer. The plan puts background
jobs in phase 9 and the rights queue in phase 7.

*The readings.*

1. Build a timer in phase 7 so the criterion is met end to end now.
2. Build the pass the timer will call, with its own tests, and leave the schedule to
   the one place the plan puts schedules.

*Chosen: 2.* Entry 23 already settled that the outbox publisher's schedule belongs to
phase 9; a second scheduling mechanism built here would be the second way of doing
something that already has one. `DeadlineSweep.SweepAsync` decides everything the
criterion describes and is tested against the clock; phase 9 calls it on
`sweep.interval`.

*Tests that pin what is built.*
`DeadlineSweepTests.PRIV_RIGHT_002_AC2_TheNormalAlertFiresTwoWorkingDaysBeforeAsync`,
`DeadlineSweepTests.PRIV_RIGHT_002_AC2_TheHighAlertFiresOnTheDeadlineDayAsync`,
`DeadlineSweepTests.PRIV_RIGHT_002_AC3_ARestrictionUndecidedAtTheDeadlineIsGrantedAsync`.

*Chapter text that should change.* The plan's phase 9 row should name the privacy
deadline sweep beside the outbox publisher, or PRIV-RIGHT-002 should say which phase
runs it.

---

## 92. The receipt and the lapse notice are two new message kinds

**Phase 7 · 2026-09-20 · Tier 2 · PRIV-RIGHT-002, `10` section 5b, CONV-CONTENT-001**

*The question.* PRIV-RIGHT-002 requires an automatic receipt the moment a request
enters the queue, and an honest notice to a subject whose erasure request reached its
deadline undecided. The library asks for a message by naming a `MessageKind`, and no
member of that vocabulary stands for either message.

*The readings.*

1. Send one of the existing kinds, which would have the deployment catalogue answer
   with words written for something else.
2. Add `privacy-request-received` and `privacy-request-lapsed`, and record that `10`
   needs the two rows.

*Chosen: 2.* Reading 1 puts the wrong sentence in front of the person, which is the
one thing CONV-CONTENT-001 exists to prevent; the chapter requires both messages, so
neither can go unsent. The two names are the chapter's own words for what they are.

*Tests that pin what is built.*
`PrivacyRequestTests.PRIV_RIGHT_002_AC1_TheSubjectIsSentAReceiptOnEntryAsync`,
`DeadlineSweepTests.PRIV_RIGHT_002_AC4_AnErasureUndecidedAtTheDeadlineIsDeemedRefusedAsync`,
`VocabularyContractTests.WireNames_TheKeysTheCatalogueIsAskedBy_AreWritten`.

*Chapter text that should change.* The message vocabulary needs the two kinds, and
`18` should say which screens the deployment writes the two texts for.

---

## 93. A request the caller may not decide is refused as denied, whatever the reason

**Phase 7 · 2026-09-20 · Tier 2 · PRIV-RIGHT-001, AUTHZ-CONCEAL-005, `10` section 1.4**

*The question.* Three refusals of the decision endpoints have no code in `10` section
1.4: a request identifier that names no row, a request already decided, and a caller
without `privacyrequest:manage`.

*The readings.*

1. Tell the three apart on the wire, which needs two codes `10` does not carry.
2. Answer all three with `authz.denied`.

*Chosen: 2*, following entry 83. Telling a missing identifier from an existing one
answers a question the caller has no permission to ask (AUTHZ-CONCEAL-005), and a code
`10` does not carry is a code the frontend cannot write words for.

*Tests that pin what is built.*
`PrivacyRequestTests.PRIV_RIGHT_002_AC5_ARequestIsDecidedOnceAsync`,
`PrivacyRequestTests.PRIV_RIGHT_001_AC2_EnteringWithoutThePermissionIsRefusedAsync`,
`PrivacyRequestTests.PRIV_RIGHT_001_AC2_TheQueueIsReadByTheHumanWhoWorksItAsync`.

*Chapter text that should change.* `10` section 1.4 needs a row for a request that
cannot be decided, or `09` section 8a should say the three answer alike.

**Superseded by D-162.** Applied in entry 131.

---

## 94. A rectification that lapses is deemed refused, as an erasure is

**Phase 7 · 2026-09-20 · Tier 3 · PRIV-RIGHT-002**

*The question.* PRIV-RIGHT-002 says the lapse of the six working days is deemed a
rejection, then names what happens for a restriction (granted by lapse) and for an
out-of-band erasure (deemed refused, subject notified). It says nothing about a
rectification, which is the third type `10` section 5.12c carries.

*The readings.*

1. Leave a lapsed rectification open, since the item does not name it.
2. Record it as deemed refused by lapse and tell the subject, which is what the
   statute's default and the erasure path both say.

*Chosen: 2, the strictest reading.* Leaving it open would have the clock pass with no
decision and no word to the subject, which is the outcome the whole item exists to
prevent; the statute deems the lapse a rejection for every request, and only
restriction is carved out of that because granting it is always safe. Nothing is
granted, nothing is erased, and the record persists.

*Tests that pin what is built.*
`DeadlineSweepTests.PRIV_RIGHT_002_AC4_AnErasureUndecidedAtTheDeadlineIsDeemedRefusedAsync`,
`DeadlineSweepTests.PRIV_RIGHT_002_AC4_TheLapseOfAnErasureErasesNothingAsync`.

*Chapter text that should change.* PRIV-RIGHT-002 should name the rectification case
beside the other two.

---

## 95. The request and the delivery carry typed identifiers

**Phase 7 · 2026-09-20 · Tier 2 · CONV-DESIGN-004, LIB-API-001**

*The question.* CONV-DESIGN-004 AC2 forbids a method outside the type that gives a
value its rules from taking that value as the type it is stored in, and the contract
test enforces it over every area. A request identifier and an outbox delivery
identifier are both stored as a `Guid`.

*The readings.*

1. Pass the `Guid`, which the contract test refuses.
2. Add `PrivacyRequestId` to the public contract, as `GrantId` and `SessionId` already
   are, and `DeliveryId` internal to the privacy area.

*Chosen: 2.* The convention is settled and the pattern already has thirteen instances;
`PrivacyRequestId` is public because `IPrivacyRequests` takes it, and the wire carries
its value rather than the type. The delivery never crosses the boundary, so its
identifier stays internal.

*Tests that pin what is built.*
`LibraryStructureTests.CONV_DESIGN_004_AC2_NoMethodTakesAValueAsItsUnderlyingType`,
`PrivacyRequestStoreTests.PRIV_RIGHT_002_AC1_ARequestReadsBackEveryFieldItWasWrittenWithAsync`.

*Chapter text that should change.* LIB-API-001 should list `PrivacyRequestId` among
the identifiers the contract carries.

---

## 96. A handler names what it covers, and the check reads the declaration against it

**Phase 7 · 2026-09-20 · Tier 2 · PRIV-RIGHT-005b AC3, PRIV-RIGHT-001a AC3, LIB-HOST-001**

*The question.* Startup fails where a resource type declared sensitive has no
registered handler, and where a purpose on an objectable basis has none. No chapter
says how the library learns which handler covers which type or which purpose.
`ISubjectEventSubscriber` carried a name and whether a delivery waits for it;
`ConsentChanged` and `ObjectionChanged` reach their handlers through the host's own
`IEvents`, which the library never resolves.

*The readings.*

1. One required subject-event subscriber registered anywhere satisfies every
   sensitive type, and one handler registered anywhere satisfies every objectable
   purpose.
2. A subject-event subscriber names the resource types it does the work for, a
   purpose handler names its purposes, and a type or purpose that no registration
   names stops the deployment.

*Chosen: 2.* LIB-HOST-001 requires the handlers "per sensitive resource type" and
D-068 that "a resource type declared sensitive must have a handler registered", so
reading 1 leaves the words "a resource type" carrying nothing and admits deployments
reading 2 refuses. The surface added is the least that lets the check mean what the
chapters say: one member on the interface that already existed
(`ISubjectEventSubscriber.Covers`) and one interface with one member
(`IPurposeHandler.Purposes`). The purpose handler is a registration the library
verifies at startup and never calls, because the event reaches it through the host's
own `IEvents`.

*Tests that pin what is built.*
`HandlerCoverageTests.PRIV_RIGHT_005b_AC3_ASensitiveTypeWithNoRegisteredHandlerFailsStartup`,
`HandlerCoverageTests.PRIV_RIGHT_001a_AC3_AnObjectablePurposeWithNoRegisteredHandlerFailsStartup`,
`StartupValidationTests.PRIV_RIGHT_005b_AC3_ADeploymentWithNoHandlerForItsSensitiveTypeIsRefusedAsync`.

*Chapter text that should change.* LIB-API-001 should list `IPurposeHandler` and the
`Covers` member beside `ISubjectEventSubscriber`, and LIB-HOST-001's subject-event
row should say that the handler names the types it covers.

---

## 97. A handler that faults is a handler that did not confirm

**Phase 7 · 2026-09-20 · Tier 2 · IDN-LIFE-003a, CONV-ERR-002, CONV-ERR-003**

*The question.* The subscriber contract says a handler that did not do its work says
so rather than throwing. No chapter says what the publisher does with one that throws
anyway.

*The readings.*

1. The fault leaves the pass, and the worker's next run offers the delivery again.
2. The fault is caught at the boundary, the handler counts as one that did not
   confirm, and the attempt, the backoff and the budget are recorded as for a refusal.

*Chosen: 2.* Under reading 1 the attempt is never counted, so the delivery is offered
again at every `outbox.poll.interval` with no backoff, never reaches `failed` and
never raises the exhaustion the item requires; and one faulting handler holds up every
other person's delivery in the same pass. The catch returns a failure result and
nothing permitted or successful, so CONV-ERR-002 and CONV-ERR-003 hold and JAN0006
passes on its own terms.

*Tests that pin what is built.*
`OutboxPublisherTests.PRIV_RIGHT_005b_AC2_ASubscriberThatFaultsIsRetriedRatherThanConfirmedAsync`.

*Chapter text that should change.* IDN-LIFE-003a should say, beside the requirement
that subscribers be idempotent, that a handler which faults is one that did not
confirm.

---

## 98. The link-borne bodies carry `linkToken`, including reactivation

**Phase 7 · 2026-09-20 · Tier 2 · chapter 09 sections 6 and 6a, IDN-LIFE-013**

*The question.* Chapter 09's entry for `POST /account/reactivate` spells its body
`{ "token": "..." }` and says in the same paragraph that it is "the same shape as
deletion cancellation". The entry for `POST /account/identifiers/{id}/undo` also says
it is "the same shape as deletion cancellation" and spells `{ "linkToken": "..." }`.
Deletion cancellation's own entry spells no body. The two sentences cannot both hold.

*The readings.*

1. The reactivation body is `token`, as its own JSON block spells, and deletion
   cancellation is `linkToken`, as the undo entry's cross-reference spells.
2. Every link-borne body is `linkToken`, and the block at the reactivation entry is
   the slip.

*Chosen: 2.* The reactivation entry is inconsistent with itself, so it cannot settle
its own shape; what remains is the rest of the chapter, where every body carrying a
token out of a notice (`POST /register/verify/{id}`, `POST /register/abandon`,
`POST /auth/link/abandon`, the identifier undo and the identifier abandon) is spelled
`linkToken`, and `token` is spelled only where the token is an enrolment or recovery
token the person was handed rather than a link.
Reading 2 also keeps one request shape for the four link-borne account paths instead
of two.

*Tests that pin what is built.*
`AccountLifecycleFlowTests.IDN_LIFE_013_AC1_TheNoticesLinkStandsTheAccountBackUpAsync`,
`AccountLifecycleFlowTests.IDN_ACCT_007_AC4_TheLinkEndsTheWindowAsync`.

*Chapter text that should change.* The `POST /account/reactivate` entry's JSON block
should read `{ "linkToken": "..." }`.

**Superseded by D-162.** Applied in entry 149.

---

## 99. The export asks for step-up through a Core port the authentication area implements

**Phase 7 · 2026-09-22 · Tier 2 · CONV-LAYOUT-001, LIB-API-001, LIB-API-005, chapter 09 section 7**

*The question.* Chapter 09 requires `GET /privacy/export` to be gated at the account's
reachable assurance (AUTH-STEP-002a, D-141). CONV-LAYOUT-001 puts erasure and export in
`Janus.Privacy`, which references `Janus.Core` and nothing else, and the guard that
resolves a step-up against the principal's policy is `StepUpGuard` in
`Janus.Authentication`. Privacy cannot reach it.

*The readings.*

1. A public `IStepUpGate` in `Janus.Core`, implemented in `Janus.Authentication` over
   the existing guard, which is the seam `IAccessGate` already is for authorization.
2. Move the export into `Janus.Authentication`, which can reach the guard directly.
3. Gate the export in the Hosting endpoint, before the contract is called.

*Chosen: 1.* Reading 3 is refused by LIB-API-005 AC1 and AC2: an endpoint is a mapping
onto one service contract and decides nothing itself, and a gate in the endpoint is a
rule no other caller of the contract obeys. Reading 2 moves a chapter 04 item into the
chapter 02 area and would need a second way for that area to announce a subject event,
since the outbox is Privacy's; CONV-LAYOUT-001 assigns export to Privacy by name.
Reading 1 adds one interface to the surface LIB-API-001 already carries the twin of,
and no area learns anything about another.

*Tests that pin what is built.*
`ExportServiceTests.PRIV_RIGHT_003_TheGateIsAskedBeforeAnythingIsReadAsync`,
`ExportEndpointTests.PRIV_RIGHT_003_AC1_BothFormatsContainTheSameDataAsync`.

*Chapter text that should change.* CONV-LAYOUT-002 should name `IStepUpGate` beside
`IAccessGate` as a seam `Janus.Core` carries for the areas, and LIB-API-001's
operations row should say that the gates an area asks of another area are part of the
public contract.

---

## 100. The export rate limit counts over a rolling day, and the refusal names the instant it lifts

**Phase 7 · 2026-09-22 · Tier 2 · chapter 10 section 4.1 (`privacy.export.ratelimit`), D-086, API-CONV-003**

*The question.* `privacy.export.ratelimit` is "3 per day". A day is either the calendar
day of `privacy.calendar.timezone`, which resets at midnight, or a window of
twenty-four hours that rolls.

*The readings.*

1. The calendar day: the count resets at local midnight, so an account may take three
   exports before midnight and three after, six inside one sitting.
2. A rolling twenty-four hours: an export counts until it is twenty-four hours old,
   so no six exports can ever fall inside one day.

*Chosen: 2.* The key is a rate limit and D-086's reason for it is the borrowed session
that needs one successful pull; a boundary an attacker can wait ten minutes for is not
a limit against that. Reading 2 grants strictly less than reading 1 and never more.
The window is exclusive at its old end, so the `retryAt` the refusal carries is exactly
the instant the oldest counted export falls out and the next one is allowed: a client
that retries at the instant it was given is served rather than refused again.

*Tests that pin what is built.*
`ExportServiceTests.PRIV_RIGHT_003_AnExportThatHasFallenOutOfTheWindowNoLongerCountsAsync`,
`ExportServiceTests.PRIV_RIGHT_003_TheExportAfterTheLastOneAllowedIsRefusedAsync`,
`ExportEndpointTests.PRIV_RIGHT_003_TheSpentRateLimitAnswersWithWhenItLiftsAsync`.

*Chapter text that should change.* The `privacy.export.ratelimit` row should read "3
per rolling 24 hours" and say that the refusal carries the instant the window lifts.

---

## 101. A request naming no format, or one the chapter does not name, is malformed

**Phase 7 · 2026-09-22 · Tier 2 · chapter 09 section 7 (`GET /privacy/export`)**

*The question.* The entry is spelled `GET /privacy/export?format=human|machine` and
says nothing about a request that carries no `format`, an empty one, or a third value.

*The readings.*

1. One of the two is the default, and a value the chapter does not name is served as
   that default.
2. The parameter is required and its two values are the whole of it, so anything else
   is a malformed request answered 400.

*Chosen: 2.* Reading 1 requires choosing a default the chapter does not give, and the
two arrangements are not interchangeable to a caller: a reader that asked for the
portable names and was handed the readable grouping fails on the data rather than on
the request. Reading 2 grants less, invents nothing, and matches how the chapter's
other enumerated bodies are handled (the request types of `POST /privacy/requests`).
The match is exact and case-sensitive, as every other enumerated value in chapter 09
is.

*Tests that pin what is built.*
`ExportEndpointTests.PRIV_RIGHT_003_AFormatTheChapterDoesNotNameIsMalformedAsync`.

*Chapter text that should change.* The `GET /privacy/export` entry should say that
`format` is required, that its two values are exact, and that anything else is 400.

---

## 102. The export carries every group of REG-ACCT-001 the account may see, not only the three PRIV-RIGHT-003 names

**Phase 7 · 2026-09-22 · Tier 2 · PRIV-RIGHT-003, REG-ACCT-001, PRIV-RIGHT-005**

*The question.* PRIV-RIGHT-003 names three things the export "SHALL include": the
host-declared preferences, the identifiers with their roles and verification state, and
the location records of the live sessions. It does not say whether those three are the
whole of it.

*The readings.*

1. The three are the export. Anything else the account holds is reached through
   `GET /account` and is not part of the access right the library serves.
2. The three are a floor the item states because they are the parts most easily
   missed, and the export is the access right: every group of REG-ACCT-001 the person
   may see, plus the standing chapter 04 holds for them.

*Chosen: 2.* An access export that leaves out the profile and the consent records
would not satisfy the right it exists to satisfy, and PRIV-RIGHT-005's own reasoning
treats the declared profile values and the preferences alike as the person's data. The
sections built are `account` (the opaque subject, the state, when it was registered),
`profile`, `identifiers`, `identifier-backup`, `preferences` (the value in force for
every declared key, the declared default where the account set none), `sessions` (both
location records), `consents` and `objections`. Credentials are not among them: what
signs in to an account is not data held about the person, and a list of a person's
authenticators in a file they may forward is an exposure the right does not ask for.

*Tests that pin what is built.*
`ExportSourceTests.PRIV_RIGHT_003_AC3_TheExportCarriesThePreferencesIdentifiersAndSessionsAsync`,
`ExportSourceTests.PRIV_RIGHT_003_AnAccountThatHasSettledNothingStillExportsAsync`,
`ExportServiceTests.PRIV_RIGHT_003_TheAreasAndTheDecisionsReachTheExportTogetherAsync`.

*Chapter text that should change.* PRIV-RIGHT-003 should list the sections the export
carries and say that credentials are not among them.

**Superseded by D-162.** Applied in entry 150.

---

## 103. The provider register of chapter 05 section 8 ships as a default the host takes, and a row that names no location follows the hosting

**Phase 7 · 2026-09-22 · Tier 2 · PRIV-ROPA-002, PRIV-ROPA-003**

*The question.* PRIV-ROPA-002 says the library "ships that section's rows as defaults
the host edits". It does not say whether a deployment that declares no recipients gets
those seven rows anyway, and the section's "Location field" column has three kinds of
entry (Configured, Follows hosting, Outside Egypt, and a dash) with no value spelled
for the third and fourth.

*The readings.*

1. The rows are applied: a deployment that declares nothing reports the seven
   providers, because the chapter calls them "current processors".
2. The rows are offered: `ProviderRegister.Default` is a list the host declares from,
   and a deployment that declares no recipient reports none.

*Chosen: 2.* A generic library cannot know that a given deployment uses a payment
provider or an SMS gateway, and a register that names a processor the deployment does
not have is a false statement to a regulator, which is worse than an empty column a
flag already points at. The rows ship with no agreement reference, so every one a host
takes is flagged until the host gives it one, which is the chapter's "each requires an
agreement reference". A row's `location` is nullable and an unstated one is read as
the hosting location, which is what "Follows hosting" says and what the dash leaves;
`password screening` is the one row shipped fixed as outside, so PRIV-ROPA-003's
transfer is reported with the configured basis whatever else a host edits.

*Tests that pin what is built.*
`ProcessingRecordsTests.PRIV_ROPA_002_AC1_EveryRecipientAppearsAndAProcessorWithoutAnAgreementIsFlaggedAsync`,
`ProcessingRecordsTests.PRIV_ROPA_003_AC1_ThePasswordScreeningCallAppearsAsACrossBorderTransferAsync`,
`ProcessingRecordsEndpointTests.PRIV_ROPA_001_AC1_TheGeneratedOutputMatchesTheTemplatesFieldSetAndOrderingAsync`.

*Chapter text that should change.* PRIV-ROPA-002 should say that the shipped rows are
declared by the host rather than applied, that an unstated location is the hosting
location, and that `password screening` is fixed outside.

**Superseded by D-162.** Applied in entry 151.

---

## 104. The children's column is true for every row exactly when the deployment admits minors

**Phase 7 · 2026-09-22 · Tier 2 · PRIV-ROPA-001, PRIV-MINOR-001, PRIV-SENS-001**

*The question.* The template has a children's column beside the non-sensitive and
sensitive ones, sourced from PRIV-SENS-001. Children's data is one of the declared
sensitivity categories, but no resource type can say of itself that its rows belong to
a child: whether a child's data is present is a property of the deployment, not of a
type.

*The readings.*

1. The column is true only for a purpose over a type declaring the children's
   category, like any other sensitivity category.
2. The column is true for every row exactly when the deployment admits minors, which
   is `registration.adultaffirmation` being `off`.

*Chosen: 2.* Reading 1 reports no children's processing at all in a deployment that
takes minors and declares no type as children's data, which is the under-report a
regulator would object to; the category is one a host declares over a type it knows
holds a minor's records, and most do not. Where the affirmation is required the
service is adults only and no row is in the column; where it is off any row may be, so
every row is, which over-reports rather than under-reports. The column is derived, so
a deployment that closes registration to minors sees it go false with no separate
edit.

*Tests that pin what is built.*
`ProcessingRecordsTests.PRIV_ROPA_001_ADeploymentAdmittingMinorsIsInTheChildrensColumnAsync`,
`ProcessingRecordsTests.PRIV_ROPA_001_SensitivityIsAColumnOfItsOwnAsync`.

*Chapter text that should change.* PRIV-ROPA-001 should say the children's column is
derived from the registration affirmation and not from a sensitivity category.

**Superseded by D-162.** Applied in entry 132.

---

## 105. The three supplied fields are one replaceable row, and the register is generated only for `format=template`

**Phase 7 · 2026-09-22 · Tier 2 · PRIV-ROPA-001, chapter 09 sections 8 and 8a**

*The question.* `PUT /admin/compliance/assessments` carries "LIA, DPIA and TIA
references, and the declared human-input fields of the records of processing". Neither
chapter says whether a second statement merges with the first or replaces it, nor what
`GET /admin/ropa` does with a request that names no `format`.

*The readings.*

1. A statement carries only what it changes, so an omitted field keeps its stored
   value and the links are added to.
2. A statement is the whole of the three fields, so an omitted field is cleared and
   the links are the list as given.

*Chosen: 2, with `format` required and exact.* A `PUT` replaces the resource it names,
and a compliance record a person can only add to is one they cannot correct: an
assessment link that is retired has to be removable through the same endpoint that
added it. The row is held at a fixed identifier under a check constraint, so a
deployment has one register and not a history of partial ones; what a statement
replaced is in the audit trail, not in the table. `format` follows entry 101: the one
value chapter 09 names is the whole of it, and anything else is 400 rather than a
guess at what a submitter wanted.

*Tests that pin what is built.*
`ComplianceStoreTests.PRIV_ROPA_001_AC2_TheSuppliedFieldsAreReadBackAndASecondStatementReplacesThemAsync`,
`ProcessingRecordsEndpointTests.PRIV_ROPA_001_AC2_TheThreeSuppliedFieldsAreStatedOverTheEndpointAsync`,
`ProcessingRecordsEndpointTests.PRIV_ROPA_001_AShapeTheEndpointDoesNotGenerateIsMalformedAsync`.

*Chapter text that should change.* The section 8a row should say the statement
replaces the three fields whole, and the `GET /admin/ropa` entry should say `format`
is required and exact.

---

## 106. The retention cell is one entry a data category, longest first, and a category with no key is flagged

**Phase 7 · 2026-09-22 · Tier 2 · PRIV-ROPA-001, PRIV-RET-001**

*The question.* The template's cell is "Retention period or criteria", sourced from
chapter 04 section 8. A purpose is declared over several data categories, and
`retention.<host-category>` is one key each, so one purpose has several periods and
the chapter does not say how they reach one cell.

*The readings.*

1. One period a purpose: the longest of its categories, because that is how long the
   purpose's data actually survives.
2. One entry a category, so the cell reads `<category> <period>` for each category the
   purpose is over.

*Chosen: 2, ordered longest first.* A single period hides which category carries it,
and a regulator reading the row cannot tell whether an identity record is kept as long
as an order. The entries are ordered longest first so the governing period is the one
read first, the period is written in the ISO 8601 duration form the key is stored in,
and a declared category for which the deployment named no key is reported as
`retention-missing` against that category rather than rendered as an empty or invented
period.

*Tests that pin what is built.*
`ProcessingRecordsTests.PRIV_RET_001_AC3_TheRetentionOfEachCategoryIsOnTheRowAsync`.

*Chapter text that should change.* PRIV-ROPA-001's retention row should say the cell
is one entry a data category, longest first, in ISO 8601 duration form.

---

## 107. A record whose subject key is destroyed is read anonymised, not refused

**Phase 7 · 2026-09-22 · Tier 3 · PRIV-BREACH-002, PRIV-RET-002, PRIV-RIGHT-005**

*The question.* PRIV-BREACH-002 AC2 requires the trail to answer who was affected
within the hour for a period that may cover erased accounts, and the read by subject is
what answers it. PRIV-RET-002 AC4 requires an erased subject's attributes to be
unreadable while the row stays. The read decrypts a record's personal details under the
subject key, and after an erasure that key refuses every unwrap, so one read of a
period covering an erased subject either fails entirely or returns something.

*The readings.*

1. The read fails where any record in the range belongs to an erased subject. Nothing
   erased is ever decrypted, and the operator answers the breach question from another
   source.
2. The read returns the record with its personal details empty: what happened, when,
   and to whom by opaque identifier, which are not encrypted and which erasure does not
   touch.

*Chosen: 2.* Reading 1 makes the trail unable to answer the one question it exists to
answer, at exactly the moment PRIV-BREACH-002 puts an hour on it, and it does so for
the whole range rather than for the erased subject alone. Reading 2 discloses nothing:
the fields erasure destroyed stay destroyed and are returned empty, and the identifiers
returned are the pseudonymous ones IDN-PRIN-003 keeps in the trail by design. The
branch is on the erased marker the wrapped key carries (`0x00`, PRIV-RIGHT-005a), not
on a caught decryption failure, so a key that is present but unreadable for any other
reason still fails the read.

*Tests that pin what is built.*
`AuditStoreTests.PRIV_BREACH_002_AC2_TheReadAnswersAfterErasureWithTheRecordsAnonymisedAsync`,
`AuditStoreTests.PRIV_RET_002_AC4_ErasureLeavesTheAttributeUnreadableAndTheRowIntactAsync`.

*Chapter text that should change.* PRIV-BREACH-002 should say that a record of an
erased subject is answered with its personal details empty, and PRIV-RET-002 AC4 should
say that unreadable means returned empty rather than refused.

---

## 108. A declared data category with no retention key stops the deployment

**Phase 7 · 2026-09-22 · Tier 2 · PRIV-RET-001, LIB-HOST-001, `10` section 4.7**

*The question.* Chapter 10 section 4.7 says startup fails for a declared data category
that has no `retention.<category>` key. LIB-HOST-001 AC3 says a deployment that sets
only the required values starts, and the retention keys are a family the host names per
category rather than one of the eight. Read together, a host that declares a category
and names no period either starts with no period for it or does not start.

*The readings.*

1. LIB-HOST-001 AC3 governs: the deployment starts, and the missing period is a finding
   on the records of processing rather than a refusal.
2. Chapter 10 section 4.7 governs: the deployment does not start, because a category
   with no period is personal data with no end.

*Chosen: 2.* Section 4.7 is written about this exact case and LIB-HOST-001 AC3 is
written about the eight keys that have no default; a category the host itself declared
is not one of those, so nothing it says is contradicted by refusing. Failing closed is
also the reading that grants least: data held with no stated period is the failure
PRIV-RET-001 exists to prevent. The check runs at startup over the declared purposes,
before a request is served, and names the `retention.<category>` key that is missing.
The records of processing keep their `retention-missing` finding for the case where the
register is generated against a configuration read at runtime.

*Tests that pin what is built.*
`ConfigurationCoverageTests.PRIV_RET_001_AC1_ADeclaredCategoryWithNoPeriodFailsStartupAsync`,
`ConfigurationCoverageTests.PRIV_RET_001_AC1_EveryDeclaredCategoryWithAPeriodStartsAsync`.

*Chapter text that should change.* LIB-HOST-001 AC3 should say "only the required
values and a retention period for each data category it declares".

---

## 109. A subject column is one the declared type holds and that holds a subject

**Phase 7 · 2026-09-22 · Tier 2 · PRIV-RIGHT-005a, AUTHZ-MODEL-003, AUTHZ-MODEL-004**

*The question.* PRIV-RIGHT-005a AC2 requires startup to fail for a declared subject
column that does not exist or does not reference a subject. The library never sees the
host's schema, so "does not exist" cannot mean a column of a table it does not know;
what it has is the host's own type, which the declaration names, and the member names
the declaration carries.

*The readings.*

1. The check is the compiler's: type the builder's second argument so that only a
   subject-typed member can be passed, and nothing is checked at startup.
2. The check is the model builder's: the declared field and the declared subject column
   must both be members the declared type holds, and the subject column's member must
   be a `SubjectId`.

*Chosen: 2.* Reading 1 changes the public surface and still leaves the case open,
because the declaration types are public records a host may construct directly, which
is the path that reaches the model without an expression. Reading 2 covers both paths
and adds nothing public. "Does not exist" is read as a member the declared type does
not hold, and "does not reference a subject" as a member whose type is not `SubjectId`
or a nullable one; an encrypted field naming no subject column at all is refused with
`model.startup.declarationmissing`, and the other two with a refusal that names the
type, the field and the column. The reflection is the model builder's, which is where
CONV-CODE-004 AC2 admits it.

*Tests that pin what is built.*
`AuthorizationModelTests.PRIV_RIGHT_005a_AC1_AnEncryptedFieldNamingNoSubjectColumnFailsStartup`,
`AuthorizationModelTests.PRIV_RIGHT_005a_AC2_ASubjectColumnNamingNoSubjectFailsStartup`.

*Chapter text that should change.* PRIV-RIGHT-005a AC2 should say the column is a
member of the declared type and its type is the library's subject identifier, and
chapter 10 section 1.5 should carry a row for the refusal if the owner wants it to
carry its own code rather than be a malformed-model refusal.

---

## 110. A capability page is one query over the host's rows, whatever it asks for

**Corrections 1 · 2026-09-22 · D-162 section B, correcting entry 4 · AUTHZ-GATE-005 AC1**

*What D-162 decided.* The capability page is one host-context query: beside the
stored-grant capabilities it projects one `EXISTS` per derivation reaching the type, and
the role each derivation confers is model data mapped in memory. AUTHZ-GATE-005 AC1
("without additional queries") stands as written.

*What was built.* The page evaluates every derivation that reaches the type, whatever
its role allows, in one query composed from the rows the host supplied: one clause per
derivation, each carrying the relationship it followed from beside the record it
admitted. What the conferred role allows is read where the model is read and mapped in
memory afterwards, so the cost follows neither the page's size nor the number of
permissions asked for. Entry 4's one-query-per-permission is gone.

*Tests that pin it.*
`GateBehaviourTests.AUTHZ_GATE_005_AC1_APageCostsOneStatementOverTheHostsRowsAsync`,
which asks a page for three permissions and counts the statements the host's context
sent,
`GateBehaviourTests.AUTHZ_GATE_005_AC1_APageOfFiftyIsAnsweredWithoutAQueryPerRecordAsync`,
`GateBehaviourTests.AUTHZ_GATE_005_AC2_ADerivedGrantReachesTheCapabilityPageAsync`.

---

## 111. An explanation takes the host's rows and names the grant a fact produced

**Corrections 1 · 2026-09-22 · D-162 section B, correcting entry 3 · AUTHZ-GATE-004, AUTHZ-DERIVE-001**

*What D-162 decided.* `ExplainAsync` takes the same sources as the check and the page;
without them on a derived type it is refused with `authz.derivation.sourcesmissing`;
with them it names the deciding grant, a derived one as `{ id: null, kind: derived,
subjectType, subjectId, role, deny: false, inheritedFrom }` (`10` 5.6 already has the
kind). Refusing every explanation on such a type removed a SHALL operation.

*What was built.* `IAccessGate` carries an `ExplainAsync` overload taking
`FilterSources`, beside the check and the page that already took them. It reads
concealment first, decides over the stored grants, and where none decided and the record
is one of an organization's, evaluates the derivations whose role confers the permission
over the rows the host supplied. A record one of them admits is explained as allowed by
a grant carrying no identifier, the derived kind, the asking account, the role the
derivation confers, no deny, and the container the relationship is declared on (nothing
where that container is the record itself). `ExplainedGrant.Id` is optional for that
reason; a stored or materialised grant still carries its row's identifier. A deny still
defeats a derived grant, because the host's rows are read only where nothing has
decided.

*Tests that pin it.*
`ExplanationTests.AUTHZ_GATE_004_AC2_AnApprovalNamesTheGrantAFactProducedAsync`,
`ExplanationTests.AUTHZ_GATE_004_AC1_ADenialWithTheHostsRowsStatesNoGrantMatchedAsync`,
`ExplanationTests.AUTHZ_GATE_004_AC2_AnApprovalNamesTheGrantAndWhatItWasInheritedFromAsync`,
`ExplanationTests.AUTHZ_GATE_004_AC3_OnlyATypeThatDisclosesExplainsToTheCallerAsync`,
`MaterialisationTests.AUTHZ_DERIVE_005_AC2_AnExplanationNamesTheGrantAsMaterialisedAsync`.

---

## 112. A type a derivation reaches is asked with the host's rows, whatever it confers

**Corrections 1 · 2026-09-22 · D-162 section B, correcting entry 2 · AUTHZ-DERIVE-001, AUTHZ-PRIN-001 AC2**

*What D-162 decided.* The refusal `authz.derivation.sourcesmissing` applies on any type
that a non-materialised derivation is declared on or reaches through containment,
whatever the derivation confers; the narrowing to "a derivation that confers what is
asked" makes a host call site pass until an administrator edits a role, then fault.
Materialised derivations stay excluded.

*What was built.* The three paths that take no sources (the check, the page and the
explanation) ask one predicate: whether a non-materialised derivation is declared on the
type or on a type containing it. Neither the role nor what it allows is read there, so
nothing about a call site's fate depends on a row an administrator may edit. The rule
composed for an evaluation still carries only the derivations whose role confers what is
being asked, because a relationship conferring nothing asked for must grant nothing.

*Tests that pin it.*
`GateBehaviourTests.AUTHZ_PRIN_001_AC2_ACheckWithoutTheHostsRowsIsAFaultAsync`, which
asks for a permission the reviewer's role does not confer and expects the fault,
`GateBehaviourTests.AUTHZ_PRIN_001_AC2_ACheckNoDerivationReachesNeedsNoRowsAsync`, which
checks a record of a type no derivation reaches and is answered,
`MaterialisationTests.AUTHZ_DERIVE_005_AC2_AnExplanationNamesTheGrantAsMaterialisedAsync`,
which explains a materialised deployment's record without rows.

---

## 113. A password beyond the maximum is refused as a password

**Corrections 1 · 2026-09-22 · D-162 section B, correcting entry 11 · AUTH-PASS-001 AC3, `10` section 1.2**

*What D-162 decided.* A password beyond `password.maximum` is refused with
`auth.password.toolong` (422), a new `10` section 1.2 row.
`config.value.aboveceiling` is a configuration-management code and would put a
configuration sentence on a password field.

*What was built.* `ErrorCodes.PasswordTooLong` is `auth.password.toolong`, mapped to
422 in the status table, and the length rule refuses an over-long password with it. The
refusal carries no detail, as the floor's refusal carries none: the deployment's maximum
is not a password field's business, and the key the old refusal named was the
configuration code's detail, not this one's.

*Tests that pin it.*
`PasswordFloorTests.AUTH_PASS_001_AC3_TheMaximumIsAcceptedAndOneBeyondItIsRefused`,
`ApiStatusTests.Of_ACodeTheLibraryRaises_HasAStatusOfItsOwn`.

---

## 114. The offline list travels in the package and the self-hosted corpus has an address

**Corrections 1 · 2026-09-22 · D-162 section B, correcting entry 12 · AUTH-PASS-004, INT-PWD-002, INT-PWD-003**

*What D-162 decided.* The offline leaked list is a resource embedded in the package,
dated, working with no deployment file (AUTH-PASS-004 Values). The self-hosted corpus
address is a new key `password.blocklist.selfhosted.address` (string, required only when
`password.blocklist.source` is `selfHosted`, the `service.name` pattern).

*What was built.* `leaked-passwords.txt` is an embedded resource of `Janus.Hosting`,
read through `OfflineCorpus`, which replaces the file reader. A deployment that holds no
file of its own falls back to it, and the date on its first line is still what
`password.blocklist.corpusmaxage` judges, so a release that does not refresh it goes
stale rather than silently trusted. The self-hosted corpus is no longer a second file:
it answers the same range protocol the primary source does, at the address the new key
names, which is what INT-PWD-003 ("brought in-house") describes and what makes switching
configuration only. `Settings.ThrowIfIncomplete` takes the corpus choice and requires the
address where it is `selfHosted`, beside the two conditional declarations already there.
A self-hosted corpus with no address reachable falls back to the package's list and
records the degradation, as any unreachable primary source does.

*What the release still owes.* The file's content is composed from the weak passwords
that can be enumerated without the provider's downloadable corpus (54,676 hashes). The
Values paragraph of AUTH-PASS-004 names the 100,000 most prevalent hashes of that corpus,
whose licence is verified from the provider's published terms before the file is added
and attributed in `NOTICE`. Neither the corpus nor the terms can be read from here, so
the mechanism is built and dated and the content is what the release refreshes. Nothing
in the code names the provider as the source of the shipped file.

*Tests that pin it.*
`ScreeningTests.INT_PWD_002_AC1_WithTheServiceUnreachableTheOfflineListAnswersAsync`,
which holds no file at all,
`ScreeningTests.INT_PWD_003_AC1_SwitchingToTheSelfHostedCorpusIsConfigurationOnlyAsync`,
`ScreeningTests.ScreenAsync_TheSelfHostedCorpusWithNoAddress_FallsBackAsync`,
`ScreeningTests.ScreenAsync_ACorpusOlderThanTheMaximumAge_RefusesAsync`,
`ScreeningTests.ScreenAsync_ACorpusWithNoDate_RefusesAsync`,
`StartupConfigurationTests.ThrowIfIncomplete_TheCorpusIsSelfHosted_RequiresItsAddress`,
`StartupConfigurationTests.ThrowIfIncomplete_TheCorpusIsNotSelfHosted_NeedsNoAddress`.

---

## 115. The verification code is an aggregate of its own, and the device check issues through it

**Corrections 1 · 2026-09-22 · D-162 section B, correcting entry 17 · AUTH-FACT-004, AUTH-FACT-016**

*What D-162 decided.* The verification-code aggregate of AUTH-FACT-004 and its port are
built in `Janus.Authentication` now (lifetime `code.verification.lifetime`, attempt cap
`code.verification.attempts`, invalidation on the cap, single use, its own storage per
AC2); the device check of AUTH-FACT-016 issues its code through it. AC2 and AC3 are
proved.

*What was built.* `VerificationCode` is the aggregate: what it was issued against, the
digits, when it was issued, when it stops answering, and the wrong tries entered against
it. `IVerificationCodeStore` is its port and `verification_codes` its table, which no
credential is reachable through. `VerificationCodes` issues and answers: a code lives
`code.verification.lifetime` whatever issued it, a wrong try is counted, the try that
reaches `code.verification.attempts` ends the code, and the first right try spends it,
so the same digits never answer twice. Issuing again replaces whatever the holder had
outstanding. The value helpers the registration area held (drawing, holding, reading and
the fixed-time comparison) moved into the aggregate, so there is one verification code in
the library and not two.

The new-device check of AUTH-FACT-016 now issues and answers through it, and the sign-in
challenge no longer carries a code or a counter: `signin_challenges.device_code` and
`device_attempts` are dropped by the migration that creates the table. What the check
answers with is unchanged, a wrong code while tries remain and the expiry once they are
gone, so no behaviour of the sign-in moved with the storage. The other issuers
(registration, identifier verification and the sign-in link) keep their own arrangement,
which D-162 does not reverse.

*Tests that pin it.*
`VerificationCodesTests.AUTH_FACT_004_AC2_ACodeIsHeldApartAndLivesItsOwnLifetimeAsync`,
`VerificationCodesTests.AUTH_FACT_004_AC2_TheLifetimeIsWhatTheDeploymentConfiguresAsync`,
`VerificationCodesTests.AUTH_FACT_004_AC3_TheCapEndsTheCodeAndAReplacementLeavesItDeadAsync`,
`VerificationCodesTests.AUTH_FACT_004_AC3_TheRightCodeIsSpentOnceAsync`,
`VerificationCodesTests.IssueAsync_ACodeIsOutstanding_ReplacesItAsync`,
`VerificationCodesTests.PresentAsync_NoCodeIsOutstanding_RefusesAsync`,
`AuthenticationServiceTests.AUTH_FACT_016_AC3_WrongCodesInvalidateTheHeldSignInAsync`,
`ModelTests.REG_ACCT_001_AC2_NoFieldExistsOutsideTheGroupsTheTableNames`, which carries
the new table's columns and no longer the challenge's two.

---

## 116. The configuration store stands, and the area's services are the container's

**Corrections 1 · 2026-09-22 · D-162 section B, correcting entry 19 · OPS-CFG-008, `10` section 4**

*What D-162 decided.* `IConfigurationStore` is implemented in `Janus.Storage` over the
`settings` table using the `10` section 4 value grammar (a stored value that does not
parse is a fault), and every authentication service is registered in `AddJanus`. This
was phase 0's item.

*What was built.* Both halves stand in the code as D-162 requires, phase 4 having built
what entry 19 deferred: `Janus.Storage.Settings.ConfigurationStore` reads and writes the
`settings` table through each key's own written form, is registered as
`IConfigurationStore`, and every service of the authentication area is registered in
`AddJanus` (a repository search over the area's service types found none missing). What
was not pinned was the fault: a row whose text the key cannot read was answered
correctly and no test said so. It now has one, so a later change cannot quietly let a
malformed row read as the key's default.

*Tests that pin it.*
`ConfigurationStoreTests.ReadAsync_AStoredValueThatDoesNotParse_IsAFaultAsync`,
beside `ConfigurationStoreTests.OPS_CFG_008_AC1_AChangedSettingIsInForceForTheNextReadAsync`
and the registration the hosting tests exercise end to end.

---

## 117. The library resolves a session's city from the address it already holds

**Corrections 1 · 2026-09-22 · D-162 section B, correcting entry 20 · INT-GEN-006, AUTH-SESS-013**

*What D-162 decided.* The session takes the client address it already records; an
internal resolver port maps address to `{ city, country }`; until INT-GEN-006 is built
the implementation answers no location and raises the degradation. No caller-supplied
place on any signature.

*What was built.* `ILocationResolver` is a port of the sessions area taking the address
a session was used from and answering a `SessionLocation` or nothing.
`SessionService` asks it at the four points a session records where it was used from
(begun, derived, resolved, restored) and writes the answer onto the origin, so the city
on a listing is the library's own reading of the address and never a caller's claim.
`SessionOrigin` no longer takes a place as a constructor part, and `SessionLocation`
left `PresentAsync`, `VerifyDeviceAsync`, `LandAsync` and `AcceptTermsAsync`; a contract
test now refuses its return to any public parameter. The implementation registered in
`AddJanus` is `Janus.Hosting.Sessions.LocationDatabase`, which holds the event stream
and the clock and nothing it could reach a third party with: with no file to read it
raises the `degradation` condition under the absent file, the way every other condition
in the library is raised, and answers no location. The router carries one alert a
deduplication window rather than one a sign-in, and an alert that cannot be carried
does not refuse the session. The file itself and the job that refreshes it are INT-GEN-006's own work in
phase 9 (INF-BG-001), and what that phase adds is the reading of the file behind this
port, not another signature.

*Tests that pin it.*
`SessionServiceTests.INT_GEN_006_AC3_TheCityIsWhatTheDatabaseMadeOfTheAddressAsync`,
`SessionServiceTests.INT_GEN_006_AC3_WithNoDatabaseTheSessionIsListedWithoutALocationAsync`,
`SessionServiceTests.AUTH_SESS_013_AC2_EachEntryCarriesTimesDeviceAndCityAsync`,
`PublicSurfaceTests.INT_GEN_006_AC3_NoContractMemberIsToldWhereASessionWas`,
`LocationDatabaseTests.INT_GEN_006_AC1_TheResolverHoldsNothingItCouldCallOutWith`,
`LocationDatabaseTests.INT_GEN_006_AC2_TheMissingFileSurfacesAsADegradationAsync`,
`LocationDatabaseTests.INT_GEN_006_AC3_WithNoFileAvailableNoLocationIsAnsweredAsync`.

*Chapter text that should change.* None in `05` or `09`, which name a location only
where a session is listed. The implementation plan should name the phase that builds
the file and its refresh behind this port.

---

## 118. The restrictions stay in the area; the pipeline, the templates and the router are the host's

**Corrections 1 · 2026-09-22 · D-162 section B, correcting entry 22 · CONV-LAYOUT-001, LIB-EXT-001, AUTH-ABUSE-004, OPS-ALERT-001**

*What D-162 decided.* The restriction model (keys, purposes, buckets, grants,
evaluation) stays in `Janus.Authentication`. The notification-handling contract (send a
message kind to a subject in a language) is declared in `Janus.Core` beside the
transport ports; the pipeline with retry, template resolution, the outbox publisher and
the alert router live in `Janus.Hosting` with the hosted worker.

*What was built.* `Janus.Core` declares `INotificationHandler`, the one member of which
takes a `SendRequest` (which message, to which destination, for which purpose, from
which address, in which language, with the values the library supplies) and answers the
correlation reference it was taken under or the refusal. `SendRequest`,
`SendDestination` and `SendReference` moved to `Janus.Core` with it; nothing else did.
The hash a reference is kept as is not part of the contract, so it moved the other way,
to `Janus.Authentication.Sending.SendReferences`, and the public surface hands out no
`byte[]` (CONV-CODE-003).

`SendingService` and `MessageRendering` moved to `Janus.Hosting.Sending`, and
`AlertRouter`, `AlertAudience`, `AlertDelivery` and `AlertDestinationChange` to
`Janus.Hosting.Alerting`. `AddJanus` registers the shipped handler with `TryAddScoped`,
so a deployment that registers its own keeps it (LIB-EXT-001 AC1, AC2). The restriction
model, the throttles, the budgets, the phone signals, the gateway balance, the
non-existence notice, the delivery reports, the alert vocabulary (`Alerts`) and every
persistence port stayed in `Janus.Authentication`, which is what `Janus.Storage`
implements against. What a restriction makes of one send moved from the request to
`Restrictions.IsNoticeToHolder`, where the rest of that reasoning already was.

The nine services of the authentication area now take `INotificationHandler`, so the
area sends without knowing what carries it, and their tests take a handler that records
what was asked for. What the words look like once a template has them is now tested
where the rendering is: `MessageRenderingTests` in the hosting suite carries the
guarantee that a place the values do not name is left as it stands. The two test classes
of the pipeline and the router moved with their subjects, as did the one restriction
test that proved an edit reaches the next send. The hosting suite had a second
`MessageTemplatesInMemory` of its own; it is gone, and one fake answers for the
catalogue.

*Tests that pin it.*
`LibraryStructureTests.CONV_LAYOUT_001_AC3_DependenciesAreExactlyTheOnesTheTableGives`,
`PublicSurfaceTests.CONV_CODE_003_AC1_NoContractMemberExposesAMutableCollection`,
`SendingServiceTests` in full (now in the hosting suite), including
`AUTH_ABUSE_004_AC3_AnEditAppliesToTheNextSendAsync`,
`AlertRouterTests` and `AlertDestinationChangeTests` in full (now in the hosting suite),
`MessageRenderingTests.CONV_CONTENT_001_AC1_TheNamedPlacesAreFilledAndTheWordsAreNotTouched`,
`MessageRenderingTests.Fill_APlaceTheValuesDoNotName_IsLeftAsItStands`,
and every test of the nine sending services, which now read what the library asked for
rather than what a transport was handed.

*Chapter text that should change.* CONV-LAYOUT-001's `Janus.Authentication` row should
read "factors, sessions, flows, and the restriction model the sending path answers to",
and its `Janus.Hosting` row should name the notification pipeline, template resolution,
the outbox publisher and the alert router beside the hosted worker. LIB-EXT-001's
"Notification handling" row should name `INotificationHandler` as the contract, and
LIB-API-001 should carry it, `SendRequest`, `SendDestination` and `SendReference` in the
public surface.

---

## 119. Every send is written to the library's own outbox and carried from the row

**Corrections 1 · 2026-09-22 · D-162 section B, correcting entry 23 · D-022, IDN-PRIN-003, PRIV-RIGHT-005a, AUTH-ABUSE-004**

*What D-162 decided.* Every send is written as a row in the library's own outbox table
inside the caller's transaction and delivered by the worker under `outbox.retry.*` with
status recorded. Until the publisher exists (phase 9) the send path attempts once at
commit and leaves the row in its recorded state. A refused or failed delivery counts
against no bucket. Exhaustion raises the `degradation` condition.

*What was built.* `Janus.Authentication.Sending` declares the outbox: `SendDelivery`
(what is to be sent, when it was undertaken, under which identifier), `SendDeliveryId`
(a version 7 value, so the messages written together sit together in the index a
publisher reads them in) and `ISendOutbox` (add, find, remove). `Janus.Storage`
implements it over the new `send_outbox` table, which holds `id`, `recorded_at`,
`subject`, `key_version`, `wrapped_key` and `enc_message` and nothing else.

The send path now writes the row once the restrictions have let the message through and
before any transport is asked, reads the message back from the row, carries it, and
removes the row in the transaction that counts the send. A transport that refuses
leaves the row exactly as it was recorded, which is what the publisher will retry from,
and counts nothing. Because the caller holds the transaction and the unit of work is
re-entrant with no after-commit hook, the row joins the caller's transaction: a message
undertaken by an operation that then fails is never sent, because the row and the change
roll back together, and one taken immediately leaves no row at all.

*Decided in the owner's absence (Tier 3, strictest reading).* Three points D-162 does
not settle:

1. *What the row holds.* The destination, the source address and the template values are
   all personal, and nothing queries inside an undelivered message, so the whole request
   is one encrypted column, as `registration_sessions` holds a staged registration
   (PRIV-RIGHT-005a). The table holds no destination, no address and no value in plain.
2. *Which key it is held under.* The row's own data key, wrapped under the
   key-encryption key, not the subject's. A send may name a subject that holds no key
   yet (a registration in progress names its provisional subject) and a send may name no
   subject at all (a notice to an address no account holds, an alert to an operator
   destination), so a subject key cannot serve every row and two shapes in one table
   would be worse than one. The consequence, stated rather than implied: a message
   outstanding for a subject erased between its recording and its carriage stays
   readable to a holder of the key-encryption key until the row is spent. The `subject`
   column is what an erasure sweep would find those rows by.
3. *What "attempts once at commit" means with a caller's transaction open.* The library
   has no after-commit hook, so the attempt is made after the send path's own commit
   call, which is the outermost commit only when no caller holds one. With a caller's
   transaction open the transport is asked before that transaction commits. This is the
   residue the publisher removes in phase 9: once it exists, the send path writes the row
   and nothing else asks a transport inside a caller's transaction.

*Not built, by D-162's own terms.* The retry schedule (`outbox.retry.*`) and the
`degradation` condition on exhaustion belong to the publisher, which counts the attempts;
D-162 places it in phase 9. One attempt is not exhaustion, and the row is left recorded
for that publisher.

*Tests that pin it.*
`SendingServiceTests.D_022_TheMessageIsWrittenToTheOutboxAndRemovedOnceTakenAsync`,
`SendingServiceTests.D_022_ATransportRefusalLeavesTheMessageRecordedAsync`,
`SendOutboxTests.D_022_TheMessageReadsBackAsItWasUndertakenAsync`,
`SendOutboxTests.PRIV_RIGHT_005a_AC8_TheTableYieldsNoDestinationInPlainAsync`,
`SendOutboxTests.IDN_PRIN_003_AMessageTakenLeavesNoRowAsync`,
`ModelTests` on the six columns,
and `SendingServiceTests.AUTH_ABUSE_004_AC2_ATransportRefusalCountsNothingAsync`, which
still holds: a refused delivery counts against no bucket.

*Chapter text that should change.* An item of `02` should state the outbox a send is
written to, that the row is removed once a transport has taken it (IDN-PRIN-003), and
that the row's message is encrypted under a key of the row's own because a send may name
no subject. `10` should carry the `send_outbox` table and its six columns.

---

## 120. A template is measured at startup with every place it names at its widest

**Corrections 1 · 2026-09-22 · D-162 section B, correcting entry 26 · INT-SMS-003, INT-SMS-005a, AUTH-ABUSE-005**

*What D-162 decided.* At startup every template in every language of
`notification.languages` is rendered with the maximum-width value of each placeholder
(defined once per placeholder beside the message kind) and refused where it exceeds the
single-message budget (70 non-GSM, 160 GSM-7). No measurement at send. "Any rendered
template" stands.

*What was built.* `MessagePlaceholders`, beside `MessageChannels`, defines every place
the library fills and the width each is measured at, once per place. The startup check
now renders each text template with every place it names at that width and refuses the
deployment where the result exceeds its language's budget. Nothing is measured at a
send, as before.

The widths the library fixes are derived rather than written down: a code is the
verification code's digits, a token is the width of a drawn token, a condition is the
widest written alert condition, an instant is the width of the round-trip form, a count
is the width of an integer, an amount the width of a decimal and an identifier the width
of a written identifier. The four places whose values are a deployment's or a host's (a
restriction name, a governing document, a settings key, the subscribers still to confirm
an erasure) carry the width past which the library will not promise one message. A place
the library does not fill is left as it stands, at startup exactly as at a send.

The filler is a letter of the default alphabet, so filling a template leaves it in the
alphabet it was written in and the budget it is measured against unchanged.

*Decided in the owner's absence (Tier 3, strictest reading).* A value wider than the
width its place is defined at costs a second message and is not refused at the moment of
the send, because D-162 says nothing is measured there. The four stated widths are
therefore the library's promise and not a check: a deployment that names a restriction
wider than 64 characters, or a host whose erasure subscribers are named at more than 128
characters in total, pays for a second message rather than being refused. The alternative
readings (truncating a value at send, or measuring the rendered text at send) both
reintroduce the measurement D-162 removed, and truncation would corrupt a code or a link.

*Tests that pin it.*
`SendingValidationTests.INT_SMS_003_AC1_ATemplateIsMeasuredWithItsPlacesAtTheirWidestAsync`,
`SendingValidationTests.INT_SMS_003_AC1_APlaceTheLibraryDoesNotFillIsMeasuredAsWrittenAsync`,
`MessagePlaceholdersTests.INT_SMS_003_AC1_APlaceIsFilledToTheWidthItIsDefinedAt`,
`MessagePlaceholdersTests.INT_SMS_003_AC1_TheValuesTheLibraryDrawsFitTheWidthsItMeasuresAt`,
and the three that already stood:
`SendingValidationTests.AUTH_ABUSE_005_AC3_AnOverBudgetTextMessageStopsStartupAsync`,
`SendingValidationTests.INT_SMS_003_AC1_ALatinMessageOverItsBudgetStopsStartupAsync`,
`SendingValidationTests.INT_SMS_003_AC2_EveryTextMessageIsMeasuredInEveryLanguageAsync`.

*Chapter text that should change.* INT-SMS-003 AC1 stands as written ("any rendered
template"), and the item should state that the render is done at startup with each place
at its defined width, that the widths are defined once per place beside the message
kinds, and that a place the library does not fill is left as it stands. `10` should carry
the places and their widths, which are listed under "Rows for chapter 10".

---

## 121. Publishing answers for itself, and the operation that made the event carries it

**Corrections 1 · 2026-09-22 · D-162 section B, correcting entry 29 · CONV-DESIGN-005 AC1, LIB-API-001, IDN-LIFE-003a, INT-GEN-006**

*What D-162 decided.* `IEvents.PublishAsync` returns `ValueTask<Result>`; on failure the
outbox row stays unmarked for the publisher and the degradation is raised; on success
the row is marked. No exemption for ports from CONV-DESIGN-005.

*What was built.* `IEvents.PublishAsync` answers with an outcome, and every one of the
twenty-seven places that publishes now carries it. `IEvents` is out of the gate test's
exemption list, and the two that remain there are justified by CONV-ERR-001 (their
failures are faults and throw) rather than by being ports.

Three publications were made after the commit of the transaction that made them true
(the account a registration creates, and the two changes a restriction administration
records). They are now made inside that transaction, which is what lets the failure be
carried: an operation that cannot record its event commits nothing. The balance reading
and the alert it raises likewise commit together.

`ILocationResolver.ResolveAsync` answers with an outcome for the same reason: the
shipped resolver's job is to answer no location *and* raise the degradation, and one of
those failing is the resolver failing. `Supersession.OfAsync` and
`IdentifierService.SettleAsync` answer with outcomes because each publishes on behalf of
its caller.

*Decided in the owner's absence (Tier 3, strictest reading).* Three points:

1. *What a caller does with a failed publication.* It fails the operation. A failure
   from the port cannot tell a caller whether an outbox row was written (and so will be
   retried) or not written at all (and so is lost), and the safe reading of the two is
   the second: a state change whose event may never arrive is not committed. The
   alternative, continuing and letting the publisher catch up, is only safe under the
   reading the caller cannot verify.
2. *Where the publication happens.* Inside the transaction that made the event true,
   not after its commit. The other reading (publish after the commit, as three sites
   did) makes the outcome uncarryable: the account exists, so returning a failure would
   tell the caller a registration failed that in fact succeeded. `IEvents` is documented
   accordingly: the publication is part of the operation.
3. *"No exemption for ports."* Applied to the reason, which entry 29 used and which
   D-162 reverses: being a port buys no exemption. `ISecretSource` and `IUnitOfWork`
   stay outside the gate's list on a different ground, that they carry no expected
   failure at all (CONV-ERR-001 makes theirs faults, which throw). Reading the sentence
   to cover them as well would make a commit answer with an expected outcome that can
   only ever be success, which is ceremony CONV-DESIGN-005 does not ask for. The owner
   may disagree, and this is the one line of D-162 item 29 not applied literally.

*Not built, by D-162's own terms.* The outbox row a publication is recorded in, the
marking of it, and the degradation raised on exhaustion belong to the publisher of phase
9. No implementation of `IEvents` is shipped yet.

*Tests that pin it.*
`ResultContractTests.CONV_DESIGN_005_AC1_EveryContractMethodReturnsAnOutcome`, which now
reaches `IEvents`,
`AccountLifecycleTests.CONV_DESIGN_005_AC1_AnEventThatIsNotTakenFailsTheOperationAsync`,
`SessionServiceTests.INT_GEN_006_AResolverThatCouldNotReportFailsTheSignInAsync`,
and every existing test of the twenty-seven publishing operations, which still pass
because a publication that is taken changes nothing.

*Chapter text that should change.* CONV-DESIGN-005 should say that AC1 reaches every
public contract including the ports, and name what it does not reach and why. LIB-API-001
should carry `IEvents.PublishAsync` with its new return. An item of `02` should state
that an operation publishes inside its transaction and commits nothing it could not
publish.

---

## 122. A send counter is kept for what the restrictions now declare, not for what a send was written under

**Corrections 1 · 2026-09-22 · D-162 section B, correcting entry 31 · AUTH-ABUSE-004 AC6, PRIV-RET-005 AC2, INT-SMS-005**

*What D-162 decided.* No settle column on a destination record. Before a send's counters
are read, destination rows whose newest timestamp is older than the longest interval any
current destination restriction declares are deleted (one indexed delete). The record
holds the HMAC and timestamps only, and a tightened interval is honoured for sends
already counted.

*What was built.* `send_counters` holds the key and the times and nothing else. The
`settles_at` column and its index are dropped. The sweep moved from the recording path to
the read: `ISendLedger.CountersAsync` takes the instant before which a time decides
nothing and deletes every record whose newest time is older than it before it reads
anything. The sending service computes that instant as the clock less the longest
interval the restrictions it has just read declare, so a host that shortens an interval
reaches the records already written, which the derived column could not.

The times are written oldest first, so the newest is the last element of the array. No
model builder expresses an index over an expression, and PostgreSQL matches an expression
index only on the expression as written, so the migration writes the index over the
statement the sweep generates, element for element, and a unit test holds the two
together.

*Decided in the owner's absence (Tier 3, strictest reading).* One point: *which
restrictions the longest interval is taken over.* D-162 says the longest any current
destination restriction declares. The record is the keyed hash of the restriction name
and the value and nothing else, so the table cannot say which rows are destination rows
and a single delete cannot be narrowed to them. The longest over all current restrictions
is the only interval the delete can be written against, and it is also the reading that
deletes least: it is greater than or equal to the destination-only interval, so the
instant it computes is earlier and every row the narrower reading would keep is kept. A
row is therefore never deleted while a restriction could still count it.

*Residue.* A record of a key the deployment stops sending to altogether stands until the
next send of any kind is planned, because the sweep runs on the read and there is no
schedule behind it. D-162 asks for it there and nowhere else.

*Tests that pin it.*
`SendLedgerTests.AUTH_ABUSE_004_AC6_TheRecordHoldsAHashAndTimesAndNothingElseAsync`,
`SendLedgerTests.AUTH_ABUSE_004_AC6_TheRecordIsGoneOnceItsBucketsAreEmptyAsync`,
`SendLedgerTests.AUTH_ABUSE_004_AC6_AShortenedIntervalReachesTheSendsAlreadyCountedAsync`,
`SendLedgerTests.PRIV_RET_005_AC2_TheRecordLivesAtMostTheLongestBucketIntervalAsync`,
`SendCounterSweepTests.AUTH_ABUSE_004_AC6_TheSweepReadsTheExpressionTheIndexIsOver`,
`SendingServiceTests.AUTH_ABUSE_004_AC6_ARecordOlderThanTheLongestIntervalGoesWithTheNextReadAsync`,
`ModelTests.REG_ACCT_001_AC2_NoFieldExistsOutsideTheGroupsTheTableNames`.

*Chapter text that should change.* AUTH-ABUSE-004 AC6 should say when the record is
deleted and against what, and should say whether the interval is taken over the
destination restrictions or over all of them. PRIV-RET-005 AC2 should say that the
retention of a counter follows the declaration as it now stands.

---

## 123. The library ships the words, and declaring no catalogue is not a refusal

**Corrections 1 · 2026-09-22 · D-162 section B, correcting entry 32 · LIB-EXT-001, AUTH-ABUSE-005 AC3, CONV-CONTENT-001, LIB-HOST-001**

*What D-162 decided.* `Janus.Hosting` ships the default message catalogue (LIB-EXT-001)
in the languages the library carries; a host may replace it. The startup check refuses
only a message with no text in a declared language or over budget. No deployment fails
startup for declaring no catalogue. The default texts are the owner's to review before
release.

*What was built.* `DefaultMessageTemplates` in `Janus.Hosting` holds one text for every
message the library sends, on every channel that message goes out on, in English and in
Arabic. It is registered the way the shipped notification handler is: a deployment that
registered a catalogue of its own keeps it, and one that registered none gets this. The
startup check now takes the catalogue in force rather than an optional one, so the only
refusals it can produce are a message with no text in a declared language and a text
message over its budget.

Every shipped text message is measured with its places at their widest, as entry 120
requires of any catalogue, and fits one message: the Arabic texts of the messages that
carry a token are written short enough to hold a forty-three character token inside the
seventy units a non-Latin message gets.

*The texts are for review.* They are plain, they say what happened and what to do, and
they are the owner's to rewrite before release. Nothing in the library reads them; a
deployment that wants other words registers a catalogue.

*Decided in the owner's absence (Tier 3, strictest reading).* Two points:

1. *Which languages the library carries.* English and Arabic. `00` names Arabic as
   first-class and never a lagging translation, and no chapter names a third. Carrying
   fewer would make the shipped catalogue useless to the default deployment; carrying
   more would be words nobody has reviewed.
2. *How a language is matched.* Exactly, as the tag is written. A deployment declaring
   `en-GB` is not answered out of `en`: matching by prefix would let a deployment start
   on words written for another variant without anybody deciding that they serve, and
   the check that refuses it names the key the deployment has to supply.

*Tests that pin it.*
`DefaultMessageTemplatesTests.LIB_EXT_001_EveryMessageIsWordedOnEveryChannelInEveryLanguageCarried`,
`DefaultMessageTemplatesTests.AUTH_ABUSE_005_EveryShippedTextMessageFitsOneMessageAtItsWidest`,
`DefaultMessageTemplatesTests.CONV_CONTENT_001_EveryPlaceAShippedTextNamesIsOneTheLibraryFills`,
`DefaultMessageTemplatesTests.LIB_EXT_001_AC1_ADeploymentThatRegistersNoCatalogueGetsTheShippedOne`,
`DefaultMessageTemplatesTests.LIB_EXT_001_AC2_TheCatalogueTheDeploymentRegistersIsTheOneInForce`,
`DefaultMessageTemplatesTests.AUTH_ABUSE_005_AC3_ADeploymentOnTheShippedCatalogueStartsAsync`,
`DefaultMessageTemplatesTests.AUTH_ABUSE_005_AC3_ALanguageTheShippedCatalogueLacksStopsStartupAsync`,
`StartupValidationTests.AUTH_ABUSE_005_AC3_ADeploymentThatDeclaredNoMessagesStartsOnTheShippedOnesAsync`.

*Chapter text that should change.* LIB-EXT-001's row for message templates should name
the languages the shipped catalogue is written in. AUTH-ABUSE-005 AC3 should say that
what is refused is a message with no text in a declared language or over budget, and
that a deployment which registers no catalogue is answered out of the shipped one.
LIB-HOST-001 should not list a message catalogue among the declarations a deployment
must make.

---

## 124. Every runtime setting is written through one operation, which classifies it, gates it, requires a reason and writes it down

**Corrections 1 · 2026-09-22 · D-162 section B, correcting entry 34 · OPS-CFG-002, OPS-CFG-005, OPS-CFG-008, OPS-ALERT-004a**

*What D-162 decided.* The general configuration audit is built now. Every runtime write
goes through one operation taking the access context and a reason, classifying direction
per `10` section 4, requiring step-up and a non-empty reason for a loosening or a
no-direction change, writing actor, key, before, after, timestamp and reason, queryable
by setting and by actor. `IConfigurationStore.WriteAsync` is called only from it, and
the alert-destination change goes through it.

*What was built.* `ConfigurationAdministration` in `Janus.Authentication/Configuration/`
is that operation. It refuses a system principal outright, because a change answers for
itself through the person who made it and background work is nobody to answer. It begins
the unit of work, reads the value in force, asks the setting which way the change runs,
and where the change loosens it refuses a challenge the `config:loosen` gate is not
satisfied by and then a reason that is absent or blank, before writing anything. The
write, the audit record and the commit are one transaction.

Direction is decided by the setting itself. `Setting<TValue>.Loosens` reads the
`SettingDirection` of chapter 10 section 4: a key bounded above loosens upward, one
bounded below loosens downward, and a key with no direction is loosened by any change at
all. `MultipleChoiceSetting` overrides it for a set, which loosens by the member it lost
or gained as its own direction says.

`RestrictionSetSetting` overrides it too, because chapter 10 section 4 lets a key whose
own chapter states its direction govern, and AUTH-ABUSE-004 states the restriction set's:
a set loosens where a restriction in it was deleted, widened, raised, shortened or lost a
bucket, and a set that only gains restrictions or tightens them does not. The rule that
had lived in `Restrictions.IsLoosening` is the override; `Restrictions.IsLoosening` calls
it, so there is one implementation and a restriction tightening stays free of a reason as
`/admin/restrictions` requires.

`IConfigurationAudit` is the port and `ConfigurationAudit` the adapter. The record goes to
the one audit trail under the security retention, exactly as a permission grant does,
with the action `ops.configuration.changed` and the key, before, after, direction and
reason in its details. It reads back by setting through a jsonb containment query and by
actor through the acting subject, over two indexes the migration adds: a partial GIN
index on the details for that action, and a composite index on the acting subject and
the instant.

`RestrictionAdministration` and `AlertDestinationChange` write through the operation, and
a gate test over the source tree holds that nothing else calls
`IConfigurationStore.WriteAsync`.

*Decided in the owner's absence.* Four points.

1. *Which error code a missing reason carries (Tier 2).* Chapter 09 gives
   `PUT /admin/config/{key}` the codes `auth.restriction.reasonrequired` (422) and
   `auth.stepup.required` (403), so no code is added. The existing code's documentation
   is widened to cover a runtime configuration change, which is the chapter's own
   reading; `09` is authoritative for status codes and names.

2. *When a reason is required (Tier 3, strictest reading).* Chapter 09's prose says a
   reason is required on every change to that endpoint; its status list requires one only
   where the change loosens. D-162 item 34 says step-up and a non-empty reason for a
   loosening or a no-direction change, and D-162 wins. The operation therefore requires a
   reason exactly where it computes a loosening, which is also what keeps a restriction
   tightening free of one.

3. *Where the operation lives (Tier 2).* `Janus.Core` internals are not visible to
   `Janus.Authentication`, and `RestrictionAdministration` is in `Janus.Authentication`
   and has to call it, so the operation cannot be a Core-internal helper.
   `Janus.Authentication/Configuration/` is the one place both callers can reach.

4. *When a destination change is refused for want of a reason (Tier 3, strictest
   reading).* OPS-ALERT-004a tells the destinations being replaced before the change
   takes effect. A change refused after that notice would tell them of something that did
   not happen, and the event is not published on a refusal, so the trail and the notice
   would disagree. What the change costs is therefore decided before the notice goes out,
   through `ConfigurationAdministration.AllowedAsync`, which applies the same rule the
   change applies and writes nothing.

*Tests that pin it.*
`ConfigurationAdministrationTests.OPS_CFG_002_AC1_ShorteningASessionTimeoutRequiresNoStepUpAsync`,
`ConfigurationAdministrationTests.OPS_CFG_002_AC2_LengtheningOneRequiresStepUpAsync`,
`ConfigurationAdministrationTests.OPS_CFG_002_AC2_LengtheningOneRequiresAReasonAsync`,
`ConfigurationAdministrationTests.OPS_CFG_002_AC3_AChangeWithNoDirectionRequiresStepUpAndAReasonAsync`,
`ConfigurationAdministrationTests.OPS_CFG_002_AC4_ATighteningAndALooseningAreBothAuditedAsync`,
`ConfigurationAdministrationTests.OPS_CFG_005_AC1_TheRecordCarriesBeforeAndAfterAsync`,
`ConfigurationAdministrationTests.OPS_CFG_005_AC2_TheRecordsAreQueryableBySettingAndByActorAsync`,
`ConfigurationAdministrationTests.OPS_CFG_005_ASystemPrincipalChangesNoSettingAsync`,
`SettingDirectionTests.OPS_CFG_002_AKeyBoundedOnlyAboveLoosensUpward`,
`SettingDirectionTests.OPS_CFG_002_AKeyBoundedOnlyBelowLoosensDownward`,
`SettingDirectionTests.OPS_CFG_002_ABooleanLoosensAwayFromItsDefault`,
`SettingDirectionTests.OPS_CFG_002_ASetLoosensByTheMemberItLost`,
`SettingDirectionTests.OPS_CFG_002_TheRestrictionSetLoosensByItsOwnRule`,
`ConfigurationAuditTests.OPS_CFG_005_AC1_TheRecordCarriesBeforeAndAfterAsync`,
`ConfigurationAuditTests.OPS_CFG_005_AC2_TheRecordsAreQueryableBySettingAsync`,
`ConfigurationAuditTests.OPS_CFG_005_AC2_TheRecordsAreQueryableByActorAsync`,
`AlertDestinationChangeTests.ChangeAsync_ADestinationChange_IsWrittenDownAsARuntimeChangeAsync`,
`AlertDestinationChangeTests.ChangeAsync_ADestinationChangeWithNoReason_TellsNobodyAsync`,
`LibraryStructureTests.OPS_CFG_002_OnlyTheConfigurationAdministrationWritesARuntimeSetting`.

*Chapter text that should change.* Chapter 09's prose for `PUT /admin/config/{key}`
should say a reason is required where the change loosens, which is what its own status
list says and what D-162 item 34 decides. Chapter 10 section 4 should say that a key
whose chapter states its direction governs, naming the restriction set as the one that
does. Chapter 10 needs a row for the audit action `ops.configuration.changed`, listed
under **Rows for chapter 10**.

---

## 125. A signed-in browser asking to register is refused, and no account document crosses a registration route

**Corrections 1 · 2026-09-22 · D-162 section B, correcting entry 46 · REG-SESS-002, API-LAND-001**

*What D-162 decided.* `POST /register` from a browser holding a live session creates
nothing and answers 409 with `identity.registration.signedin`; the frontend navigates to
the account application. No account document crosses that boundary.

*What was built.* The endpoint refuses with the new code and stages nothing. The
`Landing` answer, which rendered the account document from a registration route, is gone
along with the route's dependency on `IAccount`: a registration route has no business
reading an account, and the account application fetches its own document behind its own
gate. The code is 409 in the status table, which is the status a conflict with the
browser's own state takes.

*Tests that pin it.*
`RegistrationFlowTests.BeginAsync_ABrowserAlreadySignedIn_IsRefusedAndStagesNothingAsync`.

*Chapter text that should change.* `09` section 2 should say the request is refused 409
with `identity.registration.signedin` rather than "answered with the account landing".
Chapter 10 section 1.1 needs the row, listed under **Rows for chapter 10**.

---

## 126. A request the library cannot read is answered like every other refusal

**Corrections 1 · 2026-09-22 · D-162 section B, correcting entry 49 · API-CONV-002, API-CONV-003**

*What D-162 decided.* A request the reader cannot parse answers 400 with the
API-CONV-002 body: `api.request.malformed`, `correlationId`, `details` naming the
offending member and nothing of its value.

*What was built.* The code, at 400 in the status table. A body that never reaches an
endpoint fails at binding, so the reader is told to raise the failure
(`RouteHandlerOptions.ThrowOnBadRequest`) instead of writing a bare status the pipeline
never sees, and one layer, mounted outermost in both profiles, turns it into the usual
refusal. The member it names is the reader's own path, which names members and carries
nothing of the values between them; where the body failed before any member, the refusal
carries the code alone. The layer records the refusal and answers nothing it did not
record.

Every other 400 the library answered went the same way. Each endpoint held a shared
`TypedResults.BadRequest()` with no body, so a caller could not trace the one refusal
that told them least. They now answer through `Answers.Malformed`, naming the member the
endpoint required. Where a site tested several members in one condition, the tests are
now one per member, so the answer names the member that was actually wanting.

*Decided in the owner's absence.* Two points.

1. *Whether the endpoints' own 400 is in scope (Tier 2).* D-162 item 49 names the parse
   failure. Answering only that would have left the library with two ways of refusing a
   400, one with a body and one without, and API-CONV-002 AC2 requires a correlation
   identifier on every error. Every 400 therefore carries the body.

2. *Where the layer is mounted (Tier 2).* BFF-ORDER-001 stage 11 is error translation,
   which is last on the way out and therefore first on the way in. It is mounted there in
   both profiles; nothing between the stages moved, and every stage inside it answers as
   it did before.

*Residue.* The layer catches what the reader raises about the request and nothing else.
Stage 11's concealment is not built here.

*Tests that pin it.*
`ApiConventionTests.MapRegistration_ABodyThatDoesNotParse_AnswersTheUsualBodyAsync`,
`ApiConventionTests.MapRegistration_ABodyMissingAMember_NamesTheMemberAsync`,
`ErrorCodesTests.CONV_NAME_003_AC2_ChangingACodeFailsTheContractTest`.

*Chapter text that should change.* Chapter 10 section 1.5 needs the row, listed under
**Rows for chapter 10**. API-CONV-003 should say that 400 carries the API-CONV-002 body
like every other status.

---

## 127. The frontend's passkey pages are a declaration the deployment cannot start without

**Corrections 1 · 2026-09-22 · D-162 section B, correcting entry 50 · REG-PM-001, LIB-HOST-001, LIB-HOST-003**

*What D-162 decided.* The passkey-pages declaration is required with no default and its
absence fails startup with `model.startup.declarationmissing`; it joins LIB-HOST-001's
table.

*What was built.* `PasskeyAddresses.None` is gone and nothing registers a default, so a
deployment either declares the three addresses or does not start. A new startup check,
`DeclarationCoverage`, reads what LIB-HOST-001 requires against what the host registered
and names what is absent: the declaration itself, or the field of it that is empty. It
runs from a hosted service registered behind the other four, before the web server.

Both well-known documents now always answer, which is what REG-PM-001 AC2 states without
qualification: the redirect and the JSON no longer have an absence to answer for, and the
404 branches are gone.

*Decided in the owner's absence.* One point, Tier 2: *what the refusal names.* The other
startup checks name a `key`, a `handler` or a `supplier`. A host declaration is nearest a
key, so this one names `key` and spells the declaration as the host writes it,
`passkeyAddresses`, with the empty field appended where the declaration is present but
incomplete.

*Tests that pin it.*
`StartupValidationTests.REG_PM_001_ADeploymentThatDeclaredNoPasskeyPagesIsRefusedAsync`,
`WellKnownTests.REG_PM_001_AC2_TheWellKnownDocumentsAnswerAndTheProbeDoesNotAsync`,
`WellKnownTests.MapWellKnown_TheDeclaredAddresses_AreWhatBothDocumentsCarryAsync`,
`ApiConventionTests.API_CONV_001_AC1_TheHostMountsTheLibraryWhereItLikesAsync`.

*Chapter text that should change.* REG-PM-001 should name the declaration the host
registers and say that its absence stops the deployment. LIB-HOST-001's table needs the
row, listed under **Rows for chapter 10**.

---

## 128. The sign-in screen is a declaration the deployment cannot start without

**Corrections 1 · 2026-09-22 · D-162 section B, correcting entry 61 · AUTH-SESS-012 AC3, LIB-HOST-001, LIB-HOST-003**

*What D-162 decided.* The authentication application's sign-in address is a required
declaration with no default; its absence fails startup with
`model.startup.declarationmissing`; `login_required` is the answer to `prompt=none`
alone.

*What was built.* `AuthenticationAddresses.None` is gone, nothing registers a default,
and `DeclarationCoverage` now reads the sign-in address beside the passkey pages, naming
`authenticationAddresses.signIn` where it is absent or empty. The OIDC registration no
longer registers an address of its own, so the only address in the process is the one the
host declared.

`AuthorizationIssue` forwards every interactive request whose refusal is a missing
session, because there is now always somewhere to forward it, and answers
`login_required` only where the request carried `prompt=none`. A request that did not ask
to be told is told nothing it did not ask for; anything else is `access_denied`.

*Decided in the owner's absence.* One point, Tier 2: *what the refusal names.* As in
entry 127, a host declaration is nearest a settings key, so the refusal names `key` and
spells the declaration as the host writes it, with the absent field appended:
`authenticationAddresses.signIn`.

*Tests that pin it.*
`StartupValidationTests.AUTH_SESS_012_AC3_ADeploymentThatDeclaredNoSignInScreenIsRefusedAsync`,
`OidcFlowTests.AUTH_SESS_012_AC3_AnInteractiveRequestReachesTheSignInScreenAsync`,
`OidcFlowTests.AUTH_SESS_012_AC3_ASilentRequestWithoutASessionSaysSoAsync`.

*Chapter text that should change.* AUTH-SESS-012 AC3 should say that the address is
declared and that its absence stops the deployment, and that `login_required` answers a
silent request alone. LIB-HOST-001's table needs the row, listed under **Rows for chapter
10**.

---

## 129. The ceremony carries the account it is for

**Corrections 1 · 2026-09-22 · D-162 section B, correcting entry 75 · REG-PM-001, REG-SESS-001, AUTH-FACT-014**

*What D-162 decided.* The creation ceremony's `begin` carries `user.id` equal to the
subject identifier (the registration session's provisional handle, reserved as the future
`SubjectId`), `user.name` the primary email and `user.displayName` the display name or
empty; the assertion path resolves the account from the returned handle.

*What was built.* `CeremonyUser` carries the three, and `WebAuthnCeremony` and the public
`CredentialCeremony` carry one. `WebAuthnService.BeginAsync` takes the user rather than
composing it, so the account path passes the account and the registration path will pass
the session's provisional handle without a second way of building one.
`CredentialService` reads the primary email from the identifier directory and the display
name from the account directory, exactly as the userinfo claims do.

`AuthenticatorAssertion` and the verified `WebAuthnAssertion` carry the handle the
authenticator returned. `PresentAsync` refuses an assertion whose handle names an account
other than the credential's owner, or decodes to nothing the library ever issued. The
refusal is the ordinary `auth.factor.rejected`, so whose credential it is stays
undisclosed.

*Decided in the owner's absence.* Two points.

Tier 2: *the shape on the wire.* D-162 names the three members with dots, so they are a
nested `user` object of `id`, `name` and `displayName` rather than three flat fields:
that is what a frontend passes straight to the browser, and `09` section 4 describes the
ceremony in WebAuthn's own vocabulary.

Tier 3: *what the returned handle decides.* Two readings: the handle replaces the
credential lookup, or the handle must agree with it. The handle is not covered by the
assertion signature, so it cannot be the only thing consulted; the strictest reading is
that the credential is still found by its identifier and a handle that disagrees is a
refusal. A credential that returns no handle, which is what a second-factor security key
does, is judged as before.

*Tests that pin it.*
`WebAuthnServiceTests.REG_PM_001_AC1_TheHandleIsTheSubjectIdentifierAndNothingElseAsync`,
`WebAuthnServiceTests.REG_PM_001_TheCeremonyCarriesTheNameAndTheDisplayNameAsync`,
`WebAuthnServiceTests.REG_PM_001_AnAssertionWhoseHandleNamesAnotherAccountIsRefusedAsync`,
`WebAuthnServiceTests.REG_PM_001_AnAssertionWhoseHandleIsNotOneWeIssuedIsRefusedAsync`,
`CredentialServiceTests.REG_PM_001_TheCeremonyCarriesTheAccountsHandleAndPrimaryEmailAsync`.

*Chapter text that should change.* `09` section 4 should give the response shape of
`POST /auth/webauthn/register/begin`, including the `user` object, and say that the
assertion may carry the handle the authenticator returned.

---

## 130. Three privacy refusals take names of their own

**Corrections 1 · 2026-09-22 · D-162 section B, correcting entry 83 · PRIV-CONS-005, PRIV-CONS-008a, PRIV-SENS-002a, `10` section 1.4**

*What D-162 decided.* No `privacy.denied` exists and `authz.denied` is wrong for the
three cases. Three new `10` section 1.4 rows: `privacy.document.notfound` (404),
`privacy.purpose.noconsent` (422), `privacy.notice.unpublished` (409).

*What was built.* The three codes are in the catalogue and in the status table.
`LegalDocumentService` answers `privacy.document.notfound` where the document or the
named version does not exist, on the read and on the translation alike.
`ConsentService.GrantAsync` answers `privacy.purpose.noconsent` where the purpose is
undeclared or rests on another basis, and `privacy.notice.unpublished` where no notice
version has been published. `WithdrawAsync` now makes the same purpose check as the
grant, because D-162 names the withdrawal beside it.

*Decided in the owner's absence.* One point, Tier 2: *what a withdrawal of a consent that
was never granted answers.* D-162 names the undeclared and the non-consent purpose and
nothing else, so the refusal for a declared consent purpose with no record held is left
as it stands. Inventing a fourth code would put a name on the wire that no chapter
carries.

*Tests that pin it.*
`ConsentTests.PRIV_CONS_008a_AC3_APurposeThatTakesNoConsentIsNamedAsSuchAsync`,
`ConsentTests.PRIV_CONS_005_AConsentBeforeAnyNoticeIsPublishedIsRefusedAsync`,
`LegalDocumentTests.PRIV_CONS_005_AC1_AnUnpublishedDocumentIsRefusedAsync`,
`LegalDocumentEndpointTests.PRIV_CONS_005_AC1_AnUnpublishedDocumentIsRefusedAsync`,
`ConsentEndpointTests.PRIV_CONS_008a_AC3_APurposeOnAnotherBasisTakesNoConsentAsync`.

*Chapter text that should change.* `10` section 1.4 needs the three rows, listed under
**Rows for chapter 10**. `09` section 7 should carry the three statuses on the consent
and document endpoints.

---

## 131. The administrative routes conceal nothing from the staff who work them

**Corrections 1 · 2026-09-22 · D-162 section B, correcting entry 93 · PRIV-RIGHT-001, PRIV-RIGHT-002, AUTHZ-CONCEAL-005, `10` section 1.4**

*What D-162 decided.* Under `/admin` nothing is concealed: a missing permission is 403
`authz.denied`, an identifier naming no row is 404 `privacy.request.notfound`, a request
already decided is 409 `privacy.request.decided`. Two new `10` section 1.4 rows.

*What was built.* `DecidableAsync` answers a result rather than a value or nothing, so
the three are distinct where they arise: the scope's own refusal, the absent row, and the
decision that already stands. `FulfilAsync` and `RefuseAsync` return what it gives them.

The reading entry 93 rested on was that AUTHZ-CONCEAL-005 covers these routes. It covers
what a caller with no business with a record is told. A member of staff holding
`privacyrequest:manage` has that business, so the concealment rule reaches the permission
check and stops there.

*Tests that pin it.*
`PrivacyRequestTests.PRIV_RIGHT_002_AC5_ARequestIsDecidedOnceAsync`,
`PrivacyRequestTests.PRIV_RIGHT_001_AC2_TheThreeWaysADecisionIsRefusedAreToldApartAsync`,
`PrivacyRequestTests.PRIV_RIGHT_001_AC2_EnteringWithoutThePermissionIsRefusedAsync`.

*Chapter text that should change.* `10` section 1.4 needs the two rows, listed under
**Rows for chapter 10**. `09` section 8a should carry the three statuses on the decision
endpoints, and say that the concealment rule of AUTHZ-CONCEAL-005 reaches the permission
and not what follows it.

---

## 132. The children's column is a declared category, and its absence is flagged

**Corrections 1 · 2026-09-22 · D-162 section B, correcting entry 104 · PRIV-ROPA-001, PRIV-SENS-001, PRIV-MINOR-001**

*What D-162 decided.* The children's column is true for a purpose exactly when its type
declares the `children` sensitivity category; where `registration.adultaffirmation` is
`off` and no type declares it, the register carries the flag `children-undeclared`
instead of an invented column.

*What was built.* The column reads the purpose's sensitivity categories, as the sensitive
column does, so it is a category like any other. The register gains one flag,
`children-undeclared`, raised once where minors are admitted and no declared type carries
the category. The affirmation no longer reaches any row.

`children` is the one sensitivity category the library reads by name, held as one
constant on the records service and listed under **Rows for chapter 10** beside the new
finding.

*Tests that pin it.*
`ProcessingRecordsTests.PRIV_SENS_001_AC2_TheChildrensColumnFollowsTheDeclaredCategoryAsync`,
`ProcessingRecordsTests.PRIV_ROPA_001_ADeploymentAdmittingMinorsAndDeclaringNoneIsFlaggedAsync`,
`ProcessingRecordsTests.PRIV_ROPA_001_ADeploymentThatDeclaredOneIsNotFlaggedAsync`,
`ProcessingRecordsTests.PRIV_SENS_001_AC2_SensitivityIsAColumnOfItsOwnAsync`.

*Chapter text that should change.* PRIV-ROPA-001 should say the children's column is the
`children` sensitivity category and that its absence under an open registration is
flagged. PRIV-SENS-001 says nothing in the library branches on a category; that sentence
now has one exception, which it should name.

---

## 133. The consent the gate reads is the record's data subject's

**Corrections 1 · 2026-09-22 · D-162 section B, correcting entry 89 · PRIV-SENS-002 AC1, AUTHZ-GATE-005, PRIV-RIGHT-005a**

*What D-162 decided.* The `consent` residual and `privacy.consent.required` are evaluated
against the consent of the record's data subject, for the purpose bound to the action,
whoever the caller is. The data subject is the one the type's encrypted fields name as
their subject. A consent-based purpose bound to a type that names none fails startup
validation. Entry 89 read PRIV-SENS-002 AC1 as a fact about the caller and is superseded.

*What was built.* LIB-HOST-002 AC1 forbids the library a query against a host-owned
table, so the library cannot read the subject column itself. The host reads it and says
who the subject is when it registers the record; the library holds the value on its own
`resources` row, beside the organization it already holds for the same reason. The gate
resolves the record, takes the subject from that row and reads that subject's consent.
A capability page reads one consent per distinct subject on the page rather than one for
the page. Startup validation refuses a deployment that binds a consent-based purpose to
a type whose encrypted fields name no one subject column.

*Two points D-162 does not settle, taken at the strictest reading.* A type whose
encrypted fields name two different subject columns has no one data subject, so it is
treated as naming none and refuses at startup. A check that names no record, an
organization-wide `RequireAsync` or a record the library holds no row for, has no data
subject; a consent-based purpose is refused there rather than admitted on nobody's
consent, which is the admission PRIV-SENS-002 AC1 forbids.

*Tests that pin it.*
`ConsentGateTests.PRIV_SENS_002_AC1_StaffAreGatedByTheRecordsSubjectsConsentAsync`,
`ConsentGateTests.PRIV_SENS_002_AC1_ARecordNamingNoSubjectAdmitsNoConsentedActionAsync`,
`ConsentGateTests.PRIV_SENS_002_AC1_AWrittenConsentAdmitsTheActionAsync`,
`ConsentGateTests.PRIV_SENS_002a_AC2_WithdrawingStopsThePurposeOnTheNextRequestAsync`,
`DeclaredProcessingTests.AUTHZ_MODEL_003_AC2_DeclaringATypeSensitiveChangesWhatItsConsentAsksFor`,
`RegisteredResourceTests.Existing_ARow_CarriesWhatWasWritten`,
`ModelTests.REG_ACCT_001_AC2_NoFieldExistsOutsideTheGroupsTheTableNames`.

*Chapter text that should change.* PRIV-SENS-002 AC1 should say the consent read is the
record's data subject's and that the caller's own consent is never what admits an action.
`03` should say the host supplies the data subject when it registers a record, reading
the column its type declares for its encrypted fields, and that the library holds it on
the `resources` row because LIB-HOST-002 forbids it the host's table. AUTHZ-MODEL-003
should list the new startup refusal, and `10` should carry the `subject` column of
`resources`.

---

## 134. The collation is created in the schema the library owns

**Corrections 1 · 2026-09-22 · D-162 section B, Tier 1 reversal, correcting the phase 1 Tier 1 resolution recorded in `docs/reports/phase-01.md` · OPS-DB-001, OPS-DB-002 AC1**

*What D-162 decided.* `janus_ci` is created in the `janus` schema; `COLLATE
janus.janus_ci` is valid and OPS-DB-002 applies to the collation as it does to every
other object of the library's. Phase 1 had moved it to `public` on the reading that a
column names a collation by one identifier and cannot reach one held elsewhere.

*What was built.* The context creates the collation in the library's own schema. The
migration creates it there, moves `organizations.name` onto it and only then drops the
one in `public`, in that order, because the column depends on the collation it carries.
The schema the collation lives in is now asserted rather than assumed.

*One point the reversal does not settle, taken at the strictest reading.* The provider
writes a column's collation as one quoted identifier, so `COLLATE janus.janus_ci` cannot
be produced by the model's `UseCollation`, which yields `COLLATE "janus.janus_ci"` and
fails with `42704`. The model therefore records the collation's name, `janus_ci`, which
is what it is called, and the one statement that names its schema is written out in the
migration. A later migration that puts a column on this collation writes its own
statement the same way; one that does not fails at once with `42704` rather than
silently taking another collation, so the constraint is loud where it is broken.

*Tests that pin it.*
`SchemaTests.OPS_DB_002_AC1_TheCollationLivesInTheLibrarysSchemaAsync`,
`SchemaTests.OPS_DB_001_AC2_TheCaseInsensitiveCollationIgnoresCaseAsync`,
`SchemaTests.OPS_MIG_007_AC1_TheMigrationsApplyASecondTimeAsync`,
`OrganizationStoreTests.OPS_DB_001_AC2_TheOrganizationNameComparesWithoutRegardToCaseAsync`.

*Chapter text that should change.* OPS-DB-001's values should say the collation is
created in the library's schema and that a column names it by that schema.

---

## 135. The maintenance grants are listed in the serialized model

**Corrections 1 · 2026-09-22 · D-162 section B, Tier 1 reversal, correcting the phase 1 Tier 1 resolution recorded in `docs/reports/phase-01.md` · OPS-MIG-003a AC2, AC4, AUTHZ-MODEL-005**

*What D-162 decided.* The grants of OPS-MIG-003a are listed in the serialized model
output, `artifacts/model.json`, not only in the migration. Phase 1 had read the migration
file as the listing, because the serialized model carried tables, columns, keys and
indexes and neither a function nor a grant.

*What was built.* The serialized model carries a `maintenanceGrants` list: what the
credential may reach, its kind first, and the right it holds on it. It names the two
audit partition functions and the two rights on the wrapped keys, plus the schema usage
both runtime roles need.

*Two points D-162 does not settle, taken at the strictest reading.* The listing is held
beside the serializer in `Janus.Authorization`, not in `Janus.Storage`, because the
serialized model is written there and an area project may not reference the storage
project (CONV-DESIGN-003). Held alone that listing could drift from the migration, so
`DatabaseRoleTests` reads the listing out of the serialized model and compares it with
what the database actually grants `janus_maintenance`: every right on every table,
function and schema of the library's. A grant added, removed or widened by a later
migration and not listed fails that test.

*Tests that pin it.*
`SerializedModelTests.OPS_MIG_003a_AC4_TheMaintenanceGrantsAreListedInTheSerializedModel`,
`DatabaseRoleTests.OPS_MIG_003a_AC4_TheListedGrantsAreTheOnesTheDatabaseHoldsAsync`,
`DatabaseRoleTests.OPS_MIG_003a_AC4_TheMaintenanceRoleReachesTheKeysAndNoOtherTableAsync`,
`SerializedModelTests.AUTHZ_MODEL_005_AC1_OneConfigurationSerializesToTheSameBytes`.

*Chapter text that should change.* AUTHZ-MODEL-005 should say the serialized model also
carries the maintenance credential's grants, which is the only part of it the host does
not declare.

---

## 136. Every gate refusal carries a correlation identifier

**Corrections 1 · 2026-09-22 · D-162 section B, Tier 1 reversal, correcting the phase 2 Tier 1 resolution recorded in `docs/reports/phase-02.md` · AUTHZ-CONCEAL-004, IDN-AUD-001 AC1**

*What D-162 decided.* Every gate refusal carries `correlationId`, including one for an
anonymous principal. Phase 2 had left the refusal of a context naming no account
carrying the code alone, because both identity columns of `janus.audit_records` were
accounts and IDN-AUD-001 AC1 requires both to be populated.

*What was built.* The gate records every refusal and answers every refusal with the
identifier of the row it recorded. A request made under no account, which today is
background work asking as a system principal (IDN-PRIN-001), produces a row naming
neither identity; the absence is the recorded fact, as IDN-AUD-001 already says of the
organization. `ResolveAsync` reads such a row back and explains it with no principal,
which `ExplainedPrincipal` already admitted.

*Two points D-162 does not settle, taken at the strictest reading.* IDN-AUD-001 AC1
still holds of every identity lifecycle event, so the relaxation is granted to the one
action that needs it and to nothing else: a check constraint on `audit_records` admits
an absent identity only where the action is `authz.access.denied`, and the database
refuses every other event that names neither. The mapping of the table stays
non-nullable on both identity columns for that reason: the refusal row is written and
read by the gate's own statements, and every query that mapping serves names a subject
or an action, so a row naming nobody never reaches it. The rollback of the migration
restores the two `NOT NULL` constraints without deleting anything, so a rollback over a
trail that already holds such a refusal fails rather than removing an audit row
(PRIV-RET-002 AC1).

*Tests that pin it.*
`ExplanationTests.AUTHZ_CONCEAL_004_AC1_ARefusalUnderNoAccountCarriesAnIdentifierAsync`,
`ExplanationTests.AUTHZ_IMP_001_AC2_BothIdentitiesAreWrittenToTheAuditRecordAsync`,
`ExplanationTests.AUTHZ_CONCEAL_002_AC1_ARefusalIsTheSameAnswerWhetherTheRecordIsThereAsync`,
`AuditStoreTests.IDN_AUD_001_AC1_AnEventNamingNobodyIsRefusedByTheDatabaseAsync`,
`AuditStoreTests.IDN_AUD_001_AC1_BothIdentityFieldsArePopulatedAsync`.

*Chapter text that should change.* IDN-AUD-001 should say that its AC1 is about identity
lifecycle events and that an authorization refusal made under no account names neither
identity, the absence being the recorded fact. AUTHZ-CONCEAL-004 should say the
identifier is carried whoever asked.

---

## 137. The audit actions are a catalogue, and the closed vocabularies are listed

**Corrections 1 · 2026-09-22 · D-162 section B, Tier 1 reversals · IDN-AUD-001, CONV-NAME-003, `10` section 5**

*What D-162 decided.* `AuditAction` is a closed vocabulary: every member is listed for a
`10` section 5 row and no member is added without being listed. `MessageKind` is
likewise a closed set and its members are listed.

*What was built.* The audit actions were thirty-eight literals spelled in eighteen files
and listed nowhere. They are now one catalogue, `Janus.Core.AuditActions`, built exactly
as `ErrorCodes` is: one documented member per action, every call site naming the member
rather than the code. A contract test holds the catalogue against a written list, so an
action added, removed or respelled fails it. `MessageKind` was already closed and
already pinned member by member; nothing in it changed. Both are listed under **Rows for
chapter 10**, member by member.

*One point D-162 does not settle, taken at the strictest reading.* `AuditAction.Parse`
keeps its shape check and does not refuse a code outside the catalogue, because that is
exactly how `ErrorCode.Parse` and `ErrorCodes` already work: the catalogue plus the
contract test is the mechanism CONV-NAME-003 AC2 uses for the library's other closed
wire vocabulary, and a second mechanism for the same problem would be a second way of
doing something that already has one.

*Tests that pin it.*
`AuditActionsTests.IDN_AUD_001_TheSetOfActionsIsClosed`,
`AuditActionsTests.CONV_NAME_003_AC2_ChangingAnActionFailsTheContractTest`,
`AuditActionsTests.CONV_NAME_003_AC1_EveryActionSaysWhatItRecords`,
`VocabularyContractTests.WireNames_TheKeysTheCatalogueIsAskedBy_AreWritten`,
`VocabularyContractTests.WireNames_EveryVocabularyMember_CarriesOne`.

*Chapter text that should change.* `10` section 5 should carry a subsection listing the
audit actions and one listing the message kinds, from the rows below. CONV-NAME-003
should say the audit actions are catalogued and stable as the error codes are.

---

## 138. The preference types are spelled as the item spells them

**Corrections 1 · 2026-09-22 · D-162 section B, Tier 1 reversal · REG-PREF-001, LIB-HOST-001**

*What D-162 decided.* `PreferenceKind` spells its members as LIB-HOST-001 does: `string`,
`boolean`, `integer`, `enum`. They had been `Text`, `Flag`, `Number` and `Choice`.

*What was built.* The four members are renamed to `String`, `Boolean`, `Integer` and
`Enum`. The wire names, which were already the item's four words, are unchanged and are
now pinned by name like every other vocabulary.

*One point D-162 does not settle, taken at the strictest reading.* `CA1720` fires on
`String` and `Integer`. It is suppressed on the type, with the justification in place,
rather than the names being bent back: REG-PREF-001 names those four types and a host
writes its declaration in those words.

*Tests that pin it.*
`VocabularyContractTests.REG_PREF_001_ThePreferenceTypesAreTheOnesTheItemNames`,
`VocabularyContractTests.WireNames_EveryVocabularyMember_CarriesOne`,
`PreferenceDeclarationsTests.REG_PREF_001_AC1_ADefaultTheKindRefusesFailsStartup`.

*Chapter text that should change.* None; the item already spells them this way.

---

---

## 139. The two browser headers are separate and each is named on the wire

**Corrections 1 · 2026-09-22 · D-162 section C, item 10 · BFF-CSRF-001, BFF-CSRF-003, D-153**

*What D-162 decided.* The synchronizer token travels in `X-Janus-Csrf`; `X-Janus-Request`
stays a presence check. D-153's sentence naming one header is corrected.

*What was built.* The library already carried both headers exactly this way, and
nothing changed. What was missing is that neither wire name was asserted anywhere: both
were constants a rename would have carried silently through every test. Each is now held
against the name the frontend writes.

*Tests that pin it.*
`BrowserProfileTests.BFF_CSRF_003_AC1_TheTwoHeadersAreNamedAsTheFrontendWritesThem`,
`BrowserProfileTests.BFF_CSRF_003_AC1_ARequestWithoutTheCustomHeaderIsRejectedAsync`,
`BrowserProfileTests.BFF_CSRF_001_AC1_AStateChangeWithoutASessionBoundTokenIsRejectedAsync`.

*Chapter text that should change.* D-153's sentence naming one header, as D-162 says.
`10` should carry both header names.

**Superseded by D-163** as to the header names. Applied in entry 166.

---

## 140. The two endpoints the library calls are keys of its own

**Corrections 1 · 2026-09-22 · D-162 section C, item 27 · INT-GEN-001, LIB-EXT-001, `10` section 4**

*What D-162 decided.* The shipped default transports take the `10` keys
`integration.mail.endpoint` and `integration.sms.endpoint`, protected, required only
when the default transport is used. INT-GEN-001's check names the key. There is no
host-declared endpoint register.

*What was built.* The register, `IntegrationEndpoints` and `IntegrationEndpoint`, is
removed from the public surface, and with it the idea that a host lists its own outbound
addresses for the library to check. Startup now reads the two keys and refuses a
deployment whose mail or SMS endpoint is not an absolute `https` address, naming the key
it stopped at. Both keys are protected and default to empty.

*One point D-162 does not settle, taken at the strictest reading.* The shipped default
transports themselves are not built yet, so "required only when the default transport is
used" has nothing to require against today: the key defaults to empty, an empty key is
not checked, and a non-empty one that is not TLS refuses. The transport that reads the
key requires it when it is built. An address that is not an absolute address at all is
treated exactly as a plaintext one, because it is not an address the library will call
either.

*Tests that pin it.*
`SendingValidationTests.INT_GEN_001_AC1_APlaintextEndpointStopsStartupAsync`,
`SendingValidationTests.INT_GEN_001_AC1_EveryEndpointOverTlsStartsAsync`,
`SettingsCatalogueTests.LIB_API_001_AC2_TheKeyNamesAreTheContract`,
`SettingsCatalogueTests.Scope_TheCatalogue_ProtectsTheKeysSectionFourMarks`,
`SettingWrittenFormTests.Written_EveryDefaultOfTheCatalogue_ReadsBackAsItself`.

*Chapter text that should change.* `10` section 4 should carry the two keys, from the
rows below. INT-GEN-001's acceptance criteria should say the error names the key, which
names the integration. LIB-EXT-001 should say where the shipped transports are called.

---

## 141. A reserved value is answered exactly as a held one

**Corrections 1 · 2026-09-22 · D-162 section C, item 35 · REG-IDENT-006, REG-IDENT-001,
REG-SESS-005**

*What D-162 decided.* A removed identifier stays reserved for the undo window, and an
attempt to take it answers exactly as a duplicate does. Entry 35 chose the reservation
and stopped there; the second half is what D-162 adds.

*What was built.* Nothing in the library changed: the path that takes an address already
treats a reserved value and a value another account holds as one outcome, and neither
stages anything nor tells the account asking which it met. What was missing was the test
that holds it there. Two now do, one over the service and one over the real database:
an account offering a value that was given up hours ago is accepted with nothing staged
and nobody told, the value belongs to nobody while the undo can still restore it, and
the moment the window runs out the same offer stages a verification.

*Tests that pin it.*
`IdentifierServiceTests.REG_IDENT_006_AC2_AReservedAddressIsAnsweredAsAHeldOneIsAsync`,
`IdentifierStoreTests.REG_IDENT_006_AC2_AGivenUpValueIsOutOfReachUntilTheUndoLapsesAsync`,
`IdentifierServiceTests.REG_IDENT_001_AC2_ANumberOnOneAccountDoesNotVerifyOnAnotherAsync`.

*Chapter text that should change.* REG-IDENT-006 should say that the value is
unavailable to other accounts until the undo window ends, and that an account offering it
meanwhile is answered as REG-IDENT-001 AC2 answers an account offering a held value.

---

## 142. A dead cookie leaves the request anonymous and one stage requires a session

**Corrections 1 · 2026-09-22 · D-162 section C, item 41 · BFF-ORDER-001 stage 5,
BFF-STEP-001, BFF-CSRF-001, AUTH-SESS-007, API-CONV-003**

*What D-162 decided.* Stage 5 clears a dead cookie and leaves the request anonymous;
the requirement of a session is asserted once in the pipeline for endpoints that need
one and answers 401 `auth.session.expired` there. Entry 41 chose the opposite and is
reversed.

*What was built.* Session resolution no longer answers a cookie that fails to resolve:
it clears the pair, keeps what resolving it answered, and carries the request on as the
request of a browser that carried nothing. A person whose session ended can therefore
reach the endpoints that sign them in again while the browser still holds the dead
cookie, which under entry 41 answered 401 to every endpoint including those. A new
stage, mounted once after the token check, holds the endpoints that answer only a
signed-in person to having a session and answers them all the same way: the refusal
the resolution kept, which carries what has to be done again (BFF-STEP-001 AC3), or the
code alone where the browser never held a session. The thirty-six endpoints that need
one say so where they are mounted, and the thirty-eight refusals their handlers each
carried are gone.

*Three points D-162 does not settle, taken at the strictest reading.*

1. Which endpoints need a session is read from endpoint metadata, which the two gate
   tests of BFF-CSRF-001 AC2 and AUTH-SESS-007 AC2 forbade any file of the boundary to
   read. Those criteria say that no endpoint can opt out of the token requirement or be
   excluded from it; what an endpoint carries here can only add a refusal, never take
   one away, and no stage that enforces the token reads it. The two tests now name the
   two files that read it and stay closed against every other, so a third file reading
   an endpoint still fails them.
2. The endpoints that answer an enrolment session as well as a session are not held to
   one: the eight credential endpoints and the two account endpoints that a browser
   recovering a lost mailbox reaches. They need a session or an enrolment session, which
   is not the requirement this stage asserts, so they keep the answer of their own.
   The endpoint that needs a first contact rather than a session keeps its own likewise.
3. A handler of a held endpoint reads the session through a member that throws where the
   endpoint was mounted without the mark. That is a mounting error, not something a
   caller can produce, and the catalogue test below fails on it before it can ship.

*Tests that pin it.*
`SessionRequirementTests.BFF_STEP_001_TheEndpointsThatNeedASessionAreTheOnesListed`,
`SessionRequirementTests.BFF_STEP_001_AC3_AnEndedSessionIsAnsweredWithWhatMustBeRedoneAsync`,
`SessionRequirementTests.BFF_ORDER_001_AnEndedSessionDoesNotRefuseAnEndpointThatNeedsNoneAsync`,
`SessionRequirementTests.API_CONV_003_ABrowserThatHeldNoSessionIsAnsweredWithTheCodeAloneAsync`,
`BrowserProfileTests.BFF_ORDER_001_AnEndedSessionIsClearedAndLeavesTheRequestAnonymousAsync`,
`BrowserProfileTests.AUTH_SESS_007_AC2_NoEndpointCanOptOut`,
`BrowserProfileTests.BFF_CSRF_001_AC2_NoEndpointCanBeExcludedByConfigurationOrAttribute`.

*Chapter text that should change.* BFF-ORDER-001 should say that stage 5 clears a
cookie that no longer resolves and leaves the request anonymous, and that the
requirement of a session is asserted at stage 8 for the endpoints that carry it.
BFF-CSRF-001 AC2 and AUTH-SESS-007 AC2 should say that no endpoint can be excluded from
a stage, which is what they mean, rather than that no stage reads what an endpoint
carries.

---

## 143. The stream is woken by a database channel and reads back on a key

**Corrections 1 · 2026-09-22 · D-162 section C, item 48 · REG-SESS-003, FE-VER-001,
CONV-DESIGN-008, `10` section 4**

*What D-162 decided.* The registration event stream is driven by PostgreSQL
`LISTEN`/`NOTIFY` raised in the transaction that verifies or completes a step, with a
poll fallback every `registration.events.pollinterval`, a new key with default `PT1S`
and floor `PT1S`. Entry 48 read the state back on a hard-coded second and nothing else,
on the ground that a database channel would be a package the conventions do not admit.
It is not one: the channel is the driver the conventions already name.

*What was built.* The registration session store announces the session on the channel
`janus_registration` whenever it records a change to one or removes one, through the
operation's own connection and inside its transaction, so the database releases the
announcement when that transaction commits and never for one that rolls back. One
listening connection per instance serves every stream that instance holds open, and a
stream's wait ends on the channel or on the interval, whichever comes first. The
interval is now the new key rather than a constant.

*Three points D-162 does not settle, taken at the strictest reading.*

1. The announcement is made by the store rather than by each of the fourteen operations
   that commit a change, because the store is the one place that cannot be forgotten and
   is inside the transaction by construction.
2. A listening connection that drops is not retried in a loop and not logged, because
   `Janus.Storage` has no logger and adding one would be a package the conventions do
   not name. The failure is taken by the next wait, which opens the connection again;
   every wait ends on the interval as well, so a deployment that never hears the channel
   behaves exactly as it did before this change.
3. `Npgsql` is used directly for the listening connection. It is not a new package: it
   is the driver `Npgsql.EntityFrameworkCore.PostgreSQL` carries, which CONV-DESIGN-008
   names for relational access, and no project reference was added.

*Tests that pin it.*
`RegistrationSignalsTests.REG_SESS_003_AWaitNothingSignalsEndsOnTheIntervalAsync`,
`RegistrationSignalsTests.REG_SESS_003_AWaitHearsTheCommittedAnnouncementAndNoOtherAsync`,
`RegistrationFlowTests.REG_SESS_003_TheSignalWakesTheStreamBeforeTheIntervalAsync`,
`RegistrationFlowTests.BFF_CSRF_005b_AC3_TheStreamAndThePollCarryTheSameStateAsync`,
`SettingsCatalogueTests.LIB_API_001_AC2_TheKeyNamesAreTheContract`.

*Chapter text that should change.* `10` section 4 should carry
`registration.events.pollinterval`, from the row below. REG-SESS-003 should say what
drives the stream and that the interval is the fallback.

---

## 144. The photo is the library's and the codec is the host's

**Corrections 1 · 2026-09-23 · D-162 section C, item 55 · IDN-ATTR-002, IDN-ATTR-003,
IDN-ATTR-004, PRIV-RIGHT-005 AC4, LIB-HOST-001, `09` section 6, `10` sections 1.1 and 4**

*What D-162 decided.* The photo stays in the library and the codec does not: a
host-declared image codec, optional in LIB-HOST-001, validates by content, bounds and
re-encodes to JPEG with metadata removed; the library stores what it returns, encrypted;
while any policy enables photos and no codec is declared, startup fails with
`model.startup.declarationmissing`. The photo endpoints and their three codes are built.
Entry 55 built none of them, on the ground that no permitted package decodes an image.

*What was built.* `ImageCodec` is a declaration of the same shape as the challenge
verifier and the phone signal provider: one callback taking the uploaded bytes and the
longest side the stored image is held to, answering the re-encoded image or nothing.
Nothing the callback answers chooses a code: bytes it refuses are
`identity.photo.invalid`, whatever the request called them. `GET`, `PUT` and
`DELETE /account/photo` are mounted as endpoints that need a session. `GET` serves the
stored JPEG with `Cache-Control: no-store` and no entity tag, and answers 404 where the
account shows none and where the policy shows none, in the same bytes. `PUT` refuses an
upload longer than `photo.maxbytes` with `identity.photo.toolarge` before the codec
reads it, hands the codec `photo.maxdimension`, and stores what the codec answered.
Setting and giving up a photo are audited as profile changes.

*Five points D-162 does not settle, taken at the strictest reading.*

1. **Availability is a key of its own, not a seventh field of the policy object.**
   `10` section 4.1a states six fields and says `stepup.enforcement.<organization>` is
   not one of them, which is the precedent for an organization-scoped value outside the
   object. The new family is `photo.enabled.<organization>`, runtime, default `false`.
   IDN-ATTR-002's "enabled for the administrative organization" is therefore a bootstrap
   value, exactly as that organization's policy is, and no code special-cases it.
2. **An account of no organization shows no photo, and an account of several shows one
   only where every one of them shows one.** AUTH-PRIN-002 resolves several memberships
   to the strictest, and "disabled elsewhere" reads on an account that belongs nowhere.
3. **The upload is the request body, not a field of a document.** IDN-ATTR-004 decides
   what an upload is by reading it, so the endpoint takes the bytes, reads at most the
   ceiling of `photo.maxbytes` and one chunk of them, and says nothing about the content
   type the request claimed.
4. **A removal is not held to the policy.** An organization that stops showing photos
   withholds the image from every read of it, and the account can still take it down:
   refusing the removal would leave an image its subject could never take down again,
   which IDN-PRIN-003 does not intend and no chapter asks for. Setting one is refused,
   reading one answers as no photo.
5. **A subject whose key is erased shows no photo rather than failing on ciphertext.**
   PRIV-RIGHT-005 AC4 says `GET /account/photo` answers as for an account with no photo;
   the photo port throws on an erased key by contract, so the directory reads the
   subject key first and answers empty where it is gone, which needs no catch and
   weakens nothing (D-157).

*One addition to a public interface.* The startup check has to know whether any
organization shows photos, which no read of one member can answer, so
`IConfigurationStore` gains `ReadWrittenAsync`: every member of a family the deployment
has written a value for. It is one method, it reads, and the admin configuration screens
of a later phase need the same answer.

*Tests that pin it.*
`ProfilePhotosTests.IDN_ATTR_002_AC1_AnAccountInNoOrganizationShowsNoPhotoAsync`,
`ProfilePhotosTests.IDN_ATTR_002_AC2_AnOrganizationIsGivenPhotosByItsKeyAloneAsync`,
`ProfilePhotosTests.IDN_ATTR_002_AnOrganizationThatShowsNoPhotoWithholdsItFromItsMembersAsync`,
`ProfilePhotosTests.IDN_ATTR_002_AnImageIsWithheldByAPolicyAndStillGivenUpByItsAccountAsync`,
`ProfilePhotosTests.LIB_HOST_001_AnUploadIsRefusedWhereTheDeploymentDeclaredNoCodecAsync`,
`ProfilePhotosTests.IDN_ATTR_004_AC1_AnUploadTheCodecDoesNotRecogniseIsRefusedAsync`,
`ProfilePhotosTests.IDN_ATTR_004_AC2_WhatIsStoredIsWhatTheCodecAnsweredAsync`,
`ProfilePhotosTests.IDN_ATTR_004_TheCodecIsHandedTheConfiguredLongestSideAsync`,
`ProfilePhotosTests.IDN_ATTR_004_AnUploadOverTheConfiguredLengthIsRefusedUnreadAsync`,
`ProfilePhotosTests.IDN_ATTR_003_AnAccountGivesUpTheImageItShowsAsync`,
`ProfilePhotosTests.IDN_AUD_001_SettingAndGivingUpAPhotoAreRecordedAsProfileChangesAsync`,
`PhotoFlowTests.IDN_ATTR_002_AnAccountThatShowsNoPhotoAndOneWithNoPolicyAnswerAlikeAsync`,
`PhotoFlowTests.IDN_ATTR_004_AnUploadIsStoredReencodedAndServedAsAJpegAsync`,
`PhotoFlowTests.IDN_ATTR_003_AC3_TheImageIsServedWithNothingACacheCouldShareAsync`,
`PhotoFlowTests.IDN_ATTR_002_AnUploadIsRefusedWhereTheOrganizationShowsNoPhotoAsync`,
`PhotoFlowTests.IDN_ATTR_004_AC1_BytesTheCodecRefusesAreRefusedWhateverTheRequestCalledThemAsync`,
`PhotoFlowTests.IDN_ATTR_004_AnUploadOverTheConfiguredLengthIsRefusedAsync`,
`PhotoFlowTests.IDN_ATTR_003_AnAccountGivesUpTheImageItShowsAsync`,
`SubjectEraserTests.PRIV_RIGHT_005_AC4_TheDirectoryShowsAnErasedSubjectAsOneWithNoPhotoAsync`,
`StartupValidationTests.IDN_ATTR_002_ADeploymentThatShowsPhotosWithNoCodecIsRefusedAsync`,
`StartupValidationTests.IDN_ATTR_002_ADeploymentThatShowsPhotosAndDeclaredACodecStartsAsync`,
`SessionRequirementTests.BFF_STEP_001_TheEndpointsThatNeedASessionAreTheOnesListed`,
`SettingsCatalogueTests.LIB_API_001_AC2_TheFamiliesAreTheContract`,
`ErrorCodesTests.CONV_NAME_003_AC2_ChangingACodeFailsTheContractTest`.

*Chapter text that should change.* LIB-HOST-001 should carry the image codec row below.
`10` section 4 should carry `photo.enabled.<organization>`, and its section 4.1a should
say that the key is not a field of the policy object, as it already says of
`stepup.enforcement.<organization>`. IDN-ATTR-002 should say that the administrative
organization's key is written at bootstrap. IDN-ATTR-004 should say that the codec is the
host's and that the library stores what it answers. `09` section 6 should say that the
upload is the request body and that `DELETE` is not held to the policy. `10` section 1.1
should drop the **(new)** mark from the three photo codes, which are now raised.

---

## 145. The registry is the list, and the default is a client it holds

**Corrections 1 · 2026-09-23 · D-162 section C, item 60 · API-REDIR-001, API-REDIR-002,
LIB-HOST-001 AC3, `10` sections 1.5 and 4**

*What D-162 decided.* API-REDIR-001's configured list is the set of origins of the
registered clients' return addresses, and the default is a named first-party client in a
new key `redirect.defaultclient`, validated against the registry at startup. Entry 60
chose the other reading: no key at all, and the list read from whatever each endpoint's
own chapter already registered, `webauthn.origins` at a landing and the client's one
registered address at the authorization endpoint. D-057 says it in as many words: the
registry is where the browser applications are registered, and it is therefore the
source of both the valid destinations and the origin list, which cannot drift apart.

*What was built.* `RedirectValidation` reads the registry and the key once, before the
web server starts, as the five checks of D-160 already do, and is registered behind
them. A registered client whose return address is not an absolute address with a host
stops the deployment with `model.startup.redirectclient` and `details.client` naming it.
Where `redirect.defaultclient` is set and names no registered browser application, the
same code stops it with `details.key` naming the key. Registration resolves the key at
capture: an identifier the registry does not hold is stored as the default client, and
the completion returns the person to the address the registry holds for it, so the
resolution happens once and the last step has nothing left to validate. The
`webauthn.origins` check stays where it is; it was AUTH-FACT-010's own and only its
citation was wrong.

*Three points D-162 does not settle, taken at the strictest reading.*

1. **The key is optional, and a deployment that names no default starts.** LIB-HOST-001
   AC3 states that no key outside its list fails startup when unset, and D-162 adds the
   key to `10` section 4 and not to LIB-HOST-001. A deployment that names none falls
   back to nothing and the frontend decides where the person goes, which is what entry
   61 settled for the sign-in address and what the completion already carried.
2. **The default names a browser application or startup refuses it.** D-162 says a
   first-party client, and every registered client is first-party (D-057), so the word
   that does work is the kind: a protocol client's address is a token endpoint's
   callback and no place to land a person. Refusing is the reading that grants least.
3. **The authorization endpoint still replaces an unknown `redirect_uri` with the
   requesting client's own registered address, not with the default.** Sending an
   authorization code to another application's address is worse than sending it to the
   one that asked, the exact match of RFC 9700 is what chapter 02 requires there, and
   AUTH-OIDC-001 AC2 refuses an unregistered client outright rather than forwarding it
   anywhere. The default is for the flows that carry no code.

*What is not checked.* The address is held to being absolute with a host and to nothing
else. No chapter asks a client's return address to be TLS, as INT-GEN-001 asks of the
two integration endpoints, and refusing one would be a behaviour no chapter describes.

*Tests that pin it.*
`RedirectValidationTests.API_REDIR_001_AC3_ARegistryOfAbsoluteOriginsStartsAsync`,
`RedirectValidationTests.API_REDIR_001_AC3_AnEntryThatIsNotAnAbsoluteOriginFailsAsync`,
`RedirectValidationTests.LIB_HOST_001_AC3_ADeploymentThatNamesNoDefaultStartsAsync`,
`RedirectValidationTests.API_REDIR_001_ADefaultNamingNoRegisteredClientIsRefusedAsync`,
`RedirectValidationTests.API_REDIR_001_ADefaultNamingAProtocolClientIsRefusedAsync`,
`RedirectValidationTests.API_REDIR_001_ADefaultNamingARegisteredApplicationStartsAsync`,
`StartupValidationTests.API_REDIR_001_ADeploymentNamingADefaultClientTheRegistryLacksIsRefusedAsync`,
`RegistrationServiceTests.API_REDIR_002_AC2_AnUnrecognisedIdentifierIsStoredAsTheNamedDefaultAsync`,
`RegistrationServiceTests.API_REDIR_002_AC2_AnUnrecognisedIdentifierIsTheDefaultAndNoRefusalAsync`,
`RegistrationServiceTests.API_REDIR_002_AC4_TheReturnIsTheStoredClientsAndNoOthersAsync`,
`OidcFlowTests.API_REDIR_001_AC1_AnUnknownDestinationIsReplacedAndLoggedAsync`,
`OidcFlowTests.API_REDIR_001_AC4_OnlyTheReplacedDestinationIsRecordedAsync`,
`OidcServiceTests.API_REDIR_001_AC2_ADestinationContainingAKnownOneIsNotAcceptedAsync`,
`RelyingPartyTests.AUTH_FACT_010_AnEntryThatIsNotAnAbsoluteOriginFails`,
`SettingsCatalogueTests.LIB_API_001_AC2_TheKeyNamesAreTheContract`,
`ErrorCodesTests.CONV_NAME_003_AC2_ChangingACodeFailsTheContractTest`.

*Chapter text that should change.* API-REDIR-001 should say that the list is the origins
of the registered clients' return addresses and that the default is
`redirect.defaultclient`, and AC3 should say that it is the registry that is read at
startup. It should also say that the authorization endpoint replaces an unknown
destination with the requesting client's own, the default standing for the flows that
carry no code. API-REDIR-002 AC2 should say that the default is the configured client
and that a deployment naming none stores nothing. `10` section 4 should carry
`redirect.defaultclient` and section 1.5 `model.startup.redirectclient`, both below.

---

## 146. A reported change of SIM withholds the entry that rides the number

**Corrections 1 · 2026-09-23 · D-162 section C, item 68 · Tier 3 · AUTH-FACT-002b AC6,
AUTH-FACT-003, AUTH-ABUSE-003 AC1, AUTH-FACT-001 AC1**

*What D-162 decided.* On a `risk` answer the SMS factor is withheld for that sign-in and
the challenge offers the account's other factors; `phoneLink` alone is refused with
`auth.factor.rejected`. Entry 68 chose the other reading, that the answer is recorded and
nothing else, on the ground that no chapter named a refusal or a code for one. D-162
names both.

*What was built.* `PhoneSignals` gains one answering method: it asks the provider the
deployment declared and answers whether the entry may still be used, false only on
`risk`. The second-step challenge asks it for every entry a text carries that it was
about to offer, drops the ones it refuses, and offers what is left. A sign-in link by
text asks it before anything goes out. The consideration is recorded where it refuses,
because nothing is sent after that and the send is where a consideration is otherwise
written down, so one use of a restricted entry still writes one row.

*Three points D-162 does not settle, taken at the strictest reading (Tier 3: a signal
that decides what is refused is security semantics).*

1. **The refusal of a sign-in link is decided on the number, not on the account.**
   AUTH-ABUSE-003 AC1 states that responses for existing and non-existent addresses are
   byte-identical. A refusal read off the account would tell an enumerator that a risky
   number belongs to one. The signal is therefore asked for every phone channel the ask
   resolves to a channel at all, whether or not an account holds it, and the same
   refusal is answered either way. Both texts hold, and nothing reaches the number.
2. **Withholding the only second step refuses the sign-in; it never completes it.**
   Where the account's own second step is the one the signal refused, there is nothing
   left to present, and a challenge with nothing to present must not fall through to a
   session at the level the second step was there to raise. It is refused with
   `auth.factor.rejected`, the code D-162 names for the parallel case. A sign-in that
   needed no second step is untouched: the refusal fires only where one was offered.
3. **Which entries a text carries is the catalogue's, not a name in a rule.**
   AUTH-FACT-001 AC1 admits one conditional in the library that tests for an entry by
   name, in the policy object. The withholding therefore reads
   `FactorCatalogue.Of(entry).Restricted`, which AUTH-FACT-002b AC5 makes exactly the
   entries a text carries, so the rule names no factor and an entry added to the
   catalogue is covered without a code change.

*What is not changed.* The consideration at the send stays where it is and still covers
every restricted send the sign-in path does not govern, which is what AUTH-FACT-002b AC6
asks for. Recovery and enrolment are not touched: D-162 speaks of a sign-in, and
refusing an enrolment on the same signal would decide behaviour no chapter describes.

*Tests that pin it.*
`AuthenticationServiceTests.AUTH_FACT_002b_AC6_AReportedChangeWithholdsTheTextCodeAndOffersTheRestAsync`,
`AuthenticationServiceTests.AUTH_FACT_002b_AC6_AnAnswerThatReportsNoChangeLeavesTheTextCodeOnOfferAsync`,
`AuthenticationServiceTests.AUTH_FACT_002b_AC6_AReportedChangeRefusesASignInWhoseOnlySecondStepIsTheTextCodeAsync`,
`AuthenticationServiceTests.AUTH_FACT_002b_AC6_AWithholdingIsRecordedWithTheEntryAndNotTheNumberAsync`,
`AuthenticationServiceTests.AUTH_FACT_002b_AC6_AReportedChangeRefusesASignInLinkByTextAsync`,
`AuthenticationServiceTests.AUTH_ABUSE_003_AC1_ANumberNoAccountHoldsIsRefusedInTheSameBytesAsync`,
`FactorCatalogueTests.AUTH_FACT_001_AC1_NoConditionalTestsForAFactorByName`,
`SendingServiceTests.AUTH_FACT_002b_AC6_TheSignalIsConsideredBeforeARestrictedFactorGoesAsync`,
`SendingServiceTests.AUTH_FACT_002b_AC6_AnAbsentProviderIsItselfRecordedAsync`.

*Chapter text that should change.* AUTH-FACT-002b AC6 should say what a `risk` answer
does: the entry is withheld from that sign-in, the challenge offers what is left, and a
sign-in with nothing left is refused with `auth.factor.rejected`. It should say that the
question is asked of the number and that the answer to an ask is the same whether or not
an account holds it, so AUTH-ABUSE-003 AC1 still holds. `09` section 3 should add
`auth.factor.rejected` to what `POST /auth/link` can answer.

---

## 147. A purpose names the document that governs its consent

**Corrections 1 · 2026-09-23 · D-162 section C, item 82 · PRIV-CONS-001 AC2,
PRIV-CONS-005, PRIV-CONS-007, LIB-HOST-001, `09` section 7**

*What D-162 decided.* A purpose declaration names the document that governs its
consent, defaulting to the privacy notice; a material revision of a document supersedes
the live consents of the purposes that name it; the consent record names that document's
version. Entry 82 chose the other reading, that the privacy notice governs every consent
and a revision of any other document supersedes none, on the ground that nothing
declared which purposes a document covered. D-162 makes the purpose declaration that
place.

*What was built.* `PurposeDeclaration` and the resource builder take a `document`, and
`DeclaredPurpose` carries it; absent, the privacy notice governs. A grant reads the
current version of that document and refuses with `privacy.notice.unpublished` where it
has none, which is the rule the notice already carried, now read of whichever document
governs. Publication hands the document and the version to supersession, which resolves
the purposes the document governs from the declaration and ends their live consents
recorded against an earlier version. `IConsentStore.LiveAgainstAnotherAsync` takes those
purposes, because a version counter is per document: two documents both stand at `1`,
and without the purposes a revision of one would end the consents given against the
other.

*Four points D-162 does not settle, taken at the strictest reading.*

1. **The record's field keeps the name `noticeVersion`.** `09` section 7 gives
   `GET /privacy/consents` the field by that name, and a chapter wins. What changed is
   what goes in it: the version of the document that governs the consent, which for a
   purpose naming none is still the notice's, unchanged.
2. **A purpose declared on two types against two documents fails startup.** One purpose
   is one thing to the person exercising a right over it, and the document a consent is
   recorded against has to be the document a revision of it ends. A declaration that
   says two is a deployment that cannot answer which revision ends the consent, so it
   is refused where the two-bases disagreement already is, in `DeclaredProcessing`. A
   purpose naming a document on one type and nothing on another is the same
   disagreement, because naming nothing names the notice.
3. **An objection still stands against the notice.** A purpose on an objectable basis
   holds no consent, so it has no consent for a document to govern; the objection record
   goes on naming the notice version in force, which is what PRIV-RIGHT-001a has it do.
4. **A document no purpose names supersedes nothing, and so does a material revision of
   it.** Nothing about a document is declared except by the purposes that name it, so
   the terms of service end no consent unless a purpose says they govern one.

*Tests that pin it.*
`SupersessionTests.PRIV_CONS_007_AConsentNamesTheVersionOfItsOwnGoverningDocumentAsync`,
`SupersessionTests.PRIV_CONS_007_AC1_AMaterialRevisionEndsTheConsentsOfThePurposesNamingItAsync`,
`SupersessionTests.PRIV_CONS_007_AC2_ARevisionOfTheNoticeLeavesAPurposeNamingAnotherDocumentAsync`,
`SupersessionTests.PRIV_CONS_005_AGrantIsRefusedBeforeItsGoverningDocumentIsPublishedAsync`,
`SupersessionTests.PRIV_CONS_007_AC1_AMaterialChangeIdentifiesWhoMustBeAskedAgainAsync`,
`SupersessionTests.PRIV_CONS_007_AC2_AMaterialRevisionOfAnotherDocumentEndsNoConsentAsync`,
`SupersessionTests.PRIV_CONS_007_AC2_OnlyTheConsentBasedPurposesAreSuspendedAsync`,
`DeclaredProcessingTests.PRIV_CONS_007_APurposeDeclaredAgainstTwoDocumentsIsRefused`,
`DeclaredProcessingTests.PRIV_CONS_007_APurposeDeclaredAgainstOneDocumentTwiceStands`,
`ConsentStoreTests.PRIV_CONS_007_AC1_OnlyLiveConsentsAgainstAnEarlierVersionAreFoundAsync`.

*Chapter text that should change.* PRIV-CONS-007's values note should say that a
material revision supersedes the live consents of the purposes that name the document,
and that a purpose naming none is governed by the privacy notice, rather than "the
purposes the document covers". PRIV-CONS-001 AC2 should say that the version resolves
to the exact text of the governing document. LIB-HOST-001's purpose declaration should
carry the governing document. `09` section 7 should say that `noticeVersion` carries the
version of the document that governs the purpose.

---

## 148. The dashboard records whether it was asked again

**Corrections 1 · 2026-09-23 · D-162 section C, item 87 · PRIV-CONS-001 AC1,
PRIV-CONS-007, `09` section 7, `10` section 5.21**

*What D-162 decided.* A grant for a purpose on which the subject holds a superseded,
unwithdrawn consent records `reconsent`; any other grant records `dashboard`. Entry 87
chose the other reading, that the endpoint always records `dashboard` and `reconsent` is
left to a host calling the contract, on the ground that which surface asked is the
frontend's fact. D-162 makes it the library's, and it is derivable without asking the
frontend anything.

*What was built.* `POST /privacy/consents/{purpose}/grant` reads the subject's records
first and names `reconsent` where the one for that purpose was ended by a revision and
never taken back, and `dashboard` otherwise. Nothing else changes: the endpoint that
serves the first grant is the endpoint that serves the prompt, which is why the record
has to tell them apart.

*Two points D-162 does not settle, taken at the strictest reading.*

1. **The derivation is at the endpoint, not in the consent service.** `09` section 7
   gives the grant endpoint no body, so the mechanism there is the library's to choose;
   everywhere else it is the caller's. Putting the rule in the service would have it
   overwrite what a caller named, so a host recording an administrator's grant over a
   superseded consent would lose the administrator. The service goes on recording what
   it is told, and `registration` and `administrator` are untouched.
2. **Withdrawn beats superseded.** A consent the subject took back before a revision
   ended it raises no prompt to answer, so granting it again is an ordinary dashboard
   grant. D-162's "superseded, unwithdrawn" says exactly this, and the withdrawal is
   the subject's own act while the supersession is not.

*Tests that pin it.*
`ConsentEndpointTests.PRIV_CONS_001_AC1_AGrantOverASupersededConsentIsRecordedAsReconsentAsync`,
`ConsentEndpointTests.PRIV_CONS_001_AC1_AGrantOverAWithdrawnConsentIsRecordedAsTheDashboardAsync`,
`ConsentEndpointTests.PRIV_CONS_011_AC1_EveryConsentHeldIsVisibleToItsSubjectAsync`.

*Chapter text that should change.* `09` section 7 should say that the grant endpoint
records `reconsent` where the subject holds a superseded, unwithdrawn consent for the
purpose and `dashboard` otherwise, and that a host calling the contract names its own
mechanism. `10` section 5.21's `reconsent` row should say the library writes it.

---

## 149. Reactivation's body is `linkToken`, as it was built

**Corrections 1 · 2026-09-23 · D-162 section C, item 98 · `09` sections 6 and 6a,
IDN-LIFE-013**

*What D-162 decided.* `POST /account/reactivate` takes `linkToken`; the `09` block is
corrected. That is the reading entry 98 took, so nothing in the code changes: the
endpoint reads `linkToken`, answers `api.request.malformed` naming `linkToken` where the
member is absent, and the four link-borne account paths keep one request shape between
them.

*What was built.* Nothing new. This entry records that D-162 settled the point the way
entry 98 did, so the owner's reconciliation pass corrects the chapter's JSON block
rather than the code.

*Tests that pin it.*
`AccountLifecycleFlowTests.IDN_LIFE_013_AC1_TheNoticesLinkStandsTheAccountBackUpAsync`,
`AccountLifecycleFlowTests.IDN_ACCT_007_AC4_TheLinkEndsTheWindowAsync`.

*Chapter text that should change.* The `POST /account/reactivate` entry's JSON block
should read `{ "linkToken": "..." }`.

---

## 150. The export carries the credentials group and the whole standing group

**Corrections 1 · 2026-09-23 · D-162 section C, item 102 · PRIV-RIGHT-003,
REG-ACCT-001, REG-SESS-007**

*What D-162 decided.* The export carries every group `GET /account` shows the person,
credentials included, and the whole Standing group. Entry 102 was right that the three
things PRIV-RIGHT-003 names are a floor and not the whole, and wrong to leave the
credentials out: what signs in to an account is held about the person, and REG-ACCT-001
puts it in the table the export answers from. Nothing the person may see is withheld
from a copy of their own record.

*What was built.* The sections are now, in order: `account`, `profile`, `identifiers`,
`identifier-backup`, `credentials`, `recovery-codes`, `devices`, `preferences`,
`memberships`, `grants`, `assurance`, `sessions`, and then `consents` and `objections`
from the export service as before.

- `credentials` carries one record per enrolment, by identifier, catalogue entry,
  label, state, whether it is the second step offered first, when it was added, when it
  was last presented, when a reported loss invalidates it, and the two WebAuthn backup
  properties. The password is one record of the group, carrying when it was set and
  whether a change is required. No secret material crosses: not a hash, not a TOTP
  secret, not a public key, not a credential identifier.
- `recovery-codes` carries how the set stands (remaining, generated, viewed, exported)
  and never a code of it, which is all AUTH-FACT-008 AC2 admits.
- `devices` carries the browsers the account is known at, by label, kind and the three
  instants, never the fingerprint the row is matched by.
- `memberships` carries every membership including one that has ended, with the instant
  it ended on it, because the row is held either way.
- `grants` carries the live grants the account holds itself, in every organization its
  memberships name. A grant a group holds is the group's record and is not carried
  here.
- `assurance` carries the tier the account can reach with what still stands against it,
  and whether what reaches it resists relay.
- `account` gains the terms step's record (REG-SESS-007): the terms version accepted,
  the notice version presented, when the age screen was answered, and the affirmation
  it derived or the band recorded in its place.

*Three points decided inside the item.* Revoked and expired grants are not carried: the
standing group is where the account stands, and `IGrantStore.HeldByAsync` is the read
that answers it. Ended memberships are carried, because the membership row persists by
IDN-MEM-001 and ending it is an instant on the record rather than a removal. The
preferred second step is derived through `SecondStep.Preferred`, not read off the
stored flag, so the export and the account page agree on which one is offered first.

*Tests that pin it.*
`ExportSourceTests.REG_ACCT_001_AC1_TheExportCarriesTheCredentialsTheAccountShowsAsync`,
`ExportSourceTests.REG_ACCT_001_TheExportCarriesTheWholeStandingGroupAsync`,
`ExportSourceTests.REG_SESS_007_AC2_TheExportCarriesTheNoticeAndAffirmationRecordsAsync`,
`ExportSourceTests.PRIV_RIGHT_003_AnAccountThatHasSettledNothingStillExportsAsync`,
`ExportSourceTests.PRIV_RIGHT_003_AC3_TheExportCarriesThePreferencesIdentifiersAndSessionsAsync`.

*Chapter text that should change.* PRIV-RIGHT-003 should say that the export carries
every group of REG-ACCT-001 the person may see and the whole of the standing group, and
list the sections above, in place of the three it names now. The sentence entry 102
asked for, that credentials are not among them, should not be written.

---

## 151. The three rows the library makes true are applied; the rest of the register is offered

**Corrections 1 · 2026-09-23 · D-162 section C, item 103 · PRIV-ROPA-002,
PRIV-ROPA-003, chapter 05 sections 7 and 8**

*What D-162 decided.* The shipped provider register applies by default only the rows
the library itself makes true (password screening while online screening is configured,
the hosting provider from `hosting.location`, the mail server while mail is configured)
and offers the rest; `location` stays `inside` and `outside`, resolved from
`hosting.location` at generation.

*What entry 103 had.* All seven rows were offered and none applied, on the ground that a
register naming a processor the deployment does not have is a false statement to a
regulator. That holds for the payment provider, the shipping provider, the SMS gateway
and the developer, which the library cannot know a deployment has. It does not hold for
three rows the library knows about: the deployment is hosted somewhere, and where
`integration.mail.endpoint` is set or `password.blocklist.source` is `rangeApi` it is
the library itself that calls those two providers. Leaving them out was the
under-report, and PRIV-ROPA-003 requires the screening call to appear.

*What was built.* `ProcessingRecordsService` reads `integration.mail.endpoint` and
`password.blocklist.source` at generation and adds to the declared recipients, from
`ProviderRegister.Default`:

- `hosting provider`, always;
- `mail server`, while `integration.mail.endpoint` is not empty, which is what marks a
  deployment as using the library's own transport rather than one of its own
  (INT-GEN-001);
- `password screening`, while `password.blocklist.source` is `rangeApi`. `offline` is a
  local copy and `selfHosted` is the deployment's own corpus, so neither reaches a
  recipient at all.

Each ships with no agreement reference, so each processor among them is flagged until
the deployment gives it one. `password screening` is characterised as a recipient and
not a processor, so no agreement is asked of it.

*Two points decided inside the item.* A deployment that has declared a row of the same
name has edited the shipped default, so its own row stands in place of the applied one
and not beside it; the match is on the name, ignoring case, because the name is the
register's column and editing a default means filling in its reference rather than
renaming it. The `location` half of D-162's sentence needed no change: the declaration
field is `inside` and `outside`, a row that states neither is read as the hosting
location at generation, and `password screening` is the one shipped row fixed outside.

*Tests that pin it.*
`ProcessingRecordsTests.PRIV_ROPA_002_TheRowsTheLibraryMakesTrueAreAppliedWithoutADeclarationAsync`,
`ProcessingRecordsTests.PRIV_ROPA_002_TheRestOfTheShippedRegisterIsOfferedAndNotAppliedAsync`,
`ProcessingRecordsTests.PRIV_ROPA_002_AnUncalledProviderIsNotInTheRegisterAsync`,
`ProcessingRecordsTests.PRIV_ROPA_002_AC2_AnEditedRowStandsInPlaceOfTheShippedDefaultAsync`,
`ProcessingRecordsTests.PRIV_ROPA_002_AC1_EveryRecipientAppearsAndAProcessorWithoutAnAgreementIsFlaggedAsync`,
`ProcessingRecordsTests.PRIV_ROPA_003_AC1_ThePasswordScreeningCallAppearsAsACrossBorderTransferAsync`.

*Chapter text that should change.* PRIV-ROPA-002 should say that the library applies the
three rows it makes true (the hosting provider, and the mail server and the screening
service where the deployment uses them) and offers the other four as defaults the host
declares from, that an unstated location is the hosting location at generation, and that
`password screening` is fixed outside. Chapter 05 section 8's "Location field" column
should read `inside` or `outside` for every row that states one and "follows hosting"
for every row that does not.

---

## 152. The four credential events of section 5b are emitted

**Corrections 1 · 2026-09-23 · D-162 section D · AUTH-STEP-007, AUTH-RECOV-007,
`10` section 5b**

*What D-162 decided.* Build now, in the phase whose item it is: `CredentialEnrolled`
(AUTH-STEP-007); `CredentialSuspended`, `CredentialRestored`, `CredentialInvalidated`
(AUTH-RECOV-007). All four were in the emitted contract of `10` section 5b and none was
built.

*What was built.* Four public events on `JanusEvent`, each carrying the credential
identifier, its catalogue entry, and the subject, and none carrying material that
proves anything.

- `CredentialEnrolled` is published from the one place every completed enrolment passes
  through, after the notice to every recorded channel and after the enrolment session
  ends, so a refusal to take the event cannot cost the notification AUTH-STEP-007 AC1
  requires.
- `CredentialSuspended` carries when the window ends, which is the fact a loss report
  states; it is published where a report is opened, whether by a report or by a removal
  that would lower reachable assurance.
- `CredentialRestored` is published where a report is cancelled, from the link or from
  a session, and only where there is still a credential to restore.
- `CredentialInvalidated` is published where the window completes, which AUTH-RECOV-007
  makes the one point at which reachable assurance is recomputed.

The invalidation sweep now carries a `Result<int>` through `CarryAsync` and
`InvalidateAsync`, so a refused announcement stops `AdvanceAsync` with the refusal
rather than being counted as work carried.

*One point decided inside the item.* Each event's idempotency key is the credential and
the instant, not the credential alone: one credential may be reported lost, cancelled
and reported again, and three reports of one credential are three facts.

*Tests that pin it.*
`CredentialServiceTests.AUTH_STEP_007_AnEnrolmentThatReachedActiveIsAnnouncedAsync`,
`LossReportsTests.AUTH_RECOV_007_EachTurnOfAReportIsAnnouncedAsync`,
`LossReportsTests.AUTH_RECOV_007_ASweepWhoseAnnouncementIsRefusedAnswersWithTheRefusalAsync`.

*Chapter text that should change.* None. `10` section 5b already names all four and
says what each is raised for.

---

## 153. Startup verifies the schema, and refuses only a database behind the model

**Corrections 1 · 2026-09-23 · D-162 section D · OPS-MIG-002, OPS-MIG-001, OPS-MIG-005,
`10` section 1.5**

*What D-162 decided.* Build now, in the phase whose item it is:
`model.startup.schemamismatch` raised by the phase 1 schema check. Phase 1 deferred
OPS-MIG-002 for want of a startup path; phase 6 built one, and the check was never
picked up.

*What was built.* A check over the library's own context that reads the migration
history against the migrations this build declares, registered as the first hosted
service of the six that already run before the host's web server. A database owing at
least one migration stops the application with `model.startup.schemamismatch` and the
names of the migrations owed under `pending`. The check issues no DDL and applies
nothing, so the database it refused is as un-migrated afterwards as it was before, which
is what OPS-MIG-001 AC1 asks of a starting application.

*Decided in the owner's absence (Tier 3, strictest reading).* A startup gate is security
semantics, so the reading that refuses most was taken as far as the chapters allow.

- **Reading one.** "The schema matches the model" is read both ways: a database owing a
  migration and a database holding one this build does not declare are both mismatches
  and both refuse.
- **Reading two.** Only a database behind the model refuses.

Reading two was taken. OPS-MIG-005 says every migration "SHALL work against the
**previous** application version" and that "Both versions run briefly during rollout",
and OPS-MIG-001 leaves migration to the pipeline, which runs before the deployment. A
schema ahead of the model is therefore the ordinary state of every instance of the
outgoing version during a rollout, and refusing it would stop the running application on
every deploy, which no chapter asks for. Reading one is the stricter of the two but
contradicts a chapter that speaks; reading two is the strictest reading left. Within
reading two the gate still fails closed: any pending migration at all refuses, one is
enough, and nothing about the database's own contents is consulted.

*Also decided: where the check runs.* The schema check is registered ahead of the five
checks D-160 put at the head of the collection, because every one of them reads a table
and would otherwise meet a missing column with a failure that does not name the cause.
The order test that pinned `ModelValidationService` at index 0 now pins
`SchemaValidationService` there; AUTHZ-MODEL-004 AC2 is about the checks standing before
the web server, which is unchanged.

*Tests that pin it.*
`SchemaValidationTests.OPS_MIG_002_AC1_TheMigratedDatabaseMatchesTheModelAsync`,
`SchemaValidationTests.OPS_MIG_002_AC1_ADatabaseBehindTheModelIsRefusedByNameAsync`,
`SchemaValidationTests.OPS_MIG_001_AC1_TheCheckLeavesTheDatabaseUnmigratedAsync`,
`StartupValidationTests.OPS_MIG_002_AC1_ADeploymentOnAnUnmigratedDatabaseIsRefusedAsync`,
`StartupValidationTests.AUTHZ_MODEL_004_AC2_TheChecksStartBeforeEverythingElseRegistered`.

*Chapter text that should change.* OPS-MIG-002 should say which direction of mismatch
refuses: a schema behind the model, and not a schema ahead of it, which OPS-MIG-005
requires the previous application version to run against. `10` section 1.5 already
carries the row for `model.startup.schemamismatch`; the description should name
`pending` as the detail it carries.

---

## 154. The membership aggregate refuses the second membership

**Corrections 1 · 2026-09-23 · D-162 section D · IDN-MEM-002, `10` section 1.1**

*What D-162 decided.* Build now, in the phase whose item it is: the
`identity.membership.limitreached` refusal in the membership aggregate. Phase 1 built
the schema that takes any number of memberships (AC1) and deferred AC2 and AC3 to phase
2 as "stated over the setting"; phase 2 did not pick them up, and no code has read
`organization.multiplememberships` since.

*What was built.* The one factory that makes a new membership now takes the memberships
the account already holds and what the setting allows, and answers `Result<Membership>`.
The rule lives with the aggregate and not with a caller, so there is no second door:
the schema takes any number of rows and the aggregate decides how many an account holds
at once, which is exactly the division IDN-MEM-002 draws. An ended membership is not one
the account holds, so an account whose membership ended joins again on the default. A
caller handing over another account's memberships throws, because such a list would
otherwise answer the question about the wrong account.

*Decided in the owner's absence (Tier 3, strictest reading).* Two points, both failing
closed where the readings differ on what is allowed.

- **What "a second membership" counts.** Reading one: any second current membership.
  Reading two: a second membership of a different organization, the same organization
  being the same membership. Reading one was taken; it grants least, and IDN-MEM-002
  says "Policy SHALL forbid more than one by default" without qualifying which one.
- **A second membership of an organization the account is already a member of, with
  the setting enabled.** Reading one: refuse; one membership of an organization is one
  membership of it, and no chapter contemplates two live rows for one pair. Reading two:
  allow; IDN-MEM-002 permits "zero or more" and names no other limit. Reading one was
  taken. It is the only refusal in this item that is not the setting's, so it carries
  the organization under `organization` in the details, which tells a caller the two
  cases apart. The owner may want a code of its own here; the chapters name none.

*Tests that pin it.*
`MembershipTests.IDN_MEM_002_AC2_ASecondMembershipIsRefusedByDefault`,
`MembershipTests.IDN_MEM_002_AC3_TheSettingAloneAdmitsTheSecondMembership`,
`MembershipTests.IDN_MEM_002_AC2_AMembershipThatEndedLeavesRoomForAnother`,
`MembershipTests.IDN_MEM_002_ASecondMembershipOfTheSameOrganizationIsRefused`,
`MembershipTests.Create_MembershipsOfAnotherAccount_Throws`,
`OrganizationStoreTests.IDN_MEM_002_AC1_TheSchemaTakesMoreThanOneMembershipPerAccountAsync`.

*Chapter text that should change.* IDN-MEM-002 should say that the memberships counted
are the current ones, and what happens to a second membership of an organization the
account already belongs to. `10` section 1.1 carries the row for
`identity.membership.limitreached`; its description should say the code covers both, and
that the details name the organization in the second case.

---

## 155. The organization erasure executes, and announces itself

**Corrections 1 · 2026-09-23 · D-162 section D · IDN-ORG-003, IDN-ORG-005, IDN-PRIN-003,
`10` sections 5 and 5b**

*What D-162 decided.* Build now, in the phase whose item it is: `OrganizationErased`
with the grace-window execution it reports; the scheduling may wait for phase 9, the
operation may not. Phase 1 built the four stages on the aggregate and nothing ever
called the last of them, so `ErasureReason.OrganizationErasure` and the `10` section 5b
row both stood with no code behind them.

*What was built.* One pass over the organization windows the clock has run out on,
shaped exactly as the account pass of IDN-LIFE-014 is: the window's length is read from
`organization.deletion.grace`, the organizations whose window began on or before that
are read, and each is erased in a transaction of its own. The erasure ends every current
membership of the organization, replaces what the organization was called with its own
identifier, and writes the instant it executed onto the row. Nothing is removed. The
pass then writes the audit row and announces `OrganizationErased`, which carries the
organization and how many memberships ended and names no person, because an
organization erasure is nobody's act.

*Decided in the owner's absence (Tier 3, strictest reading).* Three points.

- **What "its identifying data is rendered unreadable" reaches.** Reading one: the
  organization's own name, which is the only thing about the organization a person
  spells. Reading two: nothing of the organization, only the members' personal
  attributes inside the trail, which the subject erasure already renders unreadable.
  Reading one was taken, as the reading that keeps least of what identifies. The name
  becomes the identifier, which is the pseudonymisation D-026.1 describes rather than a
  destruction, and IDN-ORG-003 AC5 still holds: the row exists and the identifier
  resolves.
- **Whether the erasure erases the member accounts.** Reading one: it does, since
  `organization-erasure` is an erasure reason and the erasures table is per subject.
  Reading two: it does not; it ends the memberships. Reading two was taken. An
  organization is not a tenancy or an isolation boundary (IDN-ORG-001), an account may
  hold memberships of several organizations (IDN-MEM-002), and REG-MAIL-003 describes
  what happens to an account's primary identifier when a membership ends, which is a
  rule about an account that outlives its membership. Erasing a person's account
  because a company was deleted is not something any criterion asks for, and it cannot
  be undone. **This leaves `ErasureReason.OrganizationErasure` with no producer**; the
  owner should say which subjects, if any, an organization erasure erases.
- **Whether the erasure takes back the grants made within the organization.** Reading
  one: it does, because D-038 says the deletion cascades to "members, grants, owned
  data" and a live grant of an erased organization would go on conferring access.
  Reading two: it does not; no acceptance criterion of IDN-ORG-003 names grants, and
  the access stop the item does name is the suspension of AC1. Reading two was taken,
  for a reason the schema settles: `ck_grants_revocation` requires `revoked_by` on any
  revoked grant, so a revocation the library makes by itself cannot be written without
  either a migration that weakens that constraint or a person the library invents, and
  neither is this run's to decide. **This is the one point of the three that leaves a
  hole**: IDN-ORG-003 AC1, the stop on member access within one request cycle, was
  deferred by phase 1 to phase 3 and has not been built, so nothing today refuses
  access through a suspended or erased organization. The owner should say whether the
  erasure revokes, and if it does, what a revocation with no revoker looks like.

*Tests that pin it.*
`OrganizationErasureSweepTests.IDN_ORG_003_AC3_TheErasureDoesNotExecuteBeforeTheWindowElapsesAsync`,
`OrganizationErasureSweepTests.IDN_ORG_003_AC2_ACancelledWindowIsNotReachedByThePassAsync`,
`OrganizationErasureSweepTests.IDN_ORG_003_TheErasureIsAnnouncedWithWhatItEndedAsync`,
`OrganizationErasureSweepTests.IDN_ORG_003_TheErasureIsWrittenDownAsync`,
`OrganizationErasureSweepTests.IDN_ORG_003_APassWhoseAnnouncementIsRefusedAnswersWithTheRefusalAsync`,
`OrganizationStatesTests.IDN_ORG_003_TheWindowsThatHaveRunOutAreWhatThePassReadsAsync`,
`OrganizationStatesTests.IDN_ORG_003_AC5_TheRowSurvivesAndTheNameBecomesTheIdentifierAsync`,
`OrganizationStatesTests.IDN_ORG_003_TheErasureEndsEveryCurrentMembershipAsync`,
`OrganizationStatesTests.IDN_ORG_003_AC3_AnErasureBeforeTheWindowElapsesWritesNothingAsync`.

*Chapter text that should change.* IDN-ORG-003 should say what the erasure reaches:
the memberships and the name, and whether the grants and the member accounts are among
them. `10` section 5b already names `OrganizationErased`; the row should say it carries
the organization and the count of memberships ended and no subject. `10` section 5 needs
the new audit action `identity.organization.erased` (listed under "Rows for chapter 10").

---

## 156. The shipped lawful bases and sensitive categories exist

**Corrections 1 · 2026-09-23 · D-162 section D · PRIV-BASIS-001, PRIV-BASIS-004,
PRIV-SENS-001, `07` LIB-HOST-001, `10` sections 5.7 and 5.9**

*What D-162 decided.* "Verify with one repository search that the shipped Egypt default
declarations of PRIV-BASIS-001 and PRIV-SENS-001 exist in source."

*What the search found.* They did not. `LawfulBasisDeclaration` and
`AuthorizationDeclarationBuilder.SensitiveCategory` both existed and had since phase 4,
but no file shipped the lists themselves. A search of `src/` for the six basis keys
found one file, `CapabilityResidual.cs`, carrying `consent` as the wire name of
something else; a search for the eight categories found `children` alone, in the
register. Every test and fixture that needed a basis wrote its own, which is why
nothing noticed. PRIV-BASIS-001 says "Egypt's six SHALL ship as the default
declaration", PRIV-SENS-001 says "Egypt's list ships as the default", and `07`
LIB-HOST-001 says the same of both, so they were built.

*What was built.* Two shipped catalogues in `Janus.Core`, in the shape `ProviderRegister`
already ships the provider register in: `LawfulBases.Default`, the six of `10` section
5.7 in the order PRIV-BASIS-001 tables them, each carrying the four properties that
table gives it, and `SensitiveCategories.Default`, the eight of `10` section 5.9 in its
order. A deployment declares them; nothing registers them by itself, because the list is
the host's declaration and the library holds no jurisdiction of its own.
`SensitiveCategories.Children` is the one category the library reads by name, and the
register now reads it from there rather than from a second spelling of its own.

*Resolved by rule (Tier 1).* `DeclaredProcessingTests.PRIV_BASIS_001_AC4_NoLibrarySourceNamesABasis`
asserted that one file in `src/` carries a basis key as a literal. The shipped
declaration is a second, and is the thing PRIV-BASIS-001 requires to exist, so the gate
as written could not admit the item it belongs to. AC4 says "No **conditional** in the
library tests for a basis by name", so the test now asserts what the item says: no file
carries a basis key on a line that also carries a construct choosing between two paths,
the two files carrying the keys at all are named, and the same is asserted of the
categories. The rule the test enforces is unchanged and no weaker; the same branch-token
list the factor catalogue gate of AUTH-FACT-001 AC1 uses decides what a conditional is.

*Tests that pin it.*
`DeclaredProcessingTests.PRIV_BASIS_001_TheShippedDeclarationCarriesTheDeclaredProperties`,
`DeclaredProcessingTests.PRIV_BASIS_001_AC4_NoLibrarySourceNamesABasis`,
`DeclaredProcessingTests.PRIV_BASIS_004_AC1_NoLibrarySourceNamesABasisWhosePropertiesAreAllUnset`,
`DeclaredProcessingTests.PRIV_SENS_001_TheShippedCategoriesAreLabelsNothingBranchesOn`.

*Chapter text that should change.* None of substance. `07` LIB-HOST-001 could name the
two catalogues a deployment declares from, as it names the other shipped defaults;
they are listed under "Rows for chapter 10" for that purpose.

---

## 157. Two points of section D were already true, and one chapter row is stale

**Corrections 1 · 2026-09-23 · D-162 section D · `10` sections 1.1 and 5b, OPS-ALERT-001,
IDN-ACCT-007, AUTHZ-CONCEAL-004**

D-162 names two things in section D that the search shows the library already does.
Neither needed code; both are recorded so the owner does not look for them, and one
leaves a chapter row to delete.

*`AlertRaised` as a 5b event.* It already is one. `Janus.Core.AlertRaised` derives from
`JanusEvent` and carries the condition identifier of `10` section 5.23, the severity and
the structured details of the row, exactly as the 5b row describes. Every place that
raises one publishes it through `IEvents.PublishAsync`, and there are nine:
`RecoveryService` (recovery clustering, approver volume), `DeliveryReports` (callback
verification failed), `NonExistenceNotice` (nonexistent notice rate),
`RestrictionAdministration` (restriction loosened, restriction granted), `SmsBalance`,
`ThrottleService` (sustained authentication failures) and `PrivacyAlerts`, with
`AlertDestinationChange` publishing one that names the actor. `Alerts.Of` is the only
constructor of one and every call of it is an argument to `PublishAsync`. Nothing to
build.

*`identity.account.restricted` retired in favour of `authz.restricted`.* Also already
true. A search of `src/` for `identity.account.restricted` finds nothing at all: no
`ErrorCodes` member, no status row, no caller. What a restricted subject meets is
`ErrorCodes.Restricted`, which is `authz.restricted`, and the catalogue test pins the
whole closed set, so no path can raise the retired code.

**Chapter text that should change.** `10` section 1.1 still carries the row
`` `identity.account.restricted` **(new)** | Processing restricted at the subject's
request | IDN-ACCT-007 ``. D-162 retires it, and no code has ever raised it, so the row
should be deleted or struck through in the way the chapter already strikes
`identity.identifier.duplicate`, naming `authz.restricted` as its successor.

---

## 158. The protocol server owns the protocol, and the library's tables hold its records

**Corrections 1 · 2026-09-23 · D-162 items 58 and 59 · AUTH-OIDC-001 to AUTH-OIDC-004,
AUTH-SESS-012, AUTH-KEY-001, API-REDIR-001, CONV-DESIGN-008**

D-162 reverses entries 58 and 59. Degraded mode is gone. The four store interfaces the
server asks its questions through are hand-written in `Janus.Storage` over the library's
own tables, the server's own handlers validate the clients and issue the codes and the
tokens, and the library adds only what no protocol server can know.

*What the library now holds.* `oidc_clients` as it always did, and three new tables:
`oidc_authorizations` for the grant a code and its tokens hang from, `oidc_tokens` for
every code, refresh token and reference the server issues, and `oidc_scopes` for the
scopes a deployment registers beyond the four the server is built with. Each row hangs
from the account it names and the client it belongs to by foreign key, and the grant and
token rows carry a concurrency token, so two presentations of one row cannot both change
it and the second is refused rather than lost. `OidcApplicationStore` and
`OidcScopeStore` are read only: the registry is the deployment's and every write through
them is refused where it arrives.

*What the library still decides.* The session record every token is minted from
(`OidcService.MintAsync`, AUTH-OIDC-004 AC1), the end of everything derived from a record
whose token came back twice (`OidcService.ReuseAsync` through the `TokenReuse` handler,
AUTH-OIDC-003 AC1 and AC2), the one destination a code returns to (`RegisteredDestination`,
API-REDIR-001 AC1), the claims a scope names (`ClaimsAnswer`), the published key set
(`KeySetAnswer`), and the kind of client, which is what the application store turns into
the grant types the server admits (09 section 9).

*The signing key.* There is no ephemeral pair. `SigningCredentialSource` holds the
credentials the server signs with, primed at startup by `SigningKeyValidationService`
from the deployment's own key store and replaced in process when the key rotates, so a
rotation still needs no restart (AUTH-KEY-001 AC1) and the key that signs is the key the
set publishes. A deployment whose key store cannot answer stops at startup with the
refusal the store gave.

*Tests that pin what is built.*
`OidcStoreTests.AUTH_OIDC_001_AC2_TheRegistryHoldsWhatTheSecretHashesToAsync`,
`OidcStoreTests.AUTH_OIDC_001_AC3_NoRequestWritesTheRegistryAsync`,
`OidcStoreTests.AUTH_OIDC_002_AC1_OnlyAProtocolClientMayRefreshAsync`,
`OidcStoreTests.AUTH_OIDC_003_AC1_RevokingAGrantTakesEveryTokenUnderItAsync`,
`OidcStoreTests.AUTH_OIDC_003_AC1_TwoWritesOfOneRowCannotBothSucceedAsync`,
`OidcStoreTests.AUTH_KEY_003_AC1_TheSweepTakesTheTokensThatCanNoLongerBePresentedAsync`,
`OidcFlowTests.AUTH_KEY_001_AC2_TheKeyThatSignsIsTheKeyTheSetPublishesAsync`,
`OidcFlowTests.AUTH_SESS_012_AC5_TheExchangeIsBackChannelAndNamesTheSessionAsync`,
`OidcFlowTests.AUTH_OIDC_003_AC1_ARefreshTokenRotatesAndTheOldOneIsSpentAsync`,
`OidcFlowTests.API_REDIR_001_AC1_AnUnknownDestinationIsReplacedAndLoggedAsync`,
`OidcServiceTests.AUTH_OIDC_003_AC1_AReuseEndsEverythingDerivedFromTheRecordAsync`.

*Chapter text that should change.* Chapter 07's list of library-owned tables names the
one-time codes and the refresh-token families; those two tables are gone and the three
named above take their place. `08` CONV-DESIGN-008 already carries the D-162 wording.

---

## 159. The codes and the refresh tokens are encrypted under a key derived from the key-encryption key

**Corrections 1 · 2026-09-23 · Tier 2 · AUTH-KEY-002, AUTH-OIDC-002, OPS-SEC-001**

*The question.* The server encrypts the codes and the refresh tokens it issues and
refuses to start without a key to do it with. Entry 59 gave it an ephemeral pair because
nothing it protected was real; now the codes and the refresh tokens are the server's own
encrypted values, so the key is load bearing. No chapter names one.

*The readings.*

1. A key of the server's own, created per process. A code issued by one instance is then
   unreadable by any other and by the same instance after a restart.
2. A key of its own in a new table, with a ceremony and a rotation of its own.
3. A key derived from the key-encryption key the secrets manager hands the deployment at
   startup (OPS-SEC-001), under a purpose string of this use alone.

*Chosen: 3.* Reading 1 breaks a deployment that runs more than one instance, which
OPS-SEC-001 assumes. Reading 2 adds a second secret, a second ceremony and a second
rotation for a value that lives for sixty seconds or for the life of a session record.
Reading 3 gives every instance the same key without holding a second secret, and every
version the deployment still holds is derived, current first, so a rotation of the
key-encryption key leaves the codes and refresh tokens already issued readable. The
purpose string `janus:oidc:token-protection:v1` separates this material from every other
use of the same key, so what is derived here cannot unwrap a subject's data key and what
is derived elsewhere cannot read a token.

*Tests that pin what is built.*
`OidcFlowTests.AUTH_SESS_012_AC5_TheExchangeIsBackChannelAndNamesTheSessionAsync`,
`OidcFlowTests.AUTH_OIDC_003_AC1_ARefreshTokenRotatesAndTheOldOneIsSpentAsync`.

*Chapter text that should change.* AUTH-KEY-002 could name this derivation beside the
subject data keys, so no later run reaches for a key table of its own.

---

## 160. `IOidc` carries the two operations the contract exposes twice, and nothing else

**Corrections 1 · 2026-09-23 · Tier 2 · LIB-API-005, CONV-DESIGN-005, AUTH-OIDC-001**

*The question.* D-162 says `IOidc.FindClientAsync` no longer answers `authz.denied`
because the protocol error is the server's. That leaves the member answering the client
or nothing, which CONV-DESIGN-005 AC2 forbids on a contract, and `10` section 1 holds no
code for "the registry holds no such client" to answer instead. `MintAsync` and
`ReuseAsync` are in the same position: both exist for the provider's own handlers and
neither is reachable over HTTP.

*The readings.*

1. Keep all three and invent a code, such as `auth.client.notfound`, for the lookup to
   fail with, and give `ReuseAsync` a `Result` it can never fail with.
2. Keep all three and let the lookup answer a nullable, against CONV-DESIGN-005 AC2.
3. Read LIB-API-005 as it is written. Every library-owned operation exists once as a
   contract and is exposed twice, in process and as an HTTP endpoint. The two members
   that answer an endpoint stay; the three that are the provider's own working parts move
   to `OidcService`, which is where the handlers reach them.

*Chosen: 3.* Reading 1 invents vocabulary no chapter uses, which section 4 of the
instructions forbids, and puts a `Result` on a method with no expected failure. Reading 2
breaks a convention `08` states without qualification. Reading 3 breaks nothing: `IOidc`
keeps `ClaimsAsync`, which answers `GET /oidc/userinfo`, and `KeysAsync`, which answers
`GET /oidc/jwks`, and both return `Result<T>`. `RegisteredDestination` reads the registry
through `IOidcClientStore`, the port that owns it. `MintedSession` moves to
`Janus.Authentication.Oidc` and leaves the public surface with the members that carried
it. The public contract gets smaller and nothing a host could call is lost, because
nothing a host could call was ever among the three.

*Tests that pin what is built.*
`ResultContractTests.CONV_DESIGN_005_AC1_EveryContractMethodReturnsAnOutcome`,
`ResultContractTests.CONV_DESIGN_005_AC2_NoContractReturnsNullForNotFound`,
`OidcFlowTests.AUTH_OIDC_001_UserInfoAnswersWhatTheScopeNamesAsync`,
`OidcFlowTests.AUTH_KEY_001_AC4_TheKeySetCarriesTheConfiguredAlgorithmAsync`.

*Chapter text that should change.* D-162's sentence about `IOidc.FindClientAsync` should
say the member is gone rather than that it answers something else.

---

## 161. A browser application asking to hold a refresh token is refused where it asks

**Corrections 1 · 2026-09-23 · Tier 3 · AUTH-OIDC-002, AUTH-SESS-012, 09 section 9**

*The question.* AUTH-OIDC-002 AC1 and AC2 say a browser application's own layer receives
no refresh token and holds nothing after the exchange. The retired implementation issued
the code anyway and quietly handed nothing back at the token endpoint. The server reads
the same rule from the grant types the client holds and refuses the authorization
request outright, because `offline_access` is governed by the refresh grant and a
browser application does not hold it.

*The readings.*

1. Grant every client the refresh grant at the authorization endpoint and withhold the
   refresh token at the token endpoint, which reproduces the old behaviour.
2. Let the refusal stand where the client asks: a client that asks for what it may not
   have is told so, with the protocol's own `invalid_request`.

*Chosen: 2, as the strictest reading.* This is a question about what is refused, so it is
Tier 3 and takes the reading that grants least. Reading 1 would have the deployment admit
a request it intends to answer incompletely, and would put the same rule in two places,
one of which could drift. Reading 2 keeps the rule in one place, the grant types the
application store derives from the kind of client, and a relying party learns at once
that it asked for something it cannot have. A browser application that asks only for what
it may hold is unaffected.

*Tests that pin what is built.*
`OidcStoreTests.AUTH_OIDC_002_AC1_OnlyAProtocolClientMayRefreshAsync`,
`OidcFlowTests.AUTH_OIDC_002_AC1_ABrowserApplicationAskingToHoldOneIsRefusedAsync`,
`OidcFlowTests.AUTH_SESS_012_AC6_TheExchangeHandsABrowserApplicationNoRefreshTokenAsync`.

*Chapter text that should change.* AUTH-OIDC-002 could say where the refusal falls, so a
frontend knows to ask for `offline_access` only on behalf of a protocol client.

---

## 162. The two retired tables are dropped by the migration that creates their successors

**Corrections 1 · 2026-09-23 · Tier 2 · OPS-MIG-005, OPS-MIG-001**

*The question.* OPS-MIG-005 says every migration "SHALL work against the **previous**
application version" and that "Renaming or dropping SHALL NOT occur in the same release
that changes the code using it". `oidc_codes` and `oidc_refresh_tokens` are the tables of
the implementation D-162 reverses, and nothing reads or writes them after this branch.

*The readings.*

1. Create the three new tables now and leave the two dead ones in place until a later
   release drops them, which is expand and contract read literally.
2. Drop them in the same migration, because there is no previous release to be compatible
   with.

*Chosen: 2.* The rule protects a deployment running the previous version against a schema
the new one changed under it. No version has been released: `git tag --list 'v*'` is
empty, and the double-migration gate says so itself, taking the previous schema to be the
empty one when nothing is tagged. Leaving two dead tables in the schema for a
compatibility window that has no other side would make the first release ship them and a
second migration remove them. If a release is tagged before this branch merges, the drop
must move to its own migration in the release after it.

*Tests that pin what is built.* The double-migration gate of CONV-GATE-001, which applies
the migrations twice over an empty database and compares the model against the schema.

*Chapter text that should change.* None. OPS-MIG-005 is right; it simply has no previous
version to bind here.

---

## 163. The client half of the sign-on is the library's, and a host declares which client it is

**Corrections 1 · 2026-09-23 · D-162 item 66 · BFF-SESS-006, BFF-OWN-001, LIB-HOST-001,
OPS-SEC-001, API-REDIR-001**

D-162 reverses entry 66. Both halves of BFF-SESS-006 are the library's. An application
establishes its own session from the record the authentication application holds by the
authorization code flow with proof key, as a confidential client, and retains no token.

*What the library now ships.* Two routes of the browser profile: `GET /auth/signon`,
which forwards the browser to the provider with `prompt=none`, the destination the
registry holds for this client, an S256 challenge and a state bound to the
pre-authentication session; and `GET /auth/signon/return`, which judges the state,
trades the code on this server's own connection with the client secret and the proof
key, reads `sid` from the identity token, derives the per-application session from that
record, writes the pair of cookies, ends the pre-authentication session and sends the
browser where it was going. `login_required` answers the silent attempt only, and the
second attempt asks for a sign-in, which the provider answers by forwarding to the
declared screen. The attempt is forgotten on every return, so one code is judged once.

*What a host declares.* `SignOnClient`, the identifier this application is registered
under, a new LIB-HOST-001 row with no default. `AuthenticationAddresses` gains
`Provider`, the address the library is mounted at on the authentication application,
because the endpoints are the library's own routes under a mount only the deployment
knows. Both fail startup with `model.startup.declarationmissing` naming the key.

*What the secrets manager supplies.* `ISecretSource.ReadSignOnSecretAsync`, passed to
`AddJanus` beside the key-encryption key and the fingerprint key; absent, startup fails
with `model.startup.keyunavailable`. Nothing of it is written anywhere. The proof key,
which is this server's own secret for the life of one flow, is held on the
pre-authentication row wrapped under the key-encryption key.

*Tests that pin what is built.*
`SignOnTests.BFF_SESS_006_AC1_ALiveRecordEstablishesASessionWithNoInteractionAsync`,
`SignOnTests.BFF_SESS_006_AC2_TheExchangeIsServerToServerAndHandsTheBrowserNoTokenAsync`,
`SignOnTests.BFF_SESS_006_AC3_AMismatchedStateIsRejectedAndLoggedAsync`,
`SignOnTests.BFF_SESS_006_AC3_AReturnedCodeIsNotAcceptedTwiceAsync`,
`SignOnTests.BFF_SESS_006_AC4_NothingButThePerApplicationSessionIsHeldAfterwardsAsync`,
`SignOnTests.BFF_SESS_006_AC5_RevokingTheRecordEndsThePerApplicationSessionAsync`,
`SignOnTests.BFF_SESS_006_ABrowserWithNoRecordIsSentToSignInAsync`,
`SignOnTests.BFF_SESS_006_TheDestinationIsTheRegisteredOneAndNeverAskedForAsync`,
`SignOnTests.BFF_SESS_006_AReturnAddressOffThisApplicationIsNotFollowedAsync`,
`PreAuthenticationStoreTests.BFF_SESS_006_TheProofKeyIsAtRestUnderTheKeyEncryptionKeyAsync`,
`PreAuthenticationStoreTests.BFF_SESS_006_AC3_ForgettingTheAttemptClearsEveryColumnOfItAsync`,
`StartupValidationTests.BFF_SESS_006_ADeploymentThatDeclaredNoSignOnClientIsRefusedAsync`.

*Decided in the owner's absence, within this item.*

1. *Where the provider is* (Tier 2). D-162 names one new declaration, the client
   identifier, and section E names no key for the provider's address. The readings were
   to derive the origin from the declared sign-in address, or to declare the address.
   Declaring it was chosen: the sign-in address is a frontend page, the endpoints sit
   under the library's mount, and a deployment that mounts the library under a prefix
   has no address the library could derive. It is a component of the row D-162 item 61
   created rather than a row of its own.
2. *What the sign-on answers a refusal with* (Tier 3). No `10` code names a sign-on,
   and section E adds none. A state that is absent, unbound or not the one this browser
   was sent out with answers `auth.session.csrfinvalid`, because BFF-CSRF-005a makes the
   pre-authentication session the binding target of both the synchronizer token and this
   state; everything else answers `auth.session.expired`, which is what the request
   failed to obtain. No code is invented.
3. *Where the proof key lives* (Tier 3). It is a secret the server holds and the browser
   never sees. Holding it in a cookie would put something other than an opaque identifier
   in the browser (BFF-SESS-001), and deriving it from the state would be cleverness in
   place of a rule. It is stored on the pre-authentication row wrapped under the
   key-encryption key, as the signing key's private half is (AUTH-KEY-002), with the four
   columns written together or not at all.

*Chapter text that should change.* `17` BFF-SESS-006 should name the two routes and say
that both halves are the library's; `07` LIB-HOST-001 should carry the client identifier
row and the provider address beside the sign-in address; `10` section 4 needs no key.

---

## 164. The public types are named for what they are

**Corrections 2 · 2026-09-23 · Tier 2 · CONV-NAME-001, CONV-LAYOUT-002, CONV-DESIGN-007, D-163**

*The question.* D-163 renames every type and member that carries the product name "for
what the thing is", and names four successors: `StoreContext`, `DomainEvent`,
`MapAuthorizationTables` and `AddIdentityArea` with its siblings. Nine more names in the
code carried it, seven of them on the public surface of `Janus.Hosting`, and no chapter
names their successors.

*The readings.*

1. Name each for what it is, with the prefix `identity` only where the name would
   otherwise not say whose it is beside a host's own names on the same builder.
2. Put `identity` on every one, so each successor is the old name with the word
   swapped.

*Chosen: 1.* D-163 asks for the name of the thing and keeps the prefix for artefacts
that need keeping apart from a host's; its own examples carry none (`StoreContext`,
`MapAuthorizationTables`). The successors:

| Was | Is | Why |
| --- | --- | --- |
| `JanusRegistration` | `HostingRegistration` | The registration class of `Janus.Hosting`, beside `StorageRegistration` in `Janus.Storage`. `AddJanus` stays on it. |
| `JanusEndpoints.MapJanus` | `IdentityEndpoints.MapIdentityEndpoints` | Mounts the library's endpoints among the host's own on the host's route builder, where a bare `MapEndpoints` would not say whose. |
| `JanusEndpoints.MapJanusWellKnown` | `IdentityEndpoints.MapIdentityWellKnown` | The same reason, for the two documents of REG-PM-001 at the site root. |
| `JanusPipeline` | `PipelineProfiles` | Holds the two profiles of BFF-OWN-001 and BFF-MACH-001. |
| `UseJanusBrowserProfile`, `UseJanusMachineProfile` | `UseBrowserProfile`, `UseMachineProfile` | The chapters' own names for what each mounts. |
| `JanusAuthorizationModel` | `AuthorizationTables` | Holds `MapAuthorizationTables` (D-163). |
| `JanusApplication` | `ApplicationKind` | Which of the deployment's applications the pipeline is mounted in (BFF-CSRF-005). |
| `JanusEvent` | `DomainEvent` | D-163. |
| `JanusDbContext` | `StoreContext` | D-163. |
| `AddJanusStorage` | `AddStorageArea` | The storage project's sibling of `AddIdentityArea` (CONV-DESIGN-007). |

*Tests that pin it.* The declared public API of `Janus.Core` and `Janus.Hosting`, which
fails the build on any other name (CONV-SETUP-003);
`BrowserProfileTests.BFF_OWN_001_AC1_MountingTakesNoSecurityRelevantConfiguration`;
`TruthTableTests.AUTHZ_GATE_002_AC2_EveryCaseIsEqualAcrossBothRenderingsAsync`, whose
host maps the tables with `MapAuthorizationTables`.

*Chapter text that should change.* `07` LIB-API-005 or `17` BFF-OWN-001 could name
`MapIdentityEndpoints`, `MapIdentityWellKnown`, `UseBrowserProfile` and
`UseMachineProfile` as the mounting calls, so the chapters name what a host writes.

---

## 165. The database names no chapter fixes take the prefix only where they meet a host's

**Corrections 2 · 2026-09-23 · Tier 2 · CONV-NAME-001, OPS-DB-002, OPS-MIG-007, AUTHZ-GATE-002, D-163**

*The question.* D-163 fixes the schema, the collation and the three roles. Four more
database names carried the product name and no chapter names them: the migrations
history table, the channel a registration wizard's stream listens on, the identifiers
the permission rule writes into the SQL it renders, and the names of the databases the
tests, the design-time factory and the double-migration gate create.

*The readings.*

1. `identity` on each, as on the schema.
2. `identity` only where the name shares a namespace with a host's, and the plain name of
   the thing elsewhere.

*Chosen: 2.* The prefix exists to keep the library's artefacts apart from a host's
(CONV-NAME-001), so it goes where they meet and nowhere else.

| Was | Is | Why |
| --- | --- | --- |
| `__janus_migrations_history` | `__migrations_history` | The table is in the `identity` schema, which already keeps it apart from the host's history (OPS-DB-002). |
| `janus_registration` | `identity_registration` | A notification channel is named per database, beside any channel the host listens on. |
| `janus_authz_*` | `identity_authz_*` | The fragment is composed into the host's own query, beside the host's aliases and parameters (AUTHZ-GATE-002). |
| `janus` (test and design-time database) | `identity` | The library's database. |
| `janus_from_empty`, `janus_from_previous` | `migrated_from_empty`, `migrated_from_previous` | Throwaway databases on the gate's own server, named for the run each holds (OPS-MIG-007). |

Entries 55, 134, 135 and 136 name the schema, the collation or the maintenance role in
passing; what each decided stands, and each name reads as renamed.

*Tests that pin it.*
`SchemaTests.OPS_DB_002_AC1_TheLibraryKeepsItsOwnMigrationHistoryAsync`,
`SchemaTests.OPS_DB_002_AC1_TheCollationLivesInTheLibrarysSchemaAsync`,
`DatabaseRoleTests.OPS_MIG_003a_AC1_TheMaintenanceRoleAltersNoSchemaAsync`,
`RegistrationSignalsTests.REG_SESS_003_AWaitHearsTheCommittedAnnouncementAndNoOtherAsync`,
`PermissionRuleTests.LIB_HOST_002_AC1_NoRenderingReadsATableTheHostOwns`,
`PermissionRuleTests.LIB_HOST_002_AC1_OnlyWhatTheHostRunsNamesTheRelationItDeclared`,
and the double-migration gate.

*Chapter text that should change.* `06` OPS-DB-002 could name the history table beside
the schema, since a deployment's database administrator sees it.

---

## 166. The names on the wire no chapter fixes take the prefix, as the ones D-163 fixes do

**Corrections 2 · 2026-09-23 · Tier 2 · CONV-NAME-001, BFF-CSRF-001, BFF-CSRF-003, BFF-SESS-006, AUTH-OIDC-003, AUTH-PASS-004, D-163**

*What D-163 decided, and what was built.* The five cookies are `__Host-identity-*` and
the two headers `X-Identity-Request` and `X-Identity-Csrf`; the code carries them so,
and the tests that hold each name against what the frontend writes were changed with
them. The domain record `_identity-verify` and its value are not in the code yet: the
domain lock (REG-DOM-001) is built in phase 8 and takes the names from `09` as they
now stand.

*The question.* Four more names outside the process carried the product name and no
chapter names them: the two private claims the protocol server's principal carries the
issued code and refresh token in, the named client of the sign-on back channel, the
directory beside the application that holds the word lists, and the purpose the key
protecting codes and refresh tokens is derived under.

*The readings.*

1. `identity` on each, as on the cookies and headers.
2. The plain name of the thing, since none is a cookie, a header or the DNS record.

*Chosen: 1.* Each of the four sits beside a host's own names in a space the host shares:
the claims beside the host's and the protocol's claims, the named client in the host's
client factory, the directory among the host's files, and the purpose in the host's key
ring. That is the case CONV-NAME-001 gives the prefix for.

| Was | Is |
| --- | --- |
| `janus_code`, `janus_refresh` | `identity_code`, `identity_refresh` |
| `janus-signon` | `identity-signon` |
| `janus-corpus` | `identity-corpus` |
| `janus:oidc:token-protection:v1` | `identity:oidc:token-protection:v1` |

Entries 10 and 139 chose and applied the two header names; both are marked. Entries
143, 152, 157 and 159 name the registration channel, the event base type or the
protection purpose in passing; what each decided stands.

*Tests that pin it.*
`BrowserProfileTests.BFF_CSRF_003_AC1_TheTwoHeadersAreNamedAsTheFrontendWritesThem`,
`PrivacyContractTests.PRIV_CONS_006a_AC3_TheLibrarySetsOnlyTheFiveNecessaryCookies`,
`BrowserCookieTests.BFF_SESS_002_AC1_EveryIssueCarriesTheFourAttributes`,
`SignOnTests.BFF_SESS_006_AC2_TheExchangeIsServerToServerAndHandsTheBrowserNoTokenAsync`,
`OidcFlowTests.AUTH_OIDC_003_AC1_ARefreshTokenRotatesAndTheOldOneIsSpentAsync`.

*Chapter text that should change.* `07` LIB-HOST-001 could name `identity-signon` as the
client a host configures, and `05` INT-PWD-003 or `10` `password.blocklist.sources` the
`identity-corpus` directory, since a deployment has to put files there.

---

## 167. The scan reads the source, and allows the name only as the head of a dotted name

**Corrections 2 · 2026-09-23 · Tier 2 · CONV-NAME-001 AC2, D-163**

*The question.* CONV-NAME-001 AC2 reads "A source scan finds the product name only in
namespaces, project and package identifiers and `AddJanus` (D-163)." It does not say
which files are the source, or how a scan tells a namespace or an identifier from any
other use of the word.

*The readings for what is scanned.*

1. Every file in the repository.
2. Every file the build, the tests and the pipeline read: `src`, `tests`, `tools`,
   `.github`, `.config` and the files at the root, leaving out the documents (`docs/`,
   the Markdown files at the root and `NOTICE`) and the Unicode data vendored as
   published.

*Chosen: 2.* The criterion names a source scan. The documents name the package as a
package: the changelog is written for its reader, `NOTICE` heads its attribution with
the package's name, and `docs/` is the owner's. The Unicode files are Unicode's.

*The readings for what is allowed.*

1. A parse per file type that finds namespace declarations, `using` directives, project
   references and package identifiers, and allows the name inside those alone.
2. A textual rule: the name as the first segment of a dotted name (a namespace, a
   `using`, a namespace-qualified type name, or a project, assembly, package or solution
   identifier such as `Janus.Core` or `Janus.slnx`), `AddJanus` as a whole word, and the
   lower-case package identifiers NuGet writes in `packages.lock.json`.

*Chosen: 2.* Every form the criterion allows is the head of a dotted name or the entry
point, and none of the forms D-163 forbids is: a type or member name, a schema-qualified
relation (lower case, outside the lock files), a role, a cookie, a header, a constant, a
comment or a test's data all fail. A parse would pass a comment or a string that spells
the name alone; the textual rule does not. The scan reads the name from the root
namespace, so the test does not spell it.

*What the scan found after the three renames, and what they became.* The administrative
organization in two tests' data, now `Administration`; the pipeline's job identifier
`janus-analyzers`, now `analyzer-rules` (the check's name, which branch protection
requires, is unchanged); and the double-migration gate's worktree folder, now
`release-<tag>`.

*Tests that pin it.*
`ProductNameTests.CONV_NAME_001_AC2_TheProductNameAppearsOnlyInNamespacesIdentifiersAndTheEntryPoint`.

*Chapter text that should change.* `08` CONV-NAME-001 AC2 could say which files the scan
reads, and that the solution file and the lock files' lower-case identifiers count as
project and package identifiers.

---

## 168. A takedown is identified by the outbox record its trigger writes

**Phase 8 · 2026-09-23 · Tier 2 · IDN-LIFE-003, 09 section 8a**

*The question.* 09 section 8a answers the trigger with "**202** `{ takedownId,
erasureDue }`". No chapter says what a `takedownId` identifies. IDN-LIFE-003 names one
record the trigger writes for the takedown: "That outbox record is the per-subscriber
completion record of the cancellation: it is written in the trigger transaction, the
worker delivers it and each required subscriber confirms against it (IDN-LIFE-003a),
and it is what the takedown screen reads during the window (D-148). No erasures row
exists yet".

*The readings.*

1. A takedowns table of its own, keyed by a new identifier.
2. The identifier of the `TakedownExecuted` outbox record written in the trigger
   transaction.

*Chosen: 2.* The chapter names that record as the takedown's record during the window
and names no other. A table of its own would be a second record of the same fact and a
schema no chapter describes.

*Tests that pin it.*
`TakedownServiceTests.IDN_LIFE_003_AC4_TheTriggerTakesTheAccountIntoItsWindowInOneTransactionAsync`,
`TakedownEndpointTests.IDN_LIFE_003_TheTriggerIsAnsweredWithTheTakedownAndItsErasureDueAsync`.

*Chapter text that should change.* 09 section 8a could say that `takedownId` is the
identifier of the outbox record the trigger writes.

---

## 169. The takedown screen reads the latest takedown at `GET /admin/accounts/{subject}/takedown`

**Phase 8 · 2026-09-23 · Tier 2 · IDN-LIFE-003 AC2, PRIV-MINOR-002 AC2, 09 section 8a**

*The question.* IDN-LIFE-003 AC2 requires "per-subscriber completion visible" and says
"the cancellation's completion is read from its outbox record from the moment of
trigger, the erasure's from the erasures row once phase two has run." The chapter
speaks of "the takedown screen". 09 section 8a lists the trigger and the reversal and
no read, and `GET /admin/erasures` reads the erasures table, which holds no row until
phase two.

*The readings.*

1. No read: the cancellation's completion is not visible through the library until
   phase two.
2. A read beside the trigger, `GET /admin/accounts/{subject}/takedown`, under the same
   `takedown:execute`, answering the account's latest takedown from its outbox record.
3. The erasures listing extended to outbox records of kind `takedown-executed`.

*Chosen: 2.* Reading 1 fails AC2 as written. Reading 3 widens an endpoint over a table
the chapter keeps separate (IDN-LIFE-003b). Reading 2 adds one read, gated by the
permission that already sees the takedown, and nothing else. It answers **200**
`{ takedownId, subject, triggeredAt, erasureDue, status, attempts, subscribers: [{
name, required, confirmedAt }] }`, one line per registered subject-event subscriber,
`confirmedAt` absent where that subscriber has not confirmed; `status` is spelled as
`10` section 5.12 spells erasure status. An account never taken down answers **404**
`identity.takedown.notfound`, a new code, because no existing code says that the
account holds no takedown.

*Tests that pin it.*
`TakedownServiceTests.IDN_LIFE_003_AC2_TheHostsProgressIsReadableFromTheTriggerAsync`,
`TakedownServiceTests.IDN_LIFE_003_AnAccountNeverTakenDownHasNoProgressAsync`,
`TakedownServiceTests.IDN_LIFE_003_TakedownExecuteIsRequiredForEveryOperationAsync`,
`TakedownEndpointTests.IDN_LIFE_003_TheTriggerIsAnsweredWithTheTakedownAndItsErasureDueAsync`,
`OutboxStoreTests.IDN_LIFE_003_AC2_TheLatestTakedownIsReadWithItsConfirmationsAsync`,
`OutboxStoreTests.IDN_LIFE_003_AnAccountNeverTakenDownHasNoTakedownToReadAsync`.

*Chapter text that should change.* 09 section 8a gains the row for
`GET /admin/accounts/{subject}/takedown`; 10 section 1.1 gains
`identity.takedown.notfound` (404), listed below under the rows for chapter 10.

---

## 170. A takedown starts from active, restricted or suspended, and from nothing else

**Phase 8 · 2026-09-23 · Tier 3 · IDN-LIFE-003, IDN-LIFE-003b**

*The question.* IDN-LIFE-003 gives the "State sequence: `active → suspended → deleting
→ deleted`, the first two transitions in the trigger transaction", and IDN-LIFE-003b
says access stopped "for a takedown from `suspended` (IDN-LIFE-003)". No chapter says
what a trigger does on an account that is restricted, already taken down, in a
deletion window of another origin, or deleted. `10` section 1.1 gives
`identity.takedown.active` for "Deletion cancellation attempted on a
takedown-originated `deleting`; use `/takedown/reverse`", which is not a second
trigger.

*The readings.*

1. Only from `active`.
2. From `active`, `restricted` and `suspended`; a second trigger refused; an account
   deleting by another origin or deleted refused.
3. As 2, and an account deleting by `self` or `oob-request` converted into a takedown.
4. As 2, and a second trigger answered with the running takedown as a success.

*Chosen: 2, the strictest reading.* A restricted or suspended account is still an
account whose data the takedown must stop and remove, so refusing it (reading 1) would
leave a minor's account outside the procedure. Converting a running deletion
(reading 3) rewrites why the account is leaving and its clock, which is more than any
chapter grants a trigger. A second trigger writes nothing and answers **409**
`identity.takedown.active`, the code that already names a takedown-originated
`deleting`. An account deleting by another origin, or deleted, answers **403**
`authz.denied` and nothing is written. The aggregate enforces the same three states.

*Tests that pin it.*
`TakedownServiceTests.IDN_LIFE_003_AnAccountIsTakenDownFromWhereverItStandsAsync`,
`TakedownServiceTests.IDN_LIFE_003_ASecondTriggerAnswersTakedownActiveAsync`,
`TakedownServiceTests.IDN_LIFE_003_AnAccountAlreadyLeavingIsNotTakenDownAsync`,
`AccountStatesTests.IDN_LIFE_003_AnAccountInItsOwnWindowIsNotTakenDownAsync`.

*Chapter text that should change.* IDN-LIFE-003 could name the states a takedown
starts from, and `10` section 1.1 could widen `identity.takedown.active` to a second
trigger.

---

## 171. `AccountSuspended` is published after the trigger commits, and `TakedownExecuted` travels on the outbox

**Phase 8 · 2026-09-23 · Tier 2 · IDN-LIFE-003 AC6, IDN-LIFE-003a, CONV-DESIGN-002**

*The question.* IDN-LIFE-003 says "`AccountSuspended` and `TakedownExecuted` fire at
trigger; no `AccountDeletionRequested` fires", and `10` section 5b says
`TakedownExecuted` "fires with `AccountSuspended`; `AccountDeletionRequested` does
**not** fire for a takedown". IDN-LIFE-003a delivers the cancellation through the
outbox. No chapter says how the two travel or what the caller is told when the
publication of the first is refused after the commit.

*The readings.*

1. Both on the outbox.
2. `TakedownExecuted` on the outbox, written in the trigger transaction (it is a
   subject event the hosts confirm); `AccountSuspended` published through `IEvents`
   after the commit, as every other state change is (CONV-DESIGN-002: commit, then
   publish), with a refused publication answered to the caller while the takedown
   stands.
3. As 2, with a refused publication swallowed and the trigger answered 202.

*Chosen: 2.* `AccountSuspended` is not a subject event and carries no per-subscriber
confirmation, so it travels as the library's other domain events do. The takedown has
committed by the time the publication is refused; undoing it is not possible and
hiding the refusal would tell the operator that everything was announced. The
refusal is answered, the account stays taken down and the progress read (entry 169)
shows the delivery.

*Tests that pin it.*
`TakedownServiceTests.IDN_LIFE_003_AC6_TheSuspensionIsAnnouncedAndNoDeletionIsAsync`,
`TakedownServiceTests.IDN_LIFE_003_AC4_NoErasureIsRequestedAtTheTriggerAsync`,
`TakedownServiceTests.IDN_LIFE_003_AnUnannouncedTriggerStillStandsAsync`.

*Chapter text that should change.* IDN-LIFE-003 could say that `TakedownExecuted` is
the outbox record and `AccountSuspended` follows the commit.

---

## 172. A reversal publishes `TakedownReversed` and nothing else

**Phase 8 · 2026-09-23 · Tier 2 · IDN-LIFE-003 AC5, 10 section 5b**

*The question.* `10` section 5b raises `TakedownReversed` when "A takedown reversed
inside its window", and `AccountSuspended` · `AccountReactivated` when "State enters or
leaves `suspended`, by the subject or an administrator". A reversal moves the account
from `deleting` to `active`.

*The readings.*

1. `TakedownReversed` only.
2. `TakedownReversed` and `AccountReactivated`.

*Chosen: 1.* The reversal leaves `deleting`, not `suspended`, and `10` names the one
event for it. A consumer that acted on `AccountSuspended` at the trigger, such as mail
provisioning, undoes it on `TakedownReversed`; the library's own mail provisioning,
when it is built in this phase, consumes it so.

*Tests that pin it.*
`TakedownServiceTests.IDN_LIFE_003_AC5_AReversalInsideTheWindowRestoresActiveAsync`.

*Chapter text that should change.* `10` section 5b could add mail provisioning to the
consumers of `TakedownReversed`.

---

## 173. The reversal window closes at the start of the window plus `takedown.grace`, whether or not the sweep has run

**Phase 8 · 2026-09-23 · Tier 3 · IDN-LIFE-003 AC5, 09 section 8a**

*The question.* 09 section 8a answers a reversal after the window with "**422**
`identity.takedown.windowelapsed`", and `10` section 4 gives `takedown.grace` "`P7D` |
R, floor `P7D`". The erasure runs when the sweep reaches the account. No chapter says
whether a reversal between the end of the window and the sweep is honoured.

*The readings.*

1. Honoured until the sweep has erased the account.
2. Refused from the instant the window ends, measured from when the account entered
   `deleting` with the `takedown.grace` in force when the reversal is asked.

*Chosen: 2, the strictest reading.* The window is what the chapters grant; a reversal
after it would race the erasure the chapter says is due. The deletion sweep measures a
takedown by the same `takedown.grace`, and the progress read (entry 169) reports the
same instant as `erasureDue`. An erased account answers the same code.

*Tests that pin it.*
`TakedownServiceTests.IDN_LIFE_003_AC5_AReversalAfterTheWindowIsRefusedAsync`,
`TakedownEndpointTests.IDN_LIFE_003_AC5_TheReversalIsAnsweredInsideAndAfterItsWindowAsync`,
`DeletionSweepTests.IDN_LIFE_003_AC5_ATakedownIsErasedWhenItsOwnWindowElapsesAsync`.

*Chapter text that should change.* IDN-LIFE-003 could say that the window ends at the
trigger plus `takedown.grace` whatever the sweep has done.

---

## 174. A trigger and a reversal carry a reason, and the trigger one of the four spellings

**Phase 8 · 2026-09-23 · Tier 2 · IDN-LIFE-003, 09 section 8a, 10 section 5.12d**

*The question.* 09 section 8a says the trigger "records `reason` and `trigger`", and
IDN-LIFE-003 says the only way back is the reversal "under `takedown:execute`, with a
reason." Neither says what a body without a reason, or with a trigger outside `10`
section 5.12d, is answered.

*The readings.*

1. An absent or blank reason accepted and recorded empty; an unknown trigger refused.
2. Both refused with **400** `api.request.malformed`, `details.member` naming the member.

*Chosen: 2, the smaller surface.* A takedown recorded without its reason fails
IDN-LIFE-003 AC3. The reason is recorded trimmed; the trigger is recorded in the
spelling of `10` section 5.12d.

*Tests that pin it.*
`TakedownServiceTests.IDN_LIFE_003_ATriggerWithoutAReasonIsMalformedAsync`,
`TakedownServiceTests.IDN_LIFE_003_AReversalWithoutAReasonIsMalformedAsync`,
`TakedownServiceTests.IDN_LIFE_003_AC3_TheTriggerIsAuditedWithItsReasonAsync`,
`TakedownEndpointTests.IDN_LIFE_003_ATriggerOutsideTheFourSpellingsIsMalformedAsync`.

*Chapter text that should change.* 09 section 8a could mark `reason` required on both
endpoints.

---

## 175. An operation of the deployment asks for its permission in the administrative organization

**Phase 8 · 2026-09-23 · Tier 3 · AUTHZ-SCOPE-001, IDN-ORG-001, LIB-API-005, 09 section 8**

*The question.* AUTHZ-SCOPE-001 says "Every permission evaluation SHALL be scoped to the
organization owning the resource, resolved from the resource and never from the
session." 09 section 8 says "All endpoints under `/admin` require the corresponding
permission." Many administrative operations act on the deployment or on an account
rather than on a record an organization owns: session revocation, configuration, the
restriction set, account suspension, the takedown, the privacy request queue, erasures,
the audit trail, compliance text and records, recovery approval. A customer holds no
membership (AUTH-PRIN-002: "A principal holding **no membership** (an individual user)
follows the **system policy**"), so the account acted on names no organization either.
IDN-ORG-001 says "Organization #1 is the **administrative organization**; its members
are what would elsewhere be called staff." No chapter says in which organization the
gate is asked for such an operation. Phases 6 and 7 asked it in every organization the
caller belongs to and accepted the first that granted; no report recorded that choice.

*The readings.*

1. In every organization the caller belongs to, the first grant deciding (what phases 6
   and 7 built for recovery approval and the privacy area).
2. In the administrative organization only.
3. In an organization the target account belongs to, or in the administrative
   organization for an account holding none.

*Chosen: 2, the strictest reading.* Reading 1 lets a role granted inside any
organization, for that organization's own members, act on every account in the pool and
on the deployment's settings. Reading 3 still lets a manager of one organization act on
the deployment wherever no target account exists. Reading 2 grants least: the deployment
is administered by the organization the chapters name for that purpose, and before
bootstrap has marked one, every such operation is refused. Operations on an
organization's own records (grants, groups, memberships, invitations, policy, domains)
are asked in that organization, as AUTHZ-SCOPE-001 says, and are not affected.

*What changed with it.* The privacy area's scope and recovery approval now ask the
administrative organization, and `ISessions.RevokeAccountAsync` and
`ISessions.RevokeEveryAsync` no longer take an organization. Each area reads the
administrative organization through a port of its own, as each reads memberships.

*Tests that pin it.*
`Janus.Authentication.Tests.Policies.AdministrativeScopeTests` (all three),
`Janus.Privacy.Tests.Policies.AdministrativeScopeTests` (all three),
`AdministrativeOrganizationTests.IDN_ORG_001_TheMarkedOrganizationIsTheAdministrativeOneAsync`,
`SessionServiceTests.AUTH_SESS_011_AC2_RevokingOneAccountWithoutThePermissionIsRefusedAsync`.

*Chapter text that should change.* AUTHZ-SCOPE-001 could say that an operation on the
deployment, or on an account, is scoped to the administrative organization.

---

## 176. A correlation identifier resolves for `audit:read` in the administrative organization, and for its own principal only on a disclosing type

**Phase 8 · 2026-09-23 · Tier 3 · AUTHZ-GATE-004, AUTHZ-CONCEAL-004, AUTHZ-SCOPE-001, 09 section 8a**

*The question.* 09 section 8a mounts `GET /admin/explanations/{correlationId}` under
`audit:read` and says "Self-service explanation for **non-concealed** types is
`GET /account/explanations/{correlationId}`, requiring only the subject's own session."
Neither route names an organization. AUTHZ-GATE-004 says "Those resolve only for a
support role, from the correlation identifier." The contract built in phase 2 took the
organization the support role is held in and resolved only a refusal recorded in that
organization or in none. `10` section 2.1 gives `audit:read` as "Reading the audit
trail, querying it by subject, and resolving a concealed denial's correlation
identifier", and entry 175 already places the audit trail with the deployment. No
chapter says whose refusal the self-service route resolves.

*The readings.*

1. The support resolution asks `audit:read` in the administrative organization and
   resolves any recorded refusal.
2. It asks `audit:read` in the organization the refusal was recorded in, and in the
   administrative organization for one recorded in none.
3. As 1, resolving only refusals recorded in the administrative organization or in
   none.

For the self-service route: (a) the caller is the acting principal of the refusal;
(b) the caller is both its acting and its effective principal.

*Chosen: 1 and (b).* Every refusal is written to the audit trail under the refused
subject, so `audit:read` in the administrative organization already reads it through
`GET /admin/audit?subject=`; reading 1 grants nothing entry 175 has not. Reading 2 adds
a reader in every customer organization. Reading 3 leaves a refusal recorded in a
customer organization resolvable by nobody, which fails AUTHZ-CONCEAL-004 AC1 for it.
On the self-service route, (b) grants least: a refusal taken while acting for another
account resolves for neither party alone. A type the model declares concealing, or no
longer declares at all, answers `authz.denied` with no correlation; a refusal tied to no
record (AUTHZ-CONCEAL-005) discloses. `IAccessGate.ResolveAsync` no longer takes an
organization, and `IAccessGate.ResolveOwnAsync` is added.

*Tests that pin it.*
`ExplanationTests.AUTHZ_GATE_004_AC4_ACorrelationIdentifierResolvesOnlyForASupportRoleAsync`,
`ExplanationTests.AUTHZ_GATE_004_AC4_AReadRoleOutsideTheAdministrativeOrganizationResolvesNothingAsync`,
`ExplanationTests.AUTHZ_GATE_004_AC3_AnIdentifierResolvesForItsOwnerOnlyWhereTheTypeDisclosesAsync`,
`ExplanationTests.AUTHZ_GATE_004_AC3_AnotherPrincipalsIdentifierDoesNotResolveForTheCallerAsync`,
`ExplanationEndpointTests` (all three).

*Chapter text that should change.* 09 section 8a could say that the support resolution
is held in the administrative organization, and that the self-service route resolves a
refusal of the caller acting as themselves.

---

## 177. The audit trail read by subject carries each record's codes and never what it holds under the subject's key

**Phase 8 · 2026-09-23 · Tier 3 · PRIV-BREACH-002, IDN-AUD-001, 09 section 8a**

*The question.* 09 section 8a mounts `GET /admin/audit?subject=...` under `audit:read`:
"Every audit record for one subject, without a full scan (PRIV-BREACH-002)."
PRIV-BREACH-002 AC2 says "The query works after erasure, returning anonymised records."
An audit record holds its codes and references in the clear and, where an event has to
carry a personal value, holds that value under the subject's key (IDN-AUD-001). No
chapter says whether the read hands the personal values to the reader while the key
still exists.

*The readings.*

1. Every field of the record, the personal values included while the key exists.
2. The codes, identities, organization and plain details only; the values held under
   the key never cross into the answer.

*Chosen: 2, the strictest reading.* The read serves "who was affected", which the codes
and identities answer. Reading 1 hands a person holding `audit:read` personal values the
operation does not need, and makes the same query answer differently before and after
erasure. Reading 2 grants least, and an entry reads the same either side of erasure, so
AC2 holds by construction. The contract is `IAuditTrail.OfSubjectAsync`, asked in the
administrative organization as entry 175 places the audit trail.

*Tests that pin it.*
`AuditStoreTests.PRIV_BREACH_002_AC2_TheTrailReadsTheSameBeforeAndAfterErasureAsync`,
`AuditTrailServiceTests` (both),
`AuditTrailEndpointTests` (all three).

*Chapter text that should change.* 09 section 8a could say that the entries carry the
record's codes and plain details and not the values held under the subject's key.

---

## 178. Every loosening of runtime configuration also needs `system:administer`

**Phase 8 · 2026-09-23 · Tier 3 · OPS-CFG-002, 10 section 2.1, AUTH-ABUSE-004, OPS-ALERT-004a**

*The question.* 10 section 2.1 gives `system:administer` as governing "**Loosening**
configuration changes (OPS-CFG-002) and granting the seeded administrative role", and
`config:manage` as "Changing configuration". The `restriction:edit` row says "Every
edit is a step-up action; a loosening also needs a reason and alerts" and names no
second permission. The alerting destination keys change through
`PUT /admin/config/{key}` (10 section 5, `alerting:destinations`). No chapter says
where the permission to loosen is asked, or whether the restriction set, which is
runtime configuration (D-142), is one of the "configuration changes" it governs.

*The readings.*

1. `config:manage` alone; `system:administer` is never asked on a change.
2. `system:administer` asked on a loosening through `PUT /admin/config/{key}` only.
3. `system:administer` asked on every loosening of runtime configuration, the
   restriction set and the alerting destinations included, where the direction is
   classified.

*Chosen: 3, the strictest reading.* The one operation every runtime write goes through
(`ConfigurationAdministration`) asks it in the administrative organization as soon as
it has read the value in force and found the change to be a loosening, before the
step-up and the reason. It is asked in the same transaction as the read that decides
the direction, so a concurrent change cannot turn a tightening into an unpermitted
loosening. A tightening asks nothing more than the route's own permission. Reading 1
lets a holder of `config:manage` loosen what 10 section 2.1 reserves; reading 2 leaves
the restriction set, the widest loosening a send can meet, to `restriction:edit` alone.

*Tests that pin it.*
`ConfigurationAdministrationTests.OPS_CFG_002_ALooseningIsRefusedWithoutSystemAdministerAsync`,
`ConfigurationAdministrationTests.OPS_CFG_002_ATighteningNeedsNoSystemAdministerAsync`,
`ConfigurationEndpointTests.OPS_CFG_002_ALooseningRequiresSystemAdministerAsync`.

*Chapter text that should change.* The `restriction:edit` row of 10 section 2.1 could
say that a loosening also needs `system:administer`, or say that it does not.

---

## 179. A change through the configuration route carries a reason whichever way it moves

**Phase 8 · 2026-09-23 · Tier 2 · 09 section 8, OPS-CFG-002, OPS-CFG-005, D-147**

*The question.* 09 section 8 says of `PUT /admin/config/{key}` that "`reason` is
required on every change and recorded in the audit entry (OPS-CFG-005, OPS-CFG-008;
D-147)". Its 422 row lists "`auth.restriction.reasonrequired` where a loosening arrives
without a reason", and the closing paragraph says "Tightening requires no step-up.
Loosening requires step-up, a reason, and produces an audit entry."

*The readings.*

1. A reason only on a loosening; a tightening may arrive without one.
2. A reason on every change through the route; the 422 row names the case where it
   costs most.

*Chosen: 2.* The request shape states it for every change and the audit entry records
it; a tightening that carries no reason is a record that answers "why" with nothing.
The route refuses a blank or missing reason with `auth.restriction.reasonrequired`
(422) naming the key, before the value is read. The one operation underneath still
asks a reason only of a loosening, because the restriction set, which 09 section 8
asks a reason of only on a loosening, goes through it too.

*Tests that pin it.*
`ConfigurationEndpointTests.OPS_CFG_005_EveryChangeCarriesAReasonAsync`.

*Chapter text that should change.* The 422 row of `GET|PUT /admin/config/{key}` could
read "`auth.restriction.reasonrequired` where a change arrives without a reason".

---

## 180. The configuration route serves the deployment's own keys, the restriction set excepted

**Phase 8 · 2026-09-23 · Tier 2 · 09 section 8, 10 section 4, AUTH-ABUSE-004, D-151**

*The question.* 09 section 8 mounts `GET|PUT /admin/config/{key}` and says "`value`
takes the key's type (`10` section 4)". 10 section 4 holds keys that exist once for the
deployment, the named restriction set (`restrictions`, which 09 section 8 edits through
`/admin/restrictions` under `restriction:edit`), and keys that exist once per
organization or per declared category (D-151: `policy.{organization}`,
`photo.enabled.{organization}`, `stepup.enforcement.{organization}`,
`retention.{category}`). The route lists no answer for a name that is no key.

*The readings.*

1. Every key of 10 section 4 through this route, the restriction set and the family
   members included.
2. The keys that exist once for the deployment, less the restriction set; any other
   name is not a key of this route.

*Chosen: 2, the smaller surface.* Serving the restriction set here would let a holder
of `config:manage` edit it round `restriction:edit` and round the alert its loosening
raises. A family member belongs to its organization or category, and the organization
policy has its own route under `organization:manage`. A name the route does not serve,
and a name outside the catalogue, answer 400 `api.request.malformed` naming `key`: the
route lists no 404, and the name is part of the request.

*Tests that pin it.*
`ConfigurationEndpointTests.AUTH_ABUSE_004_TheRestrictionSetIsNoKeyOfTheConfigurationRouteAsync`,
`ConfigurationEndpointTests.LIB_API_005_ANameOutsideTheCatalogueIsMalformedAsync`.

*Chapter text that should change.* 09 section 8 could say which keys the route serves
and add the 400 for a name that is not one of them.

---

## 181. A configuration value crosses the interface in its own JSON type

**Phase 8 · 2026-09-23 · Tier 2 · 09 section 8, 10 section 4, 10 section 1.5, D-153**

*The question.* 09 section 8 says `GET` returns "`{ key, value, default, protected,
direction }` (D-153)" and that "`value` takes the key's type (`10` section 4)". The
chapters do not say how each type is written in JSON, what `default` is for a key the
deployment names, or how `direction` is spelled.

*The readings.*

1. Every value as the text the settings table stores, in a JSON string.
2. Every value in its own JSON type: text, durations and enum members as strings in the
   form 10 section 4 writes them; flags as booleans; numbers as numbers; lists and sets
   as arrays; a policy as its object.

*Chosen: 2.* "Takes the key's type" is a type, not a string holding one. A value of
another JSON type is refused with `config.value.notallowed` naming the key, which 10
section 1.5 gives for a value "of the wrong type": a number for a duration, a string
for a number. `default` is null for a key the deployment names (LIB-HOST-001), since
it has none. `direction` is the member name (`Increase`, `Decrease`, `AnyChange`), as
every other view writes an enum.

*Tests that pin it.*
`ConfigurationEndpointTests.OPS_CFG_004_AKeyReadsWithItsDefaultAndWhetherItIsProtectedAsync`,
`ConfigurationEndpointTests.OPS_CFG_008_AChangedKeyReadsBackBesideItsDefaultAsync`,
`ConfigurationEndpointTests.OPS_CFG_003_AValueOfTheWrongTypeIsNotAllowedAsync`.

*Chapter text that should change.* 09 section 8 could show one `GET` answer and name
the spelling of `direction`.


# Rows for chapter 10

D-162 section E names codes, keys, declarations and vocabularies the library now
carries and chapter 10 does not yet hold rows for. Each is listed with what the code
does, so the row can be written from it. Nothing here is a decision; the shapes are
D-162's.

## Section 1, error codes

The subsection each row belongs in is named with it.

| Code | Section | Status | Raised when |
| --- | --- | --- | --- |
| `api.request.malformed` | 1.5 | 400 | The request could not be read: its body is not the shape the endpoint takes, or a member it requires is absent or empty. `details.member` names the member the reader stopped at, or the one the endpoint required, and carries nothing of its value; where the body failed before any member, the refusal carries the code alone (API-CONV-002). |
| `identity.registration.signedin` | 1.1 | 409 | `POST /register` arrives from a browser holding a live session. Nothing is staged and no account document is answered; the frontend navigates to the account application (REG-SESS-002). |
| `auth.password.toolong` | 1.2 | 422 | A password longer than `password.maximum` is set, at registration, at a password change or at a reset. Nothing is truncated. |
| `identity.takedown.notfound` | 1.1 | 404 | The takedown of an account is read at `GET /admin/accounts/{subject}/takedown`, by a caller holding `takedown:execute`, and the account was never taken down (IDN-LIFE-003 AC2, entry 169). |
| `privacy.document.notfound` | 1.4 | 404 | A legal document, or a named version of one, that does not exist or was never published is read, or a translation is filed against one. |
| `privacy.notice.unpublished` | 1.4 | 409 | A consent is granted before any privacy-notice version has been published, so there is no version for it to stand against (PRIV-CONS-005). |
| `privacy.purpose.noconsent` | 1.4 | 422 | A consent is granted or withdrawn on a purpose the deployment did not declare, or one that rests on a basis other than consent, so it is not the subject's to agree to (PRIV-CONS-008a). |
| `privacy.request.notfound` | 1.4 | 404 | A decision is made on an identifier that names no privacy request, by a caller holding `privacyrequest:manage` (PRIV-RIGHT-001). |
| `privacy.request.decided` | 1.4 | 409 | A decision is made on a privacy request that is already decided; the standing decision is not replaced (PRIV-RIGHT-002 AC5). |
| `model.startup.redirectclient` | 1.5 | 500 | Startup: a registered client's return address is not an absolute address with a host, or `redirect.defaultclient` names no registered browser application. `details.client` names the client the bad address was read from; `details.key` names the setting where the configured default will not resolve (API-REDIR-001). |

## LIB-HOST-001, host declarations

| Declaration | Required | Absent |
| --- | --- | --- |
| `PasskeyAddresses` (`changePassword`, `enrol`, `manage`) | yes, no default | Startup fails with `model.startup.declarationmissing`; `details.key` names `passkeyAddresses` or the field of it that is empty. The addresses are the frontend pages `/.well-known/change-password` and `/.well-known/passkey-endpoints` point at (REG-PM-001). |
| `AuthenticationAddresses` (`signIn`, `provider`) | yes, no default | Startup fails with `model.startup.declarationmissing`; `details.key` names `authenticationAddresses.signIn` or `authenticationAddresses.provider`. The first is where an authorization request that is not silent and holds no session is forwarded (AUTH-SESS-012 AC3). The second is the address the library is mounted at on the authentication application, which is where another application finds `/oidc/authorize` and `/oidc/token` (BFF-SESS-006). |
| `SignOnClient` (`clientId`) | yes, no default | Startup fails with `model.startup.declarationmissing`; `details.key` names `signOnClient.clientId`. The identifier is what this application calls itself at the provider when it establishes its own session, and the registry holds the one destination a code returns to under it. The secret it presents is not a declaration: it comes from the secrets manager through `ISecretSource.ReadSignOnSecretAsync` and is passed to `AddJanus`, which refuses to start without it with `model.startup.keyunavailable` and `details.key` naming `signOnSecret` (BFF-SESS-006, OPS-SEC-001). |
| `ImageCodec` (`Reencode`) | optional, and required while any organization shows photos | Startup fails with `model.startup.declarationmissing` and `details.key` naming `imageCodec` where a `photo.enabled.<organization>` key is on and no codec is registered. The callback is `Func<ReadOnlyMemory<byte>, int, CancellationToken, ValueTask<ReadOnlyMemory<byte>?>>`: the uploaded bytes and the longest side in pixels the stored image is held to, answering the re-encoded JPEG with every metadata segment removed, or nothing where the bytes are not an image the deployment accepts. Nothing it answers chooses a code: a refusal is `identity.photo.invalid` (IDN-ATTR-002, IDN-ATTR-004). |

## Shipped default declarations

Two lists PRIV-BASIS-001, PRIV-SENS-001 and `07` LIB-HOST-001 require the library to
ship and which no file held until now (D-162 item 156). They are declarations a
deployment passes to the builder, not defaults the library registers by itself.

| Catalogue | Holds | Read where |
| --- | --- | --- |
| `LawfulBases.Default` | The six of `10` section 5.7, in the order PRIV-BASIS-001 tables them, each carrying `IsConsent`, `RequiresWrittenConsentForSensitive`, `RequiresAssessment` and `IsObjectable` as that table gives them. | A deployment passes each to `AuthorizationDeclarationBuilder.LawfulBasis`. The library reads the four properties and never the key. |
| `SensitiveCategories.Default` | The eight of `10` section 5.9, in its order. | A deployment passes each to `AuthorizationDeclarationBuilder.SensitiveCategory`. `SensitiveCategories.Children` is the one member the library reads by name, for the children's column of the register (PRIV-ROPA-001). |

## Section 3, the `resources` table

| Column | Type | Holds |
| --- | --- | --- |
| `subject` | `uuid`, nullable | The data subject of the record, which the host supplies when it registers it, reading the column its resource type declares for its encrypted fields. Absent where the record is about nobody. The consent gate of PRIV-SENS-002 reads this subject's consent for the purpose bound to the action. |

## AUTHZ-MODEL-003, startup refusals

| Refusal | Code | `details.key` |
| --- | --- | --- |
| A consent-based purpose is bound to a resource type whose encrypted fields name no one subject column, or name two different ones, so the record has no data subject whose consent could admit the action. | `model.startup.declarationmissing` | `<type>.<purpose>`, the type and the purpose that cannot be gated. |

## Register findings

`RegisterFinding` is a closed vocabulary; chapter 10 carries no list of it yet. This is
the member added since (D-162 item 104).

| Finding | Raised when |
| --- | --- |
| `children-undeclared` | `registration.adultaffirmation` is `off` and no resource type declares the `children` sensitivity category, so the children's column of the register is empty (PRIV-ROPA-001, PRIV-MINOR-001). |

## Sensitivity categories the library reads by name

| Category | Read where |
| --- | --- |
| `children` | The children's column of the records of processing is true for a purpose exactly where a type it is declared on declares this category (PRIV-SENS-001, PRIV-ROPA-001, D-162 item 104). It is the only category the library reads by name. |

## Section 4, configuration keys

| Key | Type | Scope | Default | Named when |
| --- | --- | --- | --- | --- |
| `password.blocklist.selfhosted.address` | string | R | none | Required where `password.blocklist.source` is `selfHosted`. Where the deployment's own corpus serves the ranges the primary source serves. |
| `integration.mail.endpoint` | string | P | none | Where the shipped default mail transport is called. Empty while the deployment supplies a transport of its own; required only when the shipped one is used, and refused at startup where it is not TLS (INT-GEN-001, LIB-EXT-001). |
| `integration.sms.endpoint` | string | P | none | Where the shipped default SMS transport is called. Empty while the deployment supplies a transport of its own; required only when the shipped one is used, and refused at startup where it is not TLS (INT-GEN-001, INT-SMS-001). |
| `photo.enabled.<organization>` | flag | R, one key per organization | `false` | Whether the organization's accounts show a profile photo (IDN-ATTR-002). It is not a field of the policy object of section 4.1a, as `stepup.enforcement.<organization>` is not: the object states six fields. The administrative organization's key is written `true` at bootstrap, as that organization's policy is. An account of no organization shows no photo; an account of several shows one only where every one of them shows one. Turning it on is loosening (OPS-CFG-002). |
| `registration.events.pollinterval` | duration | R | `PT1S`, floor `PT1S` | How often the waiting screen's stream reads the registration state back where no signal has reached it. The database channel is what usually wakes it; the interval is the fallback, and the floor is the default because a press has to feel immediate (REG-SESS-003, FE-VER-001). |
| `redirect.defaultclient` | string | P | none | The registered client a browser falls back to where the identifier a request carried is not one the registry holds. It names a client of kind `browser-application`, and startup refuses a value that names a client the registry does not hold or one of kind `protocol`. Empty while the deployment names none, in which case a registration begun with an unrecognised identifier stores nothing and its completion carries no return address (API-REDIR-001, API-REDIR-002). |

## Section 5, message places

The places a template leaves for the library's values, and the width each is measured at
when a template is checked against its text-message budget at startup (INT-SMS-003,
D-162 item 26). A place the library does not fill is left as it stands.

| Place | Width | What it carries |
| --- | --- | --- |
| `code` | the verification code's digits | The code a person is to enter. |
| `token` | the width of a drawn token | The link a person is to open. |
| `condition` | the widest written alert condition | Which condition was raised. |
| `raisedAt` | the width of an instant | When it was raised. |
| `restriction` | 64 | The restriction a grant or a loosening names. |
| `key` | the widest settings key | The setting a change names. |
| `destinationsBefore` | the width of a count | How many alert destinations stood before a change. |
| `destinationsAfter` | the width of a count | How many stand after it. |
| `balance` | the width of an amount | What the gateway account stands at. |
| `floor` | the width of an amount | The floor it is measured against. |
| `spentLastHour` | the width of an amount | What the last hour cost. |
| `document` | 64 | The governing document with no text. |
| `delivery` | the width of an identifier | The erasure delivery that exhausted its attempts. |
| `kind` | 32 | What that delivery carries. |
| `attempts` | the width of a count | How many attempts it made. |
| `outstanding` | 128 | The subscribers that have not confirmed it. |
| `request` | the width of an identifier | The privacy request whose deadline was reached. |
| `type` | the widest written request type | What was asked for. |
| `status` | the widest written request status | Where it had got to. |
| `decisionDue` | the width of an instant | When the decision was due. |

## Audit actions

Chapter 10 holds no list of audit actions yet. D-162 makes `AuditAction` a closed
vocabulary, so every action the library writes is listed here, member by member, as
the catalogue `Janus.Core.AuditActions` holds it. The category is the partition the
row is routed to, which is what its retention follows (PRIV-RET-002).

| Action | Category | Catalogue member | Written when |
| --- | --- | --- | --- |
| `auth.botdefence.signalled` | security | `AuditActions.BotDefenceSignalled` | The bot defence answered a send with a signal, which is recorded without the signal's own detail. (AUTH-ABUSE-009) |
| `auth.credential.countermismatch` | security | `AuditActions.CredentialCounterMismatch` | An authenticator presented a signature counter that did not advance, which is what a cloned credential looks like. (AUTH-FACT-002) |
| `auth.credential.enrolled` | security | `AuditActions.CredentialEnrolled` | A credential was enrolled on an account. (AUTH-FACT-001) |
| `auth.credential.invalidated` | security | `AuditActions.CredentialInvalidated` | A credential was invalidated by a loss report that took effect. (AUTH-REC-004) |
| `auth.credential.invalidationheld` | security | `AuditActions.CredentialInvalidationHeld` | An invalidation was held rather than carried out, because carrying it out would leave the account with no way in. (AUTH-REC-004) |
| `auth.credential.removed` | security | `AuditActions.CredentialRemoved` | A credential was removed from an account. (AUTH-FACT-001) |
| `auth.credential.reportcancelled` | security | `AuditActions.CredentialReportCancelled` | A loss report was cancelled before it took effect. (AUTH-REC-004) |
| `auth.credential.reportedlost` | security | `AuditActions.CredentialReportedLost` | A credential was reported lost, which starts the window before it is invalidated. (AUTH-REC-004) |
| `auth.oidc.refreshreused` | security | `AuditActions.RefreshTokenReused` | A refresh token was presented a second time, which revokes the family it belongs to. (AUTH-TOK-004) |
| `auth.phonesignal.considered` | security | `AuditActions.PhoneSignalConsidered` | A phone signal was consulted before a send, recorded without the number it was consulted for. (AUTH-ABUSE-006) |
| `auth.recovery.approved` | security | `AuditActions.RecoveryApproved` | An assisted recovery was approved, naming the approver and the reason given. (AUTH-REC-006) |
| `auth.restriction.edited` | security | `AuditActions.RestrictionEdited` | A sending restriction was edited. (AUTH-ABUSE-005) |
| `auth.restriction.granted` | security | `AuditActions.RestrictionGranted` | A sending restriction was granted against an address or a number. (AUTH-ABUSE-005) |
| `auth.session.presented` | security | `AuditActions.SessionPresented` | A session was presented, which is what a sign-in history is read from. (AUTH-SESS-010) |
| `authz.access.denied` | security | `AuditActions.AccessDenied` | A permission was refused, which is the row the refusal's correlation identifier resolves to. (AUTHZ-CONCEAL-004) |
| `identity.account.deactivated` | routine | `AuditActions.AccountDeactivated` | An account was deactivated by its own owner. (IDN-LIFE-013) |
| `identity.account.reactivated` | routine | `AuditActions.AccountReactivated` | A deactivated account was stood back up. (IDN-LIFE-013) |
| `identity.credential.labelled` | routine | `AuditActions.CredentialLabelled` | A credential was given or renamed a label by its holder. (REG-PM-002) |
| `identity.deletion.cancelled` | routine | `AuditActions.DeletionCancelled` | A deletion was cancelled inside its grace window. (IDN-LIFE-014) |
| `identity.deletion.requested` | routine | `AuditActions.DeletionRequested` | A deletion was requested, which opens the grace window it can be brought back from. (IDN-LIFE-014) |
| `identity.organization.erased` | routine | `AuditActions.OrganizationErased` | An organization's deletion grace window elapsed and the erasure executed. Details carry `organization`, `deletingSince` and `membershipsEnded`; the row names no subject and no actor. (IDN-ORG-003) |
| `identity.preferences.changed` | routine | `AuditActions.PreferencesChanged` | The account's preference values were changed, recorded by key and never by value. (REG-PREF-001) |
| `identity.profile.changed` | routine | `AuditActions.ProfileChanged` | A profile attribute of the account was changed. (IDN-ATTR-001) |
| `identity.secondstep.preferred` | routine | `AuditActions.SecondStepPreferred` | The account's preferred second step was changed. (AUTH-FACT-007) |
| `identity.takedown.executed` | security | `AuditActions.TakedownExecuted` | Phase one of a takedown committed: the account entered its window, its sessions ended and the hosts' delivery was written. Details carry `takedown`, `trigger` (spelled as `10` section 5.12d), `reason` and `erasureDue`. (IDN-LIFE-003) |
| `identity.takedown.reversed` | security | `AuditActions.TakedownReversed` | A takedown was reversed inside its window and the account restored to active. Details carry `reason`. (IDN-LIFE-003) |
| `identity.username.changed` | routine | `AuditActions.UsernameChanged` | The account's username was changed, which holds the old one for as long as the retention says. (REG-IDENT-009) |
| `ops.configuration.changed` | security | `AuditActions.ConfigurationChanged` | A runtime setting is put in force through the one configuration operation. Details carry `key`, `before`, `after`, `loosening` and, where the change is a loosening, `reason`. (OPS-CFG-002, OPS-CFG-005) |
| `privacy.consent.granted` | security | `AuditActions.ConsentGranted` | A consent was granted for a purpose, naming the document version it was given against. (PRIV-CONS-004) |
| `privacy.consent.withdrawn` | security | `AuditActions.ConsentWithdrawn` | A consent was withdrawn for a purpose. (PRIV-CONS-008) |
| `privacy.document.published` | security | `AuditActions.DocumentPublished` | A version of a legal document was published in the governing language. (PRIV-CONS-005) |
| `privacy.document.translated` | security | `AuditActions.DocumentTranslated` | A translation was filed against a published version of a legal document. (PRIV-CONS-005) |
| `privacy.erasure.executed` | security | `AuditActions.ErasureExecuted` | An erasure was carried out, which destroys the subject key and leaves the trail resolving. (PRIV-RIGHT-005) |
| `privacy.export.assembled` | security | `AuditActions.ExportAssembled` | A subject export was assembled and made available to the subject. (PRIV-RIGHT-003) |
| `privacy.objection.recorded` | security | `AuditActions.ObjectionRecorded` | An objection to a purpose was recorded. (PRIV-BASIS-003) |
| `privacy.objection.withdrawn` | security | `AuditActions.ObjectionWithdrawn` | An objection to a purpose was withdrawn and the purpose resumed. (PRIV-BASIS-003) |
| `privacy.request.entered` | security | `AuditActions.RequestEntered` | A data subject request entered the queue staff work. (PRIV-RIGHT-002) |
| `privacy.request.fulfilled` | security | `AuditActions.RequestFulfilled` | A data subject request was fulfilled. (PRIV-RIGHT-002) |
| `privacy.request.lapsed` | security | `AuditActions.RequestLapsed` | A data subject request reached its deadline undecided. (PRIV-RIGHT-002) |
| `privacy.request.refused` | security | `AuditActions.RequestRefused` | A data subject request was refused, with the reason recorded against it. (PRIV-RIGHT-002) |
| `privacy.request.submitted` | security | `AuditActions.RequestSubmitted` | A data subject request was submitted by the subject. (PRIV-RIGHT-002) |

## Message kinds

`MessageKind` is a closed set and chapter 10 carries no list of it. Every member is
listed here: the name the deployment's catalogue is asked by, and what the library
asks for it. The library never holds the words (CONV-CONTENT-001).

| Key | Member | Asked for when |
| --- | --- | --- |
| `account-exists` | `MessageKind.AccountExists` | The answer to a registration or a change made with an address an account already holds, sent to the holder and never to the person who tried. |
| `alert` | `MessageKind.Alert` | A condition the operator has to see. |
| `credential-enrolled` | `MessageKind.CredentialEnrolled` | A credential was enrolled on the account. |
| `deactivation-notice` | `MessageKind.DeactivationNotice` | The word to an account that has just deactivated itself, carrying the link that stands it back up (IDN-LIFE-013). |
| `deletion-notice` | `MessageKind.DeletionNotice` | The word to an account whose deletion grace window has begun, carrying the link that cancels it where the deletion is the account's own (IDN-LIFE-014). |
| `enrolment-link` | `MessageKind.EnrolmentLink` | A link that carries an admin-assisted enrolment. |
| `identifier-added` | `MessageKind.IdentifierAdded` | An identifier was added to the account. |
| `identifier-change-confirm` | `MessageKind.IdentifierChangeConfirm` | The address being displaced by a change is asked to confirm it, which is asked only where the account has no other channel at all. |
| `identifier-detached` | `MessageKind.IdentifierDetached` | The identifier that was removed no longer reaches the account. It carries no link and no powers. |
| `identifier-removed` | `MessageKind.IdentifierRemoved` | An identifier was removed, sent to the members of the security-notice set that remain and carrying the link that undoes it. |
| `identifier-settings-changed` | `MessageKind.IdentifierSettingsChanged` | The primary identifier of a kind, or the kind's backup setting, changed. |
| `no-account` | `MessageKind.NoAccount` | The answer to a request made for an address no account holds. |
| `privacy-request-lapsed` | `MessageKind.PrivacyRequestLapsed` | The honest word to a subject whose out-of-band erasure request reached its deadline undecided (PRIV-RIGHT-002). |
| `privacy-request-received` | `MessageKind.PrivacyRequestReceived` | The automatic receipt a data subject request gets the moment it enters the queue, which is not a decision and starts nothing (PRIV-RIGHT-002). |
| `recovery-link` | `MessageKind.RecoveryLink` | The link a person asked for to set a new password, which restores nothing else and removes no factor. |
| `secondstep-code` | `MessageKind.SecondStepCode` | A code presented as a second step. |
| `security-notice` | `MessageKind.SecurityNotice` | A notice that something happened to the account. |
| `signin-link` | `MessageKind.SignInLink` | A link that signs the person in. |
| `verification-code` | `MessageKind.VerificationCode` | A code that proves control of an address or a number. |
