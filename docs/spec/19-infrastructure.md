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

**INF-TLS-002** — *Retired by D-225. Issuing and renewing certificates is the
deployment's.*

---

**INF-TLS-003** — *Retired by D-225. Watching certificates is the deployment's.*

---

**INF-TLS-004** — Outbound integrations SHALL use TLS, with plaintext endpoints rejected
at startup and at a change (INT-GEN-001, D-198).

*Source: INT-GEN-001, D-162, D-166, D-198*

Already required; restated here because it is an environment property. A provider's
published sample configuration can specify a plaintext base URL, which would send
personal data unencrypted if copied unexamined.

**Acceptance criteria**
1. A configured endpoint with a plaintext scheme fails startup with
   `integration.endpoint.insecure`, `details.key` naming the setting (INT-GEN-001).

---

## 3. Host

**INF-HOST-004** — *Retired by D-225. Where a deployment is hosted is its own; the
library records the location it is given (`05` INT-HOST-001).*

---

**INF-HOST-001** — The environment SHALL provide clock synchronisation.

*Source: AUTH-FACT-005, AUTH-SESS-005, D-166, D-225*

Time-based codes fail silently against a drifting clock — valid codes rejected, users
told they are wrong. Absolute session expiry and grant expiry depend on it equally.

The library does not watch the clock: keeping it synchronised, and watching it, are the
deployment's (D-225).

**Acceptance criteria**
1. Host clock drift stays within the TOTP drift tolerance.

---

**INF-HOST-002** — *Retired by D-225. How many hosts a deployment uses is its own.*

---

**INF-HOST-003** — The environment SHALL provide a secrets manager with the
properties below. **Exactly two bootstrap values** MAY be present on the host outside
it, as deployment-injected configuration: the **database connection** and the
**secrets-manager access credential**. No other secret SHALL be present in files,
images, environment variables, or the repository.

*Source: D-148, D-166; OPS-SEC-001, D-047, D-105, D-204*

**Why two, and why these.** The application must prove who it is to the secrets
manager before it can fetch anything, and that proof has to live somewhere — this is
secret zero, and no wording removes it. The database connection is needed before the
settings table can be read (OPS-CFG-008). Both are injected at deployment, never
committed and never baked into an image.

**How the library reads it (D-166).** The application reads every secret it needs
through the host's secret source (`ISecretSource`, LIB-EXT-001), in the startup hosted
service, before the server serves; no secret is an argument of `AddJanus`. Those
secrets are the key-encryption key versions, the fingerprint key versions, the
maintenance credential, the mail server's management key where the shipped adapter is
used (INT-MAIL-001), and each declared social provider's credential by the provider's
name (INT-GEN-002); a refusal names them in `details.key` as OPS-SEC-001 lists. A
`Janus.Cli` command reads its database connection and every key
version and credential it needs from one JSON document the operator, or for `migrate`
the pipeline's migration step, pipes to its standard input from the secrets manager's own
client (`migrate`'s carries the migration connection alone, OPS-MIG-001, D-204), never from an argument, a file or
an environment variable (OPS-SEC-001). The secrets of the
provider's client registry are none of these: the library generates and rotates them
itself and no deployment supplies one (OPS-SEC-002).

**The secrets manager SHALL:**

| Property | Why |
|---|---|
| Be **off-host** | Losing the host must not lose the keys, without which the data cannot be read |
| Be **reachable at boot** | Every secret above is read at startup; nothing serves without them |
| Hold **every version of the KEK and of the fingerprint key** the deployment holds | Each key is a set of versions with one current. A version a rotation retires leaves the application's document; keeping it for a backup taken under it is the deployment's (OPS-SEC-003, D-225) |
| Hold the **migration database credential** | Read only by the pipeline's migration step, which pipes it to `migrate` (OPS-MIG-001, OPS-MIG-003, D-204); never in the application's configuration (INF-DB-003) |
| Hold the **maintenance database credential** | Read by the application at startup for the audit partition job, and by the `Janus.Cli` key-rotation commands from the document piped to them (OPS-MIG-003a, OPS-SEC-003, D-148); recreated by migration |
| Hold the **mail server's management key**, where the shipped adapter is used, and **each social provider's credential** the deployment declares | Read at startup (INT-MAIL-001, INT-GEN-002, IDN-LIFE-012) |
| Authenticate the application by a **scoped credential**, revocable and rotatable without a code change | A leaked secret zero is contained and replaced, not redeployed around |
| Authenticate the pipeline's migration step by a **credential of its own**, which reads the migration database credential and nothing else, held by the pipeline platform's secret store and never on the host | The migration step never reaches the key-encryption key (OPS-MIG-001, D-173, D-204) |
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

**INF-DB-004** — *Retired by D-225. Backups are the deployment's.*

---

## 5. Cache

**INF-CACHE-001** — *Deferred by D-197. Version 1 runs without a cache: every record is
read from PostgreSQL. A cache returns, as an item of its own, when session or permission
reads are among the slowest queries the deployment measures.*

---

## 6. Background execution

**INF-BG-001** — The environment SHALL support scheduled and queued background work.

Required by: the handing of each message to the host's notification handler with retry
(D-022, D-225): an admitted send writes its outbox row in the transaction that
undertakes it, one immediate attempt, where the message has one (none where its ask is
answered before the handler is called, AUTH-ABUSE-003, AUTH-ABUSE-004), runs after the
outermost transaction commits and never inside an open one, and the worker carries
whatever that attempt did not (AUTH-ABUSE-004); Stalwart reconciliation (INT-MAIL-007);
mailbox provisioning, which pushes the state each mailbox is owed (INT-MAIL-006,
INT-MAIL-006a); expired-session and consumed-token sweeps (AUTH-KEY-003); the
sending-restriction record sweep (PRIV-RET-005); organization deletion grace windows
(IDN-ORG-003); the membership ends set in advance (`membership-ends`, IDN-MEM-001,
D-213); authenticator invalidation windows (AUTH-RECOV-007); the key-encryption key's
cryptoperiod (DR-009a); the audit partition job, which creates the months ahead and
drops expired partitions (PRIV-RET-002, OPS-MIG-003a); the registration-session sweep
(REG-SESS-001); the invitation sweep, which forgets what an expired invitation bound
(PRIV-RIGHT-005a); domain-lock re-verification (REG-DOM-001); the IP-to-city database
refresh (INT-GEN-006); the datacenter range refresh (AUTH-ABUSE-008); the recovery-code
reminder (AUTH-FACT-008); the account deletion grace window (`account.deletion.grace`,
IDN-ACCT-007); the takedown window (`takedown.grace`, IDN-LIFE-003); the transactional
outbox publisher (IDN-LIFE-003a); the event publisher, which carries each event from the
row written in the transaction that made its fact true (CONV-DESIGN-002, LIB-API-001);
the daily drift check of materialised derivations over the declared relationship sources
(`derivation.materialised.driftcheck`, AUTHZ-DERIVE-005), under the principal
`derivation-driftcheck`; the handing of raised alerts to the host's alert handler every
`outbox.poll.interval` (OPS-ALERT-003); the watch of the emergency credential
(OPS-BOOT-001); the privacy-request deadline alerts and the holiday-list look
(PRIV-RIGHT-002); and the read-volume baseline (OPS-ALERT-005) (D-148, D-166). Each runs
as the system principal `10` names for it (INF-BG-002). A job may run in several
processes at once; what a delivery job carries is claimed row by row before it is
carried (CONV-DESIGN-003), so each row is carried by one process at a time: a row is
claimed by a pass only once it is due, a send's immediate attempt claiming its new row
whatever its due instant (CONV-DESIGN-003, D-188), and a row that tracks several
deliveries is carried under one claim, renewed before each delivery (D-186). The worker
waits between rounds until the next job is due, or a run ends, and never longer than the
longest single delay the runtime's timer accepts: a job due further ahead is looked at
again after that delay and run when it is due (D-209). The key-encryption-key re-wrap
(OPS-SEC-003) is not background work of the worker: it runs inside the command-line
process so that it does not depend on the application being up (D-147).

*Source: D-148, D-166, D-183, D-186, D-188, D-189, D-209; D-147, the items named above*

**Acceptance criteria**
1. Scheduled work runs without a person triggering it.
2. Failure to run raises an alert rather than passing unnoticed: a job whose last
   successful run is older than twice its interval raises `background-job-failed`
   (D-153). The lapse of the job that carries raised alerts is delivered by the alert
   router directly from the worker, so a stalled carrier still reports itself
   (OPS-ALERT-001).
3. No job's run delays another job's turn in the same process.
4. Two processes running one delivery job at once carry each row once, and each delivery
   of a row that tracks several once.
5. A worker whose every job not running is due further ahead than the longest delay the
   runtime's timer accepts keeps running, looks again after that delay, and runs each
   job when it is due (D-209).

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

**INF-DEP-002** — *Retired by D-225. How a deployment changes production is its own.*

---

## 8. Observability

**INF-OBS-001** — *Retired by D-225. Delivering alerts is the host's (`06`
OPS-ALERT-003).*

---

**INF-OBS-003** — *Retired by D-225. Watching a deployment from outside is the
deployment's.*

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
| Object storage | Customer photos, or any large artifact | D-060 |
| A cache in front of PostgreSQL for sessions and permissions (INF-CACHE-001) | When session or permission reads are among the slowest queries the deployment measures | D-197 |

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
document; how each is defined, and where, is the deployment's (D-225).

---

## 11. Open items

None (D-225).
