# 00 — Overview

Specification for a reusable .NET library providing **identity**, **authentication**,
and **authorization**, to be imported by the company's projects rather than rebuilt
per project.

Intended experience for a new project: **import → wire and configure → use.**

**Status:** specification. **Complete on its own** — everything needed to build is
in documents 00–20. `decision-log.md` records why each choice was made and what was
rejected; it ships alongside as background and is never required to build (§9).
Requirements cite their source decision so the reasoning is one lookup away for
anyone who wants it.

---

## 1. Reading this specification

### 1.1 Document set

| Document | Covers |
|---|---|
| **00-overview** | This document — system shape, glossary, scope boundaries |
| **01-identity** | Accounts, organizations, memberships, lifecycle |
| **02-authentication** | Factors, flows, sessions, recovery, abuse controls |
| **03-authorization** | Grants, roles, the model builder, permission filtering |
| **04-privacy** | Consent, data subject rights, RoPA, retention, sensitivity |
| **05-integrations** | Stalwart, the host's message and alert handlers, generic outbound and callback rules: contracts and field mappings |
| **06-operations** | Migrations, CI, secrets, bootstrap, configuration |
| **07-library-contract** | Public API surface, packaging, versioning |
| **08-engineering-conventions** | Naming, layout, error handling, testing, review |
| **09-api-contract** | Endpoints, request and response shapes, status codes |
| **10-reference** | Error codes, permissions, configuration keys, enumerations |
| **11-runbook** | The library's operational procedures: break-glass, admin-assisted recovery, outside dependencies, incidents, configuration changes |
| **12-disaster-recovery** | Erasure across backups: key destruction, the off-host erasure ledger, the key-encryption key's cryptoperiod |
| **13-risk-register** | Risks identified, accepted, and mitigated |
| **14-takedown-procedure** | *Retired by D-225: the procedure is the host's; the takedown is `01` IDN-LIFE-003* |
| **15-threat-model** | Actors, assets, attack surfaces, and coverage gaps |
| **16-offboarding-procedure** | *Retired by D-225: the procedure is the host's; a membership's end is `01` IDN-MEM-001* |
| **17-bff** | Browser-facing security boundary — sessions, CSRF, capabilities |
| **18-frontend-integration** | How a frontend uses the library: wiring and constraints, not UI |
| **19-infrastructure** | Requirements the environment must satisfy — not how |
| **20-registration-and-account** | What an account holds, the registration session and steps, identifier management, invitations, domain lock, corporate mailbox |
| **21-people-without-an-account** | The person record: collection in a host's own screens, the person's access by a code, attaching to an account, the age question and the takedown for records, what staff do with them |
| `decision-log.md` | Rationale — why each decision was made, what was rejected. **Supplementary; not required to build** |

### 1.2 Requirement format

Every requirement carries an identifier, a normative statement and its source decision.
**Acceptance criteria accompany every requirement that can be tested** — which is most
of them. A few state a fact, give advisory guidance, or set a review cadence, and carry
none; where consecutive requirements share a single testable outcome, one criteria block
covers them.

> **AUTH-SESS-005** — Session lifetimes SHALL be derived from the assurance level the
> principal's policy requires, never from user type or application identity.
>
> *Source: D-033.1, D-020.2, D-123*

Identifiers are stable. If a requirement is withdrawn its identifier is retired,
never reused.

### 1.3 Normative language

**SHALL** / **SHALL NOT** — mandatory. A build that does otherwise is
non-conformant.
**SHOULD** / **SHOULD NOT** — strong default. Departure requires a recorded reason.
**MAY** — genuinely optional.

Anything not marked is descriptive context, not a requirement.

### 1.4 Acceptance criteria

Each requirement lists conditions that are objectively checkable. They are written
to be turned into tests. A requirement whose criteria cannot be checked without
human judgment is under-specified and should be raised rather than guessed at.

---

## 2. What this is

**A library, not a service.**

- Imported by the host project and running **in-process**. No separate
  authentication server to deploy, monitor, or keep available
- Ships its **own database schemas and migrations**. It never reads or writes the
  host application's tables
- Ships its own **endpoints and middleware**, mounted by the host where it chooses
- Every business project gets its **own deployment and database**. Projects never
  share identity data

*Source: D-005, D-006, D-015, D-018*

### 2.1 Tech stack

Latest production-safe versions — verify current releases rather than assuming.

| Layer | Choice |
|---|---|
| Backend | .NET |
| Database | PostgreSQL |
| Cache | None in version 1: every record is read from PostgreSQL (D-197) |
| Data access | EF Core for everything normal; Dapper for hand-written SQL |
| Mail server | Stalwart, for staff mailboxes (pluggable, not a library dependency) |
| Messages | Sent by the host; the library hands each one over (`05` INT-SMS-006, D-225) |

*Source: D-017, D-197*

### 2.2 Reference topology

The shape the library is built to support. It is a topology the library **works
with**, not one it assumes — a smaller project may use the same library as a single
application with no separate authentication or account app.

- **Public application** (app + BFF): customer-facing, the host's own product
- **Management app** (app + BFF) — internal, administrative organization only
- **Authentication app** (app + gateway) — authentication endpoint, holds the auth
  session that makes cross-app SSO possible
- **Account app** (app + BFF) — account management and the privacy dashboard
- **Single database**, multiple schemas
- **Background worker**
- **Stalwart** mail

*Source: D-033.3, D-197*

---

## 3. The core model in brief

Full detail lives in documents 01 to 03. This is orientation.

### 3.1 Organizations

There is **one identity pool, one deployment, one database**. Organizations are
entities within it, not isolation boundaries. The business is organization #1 — the
**administrative organization** — and staff are simply its members.

**Staff is a membership, not a user type.** Authentication policy, including
"passkeys only," attaches to the organization. A second organization with different
rules is a configuration row, not a code branch.

*Source: D-001, D-002, D-003*

### 3.2 Permissions

Every permission is the same sentence:

> **[somebody]** has **[a role]** on **[something]**

**Somebody** is a user or a group. **A role** is a named bundle of actions, stored
as data. **Something** is one item, a container, or the whole organization.

Groups nest. Containers pass access downward. Deny entries exist and always win.
Grants and roles are data, so granting, revoking and adding roles need no deploy.

A grant is either **stored** (a row someone wrote) or **derived** (computed from a
relationship in the host's own data, such as "the assigned representative on an
account may read that account's records"). A derivation evaluated at each check needs
no maintenance and cannot drift; one materialised for cost is stored as grants and
checked for drift daily (AUTHZ-DERIVE-005). Both obey identical rules.

*Source: D-015, D-016, D-043, D-166*

### 3.3 Sessions

The session record is the spine; every credential derives from it, so revoking the
session kills everything derived from it at once.

| Session type | Held by | Purpose |
|---|---|---|
| Per-app session | Each app's BFF, opaque cookie | The user's session with that app |
| Auth session | The authentication app | Makes silent SSO between apps possible |
| OIDC tokens | Protocol clients (Stalwart) | Mail and future native clients |

The browser holds an opaque identifier and nothing else. No tokens in browser
storage, ever.

*Source: D-007, D-033*

### 3.4 Sensitivity

A host's ordinary business records can be **special-category data**: a record of what
a person obtained can disclose a diagnosis, a belief or a financial position through
the record itself. Which of its resource types are sensitive is the host's declaration.

Sensitivity is a **declared property of a resource type**, driving written-consent
capture, stricter retention, mandatory encryption at rest, and separate treatment
in compliance records.

*Source: D-030*

---

## 4. Design principles

These are review criteria, not aspirations. Each carries a test.

| Principle | Violated if |
|---|---|
| **Secure by default** | A consumer can produce an insecure deployment by not configuring something |
| **Configurable by default** | A constant exists that a consumer might reasonably want different |
| **Runtime where possible** | Something is protected from runtime change because it was easier, not because it must be |
| **History is never physically removed** | A record of something that happened is deleted rather than made unreadable. Transient artefacts — expired sessions, consumed tokens, delivered events — are swept |
| **Fail closed and loud** | A dependency being unavailable makes something permitted, or a check silently skips |
| **Separation of concerns** | Authorization cannot be used without authentication |
| **Decoupled by default** | A vendor name appears in a core namespace |
| **No host-domain knowledge** | The library contains a type name belonging to any business |
| **Extensible without modification** | Adding a factor, resource type, role or provider requires editing library source |
| **One rule, one place** | A single permission check and a list filter can disagree |
| **Observable by default** | Why access was denied cannot be determined without a debugger |
| **Zero maintenance** | Anything requires a recurring calendar entry |
| **Standards over invention** | A hand-rolled version exists of something with an RFC |
| **Reversible behind seams** | A decision would be expensive to undo in a year |

*Source: P-003*

**One accepted maintenance exception**, named rather than hidden: the annual rotation of
the key-encryption key (`12` DR-009a, D-225).

**Two resolved tensions:** configurability against security, resolved by direction:
every runtime change carries a reason and an audit entry, a loosening also requires
step-up, and the most dangerous settings are unreachable from the application entirely
(D-010, D-071, D-166). Zero-maintenance against key rotation, resolved by the exception
above.

---

## 5. Language

*Source: D-031*

| Axis | Rule |
|---|---|
| **Presentation** | English is primary everywhere, including compliance screens |
| **Quality** | Arabic is first-class everywhere — written natively, RTL handled properly, never a lagging translation |
| **Legal authority** | Each legal document version has one governing language, defaulted from `legal.governinglanguage` (Arabic on the default deployment); where a translation diverges, the governing text governs (PRIV-CONS-005) |

**Compliance text is in scope now**; general localization is deferred to its own
library (D-021). Compliance text means consent requests, the privacy notice,
withdrawal flows, and the privacy dashboard — a bounded, legally-specified set.

A document version is the governing-language text; translations attach to it and are
optional (PRIV-CONS-006). Wherever a document is shown, the reader can view the
governing text and any translation without changing the interface language.

---

## 6. Regulatory context

The privacy features (`04`) take Egypt's Law 151/2020 and its Executive Regulations as
their default values: the lawful bases, the rights and the deadlines for deciding a
request, each a key or a declaration a host changes for another law (D-166). A deploying
company's own obligations are its own documents' (D-225).

*Source: D-023, D-029, D-036, D-037, D-147, D-225*

**This specification is not legal advice.**

---

## 7. Scope boundaries

Everything below is deliberately excluded and labelled, per the completeness
requirement.

### 7.1 Out of scope

| Area | Decision | Seam retained |
|---|---|---|
| Localization library and general UI translation | D-021 | Compliance text is in scope; the rest is deferred |
| Impersonation | D-014 | Acting identity and effective identity carried separately; audit records both |
| Third-party application platform, consent screens, dynamic client registration | D-005 | Stable opaque subject identifier; claims as data; session and credential separate; a place for consent |
| Account merging | D-023 context | None — accounts are never merged |
| Video surveillance | — | Separate PDPC licence if ever used |

### 7.2 Cross-boundary — owned elsewhere

| Area | Owner | Contract with this system |
|---|---|---|
| Mailboxes | Stalwart | OIDC authentication; JMAP provisioning; its own database. Never its internals |
| Delivering messages (email, SMS, push) | The host | The library hands each message it owes to the host's notification handler and each alert to its alert handler (`05` INT-SMS-006, `06` OPS-ALERT-003, D-225) |
| Deploying, hosting, backups and watching the server | The host | The environment the library needs is `19`; a backup's erasure is `12` (D-225) |
| Payment, shipping and any other business processor | The host | Declared by the host in its processor register and integrated through the generic outbound and callback rules (`05` INT-GEN); the library names none |
| DPIA / LIA / TIA production | Company + counsel | System stores references and flags absence |
| Electronic direct marketing sending | Separate system | Requires its own licence; consent is captured here |

### 7.3 Deferred with triggers

Nothing is deferred silently.

| Deferred | Reactivation trigger |
|---|---|
| Localization design | Before any general user-facing text is written |
| Impersonation feature | When support staff exist who need customer views |
| Third-party app platform | First external client that is not first-party |
| Multiple organization memberships enabled by default | First person needing access across organizations |
| Two-approver recovery | First additional person with recovery approval rights |
| Self-hosted password breach corpus | If counsel objects to the range API |
| Read/write separation for reporting | When reporting queries outgrow tuning |
| Migration away from in-library authorization | When permission queries are a top-3 slow query and the optimisation ladder is exhausted, **or** reverse lookup over derived grants exceeds its budget (`authz.reverselookup.budget`, 2 s) at production volume (AUTHZ-SEAM-001, R-A09) |
| Multi-market legal document sets (several governing languages on one deployment, D-146) | Serving a second market whose law names another language |
| Hash-chained tamper-evident audit records (D-071); the recorded answer to the separation-of-duties observation in `15` section 3.6 | When a second person shares administrative duties, or audit integrity is challenged |
| Automated erasure routine for the management schemas (D-101); internal-side erasure is manual until then | The user's decision, not an automatic trigger |
| External key service with per-subject keys (D-082, superseded by D-097) | If protecting pre-erasure backups against a compromised secrets manager ever justifies a second stateful store |
| Profile photos enabled for customers (D-060) | When a feature makes a customer photo meaningful |
| Object storage for uploads (D-060) | Customer photos, or any genuinely large artifact |
| A cache in front of PostgreSQL for sessions and permissions (INF-CACHE-001, D-197) | When session or permission reads are among the slowest queries the deployment measures |

This table carries every row of the decision log's "Deferred, with reactivation
triggers" table (D-147); a deferral recorded in the log and absent here is a defect.

---

## 8. Glossary

| Term | Meaning here |
|---|---|
| **AAL** | Authentication Assurance Level, per NIST SP 800-63B-4. AAL1 single factor; AAL2 two factors **or one multi-factor authenticator** — a passkey alone reaches AAL2; AAL3 hardware-bound, which no combination the library offers reaches. Assignments in `02-authentication` AUTH-SESS-005a |
| **Administrative organization** | Organization #1. Its members are what would elsewhere be called staff |
| **Ancestry closure** | Precomputed table of every container above each resource. Makes inheritance a join rather than recursion |
| **Assurance provider** | Component supplying the assurance level of a session. Required if authorization is used without authentication |
| **Auth session** | Session held by the authentication app that makes silent SSO between apps possible |
| **BFF** | Backend for Frontend. Holds the session; the browser holds only an opaque cookie |
| **Break-glass** | Single-use sealed credential granting a time-boxed system-admin session. Where it is kept is the host's (D-225) |
| **Controller / Processor** | PDPL roles. The deploying company is controller. The library ships three register rows (`05` section 6): the mail server (where one is integrated) and the hosting provider as processors, and password screening as a recipient; the providers that deliver the host's messages are rows the host declares (D-225). Every other processor is one the host declares, a developer who administers the deployment for the company among them (D-029, D-165, D-166) |
| **Data user** | PDPL term covering both controllers and processors |
| **Governing language** | The one language in which a version of a legal document is authoritative, defaulted from `legal.governinglanguage`; translations attach to the version and never govern (`04-privacy`, D-146) |
| **Grant** | One row expressing "[somebody] has [a role] on [something]" |
| **Model builder** | Fluent startup declaration where the host describes its resource types, containment, concealment, sensitivity and processing purposes |
| **Organization** | A domain entity, not a tenancy boundary |
| **Person record** | A data subject a host keeps data about in their own right who holds no account: its own subject identifier, key and consents, no sign-in; reached by the person by a code, or through an account once attached (`21-people-without-an-account` REC-REC-001, D-221) |
| **Registration session** | Opaque server-side record bound to the requesting browser that stages everything registration collects and reserves nothing; the account is created in one transaction at the terms step or not at all (`20-registration-and-account` REG-SESS-001) |
| **Restriction** | A named sending limit: a key (destination, account, source, global or host-supplied), an optional purpose, an optional channel (`sms`, `email` or `any`), one or more (max, interval) buckets, sliding or fixed, and an optional `resend` mark, under which a send is judged and counted once whatever its languages (AUTH-ABUSE-004, D-197). Every send of every kind except an alert is checked against every restriction that applies to it; a security notice to an existing holder answers only to restrictions whose purpose is `notification`, and an alert only to its deduplication (`02-authentication` AUTH-ABUSE-004, D-166) |
| **RoPA** | Record of Processing Activities. Generated, not maintained by hand |
| **Security-notice set** | The identifiers that receive security notices and recovery links: the primary of each kind plus whatever the account's backup setting adds (`20-registration-and-account` REG-IDENT-002) |
| **Sign-in link** | A link sent by email or SMS that signs the person in at AAL1 when pressed in the browser that asked for it; never a second step. Off by default (`loginFactors` `emailLink`, `phoneLink`) |
| **Step-up** | Requiring fresh strong authentication before a sensitive action |
| **System-admin** | Permission, never a rank. Not implied by organizational seniority |

---

## 9. Conformance

An implementation is conformant when every **SHALL** requirement is met and every
acceptance criterion passes.

**The specification is complete on its own.** Documents 00–20 contain everything
needed to build. `decision-log.md` ships alongside and records rationale and
rejected options; a builder who must consult it to understand a requirement has
found a **defect in the specification** — report it, and the specification is
corrected.

**SHOULD** departures are conformant only where the reason is recorded in the
decision log — not in a code comment.

Where this specification and the decision log disagree, **the specification is
authoritative for what is built** and the disagreement is a defect to raise; it is
resolved by correcting whichever document is wrong, never by building from the log.

*Source: D-119*
