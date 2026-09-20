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
2. A new project. CONV-LAYOUT-001 states the list; adding to it is not the agent's.
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
