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

**Superseded by D-165.** The host's own callbacks are mounted at paths it chooses; the library's routes stay a fixed list. Applied in entry 276.

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

**Superseded by D-165.** The callbacks only; break-glass stays with phase 9. Applied in entry 276.

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

**Superseded by D-165.** Applied in entry 270.

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

**Revised by entry 358.**

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
the page. **Revised in phase 10:** a page reads the consents of every subject on it in one
query (AUTHZ-GATE-005 AC1). Startup validation refuses a deployment that binds a consent-based purpose to
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

**Superseded by D-165.** Applied in entry 270.

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
with `model.startup.kekunavailable`. Nothing of it is written anywhere. The proof key,
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

---

## 182. A host key with no supplier is refused at the edit as a value the set does not admit

**Phase 8 · 2026-09-23 · Tier 2 · 09 section 8, LIB-HOST-001, 10 section 1.5, AUTH-ABUSE-004**

*The question.* 09 section 8 answers `PUT /admin/restrictions/{name}` with "**422**: a
`host:<name>` key with no registered supplier (LIB-HOST-001); an empty bucket list" and
names no code. The only code 10 gives the absence is `model.startup.declarationmissing`:
"Startup: a required deployment value, subject-event handler or restriction key
supplier is absent; `details.key`, `details.handler` or `details.supplier` names it",
which the library answers 500 wherever a request meets it, since at request time it is
a fault of the deployment.

*The readings.*

1. `model.startup.declarationmissing`, answered 422 on this route and 500 elsewhere.
2. `model.startup.declarationmissing` as it stands, answered 500.
3. `config.value.notallowed` (422), naming the key `restrictions` and the `supplier`.

*Chosen: 3.* The status of a code is one status wherever it is raised, so reading 1
would turn every request-time missing declaration into a 422 the caller is told to fix.
Reading 2 answers what 09 gives as 422 with a server fault. At the edit the supplier's
absence makes the restriction a value the set does not admit, which is what
`config.value.notallowed` means; `details.supplier` names it as the startup code
would. A send that meets a host key with no supplier is still refused with the startup
code, because there the deployment is at fault.

*Tests that pin it.*
`RestrictionEndpointTests.LIB_HOST_001_AHostKeyWithNoSupplierIsRefusedAsync`.

*Chapter text that should change.* The 422 row of `PUT /admin/restrictions/{name}`
could name `config.value.notallowed` with `details.supplier`.

---

## 183. What the restriction routes read, and what they answer for a name the set does not hold

**Phase 8 · 2026-09-23 · Tier 2 · 09 section 8, 10 section 4 `restrictions`, API-CONV-001**

*The question.* 09 section 8 gives the body of `PUT /admin/restrictions/{name}` as
`{ key, purpose, buckets }` and says a loosening "also requires a reason (OPS-CFG-002)",
without a field for it; it mounts `DELETE /admin/restrictions/{name}`, which "is a
loosening, not an error" for a shipped default and so needs a reason, without a body;
10 section 4 calls the purpose optional. No answer is listed for a `GET` or a `DELETE`
of a name the set does not hold.

*The readings and the choices.*

1. The reason travels as `reason` in the body of `PUT` and in a JSON body
   `{ "reason": "..." }` of `DELETE`, not in the query, where free text would sit in
   every access log. Chosen.
2. An absent `purpose` reads as `any`, which is what an optional purpose means. Chosen.
3. A `GET` or `DELETE` of a name the set does not hold answers 400
   `api.request.malformed` naming `name`, as entry 180 answers a name outside the
   configuration catalogue: no 404 is listed, and a deletion of nothing would otherwise
   write down and announce a change that changed nothing. Chosen.
4. A key or window outside the vocabulary, a negative `max`, or an `interval` that is
   no positive ISO 8601 duration is malformed, naming `key`, `purpose` or `buckets`;
   an empty bucket list is refused with `config.value.notallowed` (422) as 09 gives it.
   Chosen.

*Tests that pin it.*
`RestrictionEndpointTests.AUTH_ABUSE_004_OneRestrictionReadsByItsNameAsync`,
`RestrictionEndpointTests.AUTH_ABUSE_004_DeletingAShippedDefaultIsALooseningAsync`,
`RestrictionEndpointTests.AUTH_ABUSE_004_DeletingAnUnknownNameIsMalformedAsync`,
`RestrictionEndpointTests.AUTH_ABUSE_004_AnUnreadableRestrictionIsMalformedAsync`,
`RestrictionEndpointTests.AUTH_ABUSE_004_AnEmptyBucketListIsRefusedAsync`.

*Chapter text that should change.* 09 section 8 could show `reason` in the `PUT` body,
give `DELETE` its body, and add the 400 for a name the set does not hold.

---

## 184. How a grant names the whole organization, and what else it must name

**Phase 8 · 2026-09-23 · Tier 2 · 09 section 8, AUTHZ-GRANT-001, AUTHZ-SCOPE-001, API-CONV-001**

*The question.* 09 section 8 gives the body of `POST /admin/grants` as `subjectType`,
`subjectId`, `resourceType`, `resourceId`, `role`, `deny`, `expiresAt` and `reason`.
AUTHZ-GRANT-001 AC2 says "A grant with a null resource scopes to the whole
organization", and AUTHZ-SCOPE-001 that every evaluation is "scoped to the organization
owning the resource, resolved from the resource and never from the session". The body
has no member naming an organization, so a grant on the whole organization has no way
to say which. No answer is listed for a role, record or group the deployment does not
hold.

*The readings and the choices.*

1. A grant on the whole organization is written `resourceType: "organization"` and
   `resourceId` the organization's identifier, the type the gate already asks an
   organization-wide question under; it is stored with no resource. Chosen. The other
   reading, an `organizationId` member beside absent resource members, adds a member 09
   does not give.
2. `grant:manage` is asked in the organization the grant is scoped to: the one a record
   was registered in, or the one named. Chosen, as AUTHZ-SCOPE-001 reads; asking it in
   the administrative organization instead would leave no organization able to manage
   its own grants.
3. A record that is not registered, or an organization identifier that is not one,
   answers 400 `api.request.malformed` naming `resourceId`; a role the deployment does
   not hold names `role`; a group that does not exist or belongs to another
   organization names `subjectId`, since a group's members hold what it holds in its
   own organization. Chosen, as entry 180 answers a name outside the catalogue.
4. A user subject is taken as given. The area holds no port to accounts, and a grant to
   an identifier no account carries reaches nobody. Chosen.

*Tests that pin it.*
`GrantEndpointTests.AUTHZ_GRANT_001_AC2_AnOrganizationWideGrantIsWrittenWithNoResourceAsync`,
`GrantEndpointTests.AUTHZ_SCOPE_001_ARecordGrantIsScopedToTheRecordsOrganizationAsync`,
`GrantEndpointTests.AUTHZ_SCOPE_001_GrantManageElsewhereDoesNotReachTheRecordAsync`,
`GrantEndpointTests.AUTHZ_GRANT_001_WhatAGrantNamesMustExistAsync`.

*Chapter text that should change.* 09 section 8 could say how the body names the whole
organization, that `grant:manage` is asked in the grant's organization, and add the 400
for a role, record or group the deployment does not hold.

---

## 185. Which grants of system administration need system administration

**Phase 8 · 2026-09-23 · Tier 3 · OPS-CFG-007, 10 section 2.1, AUTHZ-GRANT-002**

*The question.* OPS-CFG-007 says "Granting or revoking system administration SHALL
require system administration", and 10 section 2.1 that `system:administer` governs
"granting the seeded administrative role". Permissions reach a principal through
roles, and a deny takes one away (AUTHZ-GRANT-002). Neither text says whether the rule
covers a role other than the seeded one that carries the permission, a deny, or a
grant in an organization other than the administrative one, where the permission is
never asked.

*The readings.*

1. Only an allow of the seeded `system-administrator` role in the administrative
   organization needs it.
2. Any allow of a role carrying `system:administer`, in the administrative
   organization.
3. Any grant or revocation, allow or deny, in any organization, of a role carrying
   `system:administer` needs that permission in the administrative organization; a
   role that cannot be read counts as carrying it.

*Chosen: 3, the strictest reading.* Reading 1 is the "one step removed" OPS-CFG-007
exists to prevent, reached by copying the role under another name. A deny of it takes
system administration away, which is the revocation the item names. A grant in
another organization is still a record of the permission held, so no organization is
left out. The check fails closed where the role cannot be read.

*Tests that pin it.*
`GrantEndpointTests.OPS_CFG_007_AC1_AGrantManagerWithoutSystemAdministerCannotConferItAsync`,
`GrantEndpointTests.OPS_CFG_007_RevokingSystemAdministrationRequiresItAsync`.

*Chapter text that should change.* OPS-CFG-007 could say that it covers every role
carrying the permission, deny grants, and every organization.

---

## 186. What the grant routes answer beyond the rows 09 lists

**Phase 8 · 2026-09-23 · Tier 2 · 09 section 8, 10 section 1.3, AUTHZ-GRANT-003, API-CONV-002**

*The question.* 09 section 8 lists **201** / **204** and **409**
`authz.grant.duplicate` for `POST /admin/grants` and `DELETE /admin/grants/{id}`, and
says "`reason` is recorded. Revocation records the revoking actor", without a body for
`DELETE`. 10 section 1.3 gives `authz.grant.expired` ("Grant past its expiry"),
`authz.grant.notfound` ("No such grant") and `authz.grant.reasonrequired`, 422, and
API-CONV-002 bounds every free-text field at 1 to 1024 characters after trimming.

*The readings and the choices.*

1. A grant whose `expiresAt` is at or before the present is refused with
   `authz.grant.expired`, 409 as the status map already answers it, rather than written
   as a row that confers nothing. Chosen.
2. `DELETE` of an identifier that is unknown, already revoked, or a derived or
   materialised grant answers 404 `authz.grant.notfound`: only a row someone wrote is
   revoked by someone, and a derivation's refresh would write the other back. Chosen.
3. The revocation's reason travels as `{ "reason": "..." }` in the body of `DELETE`, as
   entry 183 carries a deletion's reason. Chosen.
4. A blank reason is `authz.grant.reasonrequired` (422); one longer than 1024
   characters is 400 `api.request.malformed` naming `reason`. Chosen.
5. The 201 body is `{ "id": "..." }`, the camelCase noun API-CONV-002 gives. Chosen.
6. The step-up is judged after everything else, so nobody is asked to prove themselves
   for a request that would be refused. Chosen.

*Tests that pin it.*
`GrantEndpointTests.AUTHZ_GRANT_003_AnExpiryIsRecordedAndOneAlreadyPassedIsRefusedAsync`,
`GrantEndpointTests.AUTHZ_GRANT_001_OnlyAnUnrevokedStoredGrantIsRevokedAsync`,
`GrantEndpointTests.AUTHZ_GRANT_003_ABlankReasonIsRefusedAsync`,
`GrantEndpointTests.API_CONV_002_AReasonPastTheLimitIsMalformedAsync`,
`GrantEndpointTests.AUTHZ_GRANT_003_AC2_TheRevocationRecordsWhoWhenAndWhyAsync`,
`GrantEndpointTests.AUTH_STEP_001_GrantingAndRevokingAreStepUpActionsAsync`.

*Chapter text that should change.* 09 section 8 could list the 404, the 409
`authz.grant.expired`, the 422 and the 400, show the 201 body, and give `DELETE` its
body.

---

## 187. What the role routes carry, and where `role:manage` is asked

**Phase 8 · 2026-09-23 · Tier 2 · 09 section 8, AUTHZ-GRANT-004, 10 section 2.1, API-CONV-001**

*The question.* 09 section 8 gives `GET|POST|DELETE /admin/roles` as "Runtime role
management" with no body, no answers and no path for one role. 10 section 2.1 gives
`role:manage` as governing "Creating and changing roles", and 10 section 3 says the
seeded roles are "editable after bootstrap". No route changes a role that stands, and
no chapter says where the permission is asked for a role, which belongs to no
organization.

*The readings and the choices.*

1. `role:manage` is asked in the administrative organization, for reading as well as
   for changing: a role is the deployment's, as the restriction set is (entry 175).
   Chosen. Reading the roles under `grant:read` was the other reading; it lets the
   auditor read what only role managers change, and 10 does not name it.
2. `POST /admin/roles` carries `{ name, permissions, reason }` and either creates the
   role (201) or gives the one by that name the permissions stated (204). Chosen, as
   the only way to change a role within the three methods 09 lists; a `PUT` route would
   add one 09 does not have.
3. `DELETE /admin/roles/{name}` names the role in the path, as `DELETE /admin/grants/{id}`
   does, and carries `{ "reason": "..." }` in its body (entry 183). Chosen.
4. A name that is not a role name, or a role the deployment does not hold, answers 400
   `api.request.malformed` naming `name`; a permission neither the library nor the
   host declares names `permissions` (AUTHZ-MODEL-004), where startup would refuse the
   same role with `model.role.undeclaredpermission`. Chosen, as entry 182 keeps the
   startup code for a fault of the deployment.

*Tests that pin it.*
`RoleEndpointTests.AUTHZ_GRANT_004_AC1_ARoleIsCreatedWithItsPermissionsWithoutARestartAsync`,
`RoleEndpointTests.AUTHZ_GRANT_004_ARoleIsChangedInPlaceAsync`,
`RoleEndpointTests.AUTHZ_MODEL_004_ARoleNamesOnlyDeclaredPermissionsAsync`,
`RoleEndpointTests.AUTHZ_CONCEAL_005_AC1_RoleManageIsAskedInTheAdministrativeOrganizationAsync`,
`RoleEndpointTests.AUTHZ_GRANT_004_OnlyARoleNothingNamesIsRemovedAsync`.

*Chapter text that should change.* 09 section 8 could give the body of `POST`, its 201
and 204, the path and body of `DELETE`, the 400s, and say that `role:manage` is asked
in the administrative organization.

---

## 188. A change to a role is reasoned, audited, and guarded as a grant of what it carries

**Phase 8 · 2026-09-23 · Tier 3 · AUTHZ-GRANT-004, OPS-CFG-007, OPS-CFG-005, IDN-AUD-001, 10 section 5a**

*The question.* 10 section 5a makes `/admin/roles/*` the `grant:manage` step-up
action. No chapter says whether a change to a role needs a reason or is audited, though
AUTHZ-GRANT-003 requires both of a grant and OPS-CFG-005 describes grants as audited
"who, what, from, to, when, why". OPS-CFG-007 says granting or revoking system
administration requires it; adding `system:administer` to a role, or taking it out of
one, confers or removes it for every holder at once.

*The readings.*

1. A role change is stepped up and nothing more: no reason, no record, and
   `role:manage` alone suffices whatever the role carries.
2. A role change is stepped up and audited, with no reason.
3. A role change is stepped up, carries a reason of 1 to 1024 characters (400
   `api.request.malformed` naming `reason` otherwise, as the takedown's does), and is
   audited as `authz.role.defined` or `authz.role.removed` with the role, its
   permissions before and after, the reason and the actor; a change to a role that
   carries `system:administer` before or after the change also needs that permission
   in the administrative organization.

*Chosen: 3, the strictest reading.* A role change moves what every holder may do in
one write, which is a grant in all but name, so it keeps what a grant keeps. Reading 1
lets a role manager make any grant-holder a system administrator by editing their role,
which is the "one step removed" OPS-CFG-007 exists to prevent, and would let the seeded
`system-administrator` role be narrowed by someone who does not hold it.

*Tests that pin it.*
`RoleEndpointTests.OPS_CFG_007_AC1_ARoleCarryingSystemAdministrationNeedsItAsync`,
`RoleEndpointTests.AUTH_STEP_001_DefiningAndRemovingARoleAreStepUpActionsAsync`,
`RoleEndpointTests.AUTHZ_MODEL_004_ARoleNamesOnlyDeclaredPermissionsAsync`,
`RoleAuditTests.AUTHZ_GRANT_004_ADefinitionRecordsWhatTheRoleWasAndBecameAsync`,
`RoleAuditTests.AUTHZ_GRANT_004_ARemovalRecordsWhatTheRolePermittedAsync`.

*Chapter text that should change.* AUTHZ-GRANT-004 could require the reason and the
audit record of a role change, and OPS-CFG-007 could name role changes.

---

## 189. A role a grant or a derivation names is not removed, and the refusal has a code of its own

**Phase 8 · 2026-09-23 · Tier 2 · AUTHZ-GRANT-004, AUTHZ-GRANT-003 AC3, AUTHZ-DERIVE-001, 10 section 1.3**

*The question.* 09 section 8 mounts `DELETE /admin/roles` and says nothing of a role in
use. Every grant row names its role and keeps it after revocation, since AUTHZ-GRANT-003
AC3 asks that "who granted this and when" stay answerable by query, and the schema
holds the reference; a derivation confers a role from the host's data. 10 section 1.3
has no code for a role that cannot be removed.

*The readings.*

1. Removing a role revokes its live grants and deletes every row that names it.
2. Removing a role that anything names is refused as a malformed request.
3. Removing a role that a grant (live, expired or revoked) or a declared derivation
   names is refused with a new code `authz.role.inuse`, 409; the way to take its access
   away is to change its permissions or revoke its grants.

*Chosen: 3.* Reading 1 destroys the grant history AUTHZ-GRANT-003 keeps. Reading 2 tells
the caller the request was unreadable when it was read and refused. A removed role a
derivation still names would make that derivation confer nothing without any error,
so it is in use as much as one a grant names. The code is new and follows the shape of
the others in 10 section 1.3.

*Tests that pin it.*
`RoleEndpointTests.AUTHZ_GRANT_004_OnlyARoleNothingNamesIsRemovedAsync`,
`GrantStoreTests.AUTHZ_GRANT_004_ARoleAnyGrantConfersIsNamedAsync`.

*Chapter text that should change.* 10 section 1.3 could add
`authz.role.inuse`: "A grant or a derivation names the role, so it cannot be removed;
409", and 09 section 8 could list it under `DELETE /admin/roles`.

---

## 190. What the group routes carry, and where `group:manage` is asked

**Phase 8 · 2026-09-23 · Tier 2 · 09 section 8a, AUTHZ-GROUP-001, AUTHZ-SCOPE-001, 10 section 2.1, API-CONV-002**

*The question.* 09 section 8a gives `GET|POST|DELETE /admin/groups` and
`POST|DELETE /admin/groups/{id}/members` with no body, no answer and no path naming the
group to remove. 10 section 2.1 gives `group:manage` as governing "Creating groups,
nesting them, and changing their members". A group belongs to one organization, and no
chapter says where the permission is asked or which organization a listing reads.

*The readings and the choices.*

1. `group:manage` is asked in the organization the group belongs to, read from its row
   and never from the caller, as `grant:manage` is asked in the grant's (entry 184).
   Chosen. Asking it in the administrative organization was the other reading; it
   would put every organization's groups behind one permission a branch administrator
   does not hold.
2. `GET /admin/groups?organization={id}` answers 200 with
   `[ { id, name, members: [ { subjectType, subjectId } ] } ]`, the members being those
   the group holds directly. Chosen: no other route reads a group's members, and the
   transitive set is the gate's, not the management application's.
3. `POST /admin/groups` carries `{ organization, name, reason }` and answers 201
   `{ id }`. `DELETE /admin/groups/{id}` names the group in the path, as
   `DELETE /admin/grants/{id}` does, and carries `{ "reason": "..." }` in its body
   (entry 183). Chosen.
4. `POST` and `DELETE /admin/groups/{id}/members` both carry
   `{ subjectType, subjectId, reason }`, the member shape a grant's holder has, and
   answer 204. Adding a member already held, or taking out one that is not, answers 204
   and writes and records nothing. Chosen: the one address 09 gives serves both, and the
   state asked for holds.
5. A member group of another organization, a group the deployment does not hold, a
   name or reason blank or past 1024 characters, and an absent or unreadable
   `organization`, `subjectType` or `subjectId` answer 400 `api.request.malformed`
   naming the field (`subjectId`, `id`, `name`, `reason`, `organization`,
   `subjectType`). A member account is not looked up, as a grant's holder is not.
   Chosen.

*Tests that pin it.*
`GroupEndpointTests.AUTHZ_GROUP_001_AGroupIsCreatedAndReadWithItsMembersAsync`,
`GroupEndpointTests.AUTHZ_SCOPE_001_GroupManageIsAskedInTheGroupsOrganizationAsync`,
`GroupEndpointTests.AUTHZ_GROUP_001_AChangeThatChangesNothingRecordsNothingAsync`,
`GroupEndpointTests.AUTHZ_GROUP_001_WhatAChangeNamesMustBeReadableAsync`,
`GroupEndpointTests.AUTHZ_GROUP_001_AGroupThatWouldContainItselfIsRefusedAsync`,
`GroupClosureStoreTests.AUTHZ_GROUP_001_AnOrganizationsGroupsAreReadByNameAsync`.

*Chapter text that should change.* 09 section 8a could give the query of `GET`, the
bodies of `POST` and of both member routes, the path and body of `DELETE`, the 201 and
204 answers and the 400s, and say that `group:manage` is asked in the group's
organization.

---

## 191. A change of members is stepped up, reasoned, audited, and guarded as a grant of what the group holds

**Phase 8 · 2026-09-23 · Tier 3 · AUTHZ-GROUP-001, OPS-CFG-007, AUTH-STEP-001, 10 section 5a, IDN-AUD-001**

*The question.* 10 section 5a lists no step-up action for the group routes, and no
chapter says whether a change to a group carries a reason or is audited. A member
holds what the group holds and what every group holding it holds, so adding an account
to a group confers every grant the group reaches, and taking it out removes them.
OPS-CFG-007 says granting or revoking system administration requires it.

*The readings.*

1. The group routes ask `group:manage` and nothing more: no step-up, no reason, no
   record, whatever the group holds.
2. As 1, with every change audited.
3. Every write (create, remove, add a member, take one out) carries a reason of 1 to
   1024 characters (400 `api.request.malformed` naming `reason` otherwise) and is
   audited as `authz.group.created`, `authz.group.removed`,
   `authz.group.memberadded` or `authz.group.memberremoved` with the group, its name,
   the member where one changed, the reason, the actor and the group's organization. A
   change of members is the `grant:manage` step-up action, judged last as for a grant;
   where the group, or any group holding it, holds a live grant (allow or deny) of a
   role carrying `system:administer`, or of a role that cannot be read, the change
   also needs `system:administer` in the administrative organization. Creating a group
   and removing one nothing names (entry 192) confer and remove nothing, and are not
   stepped up.

*Chosen: 3, the strictest reading.* A change of members is a grant in all but name,
as a change to a role is (entry 188), so it keeps what a grant keeps. Reading 1 lets a
group manager make anyone a system administrator by adding them to the group that
holds it, which is the "one step removed" OPS-CFG-007 exists to prevent. The step-up
reuses the `grant:manage` gate name 10 section 5a already defines rather than
inventing one.

*Tests that pin it.*
`GroupEndpointTests.OPS_CFG_007_AC1_ChangingAnAdministeringGroupNeedsSystemAdministrationAsync`,
`GroupEndpointTests.AUTH_STEP_001_AChangeOfMembersIsAStepUpActionAsync`,
`GroupEndpointTests.AUTHZ_GROUP_001_AGroupIsCreatedAndReadWithItsMembersAsync`,
`GroupAuditTests.AUTHZ_GROUP_001_AMemberAddedIsRecordedWithTheGroupAndTheMemberAsync`,
`GroupAuditTests.AUTHZ_GROUP_001_AGroupCreatedIsRecordedWithoutAMemberAsync`.

*Chapter text that should change.* 10 section 5a could list
`POST|DELETE /admin/groups/{id}/members` under `grant:manage`; AUTHZ-GROUP-001 could
require the reason and the audit record of every change to a group; OPS-CFG-007 could
name a change of members of a group that reaches system administration; 10 section 5
could list the four audit actions.

---

## 192. A group anything names is not removed, and the refusal has a code of its own

**Phase 8 · 2026-09-23 · Tier 2 · AUTHZ-GROUP-001, AUTHZ-GRANT-003 AC3, 10 section 1.3**

*The question.* 09 section 8a mounts `DELETE /admin/groups` and says nothing of a
group in use. A grant given to a group names it and keeps it after revocation, since
AUTHZ-GRANT-003 AC3 asks that "who granted this and when" stay answerable by query. A
group's members lose what it holds when it goes, and a group it belongs to loses a
member, with no change of members recording either. 10 section 1.3 has no code for a
group that cannot be removed.

*The readings.*

1. Removing a group takes its members out, takes it out of every group holding it and
   revokes its live grants, each recorded.
2. Removing a group that anything names is refused as a malformed request.
3. Removing a group that holds a member, belongs to a group, or was given any grant
   (live, expired or revoked) is refused with a new code `authz.group.inuse`, 409;
   the members are taken out through the member route first, which is stepped up and
   recorded (entry 191).

*Chosen: 3*, as entry 189 chose for a role. Reading 1 does in one write, without its
step-up, what entry 191 guards change by change, and loses the grant history
AUTHZ-GRANT-003 keeps. Reading 2 tells the caller the request was unreadable when it
was read and refused.

*Tests that pin it.*
`GroupEndpointTests.AUTHZ_GROUP_001_OnlyAGroupNothingNamesIsRemovedAsync`,
`GrantStoreTests.AUTHZ_GROUP_001_AGroupAnyGrantWasGivenToIsNamedAsync`,
`GroupClosureStoreTests.AUTHZ_GROUP_001_ARemovedGroupIsGoneAsync`.

*Chapter text that should change.* 10 section 1.3 could add `authz.group.inuse`: "The
group holds a member, belongs to a group, or was given a grant, so it cannot be
removed; 409", and 09 section 8a could list it under `DELETE /admin/groups`.

---

## 193. What the organization lifecycle routes carry, and the policy row a new organization gets

**Phase 8 · 2026-09-23 · Tier 2 · 09 section 8a, IDN-ORG-002, IDN-ORG-003, 10 section 4, API-CONV-002**

*The question.* 09 section 8a gives `POST /admin/organizations`, which "Creates an
organization with its policy row (IDN-ORG-002)", and
`POST /admin/organizations/{id}/delete` · `/delete/cancel`, "Request → suspend → grace
→ erasure, cancellable (IDN-ORG-003)", with no body, no answer and no word on a repeat.
10 section 4 gives `policy.<organization>` a default of "`{}` (no override: every field
inherits `policy.default`)", "one key per organization identifier, created empty when the
organization is". The configuration store had no write for one member of such a key.

*The readings and the choices.*

1. `POST /admin/organizations` carries `{ name, reason }` and answers 201 `{ id }`.
   Chosen, as `POST /admin/groups` answers (entry 190).
2. The policy row: written as `{}` in the transaction that creates the organization,
   through a new `IConfigurationStore.WriteAsync` for one member of a family, or left
   unwritten since an unwritten member already reads as `{}`. Chosen: written. The
   chapter says the key is created with the organization, and a row the deployment
   holds is what `ReadWrittenAsync` answers for. It is written through the one
   configuration operation every runtime write goes through (OPS-CFG-005), so it is
   recorded as `ops.configuration.changed` with `{}` before and after, not a loosening,
   carrying the creation's reason.
3. `/delete` and `/delete/cancel` carry `{ "reason": "..." }` and answer 204. A request
   for an organization already being deleted, and a cancellation for one that is not,
   answer 204 and write and record nothing. Chosen: the state asked for holds, as for a
   group member already held (entry 190).
4. A name or reason blank or past 1024 characters after trimming, and an organization
   the deployment does not hold, answer 400 `api.request.malformed` naming `name`,
   `reason` or `id`. Chosen.

*Tests that pin it.*
`OrganizationEndpointTests.IDN_ORG_002_AnOrganizationIsCreatedAsync`,
`OrganizationEndpointTests.IDN_ORG_003_ASecondRequestChangesNothingAsync`,
`OrganizationEndpointTests.IDN_ORG_004_ARequestIsCancelledOnlyWithinTheWindowAsync`,
`OrganizationEndpointTests.IDN_ORG_002_WhatAChangeNamesMustBeReadableAsync`,
`OrganizationDirectoryTests.IDN_ORG_002_AnOrganizationCreatedIsFoundAsync`,
`ConfigurationStoreTests.OPS_CFG_008_AC1_AWrittenMemberOfAFamilyIsInForceForTheNextReadAsync`,
`ConfigurationStoreTests.WriteAsync_AMemberOfAProtectedFamily_IsRefusedAndWritesNothingAsync`,
`ConfigurationStoreTests.WriteAsync_AMemberValueTheFamilyDoesNotAdmit_IsRefusedAsync`,
`ConfigurationAdministrationTests.OPS_CFG_005_AChangedMemberOfAFamilyIsWrittenDownAsync`,
`ConfigurationAdministrationTests.OPS_CFG_004_AMemberOfAProtectedFamilyIsRefusedAsync`.

*Chapter text that should change.* 09 section 8a could give the bodies of the three
routes, the 201 and 204 answers, the 400s and the answer to a repeat, and say the policy
row is `{}` and recorded as a configuration change.

---

## 194. `organization:manage` is asked in the administrative organization

**Phase 8 · 2026-09-23 · Tier 3 · 10 section 2.1, IDN-ORG-003, AUTHZ-SCOPE-001**

*The question.* 10 section 2.1 gives `organization:manage` as "Organization lifecycle,
policy, and deletion cancellation" and does not say where it is asked. IDN-ORG-003 says
of a deletion request "Organization suspends immediately; access stops", which the
effective grants view now makes true of every grant of the organization (commit
`0f80902`, entry 196).

*The readings.*

1. Asked in the organization named in the path, as `group:manage` is asked in the
   group's (entry 190); creation, which names none, asked in the administrative one.
2. Asked in the administrative organization for all three routes.

*Chosen: 2, the strictest reading.* Under 1 an organization's own administrators could
delete it, and once they had, the grant that would cancel the request confers nothing,
so the request could only be undone by carving an exception into the suspension. Under
2 the permission to end an organization's access and to give it back sits where a
suspension never reaches, and a permission held in the organization itself grants
nothing here.

*Tests that pin it.*
`OrganizationEndpointTests.IDN_ORG_002_TheLifecycleIsGovernedFromTheAdministrativeOrganizationAsync`.

*Chapter text that should change.* 10 section 2.1 could say that `organization:manage`
is asked in the administrative organization.

---

## 195. A deletion request and its cancellation are stepped up under a new gate `organization:delete`

**Phase 8 · 2026-09-23 · Tier 3 · 09 section 8a, 10 section 5a, AUTH-STEP-001, IDN-LIFE-013**

*The question.* 10 section 5a lists the library's step-up actions "once here so a
builder does not have to infer them endpoint by endpoint", and names none for
`/admin/organizations/{id}/delete` or `/delete/cancel`. 09 section 8a says "step-up
applies where the operation loosens a control (OPS-CFG-002) or touches another person's
account". A deletion request ends every member's sessions (entry 196); a cancellation
gives every member back what the organization grants. 09 section 8a steps up the same
pair for one account: `POST /admin/accounts/{subject}/suspend` · `/reactivate`,
"reactivation restores grants exactly (IDN-LIFE-013). Requires step-up".

*The readings.*

1. Neither is stepped up, since 10 section 5a names no gate for them.
2. Both are stepped up under a gate name 10 section 5a already has (`grant:manage` or
   `account:suspend`).
3. Both are stepped up under a new gate `organization:delete`, "Request or cancel an
   organization's deletion", judged last as every step-up is; creating an organization,
   which touches no account, is not stepped up.

*Chosen: 3, the strictest reading.* 09 section 8a's own rule reaches both routes, and
the account pair it steps up is the same change made to one person. A borrowed name
would let a host that loosens the gate for grants or for one account loosen it for a
whole organization without saying so. One name for the pair is the smaller surface, as
`identifier:add` covers an add and a replace; `StepUpAction.OrganizationDelete` is
added to the closed set.

*Tests that pin it.*
`OrganizationEndpointTests.AUTH_STEP_001_ADeletionAndItsCancellationAreAStepUpActionAsync`,
`VocabularyContractTests` (the step-up names).

*Chapter text that should change.* 10 section 5a could add the row `organization:delete`
| Request or cancel an organization's deletion | `POST /admin/organizations/{id}/delete`,
`/delete/cancel`; 09 section 8a could say "Requires step-up" on that row.

---

## 196. A deletion request ends every member session, and a suspended organization's grants confer nothing

**Phase 8 · 2026-09-23 · Tier 3 · IDN-ORG-003 AC1 and AC2, IDN-ORG-001, entry 155**

*The question.* IDN-ORG-003 AC1: "Requesting deletion halts member access within one
request cycle: the first request on any member session that reaches the session record
after the commit is refused." AC2: "Cancelling on day 29 restores all memberships and
grants intact." Entry 155 recorded this criterion as "the one point of the three that
leaves a hole". Sessions carry no organization, and
IDN-ORG-001 makes an organization "NOT a tenancy or isolation boundary".

*The readings.*

1. Only the grants stop: the effective grants view leaves out every grant of an
   organization whose deletion was requested, and sessions live on.
2. As 1, and every live session of every current member ends in the transaction that
   suspends, the requester's own where they are a member.
3. As 2, and a member cannot sign in again while the organization is suspended.

*Chosen: 2, the strictest reading the chapters allow.* Reading 1 leaves a member's
session answering every request that needs no grant, which AC1 refuses. Reading 3 would
shut a person out of their own account, which belongs to the pool and not the
organization (IDN-ORG-001), including the privacy rights exercised through it and any
other membership they hold; no chapter ties signing in to an organization. The grants
half shipped in commit `0f80902` (`identity.effective_grants` joins the organization and
leaves out a row whose `deletion_requested_at` is set, so checks, filters and
capability arrays all see nothing); the sessions half ships with the lifecycle routes.
Ended sessions are not given back on cancellation: AC2 names memberships and grants,
and both are untouched. The "who can access" read of `GrantStore.OnAsync` still reads
the grants table; it is brought onto the view with the reverse lookup of
AUTHZ-DERIVE-007 in this phase.

*Tests that pin it.*
`GateBehaviourTests.IDN_ORG_003_AC1_ASuspendedOrganizationConfersNothingAsync`,
`OrganizationEndpointTests.IDN_ORG_003_AC1_ADeletionRequestEndsEveryMemberSessionAsync`,
`OrganizationDirectoryTests.IDN_ORG_003_AC1_OnlyCurrentMembersAreNamedAsync`,
`OrganizationDirectoryTests.IDN_ORG_004_ARequestIsKeptUntilItIsCancelledAsync`.

*Chapter text that should change.* IDN-ORG-003 could say that the request ends every
member session and that the organization's grants confer nothing while it is
suspended, and that a member may still sign in to their own account.

---

## 197. Every lifecycle change carries a reason and is audited under the organization

**Phase 8 · 2026-09-23 · Tier 3 · IDN-AUD-001, IDN-ORG-003, 10 section 5**

*The question.* No chapter says whether creating an organization, requesting its
deletion or cancelling the request carries a reason or is audited, and 10 section 5 has
no audit action for any of them. Only the erasure is recorded
(`identity.organization.erased`).

*The readings.*

1. No reason and no record.
2. A record without a reason.
3. Each carries a reason of 1 to 1024 characters (400 `api.request.malformed` naming
   `reason` otherwise) and is recorded as `identity.organization.created`,
   `identity.organization.deletionrequested` or
   `identity.organization.deletioncancelled`, security category, filed under the
   organization, the actor as both identities, details `{ reason }`.

*Chosen: 3, the strictest reading*, as for a grant (entry 183) and a group (entry 191).
A request ends every member session and a cancellation gives back every grant; both
are changes someone must answer for. A repeat that changes nothing records nothing
(entry 193).

*Tests that pin it.*
`OrganizationEndpointTests.IDN_ORG_002_AnOrganizationIsCreatedAsync`,
`OrganizationDirectoryTests.IDN_ORG_003_AChangeIsRecordedUnderTheOrganizationAsync`,
`AuditActionsTests.IDN_AUD_001_TheSetOfActionsIsClosed`.

*Chapter text that should change.* 10 section 5 could list the three actions (rows
under "Rows for chapter 10"); 09 section 8a could name the reason on each route.

---

## 198. A cancellation after the window answers `identity.deletion.windowelapsed`

**Phase 8 · 2026-09-23 · Tier 2 · IDN-ORG-003, 10 section 1.1, 10 section 4**

*The question.* IDN-ORG-003 is cancellable "at any point before the window closes"
and names no refusal for after it. 10 section 1.1 has
`identity.deletion.windowelapsed`, "The deletion grace window has closed; cancellation
is no longer possible", sourced to the account's deletion.

*The readings.*

1. A new code `identity.organization.windowelapsed`.
2. The existing code, 422, for an organization whose request is at least
   `organization.deletion.grace` old or that was erased, whether or not the pass that
   erases has reached it yet.

*Chosen: 2*, the smaller surface; the row's meaning holds word for word for an
organization.

*Tests that pin it.*
`OrganizationEndpointTests.IDN_ORG_004_ARequestIsCancelledOnlyWithinTheWindowAsync`.

*Chapter text that should change.* The source column of
`identity.deletion.windowelapsed` in 10 section 1.1 could add IDN-ORG-003, and 09
section 8a could list it under `/delete/cancel`.

---

## 199. The resolved policy marks each field and each gate `{ value, overridden }`

**Phase 8 · 2026-09-23 · Tier 2 · AUTH-STEP-002a, D-143, 09 section 8a, 10 section 4.1a**

*The question.* 09 section 8a: "`GET` returns the resolved policy with each field
marked inherited or overridden (D-143)". D-143 says the same, "each field marked
inherited/overridden". Neither gives the shape of the mark, whether `gates` is marked
as one field or per action, or what an override the system policy has since overtaken
shows as.

*The readings.*

1. The resolved object as 10 section 4.1a writes it, beside a list of overridden
   field names.
2. Each field as `{ value, overridden }`, `value` in the form 10 section 4.1a writes
   it; `gates` an object keyed by action name, each action `{ value, overridden }`
   with `value` its `{ level, phishingResistant, maxAge }`. `overridden` is true where
   the organization states the field (or the action) and the value in force is its
   own: the value it stated, or one that differs from the system's. A stated value the
   system policy has since raised past shows as inherited, since the system's is what
   is in force.

*Chosen: 2.* 10 section 4.1a stores gates per action ("an organization stores only
what it overrides"), so a gate is overridden per action, and a mark that said
"overridden" over a value the organization did not choose would mislead the reader
about where the value comes from.

*Tests that pin it.*
`OrganizationPolicyEndpointTests.AUTH_STEP_002a_ThePolicyIsReadWithEachFieldMarkedAsync`,
`OrganizationPolicyEndpointTests.AUTH_STEP_002a_AFieldTheSystemHasOvertakenIsInheritedAsync`,
`PolicyStrictnessTests.AUTH_STEP_002a_AStatedFieldIsTheOrganizationsOwnWhileItIsInForce`.

*Chapter text that should change.* 09 section 8a could give the response shape of
`GET /admin/organizations/{id}/policy`.

---

## 200. Every change of an organization's policy is stepped up and reasoned

**Phase 8 · 2026-09-23 · Tier 3 · OPS-CFG-002, AUTH-STEP-001, 09 section 8a, 10 section 5a**

*The question.* OPS-CFG-002: "**Tightening** a security control at runtime is free.
**Loosening** SHALL require step-up authentication, a written reason, and an audit
entry." 09 section 8a: "loosening any field requires step-up and a reason
(OPS-CFG-002)". 10 section 5a lists `policy:change`, "Change an organization's
policy", against `PUT /admin/organizations/{id}/policy` with no direction.

*The readings.*

1. Only a loosening is stepped up and needs a reason; a tightening is free.
2. Every replacement is the `policy:change` step-up action and carries a reason of 1
   to 1024 characters (400 `api.request.malformed` naming `reason` otherwise); every
   replacement is written down as `ops.configuration.changed` with its `loosening`
   flag (OPS-CFG-005, entry 193).

*Chosen: 2, the strictest reading.* The gate is a security control and the two
chapters differ on whether a tightening passes it; the gate that asks is the one kept.
The configuration route already takes a reason on every change (D-147).

*Tests that pin it.*
`OrganizationPolicyEndpointTests.AUTH_STEP_001_AChangeOfPolicyIsAStepUpActionAsync`,
`OrganizationPolicyEndpointTests.AUTH_STEP_002a_ATighteningIsWrittenDownAsync`,
`OrganizationPolicyEndpointTests.AUTH_STEP_002a_WhatAReplacementNamesMustBeReadableAsync`.

*Chapter text that should change.* 09 section 8a could say whether a tightening is
stepped up, in the same words as 10 section 5a.

---

## 201. A loosening of an organization's policy also needs `system:administer`

**Phase 8 · 2026-09-23 · Tier 3 · OPS-CFG-002, 10 section 2.1, 10 section 4.1a**

*The question.* 10 section 2.1 gives `organization:manage` "Organization lifecycle,
policy, and deletion cancellation" and `system:administer` "**Loosening**
configuration changes (OPS-CFG-002)". An organization's policy is the runtime key
`policy.<organization>` (10 section 4.1). Nothing says whether the second row reaches
it.

*The readings.*

1. `organization:manage` alone, in either direction.
2. `organization:manage` in the administrative organization always, and
   `system:administer` there as well where the replacement loosens. A replacement
   loosens where the policy the organization's members resolve to after it grants
   more on any field than before it, by the direction column of 10 section 4.1a;
   sets compare without order, and dropping an override loosens where the value in
   force falls. Refused 403 `authz.denied` before the step-up.

*Chosen: 2, the strictest reading*, which is also the one the configuration route
applies to every key.

*Tests that pin it.*
`OrganizationPolicyEndpointTests.OPS_CFG_002_ALooseningAlsoNeedsTheSystemPermissionAsync`,
`OrganizationPolicyEndpointTests.AUTH_STEP_002a_ThePolicyIsGovernedFromTheAdministrativeOrganizationAsync`,
`PolicyStrictnessTests.OPS_CFG_002_AChangeIsALooseningWhereAnyFieldGrantsMore`.

*Chapter text that should change.* The `system:administer` row of 10 section 2.1
could name `policy.<organization>`.

---

## 202. `config.policy.belowsystem` names the first looser field as `details.field`

**Phase 8 · 2026-09-23 · Tier 2 · AUTH-STEP-002a, 09 section 8a, 10 section 1**

*The question.* 09 section 8a: "**422** `config.policy.belowsystem` where a field is
looser than the system default (AUTH-STEP-002a)". No detail is named, and nothing says
whether the fields stated or the policy resolved are judged.

*The readings.*

1. The code alone.
2. The code with `details.field`, the first field the replacement states looser than
   the system policy, in the order of 10 section 4.1a (`requiredAssurance`,
   `loginFactors`, `gates`, `credentialRedundancy`, `selfServiceRecovery`). What the
   replacement states is judged; a field it leaves out inherits and cannot be looser.

*Chosen: 2*, as `api.request.malformed` names its member; the name is the field's,
never its value.

*Tests that pin it.*
`OrganizationPolicyEndpointTests.AUTH_STEP_002a_AFieldLooserThanTheSystemIsRefusedAsync`,
`PolicyStrictnessTests.AUTH_STEP_002a_AFieldStatedLooserThanTheSystemIsNamed`,
`PolicyStrictnessTests.AUTH_STEP_002a_AnOverrideNoLooserThanTheSystemIsAllowed`.

*Chapter text that should change.* The row of `config.policy.belowsystem` in 10
section 1 could name `details.field`.

---

## 203. The administrative organization's policy never resolves below `aal2`

**Phase 8 · 2026-09-23 · Tier 3 · AUTH-SESS-005b, 10 section 4.1a**

*The question.* AUTH-SESS-005b: "Administrative-organization sessions SHALL be held to
a **stated assurance floor of AAL2**, the policy's `requiredAssurance` field ... set to
`aal2` at bootstrap". A replacement "omitted fields inherit `policy.default`" (09
section 8a), so a replacement of the administrative organization's policy that does
not restate `requiredAssurance` would drop the floor to the system's `aal1`, and
nothing forbids it.

*The readings.*

1. Allowed: the floor is stated at bootstrap and the administrator may change it.
2. Refused: a replacement for the administrative organization under which its
   members resolve below `aal2` is 422 `config.value.belowfloor` with
   `details.field` `requiredAssurance`, before the step-up; nothing is written.

*Chosen: 2, the strictest reading.* The requirement says the floor SHALL hold, not
that it holds at bootstrap.

*Tests that pin it.*
`OrganizationPolicyEndpointTests.AUTH_SESS_005b_TheAdministrativeOrganizationKeepsItsFloorAsync`.

*Chapter text that should change.* AUTH-SESS-005b or 10 section 4.1a could say that no
change of the administrative organization's policy lowers `requiredAssurance` below
`aal2`, and name the refusal.

---

## 204. A replacement names only the policy's fields, never `emailDomains`

**Phase 8 · 2026-09-23 · Tier 2 · 10 section 4.1a, API-CONV-002, 09 section 8a**

*The question.* 10 section 4.1a: `emailDomains` is "Written only through
`/admin/organizations/{id}/domains`, never through `PUT .../policy`". 09 section 8a
lists it among the body's fields "(managed through the domain endpoints below, never
written here)". Neither says what a body carrying it, or carrying a member the object
does not have, answers.

*The readings.*

1. Ignore both; the lock in force is kept.
2. Refuse both with 400 `api.request.malformed` naming the member; a body that is no
   object is the code alone; a field whose value the object does not take is 422
   `config.value.notallowed`, as the configuration route answers. The lock in force
   is carried into the new override unchanged.

*Chosen: 2.* A misspelt tightening passed over would answer 204 and leave the
organization under a looser policy than its administrator wrote; refusing it fails
closed.

*Tests that pin it.*
`OrganizationPolicyEndpointTests.AUTH_STEP_002a_WhatAReplacementNamesMustBeReadableAsync`.

*Chapter text that should change.* 09 section 8a could list the refusals of
`PUT /admin/organizations/{id}/policy`.

---

## 205. The domain lock is governed from the administrative organization, and verifying is a loosening

**Phase 8 · 2026-09-23 · Tier 3 · REG-DOM-001, IDN-ORG-006, OPS-CFG-002, 09 section 8a, 10 sections 2.1 and 5a**

*The question.* 10 section 2.1: `domain:manage` is "Adding, verifying and removing an
organization's locked email domains (REG-DOM-001, IDN-ORG-006, D-146). Adding is a
loosening under OPS-CFG-002". 10 section 5a makes `domain:manage` the step-up gate for
"Add, verify or remove a locked domain". Neither says in which organization the
permission is asked, whether verifying (which is what makes a listed domain admit
addresses) is a loosening, or whether removing the last domain, which turns the lock
off, is one.

*The readings.*

1. `domain:manage` in the organization named by the path; only adding is a loosening.
2. `domain:manage` in the administrative organization, as `organization:manage` is for
   the rest of the policy (entry 201); adding and verifying are loosenings and also
   need `system:administer`; removing a domain is a loosening only where it leaves the
   list empty. Every change is the `domain:manage` step-up action and carries a reason.

*Chosen: 2, the strictest reading.* The list is a field of the organization's policy,
which is governed from the administrative organization. Verifying is the step that
widens what the lock admits, so it is at least as loose as adding. Emptying the list
turns the lock off for every member, which is the widest loosening the field has.

*Tests that pin it.*
`OrganizationDomainEndpointTests.OPS_CFG_002_ListingADomainIsASteppedUpLooseningAsync`,
`OrganizationDomainEndpointTests.REG_DOM_001_TheLockIsGovernedFromTheAdministrativeOrganizationAsync`,
`OrganizationDomainEndpointTests.IDN_ORG_006_AC3_TheLockIsAPolicyValueChangedWithoutADeployAsync`.

*Chapter text that should change.* 10 section 2.1 could say `domain:manage` is asked in
the administrative organization and that verifying, and removing the last domain, are
loosenings.

---

## 206. A lock whose listed domains are all unverified admits no address

**Phase 8 · 2026-09-23 · Tier 3 · REG-DOM-001 AC1, IDN-ORG-006, 10 section 4.1a**

*The question.* REG-DOM-001 AC1: "A domain listed but not yet verified does not admit
any address." 10 section 4.1a: `emailDomains` is "`off`, or the list of domains
**verified by DNS** whose addresses members may sign in with". Neither says whether the
lock is on while the list holds only unverified domains, in which case it admits
nobody, or off until a domain is verified.

*The readings.*

1. The lock is on only once a listed domain is verified.
2. The lock is on as soon as the list holds a domain; while none is verified it admits
   no address.

*Chosen: 2, the strictest reading.* Reading 1 lets a member sign in with any address
between the add and the verification, which is the window the lock exists to close.
The administrator adds, publishes the record and verifies; members outside the
organization's mail are refused from the add on.

*Tests that pin it.*
`OrganizationDomainEndpointTests.REG_DOM_001_AC1_AListedDomainAdmitsNothingUntilVerifiedAsync`.

*Chapter text that should change.* REG-DOM-001 could say the lock is on from the first
domain listed, verified or not.

---

## 207. A removed domain goes on refusing its addresses until it is listed anew

**Phase 8 · 2026-09-23 · Tier 3 · REG-DOM-001 AC4, 09 section 8a**

*The question.* 09 section 8a: "removal stops new sign-ins with addresses in the domain
and raises an alert (OPS-ALERT-001)". REG-DOM-001 AC4: "After a domain is removed,
sign-in with an address in it is refused and an alert is raised." Where the removal
leaves the list empty, the lock is off (10 section 4.1a), and nothing else would refuse
the domain's addresses.

*The readings.*

1. A removal takes the domain out of the list and nothing more; removing the last
   domain admits every address again.
2. The removed domain's row is kept with the moment of removal, and an address in it
   is refused for every member of the organization until the domain is listed anew,
   whether or not the list still holds other domains.

*Chosen: 2, the strictest reading.* It is the only reading under which AC4 holds for
the last domain. The kept row is also what holds D-153's "never reused": the token it
was drawn with stays drawn, and a domain listed again draws a new one.

*Tests that pin it.*
`OrganizationDomainEndpointTests.REG_DOM_001_AC4_ARemovedDomainRefusesSignInAndAlertsAsync`,
`OrganizationDomainEndpointTests.REG_DOM_001_AnUnprovedDomainIsNotVerifiedAsync`,
`DomainStoreTests.REG_DOM_001_ARemovedDomainIsKeptBesideItsSuccessorAsync`.

*Chapter text that should change.* REG-DOM-001 could say a removed domain refuses its
addresses until it is listed again.

---

## 208. What the lock judges, when, and how a refusal is told

**Phase 8 · 2026-09-23 · Tier 3 · REG-DOM-001, IDN-ORG-006 AC2, AUTH-ABUSE-003, AUTH-FACT-003, REG-MAIL-001 AC5**

*The question.* REG-DOM-001: "While the lock is on, every member's sign-in email and
any open email chosen at invitation acceptance SHALL be in the list
(`identity.identifier.domainnotallowed`)." The chapters do not say which address is a
member's "sign-in email", whose memberships count, at which step of a sign-in the
lock is told, or what a sign-in link asked for a refused address does. AUTH-ABUSE-003
requires that nothing before a factor succeeds tells an address apart.

*The readings.*

1. Judge the account's primary email at every sign-in, whatever identifier opened it,
   and refuse at `/auth/begin`.
2. Judge the email a sign-in was opened with, and the email a link or code went to,
   once a factor has succeeded; refuse with `identity.identifier.domainnotallowed`
   (422). A link or code asked for a refused address is answered as any other ask and
   sends nothing, as a factor the policy has not enabled is (AUTH-FACT-003 AC5). A
   link or code that went out before the lock changed is judged again when it is
   used. The lock reaches an account through its current memberships only, and each
   organization's lock is judged on its own. A sign-in opened with a username, a
   phone number or a discoverable passkey is not judged, since no email opened it.

*Chosen: 2.* Refusing at `/auth/begin` tells anyone who types an address whether an
account holds it and is under a lock, which AUTH-ABUSE-003 forbids. The challenge and
the pending link each keep the identifier of the address, never the address.

*Tests that pin it.*
`OrganizationDomainEndpointTests.REG_DOM_001_TheLockIsToldOnlyAfterAFactorSucceedsAsync`,
`OrganizationDomainEndpointTests.REG_DOM_001_AC4_ALinkNoLongerSignsInToARemovedDomainAsync`,
`OrganizationDomainEndpointTests.IDN_ORG_006_AC2_AnAddressOutsideTheVerifiedListIsRefusedAsync`,
`OrganizationDomainEndpointTests.IDN_ORG_006_AC1_WithTheLockOffNoAddressIsRefusedAsync`.

*Chapter text that should change.* REG-DOM-001 could define "sign-in email" as the
address a sign-in is opened with or a link is sent to, and say the refusal follows a
successful factor.

---

## 209. Verifying a domain, and the new code `identity.domain.unverified`

**Phase 8 · 2026-09-23 · Tier 2 · REG-DOM-001, D-153, 09 section 8a, 10 section 1.1**

*The question.* 09 section 8a: the domain is verified "by the DNS TXT record the add
response names". Nothing says what a verification that does not find the record
answers, what a lookup that fails answers, or what a repeated add, a verify of a
domain not listed, or a removal of one not listed answers. 10 section 1.1 holds no
code for a failed verification.

*The readings.*

1. A failed verification answers `config.value.notallowed`.
2. A new code `identity.domain.unverified` (422). The record proves the domain where
   one TXT value at `_identity-verify.<domain>` equals
   `identity-domain-verification=<token>` exactly; a lookup that could not be made
   proves nothing and is answered the same way; a failed verification writes nothing.
   Listing a domain already listed, and verifying one already verified, answer the
   domain as it stands and change nothing; verifying a domain the organization does
   not list is 400 `api.request.malformed` naming `domain`; removing one it does not
   list is 204 and changes nothing.

*Chosen: 2.* No value was written, so `config.value.notallowed` would mislead; the
failure is about the domain's proof.

*Tests that pin it.*
`OrganizationDomainEndpointTests.REG_DOM_001_AnUnprovedDomainIsNotVerifiedAsync`,
`OrganizationDomainEndpointTests.REG_DOM_001_WhatAChangeNamesMustBeReadableAsync`,
`ErrorCodesTests.CONV_NAME_003_AC2_ChangingACodeFailsTheContractTest`.

*Chapter text that should change.* 10 section 1.1 needs the row for
`identity.domain.unverified` (below, under the rows for chapter 10).

---

## 210. A failed scheduled check keeps the domain verified; only verified domains are re-checked

**Phase 8 · 2026-09-23 · Tier 2 · REG-DOM-001 AC3, 09 section 8a, 10 section 4**

*The question.* REG-DOM-001: "A failed re-verification SHALL raise an alert
(OPS-ALERT-001) and SHALL revoke nothing by itself." 10 section 4:
`domain.reverify.interval` "the sweep re-verifies every locked domain's TXT record at
this interval". Neither says whether a domain whose check failed goes on admitting
addresses, or whether a domain never verified is re-checked by the sweep.

*The readings.*

1. A failed check unverifies the domain, so its addresses are refused until an
   administrator verifies it again.
2. A failed check is recorded (`lastCheckPassed` false, shown by `GET`) and alerts
   with `domain-reverification-failed`; the domain stays verified and admitting. The
   sweep re-checks every listed, verified domain last checked an interval ago or
   more; an unverified domain waits for the administrator's verify.

*Chosen: 2.* Unverifying a domain refuses every member's sign-in with it, which is a
revocation in all but name, and AC3 says a failure "leaves every membership and
session intact". A resolver outage would otherwise lock a whole organization out.

*Tests that pin it.*
`OrganizationDomainEndpointTests.REG_DOM_001_AC3_AFailedReverificationAlertsAndRevokesNothingAsync`,
`OrganizationDomainEndpointTests.REG_DOM_001_AScheduledCheckRunsOncePerIntervalAsync`,
`DomainStoreTests.REG_DOM_001_AC3_OnlyAVerifiedListedDomainIsDueAsync`.

*Chapter text that should change.* REG-DOM-001 could say a domain whose scheduled check
failed stays verified.

---

## 211. A domain's canonical form

**Phase 8 · 2026-09-23 · Tier 2 · REG-DOM-001, IDN-ACCT-004, D-153**

*The question.* The chapters do not say how a domain is entered, compared or stored,
nor how an address's domain is compared with a listed one.

*The readings.*

1. Compare the text as entered, ignoring case.
2. The canonical form of IDN-ACCT-004 (NFKC case fold), then its IDNA ASCII form under
   the STD3 rules; at least two labels, no trailing dot, and at most 236 octets so
   that `_identity-verify.<domain>` fits DNS's 253. An address's domain is read the
   same way. An address whose domain does not read is admitted only where no lock
   applies.

*Chosen: 2.* The Unicode and the ASCII forms of one domain are one domain; comparing
entered text would let `Bücher.example` and `xn--bcher-kva.example` be listed as two
and verified under different tokens.

*Tests that pin it.*
`OrganizationDomainEndpointTests.REG_DOM_001_WhatAChangeNamesMustBeReadableAsync`.

*Chapter text that should change.* REG-DOM-001 could name the domain's form.

---

## 212. The resolver is the host's; none is shipped

**Phase 8 · 2026-09-23 · Tier 2 · LIB-EXT-001, REG-DOM-001, implementation plan section 3**

*The question.* The plan: "What Milestone 1 does **not** touch: a real mail server,
SMS gateway, secrets manager, DNS, ... Each is met at its abstraction (LIB-EXT-001)
with a fake that honours the contract". LIB-EXT-001's table has no DNS row, so nothing
says whether the library ships a resolver or what happens without one.

*The readings.*

1. Ship a default resolver.
2. `IDnsResolver` is an extension point the host registers, optional as the phone
   signal provider is; without one, every verification answers
   `identity.domain.unverified` and every scheduled check fails and alerts. A
   deployment that never locks a domain needs none.

*Chosen: 2.* The plan keeps DNS out of Milestone 1, and CONV-DESIGN-008 names no DNS
package. Failing closed without a resolver can never admit an unproved domain.

*Tests that pin it.*
`OrganizationDomainEndpointTests.REG_DOM_001_AnUnprovedDomainIsNotVerifiedAsync` (a
resolver that cannot be reached).

*Chapter text that should change.* LIB-EXT-001's table could carry a row: DNS TXT
lookup, none shipped. Milestone 2 supplies the resolver.

---

## 213. The system policy locks no domain

**Phase 8 · 2026-09-23 · Tier 3 · 10 section 4.1a, REG-DOM-001, OPS-CFG-002**

*The question.* 10 section 4.1a: `emailDomains` is "Written only through
`/admin/organizations/{id}/domains`, never through `PUT .../policy`". It says nothing
of `PUT /admin/config/policy.default`, which writes the same object for the system
policy. A domain verified nowhere could be listed there and inherited by every
organization.

*The readings.*

1. Accept the list on the system policy.
2. Refuse a system policy whose `emailDomains` is not empty with 422
   `config.value.notallowed`, `details.field` `emailDomains`, as entry 202 names a
   field.

*Chosen: 2, the strictest reading.* A domain is verified for one organization (one
token per organization and domain, D-153); a system list could be verified nowhere,
so it would either admit unproved domains or lock every member of every organization
out.

*Tests that pin it.*
`OrganizationDomainEndpointTests.IDN_ORG_006_AC3_TheLockIsAPolicyValueChangedWithoutADeployAsync`.

*Chapter text that should change.* 10 section 4.1a could say the system policy's
`emailDomains` is always `off`.

---

## 214. The domain endpoints' shapes and what each change writes down

**Phase 8 · 2026-09-23 · Tier 2 · 09 section 8a, API-CONV-002, IDN-AUD-001, OPS-CFG-005**

*The question.* 09 section 8a: "`GET` returns each domain with its verification state
and last check". No shape is given for it, for the add response that "names" the
record, for the verify response, or for the bodies; no audit action is named.

*The readings.*

1. Answer the domain names only.
2. `GET` answers an array of `{ domain, recordName, recordValue, addedAt, verifiedAt,
   checkedAt, lastCheckPassed }`, the domain in its ASCII form; `POST .../domains`
   takes `{ domain, reason }` and answers 201 with one such object; `POST
   .../{domain}/verify` takes `{ reason }` and answers 200 with it; `DELETE
   .../{domain}` takes `{ reason }` in the body and answers 204. Each change writes
   `identity.organization.domainadded`, `.domainverified` or `.domainremoved` with
   details `{ domain, reason }`, and each change to the list is also written down as a
   runtime configuration change of `policy.<organization>`.

*Chosen: 2.* The record is what the administrator publishes, so the response carries
it; the audit actions follow the organization's own (entry 197).

*Tests that pin it.*
`OrganizationDomainEndpointTests.REG_DOM_001_EveryChangeIsWrittenDownAsync`,
`OrganizationDomainEndpointTests.REG_DOM_001_AC1_AListedDomainAdmitsNothingUntilVerifiedAsync`,
`AuditActionsTests.IDN_AUD_001_TheSetOfActionsIsClosed`.

*Chapter text that should change.* 09 section 8a could give the shapes; chapter 10
needs the three audit rows (below).

---

## 215. The mail server is a port the host registers, and no adapter ships in Milestone 1

**Phase 8 · 2026-09-24 · Tier 3 · INT-MAIL-001, INT-MAIL-008, INT-MAIL-009, LIB-EXT-001**

*The question.* The plan's phase 8 names "mail server integration (JMAP provisioning,
disabled at invitation, enabled at membership, lifecycle push, reconciliation, app
passwords through the first-party client)". INT-MAIL-001 AC3: "Every provisioning and
app-password operation is a JMAP request; no other management interface of the mail
server is called." INT-MAIL-008: "No library dependency SHALL be on a specific mail
server", AC1 "No provider name appears in a core namespace". The plan, section 3:
"What Milestone 1 does **not** touch: a real mail server, ... Each is met at its
abstraction (LIB-EXT-001) with a fake that honours the contract". 08 places "the
default mail and SMS transports and the mail-server adapter (chapter `05`, LIB-EXT-001
defaults)" in `Janus.Hosting`. INT-MAIL-001 names "JMAP objects through the JMAP
management API"; neither chapter `05` nor the decision log gives the objects (D-006
says only "`stalwart-cli apply` or the JMAP management API").

*The readings.*

1. Write the JMAP adapter in phase 8, in `Janus.Hosting` as 08 places it, tested
   against a fake HTTP endpoint.
2. Define the abstraction only: `IMailServer` in `Janus.Core` with
   `ProvisionAsync(MailboxPush, CancellationToken)` answering `ValueTask<Result>` and
   `MailboxesAsync(CancellationToken)` answering
   `ValueTask<Result<IReadOnlyList<HostedMailbox>>>`; `MailboxPush(Guid Key, string
   Address, MailboxState State)`; `MailboxState` `disabled` · `enabled` · `removed`;
   `HostedMailbox(string Address, bool Enabled)`. The host registers it; the library
   ships none. Integration tests run against a fake that honours the contract.

*Chosen: 2, the strictest reading.* The adapter would have to send objects whose shape
no chapter states and no source in front of this run confirms, which is a claim about
a product the working guide does not let a run make; a fake HTTP endpoint built to the
same guess would test nothing. The plan puts the real server out of Milestone 1. An
absent registration is not a refusal at startup: nothing is pushed and nothing is
compared, mailbox rows are still written, and the first pass after a registration
pushes every state owed (INT-MAIL-009 AC1: hosting is a registration of its own,
independent of outbound delivery). INT-MAIL-001 AC3 is verified by the adapter in
Milestone 2 step 5, not by a test in this phase. The app-password members join the
port with REG-MAIL-002.

*Tests that pin it.*
`MailboxPublisherTests.INT_MAIL_009_AC1_MailboxHostingIsARegistrationOfItsOwnAsync`.

*Chapter text that should change.* `05` INT-MAIL-001 could give the JMAP objects and
methods the adapter sends, and 07 LIB-HOST-001 the declaration (row below); the plan's
phase 8 line could say "provisioning through the mail-server abstraction, the adapter
in Milestone 2 step 5".

---

## 216. Which organization's mail is integrated

**Phase 8 · 2026-09-24 · Tier 2 · REG-MAIL-001, INT-MAIL-006, INT-MAIL-009**

*The question.* REG-MAIL-001: "Where the organization's mail server is integrated (`05`
INT-MAIL)" and "Where the organization runs its own mail, the corporate address SHALL
be verified by the person like any other." INT-MAIL-006: "**Human
administrative-organization members** SHALL have a mailbox pre-created in the mail
server". Nothing says how the library knows an organization is integrated.

*The readings.*

1. A per-organization setting declares integration.
2. Integrated means the administrative organization, in a deployment that registered
   `IMailServer` (entry 215); every other organization runs its own mail.

*Chosen: 2.* INT-MAIL-006 and the INT-MAIL-009 table scope mailbox hosting to the
administrative organization, and a per-organization key would be a setting chapter 10
does not hold. A holder stands, and the mailbox is owed `enabled`, only while the
account is `active` and a membership of the administrative organization is current;
a membership of any other organization does not count.

*Tests that pin it.*
`MailboxStoreTests.INT_MAIL_006_AMembershipElsewhereDoesNotStandAsync`,
`MailboxStoreTests.INT_MAIL_006a_AHolderStandsOnlyWhileActiveAndAMemberAsync`.

*Chapter text that should change.* REG-MAIL-001 could say "where the invitation is into
the administrative organization and the deployment registers a mail server".

---

## 217. The mailbox row is its own outbox, and the state owed is read, not published

**Phase 8 · 2026-09-24 · Tier 2 · INT-MAIL-006, INT-MAIL-006a, INT-MAIL-007, 10 section 5b**

*The question.* INT-MAIL-006: "the provisioning request is written to the retried
outbox (D-022, INT-MAIL-008), so the mailbox is created the moment the mail server is
reachable." INT-MAIL-006a AC1: "the disable request is published on the first outbox
publisher run (`outbox.poll.interval`) after the suspension commits." 10 section 5b
names "Mail provisioning" as a consumer of `AccountSuspended`, `AccountReactivated`,
`MembershipChanged` and the `Identifier*` events. The event outbox is phase 9's
(entry 121).

*The readings.*

1. Every operation that suspends, reactivates, deactivates, deletes, takes down or ends
   a membership writes a provisioning record into the outbox in its own transaction.
2. Each `mailboxes` row carries the state the server last confirmed and the one push
   outstanding with its key; the state owed is read on every pass from the account's
   state and its memberships in the same query, and the publisher pushes the
   difference.

*Chosen: 2.* With 1, a path that forgot the record would leave a former employee
reading mail, which is the failure INT-MAIL-006a names; with 2 nothing can forget, and
the first pass after any change commits pushes it, which is AC1. The schedule is the
outbox's own: `outbox.retry.initial`, `outbox.retry.factor`,
`outbox.retry.maxattempts`, full jitter. `MailboxPublisher.PublishAsync` and
`MailboxReconciliation.ReconcileAsync` are registered here and scheduled by phase 9's
worker, at `outbox.poll.interval` and daily.

*Tests that pin it.*
`MailboxPublisherTests.INT_MAIL_006a_AC1_ASuspendedHolderIsDisabledOnTheFirstPassAsync`,
`MailboxPublisherTests.INT_MAIL_006_AC1c_AReservedMailboxIsCreatedDisabledAsync`,
`MailboxPublisherTests.INT_MAIL_006a_TheLastStateOwedIsTheOneTheServerEndsInAsync`,
`MailboxPublisherTests.PublishAsync_ASettledMailbox_IsLeftAloneAsync`,
`MailboxStoreTests.INT_MAIL_007_AnOutstandingPushKeepsItsKeyAsync`.

*Chapter text that should change.* 10 section 5b could mark mail provisioning as
reading the state those events announce rather than consuming them.

---

## 218. A push is written down before it leaves, and a lost answer is never assumed

**Phase 8 · 2026-09-24 · Tier 3 · INT-MAIL-007 AC1, INT-MAIL-006a**

*The question.* INT-MAIL-007: "Lifecycle events SHALL carry a **stable idempotency
key**", AC1 "Replaying an event produces no duplicate." A push the server applies
while its answer is lost, or while the process stops, leaves the server's state
unknown. Nothing says what the library then assumes.

*The readings.*

1. Keep the push in memory until the server answers; where the state owed returns to
   the one last confirmed, drop the outstanding push.
2. Write the push and its key down, and commit, before it leaves; keep the key until
   the server confirms; where the state owed returns to the one last confirmed while a
   push is outstanding, push that state again under a key of its own.

*Chosen: 2, the strictest reading.* Under 1, an enable the server applied and whose
answer was lost is dropped when the holder is suspended before the retry: the library
believes the mailbox disabled and the suspended person goes on reading mail. Under 2 a
retry is always under the same key, and the server ends in the last state owed.

*Tests that pin it.*
`MailboxPublisherTests.INT_MAIL_007_AC1_APushIsWrittenDownBeforeItLeavesAsync`,
`MailboxPublisherTests.INT_MAIL_007_AC1_APushMadeAgainProducesNothingTwiceAsync`,
`MailboxPublisherTests.INT_MAIL_007_AC1_AReturnWhileAPushIsOutstandingIsPushedAgainAsync`.

*Chapter text that should change.* INT-MAIL-007 could state that a push is recorded
before it is made.

---

## 219. A push that spends its budget stays failed and is not retried

**Phase 8 · 2026-09-24 · Tier 3 · INT-MAIL-007 AC3, OPS-OBS-002, OPS-ALERT-001**

*The question.* INT-MAIL-007 AC3: "Propagation failures are visible in monitoring."
OPS-ALERT-001 lists "Degradation: blocklist fallback, failed provider push,
undelivered notification, reconciliation drift" as Normal. `outbox.retry.maxattempts`
is "attempts before `failed`". Nothing says what follows a spent budget for a push.

*The readings.*

1. Start a fresh budget on the next pass.
2. Mark the push failed, raise `degradation` in the same transaction as the mark, and
   make no further attempt until the state owed changes, which begins a push of its
   own; reconciliation goes on reporting the difference.

*Chosen: 2, the strictest reading.* INT-MAIL-007: "Silent auto-correction conceals a
broken pipeline." A retried push that finally lands would clear the difference nobody
looked at. The alert's scope is `mailbox.push:<mailbox id>` and its details are
`{ mailbox, state, attempts }`: the mailbox is named by its identifier and never by its
address, because the address is personal data and an alert travels to channels that
are not the account's. A server that throws is a server that did not confirm.

*Tests that pin it.*
`MailboxPublisherTests.INT_MAIL_007_AC3_APushThatSpendsItsBudgetIsVisibleAsync`.

*Chapter text that should change.* INT-MAIL-007 could name the end of the budget; 10
section 5.23 could give `degradation`'s details for a push.

---

## 220. What reconciliation compares and what it reports

**Phase 8 · 2026-09-24 · Tier 3 · INT-MAIL-006 AC1b, AC1c, INT-MAIL-006a AC3, INT-MAIL-007 AC2, OPS-OBS-002**

*The question.* INT-MAIL-007: "Reconciliation SHALL run daily, comparing both sides
and **flagging drift without auto-correcting**." INT-MAIL-006a AC3: "Reconciliation
compares enabled state and flags drift." INT-MAIL-006 AC1c: "reconciliation treats a
reserved mailbox for an open or expired invitation as expected, not as drift."
Nothing says whether a mailbox with a push outstanding is compared, what an address
the library does not know is, or what a failed listing is.

*The readings.*

1. Skip mailboxes with a push outstanding, ignore addresses the library does not know,
   and stay silent when the server cannot be listed.
2. Compare every mailbox the library reads with what the server lists: `enabled` must
   be listed enabled, `disabled` listed disabled (a reservation included), `removed`
   absent; count every address the server lists and the library does not read; raise
   `degradation` with scope `mailbox.reconciliation` and details
   `{ mailboxes: [ids], unknown: count }`; raise it with `{ listed: false }` where the
   listing fails; change nothing on either side.

*Chosen: 2, the strictest reading.* A push outstanding for a day is itself the
failure the comparison exists to show, and an address nobody provisioned is the one a
former employee might still be reading. Addresses are compared ordinally in their
canonical form and never written into the alert.

*Tests that pin it.*
`MailboxReconciliationTests.INT_MAIL_007_AC2_ADriftIsReportedAndNothingIsChangedAsync`,
`MailboxReconciliationTests.INT_MAIL_006a_AC3_AnEnabledMailboxOwedDisabledIsDriftAsync`,
`MailboxReconciliationTests.INT_MAIL_006_AC1c_AReservedMailboxIsNoDriftAsync`,
`MailboxReconciliationTests.INT_MAIL_006_AC1b_OnlyWhatEitherSideHoldsIsComparedAsync`,
`MailboxReconciliationTests.INT_MAIL_007_AReleasedMailboxIsExpectedGoneAsync`,
`MailboxReconciliationTests.INT_MAIL_007_AC3_AComparisonThatCouldNotBeMadeIsVisibleAsync`.

*Chapter text that should change.* INT-MAIL-007 could list what is compared and what
the report carries.

---

## 221. One mailbox per address, for good

**Phase 8 · 2026-09-24 · Tier 3 · INT-MAIL-006, REG-MAIL-001, REG-MAIL-003**

*The question.* REG-MAIL-001: "An expired invitation SHALL leave the mailbox reserved
and disabled until the administrator re-invites or deletes it." REG-MAIL-003: "A
retired corporate address SHALL be available to a later invitation." Nothing says
what deleting a reservation does to a mailbox someone once held, or whether a later
invitation gets a new mailbox or the old one.

*The readings.*

1. Each invitation provisions a mailbox of its own, and revoking one removes it.
2. An address is one row and one mailbox on the server for good. A reservation nobody
   ever held is removed when its invitation is revoked; a mailbox anyone has held is
   never removed by the library, and a retired one is reserved again, disabled, for a
   later invitation of the same address. A mailbox held now cannot be reserved.

*Chosen: 2, the strictest reading.* Removing a mailbox someone held destroys company
mail no chapter lets the library destroy, and a second mailbox for the same address is
one the server cannot hold. The unique index on the address's fingerprint holds it
whatever a service does.

*Tests that pin it.*
`MailboxStoreTests.INT_MAIL_006_AnAddressIsOneMailboxAsync`,
`MailboxReconciliationTests.INT_MAIL_007_AReleasedMailboxIsExpectedGoneAsync`; the
revoke and re-invite tests land with the invitation endpoints.

*Chapter text that should change.* REG-MAIL-003 could say the retired mailbox is
reserved again rather than recreated.

---

## 222. The mailbox address is its holder's personal field

**Phase 8 · 2026-09-24 · Tier 3 · PRIV-RIGHT-005a, PRIV-RIGHT-005c, INT-MAIL-007, REG-MAIL-003**

*The question.* The corporate address is an identifier of the account (REG-MAIL-003)
and the mailbox must be found by it. PRIV-RIGHT-005c: "Searchable identifiers SHALL
be stored as a **keyed fingerprint** ... alongside their encrypted form. Erasure SHALL
**neutralise** the fingerprint", "with the canonicalisation version (the pinned
Unicode version) stored alongside". Nothing says whose field the mailbox's address is.

*The readings.*

1. Store the address in plaintext: it is the organization's, not the person's.
2. Store it as `enc_canonical` under the holder's data key, or under a key of the
   row's own while nobody holds it, with `fingerprint` and `canonicalisation_version`
   beside it; erasure neutralises the fingerprint of every mailbox the subject holds or
   last held and leaves the row where it is.

*Chosen: 2, the strictest reading.* The address names the person, and a plaintext copy
would survive their erasure. Consequences: a row whose holder was erased is not read
again, so a push outstanding at erasure is abandoned and the server's mailbox is then
reported by reconciliation as an address the library does not know (entry 220); the
neutralised fingerprint leaves the address free for a later invitation, which reserves
it as a row of its own (REG-MAIL-003).

*Tests that pin it.*
`SubjectEraserTests.PRIV_RIGHT_005c_TheAddressOfAMailboxGoesWithItsHolderAsync`,
`ModelTests` (the `mailboxes` columns).

*Chapter text that should change.* PRIV-RIGHT-005a's list of personal fields could name
the mailbox address.

---

## 223. The invitation names its corporate address as `corporateEmail`

**Phase 8 · 2026-09-24 · Tier 2 · REG-INV-001, REG-MAIL-001, IDN-LIFE-009a**

*The question.* 09 section 8a: "Where the mail server is integrated, the invitation
names a personal `email` (required; the link goes to it and its press verifies it, so
the account is created with a verified personal email), and a corporate `email` is
asserted and its mailbox provisioned disabled (REG-MAIL-001, D-148)." and "An
integrated-mail invitation without a personal `email` is refused as a validation error
(**422**)." One body cannot hold two members named `email`, and chapter 10 holds no
code for the validation error. REG-MAIL-001: "Where the organization runs its own mail,
the corporate address SHALL be verified by the person like any other."

*The readings.*

1. `email` is the personal address, the one every invitation sends its link to, and
   `corporateEmail` the asserted one.
2. `email` is the corporate address and `personalEmail` the personal one.
3. An `emails` object with two members.

*Chosen: 1.* `email` keeps one meaning across every invitation: the address the link
goes to. Where the mail is integrated (entry 216) both are required: a missing one is
`422 identity.identifier.invalid` with `details.member` naming `email` or
`corporateEmail`, the 422 code chapter 10 holds for an identifier that is not a
well-formed identifier of its kind; a personal `email` equal to the corporate address
is refused the same way naming `email`, since the link would go to a mailbox that is
disabled. Where the mail is not integrated, `corporateEmail` is
`400 api.request.malformed` naming it: there is nothing for an administrator to assert.

*Tests that pin it.*
`InvitationServiceTests.REG_INV_001_AC4_AnIntegratedInvitationNeedsAPersonalEmailAsync`,
`InvitationServiceTests.REG_MAIL_001_ACorporateAddressIsRefusedWhereTheMailIsNotIntegratedAsync`,
`InvitationEndpointTests.REG_INV_001_AC4_AnIntegratedInvitationWithoutAPersonalEmailIsRefusedAsync`,
`InvitationEndpointTests.IDN_LIFE_009a_WhatAnInvitationNamesMustBeReadableAsync`.

*Chapter text that should change.* 09 section 8a could name the members `email`,
`phone`, `corporateEmail`, `roles` and `documents`, and the 422 code.

---

## 224. Which address the domain lock judges at issue, and what a refusal names

**Phase 8 · 2026-09-24 · Tier 3 · REG-DOM-001, REG-MAIL-001, REG-INV-001**

*The question.* REG-DOM-001: "While the lock is on, every member's sign-in email and
any open email chosen at invitation acceptance SHALL be in the list
(`identity.identifier.domainnotallowed`)." REG-MAIL-001 of the personal email: "while
the membership lasts the organization's domain lock (REG-DOM-001) prevents sign-in with
it". Nothing says whether an address the invitation binds is judged when it is issued.
Up to three identifiers are read from one body, and chapter 10's identifier codes carry
no detail naming which.

*The readings.*

1. Issue judges nothing; the lock judges at registration and sign-in only.
2. Issue judges the address the member will sign in with: the corporate address where
   the mail is integrated, the bound `email` otherwise. The personal email of an
   integrated invitation is not judged.
3. Issue judges every bound email.

*Chosen: 2, the strictest reading that can issue at all.* Reading 3 refuses every
integrated invitation whose personal address lies outside the list, which REG-MAIL-001
expects it to; reading 1 issues an invitation whose bound and locked email could never
sign in. The lock's rule is read through `DomainLock.RefusedInAsync`, the same rule
sign-in and registration apply. `identity.identifier.invalid` and
`identity.identifier.mixedscript` carry `details.member` naming `email`, `phone` or
`corporateEmail`; `identity.identifier.domainnotallowed` carries none, since one address
alone is judged.

*Tests that pin it.*
`InvitationServiceTests.REG_DOM_001_TheLockJudgesTheAddressTheMemberWillSignInWithAsync`,
`InvitationServiceTests.REG_INV_001_AnIdentifierThatDoesNotReadIsRefusedByMemberAsync`.

*Chapter text that should change.* REG-DOM-001 could say a bound email is judged when
the invitation is issued; chapter 10 could note the `member` detail of the two
identifier codes.

---

## 225. A phone the deployment does not collect is not bound

**Phase 8 · 2026-09-24 · Tier 2 · REG-INV-001, REG-MAIL-001**

*The question.* REG-INV-001: "An invitation MAY name the email, the phone, both, or
neither." REG-MAIL-001 AC3: "A bound phone is verified before the membership step can
be reached." A registration where `registration.phone` is `off` collects no phone.

*The readings.*

1. Bind it anyway.
2. Refuse it: `400 api.request.malformed` naming `phone`.

*Chosen: 2.* A bound phone is one the registration must verify, and one that collects
none never would, so the membership step would be unreachable.

*Tests that pin it.*
`InvitationServiceTests.REG_INV_001_APhoneIsNotBoundWhereTheDeploymentCollectsNoneAsync`.

*Chapter text that should change.* REG-INV-001 could say a phone is bound only where
`registration.phone` is not `off`.

---

## 226. Where the link goes, and the token answered once where no email is bound

**Phase 8 · 2026-09-24 · Tier 3 · IDN-LIFE-009a, REG-INV-001**

*The question.* 09 section 8a: "Issues a time-boxed, single-use enrolment link
(IDN-LIFE-009a). MAY bind `email`, `phone`, both or neither". REG-INV-001: "Binding the
phone means the link alone is not enough to accept, and gives the invitation a second,
personal channel." Nothing says where the link goes when no email is bound, and 09
gives no response body.

*The readings.*

1. The link goes to the bound email; with none, to the bound phone by SMS; with
   neither, the token is answered to the administrator.
2. The link goes to the bound email alone. With no email bound the token is answered
   once, in the `201`, for the administrator to hand over; the phone is never sent it.
3. Refuse an invitation that binds no email.

*Chosen: 2, the strictest reading.* Reading 1 carries the link on the channel binding
the phone makes the second one, so the link alone would again be enough; reading 3
forbids what REG-INV-001 allows. The `201` body is `id`, `expiresAt` and `token`, which
is `null` wherever the link was sent. The token is held only as its fingerprint.

*Tests that pin it.*
`InvitationServiceTests.REG_INV_001_TheLinkGoesToTheBoundEmailOrToTheAdministratorAsync`,
`InvitationServiceTests.REG_MAIL_001_AC2_TheCorporateAddressIsSentNothingAsync`,
`InvitationEndpointTests.IDN_LIFE_009a_AnInvitationIsIssuedAndItsLinkSentAsync`,
`InvitationStoreTests.REG_INV_001_AnInvitationReadsBackAsItWasIssuedAsync`.

*Chapter text that should change.* 09 section 8a could give the `201` body and say the
link is sent to the bound email only.

---

## 227. How the link is sent, and a link that cannot be sent issues nothing

**Phase 8 · 2026-09-24 · Tier 2 · IDN-LIFE-009a, REG-MAIL-001**

*The question.* REG-MAIL-001: "the invitation SHALL name a **personal email**, to
which the invitation link is sent". Nothing says under which purpose the sending
restrictions judge the link, or what an unsent link does to the invitation. The
invitee holds no account.

*The readings.*

1. Write the invitation, then send the link, and keep the invitation where the send
   fails.
2. Send the link before the transaction, as the recovery link is, and issue nothing
   where the send is refused.

*Chosen: 2.* An invitation whose link never left cannot be accepted and would hold
its mailbox reserved for its whole lifetime. The link is sent as the message kind
`invitation-link` (row below) with the purpose `notification` and no subject, so the
restrictions a notification answers to judge it, as they judge a recovery link
(chapter 10: "A recovery link carries the `notification` purpose").

*Tests that pin it.*
`InvitationServiceTests.IDN_LIFE_009a_ALinkThatCouldNotBeSentIssuesNothingAsync`.

*Chapter text that should change.* Chapter 10 could list the message kind (row below).

---

## 228. The roles an invitation attaches ask what a grant asks

**Phase 8 · 2026-09-24 · Tier 3 · REG-INV-001, AUTHZ-GRANT-004, IDN-LIFE-009a**

*The question.* REG-INV-001: "At the membership step the person SHALL be shown who
invited them, the organization, the roles and grants that will attach". Chapter 10
gives `membership:manage` "Adding and removing members; issuing and revoking
invitations, bound or open", and a grant is `grant:manage`. 09 names no member for the
roles.

*The readings.*

1. `membership:manage` alone attaches any role.
2. The body names organization-wide role names in `roles`. Naming any asks
   `grant:manage` in the organization; a role carrying `system:administer` asks what a
   grant of it asks, `system:administer` in the administrative organization; a role the
   deployment does not define is `400 api.request.malformed` naming `roles`; a name
   given twice counts once; no `roles` attaches none.
3. Nothing attaches at issue; grants are made after acknowledgement.

*Chosen: 2, the strictest reading.* Reading 1 lets a holder of `membership:manage`
grant what `grant:manage` withholds; reading 3 contradicts "the roles and grants that
will attach". The roles are read through a port of their own, `IRoleCatalogue`, which
the store implements over the roles tables.

*Tests that pin it.*
`InvitationServiceTests.REG_INV_001_TheRolesAttachedAskWhatAGrantAsksAsync`,
`InvitationEndpointTests.IDN_LIFE_009a_WhatAnInvitationNamesMustBeReadableAsync`.

*Chapter text that should change.* 09 section 8a could name `roles` and say that
naming one asks `grant:manage`.

---

## 229. The documents an invitation attaches, at the version current when it is issued

**Phase 8 · 2026-09-24 · Tier 2 · REG-INV-001**

*The question.* REG-INV-001: "the documents the organization attached to the
invitation, each with a version", and AC3 "The membership record carries the
acknowledgement with the document versions shown." Nothing says whether the body names
the version.

*The readings.*

1. The body names each document and its version.
2. The body names each document; the version is the one current when the invitation
   is issued, and is what is shown and recorded.

*Chosen: 2.* What is shown is what the organization attached, not what was published
after it. A document never published, or a blank name, is
`400 api.request.malformed` naming `documents`; a name given twice counts once; no
`documents` attaches none.

*Tests that pin it.*
`InvitationServiceTests.REG_INV_001_TheInvitationKeepsWhatItBindsAndTheVersionsShownAsync`,
`InvitationServiceTests.REG_INV_001_AnUnpublishedDocumentIsRefusedAsync`.

*Chapter text that should change.* 09 section 8a could name `documents` and say the
version is fixed at issue.

---

## 230. An organization on its way out takes no invitation

**Phase 8 · 2026-09-24 · Tier 3 · IDN-ORG-003, IDN-LIFE-009a**

*The question.* IDN-ORG-003: "Deletion requested | Organization suspends immediately;
access stops". Nothing says whether an invitation is issued into it.

*The readings.*

1. Issue it; the membership fails when it would attach.
2. Refuse it: `400 api.request.malformed` naming `id`, as an organization that does
   not exist is.

*Chosen: 2, the strictest reading.* A suspended organization admits nobody, and a
link sent into it could not be kept.

*Tests that pin it.*
`InvitationServiceTests.IDN_LIFE_009a_IssuingIsGatedAndSteppedUpAsync`.

*Chapter text that should change.* IDN-ORG-003 could say a suspended organization is
invited into by nobody.

---

## 231. One standing invitation per mailbox, and re-inviting an expired one

**Phase 8 · 2026-09-24 · Tier 3 · REG-MAIL-001, INT-MAIL-006**

*The question.* REG-MAIL-001: "An expired invitation SHALL leave the mailbox reserved
and disabled until the administrator re-invites or deletes it." Nothing says what an
invitation over an address another invitation already reserves does, or what becomes
of the expired invitation when the address is invited again.

*The readings.*

1. Several invitations may stand over one mailbox; the first acknowledged wins.
2. One invitation stands over a mailbox at a time. An open one, or a mailbox a member
   holds, refuses a second with `400 api.request.malformed` naming `corporateEmail`.
   An expired one is revoked in the transaction that issues the new one, and audited as
   revoked, and the mailbox is kept.

*Chosen: 2, the strictest reading.* Two invitations over one mailbox would let two
people race for one address. A partial unique index on the mailbox, over invitations
neither revoked nor acknowledged, holds it whatever a service does.

*Tests that pin it.*
`InvitationServiceTests.REG_MAIL_001_AC1_ExpiryLeavesTheMailboxReservedUntilTheAddressIsInvitedAgainAsync`,
`InvitationServiceTests.REG_MAIL_001_AnAddressAlreadyTakenIsRefusedAsync`,
`InvitationStoreTests.REG_MAIL_001_OneInvitationStandsOverAMailboxAsync`.

*Chapter text that should change.* REG-MAIL-001 could say re-inviting revokes the
expired invitation.

---

## 232. Issuing says nothing about who holds an account

**Phase 8 · 2026-09-24 · Tier 3 · REG-INV-001, REG-INV-002, D-076**

*The question.* 09 section 8a: "The identifier-mismatch refusal belongs to acceptance
(`POST /account/invitation/acknowledge`), not to issue". Nothing says whether issue
checks a bound identifier against the accounts.

*The readings.*

1. Refuse at issue an identifier another account holds.
2. Check nothing at issue; the mismatch is refused at acceptance.

*Chosen: 2.* 09 places the refusal at acceptance, and an answer at issue would tell
the administrator which addresses hold accounts, which D-076 closes everywhere else.

*Tests that pin it.* None at issue: nothing is read, so nothing can be answered. The
acceptance refusal is pinned with the acknowledgement endpoint.

*Chapter text that should change.* None.

---

## 233. What revoking takes, and what an acknowledged invitation answers

**Phase 8 · 2026-09-24 · Tier 3 · IDN-LIFE-009a, REG-MAIL-001**

*The question.* 09 section 8a: "`DELETE /admin/organizations/{id}/invitations/{invitationId}`
| Revokes an unused invitation". Nothing says when an invitation stops being unused,
what a used one answers, or whether revoking is a step-up action; chapter 10's step-up
table lists `invitation:issue` alone.

*The readings.*

1. Unused until its token attaches to a registration.
2. Unused until it is acknowledged: an invitation attached but not acknowledged is
   revoked, and the membership never attaches.

*Chosen: 2, the strictest reading.* The administrator keeps the power to withdraw
until the moment the membership exists. An acknowledged invitation answers
`422 identity.invitation.expired` ("already used"); one of another organization, or
none, is `400 api.request.malformed` naming `invitationId`; a second revoke answers
`204` again. No step-up is asked: revoking takes access away and chapter 10 lists no
action for it. What the invitation bound is forgotten, and its mailbox is released only
where nobody ever held it (entry 221).

*Tests that pin it.*
`InvitationServiceTests.IDN_LIFE_009a_OnlyAnUnacknowledgedInvitationOfTheOrganizationIsRevokedAsync`,
`InvitationServiceTests.REG_MAIL_001_RevokingGivesUpAMailboxNobodyHeldAsync`,
`InvitationServiceTests.REG_MAIL_001_RevokingKeepsAMailboxSomeoneHeldAsync`,
`InvitationEndpointTests.IDN_LIFE_009a_AnUnusedInvitationIsRevokedAsync`.

*Chapter text that should change.* 09 section 8a could define unused as not yet
acknowledged, and give the `204` and the refusals.

---

## 234. What an invitation row keeps, and for how long

**Phase 8 · 2026-09-24 · Tier 3 · PRIV-RIGHT-005a, PRIV-RIGHT-005c, REG-INV-001, IDN-LIFE-009a**

*The question.* An invitation binds a person's addresses before any account holds
them, so no subject key exists to encrypt them under. PRIV-RIGHT-005a lists the
personal fields that are encrypted; nothing says where an invitation's are held, or
how long.

*The readings.*

1. Hold the bound identifiers in plaintext until the invitation is used.
2. Hold them as one encrypted document under a key of the row's own, and forget the
   document and the key when the invitation is revoked, acknowledged or swept after
   expiry. The token is held only as its fingerprint. The audit record names the
   invitation and not what it bound.

*Chosen: 2, the strictest reading.* Nothing about a person survives an invitation that
led nowhere, and what stays is who invited into what, and when.

*Tests that pin it.*
`InvitationStoreTests.REG_INV_001_AnInvitationReadsBackAsItWasIssuedAsync`,
`InvitationStoreTests.REG_INV_001_ARevokedInvitationForgetsWhatItBoundAsync`,
`ModelTests` (the `invitations` columns).

*Chapter text that should change.* PRIV-RIGHT-005a could name an invitation's bound
identifiers among the personal fields and when they are forgotten.

---

## 235. Which language a message goes out in, and how a message in every language counts

**Phase 8 · 2026-09-24 · Tier 3 · IDN-ATTR-001, AUTH-ABUSE-004, AUTH-ABUSE-005**

*The question.* IDN-ATTR-001 gives the "Resolution order for any outbound message":
"1. Stored account preference, where set 2. The current request's locale, where a
request exists 3. **Every language in `notification.languages`** (the deployment
declares them, D-153), only when there is neither", and "**Registration SHALL set the
preference from the request locale**". AC2: "A background-triggered notification
resolves language without a request." AC3: "An account with no preference and no
request receives every declared language." Phase 1 sent every message in one
language and fell back to the first declared one, so AC2 and AC3 were not met;
registration settled no preference. Four points are open. Whose request step 2
reads. How a request's tag meets the declared list. Whether the approval of a
recovery carries the approver's locale. And how step 3 counts against the
restrictions, where AUTH-ABUSE-004 AC1 counts messages: "a second email to one
address inside 60 seconds is refused", and `email.destination` holds "1 per 60 s,
fixed", which would refuse the second language of one message judged language by
language.

*The readings.*

1. *Whose request.* (a) Any request in progress when the message is sent, including
   an administrator's. (b) Only a request the recipient made: registration, an
   anonymous sign-in link or code, a recovery request, the notice that no account
   holds an address, and the notice to the holder of an address someone tried to
   register (whose request is the registrant's, so the holder's stored preference
   answers first and the registrant's locale second).
2. *Matching.* (a) Exactly as written. (b) By the lookup of RFC 4647 section 3.4:
   case-insensitive, truncating a subtag at a time and a trailing singleton with it,
   so `en-GB` finds a declared `en`; what is found is always a declared tag, which
   the catalogue then answers exactly as entry 123 decided.
3. *Approval.* (a) `IRecovery.ApproveAsync` keeps its language argument. (b) It
   loses it: the approver's locale says nothing of the person recovered.
4. *Counting step 3.* (a) Judge and count each language as a send of its own, which
   refuses the second language inside the minute and leaves AC3 unreachable for
   mail. (b) Judge once and count once under one reference, which counts two mails
   as one. (c) Judge the request once, then count each language a transport takes
   as a message of its own, under its own reference and announcement, so a failed
   delivery report releases that one alone; where a transport takes one language
   and refuses the next, what it took counts and the row stays for the retry.

*Chosen: 1b, 2b, 3b and 4c; 4c is the strictest reading AC3 leaves open.* An
administrator's locale is not the recipient's, so it never decides the language, and
the public surface is smaller for it. A signed-in person's own operations carry no
locale either: registration settles the preference, so step 1 answers them. Exact
matching would send a person whose browser says `ar-EG` every language instead of
the Arabic declared. Counting once would let one request put two mails in a bucket
that holds one; judging each language would make AC3 impossible for mail. Under 4c
every message a transport took is in the buckets, and the next request inside the
minute is refused. `SendRequest.Language` is nullable, and null means every declared
language. Alerts, which were sent once per language, and an invitation link (entry
227), which names no request of the invitee's, now take step 3.

*Tests that pin it.*
`RecipientLanguageTests.IDN_ATTR_001_ThePreferenceComesBeforeTheRequestAndEitherBeforeEveryLanguage`,
`RecipientLanguageTests.IDN_ATTR_001_ATagFindsTheDeclaredLanguageItNarrows`,
`SendingServiceTests.IDN_ATTR_001_AC3_NoKnownLanguageGoesOutInEveryDeclaredOneAsync`,
`SendingServiceTests.IDN_ATTR_001_ALanguageTheTransportRefusedLeavesTheMessageRecordedAsync`,
`SendingServiceTests.IDN_ATTR_001_AKnownLanguageIsTheOnlyOneSentAsync`,
`LossReportsTests.IDN_ATTR_001_AC2_ANoticeTheSweepSendsResolvesItsLanguageWithoutARequestAsync`,
`RegistrationServiceTests.IDN_ATTR_001_RegistrationSettlesTheLanguageItWasBegunInAsync`,
`RegistrationServiceTests.IDN_ATTR_001_ALocaleTheDeploymentDoesNotWriteInSettlesNoneAsync`,
`RegistrationServiceTests.IDN_ATTR_001_TheHolderIsToldInTheirOwnLanguageAsync`,
`RegistrationDirectoryTests.IDN_ATTR_001_TheAccountKeepsTheLanguageItsRegistrationFoundAsync`,
`SendOutboxTests.IDN_ATTR_001_AMessageInEveryLanguageReadsBackWithNoneAsync`.

*Chapter text that should change.* IDN-ATTR-001 could say that step 2 reads only a
request the recipient made, and that a tag is matched against
`notification.languages` by RFC 4647 lookup. The `notification.languages` row in
chapter 10 could say that step 3 is one message per language. AUTH-ABUSE-004 could
say that such a request is judged once and each language counts as one send.

---

## 236. The token a registration is begun with travels as `invitationToken`

**Phase 8 · 2026-09-24 · Tier 2 · REG-INV-001, REG-SESS-001**

*The question.* 09 section 3: "A staff invitation token is not an enrolment token: it
opens a registration session (`POST /register` with the token, REG-INV-001)". The
body 09 section 2 gives `POST /register` is `{ "clientId": "..." }` and names no member
for the token. REG-SESS-001 lists "the invitation token" among what the session
stages.

*The readings.*

1. A member `token`.
2. A member `invitationToken`, as `POST /register/verify/{id}` names its token
   `linkToken`.
3. An endpoint of its own.

*Chosen: 2.* It names what the token is and adds no endpoint. The session stages the
invitation it opened, never the token: the token is a credential and the session
document outlives the request.

*Tests that pin it.*
`RegistrationFlowTests.IDN_LIFE_009a_AC2_AnInvitationTokenThatOpensNothingIsRefusedAsync`,
`RegistrationSessionStoreTests.IDN_LIFE_009a_ARegistrationKeepsTheInvitationThatOpenedItAsync`.

*Chapter text that should change.* 09 section 2 could add `invitationToken` to the
`POST /register` body.

---

## 237. What the press on the link verifies, and a bound email an account holds

**Phase 8 · 2026-09-24 · Tier 3 · REG-INV-001, REG-MAIL-001, REG-INV-002, REG-SESS-005, D-076**

*The question.* REG-MAIL-001: "the press on the invitation link in the registering
browser (REG-SESS-003) is the verification, and the person does nothing further for
it". REG-INV-002: "A person who already holds an account SHALL accept an invitation by
**signing in**". REG-SESS-005 answers a registration identically whether an
identifier is held. Nothing says what a registration begun with a link whose bound
email an account already holds does: the press verifies the address, so the terms
step would create a second holder of it.

*The readings.*

1. Stage the bound email verified and let the terms step fail.
2. Refuse at `POST /register` with `identity.invitation.identifiermismatch`, before
   the invitation is spent. Check the bound phone the same way.
3. As 2 for the email only. The phone is staged locked and goes through its step as
   any phone does, so where an account holds it the holder is told instead of sent a
   code (REG-SESS-005).

*Chosen: 3, the strictest reading.* `POST /register` with the token is the press: it
comes from the browser the session is then bound to, and a plain open of the landing
does nothing. The link went to the bound email alone (entry 226), so only that
mailbox can learn that an account holds the address, and it learns it in the code
chapter 10 gives for "An identifier bound to the invitation is verified on a different
account than the one accepting". Where no email is bound the administrator holds the
token (entry 226), so checking the phone would answer the administrator whether a
number holds an account, which entry 232 refuses.

*Tests that pin it.*
`RegistrationServiceTests.REG_MAIL_001_AC4_ThePressVerifiesTheBoundEmailWithoutACodeAsync`,
`RegistrationServiceTests.REG_INV_002_ABoundEmailAnAccountHoldsOpensNoRegistrationAsync`.

*Chapter text that should change.* REG-INV-001 could say that beginning a
registration with the token is the press, and that a bound email an account holds is
refused there with the mismatch code.

---

## 238. A token that opens nothing, and a link spent by a registration never finished

**Phase 8 · 2026-09-24 · Tier 3 · IDN-LIFE-009a, REG-INV-001, REG-SESS-001**

*The question.* IDN-LIFE-009a AC2: "The enrolment link is time-boxed and single-use."
Chapter 10: `identity.invitation.expired` is "Invitation link past its lifetime or
already used". Nothing names the answer to a token nobody issued or one revoked, or
says whether a registration abandoned or swept gives its link back. REG-SESS-001: an
expired or abandoned session "SHALL leave nothing behind".

*The readings.*

1. Distinguish an unknown token from a spent one.
2. Answer every token that opens nothing `identity.invitation.expired`. The press
   spends the link, and a registration that never finishes does not give it back.
3. As 2, but an abandoned or swept registration releases its invitation.

*Chosen: 2, the strictest reading.* One answer tells a guesser nothing, and a link
that could be used again after its registration ended would not be single use. What
the abandoned registration leaves is the invitation's own record that its link was
used, not anything of the person. The administrator revokes it and invites again
(entries 231 and 233).

*Tests that pin it.*
`RegistrationServiceTests.IDN_LIFE_009a_AC2_TheLinkOpensItsInvitationOnceAndInTimeAsync`,
`RegistrationFlowTests.IDN_LIFE_009a_AC2_AnInvitationTokenThatOpensNothingIsRefusedAsync`.

*Chapter text that should change.* Chapter 10's row for `identity.invitation.expired`
could add a token that opens no invitation, and IDN-LIFE-009a could say the press
spends the link.

---

## 239. A bound phone at its step

**Phase 8 · 2026-09-24 · Tier 2 · REG-INV-001, REG-IDENT-010, REG-MAIL-001, REG-SESS-002**

*The question.* REG-INV-001: "A named identifier SHALL be pre-filled and locked at
registration". REG-MAIL-001 AC3: "A bound phone is verified before the membership step
can be reached." REG-IDENT-010 gives every other identifier "its own Change". Nothing
says how the code reaches a bound phone, or how a new one is asked for when Change is
what asks for one and a locked identifier has none.

*The readings.*

1. Send the code when the token is pressed.
2. Stage the phone locked at the press. `PUT /register/phone` at the phone step takes
   the bound number and no other, and sends the code. Change on it takes its own
   number and no other, and sends a new code while it is unverified. Anything else is
   `identity.identifier.locked`. The phone step cannot be skipped.

*Chosen: 2.* Nothing goes to an identifier before its step (REG-PROF-002 AC1), the
person asks for each code as for any other, and the lock refuses every other value.
An email the invitation bound was verified by the press, so the age step leads past
the email step.

*Tests that pin it.*
`RegistrationServiceTests.REG_INV_001_AC1_ABoundIdentifierCannotBeChangedAndAnOpenOneCanAsync`,
`RegistrationServiceTests.REG_MAIL_001_AC3_ABoundPhoneIsVerifiedBeforeTheRegistrationGoesOnAsync`.

*Chapter text that should change.* 09 section 2 could say `PUT /register/phone` and
Change take a bound phone's own number and send its code.

---

## 240. What of the inviting organization's policy governs the registration

**Phase 8 · 2026-09-24 · Tier 3 · IDN-LIFE-009a, REG-INV-001, REG-INV-002, REG-DOM-001, REG-SESS-001**

*The question.* REG-INV-001: "The organization's policy SHALL govern every step from
the moment the token attaches (IDN-LIFE-009a)." REG-SESS-001 AC4: "no account exists in
a state that is not `active` immediately after creation, save where an invitation's
policy holds it at enrolment (AUTH-RECOV-001 enforced)". 09 section 6a gives the
acknowledgement "**403** `auth.stepup.required` with outcome `enrol` when the account
does not yet satisfy the organization's `requiredAssurance` or
`credentialRedundancy`". REG-DOM-001: "any open email chosen at invitation acceptance
SHALL be in the list". Nothing says which fields a registration step reads.

*The readings.*

1. Only the acknowledgement reads the policy.
2. Every field at the step it bears on: the security step refuses to complete below
   `requiredAssurance`.
3. The policy in force for the registration is the one a member of the inviting
   organization resolves to. It decides which login factors complete the security
   step and which addresses the lock admits: every email the person chooses at step
   2, among the extras of step 4, or by a Change. `requiredAssurance` and
   `credentialRedundancy` are judged at the acknowledgement, which 09 gives the refusal
   for, and the membership attaches only once they are met.

*Chosen: 3, the strictest reading the chapters leave consistent.* Reading 1 lets a
registration finish on a factor the organization forbids. Reading 2 contradicts
REG-SESS-001 AC4, which lets an invitation's policy hold a created account at
enrolment. Under 3 nothing of the organization is granted before the acknowledgement,
and the acknowledgement grants nothing below the policy. An email the invitation bound
is not held to the lock at registration: it was judged at issue where it is the
sign-in address (entry 224).

*Tests that pin it.*
`RegistrationServiceTests.IDN_LIFE_009a_TheInvitingOrganizationsPolicyGovernsTheRegistrationAsync`,
`RegistrationServiceTests.REG_DOM_001_AC2_AnOpenEmailOutsideTheListIsRefusedAsync`,
`RegistrationServiceTests.REG_INV_001_AC2_TheAccountHoldsTheInvitationAndNoMembershipAsync`.

*Chapter text that should change.* REG-INV-001 could list which policy fields a
registration step reads and leave `requiredAssurance` and `credentialRedundancy` to the
acknowledgement.

---

## 241. Where a signed-in person's press on the link attaches the invitation

**Phase 8 · 2026-09-24 · Tier 2 · REG-INV-002, IDN-LIFE-009a, REG-SESS-002**

*The question.* REG-INV-002 AC1: "Opening an invitation while signed in, or signing in
from the invitation landing page, reaches the membership step without a registration
session." 09 section 3: the token opens "for a person who already holds an account, a
sign-in followed by the membership step (REG-INV-002)". 09 section 2: a `POST
/register` that "arrives with a live session creates no registration session and is
answered with the account landing (REG-SESS-002)". No endpoint is named that attaches
the invitation to an account.

*The readings.*

1. A new endpoint under `/account` that takes the token.
2. Every sign-in endpoint takes the token.
3. `POST /register` with `invitationToken` from a signed-in browser attaches the
   invitation to that account and is answered as any signed-in request to it is,
   `registration.signedin`, which sends the frontend to the account; the landing page
   presses it again after a sign-in. `IInvitations.OpenAsync` is the operation in
   process.

*Chosen: 3.* It is the one route 09 gives the token, it adds no endpoint, and both
cases of AC1 reach it. The press spends the link as a registration's does (entry
238), and every token that opens nothing is answered alike. Nothing is checked of the
account's identifiers here: the mismatch is the acknowledgement's (09 section 8a).

*Tests that pin it.*
`InvitationServiceTests.REG_INV_002_AC1_ALinkPressedWhileSignedInAttachesToThatAccountAsync`,
`RegistrationFlowTests.REG_INV_002_AC1_ALinkPressedWhileSignedInAttachesToTheAccountAsync`.

*Chapter text that should change.* 09 section 2 could say that a signed-in `POST
/register` carrying `invitationToken` attaches the invitation to the account before it
answers.

---

## 242. What the membership step reads, and the answer where nothing is attached

**Phase 8 · 2026-09-24 · Tier 2 · REG-INV-002, REG-INV-001, chapter 09 section 6a**

*The question.* 09 section 6a: `GET /account/invitation` "Returns, for the invitation
attached to the signed-in person's registration or sign-in (REG-INV-001, REG-INV-002):
who invited them, the organization, the roles and grants that will attach, and the
documents attached to the invitation with their versions. **404** when no invitation
is attached." No member names are given, no code for the 404 is named, nothing says
which invitation is read where an account opened more than one link, or what "who
invited them" carries.

*The readings.*

1. A bare 404, as the photo read answers, and the inviter's display name, else their
   primary email.
2. `identity.invitation.notfound` for the 404, as every other absent record of 09 is
   answered (`identity.takedown.notfound`, `auth.credential.notfound`). The body is
   `{ id, organization, organizationName, invitedBy, roles, documents: [{ document,
   version }], expiresAt }`. `invitedBy` is the display name the inviter's account
   shows, or null. The invitation read is the standing one (neither acknowledged nor
   revoked) whose link the account opened last, expired or not.

*Chosen: 2.* A code is how every other absent record is answered, and the in-process
operation needs one to refuse with. The inviter's email is theirs and not the
organization's, so nothing of the inviter is shown that their account does not
already show others. An expired invitation is still read, so the frontend can say
it expired rather than that there is none; the acknowledgement refuses it. The
grants that attach are the roles, each granted across the organization, so `roles`
carries both.

*Tests that pin it.*
`InvitationServiceTests.REG_INV_002_TheMembershipStepReadsTheInvitationOpenedLastAsync`,
`InvitationStoreTests.REG_INV_002_AnAccountReadsTheInvitationItOpenedLastAsync`,
`InvitationAcknowledgementFlowTests.REG_INV_002_TheMembershipStepReadsTheAttachedInvitationAsync`.

*Chapter text that should change.* 09 section 6a could give the body and the code
`identity.invitation.notfound` for the 404, and say which invitation is read.
Chapter 10 section 1.1 could add the row for `identity.invitation.notfound`.

---

## 243. How the account keeps the personal email through the membership

**Phase 8 · 2026-09-24 · Tier 3 · REG-MAIL-001, REG-MAIL-003, REG-IDENT-002, REG-IDENT-005, REG-IDENT-006**

*The question.* REG-MAIL-001: "The personal email SHALL stay on the account as a
verified, non-primary email for the whole membership and SHALL be in the
security-notice set (REG-IDENT-002) whatever the backup setting". REG-MAIL-003: the
personal email "SHALL become the primary email **automatically, in the same
operation**" when the membership ends. The invitation forgets what it bound at the
acknowledgement, so nothing records which email that is, and nothing says what the
person is told on trying to remove it, make it primary or replace it.

*The readings.*

1. Derive it when needed: the verified email that is not the corporate address.
2. Record it on the membership row.
3. A flag on the identifier row, `is_personal`, set at the acknowledgement and held to
   a verified email that is not the primary by a check constraint. While it is set the
   security-notice set holds the email whatever the backup setting, and removing it,
   making it primary or replacing it is refused with `identity.identifier.locked`. The
   account view shows it `locked`.

*Chosen: 3, the strictest reading.* Reading 1 has no answer where the account holds
two other emails. Reading 2 makes the identifier rules read the memberships. The
flag keeps every rule over identifiers in the set. The person cannot remove the one
address a compromised corporate mailbox cannot silence, cannot make it primary while
the corporate address is, and cannot replace it with an address the invitation never
proved; `identity.identifier.locked` already says nothing about it is theirs to
change.

*Tests that pin it.*
`IdentifierSetTests.REG_MAIL_001_AC5_ThePersonalEmailStaysVerifiedNonPrimaryAndNotified`,
`IdentifierSetTests.REG_MAIL_001_OnlyAVerifiedEmailOtherThanThePrimaryIsKept`,
`IdentifierServiceTests.REG_MAIL_001_AC5_ThePersonalEmailStaysAsTheMembershipKeepsItAsync`,
`IdentifierStoreTests.REG_MAIL_001_ThePersonalEmailAMembershipKeepsReadsBackKeptAsync`.

*Chapter text that should change.* REG-MAIL-001 could say that removing, promoting or
replacing the personal email during the membership is refused with
`identity.identifier.locked`, and chapter 10's row for that code could name it.

---

## 244. What the acknowledgement names, and what it answers where the invitation no longer stands

**Phase 8 · 2026-09-24 · Tier 2 · REG-INV-001, REG-INV-002, IDN-LIFE-009a, IDN-MEM-002, chapter 09 section 6a**

*The question.* Chapter 09 gives `POST /account/invitation/acknowledge` its answers
(**204**, **422** `identity.invitation.expired`, **422**
`identity.invitation.identifiermismatch`, **403** `auth.stepup.required`) and no
request body. It does not say which invitation is acknowledged where more than one is
attached to the account, what is answered where none is, or whether an organization
whose deletion is requested takes the member.

*The readings.*

1. No body: the acknowledgement takes whatever `GET /account/invitation` would answer at
   that moment.
2. The body names the invitation the membership step showed, `{ "invitationId": "..." }`,
   required (`api.request.malformed` where absent).

*Chosen: 2.* What REG-INV-001 has the person acknowledge is what they were shown; under
reading 1 a link opened in another tab between the read and the press would attach a
membership, roles and documents the person never saw. The answers:

- `identity.invitation.notfound` (**404**, the code of entry 242) where the invitation
  does not exist or is attached to another account, so nothing is learned of anyone
  else's invitation.
- `identity.invitation.expired` (**422**) where it was revoked, was acknowledged
  already, is past `expiresAt`, or its organization has a deletion requested or was
  erased: an organization on its way out takes no new member, as it issues no new
  invitation.
- `identity.membership.limitreached` where `organization.multiplememberships` leaves
  the account no room (IDN-MEM-002), with nothing written and the invitation standing.
- `identity.identifier.maximum` where the corporate address would take the account past
  `identifiers.email.max`.

Everything the acknowledgement writes is written in one transaction, and a refusal
writes nothing.

*Tests that pin it.*
`InvitationServiceTests.REG_INV_002_OnlyAStandingInvitationOfTheAccountIsAcknowledgedAsync`,
`InvitationServiceTests.IDN_MEM_002_AnAccountAtItsMembershipLimitIsRefusedAsync`,
`MembershipAttachmentTests.IDN_MEM_002_AnAccountThatMayHoldNoMoreIsRefusedAsync`,
`InvitationAcknowledgementFlowTests.REG_INV_002_TheAcknowledgedInvitationIsNamedAsync`,
`InvitationServiceTests.REG_MAIL_001_TheCorporateAddressCountsAgainstTheEmailMaximumAsync`.

*Chapter text that should change.* Chapter 09 section 6a could give the body
`{ "invitationId" }`, and add **404** `identity.invitation.notfound`,
`identity.membership.limitreached` and `identity.identifier.maximum` to the answers.

---

## 245. Which identifiers must match at the acknowledgement

**Phase 8 · 2026-09-24 · Tier 3 · REG-INV-001, REG-INV-002, REG-MAIL-001, REG-DOM-001**

*The question.* Chapter 09 section 6a: `identity.invitation.identifiermismatch` is
answered where "a bound identifier is verified on a different account than the one
accepting". REG-INV-001: "Binding the phone means the link alone is not enough to
accept". Neither says what is answered where the accepting account does not hold the
bound identifier and no other account does either, nor what happens where the corporate
address the organization asserts is already held.

*The readings.*

1. Refuse only where another account holds a bound identifier verified.
2. Refuse unless the accepting account holds every bound email and phone verified, and
   refuse where any account, this one included, already holds the corporate address.

*Chosen: 2, the strictest reading.* Under reading 1 an account holding neither the
bound phone nor any claim to it accepts an invitation whose phone was bound precisely
so that the link alone would not be enough. Reading 2 grants least. On registration
from an invitation the bound email is verified by the press and the bound phone before
the membership step, so reading 2 refuses nothing there that reading 1 admits. The
values are compared in canonical form. The domain lock is not read at the
acknowledgement: REG-DOM-001 governs the sign-in email and an open email chosen at
registration, and both are already enforced where they are used.

*Tests that pin it.*
`InvitationServiceTests.REG_INV_002_AnIdentifierTheInvitationBindsIsVerifiedOnTheAccountAsync`.

*Chapter text that should change.* Chapter 09 section 6a could read "a bound identifier
is not verified on the account accepting, or the corporate address is already held".

---

## 246. How the credential policy is met before the membership attaches

**Phase 8 · 2026-09-24 · Tier 3 · REG-INV-002, IDN-LIFE-009a, IDN-LIFE-009b, AUTH-FACT-002**

*The question.* REG-INV-002: the organization's `requiredAssurance`,
`credentialRedundancy` and `loginFactors` "SHALL be satisfied before the membership
attaches"; chapter 09 section 6a answers **403** `auth.stepup.required` "with outcome
`enrol`". Neither says which credentials count, which policy is read where the account
already belongs elsewhere, or what the refusal carries beside `outcome`.

*The readings.*

1. Count every credential the account holds against the organization's own policy.
2. Count only the credentials whose factor the policy in force once the membership
   attaches permits, against that policy (the strictest of the organization's and of
   every organization the account already belongs to).

*Chosen: 2, the strictest reading.* A credential the organization does not permit stops
signing in once the membership attaches (IDN-LIFE-009b), so counting it would attach a
membership the account then cannot sign in under. The refusal carries `outcome`
(`enrol`), `field` (`requiredAssurance` or `credentialRedundancy`, the `PolicyField`
vocabulary a policy hold already uses) and `value` (the level or `enforced`).
`policy.enforcement.grace` is not applied: it is the run-up for people already under a
policy that is raised, and an account joining is not yet under it. IDN-LIFE-009b needs
nothing written: the session gate refuses any factor outside the policy in force, and
the membership makes the organization's policy the one in force.

*Tests that pin it.*
`InvitationServiceTests.REG_INV_002_AC2_AnAccountBelowTheRequiredAssuranceIsHeldAtEnrolmentAsync`,
`InvitationServiceTests.REG_INV_002_EnforcedRedundancyAsksForASecondCredentialAsync`,
`SessionServiceTests.IDN_LIFE_009a_AC3_APasswordHeldBeforeTheMembershipNoLongerAuthenticatesAsync`.

*Chapter text that should change.* Chapter 09 section 6a could name the details of the
**403** and say that only the factors the policy permits are counted.

---

## 247. How the roles of an invitation are granted

**Phase 8 · 2026-09-24 · Tier 2 · REG-INV-001, IDN-LIFE-009a, AUTHZ-GRANT-001**

*The question.* REG-INV-001: on acknowledgement "the membership SHALL attach" with "the
roles and grants that will attach". Nothing says in what scope, of what kind, granted by
whom, with what reason, or what happens to a role the account already holds.

*The readings.*

1. Each role as a stored grant across the organization, granted by who issued the
   invitation, with the machine reason `invitation:<id>` in the precedent of
   `derivation:<relationship>`, once however often the invitation names it, and not
   again where the account already holds the same live grant.
2. The same, granted by the person acknowledging.

*Chosen: 1.* The inviter is who decided the grant and whose permission to grant it was
checked at issue (a role asks what a grant asks, entry 228); the person acknowledging
decided nothing but to accept. The reason names the invitation so that the audit record
of the issue explains the grant; the words are the frontend's (CONV-CONTENT-001).

*Tests that pin it.*
`InvitationServiceTests.REG_INV_001_AC3_AcknowledgingAttachesTheMembershipThatWasShownAsync`,
`MembershipAttachmentTests.REG_INV_001_AC3_TheMembershipCarriesTheAcknowledgementAndTheGrantsAsync`,
`MembershipAttachmentTests.REG_INV_001_AGrantTheAccountHoldsIsNotWrittenAgainAsync`.

*Chapter text that should change.* REG-INV-001 could state the scope, the grantor and
the reason of the grants an invitation attaches.

---

## 248. What the corporate address does at the acknowledgement

**Phase 8 · 2026-09-24 · Tier 2 · REG-MAIL-001, REG-INV-001, REG-IDENT-004, INT-MAIL-006**

*The question.* Chapter 09 section 6a: the acknowledgement "makes the corporate address
primary where one exists, enables the pre-provisioned mailbox" and "Emits
`MembershipChanged` and, where the primary email changed, `IdentifierPrimaryChanged`".
Nothing says whether the person is told of the address as of any identifier added
(REG-IDENT-004), or which email the membership keeps where the account holds several.

*Chosen.* The corporate address is added verified, locked and primary; the email the
membership keeps (entry 243) is the personal email the invitation bound, which entry 245
has made sure the account holds verified. The security-notice set as it stood before
the change is told of the address once, as `IdentifierAdded`, which is what any added
identifier sends, so a person whose account gained an address they did not expect hears
of it at the address they already had. The mailbox becomes the person's in the same
transaction and is owed enabled from then on; the provisioning pass pushes it. The two
events of chapter 09 are the only ones published.

*Tests that pin it.*
`InvitationServiceTests.REG_INV_001_AC4_TheCorporateAddressBecomesPrimaryBesideThePersonalEmailAsync`,
`IdentifierStoreTests.REG_MAIL_001_TheCorporateAddressIsTakenOnPrimaryBesideThePersonalEmailAsync`.

*Chapter text that should change.* REG-MAIL-001 could say that the security-notice set
is told of the corporate address when it is taken on.

---

## 249. How the acknowledgement is audited and exported

**Phase 8 · 2026-09-24 · Tier 2 · REG-INV-001, IDN-AUD-001, PRIV-RIGHT-003, REG-ACCT-001**

*The question.* IDN-AUD-001 has every organization change recorded, and entry 150 has
the export carry everything held about the person. Chapter 10 has no audit action for
an acknowledgement, and the export has no place for what REG-INV-001 AC3 records on the
membership.

*Chosen.* A new audit action, `identity.invitation.acknowledged`, security category,
filed under the organization with the person as the actor and the invitation in the
details, as its issue and revocation are (entries 231 and 234). The export's
`memberships` record gains `acknowledgedAt`, and a new section,
`membership-acknowledgements`, follows it with one record per document acknowledged:
`membership`, `document`, `version`, `acknowledgedAt`.

*Tests that pin it.*
`InvitationServiceTests.REG_INV_001_AC3_AcknowledgingAttachesTheMembershipThatWasShownAsync`,
`ExportSourceTests.REG_INV_001_AC3_TheExportCarriesWhatWasAcknowledgedAsync`,
`AuditActionsTests` (the list of actions).

*Chapter text that should change.* Chapter 10 could hold the audit action row below,
and chapter 09's export description could name the new section.

---

## 250. Where a membership is ended, and what it answers

**Phase 8 · 2026-09-24 · Tier 2 · IDN-MEM-001, REG-MAIL-003, LIB-API-005, chapter 09 section 8a**

*The question.* Chapter 09 section 8a lists `DELETE /admin/organizations/{id}/memberships/{subject}`
in its table of memberships and invitations under `membership:manage`, with "Ends a membership; the
account and organization persist (IDN-MEM-001)". It names no answers, and no service
contract carries the operation. Chapter 10 section 5a lists no step-up action for it,
and no chapter asks a reason.

*The readings.*

1. A method on `IInvitations`, the contract that already carries the section's other
   operations under the same permission.
2. A method on `IOrganizations`, whose operations are `organization:manage` in the
   administrative organization.
3. A new public contract for memberships.

*Chosen: 1.* It adds one method and no type, and keeps one contract per permission of
the section. `membership:manage` is asked in the organization the membership is of, as
it is for issuing and revoking. No step-up is asked, because chapter 10 section 5a lists
none, and no reason, because the endpoint names no body. The answers:

- **204** where the membership ended.
- **400** `api.request.malformed` naming `subject` where the account holds no current
  membership of the organization, in the precedent of revoking an invitation the
  organization never issued (`invitationId`), so ending twice is refused the second time.
- **403** `authz.denied` without the permission, or where no person acts.

The end is written in one transaction with everything it changes. It is audited as a new
action, `identity.membership.ended`, security category, filed under the organization,
with the administrator as the acting subject, the member as the effective subject, and
`membership` in the details, in the precedent of an administrator acting on someone
else's account (the recovery audit). `MembershipChanged` is published with `change`
`ended` under the key `membership-ended:<membership>@<ticks>` the organization erasure
already uses for the same event. The account's state is not read or written
(REG-MAIL-003: "Membership end SHALL NOT by itself change the account's state").

*Tests that pin it.*
`InvitationServiceTests.IDN_MEM_001_OnlyACurrentMembershipIsEndedAsync`,
`InvitationServiceTests.REG_MAIL_003_AC2_EndingTheMembershipRetiresTheCorporateAddressAsync`,
`InvitationEndpointTests.IDN_MEM_001_AMembershipIsEndedAsync`,
`MembershipEndingTests.IDN_MEM_001_AC2_TheMembershipEndsAndItsRecordStaysAsync`,
`SessionRequirementTests` (the list of session routes), `AuditActionsTests` (the list of
actions).

*Chapter text that should change.* Chapter 09 section 8a could give the endpoint its
answers, and chapter 10 could hold the audit action row below.

---

## 251. What the end of a membership does to the corporate address

**Phase 8 · 2026-09-24 · Tier 2 · REG-MAIL-003, REG-IDENT-005, INT-MAIL-006a, chapter 10 section 5b**

*The question.* REG-MAIL-003: "the corporate address SHALL stop being a valid identifier
of the account and the mailbox SHALL be disabled (INT-MAIL-006a)", the personal email
"SHALL become the primary email **automatically, in the same operation**", and "A
retired corporate address SHALL be available to a later invitation". Chapter 10 section
5b has `IdentifierPrimaryChanged` fire "when a membership ends (REG-MAIL-003)". REG-IDENT-005
has setting the primary "produce one notice to the security-notice set". Nothing says
which set is told where the primary moves because the old one left, whether the address
is removed or kept unusable, or which membership's end retires the mailbox where the
account holds more than one.

*Chosen.*

- Only the end of a membership of the administrative organization retires anything,
  because the mailboxes are that organization's (INT-MAIL-006); ending another
  membership of the same account leaves the address, the primary and the mailbox where
  they were.
- The corporate address is removed from the account, so it resolves to nobody at every
  path, including recovery (REG-MAIL-003 AC1: it "behaves as an unknown identifier").
  No removal record is written: the address was never the person's to take back, and
  REG-MAIL-003 makes it free for a later invitation.
- The personal email becomes the primary and is kept by no membership from then on; a
  phone is not touched.
- The mailbox is retired, which leaves it owed disabled whatever its holder's standing;
  the provisioning pass pushes the disable, which ends every app password (INT-MAIL-006a).
- The set as it stands after the change is told once, as `IdentifierSettingsChanged`:
  the set before, less the address that left, since the personal email was in it and no
  backup setting changed. The retired address is told nothing, because it behaves as
  unknown and its mailbox is the organization's.
- `IdentifierPrimaryChanged` is published for the personal email, keyed
  `<identifier>@<ticks>` as the acknowledgement keys it.

*Tests that pin it.*
`InvitationServiceTests.REG_MAIL_003_AC2_EndingTheMembershipRetiresTheCorporateAddressAsync`,
`InvitationServiceTests.IDN_MEM_001_AC1_EndingAnotherMembershipLeavesTheCorporateAddressAsync`,
`IdentifierSetTests.REG_MAIL_003_AC2_ThePersonalEmailBecomesPrimaryAndTheCorporateAddressLeaves`,
`IdentifierStoreTests.REG_MAIL_003_TheCorporateAddressLeavesAndThePersonalEmailIsPrimaryAsync`,
`MailboxStoreTests.REG_MAIL_003_TheMailboxAnAccountHoldsIsReadUntilRetiredAsync`.

*Chapter text that should change.* REG-MAIL-003 could say that the security-notice set
as it stands after the change is told once, and that only the end of the administrative
organization's membership retires the address.

---

## 252. Whether the end of a membership removes the member's grants

**Phase 8 · 2026-09-24 · Tier 3 · IDN-MEM-001, REG-MAIL-003, chapter 16 section 3**

*The question.* No chapter says whether ending a membership removes the grants the
account holds in the organization. The gate does not ask for a membership, so a grant
kept after the end still confers once the account is active.

*The readings.*

1. Ending the membership removes the account's grants in the organization: the reading
   that grants least.
2. Ending the membership changes only what REG-MAIL-003 and chapter 16 step 3 list, and
   the grants stay until an administrator removes or transfers them.

*Chosen: 2.* Reading 1 is not open. Chapter 16 section 3 orders the steps of a departure
and makes the grants a step of their own after the end of membership: "Remove or transfer
their grants", with "If they were the sole holder of a permission, transfer it before
removing". Removing them at step 3 would make that instruction impossible to follow, and
chapter 16 step 3 and REG-MAIL-003 list what the end does without the grants. The risk
the owner should see: an account whose suspension (step 2) is reversed before step 4 is
done holds its former organization's grants again, since IDN-LIFE-013 suspends grants
and does not remove them.

*Tests that pin it.* None writes a grant: `MembershipEnd` holds no grant store, and
`InvitationServiceTests.REG_MAIL_003_AC2_EndingTheMembershipRetiresTheCorporateAddressAsync`
pins everything the end writes.

*Chapter text that should change.* IDN-MEM-001 could state that ending a membership does
not remove the account's grants in the organization, pointing at chapter 16 step 4, or
the owner could decide that it does and reorder chapter 16.

---

## 253. What an erasure does to an invitation attached to the subject

**Phase 8 · 2026-09-24 · Tier 3 · PRIV-RIGHT-005, PRIV-RIGHT-005a, REG-INV-001**

*The question.* Entry 234 holds what an invitation binds under a key of the row's own
and forgets it when the invitation is revoked, acknowledged or swept after expiry. An
invitation attached to an account that is erased before any of those happens still
holds the person's addresses, readable, until it expires. PRIV-RIGHT-005 has an erasure
leave nothing of the subject readable; nothing names the invitation.

*The readings.*

1. Leave the invitation to the expiry sweep.
2. Forget what every invitation attached to the subject binds, and its key, in the
   erasure's own transaction, keeping the row that names who invited into what.

*Chosen: 2, the strictest reading.* The addresses are the subject's, and under reading 1
they outlive the erasure by up to `link.invitation.lifetime`. An invitation attached to
nobody cannot be tied to the subject and keeps what it binds until it is used or expires,
as entry 234 has it.

*Tests that pin it.*
`SubjectEraserTests.PRIV_RIGHT_005a_WhatAnAttachedInvitationBindsGoesWithTheSubjectAsync`.

*Chapter text that should change.* PRIV-RIGHT-005a could name the identifiers an
attached invitation binds among what an erasure makes unreadable.

---

## 254. What an administrator's suspension and reactivation answer and record

**Phase 8 · 2026-09-24 · Tier 2 · IDN-LIFE-013, AUTH-SESS-010, IDN-AUD-001, LIB-API-005, chapter 09 section 8a, chapter 10 section 5a**

*The question.* Chapter 09 section 8a lists `POST /admin/accounts/{subject}/suspend` and
`/reactivate` under "Accounts: `account:manage`", with "Suspension ends sessions in the
same operation (AUTH-SESS-010); reactivation restores grants exactly (IDN-LIFE-013).
Requires step-up". Chapter 10 section 5a names the step-up actions `account:suspend` and
`account:reactivate`. Nothing names the service contract, the answers, what either
announces or what either writes to the audit trail, and chapter 10 holds audit rows only
for the owner's own deactivation and reactivation, both routine.

*Chosen.*

- A new public contract, `IAccounts`, carries both, in the pattern of `IOrganizations`
  for the section's organization operations; the section's other account operations
  join it as they are built. `account:manage` is asked in the administrative
  organization, as every deployment operation is.
- **204** where the change is made; **400** `api.request.malformed` naming `subject`
  where no account bears it, in the precedent of entry 250; **403** `authz.denied`
  without the permission, where no person acts, or where the account is in a state the
  operation does not apply to (deleting or deleted for suspension, anything but an
  administrator's suspension for reactivation), in the takedown precedent; **403**
  `auth.stepup.required` where the session has not stepped up.
- Suspending an account an administrator already suspended changes nothing and answers
  204 before step-up is asked, in the precedent of requesting an organization's deletion
  twice.
- No reason is asked, because the endpoint names no body, unlike the takedown and the
  organization operations beside it.
- Suspension ends every session of the account in its transaction and publishes
  `AccountSuspended` with `by` `administrator` and the administrator as actor;
  reactivation publishes `AccountReactivated` with the administrator as actor.
- Both are audited in the transaction that makes the change, security category,
  acting subject the administrator and effective subject the account, no organization:
  suspension as a new action, `identity.account.suspended`, and reactivation as the existing
  `identity.account.reactivated`. An administrator acting on another person's standing
  is a security event (chapter 04, retention "Security events, permission changes"),
  while the owner's own reactivation stays routine as it was, so the category of an
  `identity.account.reactivated` row follows who acted.

*Tests that pin it.*
`AccountAdministrationTests.AUTH_SESS_010_SuspensionEndsTheSessionsOfTheAccountAsync`,
`AccountAdministrationTests.IDN_LIFE_013_AnAccountBeingDeletedOrUnknownIsNotSuspendedAsync`,
`AccountAdministrationTests.IDN_LIFE_013_SuspendingTwiceChangesNothingAsync`,
`AccountAdministrationTests.IDN_LIFE_013_WithoutThePermissionOrAStepUpNothingChangesAsync`,
`AccountAdministrationEndpointTests.AUTH_SESS_010_AnAccountIsSuspendedAndReactivatedAsync`,
`SessionRequirementTests` (the list of session routes), `AuditActionsTests` (the list of
actions).

*Chapter text that should change.* Chapter 09 section 8a could give the two endpoints
their answers, and chapter 10 could hold the audit rows below, with the category of
`identity.account.reactivated` depending on who acted.

---

## 255. An administrator's suspension of an account its owner deactivated

**Phase 8 · 2026-09-24 · Tier 3 · IDN-LIFE-013, D-140, chapter 16 section 3 step 2**

*The question.* IDN-LIFE-013 has the account record who suspended it "because the two
are reversed differently", and the self suspension reversed by its owner's link or by
recovery (D-140). Chapter 16 step 2 has the administrator suspend a departing person's
account so that "only an administrator can reverse it". Nothing says what suspending an
account its owner has already deactivated does, or whether an administrator may
reactivate an account its owner deactivated.

*The readings.*

1. Refuse the suspension, since the account is not active or restricted; the owner can
   still stand it back up with the link or by recovery.
2. The account stays suspended and becomes the administrator's: `suspendedBy` becomes
   `administrator`, so neither the link nor recovery stands it back up.
3. Answer success and change nothing.

*Chosen: 2, the strictest reading.* Readings 1 and 3 leave the departing person able to
reverse what the administrator meant only an administrator to reverse. The link the
deactivation notice carried is kept, so presenting it answers
`identity.account.adminsuspended` as it does for any account an administrator suspended.
The sessions are ended in the transaction as for any suspension, though a suspended
account holds none. `AccountSuspended` is not published, because chapter 10 section 5b
fires it when the "State enters or leaves `suspended`" and the state did not change; the
change is audited as `identity.account.suspended`.

For the reverse, an administrator reactivates only what an administrator suspended; an
account its owner deactivated answers `authz.denied`, since reversing the owner's own
choice is theirs, by the link or by recovery.

*Tests that pin it.*
`AccountAdministrationTests.IDN_LIFE_013_ADeactivatedAccountBecomesTheAdministratorsAsync`,
`AccountAdministrationTests.IDN_LIFE_013_OnlyAnAdministratorsSuspensionIsReactivatedAsync`,
`AccountTests.IDN_LIFE_013_AC1_ReactivationRestoresARestrictionInForce`,
`AccountLifecycleTests.IDN_LIFE_013_AdministrativeSuspensionIsNotReversedByALinkAsync`.

*Chapter text that should change.* IDN-LIFE-013 could say that an administrator's
suspension of a self-deactivated account makes it the administrator's to reverse, and
that an administrator does not reactivate an account its owner deactivated.

---

## 256. What reactivation restores to an account suspended while restricted

**Phase 8 · 2026-09-24 · Tier 3 · IDN-LIFE-013, PRIV-RIGHT-004, IDN-ACCT-007**

*The question.* The account already let an administrator suspend a restricted account,
and IDN-LIFE-013 AC1 has "Reactivation restores prior access exactly". IDN-ACCT-007 has
an account "in exactly one state at any time", so once it was suspended the restriction
was recorded nowhere, and reactivation made every suspended account `active`.

*The readings.*

1. Reactivation makes the account `active`, which lifts the restriction as a side
   effect of the suspension.
2. The account remembers that it was restricted when an administrator suspended it,
   and reactivation makes it `restricted` again.

*Chosen: 2, the strictest reading.* Reading 1 ends a restriction the subject asked for
without the decision PRIV-RIGHT-004 requires to lift it, and it is not "prior access
exactly". The account carries a new column, `restriction_held`, which can be true only
while the account is suspended or in its deletion window (a check constraint pins it).
An administrator's suspension of a restricted account sets it and the reactivation
clears it on the way back to `restricted`. Nothing is sent to the privacy subscribers on
either side: the restriction was never lifted.

*Tests that pin it.*
`AccountTests.IDN_LIFE_013_AC1_ReactivationRestoresARestrictionInForce`,
`AccountStoreTests.IDN_LIFE_013_AC1_TheRowCarriesTheRestrictionToRestoreAsync`,
`AccountAdministrationTests.IDN_LIFE_013_AC1_ReactivationRestoresPriorAccessExactlyAsync`,
`ModelTests` (the list of columns).

*Chapter text that should change.* IDN-LIFE-013 could say that reactivation returns an
account suspended while restricted to `restricted`, and chapter 10 section 5.12b could
name the recorded fact beside `suspendedBy`.

---

## 257. A restriction held while the account is away from the restricted state

**Phase 8 · 2026-09-24 · Tier 3 · PRIV-RIGHT-004, PRIV-RIGHT-002, IDN-LIFE-003, IDN-LIFE-014, chapter 09 section 8a**

*The question.* PRIV-RIGHT-004 AC2 has "Lifting it restores prior behaviour exactly",
and nothing but a decision lifts a restriction. Three paths lost one without a decision:

- a restricted account that entered its deletion window and cancelled it came back
  `active` (IDN-LIFE-014 cancellation restores "the account exactly as it stood");
- a restricted account taken down and reversed came back `active`, which is what chapter
  09 section 8a says of the reversal ("Restores `active`");
- a restriction fulfilled, or granted by lapse (PRIV-RIGHT-002), while the account was
  suspended or in its deletion window was recorded nowhere: the request was fulfilled,
  the subscribers were told nothing, and the account later came back `active`.

*The readings.*

1. Leave the three paths as they were: a restriction is only a state, and a state the
   account leaves is gone.
2. Hold the restriction on the account while it is suspended or deleting, tell the
   subscribers when it is decided, and bring the account back `restricted` from any of
   the three.

*Chosen: 2, the strictest reading.* Reading 1 ends a restriction the subject asked for
without a decision, and in the third path acts on records the company decided not to
act on. The column entry 256 added, `restriction_held`, carries it: entering the
deletion window or a takedown from `restricted` sets it, a restriction decided while the
account is suspended (by either origin) or deleting sets it and writes the
`RestrictionChanged` delivery with `restricted` true in the same transaction, leaving the
window by cancellation or reversal and reactivation return the account to `restricted`,
and the erasure clears it. The reversal of a takedown restores `active` as chapter 09
section 8a says except where a restriction is held, where it restores `restricted`. A
restriction decided twice is recorded and announced once.

*Tests that pin it.*
`AccountTests.PRIV_RIGHT_004_AC2_ARestrictionIsHeldThroughADeletionWindow`,
`AccountTests.PRIV_RIGHT_004_ARestrictionDecidedAwayFromActiveIsHeld`,
`AccountStatesTests.PRIV_RIGHT_004_ARestrictionIsHeldThroughATakedownAsync`,
`PrivacyRequestTests.PRIV_RIGHT_004_ARestrictionFulfilledWhileSuspendedIsHeldAsync`,
`AccountTests.Transitions_FromAStateThatDoesNotMakeThem_Throw` (restriction of a
deleting account still refused by `Restrict`, which is the active path).

*Chapter text that should change.* PRIV-RIGHT-004 could say that a restriction decided
while the account is suspended or deleting is held and in force when it returns, and
chapter 09 section 8a could say that the takedown reversal restores `restricted` where
the account was restricted.

---

## 258. What lifting a restriction answers, writes and asks

**Phase 8 · 2026-09-24 · Tier 2 · PRIV-RIGHT-004, IDN-AUD-001, LIB-API-005, chapter 09 section 8a, chapter 10 section 5a**

*The question.* Chapter 09 section 8a lists `POST /admin/accounts/{subject}/restriction/lift`
under "Accounts: `account:manage`" with "Lifts a processing restriction
(PRIV-RIGHT-004)". Chapter 10 section 5b has `RestrictionChanged` fire when
"`restricted` set or lifted" to every registered subject-event handler, required. Nothing
names the answers or the audit action, and the section's preamble says step-up applies
where an operation "touches another person's account", while chapter 10 section 5a,
"Listed once here so a builder does not have to infer them endpoint by endpoint", names
no gate for the lift and the row marks none.

*Chosen.*

- `IAccounts.LiftRestrictionAsync`, under `account:manage` in the administrative
  organization, as entry 254 has for the section's other account operations.
- **204** where the restriction is lifted; **400** `api.request.malformed` naming
  `subject` where no account bears it; **403** `authz.denied` without the permission,
  where no person acts, or where the account is not restricted (entry 259). A second
  lift is refused, as a second reactivation is.
- No step-up and no reason. The gate is keyed by a name from chapter 10 section 5a, the
  list says it is complete, and the row marks none; taking the preamble would need a
  name no chapter holds. The same holds for the membership end (entry 250), the
  deletion cancellation on the subject's behalf and the account session revocation,
  which also touch another person's account without a gate.
- In one transaction: the account becomes `active`, the `RestrictionChanged` delivery
  with `restricted` false is written to the outbox, and the lift is audited as a new
  action, `privacy.restriction.lifted`, security category, acting subject the
  administrator and effective subject the account, no organization. The privacy request
  that restricted the account is left as it was decided.

*Tests that pin it.*
`AccountAdministrationTests.PRIV_RIGHT_004_AC2_LiftingARestrictionRestoresTheAccountAsync`,
`AccountAdministrationTests.PRIV_RIGHT_004_OnlyARestrictionInForceIsLiftedAsync`,
`AccountDirectoryTests.PRIV_RIGHT_004_AC2_TheLiftTellsTheSubscribersAsync`,
`AccountAdministrationEndpointTests.PRIV_RIGHT_004_AC2_ARestrictionIsLiftedAsync`,
`SessionRequirementTests` (the list of session routes), `AuditActionsTests` (the list of
actions).

*Chapter text that should change.* Chapter 09 section 8a could give the endpoint its
answers and say whether its preamble's "touches another person's account" adds gates
chapter 10 section 5a does not list; chapter 10 could hold the audit row below.

---

## 259. Whether a restriction held away from the restricted state is lifted

**Phase 8 · 2026-09-24 · Tier 3 · PRIV-RIGHT-004, entry 257**

*The question.* Under entry 257 an account suspended or deleting may hold a restriction.
Nothing says whether an administrator may lift it while the account is away.

*The readings.*

1. Lift it: clear what is held and tell the subscribers.
2. Refuse until the account is back in `restricted`, where the lift applies as usual.

*Chosen: 2, the strictest reading.* It keeps the restriction the subject asked for
until it can be lifted from the state that shows it, and it keeps one path for a lift.
The refusal is `authz.denied`, as for an account not restricted at all.

*Tests that pin it.*
`AccountAdministrationTests.PRIV_RIGHT_004_OnlyARestrictionInForceIsLiftedAsync`.

*Chapter text that should change.* PRIV-RIGHT-004 could say that a restriction held
while the account is suspended or deleting is lifted only once the account is back.

---

## 260. What a deletion cancelled on the subject's behalf answers and records

**Phase 8 · 2026-09-24 · Tier 2 · IDN-LIFE-003, IDN-LIFE-014, IDN-AUD-001, chapter 09 section 8a, chapter 10 section 5a**

*The question.* Chapter 09 section 8a has `POST /admin/accounts/{subject}/delete/cancel`
cancel "a pending deletion on the subject's behalf", with **409**
`identity.takedown.active` for a takedown. IDN-LIFE-003 has the cancellation of a window
an out-of-band request began made by "the administrator handling the request ... recorded
against the request". Nothing names the other answers, whether a window the subject
began may be cancelled here, or what "recorded against the request" writes, since the
request's status vocabulary has no value for it.

*Chosen.*

- `IAccounts.CancelDeletionAsync`, under `account:manage` in the administrative
  organization; no step-up (entry 258) and no reason, the endpoint naming no body.
- A window the subject began and one an out-of-band request began are both cancelled:
  the row says "on the subject's behalf" without limiting the origin, and the subject
  whose link is lost has no other way back inside the window.
- **204** where cancelled; **409** `identity.takedown.active` for a takedown, answered
  before the window is looked at, as the link-borne cancellation does; **422**
  `identity.deletion.windowelapsed` where the window has closed; **400**
  `api.request.malformed` naming `subject` where no account bears it; **403**
  `authz.denied` where the account is in no window, without the permission, or where no
  person acts.
- In one transaction: the account comes back as it stood (entry 257), the link a
  self-deletion notice carried is spent, `AccountDeletionCancelled` is published with the
  administrator as actor, and the cancellation is audited as `identity.deletion.cancelled`
  in the security category, acting subject the administrator and effective subject the
  account. "Recorded against the request" is the audit row's `request` detail, naming the
  fulfilled erasure request whose decision began the window (the latest fulfilled erasure
  request of the subject decided no later than the window began). The request itself
  keeps its status: chapter 09 section 8a lists no status for it.

*Tests that pin it.*
`AccountAdministrationTests.IDN_LIFE_003_AnOutOfBandDeletionIsCancelledAgainstItsRequestAsync`,
`AccountAdministrationTests.IDN_LIFE_014_ASelfDeletionIsCancelledOnTheSubjectsBehalfAsync`,
`AccountAdministrationTests.IDN_LIFE_003_ATakedownOrAClosedWindowIsNotCancelledAsync`,
`AccountDirectoryTests.IDN_LIFE_003_TheErasureRequestBehindTheWindowIsFoundAsync`,
`AccountDirectoryTests.IDN_LIFE_003_TheCancellationIsRecordedAgainstTheRequestAsync`,
`AccountAdministrationEndpointTests.IDN_LIFE_003_ADeletionIsCancelledOnTheSubjectsBehalfAsync`,
`SessionRequirementTests` (the list of session routes).

*Chapter text that should change.* Chapter 09 section 8a could give the endpoint its
answers and say which origins it cancels, and IDN-LIFE-003 could say that "recorded
against the request" is the audit row naming it.

---

## 261. What reading an account's photo as an administrator answers and whose policy withholds it

**Phase 8 · 2026-09-24 · Tier 2 · IDN-ATTR-002, IDN-ATTR-003, IDN-AUD-001, chapter 09 sections 6 and 8a**

*The question.* Chapter 09 section 8a lists `GET /admin/accounts/{subject}/photo` under
"Accounts: `account:manage`" with "**404** where none is set or the policy does not
enable photos (D-147)", and section 6 has staff photos read through it "under the same
rule" as the account's own read. Nothing says whose policy is meant, since the
administrator and the account may stand in different organizations, what an unknown
subject answers, or whether the read is audited.

*The readings.*

1. The account's own organizations decide, as for its own read: every organization it
   belongs to must show photos.
2. The administrative organization's key decides.
3. Both must show photos.

*Chosen: 1.* IDN-ATTR-002 makes photos an organization's to show for its members, and
the administrative organization is not the account's; under 1 a photo the account's
policy withholds is withheld from every read, the administrator's included, which is
the rule the account's own read already follows. Reading 3 would add a key the
administrative organization never declared for anyone but its own members.

- `IAccounts.ReadPhotoAsync`, under `account:manage` in the administrative
  organization; no step-up, as chapter 10 section 5a names no gate for it.
- **200** with the stored JPEG as `image/jpeg` and `Cache-Control: no-store`, no `ETag`;
  **404** where the account shows none or its policy withholds photos, alike; **400**
  `api.request.malformed` naming `subject` where no account bears it (entry 254);
  **403** `authz.denied` without the permission or where no person acts.
- Not audited: IDN-AUD-001 records lifecycle events, and a read changes nothing.

*Tests that pin it.*
`AccountAdministrationTests.IDN_ATTR_003_AC3_AnAccountsPhotoIsReadThroughTheGateAsync`,
`AccountAdministrationTests.IDN_ATTR_002_APhotoThePolicyWithholdsIsNotReadAsync`,
`AccountAdministrationEndpointTests.IDN_ATTR_003_AC3_AnAccountsPhotoIsServedToAnAdministratorAsync`,
`SessionRequirementTests` (the list of session routes).

*Chapter text that should change.* Chapter 09 section 8a could say that "the policy" is
that of the account's organizations and give the endpoint its other answers.

---

## 262. How the library obtains the person's token for the mail server

**Phase 8 · 2026-09-24 · Tier 3 · INT-MAIL-010, REG-MAIL-002, AUTH-OIDC-001 AC4, AUTH-OIDC-004, LIB-HOST-001, entries 71 and 215**

*The question.* INT-MAIL-010: app passwords are managed through the mail server's call
"made by the library with a token obtained for the signed-in person through the
library's **first-party OIDC client** for the mail server", AC1 "one call to the mail
server's app-password call carrying the person's token, and no row in the library's
schema". AUTH-OIDC-001 AC4: "the token it obtains is scoped to the signed-in person".
No chapter says how the library, being both the provider and that client, comes by the
token, which client in the registry is the mail server's, what the token carries, or
how long it lasts.

*The readings.*

1. Run the authorization code flow against the library's own provider from inside the
   request, as an outside client would.
2. Issue the token in process from the person's session record, through the provider's
   own token generation, to the client the deployment declares as the mail server's.
3. Sign a token with the provider's key outside the provider's pipeline.

*Chosen: 2, the strictest reading.* Reading 1 needs a redirect back to an address the
library would have to register and serve for itself, which no chapter names. Reading 3
would be a second way of issuing a token beside the provider's, with its own claims.
Under 2:

- The token stands on the session the request came in under, as every token the
  provider issues does (entry 71): a session that is ended, expired or not the
  person's issues nothing (`auth.session.expired`, `authz.denied`), and the server is
  not called.
- It is generated by the provider's own handlers, so it is signed with the key the
  provider signs with now (AUTH-KEY-001) and carries what the provider's access tokens
  carry for that client: the person as `sub`, the client as `client_id` and presenter,
  the scopes the client was registered with, the provider as issuer, and no audience.
  The provider's own validation accepts it.
- It lasts `oidc.accesstoken.lifetime`, or less where the session record ends sooner,
  so it never outlives the record (AUTH-OIDC-004).
- No token entry is written: AC1 allows no row, and the token is used for the one call
  it was issued for.
- Which client is the mail server's is a new host declaration, `MailServerClient`
  (`clientId`), required where a mail server is registered, so a deployment that
  registers one and declares no client is stopped at startup with
  `model.startup.declarationmissing` naming `mailServerClient.clientId`. A declared
  client the registry does not hold as a `protocol` client is a deployment put
  together wrongly and faults; bootstrap registers it (AUTH-OIDC-001 AC4).

Whether the mail server accepts this token is a claim about the product, verified by
the adapter in Milestone 2 step 5 (entry 215).

*Tests that pin it.*
`AppPasswordFlowTests.INT_MAIL_010_AC1_TheServerIsCalledWithThePersonsTokenAsync`,
`AppPasswordsTests.AUTH_OIDC_001_AC4_OnlyThePersonsLiveSessionObtainsATokenAsync`,
`StartupValidationTests.INT_MAIL_010_ADeploymentHostingMailDeclaresTheMailServersClientAsync`,
`ModelTests.REG_MAIL_002_AC1_NoTableHoldsAnAppPassword`.

*Chapter text that should change.* INT-MAIL-010 could say that the token is issued in
process from the session record to the declared client and is not stored, and
LIB-HOST-001 could hold the declaration (row below).

**Superseded by D-164.** Applied in entry 281.

---

## 263. What the app-password endpoints answer and record

**Phase 8 · 2026-09-24 · Tier 2 · REG-MAIL-002, INT-MAIL-006, INT-MAIL-010, IDN-AUD-001, chapter 09 section 6, chapter 10 section 5a**

*The question.* Chapter 09 section 6 gives the three endpoints, "Present only where the
account holds a mailbox (INT-MAIL-006)", **200** for the listing and the creation,
**204** for the revocation and **403** `auth.stepup.required`. Nothing names what an
account without a mailbox is answered, how the label is bounded, what an unknown
identifier answers, or the audit actions.

*Chosen.*

- `IAppPasswords` (`ListAsync`, `CreateAsync`, `RevokeAsync`), and three members on
  `IMailServer` (`AppPasswordsAsync`, `CreateAppPasswordAsync`,
  `RevokeAppPasswordAsync`), each carrying the person's token and acting on the account
  the server finds in it. `AppPassword` (`id`, `label`, `createdAt`, `expiresAt`) and
  `IssuedAppPassword` (`id`, `secret`) are what the server answers.
- Present only where the account is `active` and holds a mailbox not retired, which is
  a mailbox the server is told to enable; otherwise, and where the deployment
  registers no mail server, **403** `authz.denied`, the refusal for an operation the
  account is not in a state for.
- The label is bounded as a credential label is (`CredentialLabel`), **422**
  `auth.credential.labelinvalid` otherwise; an absent label is **400**
  `api.request.malformed` naming `label`. Uniqueness is not checked, since that would
  be a second call to the server (AC1). The expiry is passed as given and the server
  judges it.
- Creation and revocation are the `mailcredential:create` and `mailcredential:revoke`
  gates; the listing asks for none, as chapter 10 section 5a names none for it.
- A revocation of an identifier the server does not hold for the person answers **404**
  `auth.credential.notfound`, which the port's contract asks the server to answer.
  Any other failure of the server is answered as it came.
- Once the server has acted, in one transaction: the security-notice set is sent
  `security-notice`, and the change is audited as `auth.mailcredential.created` or
  `auth.mailcredential.revoked`, security category, the account as both subjects, no
  organization, the detail `credential` naming the server's identifier and nothing the
  person typed. The creation answer carries `Cache-Control: no-store`.

*Tests that pin it.*
`AppPasswordsTests.REG_MAIL_002_AC1_TheServerGeneratesTheSecretAndTheLibraryKeepsNoneAsync`,
`AppPasswordsTests.REG_MAIL_002_AC2_CreationAndRevocationAskForAStepUpAsync`,
`AppPasswordsTests.INT_MAIL_010_AC3_TheRevokedAppPasswordIsGoneAtTheServerAsync`,
`AppPasswordsTests.INT_MAIL_010_AC2_TheListingIsWhatTheServerHoldsNowAsync`,
`AppPasswordsTests.INT_MAIL_006_WithoutAnEnabledMailboxThereAreNoAppPasswordsAsync`,
`AppPasswordsTests.REG_MAIL_002_AnAppPasswordCarriesALabelAsync`,
`AppPasswordFlowTests.INT_MAIL_006_AnAccountWithoutAMailboxIsRefusedAsync`,
`SessionRequirementTests` (the list of session routes), `AuditActionsTests` (the list of
actions).

*Chapter text that should change.* Chapter 09 section 6 could give the endpoints their
other answers and say what "present only" answers; chapter 10 could hold the audit
rows below.

---

## 264. What an erasure's identifier is, what the erasure endpoints read, and what the manual completion records

**Phase 8 · 2026-09-24 · Tier 3 · IDN-LIFE-003a, IDN-LIFE-003b, chapter 09 section 8a, chapter 10 sections 1.4 and 5, entries 168 and 169**

*The question.* Chapter 09 section 8a gives "`GET /admin/erasures` · `GET
/admin/erasures/{id}` | Every incomplete erasure in one query; per-subscriber state for
one (IDN-LIFE-003b): `{ id, subject, reason, status, attempts, subscribers: [ { name,
required, confirmedAt } ] }` (D-153)" and "`POST /admin/erasures/{id}/complete` | The
manual completion path after exhausted retries, itself recorded (IDN-LIFE-003a).
Requires step-up". IDN-LIFE-003b gives the erasures table's columns, none of them an
identifier; the table is keyed by the subject. IDN-LIFE-003a: "A manual completion path
SHALL exist for permanent failure, itself recorded". Chapter 10 gives
`privacy.erasure.notfailed` and `erasure:complete`. No chapter says what `id` names,
what an identifier naming no erasure answers, what the completion answers or records,
or whether the path closes a failed delivery that is not an erasure's.

*The readings.*

1. `id` is the subject, the erasures table's key.
2. `id` is the identifier of the delivery the erasure's host-side work travels on, as
   `takedownId` is the takedown's delivery (entry 168).

And for the manual path: (a) it closes any failed delivery its identifier names; (b) it
closes an erasure's and nothing else.

*Chosen: 2 and (b), the strictest reading.* The shape carries `id` beside `subject`, so
`id` is not the subject. The per-subscriber state the read must show lives on the
delivery, which the erasures row follows step for step: the worker carries the
attempts, the completion and the failure onto the row in the transaction that records
them on the delivery. The path is named for erasures, and widening it to a takedown's
cancellation or a restriction would be more than the chapter grants (entry 169 declined
the same widening for the listing). Under this:

- `IErasures` (`ListAsync`, `ReadAsync`, `CompleteAsync`), `ErasureId` and
  `ErasureProgress`, every operation under `privacyrequest:manage`, **403**
  `authz.denied` otherwise, with nothing read.
- The listing answers **200**, every erasure awaiting subscribers or failed, oldest
  first, read in one query with the confirmations. Each entry, and the read of one,
  carries `{ id, subject, reason, status, attempts, subscribers: [ { name, required,
  confirmedAt } ] }`, one line per registered subject-event subscriber, `confirmedAt`
  absent where it has not confirmed, `reason` and `status` spelled as `10` sections
  5.12a and 5.12 spell them.
- An identifier that names no erasure, including one naming a takedown's or a
  restriction's delivery, answers **404** `privacy.erasure.notfound`, a new code,
  because no existing code says that no erasure is held under an identifier.
- The completion asks the permission, then the step-up `erasure:complete` on the
  caller's session (**403** `auth.stepup.required`), then answers **404** as above, then
  **409** `privacy.erasure.notfailed` where the erasure is awaiting subscribers or
  complete. Otherwise, in one transaction, the delivery and the erasures row are
  completed and the completion is audited as `privacy.erasure.completed`, security
  category, the operator as the acting subject and the erased subject as the effective
  one, details `erasure` (the identifier) and `outstanding` (the required subscribers
  that had not confirmed, by name). It answers **204**.
- A failed delivery of another kind is not closed on this path; it stays failed and its
  alert stands. The worker's own manual completion, which nothing reached, is removed,
  so the manual path exists once.

*Tests that pin it.*
`ErasureServiceTests.IDN_LIFE_003b_AC2_EveryIncompleteErasureIsListedAsync`,
`ErasureServiceTests.IDN_LIFE_003b_OneErasureIsReadWithEachSubscribersConfirmationAsync`,
`ErasureServiceTests.IDN_LIFE_003b_AnIdentifierThatNamesNoErasureIsNotFoundAsync`,
`ErasureServiceTests.IDN_LIFE_003a_AManualCompletionClosesAFailedErasureAndIsRecordedAsync`,
`ErasureServiceTests.IDN_LIFE_003a_AManualCompletionRefusesAnErasureThatNeverFailedAsync`,
`ErasureServiceTests.IDN_LIFE_003a_OnlyAnErasureIsCompletedByHandHereAsync`,
`ErasureServiceTests.IDN_LIFE_003a_TheManualCompletionRequiresStepUpAsync`,
`ErasureServiceTests.IDN_LIFE_003b_PrivacyRequestManageIsRequiredForEveryOperationAsync`,
`OutboxStoreTests.IDN_LIFE_003b_AC2_EveryOutstandingErasureIsReadInOneQueryAsync`,
`OutboxStoreTests.IDN_LIFE_003b_OneDeliveryIsReadWithItsConfirmationsAsync`,
`ErasureEndpointTests.IDN_LIFE_003b_TheOutstandingErasuresAreListedAndReadAsync`,
`ErasureEndpointTests.IDN_LIFE_003a_AFailedErasureIsCompletedByHandAsync`,
`ErasureEndpointTests.IDN_LIFE_003b_TheEndpointsAreRefusedToACustomerAsync`,
`SessionRequirementTests` (the list of session routes), `AuditActionsTests` and
`ErrorCodesTests` (the lists).

*Chapter text that should change.* Chapter 09 section 8a could say that an erasure's
`id` is the identifier of its delivery and give the completion its **204** and its
refusals; IDN-LIFE-003a could say whether a takedown's or a restriction's failed
delivery has a manual path; chapter 10 could hold the rows below.

---

## 265. What the "who can access this?" view reads, whom it answers, and what it answers over HTTP

**Phase 8 · 2026-09-24 · Tier 3 · AUTHZ-DERIVE-007, AUTHZ-GATE-004, chapter 09 section 8, chapter 10 sections 3 and 4.5a, D-161, D-162, entry 196**

*The question.* Chapter 09 section 8 answers `GET /admin/access?resourceType=...&resourceId=...`
with **200**, "who can access this resource, and through which grant or container", and
"Stored and derived grants are reported **distinctly**", with `partial: true` and
`unevaluated` past `authz.reverselookup.budget`. AUTHZ-DERIVE-007 (D-161) says it
"answers stored grants by query, materialised derived grants by query (they are rows),
and unmaterialised derivations by evaluating each declared derivation over the
host-supplied relation for the resource and its ancestors". Chapter 10 section 3 gives
`grant:read` as "Viewing grants and the \"who can access this?\" view". No chapter says:
in which organization `grant:read` is asked; what each grant in the answer carries;
whether a grant that confers nothing (a suspended organization's, a role allowing
nothing) is reported; what `unevaluated` names; or how the HTTP view reaches the
host-supplied relation, since the library holds no host rows and a request carries none.

*The readings.*

1. Over HTTP, answer the stored and materialised grants and mark every unmaterialised
   derivation reaching the record as `unevaluated`, with `partial: true`.
2. Over HTTP, answer only the stored grants, silently.
3. Over HTTP, refuse a record a non-materialised derivation reaches with
   `authz.derivation.sourcesmissing`, as every other path without the host's rows is
   refused (D-162), and answer it in full through the library call the host makes with
   its rows.

*Chosen: 3, the strictest reading.* Reading 2 is the silent partial answer AC2
forbids. Reading 1 widens `partial` and `unevaluated`, which the chapter ties to the
budget alone, to a second meaning, and hands an administrator a list that looks
complete for every host that never mounts its own call. D-162: "No path answers from
stored grants alone." Under this:

- `IAccessGate.WhoCanAccessAsync(context, resource)` and
  `WhoCanAccessAsync<TResource>(context, resource, sources)` answer `ResourceAccess
  { Resource, Grants, Partial, Unevaluated }`, each grant in the `ExplainedGrant` shape
  an explanation carries (AUTHZ-GATE-004): identifier (none for a derived one), kind,
  subject type and identifier, role, deny, and the container it sits on (none where it
  sits on the record itself or on the whole organization).
- The view is `grant:read` in the organization the record sits in, read from the
  registry; `resourceType` `organization` with the organization's identifier asks for
  the whole of it. A caller without it is refused **403** `authz.denied` with the
  refusal recorded as every refusal is; nothing is concealed (AUTHZ-CONCEAL-005). A type
  the host did not declare, or a record the registry does not hold, is **400**
  `api.request.malformed` naming `resourceType` or `resourceId`.
- Without the host's rows, a record a non-materialised derivation reaches is refused
  **500** `authz.derivation.sourcesmissing`; the whole organization, which no derivation
  reaches, and a type no such derivation reaches are answered. `GET /admin/access` has
  no rows to give, so it answers those and refuses the rest; a host wanting the full
  view on a derived type makes the library call with its `FilterSources`.
- Stored grants, materialised ones among them, are read in one query, as the effective
  grants view confers them (entry 196): live, not revoked, of a role that allows
  something, in an organization whose deletion is not requested. They are ordered: on
  the record, then on each container nearest first, then on the whole organization.
- Each non-materialised derivation reaching the record is evaluated over the host's
  relation for the record and each container of the type the relationship is declared
  on, and each holder is reported as a derived grant of the role the derivation
  confers. The bound is `authz.reverselookup.budget`, read on the injected clock across
  all derivations and checked between rows; a derivation it stops contributes nothing
  and its relationship's name is added once to `unevaluated`, with `partial: true`.
- The HTTP answer is `{ resource: { resourceType, resourceId }, grants: [ { id, kind,
  subjectType, subjectId, role, deny, inheritedFrom } ], partial, unevaluated }`.

*Tests that pin it.*
`ReverseLookupTests.AUTHZ_DERIVE_007_AC1_StoredAndDerivedGrantsAreReportedDistinctlyAsync`,
`ReverseLookupTests.AUTHZ_DERIVE_007_AC2_PastTheBudgetTheAnswerIsPartialAndNamesWhatWentUnevaluatedAsync`,
`ReverseLookupTests.AUTHZ_DERIVE_007_WithoutTheHostsRowsARecordADerivationReachesIsRefusedAsync`,
`ReverseLookupTests.AUTHZ_DERIVE_007_TheViewRequiresGrantReadInTheRecordsOrganizationAsync`,
`ReverseLookupTests.AUTHZ_DERIVE_007_AGrantThatConfersNothingIsNotReportedAsync`,
`ReverseLookupTests.AUTHZ_DERIVE_007_AnUnknownRecordOrTypeIsRefusedAsMalformedAsync`,
`MaterialisationTests.AUTHZ_DERIVE_007_AMaterialisedGrantIsReportedAsARowAsync`,
`GrantStoreTests.AUTHZ_DERIVE_007_ASuspendedOrganizationsGrantsAreNotReadOnARecordAsync`,
`AccessEndpointTests.AUTHZ_DERIVE_007_TheViewAnswersTheRecordItsGrantsAndWhatWentUnevaluatedAsync`,
`AccessEndpointTests.AUTHZ_DERIVE_007_ARecordNotNamedIsRefusedAsMalformedAsync`,
`AccessEndpointTests.AUTHZ_DERIVE_007_TheViewIsRefusedWithoutGrantReadAsync`,
`SessionRequirementTests` (the list of session routes).

*Chapter text that should change.* Chapter 09 section 8 could give the response shape
above, the permission and the organization it is asked in, the 400 and 500 refusals,
and say how the HTTP view reaches the host's relation, or that it does not and the
view on a derived type is the host's own call; AUTHZ-DERIVE-007 could say that a grant
conferring nothing is not reported and that `unevaluated` names relationships.

---

## 266. A derivation confers nothing in a suspended organization

**Phase 8 · 2026-09-24 · Tier 3 · IDN-ORG-003 AC1 and AC2, AUTHZ-TEST-001 AC3, AUTHZ-DERIVE-001, entry 196**

*The question.* IDN-ORG-003 gives the stage "Deletion requested | Organization suspends
immediately; access stops". Entry 196 stopped the organization's grants by leaving
them out of `identity.effective_grants`, which also stops a materialised derivation,
whose grants are rows there. A derivation evaluated per request has no row: its clause
reads the host's relation and the ancestry and nothing of the organization, so a
holder of the host's fact kept access to a suspended organization's records.
AUTHZ-TEST-001 AC3: "Where a derivation is materialised, the same cases pass
identically before and after materialisation." No chapter says whether a derived grant
confers while the organization is suspended.

*The readings.*

1. A derivation confers as before; only written and materialised grants stop.
2. A derivation reaching a record of a suspended organization confers nothing, on the
   check, the filter, the fragment, the capability page and the explanation, and
   confers again once the request is cancelled.

*Chosen: 2, the strictest reading.* Reading 1 leaves access running after "access
stops", and makes materialising a derivation change who gets in, which AC3 forbids.
Under this:

- The gate reads the organization's `deletion_requested_at` through a port of its own,
  `IOrganizationSuspensions`, as it reads a restriction (CONV-DESIGN-003), since the
  organization is another area's aggregate.
- It is read where the derivations reaching a type are gathered for one organization,
  once per operation and only where a non-materialised derivation reaches the type, so
  a type no derivation reaches costs no query. While the organization is suspended no
  derivation is gathered, and every path built on them admits nothing through one.
- A path still refuses a type a derivation reaches without the host's rows
  (`authz.derivation.sourcesmissing`, D-162); suspension does not excuse the sources.
- The host's fact is untouched; cancelling the request (AC2) restores the access with
  nothing to rebuild.
- The "who can access this?" view needs `grant:read` in the organization, which a
  suspended organization's grants do not confer, so it is refused there before any
  derivation is evaluated (entry 265).

*Tests that pin it.*
`GateBehaviourTests.IDN_ORG_003_AC1_ASuspendedOrganizationsDerivationsConferNothingAsync`,
`GateBehaviourTests.IDN_ORG_003_AC1_ASuspendedOrganizationConfersNothingAsync` (the
written grants, unchanged).

*Chapter text that should change.* IDN-ORG-003 could say that a suspended
organization's records admit no one through any grant, derived ones included, and that
cancellation restores derived access with the host's facts as they stand.

---

## 267. The audit trail by subject names what the subject did as well as what was done to it

**Phase 8 · 2026-09-24 · Tier 3 · PRIV-BREACH-002, 09 section 8a, 16 sections 3 and 5, D-014, entry 177**

*The question.* 09 section 8a: "`GET /admin/audit?subject=...` | Every audit record for
one subject, without a full scan (PRIV-BREACH-002)". Chapter 16 section 3 step 5: "If
they held `recovery:approve`, review their recent approvals." Section 5, for a hostile
departure: "Review their audit trail for the preceding weeks". D-014: "Audit records
both" the acting and the effective identity. The read built in this phase (entry 177)
answered the records naming the subject as the effective identity only, so an
approval, a grant or any action a person took on someone else's account was not in
their trail, and no other read in 09 reaches it. No chapter says which identity "for
one subject" means.

*The readings.*

1. The records naming the subject as the effective identity only.
2. Every record naming the subject as either identity.

*Chosen: 2, the strictest reading.* Reading 1 leaves the review chapter 16 asks for
without a query, which is the "access that survives departure" and the "data leaving
with them" the procedure exists to catch. No record is disclosed that `audit:read`
could not already read under the other subject. Under this:

- `IAuditStore.FindNamingAsync` reads every record whose acting or effective identity
  is the subject, most recent first, through `ix_audit_records_effective_subject` and
  `ix_audit_records_acting_subject` (the second written by the configuration change
  index migration for OPS-CFG-005, now also declared on the model), with no key read:
  what a record holds under any subject's key never comes back, as entry 177 holds for
  the trail.
- The trail (`IAuditTrail`, `GET /admin/audit?subject=...`) reads through it; the
  entry shape is unchanged. The port's read by effective identity with the personal
  values (`FindBySubjectAsync`) is unchanged.

*Tests that pin it.*
`AuditStoreTests.PRIV_BREACH_002_TheTrailNamesWhatTheSubjectDidToOthersAsync`,
`AuditStoreTests.PRIV_BREACH_002_AC1_TheTrailNamingASubjectEitherWayTakesTheIndexesAsync`,
`AuditStoreTests.PRIV_BREACH_002_AC2_TheTrailReadsTheSameBeforeAndAfterErasureAsync`,
`AuditRecordTests.PRIV_RET_002_AC1_TheAuditPortOffersNoWriteButAnAppend` (the port's
methods).

*Chapter text that should change.* 09 section 8a could say "every audit record naming
one subject, as the acting or the effective identity".

---

## 268. The grants one user or group holds in its own name are read under `grant:read`

**Phase 8 · 2026-09-24 · Tier 2 · AUTHZ-GRANT-003 AC3, 09 section 8, 10 `grant:read`, 16 section 3 step 4**

*The question.* Chapter 16 section 3 step 4, "Remove or transfer their grants", reads:
"Anything granted directly to them rather than through a group." AUTHZ-GRANT-003 AC3:
"\"Who granted this and when\" is answerable by query." Chapter 10 gives `grant:read`
as "Viewing grants and the \"who can access this?\" view". Chapter 09 section 8 names
`POST /admin/grants` and `DELETE /admin/grants/{id}` and no read of grants, and the
revocation needs the grant's identifier, which only its creation returned. The "who
can access this?" view (entry 265) answers per record, so the grants one person holds
across an organization could be found only record by record, and an organization-wide
grant only by asking for the organization.

*The readings.*

1. No listing: 09 names none; the operator finds a person's grants through the "who can
   access this?" view, record by record.
2. `GET /admin/grants?organization=...&subjectType=user|group&subjectId=...` and
   `IGrants.HeldAsync`, under `grant:read` in that organization: the live grants the
   user or group holds in its own name there, oldest first, each in the shape a grant
   is written in, with its identifier, kind, who granted it, when, why and its expiry.

*Chosen: 2.* Reading 1 leaves step 4 without a way to know what to remove, and a
revocation without the identifier it needs. Reading 2 is the smallest surface that
serves both, under the permission 10 already names for viewing grants. Under it:

- It is read per organization, as a grant is scoped (AUTHZ-SCOPE-001), and the
  permission is asked there; `grant:read` elsewhere, or `grant:manage` alone, is
  `authz.denied` (AUTHZ-CONCEAL-005).
- It lists grants naming the holder itself; a grant reaching a user through a group is
  read under the group, which is the step's "rather than through a group".
- Revoked and expired grants are left out, as the gate leaves them out; the full history
  of a grant stays in the audit trail (entry 267).
- An organization-wide grant reads as `resourceType` `organization` with the
  organization's identifier, as it is written (AUTHZ-GRANT-001 AC2).
- A materialised grant is listed with `kind` `materialised`, so the one a revocation
  refuses as `authz.grant.notfound` is told apart; the host's data answers for it.
- A malformed `organization`, `subjectType` or `subjectId` is `api.request.malformed`
  naming the member (API-CONV-002).
- Nothing is written, so it is not audited, as the other reads under `grant:read` are
  not.

*Tests that pin it.*
`GrantEndpointTests.AUTHZ_GRANT_003_AC3_WhoGrantedWhatAHolderHoldsAndWhenIsReadAsync`,
`GrantEndpointTests.AUTHZ_GRANT_003_AC3_AGroupsAndAMaterialisedGrantAreReadWithTheirKindAsync`,
`GrantEndpointTests.AUTHZ_GRANT_003_AC3_ReadingWhatAHolderHoldsNeedsGrantReadThereAsync`,
`GrantEndpointTests.API_CONV_002_AReadOfHeldGrantsNamesWhoseAndWhereAsync`,
`SessionRequirementTests` (the route).

*Chapter text that should change.* 09 section 8 could add
`GET /admin/grants?organization=...&subjectType=...&subjectId=...` with its response
shape and `grant:read`, and 16 section 3 step 4 could name it.

---

## 269. When and how the relay registration warning is raised

**Phase 8 · 2026-09-24 · Tier 2 · INT-MAIL-011 AC1 to AC3, 10 section 4 `notification.email.relayregistered`, AUTH-STEP-002a, D-162**

*The question.* INT-MAIL-011: "configuration validation SHALL surface, at startup and
when the setting changes, a sending domain that is not declared as registered while
Continue with Apple is enabled." Its values paragraph: "When `apple` is in any effective
`loginFactors` and the sending domain is not in that set, startup raises the Normal
condition `relay-domain-unregistered` with `details.domain`." AC2: "Changing the sending
domain to an undeclared one produces the same warning at the point of change." No
chapter says which changes count as "the setting changes", whether the warning stops a
start or a change, what happens when it cannot be raised, or how two spellings of one
domain compare.

*The readings.*

1. Raise at startup and on a change of `notification.email.sendingdomain` only, the one
   AC2 names.
2. Raise at startup and on a change of any of the three values the condition reads:
   the sending domain, `notification.email.relayregistered`, and the system policy
   (`policy.default`), whose `loginFactors` decides whether Apple is a way in.

*Chosen: 2.* Withdrawing the declaration or enabling Apple over an undeclared domain
leaves the same silent failure AC2 exists to surface; reading 1 lets either pass
unwarned until the next restart. Under it:

- "Any effective `loginFactors`" is read as the system policy's: an organization's
  override can only narrow it (AUTH-STEP-002a, refused by the policy strictness check
  otherwise), so Apple in any effective policy is Apple in the system's.
- Whether Continue with Apple is a way in is read from a property of the catalogue
  entry, `RelaysAddress` (the identity it asserts may carry a relay address,
  REG-IDENT-008), and not from the entry's name: AUTH-FACT-001 AC1 says "No
  conditional anywhere in the library tests for a factor by name". The property is
  internal and Apple is the one entry carrying it.
- A domain in the declaration matches the sending domain in any case, since a domain
  name is one name whatever its case (RFC 4343); nothing else is normalised.
- The warning is raised under the domain (`relay-domain-unregistered:<domain>`), so
  OPS-ALERT-002 deduplicates it per domain across restarts and changes.
- The warning stops nothing: the deployment starts and the change is made, as a
  "warning" and a Normal condition say.
- A warning that cannot be raised does stop them, the strictest reading of D-162: at
  startup it is a `StartupException` carrying the failure (an unset sending domain is
  `model.startup.declarationmissing`), and at a change the change is refused and
  nothing is written, as any publication inside a transaction is.
- The start check runs among the library's hosted checks, after the others, and
  reads the database as they do.

*Tests that pin it.*
`RelayRegistrationTests.INT_MAIL_011_AC1_AnUndeclaredSendingDomainIsNamedInTheWarningAsync`,
`RelayRegistrationTests.CheckAsync_TheDomainDeclaredInAnotherCase_RaisesNothingAsync`,
`RelayRegistrationTests.CheckAsync_AppleIsNoWayIn_RaisesNothingAsync`,
`RelayRegistrationTests.CheckAsync_NoSendingDomainNamed_IsRefusedAsUndeclaredAsync`,
`FactorCatalogueTests.INT_MAIL_011_TheOneEntryWhoseAddressMayBeARelayIsApple`,
`FactorCatalogueTests.AUTH_FACT_001_AC1_NoConditionalTestsForAFactorByName`,
`StartupValidationTests.INT_MAIL_011_AC1_AnUndeclaredSendingDomainWarnsAsTheDeploymentStartsAsync`,
`ConfigurationAdministrationTests.INT_MAIL_011_AC2_ChangingTheSendingDomainToAnUndeclaredOneWarnsAsync`,
`ConfigurationAdministrationTests.INT_MAIL_011_AC2_EveryChangeThatLeavesTheDomainUndeclaredWarnsAsync`,
`ConfigurationAdministrationTests.INT_MAIL_011_AC3_DeclaringTheDomainIsAConfigurationChangeAsync`,
`ConfigurationAdministrationTests.ChangeAsync_AWarningThatIsNotTaken_IsRefusedAndNotWrittenDownAsync`.

*Chapter text that should change.* INT-MAIL-011 could name the three values whose
change is checked, say that the warning stops neither a start nor a change, and say
that domains compare without regard to case.

---

## 270. The shipped provider register is four rows; the developer row is the host's

**Corrections 3 · 2026-09-24 · Tier 2 · PRIV-ROPA-002, LIB-HOST-001 Recipients, 05 section 6, D-165, D-029**

*The question.* Chapter 05 section 6 tabulates five rows: mail server, SMS gateway,
hosting provider, password screening and "Developer | Processor | All stored data |
n/a | No", and says "These rows are the processors the library itself makes true and
are shipped as defaults, each applied only while the integration it describes is
configured". LIB-HOST-001 Recipients says "the library ships as the default set only
the rows its own processing makes true (mail server, SMS gateway, hosting provider,
password screening; `05` section 6, D-153, D-162)". D-165 says "the four library-true
rows" and, under "Kept on purpose", "the recipient row for the developer
relationship (D-029)". The corrections-3 instruction says the shipped defaults are
"exactly the four library-true rows (mail server, SMS gateway, hosting provider,
password screening), nothing else".

*The readings.*

1. Ship five rows, the developer row among them, as 05 section 6's table lists.
2. Ship the four rows LIB-HOST-001 and D-165 name; the developer relationship is a
   row the host declares through `recipients`, and D-165's "kept on purpose" keeps the
   row in the chapter as the example of one.

*Chosen: 2.* Three sources name four and the owner's instruction says "nothing else".
The developer relationship is the deployment's contract (D-029), not something the
library's own processing makes true, and a shipped row the library cannot make true is
a register entry that may be false. Under it:

- `ProviderRegister.Default` is the four rows in 05 section 6's order.
- The SMS gateway row is applied to every deployment, as the hosting provider is:
  `ISmsTransport` is a required registration (the sending service takes it), so the
  integration it describes is always configured.
- The unread second copy of the list (`Recipients`, `Recipient`, `RecipientLocation`)
  is removed rather than kept beside the register.

*Tests that pin it.*
`ProcessingRecordsTests.PRIV_ROPA_002_TheShippedRegisterIsTheFourRowsTheLibraryMakesTrue`,
`ProcessingRecordsTests.INT_GEN_004_AC1_AProviderAddedWithoutAnAgreementReferenceIsFlaggedAsync`,
`ProcessingRecordsTests.PRIV_ROPA_002_TheRowsTheLibraryMakesTrueAreAppliedWithoutADeclarationAsync`.

*Chapter text that should change.* 05 section 6 could drop the Developer row from the
shipped table, or mark it as a row the host declares, and say that the SMS gateway row
is always applied because an SMS transport is a required registration.

---

## 271. What the D-165 word search covers

**Corrections 3 · 2026-09-24 · Tier 2 · D-165 code consequences, 08 CONV-NAME-001**

*The question.* The corrections-3 instruction says "A repository-wide search for those
words must come back empty outside docs/", the words being payment provider, courier,
order, cart, product, checkout and cash on delivery. D-165's code consequences say "no
fixture, test name, sample configuration or comment names a payment or shipping
provider, an order, a cart, a courier or cash on delivery". A literal search for
"order", "product" and "checkout" also finds words that are not business: sort order
and ordering in prose, `ORDER BY`, the provider framework's handler `Order` constants,
the lawful basis key `court-judgment-or-order` (10), the product name of CONV-NAME-001
and the changelog lines about it, and the pinned platform action `actions/checkout` in
the pipeline.

*The readings.*

1. Remove every literal occurrence, renaming the framework constants, the lawful-basis
   key, CONV-NAME-001's vocabulary and the pipeline action.
2. Remove every occurrence in the business sense D-165 names; keep the non-business
   senses, which are fixed by a framework, by chapter 10, by CONV-NAME-001 or by the
   platform.

*Chosen: 2.* D-165 states the rule in the business sense ("names ... an order"), and
reading 1 would break the OpenIddict handler contract, change a chapter 10 key and
rename the pipeline's pinned action. The business senses are gone from code, tests,
fixtures, comments, the changelog and NOTICE; the search script that checks it lists
the non-business senses it passes, and the corrections-3 report lists them.

*Tests that pin it.* None; verified by the search recorded in the corrections-3
report.

*Chapter text that should change.* None in the chapters; the instruction's search
could name the business sense.

---

## 272. What carries `[NeverLogged]`

**Corrections 3 · 2026-09-24 · Tier 2 · CONV-LOG-003 AC1, CONV-CODE-008 JAN0002**

*The question.* CONV-LOG-003 lists what "SHALL NEVER be logged": "passwords, tokens,
session identifiers, TOTP secrets or codes, recovery codes, verification codes, the
hash prefix sent for password screening", "any field of a resource type the host
declares sensitive", "the body of any endpoint the host marks `SensitiveBody`", and
"the content of consent or notice text in any language". JAN0002 reports "A logging
call whose argument is a type or member marked as never-logged". No chapter says
which declarations carry the marker, and C# admits no attribute on a local variable.

*The readings.*

1. Mark the types whose whole value is forbidden, and nothing else.
2. Mark those types, every property or field of another type that holds a forbidden
   value, and every parameter through which one passes as a plain string or bytes.

*Chosen: 2*, the strictest reading. A password is carried as a `string` far more
often than as a `Password`, and the rule catches only what it can see marked. Under
it:

- Types: `Password`, `PasswordHash`, `OpaqueToken`, `TotpMaterial`, `TotpEnrolment`,
  `VerificationCode`, `RecoveryCodeEntry`, `PreparedRecoveryCodes`, `SigningMaterial`,
  `SessionId`, `GeneratedRecoveryCodes`, `KeyEncryptionKeys`, `SignOnSecret`,
  `RecoveryCodesView`.
- Members: every request member carrying a password, code, link token or invitation
  token; every answer member carrying a code, secret, token or `otpauth` address; the
  secret columns of the rows (hashes, tokens, codes, the client secret, the signing
  key, the stored token payload, the staged password); the text of a notice and of
  its translations.
- Session identifiers are read to include the fingerprints a session, a
  pre-authentication record or a link is found by, which identify it as surely as
  the secret does.
- Parameters: every `string`, `byte[]` or `ReadOnlyMemory<byte>` parameter carrying one
  of those values, in the public service contracts and in their implementations
  alike, since the rule reads the implementation's parameter and not the contract's.
- Not marked: locals, which C# does not allow; spans, which cannot reach a logging
  call; wrapped data keys, which are ciphertext; the send reference, which CONV-LOG-003
  does not list. A host's own sensitive types and `SensitiveBody` endpoints are the
  host's to mark; the library enforces the latter at runtime (BFF-LOG-002).

*Tests that pin it.*
`NeverLoggedValueAnalyzerTests.CONV_LOG_003_AC1_TheLibrarysOwnCarriersAreReportedAsync`,
`NeverLoggedValueAnalyzerTests.CONV_LOG_003_AC1_TheVersionAndTheIdentifiersBesideThemAreNotReportedAsync`,
`Janus.Authentication.Tests.NeverLoggedTests` (two tests),
`Janus.Hosting.Tests.NeverLoggedTests` (two tests),
`Janus.Storage.Tests.NeverLoggedTests.CONV_LOG_003_AC1_EveryColumnCarryingAForbiddenValueIsMarked`.

*Chapter text that should change.* CONV-LOG-003 or CONV-CODE-008 could say which
declarations carry the marker (types, members and parameters), that session
identifiers include the fingerprints they are found by, and that locals cannot carry
it.

---

## 273. Where the required keys are checked, and when the register counts as generated

**Corrections 3 · 2026-09-24 · Tier 2 · LIB-HOST-001 AC2 and AC4, 10 section 4, PRIV-ROPA-001**

*The question.* LIB-HOST-001 AC2: "Omitting any produces a named startup error
identifying which". Chapter 10 section 4: "`hosting.environment`, required only when
the records-of-processing generator is used (PRIV-ROPA-001)", and "startup **fails**
with a named error if any is unset (the conditional one, when its condition holds)".
`Settings.ThrowIfIncomplete` implements the rule, and nothing called it. No chapter
says where among the startup checks it runs, how a deployment is known to use the
generator, or what a named value that cannot be read does.

*The readings.*

1. The generator is used when a request for the register arrives, so the key is read
   and refused then, and startup never requires it.
2. The generator is part of every deployment of the library, which serves the
   register at `GET /admin/privacy/records` and cannot be told not to, so startup
   requires the key.

*Chosen: 2*, the reading that refuses. Under reading 1 an incomplete register would be
found by the regulator's request rather than by the operator's start. Under it:

- The check is a startup check of its own, `SettingsValidationService`, registered
  directly after the schema check and before every other, so an unnamed key is
  reported by its own name and code (`model.startup.governinglanguage` for the
  governing language, `model.startup.declarationmissing` with `details.key`
  otherwise) and never by whichever later check happens to read it.
- A key counts as named when the deployment wrote a row for it. A row that exists and
  cannot be read stops the start as well, carrying the read's own failure.
- The conditions are read from the values in force: `hosting.location` where it is
  named, and `password.blocklist.source` and `password.blocklist.sources`, which have
  defaults.

*Tests that pin it.*
`StartupValidationTests.LIB_HOST_001_AC3_ADeploymentNamingOnlyTheListedKeysStartsAsync`,
`StartupValidationTests.LIB_HOST_001_AC4_ADeploymentWithoutItsGoverningLanguageIsRefusedAsync`,
`StartupValidationTests.LIB_HOST_001_AC2_AnUnnamedKeyIsRefusedByNameBeforeTheServerStartsAsync`,
`StartupValidationTests.LIB_HOST_001_AC2_AConditionalKeyIsRefusedOnceItsConditionHoldsAsync`,
and the `StartupConfigurationTests` of `Settings.ThrowIfIncomplete`.

*Chapter text that should change.* Chapter 10 section 4 could say that the register
is generated by every deployment, making `hosting.environment` required outright, or
name how a deployment declines the generator.

---

## 274. How a sensitive body is kept out of request logging

**Corrections 3 · 2026-09-24 · Tier 2 · BFF-LOG-002, CONV-LOG-003, LIB-HOST-001**

*The question.* BFF-LOG-002: "Such an endpoint is one the host marks with the
`SensitiveBody` endpoint metadata (LIB-HOST-001); the library has no knowledge of what
the body holds. Body logging is off for a marked endpoint whatever the setting." The
chapters name the metadata and nothing else: not which assembly carries it, whether
the library's own endpoints carry it, or what happens to a body the logging meets
before the endpoint is known.

*The readings.*

1. The mark is the host's alone, so only the endpoints the host marks are covered, and
   a body logged ahead of routing is logged, since no mark is known yet.
2. The mark is the host's to place and the library's to honour everywhere: the
   library marks every endpoint it maps, and a request whose endpoint is not known
   when the logging reads it is treated as marked.

*Chosen: 2*, the reading that logs least. Under it:

- `SensitiveBodyAttribute` is public in `Janus.Core`, beside `NeverLoggedAttribute`,
  so a host puts it on an endpoint as an attribute or as endpoint metadata without a
  second vocabulary. It carries nothing.
- The library registers an `IHttpLoggingInterceptor` with `AddJanus`. It runs after
  the framework has applied the deployment's fields and any per-endpoint setting, and
  turns off request and response bodies where the endpoint carries the mark or is
  not known. Body logging stays wholly the host's to turn on; the library turns
  nothing on.
- Every endpoint `MapIdentityEndpoints` maps carries the mark, since each body holds
  a credential or a person's data.
- A host's own interceptor registered after the library's runs after it and could
  turn a body back on. That is host code choosing to log the body, not a setting,
  and is outside what the library can refuse.
- The endpoint metadata the interceptor reads makes it a third reader of endpoint
  metadata in `Bff/`; the gates of BFF-CSRF-001 AC2 and AUTH-SESS-007 AC2 list it, and
  it enforces no token and admits nothing.

*Tests that pin it.*
`SensitiveBodyLoggingTests.BFF_LOG_002_AC1_BodyLoggingIsOffByDefault`,
`SensitiveBodyLoggingTests.BFF_LOG_002_AC2_NoLogEntryHoldsAFieldOfAMarkedEndpointsBodyAsync`,
`SensitiveBodyLoggingTests.BFF_LOG_002_AC1_EveryEndpointTheLibraryMapsIsMarked`,
`SensitiveBodyLoggingTests.BFF_LOG_002_AC1_ABodyMetBeforeRoutingIsNotLoggedAsync`,
`BrowserProfileTests.BFF_CSRF_001_AC2_NoEndpointCanBeExcludedByConfigurationOrAttribute`,
`BrowserProfileTests.AUTH_SESS_007_AC2_NoEndpointCanOptOut`.

*Chapter text that should change.* Chapter 07's "Sensitive-body endpoints" row could
name the type (`SensitiveBodyAttribute`, in `Janus.Core`) and say that the library's
own endpoints carry it and that an unknown endpoint counts as marked; chapter 17
BFF-LOG-002 could say the same.

---

## 275. What the browser profile does with a processor's cross-site post return

**Corrections 3 · 2026-09-24 · Tier 3 · BFF-CSRF-005 AC4, BFF-CSRF-002**

*The question.* BFF-CSRF-005 AC4: "A cross-site POST return to a host route carrying
no session cookie is handled as the host's GET continuation, not as a lost session;
the library documents the pattern." The prose: "where one posts, the host's return
route must be a GET that then continues, rather than relying on the session being
present on the POST itself." BFF-CSRF-002: "Reject where `Sec-Fetch-Site` is
`cross-site` and the request is state-changing", AC1 "A cross-site POST is rejected
before reaching any endpoint." A post the processor makes reaches the resource
isolation stage first and is refused there, so no host route can turn it into a GET
unless the host puts its own code ahead of the stages. The criterion touches a gate,
so the strictest reading is taken.

*The readings.*

1. The library refuses the post as today and documents that the host writes its own
   middleware, ahead of the browser profile, that answers its return route with a
   redirect.
2. The resource isolation stage itself answers such a post 303 with its own address,
   so the browser reads that address as a top-level navigation and the lax cookie
   goes with it; nothing of the post is carried.

*Chosen: 2*, in its narrowest form. Reading 1 asks every host to write code that
meets cross-site posts ahead of every gate the library has, which is the hazard the
stages exist to remove. Under reading 2:

- Only a `POST` whose `Sec-Fetch-Site` is `cross-site`, `Sec-Fetch-Mode` is
  `navigate` and `Sec-Fetch-Dest` is `document`, and which carries no session cookie,
  is answered 303. A post that carries the session, one that does not navigate the
  page, one that loads into a frame, and one without fetch metadata are refused
  exactly as before with `session.csrf.invalid`.
- The `Location` is the request's own encoded path and query, relative, so the answer
  is never a redirect elsewhere. The body is not read and not forwarded.
- It applies to every route of the browser profile, the library's included; a library
  route that only accepts a post answers the read with 405, and no state changes.
- The answer is recorded at Information under event 13 of the browser profile's log,
  with the correlation identifier only.
- BFF-CSRF-002 AC1 still holds: the post is not carried and reaches no endpoint.
- The pattern is documented on `UseBrowserProfile`: the host's route there is a GET
  that asks the processor for the outcome.

*Tests that pin it.*
`BrowserProfileTests.BFF_CSRF_005_AC4_ACrossSitePostReturnContinuesAsTheHostsGetAsync`,
`BrowserProfileTests.BFF_CSRF_002_AC1_ACrossSitePostIsRejectedBeforeAnyEndpointAsync`.

*Chapter text that should change.* Chapter 17 BFF-CSRF-005 could state the rule the
stage applies (the three header values, no session cookie, 303 to the same address),
and BFF-CSRF-002 could name it as the one cross-site post answered other than by
refusal.

---

## 276. The seam a host mounts its own callbacks on

**Corrections 3 · 2026-09-24 · Tier 2, Tier 3 where marked · BFF-MACH-001, BFF-MACH-002, BFF-MACH-003, INT-GEN-003, BFF-OWN-001, CONV-DESIGN-005**

*The question.* D-165 and 05 INT-GEN-003: "The library ships these controls as a
host-mountable machine-profile pipeline (BFF-MACH-002, BFF-MACH-003): signature
verification, correlation references, the rate limit and source restriction", and "A
host mounts each of its own providers' callbacks on the same pipeline". No chapter
names the mounting call, how a host states its provider's scheme, where the secret is
read, how an event is claimed and what happens to a claim the host's route fails, what
an unsigned callback is confirmed through, or what status a rejection answers.

*The readings.*

1. Hand the host the checks as separate middleware to arrange around its own routes.
2. One mount per callback, where the callback is a contract the host implements to
   declare its provider's scheme, and the library runs every check in a fixed order
   before the host's route.

*Chosen: 2.* Reading 1 leaves the presence and order of each check to every host,
which is what BFF-OWN-001 refuses for the browser profile. Under it:

- `UseCallback(PathString, ISignedCallback)` and `UseCallback(PathString,
  IUnsignedCallback)` mount a callback before `UseBrowserProfile`, at a path the host
  chooses (09 section 10, "under paths of its choosing"), matched without regard to
  case in `PipelineProfiles` only.
- The branch runs the machine profile's stages, so a request carrying a session cookie
  is refused (BFF-MACH-001 AC2), then the callback's checks, then the host's route. It
  marks the request with an internal feature the browser profile passes untouched; no
  host code can set it, so no browser endpoint reaches the machine profile by
  configuration (BFF-MACH-001 AC1).
- The checks, in order: `integration.callback.ratelimit` per source, before anything
  is read; the provider's published ranges where declared, an IPv4-mapped address
  compared as IPv4; then, signed, a signature present, the five-minute window where
  the scheme carries an instant, and verification; or, unsigned, a reference issued
  for that callback, then the host's confirmation.
- Tier 3. Signed: the host declares the hash of the provider's keyed-hash scheme
  (`Algorithm`), where the signatures and the signed bytes are (`Presented`), the
  secrets (`ReadSecretsAsync`) and the event identifier (`EventOf`). The library
  computes the HMAC itself, compares every presented signature in fixed time against
  the current secret and, for 24 hours after `CurrentSince`, the previous one,
  whichever matches, and zeroes what it computed. A hash the platform cannot compute
  fails at mount; secrets that cannot be read verify nothing. Only shared-secret
  schemes are covered, which is what "Secret: from the secrets manager, rotatable with
  an overlap window" describes.
- Tier 3. Idempotency: the SHA-256 of the event identifier is claimed per callback in
  `callback_events`, in one insert that does nothing on conflict, before the route
  runs. A delivery of an event already claimed is answered 200 without reaching the
  route and recorded at Information. A delivery the route does not answer with a 2xx,
  or that throws, gives its claim back in a scope of its own under a token the
  request's abandonment does not cancel, so the provider's retry is carried. A
  verified delivery carrying no identifier is refused.
- Unsigned: a reference is issued through the public `ICallbackReferences`: 128 random
  bits, base64url, kept as its SHA-256 in `callback_references`, so the lookup is by
  hash and nothing secret is compared in variable time. The route is reached only once
  the host's `ConfirmAsync` succeeds. No claim is made, since the route acts only on
  what the provider's API confirmed.
- Every refusal is recorded against its source in the callbacks ledger (INT-GEN-003
  AC3), counts toward `alerting.callback.threshold`, and is logged at Warning with the
  callback's name, the check and the correlation identifier.
- Tier 3. `integration.callback.rejected` answers 429 for every cause, the status 05
  and 10 give it for the rate limit, where it answered 422 before. `ApiStatus` gives a
  code one status, and one answer for a forged reference and a flood tells the sender
  nothing of which it was. The rate-limited refusal carries `retryAt`, so the answer
  carries `Retry-After`.
- Every method of both contracts returns `Result` or `Result<T>` (CONV-DESIGN-005),
  and a failure refuses the delivery. `BFF_OWN_001_AC1` now holds the two profile
  mounts to the builder alone and `UseCallback` to the builder, a path and a callback
  contract, with nothing that could turn a check off.
- The public types are under `Janus.Hosting.Callbacks` as mounting types
  (CONV-LAYOUT-002); admission, references and the ledgers are under
  `Janus.Authentication.Callbacks`, where `ICallbackLedger` moved from `Sending`
  (CONV-DESIGN-001 AC2).
- Neither new table has a retention: a claimed event and an issued reference stay.

*Tests that pin it.*
`HostCallbackTests.BFF_MACH_002_AC1_AnUnsignedOrMisSignedCallbackIsRejectedBeforeParsingAsync`,
`HostCallbackTests.BFF_MACH_002_AC2_AReplayOutsideTheWindowIsRejectedAsync`,
`HostCallbackTests.BFF_MACH_002_AC3_ADuplicateEventIdentifierIsProcessedOnceAsync`,
`HostCallbackTests.BFF_MACH_002_AC3_ADeliveryTheRouteFailedIsCarriedAgainAsync`,
`HostCallbackTests.BFF_MACH_002_AC3_ADeliveryCarryingNoEventIdentifierIsRefusedAsync`,
`HostCallbackTests.BFF_MACH_002_AC4_ComparisonIsConstantTime`,
`HostCallbackTests.BFF_MACH_002_TheSecretReplacedVerifiesFor24HoursAsync`,
`HostCallbackTests.BFF_MACH_002_SecretsTheManagerCannotGiveVerifyNothingAsync`,
`HostCallbackTests.BFF_MACH_002_AnAlgorithmThePlatformCannotComputeFailsWhenMounted`,
`HostCallbackTests.INT_GEN_003_AFloodIsAnsweredBeforeAnyLookupAsync`,
`HostCallbackTests.INT_GEN_003_ACallbackFromOutsideThePublishedRangesIsRejectedAsync`,
`HostCallbackTests.INT_GEN_003_AReferenceIs128RandomBitsKeptByItsHashAsync`,
`HostCallbackTests.BFF_MACH_003_AC1_NoUnsignedCallbackAdvancesStateWithoutConfirmationAsync`,
`HostCallbackTests.BFF_MACH_003_AC2_AForgedCallbackWithAGuessedReferenceIsRejectedAndLoggedAsync`,
`HostCallbackTests.BFF_MACH_003_AC3_RepeatedVerificationFailuresRaiseAnAlertAsync`,
`HostCallbackTests.BFF_MACH_001_AC2_AHostCallbackCarryingASessionCookieIsRefusedAsync`,
`CallbackContractTests.CONV_DESIGN_005_AC1_EveryCallbackContractMethodReturnsAnOutcome`,
`CallbackContractTests.CONV_DESIGN_005_AC2_NoCallbackContractReturnsNull`,
`BrowserProfileTests.BFF_MACH_001_AC3_ARequestTheMachineProfileGovernsPassesTheBrowserProfileAsync`,
`BrowserProfileTests.BFF_OWN_001_AC1_MountingTakesNoSecurityRelevantConfiguration`,
`CallbackStoreTests.BFF_MACH_002_AC3_AnEventIsClaimedOnceAsync`,
`CallbackStoreTests.BFF_MACH_002_AC3_AnEventGivenBackIsClaimedAgainAsync`,
`CallbackStoreTests.INT_GEN_003_AReferenceIsHeldForItsCallbackOnlyAsync`,
`CallbackStoreTests.INT_GEN_003_TheTablesHoldHashesAndTimesAndNothingElseAsync`.

*Chapter text that should change.* 17 BFF-MACH-002 could name the mount, limit the
item to shared-secret keyed hashes, and state what a failed route does to a claim and
what a repeated delivery is answered; BFF-MACH-003 could name the reference issuer and
the confirmation a host supplies; 10 section 1 could give `integration.callback.rejected`
429 for every cause; 04 or 06 could give `callback_events` and `callback_references` a
retention.

---

## 277. A report of delivery is held to a live send

**Corrections 3 · 2026-09-24 · Tier 3 · INT-GEN-003 AC1, 09 section 10 AC1, INT-SMS-005, AUTH-ABUSE-007**

*The question.* INT-GEN-003 AC1: "A callback with a guessed reference is rejected." 09
section 10 AC1: "A forged callback with a guessed reference is rejected and logged."
INT-SMS-005 gives a report of failed delivery one effect and says nothing of a report
of delivery. Since phase 4 a report of delivery was taken and did nothing whatever its
reference, so a guessed reference reporting delivery was answered as a genuine one
and never counted toward `alerting.callback.threshold`. The endpoint answers on the
wire for the first time in this run. The item touches rejection and alerting, so the
strictest reading is taken.

*The readings.*

1. A report of delivery changes nothing, so its reference is not looked up and it is
   always taken.
2. Every report is held to a live send, whatever it says: a report of delivery is
   checked against the send and changes nothing of it; one whose reference no send
   holds is rejected, recorded against its source and counted, as a failure report
   with a guessed reference already was.

*Chosen: 2.* Both criteria name a callback with a guessed reference without regard to
what it reports. Under it:

- `ISendLedger.HoldsAsync(reference)` answers whether a send the transport took under
  that reference is still held, reading the same `sends` row a release removes. It
  writes nothing.
- A genuine report of delivery that arrives after its send settled is refused. A send
  is held for as long as any bucket it counted against decides anything, which is
  hours, and a gateway reports within minutes.

*Tests that pin it.*
`DeliveryReportsTests.INT_GEN_003_AC1_ACallbackWithAGuessedReferenceIsRejectedAsync`
(both reports), `DeliveryReportsTests.INT_SMS_005_AC1_AForgedReportVerifiesNoPhoneAsync`,
`DeliveryReportsTests.INT_GEN_003_AC2_ACallbackAdvancesNoStateOfItsOwnAsync`,
`SendLedgerTests.HoldsAsync_ASendCounted_IsHeldUntilItIsReleasedAsync`.

*Chapter text that should change.* 05 INT-SMS-005 could say that a report of delivery
is checked against the send it names and is rejected where no send holds its
reference.

---

## 278. How the delivery report is read and what it answers

**Corrections 3 · 2026-09-24 · Tier 2 · INT-SMS-005, 09 section 10, LIB-EXT-001, CONV-DESIGN-005, BFF-MACH-001**

*The question.* D-165 keeps `GET /callbacks/sms/dlr`, and INT-SMS-005 says the
gateway "calls over plain HTTP with parameters in the query string". No chapter names
those parameters, which are each gateway's own, nor what a report taken is answered.

*The readings.*

1. The library fixes the parameter names, and a host whose gateway uses others adapts
   them in front of the endpoint.
2. The transport the host registers reads the report, since it is the one component
   that knows the gateway's scheme.

*Chosen: 2.* Reading 1 writes one gateway's scheme into the library, which LIB-EXT-001
AC3 keeps out, and puts host code ahead of the machine profile. Under it:

- `ISmsTransport.ReadReport(parameters)` returns `Result<SmsDeliveryReport>`, the
  reference the send was given and whether it was delivered; a failure is a report
  that cannot be read (CONV-DESIGN-005). Every transport implements it.
- The endpoint passes the query string one value a name; a name the gateway repeated
  is not passed.
- A report that cannot be read is rejected as one carrying a guessed reference is:
  429 `integration.callback.rejected`, recorded against its source and counted toward
  the alert.
- A report taken answers 200 with no body.
- The path is one of the machine profile's routes, so a request carrying the browser
  session cookie is refused 403 (BFF-MACH-001 AC2) and the browser profile never runs
  for it.
- `ISmsTransport` is a required registration (entry 270), so the endpoint's transport
  is always resolvable when the host's endpoints are built.

*Tests that pin it.*
`DeliveryReportEndpointTests.BFF_MACH_001_AC3_TheDeliveryReportIsCarriedOnTheMachineProfileAsync`,
`DeliveryReportEndpointTests.INT_GEN_003_AC1_AForgedOrUnreadableReportIsRejectedAsync`,
`DeliveryReportEndpointTests.BFF_MACH_001_AC2_ADeliveryReportCarryingASessionCookieIsRefusedAsync`,
`SensitiveBodyLoggingTests.BFF_LOG_002_AC1_EveryEndpointTheLibraryMapsIsMarked`,
`ResultContractTests.CONV_DESIGN_005_AC1_EveryContractMethodReturnsAnOutcome`.

*Chapter text that should change.* 05 INT-SMS-005 could say that the registered
transport reads the report; 09 section 10 could give the answer to a report taken (200,
no body) and the status of a refused one.

---

## 279. When a pushed request is spent, and where its destination is judged

**Corrections 3 · 2026-09-24 · Tier 2 · AUTH-OIDC-006 AC2, API-REDIR-001, 09 section 9, BFF-SESS-006**

*The question.* AUTH-OIDC-006 AC2: "the `request_uri` is single use and expires in 60
seconds." The protocol server the library is built on spends a reference only when it
issues a code against it, so a reference answered with `login_required`, or one that
forwarded the browser to sign in, could be presented again for the rest of its minute.
Separately, API-REDIR-001 replaces a destination that is not the registered one rather
than refusing it, and every authorization request now reaches `/oidc/authorize` as a
reference whose parameters were read at the push.

*The readings.*

1. A reference is spent when a code is issued against it; any other answer leaves it
   presentable until it lapses.
2. A reference is spent by the first answer it is given, whatever that answer is.

For the destination:

1. It is judged at the push, where the parameters are read, and the replacement of
   API-REDIR-001 applies there.
2. A push naming another destination is refused, since RFC 9126 has the server
   validate it at the push.

*Chosen: 2, and 1 for the destination.* "Single use" says nothing of the answer, and
a copy of a reference that can still be presented is what the criterion exists to
prevent; the strictest reading is taken. For the destination, API-REDIR-001 AC1 is the
chapters' rule and the push is where the parameters are now read; replacing still
sends the code nowhere but the registered destination. Under it:

- The server spends the reference when it issues a code. Every other answer of
  `/oidc/authorize` spends it too: a refusal as the answer is applied, a browser
  forwarded to sign in before it is forwarded. A reference presented a second time is
  refused 400 and forwards nowhere.
- The reference lapses 60 seconds after issue; the push answers `expires_in: 60`.
- The pushed request is kept as a row of `oidc_tokens` taken before anyone is known,
  so the row's `subject` is nullable (migration `AllowPushedRequestTokens`). A subject
  that names no account of this deployment is still refused where it arrives.
- The destination check runs on the push only. At `/oidc/authorize` the parameters are
  the ones the push kept, so nothing there can name another destination.
- Every registered client holds the permission to push. The browser application's
  own sign-on pushes on the same back channel and with the same secret as its
  exchange; a push the provider refuses answers `session.expired`, forwards nowhere
  and is logged (`BrowserProfileLog` event 14).

*Tests that pin it.*
`ProviderConformanceTests.AUTH_OIDC_006_AC2_ADirectAuthorizationRequestIsRefusedAsync`,
`ProviderConformanceTests.AUTH_OIDC_006_AC2_AReferenceIsTakenOnceAsync`,
`ProviderConformanceTests.AUTH_OIDC_006_AC2_AReferenceAnsweredWithoutACodeIsSpentAsync`,
`ProviderConformanceTests.AUTH_OIDC_006_AC2_AReferenceLapsesAfterSixtySecondsAsync`,
`ProviderConformanceTests.AUTH_OIDC_006_AC4_TheDocumentRequiresPushedRequestsAsync`,
`OidcFlowTests.API_REDIR_001_AC1_AnUnknownDestinationIsReplacedAndLoggedAsync`,
`SignOnTests.AUTH_OIDC_006_AC2_TheBrowserCarriesOnlyThePushedReferenceAsync`,
`SignOnTests.AUTH_OIDC_006_AC2_ARequestThePushRefusesIsNotForwardedAsync`,
`OidcStoreTests.AUTH_OIDC_006_AC2_APushedRequestIsKeptNamingNobodyAsync`.

*Chapter text that should change.* 02 AUTH-OIDC-006 could say that a `request_uri` is
spent by the first answer it is given; 09 section 9 could say that API-REDIR-001's
replacement applies at `POST /oidc/par`.

---

## 280. What the provider's conformance suite is and what it asserts

**Corrections 3 · 2026-09-24 · Tier 2 · AUTH-OIDC-006 AC1, CONV-TEST-002, LIB-TEST-001, API-REDIR-001**

*The question.* AUTH-OIDC-006 AC1: "A conformance suite asserts each refusal named
above and the exact-match rule." CONV-TEST-002 gives "conformance" as the kind that
runs against a host's own configuration and ships per LIB-TEST-001, whose package is
built in phase 10 and whose criteria name the model and the permission checks only.
The item names the implicit, password and plain-PKCE forms and public clients without
saying which requests each of them covers.

*The readings.*

1. The suite is a suite of the library's own tests over its provider, written now.
2. The suite is part of the `Janus.Conformance` package a host runs, written in phase
   10 with that package.

*Chosen: 1, and phase 10 carries the same assertions into the package.* The criterion
is D-164's and belongs to this correction; the behaviour it asserts is set by the
library's configuration of its provider, which the tests exercise through the real
`AddJanus`. So that a host can also prove it against its own deployment, phase 10's
host-run suite runs the same refusals. Each named form is read at its widest:

- Implicit: every response type but `code`, the hybrid ones included, is refused at the
  push with `unsupported_response_type`.
- Password: that grant and every grant but the code and the refresh (client
  credentials, device code, token exchange) is refused with `unsupported_grant_type`.
- Plain PKCE: the plain method and a challenge that names no method, which RFC 7636
  reads as plain, are refused with `invalid_request`, and so is a request with no
  proof key. The discovery document lists S256 alone.
- Exact match: a destination differing from the registered one in any character
  (trailing slash, query, path, case of the host, scheme, port, suffix) never receives
  the code, which API-REDIR-001 sends to the registered one; and a code is exchanged
  only by naming its destination exactly.
- Public clients: none exists, since a client that does not authenticate is refused
  at the push and at the exchange with `invalid_client`; the one kind that
  authenticates and holds nothing, a browser application's own layer, is handed no
  refresh token.

*Tests that pin it.*
`ProviderConformanceTests.AUTH_OIDC_006_AC1_TheImplicitFormsAreRefusedAsync`,
`ProviderConformanceTests.AUTH_OIDC_006_AC1_EveryOtherGrantIsRefusedAsync`,
`ProviderConformanceTests.AUTH_OIDC_006_AC1_OnlyTheS256ProofKeyIsTakenAsync`,
`ProviderConformanceTests.AUTH_OIDC_006_AC1_OnlyTheExactRegisteredDestinationReceivesTheCodeAsync`,
`ProviderConformanceTests.AUTH_OIDC_006_AC1_ACodeIsNotExchangedForAnotherDestinationAsync`,
`ProviderConformanceTests.AUTH_OIDC_006_AC1_NoPublicClientReceivesARefreshTokenAsync`,
`ProviderConformanceTests.AUTH_OIDC_006_AC1_TheDocumentNamesOnlyWhatIsAdmittedAsync`.

*Chapter text that should change.* 02 AUTH-OIDC-006 AC1 could say whether the suite is
the library's own or part of LIB-TEST-001's package; 07 LIB-TEST-001 could name the
provider's refusals among what the host-run suite verifies.

---

## 281. An access token's audience, and the adapter that verifies it

**Corrections 3 · 2026-09-24 · Tier 3 · AUTH-OIDC-006 AC3, INT-MAIL-004, INT-MAIL-010, entry 215**

*The question.* AUTH-OIDC-006 AC3: "Every access token carries `typ: at+jwt` and the
seven claims; a token whose `aud` is not the mail server's client identifier is
rejected by the adapter." No chapter says what `aud` names for a client other than the
mail server's. The mail server's adapter is not built in Milestone 1 (entry 215), so
there is no adapter in the library to reject anything.

*The readings.*

1. `aud` names the client the token was issued to, for every client; the adapter's
   half of the criterion waits for the adapter.
2. `aud` names the client the token was issued to, for every client; the adapter's
   half is proved now by a verifier configured as the adapter must be, against what
   the provider publishes, and the adapter built in Milestone 2 is held to the same
   test.
3. `aud` names a resource the deployment declares, one per relying party.

*Chosen: 2, the strictest reading.* Reading 3 adds a declaration no chapter has.
Reading 1 leaves a criterion untested. Under 2:

- Every access token the token endpoint issues, on a code and on a refresh, names the
  client it was issued to as `aud`, beside `client_id`. The token issued to the mail
  server's client for app passwords (INT-MAIL-010) does the same.
- The verifier the tests use reads the published key set and the issuer from the
  discovery document, and requires the type `at+jwt`, the issuer, the audience of the
  mail server's client and the lifetime on the deployment's clock. It takes the mail
  server's token and refuses a token issued to a browser application's own layer and
  an identity token issued to the mail server's own client.
- The adapter built in Milestone 2 verifies the same things; its test is this one run
  against it.

*Tests that pin it.*
`ProviderConformanceTests.AUTH_OIDC_006_AC3_EveryAccessTokenIsTypedAndCarriesTheSevenClaimsAsync`,
`ProviderConformanceTests.AUTH_OIDC_006_AC3_ARefreshedAccessTokenIsTypedAndCarriesTheSevenClaimsAsync`,
`ProviderConformanceTests.AUTH_OIDC_006_AC3_TheAdapterRefusesATokenForAnotherAudienceAsync`,
`AppPasswordFlowTests.AUTH_OIDC_006_AC3_TheMailServersTokenIsOneItsAdapterTakesAsync`.

*Chapter text that should change.* 02 AUTH-OIDC-006 could say that `aud` is the client
identifier the token was issued to; 05 INT-MAIL-004 could list what the adapter
verifies (signature, `typ`, issuer, audience, lifetime).

---

## 282. The provider event endpoint beside chapter 09's one callback

**Corrections 3 · 2026-09-24 · Tier 3 · IDN-LIFE-012a AC3, 09 section 10, BFF-MACH-001, INT-GEN-003**

*The question.* 09 section 10: "The library defines one callback endpoint of its own",
and its table lists `GET /callbacks/sms/dlr` alone. IDN-LIFE-012a AC3: "The endpoint
`POST /callbacks/providers/{provider}` is on the machine profile (BFF-MACH-001) and
rate-limited like every callback." The two chapters contradict each other on what the
library defines. No chapter says what the endpoint answers, where the event sits in
the request, or which values `{provider}` takes.

*The readings.*

1. Build nothing until 09 names the endpoint.
2. Build the endpoint IDN-LIFE-012a names, as the owner's corrections-3 order says,
   and leave 09 to be brought into line.

*Chosen: 2.* The owner's order names the endpoint and the item. Under it:

- The library maps `POST /callbacks/providers/google` and `POST
  /callbacks/providers/apple`, one route for each factor the catalogue marks as a
  social provider; a path naming anything else is not the library's. Both are on the
  machine profile's exact list.
- Each delivery is counted against `integration.callback.ratelimit` before anything is
  read, as INT-GEN-003 has it. No source is refused for where it is: neither provider
  publishes the ranges its deliveries come from.
- Google delivers the Security Event Token as the request's body (RFC 8935) and is
  answered 202; Apple posts `{"payload": "<token>"}` and is answered 200. Every
  refusal is `integration.callback.rejected`, 429, as every rejected callback is
  (entry 276), and counts towards the callback alert.

*Tests that pin it.*
`ProviderEventTests.IDN_LIFE_012a_AC3_TheEndpointIsOnTheMachineProfileAsync`,
`ProviderEventTests.IDN_LIFE_012a_AC3_TheEndpointIsRateLimitedLikeEveryCallbackAsync`,
`ProviderEventTests.IDN_LIFE_012a_AC1_ASignedCompromiseEndsEverySessionAndHoldsTheCredentialAsync`,
`ProviderEventTests.IDN_LIFE_012a_AC2_AWithdrawnIdentityIsUnlinkedAsync`.

*Chapter text that should change.* 09 section 10 could say the library defines two
callback endpoints of its own and list `POST /callbacks/providers/{provider}` (source:
Google, Apple; never does: act on an event its provider's keys do not verify), with
`{provider}` one of `google` and `apple` and the answers 202 and 200.

---

## 283. What a deployment declares of a social provider, and how its keys are read

**Corrections 3 · 2026-09-24 · Tier 3 · IDN-LIFE-012a, LIB-HOST-001, INT-GEN-003**

*The question.* IDN-LIFE-012a: "Every event is verified against the provider's
published keys". No chapter says where the library learns the address of those keys,
the issuer an event names, or the audience it is addressed to, which is the
deployment's own client identifier at the provider and nothing the library can know.
LIB-HOST-001 has no row for a social provider.

*The readings.*

1. A fixed list of the providers' public addresses inside the library, with the client
   identifiers in a configuration key.
2. A host declaration per provider: the address of the provider's document naming its
   issuer and key set, and the deployment's client identifiers there; optional, with
   no default.

*Chosen: 2, the strictest reading.* Reading 1 puts a vendor's address in library code
and holds the audience in runtime configuration an administrator can change. Under 2:

- `SocialProvider(Factor Provider, Uri Metadata, IReadOnlyList<string> ClientIds)` is a
  public declaration registered once per provider. It is optional: a deployment that
  declares none takes no provider event, and an event of an undeclared provider is
  refused and counted as rejected, since nothing it holds could verify it.
- A declaration that could verify nothing stops the deployment at startup with
  `model.startup.declarationmissing`: `details.key` is `socialProvider.provider` for a
  provider declared twice or a factor that is not a social provider,
  `socialProvider.metadata` for an address that is not absolute HTTPS, and
  `socialProvider.clientIds` for no client or an empty one.
- The document and the key set are read over HTTPS only, on the framework's client
  `identity-providers`, and held between events by the configuration manager of the
  token library the provider stack already carries; nothing an event carries names
  where keys are read from. An event naming a key the set does not hold is refused and
  the set is asked for again, for the provider to deliver again. A set that cannot be
  read refuses every event and is logged at error.
- The signature is RS256 only; the issuer is the document's; the audience is one of
  the declared clients. An event's lifetime is judged where it states one; a security
  event states none and is not refused for that.

*Tests that pin it.*
`StartupValidationTests.IDN_LIFE_012a_ASocialProviderDeclaredShortOfWholeIsRefusedAsync`,
`ProviderEventTests.IDN_LIFE_012a_AnEventNothingDeclaredOrReadableVerifiesIsRefusedAsync`,
`ProviderEventTests.IDN_LIFE_012a_AC1_AnUnsignedEventChangesNothingAndIsAuditedAsRejectedAsync`.

*Chapter text that should change.* 07 LIB-HOST-001 could list `SocialProvider` as an
optional host declaration with the startup refusals above; 10 could carry its row.

---

## 284. How a provider's event finds the account it concerns

**Corrections 3 · 2026-09-24 · Tier 2 · IDN-LIFE-012a, IDN-LIFE-012, PRIV-RIGHT-005c, REG-ACCT-001**

*The question.* A provider's event names the identity by the provider's own subject
identifier (`sub`), never by the account. No chapter says where the library holds that
identifier for a linked identity, and social linking itself is built in phase 10.

*The readings.*

1. Hold the provider's subject in plain on the linked credential.
2. Hold it as the keyed fingerprint every other searchable value is held as
   (PRIV-RIGHT-005c), on the linked credential, unique per provider, and neutralised
   at erasure with the rest.

*Chosen: 2.* The identifier is the person's at the provider and is only ever looked up,
never shown. Under it:

- `authenticators.provider_subject` holds the 32-byte fingerprint of the provider's
  subject under the fingerprint key. A check constraint holds it on a Google or Apple
  credential and nowhere else; a unique index on the factor and the fingerprint, which
  leaves out the neutralised value, keeps one account to an identity.
- `IAuthenticatorStore.LinkAsync` records a linked identity with its subject and
  `ByProviderAsync` finds it; phase 10's linking calls the first.
- Erasure neutralises the fingerprint in the transaction that neutralises the others,
  so the provider's events find the erased account no longer and the identity can be
  linked afresh.
- The column is a field of the Credentials group of REG-ACCT-001 ("provider links").

*Tests that pin it.*
`AuthenticatorStoreTests.IDN_LIFE_012a_ALinkedIdentityIsFoundByTheProvidersSubjectAsync`,
`AuthenticatorStoreTests.IDN_LIFE_012a_AProvidersSubjectIsLinkedOnceAsync`,
`AuthenticatorStoreTests.IDN_LIFE_012a_OnlyALinkedIdentityHoldsAProvidersSubjectAsync`,
`SubjectEraserTests.IDN_LIFE_012a_TheProvidersSubjectOfALinkedIdentityGoesWithItsHolderAsync`,
`ModelTests.REG_ACCT_001_AC2_NoFieldExistsOutsideTheGroupsTheTableNames`.

*Chapter text that should change.* 01 IDN-LIFE-012 could say the provider's subject
identifier is held as a keyed fingerprint on the linked credential; 04 PRIV-RIGHT-005c
could list it among the fingerprints erasure neutralises.

---

## 285. Which provider events do what

**Corrections 3 · 2026-09-24 · Tier 3 · IDN-LIFE-012a**

*The question.* IDN-LIFE-012a names outcomes, not event types: "compromised, disabled,
or its sessions revoked"; "consent revoked or account deleted"; "an email change or
disable". AC1 names `sessions-revoked` and `account-disabled`, AC2 `consent-revoked`
and `account-delete`. Google's catalogue also carries `tokens-revoked`,
`token-revoked`, `account-credential-change-required`, `account-enabled` and
`verification`; Apple's carries `email-enabled` and spells deletion `account-delete`.
Neither provider sends an event for an email change.

*The readings.*

1. Act on the four types the criteria name and record every other.
2. Act on every type whose meaning falls under an outcome the item names, and record
   every other.

*Chosen: 2, the strictest reading.* Under it:

- Ends every session and holds the credential: Google `sessions-revoked`,
  `account-disabled` (whatever its reason), `account-credential-change-required` (the
  provider suspects a compromise) and `tokens-revoked` (the grant behind the sign-in is
  gone).
- Unlinks, or suspends where it is the last way in (entry 286): Apple
  `consent-revoked`, `account-delete` and `account-deleted`.
- Drops the address to unverified: Apple `email-disabled`, naming the address.
- Recorded and changes nothing: `token-revoked`, `account-enabled`, `verification`,
  `email-enabled` and any type the library does not know. `account-enabled` does not
  lift a hold; only a sign-in by another factor does (entry 288).
- An event naming no identity (`verification`, a revoked token) is acknowledged and
  recorded nowhere, as is one naming an identity no account links.
- "An email change" has no event in either provider's catalogue, so nothing answers
  it.

*Tests that pin it.*
`ProviderEventTests.IDN_LIFE_012a_AC1_ASignedCompromiseEndsEverySessionAndHoldsTheCredentialAsync`,
`ProviderEventTests.IDN_LIFE_012a_AC2_AWithdrawnIdentityIsUnlinkedAsync`,
`ProviderEventTests.IDN_LIFE_012a_AnAddressTheProviderStoppedForwardingToDropsToUnverifiedAsync`,
`ProviderEventTests.IDN_LIFE_012a_AnEventThatChangesNothingIsRecordedAndAcknowledgedAsync`.

*Chapter text that should change.* 01 IDN-LIFE-012a could name the event types for
each outcome and drop "an email change", or name the event it means.

---

## 286. A withdrawn identity that is the last way in

**Corrections 3 · 2026-09-24 · Tier 3 · IDN-LIFE-012a AC2, IDN-LIFE-012 AC3, IDN-LIFE-013, AUTH-SESS-010, REG-ACCT-001**

*The question.* IDN-LIFE-012a: "IDN-LIFE-012 AC3 still refuses to remove the last
credential, in which case the account is `suspended` with a security notice to the
security-notice set". No chapter says what "the last credential" counts, who the
suspension is recorded as made by (IDN-LIFE-013: `self` or `administrator`), what
happens to an account already suspended or in its deletion window, or whether the
credential stays.

*The readings.*

1. The last credential is the last row of any kind; the suspension is the owner's.
2. The last credential is the last way in: nothing else the account holds may begin a
   sign-in (REG-ACCT-001, "at least one primary sign-in method"). The suspension is an
   administrator's, since only an administrator stands it back up.

*Chosen: 2, the strictest reading.* A recovery code or a generator cannot sign a person
in alone, and a suspension its owner could lift by the deactivation link would hand
the account back to whoever holds the owner's mail. Under it:

- The credential is the last where, without it, the password and every other usable
  credential the account holds include no factor that may begin a sign-in.
- Where it is not the last, it is removed and the security-notice set is told.
- Where it is, it stays, and the account is suspended as an administrator suspends it:
  `suspendedBy` is `administrator`, every session ends in the same transaction
  (AUTH-SESS-010), `AccountSuspended` is announced with that origin, and the
  security-notice set is told. An account its owner deactivated is taken over and
  announces nothing, since its state does not change; one an administrator suspended
  stays as it is; one in its deletion window or erased is left to that.

*Tests that pin it.*
`ProviderEventTests.IDN_LIFE_012a_AC2_AWithdrawnIdentityIsUnlinkedAsync`,
`ProviderEventTests.IDN_LIFE_012a_AC2_AWithdrawnLastCredentialSuspendsTheAccountWithANoticeAsync`.

*Chapter text that should change.* 01 IDN-LIFE-012a could say that the last credential
is the last way to begin a sign-in and that the suspension is recorded as the
administrator's.

---

## 287. A disabled address that is the personal email a membership keeps

**Corrections 3 · 2026-09-24 · Tier 3 · IDN-LIFE-012a, REG-MAIL-001 AC5, REG-MAIL-003 AC3**

*The question.* IDN-LIFE-012a: "on an email change or disable, the provider-verified
identifier SHALL drop to unverified". REG-MAIL-001 AC5 and REG-MAIL-003 AC3 hold the
personal email a membership keeps verified for as long as the membership lasts, and
the database refuses a kept email that is not verified. The two contradict each other
where the provider's address is the one the membership keeps. No chapter says what
becomes of the primary role an unverified address held.

*The readings.*

1. Drop it anyway and release it from the membership.
2. Leave the kept personal email verified; drop every other.

*Chosen: 2, the strictest reading.* It keeps most: a member keeps the verified address
the end of the membership falls back on. Under it:

- The verified email at the address the event names drops to unverified, unless it is
  the kept personal email, which stays as it is and the event is recorded.
- Where the dropped address was the primary email, the role passes to the earliest
  verified email that may hold it, and stays vacant where none may.

*Tests that pin it.*
`IdentifierSetTests.IDN_LIFE_012a_AnUnvouchedAddressDropsToUnverifiedAndHandsThePrimaryOn`,
`IdentifierStoreTests.IDN_LIFE_012a_AnUnvouchedAddressDropsToUnverifiedAsync`,
`ProviderEventTests.IDN_LIFE_012a_AnAddressTheProviderStoppedForwardingToDropsToUnverifiedAsync`.

*Chapter text that should change.* 01 IDN-LIFE-012a could except the personal email a
membership keeps and say where the primary role goes.

---

## 288. How a credential is held until the person signs in by another factor

**Corrections 3 · 2026-09-24 · Tier 2 · IDN-LIFE-012a AC1, AUTH-RECOV-007, AUTH-SESS-001**

*The question.* IDN-LIFE-012a: "the linked credential SHALL be `suspended` until the
person signs in by another factor". `suspended` is also the state a reported loss puts
a credential in, which is invalidated at the end of a window (AUTH-RECOV-007). No
chapter says how the two are told apart, what counts as signing in by another factor,
or whether the restoration is recorded.

*The readings.*

1. A new credential state.
2. The existing `suspended` state with no instant at which it is invalidated, which a
   reported loss always carries.

*Chosen: 2.* It adds no value to a vocabulary chapter 10 holds. Under it:

- The provider's event suspends the linked credential with no `invalidates_at`; the
  sweep that invalidates a lost credential never reaches it.
- Every session that begins for the account, on factors that do not include the held
  credential's, restores it in the transaction the session begins in, and the
  restoration is recorded as `auth.credential.restored` against the credential. A
  credential suspended for a reported loss is not restored.

*Tests that pin it.*
`SessionServiceTests.IDN_LIFE_012a_AHeldCredentialStandsAgainAtASignInByAnotherFactorAsync`,
`ProviderEventTests.IDN_LIFE_012a_AHeldCredentialStandsAgainOnceThePersonSignsInByAnotherFactorAsync`,
`AuthenticatorStoreTests.IDN_LIFE_012a_AHeldCredentialReadsBackHeldAsync`.

*Chapter text that should change.* 01 IDN-LIFE-012a could say the credential is
suspended with no invalidation instant and restored at the first session begun on
another factor; 10 could list `auth.credential.restored`.

---

## 289. How a provider's event is carried once and audited

**Corrections 3 · 2026-09-24 · Tier 2 · IDN-LIFE-012a AC1, IDN-AUD-001, INT-GEN-003, CONV-LOG-003**

*The question.* IDN-LIFE-012a: every event "is idempotent by its `jti`, and is
audited"; AC1: "an unsigned or replayed event changes nothing and is audited as
rejected". No chapter names the audit actions or their details, says whose trail an
unsigned event is recorded in, or what a replay is answered.

*The readings.*

1. Audit only what verified, and answer a replay as a refusal.
2. Audit what verified and what did not against the account whose linked identity it
   names, and acknowledge a replay so the provider stops delivering it.

*Chosen: 2.* AC1 asks for the unsigned event to be audited, and a refused replay would
be delivered again. Under it:

- The `jti` is claimed under the provider's own callback name (`providers/google`,
  `providers/apple`) in the ledger every callback claims its event in, in the
  transaction the event's work runs in, so an event whose work fails is neither
  claimed nor half done.
- A carried event is recorded as `auth.providerevent.taken`; a replayed one as
  `auth.providerevent.rejected`, answered as a carried one is. An event the keys do not
  verify is recorded as `auth.providerevent.rejected` against the account whose linked
  identity its unverified claims name, where they name one, and refused. Both are
  security records with `credential`, `event` (the type as the provider spells it)
  and `outcome`: `sessionsEnded`, `credentialUnlinked`, `accountSuspended`,
  `addressUnverified`, `recorded`, `unsigned` or `replayed`. The provider's subject is
  never recorded or logged.
- An event about no linked identity is claimed and acknowledged and recorded nowhere.

*Tests that pin it.*
`ProviderEventTests.IDN_LIFE_012a_AC1_AnUnsignedEventChangesNothingAndIsAuditedAsRejectedAsync`,
`ProviderEventTests.IDN_LIFE_012a_AC1_AReplayedEventChangesNothingAndIsAuditedAsRejectedAsync`,
`ProviderEventTests.IDN_LIFE_012a_AnEventThatChangesNothingIsRecordedAndAcknowledgedAsync`,
`AuditActionsTests` (the catalogue).

*Chapter text that should change.* 10 could list the three audit actions and the
`outcome` vocabulary.

---

## 290. How a raised condition reaches the alert channels

**Phase 9 · 2026-09-24 · Tier 3 · OPS-ALERT-001, OPS-ALERT-002, CONV-DESIGN-002**

*The question.* OPS-ALERT-001: "The following conditions SHALL raise alerts." Chapter 10
section 5b gives `AlertRaised` one consumer, "Alert channels", and CONV-DESIGN-002
orders every operation "**commit**, **publish** (after commit, the in-process events of
LIB-API-001, raised from the committed outbox row)". The library published each
condition to the host's `IEvents` and nothing else; only a change of the alert
destinations was routed to a destination. No chapter says whether the channels send
inside the operation that raised the condition or after it commits. It is Tier 3
because it decides when a security alert, the break-glass use among them, leaves.

*The readings.*

1. Route the condition to the destinations inside the raising operation.
2. Write the condition as a row in the raising transaction, and carry it to the
   destinations by a pass of the alert channels after commit.

*Chosen: 2.* CONV-DESIGN-002 puts what is raised after commit and reads it from the
committed row; the first reading sends an alert for an operation that then rolls back,
and builds the router, and with it the whole sending pipeline, inside every operation
that can raise. Under it:

- Every raise site calls the alert channels, which write the row (`raised_alerts`)
  and publish `AlertRaised` to the host inside the raising transaction; a host that
  refuses the event fails the raise and nothing commits, as entry 121 has every
  publication.
- A pass takes the oldest 100 rows and, one transaction each, routes the row through
  the alert router (OPS-ALERT-002 deduplication, the owner's destinations) and removes
  it. A row the router refuses stays, and the pass stops there so the order holds.
- "Immediately" (OPS-BOOT-002 AC3) is the next pass of the alert job.
- The row holds the structured details the alert carries, and nothing more, until it
  is carried.
- The pass assumes one worker; a second pass reaching the same row is caught by the
  router's deduplication ledger, whose key refuses the second delivery.

*Tests that pin it.*
`AlertChannelsTests.OPS_ALERT_001_AC1_ARaisedConditionWaitsForTheChannelsAsync`,
`AlertChannelsTests.CONV_DESIGN_005_AC1_AnEventTheHostRefusedFailsTheRaiseAsync`,
`AlertDispatchTests.OPS_ALERT_001_AC1_ARaisedConditionIsCarriedOnceAsync`,
`AlertDispatchTests.OPS_ALERT_001_AConditionTheRouterRefusedWaitsAsync`,
`RaisedAlertsTests.OPS_ALERT_001_AC1_ARaisedConditionReadsBackAsItWasRaisedAsync`,
`RaisedAlertsTests.OPS_ALERT_001_AC1_ACarriedConditionIsRemovedAsync`.

*Chapter text that should change.* OPS-ALERT-001 could say that a condition is carried
after the transaction that raised it commits, and chapter 10 section 5b that the alert
channels read it from the committed row.

---

## 291. Generating the break-glass credential raises the break-glass alert

**Phase 9 · 2026-09-24 · Tier 3 · OPS-BOOT-004 AC2, OPS-ALERT-001, OPS-ALERT-002**

*The question.* OPS-BOOT-004 AC2: "Generation is audited and alerted, to the owner
regardless of `alerting.owner.enabled` (OPS-BOOT-002)." OPS-ALERT-001 has a row for
"Break-glass credential used" and none for generation, and chapter 10 section 5.23 has
no identifier for it. OPS-ALERT-002 deduplicates on the identifier and what it fired
about.

*The readings.*

1. Raise nothing on generation, since no row names it.
2. Raise `breakglass-used` on generation as well as on use, told apart by the details.
3. Raise a new condition for generation.

*Chosen: 2.* AC2 requires the alert and the third reading adds an identifier chapter
10 does not hold. `breakglass-used` is the one condition the router always sends to
the owner (OPS-ALERT-004), which is what AC2 asks. Under it:

- The details carry `event`: `generated` or `used`.
- The deduplication scope is `generated:<issue>` or `used:<issue>`, so the use of an
  issue is never folded into the alert its generation raised inside the window, and
  each issue alerts once for each.

*Tests that pin it.*
`BreakGlassEndpointTests.OPS_BOOT_004_AC2_GenerationIsAuditedAndReachesTheOwnerAsync`,
`BreakGlassEndpointTests.OPS_BOOT_002_AC3_UseReachesTheOwnerWithOwnerAlertsOffAsync`.

*Chapter text that should change.* OPS-ALERT-001's break-glass row could read "used or
generated", and chapter 10 section 5.23 could name the `event` detail.

---

## 292. Every attempt at the break-glass credential counts against the global limit

**Phase 9 · 2026-09-24 · Tier 3 · OPS-BOOT-004 AC7, AUTH-ABUSE-001**

*The question.* OPS-BOOT-004: attempts "SHALL be source-throttled per AUTH-ABUSE-001
and, in addition, limited to at most 5 attempts per hour globally across all sources.
With 128 bits behind it, the global limit exists to make the attack loud, not to make
it infeasible." AC7: "A sixth attempt within one hour ... from any source, is refused".
Nothing says whether a refused attempt counts, which of the two limits is applied
first, or what time a refusal promises.

*The readings.*

1. Count only the attempts that reached the code (a sliding window of five).
2. Count every arrival, the refused ones included, before anything else is looked at.

*Chosen: 2*, the reading that refuses more. Under it:

- The attempt is counted in a transaction of its own before the per-source delay, the
  check symbols or any hash, so a refused attempt is counted as surely as one that
  succeeds, and concurrent attempts are counted one at a time.
- The sixth arrival within the hour, and every one after it, is answered 429
  `auth.throttled` with `retryAt` an hour after the refused attempt. A sustained attack
  therefore keeps the credential closed while it lasts; that is the loudness the item
  asks for, and each refusal is an attempt the alerting sees.
- The per-source delay of AUTH-ABUSE-001 then applies as for sign-in, keyed on the
  source alone.
- Attempts older than the window are forgotten by a sweep.

*Tests that pin it.*
`BreakGlassEndpointTests.OPS_BOOT_004_AC7_TheSixthAttemptInAnHourIsRefusedFromAnySourceAsync`,
`BreakGlassStoreTests.OPS_BOOT_004_AC7_EveryAttemptIsCountedWithinTheWindowAsync`.

*Chapter text that should change.* OPS-BOOT-004 could say that every attempt counts,
refused ones included, and what `retryAt` a refusal carries.

---

## 293. What a refused break-glass code is answered

**Phase 9 · 2026-09-24 · Tier 3 · OPS-BOOT-002 AC1, OPS-BOOT-004 AC3, AC6**

*The question.* Chapter 09 answers `POST /auth/break-glass` with 422
`auth.breakglass.invalid` and 409 `auth.breakglass.consumed`. OPS-BOOT-002
AC1: "Use consumes it; a second attempt fails." OPS-BOOT-004 AC6: "a group with a
wrong check character is refused before the hash is compared." Nothing says which
codes are "consumed" rather than "invalid": a code already used, one replaced, or one
that never existed.

*The readings.*

1. Answer `consumed` for any code that was ever issued and no longer stands.
2. Answer `consumed` only for the issue most recently used, and `invalid` for
   everything else.

*Chosen: 2.* It tells the least: only the person who just used the envelope learns
the code was good, and a replaced issue reveals nothing about itself. Under it:

- A code that is not 36 symbols of the alphabet, hyphens and spaces aside, or whose
  groups do not all hold their check symbol, is answered 422 `auth.breakglass.invalid`
  before any issue is read.
- A code that matches the standing issue opens the session and spends it; the record
  of the use is written only while the issue still stands, so of two concurrent uses
  one opens a session and the other is answered 409 `auth.breakglass.consumed`.
- A code that matches the issue last used is answered 409 `auth.breakglass.consumed`.
- Any other code, a replaced one included, is answered 422 `auth.breakglass.invalid`.
- Every refusal counts against the source's delay (entry 292).

*Tests that pin it.*
`BreakGlassEndpointTests.OPS_BOOT_002_AC1_UseConsumesTheCredentialAsync`,
`BreakGlassEndpointTests.OPS_BOOT_004_AC3_ASecondGenerationInvalidatesTheFirstAsync`,
`BreakGlassEndpointTests.OPS_BOOT_004_AC6_AWrongCheckIsRefusedBeforeAnyHashIsComparedAsync`,
`BreakGlassStoreTests.OPS_BOOT_002_AC1_AnIssueIsSpentOnceAsync`, `BreakGlassCodeTests`.

*Chapter text that should change.* Chapter 09 could say that 409 answers the issue
last used and 422 every other code, a replaced one included.

---

## 294. How the reserved `emergency` account is marked and found

**Phase 9 · 2026-09-24 · Tier 2 · OPS-BOOT-001, OPS-BOOT-002, REG-ACCT-001**

*The question.* OPS-BOOT-002: "**The session belongs to a reserved account,
`emergency`**", created at bootstrap (OPS-BOOT-001), "with **no sign-in method of any
kind**". An account holds no name the library could look it up by, and this one has no
identifier to be found through. Nothing says how the library knows which account it
is.

*The readings.*

1. Keep its subject in a configuration key written by bootstrap.
2. Mark the account row itself, and let the database hold at most one marked row.

*Chosen: 2.* A configuration key is a value an administrator can change, which would
move the break-glass session onto another account; a mark on the row is written once,
by bootstrap alone. Under it:

- `accounts.emergency`, a boolean that is false on every account but the one, with a
  unique partial index (`ux_accounts_emergency`) over the marked row.
- `Account.CreateEmergency` is the only way to set it; it is carried through erasure
  and read back unchanged.
- The authentication and authorization areas read it through a port each, so neither
  learns the other's types.

*Tests that pin it.*
`AccountStatesTests.OPS_BOOT_002_TheReservedAccountIsNeverTakenDownAsync`,
`AccountTests.OPS_BOOT_002_TheEmergencyAccountIsNeverSuspendedOrDeleted`,
`ModelTests.REG_ACCT_001_AC2_NoFieldExistsOutsideTheGroupsTheTableNames`.

*Chapter text that should change.* OPS-BOOT-002 could name the mark and say the
database holds at most one such account.

---

## 295. What the reserved account is refused, and with which code

**Phase 9 · 2026-09-24 · Tier 3 · OPS-BOOT-002, AUTH-STEP-004**

*The question.* OPS-BOOT-002: the `emergency` account has "**no sign-in method of any
kind** ... no factor can be enrolled on it ... it appears in no device list, holds no
mailbox, and cannot be granted anything further, suspended, or deleted." AUTH-STEP-004:
"A **break-glass session SHALL satisfy the step-up requirement** for the duration of
its lifetime." No chapter names the refusal, and the session that satisfies every gate
is itself the only session the account has.

*The readings.*

1. Refuse only through the absence of any flow that reaches the account.
2. Refuse each operation named, explicitly, wherever it could reach the account, and
   refuse the gated actions of the session that would give it a way in or end it.

*Chosen: 2*, which refuses in more places. Under it, each is answered 403
`authz.denied`, the refusal the caller would see for an operation it may not perform:

- suspension by an administrator, and a takedown;
- a grant to the account, and its addition to a group, whose grants would be
  something further granted;
- from the break-glass session, the step-up actions `password:set`,
  `identifier:add`, `username:change`, `factor:enrol`, `recoverycodes:generate`,
  `mailcredential:create`, `account:deactivate` and `account:delete`, which it
  passes no gate for although it passes every other.
- The domain refuses the same transitions itself (`Account` throws on suspension,
  deactivation, takedown and deletion of the reserved account), so a path this list
  missed faults rather than succeeds.
- The session never passes through device trust, so it appears in no device list.

*Tests that pin it.*
`AccountAdministrationTests.OPS_BOOT_002_TheEmergencyAccountIsNeverSuspendedAsync`,
`AccountTests.OPS_BOOT_002_TheEmergencyAccountIsNeverSuspendedOrDeleted`,
`AccountStatesTests.OPS_BOOT_002_TheReservedAccountIsNeverTakenDownAsync`,
`GrantEndpointTests.OPS_BOOT_002_TheReservedAccountIsGrantedNothingAsync`,
`GroupEndpointTests.OPS_BOOT_002_TheReservedAccountJoinsNoGroupAsync`,
`BreakGlassEndpointTests.OPS_BOOT_002_NoSignInMethodIsGivenToTheReservedAccountAsync`.

*Chapter text that should change.* OPS-BOOT-002 could name `authz.denied` as the
refusal and list the gated actions the session does not pass.

---

## 296. How long the break-glass session lives

**Phase 9 · 2026-09-24 · Tier 3 · OPS-BOOT-002 AC2, AUTH-STEP-004 AC3, AUTH-SESS-005b**

*The question.* OPS-BOOT-002: "The session SHALL satisfy step-up for its lifetime
(AUTH-STEP-004). Lifetime is configurable with an enforced ceiling (D-138)." Chapter 10
section 4: `breakglass.session.lifetime`, "4 hours, ceiling 12". AC2: "The session
expires automatically at a configured lifetime within the ceiling." Nothing says
whether the session also ends when idle, and under which inactivity window.

*The readings.*

1. The lifetime is the only limit; an idle session lives to it.
2. The lifetime is the absolute limit, and the account's policy's inactivity window
   applies as well, never longer than the lifetime.

*Chosen: 2*, which ends the session sooner. An unattended emergency session is the
case the idle window exists for. Under it:

- The absolute expiry is `breakglass.session.lifetime` from the moment of use,
  whatever the policy's absolute lifetime says.
- The inactivity window is the one the policy gives the session's assurance, cut to
  the lifetime where it is longer.
- The session satisfies every gate (entry 295 excepted) until it ends, and the
  exception ends with it.

*Tests that pin it.*
`BreakGlassEndpointTests.OPS_BOOT_002_AC2_TheSessionExpiresAtTheConfiguredLifetimeAsync`,
`StepUpTests.AUTH_STEP_004_AC3_TheExceptionDoesNotOutliveTheSession`.

*Chapter text that should change.* OPS-BOOT-002 could say that the session's idle
window is the policy's, bounded by the lifetime.

---

## 297. Break-glass has no service contract in `Janus.Core`

**Phase 9 · 2026-09-24 · Tier 2 · LIB-API-001, LIB-API-005, CONV-DESIGN-002**

*The question.* CONV-DESIGN-002: the unit of application logic is "a **service class
per operation group**, implementing the corresponding contract from `Janus.Core`
(LIB-API-005)". The two break-glass endpoints are the library's own and chapter 07
names no contract a host calls for them.

*The readings.*

1. Publish an `IBreakGlass` contract in `Janus.Core` and implement it.
2. Keep the service internal, reached only through the two endpoints.

*Chosen: 2*, the smaller public surface. A host has no business presenting or
generating the credential except through the frontend route and the management
application, and a public contract would be a second way in that the endpoints'
throttle and profile do not guard. The service is `internal sealed`, constructed by
dependency injection and ordered as CONV-DESIGN-002 orders every operation; the only
additions to the public surface are the two error codes and the two audit actions.

*Tests that pin it.* The public-surface file (`PublicAPI.Unshipped.txt`) and its
analyser; `BreakGlassEndpointTests`.

*Chapter text that should change.* Chapter 07 could say that the break-glass
operations are reached through the endpoints alone.

---

## 298. The address the generated page carries

**Phase 9 · 2026-09-24 · Tier 2 · OPS-BOOT-004, FE-BG-001, LIB-HOST-001**

*The question.* OPS-BOOT-004: the page shows "the `/break-glass` address". Chapter 09
answers `POST /admin/break-glass/generate` with `{ "credential": "...", "address":
"...", "issuedAt": "..." }`. FE-BG-001: "The authentication application SHALL provide
a route `/break-glass`". Nothing says where the absolute address comes from.

*The readings.*

1. Answer the path alone and let the frontend make it absolute.
2. Answer the absolute address: the authentication application's origin, which the
   host already declares (`AuthenticationAddresses.Provider`), and `/break-glass`.

*Chosen: 2.* The envelope is printed from this answer and read years later by someone
who is not technical, so what it prints has to be complete; and the origin is a value
the host has declared once already, not a new one. The address is composed in the
service, from the declaration, and never from the request.

*Tests that pin it.*
`BreakGlassEndpointTests.OPS_BOOT_004_AC1_ABreakGlassSessionGeneratesAReplacementAsync`.

*Chapter text that should change.* Chapter 09 could say `address` is absolute and
built from the authentication application's declared origin.

---

## 299. One break-glass issue stands at a time

**Phase 9 · 2026-09-24 · Tier 2 · OPS-BOOT-004 AC3, CONV-DESIGN-003**

*The question.* OPS-BOOT-004 AC3: "a second request generates a new credential and
invalidates the previous one." Two generations, or a generation and a use, can run at
once, and nothing says what keeps two issues from standing together.

*The readings.*

1. Rely on the service reading what stands before it writes.
2. Serialize every operation that reads what stands, and let the database refuse a
   second standing issue as well.

*Chosen: 2.* Under it:

- Generation and use take a transaction-scoped advisory lock before they read what
  stands, so a crashed operation releases it with its transaction.
- A unique index over a constant (`ux_break_glass_credentials_standing`, on rows
  neither used nor replaced) refuses a second standing issue whatever reaches the
  table, and a check constraint refuses an issue both used and replaced.
- The record of a use or a replacement is written only where the issue still stands,
  and says whether it was.
- An issue keeps `issued_by`, the account that generated it, and never its code.

*Tests that pin it.*
`BreakGlassStoreTests.OPS_BOOT_004_OneIssueStandsAtATimeAsync`,
`BreakGlassStoreTests.OPS_BOOT_004_AnIssueEndsOnceAsync`,
`BreakGlassStoreTests.OPS_BOOT_004_AC3_AReplacementStandsInThePreviousPlaceAsync`,
`BreakGlassEndpointTests.OPS_BOOT_004_AC3_ASecondGenerationInvalidatesTheFirstAsync`.

*Chapter text that should change.* None; this is how the requirement is held.

---

## 300. How the break-glass code is hashed

**Phase 9 · 2026-09-24 · Tier 3 · OPS-BOOT-004 AC4, INT-PWD**

*The question.* OPS-BOOT-004: "The system stores **only a hash**". AC4: "No plaintext
break-glass credential exists in the database, the secrets manager, logs, mail, or any
file." Nothing names the hash. The code carries 135 bits, so a fast hash would already
resist guessing; a slow one costs time at every use.

*The readings.*

1. A keyed or plain fast hash, which 135 bits make safe against guessing.
2. Argon2id with the password parameters of chapter 10 section 4.

*Chosen: 2*, the one that keeps most in reserve. The code is used a handful of times
in the life of a deployment, so the cost is paid rarely, and a slow hash protects the
code if the entropy of the draw is ever less than the item promises. Under it:

- The code is hashed in its canonical form (case folded, the check symbols kept,
  hyphens and spaces dropped), under `password.argon2.memory`, `.iterations` and
  `.parallelism`, and the hash carries its parameters, so a later change of them
  still verifies an issue made before it.
- The bytes of the code are cleared once hashed or compared, and the code is marked
  never to be logged.

*Tests that pin it.*
`BreakGlassStoreTests.OPS_BOOT_004_AC4_OnlyAHashOfTheCodeIsHeldAsync`,
`BreakGlassEndpointTests.OPS_BOOT_004_AC3_ASecondGenerationInvalidatesTheFirstAsync`,
`NeverLoggedTests.CONV_LOG_003_AC1_EveryMemberCarryingAForbiddenValueIsMarked` (the
authentication and hosting ones).

*Chapter text that should change.* OPS-BOOT-004 could name the hash.

---

## 301. What "any cookie present is ignored" does at `/auth/break-glass`

**Phase 9 · 2026-09-24 · Tier 3 · FE-BG-001 AC3, BFF-MACH-001, BFF-CSRF**

*The question.* Chapter 09: `POST /auth/break-glass` is "**Mounted on the machine
profile** (BFF-MACH-001): no session, outside the session-bound CSRF layer,
source-throttled. **Any cookie present is ignored rather than refused**". The machine
profile refuses a request that carries the session cookie. FE-BG-001 AC3: "The page
works with a stale session cookie present for the domain."

*The readings.*

1. Exempt the route from the machine profile altogether.
2. Keep the route on the machine profile, marked as machine-governed and held to the
   malformed-request answer, but without the refusal of a cookie, and never resolve
   the session the cookie names.

*Chosen: 2*, the one that exempts the least. Under it:

- The route is one of the machine routes, and the only one listed as ignoring the
  cookie; everything else the profile does to it stays.
- No session is read from the cookie, so nothing the stale session holds reaches the
  operation.
- Success writes the new session's cookie over the stale one and clears the
  first-contact cookie, so nothing of the browser's earlier state survives.

*Tests that pin it.*
`BreakGlassEndpointTests.FE_BG_001_AC3_AStaleSessionCookieIsIgnoredAsync`.

*Chapter text that should change.* BFF-MACH-001 could list `/auth/break-glass` as
the machine route that ignores a cookie.

---

## 302. The owner's stated reason has nowhere to be stated

**Phase 9 · 2026-09-24 · Tier 3 · OPS-BOOT-002, FE-BG-001, AUTHZ-IMP-001**

*The question.* OPS-BOOT-002: "Every action in a break-glass session is audited with
`emergency` as acting and effective identity (AUTHZ-IMP-001) and the owner's stated
reason". Chapter 09 gives the request as `{ "credential": "..." }` and nothing else,
and FE-BG-001: "one field for the sealed credential, one button, no other controls."
The two chapters contradict each other: there is no place for the owner to state a
reason once for the session.

*The readings.*

1. Add a `reason` member to `POST /auth/break-glass`, and a control to the page.
2. Keep the shape chapter 09 gives, and record for each action in the session the
   reason its own request carries.

*Chosen: 2.* Chapters 09 and 10 are authoritative for shapes, and a second control on
a page for someone who is not technical is what FE-BG-001 forbids. Under it:

- Every action of the session is audited with `emergency` as acting and effective
  identity, which no other session can be.
- An action whose request carries a reason (a grant, a recovery approval, a
  configuration change, a takedown) records it as it would for anyone; the use itself
  records none.
- The alert the use raises is the control that asks the owner why.

*Tests that pin it.*
`BreakGlassEndpointTests.AUTH_STEP_004_AC2_AnActionInTheSessionIsAuditedAsTheReservedAccountAsync`,
`BreakGlassEndpointTests.OPS_BOOT_002_AC4_TheSessionGrantsSystemAdministrationAsync`.

*Chapter text that should change.* Either OPS-BOOT-002 drops "and the owner's stated
reason", or chapter 09 and FE-BG-001 gain the member and the control; the owner
decides which.

---

## 303. How an action of background work is audited, and what refuses it to nobody

**Phase 9 · 2026-09-24 · Tier 3 · IDN-PRIN-001 AC1, AC3, AC4, INF-BG-002, IDN-AUD-001**

*The question.* IDN-PRIN-001 AC4: "Actions taken by either are audited with the
reason." INF-BG-002 AC1: "A background job cannot query without a principal." AC2:
"Its actions are audited with the stated reason." The audit record holds an acting and
an effective subject and nothing else about the actor, and the passes that audit took
no principal at all: an erasure the sweep executed was recorded with the empty subject
as its actor and no reason.

*The readings.*

1. Write the principal's name and reason into the record's `details` document.
2. Give the record columns of its own for the principal and its reason, with the
   acting subject left empty, and let the database refuse any other combination.

*Chosen: 2*, the one the database enforces rather than the code remembering. Under it:

- `audit_records` gains `principal` and `principal_reason`, and
  `ck_audit_records_principal` admits them only together and only beside the empty
  acting subject; the effective subject is still the account the action was taken
  on, where there is one.
- `AuditRecord.Of` has a form that takes a `SystemPrincipal`, and the privacy and
  credential audit ports a form each; nothing else writes a principal.
- Every pass that records what it does (the account and organization erasure sweeps,
  the privacy-request deadline sweep and the loss-report windows) takes the
  `AccessContext` it runs under and throws unless it carries a principal that may run
  `expiry-sweep`, so a person, or a principal named for other work, cannot run it.
- The breach query's public `AuditEntry` is unchanged, so the public surface does not
  grow; it shows the empty acting subject for such a record.

*Tests that pin it.*
`AuditStoreTests.IDN_PRIN_001_AC4_ABackgroundActionIsRecordedWithItsReasonAsync`,
`AuditStoreTests.IDN_PRIN_001_AC4_APrincipalIsRecordedOnlyWithItsReasonAndNoActorAsync`,
`DeletionSweepTests.IDN_AUD_001_ThePassRecordsWhatItDidAndToWhomAsync`,
`LossReportsTests.IDN_PRIN_001_AC4_AnInvalidationIsRecordedUnderTheSweepAsync`,
`DeletionSweepTests.INF_BG_002_AC1_TheSweepNeverRunsAsNobodyAsync`,
`OrganizationErasureSweepTests.INF_BG_002_AC1_TheSweepNeverRunsAsNobodyAsync`,
`DeadlineSweepTests.INF_BG_002_AC1_TheSweepNeverRunsAsNobodyAsync`,
`LossReportsTests.INF_BG_002_AC1_TheAdvanceNeverRunsAsNobodyAsync`.

*Chapter text that should change.* IDN-AUD-001 could name the two columns beside the
acting and effective subjects.

---

## 304. Which pool-wide operations the scheduled jobs run as

**Phase 9 · 2026-09-24 · Tier 3 · IDN-PRIN-001 AC3, INF-BG-001, INF-BG-002**

*The question.* IDN-PRIN-001 lists the pool-wide work a deployment-scoped principal
exists for, as reconciliation, retention purging, records-of-processing generation and
expiry sweeps, and says it "is restricted to those named operations." INF-BG-001
requires jobs that are none of the four: the transactional outbox publisher, the
mailbox provisioning that follows it, the carrying of raised alerts, and the gateway
balance poll. Each of them has to run as a principal naming some operation.

*The readings.*

1. Keep the four, and run each other job under the nearest of them (a publisher as
   reconciliation, the balance poll as an expiry sweep).
2. Add one operation per job.
3. Add the fewest operations that cover the jobs outside the four: `delivery`, carrying
   what has been committed to where it goes, and `monitoring`, reading the state of
   something the deployment depends on and raising what the reading calls for.

*Chosen: 3.* Reading 1 grants a job an operation it does not perform, so the outbox
publisher's principal could run a reconciliation; reading 2 grows the public
enumeration by a member for every job. Under 3:

- `SystemOperation` gains `Delivery` (`delivery`) and `Monitoring` (`monitoring`).
- Every job is its own deployment-scoped principal naming exactly one operation, and
  states as its reason the item that requires it: the sweeps and grace windows run as
  `expiry-sweep`, the mail reconciliation as `reconciliation`, the outbox, mailbox
  provisioning and alert carrying as `delivery`, the balance poll as `monitoring`.
- A pass that records what it does still refuses any principal that may not run
  `expiry-sweep` (entry 303).

*Tests that pin it.*
`BackgroundWorkerTests.INF_BG_002_EveryScheduledJobIsANamedRestrictedPrincipal`,
`BackgroundWorkerTests.INF_BG_002_AC1_AJobRunsAsItsPrincipalAsync`.

*Chapter text that should change.* IDN-PRIN-001 could name delivery and monitoring
beside the four operations it lists, or say which of the four each of the jobs of
INF-BG-001 runs as.

---

## 305. How often the jobs run that no setting paces

**Phase 9 · 2026-09-24 · Tier 2 · INF-BG-001, INT-MAIL-007, OPS-ALERT-001, INT-SMS-004**

*The question.* INF-BG-001 AC2 judges a job by twice its interval, so every job needs
one. `10` names the interval of most of them (`sweep.interval`, `outbox.poll.interval`,
`abuse.sms.pollinterval`) but not of two: the carrying of raised alerts to their
channels, and the mail reconciliation, which INT-MAIL-007 says "SHALL run daily". Nor
does any chapter say what the balance poll does in a deployment that registered no SMS
transport.

*The readings.*

1. Carry raised alerts every `sweep.interval`, as the other passes over a table are.
2. Carry them every `outbox.poll.interval`, as the outbox publisher is.

For the reconciliation, a new setting, or the day the chapter fixes. For the poll, a
failure where no transport is registered, or a run with nothing to read.

*Chosen: 2*, a day for the reconciliation, and nothing to read for the poll. A raised
alert waits in `raised_alerts` exactly as an event waits in the outbox, and it is the
record least able to wait five minutes. The reconciliation's day is the chapter's own
and needs no key. A deployment with no transport has no balance, and a failure there
would raise `background-job-failed` every hour for a gateway that does not exist.

*Tests that pin it.* `BackgroundJobsTests.INF_BG_001_AC1_EveryJobRunsWithoutAPersonAsync`.

*Chapter text that should change.* The `outbox.poll.interval` row of `10` could name
OPS-ALERT-001 among the passes it paces.

---

## 306. How far back the token sweep reaches, and the one sweep not scheduled

**Phase 9 · 2026-09-24 · Tier 3 · AUTH-KEY-003, AUTH-OIDC-003, OPS-OBS-003**

*The question.* AUTH-KEY-003 requires consumed refresh tokens to be swept. AUTH-OIDC-003
AC1 requires "A refresh token presented twice revokes all derived sessions", which the
provider can only do while the redeemed row exists; the store's prune removes a
redeemed row created before the instant it is given, whatever that row's own expiry.
Separately, the store of staged identifier verifications has a sweep that takes an
instant, and no chapter says how old a staged verification is when it is abandoned.

*The readings.* For the tokens:

1. Prune at the access token lifetime, or at some shorter span.
2. Prune only what is older than the longest session the settings allow, the ceiling of
   `session.default.absolute`, since no refresh token outlives its session.

For the staged verifications:

1. Sweep them at `code.verification.lifetime` after staging.
2. Leave them unswept, ended only by `POST /account/identifiers/{id}/abandon` or by
   completion.

*Chosen: 2 and 2*, the readings that keep most. Pruning a redeemed refresh token that
could still be presented turns a stolen token's reuse from a revocation of the family
into a plain refusal, which AC1 forbids. A staged verification swept by age would
strand the unverified identifier the account still holds: asking for the code again
finds no pending verification and sends nothing.

*Tests that pin it.* `BackgroundJobsTests.INF_BG_001_AC1_EveryJobRunsWithoutAPersonAsync`
runs the sweep over the database; the reach itself is the constant `LongestSession` in
`BackgroundJobs`.

*Chapter text that should change.* AUTH-KEY-003 could say how long a consumed refresh
token is kept, and chapter `20` how long a staged identifier verification stands.

---

## 307. How the command-line application reaches the deployment's keys

**Phase 9 · 2026-09-24 · Tier 3 · OPS-BOOT-001, OPS-SEC-001, OPS-SEC-003, LIB-EXT-001, INF-HOST-003**

*The question.* `janus bootstrap` creates an account whose identifiers are encrypted
under a subject key wrapped by the key-encryption key and found by fingerprints
computed under the fingerprint key, and `janus rotate-kek` needs the key versions and
the maintenance credential. LIB-EXT-001 makes the secret source the host's to supply,
"registered through configuration, not inheritance from a library type", and
`ISecretSource` says each value is read "at startup or at the start of a command". The
command-line application is an executable of the package: CONV-LAYOUT-002 allows it no
public type a host could call with its own source, and CONV-CODE-004 allows no
reflection to load one. INF-HOST-003 allows no secret in a file, an image or an
environment variable.

*The readings.*

1. Give the command a public entry point, in `Janus.Core` or in the mounting types,
   that a host calls from a program of its own with its `ISecretSource`.
2. Load the host's `ISecretSource` from an assembly the command line names.
3. Take the values as arguments, from a file, or from the environment.
4. Read one document from standard input, which the operator pipes from the secrets
   manager's own client, and refuse to run where standard input is a terminal.

*Chosen: 4*, the one reading that crosses no rule: 1 grows the public surface against
CONV-LAYOUT-002, 2 is reflection against CONV-CODE-004, 3 puts a secret where
INF-HOST-003 forbids one or in the process list. Under 4:

- The document is JSON with `connection` (the database connection the command runs
  under), `keyEncryptionKeys` (`current` and `versions`, each version's key in
  base64) and `fingerprintKey` in base64. The rotation commands read what they need
  from the same document, and are recorded where they are built.
- The command reads it once, at its start, and clears every key's bytes when it
  ends; nothing of it is written anywhere. It is read by the command itself and not
  through an `ISecretSource` of the library's own, which would be a default secret
  source that LIB-EXT-001 leaves to the host.
- A document that is not JSON, or larger than 64 KiB, is refused with
  `api.request.malformed` naming `input`; a missing or unusable key with
  `model.startup.kekunavailable` naming `keyEncryptionKeys` or `fingerprintKey`. A
  refusal carries the member it concerns and nothing of the document.
- A command whose standard input is not redirected is refused before it reads
  anything, so no key is typed or pasted into a terminal.

*Tests that pin it.* `BootstrapRefusalTests.OPS_SEC_001_TheCommandRefusesATerminalAsync`,
`BootstrapRefusalTests.OPS_SEC_001_AC2_TheCommandRefusesADocumentWithoutTheKeysAsync`,
`BootstrapRefusalTests.OPS_SEC_001_AC2_TheCommandRefusesAKeyThatCannotBeUsedAsync`,
`BootstrapRefusalTests.OPS_SEC_001_ARefusalCarriesNoneOfTheDocumentAsync`.

*Chapter text that should change.* OPS-BOOT-001 and OPS-SEC-003 could say how the
command is handed the values of INF-HOST-003, and LIB-EXT-001 how the secret source is
supplied to `Janus.Cli`.

---

## 308. What `janus bootstrap` takes on its command line, and how it refuses

**Phase 9 · 2026-09-24 · Tier 2 · OPS-BOOT-001, LIB-HOST-001, OPS-CFG-004, CONV-NAME-001**

*The question.* OPS-BOOT-001 gives the arguments as "the organization name, the
administrator's email and phone, and every required deployment value by its key name",
and the exit codes, and no more: not the spelling of an argument, whether a key that is
not required may be named too, what completeness means, or where a refusal is written.

*The readings.*

1. Take any key of chapter 10 section 4, required or not, as `--<key> <value>`.
2. Take the required keys only, and refuse every other.

*Chosen: 2.* Every other key keeps its safe default until the application changes it
through the audited configuration path, so a value bootstrap wrote could not bypass a
direction rule or a protected key. Under 2:

- The executable is the project's own, `Janus.Cli`, and `bootstrap` is its first
  argument. Naming the executable `janus` would put the product name where
  CONV-NAME-001 (D-163) allows it nowhere; a deployment may install it under any name.
- Every argument is `--<name> <value>`: `--organization`, `--email`, `--phone`, and
  each required key by its key name. A name given twice, without a value, or not
  starting `--` is refused with `api.request.malformed` naming it.
- A key that is not among the required ones is refused the same way, naming
  `--<key>`. A required value its key does not admit is refused with the code the key
  gives it, before the database is reached.
- What is complete is decided by the same rule the host's start applies
  (`model.startup.declarationmissing` naming the key,
  `model.startup.governinglanguage` for `legal.governinglanguage`), so what bootstrap
  accepts is a deployment that starts.
- The schema is validated before anything is written, and the whole run is one
  transaction.
- Success prints the enrolment address alone on standard output and exits 0. A
  refusal writes one JSON line `{"code": ..., "details": {...}}` to standard error,
  prints nothing on standard output, and exits 1.

*Tests that pin it.*
`BootstrapRefusalTests.OPS_BOOT_001_AC4_BootstrapWithoutTheGoverningLanguageIsRefusedByNameAsync`,
`BootstrapRefusalTests.OPS_BOOT_001_AValueTheDeploymentLeftUnnamedIsRefusedByItsKeyAsync`,
`BootstrapRefusalTests.OPS_BOOT_001_AValueItsKeyDoesNotAdmitIsRefusedAsync`,
`BootstrapRefusalTests.OPS_BOOT_001_AKeyThatDoesNotNameTheDeploymentIsRefusedAsync`,
`BootstrapTests.OPS_BOOT_001_AFreshDeploymentIsStoodUpByTheCommandAsync`,
`BootstrapTests.OPS_BOOT_001_TheNamedValuesAndTheAdministrativePolicyAreWrittenAsync`.

*Chapter text that should change.* OPS-BOOT-001 could give the argument form, say
that only the required keys are taken, say where a refusal is written, and name the
command without the product name.

---

## 309. How bootstrap queues the first administrator's mailbox

**Phase 9 · 2026-09-24 · Tier 2 · OPS-BOOT-001, INT-MAIL-006 AC1a, REG-MAIL-001**

*The question.* INT-MAIL-006 AC1a requires that the bootstrap-created administrator's
"provisioning request is queued and completes on the first successful push". The
arguments OPS-BOOT-001 lists name the administrator's email and phone and no corporate
address, and the command cannot tell whether the deployment integrates a mail server,
since it needs only the database.

*The readings.*

1. Queue a mailbox at the administrator's email.
2. Queue none.
3. Take an optional `--mailbox <corporate address>` and queue a mailbox there where
   one is named.

*Chosen: 3.* Reading 1 makes the personal email the corporate one, which REG-MAIL-001
forbids; reading 2 leaves AC1a unmet on a deployment that has a mail server. Under 3:

- `--mailbox` is optional. Where it is named, the administrator takes the corporate
  address as it would at an invitation's acknowledgement (primary, locked, verified),
  and a mailbox is reserved at it and held by the administrator, which the delivery
  job pushes once the mail server is reachable.
- An address equal to the personal email is refused with
  `identity.identifier.invalid` naming `mailbox`.
- The `emergency` account gets no mailbox and no identifier (AC1b).

*Tests that pin it.* `BootstrapTests.INT_MAIL_006_AC1a_TheAdministratorsMailboxIsQueuedAsync`,
`BootstrapTests.OPS_BOOT_002_TheEmergencyAccountHoldsTheRoleAndNoWayInAsync`.

*Chapter text that should change.* OPS-BOOT-001 could list the corporate address among
the arguments, and say it is optional where no mail server is integrated.

---

## 310. The enrolment address bootstrap prints

**Phase 9 · 2026-09-24 · Tier 2 · OPS-BOOT-001, INT-MAIL-006, API-LAND-001**

*The question.* OPS-BOOT-001 prints the link "as the full `/enrol` address" and says
"nothing is transmitted". INT-MAIL-006 says the administrator is reached at the
personal address "where the enrolment link goes in any case". Neither names the origin
the address is under or where the token travels in it.

*The readings.*

1. Print the address and also send it to the personal email.
2. Print it only.

*Chosen: 2.* The command needs only the database, and a message sent would be a
transmission OPS-BOOT-001 rules out; the personal email is where the administrator is
reached afterwards. Under 2:

- The address is the first `webauthn.origins` entry's scheme and authority, then
  `/enrol#token=<token>`. The token travels in the fragment, which no request carries,
  so no server log or referrer holds it.
- The token is an enrolment link held for the administrator, which lives
  `recovery.link.lifetime`.
- A first `webauthn.origins` entry that is not an absolute address is refused with
  `api.request.malformed` naming the key.

*Tests that pin it.* `BootstrapTests.OPS_BOOT_001_ThePrintedAddressEnrolsTheFirstAdministratorAsync`,
`BootstrapTests.OPS_BOOT_001_AFreshDeploymentIsStoodUpByTheCommandAsync`.

*Chapter text that should change.* OPS-BOOT-001 could name the origin and the
fragment, and INT-MAIL-006 could say the link is printed, not sent.

---

## 311. What "a system administrator exists" means to bootstrap

**Phase 9 · 2026-09-24 · Tier 3 · OPS-BOOT-001 AC1, OPS-CFG-007, D-028**

*The question.* OPS-BOOT-001 AC1: "Running it twice while a system administrator
exists is refused." A deployment can lose its administrator in several ways: the grant
revoked, the grant expired, the account suspended or erased. Nothing says which of
them re-opens bootstrap, which runs without any gate (D-028).

*The readings.*

1. Refuse only while some account holds a live, unexpired allow grant of a role
   holding `system:administer`.
2. Refuse where the administrative organization exists, or where any allow grant of
   such a role was made and not revoked, expired or not.

*Chosen: 2*, the strictest: it refuses most. Reading 1 would let whoever reaches the
database mint a new administrator once every grant has lapsed, which is a way in with
no gate at all. Under 2:

- The check runs inside the transaction bootstrap writes in.
- The refusal is `authz.denied`, with no details, and nothing is written.

*Tests that pin it.*
`BootstrapTests.OPS_BOOT_001_AC1_RunningItAgainWhileASystemAdministratorExistsIsRefusedAsync`.

*Chapter text that should change.* OPS-BOOT-001 AC1 could say that a deployment once
stood up is never stood up again, and name the refusal's code.

---

## 312. The canary subject and the reserved account bootstrap seeds

**Phase 9 · 2026-09-24 · Tier 2 · OPS-BOOT-001, OPS-BOOT-002, DR-007, INT-MAIL-006 AC1b**

*The question.* DR-007 says the canary holds "one encrypted field" and "a verified
email" found by its fingerprint, and chapter 10 records it in
`backup.restoretest.canary` as "the canary subject bootstrap seeds in the
administrative organization". Neither names the field, the address, or whether the
canary is an account.

*The readings.*

1. A subject key and rows with no account.
2. An account, a member of the administrative organization holding no role.

*Chosen: 2.* The identifier and profile rows are an account's, and a membership
without a role grants nothing. Under 2:

- The address is `canary@restore-test.invalid`, under the name RFC 2606 reserves for
  what never resolves, so nothing addressed to it reaches anybody.
- The encrypted field is the display name `Restore canary`.
- The canary holds no credential, so it has no way in.
- The `emergency` account is given a subject key like any other account, because a
  break-glass session acts under it and what it does is recorded against it.

*Tests that pin it.* `BootstrapTests.DR_007_TheCanarySubjectIsSeededAsync`,
`BootstrapTests.OPS_BOOT_002_TheEmergencyAccountHoldsTheRoleAndNoWayInAsync`,
`BootstrapTests.OPS_BOOT_001_AC2_NoAccountHoldsACredentialAsync`.

*Chapter text that should change.* DR-007 or chapter 10's `backup.restoretest.canary`
row could name the field, the address and the membership.

---

## 313. Who grants what bootstrap grants, and how its alert is raised

**Phase 9 · 2026-09-24 · Tier 3 · OPS-BOOT-001 AC3, IDN-LIFE-009a AC1, AUTHZ-GRANT-004, OPS-ALERT-001, D-133**

*The question.* IDN-LIFE-009a AC1: "Staff membership originates from an invitation,
never from a bare grant", while OPS-BOOT-001 and INT-MAIL-006 AC1a have bootstrap make
the first administrator's membership with no invitation. Every stored grant names who
made it and why, and bootstrap makes the first grants, when nobody holds anything.
OPS-BOOT-001 AC3 also has the "no emergency credential" alert raised on
OPS-ALERT-001, which is carried by the alert channels the host registers, and the
command runs with no host.

*The readings.*

1. For the membership: have bootstrap issue an invitation and acknowledge it itself,
   or attach the membership directly as the one exception.
2. For the granter: name none, or a subject no account holds, or each holder as its
   own granter with the item as the reason.
3. For the alert: send it from the command, or write it to the raised-alerts outbox
   for the delivery job to carry.

*Chosen: the direct attachment, the holder as granter, and the outbox.* An invitation
bootstrap acknowledged itself would record a consent nobody gave. A grant with no
granter is one no check can trace, and a granter no account holds reads as a system
action where a person stood behind the command. Under this:

- Bootstrap attaches its memberships through the same attachment the acknowledgement
  uses, and is the one caller of it beside the acknowledgement; the structure test
  that pins IDN-LIFE-009a AC1 names the two and no other.
- The administrator's and the emergency account's `system-administrator` grants, and
  the canary's membership, name the holder as granter and `OPS-BOOT-001` as the reason.
- The alert is written to the raised-alerts outbox in the bootstrap transaction, and
  carried by the delivery job like any raised alert, since the command sends nothing.

*Tests that pin it.*
`BootstrapTests.OPS_BOOT_001_AC3_NoEmergencyCredentialIsIssuedAndItsAbsenceIsRaisedAsync`,
`BootstrapTests.OPS_BOOT_001_ThePrintedAddressEnrolsTheFirstAdministratorAsync`,
`LibraryStructureTests.IDN_LIFE_009a_AC1_OnlyAnAcknowledgedInvitationMakesAMembership`.

*Chapter text that should change.* IDN-LIFE-009a AC1 could except the bootstrap
administrator, and OPS-BOOT-001 could name the granter of the first grants and say the
alert is queued rather than sent.

---

## 314. What bootstrap records in the audit trail

**Phase 9 · 2026-09-24 · Tier 3 · IDN-PRIN-001, IDN-AUD-001, AUTHZ-GRANT-004, OPS-BOOT-001**

*The question.* Bootstrap creates the administrative organization and defines the three
administrative roles. A person with server access runs it, but no one is signed in, and
IDN-PRIN-001 requires that non-human work act as a named principal with a stated
reason. Chapter 06 does not say whether bootstrap is audited, or as whom.

*The readings.*

1. Record nothing, since the rows themselves show what was made.
2. Record under the administrator bootstrap creates.
3. Record under a deployment-scoped system principal of its own.

*Chosen: 3*, the strictest: it keeps most, and does not credit the administrator with
what nobody signed in did. Under 3:

- `SystemOperation` gains `Bootstrap` (`bootstrap`). The principal is named
  `bootstrap`, states `OPS-BOOT-001` as its reason, and may run nothing else.
- The organization is recorded as `identity.organization.created`, and each role
  bootstrap defines as `authz.role.defined` with `before` empty and `reason`
  `OPS-BOOT-001`, both under the principal and in the bootstrap transaction.
- Every value bootstrap sets is recorded under the principal too, as entry 315 says.
- `IOrganizationAudit`, `IRoleAudit` and `IConfigurationAudit` gain a form that takes
  a system principal, as `ICredentialAudit` did in entry 303.

*Tests that pin it.*
`BootstrapTests.IDN_PRIN_001_AC4_WhatBootstrapDefinesAndSetsIsRecordedUnderItsPrincipalAsync`.

*Chapter text that should change.* OPS-BOOT-001 could say bootstrap is audited under a
principal of its own, and IDN-PRIN-001 could list `bootstrap` beside the operations
entry 304 added.

---

## 315. How bootstrap writes the values it sets

**Phase 9 · 2026-09-24 · Tier 3 · OPS-BOOT-001, OPS-CFG-002, OPS-CFG-004, OPS-CFG-005, chapter 10 section 4.1a**

*The question.* Bootstrap sets the values the deployment names, protected keys among
them, the administrative organization's `policy.<organization>` ("set at bootstrap",
chapter 10 section 4.1a) and `backup.restoretest.canary` ("set at bootstrap"). The
library's one path for a runtime setting is the configuration administration, which
refuses a system principal ("a system principal is nobody to answer", OPS-CFG-005) and
never writes a protected key (OPS-CFG-004). OPS-CFG-005 AC1: "Every change produces an
audit record with before and after values."

*The readings.*

1. Write through the configuration store as the administration does, leaving the
   values unrecorded.
2. Write the rows directly, unrecorded, as the initial state rather than a change.
3. Write the rows directly and record each value as a configuration change under
   bootstrap's principal.

*Chosen: 3*, the strictest: it keeps most. Reading 1 puts a second caller on the
store's write, which the structure test pinning OPS-CFG-002 refuses, and cannot write
the protected keys; reading 2 leaves the security values the deployment starts from
with no record. Under 3:

- Every value bootstrap sets is written by its seed, in the transaction, and recorded
  as `ops.configuration.changed` under the `bootstrap` principal with `key`, `before`
  (null where no value stood), `after`, `loosening` false and `reason`
  `OPS-BOOT-001`.
- A value set where none stood is recorded as no loosening: OPS-CFG-002 prices a change
  made through the application, and the value bootstrap sets is the one the
  deployment starts from, named by whoever holds the server (D-028).
- The settings table is written by the store and by bootstrap's seed alone; the
  structure test pinning OPS-CFG-002 now names the two.

*Tests that pin it.*
`BootstrapTests.IDN_PRIN_001_AC4_WhatBootstrapDefinesAndSetsIsRecordedUnderItsPrincipalAsync`,
`BootstrapTests.OPS_BOOT_001_TheNamedValuesAndTheAdministrativePolicyAreWrittenAsync`,
`LibraryStructureTests.OPS_CFG_002_OnlyTheConfigurationAdministrationWritesARuntimeSetting`.

*Chapter text that should change.* OPS-CFG-005 could say how the values set at
bootstrap are recorded, and what `before` holds where no value stood.

---

## 316. What the key-encryption key's rotation re-wraps, and what the maintenance credential reaches for it

**Phase 9 · 2026-09-24 · Tier 3 · OPS-SEC-003 AC2, AC3, AC5, OPS-MIG-003a AC4, DR-009a AC5, INF-BG-001**

*The question.* OPS-SEC-003 has the command re-wrap "every subject key" and retire the
previous version once the job reports complete, "a value still wrapped under it (there
is none by construction)". The library also holds six other values wrapped directly
under the key-encryption key with their version beside them: an invitation's, a
reserved mailbox's, a staged registration's and a queued message's own data key, the
sign-on proof of a pre-authentication session, and the private half of each token
signing key. A signing key stays current for `token.signing.rotation`. OPS-MIG-003a AC4
holds the maintenance credential to "the subject-key table and the rotation progress
table, and ... no other table", INF-BG-001 says the re-wrap "is not background work of
the worker", and DR-009a AC5 says it is performed through OPS-SEC-003 "and by no other
path". OPS-SEC-003 AC5 has each step audited, and the maintenance credential holds no
right on the trail.

*The readings.*

1. Re-wrap the subject keys alone. Once the operator removes the previous version, the
   signing key and any mailbox, invitation or registration still under it no longer
   unwrap, and on a suspected exposure they stay readable to whoever holds the old
   version until they age out.
2. Have the worker re-wrap the six under the application's credential, which INF-BG-001
   and DR-009a AC5 refuse.
3. Have the command re-wrap all seven under the maintenance credential, granted the row
   key, the version and the wrapped value of each of the six tables and no other column,
   and the append to the trail.

*Chosen: 3*, the strictest where granting least and keeping most pull apart: reading 1
keeps least, since the previous version is retired while values stand under it, and
reading 2 goes round the one path. Under 3:

- The command re-wraps the subject keys in the ordered pass of OPS-SEC-003, then sweeps
  subject keys written under a previous version behind the point the pass had reached,
  then the six other columns, each in batches of 500 committed on their own. Each
  value is written back only where it still stands as it was read, so an erasure or a
  newer wrapping made meanwhile is never overwritten and no value is re-wrapped twice.
  An erased subject key is under no version and is left alone.
- The processed count, and the count audited, is the subject keys re-wrapped, as
  OPS-SEC-003 AC5 names it.
- The maintenance credential gains, by migration: read and write of `key_rotations`;
  `INSERT` on `audit_records`, as the application holds it; and column rights, the row's
  key, the version and the wrapped value to read and the last two to write, on
  `invitations`, `mailboxes`, `registration_sessions`, `send_outbox`, `signing_keys` and
  `preauthentication_sessions`. The serialized model lists every grant and the role
  tests hold the list against what the database grants. The application's credential
  reaches nothing of `key_rotations`.
- The codes and refresh tokens the OIDC server encrypts under keys derived from each
  held version (entry 159) are not re-encrypted: once the previous version is removed,
  a refresh token issued before the rotation no longer reads and its holder signs in
  again. A code lives sixty seconds.

*Tests that pin it.*
`KeyRotationTests.OPS_SEC_003_AC3_AfterRetirementNoValueIsWrappedUnderThePreviousVersionAsync`,
`KeyRotationTests.OPS_SEC_003_AC2_AKilledRunResumesFromItsProgressAndReWrapsEachKeyOnceAsync`,
`DatabaseRoleTests.OPS_MIG_003a_AC4_TheMaintenanceRoleReachesTheWrappedValuesAndNoOtherColumnAsync`,
`DatabaseRoleTests.OPS_MIG_003a_AC4_TheListedGrantsAreTheOnesTheDatabaseHoldsAsync`,
`SerializedModelTests.OPS_MIG_003a_AC4_TheMaintenanceGrantsAreListedInTheSerializedModel`.

*Chapter text that should change.* OPS-SEC-003 could name every value wrapped under the
key rather than the subject keys alone, and say what becomes of tokens protected under
a derived key; OPS-MIG-003a AC4 could list the column rights on the six tables and the
append to the trail beside the subject-key and progress tables.

---

## 317. How a key-encryption key rotation starts, is confirmed and retires

**Phase 9 · 2026-09-24 · Tier 3 · OPS-SEC-003 AC1, AC3, AC4, DR-009, DR-009a, IDN-PRIN-001**

*The question.* OPS-SEC-003 has the command "introduce a new key version in the secrets
manager and record it as current for wrapping", produce the escrow copy, not report the
operation complete "until the operator confirms it is sealed", and retire the previous
version once the job reports complete. The library holds no client of any secrets
manager (LIB-EXT-001, CONV-DESIGN-008) and reads its keys only from the document of
entry 307. Nothing says how the confirmation is given, what "retired" does, what the
escrow copy looks like, which refusals the command gives, or who the actor of its audit
records is.

*The readings.*

1. For the new version: the command draws it and prints it for the operator to store,
   and re-wraps under it at once; or the operator adds it to the secrets manager as
   current, keeping the previous one, and pipes the document to the command.
2. For the confirmation: an interactive prompt, or a second run with an argument.
3. For retirement: the library drops the version itself, or records the retirement and
   leaves the removal from the secrets manager to the operator.

*Chosen: the operator adds the version, a second run confirms, and retirement is
recorded*, the strictest: a key the command drew and re-wrapped under before it was
stored anywhere is lost with the process, a prompt reads standard input that carries
the keys, and the library cannot remove a secret it never wrote. Under this:

- `rotate-kek` runs under the maintenance credential: the connection's role holds the
  maintenance role's rights and has no path to the application's. Anything else,
  including a superuser, is refused with `authz.denied` and no details, before anything
  is read.
- The document's current version is the one rotated to. A rotation starts only to a
  version later than any rotated to before, and only where every value stored is under
  a version the document holds and none under one later than the current; otherwise
  `model.startup.kekunavailable` naming `keyEncryptionKeys`. A rotation that stopped is
  resumed by a run whose document names its version current, and no other starts
  while it stands.
- A run that completes the re-wrap prints the escrow copy, then the report
  `{"version":N,"processed":M}`. The escrow copy is the document member the version
  would be restored from, `{"keyEncryptionKeys":{"current":N,"versions":{"N":"<base64>"}}}`,
  written from a buffer cleared afterwards and never held in a string. A later run
  before the seal prints it again, since a process that died after recording completion
  may never have printed it.
- `rotate-kek --sealed` confirms the seal. It is refused with `api.request.malformed`
  naming `sealed` where no rotation has completed or the latest has already retired.
  It sweeps once more; where it finds a value wrapped under a previous version since
  the rotation completed, which is an application not yet handed the new version, it
  re-wraps it and refuses with the same code and `pending` giving the count, and the
  previous version stays. Otherwise it records `retired_at` and reports
  `{"version":N,"processed":M,"retired":[...]}`, the versions the operator then removes
  from the secrets manager. A value found under a removed version fails with the named
  error "The subject's key is wrapped under a retired version."
- The steps are audited as `ops.keyrotation.started`, `resumed`, `completed` and
  `retired`, security category, with `kind`, `version`, `processed` and, on retirement,
  `retired`. The actor is the deployment-scoped principal `rotate-kek` with the reason
  `OPS-SEC-003` and the new operation `key-rotation`, since the command cannot know
  which person runs it.
- The runbook order this assumes: add the version as current and keep the previous;
  restart the application on the new document; run `rotate-kek`; seal the copy; run
  `rotate-kek --sealed`; remove the retired versions.

*Tests that pin it.*
`KeyRotationTests.OPS_SEC_003_AC1_TheCommandIsRefusedWithoutTheMaintenanceCredentialAsync`,
`KeyRotationTests.OPS_SEC_003_AC4_TheEscrowCopyIsPrintedAndTheRotationRetiresOnlyOnceItIsSealedAsync`,
`KeyRotationTests.OPS_SEC_003_AC4_ASealBeforeTheRotationCompletesIsRefusedAsync`,
`KeyRotationTests.OPS_SEC_003_AC3_RetirementWaitsWhileValuesAreStillWrappedUnderThePreviousVersionAsync`,
`KeyRotationTests.OPS_SEC_003_ARotationNeedsANewVersionAndEveryVersionInUseAsync`,
`KeyRotationTests.OPS_SEC_003_AC5_EachStepIsRecordedWithTheVersionTheCountAndThePrincipalAsync`,
`PersonalFieldCipherTests.OPS_SEC_003_AC3_AKeyUnderARetiredVersionFailsWithANamedError`,
`LibraryStructureTests.OPS_SEC_003_AC1_OnlyTheCommandLineRunsTheRotation`.

*Chapter text that should change.* OPS-SEC-003 could say that the operator adds the
version to the secrets manager, how the seal is confirmed, what the escrow copy holds
and that retirement is the operator's removal once the command records it; chapter 10
could list the four `ops.keyrotation` actions and the `key-rotation` operation; the
runbook's section 9 could give the order above.

---

## 318. What the fingerprint key's rotation computes again, and what it keeps until the previous version retires

**Phase 9 · 2026-09-24 · Tier 3 · OPS-SEC-003 AC6, OPS-SEC-001, PRIV-RIGHT-005c, AUTH-KEY-002 AC2, AUTH-ABUSE-004 AC6, OPS-MIG-003a AC4**

*The question.* OPS-SEC-003 has the fingerprint key's rotation use "the same shape (new
version, resumable batch job, previous version usable until complete, escrow copy)"
but re-compute "every stored fingerprint rather than re-wrapping a key", and AC6 asks
for a test of it on a small fixture. OPS-SEC-001 and AUTH-KEY-002 speak of one
fingerprint key; `ISecretSource`, `AddJanus` and the command-line key document carried
one. A keyed fingerprint carries no version, so nothing said which key a stored one
was computed under, and "previous version usable until complete" needs a lookup that
matches under more than one. Four kinds of stored fingerprint have no value beside
them to compute from: a username held after erasure, and the keys of the seven
ledgers (throttle counters, send counters, send grants, sends, registration sources,
non-existence notices, callbacks), which are hashes of addresses, numbers and sources
the library never stores. A provider's link held the fingerprint of the provider's
subject and not the subject. An address an erased subject gave up stays reserved
until its undo lapses, and the subject's key that would decrypt it is destroyed.
OPS-MIG-003a AC4 holds the maintenance credential to the subject-key and progress
tables, entry 316 already widened it, and AUTH-ABUSE-004 AC6 has the send-counter
row "a keyed hash and times and nothing else".

*The readings.*

1. Keep one key and have the command switch every fingerprint at once, the
   application stopped. The previous version is not usable until complete, a stop
   mid-run leaves lookups failing for whatever was not reached, and a rotation on
   suspected exposure takes the service down for its length.
2. Version the key as the key-encryption key is versioned, record the version beside
   every stored fingerprint, look up under every version held, and have the command
   compute each fingerprint again from the value beside it; what no value stands
   behind is read under its version until it lapses or the retirement forgets it.
3. As 2, but carry what no value stands behind across by copying it under the new
   version at retirement. A keyed hash cannot be recomputed without its input, so this
   is not open.

*Chosen: 2*, the one reading that keeps the shape the chapter names; under it the
previous version is retired only once nothing still read stands under it, which keeps
most. Under 2:

- `FingerprintKeys` (the current version and every version held) replaces the single
  key in `ISecretSource.ReadFingerprintKeysAsync`, in `AddJanus` and in the key
  document, whose member is now `fingerprintKeys` with `current` and `versions` as
  `keyEncryptionKeys` has. Startup refuses a set whose current version is absent or
  any of whose versions is shorter than 32 bytes, named as
  `model.startup.kekunavailable` with `details.key` `fingerprintKeys`.
- `fingerprint_version` is added beside the fingerprint on `identifiers`,
  `identifier_removals`, `mailboxes`, `username_holds`, `authenticators` (nullable, set
  with the provider subject) and the seven ledgers. Rows written before the migration
  are version 1, which a deployment's first key document names; every write names its
  version. A write is under the current version; a lookup matches under the current
  version first and then each other version held, newest first.
- A provider's link now also holds the provider's subject encrypted under the
  subject's key (`enc_provider_subject`), so its fingerprint can be computed again.
  Erasure neutralises the fingerprint as before and leaves the ciphertext under the
  destroyed key.
- `janus rotate-fingerprint-key` runs under the maintenance credential in the shape of
  `rotate-kek` and shares its progress table: an ordered pass over the subjects in
  batches of 500, each committing with the point it reached, computing again the
  identifiers, the live reservations and the provider links of each live subject from
  the value decrypted under the subject's key; then a sweep of what the pass could not
  reach, the mailboxes' addresses included. Each fingerprint is written back only
  where it and its version still stand as they were read. A stored fingerprint its own
  value does not compute under its version stops the run as a defect.
- The command refuses a current version no later than one rotated to before, and a
  fingerprint still read under a version it was not handed, as `rotate-kek` does.
- `--sealed` retires every version but the current once the run is complete and
  nothing still read stands under a previous version: it is refused, with `pending`,
  while a username is held under one or an erased subject's reservation has not
  lapsed. On retirement, the ledger lines and the released holds under a previous
  version are deleted. A throttle counter or a send count kept under the previous
  version is lost with it, so a counter untouched since the switch starts again from
  nothing; one touched since the switch was already counted under the new version.
- The maintenance credential gains, by migration and in the serialized model: on
  `identifiers` and `identifier_removals` the key, subject, fingerprint, version and
  canonical ciphertext to read (and `expires_at` on the second), the fingerprint and
  version to write; the same shape on `authenticators` (`provider_subject`,
  `enc_provider_subject`) and on `mailboxes` (`holder`, `enc_canonical`); on
  `username_holds` the version and release instant to read and `DELETE`; on each
  ledger the version to read and `DELETE`, never the hash.
- The send-counter row holds the version beside the hash, which AUTH-ABUSE-004 AC6's
  "nothing else" does not list; it is not a value about anyone.

*Tests that pin it.*
`FingerprintRotationTests.OPS_SEC_003_AC6_ALookupMatchesUnderEveryVersionHeldAsync`,
`FingerprintRotationTests.OPS_SEC_003_AC6_EveryFingerprintIsComputedAgainUnderTheNewVersionAsync`,
`FingerprintRotationTests.OPS_SEC_003_AC6_AKilledRunResumesFromItsProgressAndComputesEachFingerprintOnceAsync`,
`FingerprintRotationTests.OPS_SEC_003_AC6_RetirementWaitsForAHeldUsernameAndForgetsWhatTheVersionHashedAsync`,
`FingerprintRotationTests.OPS_SEC_003_AC6_RetirementWaitsForTheReservationOfAnErasedSubjectAsync`,
`FingerprintRotationTests.OPS_SEC_003_AC6_AFingerprintItsValueDoesNotComputeStopsTheRunAsync`,
`FingerprintKeyRotationTests.OPS_SEC_003_AC6_TheCommandIsRefusedWithoutTheMaintenanceCredentialAsync`,
`FingerprintKeyRotationTests.OPS_SEC_003_AC6_TheEscrowCopyIsPrintedAndTheRotationRetiresOnlyOnceItIsSealedAsync`,
`FingerprintKeyRotationTests.OPS_SEC_003_AC6_ASealBeforeTheRotationCompletesIsRefusedAsync`,
`FingerprintKeyRotationTests.OPS_SEC_003_AC6_EachStepIsRecordedWithTheVersionTheCountAndThePrincipalAsync`,
`FingerprintKeyRotationTests.OPS_SEC_003_AC6_ARotationNeedsANewVersionAndEveryVersionInUseAsync`,
`KeyMaterialTests.AUTH_KEY_002_AC2_StartupFailsNamedOnARetainedFingerprintKeyShorterThanTheHash`,
`BootstrapRefusalTests.OPS_SEC_001_AC2_TheCommandRefusesAFingerprintKeyThatCannotBeUsedAsync`,
`DatabaseRoleTests.OPS_MIG_003a_AC4_TheMaintenanceRoleReachesTheFingerprintsAndNoOtherColumnAsync`,
`DatabaseRoleTests.OPS_MIG_003a_AC4_TheListedGrantsAreTheOnesTheDatabaseHoldsAsync`,
`SendLedgerTests.AUTH_ABUSE_004_AC6_TheRecordHoldsAHashAndTimesAndNothingElseAsync`,
`LibraryStructureTests.OPS_SEC_003_AC1_OnlyTheCommandLineRunsTheRotation`.

*Chapter text that should change.* OPS-SEC-001, AUTH-KEY-002 and INF-HOST-003 could
speak of the fingerprint key's versions as they speak of the key-encryption key's;
PRIV-RIGHT-005c could say a fingerprint is stored with the version it was computed
under; OPS-SEC-003 could say what a fingerprint with no value behind it becomes at
retirement and that retirement waits for held usernames and unlapsed reservations;
IDN-LIFE-012a could say the provider's subject is held encrypted beside its
fingerprint; AUTH-ABUSE-004 AC6 could admit the version; OPS-MIG-003a AC4 could list
the rights above; the runbook's "printed but not rotated" could point at the command.

---

## 319. How a protected key is changed from the server, and what the change records and raises

**Phase 9 · 2026-09-24 · Tier 3 · OPS-CFG-004 AC2, OPS-CFG-005, OPS-ALERT-001, D-071, chapter 10 section 4.8**

*The question.* OPS-CFG-004 AC2: "Changing one requires access the application itself
does not have; a command-line operation restricted to the server satisfies this as well
as a redeployment." D-071: "Whatever mechanism is used must record the change and
alert." Chapter 10 section 4.8 leaves the mechanism to the deployment ("an
infrastructure choice"). Until now a protected key was written by bootstrap alone
(entry 315), so after bootstrap no mechanism existed, and a key changed by hand in the
database would be neither recorded nor raised. Nothing says which credential the
change runs under, whether it carries a reason, whether it is priced by direction,
what a member of `stepup.enforcement.<organization>` names, or what the governing
language's "a restart to change" means for a command.

*The readings.*

1. Build no mechanism: a protected key changes by redeployment of a fresh database or
   by hand, unrecorded.
2. A command of `Janus.Cli` under the maintenance credential.
3. A command of `Janus.Cli` under the connection the piped document names, as
   bootstrap runs, taking protected keys only, each change recorded and raised.

*Chosen: 3*, the strictest that builds what D-071 requires: reading 1 leaves every
change unrecorded, and reading 2 gives the maintenance credential a third use that
OPS-MIG-003a ("exactly two uses") forbids. Under 3:

- The command is `configure`: `--<key> <value>` for each key and `--reason <text>`.
  Only a key the catalogue marks protected is taken, or one organization's member of
  the protected family, named `stepup.enforcement.<organization identifier>` with the
  identifier as the key holds it. Any other key, including every key the application
  may change, is refused with `api.request.malformed` naming `--<key>`, as bootstrap
  refuses a key it does not take (entry 308). A value its key does not admit is
  refused with the key's own code before the database is reached.
- A reason is required whatever the direction, and a change without one, or with a
  blank one, is refused with `auth.restriction.reasonrequired`, the code OPS-CFG-002
  gives a loosening without a reason. No step-up is asked: the command has no session,
  and whoever holds the server can already do worse (D-071).
- The command runs under the connection the piped document names (entry 307), as
  bootstrap does, so the protection is access to the server and to the document, not
  a database right the application lacks. The application's own credential can still
  write the settings table; the application's code cannot write a protected key
  (`config.key.protected`), and no endpoint or job reaches the command's service.
- Each value is written in one transaction with the rest, and recorded as
  `ops.configuration.changed` under the deployment-scoped principal `configure` with
  the reason `OPS-CFG-004` and the new operation `configuration`, carrying `key`,
  `before` (null where no value stood), `after`, `loosening` and the operator's
  `reason`. The direction is the key's own rule; a value set where none stood loosens
  nothing, as bootstrap's values do not (entry 315), and one whose direction cannot be
  read from what stands is a loosening (D-079b). Bootstrap now writes its values
  through the same writer, and the settings table has two writers: the configuration
  store and that writer.
- Each key changed raises `protected-setting-changed` (High) with the key in its
  details, deduplicated per key. The governing language also raises
  `governing-language-changed` (Normal): the two rows of OPS-ALERT-001 are both about
  this change, and raising both keeps most. Every named key is written, recorded and
  raised, even where its value is the one in force.
- A member of `stepup.enforcement.<organization>` for an organization the deployment
  does not hold is refused with `config.value.notallowed` naming the key and the field
  `organization`, so a mistyped identifier does not leave an operator believing a
  switch was thrown.
- The change is checked by the rule the host's start applies (LIB-HOST-001) over what
  the settings table holds once it is written, so a change that would leave the
  deployment unable to start (`hosting.location` outside Egypt with no
  `hosting.crossborderbasis`) is refused with the code and key that start would give,
  and nothing of it stays.
- A change takes effect where the library next reads the key. The governing language
  is read where a document version is published, so a change from the server takes
  effect without a restart; OPS-CFG-004 AC2 admits a command as well as a
  redeployment, and D-146's ground (not a runtime toggle of the application) holds.
- `audit.enabled`, `exfiltration.export.auditing`, `token.signature.verification` and
  `stepup.enforcement.<organization>` are read by nothing in the library: it audits,
  audits exports, verifies signatures and enforces step-up whatever they hold. A change
  to one from the server is recorded and raised and changes no behaviour. The chapters
  say these may not be turned off through the application and nowhere what turning
  one off does, and honouring an off-switch would grant more than ignoring it.
- Success prints `{"changed":[...]}`, the keys in the order named and nothing of their
  values; a refusal is the JSON line of entry 308 on standard error with exit code 1.

*Tests that pin it.*
`ConfigureTests.OPS_CFG_004_AC2_AProtectedKeyIsChangedFromTheServerWrittenDownAndRaisedAsync`,
`ConfigureTests.OPS_CFG_004_TheGoverningLanguageIsRaisedUnderItsOwnConditionAsync`,
`ConfigureTests.OPS_CFG_004_AnOrganizationsStepUpEnforcementIsSwitchedFromTheServerAsync`,
`ConfigureTests.OPS_CFG_004_AKeyTheApplicationChangesIsRefusedAsync`,
`ConfigureTests.OPS_CFG_004_AChangeWithoutAReasonIsRefusedAsync`,
`ConfigureTests.OPS_CFG_004_AValueItsKeyDoesNotAdmitIsRefusedAsync`,
`ConfigureTests.OPS_CFG_004_AChangeThatLeavesTheDeploymentUnableToStartIsRefusedAsync`,
`LibraryStructureTests.OPS_CFG_004_AC2_OnlyTheCommandLineWritesAProtectedKey`,
`LibraryStructureTests.OPS_CFG_002_OnlyTheConfigurationAdministrationWritesARuntimeSetting`.

*Chapter text that should change.* OPS-CFG-004 could name the command, say that it
takes a reason and no step-up, and say what turning off each switch does, if
anything; chapter 10 section 4.8 could say the mechanism is the command rather than
an infrastructure choice; OPS-CFG-005 could say how a change from the server is
recorded; IDN-PRIN-001 could list `configure` among the system principals;
OPS-ALERT-001 could say whether the governing language raises one alert or both;
CONV-LAYOUT-001 could list `configure` among what `Janus.Cli` carries, beside bootstrap
and key rotation.

---

## 320. The library carries its own events: a row on the transaction, and a publisher that offers it to the host's consumers

**Phase 9 · 2026-09-24 · Tier 2 · LIB-API-001, LIB-HOST-001 AC1 and AC3, CONV-DESIGN-002, IDN-LIFE-003a, INF-BG-001, D-162 items 22 and 29, entry 121**

*The question.* Entry 121 left the row a publication is recorded in, its marking and
the degradation on exhaustion to "the publisher of phase 9", and noted that no
implementation of `IEvents` was shipped. Every operation that emits an event publishes
through `IEvents`, and the library registered nothing for it, so a host declaring only
what LIB-HOST-001 lists could resolve none of those operations. CONV-DESIGN-002 has the
events "raised from the committed outbox row"; D-162 item 29 has the row stay unmarked
on failure, with the degradation raised, and marked on success; `IEventConsumer<TEvent>`
in `Janus.Core` is "what a consumer registers" and nothing called it. No chapter names
the table, says how a consumer is known, whether a consumer that took an event is
offered it again when another refused it, what order the events keep, or what becomes
of a marked row.

*The readings.*

1. `IEvents` is the host's to implement, as the test hosts treated it: the library
   calls it inside the transaction and keeps no row.
2. The library implements `IEvents` by writing a row onto the operation's transaction,
   and a background publisher offers each committed row to the host's
   `IEventConsumer<TEvent>` registrations, retries under `outbox.retry.*`, marks the row
   once every consumer has taken it, and fails it and raises `degradation` once the
   budget is spent.

*Chosen: 2.* Reading 1 adds a declaration LIB-HOST-001 does not list (AC1, AC3), leaves
`IEventConsumer<TEvent>` uncalled, and leaves "the committed outbox row" and "the row is
marked" with no row. Under 2:

- The row is `identity.events`: the event's name in chapter 10 section 5b as `kind`,
  the event itself as generated JSON with every value of the vocabulary written by
  name, the attempts, the next attempt, the consumers that have taken it, `published_at`
  (the mark) and `failed_at`. The application's credential holds all four rights on it.
- A publication succeeds once the row is on the transaction, and a rollback leaves no
  row, so no consumer hears of a change that did not happen. A consumer's refusal never
  fails the operation; a failure to write the row still does (entry 121, point 1).
- The publisher is the job `events` (reason IDN-LIFE-003a, operation `delivery`, every
  `outbox.poll.interval`) in `Janus.Hosting`, where D-162 item 22 puts the outbox
  publisher. A consumer is an `IEventConsumer<TEvent>` registered for the event's kind,
  known by the full name of its type. One that took an event is not offered it again;
  one that refused it or threw is, after `outbox.retry.initial` multiplied by
  `outbox.retry.factor` per attempt with full jitter. The row is marked once every
  registered consumer has taken it, which is at once where none is registered.
- After `outbox.retry.maxattempts` the row is failed and `degradation` (Normal) is
  raised, scoped to the event's kind so that OPS-ALERT-002 keeps a consumer failing
  every event of one kind to one alert inside `alerting.dedupe.window`, naming the row,
  the attempts and the consumers still outstanding, in the transaction that records
  the failure. A failed row is not offered
  again. There is no manual completion path: IDN-LIFE-003a requires one for erasure,
  restriction and takedown, which keep their own outbox.
- Events are offered by the instant they were raised. Events of one millisecond carry
  no order among themselves, and one a consumer refused waits out its delay while
  later ones reach it: delivery promises each consumer the event, not its place.
- Marked rows are kept, and nothing removes them. D-162 item 29 says the row is marked,
  and no chapter names a retention for it.
- The library's registration gives way to an `IEvents` a host registered before it, as
  the replaceable defaults do; such a host bypasses the row and the publisher.

*Tests that pin it.*
`PendingEventsTests.LIB_API_001_EveryEmittedEventReadsBackAsItWasRaisedAsync`,
`PendingEventsTests.CONV_DESIGN_002_AnEventWaitsOnlyOnceItsTransactionCommitsAsync`,
`PendingEventsTests.IDN_LIFE_003a_AMarkedOrFailedEventIsNotReadAgainAsync`,
`EventPublisherTests.CONV_DESIGN_002_AnEventReachesEveryConsumerOfItsKindAndIsMarkedAsync`,
`EventPublisherTests.IDN_LIFE_003a_OnlyAConsumerThatRefusedIsOfferedTheEventAgainAsync`,
`EventPublisherTests.IDN_LIFE_003a_AnEventWhoseBudgetIsSpentFailsAndRaisesDegradationAsync`,
`EventPublisherTests.LIB_API_001_EveryEmittedEventHasItsConsumers`,
`BackgroundJobsTests.INF_BG_001_AC1_EveryJobRunsWithoutAPersonAsync`.

*Chapter text that should change.* LIB-API-001 or CONV-DESIGN-002 should say that the
library carries its events through a row of its own and that a host consumes them by
registering `IEventConsumer<TEvent>`; INF-BG-001 should name the event publisher beside
the outbox publisher; chapter 10 should name a retention for a marked row, or say that
a marked row is removed.

---

## 321. What a denial spike counts, and when it is raised

**Phase 9 · 2026-09-24 · Tier 2 · AUTHZ-GATE-004, OPS-ALERT-001, OPS-ALERT-002, chapter 10 section 4.5 (`alerting.denials.threshold`)**

*The question.* Chapter 10 gives `alerting.denials.threshold` as "50 per `PT10M`",
"integer denials per actor in a fixed ten-minute window". Nothing says where a fixed
window begins, who the actor is for a refusal that names no one (a request under no
account, or a system principal), or whether the condition is raised at the number or
above it. The gate had no alert port, so the condition was raised nowhere.

*The readings.*

1. Windows begin at the first refusal counted; refusals naming no actor are not
   counted; the condition is raised at the number.
2. Windows are fixed on the clock; refusals naming no actor are counted together; the
   condition is raised above the number.

*Chosen: 2.* The windows are ten minutes long and counted from the Unix epoch in UTC,
so every instance of the library places a refusal in the same window, which "fixed"
asks. The actor is the acting subject the refusal records; under impersonation that is
the person acting, not the account whose authority is used. Every refusal that names no
acting subject is counted together, raised with no scope, so a run of refusals to
requests under no account is raised like anyone's; leaving them out would make the one
run never raised the one a probe produces, and counting them raises more. The
condition is raised once the window holds more refusals than the threshold, as the
key's own description reads it, on each refusal past it, and OPS-ALERT-002 keeps that
to one alert per actor inside `alerting.dedupe.window`; the details carry the count.
The count is read from the audit trail the refusal has just been written to, so it
needs no counter of its own. The gate reaches the alert router through a port of its
own (`IAccessAlerts`), as the privacy area does, because an area project reaches no
other area project (CONV-LAYOUT-001).

A refusal recorded inside a transaction that then rolls back is neither kept nor
counted nor raised; this is the lost-audit-row defect the phase 9 report names, not a
choice made here.

*Tests that pin it.*
`GateBehaviourTests.OPS_ALERT_001_AC1_ADenialSpikeOfOneActorIsRaisedAsync`.

*Chapter text that should change.* Chapter 10 section 4.5 should say where a fixed
window begins, who the actor is for a refusal that names no one, and whether the
condition is raised at the number or above it.

---

## 322. How a message no transport took is carried again

**Phase 9 · 2026-09-24 · Tier 2 · D-022, INF-BG-001, AUTH-ABUSE-004, INT-SMS-004, IDN-ATTR-001, IDN-PRIN-003, D-162 item 23**

*The question.* D-162 item 23 has every send written to the library's outbox and
"delivered by the worker under `outbox.retry.*` with status recorded", and says that
until the publisher exists the send path attempts once and leaves the row as recorded.
It does not say whether the send path still makes the first attempt once the worker
exists, what a retry counts against or whether the restrictions judge it again, which
languages a retry carries where a transport took some of them, what becomes of the row
when the budget is spent, or what the `degradation` alert is raised under.

*The readings.*

1. The send path only writes the row and answers at once; the worker makes every
   attempt.
2. The send path makes the first attempt once its row is written and answers with its
   outcome; the worker makes the rest.

*Chosen: 2.* Every caller acts on the answer: the alert router moves to the next
destination when a transport refuses, the loss report records whether anyone was told,
and a refusal by a restriction has to reach the person with its `retryAt`. Reading 1
would answer success for a message no transport has taken. The row is written before
the attempt with its next attempt set one `outbox.retry.initial` ahead, so the worker
does not carry a message the send path is carrying; an attempt that leaves a language
untaken is the first of `outbox.retry.maxattempts`, scheduled as the event outbox is
(initial times factor per further attempt, full jitter).

A retry is judged by the restrictions again, as they stand when it is made, and one
they refuse waits as any refused attempt does. A refused delivery counts against no
bucket (AUTH-ABUSE-004 AC2), so while a transport is down every send is admitted; not
judging retries again would let a transport that comes back carry at once everything
the restrictions would have held. What a carried retry counts against is read the same
way. The gateway floor holds a retried text message as it holds any; an alert is
exempt, as on the send path (OPS-ALERT-003).

A retry carries only the languages no transport has taken; the row keeps them, outside
the encrypted column, because a row holds more than one language only where the
message goes out in every language the deployment declares, which says nothing of the
recipient.

When the budget is spent the row is removed and `degradation` is raised under the
scope `send:<channel>`, so OPS-ALERT-002 keeps a failing transport to one alert per
channel inside `alerting.dedupe.window`. The details name the message by its
identifier, its message kind, its channel and the attempts, never its destination.
Removing rather than marking the row keeps no record of where somebody was written to
(IDN-PRIN-003): the alert, not the row, is the signal. The job is `sends`, run every
`outbox.poll.interval`.

Where a caller undertakes a send inside its own transaction, the row, and the first
attempt with it, precede that transaction's commit; this is the observation the
phase 9 report names, not a choice made here.

*Tests that pin it.*
`SendingServiceTests.D_022_ATransportRefusalLeavesTheMessageRecordedAsync`,
`SendingServiceTests.D_022_ARefusedMessageIsCarriedOnceItsRetryIsDueAsync`,
`SendingServiceTests.AUTH_ABUSE_004_AC2_ARetryIsJudgedByTheRestrictionsAgainAsync`,
`SendingServiceTests.IDN_ATTR_001_ARetryCarriesOnlyTheLanguagesStillOwedAsync`,
`SendingServiceTests.D_022_AMessageWhoseBudgetIsSpentIsRemovedAndRaisesDegradationAsync`,
`SendingServiceTests.INT_SMS_004_AC2_ARetryIsHeldBelowTheFloorAsync`,
`SendOutboxTests.D_022_AnAttemptReadsBackAsItWasRecordedAsync`,
`SendOutboxTests.D_022_OnlyAMessageWhoseAttemptIsDueIsReadAsync`.

*Chapter text that should change.* INF-BG-001 could say that the send path makes the
first attempt and the worker the rest; AUTH-ABUSE-004 could say that a retry is judged
again; chapter 10 section 4 could name send delivery beside IDN-LIFE-003a on the
`outbox.*` rows and say that a spent send is removed and raises `degradation` per
channel.

---

## 323. The licence and maintenance log endpoints, and who may use them

**Phase 9 · 2026-09-24 · Tier 2 · OPS-MAINT-001, OPS-ALERT-001, OPS-ALERT-002, OPS-MIG-003, `09` section 8a**

*The question.* OPS-MAINT-001 has the system store licence and permit expiry dates,
warn `maintenance.expiry.warninglead` ahead, and keep a maintenance log "in the
management application" whose entries "cannot be deleted through the application".
Chapter 09 section 8a lists only `PUT /admin/compliance/licences` under
`compliance:manage`. It does not say how the management application reads the
licences back, how an entry reaches the log or is read, who may record one, where a
licence's identifier comes from, whether a licence that has lapsed unrenewed is still
warned of, or what runs the warning.

*The readings.*

1. Only the listed route exists; the log and the reads are left to a later chapter.
2. The listed route, a read of the licences, and a read and an append of the log, all
   under the permission the section names.

*Chosen: 2.* AC1 has the dates "surfaced" and AC3 has the log "in the management
application", and neither can be met without a way to read and to append. The routes
are `GET` and `PUT /admin/compliance/licences` and `GET` and `POST
/admin/compliance/maintenance`, each under `compliance:manage` and a session, the
narrowest permission the section names; no route changes or removes an entry, and the
application's database role holds `SELECT, INSERT` only on the log table, as on the
audit records (OPS-MIG-003), so AC3's "cannot be deleted" holds below the endpoint as
well.

`PUT` replaces the whole list and is idempotent: the identifier is the caller's, and
two licences under one identifier are refused as `api.request.malformed` at
`licences` rather than one chosen. A log entry's actor is the signed-in subject, never
a value the body names, and an entry dated after now has not been performed and is
refused as malformed at `performedAt`. The views carry the D-153 value shapes and
nothing more: an entry is read without its storage identifier.

The warning is the job `licence-expiry`, run daily as a monitoring operation. Every
licence whose expiry lies within the lead raises `expiry-approaching` under the scope
`licence:<id>`, a lapsed one included, so the warning does not stop when the date
passes unrenewed; OPS-ALERT-002 keeps each licence to one alert inside
`alerting.dedupe.window`. The details carry the identifier, kind, name and expiry. The
feature sits in `Janus.Authentication` beside the alerting it raises on.

*Tests that pin it.*
`MaintenanceRecordsTests.OPS_MAINT_001_EveryOperationAnswersToComplianceManageAsync`,
`MaintenanceRecordsTests.OPS_MAINT_001_AC1_TheExpiryDatesAreStoredAndReadBackAsync`,
`MaintenanceRecordsTests.OPS_MAINT_001_TwoLicencesUnderOneIdentifierAreRefusedAsync`,
`MaintenanceRecordsTests.OPS_MAINT_001_AC3_AnEntryIsDatedAndCarriesThePersonAskingAsync`,
`MaintenanceRecordsTests.OPS_MAINT_001_AC3_ATaskDatedAfterNowIsRefusedAsync`,
`LicenceExpiryTests.OPS_MAINT_001_AC2_ALicenceInsideTheLeadRaisesExpiryApproachingAsync`,
`LicenceExpiryTests.OPS_MAINT_001_AC2_ALicenceThatLapsedUnrenewedIsStillRaisedAsync`,
`LicenceExpiryTests.OPS_MAINT_001_AC2_TheLeadIsTheConfiguredOneAsync`,
`MaintenanceEndpointTests.OPS_MAINT_001_AC1_TheExpiryDatesAreStoredAndReadBackAsync`,
`MaintenanceEndpointTests.OPS_MAINT_001_AC3_ARecordedTaskIsDatedAndCarriesThePersonAskingAsync`,
`MaintenanceEndpointTests.OPS_MAINT_001_ABodyThatCannotBeReadIsMalformedAsync`,
`MaintenanceEndpointTests.OPS_MAINT_001_AnAccountWithoutComplianceManageIsRefusedAsync`,
`MaintenanceStoreTests.OPS_MAINT_001_AC1_TheExpiryDatesReadBackAsTheyWereReplacedAsync`,
`MaintenanceStoreTests.OPS_MAINT_001_AC3_AnEntryReadsBackDatedAndWithItsActorAsync`,
`DatabaseRoleTests.OPS_MAINT_001_AC3_TheApplicationCannotChangeOrRemoveALogEntryAsync`.

*Chapter text that should change.* Chapter 09 section 8a could list `GET
/admin/compliance/licences` and `GET` and `POST /admin/compliance/maintenance` beside
the `PUT`; OPS-MAINT-001 could say that the log is append-only at the database role and
that a lapsed licence stays warned of.

---

## 324. When the holiday list has run out, and what looks

**Phase 9 · 2026-09-24 · Tier 2 · PRIV-RIGHT-002, D-142, OPS-ALERT-001, `10` row `privacy.holidays`**

*The question.* D-142 item 4 and the `10` row for `privacy.holidays` raise a Normal
alert "when no listed date lies beyond `maintenance.expiry.warninglead`", and
PRIV-RIGHT-002 calls it "a list running out". Neither says whether an empty list,
which is the default, has run out, in which zone "beyond" is judged, or what looks.

*The readings.*

1. Only a list that holds dates can run out; an empty list raises nothing.
2. The words as written: an empty list lists no date beyond the lead and is raised.

*Chosen: 2.* It is what the sentence says, and it is the reading that warns: a
deployment that never lists a holiday counts every one as a working day, which is
compliant but is what the alert exists to bring to a person's attention. OPS-ALERT-002
keeps it to one alert inside `alerting.dedupe.window`, so it recurs at that pace until
a date is listed.

A date lies beyond the lead when it falls after the calendar day that now plus the
lead falls on in `privacy.calendar.timezone`, the zone every holiday is determined in
(D-153); a date on that day itself is not beyond it. The condition names no one and
carries the horizon instant. The look is the job `holiday-list`, run daily as a
monitoring operation beside `licence-expiry`, since the lead it measures is counted in
days.

*Tests that pin it.*
`HolidayListWatchTests.OPS_ALERT_001_AC1_AHolidayListRunningOutIsRaisedAsync`,
`HolidayListWatchTests.PRIV_RIGHT_002_AnEmptyHolidayListIsRaisedAsync`,
`HolidayListWatchTests.PRIV_RIGHT_002_AListReachingPastTheLeadRaisesNothingAsync`,
`HolidayListWatchTests.PRIV_RIGHT_002_TheLeadIsTheConfiguredOneAsync`.

*Chapter text that should change.* The `10` row for `privacy.holidays` could say that
an empty list is raised and that "beyond" is judged by calendar day in
`privacy.calendar.timezone`; INF-BG-001 could name the holiday-list look among the
jobs.

---

## 325. Where the IP-to-city file comes from, what it looks like, and how a process holds it

**Phase 9 · 2026-09-24 · Tier 2 · INT-GEN-006, AUTH-SESS-013, LIB-EXT-001, INF-BG-001, D-162 section B (entry 117)**

*The question.* INT-GEN-006 resolves the city on a session from "a local IP-to-city
database: a file bundled with the library or supplied by the host, read in process,
never a call to a third party", refreshed every `location.database.refresh` by a
background job, stale beyond `location.database.maxage`. No chapter names the file's
format, how a host supplies it, where a refresh reads it from, or how a process that
has just started resolves an address before the job next runs. CONV-DESIGN-008 names
no package that reads any published database format.

*The readings.*

1. The library bundles a database and a reader for a published format.
2. The host supplies the file through a public port, in a format the library defines;
   the library reads it whole into memory and resolves against the copy.
3. The host supplies a path through a new setting.

*Chosen: 2.* Reading 1 needs a data licence and a package neither the chapters nor
CONV-DESIGN-008 give; reading 3 adds a key to `10`. Reading 2 is the shape the library
already gives the deployment's DNS (`IDnsResolver`): one public interface,
`ILocationSource`, with one method that opens the file as it now stands and answers a
`Result<Stream>`. It is optional; with none registered no session carries a location
and `degradation` is raised, as before. Opening it reads a file the deployment holds
and reaches no network; keeping that file current is the deployment's.

The format is documented on the interface: UTF-8 text, a line `# YYYY-MM-DD` giving
the date the data was produced, and one tab-separated range per line (first address,
last address, country as ISO 3166-1 alpha-2 or nothing, city or nothing, latitude and
longitude of the city present exactly where the city is). A file is taken whole or not
at all: no date, a line that cannot be read, ranges of mixed family or reversed, or two
ranges that overlap refuse it, because a file read in part would resolve some
addresses and silently not others. Its age is judged from its own date, not from when
it was read.

A process holds one parsed copy for every scope. It reads the file the first time it
resolves an address, so a restart does not wait for the next refresh, and after that
only the job `location-database` (INF-BG-001, every `location.database.refresh`, a
monitoring operation) reads it again and replaces the copy whole. A refresh that could
not open or read the file raises `degradation` under `location.database.refresh` and
keeps the copy it had, until that copy is stale; a stale copy answers no location and
raises under `location.database.stale`; no copy at all raises under
`location.database.absent`. A failed refresh is surfaced by that alert and the run
counts as run, so `background-job-failed` stays the signal of a job that could not run.
The copy is per process; a deployment runs the worker in the process that serves
requests.

The internal resolver port of entry 117 now answers the city and country and, beside
them, where the city lies, which OPS-ALERT-007 measures by; the location a session
shows is still `{ city, country }` and no public signature carries a place.

*Tests that pin it.*
`LocationDatabaseTests.INT_GEN_006_AC1_TheResolverHoldsNothingItCouldCallOutWith`,
`LocationDatabaseTests.INT_GEN_006_AC1_EveryAddressIsResolvedAgainstTheCopyHeldAsync`,
`LocationDatabaseTests.INT_GEN_006_AC3_TheCityIsWhatTheFileSaysOfTheAddressAsync`,
`LocationDatabaseTests.INT_GEN_006_AC3_WithNoFileAvailableNoLocationIsAnsweredAsync`,
`LocationDatabaseTests.INT_GEN_006_AC2_TheMissingFileSurfacesAsADegradationAsync`,
`LocationDatabaseTests.INT_GEN_006_AC2_AFailedRefreshSurfacesAsADegradationAsync`,
`LocationDatabaseTests.INT_GEN_006_AC2_ARefreshReplacesTheCopyHeldAsync`,
`LocationDatabaseTests.INT_GEN_006_AFileThatCannotBeReadWholeIsRefusedAsync` (seven cases),
`LocationDatabaseTests.INT_GEN_006_AFileWithNoDateIsRefusedAsync`,
`LocationDatabaseTests.INT_GEN_006_AStaleFileAnswersNoLocationAndIsRaisedAsync`,
`BackgroundJobsTests.INF_BG_001_AC1_EveryJobRunsWithoutAPersonAsync`.

*Chapter text that should change.* INT-GEN-006 could name `ILocationSource` and the
file's format, and say that a refused file is refused whole; LIB-HOST-001 could list
the location file among the optional host declarations; `10` section 4 could say that
the file's age is judged from its own date.

---

## 326. When two sessions are looked at together, and what is kept to measure them

**Phase 9 · 2026-09-24 · Tier 2 · OPS-ALERT-007, AUTH-SESS-013, OPS-ALERT-002, CONV-DESIGN-005**

*The question.* OPS-ALERT-007 raises `concurrent-sessions-implausible` when two sessions
of one account are both used inside `alerting.sessions.window` and their resolved
cities lie further apart than `alerting.sessions.distance` or in different countries;
the same city, or an unresolved location on either side, never alerts. It does not say
at which use the two are compared, how the distance between two cities is measured,
what a session keeps to measure it by, whether sessions standing on one record count
as two, what the alert names, or what becomes of the use when the alert cannot be
raised.

*The readings.*

1. Compare at every use of every session against every other.
2. Compare only when a use begins a stretch: a session begun or derived, a use from a
   city other than the one it was last used from, or a use after a pause longer than
   the window.

*Chosen: 2.* Every pair that reading 1 would raise is raised by reading 2 at the later
of the two stretches, since the earlier session was then used inside the window, and
reading 2 reads the account's sessions once a stretch rather than once a request. A
city is resolved when the database named it; a place with a country and no city is
unresolved and never raises. Two places are the same city when country and city match
without regard to case. The distance is the great-circle distance between the two
cities' coordinates on a sphere of the Earth's mean radius; it is compared only where
the countries do not already differ. Sessions standing on one record are one session
held more than one way and are never compared with each other.

To measure a distance a session keeps, beside the city and country it shows, where
that city lies: the coordinates of the city from the location file (entry 325), stored
in the place under the person's key and gone with it (AUTH-SESS-013 AC4). They are no
finer than the city, never shown and never exported. A place written before they were
kept reads back without them and is unresolved for this purpose.

The alert is scoped to the account, so OPS-ALERT-002 keeps one account to one alert
inside `alerting.dedupe.window`, and its details name the account and the two sessions
and neither place, which stays the person's. The look runs in the transaction that
records the use; an alert that cannot be raised fails the use, as a degradation the
resolver cannot raise does (CONV-DESIGN-005 AC1).

*Tests that pin it.*
`SessionServiceTests.OPS_ALERT_007_AC1_SimultaneousSessionsFromImplausibleOriginsAlertAsync`,
`SessionServiceTests.OPS_ALERT_007_AC1_CitiesFurtherApartThanTheDistanceAlertAsync`,
`SessionServiceTests.OPS_ALERT_007_AC2_OrdinaryMultiDeviceUseDoesNotAsync`,
`SessionServiceTests.OPS_ALERT_007_ASessionTakenUpAgainFarAwayAlertsAsync`,
`SessionServiceTests.OPS_ALERT_007_TheDistanceIsTheConfiguredOneAsync`,
`SessionStoreTests.OPS_ALERT_007_WhereACityLiesReadsBackAsWrittenAsync`.

*Chapter text that should change.* OPS-ALERT-007 could say when two sessions are
compared, that distance is great-circle between the cities, that sessions on one
record are one session, and that the alert names the sessions and not the places;
AUTH-SESS-013 could say that the place kept under the person's key includes where the
city lies.

---

## 327. Where read volume is counted from, and how a person's normal is kept

**Phase 9 · 2026-09-24 · Tier 3 · OPS-ALERT-005, AUTHZ-GATE-002, LIB-API-004, PRIV-RIGHT-005c**

*The question.* OPS-ALERT-005 (D-153) counts "one record returned to the actor by a
gate-filtered query or an export" per calendar day in `privacy.calendar.timezone`, and
raises `read-volume-anomaly` when today's count exceeds `exfiltration.readvolume.factor`
times the actor's mean daily count over `exfiltration.readvolume.baselinewindow` and
exceeds `exfiltration.readvolume.minimum`. The gate returns a predicate the host runs
against its own tables (AUTHZ-GATE-002), so the library never sees how many rows came
back. The chapters do not say how the count reaches the library, who an actor is when
one person acts for another or when a system principal runs, how the mean treats a day
with no reads or a person with no history, when the mean is computed, or what is kept
and for how long.

*The readings.*

1. The library counts nothing it cannot see; the condition is left to the host.
2. The library exposes a port the host reports each filtered query's and each export's
   row count to, and counts and judges them itself.
3. As 2, and the library also counts the records its own staff routes return.

*Chosen: 2, strictest where the readings differ on what is watched.* Reading 1 leaves a
security condition of the table without a raise site, which OPS-ALERT-001 AC1 does not
allow. A public `IReadVolume` (`Janus.Core`) takes the access context and the number of
records one gate-filtered query or one export returned; a negative number is
`api.request.malformed` naming `records`. Reading 3 is not taken: no library route runs
a gate-filtered query (the library's own reads check a permission on one record or
list the library's own administrative tables), and `/privacy/export` returns only the
actor's own records and is outside the staff export controls (`10`,
`exfiltration.export.stepuprequired`).

The actor is the acting person (`AccessContext.Acting`): a staff member acting for a
customer is counted, the customer is not. Work a system principal does is nobody's
reading and is not counted. The day is the calendar day of the instant in
`privacy.calendar.timezone`, the day the privacy clock counts. The mean is the sum of
the actor's counts on the days of the window before today divided by the window's
length in days, so a day without reads counts as nothing read and today never raises
its own baseline; a person with no count in the window has a mean of nothing, so only
the minimum stands between their first busy day and the alert, as D-153 intends. Both
comparisons are strict.

The means are recomputed once a day by the `read-volume-baseline` job (Monitoring,
daily, in one transaction), which also forgets every count older than the window, so a
count is kept no longer than the window it serves. A count and a mean hold the actor's
identifier and a number and nothing of what was read; PRIV-RIGHT-005c's erasure leaves
them as it leaves every row holding the identifier alone, and they lapse with the window.
With `exfiltration.readvolume.alerting` off, counts are still kept so the mean is whole
when it is turned back on; nothing is raised. An unreadable factor, minimum or flag
falls back to its default rather than silencing the condition. The alert is scoped to
the actor and its details are `{actor, records, dailyMean}`.

*Tests that pin it.*
`ReadVolumeTests.OPS_ALERT_005_AC1_AnActorReadingFarBeyondTheirOwnPatternRaisesAsync`,
`ReadVolumeTests.OPS_ALERT_005_AC2_AnActorWhoseNormalIsHighDoesNotAlertAsync`,
`ReadVolumeTests.OPS_ALERT_005_TheMinimumKeepsAFirstBusyDaySilentAsync`,
`ReadVolumeTests.OPS_ALERT_005_TheFactorAndMinimumAreTheConfiguredOnesAsync`,
`ReadVolumeTests.OPS_ALERT_005_AlertingOffCountsAndRaisesNothingAsync`,
`ReadVolumeTests.OPS_ALERT_005_TheActorIsCountedNotThePersonActedForAsync`,
`ReadVolumeTests.OPS_ALERT_005_ASystemPrincipalIsNotCountedAsync`,
`ReadVolumeTests.OPS_ALERT_005_ANegativeCountIsMalformedAsync`,
`ReadVolumeTests.OPS_ALERT_005_ADayIsTheCalendarDayInTheZoneAsync`,
`ReadVolumeTests.OPS_ALERT_005_TheMeanIsTakenOverTheWindowBeforeTodayAsync`,
`ReadVolumeTests.OPS_ALERT_005_TheWindowIsTheConfiguredOneAsync`,
`ReadVolumeStoreTests.OPS_ALERT_005_ReportsOnOneDayAddToOneCountAsync`,
`ReadVolumeStoreTests.OPS_ALERT_005_TheMeanIsRecomputedOverTheWindowBeforeTodayAsync`,
`ReadVolumeStoreTests.OPS_ALERT_005_ARecountReplacesEveryMeanAsync`.

*Chapter text that should change.* OPS-ALERT-005 could say that the host reports each
filtered query's and export's row count through `IReadVolume` (and `07` list it among
the ports a host calls), that the actor is the acting person and a system principal is
not counted, that the mean is over every day of the window before today with a day
without reads counted as nothing, and that counts are kept for the window only.

---

## 328. How a host's action bound to a step-up gate is met

**Phase 9 · 2026-09-24 · Tier 3 · AUTH-STEP-001, AUTH-STEP-002, AUTH-STEP-003, AUTHZ-GATE-005, LIB-HOST-004**

*The question.* A host binds its own action to a step-up gate through the model builder
(`StepUpGate`, D-160), naming a gate of `10` section 5a or one of its own. Every such
action was refused with `auth.stepup.required` on every call: the gate never read a
session, since phase 2 left the session to phase 3 and phase 3 built the session's
judgement for the library's own actions only. AUTH-STEP-002 AC3 ("a subject whose
session meets the gate within the maximum age is not challenged") and D-160's
`requires` ("an action whose bound gate the session does not currently satisfy") never
held for a host's action, and the list filter admitted rows under a bound action the
check refused. The chapters do not say which session judges a host's gate, what a gate
the host names costs when no policy states values for it, or what the assurance
provider's level is compared with.

*The readings.*

1. Keep refusing every bound action of a host.
2. Judge the gate against the library's session that carries the request, where it is
   the acting person's own; a gate named in section 5a costs that action's values, and
   a gate the host names costs (a) the system default gate, (b) the dearest gate of the
   person's policy, or (c) nothing it can be met by.

*Chosen: 2(b).* Reading 1 fails AUTH-STEP-002 AC3 for every host. Of the three costs,
(a) can be cheaper than what the person's own organization asks at its gates, and (c)
is reading 1 again; (b) never asks less than a named gate of the same policy would. The
session judged is the one the request resolved to, and only where its account is the
context's acting person: a context acting for someone else is judged by the actor's
session, and a system principal, a call carrying no session of the library (a token, a
background job) or a context the session does not belong to is not judged by it. That
unjudged case is as before: `auth.stepup.unavailable` with no assurance provider, and
`auth.stepup.required` with one. The provider's level is still not compared with any
gate, because it reports a level alone and a gate is three values (AUTH-STEP-002); no
level can show a phishing-resistance requirement or a maximum age met.

The refusal carries what a gate on the library's own surface carries (`action`,
`level`, `phishingResistant`, `outcome`, `combinations`), so the frontend prompts the
same way and steps up at `POST /auth/step-up`. A gate is judged once a request and once
a capability page, never once a row (AUTHZ-GATE-005 AC1). The list filter and the SQL
fragment ask the bound gate as the check does, so the two renderings of one rule agree
(AUTHZ-GATE-001); a restricted account's list still matches nothing before any gate is
asked.

*Tests that pin it.*
`StepUpGatesTests.AUTH_STEP_002_AC3_ASessionThatMeetsAHostsGateIsNotChallengedAsync`,
`StepUpGatesTests.AUTH_STEP_002_AnotherPersonsSessionMeetsNoGateAsync`,
`StepUpGatesTests.AUTH_STEP_003_AC2_TheDenialIsDistinguishableFromAnOrdinaryOneAsync`,
`StepUpGuardTests.AUTH_STEP_002_AGateNamedInTheCatalogueCostsItsOwnValuesAsync`,
`StepUpGuardTests.AUTHZ_GATE_005_AGateTheHostNamesCostsTheDearestGateOfThePolicyAsync`,
`StepUpGuardTests.AUTH_STEP_002_AnotherPersonsSessionIsRefusedAsync`,
`GateBehaviourTests.AUTH_STEP_002_AC3_ASessionThatMeetsAHostsGateIsNotChallengedAsync`,
`GateBehaviourTests.AUTH_STEP_001_AListUnderABoundActionAsksForStepUpAsync`,
`GateBehaviourTests.LIB_HOST_004_AC2_ABoundActionIsDeniedWithNoAssuranceProviderAsync`.

*Chapter text that should change.* AUTH-STEP-002 or AUTHZ-GATE-005 could say that a
host's gate is judged against the acting person's own session, what a gate the host
names costs (or let section 4.1a's `gates` carry host-named keys), and that a list
filter asks the bound gate; LIB-HOST-004 could say what the provider's level is
compared with, or return the three values a gate needs.

---

## 329. What an export operation is, and what it asks

**Phase 9 · 2026-09-24 · Tier 3 · OPS-ALERT-006, D-045, OPS-CFG-004**

*The question.* OPS-ALERT-006 says export operations "SHALL be defined, gated by
step-up, individually audited, and rate-limited", and its AC1 that "export" is an
enumerated set of operations. `10` gives `exfiltration.export.stepuprequired` (staff
bulk export only, D-148), `exfiltration.export.ratelimit` (5 per hour) and
`exfiltration.export.auditing` (protected). No chapter says who enumerates the set,
where an export passes through the library, what counts as one export, whom the limit
counts, what the audit row carries, or what a system principal meets at the step-up
gate. Entry 319 recorded that the three keys were read by nothing.

*The readings.*

1. The set is the library's own: nothing of the library's own surface is a staff bulk
   export, so the set is empty and the keys stay unread.
2. The set is the host's: every permission the host declares whose action is `export`
   (the action AUTHZ-GATE-006 already names as reading) is an export operation, and the
   gate, through which every exercise of a host's permission passes (AUTHZ-SEAM-001),
   applies step-up, the limit and the audit row. What counts as one export is
   (a) each admitted check, list filter or SQL fragment exercising the permission, or
   (b) each record the host reports it returned.
3. As 2, with a separate host-facing call the host makes when it exports, outside the
   gate.

*Chosen: 2(a).* Reading 1 leaves every key of D-045 dead and the item unbuilt.
Reading 3 is a second path around the gate for the one kind of action the item exists
to control, and a host that forgets the call exports ungated. Reading 2 puts the
enumeration where AC1 puts it, in declared names, and never in a judgement of volume.
Of the two counts, (b) needs the host's report, which `IReadVolume` (entry 327) already
carries for volume and which the host could omit; (a) is counted where the gate admits
the operation, so an export the host runs is counted whatever it then returns.

What is built:

- An export is a host-declared permission whose action is `export`.
  `AuthorizationModel.Exports` holds the set; the library declares none, and a
  person's copy of their own records (`/privacy/export`) is no permission and is
  gated as before (D-141, D-148).
- Step-up: while `exfiltration.export.stepuprequired` is on (default, and assumed on
  where unreadable), an export the host bound to no gate asks for a gate named by the
  export itself, which costs the dearest gate of the person's policy (entry 328). A gate
  the host bound it to is asked instead. A system principal has no session and meets no
  gate, so it cannot export while the flag is on (strictest reading); a deployment whose
  background work exports turns the flag off, which is recorded and alerted as any
  runtime change is.
- Order: restriction, then grants, then the gate, then consent, then the limit, then
  the audit row. A refused export is neither counted nor recorded as an export (a
  refusal by the grants is recorded as `authz.access.denied`, as before).
- One export is each admitted `RequireAsync` (all three overloads), `FilterAsync` and
  `FragmentAsync` call exercising an export permission. A capability page is not one:
  it answers what could be done and exercises nothing. A restricted account's list
  filter matches nothing before any of this is asked; an export is a reading action
  and is not restricted.
- The limit is each actor's own over a rolling hour: a person by subject, a system
  principal by its name. Past it the call is refused with `auth.throttled` and
  `retryAt`, the instant the oldest of the hour's exports leaves the window. A limit of
  zero or less admits nothing and answers one hour from now. The table `bulk_exports`
  keeps the instants and forgets an actor's older than the hour when the actor's next
  export is recorded. Two concurrent exports of one actor can both be admitted at the
  limit, as with a subject's own exports (D-086); the limit is a brake, not a count of
  record.
- Every admitted export is recorded in the trail as `authz.access.exported`, category
  security, with the acting and effective person (or the principal's name and reason,
  with the nil subject), the organization where the call named one, and details
  `permission`, `resourceType` and, for a check, `resource`. What the export returned is
  never recorded. D-045's "why" is not carried: the gate takes no reason, and no
  chapter gives an export one.
- `exfiltration.export.auditing` is honoured: off only where the deployment turned it off
  through the command line (OPS-CFG-004), and the export is still counted when it is.
  Entry 319's observation no longer holds for this key.

*Tests that pin it.*
`AuthorizationModelTests.OPS_ALERT_006_AC1_ExportIsAnEnumeratedSetOfOperations`,
`ExportOperationsTests.OPS_ALERT_006_AC1_OnlyADeclaredExportIsGatedLimitedAndRecordedAsync`,
`ExportOperationsTests.OPS_ALERT_006_AnExportAsksForStepUpWhileTheDeploymentRequiresItAsync`,
`ExportOperationsTests.OPS_ALERT_006_AnExportPastTheHourlyLimitIsThrottledUntilAPlaceFreesAsync`,
`ExportOperationsTests.OPS_ALERT_006_TheLimitIsEachActorsOwnAsync`,
`ExportOperationsTests.OPS_ALERT_006_ALimitOfNothingAdmitsNoExportAsync`,
`ExportOperationsTests.OPS_ALERT_006_EachAdmittedExportIsIndividuallyAuditedAsync`,
`ExportOperationsTests.OPS_ALERT_006_AC2_OnlyTheDeploymentTurnsTheAuditOffAsync`,
`ConfigurationEndpointTests.OPS_ALERT_006_AC2_ExportAuditingCannotBeDisabledThroughTheApplicationAsync`,
`ExportStoreTests.OPS_ALERT_006_EachActorsHourHoldsItsOwnExportsAsync`,
`ExportStoreTests.OPS_ALERT_006_AnAdmittedExportIsRecordedOnItsOwnAsync`,
`GateBehaviourTests.OPS_ALERT_006_AnExportIsGatedRecordedAndLimitedAtTheGateAsync`.

*Chapter text that should change.* OPS-ALERT-006 could say that the set is the host's
permissions whose action is `export`, that the gate applies all three requirements, what
one export is, that the limit is per actor over a rolling hour and answered with
`auth.throttled` and `retryAt`, what a system principal meets at the gate, and whether
an export carries a reason (and so whether the gate should take one).

---

## 330. How the library learns of clock drift and a failed certificate renewal

**Phase 9 · 2026-09-24 · Tier 2 · INF-HOST-001, INF-TLS-003, OPS-ALERT-001, LIB-EXT-001**

*The question.* OPS-ALERT-001 lists `certificate-renewal-failed` (High, INF-TLS-003)
and `clock-drift` (Normal, INF-HOST-001), and the phase 9 gate asks that every
condition fire from a test. The clock and the certificates are the environment's: the
plan says Milestone 1 touches no certificate and no host machine, and places TLS and
clock synchronisation in Milestone 2 steps 3 and 4. No chapter says how the library
learns that either has failed, and neither condition had a raise site.

*The readings.*

1. Leave both to the environment's own monitoring, outside the library's alerting;
   neither condition is ever raised by the library.
2. The library measures both itself: queries a time server and connects to the served
   origins to read their certificates. This needs a time server address no `10` key
   names, and makes the library the one component watching both.
3. The deployment registers what the environment knows, through two optional seams in
   the manner of `ILocationSource` (entry 325), and the library reads them on a
   schedule and raises through its own channels.

*Chosen: 3.* Reading 1 leaves two rows of the table with no way to reach the D-048
channels, which INF-TLS-003 names. Reading 2 adds a key and a network dependency, and
reads the certificate expiry through the same component that would report renewal,
which INF-TLS-003 AC3 forbids. Reading 3 adds two public interfaces and no key.

What is built:

- `IClockReference.OffsetAsync`: the offset of this host's clock from the reference, as
  the environment last measured it, positive where the host is ahead. The hourly
  `clock-drift` job raises `clock-drift` where the offset's magnitude exceeds
  `factor.totp.drift` steps of 30 seconds (INF-HOST-001 AC1 names the TOTP drift
  tolerance), with details `offsetSeconds` and `toleranceSeconds`. The edge is within.
- `ICertificateRenewal.LastFailureAsync`: when the most recent renewal failed, or
  nothing where it succeeded. The hourly `certificate-renewal` job raises
  `certificate-renewal-failed` with `failedAt` at every pass until a renewal succeeds;
  OPS-ALERT-002 keeps it to one alert a window.
- Fail closed: a deployment that registers neither, or whose seam answers a failure, is
  raised as `degradation` with the scope `clock.reference.absent`,
  `clock.reference.unread`, `certificate.renewal.absent` or
  `certificate.renewal.unread`, as an absent location source is (entry 325). An
  unwatched clock or renewer is not known to be sound.
- The expiry of the served certificate (INF-TLS-003 AC1) and the separation of the two
  checks (AC3) are the environment's: the expiry is read off-host from what is served,
  by nothing that renews, which is the INF-OBS-003 reachability check's place. Keeping
  the clock within the tolerance (INF-HOST-001 AC1) is the environment's clock
  synchronisation. These are verified in Milestone 2 steps 3 and 4, not by a test here.

*Tests that pin it.*
`EnvironmentWatchTests.INF_HOST_001_AC2_DriftBeyondToleranceRaisesAnAlertAsync`,
`EnvironmentWatchTests.INF_HOST_001_DriftWithinToleranceRaisesNothingAsync`,
`EnvironmentWatchTests.INF_HOST_001_TheToleranceFollowsTheCodeDriftAsync`,
`EnvironmentWatchTests.INF_HOST_001_AC2_AnUnmeasuredClockIsRaisedAsADegradationAsync`,
`EnvironmentWatchTests.INF_TLS_003_AC2_ARenewalFailureRaisesAnAlertWithoutAnyoneCheckingAsync`,
`EnvironmentWatchTests.INF_TLS_003_AC2_AnUnwatchedRenewalIsRaisedAsADegradationAsync`,
`BackgroundJobsTests.INF_BG_001_AC1_EveryJobRunsWithoutAPersonAsync`.

*Chapter text that should change.* INF-HOST-001 and INF-TLS-003 could say that the
environment reports the measured offset and the renewal outcome to the library through
the two seams, and LIB-EXT-001's table could list them with "None: the deployment
supplies them; an absent one is raised as a degradation". OPS-OBS-002's list of
degradations could name the four scopes.

---

## 331. How the missing emergency credential stays raised, and where it is shown

**Phase 9 · 2026-09-24 · Tier 2 · OPS-BOOT-001 AC3, OPS-ALERT-001, OPS-ALERT-002, 09**

*The question.* OPS-BOOT-001 AC3: until a break-glass credential is generated, "a
non-dismissable High alert ... is shown to every system administrator and raised on
OPS-ALERT-001". Bootstrap raised `no-emergency-credential` once (entry 313), so the
alert went out in one window and never again, and nothing raised it after a credential
was spent. `09` names no route a management application could read the credential's
state from, so nothing could show it to an administrator.

*The readings.*

1. Raise it once at bootstrap, as before.
2. Raise it at every pass of a job while no issue stands (none generated, or the last
   spent), so OPS-ALERT-002 carries it once a window until one is generated; and for
   the showing, (a) add a route such as `GET /admin/break-glass` answering whether an
   issue stands, or (b) add no route.

*Chosen: 2(b).* Reading 1 is dismissed by the first window's end, which "non-dismissable"
forbids. The hourly `emergency-credential` job raises it while `StandingAsync` finds no
issue, which covers a spent credential too: after an emergency no credential exists.
For the showing, `09` is authoritative for routes and names none, and a new route
widens the public surface, so none is added; the alert reaches the alert destinations,
which the operator holds. What a management application shows every system
administrator needs a route `09` does not yet have.

*Tests that pin it.*
`EmergencyCredentialWatchTests.OPS_BOOT_001_AC3_TheAbsenceIsRaisedUntilACredentialIsGeneratedAsync`,
`EmergencyCredentialWatchTests.OPS_BOOT_001_AC3_ASpentCredentialLeavesTheAbsenceRaisedAsync`,
`BootstrapTests.OPS_BOOT_001_AC3_NoEmergencyCredentialIsIssuedAndItsAbsenceIsRaisedAsync`,
`BackgroundJobsTests.INF_BG_001_AC1_EveryJobRunsWithoutAPersonAsync`.

*Chapter text that should change.* `09` could add a route answering whether a
break-glass credential stands (for example `GET /admin/break-glass`, system
administrators only, `{ "standing": true|false, "issuedAt": ... }`), so the management
application can show the alert of OPS-BOOT-001 AC3; OPS-BOOT-001 AC3 could say the
alert is raised again every window until one is generated, a spent one included.

---

## 332. How the off-host erasure ledger is written, and what an erasure waits for

**Phase 9 · 2026-09-24 · Tier 3 · DR-016, DR-006a, IDN-LIFE-003a, IDN-LIFE-003b, LIB-EXT-001**

*The question.* DR-016 asks that completed erasures be appended to an off-host ledger
and that no erasure be reported complete until its line is durable (AC2); the plan puts
the ledger writer in phase 9. No chapter says how the library reaches storage off the
host, at which point of an erasure the line is written, or what happens while the
ledger cannot take it, and LIB-EXT-001's table has no row for it. It touches erasure, so
the strictest reading is taken.

*The readings.*

1. Append the line inside the erasure's own transaction, before it commits. The ledger
   is not the database, so the two cannot commit together: a transaction that fails
   after the append leaves a line for an erasure that did not happen.
2. Append once the erasure's transaction commits, tracked by a new column on the
   erasures table and a retry pass of its own. IDN-LIFE-003b lists that table's
   columns, and the pass would repeat the outbox's retry, budget and alert.
3. Make the line one more required confirmation on the erasure's outbox record, which
   IDN-LIFE-003a already describes ("the outbox record carries each required
   subscriber's confirmation"; "complete only when every required subscriber has
   confirmed"), so the record and the erasures row that follows it complete only once
   the line is durable.

*Chosen: 3.* Reading 1 writes lines for erasures that may not exist, which a replay
would then carry out. Reading 2 adds a column IDN-LIFE-003b does not list and a second
retry mechanism. Reading 3 holds the erasure open, retries it and raises it by the
mechanism the chapter already gives the host-side work.

What is built:

- `IErasureLedger.AppendAsync(line)`, answering success only once the line is durable.
  The deployment registers it over storage that does not share fate with the database
  host; that storage is the environment's (DR-016 AC1, verified in Milestone 2).
- The line is the erasure's instant in RFC 3339 UTC to the second (the instant its
  transaction committed, truncated), one space, the subject identifier in its
  lower-case form, one space, the reason in the spelling of `10` section 5.12a, and
  nothing else (AC4). DR-016's illustration shows the minute and two spaces; its
  Values paragraph (D-153) says the second and one space, and is followed. A line is
  read back only in the exact form it is written in.
- The confirmation is named `erasure-ledger`, is required, is offered before the host's
  subscribers, and only on `ErasureRequested` records. While the ledger refuses the
  line, the record and the erasures row stay `awaiting-subscribers` on the outbox
  schedule; a spent budget fails both and raises `erasure-delivery-exhausted` with
  `erasure-ledger` among the outstanding. `GET /admin/erasures/{id}` lists it first.
- The manual completion path vouches for the host's subscribers and never for the
  line: where the attempts never made it durable, the path appends it first, and while
  the ledger refuses it the path answers `system.fault` (500) and closes nothing. The
  audit's `outstanding` names the host's subscribers only.
- A confirmation is recorded under the subscriber's name, so a host subscriber named
  `erasure-ledger`, or two host subscribers under one name, would read another's
  confirmation as their own and close an erasure with its work undone. Startup refuses
  either with the new code `model.startup.subscribername`, `details.handler` naming the
  name. Two host subscribers sharing a name had that effect before this entry too.
- A line may be appended twice (a pass whose record does not commit after the append,
  or a replay's redelivery); a replay reads a repeat as the one erasure.
- A deployment that registers no ledger completes its erasures without a line and
  raises nothing: DR-016 is deferred until the tier upgrade and R-A13 accepts the
  exposure until then, so an hourly alert would raise an accepted risk as a failure.

*Tests that pin it.*
`OutboxPublisherTests.DR_016_AC2_AnErasureIsNotCompleteUntilItsLineIsDurableAsync`,
`OutboxPublisherTests.DR_016_AC4_TheLineHoldsTheInstantTheSubjectAndTheReasonAndNothingElseAsync`,
`OutboxPublisherTests.DR_016_AC2_ALedgerThatNeverTakesTheLineIsRaisedWhenTheBudgetIsSpentAsync`,
`OutboxPublisherTests.DR_016_OnlyAnErasureWaitsForTheLedgerAsync`,
`ErasureServiceTests.DR_016_AC2_AnErasureIsReadWithItsLedgerLineAsync`,
`ErasureServiceTests.DR_016_AC2_AManualCompletionWritesTheLineBeforeItClosesTheErasureAsync`,
`ErasureServiceTests.DR_016_AC2_ALineAlreadyWrittenIsNotWrittenAgainAsync`,
`ErasureServiceTests.DR_016_AC2_AManualCompletionClosesNothingWhileTheLineCannotBeWrittenAsync`,
`ErasureLedgerLineTests.DR_016_ALineIsReadAsTheErasureItWasWrittenFor`,
`ErasureLedgerLineTests.DR_016_ALineInAnyOtherFormIsNotRead`,
`HandlerCoverageTests.DR_016_AC2_ASubscriberUnderTheLedgersNameFailsStartup`,
`HandlerCoverageTests.IDN_LIFE_003a_TwoSubscribersUnderOneNameFailStartup`,
`StartupValidationTests.DR_016_AC2_ASubscriberUnderTheLedgersNameIsRefusedAsync`,
`ErasureEndpointTests.DR_016_AC2_AManualCompletionIsAFaultWhileTheLedgerCannotTakeTheLineAsync`.

*Chapter text that should change.* LIB-EXT-001's table could add "Off-host erasure
ledger (DR-016) | None: the deployment supplies it at the tier upgrade; until then
erasures complete without it (R-A13)". DR-016 could say the line is a required
confirmation on the erasure's outbox record, that the manual completion appends it and
never vouches for it, and could show its illustration to the second with one space.
IDN-LIFE-003a could say that subscriber names are distinct and that `erasure-ledger` is
the library's. `10` section 1.5 could add `model.startup.subscribername`.

---

## 333. What the replay of the erasure ledger does to a restored database

**Phase 9 · 2026-09-24 · Tier 3 · DR-016 AC3, DR-006a AC1, IDN-PRIN-001, INF-BG-002, IDN-LIFE-003**

*The question.* DR-016 names the replay (`janus replay-erasures <ledger path>`,
idempotent over every line) and says only "for every identifier in the ledger, confirm
the key is destroyed and the erasure recorded; complete anything the restore forgot".
No chapter says what a line whose account the restore brought back live becomes, since
the only transition into `deleted` is from `deleting`; whether the host is told again;
under which principal the replay acts; or what becomes of a ledger that cannot be read
whole. It touches erasure, so the strictest reading is taken.

*The readings.*

1. For each line: where the erasures row stands, nothing (the row commits with the
   key's destruction, IDN-LIFE-003b AC4); otherwise carry the erasure out again, from
   whatever state the restore left the account in, and tell the host again.
2. As 1, but carry out only an account the restore left `deleting`, and report the rest
   for the operator to put into deletion by hand.
3. As 1, but leave the host's own tables to the operator.

*Chosen: 1.* The erasure was carried out and reported complete before the restore, so
no state the restore brought back is one the account may stay in; reading 2 leaves a
person's data readable until someone acts. The restore brought the host's rows back as
well, so reading 3 leaves the host's half undone.

What is built:

- `janus replay-erasures <path>` reads the whole ledger before anything else: UTF-8
  with no byte order mark read as another encoding, each line in the one form entry 332
  writes. A file that cannot be opened or decoded, or no path, is refused as
  `api.request.malformed` with `details.member` `ledger`; a line in any other form
  refuses the whole ledger with `details.line` its number, and nothing is written. The
  key document is piped as for every command; the connection is the application's own
  credential, which holds every right the erasure writes with.
- A line whose erasures row stands, a repeated line included, is left as it is. A line
  naming an account the restored database does not hold is counted and left. Every
  other line is carried out again in one transaction: the account enters the deletion
  the line records where it was not already deleting (`takedown` for `minor-takedown`,
  `oob-request` for the other two reasons, since the ledger and not the person asks
  now), at the line's instant, from `active`, `restricted` or `suspended`, and is
  erased by the same writes as the sweep's; the `ErasureRequested` record goes on the
  outbox again at the line's instant and reason, so the host redoes its half and the
  line is appended again unchanged; and the erasure is audited as
  `privacy.erasure.executed` under the deployment-scoped principal `replay-erasures`,
  reason `DR-016`, with details `reason` and `erasedAt`.
- `SystemOperation` gains `ErasureReplay` (`erasure-replay`), the one operation that
  principal may run, as bootstrap (entry 314) and key rotation have theirs.
- The command prints `{"reapplied": n, "standing": n, "absent": n}` and nothing of a
  subject. A second run over the same ledger carries out nothing more.
- DR-006a AC1 speaks of erasures "recorded in the erasures table ... where that table
  survives the restore"; a restore of the one database restores that table with it, so
  the replay reads the ledger, which DR-006a names as what re-applies them.

*Tests that pin it.*
`ErasureReplayTests.DR_016_AC3_TheWholeLedgerIsReplayedAndASecondReplayChangesNothingAsync`,
`ErasureReplayTests.DR_016_AC3_ALedgerWithALineInAnotherFormIsRefusedWholeAsync`,
`ErasureReplayTests.DR_016_AC3_AnUnreadableLedgerIsRefusedAsync`,
`AccountTests.DR_016_AC3_AnErasureIsReappliedFromTheStateARestoreLeft`,
`AccountTests.DR_016_AC3_AnAccountLeftDeletingKeepsItsDeletion`,
`AccountTests.DR_016_AC3_AnErasedOrEmergencyAccountIsNotReapplied`.

*Chapter text that should change.* DR-016 could say what a replayed line becomes (the
deletion origin recorded, the host told again, the audit and its principal) and what
the command prints and refuses. IDN-PRIN-001 could list `erasure-replay` among the
operations. The restore procedure (`12` section 4) could say the ledger is copied from
its storage and replayed under the application's credential before cutting over, and
DR-006a AC1 could name the ledger in place of the erasures table.

---

## 334. How the restore test is run, proved, timed and recorded

**Phase 9 · 2026-09-25 · Tier 3 · DR-007, DR-008, DR-010 AC2, DR-017 AC2, OPS-ALERT-001, INF-BG-001, LIB-EXT-001**

*The question.* DR-007 names the job's steps: restore the latest base backup and logs
into a throwaway instance, decrypt the canary's field with the live key-encryption key,
verify a fingerprint resolves, record the elapsed time against the objective, tear the
instance down, and alert on failure or overrun. A library cannot restore a backup:
where the backups are, how they are opened and what the instance is built on are the
environment's (DR-010, DR-017). No chapter says how the library is handed the instance,
where the measured time is recorded, what a deployment that cannot restore at all
becomes, whether a restore running past the objective is waited for, or what proves "a
known account can sign in" for a canary that holds no credential (entry 312). It
touches the keys, so the strictest reading is taken.

*The readings.*

1. A host seam the library asks to restore into a throwaway instance and to tear it
   down; the library opens the restored database with the keys it runs on and proves
   the canary itself; a deployment that registers no seam fails every run.
2. As 1, but a deployment with no seam skips the test and raises a degradation.
3. The host runs the whole test and reports its outcome and time; the library records
   and raises.

For the record: an audit row per run, the maintenance log, or a table of its own. For a
restore still running at the objective: wait for it, or abandon it.

*Chosen: 1, an audit row, and abandoning at the objective.* Reading 3 leaves the
decryption under the live key, the half DR-007 exists to prove, to code the library
never sees. Reading 2 lets a deployment that never tested a backup pass quietly, where
DR-007 calls an untested backup a hypothesis. The maintenance log is for the human
tasks and DR-007 says this job is not one; an audit row needs no new table and is kept
for the security period. A restore that hangs would otherwise never be raised, and the
test has failed by then in any case.

What is built:

- Public `IRestoreTestInstance`. `RestoreAsync` builds an instance from the committed
  infrastructure definition, never over the running database, restores the latest
  base backup and the logs after it, and returns how to reach the restored database.
  `TearDownAsync` tears down whatever the last restore built, however far it got. The
  backup's private key is the implementation's to fetch when it restores and to hold
  in memory only (DR-010 AC2).
- The job `restore-test`, reason `DR-007`, operation `monitoring`, every
  `backup.restoretest.interval`. It reads `backup.restoretest.objective` (the default,
  which is its ceiling, where unreadable) and `backup.restoretest.canary`, and asks for
  a restore under a cancellation that fires at the objective. It opens the restored
  database as a storage area of its own under the keys the process holds, with pooled
  connections off, so no connection to the instance is left open when the teardown is
  asked. Then:
  - it decrypts the canary's display name (entry 312); none there, a canary setting
    naming no subject, or a fault is `undecrypted`;
  - it reads the canary's verified email and finds its account by it as sign-in does,
    the canonical form looked up by its fingerprint; not found, found as another
    account, or a fault is `unresolved`. This is what AC4's "a known account can sign
    in" is proved by: sign-in's resolution of an identifier to its account, since the
    canary holds no credential;
  - nothing registered, a restore that failed, or one that threw is `unrestored`; a run
    that took longer than the objective, whatever step it reached, is `overrun`; any
    other run is `passed`.
- A step that throws is read as the failure of that step and logged by the type of what
  was thrown only (hosting background event 3, a warning).
- The teardown is asked after every restore attempted, with no cancellation, so the
  worker stopping does not leave the instance standing. One that fails or throws is
  recorded as `outlived`.
- Every run is recorded as `ops.restoretest.completed`, security category, under the
  job's principal, with details `outcome`, `elapsedSeconds`, `objectiveSeconds` and
  `outlived`, and nothing of the canary. A run short of a pass, or whose instance may
  have outlived it, raises `restore-test-failed` with the same details in the same
  transaction.
- A run holds its own process's worker for as long as it takes. The deployment's other
  process keeps taking the other jobs' runs, since the database decides which process
  takes each run, and the objective bounds how long any run holds one.
- `backup.restoretest.interval` is `P3M`, which the chapter 10 holding rule reads as 93
  days (D-152), so a calendar quarter of 90 days can pass without a run.

*Tests that pin it.*
`RestoreTestTests.DR_007_AC1_TheTestRunsAtItsIntervalWithoutAPersonAsync`,
`RestoreTestTests.DR_007_AC4_TheRestoredCanaryDecryptsAndItsAccountIsFoundAsync`,
`RestoreTestTests.DR_007_AC2_TheMeasuredTimeIsRecordedAgainstTheObjectiveAsync`,
`RestoreTestTests.DR_007_AC3_ABackupTheLiveKeyCannotOpenIsRaisedAsync`,
`RestoreTestTests.DR_007_AC3_ABackupWhoseAccountsTheLiveFingerprintKeyCannotFindIsRaisedAsync`,
`RestoreTestTests.DR_007_AC3_ARunThatRestoresNothingIsRaisedAsync`,
`RestoreTestTests.DR_008_AC1_TheTestReadsTheRestoredInstanceAndLeavesTheRunningOneAsync`,
`RestoreTestTests.DR_008_AC2_TheInstanceDoesNotOutliveTheTestAsync`,
`BackgroundJobsTests.INF_BG_001_AC1_EveryJobRunsWithoutAPersonAsync`.

*Chapter text that should change.* DR-007 could name the host seam and what it returns,
the outcomes, the record and its details, that the test is abandoned at the objective,
and that "a known account can sign in" is proved by resolving the canary's verified
email to its account. DR-008 could say the teardown is asked whatever became of the
test and that a failed one raises. Chapter 10 could list `ops.restoretest.completed`.
If "at least quarterly" means once in every calendar quarter, the
`backup.restoretest.interval` row could be written as `P90D`.

---

## 335. How the recovery-code reminder is sent

**Phase 9 · 2026-09-25 · Tier 2 · AUTH-FACT-008 AC5, INF-BG-001, REG-ACCT-001, PRIV-RIGHT-003, D-022**

*The question.* AUTH-FACT-008 AC5 asks that a set older than `recovery.codes.reminder`
produce one reminder and no further reminder until the set is regenerated. Phase 3
built the set's `RemindedAt` and a service method that marked a set reminded, but no
job called it and nothing was sent, so the criterion held in a unit test and never in
a deployment. No chapter names the message, who it reaches, which accounts it skips,
when the pass runs, or where the person sees that it fired.

*The readings.*

1. A message kind of its own, sent to the security-notice set; the set is marked in the
   transaction that writes the notices; accounts that are not active are skipped; the
   instant is shown on the account and carried in the export.
2. As 1, but the reminder rides `security-notice`, which already reaches the same set.
3. As 1, but every account with a set is reminded, whatever its state.

*Chosen: 1.* A catalogue that words `security-notice` for "something happened to the
account" would tell a person their account was touched when nothing was; CONV-CONTENT-001
leaves the words to the deployment, which needs a key to word this one by. A suspended,
restricted or deleting account cannot act on the reminder, and a deleting one is owed
no mail beyond its deletion notice; its set stays owed the reminder, so it is sent if
the account comes back. D-022 writes a message in the transaction that made it
necessary, which is what makes "one reminder" hold across a failed pass.

What is built:

- `MessageKind.RecoveryCodesReminder`, key `recovery-codes-reminder`, on both channels,
  with default words in both shipped languages. It carries no link and no value.
- `IRecoveryCodeStore.DueReminderAsync`: the sets generated at or before an instant,
  never reminded, held by an active account, oldest first, a page at a time.
- `RecoveryCodeReminders.RemindAsync` reads the age (an unreadable one fails the pass
  and sends nothing), and for each set due marks it reminded, records it, and asks for
  the reminder on every channel of the security-notice set, in one transaction. A
  channel that refuses the reminder does not keep the set owed it: the one reminder
  was produced. The pass takes every page until none is left.
- The job `recovery-code-reminder`, reason `AUTH-FACT-008`, operation `expiry-sweep`,
  daily, since the age is counted in months.
- `RecoveryCodeStatus.RemindedAt` (public), shown as `recoveryCodes.remindedAt` on the
  account read, and `remindedAt` in the export's `recovery-codes` record beside the
  other instants the set carries.
- The unused `RecoveryCodeService.RemindAsync` is removed.

*Tests that pin it.*
`RecoveryCodeRemindersTests.AUTH_FACT_008_AC5_AnOldSetRemindsItsOwnerOnceAsync`,
`RecoveryCodeRemindersTests.AUTH_FACT_008_AC5_TheAgeIsWhatTheDeploymentConfiguresAsync`,
`RecoveryCodeRemindersTests.AUTH_FACT_008_AC5_AnAccountThatIsNotActiveIsNotRemindedAsync`,
`RecoveryCodeRemindersTests.AUTH_FACT_008_AC5_EverySetDueIsRemindedOnceInOnePassAsync`,
`RecoveryCodeStoreTests.AUTH_FACT_008_AC5_TheSetsOwedTheirReminderAreReadOldestFirstAsync`,
`AccountServiceTests.AUTH_FACT_008_AC5_TheReadCarriesWhenTheSetWasRemindedOfAsync`,
`ExportSourceTests.REG_ACCT_001_AC1_TheExportCarriesTheCredentialsTheAccountShowsAsync`,
`VocabularyContractTests.WireNames_TheKeysTheCatalogueIsAskedBy_AreWritten`,
`BackgroundJobsTests.INF_BG_001_AC1_EveryJobRunsWithoutAPersonAsync`.

*Chapter text that should change.* AUTH-FACT-008 could name the message, its recipients
(the security-notice set), that only an active account is reminded, and the daily pass.
Chapter 09 could list `remindedAt` in the account's `recoveryCodes` object. Chapter 10
could carry `recovery-codes-reminder` if it lists message kinds.

---

## 336. How the audit partitions are kept, and how the maintenance credential reaches the worker

**Phase 9 · 2026-09-25 · Tier 3 · PRIV-RET-002, OPS-MIG-003a, INF-HOST-003, INF-BG-001, INF-BG-002, OPS-DATA-002, LIB-EXT-001**

*The question.* Phase 1 created `audit_ensure_partitions()` and
`audit_drop_expired_partitions(interval, interval)`, executable by the maintenance role
only, and ran the first once at migration. Nothing called either afterwards: no month
past the two the migration created was ever made, so a deployment not migrated again
within about two months would refuse every audited operation for want of a partition,
and no expired partition was ever dropped. PRIV-RET-002 has a scheduled job call the
drop with the maintenance credential "the worker fetches from the secrets manager", and
the months "created ahead by the sweep". `ISecretSource.ReadMaintenanceCredentialAsync`
exists but nothing reads it, and `AddJanus`, which takes every other value the secrets
manager supplies, does not take this one. No chapter says what form the credential has,
how the worker is handed it, what a deployment without it becomes, what the job does
under a credential that is not the maintenance one, or what the record of a run holds. It
touches credentials and retention, so the strictest reading is taken.

*The readings.*

For the credential's path: (1) a further `AddJanus` argument, as the key-encryption key,
the fingerprint key and the sign-on secret are passed; (2) the job resolves the host's
`ISecretSource` from the container at each run. For its form: (a) a whole database
connection for the maintenance login; (b) a password joined to the application's own
connection under a user name the library would have to assume. For its absence: refuse
to start, or start and fail every run. For the months ahead: this job, or the expiry
sweep, which runs under the application's credential and cannot execute the function.

*Chosen: 1, a, refuse to start, and this job.* Every other secret reaches the library as
an `AddJanus` argument read once at startup (chapter 08's "fetched once, at startup"),
and no `ISecretSource` is registered in a container anywhere, so 2 would be a second way
of doing the same thing. The role is `NOLOGIN` and the deployment attaches the login
(OPS-MIG-003), so the library cannot know the user name (b) would need, and the command
line's key document already carries the maintenance connection whole. A deployment that
starts without the credential would lose its audit trail within two months, so it does
not start. The sweep cannot reach the function, so the months ahead are this job's.

What is built:

- `AddJanus` takes `maintenanceCredential` after `signOnSecret`: the database connection
  of a login that holds the maintenance role's rights, as its UTF-8 bytes. Empty, startup
  fails with `model.startup.kekunavailable`, `details.key` `maintenanceCredential`.
- Internal `IAuditPartitions` (Identity, audit) with `AuditPartitions` (Storage): whether
  the connection is the maintenance credential (the role's rights and no path to the
  application's, the test the key-rotation commands apply, entry 316), the ensure
  function and the drop function, each through the connection accessor of its own area.
- The job `audit-partitions`, reason `PRIV-RET-002`, operation `retention-purge`, daily.
  It opens a storage area of its own over the maintenance credential, unpooled, so no
  connection stays open under it after the run, and shares none with the running one
  (OPS-DATA-002 holds within each area: nothing of the job's is in the application's
  transaction). It refuses a connection that is not the maintenance credential with
  `authz.denied` before either function is asked; then creates the months ahead; then
  reads `retention.audit.security` and `retention.audit.routine` and, where either is
  unreadable, fails the run having dropped nothing; then drops.
- Each completed run is recorded as `ops.auditpartitions.maintained`, security category,
  under the job's principal, over the application's own credential (the maintenance role
  writes no row), with details `created`, `dropped`, `securityRetentionDays` and
  `routineRetentionDays`, and no subject.
- A drop is not undone by a record that then fails to be written; the run fails and the
  worker's lapse alert applies (INF-BG-001).
- A refused or failed run is a failed run, which the worker raises as
  `background-job-failed` once the job has lapsed (PRIV-RET-002 AC3).

*Tests that pin it.*
`AuditRetentionTests.PRIV_RET_002_AC3_ExpiredPartitionsAreDroppedOnScheduleWithoutAPersonAsync`,
`AuditRetentionTests.PRIV_RET_002_AC5_ARunUnderAnotherCredentialDropsNothingAsync`,
`AuditPartitionsTests.OPS_MIG_003a_OnlyTheMaintenanceCredentialIsTakenForItAsync` (3 cases),
`AuditPartitionsTests.PRIV_RET_002_AC3_TheMonthsAheadAreCreatedAndTheExpiredDroppedAsync`,
`KeyMaterialTests.OPS_MIG_003a_StartupFailsNamedWithoutTheMaintenanceCredential`,
`BackgroundJobsTests.INF_BG_001_AC1_EveryJobRunsWithoutAPersonAsync`,
`AuditActionsTests` (the list).

*Chapter text that should change.* PRIV-RET-002 could say that the job creates the months
ahead as well as dropping, since the sweep it names cannot execute the function, and
what its record holds. INF-HOST-003 and chapter 07 could say the maintenance credential is
a whole database connection handed to `AddJanus` at startup, and that a deployment
without it does not start. Chapter 10 could list `ops.auditpartitions.maintained`.

---

## 337. How a blocklist fallback is raised

**Phase 9 · 2026-09-25 · Tier 2 · OPS-OBS-002, OPS-ALERT-001, OPS-ALERT-002, INT-PWD-002, AUTH-PASS-004**

*The question.* OPS-ALERT-001's table lists "blocklist fallback" under `degradation`,
and OPS-OBS-002 AC1 has each listed degradation produce a monitored signal. Phase 2 had
screening write the fall back to the host's log only (`IScreeningLog.Degraded`), so a
deployment whose range service had been unreachable for months would have screened
against the offline corpus without anyone being told. The other three degradations
the item lists raise the alert already (entries on `mailbox.push:<mailbox id>`,
`mailbox.reconciliation` and `send:<channel>`). No chapter names the scope of this one,
its details, or what screening does when the alert cannot be raised.

*The readings.* For the scope: (1) one scope for every fall back; (2) a scope per
configured corpus. For an alert that cannot be raised: (a) screen on against the
offline corpus and leave the log as the only trace; (b) refuse the operation with what
refused the alert.

*Chosen: 1 and b.* The condition is the deployment's, not a person's, so one scope
keeps a sustained outage to one alert a window under OPS-ALERT-002 whichever corpus is
configured; the details say which. A fall back the owner cannot be told of is the
silent degradation OPS-OBS-002 forbids, so the operation is refused, as an absent
location source is (entry 325).

What is built:

- `PasswordScreening` takes the alert channels and the clock. When the configured corpus
  cannot answer and it is not the offline one, the fall back is logged as before, then
  raised as `degradation` with the scope `password.blocklist.fallback` and details
  `configured` (the written name of the configured corpus) and `used` (`offline`),
  before the offline corpus is asked.
- A raise that fails refuses the screening with the error that refused it; nothing is
  screened past unseen.
- Screening runs outside any open transaction on every path that reaches it (set,
  change, recovery, registration), so the alert commits with its own unit of work and a
  refusal of the password that follows does not roll it back.

*Tests that pin it.*
`PasswordScreeningTests.OPS_OBS_002_AC1_ABlocklistFallbackRaisesADegradationAsync`,
`PasswordScreeningTests.OPS_OBS_002_AC2_AFallbackThatCannotBeRaisedRefusesTheOperationAsync`,
`PasswordScreeningTests.ScreenAsync_TheConfiguredCorpusAnswering_RaisesNothingAsync`,
`ScreeningTests.INT_PWD_002_AC1_WithTheServiceUnreachableTheOfflineListAnswersAsync`
(extended).

*Chapter text that should change.* OPS-OBS-002 could name the scope
`password.blocklist.fallback` beside the other degradations' scopes, and say that a
fall back that cannot be raised refuses the operation. INT-PWD-002 AC1 could say the
fall back is raised, not only recorded.

---

## 338. A development database is made ready the way a production one is

**Phase 9 · 2026-09-25 · Tier 2 · OPS-ENV-001, OPS-BOOT-001**

*The question.* OPS-ENV-001 has development databases "seeded with realistic principals
and grants", AC1 "A fresh development database yields a usable, permission-realistic
dataset", AC2 "No bypass flag exists in any environment". No chapter says what seeds a
development database, what it holds, or how the absence of a bypass flag is shown.

*The readings.*

1. A development seed of the library's own: a command or a start-up step that writes
   sample organizations, accounts and grants into a database it is told is for
   development.
2. The path a production database takes, and nothing beside it: the migrations, then
   `janus bootstrap`, which writes the administrative organization, the three
   administrative roles with their permissions, the first administrator and
   `emergency`; everything after that is made through the library's own operations, as
   in production.

*Chosen: 2.* A seed of its own is a second way of making principals and grants that
production never runs, and a path that exists only where a database is told it is for
development is itself the kind of switch AC2 forbids. Under 2 what a developer sees is
exactly what an operator sees on the first day: real roles, real grants, a real
administrator whose link enrols, and nothing granted outside a role.

What is built: nothing new in the library. AC1 is pinned on a fresh database after
bootstrap: every live grant belongs to a member of the administrative organization and
names the seeded system administrator's role, there are exactly two such principals (the
administrator and `emergency`), and no grant names a role that confers nothing. AC2 is
pinned by a scan of the shipped code: nothing asks which environment it runs in
(`IsDevelopment`, `EnvironmentName`, `GetEnvironmentVariable`), and no key of the
settings catalogue is named for bypassing, skipping or disabling a check.

*Tests that pin it.*
`BootstrapTests.OPS_ENV_001_AC1_AFreshDatabaseYieldsAUsablePermissionRealisticDatasetAsync`,
`FailClosedTests.OPS_ENV_001_AC2_NoBypassFlagExistsInAnyEnvironment`.

*Chapter text that should change.* OPS-ENV-001 could say that a development database is
made ready by the migrations and `janus bootstrap`, as a production one is, and that the
library ships no seed of its own.

---

## 339. A concealed refusal is answered by the browser profile

**Phase 9 · 2026-09-25 · Tier 3 · BFF-ERR-003, BFF-ORDER-001, OPS-ENV-002, AUTHZ-CONCEAL-001, AUTHZ-CONCEAL-002, AUTHZ-CONCEAL-004, API-CONV-003**

*The question.* BFF-ORDER-001 stage 11 is "Error translation and concealment",
BFF-ERR-003 AC3 has uniformity "enforced by the pipeline, not by endpoint discipline",
and OPS-ENV-002 AC1 has denied-access semantics enforced in shared infrastructure.
Entry 126 built stage 11's error translation and left its concealment unbuilt. The gate
refused every record-level check with `authz.denied`, which the status table maps to
403, and left the host's endpoint to answer it as an absence; the library's writer is
internal, so each host endpoint answered a concealing type however it chose, and one
that answered 403 said the record was there. Chapter 10 section 1.3 has `authz.denied`
"used where existence is not concealed" and names no code for a concealed denial. No
chapter says what the concealed answer's code and body are, how the pipeline learns
that a refusal was concealed, or what becomes of what the endpoint wrote.

*The readings.* For the code: (1) the gate returns a not-found code on a concealing type
in place of `authz.denied`; (2) the gate keeps `authz.denied` and the pipeline answers a
not-found code. For what the pipeline replaces: (a) a 403 or 404 the endpoint answered;
(b) every answer of a request in which a refusal was concealed. For what the answer
carries: (i) the code alone; (ii) the audit identifier of the refusal, as `authz.denied`
carries it.

*Chosen: 2, b and ii (Tier 3, the strictest reading).* BFF-ERR-003 puts concealment in
the pipeline, and a code from the gate would still leave the answer to each endpoint;
the result a host holds is unchanged and what crosses the boundary is not. An endpoint
that goes on past a concealed refusal and answers anything else has answered from a
record the caller may not see; under (a) a success, a 409 or a 422 would still say the
record is there. AUTHZ-CONCEAL-004 has the response carry an identifier that appears in
the audit trail, and the gate records a refusal for a record the library holds no row
for exactly as one for a record the caller may not see, so the genuine absence carries
one too and the two stay the same shape.

What is built:

- The code `authz.resource.notfound`, answered 404 (`ErrorCodes.ResourceNotFound`). No
  operation returns it; stage 11 answers it.
- The gate hands every refusal on a type that does not disclose to a holder of the
  request, under the identifier it recorded. A refusal on a disclosing type, and one
  tied to no record (AUTHZ-CONCEAL-005), hand nothing.
- Stage 11 of the browser profile, mounted outermost, stands in for the response body:
  what the endpoint writes goes through until a refusal is concealed and nothing goes
  through after. When the request comes back with a refusal concealed, the response is
  cleared to what it carried when the request reached the endpoints (the stages' own
  cookies stay, anything the endpoint added goes), and the one refusal writer answers
  404 `authz.resource.notfound` with `details.correlation` the audit identifier of the
  first refusal concealed. A log line ties the request's trace identifier to it.
- An answer the endpoint had begun before the refusal cannot be taken back: the
  connection is closed rather than finished, and the operator is told at error level.
- The machine profile does not carry the stage. Its routes are the library's own, none
  of which checks a record, and a provider's callback has no caller to conceal from.

*Residue.* Timing (BFF-ERR-003 AC2, AUTHZ-CONCEAL-002 AC2, API-CONV-003 AC1) is one
refusal path and one writer, with the byte identity asserted, and is named in the report
as verified by construction as the criteria say. The gate reads the candidate grants of
a record it holds a row for and not of one it holds none for, so the two refusals
differ by that query; the owner may want the read made for both. A host that answers
its own 404 without asking the gate is outside the pipeline: the remarks of
`UseBrowserProfile` and `IAccessGate.RequireAsync` say the gate is asked and the
profile answers.

*Rows for chapter 10.* Section 1.3: `authz.resource.notfound` | No such record, or a
record of a concealing type the caller may not see; one answer for both, 404, carrying
the audit identifier of the refusal | AUTHZ-CONCEAL-001, BFF-ERR-003.

*Tests that pin it.*
`ConcealmentTests.BFF_ERR_003_AC1_AConcealedRefusalIsTheSameBytesWhateverTheEndpointWroteAsync`,
`ConcealmentTests.AUTHZ_CONCEAL_004_AC1_TheAnswerCarriesTheIdentifierTheRefusalWasRecordedUnderAsync`,
`ConcealmentTests.InvokeAsync_TwoRefusalsConcealed_AnswersTheFirstAsync`,
`ConcealmentTests.InvokeAsync_ARefusalConcealed_KeepsWhatTheStagesWroteAsync`,
`ConcealmentTests.InvokeAsync_NothingConcealed_LeavesTheAnswerAsTheEndpointWroteItAsync`,
`ConcealmentTests.InvokeAsync_AnAnswerBegunBeforeTheRefusal_IsBrokenOffAsync`,
`BrowserProfileTests.OPS_ENV_002_AC1_TheProfileAnswersAConcealedRefusalAndKeepsWhatItsStagesWroteAsync`,
`BrowserProfileTests.BFF_ERR_003_AC3_AnEndpointAnsweringPastAConcealedRefusalIsAnsweredAsAbsenceAsync`,
`ExplanationTests.AUTHZ_CONCEAL_001_AC1_ARefusalOnATypeDeclaringNothingIsConcealedAsync`,
`ExplanationTests.AUTHZ_CONCEAL_001_AC2_ARefusalOnADisclosingTypeIsNotConcealedAsync`,
`ApiStatusTests.API_CONV_003_AC2_OnlyAFailureNamingNoRecordAnswersForbidden` (extended),
`ErrorCodesTests.CONV_NAME_003_AC2_ChangingACodeFailsTheContractTest` (extended).

*Chapter text that should change.* Chapter 10 section 1.3 needs the row above.
BFF-ERR-003 could name the code and say the answer carries the refusal's audit
identifier and nothing the endpoint wrote. AUTHZ-CONCEAL-001 could say that a host asks
the gate before it looks the record up, since a record the library holds no row for is
the genuine absence a concealed refusal is identical to.

---

## 340. How a client enters the registry, and how its secret is rotated

**Phase 9 · 2026-09-25 · Tier 3 · AUTH-OIDC-001, OPS-SEC-001, OPS-SEC-002, API-REDIR-001**

*The question.* AUTH-OIDC-001 has "a manually managed client registry" and AC4 has the
mail-server client "registered at bootstrap where the mail integration is enabled".
OPS-SEC-002 has client secrets share one lifecycle with the signing keys, with automated
rotation and overlap windows; AC1 has rotation complete without restart or manual
action, AC2 has what was issued under the previous key stay valid through the overlap.
Phase 5 built the registry's reader and a writer only the tests called. Nothing in the
library or the command-line application wrote a client, and entry 262's "bootstrap
registers it" was never built, so a deployment could register the mail server or its
own browser applications only by writing the table by hand. Nothing kept a replaced
secret. No chapter says how a client is registered, where its secret comes from, what a
registration writes down, or how long a replaced secret is taken.

*The readings.* For the route: (1) bootstrap takes the mail-server client's values and
secret; (2) a command of its own, run as the deployment is stood up and again whenever a
client changes; (3) an endpoint of the management application. For the secret: (a) an
argument; (b) a member of the key document piped from the secrets manager. For the
overlap: (i) none, the new secret replaces the old at once; (ii) the replaced secret is
taken for the signing keys' overlap, the access-token lifetime and five minutes; (iii) a
key of its own.

*Chosen: 2, b and ii (Tier 3, the strictest reading).* Bootstrap runs once and refuses a
second run, so under (1) a secret could never be rotated and a browser application added
later could never be registered. An endpoint (3) is the dynamic registration
AUTH-OIDC-001 puts out of scope, reached with a session rather than with the server. An
argument shows in the process list, so the secret travels as the keys do (entry 307). A
key of its own (iii) is a default no chapter names; the signing keys' overlap is the one
OPS-SEC-002's "one lifecycle" already prices.

What is built:

- `janus register-client --client <id> --name <name> --kind protocol|browser-application
  --redirect <address> --scopes "<scope> ..."`, the secret in the key document's
  optional member `clientSecret` as its UTF-8 bytes in base64, held and cleared with the
  keys. The command checks the schema as the others do and prints
  `{"registered":"<id>"}`.
- `ClientRegistry` refuses with `api.request.malformed` naming the member: an identifier
  or a scope that is empty or carries white space, a blank name, a destination without
  an origin (the rule API-REDIR-001 AC3 applies at startup), and a secret shorter than
  32 bytes, not UTF-8, or nothing but white space. The command refuses an absent or
  undecodable `clientSecret` the same way.
- The registry holds the SHA-256 of the secret, as before. Registering a client again
  with a different secret moves the fingerprint held to `previous_secret`, with
  `previous_secret_until` now plus `oidc.accesstoken.lifetime` plus five minutes
  (migration `AddClientSecretOverlap`; a check keeps the two columns null together). A
  registration that keeps the secret leaves both alone.
- The token and pushed-authorization endpoints take either fingerprint, each compared in
  constant time, the replaced one only before `previous_secret_until`.
- Each registration is written down in the security partition as
  `auth.oidc.clientregistered` under the principal `register-client`, reason
  `AUTH-OIDC-001`, operation `configuration`, with `details.client`, `details.kind` and
  `details.changed` (whether the registry held the client before), and nothing of the
  secret.

*Contradiction (Tier 3).* OPS-SEC-002 AC1 has rotation complete without restart or
manual action, and D-026.3 has client secrets at rest under the key-encryption key with
automated rotation. D-162 item 66 and `ISecretSource` (each value read once, at startup)
have an application's sign-on secret come from the secrets manager, and the mail
server's secret sits in the mail server's own configuration, which the library does not
reach. Under this build a client secret is rotated in three human steps: a new secret in
the secrets manager, `janus register-client` run with it, and the application restarted
or the mail server reconfigured within the overlap. AC2 holds for client secrets and AC1
holds for the signing keys; AC1 for client secrets is read under D-162 item 66 as the
overlap that lets those steps be taken without an outage. The owner decides which
governs: a library that generates and hands out client secrets itself, or a lifecycle
outside the library with the overlap as its contract.

*Residue.* API-REDIR-001 AC3 reads the registry's origins at startup, so a registration
that changes a destination is taken by a running host at its next start. Until a
registration exists, a declared mail client the registry does not hold as a `protocol`
client faults as entry 262 has it, which is what tells an operator who skipped the
command.

*Rows for chapter 10.* Audit actions: `auth.oidc.clientregistered` | security |
`AuditActions.ClientRegistered` | A client was registered in the provider's registry, or
a registered one changed, from the server.

*Tests that pin it.*
`ClientRegistryTests.AUTH_OIDC_001_AC4_TheMailServerClientIsRegisteredFromTheServerAsync`,
`ClientRegistryTests.OPS_SEC_002_AC2_AReplacedSecretIsKeptThroughTheOverlapAsync`,
`ClientRegistryTests.OPS_SEC_002_AC2_TheOverlapFollowsTheAccessTokenLifetimeAsync`,
`ClientRegistryTests.OPS_SEC_002_AChangeThatKeepsTheSecretReplacesNothingAsync`,
`ClientRegistryTests.AUTH_OIDC_001_AClientTheRegistryCannotServeIsRefusedAsync`,
`ClientRegistryTests.OPS_SEC_001_ASecretTheServerWouldNotTakeIsRefusedAsync`,
`OidcStoreTests.OPS_SEC_002_AC2_AReplacedSecretIsKeptUntilTheOverlapEndsAsync`,
`OidcFlowTests.OPS_SEC_002_AC2_AReplacedSecretAuthenticatesTheClientThroughTheOverlapAsync`,
`RegisterClientTests.AUTH_OIDC_001_AC4_TheMailServerClientIsRegisteredAsTheDeploymentIsStoodUpAsync`,
`RegisterClientTests.OPS_SEC_002_AC2_RegisteringANewSecretKeepsTheReplacedOneThroughTheOverlapAsync`,
`RegisterClientTests.AUTH_OIDC_001_ARegistrationWithoutAUsableSecretIsRefusedAsync`,
`RegisterClientTests.AUTH_OIDC_001_AnArgumentTheCommandCannotTakeIsRefusedAsync`,
`AuditActionsTests.CONV_NAME_003_AC2_ChangingAnActionFailsTheContractTest` (extended).

*Chapter text that should change.* AUTH-OIDC-001 AC4 could read "registered with
`janus register-client` as the deployment is stood up" in place of "at bootstrap", and
OPS-SEC-003's values could name the command beside `rotate-kek`. OPS-SEC-002 needs the
owner's answer on AC1 for client secrets. Chapter 10 needs the audit-action row, and
section 4.9 could say that a replaced client secret keeps the signing keys' overlap.

---

## 341. How the key-encryption key's cryptoperiod is kept

**Phase 9 · 2026-09-25 · Tier 3 · DR-009a AC1, OPS-MAINT-001, OPS-ALERT-001, OPS-ALERT-002**

*The question.* DR-009a AC1 has the cryptoperiod defined and rotation occur at its
expiry. Chapter 06 section 9 puts the rotation inside the annual operation ("Annual, one
operation") and names it a maintenance exception, a human step, and OPS-MAINT-001 logs
it as the task `envelope-rotation`. OPS-MAINT-001 warns ahead of a licence's expiry, but
nothing warned ahead of the operation, and no chapter says what tells the owner that the
key's period is ending.

*The readings.* (1) The cryptoperiod is kept by the runbook's calendar (chapter 11's
schedule) and the library does nothing; AC1 is named as verified by inspection. (2) The
library warns ahead of the anniversary of the latest operation the maintenance log
records, as it warns of a licence. (3) The library warns ahead of the anniversary of the
latest rotation the audit trail records.

*Chosen: 2 (Tier 3, the strictest reading).* Under (1) the key outlives its period
whenever the owner forgets, which OPS-MAINT-001 AC2 rules out for a licence ("Warning
does not depend on anyone remembering"). Under (3) the area reads the trail through a
port it does not hold, and the other four parts of the operation go unwatched; the log
already records the operation as one task, dated and with its actor.

What is built:

- `EnvelopeRotationWatch`, run daily as the job `envelope-rotation` (reason `DR-009a`,
  operation `monitoring`). The cryptoperiod is one year from the latest
  `envelope-rotation` entry of the maintenance log. From `maintenance.expiry.warninglead`
  before its end, and for as long as no later entry is recorded, each look raises
  `expiry-approaching` under the scope `envelope-rotation` with `details.task`,
  `details.performedAt` and `details.dueAt`. A log that records no operation has it due
  now, both instants null. OPS-ALERT-002 keeps it to one alert a window.

*Residue.* The watch reads the log, not the key: an operation recorded without the
rotation silences it, and a rotation left unrecorded keeps it raised. The rotation
stays the human step of chapter 06 section 9, so AC1's "rotation occurs at its expiry"
is met by the warning and the command together, and is named so in the report. A fresh
deployment is raised until the first sealing of the envelope is recorded, as it is for a
missing break-glass credential (entry 331).

*Tests that pin it.*
`EnvelopeRotationWatchTests.DR_009a_AC1_TheOperationInsideTheLeadIsRaisedAsDueAsync`,
`EnvelopeRotationWatchTests.DR_009a_AC1_AnOperationWithinItsCryptoperiodRaisesNothingAsync`,
`EnvelopeRotationWatchTests.DR_009a_AC1_AnOperationUndoneOrNeverRecordedIsRaisedAsync`,
`EnvelopeRotationWatchTests.DR_009a_AC1_TheLeadIsTheConfiguredOneAsync`.

*Chapter text that should change.* DR-009a could state the cryptoperiod as one year from
the last annual operation and say it is warned of from the maintenance log.
OPS-MAINT-001 could list the annual operation beside the licences and permits it warns
of, under `expiry-approaching` with the scope `envelope-rotation`.

---

## 342. A restriction names no channel

**Phase 9 · 2026-09-25 · Tier 3 · AUTH-ABUSE-004, D-146, `10` section 4.5 `restrictions`**

*The question.* AUTH-ABUSE-004 gives a restriction a key, an optional purpose filter and
its buckets, and says every send evaluates every applicable restriction. The model has
no channel. The shipped names say one: `sms.destination`, `sms.source`,
`email.destination`. Read as the model states, a mail to an address answers to
`sms.destination` (3 a day) as well as `email.destination` (5 an hour), so the hourly
bucket is never the one that refuses, and a text message answers to the one a minute of
`email.destination`. The sending phase built the model as stated and no entry recorded
the question; it was found again in this phase.

*The readings.* (1) A restriction governs every send its key and purpose match, whatever
the channel; the names are names. (2) The prefix of a shipped name is a channel filter:
`sms.*` governs text messages and `email.*` mail. (3) A channel field is added to the
restriction beside the purpose.

*Chosen: 1 (Tier 3, the strictest reading).* It grants least: every send answers to
every restriction the model makes applicable, and no send escapes one because of how a
host named it. Reading (2) gives a name a meaning the chapter does not, and a host's
restriction named `sms.anything` would silently stop governing mail. Reading (3) adds a
field to the public restriction shape and to `10` section 4.5, which is the owner's.

*Residue.* A deployment that offers both channels to one person meets the tighter of the
two sets on each: three mails a day to one address, and one text message a minute to one
number. A host that wants channel limits apart edits the shipped restrictions; it cannot
yet say "this one is for mail".

*Tests that pin it.*
`SendingServiceTests.AUTH_ABUSE_004_ARestrictionGovernsEverySendWhateverItsNameAsync`,
`SendingServiceTests.AUTH_ABUSE_004_AC1_AFourthTextMessageInsideADayIsRefusedWithTheLiftAsync`,
`SendingServiceTests.AUTH_ABUSE_004_AC1_ASecondMailInsideAMinuteIsRefusedWithTheLiftAsync`.

*Chapter text that should change.* AUTH-ABUSE-004 could either give the restriction a
channel filter (`sms` · `email` · `any`) beside the purpose, with the shipped defaults
filtered by the channel their names give, or say that a restriction applies across
channels and that the shipped names are names only.

---

## 343. The client a social sign-in is carried by

**Phase 10 · 2026-09-25 · Tier 3 · IDN-LIFE-012, IDN-LIFE-012a, LIB-HOST-001 `SocialProvider`**

*The question.* IDN-LIFE-012 has a person sign in with, register with and link a Google
or Apple identity, and no chapter says how this application is a client of the
provider: which client identifier the browser is sent out under, how the code is
traded, or what the host declares for it. The `SocialProvider` that IDN-LIFE-012a
brought (entry 283) names the provider's key document and a list of client
identifiers, which is what an event is checked against, and nothing a sign-in needs:
no address to send the browser to, no address it comes back to, no secret.

*The readings.* (1) The library is no client of the providers; a host that wants
social sign-in runs the protocol itself and hands the library the provider's `sub`.
(2) The library is a confidential client of each declared provider under the first
client identifier the host lists, with the provider's discovery document, the return
address and the client secret declared beside it. (3) As (2), under whichever listed
client the token names.

*Chosen: 2 (Tier 3, the strictest reading).* Under (1) the proof that the person
controls the identity is the host's word, and REG-IDENT-008's "matched by the
provider's `sub`" is enforced by nobody the library can see. Under (3) a token minted
for any of the host's clients (a mobile application's, say) signs a browser in here,
which is the audience confusion OpenID Connect Core 3.1.3.7 step 3 exists to refuse.
Under (2) the identity token is accepted only where its `aud` is the client the round
trip was started under, its issuer is the one the discovery document names, its
signature verifies under the provider's published keys, it has not expired and its
nonce is the round trip's; an event (IDN-LIFE-012a) still takes any client listed.

What is built:

- `SocialProvider(Provider, Metadata, Configuration, Return, Secret, ClientIds)`:
  `Configuration` is the provider's discovery document, `Return` the address the
  provider sends the browser back to, `Secret` the client secret from the secrets
  manager (for Apple, the signed secret the host mints, since signing it is the host's
  key and its rotation the host's calendar), and the first of `ClientIds` the client a
  sign-in is started under.
- The discovery document is read through the client `identity-providers` and cached as
  the key document is (entry 283).

*Tests that pin it.*
`ProviderSignInTests.IDN_LIFE_012_ALinkedIdentitySignsInOverTheRoundTripAsync`,
`ProviderSignInTests.IDN_LIFE_012_AnIdentityTokenThatDoesNotHoldUpSignsNobodyInAsync`
(nonce, audience, expired, forged),
`ProviderSignInTests.IDN_LIFE_012_AProviderThatCannotBeReadIsNotStartedAsync`.

*Chapter text that should change.* LIB-HOST-001 could give `SocialProvider` the
discovery address, the return address and the secret, say that the first client
identifier is the one a sign-in runs under, and say that Apple's signed secret is
minted by the host; the rows owed for LIB-HOST-001 below carry the three members.

---

## 344. Where a round trip to a provider runs, and what it is bound to

**Phase 10 · 2026-09-25 · Tier 3 · IDN-LIFE-012, REG-IDENT-008, BFF-CSRF-005a, BFF-MACH-001, OPS-SEC-001**

*The question.* `09` lists `POST /account/link/{provider}` and
`DELETE /account/link/{provider}` and nothing else for social identities: no route
sends the browser to a provider and none takes it back. BFF-CSRF-005a names the
pre-authentication session as the binding target of the sign-on's `state`; nothing
says what a provider round trip is bound to. BFF-MACH-001 AC2 has machine endpoints
refuse a request carrying a session cookie, with one exception, the break-glass page.
A provider returns the browser by a top-level `GET` (Google), which carries the
session cookie of a signed-in browser linking an identity, or by a cross-site form
post (Apple, `response_mode=form_post`), which carries no cookie and no synchronizer
token and would be refused by the browser profile.

*The readings.* (1) The provider returns to a browser-profile route; Apple's form post
is refused, so Apple works only where it offers a query return. (2) The provider
returns to a machine-profile route that does everything, ignoring cookies; the
browser's own session is then unknown to the route that acts. (3) The provider returns
to a machine-profile route that only re-addresses the browser, by `303`, to a
browser-profile continuation, which reads the binding from what the browser carries
and does all the work under the browser profile's gates.

*Chosen: 3 (Tier 3, the strictest reading).* Nothing is decided on the machine
profile: the converter reads `code`, `state` and `error` from the form or the query and
answers `303` to `/auth/providers/{provider}/return` carrying the three in the query,
and nothing else. It ignores a cookie rather than refusing it, as the break-glass page
does, because a browser linking an identity is signed in and its cookie arrives with
the provider's `GET`; refusing it would make linking impossible. The continuation is a
browser-profile route like any other; its `state` check is the CSRF defence of the
round trip.

What is built:

- `GET /auth/providers/{provider}?intent=signin|register|link&returnTo=...` starts a
  round trip. `returnTo` is sanitised as the sign-on's is: anything but a local path is
  `/`.
- `GET|POST /callbacks/providers/{provider}/return` on the machine profile, governed
  and cookie-ignored, answers only the `303`.
- `GET /auth/providers/{provider}/return` finishes it on the browser profile.
- `identity.provider_attempts`: one row per browser, bound to its session where it has
  one and to its pre-authentication session otherwise, and deleted with it. It holds
  the provider, the intent, the fingerprints of the `state` and the `nonce`, the return
  path, and the PKCE verifier wrapped under the key-encryption key (OPS-SEC-001) and
  re-wrapped at a rotation (OPS-SEC-003). A second start replaces the first. The row is
  taken, and so forgotten, on the first return whichever way the return goes.
- A return that finds no row, or a row of another provider, or whose `state` does not
  match in fixed time, is refused `403 session.csrf.invalid` and logged.

*Residue.* The code travels in the continuation's query for one `303`, so it is in the
browser's history for as long as it takes the continuation to trade it; it is single
use and bound to the verifier and the client secret, which never leave the server.

*Tests that pin it.*
`ProviderSignInTests.BFF_MACH_001_TheProviderReturnSendsTheBrowserOnAsync` (GET, POST),
`ProviderSignInTests.BFF_CSRF_005a_AProviderReturnWithAnotherStateIsRefusedAsync`,
`ProviderSignInTests.BFF_CSRF_005a_AProviderReturnIsJudgedOnceAsync`,
`ProviderSignInTests.IDN_LIFE_012_AReturnAddressOffThisApplicationIsNotFollowedAsync`,
`ProviderAttemptStoreTests.IDN_LIFE_012_TheProofKeyIsAtRestUnderTheKeyEncryptionKeyAsync`,
`ProviderAttemptStoreTests.BFF_CSRF_005a_ABrowserHasOneRoundTripInFlightAsync`,
`ProviderAttemptStoreTests.IDN_LIFE_012_ARoundTripGoesWithWhatItIsBoundToAsync`,
`KeyRotationTests.OPS_SEC_003_AC3_AProofKeyInFlightIsReWrappedAsync`.

*Chapter text that should change.* `09` could list the three routes. BFF-MACH-001 AC2
could name the provider return beside the break-glass page as a machine route that
ignores a cookie, and say it only re-addresses the browser. BFF-CSRF-005a could name
the provider round trip's `state` beside the sign-on's.

---

## 345. How the code is traded, and what is asked of the provider

**Phase 10 · 2026-09-25 · Tier 2 · IDN-LIFE-012**

*The question.* No chapter says how this client authenticates at a provider's token
endpoint, whether it uses PKCE, or how it asks for the browser to be returned.

*The readings.* For the client's authentication: (1) `client_secret_post`, the secret
in the form; (2) `client_secret_basic`; (3) whichever the discovery document lists
first. For PKCE: (a) always; (b) where the discovery document lists `S256`. For the
return: (i) always a query; (ii) `form_post` where the document lists it.

*Chosen: 1, b, ii.* Both providers take the secret in the form, and Apple takes it
nowhere else, so one method serves both and the library holds no negotiation. A
provider that does not list `S256` may refuse a request carrying a challenge it does
not know (Apple lists none), so the verifier is drawn only where it will be checked,
and the `nonce` binds the token to the round trip either way. `form_post` keeps the
code out of the provider's redirect and is what Apple requires when it is asked for
the email scope. The scope asked for is `openid email` and nothing else.

*Tests that pin it.*
`ProviderSignInTests.IDN_LIFE_012_ALinkedIdentitySignsInOverTheRoundTripAsync`,
`ProviderSignInTests.IDN_LIFE_012_AProviderThatTakesNoProofKeyIsSentNoneAsync`.

*Chapter text that should change.* IDN-LIFE-012 could state the scope, the client
authentication method, and that PKCE and `form_post` are used where the provider's
discovery document lists them.

---

## 346. Linking and unlinking, and the order of their refusals

**Phase 10 · 2026-09-25 · Tier 3 · IDN-LIFE-012 AC3, `09` `POST /account/link/{provider}`, `10` section 5a `provider:link` and `provider:unlink`, D-128**

*The question.* `09` gives `POST /account/link/{provider}` a `204` and no body: linking
needs the provider's round trip, which a single `POST` cannot carry. And AC3 refuses
unlinking the only remaining credential, while `provider:unlink` asks for step-up; a
session the provider alone signed in is `delegated` and passes no step-up (D-128), so
for an account whose only way in is the provider, a gate asked first answers
`auth.stepup.required` forever and the `409` AC3 names is never reached.

*The readings.* For the `POST`: (1) it starts the round trip itself; (2) it answers
`204` where the account may link the provider now (a session, the `provider:link`
step-up passed, the provider in the policy), and the round trip is started by
`GET /auth/providers/{provider}?intent=link`, which asks the same again and links on
the return. For the order: (a) the gate first, then the last-credential check; (b) the
last-credential check first, then the gate.

*Chosen: 2 and b (Tier 3, the strictest reading).* Under (2) the frontend learns
before it leaves whether a link would be refused, and the link itself is judged again
when the identity comes back, so the pre-check grants nothing. Under (b) a refusal that
holds whatever the session could prove is given without asking the session to prove
anything, and the unlink changes nothing unless the gate passes too; the order only
changes which refusal a person sees. The credential route (`DELETE
/account/credentials/{id}`) keeps its gate first, since removing a credential is its
own gate (`factor:remove`), and so answers `403 auth.stepup.required` to a delegated
session. Unlinking an identity the account does not hold is `404
auth.credential.notfound`. An identity linked to another account, or a second identity
at a provider the account already holds, is refused `auth.factor.rejected`.

*Tests that pin it.*
`ProviderSignInTests.IDN_LIFE_012_AC1_LinkingChangesNoGrantAndNoMembershipAsync`,
`ProviderSignInTests.IDN_LIFE_012_AC2_UnlinkingLeavesTheAccountUsableByWhatItKeepsAsync`,
`ProviderSignInTests.IDN_LIFE_012_AC3_UnlinkingTheOnlyRemainingCredentialIsRefusedAsync`,
`ProviderSignInTests.IDN_LIFE_012_LinkingAsksForTheStepUpItsGateDeclaresAsync`,
`ProviderSignInTests.IDN_LIFE_012_UnlinkingWhatIsNotLinkedFindsNothingAsync`,
`ProviderSignInTests.IDN_ACCT_001_AC1_DeletingTheLinkedCredentialLeavesTheAccountUsableAsync`.

*Chapter text that should change.* `09` could say that the `POST` is the check made
before the round trip and name the start route, and say that the last-credential
refusal precedes the step-up.

---

## 347. What a provider's address is taken as at registration

**Phase 10 · 2026-09-25 · Tier 3 · REG-IDENT-008, REG-IDENT-010, IDN-ACCT-001**

*The question.* REG-IDENT-008 names the provider-operated mailboxes and the answers,
and leaves four things open. (a) AC3 says a linked `sub` signs the person in and "no
registration session is created", but the registration session exists before the
email step Continue with Google belongs to. (b) A provider-operated address already on
another account: vouching for it would verify one address on two accounts. (c)
`email_verified` is a boolean at Google and has been the string `"true"` at Apple.
(d) Which addresses are Apple's relay.

*The readings.* (a1) the registration session is left as it was; (a2) it is abandoned
and the person signed in. (b1) the address is verified as for a fresh one; (b2) it is
staged as a typed duplicate is: unverified, no code, the holder told. (c1) a boolean
only; (c2) a boolean, or the string `"true"`. (d) `privaterelay.appleid.com`, beside
`icloud.com`, `me.com` and `mac.com`, and no subdomain of any.

*Chosen: a2, b2, c2, d (Tier 3, the strictest reading).* (a2) leaves no half-built
registration behind a person who has signed in instead, so nothing staged in it can be
completed later by whoever holds the browser. (b2) grants nothing on an address
another account holds; the answer differs from the fresh provider-operated case, but
only to the person the provider has just shown controls the mailbox, who is sent the
notice there anyway, so nobody learns what they could not already. (c2) reads each
provider's documented form and nothing else: any other value is unverified. An address
the provider does not call verified is sent a code whoever operates it. The domains are
carried by each provider's catalogue entry (`FactorProperties.OperatedDomains` and
`OperatesHostedDomain`), so no rule names a provider (AUTH-FACT-001 AC1).

*Tests that pin it.*
`RegistrationServiceTests.REG_IDENT_008_AC1_AGmailAddressIsVerifiedByTheSignInAndReachesConfirmAsync`,
`RegistrationServiceTests.REG_IDENT_008_AWorkspaceDomainTheTokenAssertsIsVerifiedByTheSignInAsync`,
`RegistrationServiceTests.REG_IDENT_008_AnAppleRelayAddressIsVerifiedByTheSignInAsync`,
`RegistrationServiceTests.REG_IDENT_008_AC2_AThirdPartyAddressFromAppleIsSentOneCodeAsync`,
`RegistrationServiceTests.REG_IDENT_008_AnAddressTheProviderDoesNotCallVerifiedIsSentACodeAsync`,
`RegistrationServiceTests.REG_IDENT_008_AC3_ALinkedIdentityMakesTheAttemptASignInAsync`,
`RegistrationServiceTests.REG_IDENT_008_AC4_AnAddressAnotherAccountHoldsAnswersAsAFreshOneAsync`,
`RegistrationServiceTests.REG_IDENT_008_AC4_AProviderOperatedAddressAnotherAccountHoldsIsNotVouchedForAsync`,
`RegistrationServiceTests.IDN_ACCT_001_AC2_AnAccountRegisteredThroughAProviderHasItsOwnRecordAsync`,
`FactorCatalogueTests.REG_IDENT_008_TheMailboxesAProviderOperatesAreTheOnesTheItemNames`,
`ProviderSignInTests.REG_IDENT_008_AC1_ContinueWithGoogleVerifiesAGmailAddressAsync`,
`ProviderSignInTests.REG_IDENT_008_AnAppleRelayAddressIsVerifiedOverTheRoundTripAsync`,
`ProviderSignInTests.REG_IDENT_008_AC2_AThirdPartyAddressFromAppleIsSentOneCodeAsync`,
`ProviderSignInTests.REG_IDENT_008_AC3_ALinkedIdentityOnTheRegistrationSignsInAsync`,
`ProviderSignInTests.REG_IDENT_008_AC4_AnAddressAnotherAccountHoldsAnswersAsAFreshOneAsync`.

*Chapter text that should change.* REG-IDENT-008 AC3 could read "the registration
session is abandoned"; the requirement could say a provider-operated address another
account holds is not verified by the sign-in, name the relay domain, and say how
`email_verified` is read.

---

## 348. How a refused round trip is answered, and what is logged

**Phase 10 · 2026-09-25 · Tier 2 · IDN-LIFE-012, CONV-CONTENT-001, CONV-LOG-003, BFF-ERR-001**

*The question.* The start and the continuation are navigations, not calls the frontend
makes, so a JSON refusal would be the page the person sees. And a provider's refusal
carries `error_description`, text the provider wrote.

*The readings.* (1) Answer every refusal as JSON, as the browser profile does. (2)
Send the browser back to `returnTo` with the code in `error`, and answer JSON only
where the round trip itself cannot be trusted. (3) Answer every refusal by a
redirect.

*Chosen: 2.* A refusal of the person's attempt (the provider refused, the token did not
hold up, the account may not link, the registration is gone) returns the browser to
where it started, with `error` set to the code, placed before any fragment; the
frontend writes the words (CONV-CONTENT-001). A return whose binding or `state` fails
is answered `403 session.csrf.invalid` as JSON and sent nowhere, since it may not be
the person's return at all. The log records the provider and the trace, and never the
code, the `state` or anything the provider wrote.

*Tests that pin it.*
`ProviderSignInTests.IDN_ACCT_001_AnIdentityLinkedToNoAccountSignsNobodyInAsync`,
`ProviderSignInTests.IDN_LIFE_012_AnIdentityTokenThatDoesNotHoldUpSignsNobodyInAsync`,
`ProviderSignInTests.IDN_LIFE_012_AProviderThatCannotBeReadIsNotStartedAsync`,
`ProviderSignInTests.BFF_CSRF_005a_AProviderReturnWithAnotherStateIsRefusedAsync`.

*Chapter text that should change.* BFF-ERR-001 could say that a navigation route
answers a refusal by returning the browser with the code in `error`.

---

## 349. What startup asks of a declared social provider

**Phase 10 · 2026-09-25 · Tier 2 · LIB-HOST-001, AUTHZ-MODEL-003, entry 343**

*The question.* Entry 343 adds three members to `SocialProvider`; entry 283's startup
checks cover the key document and the client identifiers only.

*The readings.* (1) Check nothing more, and let a bad value fail at the first sign-in.
(2) Refuse at startup what can be judged there.

*Chosen: 2.* Startup refuses, under `model.startup.declarationmissing`, a discovery
address that is not absolute HTTPS (`configuration`), a return address that is not
absolute HTTPS or whose path does not end in `/callbacks/providers/{provider}/return`
for the provider it is declared for (`return`), and an empty secret (`secret`). A
return address the provider will not accept is not something startup can know.

*Tests that pin it.*
`StartupValidationTests.IDN_LIFE_012a_ASocialProviderDeclaredShortOfWholeIsRefusedAsync`
(configuration, return, secret).

*Chapter text that should change.* AUTHZ-MODEL-003's refusals could list the three.

---

## 350. The disclosure at link time

**Phase 10 · 2026-09-25 · Tier 2 · AUTH-FACT-002a AC5, CONV-CONTENT-001, R-A16**

*The question.* AC5 has linking a provider to an account holding a second factor show
the disclosure. The library writes no sentence (CONV-CONTENT-001), and the round trip
leaves the application before the provider is asked anything.

*The readings.* (1) The library shows it, which it cannot. (2) The frontend shows it
before it starts the round trip, knowing from the credentials it already reads whether
the account holds a second factor.

*Chosen: 2.* AC5 is not decided by a library test; it is named in the phase report as
verified in the frontend, which reads the account's credentials before it offers the
link.

*Tests that pin it.* None in the library; the criterion is named in the phase 10
report under criteria a test cannot decide.

*Chapter text that should change.* AUTH-FACT-002a AC5 could name FE-API-004 or the
frontend chapter as where the disclosure is verified.

---

## 351. The social sign-in is the library's own client, not the handler `08` names

**Phase 10 · 2026-09-25 · Tier 3 · CONV-DESIGN-008, BFF-SESS-002, BFF-CSRF-005 AC3 and AC4, BFF-MACH-001, OPS-SEC-001, entries 163 and 343**

*The question.* CONV-DESIGN-008 names `Microsoft.AspNetCore.Authentication.OpenIdConnect`
as the OIDC client for Google and Apple. That handler keeps the round trip in the
browser: a correlation cookie and a nonce cookie of its own naming, and the `state` as
an encrypted copy of the round trip's properties, the PKCE verifier among them, under
ASP.NET Core's data-protection keys. Chapter 17 fixes the cookies the library sets at
three `__Host-identity-*` cookies (BFF-SESS-002, D-153) and issues none with
`SameSite=None` (BFF-CSRF-005 AC3). A provider that returns by form post (Apple, which
requires it when the address is asked for) withholds a `Lax` cookie, so the handler's
correlation cookie would have to be `SameSite=None` for Apple to work at all. The
handler also takes its callback in middleware ahead of the endpoints, where BFF-MACH-001
has an endpoint's protection derive from where it is mounted, and its data-protection
keys are neither under the key-encryption key nor in the secrets manager (OPS-SEC-001).

*The readings.* (1) Use the handler as `08` names it and let chapter 17 give way: two
more cookies, one of them `SameSite=None`, and the verifier leaving the server under
keys the envelope does not hold. (2) Use the handler for Google only, where the query
return lets its cookies be `Lax`, and leave Apple unbuilt. (3) Carry the round trip on
the pattern the sign-on client already follows (entry 163): the state, the nonce and
the verifier held on the server, bound to what the browser already carries, the
provider's return taken on the machine profile and continued as a GET on the browser
profile (BFF-CSRF-005 AC4), and the identity token validated by
`Microsoft.IdentityModel`, which the handler itself validates with and which the
permitted OpenID Connect packages already bring.

*Chosen: 3 (Tier 3, the strictest reading).* It keeps every chapter 17 rule and
OPS-SEC-001 whole, holds the verifier where the envelope protects it, sets no cookie
the chapter does not name, and leaves nothing unbuilt; the cryptographic judgement of
the token is still Microsoft's code, not the library's. What it costs is protocol code
the handler would have carried: building the authorization request, the form post of
the exchange, and the checks the handler makes. Each of those checks has a test.
`Microsoft.AspNetCore.Authentication.OpenIdConnect` stays in `Directory.Packages.props`,
so CONV-DESIGN-008 AC1 holds, and no project references it.

*Tests that pin it.*
`ProviderSignInTests.IDN_LIFE_012_ALinkedIdentitySignsInOverTheRoundTripAsync`,
`ProviderSignInTests.IDN_LIFE_012_AnIdentityTokenThatDoesNotHoldUpSignsNobodyInAsync`,
`ProviderSignInTests.BFF_MACH_001_TheProviderReturnSendsTheBrowserOnAsync`,
`ProviderSignInTests.BFF_CSRF_005a_AProviderReturnWithAnotherStateIsRefusedAsync`,
`ProviderSignInTests.BFF_CSRF_005a_AProviderReturnIsJudgedOnceAsync`,
`ProviderAttemptStoreTests.IDN_LIFE_012_TheProofKeyIsAtRestUnderTheKeyEncryptionKeyAsync`.

*Chapter text that should change.* CONV-DESIGN-008 could drop the row for the OIDC
client, or keep it and say the handler is not used because of BFF-SESS-002 and
BFF-CSRF-005, naming `Microsoft.IdentityModel` as what validates the provider's token.

---

## 352. A host registers and moves its records through `IResources`

**Phase 10 · 2026-09-25 · Tier 3 · AUTHZ-INHERIT-001, AUTHZ-INHERIT-002, AUTHZ-SCOPE-001, AUTHZ-MODEL-003, LIB-API-001, LIB-API-005, LIB-HOST-002, PRIV-SENS-002**

*The question.* AUTHZ-INHERIT-002 has the ancestry written in the same transaction as
the resource create or move, and LIB-HOST-002 has the library read nothing of the
host's, so the host has to tell the library when it creates or moves a record. Phases
3 and 4 built that as the internal `IResourceStore`; no public type reached it, and the
tests wrote `identity.resources` and `identity.ancestry` by hand. A host built on the
package had no way to register a record, so no grant on a container reached anything
in it. This is a latent defect from phase 3, found while building the conformance
sample host. No chapter names the call.

*The readings.* (1) The host writes the two tables itself, since LIB-API-001 makes the
ancestry closure's structure and semantics public: the ancestry is then the host's
arithmetic, and nothing stops a record being placed in a container of another
organization or of a type the declaration does not contain it in. (2) A public
operation in `Janus.Core` the host calls inside its own unit of work, registering one
record, many at once, or moving one, which judges each placement against the
declaration and the organization before writing. (3) Reading 2 with a removal as well.

*Chosen: 2 (Tier 3, the strictest reading).* `IResources` with `RegisterAsync`,
`RegisterManyAsync` and `MoveAsync`, each returning a `Result`, registered scoped by
`AddJanus` and joining the host's open `IUnitOfWork` (the outermost transaction wins,
so a host rollback leaves neither the record nor its ancestry). A registration carries
the record, its organization, its container and, for PRIV-SENS-002, whose data it is.
Refusals are `api.request.malformed` naming the member: `resourceType` for a type the
declaration does not name; `resourceId` for a record already registered, or listed
twice in one batch, or a move of one never registered; `containedIn` for a container
of a type other than the one declared, no container where the type does not belong to
the organization, a container not registered (or later in the same batch), or a
container of another organization. A bulk batch is judged whole before anything is
written, and its lookups are one query per type. The call checks no permission: it
grants nothing, and whether a caller may create a record in a container is the
host's own permission, asked of `IAccessGate` before the host writes. No removal is
added, since chapter 03 names only create and move; a removed host row leaves an
ancestry row that no host query can reach, and a reused identifier is refused. A
containment cycle is not checked per record, because AUTHZ-MODEL-004 refuses one among
the types and a record is only placed in a container of its declared type.

*Tests that pin it.*
`ResourceRegistrationTests.AUTHZ_INHERIT_002_AC1_ARecordTheHostRegistersInheritsFromItsContainerAsync`,
`ResourceRegistrationTests.AUTHZ_INHERIT_002_AC1_AHostsRollbackLeavesNeitherTheRecordNorItsAncestryAsync`,
`ResourceRegistrationTests.AUTHZ_INHERIT_002_AMovedRecordInheritsFromItsNewContainerAloneAsync`,
`ResourceRegistrationTests.AUTHZ_INHERIT_002_ABulkRegistrationTakesContainersBeforeContentsAsync`,
`ResourceRegistrationTests.AUTHZ_SCOPE_001_ARecordIsNotPlacedInAnotherOrganizationsContainerAsync`,
`ResourceRegistrationTests.AUTHZ_MODEL_003_ARecordSitsOnlyWhereItsTypeIsDeclaredToAsync`,
`ResourceRegistrationTests.AUTHZ_MODEL_001_OnlyADeclaredTypeIsRegisteredAndOnlyOnceAsync`.

*Chapter text that should change.* AUTHZ-INHERIT-002 could name `IResources` as the
create and move it speaks of, with the refusals above; LIB-API-005 could list it among
the operations contract; chapter 10 section 1 could list `resourceType`, `resourceId`
and `containedIn` as the members `api.request.malformed` names for it.

---

## 353. The suite judges a declaration by the checks startup runs, and each failure by its own code

**Phase 10 · 2026-09-25 · Tier 2 · LIB-TEST-001 AC3, AUTHZ-MODEL-004**

*The question.* LIB-TEST-001 AC3: the suite "validates the model declaration and
reports each failure distinctly". The library already refuses a declaration as it
starts (AUTHZ-MODEL-004), one failure at a time, each under its own code of chapter 10
section 1.5. The criterion does not say whether "each failure" is every failure of one
declaration or each kind of failure.

*The readings.*

1. A second validator in the package that carries on past the first failure and
   reports every failure of the declaration at once.
2. The suite runs the library's own checks, the registration a host makes, and
   reports the refusal as a finding carrying the refusal's code and details, so each
   kind of failure is reported as itself.

*Chosen: 2.* The checks stay in one place: a second copy in the package could pass a
declaration startup refuses, which is the failure the suite exists to catch. The
suite calls `AddJanus` on a collection of its own, over key material drawn for the
check and cleared after it and an address no name resolves to (RFC 2606), so nothing
is reached. A containment cycle, a reference to an undeclared type, a type with no
path to an organization, a purpose whose basis needs an assessment and names none, a
derivation from an undeclared relationship and a purpose with no data category are each
reported under their own code, naming the value at fault. Like startup, the judgement
stops at the first failure, so a declaration failing two ways reports the second once
the first is corrected. A refusal the model raises with no code (a type or
relationship declared twice, a purpose on a basis the declaration does not name) is
not a finding, since a finding is a code; it propagates as the `StartupException`. The
two checks that read the database (a stored role allowing an undeclared permission, a
derivation column no index reaches) are startup's and not this check's; a host's
deployment starting is what proves them.

*Tests that pin it.*
`ConformanceSuiteTests.LIB_TEST_001_AC3_TheSampleDeclarationHoldsTogether`,
`ConformanceSuiteTests.LIB_TEST_001_AC3_EachMalformedDeclarationIsReportedByItsOwnCode`.

*Chapter text that should change.* LIB-TEST-001 AC3 could read "reports each kind of
failure under its own code"; chapter 10 section 1.5 could give the two codeless
refusals a code, so the suite can report them as findings.

---

## 354. An entity has a policy when it is a declared type, a declared relationship's rows or a contract table

**Phase 10 · 2026-09-25 · Tier 3 · LIB-TEST-001 AC1, AUTHZ-GATE-001 AC3, CONV-TEST-003**

*The question.* LIB-TEST-001 AC1: the suite "verifies every entity has a registered
policy". A host's context maps more than its resource types: the rows of the
relationships its derivations follow (AUTHZ-DERIVE-004), which the gate reads through
the declaration, and the library's two contract tables, which `MapAuthorizationTables`
maps into it. No chapter says which of them has a policy.

*The readings.*

1. Only a declared resource type has a policy; every other mapped entity is a finding.
2. A declared resource type, the rows of a declared relationship and the two contract
   tables are covered; an owned type is covered by its owner, since it is read only
   through it; every other entity is a finding.

*Chosen: 2 (Tier 3).* Reading 1 is stricter but has no conforming configuration any
chapter describes: a relationship's rows are the gate's own input, registered with it
by the declaration and read by it, and the gate renders no filter over them, so
reading 1 would ask a host to declare its facts as resource types with purposes and
an organization path, which nothing asks. The contract tables are the library's. Each
entity outside the three is reported under `authz.policy.unregistered` with `entity`
naming its full type name, in ordinal order. An entity is judged by its type, so one
type mapped twice is reported once.

*Tests that pin it.*
`ConformanceSuiteTests.LIB_TEST_001_AC1_EveryEntityTheSampleHostMapsHasAPolicy`,
`ConformanceSuiteTests.LIB_TEST_001_AC1_AnEntityWithoutAPolicyIsFound`.

*Chapter text that should change.* AUTHZ-GATE-001 AC3 and LIB-TEST-001 AC1 could say
that a declared relationship's rows and the contract tables count as registered, or
chapter 03 could give a relationship's rows a read policy of their own.

---

## 355. The host states its truth table in the library's scenarios, and the suite writes each case

**Phase 10 · 2026-09-25 · Tier 2 · LIB-TEST-001 AC2, AUTHZ-TEST-001, AUTHZ-PRIN-001, OPS-DATA-002, LIB-HOST-002**

*The question.* LIB-TEST-001 AC2: the suite "runs the truth table through both check
and filter and asserts agreement". AUTHZ-TEST-001 has the table enumerate outcomes
across relationship, permission and condition for each resource type, and AC1 lists
the situations it covers. No chapter says who writes the table of a host's own types,
in what terms, or how the state each case needs comes to exist in a deployment whose
host rows the library never reads (LIB-HOST-002).

*The readings.*

1. The host writes every case's state itself, and the suite only asks the two paths.
2. The host states each case as a scenario, a permission and the expected outcome,
   and the suite writes the state the scenario names, with the host writing only its
   own rows through a seam it implements.

*Chosen: 2.* The table stays a reviewable list of rows in the host's code, which is
AUTHZ-TEST-001's point, and the scenarios are the library's, so a case means the same
in every deployment. The seventeen scenarios cover AC1's list (a grant on the record,
its container, above its container, on the organization; to a group and to a nested
group; deny over a grant, and deny on the container over a grant on the record; an
expired grant; a grant in another organization; a derived grant, one on the
container, and deny over one) and add a grant on a sibling, no grant, a revoked grant
and a role that does not allow the permission. A scenario the type's declaration
cannot place (a container case on a type with none, a derived case on a type deriving
nothing) is refused before anything is written.

Each case is written in an organization of its own: the organization, the granter,
the person, a role of the case's own allowing the case's permission (allowing nothing
for the role case), one record at every level of the type's chain and a sibling of
the record, registered through `IResources` after the host has written its rows for
them. The library's own rows are written by hand-written statements into the
library-owned tables, whose structure is public (LIB-API-001), so the gate is asked
about a database in a stated shape rather than one its own writers produced; a grant
is dated two hours back, and an expired or revoked one an hour back. A derived case
has the declared role allow the case's permission, the role being the deployment's
and so allowing every permission a table has derived it for; the type's first
derivation is the one used; the host writes the fact through its seam, and a
materialised derivation is refreshed as the host's own write refreshes it. The filter
is applied to the host's own rows in the host's own context. The suite writes into the
database, so it runs against a deployment kept for it. The host opens the connection
the suite writes through: OPS-DATA-002 has nothing but the accessor retrieve a
connection, and its gate scans the package, so the strictest reading keeps the package
from opening one.

*Tests that pin it.*
`ConformanceSuiteTests.LIB_TEST_001_AC2_EveryShelfCaseAgreesThroughCheckAndFilterAsync`,
`ConformanceSuiteTests.LIB_TEST_001_AC2_EveryBinderCaseAgreesThroughCheckAndFilterAsync`,
`ConformanceSuiteTests.LIB_TEST_001_AC2_EverySheetCaseAgreesThroughCheckAndFilterAsync`,
`ConformanceSuiteTests.LIB_TEST_001_AC2_ACaseDecidedOtherwiseThanTheTableStatesIsReportedAsync`.

*Chapter text that should change.* LIB-TEST-001 AC2 could say the host states the
table in the library's scenarios, and list them; OPS-DATA-002 could say whether the
conformance package is the service layer its AC2 speaks of.

---

## 356. The provider probe a host runs asks what a registered client can be refused

**Phase 10 · 2026-09-25 · Tier 2 · AUTH-OIDC-006 AC1, LIB-TEST-001, LIB-HOST-003, entry 280**

*The question.* Entry 280 kept AUTH-OIDC-006 AC1's refusals as the library's own
tests and had phase 10's host-run suite run the same refusals. Two of them, the
exact-match rule and the code exchange, need a code, and a code needs a person
signed in, which a host's deployment kept for the suite has no way to produce without
the suite holding a credential.

*The readings.*

1. The host-run probe asks every refusal of entry 280, signing a person in through a
   credential the host hands it.
2. The host-run probe asks every refusal a registered client can provoke without a
   person, and the two that need one stay the library's own tests.

*Chosen: 2.* This narrows entry 280. Reading 1 would have a conformance package hold
a person's credential, which it has no business holding. The probe reads the
discovery document under the issuer and takes the pushed-request and token endpoints
from it, so it assumes no path of the deployment's (LIB-HOST-003). It asks for the six
implicit and hybrid response types (`unsupported_response_type`), the password,
client-credentials, implicit, device-code and token-exchange grants
(`unsupported_grant_type`), the plain proof key, a challenge naming no method and no
challenge (`invalid_request`), and a push without the client's secret
(`invalid_client`), each with everything else in order so what is refused is the
form. It checks the document lists `code` alone, `S256` alone, the code and refresh
grants alone, pushed requests as required, and no unauthenticated method. Each form
admitted is a finding naming the probe, the field, what was sent, the refusal expected,
the status and the error that came back; each listing is a finding naming the member
and what it lists.

*Tests that pin it.*
`ConformanceSuiteTests.AUTH_OIDC_006_AC1_TheSampleHostsProviderRefusesEveryRetiredFormAsync`,
`ConformanceSuiteTests.AUTH_OIDC_006_AC1_AProviderAdmittingWhatItShouldRefuseIsReportedAsync`.

*Chapter text that should change.* AUTH-OIDC-006 AC1 could say the exact-match rule
and the exchange are the library's own tests and the rest the host's; LIB-TEST-001
could name the provider's refusals among what the host-run suite verifies.

---

## 357. The suite answers reports of findings, each a code, under two new codes

**Phase 10 · 2026-09-25 · Tier 2 · LIB-TEST-001, LIB-API-001, LIB-API-003, CONV-CONTENT-001, CONV-LAYOUT-001**

*The question.* LIB-TEST-001 ships the suite as `Janus.Conformance`, the one further
project with public types (CONV-LAYOUT-001), and names what it verifies but not what a
host calls or what comes back. Chapter 10 section 1 has no code for a truth-table case
decided otherwise than stated, or for a provider admitting a retired form.

*The readings.*

1. The suite is a set of test classes a host inherits, whose assertions fail with a
   sentence.
2. The suite is four calls a host makes from its own test, each answering a report
   whose findings are codes with structured data, and the host's test asserts the
   report conforms.

*Chosen: 2.* A sentence would be library wording crossing to a host (CONV-CONTENT-001,
LIB-API-003), and inheriting a test class would tie the package to the host's test
framework, which no chapter names. The public surface is `ConformanceSuite` with
`Policies`, `Declaration`, `TruthTableAsync` and `ProviderAsync`;
`ConformanceReport`, whose `Conforms` is whether it holds no finding;
`ConformanceFinding`, the check and the failure; `ConformanceCheck`; `TruthTableCase`
and `TruthTableScenario`; `IConformanceRows<TResource>`, the host's own rows of a type;
and `ConformanceClient`, the registered client the provider is asked as. A finding
reuses the code that names its condition where one exists (`authz.policy.unregistered`,
the model's own startup codes) and otherwise carries one of two new codes,
`authz.truthtable.disagreement` and `auth.oidc.nonconformant`. No request raises
either, and the status table holds every code, so both are listed at 500.

*Tests that pin it.* The ten tests of `ConformanceSuiteTests`, and
`ErrorCodesTests` and `ApiStatusTests` over the two codes.

*Chapter text that should change.* Chapter 10 section 1 should hold the two rows under
"Rows for chapter 10"; LIB-API-001 could list the conformance package's surface.

---

## 358. A category's retention floor is declared with its purposes, and its period defaults to the floor

**Phase 10 · 2026-09-25 · Tier 3 · PRIV-RET-001, LIB-HOST-001, `10` section 4.7, D-107, D-152, entries 108 and 180**

*The question.* Chapter 10 section 4.7 gives `retention.<host-category>` the default
"the floor the host declares for that category (LIB-HOST-001)", fails startup for a
declared category without one, and enforces the floor; PRIV-RET-001 AC2 rejects
configuration below the floor at validation. LIB-HOST-001 lists no floor, and entry 108
built the check as a settings row the deployment has to write before it starts. No
route writes a member of the family (entry 180) and the bootstrap takes none, so a
deployment declaring any category had no supported way to start: the sample host of
LIB-TEST-001 wrote the row by hand.

*The readings.*

1. Entry 108 stands: the row is required, and a way to write it is added (a bootstrap
   argument or a route).
2. The floor is part of the declaration, beside the purposes whose categories it
   bounds. The key defaults to it, a category with no floor is refused when the model
   is built, and a stated period below the floor is refused at startup.

*Chosen: 2 (Tier 3), revising entry 108.* It is what section 4.7 says, and it is the
reading that keeps most: a category has a period from the moment it is declared, and
configuration cannot lower the floor because the floor is not configuration. Reading 1
needs a write path no chapter names. The builder takes `RetentionFloor(category,
floor)` and refuses a blank category, one that cannot be the last segment of a key, a
period that is not positive, and a second floor for one category. Building the model
refuses a purpose over a category with no floor under `model.startup.declarationmissing`
naming `retention.<category>` (LIB-HOST-001 AC2), and refuses without a code, as it
refuses a duplicate, a floor for a category no purpose is over. At startup and in the
records of processing a category is kept for the period the deployment stated where it
stated one and for the floor where it did not. A stated period below the floor is
refused with `config.value.belowfloor` naming the key and the floor: startup stops, and
the register flags the category `retention-missing` rather than show a period nobody
may keep it for. Nothing is raised to the floor silently. No route writes a member
(entry 180 unchanged), so a deployment keeping a category longer than its floor
declares the longer floor or states the period in the settings table.

*Tests that pin it.*
`AuthorizationModelTests.PRIV_RET_001_AC1_ACategoryWithNoRetentionFloorFailsStartup`,
`AuthorizationModelTests.PRIV_RET_001_AC1_AFloorForACategoryNoPurposeIsOverFailsStartup`,
`AuthorizationModelTests.PRIV_RET_001_AC2_AFloorIsAPositivePeriodDeclaredOnce`,
`ConfigurationCoverageTests.PRIV_RET_001_AC1_ADeclaredCategoryWithNoFloorFailsStartupAsync`,
`ConfigurationCoverageTests.PRIV_RET_001_AC1_EveryDeclaredCategoryStartsOnItsFloorAsync`,
`ConfigurationCoverageTests.PRIV_RET_001_AC2_APeriodStatedBelowTheFloorFailsStartupAsync`,
`ProcessingRecordsTests.PRIV_RET_001_AC3_TheRetentionOfEachCategoryIsOnTheRowAsync`,
and the sample host of `ConformanceSuiteTests`, which starts on its floor with no row.

*Chapter text that should change.* LIB-HOST-001's "Processing purposes and lawful
bases" row could add "and a retention floor for each data category a purpose is
over"; PRIV-RET-001's row for host-declared categories could say the floor is part of
the declaration and the key defaults to it; `09` section 8 could say whether a member
of `retention.<category>` is written at runtime.

---

## 359. A version is judged at the commit that releases it, by what the shipped surface lost or gained

**Phase 10 · 2026-09-25 · Tier 2 · LIB-TEST-002 AC1, LIB-VER-001, LIB-VER-002, CONV-SETUP-003 AC2, CONV-VCS-005 AC3**

*The question.* LIB-TEST-002 AC1: "Altering a public signature without a version bump
fails the build." The version is the tag's (LIB-VER-001, CONV-VCS-005) and no project
file carries one, so between releases there is no version to raise. CONV-SETUP-003
fails the build on a public change without its line in the unshipped file, and moves
lines to the shipped file only in a release commit. No chapter says how a release
commit is recognised, what the version it names has to be, or where a major version's
migration note lives; the workflow that publishes is Milestone 2 step 1.

*The readings.*

1. The unshipped line is the bump: the analyser is the whole of AC1, and the version is
   chosen at release by whoever releases.
2. The unshipped line records the change and the release commit is judged. A commit
   that changes a shipped file is a release: it adds one dated version section to the
   changelog, above every other, and leaves Unreleased and every unshipped file empty.
   The version it names raises the major part where the shipped surface lost a line,
   at least the minor part where it gained one, and follows the one before it in every
   case. A section opening a major version links its migration note, a file in the
   repository. A release tag names the version its commit added.

*Chosen: 2.* Reading 1 lets a removed signature ship under a minor version, which is
the failure AC1 names. The rules are `.github/gates/release.sh`, run over the pushed
range in the `Public surface files up to date` job, the CONV-SETUP-003 row of
CONV-GATE-001. A lost shipped line is a changed or removed signature, since the
analyser records a change as the old line removed and the new one added. The first
version has no predecessor and so opens a major version, and links its migration note
as Milestone 2 step 12 publishes one with 1.0.0. Where a note lives is not fixed: the
gate asks for a link to a file in the repository. Publishing the package, and the
version MinVer derives from the tag, stay with the release workflow of Milestone 2
step 1.

*Tests that pin it.* None in the solution: the rule is a gate over commits and tags,
which a test cannot make. It was exercised in this phase against a scratch repository,
fourteen cases passing and failing as stated: a plain commit; a first release with and
without its note; a shipped change outside a release; a removal released as a minor,
and as a major with and without its note; an addition released as a patch and as a
minor; a minor raise keeping a patch number; a patch release; a release leaving lines
under Unreleased and in an unshipped file; a tag on a commit releasing nothing; and a
range across a merge. Over the repository's whole history it passes.

*Chapter text that should change.* LIB-TEST-002 AC1 could say the bump is judged at the
release commit; CONV-VCS-005 could say where the migration note of LIB-VER-002 lives.

---

## 360. The library-owned schema is read from a migrated database and held to a committed file

**Phase 10 · 2026-09-25 · Tier 2 · LIB-API-001 AC2, LIB-TEST-002 AC2, CONV-TEST-002, CONV-GATE-002**

*The question.* LIB-API-001 lists the database schema (all library-owned tables) and
the ancestry closure's structure as contract; its AC2 has a change to any caught by a
contract test before release, and LIB-TEST-002 AC2 fails the build on a change to the
closure's structure. No test held the schema. The model's configuration covers the
tables the context maps, but the view a host's filter reads (`effective_grants`) and
the partitioning of the audit records exist only in the migrations. CONV-TEST-002's
contract kind runs with no container, on every push (CONV-GATE-002).

*The readings.*

1. The structure is read from the model at design time, as a contract-kind test, and
   the view and the partitioning are left to the migrations.
2. The structure is read from a migrated database: every table and view in the
   library's schema with its columns, constraints and indexes, the monthly leaf
   partitions aside, compared with a committed file, as an integration-kind test.

*Chosen: 2.* Reading 1 leaves out the one relation a host's own SQL reads beside the
closure. The rendering is one query over the catalogue, and the file is
`tests/Janus.Storage.Tests/schema.txt`, beside its test as `configuration-keys.txt` is
beside its own: an intended change is written into it in the same commit, where it is
the reviewable diff. A month's partition is created by the day the migration or the
sweep runs, so the leaf partitions are not part of the contract; the partitioned
tables above them are. The closure's ten lines are also stated in the test named for
LIB-TEST-002 AC2, so restructuring it means changing a test that says it cannot be.
Being integration-kind, both run in the `Integration tests` job, on a pull request and
on the default branch, where a red check blocks the merge.

*Tests that pin it.*
`SchemaContractTests.LIB_API_001_AC2_TheLibraryOwnedSchemaIsTheContractAsync`,
`SchemaContractTests.LIB_TEST_002_AC2_TheAncestryClosureStructureIsTheContractAsync`.

*Chapter text that should change.* CONV-TEST-002 could say that a contract test which
needs the database is of the integration kind.

---

## 361. The first version's section is prepared as what 1.0.0 holds, under Added alone

**Phase 10 · 2026-09-25 · Tier 2 · CONV-VCS-005, LIB-VER-001, the plan's phase 10 row and Milestone 2 step 12**

*The question.* The plan has phase 10 deliver "`CHANGELOG.md` with the first version
section prepared under `Unreleased`", and Milestone 2 step 12 releases 1.0.0 by moving
`Unreleased` to `1.0.0`. CONV-VCS-005 has every behaviour change add its line under
`Unreleased`, written for a reader. Nothing has been released, so the section had
grown by one entry a commit: 232 entries under Added, and 66 under Changed, 17 under
Fixed and 3 under Removed that describe states of the library no release ever held.

*The readings.*

1. `Unreleased` is already the prepared section: every behaviour change carries its
   line there, and step 12 moves it as it stands.
2. `Unreleased` is rewritten to read as the first version's notes: what 1.0.0 holds,
   under Added, each Changed and Fixed entry folded into the entry it amends as the
   behaviour it states, and the Removed entries dropped, since against no earlier
   version nothing was changed, fixed or removed.

*Chosen: 2.* A reader of 1.0.0 never met the states the Changed, Fixed and Removed
entries are measured against, and an entry saying a type was removed or a fault fixed
tells them of a package they could not have installed. Keep a Changelog groups a
version's changes against the version before it, and 1.0.0 has none. Reading 1 would
leave step 12 to publish notes about unreleased history. Every fact about current
behaviour is kept: a code, a key, a path, a type or member name, a status or a
duration in a folded entry is in the Added entry it now amends; 212 remain. From here to 1.0.0 a
behaviour change adds its line as CONV-VCS-005 has it, and the release gate of entry
359 judges the release commit.

*Tests that pin it.* None: the section is prose. The `Changelog line present` job
still requires a line from every change to the library's code.

*Chapter text that should change.* CONV-VCS-005 could say that the first version's
section records what the version holds rather than its changes.

---

## 362. A processing restriction refuses every change to the account's own settings in the operation that makes it

**Phase 10 · 2026-09-25 · Tier 3 · IDN-ACCT-007 AC2, AUTHZ-GATE-006, PRIV-RIGHT-004, D-162 section D**

*The question.* IDN-ACCT-007 AC2 has a restricted account read its own data and
exercise its rights, and neither perform host write operations nor change settings.
The gate refuses the host's modifying actions (AUTHZ-GATE-006), but nothing refused a
change to the account's own settings: a restricted account holding a session could edit
its profile, change its identifiers and enrol credentials. D-162 section D retired
`identity.account.restricted` in favour of `authz.restricted`. Neither chapter lists
which operations are settings, and the table of IDN-ACCT-007 gives a restricted account
sign-in, read only, while the sign-in paths (`AuthenticationService`, `SignInLinks`,
`OidcService.ClaimsAsync`) admit an active account alone.

*The readings.*

1. The refusal is a mark on the account endpoints, asserted by the stage that holds
   an endpoint to a session.
2. The refusal is in each operation that changes a setting, so a host calling the
   same contract in process meets it too.
3. As 2, and every operation a restricted account performs on itself is refused,
   rights and sessions included.

*Chosen: 2 (Tier 3).* Reading 1 leaves the in-process contracts (LIB-API-005) open, and
reading 3 takes away the rights the criterion keeps. AUTHZ-GATE-006 AC2 and
PRIV-RIGHT-004 AC3 have the restriction enforced through the gate and not by scattered
checks, so the gate answers it: `AccessGate` refuses a settings change with
`authz.restricted` (403) beside every other refusal of a restricted subject, and the
account's operations ask it first through a port of their own (`ISettingsRestriction`),
bridged where the library is composed (the gate named there alone, LIB-SEAM-001 AC1),
as the gate asks a session's step-up through `ISessionGates`. The operations that ask are the profile edit, the photo, the preferences, a
credential's label and the preferred second step (`IAccount`), every identifier change
made under a session (add, verify, make primary, backup, remove, replace), and every
credential operation, which `CredentialService` resolves in one place (password,
passkey, generator, recovery codes, removal, upgrade, provider link and unlink). Left
open: reading, ending sessions, the privacy rights (consents, objections, requests,
export, deletion and its cancellation), and the links that undo or abandon an identifier
change, which carry no session and only restore or drop a change begun before. App
passwords already answer only an active account. Acknowledging an invitation is
membership of an organization rather than a setting of the account and is left as it
is. The sign-in paths are not changed in this phase: admitting a restricted account at
sign-in widens what an account in that state reaches, and is left for the owner's
review; until then a restricted account reads its data and exercises its rights through
a session it already held or out of band (PRIV-RIGHT-001 to PRIV-RIGHT-004).

*Tests that pin it.*
`GateBehaviourTests.IDN_ACCT_007_AC2_TheGateRefusesARestrictedAccountsSettingsAsync`,
`AccessSeamTests.AUTHZ_GATE_006_AC2_TheRestrictionIsReadAndRefusedInOnePlace`,
`AccountServiceTests.IDN_ACCT_007_AC2_ARestrictedAccountReadsAndChangesNoSettingAsync`,
`IdentifierServiceTests.IDN_ACCT_007_AC2_ARestrictedAccountChangesNoIdentifierAsync`,
`CredentialServiceTests.IDN_ACCT_007_AC2_ARestrictedAccountChangesNoCredentialAsync`.

*Chapter text that should change.* IDN-ACCT-007 could list what "change settings"
covers and say that the refusal is `authz.restricted`, and say whether the sign-in
paths admit a restricted account, since the table and the code disagree.

---

## 363. A replacement ends the other sessions when it applies, and the session that completes an identifier change rotates

**Phase 10 · 2026-09-25 · Tier 2 · IDN-LIFE-008 AC1, BFF-SESS-004, REG-IDENT-006 AC4, REG-IDENT-007**

*The question.* IDN-LIFE-008 has the session rotate and every other session end on the
removal or the replacement of a sign-in identifier. A removal ended the others and
rotated nothing; a replacement did neither. A replacement is staged by one request and
applies when its new value is verified, by a code typed under a session, by a link
pressed in the staging browser, or from an enrolment session with no browser session at
all, so the request that stages it is often not the one that completes it.

*The readings.*

1. The other sessions end, and the asking session rotates, when the replacement is
   staged.
2. The other sessions end when the replacement applies, keeping the session that staged
   it; the session that completes a change rotates at the boundary that completes it.

*Chosen: 2.* Until the swap applies the old value still signs in, so ending sessions at
staging would end them for a change that may never happen. When the swap applies, every
session but the one that staged it ends (all of them where an enrolment session staged
it). A session can only be rotated where its browser receives the new secret, so the
endpoints rotate: the removal, and a verification that completes under a live session,
whether a code was typed or a link was pressed in the same browser. That rotates on the
completion of an addition too, which is also a change to what signs in to the account
(BFF-SESS-004's privilege change). A staging session whose change completes elsewhere
is kept and is not rotated, since its browser is not there to receive a new secret.

*Tests that pin it.*
`IdentifierServiceTests.IDN_LIFE_008_AC1_EveryOtherSessionEndsWhenAReplacementAppliesAsync`,
`AccountApplicationTests.BFF_SESS_004_AC2_RemovingAnIdentifierRotatesTheSessionAsync`,
`IdentifierServiceTests.REG_IDENT_006_AC4_EveryOtherSessionEndsOnRemovalAsync`.

*Chapter text that should change.* IDN-LIFE-008 could say that a replacement ends the
other sessions when it applies, and that the rotation is of the session that completes
the change.

---

## 364. A test class uses a container when it takes one as a fixture or constructs one

**Phase 10 · 2026-09-25 · Tier 2 · CONV-TEST-002 AC1**

*The question.* CONV-TEST-002 AC1 says "Unit tests run without containers." The
exit-sweep row would decide it by refusing any unit or contract class that "uses"
`DatabaseFixture`, `HostFixture`, `SampleHost`, `ContainerRestore` or
`BootstrappedDeployment`. `KeyMaterialTests` (unit) names `HostFixture` three times.
Each time it calls the static `HostFixture.Declaration()`, which builds an
authorization declaration and starts nothing.

*The readings.*

1. Any mention of a type that starts a container is a use.
2. A use is anything that makes an instance, and so starts the container: asking the
   framework for the type as a fixture (`IClassFixture<T>`, `ICollectionFixture<T>`,
   `AssemblyFixture(typeof(T))`) or constructing it (`new T`, `T x = new(...)`). A
   static member call is not a use.

*Chosen: 2.* The criterion is about where a container runs, and a static call runs
none. Reading 1 would fail `KeyMaterialTests` today. Passing it would mean moving
`Declaration()` off the fixture, a refactor of the tests the criterion does not ask
for.

The set of types that start a container is not written down; the test derives it.
It begins with the types that reach the container package, then adds every type that
makes an instance of one, and repeats until nothing new is found. A new fixture is
therefore covered without editing the test. Today the derived set is the row's list
plus `VolumeFixture`.

*Tests that pin it.* `LibraryStructureTests.CONV_TEST_002_AC1_NoUnitOrContractClassUsesAContainer`.

*Chapter text that should change.* CONV-TEST-002 could say that a test class of the
unit or contract kind neither takes as a fixture nor constructs a type that starts a
container.

---

## 365. Every column that carries a time of day is `timestamp with time zone`

**Phase 10 · 2026-09-25 · Tier 2 · PRIV-RET-003 AC2**

*The question.* PRIV-RET-003 AC2 says "All stored timestamps are UTC." The schema
holds two `date` columns that are calendar days: `privacy_requests.received_at` and
`read_volume.day`. The test must tell a stored timestamp from them without a list,
because a list would hide a new offender.

*The readings.*

1. By name: a column ending `_at` or `_since` is an instant. `received_at` is a day,
   so this needs a list of exceptions.
2. By type, instants only: every `timestamp` or `timestamptz` column must be
   `timestamptz`.
3. By type, anything carrying a time of day: every `timestamp`, `timestamptz`, `time`
   or `timetz` column (or array of them) must be `timestamptz`. A `date` falls
   outside, since it has no time of day.

*Chosen: 3.* Reading 1 needs the list the criterion's test must not hide behind.
Reading 3 is the stricter of the two type readings. A time of day stored without a
date cannot be converted at display, and `timetz` keeps an offset rather than UTC,
so refusing both fails closed. No such column exists today. If one is ever wanted,
changing the test's query is the deliberate step.

*Tests that pin it.* `SchemaContractTests.PRIV_RET_003_AC2_EveryStoredInstantIsInUtcAsync`.

*Chapter text that should change.* PRIV-RET-003 could say that an instant is stored
as `timestamp with time zone`, and that a calendar day is stored as a `date` and is
not an instant.

---

## 366. The delivered rows the sweep removes are the events every consumer took; the outbox rows stay

**Phase 10 · 2026-09-25 · Tier 2 · IDN-PRIN-003 AC4, IDN-LIFE-003a, IDN-LIFE-003b, IDN-LIFE-003**

*The question.* IDN-PRIN-003 has "Delivered outbox records, once every subscriber has
confirmed" removed when spent, and AC4 has the sweeps of that column run without a
person. The library holds two such tables. The `events` table carries the events offered
to the host's consumers; nothing reads a row once every consumer has taken it. The
`outbox` table carries the erasure and takedown deliveries to the declared subscribers,
and it is also the erasures table IDN-LIFE-003b describes, whose status `complete` means
every required subscriber confirmed, read by `GET /admin/erasures/{id}`; and a
takedown's progress, with the instant its erasure falls due, is read from its row. Neither table was
ever swept.

*The readings.*

1. Remove both: an `events` row once published, an `outbox` row once `complete`.
2. Remove the `events` rows once published; keep the `outbox` rows, which are the record
   of an erasure or a takedown that IDN-LIFE-003b and chapter 09 section 8a read after
   the subscribers confirmed.
3. Remove neither.

*Chosen: 2.* Reading 1 would answer `privacy.erasure.notfound` for an erasure that
completed, and not find a takedown whose window is still open; IDN-LIFE-003b gives the
erasure row a `complete` status, which only a kept row can carry, so that row is a record
of something that happened. Reading 3 leaves the spent working artefact the right column
names. The expiry sweep removes every `events` row whose last consumer took it
(`published_at` set). A row whose retry budget was spent was delivered to nobody, so it
stays.

*Tests that pin it.*
`BackgroundJobsTests.IDN_PRIN_003_AC4_AnEventEveryConsumerTookIsClearedWithNobodyAskingAsync`.

*Chapter text that should change.* IDN-PRIN-003 could name the event rows as the
delivered outbox records removed when spent, and say that the erasures table
(IDN-LIFE-003b) is kept.

---

## 367. Failed authentication and failed step-up are recorded in the audit trail, under two new actions

**Phase 10 · 2026-09-25 · Tier 3 · CONV-LOG-005 AC1, IDN-AUD-001 AC1, AUTH-ABUSE-001, AUTH-ABUSE-003, CONV-LOG-003, CONV-LOG-004**

*The question.* CONV-LOG-005 says security events SHALL be logged regardless of level
configuration: failed authentication, denied authorization, step-up, configuration
change and break-glass use. AC1 says raising the minimum level does not suppress them.

- Every level is subject to a host's filter (`LogLevel.None` drops Critical), so no
  `ILogger` call can meet AC1.
- Denied authorization (`authz.access.denied`), configuration change
  (`ops.configuration.changed`), break-glass use (`auth.breakglass.used`) and step-up
  (`auth.session.presented`) are already in the audit trail, which no log level governs.
- Failed authentication and failed step-up were recorded nowhere.
- No chapter spells an action for either. Chapter 10 holds no list of audit actions; the
  list is this ledger's "Rows for chapter 10".

*The readings.*

1. "Logged" means an `ILogger` call at a level no filter drops. That cannot be built.
2. "Logged" means recorded where no log level reaches, which is the audit trail. Failed
   authentication and failed step-up each gain an action.
3. As 2, but "step-up" means only the step-up that succeeded, so a failed step-up needs
   nothing.

*Chosen: 2 (Tier 3, the strictest reading, which keeps most).*

- **Spellings.** The spellings are invented here: `auth.authentication.failed`
  (`AuditActions.AuthenticationFailed`) and `auth.stepup.failed`
  (`AuditActions.StepUpFailed`), both in the security category. They follow the
  `area.object.pastverb` shape of the catalogue. The contract test
  (`AuditActionsTests.Catalogue`) and the rows below are the reference.
- **What counts as a failed authentication.** Two cases:
  - A factor presented at sign-in and refused, whether the identifier resolved to no
    account, the account is not active, or the factor itself was refused.
  - A refused break-glass code.
- **What counts as a failed step-up.** A factor presented at `/auth/step-up` and
  refused, including against a challenge that is not the asker's.
- **What a failed-authentication row holds.**
  - The acting subject is the nil subject, because no actor was established.
  - The effective subject is the account the challenge resolved to (an inactive one
    included), or the reserved account for a break-glass code, or the nil subject where
    there is none.
  - The details hold `factor` alone.
  - The row holds no identifier as typed, no presented value, no source address and no
    organization.
- **What a failed-step-up row holds.** The session's account as both subjects, and the
  details `session` and `factor`.
- **Identities.** IDN-AUD-001 AC1 holds, since both identity fields are populated. The
  nil subject is what a system-principal row carries as its acting subject. The
  database constraint that admits a row naming nobody stays limited to
  `authz.access.denied` (entry 136).
- **AUTH-ABUSE-003.** Both sign-in refusals reach `CountedAsync` and each writes one
  row. An identifier that resolves to nothing therefore costs the same work as one that
  resolves to an account.
- **What limits the rate.**
  - Sign-in rows are written after the throttle's delay check, so the progressive delay
    of AUTH-ABUSE-001 bounds them.
  - Break-glass rows are written after the global limit (five an hour) and the source's
    delay.
  - Step-up refusals are not throttled: AUTH-ABUSE-001 names sign-in, so a live session
    presents step-up factors with no delay and each refusal is a row, bounded only by
    the request rate. That is left for the owner.
- **Order.** The row is written in a transaction of its own, before the attempt is
  counted, so a failure to count does not lose the record.
- **Not recorded.** A wrong device-verification code, a sign-in link that does not
  land, a refused delegated sign-in, a refused provider sign-in, and an unknown or
  expired challenge handle at `PresentAsync`. The handle path runs before the throttle,
  so recording it would let anyone write rows without limit into a table under security
  retention; the others are not a factor presented against a challenge, and the device
  check has no `Factor` that names it. Which of them CONV-LOG-005 means is left for the
  owner.

*Tests that pin it.* `RequestLoggingTests.CONV_LOG_005_AC1_RaisingTheLogLevelSuppressesNoSecurityEventAsync`,
`AuthenticationServiceTests.PresentAsync_ARefusedFactor_IsRecordedWhetherOrNotAnAccountHoldsTheIdentifierAsync`,
`SessionAuditTests.FailedAsync_AgainstAnAccount_NamesTheAccountAndTheFactorAloneAsync`,
`SessionAuditTests.FailedAsync_AgainstNoAccount_IsTakenNamingNoAccountAsync`,
`SessionAuditTests.StepUpFailedAsync_NamesTheAccountAndTheSessionAsync`,
`AuditActionsTests.IDN_AUD_001_TheSetOfActionsIsClosed`.

*Chapter text that should change.*

- CONV-LOG-005 could say "recorded in the audit trail" where it says "logged", and name
  the action for each of the five events.
- IDN-AUD-001 could say that a failed authentication names no acting identity (the nil
  subject), and the account attempted where there is one.
- Chapter 10 could carry the two rows below.

| Action | Category | Catalogue member | Written when |
| --- | --- | --- | --- |
| `auth.authentication.failed` | security | `AuditActions.AuthenticationFailed` | A factor presented at sign-in, or the break-glass credential, was refused. The acting subject is the nil subject; the effective subject is the account the attempt was made against, or the nil subject where the identifier resolved to none or the break-glass code was refused before the reserved account was read; `details.factor` names the factor. Nothing that was typed is written. The row names no organization. (CONV-LOG-005) |
| `auth.stepup.failed` | security | `AuditActions.StepUpFailed` | A factor presented to step a live session up was refused, including against a challenge that is not the asker's. The acting and effective subject is the session's account; `details.session` names the session and `details.factor` the factor. The row names no organization. (CONV-LOG-005) |

---

## 368. The library opens no log scope, and its refusal entries name the identifier in the message

**Phase 10 · 2026-09-25 · Tier 2 · BFF-LOG-001 AC1, CONV-LOG-002 AC1, BFF-ERR-002 AC2**

*The question.* CONV-LOG-002 says every log entry carries a correlation identifier, the
one a concealment response carries. BFF-LOG-001 says every request carries it through
logging. The identifier is `HttpContext.TraceIdentifier`, which the server assigns. No
chapter says whether the library opens a scope to carry it. An endpoint refusal logged
nothing, so a denial's identifier resolved to no entry of the library's.

*The readings.*

1. The library opens a scope at the first stage of each profile, carrying
   `CorrelationId`.
2. The library opens no scope. The server opens one per request, carrying `RequestId`
   equal to `TraceIdentifier`, so every entry of the request carries the identifier
   wherever the host's provider records scopes. The library's refusal entries also
   carry `{CorrelationId}` in their message, so they resolve even where the provider
   does not record scopes. The one writer logs every refusal it answers.

*Chosen: 2.*

- The identifier is assigned where the server's scope opens, and that scope is opened
  once. A second scope would carry the same value under a second name.
- A scope cannot help a provider that drops scopes. The message can.
- The server's behaviour was checked against Kestrel on the ASP.NET Core 10 shared
  framework with a scratch app outside the repository. Every framework and application
  entry of a request carried `RequestId` equal to `TraceIdentifier`.
- A refusal is logged at Information, so a default configuration keeps a denial
  resolvable. A fault is logged at Error, with its code and its structured details. A
  cost: an anonymous `GET /auth/session`, answered `auth.session.expired`, now writes
  one Information line.

*Tests that pin it.* `RequestLoggingTests.BFF_LOG_001_AC1_ADenialsIdentifierResolvesToThatRequestsEntriesAsync`,
`ConcealmentTests.CONV_LOG_002_AC1_TheIdentifierInADenialResolvesToThatRequestsEntriesAsync`,
`RequestLoggingTests.BFF_ERR_002_AC2_AFaultsDetailIsRetrievableByItsCorrelationIdentifierAsync`.

*Chapter text that should change.*

- CONV-LOG-002 could say the identifier is the server's request identifier. Entries
  carry it through the host's request scope, and the library's own refusal entries also
  name it in their message.
- BFF-LOG-001 could say every refusal is logged by its code, and a fault with its
  details.


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
| `privacy.erasure.notfound` | 1.4 | 404 | An erasure is read or completed at `/admin/erasures/{id}` by a caller holding `privacyrequest:manage`, and no erasure is held under the identifier, including where it names a delivery of another kind (IDN-LIFE-003b, entry 264). |
| `model.startup.redirectclient` | 1.5 | 500 | Startup: a registered client's return address is not an absolute address with a host, or `redirect.defaultclient` names no registered browser application. `details.client` names the client the bad address was read from; `details.key` names the setting where the configured default will not resolve (API-REDIR-001). |
| `identity.identifier.invalid` | 1.1 | 422 | The value is not a well-formed identifier of its kind. (REG-IDENT-001, entry 40) |
| `identity.identifier.locked` | 1.1 | 409 | The identifier is locked: an invitation bound it, a provider operates the mailbox, or it is the personal email a membership keeps and is removed, made primary or replaced during that membership, so nothing about it is the person's to change. (REG-IDENT-010, REG-MAIL-001, entries 40 and 243) |
| `identity.identifier.maximum` | 1.1 | 409 | The account or the registration already holds as many identifiers of the kind as it may; where the maximum is one, the change is a replace. (REG-IDENT-002, REG-IDENT-007) |
| `identity.registration.incomplete` | 1.1 | 422 | The step a registration request is for is not the step the registration has reached: its predecessor is incomplete, or it is complete already. (REG-SESS-002, REG-SESS-004) |
| `identity.profile.invalid` | 1.1 | 422 | A profile field is not one the library admits: a display name over its byte bound, or a legal name over its length. (REG-PROF-001, entry 40) |
| `identity.profile.notaccepted` | 1.1 | 422 | The deployment does not take the field from the person: its key is off, or it is the date of birth, which is corrected through support. (REG-PROF-001, REG-IDENT-009, entry 40) |
| `auth.credential.notfound` | 1.2 | 404 | The account holds no such credential. (AUTH-FACT-001, entry 40) |
| `auth.credential.labelinvalid` | 1.2 | 422 | A credential label is empty, longer than the bound, or already held by another credential of the same kind on the account. (AUTH-FACT-001, REG-PM-002, entry 40) |
| `auth.credential.notupgradable` | 1.2 | 409 | The credential named for an upgrade to a passkey is not a second-factor security key. (AUTH-FACT-002b) |
| `authz.role.inuse` | 1.3 | 409 | A grant or a derivation names the role, so it cannot be removed; its permissions can be changed instead. (AUTHZ-GRANT-004, AUTHZ-GRANT-003 AC3, entry 189) |
| `authz.group.inuse` | 1.3 | 409 | The group holds a member, belongs to a group, or was given a grant, so it cannot be removed. (AUTHZ-GROUP-001, AUTHZ-GRANT-003 AC3, entry 192) |
| `identity.domain.unverified` | 1.1 | 422 | A listed domain is verified and no TXT value at `_identity-verify.<domain>` is `identity-domain-verification=<token>`, or the lookup could not be made; nothing is written. (REG-DOM-001, entry 209) |
| `identity.invitation.notfound` | 1.1 | 404 | `GET /account/invitation` or the acknowledgement is asked of an account no standing invitation is attached to: none of its links was opened by it, or each it opened was acknowledged or revoked. (REG-INV-002, entry 242) |
| `model.startup.subscribername` | 1.5 | 500 | Startup: two subject-event subscribers are registered under one name, or one under `erasure-ledger`, the name the erasure ledger's confirmation is recorded under. `details.handler` names it; nothing starts (IDN-LIFE-003a, DR-016, entry 332). |
| `authz.resource.notfound` | 1.3 | 404 | The browser profile answers a request in which the gate refused a record of a type that conceals its records, whether or not the record exists, and whatever the endpoint wrote after the refusal. `details.correlation` is the audit record of the refusal; nothing else is carried (AUTHZ-CONCEAL-001, BFF-ERR-003, entry 339). |
| `authz.truthtable.disagreement` | 1.3 | 500 | A conformance finding, raised by no request: a case of the host's truth table that the single check or the list filter decides otherwise than the table states. `details` carry `type`, `scenario`, `permission`, `expected`, `check` and `filter` (LIB-TEST-001 AC2, entry 355). |
| `auth.oidc.nonconformant` | 1.2 | 500 | A conformance finding, raised by no request: the provider admitted a form AUTH-OIDC-006 retires, or its discovery document lists one. `details` carry `probe`, `field`, `sent`, `expected`, `status` and `error`, or, for the document, `probe`, `member` and `listed` (AUTH-OIDC-006 AC1, entry 356). |

## LIB-HOST-001, host declarations

| Declaration | Required | Absent |
| --- | --- | --- |
| `PasskeyAddresses` (`changePassword`, `enrol`, `manage`) | yes, no default | Startup fails with `model.startup.declarationmissing`; `details.key` names `passkeyAddresses` or the field of it that is empty. The addresses are the frontend pages `/.well-known/change-password` and `/.well-known/passkey-endpoints` point at (REG-PM-001). |
| `AuthenticationAddresses` (`signIn`, `provider`) | yes, no default | Startup fails with `model.startup.declarationmissing`; `details.key` names `authenticationAddresses.signIn` or `authenticationAddresses.provider`. The first is where an authorization request that is not silent and holds no session is forwarded (AUTH-SESS-012 AC3). The second is the address the library is mounted at on the authentication application, which is where another application finds `/oidc/authorize` and `/oidc/token` (BFF-SESS-006). |
| `SignOnClient` (`clientId`) | yes, no default | Startup fails with `model.startup.declarationmissing`; `details.key` names `signOnClient.clientId`. The identifier is what this application calls itself at the provider when it establishes its own session, and the registry holds the one destination a code returns to under it. The secret it presents is not a declaration: it comes from the secrets manager through `ISecretSource.ReadSignOnSecretAsync` and is passed to `AddJanus`, which refuses to start without it with `model.startup.kekunavailable` and `details.key` naming `signOnSecret` (BFF-SESS-006, OPS-SEC-001). |
| `IDnsResolver` (`TextRecordsAsync`) | optional | No startup refusal. Every verification of a locked domain answers `identity.domain.unverified` and every scheduled check fails and raises `domain-reverification-failed`, so no domain is ever proved. A deployment that locks no domain needs none (REG-DOM-001, entry 212). |
| `IMailServer` (`ProvisionAsync`, `MailboxesAsync`, `AppPasswordsAsync`, `CreateAppPasswordAsync`, `RevokeAppPasswordAsync`) | optional | No startup refusal. No mailbox is pushed and none is compared; the rows are still written, and the first pass after a registration pushes every state owed. A push carries a key that stays the same until the server confirms it, the address in its canonical form and the state `disabled`, `enabled` or `removed`; the server applies a key once. The listing answers every mailbox the server hosts with whether it is enabled. The three app-password calls carry the person's token and act on the account the server finds in it; the creation answers the server's new secret and its identifier, and a revocation of an identifier the server does not hold for that person answers `auth.credential.notfound`. Without a registration every app-password operation answers `authz.denied`. A deployment whose staff mail is hosted elsewhere needs none (INT-MAIL-006, INT-MAIL-008, INT-MAIL-009, INT-MAIL-010, entries 215, 262 and 263). |
| `MailServerClient` (`clientId`) | where `IMailServer` is registered, no default | Startup fails with `model.startup.declarationmissing`; `details.key` names `mailServerClient.clientId`. The identifier is the registry's `protocol` client the mail server trusts, which the library issues the person's token to for the app-password calls; it presents no secret, since the library issues the token itself (INT-MAIL-010, AUTH-OIDC-001 AC4, entry 262). |
| `SocialProvider` (`provider`, `metadata`, `configuration`, `return`, `secret`, `clientIds`) | optional, once per social provider | No startup refusal where none is declared: every event of that provider is refused as a rejected callback, and no round trip to it starts. One declared twice, naming a factor that is not a social provider, with a `metadata` or `configuration` address that is not absolute HTTPS, with a `return` address that is not absolute HTTPS or whose path does not end in `/callbacks/providers/{provider}/return` for the provider it declares, with an empty `secret`, or with no client or an empty one stops startup with `model.startup.declarationmissing` and `details.key` naming `socialProvider.provider`, `socialProvider.metadata`, `socialProvider.configuration`, `socialProvider.return`, `socialProvider.secret` or `socialProvider.clientIds`. `metadata` is the provider's document naming `issuer` and `jwks_uri` (Google's Cross-Account Protection configuration, Apple's discovery document); `configuration` is the provider's OpenID Connect discovery document a sign-in reads its endpoints from; `return` is the address registered at the provider that the browser comes back to; `secret` is the client secret from the secrets manager, for Apple the signed secret the host mints. `clientIds` are the audiences an event for the deployment names, and the first is the client a sign-in is started under and the only audience its identity token may name (IDN-LIFE-012, IDN-LIFE-012a, entries 283, 343 and 349). |
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
| `auth.breakglass.generated` | security | `AuditActions.BreakGlassGenerated` | The break-glass credential was generated, a first issue or a replacement. The acting and effective subject is who generated it: a system administrator, or the reserved account from a break-glass session; `details.credential` names the issue and `details.replaced` the one it replaced, where there was one. The row names no organization. (OPS-BOOT-004, entry 291) |
| `auth.breakglass.used` | security | `AuditActions.BreakGlassUsed` | The break-glass credential was used and opened the emergency session. The acting and effective subject is the reserved account; `details.credential` names the issue and `details.session` the session it opened. The row names no organization. (OPS-BOOT-002) |
| `auth.credential.countermismatch` | security | `AuditActions.CredentialCounterMismatch` | An authenticator presented a signature counter that did not advance, which is what a cloned credential looks like. (AUTH-FACT-002) |
| `auth.credential.enrolled` | security | `AuditActions.CredentialEnrolled` | A credential was enrolled on an account. (AUTH-FACT-001) |
| `auth.credential.invalidated` | security | `AuditActions.CredentialInvalidated` | A credential was invalidated by a loss report that took effect. (AUTH-REC-004) |
| `auth.credential.invalidationheld` | security | `AuditActions.CredentialInvalidationHeld` | An invalidation was held rather than carried out, because carrying it out would leave the account with no way in. (AUTH-REC-004) |
| `auth.credential.removed` | security | `AuditActions.CredentialRemoved` | A credential was removed from an account. (AUTH-FACT-001) |
| `auth.credential.reportcancelled` | security | `AuditActions.CredentialReportCancelled` | A loss report was cancelled before it took effect. (AUTH-REC-004) |
| `auth.credential.restored` | security | `AuditActions.CredentialRestored` | A credential a provider's event held stands again, because the person signed in by another factor. Details carry `credential`. (IDN-LIFE-012a, entry 288) |
| `auth.credential.reportedlost` | security | `AuditActions.CredentialReportedLost` | A credential was reported lost, which starts the window before it is invalidated. (AUTH-REC-004) |
| `auth.mailcredential.created` | security | `AuditActions.MailCredentialCreated` | The mail server generated an app password at its holder's request. Details carry `credential`, the server's identifier; neither the secret nor the label is written. The account is both subjects; the row names no organization. (REG-MAIL-002, INT-MAIL-010, entry 263) |
| `auth.mailcredential.revoked` | security | `AuditActions.MailCredentialRevoked` | The mail server revoked an app password at its holder's request. Details carry `credential`, the server's identifier. The account is both subjects; the row names no organization. (REG-MAIL-002, INT-MAIL-010, entry 263) |
| `auth.providerevent.rejected` | security | `AuditActions.ProviderEventRejected` | A social provider's security event about a linked identity was refused: its provider's keys do not verify it, or it had been carried before. Details carry `credential`, `event` (the type as the provider spells it) and `outcome` (`unsigned` or `replayed`); the account is both subjects. (IDN-LIFE-012a AC1, entry 289) |
| `auth.providerevent.taken` | security | `AuditActions.ProviderEventTaken` | A social provider's security event about a linked identity was carried. Details carry `credential`, `event` and `outcome` (`sessionsEnded`, `credentialUnlinked`, `accountSuspended`, `addressUnverified` or `recorded`); the account is both subjects. (IDN-LIFE-012a, entries 285 to 289) |
| `auth.oidc.clientregistered` | security | `AuditActions.ClientRegistered` | A client was registered in the provider's registry, or a registered one changed, from the server. The principal is the `register-client` command with the reason `AUTH-OIDC-001`; `details.client`, `details.kind` and `details.changed` (whether the registry held it before). No subject; the row names no organization. (AUTH-OIDC-001, OPS-SEC-002, entry 340) |
| `auth.oidc.refreshreused` | security | `AuditActions.RefreshTokenReused` | A refresh token was presented a second time, which revokes the family it belongs to. (AUTH-TOK-004) |
| `auth.phonesignal.considered` | security | `AuditActions.PhoneSignalConsidered` | A phone signal was consulted before a send, recorded without the number it was consulted for. (AUTH-ABUSE-006) |
| `auth.recovery.approved` | security | `AuditActions.RecoveryApproved` | An assisted recovery was approved, naming the approver and the reason given. (AUTH-REC-006) |
| `auth.restriction.edited` | security | `AuditActions.RestrictionEdited` | A sending restriction was edited. (AUTH-ABUSE-005) |
| `auth.restriction.granted` | security | `AuditActions.RestrictionGranted` | A sending restriction was granted against an address or a number. (AUTH-ABUSE-005) |
| `auth.session.presented` | security | `AuditActions.SessionPresented` | A session was presented, which is what a sign-in history is read from. (AUTH-SESS-010) |
| `authz.access.denied` | security | `AuditActions.AccessDenied` | A permission was refused, which is the row the refusal's correlation identifier resolves to. (AUTHZ-CONCEAL-004) |
| `authz.access.exported` | security | `AuditActions.AccessExported` | An export operation was admitted at the gate. The acting and effective subject is who exported, or the nil subject with the system principal's name and reason; details carry `permission`, `resourceType` and, where a check named one record, `resource`. Not written while `exfiltration.export.auditing` is off. (OPS-ALERT-006, entry 329) |
| `authz.group.created` | security | `AuditActions.GroupCreated` | A group was created in an organization. Details carry `group`, `name` and `reason`; the row is filed under the group's organization. (AUTHZ-GROUP-001, entry 191) |
| `authz.group.memberadded` | security | `AuditActions.GroupMemberAdded` | An account or a group was added to a group. Details carry `group`, `name`, `memberType`, `memberId` and `reason`. (AUTHZ-GROUP-001, OPS-CFG-007, entry 191) |
| `authz.group.memberremoved` | security | `AuditActions.GroupMemberRemoved` | An account or a group was taken out of a group. Details carry `group`, `name`, `memberType`, `memberId` and `reason`. (AUTHZ-GROUP-001, OPS-CFG-007, entry 191) |
| `authz.group.removed` | security | `AuditActions.GroupRemoved` | A group nothing named was removed. Details carry `group`, `name` and `reason`. (AUTHZ-GROUP-001, entries 191 and 192) |
| `authz.role.defined` | security | `AuditActions.RoleDefined` | A role was created, or the permissions it bundles were changed. Details carry `role`, `before`, `after` and `reason`; the row names no organization. (AUTHZ-GRANT-004, OPS-CFG-007, entry 188) |
| `authz.role.removed` | security | `AuditActions.RoleRemoved` | A role nothing named was removed. Details carry `role`, `before`, `after` (null) and `reason`. (AUTHZ-GRANT-004, entries 188 and 189) |
| `identity.account.deactivated` | routine | `AuditActions.AccountDeactivated` | An account was deactivated by its own owner. (IDN-LIFE-013) |
| `identity.account.reactivated` | routine, or security where an administrator acted | `AuditActions.AccountReactivated` | A suspended account was stood back up: by its owner from a deactivation (routine), or by an administrator from an administrator's suspension (security, the acting subject the administrator and the effective subject the account). (IDN-LIFE-013, entry 254) |
| `identity.account.suspended` | security | `AuditActions.AccountSuspended` | An administrator suspended an account, or took over the suspension of one its owner deactivated. The acting subject is the administrator, the effective subject the account; the row names no organization. (IDN-LIFE-013, AUTH-SESS-010, entries 254 and 255) |
| `identity.credential.labelled` | routine | `AuditActions.CredentialLabelled` | A credential was given or renamed a label by its holder. (REG-PM-002) |
| `identity.deletion.cancelled` | routine, or security where an administrator acted | `AuditActions.DeletionCancelled` | A deletion was cancelled inside its grace window: by the subject from the link (routine), or by an administrator on the subject's behalf (security, the acting subject the administrator; details carry `request` where an out-of-band erasure request began the window). (IDN-LIFE-014, IDN-LIFE-003, entry 260) |
| `identity.deletion.requested` | routine | `AuditActions.DeletionRequested` | A deletion was requested, which opens the grace window it can be brought back from. (IDN-LIFE-014) |
| `identity.invitation.issued` | security | `AuditActions.InvitationIssued` | An invitation into an organization was issued. Details carry `invitation` and nothing it binds; the row is filed under the organization. (IDN-LIFE-009a, REG-INV-001, entry 234) |
| `identity.invitation.revoked` | security | `AuditActions.InvitationRevoked` | An invitation nobody had acknowledged was revoked, or replaced by a later one for the same corporate address. Details carry `invitation`. (IDN-LIFE-009a, REG-MAIL-001, entries 231 and 233) |
| `identity.invitation.acknowledged` | security | `AuditActions.InvitationAcknowledged` | An invitation was acknowledged and the membership it offered attached. Details carry `invitation`; the actor is the person acknowledging and the row is filed under the organization. (REG-INV-001, IDN-LIFE-009a, entry 249) |
| `identity.membership.ended` | security | `AuditActions.MembershipEnded` | An administrator ended a membership; the account and the organization persist. Details carry `membership`; the acting subject is the administrator, the effective subject the member, and the row is filed under the organization. (IDN-MEM-001, REG-MAIL-003, entry 250) |
| `identity.organization.created` | security | `AuditActions.OrganizationCreated` | An organization was created, with its policy key holding no override. Details carry `reason`; the row is filed under the organization. (IDN-ORG-002, entry 197) |
| `identity.organization.deletioncancelled` | security | `AuditActions.OrganizationDeletionCancelled` | An organization's deletion request was cancelled inside its window, which gives back every grant it holds. Details carry `reason`. (IDN-ORG-003, entry 197) |
| `identity.organization.deletionrequested` | security | `AuditActions.OrganizationDeletionRequested` | An organization's deletion was requested: it is suspended and every member session ended. Details carry `reason`. (IDN-ORG-003, entry 197) |
| `identity.organization.domainadded` | security | `AuditActions.OrganizationDomainAdded` | A domain was listed in an organization's lock, unverified. Details carry `domain` and `reason`; the row is filed under the organization. (REG-DOM-001, IDN-ORG-006, entry 214) |
| `identity.organization.domainremoved` | security | `AuditActions.OrganizationDomainRemoved` | A domain was removed from an organization's lock; its addresses stay refused until it is listed anew. Details carry `domain` and `reason`. (REG-DOM-001, entries 207 and 214) |
| `identity.organization.domainverified` | security | `AuditActions.OrganizationDomainVerified` | A listed domain was verified by its TXT record and now admits its addresses. Details carry `domain` and `reason`. (REG-DOM-001, entry 214) |
| `identity.organization.erased` | routine | `AuditActions.OrganizationErased` | An organization's deletion grace window elapsed and the erasure executed. Details carry `organization`, `deletingSince` and `membershipsEnded`; the row names no subject and no actor. (IDN-ORG-003) |
| `identity.preferences.changed` | routine | `AuditActions.PreferencesChanged` | The account's preference values were changed, recorded by key and never by value. (REG-PREF-001) |
| `identity.profile.changed` | routine | `AuditActions.ProfileChanged` | A profile attribute of the account was changed. (IDN-ATTR-001) |
| `identity.secondstep.preferred` | routine | `AuditActions.SecondStepPreferred` | The account's preferred second step was changed. (AUTH-FACT-007) |
| `identity.takedown.executed` | security | `AuditActions.TakedownExecuted` | Phase one of a takedown committed: the account entered its window, its sessions ended and the hosts' delivery was written. Details carry `takedown`, `trigger` (spelled as `10` section 5.12d), `reason` and `erasureDue`. (IDN-LIFE-003) |
| `identity.takedown.reversed` | security | `AuditActions.TakedownReversed` | A takedown was reversed inside its window and the account restored to active. Details carry `reason`. (IDN-LIFE-003) |
| `identity.username.changed` | routine | `AuditActions.UsernameChanged` | The account's username was changed, which holds the old one for as long as the retention says. (REG-IDENT-009) |
| `ops.auditpartitions.maintained` | security | `AuditActions.AuditPartitionsMaintained` | A run of the audit retention job completed under the maintenance credential. Details carry `created` (months made ahead), `dropped` (partitions past retention), `securityRetentionDays` and `routineRetentionDays`; the principal is the job's, `audit-partitions`, reason `PRIV-RET-002`; the row names no subject and no organization. (PRIV-RET-002, INF-BG-002, entry 336) |
| `ops.configuration.changed` | security | `AuditActions.ConfigurationChanged` | A runtime setting is put in force through the one configuration operation, or a value is set by bootstrap or by `configure` from the server under that command's principal. Details carry `key`, `before`, `after`, `loosening` and, where the change is a loosening or is made from the server, `reason`. (OPS-CFG-002, OPS-CFG-004, OPS-CFG-005, entries 315 and 319) |
| `ops.keyrotation.completed` | security | `AuditActions.KeyRotationCompleted` | A rotation of the key-encryption key or the fingerprint key reached every value under its version. Details carry `kind`, `version` and `processed`; the principal is the command's, `rotate-kek` or `rotate-fingerprint-key`, reason `OPS-SEC-003`; the row names no subject and no organization. (OPS-SEC-003 AC5, entries 316 and 318) |
| `ops.keyrotation.resumed` | security | `AuditActions.KeyRotationResumed` | A rotation that had stopped was taken up again from its recorded progress. Details carry `kind`, `version` and `processed`, under the command's principal. (OPS-SEC-003 AC2, AC5, entries 316 and 318) |
| `ops.keyrotation.retired` | security | `AuditActions.KeyRotationRetired` | The versions before a completed rotation's were retired once its escrow copy was confirmed sealed. Details carry `kind`, `version`, `processed` and `retired`, the versions retired, under the command's principal. (OPS-SEC-003 AC3, AC4, AC5, entries 316 and 318) |
| `ops.keyrotation.started` | security | `AuditActions.KeyRotationStarted` | A rotation of the key-encryption key or the fingerprint key started. Details carry `kind`, `version` and `processed`, under the command's principal. (OPS-SEC-003 AC5, entries 316 and 318) |
| `ops.restoretest.completed` | security | `AuditActions.RestoreTestCompleted` | A run of the automated restore test ended, passed or not. Details carry `outcome` (`passed`, `unrestored`, `undecrypted`, `unresolved` or `overrun`), `elapsedSeconds`, `objectiveSeconds` and `outlived`; the principal is the job's, `restore-test`, reason `DR-007`; the row names no subject and no organization. (DR-007 AC2, DR-008 AC2, entry 334) |
| `privacy.consent.granted` | security | `AuditActions.ConsentGranted` | A consent was granted for a purpose, naming the document version it was given against. (PRIV-CONS-004) |
| `privacy.consent.withdrawn` | security | `AuditActions.ConsentWithdrawn` | A consent was withdrawn for a purpose. (PRIV-CONS-008) |
| `privacy.document.published` | security | `AuditActions.DocumentPublished` | A version of a legal document was published in the governing language. (PRIV-CONS-005) |
| `privacy.document.translated` | security | `AuditActions.DocumentTranslated` | A translation was filed against a published version of a legal document. (PRIV-CONS-005) |
| `privacy.erasure.completed` | security | `AuditActions.ErasureCompleted` | An erasure whose retries were spent was completed by hand, with its erasures row. Details carry `erasure` and `outstanding`, the required subscribers that had not confirmed, by name; the acting subject is the operator, the effective subject the erased one. (IDN-LIFE-003a, entry 264) |
| `privacy.erasure.executed` | security | `AuditActions.ErasureExecuted` | An erasure was carried out, which destroys the subject key and leaves the trail resolving. A replay of the off-host ledger records each erasure it carries out again under the principal `replay-erasures`, reason `DR-016`, with details `reason` and `erasedAt`. (PRIV-RIGHT-005, DR-016, entry 333) |
| `privacy.export.assembled` | security | `AuditActions.ExportAssembled` | A subject export was assembled and made available to the subject. (PRIV-RIGHT-003) |
| `privacy.objection.recorded` | security | `AuditActions.ObjectionRecorded` | An objection to a purpose was recorded. (PRIV-BASIS-003) |
| `privacy.objection.withdrawn` | security | `AuditActions.ObjectionWithdrawn` | An objection to a purpose was withdrawn and the purpose resumed. (PRIV-BASIS-003) |
| `privacy.request.entered` | security | `AuditActions.RequestEntered` | A data subject request entered the queue staff work. (PRIV-RIGHT-002) |
| `privacy.request.fulfilled` | security | `AuditActions.RequestFulfilled` | A data subject request was fulfilled. (PRIV-RIGHT-002) |
| `privacy.request.lapsed` | security | `AuditActions.RequestLapsed` | A data subject request reached its deadline undecided. (PRIV-RIGHT-002) |
| `privacy.request.refused` | security | `AuditActions.RequestRefused` | A data subject request was refused, with the reason recorded against it. (PRIV-RIGHT-002) |
| `privacy.request.submitted` | security | `AuditActions.RequestSubmitted` | A data subject request was submitted by the subject. (PRIV-RIGHT-002) |
| `privacy.restriction.lifted` | security | `AuditActions.RestrictionLifted` | An administrator lifted a restriction of processing and the subscribers were told. The acting subject is the administrator, the effective subject the account; the row names no organization. (PRIV-RIGHT-004, entry 258) |

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
| `invitation-link` | `MessageKind.InvitationLink` | The link an invitation into an organization carries, sent to the email the invitation binds and to nothing else; its place `token` carries the link (IDN-LIFE-009a, REG-MAIL-001, entries 226 and 227). |
| `no-account` | `MessageKind.NoAccount` | The answer to a request made for an address no account holds. |
| `privacy-request-lapsed` | `MessageKind.PrivacyRequestLapsed` | The honest word to a subject whose out-of-band erasure request reached its deadline undecided (PRIV-RIGHT-002). |
| `privacy-request-received` | `MessageKind.PrivacyRequestReceived` | The automatic receipt a data subject request gets the moment it enters the queue, which is not a decision and starts nothing (PRIV-RIGHT-002). |
| `recovery-codes-reminder` | `MessageKind.RecoveryCodesReminder` | The one reminder a set of recovery codes gets once it is older than `recovery.codes.reminder`, sent to the security-notice set of an active account and carrying no link (AUTH-FACT-008 AC5, entry 335). |
| `recovery-link` | `MessageKind.RecoveryLink` | The link a person asked for to set a new password, which restores nothing else and removes no factor. |
| `secondstep-code` | `MessageKind.SecondStepCode` | A code presented as a second step. |
| `security-notice` | `MessageKind.SecurityNotice` | A notice that something happened to the account. |
| `signin-link` | `MessageKind.SignInLink` | A link that signs the person in. |
| `verification-code` | `MessageKind.VerificationCode` | A code that proves control of an address or a number. |
