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


# Rows for chapter 10

D-162 section E names codes, keys, declarations and vocabularies the library now
carries and chapter 10 does not yet hold rows for. Each is listed with what the code
does, so the row can be written from it. Nothing here is a decision; the shapes are
D-162's.

## Section 1.2, error codes

| Code | Status | Raised when |
| --- | --- | --- |
| `auth.password.toolong` | 422 | A password longer than `password.maximum` is set, at registration, at a password change or at a reset. Nothing is truncated. |

## Section 4, configuration keys

| Key | Type | Scope | Default | Named when |
| --- | --- | --- | --- | --- |
| `password.blocklist.selfhosted.address` | string | R | none | Required where `password.blocklist.source` is `selfHosted`. Where the deployment's own corpus serves the ranges the primary source serves. |
