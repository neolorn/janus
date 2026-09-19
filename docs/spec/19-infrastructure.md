# 19 — Infrastructure Requirements

What the hosting environment must provide for these systems to work correctly.

**Prerequisite:** `06-operations.md`, `12-disaster-recovery.md`.

**Scope — read this first.** This document states **requirements the environment must
satisfy**. It does not specify how to satisfy them.

The test for inclusion is the same as elsewhere: *would getting this wrong break
security, compliance, or correctness?* Container counts, reverse proxy choice, network
segmentation, volume layout and process supervision are **not** in scope. Those belong
to whoever runs the infrastructure.

Where a requirement constrains an infrastructure choice, it says so and stops there.

---

## 1. Domain

**INF-DOM-001** — Every application sharing authentication SHALL be served from
subdomains of a **single registrable parent domain**.

*Source: D-004, D-063*

Passkeys are bound to a domain at enrolment. An application on a different registrable
domain cannot use a credential enrolled elsewhere — the person would have to enrol
twice, and existing credentials would not transfer.

**Confirmed:** all applications share one domain.

**Acceptance criteria**
1. No application sharing authentication is served from a different registrable
   domain.
2. The relying party identifier is the parent domain, not a subdomain, so credentials
   work across all applications and remain usable by a future native client.

---

**INF-DOM-002** — Adding an application under the shared domain SHALL require no
credential re-enrolment.

*Source: D-004*

**Acceptance criteria**
1. A newly added subdomain accepts existing passkeys without user action.

---

## 2. Transport security

**INF-TLS-001** — Every origin the applications serve SHALL present a valid
certificate, and its coverage SHALL match the domain the **relying party identifier**
is set to (INF-DOM-001).

*Source: D-063, D-072*

Certificates are otherwise a property of the deployment rather than of these systems —
they protect every request, and would be required with authentication removed
entirely. **The coverage match is the one dependency these systems do have:** a
mismatch causes enrolled passkeys to stop working on an application, silently.

**Acceptance criteria**
1. No origin serves plaintext or an invalid certificate.
2. Certificate coverage is verified as part of deployment, not assumed.
3. Coverage includes every origin under the relying party identifier's domain.
4. A coverage mismatch blocks the deploy rather than surfacing as failed
   authentications.

---

**INF-TLS-002** — Certificate renewal SHALL NOT depend on a person being available.
**Automated issuance is a launch requirement**, not a later migration.

*Source: D-063, D-072, P-003*

An expired certificate is a total outage — every application at once, with a browser
security warning that customers reasonably read as compromise. It is the classic
avoidable failure for a small operation, because renewal is exactly the task that gets
remembered until it isn't.

Certificate lifetimes are also shortening on a published schedule, so the manual
burden grows over time rather than staying constant.

**No interim manual path is specified.** Nothing is live at the time of writing, so
there is no migration from an existing manually-issued certificate.

**Acceptance criteria**
1. No recurring calendar entry exists for certificate renewal.
2. Renewal proceeds without human intervention.

---

**INF-TLS-003** — Certificate expiry SHALL be monitored **independently of whatever
performs renewal**, and renewal failure SHALL alert through the D-048 channels.

*Source: D-063*

These are two different checks catching two different failures. Expiry monitoring sees
the consequence, weeks late. Renewal-failure monitoring sees the cause, the same day.
Neither substitutes for the other, and **they must not share a component** — asking a
broken renewer whether renewal is working returns a confident wrong answer.

**Acceptance criteria**
1. Expiry is determined from the **served certificate**, not from renewal state.
2. A renewal failure raises an alert without anyone checking.
3. The two checks share no component.

---

**INF-TLS-004** — Outbound integrations SHALL use TLS, with plaintext endpoints
rejected at startup.

**One named exception, recorded rather than discovered:** the SMS gateway's delivery
report arrives over plain HTTP with parameters in the query string. Confirm whether the
gateway supports an HTTPS callback URL; **if it does, use it and this exception
lapses.** This confirmation is the set's one open item (R-A12, §11), closed when the
gateway integration is built. If it does not, that single path is exempt from INF-TLS-001, and the
compensating controls are the unguessable reference, rate limiting, and the fact that
the callback advances no authoritative state (BFF-MACH-003).

The report carries a delivery status against an opaque reference and no personal data.

*Source: D-092*

*Source: INT-GEN-001*

Already required; restated here because it is an environment property. At least one
provider's published configuration specifies a plaintext base URL, which would send
customer names, phone numbers and addresses unencrypted if copied unexamined.

**Acceptance criteria**
1. A configured endpoint with a plaintext scheme fails startup with a named error.

---

## 3. Host

**INF-HOST-004** — Hosting is **outside Egypt**. The hosting-location field in
generated records reads accordingly, and the cross-border basis field is required.

*Source: D-081, D-023*

---

**INF-HOST-001** — The environment SHALL provide clock synchronisation.

*Source: AUTH-FACT-005, AUTH-SESS-005*

Time-based codes fail silently against a drifting clock — valid codes rejected, users
told they are wrong. Absolute session expiry and grant expiry depend on it equally.

**Acceptance criteria**
1. Host clock drift stays within the TOTP drift tolerance.
2. Drift beyond tolerance raises an alert.

---

**INF-HOST-002** — All applications MAY share a single host. **The session isolation
in D-007 protects against web-level compromise, not host compromise**, and this is
recorded so the design is not read as offering more than it does.

*Source: D-063*

Per-application sessions defend against a stolen cookie, a cross-site request, or an
injection in one application. They do not defend against someone who has the host —
that person has every application, every process, and the database credential.

**This is the correct trade at current scale.** Web-level attacks are the ones that
occur; host compromise is a different tier of problem and separating machines would
defend against something unlikely before real scale.

**Consequence worth noting:** because everything shares a host, the mail server's
separate database (D-006) does more than avoid schema coupling — it also keeps mail
out of the database holding customer health data.

**Acceptance criteria**
1. The isolation claim is stated with its scope wherever it appears.
2. Application and database credentials are distinct, per OPS-MIG-003.

---

**INF-HOST-003** — The environment SHALL provide a secrets manager with the
properties below. **Exactly two bootstrap values** MAY be present on the host outside
it, as deployment-injected configuration: the **database connection** and the
**secrets-manager access credential**. No other secret SHALL be present in files,
images, environment variables, or the repository.

*Source: D-148; OPS-SEC-001, D-047, D-105*

**Why two, and why these.** The application must prove who it is to the secrets
manager before it can fetch anything, and that proof has to live somewhere — this is
secret zero, and no wording removes it. The database connection is needed before the
settings table can be read (OPS-CFG-008). Both are injected at deployment, never
committed and never baked into an image. The backup public key may also sit on the
host; it is not a secret (DR-010).

**The secrets manager SHALL:**

| Property | Why |
|---|---|
| Be **off-host** | Losing the host must not lose the keys (DR-009) |
| Be **reachable at boot** | The KEK and the fingerprint key are fetched at startup; nothing serves without them |
| Hold the **KEK, the fingerprint key and the backup private key** | The three keys a restore needs, also escrowed in the envelope (DR-009) |
| Hold the **maintenance database credential** | Fetched by the worker for the audit partition drop and by the `Janus.Cli` key-rotation command for the subject-key re-wrap (OPS-MIG-003a, OPS-SEC-003, D-148); not escrowed, recreated by migration |
| Authenticate the application by a **scoped credential**, revocable and rotatable without a code change | A leaked secret zero is contained and replaced, not redeployed around |
| Protect its own access by hardware-backed multi-factor authentication | D-084 |

**Boot dependency, accepted.** A restart while the secrets manager is unreachable
keeps the application down until it returns (R-O04). Caching the KEK on the host to
survive that would put the master key on the disk the design keeps it off.

Platform secret scanning is unavailable on the current plan, so nothing catches a
mistake automatically. The pipeline check in OPS-DEP-004 is the compensating control.

**Acceptance criteria**
1. Startup fails with a named error when the key-encryption key or the fingerprint
   key is unavailable.
2. No secret value other than the two bootstrap values appears on the host, and
   neither appears in any repository file or image layer.
3. Rotating any secret, including the secrets-manager credential, requires no code
   change.
4. The fingerprint key is never written to the database.

---

## 4. Database

**INF-DB-001** — PostgreSQL SHALL be provisioned with **ICU collation** and a
case-insensitive collation for identifier columns, **set at database creation**.

*Source: OPS-DB-001, D-040*

Cannot be changed later without rebuilding indexes. Without it, Arabic sorts by byte
order and two accounts can exist for one mailbox.

**Acceptance criteria**
1. Collation is verified before the first migration runs.
2. Arabic text sorts per Unicode rules.
3. Database-level case-insensitive comparison applies to plaintext columns. Identifier
   case-insensitivity comes from canonicalisation before fingerprinting
   (PRIV-RIGHT-005c), since a collation cannot see through a keyed hash.

---

**INF-DB-002** — Spatial querying SHALL be available for area containment.

*Source: D-061*

Address resolution matches coordinates against stored boundaries in our own database,
which is what keeps coordinates from reaching any third party.

**The boundary dataset SHALL be named**, and where its licence makes the supplier a
recipient, it SHALL appear in the records of processing.

*Source: D-079b*

The criterion was untestable while no dataset was identified. Start with city
boundaries, which are unambiguous and easy to source; district polygons follow if the
dropdown proves too coarse (D-061).

**Acceptance criteria**
1. A point-in-area query returns the containing area without an external service.
2. The dataset and its licence are recorded.

---

**INF-DB-003** — Two database credentials SHALL exist: one with schema-alteration
rights used only by the migration step, one for the application with row-level access
only.

*Source: OPS-MIG-003*

**Acceptance criteria**
1. The application credential cannot execute schema-altering statements.
2. The migration credential is absent from application configuration.

---

**INF-DB-004** — Continuous archiving SHALL be enabled, per `12-disaster-recovery.md`.

**Acceptance criteria**
1. Point-in-time restore to an arbitrary timestamp in the retention window succeeds.
2. Restore is tested at least quarterly by an automated, timed job that alerts on
   failure (DR-007); the environment can host the throwaway instance it needs
   (DR-008).

---

## 5. Cache

**INF-CACHE-001** — Redis SHALL be available for session and permission caching, and
its loss SHALL degrade rather than deny.

*Source: AUTH-SESS-001, AUTHZ-CACHE-001*

PostgreSQL holds the durable copy. Losing the cache costs latency, not access.

**Acceptance criteria**
1. Flushing the cache does not sign anyone out.
2. Sessions reload from the durable store.

---

## 6. Background execution

**INF-BG-001** — The environment SHALL support scheduled and queued background work.

Required by: notification delivery with retry (D-022), Stalwart reconciliation
(INT-MAIL-007), expired-session and consumed-token sweeps (AUTH-KEY-003), organization
deletion grace windows (IDN-ORG-003), authenticator invalidation windows (AUTH-RECOV-007), gateway
balance polling (INT-SMS-004), licence expiry warnings (OPS-MAINT-001), the
automated restore test (DR-007), the audit partition drop (PRIV-RET-002,
OPS-MIG-003a), the registration-session sweep (REG-SESS-001), domain-lock
re-verification (REG-DOM-001), the IP-to-city database refresh (INT-GEN-006), the
recovery-code reminder (AUTH-FACT-008), the account deletion grace window
(`account.deletion.grace`, IDN-ACCT-007), the takedown window (`takedown.grace`,
IDN-LIFE-003), the transactional outbox publisher (IDN-LIFE-003a), the privacy-request
deadline alerts (PRIV-RIGHT-002) and the read-volume baseline (OPS-ALERT-005) (D-148).
The key-encryption-key re-wrap (OPS-SEC-003)
is not background work of the worker: it runs inside the command-line process so that
it does not depend on the application being up (D-147).

*Source: D-148; D-147, the items named above*

**Acceptance criteria**
1. Scheduled work runs without a person triggering it.
2. Failure to run raises an alert rather than passing unnoticed: a job whose last
   successful run is older than twice its interval raises `background-job-failed`
   (D-153).

---

**INF-BG-002** — Background work SHALL run as a named principal with a real
organization scope, never as an absent user.

*Source: IDN-PRIN-001*

**Acceptance criteria**
1. A background job cannot query without a principal.
2. Its actions are audited with the stated reason.

---

## 7. Deployment

**INF-DEP-001** — The environment SHALL permit migrations to be applied **before** the
new version is deployed, and SHALL NOT require them at application startup.

*Source: OPS-MIG-001*

**Acceptance criteria**
1. The pipeline can run migrations against the target database independently of
   deployment.
2. Failure blocks deployment.

---

**INF-DEP-002** — Production SHALL NOT be modifiable outside the pipeline in normal
operation.

*Source: OPS-MIG-003, D-019*

**Acceptance criteria**
1. No routine procedure requires interactive access to a production host.
2. Any such access is exceptional and audited.

---

## 8. Observability

**INF-OBS-001** — The environment SHALL deliver alerts through at least two
independent channels, one of which does not depend on the mail system.

*Source: D-048*

If the mail system is the failure, an alert about it arriving by email is a loop.

**Acceptance criteria**
1. A mail-system failure still produces a reachable alert.
2. Delivery of each alert is confirmable.

---

**INF-OBS-003** — At least one reachability check of the public origins SHALL run
**off-host** and alert independently of the VPS.

*Source: D-093*

**Every alert channel currently originates on the machine being monitored.** Both email
and SMS are sends initiated by the application or the worker on the single VPS — so the
highest-severity scenario the recovery chapter plans for, **loss or hard failure of the
host, produces no alert at all.** Detection becomes a customer telephoning, or the
operator noticing at breakfast, and the 4–8 hour recovery objective acquires an
unbounded delay in front of it.

INF-OBS-001 requires two channels independent of the **mail system**; nothing required
independence from the **host**.

This is among the cheapest requirements in the set — an external uptime check with
notification.

**Acceptance criteria**
1. The check runs on infrastructure that does not share fate with the VPS.
2. It alerts the operator directly, not through anything hosted on the VPS.
3. It is named in `12-disaster-recovery` as the detection path for host loss.

---

**INF-OBS-002** — Log storage SHALL be subject to the retention rules in
`04-privacy.md`.

*Source: PRIV-RET-001, CONV-LOG-004*

Logs holding personal data fall under retention and erasure like any other store.
Logging identifiers rather than attributes is what keeps this from becoming an
erasure problem.

**Acceptance criteria**
1. Log retention is configured, not indefinite.
2. Default log output contains no email address or phone number.

---

## 9. Growth

Recorded so they are decisions rather than surprises.

| Change | Trigger | Source |
|---|---|---|
| Off-machine backups — **application database and mail store together** (DR-012) | The VPS tier upgrade, when block and object storage become available | D-044, D-085, D-102 |
| **Off-host erasure ledger** (DR-016) | The same upgrade — it needs the same object storage | D-096 |
| Second provider copy | When provider-level failure becomes unacceptable | D-044 |
| Warm standby | When a day's downtime costs more than a second host | D-044 |
| Object storage | Customer photos, or any large artifact | D-060 |
| Separate hosts per application | When host compromise becomes a threat worth defending | INF-HOST-002 |

---

## 10. Explicitly out of scope

| Area | Owner |
|---|---|
| Container arrangement, counts, orchestration | Infrastructure |
| Reverse proxy choice and configuration | Infrastructure |
| Certificate issuance mechanism, validation method, client, and where it runs | Infrastructure — these systems state what must be true, never how (D-072) |
| DNS arrangement | Infrastructure |
| Network segmentation and firewall rules | Infrastructure |
| Volume and filesystem layout | Infrastructure |
| Process supervision and restart policy | Infrastructure |
| Host operating system and patching | Infrastructure |
| Resource sizing | Infrastructure |

These systems constrain none of the above beyond the requirements stated in this
document. **The "how" stays out of scope here, but its definition must exist**: the
container arrangement, reverse proxy, DNS records, volumes, certificate mechanism and
pipeline target are committed to the repository, and the automated restore test
rebuilds from them (DR-017, D-147). This document does not say what the definition
contains; it says only that a committed, testable definition is there.

---

## 11. Open items

One: confirm whether the SMS gateway offers an HTTPS delivery-report URL
(INF-TLS-004, R-A12). Closed at integration time; until then the plain-HTTP exception
stands with its compensating controls.
