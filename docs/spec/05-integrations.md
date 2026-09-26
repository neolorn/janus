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

The endpoints the library itself calls are `10` keys: `integration.mail.endpoint` and
`integration.sms.endpoint` (the shipped transports), `integration.mailserver.endpoint`
(the shipped mail-server adapter, INT-MAIL-001) and
`password.blocklist.selfhosted.address` (the self-hosted corpus, INT-PWD-003). Each is
an absolute `https` address; startup refuses any other value with
`integration.endpoint.insecure`, and a change of a protected one from the server is
refused alike (OPS-CFG-004). A host's own integrations are the host's, and the library
holds no register of them (D-162).

*Source: P-003, D-162, D-166*

This is not theoretical. Providers publish sample configuration that specifies a plain
`http://` base URL, which would send names, addresses and phone numbers unencrypted.
Rejecting it at validation is what prevents such a value being copied into
configuration unexamined.

**Acceptance criteria**
1. A base URL with scheme `http` fails startup with a named error.
2. The error is `integration.endpoint.insecure`, and `details.key` names the setting,
   whose name names the integration.

---

**INT-GEN-002** — Provider credentials SHALL be held as rotatable secrets in the
secrets manager and read through the host's secret source (`ISecretSource`,
LIB-EXT-001, INF-HOST-003), never from configuration files or environment variables in
production.

**Values (D-166).** The provider credentials the library reads are the mail server's
management key, where the shipped adapter is used (INT-MAIL-001), and each declared
social provider's credential, read by the provider's name (IDN-LIFE-012). They are read
in the startup hosted service, before the server serves; one that cannot be read stops
startup with `model.startup.secretunavailable`, `details.key` `mailServerSecret` or
`socialProvider.<provider>`. No provider credential is an argument of `AddJanus`.

*Source: D-026.3, D-041, D-166*

**Acceptance criteria**
1. No provider credential appears in any repository file.
2. Rotating a credential requires no code change.
3. A deployment whose secret source cannot supply a provider credential it needs does
   not start, and the refusal names the credential.

---

**INT-GEN-003** — Every inbound callback SHALL be treated as hostile input.

**Where the provider supports signing**, signature verification per BFF-MACH-002 is
the primary control — the provider's published algorithm, constant-time comparison,
a five-minute timestamp
window, raw bytes, idempotency on the event identifier.

**Where it does not**, the callback is a **hint that triggers verification against the
provider's API**, never a fact — supported by unguessable references, rate limiting,
and source restriction where ranges are published.

An unsigned callback SHALL NEVER by itself advance an authoritative state; a verified,
signed provider security event acts as IDN-LIFE-012a states.

**The pipeline is the library's; the callbacks are mostly the host's.** The library
ships these controls as a host-mountable machine-profile pipeline (BFF-MACH-002,
BFF-MACH-003): signature verification, correlation references, the rate limit and
source restriction. The callback endpoints the library defines itself are the SMS
delivery report (INT-SMS-005) and the social providers' security events
(IDN-LIFE-012a), both in `09` section 10. A host mounts each of its own
providers' callbacks on the same pipeline and owns what state, if any, they may
influence.

**Values (D-153, D-166).** The callback endpoints accept `integration.callback.ratelimit`
requests per source (AUTH-ABUSE-001) per minute, fixed window, and answer 429
`integration.callback.rejected` with `Retry-After` before any lookup. Every other
refusal of a callback is 422 `integration.callback.rejected`, with no `Retry-After`;
the route of a provider whose events follow RFC 8935 answers as `09` section 10 states.
A correlation reference is 128 random bits, base64url, held only as its SHA-256 and
looked up by it (BFF-MACH-003), and never logged (CONV-LOG-003). A signing secret
rotates with a 24 hour overlap (BFF-MACH-002).

*Source: D-070*

*Source: D-013, D-030, D-164, D-166*

Callbacks arrive unauthenticated or weakly authenticated. A forged one must be
unable to mark a phone verified, or to advance any state the host hangs on a callback.

**Acceptance criteria**
1. A callback with a guessed reference is rejected.
2. An unsigned callback advances state only after the system confirms independently,
   or is treated as a hint requiring corroboration.
3. Rejected callbacks are logged with their source.
4. A callback past `integration.callback.ratelimit` is answered 429 with `Retry-After`
   before any lookup; a callback refused for any other cause is answered 422 with no
   `Retry-After`, except where `09` section 10 gives a provider's route an answer of its
   own.

---

**INT-GEN-004** — Every provider SHALL appear in generated records of processing
with its legal characterisation and agreement reference.

*Source: D-036, PRIV-ROPA-002, D-166*

**Acceptance criteria**
1. Adding a provider without an agreement reference is flagged with the register
   finding `agreement-missing`.

---

**INT-GEN-005** — Outbound payloads SHALL be constructed by a dedicated mapping
layer, never assembled ad hoc at call sites.

*Source: D-030*

The data categories declared for each recipient (PRIV-ROPA-002) are only enforceable
if there is exactly one place a payload is built; a payload carries nothing outside
its recipient's declared categories.

**Acceptance criteria**
1. A test asserts the exact field set of each outbound payload.
2. No call site constructs a provider request directly.

---

**INT-GEN-006** — The city-level location shown on a session (AUTH-SESS-013) SHALL be
resolved from a **local IP-to-city database**: a file the host supplies
(`ILocationSource`, LIB-HOST-001), read in process, **never a call to a third party**.
The file SHALL be refreshed on a schedule as a background job (INF-BG-001), and a stale
or missing file SHALL degrade to no location, never to an external lookup.

**Values (D-153, D-162, D-166).** The database is refreshed every
`location.database.refresh`; a file older than `location.database.maxage`, or no file
at all, is stale: no location is shown and the `degradation` condition is raised. The
file comes from the host through `ILocationSource` (optional) in the format that
interface documents: UTF-8 text, a first line `# YYYY-MM-DD` giving the date the data
was produced, then one tab-separated range per line (first address, last address,
ISO 3166-1 alpha-2 country or empty, city or empty, and the city's latitude and
longitude exactly where a city is given). A file with no date, an unreadable line, or
ranges of mixed family, reversed or overlapping, is refused whole. Its age is judged
from its own date. A process reads it at its first resolution and on each refresh; a
failed refresh keeps the copy held until it is stale. The address resolved is the whole
address the session records, never a counting key derived from it (AUTH-SESS-013,
AUTH-ABUSE-001).

*Source: D-146, D-162, D-166; AUTH-SESS-013*

A network lookup would hand every sign-in address to whoever runs the lookup service.
The database is a data file, refreshed like any other, and its age is a degradation to
surface (OPS-OBS-002), not a reason to look elsewhere.

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

*Source: D-006, D-146, D-166*

**Restated** (D-146): every management operation happens through JMAP objects, so
"management API" means JMAP and nothing else. The four concerns are unchanged. Should the edition change,
SCIM v2 is the alternative provisioning push for the account lifecycle; it carries no
credential and would replace the provisioning row only.

**Values (D-166).** The library ships a JMAP adapter for the mail server in
`Janus.Hosting`. It is used where `integration.mailserver.endpoint` is set and the host
registers no `IMailServer` of its own (LIB-HOST-001); a host's registration replaces
it. It speaks JMAP (RFC 8620) at `<integration.mailserver.endpoint>/jmap` with the
capabilities `urn:ietf:params:jmap:core` and `urn:stalwart:jmap`, presenting the mail
server's management key, which the library reads through the host's secret source
(INT-GEN-002). A status other than 2xx, a timeout, an answer that does not read, a
method error, or any entry the server reports as not created, not updated or not
destroyed is a failure; nothing is read as success by default. A mailbox is an
`Account` of `@type` `User` named by the address's local part in the `Domain` of its
domain, and is created with the library's identifier of the mailbox as its
`description`. `disabled` is the account with the `authenticate` permission disabled,
which still receives mail; `enabled` is the account with it not disabled; `removed` is
the account destroyed (`x:Account/set`). A create that meets an account the server
already holds under that name adopts it only where its `description` carries the
mailbox's identifier; otherwise the push fails and raises `degradation` at once,
naming the mailbox by its identifier and never by its address, without waiting for
`outbox.retry.maxattempts`, since retrying cannot resolve the conflict. The listing is
`x:Account/query` with `x:Account/get` of `emailAddress` and `permissions`. App
passwords are `AppPassword` objects (`x:AppPassword/get`, `x:AppPassword/set`) called
with the person's token (INT-MAIL-010) and never with the management key.

**Acceptance criteria**
1. No code reads Stalwart's database.
2. No app-password credential type exists in this system's schema.
3. Every provisioning and app-password operation is a JMAP request; no other
   management interface of the mail server is called.
4. A create that meets a server account whose `description` does not carry the
   mailbox's identifier adopts nothing, changes nothing at the server, and raises
   `degradation` on its first attempt; a push the library replays converges on the
   account it created and creates nothing twice.

---

**INT-MAIL-002** — Stalwart's storage SHALL NOT be co-located with business data, and
**SHALL be backed up** (DR-013).

*Source: D-006, D-085*

Separation avoids coupling to a pre-1.0 schema and keeps mail out of the database
holding the subjects' personal data. It also means the application's continuous archiving
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
Stalwart's use. Token introspection SHALL NOT be provided. The mail server SHALL be
configured to require its own client identifier as the audience of every access token
it accepts (Stalwart `requireAudience`), reading the issuer and keys from discovery.

*Source: D-005, D-041, D-164, D-166*

Stalwart validates offline via JWKS and falls back to userinfo; it does not use
introspection with client credentials. The `aud` check is the mail server's own: the
library's adapter presents tokens and verifies none (AUTH-OIDC-006).

**Acceptance criteria**
1. Stalwart authenticates successfully with introspection absent.
2. The discovery document does not advertise an introspection endpoint.
3. An access token issued to another client is refused by the mail server.

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

A deployment integrates a mail server where one is registered, the shipped adapter or
the host's own (INT-MAIL-001), and only for the administrative organization. A mailbox
SHALL be owed `enabled` only while its holder's account is `active` or `restricted` and
holds a current membership of the administrative organization, and `disabled`
otherwise; a membership of any other organization does not count. A restriction the
person asked for does not cut them off from their mail or end their app passwords
(INT-MAIL-010). An invitation whose corporate address has a mailbox held before, by
anyone, the invitee included, SHALL be refused unless the administrator names what
becomes of it (`formerMailbox`: `transfer` gives the invitee the mailbox and its mail,
`replace` removes it and reserves a new one; REG-MAIL-003); no mailbox passes to a
holder without that choice, and the issue checks nothing about who the invitee is.

*Source: D-148, D-166; D-006, D-074, D-144, D-146*

**A mailbox is for a person.** The reserved `emergency` account (OPS-BOOT-002) is a
member of the administrative organization but is not a person (it is a role a human
steps into with the sealed credential) and it holds **no email address and no
mailbox**. Nothing is ever addressed to it; every message about a break-glass session
goes to the owner's and operator's recorded destinations (D-129). This is a
definition, not an exemption: an unread mailbox on the most privileged account would
be a phishing target and a place for a warning to be missed.

**The first administrator is a person, created before the mail server may exist.**
Bootstrap (OPS-BOOT-001) runs against the database on a fresh host, and the membership
is effective at once. Where the deployment integrates the mail server, bootstrap is
given the administrator's corporate address (`--mailbox`) and reserves the mailbox at
it. The mailbox row is its own retried outbox: the state each mailbox is owed is read
on every publisher run (`outbox.poll.interval`) from its holder's account state and
memberships, and a state that differs from the one the server last confirmed is pushed
under `outbox.retry.*`, so the mailbox is created the moment the mail server is
reachable. Until then the administrator is reached at the personal address given at
bootstrap; the enrolment link itself is printed by the command and sent nowhere
(OPS-BOOT-001).

**Scope is deliberate and was previously missing.** The mail server hosts the
company's own mail: staff mailboxes. **Customers are not provisioned a mailbox**;
they hold their own email elsewhere and only receive messages from us.

With an OIDC-only directory, the mail server does not know a user exists until their
first sign-in, and inbound mail to an unknown address is rejected, hence
pre-creation for those who receive mail *at* the company.

**Acceptance criteria**
1. Adding an administrative-organization membership **through the application**
   provisions a mailbox before that membership becomes effective.
1a. The bootstrap-created administrator's membership is effective immediately. Where
   bootstrap is given `--mailbox`, the mailbox is reserved and created on the first
   successful push; without it none is reserved. Bootstrap does not depend on the mail
   server.
1b. The `emergency` account never has a mailbox or an email address; no provisioning
   is attempted for it and reconciliation (INT-MAIL-007) does not flag its absence.
1c. Sending a staff invitation creates a disabled mail account; acknowledgement enables
   it; expiry leaves it disabled and reserved, and reconciliation treats a reserved
   mailbox for an open or expired invitation as expected, not as drift.
2. Registering a customer account provisions **no** mailbox.
3. Mail to a newly provisioned staff address is accepted without prior sign-in.
4. Mail-server unavailability does not block customer registration.
5. A holder whose account is neither `active` nor `restricted`, or whose membership of
   the administrative organization has ended, is owed a `disabled` mailbox; a
   `restricted` holder with a current membership is owed `enabled`; a membership of any
   other organization enables no mailbox.
6. An invitation of an address whose mailbox was held before, by the invitee or by
   anyone else, without `formerMailbox` is refused with
   `identity.invitation.mailboxheld` and changes no mailbox.

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
1. Suspending an account disables its mailbox within one propagation cycle: the
   disable request is published on the first outbox publisher run
   (`outbox.poll.interval`) after the suspension commits.
2. App-password authentication fails once the account is disabled.
3. Reconciliation compares enabled state and flags drift.

---

**INT-MAIL-007** — Mail provisioning SHALL follow account state, not events: every
push of the state a mailbox is owed SHALL carry a **stable idempotency key**.
Reconciliation SHALL run daily, comparing both sides and **flagging drift without
auto-correcting**.

A push SHALL be recorded with its key, and committed, before it is sent. A retry of the
same state SHALL carry the same key until the server confirms it. A change of the state
owed while a push is outstanding, a return to the state last confirmed included, SHALL
begin a push under a new key. A push whose attempts reach `outbox.retry.maxattempts`
SHALL be marked failed and SHALL raise `degradation` in the same transaction, scope
`mailbox.push:<mailbox id>`, details `{ mailbox, state, attempts }`, naming the mailbox
by its identifier and never by its address; it SHALL NOT be attempted again until the
state owed changes.

Reconciliation SHALL compare every mailbox the library holds, one with a push
outstanding included, with the server's listing: owed `enabled`, listed enabled; owed
`disabled` (a reservation included), listed disabled; owed `removed`, absent. It SHALL
count every address the server lists that the library holds no mailbox for. Addresses
SHALL be compared in their canonical form (IDN-ACCT-004), the server's listed addresses
canonicalised before the comparison, and a listed address that does not read counts as
one the library does not hold. A difference SHALL raise `degradation`, scope
`mailbox.reconciliation`, details `{ mailboxes, unknown }` (mailbox identifiers and a
count, never an address); a listing that cannot be had SHALL raise it with
`{ listed: false }`. Nothing SHALL be changed on either side.

*Source: D-006, D-041, D-166*

A failed suspension leaves a person reading mail after offboarding. Silent
auto-correction conceals a broken pipeline.

**Acceptance criteria**
1. Replaying a push produces no duplicate.
2. Reconciliation reports a discrepancy without changing either side.
3. Propagation failures are visible in monitoring.
4. A push the server applied whose answer was lost, followed by a change of the state
   owed, ends with the server in the last state owed.

---

**INT-MAIL-008** — The mail transport SHALL be replaceable. No library dependency
SHALL be on a specific mail server. A deployment SHALL have a mail transport: its own
(`IMailTransport`) or the shipped default at `integration.mail.endpoint` (LIB-EXT-001).
Startup SHALL refuse a deployment that has none, with
`model.startup.declarationmissing`, `details.key` `mailTransport`.

*Source: D-022, P-003, D-166*

**Acceptance criteria**
1. No provider name appears in a core namespace.
2. A different mail server can be configured without code change.
3. A deployment with no mail transport does not start, and the refusal carries
   `details.key` `mailTransport`.

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

The token SHALL be issued in process, by the provider's own token generation, from the
signed-in person's session record to the client the deployment declares as the mail
server's (`MailServerClient`, LIB-HOST-001); it SHALL name that client as `aud`
(AUTH-OIDC-006), last no longer than `oidc.accesstoken.lifetime` or the session
record, and SHALL NOT be stored.

*Source: D-146, D-164, D-166; amends D-006*

The mail server generates the secret and accepts no supplied or pre-hashed value, so
the library can neither choose nor retain one. What the library adds is the gate
(step-up actions `mailcredential:create` and `mailcredential:revoke`), the notice to
the security-notice set and the audit record; the credential itself is the server's
(INT-MAIL-001). A revoked or disabled account (INT-MAIL-006a) ends every app password
with it. Where the deployment registers no mail server, or the account holds no mailbox
the mail server is told to enable, the app-password operations answer
`identity.mailbox.notfound`. A restricted account keeps its enabled mailbox
(INT-MAIL-006): it lists and revokes its app passwords and creates none
(`authz.restricted`, IDN-ACCT-007). `09` section 6 gives every answer.

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

**Values (D-153, D-166).** The sending domain is `notification.email.sendingdomain` (required);
the domains registered with the relay are `notification.email.relayregistered`. When
`apple` is in any effective `loginFactors` and the sending domain is not in that set,
startup raises the Normal condition `relay-domain-unregistered` with `details.domain`.
The condition is judged at startup and on every change of
`notification.email.sendingdomain`, `notification.email.relayregistered` or
`policy.default`; `apple` is in an effective `loginFactors` exactly when it is in the
system policy's, which every organization's narrows (AUTH-STEP-002a). A domain matches
without regard to ASCII case. The condition stops neither a start nor a change; a start
or change whose condition cannot be raised is refused.

*Source: D-146, D-166; REG-IDENT-008*

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
4. Withdrawing the sending domain from `notification.email.relayregistered`, or adding
   `apple` to `policy.default`, while the sending domain is undeclared produces the same
   warning at the point of change.

---

## 3. SMS

**INT-SMS-001** — SMS SHALL be used for **verification codes**, for **sign-in links**
(`phoneLink`), for **second-step codes** (`phoneCode`), for **security notices** and
**recovery links** to a phone in the security-notice set (REG-IDENT-002,
AUTH-RECOV-005), for **delivering an admin-assisted enrolment link** (AUTH-RECOV-002)
and for **high-severity alerts** to the alert destinations (OPS-ALERT-003). The two
authentication uses SHALL exist only where the applicable policy's `loginFactors`
enables them; both are off by default and are **restricted factors** (AUTH-FACT-002,
AUTH-FACT-002b). Where the phone signal answers `risk` for a number, no SMS factor and
no recovery link is sent to it (AUTH-FACT-002b). Every send other than an alert SHALL
be governed by the named restrictions whose channel is `sms` or `any`
(AUTH-ABUSE-004); an alert answers to the deduplication of OPS-ALERT-002 alone, so
that no limit an attacker can exhaust silences one.

*Source: D-013, D-111, D-146, D-166; AUTH-FACT-002*

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
   counted and refused only by restrictions whose purpose is `notification`
   (AUTH-ABUSE-004).
5. An alert by SMS is counted and refused by no restriction.

---

**INT-SMS-002** — *Retired by D-146. See AUTH-ABUSE-004.*

---

**INT-SMS-003** — Message templates SHALL carry a per-language length budget, tested,
with Arabic binding.

**Values (D-162, D-166).** At startup every template in every language of
`notification.languages` SHALL be rendered with each place the library fills at its
defined width (the message places of `10`), and a text message whose rendering exceeds
its budget SHALL stop startup. A text message that carries a link is budgeted at two
segments of its alphabet: 306 characters of the GSM 7-bit default alphabet, 134
otherwise. Every other text message is budgeted at one: 160 and 70. `{link}` is
measured at the width of the address it becomes, `<origin>/link#<kind>.<token>` with
the origin the host declares for the link's application (`LandingOrigins`,
`Authentication` or `Account`, LIB-HOST-001) and a drawn token; one token
size serves every channel. Nothing is measured at a send. A text-message template that
names a place `10` gives no width SHALL be refused at startup with
`model.startup.declarationinvalid`. A value a deployment or host chooses for a place (a
restriction's name, a governing document's name, a subject-event subscriber's name)
SHALL be 1 to 64 characters of lower-case letters and digits separated by single `.`,
`-` or `_`: a restriction's name outside the rule is refused where it is written with
`config.value.notallowed`, and a document or subscriber declared under a name outside
it is refused at startup with `model.startup.declarationinvalid`, `details.declaration`
and `details.field` naming it. Bounding these values where they are written keeps every
rendered template within the width it was measured at.

*Source: D-013, D-031, AUTH-ABUSE-005, D-162, D-166*

Unicode messages are 70 characters for a single message and 67 per part when
concatenated; Latin gets 160 and 153. An Arabic message one character over 70 costs
double.

**Acceptance criteria**
1. A test fails if any rendered template exceeds its language's budget.
2. Coverage spans every template in every supported language.
3. A restriction, governing document or subscriber whose name breaks the rule is
   refused where it is written or declared; the `key` place is measured at the widest
   key including each family's widest parameter, and `outstanding` at the joined width
   of the registered required subscribers' names.
4. A text message carrying `{link}` that fits two segments of its alphabet at the
   width of its link is accepted, one that does not stops startup, and a text-message
   template naming a place with no defined width stops startup with
   `model.startup.declarationinvalid`.

---

**INT-SMS-004** — Balance SHALL be polled, drain rate alerted on, and sends
hard-stopped below a configured floor.

**Values (D-153, D-166).** `abuse.sms.balancefloor` is a decimal in the currency the gateway
reports. The balance is read every `abuse.sms.pollinterval`; the `sms-balance` alert
fires when the last hour's spend exceeds `abuse.sms.drainfactor` times the trailing
seven-day hourly mean, or when the balance would reach the floor within 24 hours at the
current rate. A poll that cannot read the balance, no SMS transport being registered
included, fails, and its lapse raises `background-job-failed` (INF-BG-001); no poll
succeeds without a balance read.

*Source: D-013, AUTH-ABUSE-006, D-166*

**Acceptance criteria**
1. Abnormal drain raises an alert without human monitoring.
2. Below the floor, sends are refused and surfaced.

---

**INT-SMS-005** — The delivery-report callback SHALL follow INT-GEN-003 and SHALL
NOT by itself mark a phone verified. A report indicating **failed delivery** SHALL
release the send from every restriction bucket it counted against (AUTH-ABUSE-004),
and SHALL do nothing else.

*Source: D-013, D-146, AUTH-ABUSE-007, D-166*

The gateway calls over plain HTTP with parameters in the query string,
unauthenticated. The deployment's SMS transport (`ISmsTransport`, INT-SMS-006) reads
the report from the gateway's own query parameters, each name taken once; a report it
cannot read is rejected as one carrying a guessed reference is. A report of anything
other than a final failure, an intermediate state included, is read as not failed and
changes nothing. Releasing a bucket on a failure report is the one state change a
report may cause: it can only make a person less restricted, never verified, and a
forged failure report gains an attacker at most one extra send to a number the
restriction already allows.

**Acceptance criteria**
1. A forged report does not verify a phone.
2. Correlation references are unguessable.
3. A send whose report indicates failure does not count against any restriction
   bucket. A report of delivery changes nothing. A report of either kind whose
   reference no held send answers changes nothing and is rejected, recorded against its
   source and counted toward `alerting.callback.threshold` (INT-GEN-003 AC1).

---

**INT-SMS-005a** — Where message content lives is **not specified**. The
specification states invariants only: every message exists in every configured
locale, SMS stays within its language's encoded length budget, and failures surface at
startup rather than at send. The library ships a default catalogue in `Janus.Hosting`
(LIB-EXT-001), in English and Arabic, which a host may replace; its texts carry every
link as `{link}` (INT-SMS-003).

*Source: D-059, D-162, D-166*

The repository is the expected answer at current scale. Moving content elsewhere
solves for non-engineer editors, of whom there are none.

**Trigger to revisit:** when someone who is not an engineer needs to change a message.

---

**INT-SMS-006** — The SMS provider SHALL be replaceable behind an abstraction. Every
deployment SHALL have an SMS transport, since every deployment sends SMS alerts
(OPS-ALERT-003, LIB-HOST-001): its own (`ISmsTransport`) or the shipped default at
`integration.sms.endpoint` (LIB-EXT-001). Startup SHALL refuse a deployment that has
none, with `model.startup.declarationmissing`, `details.key` `smsTransport`.

*Source: P-003, D-022, D-166*

**Acceptance criteria**
1. No provider name appears in a core namespace.
2. A deployment with no SMS transport does not start, and the refusal carries
   `details.key` `smsTransport`.

---

## 4. Compromised-password screening

**INT-PWD-001** — Screening SHALL send only a hash prefix. The full hash and the
password SHALL NEVER leave the system.

**Values (D-153, D-166).** The prefix is the first 5 upper-case hexadecimal characters of the
SHA-1 of the UTF-8 password; the remaining 35 are matched locally against the range
returned (D-041). Every range request SHALL send a user agent naming the library and its
version, since the provider's acceptable use asks callers to identify themselves.

*Source: D-011, D-166*

**Acceptance criteria**
1. Outbound requests contain a prefix only.
2. A test asserts the request contains no full hash.
3. A test asserts every range request carries a user agent naming the library and its
   version.

---

**INT-PWD-002** — Screening failure SHALL fail loudly with fallback to the offline
list, never a silent skip.

*Source: D-011, AUTH-PASS-004, D-166*

**Acceptance criteria**
1. With the service unreachable, the fallback runs and the degradation is logged and
   raised as `degradation` under `password.blocklist.fallback` (OPS-OBS-002).
2. With both unavailable, the operation fails rather than accepting unscreened.

---

**INT-PWD-003** — A self-hosted corpus SHALL be available as configuration.

**Values (D-162, D-166).** The self-hosted corpus answers the range protocol of INT-PWD-001
at `password.blocklist.selfhosted.address`, an absolute `https` address (INT-GEN-001).
With `password.blocklist.source` `selfHosted` and that key unset, startup fails with
`model.startup.declarationmissing` naming it; a value written at runtime that is not an
absolute `https` address is never called, and the screening that reads it is refused
with `auth.screening.unavailable`. Where the corpus cannot answer, the package's list
answers and the degradation is raised (INT-PWD-002).

*Source: D-011, D-023, D-162, D-166*

Retained so the integration can be brought in-house if required.

**Acceptance criteria**
1. Switching to the self-hosted corpus requires configuration only.
2. With `password.blocklist.source` `selfHosted`, the prefix goes to
   `password.blocklist.selfhosted.address` and nowhere else; with that key unset,
   startup fails naming it.

---

**INT-PWD-004** — This call SHALL appear in generated records as a cross-border
transfer with destination and basis.

*Source: D-041, PRIV-ROPA-003*

**Acceptance criteria**
1. It appears with a stated basis.

---

## 5. Hosting

**INT-HOST-001** — The hosting location SHALL be recorded in configuration and
reflected in generated records as **inside** or **outside Egypt**.

**Values (D-162, D-166).** The location is `hosting.location`, protected and required,
taking `inside` or `outside` (Egypt); the same two values are the location of every
recipient the records of processing list (PRIV-ROPA-002). With `outside`,
`hosting.crossborderbasis` is required.

*Source: D-023, D-036, D-162, D-166*

**Acceptance criteria**
1. The field is populated and appears in output.
2. An outside-Egypt value causes the cross-border basis field to be required.

---

**INT-HOST-002** — Where hosting is outside Egypt, the cross-border transfer SHALL
rest on the regulator's permit, **not on consent**.

**Values (D-166).** Startup SHALL refuse a purpose that rests on consent and is named
`hosting`, `transfer`, `hosting-transfer` or `cross-border-transfer`, compared ignoring
case and every character other than a letter or a digit, with
`model.purpose.hostingconsent`, `details.key` naming `<type>.<purpose>`; the same
purpose on another basis is accepted.

*Source: D-023, PRIV-CONS-010, D-166*

A consent withdrawal would otherwise leave data that cannot lawfully be hosted.

**Acceptance criteria**
1. No consent record references hosting as its purpose.
2. After any consent is withdrawn, the generated records state the same cross-border
   basis for the hosting provider and for every recipient outside Egypt, and add no
   finding.
3. A consent-based purpose named for the hosting or its transfer fails startup with
   `model.purpose.hostingconsent`.

---

## 6. Provider register

| Provider | Role | Data received | Location field | Callback |
|---|---|---|---|---|
| Mail server | Processor | Email address and text of each message it delivers; mailbox contents and account identifiers where it hosts mailboxes | Follows hosting | No |
| SMS gateway | Processor | Phone number, message text | Configured | Yes |
| Hosting provider | Processor | All stored data | Configured | No |
| Password screening | Recipient | Hash prefix only | Outside Egypt | No |

*Source: D-036, D-029, D-041, D-162 C.103, D-165, D-166*

Each requires an agreement reference in generated records. The register is a
rendering of configuration, not a separately maintained list. These four rows are the
processors the library itself makes true and are shipped as defaults (D-162 C.103,
D-165). The hosting provider and SMS gateway rows are applied to every deployment:
every deployment holds data, and every deployment declares SMS alert destinations
(LIB-HOST-001, OPS-ALERT-004). The mail server row is applied where a mail server is
integrated (an `IMailServer` registered, or `integration.mailserver.endpoint` set) or
the shipped mail transport is in use (`integration.mail.endpoint` set); the
password-screening row while online screening is configured. Every other processor or
recipient is a row the host declares through `recipients` (PRIV-ROPA-002); the library
ships no such row.

A person or firm that administers the deployment for the controller (D-029) is one such
row. It stays here as an example of a row a host declares, not as a shipped default:

| Provider | Role | Data received | Location field | Callback |
|---|---|---|---|---|
| Developer | Processor | All stored data | Declared | No |

---

## 7. Open items

None. Cross-references: consent and minimisation policy in `04-privacy`; abuse
control behaviour in `02-authentication`; secrets handling and configuration
taxonomy in `06-operations`.
