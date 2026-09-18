# 07 — Library Contract

The public surface, what is guaranteed stable, packaging, and versioning.

**Prerequisite:** `00-overview.md`, sections 1 and 2.

**Scope.** This document defines the boundary between the library and a host
project — what a consumer may depend on, what may change beneath them, and what they
must supply.

---

## 1. Packaging

**LIB-PKG-001** — The library SHALL ship as **one package** with strict internal
boundaries between Identity, Authentication, Authorization, Privacy, Storage, and
Hosting. Those areas SHALL NOT reach into one another. The conformance suite
(LIB-TEST-001) and the analysers (`08` CONV-CODE-008) ship as two further packages,
because a host runs the first and the build consumes the second; neither carries
library behaviour.

*Source: D-005, D-149*

Splitting into separate packages up front means maintaining a version matrix between
packages that only ever ship together. Split when a real second consumer needs one
part without the others, not before.

**Acceptance criteria**
1. Each internal area compiles without reference to the others' internals.
2. A test asserts no cross-area dependency outside declared contracts.

---

**LIB-PKG-002** — The core SHALL be separable from the data-access provider. Two
rendering targets exist — expression and SQL fragment — and neither SHALL be a
bolt-on to the other.

*Source: D-017*

**Acceptance criteria**
1. Core contracts compile without a database dependency.
2. Both renderings derive from one rule definition.

---

**LIB-PKG-003** — Distribution SHALL be via a private package feed on the company's
own infrastructure.

*Source: D-005*

**Acceptance criteria**
1. The package is not published to a public registry.

---

## 2. The public contract

**LIB-API-001** — The following constitute the public contract. Changes to any are
breaking.

| Surface | Includes |
|---|---|
| **Model builder API** | Resource type declaration, containment, concealment, sensitivity, purposes, roles, **derivations** |
| **Permission filter shape** | The expression form and the SQL fragment form |
| **Operations contract** | The service contracts for every library-owned operation (LIB-API-005) |
| **Emitted events** | Notification and lifecycle event contracts, including the identifier, restriction and device events of D-146 (`IdentifierAdded`, `IdentifierRemoved`, `IdentifierPrimaryChanged`, `SendingRestrictionChanged`, `SendingRestrictionGranted`, `DeviceVerified`; `10` section 5b) |
| **Database schema** | All library-owned tables |
| **Ancestry closure table** | Structure and semantics |
| **Error codes** | Machine-readable codes and their meanings |
| **BFF middleware pipeline** | Mounting API and stage ordering (D-052) |
| **HTTP endpoints** | Paths, shapes and codes in `09-api-contract` |
| **Configuration keys** | Names, types, and value constraints |

*Source: D-026.4, D-017, D-041, D-106, D-146*

The ancestry closure is public because hand-written SQL will query it. It cannot be
restructured without a major version.

**Acceptance criteria**
1. Each surface is documented with its stability guarantee.
2. A change to any is caught by a contract test before release.

---

**LIB-API-002** — Everything not listed in LIB-API-001 SHALL be internal and MAY
change in any release.

*Source: D-026.4*

**Acceptance criteria**
1. Internal types are not publicly accessible.
2. No consumer sample depends on an internal type.

---

**LIB-API-003** — Errors crossing the boundary SHALL be **machine-readable codes with
structured data**, never rendered prose. **There is no exception.**

*Source: D-021, D-054*

An earlier draft carved out endpoints the library served directly to a browser. That
carve-out is removed: **the library never returns user-facing text over HTTP.**
Anything a person reads in a browser is rendered by the frontend and localized there.

Message content sent by other means — email and SMS — is the worker's concern
(INT-SMS-005a), and is text a person reads. The rule is about the HTTP boundary, not
about all output.

Correct API design regardless of localization, and precisely what makes a future
localization library's job possible rather than impossible.

**Acceptance criteria**
1. No error returned across the boundary contains a user-facing sentence.
2. Each code is documented with its meaning and remediation.
3. The host renders errors in the user's language from codes.
4. No library endpoint returns HTML intended for a person to read, including
   framework default error pages.

---

**LIB-API-004** — The SQL fragment renderer SHALL be documented as
**PostgreSQL-specific**. No claim of dialect portability SHALL appear.

*Source: D-041, AUTHZ-GATE-003*

**Acceptance criteria**
1. The constraint is stated in API documentation.

---

**LIB-API-005** — Every library-owned operation SHALL exist **once**, as a service
contract in `Janus.Core`, and SHALL be exposed **twice**: callable in-process by a
host, and as an HTTP endpoint in `Janus.Hosting` mapped over the same contract.

*Source: D-106*

The management application hosts the library in-process, so its natural call is the
service; the frontends reach the same operation over HTTP. This is the pattern the
gate already uses — one rule rendered two ways (AUTHZ-GATE-002) — applied to
operations. An endpoint is a mapping, never a second implementation.

**The operations covered** — each required by a SHALL elsewhere: organization
lifecycle and policy; memberships and invitations; groups; account suspension,
reactivation, restriction lift and deletion cancel; the takedown; the data-subject
request queue; consent and objection records; erasure progress and manual completion; audit query by subject;
explanation resolution; compliance-text publishing; licence, permit and assessment
records; credential and factor management; profile photo and language preference; the
registration session; identifier, preference and session management; the restriction
set and grants; mail app passwords; domain lock (D-146).
The endpoints are enumerated in `09-api-contract`; the permissions in `10-reference`
§2.1.

**Every operation takes an access context** (AUTHZ-IMP-001) — a system principal
with a stated reason when called from background work (IDN-PRIN-001) — and is
authorized by the gate exactly as the endpoint would be. Calling in-process is not a
way round the permission.

**Acceptance criteria**
1. Each endpoint in `09` resolves to exactly one service in the contract; a test
   asserts no endpoint carries logic the service does not.
2. A host calling a service in-process is subject to the same permission check as the
   endpoint.
3. Adding an operation without adding its service to the contract fails a contract
   test.

---

## 3. What the consumer supplies

**LIB-HOST-001** — A host project SHALL declare the following and no more.

| Declaration | Contents |
|---|---|
| **Connection details** | Database and cache |
| **Enabled authentication factors** and their policy | Per organization |
| **Resource types and containment** | The only place the library learns the host's domain — including, for each encrypted field, the column identifying its subject (PRIV-RIGHT-005a) |
| **Roles** | Named bundles, and the actions that exist |
| **Processing purposes and lawful bases** | Per resource type (AUTHZ-MODEL-003). The jurisdiction's basis list with its properties, and its sensitive-data categories, are declared once; Egypt's ship as the default (PRIV-BASIS-001, D-108) |
| **Hosting location and cross-border basis** | Configuration (INT-HOST-001); the basis only where hosting is outside Egypt |
| **Relying party identifier and origins** | Configuration (AUTH-FACT-010) |
| **Alert destinations** | At least one email and one SMS destination for the operator (OPS-ALERT-004), **and the owner's email and SMS destination** — break-glass alerts reach them always (OPS-BOOT-002, D-129) |
| **SMS balance floor** | A prepaid amount the library cannot guess (INT-SMS-004) |
| **Subject-event handlers** | Erasure, restriction and export, per sensitive resource type. **Startup fails without them** (PRIV-RIGHT-005b) |
| **Governing language** | `legal.governinglanguage`: the default governing language of every legal document version (PRIV-CONS-005). Required, no default; protected once set (OPS-CFG-004) |
| **Preference declaration** | Each host-declared preference: name, type (`string` · `boolean` · `integer` · `enum` with its values), default, and whether the person or only an administrator may edit it (REG-PREF-001). The declaration MAY be empty; a malformed one fails startup |
| **Restriction key suppliers** | Optional: for each `host:<name>` restriction key the host uses (AUTH-ABUSE-004), a per-send callback registered under that name that returns the key value the restriction is evaluated against. A restriction naming a `host:<name>` key with no registered supplier fails startup |

Everything else has a safe default the host may override (P-001, D-107), including
the public-holiday list, whose safe default is empty and which staff maintain at
runtime as dates are announced (`privacy.holidays`, D-142). The
declarations above are exactly the values the library cannot know for a deployment;
every other key in `10-reference` §4 carries a default at the safe end of its range.
The three rows added by D-146 differ in kind: the governing language is a value with
no default, the preference declaration and the restriction key suppliers are
declarations that may be empty, so a minimal configuration still needs only the
values without a default.

*Corrected three times: the list originally said "exactly four things," which four
other requirements already contradicted; the subject-event handlers added by D-068
were still missing after that correction; and `10` then listed twenty keys with no
default, of which fourteen could be defaulted and six belonged here (D-107).*

*Source: D-148; D-005, D-015, D-068, D-107, D-146*

**Acceptance criteria**
1. A minimal working configuration requires only the declarations listed above; every
   other key has a default.
2. Omitting any produces a named startup error identifying which.
3. No key outside this list fails startup when unset.
4. Startup without `legal.governinglanguage` fails with a named error; startup with an
   empty preference declaration and no restriction key supplier succeeds.
5. A `host:<name>` supplier is invoked once per send that a restriction with that key
   applies to, and its return value is the key the buckets are counted under.

---

**LIB-HOST-002** — The host SHALL apply library-produced filters to its **own**
queries. The library SHALL NOT query host tables.

*Source: D-015, AUTHZ-PRIN-002*

This is what keeps permission filtering inside the host's query and list screens
fast.

**Acceptance criteria**
1. The library contains no query against a host-owned table.
2. Filters compose into a host query without materialising rows.

---

**LIB-HOST-003** — The host SHALL mount library endpoints where it chooses. The
library SHALL NOT assume a route prefix.

*Source: D-005*

**Acceptance criteria**
1. Mounting under a non-default prefix requires no code change.
2. No absolute path is hardcoded, including in the discovery document.

---

**LIB-HOST-004** — Where authorization is consumed without authentication, the host
SHALL supply an assurance provider if step-up is required. Absent one, step-up checks
SHALL fail closed.

*Source: D-041, AUTH-STEP-003*

**Acceptance criteria**
1. Authorization alone compiles and runs.
2. Without an assurance provider, a step-up permission is denied, and the denial is
   distinguishable in diagnostics.

---

## 4. Extension points

**LIB-EXT-001** — The following SHALL be replaceable without modifying library
source.

| Extension point | Default shipped |
|---|---|
| Mail delivery | A default handler |
| SMS delivery | A default handler |
| Notification handling | Email and SMS |
| Message and content templates | Defaults, localizable |
| Audit sink | Database |
| Cache | Redis |
| Authentication factors | The catalogue in `02-authentication` |
| Record-level rules atop the permission model | None |
| Secret source (key-encryption key, fingerprint key, maintenance credential; INF-HOST-003) | None: the host supplies it; startup fails without one (D-149) |

*Source: D-022, D-012, P-003*

**Acceptance criteria**
1. Each is registered through configuration, not inheritance from a library type.
2. Replacing one requires no library change.
3. No provider name appears in a core namespace.

---

**LIB-EXT-002** — Adding a factor, resource type, role, or provider SHALL be a
**registration**, never an edit to library source.

*Source: D-012, D-015, P-003*

**Acceptance criteria**
1. A new factor registered with `IsPhishingResistant = true` satisfies existing
   step-up rules unmodified.
2. A new resource type requires no library change.

---

## 5. Versioning

**LIB-VER-001** — The library SHALL use **Semantic Versioning 2.0.0** against the
contract in LIB-API-001. The version SHALL be derived from the release tag (`vMAJOR.MINOR.PATCH`)
by the build (CONV-VCS-005) and SHALL appear in no project file.

*Source: D-026.4, D-149*

**Acceptance criteria**
1. A breaking change to any listed surface increments the major version.
2. Additive changes increment the minor version.

---

**LIB-VER-002** — Every major version SHALL ship with a migration note describing
what changed and what a consumer must do.

*Source: D-026.4*

**Acceptance criteria**
1. The note names each breaking change and its remediation.
2. Schema changes name the required migration order.

---

**LIB-VER-003** — Consumers MAY skip minor versions. Consumers SHALL NOT skip major
versions.

*Source: D-026.4*

**Acceptance criteria**
1. Migrations assume the immediately preceding major version.

---

## 6. Conformance testing

**LIB-TEST-001** — The library SHALL ship a conformance suite a host can run against
its own configuration, as the `Janus.Conformance` package (`08` CONV-LAYOUT-001).

*Source: P-003, AUTHZ-TEST-001, D-149*

**Acceptance criteria**
1. The suite verifies every entity has a registered policy.
2. It runs the truth table through both check and filter and asserts agreement.
3. It validates the model declaration and reports each failure distinctly.

---

**LIB-TEST-002** — Contract tests SHALL guard every surface in LIB-API-001, failing
the build on an unintended change.

*Source: D-026.4*

**Acceptance criteria**
1. Altering a public signature without a version bump fails the build.
2. Altering the ancestry closure structure fails the build.

---

## 7. Stability and the migration seam

**LIB-SEAM-001** — All permission evaluation SHALL pass through a single interface so
the implementation behind it can be replaced without changing call sites.

*Source: D-015, AUTHZ-SEAM-001*

**Acceptance criteria**
1. Replacing the implementation requires changes in one place.

---

**Recorded migration trigger.** Move away from in-library authorization when
permission queries are a top-three slow query and the optimisation ladder is
exhausted, **or** when reverse lookup over derived grants exceeds
`authz.reverselookup.budget` (default 2 seconds) for the administrative view at
production volume (AUTHZ-SEAM-001, R-A09) — not when the design feels complex.

*Source: D-015, D-125*

---

## 8. Reserved seams

Present in the contract, unused by any current feature. Each is minimal — something
a reasonable engineer would build anyway for clarity, that happens also to unblock a
deferred capability.

| Seam | Reserves | Source |
|---|---|---|
| Stable opaque subject identifier | Third-party clients, mobile | D-005 |
| Claims modelled as data | Third-party clients | D-005 |
| Session and credential kept separate | Native and protocol clients | D-005 |
| A place for consent in the model | Third-party consent screens | D-005 |
| Acting and effective identity | Impersonation | D-014 |
| Factors registered by property | Additional factors | D-012 |
| Organization as container, membership as group | Further organizations | D-001 |
| Derived grants | Delegation, approval workflows, access reviews, context conditions | D-043 |
| Multiple memberships permitted structurally | Cross-organization access | D-003 |

**LIB-SEAM-002** — Reserved seams SHALL NOT acquire **features or user interface**
until the corresponding capability is in scope.

A configuration key **is** permitted where the capability is built and disabled rather
than absent — multiple organization memberships and the recovery approver count are
both built, both defaulted off or to one, and both switchable without a deploy. A key
for something not built at all remains prohibited.

*Corrected: the earlier wording forbade configuration outright and two existing keys
contradicted it.*

*Source: P-002*

**Acceptance criteria**
1. No configuration key exists for a capability that is not built; a built-but-
   disabled capability's key defaults to off (D-135).
2. No feature reads acting and effective identity as differing.

---

## 9. Open items

None. Cross-references: behaviour in documents 01 to 05; deployment and configuration
in `06-operations`; coding conventions in `08-engineering-conventions`.
