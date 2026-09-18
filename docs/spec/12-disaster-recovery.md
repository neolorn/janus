# 12 — Backup and Disaster Recovery

Recovery objectives, backup mechanism, and the restore procedure.

**Prerequisite:** `00-overview.md`, section 1.

**Why this exists.** OPS-MIG-006 makes point-in-time recovery "the real safety net"
and rejects down-migrations on that basis. The deferred destructive-DDL gate (`08`
§CI, D-019) accepts an unrecoverable failure mode for the same reason. Both rested on
a capability that had not been specified.

---

## 1. Objectives

| | Target | Status |
|---|---|---|
| **Recovery point** — how much data may be lost | Seconds | Achieved by continuous log archiving |
| **Recovery time** — how long to be back | **4–8 hours** | **Accepted deliberately** |
| **Host loss, Phase 1** | Unrecoverable | **Accepted; open-ended until DR-005** |
| **Restore during operator absence** | **Possible with outside technical help**, following this document | Escrow per DR-009 |

**On restore during absence.** The envelope (DR-009) supplies every credential
needed, but the procedure requires someone who can provision a host, run a
point-in-time restore, and interpret an integrity check. **The owner is not
technical.** The honest statement is therefore *possible with outside technical help*,
not *possible*.

A scripted restore the owner could run himself is deferred until after release
(D-085), at which point the objective becomes true rather than merely honest.

**On recovery time.** The stated business need was 2–3 hours. Meeting that reliably
requires a warm standby server to fail over to, since restoring a database of real
size takes longer than that once provisioning, transfer, log replay and verification
are counted. A second server was **declined on cost**, so the objective is stated
honestly at 4–8 hours rather than aspirationally at 2–3.

This is a deliberate trade of recovery time for infrastructure cost, recorded so it
is a known position rather than a discovery during an incident.

**Two things that soften it:** payment records are held independently by the payment
provider, so in-flight orders are reconcilable. And the failure this protects
against is rare — most incidents are application faults fixed by deploying, not data
loss.

*Source: D-044*

---

## 2. Mechanism

**DR-001** — The database SHALL run in continuous archiving mode, retaining a base
backup plus every subsequent write-ahead log segment, enabling restoration to any
point in time.

**Acceptance criteria**
1. A point-in-time restore to an arbitrary timestamp within the retention window
   succeeds.
2. The loss window between the last archived segment and a failure is measured in
   seconds.

---

**DR-002** — Archiving SHALL be incremental. Only segments produced since the last
base backup are shipped.

A single full copy is taken; everything after is a trickle. Re-transferring the whole
database is never required.

**Acceptance criteria**
1. Steady-state transfer volume is proportional to write volume, not database size.

---

**DR-003** — Base backups SHALL be taken on a schedule, and the archive SHALL be
pruned so that a full restore never requires replaying more log than the recovery
time objective allows.

**Acceptance criteria**
1. Replay time from the most recent base backup is measured and stays within budget.

---

## 3. Phases

### 3.1 Phase 1 — through launch: local only

**DR-004** — Backups MAY reside on the same host as the database from development
through launch and into early operation.

*Source: D-044*

**What this covers:** a destructive migration, accidental deletion, application-level
corruption — the failures that actually occur, in development and in production
alike.

**What it does not cover:** loss of the host. If the server is gone, the backups are
gone with it.

**Accepted risk, stated plainly.** From launch the database holds health data —
order history that discloses a diagnosis (PRIV-SENS-003). During this phase, loss of
the host means **permanent, unrecoverable loss of customer health data**, and that
is also a reportable personal data incident under the availability limb of the
breach rules (section 7).

This is accepted deliberately, ending at the trigger in DR-005 — which has no date, so the period is open-ended (D-109).

**Acceptance criteria**
1. Point-in-time restore works and is tested during this phase.
2. The limitation is recorded, not carried as tacit knowledge.
3. Records of processing state the technical security measures accurately for this
   phase — they do not claim off-machine backups that do not exist.

---

### 3.2 Phase 2 — post-launch: off-machine

**DR-005** — Backups SHALL move off the database host **at the VPS tier upgrade**,
which is the point at which bundled object storage becomes available.

*Source: D-044*

The trigger is the upgrade already planned for after release, not a date. **The
upgrade has no date**, so Phase 1 is open-ended until it happens — recorded as such
in R-A01 rather than described as bounded (D-109).

**Acceptance criteria**
1. Backups exist on storage independent of the database host once the upgrade is in
   place.
2. The destination appears in generated records of processing.
3. Records of processing are updated to reflect the changed security measures.

---

**DR-006a** — Erasure SHALL reach backups by **key destruction**, not by rewriting
them, **within the limits of the design**: erasure overwrites the wrapped subject key in
the database, so a restore to a point **after** the erasure reproduces that write, while
a restore to a point **before** it recovers the key as it then was. The ledger (DR-016)
re-applies erasures after such a restore; otherwise erasure of pre-erasure backups
completes when they expire.

*Source: D-068, D-082*

Backups are immutable, so an erasure cannot alter them; without this the subject's
data would remain readable in every archived copy. Destroying the subject key renders
it unreadable everywhere, live and archived, with no backup file touched.

**Acceptance criteria**
1. After a restore to a point before an erasure, the restore procedure (§4)
   **re-applies every erasure recorded in the erasures table** that post-dates the
   restore point where that table survives the restore; where it does not (Phase 1,
   until the ledger DR-016 exists), the residual is R-A13 and the procedure says so.
2. No erasure procedure attempts to modify a backup.
3. A subject erased before the backup was taken is not recoverable from it.

---

**DR-006** — Where the storage supports immutability, backups SHALL be written
immutably.

Write-once storage protects backups from deletion, including by an attacker who has
reached the host. A backup an intruder can delete on the way out is not a backup.

**Acceptance criteria**
1. A backup object cannot be deleted or overwritten before its retention expires.

---

### 3.3 Phase 3 — later, when justified

Not required, recorded so they are decisions rather than omissions.

| Addition | Reactivation trigger |
|---|---|
| Copy with a second provider | When provider-level failure becomes an unacceptable risk |
| Warm standby server | When the cost of a day's downtime exceeds the cost of a second server |

A standby consumes the same log stream and requires no redesign — it is a different
consumer of what already exists.

---

## 3.4 Key material

**DR-009** — The key-encryption key SHALL be **escrowed with the break-glass
credential**, in the same sealed envelope held by the company owner, under the same
annual reseal.

*Source: D-069*

Without this, a restore during the operator's absence produces a database nobody can
read: unusable authenticator secrets, dead provider credentials, and unreadable
customer fields (PRIV-RIGHT-005a). The owner could enter the system via break-glass
and be unable to restore it.

The mechanism exists for the case where the operator is unreachable, so it cannot
depend on the operator being reachable.

**Envelope contents**, all printed; the last four also held in the secrets manager,
the **break-glass credential on paper only** (OPS-BOOT-004, D-133):

| Item | Purpose |
|---|---|
| Break-glass credential | One-time administrative session — **paper only**; the system holds a hash |
| Key-encryption key | Unwraps the per-subject keys during restore |
| Fingerprint key | Without it no identifier matches — nobody can sign in (PRIV-RIGHT-005c, DR-011) |
| Backup private key | Decrypts the backups themselves (DR-010) |
| Deployment credentials | Redeploy to a new host from the committed infrastructure definition (DR-017): the envelope names the repository that holds it |

*Source: D-148; D-069, D-084, D-103, D-147*

**Recovery order:** the secrets manager first as the fast path; the envelope as the
**standalone** fallback. The envelope must therefore be sufficient **without** the
secrets manager, since a lost or unavailable account takes its contents with it.

**Previous backup private keys** stay in the envelope and the secrets manager until
every backup encrypted under them has expired (D-103).

**Acceptance criteria**
1. The envelope contains all five items above.
2. A restore can be completed **without the operator and without the secrets
   manager**, by the owner with outside technical help following this document,
   including decrypting the backup and signing in afterwards (§1).
3. The annual reseal covers all five.
4. The envelope is tamper-evident and inspected at each reseal.

---

**DR-009a** — The key-encryption key SHALL have a **defined cryptoperiod** and SHALL
be rotated at its expiry, and immediately on any suspicion of exposure.

*Source: D-084*

**Rotation is the control, not custody.** An escrowed key can be copied without being
used and without detection; what bounds that risk is that an undetected copy stops
being valid. NIST defines a cryptoperiod as the span during which a key is authorised
for use, and exceeding it without rotation accumulates risk from exposure events that
may not yet have been detected.

**Suspicion means any of:** physical intrusion at the custody location, an alarm,
tamper evidence on the envelope, or any incident touching the secrets manager. The
response is rotation before a copied key could be used.

**Rotation is cheap** — the key-encryption key wraps other key material rather than
encrypting data, so rotating it re-wraps and never touches customer data. The
operation is OPS-SEC-003 (D-147): a resumable `Janus.Cli` command under the
maintenance credential that introduces the new version, re-wraps every subject key
in a batch job, retires the previous version when the job reports complete and
produces the escrow copy.

**Acceptance criteria**
1. The cryptoperiod is defined and rotation occurs at its expiry.
2. Rotation completes without re-encrypting customer data.
3. An out-of-cycle rotation can be performed on demand.
4. The escrowed copy is replaced in the same operation.
5. The rotation is performed through OPS-SEC-003 and by no other path.

---

**DR-009b** — Escrow recovery SHALL be **tested annually** by performing a restore
from the escrowed material.

*Source: D-084*

Testing that escrowed keys can actually restore is itself an audit of the escrow
procedure. An untested escrow is a hypothesis, exactly as an untested backup is.

**Acceptance criteria**
1. The annual test uses the envelope contents, not the operator's working access.
2. Failure is treated as an incident.

---

**DR-010** — Backups SHALL be encrypted under a **dedicated asymmetric backup key**.
The public half MAY reside on the host; the private half SHALL NOT.

*Source: D-069, D-103*

Otherwise losing the host loses both the data and the means to read it. Backups will
hold health data and will move to third-party storage at the tier upgrade, and the
records of processing state that encryption is in place.

**Why asymmetric, and why its own key.** An unattended backup job must encrypt without
holding a secret, which only a public key allows. Keeping the key separate from the
key-encryption key means the master key never touches the backup pipeline, and the
two rotate on independent cadences — a KEK rotation never forces re-encryption of
retained backups.

**The private key lives in the secrets manager and the envelope** (DR-009). It rotates
at the annual reseal; previous private keys are retained until every backup encrypted
under them has expired.

**Acceptance criteria**
1. A backup file obtained without the private key yields nothing.
2. No private key material is present on the host filesystem. The automated restore
   test (DR-007) fetches the backup private key from the secrets manager at run time,
   holds it in memory only, and writes it nowhere (D-140).
3. A backup encrypted under a retired key remains restorable until that backup
   expires.

---

**DR-016** — Completed erasures SHALL be appended to an **off-host ledger**, and the
restore procedure SHALL replay it.

*Source: D-096*

**Deferred until the tier upgrade**, when object storage becomes available (DR-005). Until
then the exposure is accepted — see R-A13.

**Why a restore alone is not enough.** Restoring to a point before an erasure recovers
the wrapped key as it then was, and the erasure record with it — so the subject is
active and readable as though the request had never been made. Every trace lived in the
database and was restored away with it. Only a record kept outside the database
survives.

**Only a record that is not restored survives**, which is why this is off-host.

**Contents — one line per erasure:**

```
2026-08-14T09:22Z  a4f2c81e-…  erasure-request
2026-08-19T16:04Z  b8c13d70-…  minor-takedown
```

Timestamp, subject identifier, reason. The reason is one of `erasure-request` ·
`minor-takedown` · `organization-erasure` (`10` section 5.12a, D-147), the same
spellings as the erasures table (IDN-LIFE-003b).

**No encryption is required, and none is specified.** The subject identifier is an opaque
value derived from nothing about the person (IDN-ACCT-002); without the database it
identifies nobody. What the file does disclose is **how many erasures occurred and when** —
not personal data, and judged acceptable against the alternative of a silently reversed
erasure.

**Size:** a handful of lines a year. Kilobytes for the life of the system.

**Replay after restore:** for every identifier in the ledger, confirm the key is destroyed
and the erasure recorded; complete anything the restore forgot. Idempotent, so replaying
the whole ledger is always safe.

**Acceptance criteria**
1. The ledger is written to storage that does not share fate with the database host.
2. An erasure is not reported complete until its ledger line is durable.
3. Replay is idempotent and covers the whole ledger, not only recent lines.
4. The ledger contains no name, address, email, phone or fingerprint.

---



**DR-013** — The mail server's database SHALL be backed up on the same schedule and
to the same destination as the application database.

*Source: D-085*

**Previously unspecified in any phase.** Continuous archiving covers the application
database only; the mail server's separate store had no backup requirement anywhere,
so staff mail history would have been lost permanently in every host-loss scenario —
unaccepted and unrecorded.

**Acceptance criteria**
1. A cold rebuild restores staff mail history.
2. The backup runs in the same operation as the application backup (DR-012).

---

**DR-012** — The database backup **and the mail server's database** SHALL be produced by
**one operation, to one destination, always together**.

*Source: D-082, D-085, D-102*

Two stores backed up separately drift apart in two ways: one is moved and the other
forgotten, or the two are restored to different points and the business is left with
orders it cannot match to correspondence. Both are avoided by producing them as one
artefact pair. This matters most at the storage upgrade: moving the application
backup to object storage while leaving the mail store on the host would produce an
off-site backup that is incomplete, undiscovered until it was needed.

**Phase 1:** both on the VPS, per DR-004. **At the upgrade:** both move together. A
**destination change only** — no code, no redesign.

**Acceptance criteria**
1. Neither artefact can be produced without the other.
2. A single setting governs the destination of both.
3. The restore test fails if either artefact is absent.

---

**DR-011** — The restore procedure SHALL enumerate **every artefact a cold rebuild
needs**, not the database alone.

| Artefact | Why |
|---|---|
| PostgreSQL base backup and archived log | The data — including the wrapped per-subject keys, which live in the library's own table (PRIV-RIGHT-005a) and restore with it |
| **Fingerprint key** | Without it no identifier matches — **nobody can sign in**, and duplicate detection fails |
| Key-encryption key | Everything encrypted under it is otherwise unreadable |
| **Backup private key** | The backup itself cannot be opened without it (DR-010) |
| Mail server's own database | Separate store (INT-MAIL-002), backed up per DR-013 |
| Certificate material | Services cannot be reached without it |
| Deployment credentials | Cannot redeploy to a new host without them |
| Infrastructure definition | The committed definition of DR-017; the host is rebuilt from it, not from memory |

*Source: D-069, D-102, D-103, D-147*

**Acceptance criteria**
1. A rebuild following only this document and the committed infrastructure
   definition (DR-017) succeeds without recourse to a person's memory.

---

**DR-017** — The complete infrastructure definition SHALL be committed to the
repository: the container arrangement, the reverse proxy configuration, the DNS
records, the volumes, the certificate mechanism and the pipeline target. DR-007's
automated restore SHALL rebuild its test instance from that definition, so the
definition is proven current on every run. The envelope (DR-009) SHALL name the
repository.

*Source: D-147; DR-007, DR-009, DR-011, INF-DEP-001*

**Why the repository and not the envelope.** A copy in the envelope goes stale the
first time the infrastructure changes and can never be tested without opening the
envelope. A definition in the repository changes with the infrastructure it
describes, is versioned with it, and is exercised by the restore test, so a drift
between the definition and the running host is caught as a failed test rather than
discovered during a real rebuild. The `19` specification still leaves the
infrastructure "how" out of scope (its section 10); this requirement is that the
"how", whatever it is, exists as a committed artefact.

**What the definition excludes.** No secret value (OPS-SEC-001, AUTH-KEY-002): the
definition references the secrets manager and the two deployment-injected bootstrap
values (INF-HOST-003) by name only.

**Acceptance criteria**
1. The repository holds a definition from which a host with no prior state can be
   brought to serving: containers, reverse proxy, DNS records, volumes, certificate
   mechanism and pipeline target are each present.
2. The DR-007 job builds its throwaway instance from the committed definition, not
   from a hand-maintained script, to the extent its phase allows (a database-only
   instance in Phase 1, DR-008); the parts a phase cannot exercise (DNS records, the
   reverse proxy, the certificate mechanism) are proven by the annual rebuild exercise
   of DR-011.
3. The envelope names the repository and how to reach it; a rebuild from the
   envelope and the repository alone succeeds (DR-011 AC1).
4. The definition contains no secret value; a scan of the repository for the
   escrowed material and the bootstrap values finds nothing.

---

## 4. Testing

**DR-007** — Restore SHALL be tested **by an automated job**, at least quarterly,
**timed**, with the measured recovery time recorded. The test SHALL **decrypt
something and prove it**, and SHALL alert on failure.

*Source: D-148; D-044, D-069, D-110, D-147*

A restore that brings back rows and not keys looks successful and is not. Verifying a
row count proves the data arrived; decrypting a subject's personal field proves it is
usable.

**The job:** restore the latest base backup and log into a separate throwaway
database instance (DR-008); decrypt a canary subject's personal field with the live
key-encryption key; verify a canary account's fingerprint resolves; record the elapsed
time against the recovery-time objective; tear the instance down; alert on any failure
or on time exceeding the objective (OPS-ALERT-001). No person runs it and no person
watches it: a failed test is an alert, not a discovery.

**The annual escrow test is the manual one** (DR-009b): its point is to use the
envelope rather than live access, which no job can do. It is one of the two accepted
recurring human tasks; this quarterly job is not.

An untested backup is a hypothesis. The measured time is what converts the stated
objective into a number that is known rather than assumed.

**Acceptance criteria**
1. A restore is performed and timed at least quarterly without human action.
2. The measured time is recorded and compared against the objective.
3. A failed test, or one exceeding the objective, raises an alert and is treated as
   an incident.
4. The test proves a personal field decrypts and a known account can sign in.
5. The test instance is built from the committed infrastructure definition (DR-017),
   so each run proves current the parts of the definition the phase exercises.

---

**DR-008** — Restore SHALL be tested to a **separate database instance**, never over
the running database. In Phase 1 that instance MAY be a throwaway container on the
same host; it is torn down after the test.

*Source: D-044, D-110*

**Acceptance criteria**
1. The procedure does not touch production.
2. The test instance does not outlive the test.

---

**Detection.** Host loss produces no alert from the host itself. The **off-host
reachability check** (INF-OBS-003) is the detection path for every scenario in this
chapter.

---

## 5. Restore procedure

1. **Stop writes.** If the application is running against a damaged database, stop
   it before restoring — a partial restore under load produces a worse state.
2. **Decide restore versus fix forward.** Data destroyed or corrupted → restore. A
   logic fault that wrote wrong values but lost nothing → usually a corrective
   migration, which is faster and loses nothing.
3. **Identify the target timestamp** — immediately before the destructive event, not
   the moment it was noticed.
4. **Provision a host** and restore the base backup.
5. **Replay logs** to the target timestamp.
6. **Verify** before cutting over: row counts against expectation, most recent
   records present, permission and ancestry integrity intact.
7. **Cut over** and record the timeline.

**Key material must be restored too**, and it comes from a different place. The
**backup private key** opens the backup (DR-010); the database restore then brings
back ciphertext **and** the wrapped subject keys; the **key-encryption key** unwraps
them and the **fingerprint key** makes sign-in possible. All three come from the
secrets manager, or from the sealed envelope (DR-009). Verify that a subject's
personal field decrypts **and that a known account can sign in** before cutting over
— a restore that brings back rows without working keys looks successful and is not.

**Subjects erased after the backup was taken stay erased**, because the erasure
overwrote the wrapped key in the database and the restore reproduces that write.

**A restore to a point _before_ an erasure recovers the wrapped key as it then was**, so
that subject's fields decrypt again. This is ordinary point-in-time behaviour, not a
disagreement between stores — there is one store. **Replay the ledger** (DR-016) after
any restore preceding a completed erasure.

**Ancestry verification matters specifically.** The closure table (AUTHZ-INHERIT-002)
is maintained transactionally, so a correct restore restores it consistently — but a
restore is exactly when an inconsistency would be invisible and consequential. Run
the ancestry integrity check before cutting over.

---

## 6. What restore does not fix

**External systems do not roll back.** Mail is delivered, payments are captured,
shipments are dispatched. A restore to an earlier point produces a database that
disagrees with the payment provider, the courier, and the mail server.

After any restore that moves time backwards:

- Reconcile orders against payment provider records
- Reconcile shipment state against the courier
- Run Stalwart reconciliation (INT-MAIL-007) and read the drift report

**Restoring is not undo.** It recovers the database; it does not recover the world.

---

## 7. Breach interaction

A restore that loses records is a personal data incident if those records were
subject data. The 72-hour notification clock (PRIV-BREACH-001) applies to loss of
availability, not only to disclosure.

Record what was lost and for whom before deciding whether notification is required.

---

## 8. Open items

None. Phase 2 — off-host backup storage — is post-launch and open-ended (R-A01,
DR-005); it is not a build item.
