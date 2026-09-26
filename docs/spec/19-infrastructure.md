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

*Source: D-063, D-166*

These are two different checks catching two different failures. Expiry monitoring sees
the consequence, weeks late. Renewal-failure monitoring sees the cause, the same day.
Neither substitutes for the other, and **they must not share a component** — asking a
broken renewer whether renewal is working returns a confident wrong answer.

**The library's part (D-166).** The outcome of the latest renewal SHALL reach the
library through `ICertificateRenewal` (LIB-HOST-001). The job `certificate-renewal`
reads it hourly and raises `certificate-renewal-failed` with `details.failedAt` while
the latest attempt failed; a deployment that registers none, or whose seam cannot
answer, raises `degradation` under `certificate.renewal.absent` or
`certificate.renewal.unread`. Expiry is read from the served certificate by the off-host
check of INF-OBS-003, which shares no component with the renewer or with the library.

**Acceptance criteria**
1. Expiry is determined from the **served certificate**, not from renewal state.
2. A renewal failure raises an alert without anyone checking.
3. The two checks share no component.
4. A deployment with no renewal outcome, or one that cannot answer, raises
   `degradation`.

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

*Source: INT-GEN-001, D-162, D-166*

Already required; restated here because it is an environment property. A provider's
published sample configuration can specify a plaintext base URL, which would send
personal data unencrypted if copied unexamined.

**Acceptance criteria**
1. A configured endpoint with a plaintext scheme fails startup with
   `integration.endpoint.insecure`, `details.key` naming the setting (INT-GEN-001).

---

## 3. Host

**INF-HOST-004** — Hosting is **outside Egypt**. The hosting-location field in
generated records reads accordingly, and the cross-border basis field is required.

*Source: D-081, D-023*

---

**INF-HOST-001** — The environment SHALL provide clock synchronisation.

*Source: AUTH-FACT-005, AUTH-SESS-005, D-166*

Time-based codes fail silently against a drifting clock — valid codes rejected, users
told they are wrong. Absolute session expiry and grant expiry depend on it equally.

**The library's part (D-166).** The environment SHALL report the offset it measured to
the library through `IClockReference` (LIB-HOST-001), positive where the host is ahead.
The job `clock-drift` reads it hourly and raises `clock-drift` where its magnitude
exceeds `factor.totp.drift` steps of 30 seconds, with `details.offsetSeconds` and
`details.toleranceSeconds`; an offset at the tolerance is within it. A stored
`factor.totp.drift` that cannot be read fails the run (OPS-CFG-008). A deployment that
registers no reference, or whose reference cannot answer, raises `degradation` under
`clock.reference.absent` or `clock.reference.unread`.

**Acceptance criteria**
1. Host clock drift stays within the TOTP drift tolerance.
2. Drift beyond tolerance raises an alert.
3. A deployment with no clock reference, or one that cannot answer, raises
   `degradation`.

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
separate database (D-006) does more than avoid schema coupling: it also keeps mail
out of the database holding the subjects' personal data, including any the host
declares sensitive.

**Acceptance criteria**
1. The isolation claim is stated with its scope wherever it appears.
2. Application and database credentials are distinct, per OPS-MIG-003.

---

**INF-HOST-003** — The environment SHALL provide a secrets manager with the
properties below. **Exactly two bootstrap values** MAY be present on the host outside
it, as deployment-injected configuration: the **database connection** and the
**secrets-manager access credential**. No other secret SHALL be present in files,
images, environment variables, or the repository.

*Source: D-148, D-166; OPS-SEC-001, D-047, D-105*

**Why two, and why these.** The application must prove who it is to the secrets
manager before it can fetch anything, and that proof has to live somewhere — this is
secret zero, and no wording removes it. The database connection is needed before the
settings table can be read (OPS-CFG-008). Both are injected at deployment, never
committed and never baked into an image. The backup public key may also sit on the
host; it is not a secret (DR-010).

**How the library reads it (D-166).** The application reads every secret it needs
through the host's secret source (`ISecretSource`, LIB-EXT-001), in the startup hosted
service, before the server serves; no secret is an argument of `AddJanus`. Those
secrets are the key-encryption key versions, the fingerprint key versions, the
maintenance credential, the mail server's management key where the shipped adapter is
used (INT-MAIL-001), and each declared social provider's credential by the provider's
name (INT-GEN-002); a refusal names them in `details.key` as OPS-SEC-001 lists. A
`Janus.Cli` command reads its database connection and every key
version and credential it needs from one JSON document the operator pipes to its
standard input from the secrets manager's own client, never from an argument, a file or
an environment variable (OPS-SEC-001). The secrets of the
provider's client registry are none of these: the library generates and rotates them
itself and no deployment supplies one (OPS-SEC-002).

**The secrets manager SHALL:**

| Property | Why |
|---|---|
| Be **off-host** | Losing the host must not lose the keys (DR-009) |
| Be **reachable at boot** | Every secret above is read at startup; nothing serves without them |
| Hold **every version of the KEK and of the fingerprint key** the deployment holds, and the **backup private key** | The keys a restore needs, also escrowed in the envelope (DR-009). Each key is a set of versions with one current; a version a rotation retires stays until every backup taken before that rotation completed has expired (OPS-SEC-003, DR-009) |
| Hold the **maintenance database credential** | Read by the application at startup for the audit partition job, and by the `Janus.Cli` key-rotation commands from the document piped to them (OPS-MIG-003a, OPS-SEC-003, D-148); not escrowed, recreated by migration |
| Hold the **mail server's management key**, where the shipped adapter is used, and **each social provider's credential** the deployment declares | Read at startup (INT-MAIL-001, INT-GEN-002, IDN-LIFE-012) |
| Authenticate the application by a **scoped credential**, revocable and rotatable without a code change | A leaked secret zero is contained and replaced, not redeployed around |
| Protect its own access by hardware-backed multi-factor authentication | D-084 |

**Boot dependency, accepted.** A restart while the secrets manager is unreachable
keeps the application down until it returns (R-O04). Caching the KEK on the host to
survive that would put the master key on the disk the design keeps it off.

Platform secret scanning is unavailable on the current plan, so nothing catches a
mistake automatically. The pipeline check in OPS-DEP-004 is the compensating control.

**Acceptance criteria**
1. Startup fails with `model.startup.secretunavailable`, `details.key` naming the
   secret, when any secret the application reads through the secret source is
   unavailable, and the server serves nothing before every one is read.
2. No secret value other than the two bootstrap values appears on the host, and
   neither appears in any repository file or image layer.
3. Rotating any secret, including the secrets-manager credential, requires no code
   change.
4. The fingerprint key is never written to the database: after bootstrap, both key
   rotations, and a write through every store that computes a fingerprint, no column of
   the library's schema holds any version of the key, as bytes or in any text
   encoding.

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


**INF-DB-003** — Two database credentials SHALL exist: one with schema-alteration
rights used only by the migration step, one for the application with row-level access
only.

*Source: OPS-MIG-003*

**Acceptance criteria**
1. The application credential cannot execute schema-altering statements.
2. The migration credential is absent from application configuration.

---

**INF-DB-004** — Continuous archiving SHALL be enabled, per `12-disaster-recovery.md`.

*Source: DR-007, DR-008, D-166*

**Acceptance criteria**
1. Point-in-time restore to an arbitrary timestamp in the retention window succeeds.
2. Restore is tested at least quarterly by an automated, timed job that alerts on
   failure (DR-007); the environment can host the throwaway instance it needs and
   supplies it to the library through `IRestoreTestInstance` (DR-008, LIB-HOST-001).

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

Required by: notification delivery with retry (D-022): an admitted send writes its
outbox row in the transaction that undertakes it, one immediate attempt runs after the
outermost transaction commits and never inside an open one, and the worker carries
whatever that attempt did not (AUTH-ABUSE-004); Stalwart reconciliation
(INT-MAIL-007); mailbox provisioning, which pushes the state each mailbox is owed
(INT-MAIL-006, INT-MAIL-006a); expired-session and consumed-token sweeps
(AUTH-KEY-003); the sending-restriction record sweep (PRIV-RET-005); organization
deletion grace windows (IDN-ORG-003); authenticator invalidation windows
(AUTH-RECOV-007); gateway balance polling (INT-SMS-004); licence expiry warnings and
the annual envelope operation (OPS-MAINT-001); the key-encryption key's cryptoperiod
(DR-009a); the automated restore test (DR-007); the audit partition job, which creates
the months ahead and drops expired partitions (PRIV-RET-002, OPS-MIG-003a); the
registration-session sweep (REG-SESS-001); the invitation sweep, which forgets what an
expired invitation bound (PRIV-RIGHT-005a); domain-lock re-verification (REG-DOM-001);
the IP-to-city database refresh (INT-GEN-006); the recovery-code reminder
(AUTH-FACT-008); the account deletion grace window (`account.deletion.grace`,
IDN-ACCT-007); the takedown window (`takedown.grace`, IDN-LIFE-003); the transactional
outbox publisher (IDN-LIFE-003a); the event publisher, which carries each event from
the row written in the transaction that made its fact true (CONV-DESIGN-002,
LIB-API-001); the daily drift check of materialised derivations over the declared
relationship sources (`derivation.materialised.driftcheck`, AUTHZ-DERIVE-005), under
the principal `derivation-driftcheck`; the carrying of
raised alerts to their channels every `outbox.poll.interval` (OPS-ALERT-001); the
watches of the host clock (INF-HOST-001), of certificate renewal (INF-TLS-003) and of
the emergency credential (OPS-BOOT-001); the privacy-request deadline alerts and the
holiday-list look (PRIV-RIGHT-002); and the read-volume baseline (OPS-ALERT-005)
(D-148, D-166). Each runs as the system principal `10` names for it (INF-BG-002).
The key-encryption-key re-wrap (OPS-SEC-003)
is not background work of the worker: it runs inside the command-line process so that
it does not depend on the application being up (D-147).

*Source: D-148, D-166; D-147, the items named above*

**Acceptance criteria**
1. Scheduled work runs without a person triggering it.
2. Failure to run raises an alert rather than passing unnoticed: a job whose last
   successful run is older than twice its interval raises `background-job-failed`
   (D-153). The lapse of the job that carries raised alerts is delivered by the alert
   router directly from the worker, so a stalled carrier still reports itself
   (OPS-ALERT-001).
3. No job's run delays another job's turn in the same process.

---

**INF-BG-002** — Background work SHALL run as a named principal with a stated reason,
never as an absent user: organization-scoped where it acts for one organization, and
deployment-scoped, restricted to one named operation, where it is pool-wide
(IDN-PRIN-001). Each scheduled job of INF-BG-001 runs as a principal of its own (`10`,
system principals) and SHALL refuse to run under a principal that may not run its
operation.

*Source: IDN-PRIN-001, D-166*

**Acceptance criteria**
1. A background job cannot query without a principal.
2. Its actions are audited with the principal's name and the stated reason
   (IDN-AUD-001).
3. A job run under a principal of another operation is refused.

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
