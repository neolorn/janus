# 12 — Erasure across backups

Backing up a deployment's database, restoring it and testing the restore are the
deployment's (D-225). The library's part is that an erasure reaches every backup by key
destruction, that a ledger kept off the host re-applies erasures after a restore, and
that the key-encryption key is rotated within its cryptoperiod.

**Prerequisite:** `00-overview.md`, section 1.

---

## 1. Erasure and backups

**DR-006a** — Erasure SHALL reach backups by **key destruction**, not by rewriting
them, **within the limits of the design**: erasure overwrites the wrapped subject key in
the database, so a restore to a point **after** the erasure reproduces that write, while
a restore to a point **before** it recovers the key as it then was. The ledger (DR-016)
re-applies erasures after such a restore; otherwise erasure of pre-erasure backups
completes when they expire.

*Source: D-068, D-082, D-166, D-225*

Backups are immutable, so an erasure cannot alter them; without this the subject's
data would remain readable in every archived copy. Destroying the subject key renders
it unreadable everywhere, live and archived, with no backup file touched.

**Acceptance criteria**
1. After a restore to a point before an erasure, replaying the off-host ledger (DR-016)
   re-applies every erasure the restored database does not hold; where the deployment
   registers no ledger, nothing re-applies them (D-225).
2. No erasure procedure attempts to modify a backup.
3. A subject erased before the backup was taken is not recoverable from it.

---

**DR-016** — Completed erasures SHALL be appended to an **off-host ledger**, and a
restore SHALL be followed by its replay.

**Values (D-153, D-166).** One line per erasure: the RFC 3339 UTC instant to the second, one space,
the full subject identifier, one space, the reason (`10` section 5.12a); UTF-8, no
header. The subject identifier is written in its lower-case hyphenated form; the
instant is the instant the erasure was recorded. The replay is the
`replay-erasures <ledger path>` command of `Janus.Cli`, idempotent over every line.

The deployment registers the ledger through `IErasureLedger` (LIB-HOST-001). The line is
a required confirmation on the erasure's outbox record (IDN-LIFE-003a), offered before
the host's subscribers and recorded under the name `erasure-ledger`, so the record and
its erasures row stay `awaiting-subscribers` until the line is durable and fail,
raising `erasure-delivery-exhausted`, when the retry budget is spent. The manual
completion path appends a line that is not yet durable before it closes anything, and
never vouches for it. When a ledger is registered, every erasure completed before it
is appended too, once. A line may be appended twice; a replay reads a repeat as one
erasure.

*Source: D-096, D-166, D-225*

Where the ledger is kept, and the restore it follows, are the deployment's (D-225).

**Why a restore alone is not enough.** Restoring to a point before an erasure recovers
the wrapped key as it then was, and the erasure record with it — so the subject is
active and readable as though the request had never been made. Every trace lived in the
database and was restored away with it. Only a record kept outside the database
survives.

**Only a record that is not restored survives**, which is why this is off-host.

**Contents — one line per erasure:**

```
2026-08-14T09:22:05Z a4f2c81e-… erasure-request
2026-08-19T16:04:41Z b8c13d70-… minor-takedown
```

Timestamp, subject identifier, reason. The reason is one of `erasure-request` ·
`minor-takedown` (`10` section 5.12a, D-147), the same spellings as the erasures table
(IDN-LIFE-003b). An organization erasure erases no account (IDN-ORG-003), so it writes
no line.

**No encryption is required, and none is specified.** The subject identifier is an opaque
value derived from nothing about the person (IDN-ACCT-002); without the database it
identifies nobody. What the file does disclose is **how many erasures occurred and when** —
not personal data, and judged acceptable against the alternative of a silently reversed
erasure.

**Size:** a handful of lines a year. Kilobytes for the life of the system.

**Replay after restore.** The `replay-erasures <ledger path>` command reads the whole
ledger before it writes anything, and refuses it whole where the file cannot be opened
or decoded as UTF-8 (`api.request.malformed`, `details.member` `ledger`) or any line is
not in the written form (`details.line` its number). A line whose erasures row the
restored database holds, a repeated line included, or one naming an account the
restored database does not hold, is left. Every other line is carried out again in one
transaction: the account enters `deleting` where it was not already, with `deletingBy`
`takedown` for `minor-takedown` and `oob-request` otherwise, at the line's instant, and
is erased by the writes the sweep uses; `ErasureRequested` is written to the outbox
again with the line's instant and reason, so the host redoes its half; and
`privacy.erasure.executed` is recorded under the deployment-scoped principal
`replay-erasures`, reason `DR-016`, operation `erasure-replay`, with `details.reason` in
the spelling of `10` section 5.12a and `details.erasedAt`. The command runs under the
application's own credential and prints `{"reapplied": n, "standing": n, "absent": n}`
and nothing of a subject. Idempotent, so replaying the whole ledger is always safe.

**Acceptance criteria**
1. The ledger is written to storage that does not share fate with the database host.
2. An erasure is not reported complete until its ledger line is durable.
3. Replay is idempotent and covers the whole ledger, not only recent lines.
4. The ledger contains no name, address, email, phone or fingerprint.
5. An erasure completed before the ledger was registered is appended once the ledger
   is registered.

---

## 2. The key-encryption key

**DR-009a** — The key-encryption key SHALL have a **defined cryptoperiod** and SHALL
be rotated at its expiry, and immediately on any suspicion of exposure.

*Source: D-084, D-166, D-225*

**Rotation is the control, not custody.** A key can be copied without being used and
without detection; what bounds that risk is that an undetected copy stops being valid.
NIST defines a cryptoperiod as the span during which a key is authorised for use, and
exceeding it without rotation accumulates risk from exposure events that may not yet
have been detected.

**Suspicion means** any incident touching the secrets manager or a copy of the key the
deployment keeps (D-225). The response is rotation before a copied key could be used.

**Rotation is cheap** — the key-encryption key wraps other key material rather than
encrypting data, so rotating it re-wraps and never touches customer data. The operation
is OPS-SEC-003 (D-147): the operator adds the new version to the secrets manager and
restarts the application on it; `rotate-kek`, a resumable `Janus.Cli` command under the
maintenance credential, re-wraps every row of the subject-key table in a batch job;
`rotate-kek --retire` retires the previous version once every row is re-wrapped.

**Values (D-166).** The cryptoperiod is one year from the completion of the rotation
that introduced the current version (`ops.keyrotation.completed` with `kind`
`key-encryption-key`, OPS-SEC-003 AC5), or from bootstrap for the version the deployment
started on. From `maintenance.expiry.warninglead` before its end, until a rotation
completes, the daily job `kek-cryptoperiod` raises `expiry-approaching` under the scope
`kek-cryptoperiod` with `details.version`, `details.rotatedAt` and `details.dueAt`.

**Acceptance criteria**
1. The cryptoperiod is defined and rotation occurs at its expiry.
2. Rotation completes without re-encrypting customer data.
3. An out-of-cycle rotation can be performed on demand.
4. *Retired by D-225: a copy of the key the deployment keeps is its own.*
5. The rotation is performed through OPS-SEC-003 and by no other path.
6. The warning of criterion 1 follows the key's own rotation record, and a rotation
   of the fingerprint key does not end it.

---

## 3. Retired

**DR-001** — *Retired by D-225. Backups, restores and the custody of keys are the
deployment's.*

**DR-002** — *Retired by D-225. Backups, restores and the custody of keys are the
deployment's.*

**DR-003** — *Retired by D-225. Backups, restores and the custody of keys are the
deployment's.*

**DR-004** — *Retired by D-225. Backups, restores and the custody of keys are the
deployment's.*

**DR-005** — *Retired by D-225. Backups, restores and the custody of keys are the
deployment's.*

**DR-006** — *Retired by D-225. Backups, restores and the custody of keys are the
deployment's.*

**DR-007** — *Retired by D-225. Backups, restores and the custody of keys are the
deployment's.*

**DR-008** — *Retired by D-225. Backups, restores and the custody of keys are the
deployment's.*

**DR-009** — *Retired by D-225. Backups, restores and the custody of keys are the
deployment's.*

**DR-009b** — *Retired by D-225. Backups, restores and the custody of keys are the
deployment's.*

**DR-010** — *Retired by D-225. Backups, restores and the custody of keys are the
deployment's.*

**DR-011** — *Retired by D-225. Backups, restores and the custody of keys are the
deployment's.*

**DR-012** — *Retired by D-225. Backups, restores and the custody of keys are the
deployment's.*

**DR-013** — *Retired by D-225. Backups, restores and the custody of keys are the
deployment's.*

**DR-017** — *Retired by D-225. Backups, restores and the custody of keys are the
deployment's.*
