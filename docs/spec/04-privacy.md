# 04 — Privacy

Consent, lawful bases, sensitivity, data subject rights, records of processing,
retention, and breach response.

**Prerequisite:** `00-overview.md`, sections 1, 5 and 6.

**Scope.** This document covers *what may lawfully be held, on what basis, for how
long, and what the person may demand.* Who exists is `01-identity`. Enforcement of
access is `03-authorization`.

**Not legal advice.** Requirements derive from the regulator's published guidance,
supplied as source material. Confirm with counsel.

---

## 1. Principles

**PRIV-PRIN-001** — Collect only what the function requires, hold it only as long as
the function requires, and where a design works without identity, do not attach
identity.

*Source: D-030, P-003*

**Acceptance criteria**
1. Each declared purpose names the specific data categories it requires.
2. A field held by no declared purpose fails model validation.

---

**PRIV-PRIN-002** — Compliance evidence SHALL be **generated from configuration**,
never maintained by hand.

*Source: D-036*

A purpose not declared cannot be processed; a declared purpose appears in the record
automatically. The document cannot drift from reality because it is not a document —
it is a query.

**Acceptance criteria**
1. Adding a processing purpose changes the generated record with no separate edit.
2. No hand-maintained inventory of processing activities exists.

---

## 2. Lawful bases

**PRIV-BASIS-001** — Every processing purpose SHALL declare a lawful basis from a
**closed list the host declares at startup**, each basis carrying the properties the
library branches on. Egypt's six SHALL ship as the default declaration.

| # | Basis | `IsConsent` | Written for sensitive | `RequiresAssessment` | `IsObjectable` | Typical use |
|---|---|---|---|---|---|---|
| 1 | Data Subject's Consent | yes | yes | no | no | Marketing; health-implying purchase data |
| 2 | Fulfilment of a Contractual Obligation | no | — | no | no | Order processing, delivery, payment |
| 3 | Fulfilment of a Legal Obligation | no | — | no | no | Tax and financial record retention |
| 4 | Legitimate Interest | no | — | yes | yes | Fraud prevention, security, abuse controls |
| 5 | Claim or Defence of a Legal Right | no | — | no | no | Retaining evidence for a dispute |
| 6 | Execution of Court Judgments or Orders from Competent Investigative Authorities | no | — | no | no | Responding to a court order |

*Source: D-032, D-091, D-108*

**The code reads the properties, never the name** — the same rule factors follow
(AUTH-FACT-001). Whether a purpose is withdrawable, shows a dashboard control, runs the
capture path, needs a linked assessment, or can be objected to is answered by the
flags on its basis. A project in another jurisdiction declares a different list with
its own flags and the library changes not at all (D-091).

**Storage is a library table seeded from the declaration.** It carries the properties
the code reads, which is what makes it a genuine table rather than a constrained column
(CONV-ENUM-001). Purposes reference a basis by key; generated records emit its label.

This is Egypt's list, not the European one. There is **no vital-interests basis and
no public-task basis**; bases 5 and 6 are Egypt-specific. An implementer defaulting
to the European six would produce values the regulator does not recognise.

**Acceptance criteria**
1. The list is closed — a purpose cannot declare a basis outside the declared list,
   and startup fails if one does.
2. Generated records emit exactly the declared labels.
3. A purpose without a declared basis fails startup validation.
4. No conditional in the library tests for a basis by name; a search of library
   source finds none.
5. Declaring a different list with different flags requires no library change.

---

**PRIV-BASIS-002** — A purpose whose basis carries `RequiresAssessment` SHALL carry a
reference to a legitimate interest assessment. Absence SHALL fail validation.

*Source: D-032, D-108*

The abuse controls in `02-authentication` run on such a basis; that reliance must be
documented rather than implicit.

**Acceptance criteria**
1. Declaring a purpose on a `RequiresAssessment` basis without an assessment
   reference fails startup.
2. The reference appears in the generated record.

---

**PRIV-BASIS-003** — Consent SHALL carry a sub-distinction between **ordinary** and
**written** consent, determining which capture path runs. Where the basis carries
`RequiresWrittenConsentForSensitive`, a purpose over sensitive data SHALL use the
written path.

*Source: D-032, D-030, D-108*

**Acceptance criteria**
1. A purpose over sensitive data declaring ordinary consent, on a basis that requires
   written consent for sensitive data, fails validation.

---

**PRIV-BASIS-004** — Bases whose properties are all unset — in Egypt's list, 5 and 6
— SHALL exist in the declaration for record completeness. No code SHALL branch on
them, which the property rule guarantees.

*Source: D-032, D-108*

They are cited when circumstances arise, not implemented.

**Acceptance criteria**
1. No conditional in the library tests for any basis by name.

---

## 3. Sensitivity

**PRIV-SENS-001** — Sensitivity SHALL be a declared property of a resource type in
the model builder, naming its category from a **list the host declares at startup**.

Egypt's list ships as the default (Law 151/2020 Article 1): health, **genetic**,
biometric information, financial details, religious beliefs, political views,
criminal records, and children's data. Nothing in
the library branches on a category — it is a label for the records of processing — so
another jurisdiction declares its own list and changes no code (D-091, D-108).

*Source: D-030, D-108*

**Acceptance criteria**
1. A type declared sensitive derives every behaviour in PRIV-SENS-002 without
   further configuration.
2. Sensitivity appears as a distinct column in generated records.

---

**PRIV-SENS-002** — A resource type declared sensitive SHALL derive:

| Behaviour | Requirement |
|---|---|
| Consent | Written consent captured before processing **for those purposes that rest on consent** (PRIV-SENS-002a) |
| Encryption | Personal fields encrypted per subject (PRIV-RIGHT-005a); backups encrypted (DR-010); disk encrypted |
| Retention | Declared like any other category (PRIV-RET-001 AC1); the library has no default retention a sensitive category could be stricter than, so the declaration is required and the register shows it (D-153) |
| Records | Reported in the sensitive category, separately from non-sensitive |

*Source: D-030*

**The threat is named, because "encryption at rest" otherwise protects nothing
useful.** Disk encryption defends against a stolen disk and does nothing against a
database dump or a backup file — which is the threat the specification itself cites
for authenticator secrets.

| Threat | Control |
|---|---|
| Stolen disk | Full-disk encryption (infrastructure) |
| Database dump | Per-subject encryption of personal fields (PRIV-RIGHT-005a) |
| Backup file | Backup encryption under a key not on the host (DR-010) |

**Which fields are encrypted is declared, not universal.** Fields used for filtering
and search stay in plaintext — encrypting an order's district would break the address
flow. The declaration names personal fields; the transaction record is not encrypted.

**And per-subject encryption is scoped by schema, not by field.**

| Schema | Protection |
|---|---|
| Identity, and the schemas serving public applications | **Per-subject encryption** (PRIV-RIGHT-005a) |
| Management-only schemas — HR, finance, internal records | Database encryption at rest |

**The reason is exposure, not ownership.** Public-facing data is reachable by anyone
who reaches the service, so it warrants isolation per person. Management-only data is
entered by staff, read by staff, and never leaves — and per-subject encryption would not
protect it from the threat that matters there, since database access implies server
access implies the master key.

**A staff member appears on both sides, and that is not ambiguity.** Their identity
profile is in the identity schema and is per-subject encrypted, because they are a user
of public services like any customer. Their HR record is in a management schema and is
not. Two records, two schemas, one rule.

**A personal field declared in a management-only schema is a design question, not an
encryption one** — public users' personal data belongs in the schemas that serve public
applications.

**Erasure on the internal side is handled manually.** There is no per-subject key to
destroy and no automated overwrite routine. A request is fulfilled by locating the
record and clearing the fields by hand, recorded in the erasures table like any other.

*Source: D-101*

Proportionate to the expected frequency, and it **cannot silently miss a field** — a
person is looking, rather than a routine working from a list that may be incomplete.

**Personal fields in management schemas SHALL be noted as they are added**, so that an
automated routine can be built later without first auditing the schema cold.

**Building that routine is the user's decision**, not a threshold that fires on its own.

*Source: D-078, D-098*

**Acceptance criteria**
1. Processing a sensitive type **for a consent-based purpose** without recorded
   written consent is refused.
2. A database dump yields no personal field for any subject.
3. Fields declared for filtering remain queryable.
4. Generated records state the measures accurately for each threat, not "encrypted at
   rest" alone.

---

**PRIV-SENS-002a** — Consent SHALL gate **purposes, not records**. A record carrying
several purposes on several bases SHALL continue to be processed under its
non-consent bases when a consent-based purpose is withdrawn.

*Source: D-066*

An order carries at least three purposes at once: fulfilment (contractual
obligation), retention (legal obligation), and personalised recommendations
(consent). Gating the whole record on consent means either stalled fulfilment when a
customer withdraws mid-delivery, or a gate that does nothing.

| Purpose | Basis | Withdrawable |
|---|---|---|
| Fulfilment and delivery | Contractual obligation | No |
| Tax and financial retention | Legal obligation | No |
| Personalised recommendations | Consent | Yes |

**Acceptance criteria**
1. Withdrawing consent during an open order does not interrupt fulfilment, the
   courier callback, or refund processing.
2. Withdrawing consent stops personalised recommendations on the next request.
3. The customer's own order history remains visible to them and retained for tax.
4. Each declared purpose names its basis; a purpose without one fails validation.

---

**PRIV-SENS-003** — Storefront order history SHALL be declared sensitive.

*Source: D-030*

An individual purchasing insulin needles discloses a diabetes diagnosis through the
purchase itself. The order history is health data held in our own database.

**Acceptance criteria**
1. Order records for individual customers carry the sensitive declaration.
2. A written-consent record exists for any **consent-based purpose** applied to the
   order — not for the order itself.
3. An order is stored, fulfilled and retained for a customer who has granted no
   consent-based purpose.

*Criterion 2 corrected per D-066 (D-089).* As previously written it required written
consent before any order could exist. Since the only consent-based purpose on an order
is personalised recommendations — optional and withdrawable — that meant either forcing
consent to an optional purpose at checkout, which the voluntariness and no-bundling
rules prohibit, or making orders unstorable for every customer who declines. It was a
surviving fragment of the record-gating model D-066 replaced.

---

**PRIV-SENS-004** — Payment card data SHALL NOT be stored. Only a provider reference
and an amount SHALL be retained.

*Source: D-030*

Card details go to the payment provider and never touch our servers, which removes
the financial-detail category from our own holdings.

**Acceptance criteria**
1. No schema field holds a card number, expiry, or verification value.
2. A search of the codebase finds no card-data field name.

---

## 4. Consent

### 4.1 Capture

**PRIV-CONS-001** — Consent SHALL be recorded, never a boolean. Each record SHALL
carry: the specific purpose, the **version of the notice displayed**, timestamp,
mechanism, and withdrawal timestamp where applicable.

**Values (D-153).** `mechanism` is one of `10` section 5.21: `registration` · `dashboard`
· `reconsent` · `administrator`.

*Source: D-024*

**Acceptance criteria**
1. Consent for two purposes produces two records.
2. The notice version is resolvable to the exact text shown.
3. Withdrawal sets a timestamp rather than deleting the record.

---

**PRIV-CONS-002** — Consent SHALL NOT be bundled across purposes.

*Source: D-024*

**Marketing and analytics are separate purposes** with separate controls and separate
records. Marketing rests on consent (Law 151/2020 Art. 17 — no existing-customer
"soft opt-in" exists in Egyptian law). **Which basis per-person analytics rests on is
the host's declaration**, not the library's; the library supports either. Guidance
for the default (Egyptian) declaration: legitimate interest is not an express basis in
Law 151/2020 or Decree 816/2025 and is untested before the Centre, and per-person
behaviour over a sensitive resource type is itself sensitive-data processing — so
per-person analytics over sensitive types SHOULD rest on **consent** (basis 1, written
path), while legitimate interest is suited to fraud prevention, security and abuse
controls (PRIV-BASIS-001 table). A purpose declared on an objectable basis is
subject to objection rather than withdrawal (PRIV-RIGHT-001a) (D-064, D-145).

**Aggregate analytics is out of scope entirely** (D-066). Genuinely non-identifying
aggregate analysis is not personal data and requires no basis. **Guard rail:** outputs
must never be granular enough to single out an individual — a single customer in a
district buying a specific product is identifiable without a name. The raw order data
feeding the analysis remains under the order's own basis.

**Personalised recommendations are personal data and require consent** (D-066). A
recommendation derived from what a specific person bought is aimed at that
individual; on-premises processing does not change that.

**Acceptance criteria**
1. No consent record references more than one purpose.
2. A single control granting consent for multiple purposes fails review — the
   interface presents one control per purpose.

---

**PRIV-CONS-003** — Consent requests SHALL be intelligible, concise, prominent, and
SHALL require affirmative action. Pre-ticked controls SHALL NOT be used.

*Source: D-024*

**Acceptance criteria**
1. No consent control is checked by default.
2. Consent cannot be inferred from continued use or from silence.

---

**PRIV-CONS-004** — Written consent for sensitive data SHALL be captured
electronically and retained.

*Source: D-030*

The guideline permits *"paper form or through electronic means,"* so this is
buildable in a web flow — no signature capture required.

**Acceptance criteria**
1. The written-consent record is distinguishable from ordinary consent in storage.
2. It is retrievable for any past order.

---

**PRIV-CONS-005** — Every legal document (terms of service, the privacy notice,
consent texts and any other notice the host publishes) SHALL have exactly **one
governing language per document version**, defaulted from `legal.governinglanguage`.
Translations MAY be attached to a version and SHALL NOT be required to publish it.
Wherever a document is shown, the reader SHALL be able to view the governing text and
any attached translation **without changing the interface language**. Where a
translation and the governing text diverge, the governing text governs.

*Source: D-146; amends D-031*

`legal.governinglanguage` is required at install (OPS-BOOT-001, LIB-HOST-001),
protected (OPS-CFG-004: a restart to change) and raises a Normal alert when it changes
(OPS-ALERT-001). The governing language of each version is exposed through the
compliance-text endpoints (`09`) and the host contract (LIB-HOST-001). For the default
(Egyptian) deployment the governing language is Arabic (D-031); a host in another
jurisdiction declares its own. How the reader reaches the governing text and a
translation from the screen is the frontend's choice (`18-frontend-integration`); this
item requires only that both are reachable in place. Several governing languages on
one deployment (a multi-market document set) are deferred (`00` section 7.3,
`13-risk-register`).

**Acceptance criteria**
1. Every published document version carries exactly one governing language; a version
   published without naming one takes `legal.governinglanguage`.
2. A version publishes with no translation attached; a translation attaches to an
   existing version without creating a new one.
3. From any screen that shows a document, the governing text and each attached
   translation are reachable while the interface language stays as it was.
4. The compliance-text endpoints return the governing language of each version.

---

### 4.2 Versioning and refresh

**PRIV-CONS-006** — The privacy notice SHALL carry its own version. A document version
SHALL consist of the governing-language text, its governing language and the
translations attached to it. A change to the governing text SHALL create a new
version; attaching or correcting a translation SHALL NOT. A version whose
governing-language text is absent SHALL NOT publish, and the condition SHALL be
surfaced as **governing-language text missing** (OPS-ALERT-001).

*Source: D-041, D-031, D-146*

Consent validity depends on which notice version was displayed, so the notice needs
a version of its own. The version follows the governing text because that is the text
that binds; a translation is an aid to the reader, and correcting one changes nothing
about what was shown as authoritative.

**Acceptance criteria**
1. Changing the governing text creates a new version; attaching a translation to a
   published version does not.
2. A consent record resolves to the governing text of the version shown at the time.
3. Publishing a version without governing-language text is refused and the condition
   is raised on OPS-ALERT-001.

---

**PRIV-CONS-006a** — The privacy notice SHALL list every cookie the deployment sets —
name, purpose, lifetime, and whether any third party can read it — and SHALL state
that none requires consent because each is strictly necessary for the service. The
library sets only such cookies (session, pre-authentication, CSRF, trusted device,
remembered browser). A host that adds any
cookie that is not strictly necessary SHALL declare a consent-based purpose for it,
at which point a consent prompt becomes necessary; no cookie banner exists otherwise.

*Source: D-145*

No Egyptian rule addresses cookies; the ePrivacy exemption for strictly necessary
storage is the reference practice, and transparency about them is required
everywhere (CJEU Planet49: duration and third-party access are required information).

**Acceptance criteria**
1. The notice enumerates the cookies set, with purpose and lifetime.
2. No page shows a cookie consent prompt while only necessary cookies are set.
3. Registering a non-necessary cookie without a consent-based purpose fails
   validation.

---

**PRIV-CONS-007** — Where a processing activity or purpose substantially changes,
prior consent SHALL cease to be a valid basis and re-consent SHALL be requested.

**A superseded consent version SHALL prompt, never block.** Processing under
contractual or legal obligation SHALL be unaffected.

**Values (D-153).** Whether a version is material is decided by the person publishing it:
`POST /admin/documents/{document}/versions` takes a required boolean `material`. `true`
supersedes every live consent on the purposes the document covers
(`privacy.consent.superseded`) and the frontend re-asks; `false` publishes the version
and touches no consent. The audit record carries the answer. Code does not judge
materiality.

*Source: D-024, D-066*

Without this, publishing a revised notice — a text edit — would mark every existing
customer's order history superseded and refuse it until each re-consented. A
storefront-wide outage caused by editing a paragraph.

**Acceptance criteria**
1. A material change identifies which subjects require re-asking.
2. Only the consent-based purposes are suspended pending re-consent.
3. Publishing a revised notice interrupts no fulfilment and no order history access.
4. Re-consent is requested at the subject's next interaction.

---

### 4.3 Withdrawal

**PRIV-CONS-008** — Withdrawal SHALL be as easy as granting — the same number of
steps, no retention flow, no hidden settings, ideally through the same mechanism by
which consent was given.

*Source: D-024*

**Withdrawal ends the purpose, not only the activity.** Any data held *solely* for the
withdrawn purpose SHALL be erased on withdrawal, in the library and by every
registered handler for that purpose (`ConsentChanged`); the consent record itself is
retained for `retention.consent` as the evidence Law 151/2020 Art. 18 requires
(D-145).

**Acceptance criteria**
1. Withdrawal requires no more interactions than granting did.
2. No interstitial attempts to dissuade.
3. Withdrawal takes effect without human approval.
4. Data held solely for the withdrawn purpose no longer exists after the handlers
   complete; the consent record does.

---

**PRIV-CONS-008a** — The privacy notice SHALL be **presented, not accepted**. The
record SHALL be that a given version was displayed at a given time — never an
agreement.

*Source: D-056*

A privacy notice is a transparency obligation satisfied by informing. Recording
acceptance implies consent is the lawful basis for everything it describes, when most
processing runs on contractual or legal obligation. Consent is withdrawable; those
bases are not, and conflating them creates a withdrawal problem that does not
otherwise exist.

Terms of service are a contract and **are** accepted.

**Acceptance criteria**
1. No interface presents the privacy notice with an acceptance control.
2. The presentation record carries the notice version and timestamp.
3. Withdrawing any consent leaves processing under other lawful bases unaffected.

---

**PRIV-CONS-009** — Subjects SHALL be informed of the right to withdraw, and how,
at or before the time consent is obtained.

*Source: D-024*

**Acceptance criteria**
1. The consent screen states the right and the method.

---

**PRIV-CONS-010** — The cross-border transfer SHALL NOT rely on consent as its lawful
basis.

*Source: D-024, D-023*

Consent is available as a narrow exception, but a withdrawal would leave data that
cannot lawfully be hosted. The transfer stands on the regulator's permit instead.

**Acceptance criteria**
1. No consent record references the hosting transfer as its purpose.
2. Withdrawing any consent never renders the hosting unlawful.

---

### 4.4 Preference management

**PRIV-CONS-011** — A customer-facing **privacy dashboard** SHALL provide access to,
management of, and updating of consent settings at any time.

*Source: D-030*

Required by the guideline for sensitive-data processing, and the natural home for
the self-service rights in section 5.

**Acceptance criteria**
1. Every consent record held is visible to its subject.
2. Each can be withdrawn from the dashboard.
3. The dashboard is reachable without contacting support.

---

## 5. Data subject rights

**PRIV-RIGHT-001** — All nine rights SHALL be supported. Law 151/2020 Article 2
enumerates six (know and access; withdraw consent; correct, edit, delete, add or
update; limit processing; be notified of a breach; object); the regulator's Compliance
Plan Checklist and this design treat portability, being informed and rectification as
distinct rights — a superset of the statute, never less (D-135).

| Right | Mechanism |
|---|---|
| Be informed | Privacy notice, plus disclosure at collection |
| Access | Self-service export, dashboard |
| Withdraw consent | Dashboard |
| Erasure | Self-service: `POST /account/delete` starts the grace window (IDN-ACCT-007, IDN-LIFE-014); may also be requested out of band |
| Restrict processing | Request path; account state `restricted`; may also be requested out of band |
| Data portability | Same export, machine-readable format |
| Object | Dashboard — one switch per purpose on an objectable basis (PRIV-RIGHT-001a) |
| Rectification | Account editing, plus a route for non-editable data |
| Be notified of a breach | Section 7 |

*Source: D-037, D-113*

Self-service deletion **is** the exercise of the erasure right — the customer is
identified by their session, the grace window (`account.deletion.grace`) gives the
reversal period, and erasure runs when it elapses. The privacy-request queue carries
erasure only for requests that arrive **out of band** — a letter, a support email, a
guardian — where a human confirms the requester's identity and enters the request on
the subject's behalf; the six-working-day clock (PRIV-RIGHT-002) applies to those.
Restriction stays on the queue because it requires a human to decide the dispute.

**Objection is self-service and immediate** (PRIV-RIGHT-001a): the twin of consent
withdrawal for purposes the person was never asked about.

**Acceptance criteria**
1. Each right has a reachable mechanism requiring no support contact.
2. Erasure and restriction can additionally be entered on the queue by an authorised
   human for an out-of-band request, with identity confirmation recorded.
3. Each exercise is audited.

---

**PRIV-RIGHT-001a** — For every purpose whose declared basis carries `IsObjectable`
(PRIV-BASIS-001), the subject SHALL be able to **object**, and to withdraw the
objection, from the dashboard and through `POST` / `DELETE
/privacy/objections/{purpose}`, with no human approval and no grounds required. An
objection SHALL be recorded as a consent record is (purpose, notice version,
timestamp, mechanism, withdrawal timestamp) and SHALL raise `ObjectionChanged`, whose
handlers are **required** for every objectable purpose: processing of that subject for
that purpose stops when the event is handled. An objection is **always honoured**;
the library offers no "compelling grounds" refusal.

*Source: Law 151/2020 Art. 2, Decree 816/2025 Art. 3(5), D-145*

Consent means the person was asked and said yes, so they *withdraw*. An objectable
basis means the controller proceeded on its own recorded justification, so the person
*objects*. From the dashboard the two look the same — one switch per purpose — and
the label states which it is. Egyptian law grants the objection right without stating
any ground on which a controller may refuse; GDPR's "compelling legitimate grounds"
has no counterpart, so none is built. Whether a deployment has any objectable purpose
is a fact about its declaration, never assumed either way.

**Acceptance criteria**
1. A purpose on an objectable basis shows an objection switch; one on a consent basis
   shows a consent switch; the label names the basis.
2. Objecting writes a record and raises `ObjectionChanged` in one transaction.
3. A deployment declaring an objectable purpose without a registered handler for it
   fails startup, as for erasure and restriction (PRIV-RIGHT-005b).
4. Objection and its withdrawal take effect without human approval.
5. A purpose whose basis is not objectable returns `privacy.purpose.notobjectable`.

---

**PRIV-RIGHT-002** — A request SHALL be **decided** within **six working days** of
its **submission** (`privacy.request.decision`), per Law 151/2020 Article 2: "within six
working days from the date of submission", and the lapse of that period without a
decision is **deemed a rejection**. Submission is the moment the subject made the
request, not the moment it entered the queue: for an in-app request the two are the
same instant; for an out-of-band request the human entering it SHALL record the
**date received** (`receivedAt`, required, never later than now), and the clock runs
from that. **Working days are computed on the deployment's calendar**
(`privacy.workingdays`, default Sunday to Thursday, and `privacy.holidays`, the public
holidays **as currently listed**), never on a Monday to Friday assumption. **The
holiday list is a living calendar, not a startup condition** (D-142): Egypt's
holidays are partly lunar and the fixed ones are routinely observed on a day
announced shortly before, so the list is empty by default and a manager holding the
configuration permission adds or moves dates from the management app as they are
announced (a loosening under OPS-CFG-002, since a holiday pushes deadlines later,
and audited). A deadline is counted on the calendar as it stands when counted, so a
holiday added later is honoured for open requests. A holiday **missing** from the
list counts as a working day and yields an **earlier** deadline, which is always
compliant; the system therefore never needs the list to run, and a list running out
raises a Normal alert (OPS-ALERT-001), never a refusal to start. Whether "submission"
of a posted letter means postmark or receipt is for counsel; receipt is used, pending
counsel's confirmation of receipt versus postmark; if counsel says postmark, the
deadline is earlier than a receipt-based one and the `receivedAt` key of the request
is adjusted accordingly by the administrator entering it (D-147). The system SHALL therefore:

- send the subject an automatic **receipt** the moment the request enters the queue:
  at submission in the application, at entry for out-of-band requests a human
  types in. A receipt is not a decision; it starts nothing and stops nothing
- raise a **Normal** alert `privacy.request.warninglead` (default **2 working days**)
  before the deadline, and a **High** alert on the deadline day (OPS-ALERT-001)
- for a **restriction** request undecided at the deadline, **apply the restriction
  automatically** and record the decision as *granted by lapse*. Restriction suspends
  action, never visibility or data (PRIV-RIGHT-004), so granting it is always safe,
  and it turns a deemed rejection into a granted request
- for an out-of-band **erasure** request undecided at the deadline, record the
  request as *deemed refused by lapse*, notify the subject honestly (that the
  deadline passed without a decision, that they may resubmit, and of their right to
  complain to the Centre) and keep the record. Erasure cannot run without a human
  confirming identity, so the system never erases on its own

**Values (D-153).** Calendar days, working days and holidays are determined in
`privacy.calendar.timezone` (a required, protected deployment value). The six working
days are the six that follow the submission's calendar day in that zone; `decisionDue`
is the end (23:59:59) of the sixth; the warning fires `privacy.request.warninglead`
before it and the High alert at 00:00 of the deadline day. `receivedAt` is a calendar
date (`YYYY-MM-DD`) in that zone, refused with `privacy.request.receivedfuture` when
later than today there; the clock runs from the end of that date.

*Source: D-148; D-037, D-122, D-126, D-147*

With one operator, any deadline that depends on a human click is a deadline missed by
an absence. **Residual, honestly stated:** a paper letter arriving while nobody is
available to enter it is not in the queue and no clock runs; that is a
business-availability matter the library cannot control.

**Acceptance criteria**
1. Every request carries a creation timestamp, a computed decision deadline, and a
   receipt-sent timestamp equal to creation.
2. The Normal alert fires `privacy.request.warninglead` before the deadline and the
   High alert on the deadline day, without human monitoring.
3. A restriction request undecided at the deadline moves the account to
   `restricted` and is recorded *granted by lapse*.
4. An erasure request undecided at the deadline is recorded *deemed refused by
   lapse*, the subject is notified, and the record persists.
5. A decision made before the deadline (fulfil or refuse) cancels both alerts.

---

**PRIV-RIGHT-003** — Access and portability SHALL derive from **one export routine
in two formats**, human-readable and machine-readable. The export SHALL include the
host-declared preferences (REG-PREF-001), the list of the account's identifiers with
their roles and verification state (REG-IDENT-002), and the location records held on
the account's live sessions (AUTH-SESS-013).

*Source: D-148; D-037, D-146*

**Acceptance criteria**
1. Both formats contain the same data.
2. The machine-readable format is structured and documented.
3. The export of an account with declared preferences, several identifiers and a live
   session contains the preference values, every identifier with its role and state,
   and each session's location record.

---

**PRIV-RIGHT-004** — Restriction SHALL **suspend action, not visibility**. Restricted
records remain visible and continue to count in aggregates; what stops is acting on
them — shipping, refunding, contacting the subject — until the restriction lifts.

*Source: D-037, D-068, AUTHZ-GATE-006*

Restriction is temporary, pending a dispute. It is not deletion and not concealment;
removing restricted records from staff views would corrupt historical and analytical
totals for a condition that is meant to be reversible.

**Acceptance criteria**
1. Restriction suspends processing without deleting anything.
2. Lifting it restores prior behaviour exactly.
3. Enforcement is through the gate, not scattered checks.

---

**PRIV-RIGHT-005** — Erasure SHALL **anonymise**: the opaque subject identifier
remains, and every personal field is rendered unrecoverable by **destroying the
subject's key** (PRIV-RIGHT-005a). It SHALL NOT merely pseudonymise.

*Source: D-148; D-026.1, D-037, D-068, D-117, D-147*

**The distinction is decisive.** Pseudonymised data is still personal data, and
controls preventing further processing combined with pseudonymisation are not
sufficient to satisfy an erasure request. Anonymised data falls outside scope, so the
request no longer applies. A 2025 coordinated enforcement review of 764 controllers
found many techniques used as a substitute for deletion amounted to mere
pseudonymisation.

**Preferences and declared values go with the key.** Declared preferences
(REG-PREF-001) and the declared profile values (legal name, date of birth,
REG-PROF-001) are personal fields under the subject key and become unreadable with the
rest. A username freed by erasure is **held** for `retention.consent` and released
afterwards (REG-IDENT-009): it is public by nature and is not personal data under the
key, and the hold stops an erased person being impersonated at once under their former
name.

**Business records are outside the erasure right.** Order and transaction records
(what was ordered, when, how much, which district) are retained under their declared
lawful basis (legal obligation: tax law requires books, records and invoice copies
to be kept five years, counted per the tax law as confirmed by the accountant (D-140);
pending that confirmation the count runs from the later of the end of the fiscal year
and the date the return was filed (D-147); or legitimate interest) for
their declared retention period. PDPL Article 4(7) permits retention after the
purpose is satisfied for a legitimate reason provided the data is kept "in a form
that does not allow the identification of the Data Subject", which key destruction
produces: name, phone, email, address and photo are gone, and what remains is a
receipt with no buyer on it, as consumer tax receipts themselves are. No
singling-out test applies to these records.

**Acceptance criteria**
1. After erasure, no personal field is recoverable; business records retained under
   their declared basis and period are unaffected.
2. Audit records still show which actions occurred and when.
3. The identifier is never reissued.
4. A profile photo is rendered unreadable by the same operation (D-060): its bytes are
   encrypted under the subject key that operation destroys, the row persists
   (IDN-ATTR-003, IDN-PRIN-003), and `GET /account/photo` answers as for an account
   with no photo (D-157).
5. Order counts, revenue totals and product analysis are unchanged by an erasure.
6. Declared preferences and declared profile values are unreadable after erasure.
7. A username freed by erasure cannot be claimed until `retention.consent` has elapsed
   and is claimable afterwards.

---

**PRIV-RIGHT-005a** — Personal fields SHALL be encrypted with a **per-subject data key,
stored wrapped in the database under a key-encryption key held in the secrets manager**.
Erasure SHALL overwrite the wrapped key with an irreversible value.

**Values (D-153).** Fields are encrypted with AES-256-GCM (32 byte data key, 12 byte
nonce, 16 byte tag), one data key per subject, wrapped under the key-encryption key
with AES key wrap with padding (RFC 5649). The format marker is one byte, `0x01` for
this scheme. An erased wrapped key is 32 zero bytes under marker `0x00`; every decrypt
refuses it, and the DR-016 ledger and a restore recognise it as erased.

*Source: D-097, D-099, D-100, D-147*

**Per column, not per row.** Name and phone on an order are encrypted; product,
quantity, date, total and district are not. Aggregates and reports are unaffected
because they never read encrypted columns.

**How it works — envelope encryption, the standard pattern.**

| | |
|---|---|
| **Data key** | One per subject. Encrypts their personal fields |
| **Stored** | Wrapped by the KEK, in the library's own table, beside the data |
| **Key-encryption key** | In the secrets manager, fetched at startup. Never in the database |
| **Erasure** | Overwrite the wrapped key. Every field encrypted under it becomes unrecoverable at once |

**Why this reaches host tables without the host acting.** The wrapped key lives in the
library's table; the ciphertext lives wherever it was written, including host tables the
library never touches. Overwriting the wrapped key makes **all of it** unrecoverable —
so erasure of encrypted fields requires no participation from any application.

**Which key encrypts a field is declared, not stored again.**

Each encrypted field names the **existing column** that identifies its subject —
typically a foreign key that is present for business reasons and already indexed.

```
orders
| order_id | customer_id | enc_cust_name | enc_recipient_name | enc_recipient_phone |

enc_cust_name       → subject is customer_id
enc_recipient_name  → subject is customer_id
enc_recipient_phone → subject is customer_id
```

**No owner column is added and nothing is embedded in the ciphertext.** An order
already references its customer because it is that customer's order; encryption reuses
that rather than recording it twice. **A delivery recipient is not a second subject**:
their details are data the customer entered and are encrypted under the customer's
key (IDN-LIFE-002a, D-131).

**The declaration is per field, not per table**, so a row that does hold data about
two account-holding subjects — a referral row naming referrer and referred, say — is
handled without special cases: each encrypted field points at its own subject column.

*Rejected: an owner column on each row.* Duplicates a reference that already exists,
must be kept consistent with it, and needs a second column whenever a row concerns two
subjects.

*Rejected: the subject reference inside each ciphertext.* Self-describing, but enlarges
every value and removes the ability to find a subject's rows by query — the business
foreign key does that today.

**Requirements the pattern carries:**

- **Ciphertext SHALL be bound to its location** by additional authenticated data —
  subject identifier, table, column, **and the format marker below**. Without it
  ciphertext can be **moved between rows or between subjects**; with it, decryption
  fails when moved
- **The KEK SHALL be versioned**, with prior versions retained until re-wrapping
  completes. Rotation otherwise strands everything encrypted under the old version
- **Each stored value SHALL carry a format marker and its initialisation vector**:

  ```
  [format marker][initialisation vector][ciphertext + authentication tag]
  ```

  The **initialisation vector** is not optional — without a distinct one per
  encryption, two subjects with the same value produce identical ciphertext, which
  discloses that they match.

  The **format marker** names the algorithm and parameters, so a future change is
  read-old-write-new rather than a single all-at-once re-encryption. This is
  cryptographic agility, and the guidance is explicit: persist enough information to
  reconstruct the cryptographic context, so algorithm upgrades are routine engineering
  rather than emergency rewrites. Without an identifier, introducing a new algorithm
  requires a costly and slow format change.

  **The marker SHALL be included in the authenticated data.** Unauthenticated, it is a
  **downgrade vector** — an adversary editing the marker to force decryption under a
  weaker scheme. Authenticated, tampering makes decryption fail.

  **It cannot be added retrospectively:** existing values would carry no marker, so the
  problem it solves would already exist.

  There is **no key version in the marker.** Rotating the KEK re-wraps subject keys and
  leaves ciphertext untouched, so nothing about a stored value changes.
- **A plaintext data key SHALL NOT be logged**, nor retained beyond request scope
- **Primitives SHALL come from a maintained cryptographic library**, not be
  hand-written. The pattern is standardised — authenticated encryption for data, key
  wrapping for keys — and implementing it directly is a documented source of error

**What this does not protect.** A backup taken **before** an erasure holds the wrapped
key as it then was. Anyone holding both that backup and the KEK can recover the
subject's fields. Erasure of pre-erasure backups therefore completes when those backups
expire — bounded by retention, and stated rather than implied.

**Acceptance criteria**
1. Every encrypted field declares the column identifying its subject; startup fails
   otherwise.
2. A declared subject column that does not exist, or does not reference a subject,
   fails startup.
3. Two encrypted fields on one row may name different subject columns.
4. No plaintext key material is present in the database.
5. Two subjects with the same plaintext value produce different ciphertext.
6. Altering the format marker causes decryption to fail rather than to select a
   different algorithm.
7. A value written under an earlier format marker remains readable after a newer
   format is introduced.
8. A database dump without the KEK yields no personal field for any subject.
9. Overwriting a subject's wrapped key renders their fields unrecoverable in the live
   system, **including fields held in host tables**, with no application involvement.
10. Ciphertext moved to another row or subject fails to decrypt.
11. Aggregate queries over non-encrypted columns are unaffected.
12. Displaying one subject's details performs one unwrap, not one per field.
13. A KEK rotation (the `Janus.Cli` operation OPS-SEC-003, D-147) re-wraps subject
    keys without re-encrypting any data.

---

**PRIV-RIGHT-005c** — Searchable identifiers SHALL be stored as a **keyed fingerprint**
— an HMAC or equivalent keyed pseudorandom function under a **dedicated secret held
outside the database** — alongside their encrypted form. Erasure SHALL **neutralise**
the fingerprint, overwriting it with an irreversible value, never removing the row
(IDN-PRIN-003).

**Keyed is not optional, and that one word carries the whole control.** An unkeyed hash
satisfies "one-way" and is reversible in practice: national mobile numbers occupy a few
hundred million candidates, exhaustible in minutes, and email addresses come from breach
corpora. A plain hash therefore hands anyone holding a database dump **every customer's
phone number**, and confirms whether any named person is a customer — the threat model's
central attacker, served offline, with no key-service compromise required. The adjacent
encrypted column becomes decorative.

**The fingerprint SHALL be computed over a pinned, versioned canonical form** —
`NFKC_Casefold` for email addresses and names, E.164 for phone numbers (IDN-ACCT-004,
D-115) — applied before the keyed function, with the canonicalisation version (the
pinned Unicode version) stored alongside. Case-insensitive matching cannot be performed
by the database on a fingerprint, so it moves entirely here and becomes load-bearing;
pinning the version means a future normalization change re-derives rather than silently
stranding every existing fingerprint.

**Where the key lives:** in the secrets manager, fetched at startup beside the
key-encryption key and never written to the database (INF-HOST-003, D-105). It is
escrowed in the envelope (DR-009) because a restore without it is a system nobody can
sign in to.

**Rotation is possible and expensive.** Re-deriving requires the plaintext identifier,
recoverable by decrypting the subject's own encrypted column — so rotation is a batch
operation across every live subject. Erased subjects need none; their fingerprints are
already neutralised.

**Values (D-153).** The keyed function is HMAC-SHA-256 over the canonical UTF-8 bytes
(IDN-ACCT-004), 32 byte output. The neutralised value is 32 zero bytes, and no lookup
path may match it.

*Source: D-082, D-093*

Email and phone must be matchable for sign-in lookup and duplicate detection, and
anything searchable cannot be encrypted. This is inherent, not a gap.

**Erasure is therefore two steps** — destroy the subject key, neutralise the
fingerprint — **both at known locations, neither removing a row.**

**Both are writes to the same database, in one transaction.**

1. Overwrite the subject's **wrapped data key**
2. Neutralise the **fingerprint**
3. Record the erasure — the erasures row (IDN-LIFE-003b) and the outbox record for
   the host-side work (IDN-LIFE-003a) — in the same transaction

**No two-store ordering problem exists.** Under the earlier design the key lived in a
separate service, so the two steps could not share a transaction and no ordering of them
was safe — hence a durable intent record before either was attempted. With the wrapped
key in the database, all three writes commit together or none do.

**The erasures table remains** (IDN-LIFE-003b), because it is still the record of what
happened, when, and why — and because the host-side steps of a takedown, cancelling
orders and clearing any derived copies, remain asynchronous. Its status describes that
host-side work, never the library's own steps, which have committed if the row exists
(D-102).

**What one transaction does not cover.** A backup taken **before** the erasure holds the
wrapped key as it then was, so a restore to that point recovers the subject's fields.
The ledger (DR-016) re-applies erasures after any such restore, and otherwise erasure of
pre-erasure backups completes when they expire.

*Source: D-097, D-102*

**Both locations are known**, so no search across the schema is required.

*Source: D-093*

**Acceptance criteria**
1. Sign-in and duplicate detection operate on the fingerprint, never on plaintext.
2. A database dump without the fingerprint key yields no recoverable identifier, even
   for a small candidate space such as national mobile numbers.
3. Two casings or Unicode compositions of one identifier produce the same fingerprint.
4. The canonicalisation version is stored, and a change to it triggers re-derivation
   rather than mismatch.
5. Erasure neutralises the fingerprint in the same operation as key destruction, and
   the row persists.
6. No requirement obliges an application to maintain a per-schema redaction routine.

---

**PRIV-RIGHT-005b** — The library SHALL raise lifecycle events; the **host application
SHALL perform the work in its own tables and confirm completion**.

| Event | The application does |
|---|---|
| `ErasureRequested` | Redacts personal fields wherever it holds them |
| `RestrictionChanged` | Stops acting on that subject's records |
| `ExportRequested` | Returns everything it holds about the subject |

*Source: D-068*

The permission gate answers "may this principal do this to that record." Erasure,
restriction and export ask "which records are **about** this person" — a different
question the gate cannot answer, over tables the library must not read
(LIB-HOST-002).

**Delivered by the transactional outbox** (IDN-LIFE-003a): the identity change and the
event are written in one transaction, the worker publishes to every registered
subscriber, each confirms independently, and the request completes only when all
required subscribers have.

**Applications no longer maintain a redaction routine.** D-082 removed that
requirement: everything encrypted is handled by destroying the subject key, and the
only other step is neutralising the fingerprint (PRIV-RIGHT-005c). Applications still handle
**restriction** and **export**, which concern data they hold rather than data they must
redact.

**Acceptance criteria**
1. The request remains open until every required subscriber confirms completion.
2. An unanswered request retries rather than being silently marked done.
3. A resource type declared sensitive without a registered handler fails startup.
4. The library performs no write to a host table.
5. A profile photo is erased by the same operation — it is stored in the database
   (D-060), so erasure cannot succeed in one store and fail in another.

---

**PRIV-RIGHT-006** — Responses SHALL be in clear, intelligible, accessible language.

*Source: D-037*

**Acceptance criteria**
1. Response templates avoid legal and technical jargon.

---

## 6. Records of processing

**PRIV-ROPA-001** — Records of processing SHALL be generated in the shape of the
regulator's template, so output is directly submittable.

Fields, and their source:

| Field | Derived from |
|---|---|
| Purpose, data categories, subject categories | Model declaration |
| Lawful basis | PRIV-BASIS-001 |
| Non-sensitive / sensitive / children's columns | PRIV-SENS-001 |
| Retention period or criteria | Section 8 |
| Recipients and their legal characterisation | Declared per recipient |
| Data Protection Agreement links | Declared per recipient |
| **Data Hosting Environment** | Configuration: `hosting.environment` (free text) and `hosting.location` (D-153) |
| **Data Hosting Location** (inside / outside Egypt) | Configuration |
| **Basis of Cross Border Transfer** | Configuration |
| Personal Data Disposal Measures | PRIV-RIGHT-005 |
| Organisational Roles with Access | Grants |
| Implemented Technical Security Measures | Derived: the PRIV-SENS-002 controls and the protected keys of `10` section 4.8 (D-153) |
| **Data Owner** | **Declared — human input** |
| **Implemented Organisational Security Measures** | **Declared — human input** |
| **Links to LIA, DPIA, TIA** | **Declared — human input** |

*Source: D-036*

**Acceptance criteria**
1. Generated output matches the template's field set and ordering.
2. Three fields are declared rather than derived, and absence is flagged.
3. A purpose lacking a required assessment reference is reported.

---

**PRIV-ROPA-002** — All processors SHALL appear as recipients with their legal
characterisation and agreement reference.

Current processors: the payment provider, the shipping provider, the SMS gateway,
the hosting provider, and the developer.

**Values (D-153).** Recipients are a `recipients` declaration on the model builder
(LIB-HOST-001): each entry `{ name, characterisation: processor · recipient,
dataReceived: [category labels], location: inside · outside, agreementReference
(optional), callback: boolean }`, the six columns of `05` section 8. The library ships
that section's rows as defaults the host edits.

*Source: D-036, D-041, D-029*

**Acceptance criteria**
1. Each processor appears with characterisation and agreement reference.
2. A processor without an agreement reference is flagged.

---

**PRIV-ROPA-003** — The compromised-password screening call SHALL appear as a
cross-border transfer with destination and basis.

*Source: D-041*

Easy to omit precisely because it is buried in a password field.

**Acceptance criteria**
1. It appears in generated records with a stated basis.

---

## 7. Breach response

**PRIV-BREACH-001** — Notification SHALL be possible within **72 hours** to the
regulator and **three days** to affected subjects.

*Source: D-025, D-125*

**The subject clock is deliberately stricter than the law.** Article 7 counts the
three days to subjects **from notification to the Centre**, and sources differ on
whether the Centre or the controller notifies and on "days" versus "working days".
This specification counts both clocks from **detection** — a conservative choice that
is always compliant. It is recorded as a choice so that, in an incident, the operator
knows the legal slack exists and is not misled into thinking it does not.

**The clock starts at detection and is NOT deferrable pending investigation.**
Detection is the point of credible indication that an incident occurred, not the later
point at which it is concluded notifiable. Where notifiability is uncertain, the clock
is already running.

*Source: D-025, D-088*

**Acceptance criteria**
1. Notification templates exist for both audiences.
2. The clock starts at detection and is tracked.
3. No procedure defers the clock start pending assessment.
4. Operator-facing documents state the same rule (`11-runbook`,
   `14-takedown-procedure`).

---

**PRIV-BREACH-002** — Audit records SHALL be **queryable by data subject**, not only
chronologically.

*Source: D-025*

Answering "who was affected" fast is what makes the 72-hour clock achievable. An
indexing decision made now or a painful one made during an incident.

**Acceptance criteria**
1. Retrieving all audit records for one subject completes without a full scan.
2. The query works after erasure, returning anonymised records.

---

**PRIV-BREACH-003** — Breach notification to subjects SHALL be non-suppressible.

*Source: D-022*

**Acceptance criteria**
1. No preference suppresses it.

---

## 8. Retention

**PRIV-RET-001** — Retention SHALL be configurable per **(record category, lawful
basis)** with an enforced floor. Where a record carries several bases, **the longest
legal basis governs storage** and consent governs **use**.

**Values (D-153).** `retention.consent` counts from `withdrawnAt` where set, otherwise from
the subject's erasure instant; a live consent on a live account is never purged. The
same rule holds for objection records.

*Source: D-026.1, D-066*

Previously undefined: an order row is both sensitive data ("stricter defaults") and a
financial record ("long"), with no rule saying which governs.

| Category | Retention (default · floor) |
|---|---|
| Security events, permission changes, financial actions | **7 years · 5 years** (`retention.audit.security`) |
| Routine access logging | **90 days · 30 days** (`retention.audit.routine`) |
| Consent records | **Life of the processing + 3 years · + 1 year** (`retention.consent`) |
| Host-declared categories | Declared by the host with a floor (`retention.<category>`) |

*Source: D-026.1, D-132*

**Acceptance criteria**
1. Each category has a declared period or criteria.
2. Configuration below the floor is rejected at validation.
3. Periods appear in generated records.

---

**PRIV-RET-002** — Audit records SHALL be **append-only from the application** — no
update path, no delete path reachable by application code.

*Source: D-026.1, D-075*

**Audit rows hold identifiers and codes, never personal attributes.** This is what
makes append-only compatible with erasure: there is nothing personal in an audit row
to redact. Where an event must record an attribute — the old and new address on an
email change, the origin address on a sign-in — that attribute is written to a
**per-subject-encrypted column**, and erasure destroys the key (PRIV-RIGHT-005a)
rather than editing the row.

**Retention purging happens by partition.** Audit tables are partitioned by calendar
month on the occurrence instant, two months created ahead by the sweep, a partition
dropped when its end is older than the category's retention (D-153);
expired partitions are dropped by a scheduled background job (INF-BG-001) calling a
single `SECURITY DEFINER` function, `audit_drop_expired_partitions()`, created by the
migration step and executable only by a dedicated **maintenance role** whose
credential the worker fetches from the secrets manager (OPS-MIG-003a, D-118). The
function takes the two effective retention periods as arguments,
`audit_drop_expired_partitions(security_retention interval, routine_retention interval)`,
because a key at its default has no settings row the database could read; the worker
passes the catalogue's effective values, and the function refuses an argument below the
PRIV-RET-001 floor (five years, thirty days), written into it by the migration, so the
caller cannot shorten retention by argument (D-157). The
drop is itself audited as the job's action (INF-BG-002). The application cannot
delete rows, and no credential with schema rights leaves the pipeline; the function
can do exactly one thing.

Without this, three requirements were mutually unbuildable: short retention requires
deleting expired rows, the database refuses deletes, and the application holds no
schema rights — so an implementer would either grant delete permission, breaking
immutability, or never purge, breaking retention.

**Acceptance criteria**
1. An update or delete statement issued by the application is refused.
2. No audit row contains a personal attribute in plaintext.
3. Expired partitions are dropped on schedule without application involvement, and
   a missed run alerts (INF-BG-001).
4. Erasure renders encrypted audit attributes unreadable without modifying any row.
5. The maintenance role can execute `audit_drop_expired_partitions()` and nothing
   else that alters schema; the application role cannot execute it.

---

**PRIV-RET-003** — Every audit and financial record SHALL carry the instant the
event **occurred**, not the instant it was written, stored in UTC.

*Source: D-026.2*

These diverge under retry and queueing, and reconstructing a timeline from write
times is how audit trails end up lying.

**Acceptance criteria**
1. An event queued and written later records the original instant.
2. All stored timestamps are UTC; conversion happens at display.

---

**PRIV-RET-004** — Audit records SHALL be language-neutral — codes and structured
fields, never rendered sentences.

*Source: D-021*

An audit trail whose text varies by locale is not queryable and does not mean the
same thing to two readers.

**Acceptance criteria**
1. No audit field holds a translated string.
2. Rendering happens at display from codes.

---

**PRIV-RET-005** — Two records introduced by D-146 SHALL be treated as personal data
with the following retention. The **session location** (city-level, resolved from a
local IP database, AUTH-SESS-013) SHALL be stored under the subject key and retained
with the session record, no longer. The **sending-restriction record** (the HMAC of a
destination and its send timestamps, AUTH-ABUSE-004) SHALL be deleted when every
bucket for that key is empty, so it lives at most the longest bucket interval of any
restriction that applies to it.

*Source: D-146; AUTH-SESS-013, AUTH-ABUSE-004*

Guidance for the default (Egyptian) declaration, as PRIV-CONS-002 gives for fraud
controls (D-145): the restriction record exists for security necessity and rests, like
the other abuse controls, on legitimate interest (basis 4) with its assessment
(PRIV-BASIS-002). The session location is part of the session record and rests on the
basis the host declares for sessions. A destination HMAC is keyed (PRIV-RIGHT-005c) and
carries no account reference, so an empty record leaves nothing to erase; a live one
disappears on its own within the interval.

**Acceptance criteria**
1. A session location is unreadable after erasure and absent once the session record
   is swept.
2. No restriction record exists for a destination whose buckets are all empty, and
   none is older than the longest applicable bucket interval.
3. Both records appear in the generated records of processing under the purposes the
   host declares for them.

---

## 9. Minors

**PRIV-MINOR-001** — Where `registration.adultaffirmation` is `required` (the default)
the service SHALL be 18+ only and the affirmation SHALL be derived at **registration**. The
affirmation SHALL be derived from the age screen (REG-PROF-002); the date of birth
itself SHALL be retained only where `profile.dateofbirth` is on. A host that serves
minors (`registration.adultaffirmation` = `off`) SHALL declare a written-consent basis
for minors' data (PRIV-BASIS-003; children's data is a sensitive category,
PRIV-SENS-001).

*Source: D-148; D-027, D-073, D-146; supersedes IDN-LIFE-002 and the earlier "date of birth
SHALL NOT be collected"*

There is no guest checkout: every order belongs to an account (IDN-LIFE-002a), so
registration is the only collection path and the only place the affirmation is needed.

**Acceptance criteria**
1. With `registration.adultaffirmation` = `required`, registration requires the derived
   affirmation.
2. With `profile.dateofbirth` off, no date of birth is stored and no record carries
   one after the age step; with it on, the date is a personal field under the subject
   key.
3. With `registration.adultaffirmation` = `required`, no order exists whose subject
   has not affirmed.
4. A deployment with `registration.adultaffirmation` = `off` and no written-consent
   basis declared for minors' data fails startup validation.

---

**PRIV-MINOR-002** — A written takedown procedure SHALL exist and be executed on any
credible indication that a customer is under 18.

*Source: D-039, IDN-LIFE-003*

The affirmation verifies nothing. What makes self-declaration proportionate for
sensitive data is that the situation has a defined outcome rather than being decided
on the spot.

**Acceptance criteria**
1. The procedure exists as `14-takedown-procedure.md`.
2. An operation performs suspension, order cancellation, data erasure and recording
   **reliably, with per-subscriber completion visible** (IDN-LIFE-003a) — not
   atomically, which is unachievable across the library/host boundary.

---

## 10. Data minimisation to processors

**PRIV-MIN-001** — Data sent to the shipping provider SHALL follow a fixed field
mapping. Order contents SHALL NEVER be disclosed.

| Field | Rule |
|---|---|
| Package description | Fixed generic string; never product names or a narrowing category; never derived from the cart |
| Notes | Delivery instructions only |
| Business reference | Opaque internal reference |
| Receiver email | **Omitted** — phone suffices for a courier |
| Product catalogue integration | **Not used** for storefront orders |

*Source: D-030*

Standard practice, written down so it is not undone later by someone adding a
helpful field.

**Acceptance criteria**
1. The description field is not reachable from order line items in code.
2. No request to the shipping provider contains a product name.
3. A test asserts the outbound payload shape.

---

**PRIV-MIN-002** — The shipping provider's status callback SHALL be treated as
hostile input.

*Source: D-030, D-013*

**Acceptance criteria**
1. Correlation references are unguessable.
2. The endpoint is rate-limited.
3. A callback does not by itself advance order state.

---

**Residual, accepted.** The shipping provider knows the sender is a medical supplies
company, implying "bought something medical" rather than a specific condition — a
much weaker inference, unavoidable if we ship at all, and proportionate.
Cash-on-delivery amounts cross by necessity.

---

## 11. Open items

None. Cross-references: account states in `01-identity`; consent capture surfaces in
`02-authentication`; the model builder and gate in `03-authorization`; processor
contracts and field mappings in `05-integrations`; retention enforcement and secrets
in `06-operations`.
