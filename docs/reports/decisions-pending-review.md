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
