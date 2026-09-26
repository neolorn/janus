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

**Values (D-166).** A purpose is declared with two lists beside its basis: the data
categories it requires and the categories of subject it is about. A purpose declared
with no data category fails startup with `model.startup.declarationmissing`,
`details.key` naming the purpose. Each encrypted field (PRIV-RIGHT-005a) names the data
category it holds; a field whose category no purpose declared on its type names fails
startup with `model.startup.declarationinvalid`, `details` naming the type and the
field. The records of processing print both lists per purpose (PRIV-ROPA-001).

*Source: D-030, P-003, D-162, D-166*

**Acceptance criteria**
1. Each declared purpose names the specific data categories it requires and the
   categories of subject it is about; a purpose naming no data category fails startup
   validation.
2. An encrypted field whose data category no purpose declared on its type names fails
   startup with `model.startup.declarationinvalid`.

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
library branches on. Egypt's six SHALL ship as a declaration the host passes to the
builder, `LawfulBases.Default`; the library SHALL apply no basis the host did not
declare.

| # | Basis | `IsConsent` | Written for sensitive | `RequiresAssessment` | `IsObjectable` | Typical use |
|---|---|---|---|---|---|---|
| 1 | Data Subject's Consent | yes | yes | no | no | Marketing; consent-based purposes over sensitive data |
| 2 | Fulfilment of a Contractual Obligation | no | n/a | no | no | Performing the contract the host has with the subject |
| 3 | Fulfilment of a Legal Obligation | no | — | no | no | Tax and financial record retention |
| 4 | Legitimate Interest | no | — | yes | yes | Fraud prevention, security, abuse controls |
| 5 | Claim or Defence of a Legal Right | no | — | no | no | Retaining evidence for a dispute |
| 6 | Execution of Court Judgments or Orders from Competent Investigative Authorities | no | — | no | no | Responding to a court order |

*Source: D-032, D-091, D-108, D-162, D-166*

**The code reads the properties, never the name** — the same rule factors follow
(AUTH-FACT-001). Whether a purpose is withdrawable, shows a dashboard control, runs the
capture path, needs a linked assessment, or can be objected to is answered by the
flags on its basis. A project in another jurisdiction declares a different list with
its own flags and the library changes not at all (D-091).

**Storage is a library table seeded from the declaration.** At startup each declared
basis is written as one row: its key, its label and its four flags. It carries the
properties the code reads, which is what makes it a genuine table rather than a
constrained column (CONV-ENUM-001). Purposes reference a basis by key; generated
records emit its label.

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

**Values (D-166).** The path is derived, not declared: a consent-based purpose takes
the written path where its basis carries `RequiresWrittenConsentForSensitive` and a type
it is declared on is declared sensitive, and the ordinary path otherwise (`10` section
5.10). A purpose declaration MAY name the written path where the ordinary one is
derived; one naming the ordinary path where the written one is derived fails startup.

*Source: D-032, D-030, D-108, D-166*

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

Egypt's list ships as a declaration the host passes to the builder,
`SensitiveCategories.Default` (Law 151/2020 Article 1): health, **genetic**, biometric
information, financial details, religious beliefs, political views, criminal records,
and children's data. The library reads one category by name, `children`, which sets the
children's column of the records of processing (PRIV-ROPA-001, D-162); nothing else
branches on a category. Every other category is a label for the records of processing,
so another jurisdiction declares its own list and changes no code; a list that names no
`children` category reports no children's processing (D-091, D-108).

*Source: D-030, D-108, D-162, D-166*

**Acceptance criteria**
1. A type declared sensitive derives every behaviour in PRIV-SENS-002 without
   further configuration.
2. Sensitivity appears as a distinct column in generated records.
3. The library applies no category the host did not declare.

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
and search stay in plaintext: encrypting a column a host record is queried by would
break the query that uses it. The declaration names personal fields; the rest of the
record is not encrypted.

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

*Source: D-078, D-098, D-162, D-166*

**Acceptance criteria**
1. Processing a sensitive type **for a consent-based purpose** without the data
   subject's recorded written consent, whoever the caller is, is refused.
2. A database dump yields no personal field for any subject.
3. Fields declared for filtering remain queryable.
4. Generated records state the measures accurately for each threat, not "encrypted at
   rest" alone.
5. A list filter or SQL fragment for a permission bound to a consent-based purpose
   admits no record whose data subject holds no live consent of the required kind for
   that purpose; a consent that is withdrawn, superseded, or recorded against a
   document other than the one the purpose now names is not live.
6. A check that names no record, or a record the library holds no subject for, is
   refused for a consent-based purpose.

---

**PRIV-SENS-002a** — Consent SHALL gate **purposes, not records**. A record carrying
several purposes on several bases SHALL continue to be processed under its
non-consent bases when a consent-based purpose is withdrawn.

*Source: D-066, D-166*

A host's business record commonly carries several purposes at once: performing the
contract (contractual obligation), retention (legal obligation), and a personalisation
purpose (consent). Gating the whole record on consent means either stalled processing
under the contract when a subject withdraws mid-way, or a gate that does nothing.

| Purpose | Basis | Withdrawable |
|---|---|---|
| Performing the contract | Contractual obligation | No |
| Tax and financial retention | Legal obligation | No |
| Personalisation | Consent | Yes |

**Acceptance criteria**
1. Withdrawing a consent-based purpose while the record is being processed under a
   non-consent purpose interrupts no processing under that purpose and invokes no
   `ConsentChanged` handler registered for it.
2. Withdrawing consent stops processing for the withdrawn purpose on the next request.
3. After the subject withdraws a consent-based purpose, the gate still admits the
   permissions serving a legal-obligation purpose on the subject's own records, the
   generated records state that purpose's basis and retention unchanged, and no
   `ConsentChanged` names that purpose.
4. Each declared purpose names its basis; a purpose without one fails validation.

---



## 4. Consent

### 4.1 Capture

**PRIV-CONS-001** — Consent SHALL be recorded, never a boolean. Each record SHALL
carry: the specific purpose, the **document and version displayed** (the document the
purpose's declaration names, the privacy notice where it names none), timestamp,
mechanism, and withdrawal timestamp where applicable.

**Values (D-153).** `mechanism` is one of `10` section 5.21: `registration` · `dashboard`
· `reconsent` · `administrator`.

**Values (D-166).** A grant named `dashboard` over a superseded, unwithdrawn consent for
the purpose is recorded `reconsent`, by the operation and whoever calls it; any other
mechanism is recorded as named.

**Every grant is a record of its own.** A grant SHALL add a record. A withdrawal or a
supersession SHALL stamp the live record and never overwrite or remove it. A subject
holds at most one live record (neither withdrawn nor superseded) per purpose, and every
earlier record stays, with its document, version and instants, for `retention.consent`
(PRIV-RET-001).

*Source: D-024, D-162, D-166*

**Acceptance criteria**
1. Consent for two purposes produces two records.
2. The document and version recorded resolve to the exact text shown.
3. Withdrawal sets a timestamp rather than deleting the record.
4. A grant after a withdrawal or a supersession adds a record and leaves the earlier one
   as it was; at most one live record per subject and purpose exists.
5. A dashboard grant over a superseded, unwithdrawn consent is recorded `reconsent`; a
   grant an administrator makes over one is recorded `administrator`.

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
must never be granular enough to single out an individual: one subject in a small
locality with a distinctive attribute is identifiable without a name. The raw host
records feeding the analysis remain under their own declared bases.

**Personalisation is personal data and requires consent** (D-066). Output derived
from what a specific person did is aimed at that individual; on-premises processing
does not change that.

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
2. It is retrievable for any past processing under the purpose it covers.

---

**PRIV-CONS-005** — Every legal document (terms of service, the privacy notice,
consent texts and any other notice the host publishes) SHALL have exactly **one
governing language per document version**, defaulted from `legal.governinglanguage`.
Translations MAY be attached to a version and SHALL NOT be required to publish it.
Wherever a document is shown, the reader SHALL be able to view the governing text and
any attached translation **without changing the interface language**. Where a
translation and the governing text diverge, the governing text governs.

*Source: D-146, D-166; amends D-031*

`legal.governinglanguage` is required at install (OPS-BOOT-001, LIB-HOST-001),
protected (OPS-CFG-004: changed from the server or by redeployment, never through the
application) and raises a Normal alert when it changes (OPS-ALERT-001). The governing language of each version is exposed through the
compliance-text endpoints (`09`) and the host contract (LIB-HOST-001). For the default
(Egyptian) deployment the governing language is Arabic (D-031); a host in another
jurisdiction declares its own. How the reader reaches the governing text and a
translation from the screen is the frontend's choice (`18-frontend-integration`); this
item requires only that both are reachable in place. Several governing languages on
one deployment (a multi-market document set) are deferred (`00` section 7.3,
`13-risk-register`).

**Values (D-166).** The library numbers a version; the publisher names none. A
version's number is the count of versions of that document published before it plus
one, written in decimal, counted per document; the highest is the current version. It
crosses the wire as the string `version` (`09` section 7).

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

*Source: D-041, D-031, D-146, D-166*

Consent validity depends on which notice version was displayed, so the notice needs
a version of its own. The version follows the governing text because that is the text
that binds; a translation is an aid to the reader, and correcting one changes nothing
about what was shown as authoritative.

**Acceptance criteria**
1. Changing the governing text creates a new version; attaching a translation to a
   published version does not.
2. A consent record resolves, through the document and version it names
   (PRIV-CONS-001), to the governing text shown at the time.
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
supersedes every live consent recorded against an earlier version of that document
(`privacy.consent.superseded`) and the frontend re-asks; `false` publishes the version
and touches no consent. The audit record carries the answer. Code does not judge
materiality.

**Values (D-166).** A purpose governs its consent by the document its declaration names,
the privacy notice where it names none (D-162). A consent recorded against a document
other than the one its purpose now names is superseded, and the subject is asked again
(AC4).

*Source: D-024, D-066, D-162, D-166*

Without this, publishing a revised notice (a text edit) would mark every live consent
superseded and refuse processing of every record it covers until each subject
re-consented. A service-wide outage caused by editing a paragraph.

**Acceptance criteria**
1. A material change identifies which subjects require re-asking.
2. Only the consent-based purposes are suspended pending re-consent.
3. Publishing a revised notice interrupts no processing under a contractual or
   legal-obligation purpose and no subject's access to their own records.
4. Re-consent is requested at the subject's next interaction.

---

### 4.3 Withdrawal

**PRIV-CONS-008** — Withdrawal SHALL be as easy as granting — the same number of
steps, no retention flow, no hidden settings, ideally through the same mechanism by
which consent was given.

*Source: D-024, D-166*

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
5. Withdrawing a consent the subject does not hold changes nothing and is answered as a
   withdrawal.

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

*Source: D-024, D-023, D-166*

Consent is available as a narrow exception, but a withdrawal would leave data that
cannot lawfully be hosted. The transfer stands on the regulator's permit instead.

**Acceptance criteria**
1. No consent record references the hosting transfer as its purpose: a consent-based
   purpose named for the hosting or its transfer fails startup with
   `model.purpose.hostingconsent` (INT-HOST-002).
2. After every consent-based purpose the deployment declares is withdrawn, the
   generated records state the same cross-border basis for the hosting provider and for
   every recipient outside Egypt, and add no finding.

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
| Rectification | Account editing, plus a request for non-editable data and, while the account is `restricted`, for any of its own fields (IDN-ACCT-007) |
| Be notified of a breach | Section 7 |

*Source: D-037, D-113, D-166*

Self-service deletion **is** the exercise of the erasure right — the customer is
identified by their session, the grace window (`account.deletion.grace`) gives the
reversal period, and erasure runs when it elapses. The privacy-request queue carries
erasure only for requests that arrive **out of band** — a letter, a support email, a
guardian — where a human confirms the requester's identity and enters the request on
the subject's behalf; the six-working-day clock (PRIV-RIGHT-002) applies to those.
Restriction stays on the queue because it requires a human to decide the dispute.

**Values (D-166).** Fulfilling an out-of-band erasure request SHALL begin the account's
deletion window (`deletingBy` `oob-request`, IDN-LIFE-003) where the account is
`active`; where it is `restricted`, holding the restriction (`restrictionHeld`,
PRIV-RIGHT-004); and where it is `suspended`, holding the suspension with its origin
(`suspensionHeld`, `10` section 5.12b), so that a cancelled deletion returns the account
to the state it was in. A fulfilment that begins the window sends the subject's
security-notice set `oob-deletion-notice`, which carries no cancel link (IDN-LIFE-003).
Where the account is already `deleting`, whatever its origin, the request is recorded
`fulfilled` against the running window and nothing restarts; where it is `deleted`, the
request is recorded `fulfilled` and nothing further happens. Fulfilling a request of any
type is a step-up action (`privacyrequest:fulfil`, `10` section 5a); refusing one is not.

**Objection is self-service and immediate** (PRIV-RIGHT-001a): the twin of consent
withdrawal for purposes the person was never asked about.

**Acceptance criteria**
1. Each right has a reachable mechanism requiring no support contact.
2. Erasure and restriction can additionally be entered on the queue by an authorised
   human for an out-of-band request, with identity confirmation recorded.
3. Each exercise is audited.
4. An out-of-band erasure request fulfilled while the account is `suspended` begins its
   deletion window; cancelling that deletion returns the account to `suspended` with its
   origin. One fulfilled while the account is `deleting` leaves the running window, its
   origin and its start as they were, and one fulfilled while it is `deleted` changes no
   account; both are recorded `fulfilled`.
5. Fulfilling a request of any type without step-up is refused with
   `auth.stepup.required` and changes nothing; refusing a request asks for none.

---

**PRIV-RIGHT-001a** — For every purpose whose declared basis carries `IsObjectable`
(PRIV-BASIS-001), the subject SHALL be able to **object**, and to withdraw the
objection, from the dashboard and through `POST` / `DELETE
/privacy/objections/{purpose}`, with no human approval and no grounds required. An
objection SHALL be recorded as a consent record is (PRIV-CONS-001: purpose, document and
version, which for an objection is always the privacy notice, timestamp, mechanism,
withdrawal timestamp) and SHALL raise `ObjectionChanged`, whose
handlers are **required** for every objectable purpose: processing of that subject for
that purpose stops when the event is handled. An objection is **always honoured**;
the library offers no "compelling grounds" refusal.

*Source: Law 151/2020 Art. 2, Decree 816/2025 Art. 3(5), D-145, D-166*

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
6. An objection before any version of the privacy notice is published is refused with
   `privacy.notice.unpublished`; withdrawing an objection the subject has not made
   changes nothing and is answered as a withdrawal.

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
  types in. The receipt is the message `privacy-request-received` (`10` section 5). A
  receipt is not a decision; it starts nothing and stops nothing
- raise a **Normal** alert `privacy.request.warninglead` (default **2 working days**)
  before the deadline, and a **High** alert on the deadline day (OPS-ALERT-001)
- for a **restriction** request undecided at the deadline, **apply the restriction
  automatically** and record the decision as *granted by lapse*. Restriction suspends
  action, never visibility or data (PRIV-RIGHT-004), so granting it is always safe,
  and it turns a deemed rejection into a granted request
- for an out-of-band **erasure** request undecided at the deadline, record the
  request as *deemed refused by lapse*, notify the subject honestly with the message
  `privacy-request-lapsed` (that the deadline passed without a decision, that they may
  resubmit, and of their right to complain to the Centre) and keep the record. Erasure
  cannot run without a human confirming identity, so the system never erases on its own
- for a **rectification** request undecided at the deadline, in-app or out of band,
  record the request as *deemed refused by lapse*, notify the subject as for erasure
  and keep the record; nothing recorded about the subject changes

**Values (D-153).** Calendar days, working days and holidays are determined in
`privacy.calendar.timezone` (a required, protected deployment value). The six working
days are the six that follow the submission's calendar day in that zone; `decisionDue`
is the end (23:59:59) of the sixth; the warning fires `privacy.request.warninglead`
before it and the High alert at 00:00 of the deadline day. `receivedAt` is a calendar
date (`YYYY-MM-DD`) in that zone, refused with `privacy.request.receivedfuture` when
later than today there; the clock runs from the end of that date. The two alerts, the
grant by lapse and the deemed refusals are taken by a pass of the one sweep of
OPS-OBS-003, every `sweep.interval`; each takes effect within that interval of its
instant.

*Source: D-148; D-037, D-122, D-126, D-147, D-166*

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
   `restricted`, or holds the restriction where the account is `suspended` or
   `deleting` (PRIV-RIGHT-004), and is recorded *granted by lapse*.
4. An erasure or rectification request undecided at the deadline is recorded *deemed
   refused by lapse*, the subject is notified, and the record persists.
5. A decision made before the deadline (fulfil or refuse) cancels both alerts.

---

**PRIV-RIGHT-003** — Access and portability SHALL derive from **one export routine
in two formats**, human-readable and machine-readable. The export SHALL carry every
group of REG-ACCT-001 that `GET /account` returns and the whole Standing group, in these
sections and this order: `account` (state, registration instant, terms version accepted,
notice version presented, the instant the age screen was answered and the affirmation or
age group it recorded), `profile`, `identifiers` (each with its kind, role, verification
state and whether it is in the security-notice set, REG-IDENT-002), `identifier-backup`,
`credentials` (the password and every enrolled credential, by property and label),
`recovery-codes` (how the set stands), `devices`, `preferences` (the value in force of
every host-declared preference, REG-PREF-001), `memberships` (every membership, one that
has ended included, with the instant it ended), `membership-acknowledgements`,
`group-memberships`, `grants` (every grant naming the account, in every organization,
live, expired or revoked, with the instant it stopped), `assurance` (the reachable
assurance and whether it resists relay), `sessions` (the location records held on the
account's live sessions, AUTH-SESS-013), `consents` and `objections` (every record,
PRIV-CONS-001). No section SHALL carry secret material: no password hash, TOTP secret,
public key, WebAuthn credential identifier, recovery code or device token fingerprint.

*Source: D-148; D-037, D-146, D-162, D-166*

**Acceptance criteria**
1. Both formats contain the same data.
2. The machine-readable format is structured and documented.
3. The export of an account with declared preferences, several identifiers and a live
   session contains the preference values, every identifier with its role and state,
   and each session's location record.
4. The export of an account holding a revoked grant, a grant in an organization it holds
   no membership of, a group membership and an ended membership carries all four, each
   ended one with the instant it ended.

---

**PRIV-RIGHT-004** — Restriction SHALL **suspend action, not visibility**. Restricted
records remain visible and continue to count in aggregates; what stops is acting on
them (contacting the subject, and whatever the host does on its own records about
them, through `RestrictionChanged`) until the restriction lifts. A restriction in force
when the account enters `suspended` or `deleting`, or decided while it is in either,
SHALL be held (`restrictionHeld`, `10` section 5.12b) and in force when the account
returns; `RestrictionChanged` is emitted when the restriction is decided and not again
on the return. A restriction is lifted by
an administrator (`POST /admin/accounts/{subject}/restriction/lift`), a step-up action
(`account:restrictionlift`); one held while the account is `suspended` or `deleting`
SHALL be lifted only once the account is `restricted` again.

Restriction is not a sanction on the person: a `restricted` account signs in, reads its
own data and exercises its rights; the changes IDN-ACCT-007 lists are refused with
`authz.restricted` (AUTHZ-GATE-006).

*Source: D-037, D-068, AUTHZ-GATE-006, D-166*

Restriction is temporary, pending a dispute. It is not deletion and not concealment;
removing restricted records from staff views would corrupt historical and analytical
totals for a condition that is meant to be reversible.

**Acceptance criteria**
1. Restriction suspends processing without deleting anything.
2. Lifting it restores prior behaviour exactly.
3. Enforcement is through the gate, not scattered checks.
4. An account restricted and then taken through a cancelled deletion window, a
   reversed takedown or a reactivated suspension returns `restricted`, and a
   restriction decided while it was away is in force on its return.
5. Lifting a restriction without step-up is refused and changes nothing; a lift of an
   account that is not `restricted`, one holding a restriction while `suspended` or
   `deleting` included, is refused with `identity.account.stateconflict`.

---

**PRIV-RIGHT-005** — Erasure SHALL **anonymise**: the opaque subject identifier
remains, and every personal field is rendered unrecoverable by **destroying the
subject's key** (PRIV-RIGHT-005a). It SHALL NOT merely pseudonymise.

*Source: D-148; D-026.1, D-037, D-068, D-117, D-147, D-166*

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
name. The address of a corporate mailbox (INT-MAIL-006) is a personal field of the
account that holds or last held it, under that account's key, and under a key of the
mailbox's own (PRIV-RIGHT-005a) while nobody holds it. Erasure neutralises the
fingerprint of every mailbox the subject holds or last held and leaves the row; the mail
server's own account is outside the library (D-101), reconciliation reports it as an
address the library does not hold, and no invitation gives the mailbox out again, to
anyone, without an administrator's choice (`formerMailbox`, REG-MAIL-003).

**The host's business records are outside the erasure right.** A record the host keeps
under a declared lawful basis (a financial record, say: what was transacted, when and
for how much) is retained under that basis (legal obligation: tax law requires books,
records and invoice copies to be kept five years, counted per the tax law as confirmed
by the host's accountant (D-140); pending that confirmation the count runs from the
later of the end of the fiscal year and the date the return was filed (D-147); or
legitimate interest) for its declared retention period. PDPL Article 4(7) permits
retention after the purpose is satisfied for a legitimate reason provided the data is
kept "in a form that does not allow the identification of the Data Subject", which key
destruction produces: name, phone, email, address and photo are gone, and what remains
is a record with no identifiable person on it, as a consumer receipt itself is. No
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
5. Aggregate queries over the non-encrypted columns of a host record are unchanged by
   an erasure.
6. Declared preferences and declared profile values are unreadable after erasure.
7. A username freed by erasure cannot be claimed until `retention.consent` has elapsed
   and is claimable afterwards.
8. After erasure, the fingerprint of every mailbox the subject holds or last held is
   neutralised and the mailbox row remains.

---

**PRIV-RIGHT-005a** — Personal fields SHALL be encrypted with a **per-subject data key,
stored wrapped in the database under a key-encryption key held in the secrets manager**.
Erasure SHALL overwrite the wrapped key with an irreversible value.

**Values (D-153).** Fields are encrypted with AES-256-GCM (32 byte data key, 12 byte
nonce, 16 byte tag), one data key per subject, wrapped under the key-encryption key
with AES key wrap with padding (RFC 5649). The format marker is one byte, `0x01` for
this scheme. An erased wrapped key is 32 zero bytes under marker `0x00`; every decrypt
refuses it, and the DR-016 ledger and a restore recognise it as erased.

*Source: D-097, D-099, D-100, D-147, D-166*

**Per column, not per row.** Name and phone on a host record are encrypted; its
non-personal columns (dates, amounts, quantities, references) are not. Aggregates and
reports are unaffected because they never read encrypted columns.

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

**A credential secret goes with the key too.** A TOTP secret (AUTH-FACT-006) is the
account's credential secret: it SHALL be encrypted under the account's subject key, and
erasure destroys it with the key.

**Which key encrypts a field is declared, not stored again.**

Each encrypted field names the **existing column** that identifies its subject —
typically a foreign key that is present for business reasons and already indexed.

```
host_records
| record_id | subject_id | enc_subject_name | enc_contact_name | enc_contact_phone |

enc_subject_name  -> subject is subject_id
enc_contact_name  -> subject is subject_id
enc_contact_phone -> subject is subject_id
```

**No owner column is added and nothing is embedded in the ciphertext.** A host record
already references its subject because it is that subject's record; encryption reuses
that rather than recording it twice. **A third party the subject names in their own
record is not a second subject**: their details are data the subject entered and are
encrypted under the subject's key (IDN-LIFE-002a, D-131).

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
- **The KEK SHALL be versioned**, with prior versions held by the application until
  re-wrapping completes. Rotation otherwise strands everything encrypted under the old
  version. A retired version leaves the application at once and stays in the secrets
  manager and the envelope until every backup taken before the rotation completed has
  expired (OPS-SEC-003, DR-009)
- **Everything under the key-encryption key is a row of the subject-key table.** A value
  the library encrypts that belongs to no subject (a secret of the deployment, or data
  held for a person who is not yet a subject) SHALL be encrypted, or its own data key
  wrapped, under the **deployment's data key**: a row of the subject-key table under a
  reserved identifier that no subject is issued and that erasure never touches, wrapped
  under the key-encryption key like any subject key. A rotation of the key-encryption key
  therefore re-wraps rows of that table and nothing else (OPS-SEC-003)
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

  There is **no key version in the marker.** Rotating the KEK re-wraps the rows of the
  subject-key table and leaves ciphertext untouched, so nothing about a stored value
  changes.
- **A plaintext data key SHALL NOT be logged**, nor retained beyond request scope
- **Primitives SHALL come from a maintained cryptographic library**, not be
  hand-written. The pattern is standardised — authenticated encryption for data, key
  wrapping for keys — and implementing it directly is a documented source of error

**What this does not protect.** A backup taken **before** an erasure holds the wrapped
key as it then was. Anyone holding both that backup and the KEK can recover the
subject's fields. Erasure of pre-erasure backups therefore completes when those backups
expire — bounded by retention, and stated rather than implied.

**Before any account exists.** The identifiers an invitation binds (REG-INV-001) SHALL be
encrypted as one value under a data key of the invitation's own, wrapped under the
deployment's data key and bound by the additional authenticated data to the invitation's
identifier, in the place a subject identifier takes. The value and its wrapped key SHALL
be overwritten when the invitation is revoked, acknowledged or found expired by the
sweep, and an erasure SHALL overwrite them, in the erasure transaction, for every
invitation attached to the subject. The invitation link's token SHALL be stored only as a one-way
hash, and an invitation's audit records SHALL name the invitation and nothing it binds.

**The send outbox.** A send may name no subject, or a subject that holds no key yet, so
an outbox row holds the whole message (destination, source address and values) in one
column under a data key of the row's own, wrapped under the deployment's data key, beside
the subject it names. Erasure SHALL overwrite the wrapped key of every outstanding row
naming the subject with the erased value (marker `0x00`, **Values** above), in the
erasure transaction, and a row whose key is erased SHALL be removed without being
carried.

**Acceptance criteria**
1. Every encrypted field declares the column identifying its subject; startup fails
   otherwise with `model.startup.declarationmissing`, `details.key` naming
   `<type>.<field>`.
2. An encrypted field or its declared subject column that is not a member of the
   declared type, or a subject column whose type is not the library's subject identifier
   (nullable or not), fails startup with `model.startup.declarationinvalid`, `details`
   naming the type, the field and the column.
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
13. A KEK rotation (the `Janus.Cli` operation OPS-SEC-003, D-147) re-wraps the rows of
    the subject-key table, the deployment's data key among them, without re-encrypting
    any data.
14. An invitation revoked, acknowledged or swept after expiry, and every invitation
    attached to an erased subject, holds no readable identifier and no wrapped key.
15. After erasure, an outstanding outbox message naming the subject cannot be decrypted
    and is never carried.
16. No value other than a row of the subject-key table is wrapped directly under the
    key-encryption key; after a rotation completes and the previous version is retired,
    every value the library encrypted, a subject's or not, still decrypts.
17. An account's TOTP secret is encrypted under its subject key, and after erasure it
    cannot be decrypted.

---

**PRIV-RIGHT-005c** — Searchable identifiers SHALL be stored as a **keyed fingerprint**
— an HMAC or equivalent keyed pseudorandom function under a **dedicated secret held
outside the database** — alongside their encrypted form. Erasure SHALL **neutralise**
the fingerprint, overwriting it with an irreversible value, never removing the row
(IDN-PRIN-003). The fingerprints erasure neutralises include the provider subject of
every linked identity (IDN-LIFE-012).

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
pinned Unicode version) stored alongside, and the fingerprint key version it was
computed under; a lookup matches under the current version first, then each other
version held. Case-insensitive matching cannot be performed by the database on a
fingerprint, so it moves entirely here and becomes load-bearing;
pinning the version means a future normalization change re-derives rather than silently
stranding every existing fingerprint.

**Where the key lives:** in the secrets manager, fetched at startup beside the
key-encryption key and never written to the database (INF-HOST-003, D-105). It is
escrowed in the envelope (DR-009) because a restore without it is a system nobody can
sign in to.

**Rotation is possible and expensive.** Re-deriving requires the plaintext identifier,
recoverable by decrypting the subject's own encrypted column — so rotation is a batch
operation across every live subject. Erased subjects need none; their fingerprints are
already neutralised. What no value stands behind (a held username, an erased subject's
reservation, the lines of the abuse ledgers) is read under its version until it lapses,
and the previous version is retired only then (OPS-SEC-003).

**Values (D-153).** The keyed function is HMAC-SHA-256 over the canonical UTF-8 bytes
(IDN-ACCT-004), 32 byte output. The neutralised value is 32 zero bytes, and no lookup
path may match it.

*Source: D-082, D-093, D-166*

Email and phone must be matchable for sign-in lookup and duplicate detection, and
anything searchable cannot be encrypted. This is inherent, not a gap.

**Erasure is therefore two steps** — destroy the subject key, neutralise the
fingerprint — **both at known locations, neither removing a row.**

**Both are writes to the same database, in one transaction.**

1. Overwrite the subject's **wrapped data key**, and the wrapped keys of its outstanding
   outbox rows and of every invitation attached to it (PRIV-RIGHT-005a)
2. Neutralise the **fingerprint**
3. Record the erasure — the erasures row (IDN-LIFE-003b) and the outbox record for
   the host-side work (IDN-LIFE-003a) — in the same transaction

**No two-store ordering problem exists.** Under the earlier design the key lived in a
separate service, so the two steps could not share a transaction and no ordering of them
was safe — hence a durable intent record before either was attempted. With the wrapped
key in the database, all three writes commit together or none do.

**The erasures table remains** (IDN-LIFE-003b), because it is still the record of what
happened, when, and why, and because the host-side steps of a takedown (whatever the
host does in its own tables on `TakedownExecuted`, and clearing any derived copies)
remain asynchronous. Its status describes that host-side work, never the library's own
steps, which have committed if the row exists (D-102).

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
7. Each fingerprint is stored with the key version it was computed under, and a lookup
   finds a value fingerprinted under any version the deployment holds.

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
| Purpose, data categories, subject categories | Model declaration: each purpose's own two lists (PRIV-PRIN-001) |
| Lawful basis | PRIV-BASIS-001 |
| Non-sensitive / sensitive / children's columns | PRIV-SENS-001; the children's column is true for a purpose exactly where a type it is declared on carries the `children` category |
| Retention period or criteria | Section 8: one entry per data category the purpose requires, written `<category> <period>` with the period as an ISO 8601 duration, longest first; a category whose period cannot be read is given none and flagged `retention-missing` against it |
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

*Source: D-036, D-162, D-166*

**Acceptance criteria**
1. Generated output matches the template's field set and ordering.
2. Three fields are declared rather than derived, and absence is flagged
   (`data-owner-missing`, `organizational-measures-missing`, `assessment-links-missing`).
3. A purpose lacking a required assessment reference is reported (`assessment-missing`).
4. With `registration.adultaffirmation` = `off` and no resource type declaring the
   `children` category, the register carries the finding `children-undeclared`
   (PRIV-MINOR-001).

---

**PRIV-ROPA-002** — All processors SHALL appear as recipients with their legal
characterisation and agreement reference.

Processors the library itself makes true: the mail server, the SMS gateway, the
hosting provider and the password-screening recipient (D-162 C.103). Every processor of
the host's own business is the host's declaration.

**Values (D-153).** Recipients are a `recipients` declaration on the model builder
(LIB-HOST-001): each entry `{ name, characterisation: processor · recipient,
dataReceived: [category labels], location: inside · outside, agreementReference
(optional), callback: boolean }`, the six columns of `05` section 6. The library ships
that section's four rows as defaults (D-162 C.103, D-165): the hosting provider and the
SMS gateway rows on every deployment, since every deployment holds data and declares
SMS alert destinations (LIB-HOST-001); the mail server row where a mail server is
integrated (`IMailServer` registered or `integration.mailserver.endpoint` set) or the
default mail transport is in use; the password-screening row while online screening is
configured. The host adds its own; a person or firm that administers the deployment for
the controller (D-029) is a processor the host declares.

*Source: D-036, D-041, D-029, D-166*

**Acceptance criteria**
1. Each processor appears with characterisation and agreement reference.
2. A processor without an agreement reference is flagged (`agreement-missing`).

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

**Values (D-166).** Every audit record SHALL carry a nullable `subject`: the data subject
the record concerns, where it concerns one. The acting and effective identities name who
acted and are the same on every record (AUTHZ-IMP-001); they do not name whom the action
concerns. The query by data subject reads, through an index, the records naming the
person as `subject` or as acting identity (`GET /admin/audit?subject=`, `09` section
8a). It returns each record's codes, instants, identifiers and plain details, and never a
value held under a subject's key (PRIV-RET-002), so it reads the same before and after
an erasure.

*Source: D-025, D-166*

Answering "who was affected" fast is what makes the 72-hour clock achievable. An
indexing decision made now or a painful one made during an incident.

**Acceptance criteria**
1. Retrieving all audit records for one subject completes without a full scan.
2. The query works after erasure, returning anonymised records: a record concerning a
   subject whose key carries the erased marker (`0x00`, PRIV-RIGHT-005a) is returned
   with its codes, instants and identifiers, as before the erasure.
3. The query returns no value a record holds under a subject's key.
4. An action taken on another person's account, from a break-glass session included, is
   returned by the query for that person through `subject`, and names the actor as both
   acting and effective identity.

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

Previously undefined: a host record can be both sensitive data ("stricter defaults")
and a financial record ("long"), with no rule saying which governs.

| Category | Retention (default · floor) |
|---|---|
| Security events, permission changes, financial actions, privacy actions (PRIV-RET-002) | **7 years · 5 years** (`retention.audit.security`) |
| Routine access logging | **90 days · 30 days** (`retention.audit.routine`) |
| Consent records | **Life of the processing + 3 years · + 1 year** (`retention.consent`) |
| Host-declared categories | The floor declared with the category (LIB-HOST-001) · that floor; a stated period below it fails startup with `config.value.belowfloor` (`retention.<category>`) |

*Source: D-026.1, D-132, D-166*

**Acceptance criteria**
1. Each category has a declared period or criteria.
2. Configuration below the floor is rejected at validation.
3. Periods appear in generated records.

---

**PRIV-RET-002** — Audit records SHALL be **append-only from the application** — no
update path, no delete path reachable by application code.

*Source: D-026.1, D-075, D-166*

**Audit rows hold identifiers and codes, never personal attributes.** This is what
makes append-only compatible with erasure: there is nothing personal in an audit row
to redact. Where an event must record an attribute — the old and new address on an
email change, the origin address on a sign-in — that attribute is written to a
**per-subject-encrypted column**, and erasure destroys the key (PRIV-RIGHT-005a)
rather than editing the row.

**Two categories.** Every audit record is `security` or `routine`, the two whose
periods `retention.audit.security` and `retention.audit.routine` set (PRIV-RET-001) and
`audit_drop_expired_partitions` takes. A record of a privacy action (consent,
objection, legal document, data subject request, erasure, export) is `security`.

**Retention purging happens by partition.** Audit tables are partitioned by calendar
month on the occurrence instant, two months created ahead by the partition job, a
partition dropped when its end is older than the category's retention (D-153); the
months ahead are created and expired partitions dropped by one daily background job
(INF-BG-001, `audit-partitions`) calling two `SECURITY DEFINER` functions,
`audit_ensure_partitions()` and `audit_drop_expired_partitions()`, created by the
migration step and executable only by a dedicated **maintenance role** whose
credential the library reads through the host's `ISecretSource` at startup
(LIB-EXT-001, OPS-MIG-003a, D-118). The drop function takes the two effective retention
periods as arguments,
`audit_drop_expired_partitions(security_retention interval, routine_retention interval)`,
because a key at its default has no settings row the database could read; the worker
passes the catalogue's effective values, and the function refuses an argument below the
PRIV-RET-001 floor (five years, thirty days), written into it by the migration, so the
caller cannot shorten retention by argument (D-157). Each completed run is audited as
the job's action (INF-BG-002): `ops.auditpartitions.maintained`, with `created`,
`dropped`, `securityRetentionDays` and `routineRetentionDays`. A run under a credential
that is not the maintenance credential is refused before either function is asked, and a
retention that cannot be read drops nothing. The application cannot delete rows, and no
credential with schema rights leaves the pipeline; each function can do exactly one
thing.

Without this, three requirements were mutually unbuildable: short retention requires
deleting expired rows, the database refuses deletes, and the application holds no
schema rights — so an implementer would either grant delete permission, breaking
immutability, or never purge, breaking retention.

**Acceptance criteria**
1. An update or delete statement issued by the application is refused.
2. No audit row contains a personal attribute in plaintext.
3. Expired partitions are dropped on schedule without application involvement, and
   a missed run alerts (INF-BG-001).
4. Erasure renders encrypted audit attributes unreadable without modifying any row; a
   read of such a row returns the attributes empty and does not refuse the row, and a
   subject key that is present and cannot be unwrapped fails the read.
5. The maintenance role can execute `audit_ensure_partitions()` and
   `audit_drop_expired_partitions()` and nothing else that alters schema; the
   application role can execute neither.

---

**PRIV-RET-003** — Every audit and financial record SHALL carry the instant the
event **occurred**, not the instant it was written, stored in UTC.

*Source: D-026.2, D-166*

These diverge under retry and queueing, and reconstructing a timeline from write
times is how audit trails end up lying.

**Acceptance criteria**
1. An event queued and written later records the original instant.
2. All stored timestamps are UTC: every column that carries a time of day is
   `timestamp with time zone`; a calendar day is a `date` and is not an instant;
   conversion happens at display.

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
with the session record, no longer. The **sending-restriction record** of a destination
(its HMAC, the fingerprint key version the HMAC was computed under and its send
timestamps, AUTH-ABUSE-004) SHALL be deleted once the longest interval the current
destination restrictions declare has passed since its newest send: before a send's
counters are read, and on every run of the expiry sweep (`sweep.interval`,
OPS-OBS-003), whether or not another send is made. A restriction on another key kind
does not lengthen it, since the records of the other key kinds are kept apart.

*Source: D-146; AUTH-SESS-013, AUTH-ABUSE-004, D-162, D-166*

Guidance for the default (Egyptian) declaration, as PRIV-CONS-002 gives for fraud
controls (D-145): the restriction record exists for security necessity and rests, like
the other abuse controls, on legitimate interest (basis 4) with its assessment
(PRIV-BASIS-002). The session location is part of the session record and rests on the
basis the host declares for sessions. A destination HMAC is keyed (PRIV-RIGHT-005c) and
carries no account reference, so an expired record leaves nothing to erase; a live one
disappears on its own once the interval has passed since its newest send.

**Acceptance criteria**
1. A session location is unreadable after erasure and absent once the session record
   is swept.
2. No restriction record exists for a destination once the longest interval of the
   destination restrictions as they now stand has passed since its newest send, whether
   or not another send is made; a restriction on another key kind does not lengthen it.
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

*Source: D-148; D-027, D-073, D-146, D-166; supersedes IDN-LIFE-002 and the earlier "date
of birth SHALL NOT be collected"*

Every subject holds an account (IDN-LIFE-002a): there is no anonymous path into the
host's records, so registration is the only collection path and the only place the
affirmation is needed.

**Acceptance criteria**
1. With `registration.adultaffirmation` = `required`, registration requires the derived
   affirmation.
2. With `profile.dateofbirth` off, no date of birth is stored and no record carries
   one after the age step; with it on, the date is a personal field under the subject
   key.
3. With `registration.adultaffirmation` = `required`, no account of a person exists
   whose subject has not affirmed; the reserved `emergency` account (OPS-BOOT-002) and
   the restore-test canary (DR-007), which no person answers for, carry no answer.
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
2. An operation performs suspension, the host's `TakedownExecuted` work, data erasure
   and recording **reliably, with per-subscriber completion visible** (IDN-LIFE-003a),
   not atomically, which is unachievable across the library/host boundary.

---

## 10. Open items

None. Cross-references: account states in `01-identity`; consent capture surfaces in
`02-authentication`; the model builder and gate in `03-authorization`; processor
contracts and the callback pipeline in `05-integrations`; retention enforcement and
secrets in `06-operations`.
