# 05 — Integrations

External systems, the contracts with them, and the field mappings that cross those
boundaries.

**Prerequisite:** `00-overview.md`, sections 1 and 7.2.

**Scope.** This document covers *what leaves the system and what enters it*. Consent
and minimisation policy is `04-privacy`; this is the mechanics.

**Governing principle for all integrations:** integrate through the provider's
**contract**, never their internals. A standards-based or documented interface
survives a provider rewriting their implementation; coupling to their storage or
schema does not.

---

## 1. General requirements

**INT-GEN-001** — Every outbound integration SHALL use TLS. A configuration
specifying a plaintext endpoint SHALL be rejected at startup.

*Source: P-003*

This is not theoretical. The shipping provider's own published environment file
specifies a plain `http://` base URL, which would send customer names, addresses and
phone numbers unencrypted. Rejecting it at validation is what prevents that value
being copied into configuration unexamined.

**Acceptance criteria**
1. A base URL with scheme `http` fails startup with a named error.
2. The error identifies which integration and which setting.

---

**INT-GEN-002** — Provider credentials SHALL be stored as rotatable secrets under
the key-encryption key, never in configuration files or environment variables in
production.

*Source: D-026.3, D-041*

**Acceptance criteria**
1. No provider credential appears in any repository file.
2. Rotating a credential requires no code change.

---

**INT-GEN-003** — Every inbound callback SHALL be treated as hostile input.

**Where the provider supports signing**, signature verification per BFF-MACH-002 is
the primary control — the provider's published algorithm, constant-time comparison,
a five-minute timestamp
window, raw bytes, idempotency on the event identifier.

**Where it does not**, the callback is a **hint that triggers verification against the
provider's API**, never a fact — supported by unguessable references, rate limiting,
and source restriction where ranges are published.

A callback SHALL NEVER by itself advance an authoritative state.

*Source: D-070*

*Source: D-013, D-030*

Callbacks arrive unauthenticated or weakly authenticated. A forged one must be
unable to mark a phone verified, an order delivered, or a payment received.

**Acceptance criteria**
1. A callback with a guessed reference is rejected.
2. State advances only after the system confirms independently, or the callback is
   treated as a hint requiring corroboration.
3. Rejected callbacks are logged with their source.

---

**INT-GEN-004** — Every provider SHALL appear in generated records of processing
with its legal characterisation and agreement reference.

*Source: D-036, PRIV-ROPA-002*

**Acceptance criteria**
1. Adding a provider without an agreement reference is flagged.

---

**INT-GEN-005** — Outbound payloads SHALL be constructed by a dedicated mapping
layer, never assembled ad hoc at call sites.

*Source: D-030*

The minimisation rules in `04-privacy` are only enforceable if there is exactly one
place a payload is built.

**Acceptance criteria**
1. A test asserts the exact field set of each outbound payload.
2. No call site constructs a provider request directly.

---

**INT-GEN-006** — The city-level location shown on a session (AUTH-SESS-013) SHALL be
resolved from a **local IP-to-city database**: a file bundled with the library or
supplied by the host, read in process, **never a call to a third party**. The file
SHALL be refreshed on a schedule as a background job (INF-BG-001), and a stale or
missing file SHALL degrade to no location, never to an external lookup.

*Source: D-146; AUTH-SESS-013*

A network lookup would hand every sign-in address to whoever runs the lookup service.
The database is a data file like the courier's district taxonomy (INT-SHIP-005), and
its age is a degradation to surface (OPS-OBS-002), not a reason to look elsewhere.

**Acceptance criteria**
1. No outbound request is made to resolve a location; a test asserts the resolver
   performs no network call.
2. Refresh runs without human action and a failed refresh surfaces as a degradation.
3. With no file available, sessions are listed without a location and nothing else
   changes.

---

## 2. Mail — Stalwart

### 2.1 Integration shape

**Edition: Community.** Verified against the full documentation — OIDC
authentication, directory backends, app passwords with credential scoping, and the
JMAP management API and CLI are all Community. Enterprise covers multi-tenancy,
dashboards, AI models, telemetry history and its own alerting, none of which are used
(D-081).

**Consequence:** the mail server will not alert on its own problems. Reconciliation
drift detection (INT-MAIL-007) and OPS-ALERT-001 carry that.

**INT-MAIL-001** — Stalwart SHALL be integrated through four contracts and no
others.

| Concern | Contract |
|---|---|
| Authentication | Stalwart configured as an OIDC client against our provider |
| Provisioning | Account lifecycle pushed as **JMAP objects** through the JMAP management API; the CLI is a client of the same API and is not a second contract |
| App passwords | **Stored by Stalwart**, never by this system; managed for the person through the library's first-party OIDC client (INT-MAIL-010) |
| Storage | Stalwart's own database, not co-located with business data |

*Source: D-006, D-146*

**Restated** (D-146): every management operation happens through JMAP objects, so
"management API" means JMAP and nothing else. The four concerns are unchanged. Should the edition change,
SCIM v2 is the alternative provisioning push for the account lifecycle; it carries no
credential and would replace the provisioning row only.

**Acceptance criteria**
1. No code reads Stalwart's database.
2. No app-password credential type exists in this system's schema.
3. Every provisioning and app-password operation is a JMAP request; no other
   management interface of the mail server is called.

---

**INT-MAIL-002** — Stalwart's storage SHALL NOT be co-located with business data, and
**SHALL be backed up** (DR-013).

*Source: D-006, D-085*

Separation avoids coupling to a pre-1.0 schema and keeps mail out of the database
holding customer health data. It also means the application's continuous archiving
does **not** cover it — unnoticed until the second review, and it would have lost all
staff mail history on host loss.

Its data store is a key-value namespace of opaque byte-keyed rows even on
PostgreSQL, so co-location yields nothing — it is not joinable or queryable. Mail is
write-heavy and would contend with application workload, and read replicas are not
available in the community edition.

**Acceptance criteria**
1. Stalwart's connection string names a different database.
2. No query joins across the two.

---

**INT-MAIL-003** — Where mail data is required in an application, it SHALL be read
through Stalwart's API, never its database.

*Source: D-006*

**Acceptance criteria**
1. No SQL in this system references a Stalwart table.

---

### 2.2 Authentication

**INT-MAIL-004** — Our OIDC provider SHALL expose discovery, JWKS and userinfo for
Stalwart's use. Token introspection SHALL NOT be provided.

*Source: D-005, D-041*

Stalwart validates offline via JWKS and falls back to userinfo; it does not use
introspection with client credentials.

**Acceptance criteria**
1. Stalwart authenticates successfully with introspection absent.
2. The discovery document does not advertise an introspection endpoint.

---

**INT-MAIL-005** — The staff authentication policy SHALL be stated accurately as
**passkeys for interactive access, scoped app passwords for mail protocols**.

*Source: D-006*

Widely deployed mail clients cannot perform OAuth bearer authentication with
third-party providers, so a non-phishing-resistant path to mail exists by necessity.
Since mailboxes receive password-reset links, this is the weakest credential in the
system and must be named rather than glossed.

**Acceptance criteria**
1. No document or interface claims staff are passkey-only without qualification.
2. The limitation is recorded in the risk register as R-A02.

---

### 2.3 Provisioning

**INT-MAIL-006** — **Human administrative-organization members** SHALL have a mailbox
pre-created in the mail server before mail can arrive for them. Where the member is
invited (REG-MAIL-001), the mailbox SHALL be provisioned **disabled** when the
invitation is sent and **enabled** when the membership attaches at acknowledgement;
an expired invitation SHALL leave it **reserved and disabled** until the administrator
re-invites or deletes it. The invitation SHALL name a personal email as well, and the
account that owns the mailbox SHALL NOT exist until that address is verified by the
press on the invitation link (REG-MAIL-001, D-148); the mailbox is reserved for the
invitation, not for an account, until then.

*Source: D-148; D-006, D-074, D-144, D-146*

**A mailbox is for a person.** The reserved `emergency` account (OPS-BOOT-002) is a
member of the administrative organization but is not a person (it is a role a human
steps into with the sealed credential) and it holds **no email address and no
mailbox**. Nothing is ever addressed to it; every message about a break-glass session
goes to the owner's and operator's recorded destinations (D-129). This is a
definition, not an exemption: an unread mailbox on the most privileged account would
be a phishing target and a place for a warning to be missed.

**The first administrator is a person, created before the mail server may exist.**
Bootstrap (OPS-BOOT-001) runs against the database on a fresh host; the membership is
effective at once and the provisioning request is written to the retried outbox
(D-022, INT-MAIL-008), so the mailbox is created the moment the mail server is
reachable. Until then the administrator is reached at the personal address given at
bootstrap, where the enrolment link goes in any case.

**Scope is deliberate and was previously missing.** The mail server hosts the
company's own mail: staff mailboxes. **Customers are not provisioned a mailbox**;
they hold their own email elsewhere and only receive messages from us.

With an OIDC-only directory, the mail server does not know a user exists until their
first sign-in, and inbound mail to an unknown address is rejected, hence
pre-creation for those who receive mail *at* the company.

**Acceptance criteria**
1. Adding an administrative-organization membership **through the application**
   provisions a mailbox before that membership becomes effective.
1a. The bootstrap-created administrator's membership is effective immediately; the
   provisioning request is queued and completes on the first successful push, and
   bootstrap does not depend on the mail server.
1b. The `emergency` account never has a mailbox or an email address; no provisioning
   is attempted for it and reconciliation (INT-MAIL-007) does not flag its absence.
1c. Sending a staff invitation creates a disabled mail account; acknowledgement enables
   it; expiry leaves it disabled and reserved, and reconciliation treats a reserved
   mailbox for an open or expired invitation as expected, not as drift.
2. Registering a customer account provisions **no** mailbox.
3. Mail to a newly provisioned staff address is accepted without prior sign-in.
4. Mail-server unavailability does not block customer registration.

---

**INT-MAIL-006a** — The lifecycle push SHALL **disable the mail account** on
suspension or deactivation, and app-password authentication SHALL NOT survive it.

*Source: D-093*

Previously unstated, and two documents disagreed by implication: lifecycle events
including suspension are pushed to the mail server, while the offboarding procedure
asserts that deactivating the account does not revoke the app password. Both cannot hold.

**Resolved: the push disables the account, and app passwords stop working with it.** The
offboarding step to revoke the app password is therefore belt-and-braces — retained,
because a failed push would otherwise leave a former employee reading mail, and
reconciliation compares **enabled state**, not merely existence.

**Acceptance criteria**
1. Suspending an account disables its mailbox within one propagation cycle.
2. App-password authentication fails once the account is disabled.
3. Reconciliation compares enabled state and flags drift.

---

**INT-MAIL-007** — Lifecycle events SHALL carry a **stable idempotency key**.
Reconciliation SHALL run daily, comparing both sides and **flagging drift without
auto-correcting**.

*Source: D-006, D-041*

A failed suspension leaves a person reading mail after offboarding. Silent
auto-correction conceals a broken pipeline.

**Acceptance criteria**
1. Replaying an event produces no duplicate.
2. Reconciliation reports a discrepancy without changing either side.
3. Propagation failures are visible in monitoring.

---

**INT-MAIL-008** — The mail transport SHALL be replaceable. No library dependency
SHALL be on a specific mail server.

*Source: D-022, P-003*

**Acceptance criteria**
1. No provider name appears in a core namespace.
2. A different mail server can be configured without code change.

---

**INT-MAIL-009** — **Mailbox hosting and outbound delivery SHALL be treated as
separate concerns.**

*Source: D-074*

| Concern | Scope | Failure means |
|---|---|---|
| **Mailbox hosting** | Administrative-organization members only | Staff cannot read company mail |
| **Outbound delivery** | Every account, staff and customer | Verification, recovery, sign-in links, notifications all stop |

The specification previously placed both under "the mail server" as though one thing.
They fail independently and affect different populations.

**Acceptance criteria**
1. Outbound delivery is configurable independently of mailbox hosting.
2. Replacing either does not require replacing the other.

---

**INT-MAIL-010** — App passwords SHALL be created, listed and revoked through the mail
server's **own app-password call**, made by the library with a token obtained for the
signed-in person through the library's **first-party OIDC client** for the mail server
(AUTH-OIDC-001, REG-MAIL-002). The library SHALL store **nothing** about an app
password: the generated secret is shown once and the server holds the credential.

*Source: D-146; amends D-006*

The mail server generates the secret and accepts no supplied or pre-hashed value, so
the library can neither choose nor retain one. What the library adds is the gate
(step-up actions `mailcredential:create` and `mailcredential:revoke`), the notice to
the security-notice set and the audit record; the credential itself is the server's
(INT-MAIL-001). A revoked or disabled account (INT-MAIL-006a) ends every app password
with it.

**Acceptance criteria**
1. Creating an app password results in one call to the mail server's app-password
   call carrying the person's token, and no row in the library's schema.
2. Listing returns what the server holds (label, created, expiry where set), never a
   cached copy.
3. Revocation removes the credential at the server; a mail client using it fails on
   its next connection.
4. No secret or hash of an app password appears in the library's database, logs or
   audit records.

---

**INT-MAIL-011** — Outbound mail to an **Apple private relay address** SHALL be
delivered only where the **sending domain is registered** with Apple's private email
relay service; the host SHALL declare its registered sending domains, and
configuration validation SHALL surface, at startup and when the setting changes, a
sending domain that is not declared as registered while Continue with Apple is
enabled.

*Source: D-146; REG-IDENT-008*

A relay address counts as verified by the sign-in (REG-IDENT-008), so it can become
the primary email of an account. Every verification code, sign-in link and security
notice to that account then depends on the relay accepting mail from the sending
domain; an unregistered domain fails silently at the relay, and the person is
unreachable without knowing it.

**Acceptance criteria**
1. With Continue with Apple enabled and the sending domain not declared as registered,
   startup produces a named warning identifying the domain.
2. Changing the sending domain to an undeclared one produces the same warning at the
   point of change.
3. The declaration is configuration and requires no code change.

---

## 3. Payments

**INT-PAY-001** — Card data SHALL NEVER touch this system. Only a provider reference
and an amount SHALL be received and stored.

*Source: D-030, PRIV-SENS-004*

This is what removes the financial-detail sensitive category from our own holdings.

**Acceptance criteria**
1. No schema field holds a card number, expiry, or verification value.
2. No request body accepted by this system contains card data.

---

**INT-PAY-002** — Payment callbacks **SHALL be signature-verified**, and SHALL NOT by
themselves mark an order paid.

*Source: D-070, D-080*

**The provider signs.** Verified against their documentation: callbacks carry an
HMAC-SHA-512-family signature over the payload, signed with the merchant secret. A
newer scheme places the signature, merchant identifier and request timestamp in
headers, signing the concatenation of timestamp and body — which supplies replay
protection natively and is preferred where available.

The provider additionally publishes IP ranges and recommends restricting callbacks to
them, and recommends confirming status against their query API rather than trusting
the callback. All three apply.

**Operational:** a non-2xx response is treated as unacknowledged and retried for 72
hours. The endpoint must therefore acknowledge quickly and process asynchronously, and
must be idempotent.

**Acceptance criteria**
1. An unsigned or mis-signed callback is rejected before parsing.
2. Where the timestamped scheme is available, a replayed callback outside the window
   is rejected.
3. A duplicate notification identifier is processed once.
4. Payment state is confirmed against the provider's query API before the order
   advances.
5. The endpoint returns 2xx within the provider's timeout and processes
   asynchronously.

---

**INT-PAY-003** — The processing location of the payment provider SHALL be recorded
in configuration and reflected in generated records. If processing occurs outside
Egypt, it SHALL be included in the cross-border transfer scope.

*Source: D-023, D-036*

**Acceptance criteria**
1. The hosting location field is populated for this provider.
2. An out-of-Egypt value causes the provider to appear in the cross-border section.

---

## 4. Shipping

### 4.1 Contract

**INT-SHIP-001** — Shipment creation SHALL send exactly the fields in the mapping
below and no others.

| Field | Rule |
|---|---|
| Package description | **Fixed generic string.** Never product names, never a category narrowing to a condition, never derived from the cart. Configurable, but not per order |
| Items count | Permitted |
| Notes | Delivery instructions only. Never order contents |
| Business reference | Opaque internal reference, meaningless outside this system |
| Receiver first name, last name, phone | Permitted |
| **Receiver email** | **Omitted.** Phone suffices for a courier |
| Drop-off address | Permitted |
| Cash on delivery amount | Permitted where applicable |

*Source: D-030, PRIV-MIN-001*

**Acceptance criteria**
1. The description field is unreachable from order line items in code.
2. A test asserts the exact outbound field set, failing if a field is added.
3. No outbound request contains a product name.

---

**INT-SHIP-002** — The provider's product-catalogue integration SHALL NOT be used
for storefront orders.

*Source: D-030*

The provider offers a product catalogue with inventory and search. Linking it to
deliveries would place product identity beside a named person's home address in a
third party's system.

**Acceptance criteria**
1. No call is made to catalogue endpoints from the storefront order path.

---

**INT-SHIP-003** — The status callback SHALL NOT by itself advance order state, and
SHALL trigger verification against the provider's API.

*Source: D-070, D-080*

**The provider does not sign.** Their published collection was searched exhaustively
for a signature header, HMAC reference, signing secret or webhook secret; `webhookUrl`
is the only webhook-related field it contains.

Verification-on-receipt is therefore the primary control, supported by unguessable
references and rate limiting.

*Source: D-030, PRIV-MIN-002*

**Acceptance criteria**
1. A forged callback does not change order state.
2. The callback endpoint is rate-limited.

---

**INT-SHIP-004** — Authentication SHALL use the provider's API key in an
`Authorization` header, held as a rotatable secret per INT-GEN-002.

*Source: verified against the provider's collection*

**Acceptance criteria**
1. The key is not present in any repository file.
2. Rotation requires no deployment.

---

**INT-SHIP-005** — The provider's city, zone and district taxonomy SHALL be cached
locally and refreshed on a schedule.

*Source: D-061*

Zones change. Addresses store identifiers rather than names (IDN-ATTR-005), so a
rename does not orphan them.

**Acceptance criteria**
1. Refresh occurs without human action.
2. Districts flagged as unserved are refused at address entry, not at dispatch.

---

**INT-SHIP-006** — Address free-text lines SHALL be passed through and SHALL receive
the same treatment as everything else sent to the provider.

*Source: D-061, D-030*

The structured part routes the parcel; the free text is what the courier reads
standing in the street. These lines are the field most likely to contain something
unexpected, since people write whatever they think will help — so they are never
logged and never used for anything but the shipment.

**Acceptance criteria**
1. Free-text lines appear in no log entry.
2. Both structured selection and free text are submitted.

---

### 4.2 Accepted residual

The shipping provider knows the sender is a medical supplies company. That implies
"bought something medical," not a specific condition — a materially weaker inference,
unavoidable if we ship at all, and proportionate. Cash-on-delivery amounts cross by
necessity.

*Source: D-030*

---

## 5. SMS

**INT-SMS-001** — SMS SHALL be used for **verification codes**, for **sign-in links**
(`phoneLink`), for **second-step codes** (`phoneCode`), for **security notices** to a
phone in the security-notice set (REG-IDENT-002), for **delivering an
admin-assisted enrolment link** (AUTH-RECOV-002) and for **high-severity alerts** to
the alert destinations (OPS-ALERT-003). The two authentication uses SHALL
exist only where the applicable policy's `loginFactors` enables them; both are off by
default and are **restricted factors** (AUTH-FACT-002, AUTH-FACT-002b). Every send
SHALL be governed by the named restrictions (AUTH-ABUSE-004).

*Source: D-013, D-111, D-146; AUTH-FACT-002*

A verification code proves control of a number and is never a credential
(AUTH-FACT-004). The enrolment link is authorised by a human approver's out-of-band
confirmation on a recorded channel; possession of the phone is the delivery route, not
the proof. `phoneLink` is AAL1 and never a second step; password plus `phoneCode` is
AAL2, never phishing-resistant, and is flagged less secure wherever it is listed
(AUTH-FACT-002).

**Acceptance criteria**
1. No authentication path accepts a verification code, or anything delivered by SMS
   other than a `phoneLink` or `phoneCode` issued for that sign-in, as a credential.
2. With `phoneLink` and `phoneCode` disabled in the applicable policy, no sign-in
   link or second-step code is sent by SMS.
3. An enrolment link is sent by SMS only after an approver has recorded the
   out-of-band confirmation and the reason.
4. A security notice by SMS reaches every phone in the security-notice set and is
   governed by `notification.destination` only (AUTH-ABUSE-004).

---

**INT-SMS-002** — *Retired by D-146. See AUTH-ABUSE-004.*

---

**INT-SMS-003** — Message templates SHALL carry a per-language length budget, tested,
with Arabic binding.

*Source: D-013, D-031, AUTH-ABUSE-005*

Unicode messages are 70 characters for a single message and 67 per part when
concatenated; Latin gets 160 and 153. An Arabic message one character over 70 costs
double.

**Acceptance criteria**
1. A test fails if any rendered template exceeds its language's budget.
2. Coverage spans every template in every supported language.

---

**INT-SMS-004** — Balance SHALL be polled, drain rate alerted on, and sends
hard-stopped below a configured floor.

*Source: D-013, AUTH-ABUSE-006*

**Acceptance criteria**
1. Abnormal drain raises an alert without human monitoring.
2. Below the floor, sends are refused and surfaced.

---

**INT-SMS-005** — The delivery-report callback SHALL follow INT-GEN-003 and SHALL
NOT by itself mark a phone verified. A report indicating **failed delivery** SHALL
release the send from every restriction bucket it counted against (AUTH-ABUSE-004),
and SHALL do nothing else.

*Source: D-013, D-146, AUTH-ABUSE-007*

The gateway calls over plain HTTP with parameters in the query string,
unauthenticated. Releasing a bucket on a failure report is the one state change a
report may cause: it can only make a person less restricted, never verified, and a
forged failure report gains an attacker at most one extra send to a number the
restriction already allows.

**Acceptance criteria**
1. A forged report does not verify a phone.
2. Correlation references are unguessable.
3. A send whose report indicates failure does not count against any restriction
   bucket; a report referencing an unknown or already-settled send changes nothing.

---

**INT-SMS-005a** — Where message content lives is **not specified**. The
specification states invariants only: every message exists in every configured
locale, SMS stays within its language's encoded length budget, and failures surface at
startup rather than at send.

*Source: D-059*

The repository is the expected answer at current scale. Moving content elsewhere
solves for non-engineer editors, of whom there are none.

**Trigger to revisit:** when someone who is not an engineer needs to change a message.

---

**INT-SMS-006** — The SMS provider SHALL be replaceable behind an abstraction.

*Source: P-003, D-022*

**Acceptance criteria**
1. No provider name appears in a core namespace.

---

## 6. Compromised-password screening

**INT-PWD-001** — Screening SHALL send only a hash prefix. The full hash and the
password SHALL NEVER leave the system.

*Source: D-011*

**Acceptance criteria**
1. Outbound requests contain a prefix only.
2. A test asserts the request contains no full hash.

---

**INT-PWD-002** — Screening failure SHALL fail loudly with fallback to the offline
list, never a silent skip.

*Source: D-011, AUTH-PASS-004*

**Acceptance criteria**
1. With the service unreachable, the fallback runs and the degradation is logged.
2. With both unavailable, the operation fails rather than accepting unscreened.

---

**INT-PWD-003** — A self-hosted corpus SHALL be available as configuration.

*Source: D-011, D-023*

Retained so the integration can be brought in-house if required.

**Acceptance criteria**
1. Switching to the self-hosted corpus requires configuration only.

---

**INT-PWD-004** — This call SHALL appear in generated records as a cross-border
transfer with destination and basis.

*Source: D-041, PRIV-ROPA-003*

**Acceptance criteria**
1. It appears with a stated basis.

---

## 7. Hosting

**INT-HOST-001** — The hosting location SHALL be recorded in configuration and
reflected in generated records as **inside** or **outside Egypt**.

*Source: D-023, D-036*

**Acceptance criteria**
1. The field is populated and appears in output.
2. An outside-Egypt value causes the cross-border basis field to be required.

---

**INT-HOST-002** — Where hosting is outside Egypt, the cross-border transfer SHALL
rest on the regulator's permit, **not on consent**.

*Source: D-023, PRIV-CONS-010*

A consent withdrawal would otherwise leave data that cannot lawfully be hosted.

**Acceptance criteria**
1. No consent record references hosting as its purpose.
2. Withdrawing any consent never renders hosting unlawful.

---

## 8. Provider register

| Provider | Role | Data received | Location field | Callback |
|---|---|---|---|---|
| Mail server | Processor | Mailbox contents, account identifiers | Follows hosting | — |
| Payment provider | Processor | Payment details, amount, order reference | Configured | Yes |
| Shipping provider | Processor | Name, phone, address, generic description, amount | Configured | Yes |
| SMS gateway | Processor | Phone number, message text | Configured | Yes |
| Hosting provider | Processor | All stored data | Configured | — |
| Password screening | Recipient | Hash prefix only | Outside Egypt | — |
| Developer | Processor | All stored data | — | — |

*Source: D-036, D-029, D-041*

Each requires an agreement reference in generated records. The register is a
rendering of configuration, not a separately maintained list.

---

## 9. Open items

None. Cross-references: consent and minimisation policy in `04-privacy`; abuse
control behaviour in `02-authentication`; secrets handling and configuration
taxonomy in `06-operations`.
