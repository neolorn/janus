# 06 — Operations

Database, migrations, deployment, configuration, secrets, bootstrap, and
observability.

**Prerequisite:** `00-overview.md`, sections 1 and 4.

**Scope.** This document covers *how the system is built, deployed, configured and
run*. Behaviour is documents 01 to 05.

---

## 1. Data access

**OPS-DATA-001** — Two data access tools SHALL be used, with one unambiguous rule.

| Tool | Used for |
|---|---|
| **EF Core LINQ** | Everything normal — reads, writes, relationships, change tracking, migrations, and the model driving the authorization registry |
| **Dapper** | Every hand-written SQL query — reports, recursive queries, historical reconstruction, bulk reads |

The rule is *am I writing SQL by hand?* Yes → Dapper. No → LINQ.

*Source: D-017*

EF Core's own raw-SQL facility SHALL NOT be used as a middle tier. Two mechanisms
for one job produce case-by-case judgment calls.

**Acceptance criteria**
1. No use of EF Core's raw SQL execution appears in the codebase.
2. Hand-written SQL lives in one place per aggregate, not inline at call sites.

---

**OPS-DATA-002** — A single accessor SHALL hand out a connection with the ambient
transaction already attached. Direct connection retrieval SHALL NOT be used.

*Source: D-017*

Without this, a hand-written query on a separate connection silently misses rows
written earlier in the same transaction — a nasty and easily-missed bug in financial
code.

**Acceptance criteria**
1. A hand-written query inside a transaction sees uncommitted writes from that
   transaction.
2. Direct connection retrieval is unreachable from the service layer.
3. A test asserts transaction visibility across both tools.

---

**OPS-DATA-003** — Direct ADO.NET use SHALL be reserved for database features with
no higher-level abstraction: binary bulk copy, advisory locks, listen/notify,
server-side cursors. It SHALL NOT be used for query optimisation.

*Source: D-041*

Query optimisation stops at Dapper. Below that is a capability question, not a
performance one.

**Acceptance criteria**
1. Each direct use carries a comment naming the database feature requiring it.

---

## 2. Database

**OPS-DB-001** — The database SHALL use ICU collation, with a case-insensitive
collation for identifier columns.

*Source: D-040*

Without ICU, Arabic sorts by byte order, producing meaningless ordering. Without
case-insensitive identifiers, two accounts can exist for one mailbox and a
lowercase-only lookup silently fails for a user who typed a capital.

**Acceptance criteria**
1. Arabic text sorts per Unicode rules, not byte order.
2. Database-level case-insensitive comparison applies to columns that remain
   **plaintext**. **Identifiers are fingerprints** (PRIV-RIGHT-005c) and a collation
   cannot see through one, so their case-insensitivity comes from the pinned canonical
   form applied before fingerprinting.
3. Collation is set at database creation, before the first migration.

---

**OPS-DB-002** — The library SHALL own its own schema with its own migration history,
separate from the host's.

*Source: D-018*

**Acceptance criteria**
1. Library and host migrations never collide.
2. The library never reads or writes a host table.

---

**OPS-DB-003** — Grant lookup SHALL be supported by a partial index excluding
revoked rows, and reverse lookup ("who can access this?") by its own index.

*Source: D-015, AUTHZ-TEST-002*

**Acceptance criteria**
1. The primary permission predicate uses an index rather than a sequential scan at
   production-scale volume.
2. Reverse lookup completes without a full scan.
3. Every column named by a declared derivation is indexed (AUTHZ-DERIVE-004).

---

## 3. Migrations

**OPS-MIG-001** — Migrations SHALL be applied by a dedicated step in the pipeline
before the new version deploys. They SHALL NOT be applied at application startup.

*Source: D-018*

Startup migration means instances racing each other on deploy, failures surfacing as
crash loops rather than failed pipelines, and the running application holding
permission to alter the tables that authorize it.

**Acceptance criteria**
1. Starting the application against an un-migrated database does not migrate it.
2. The pipeline fails before deployment if migration fails.

---

**OPS-MIG-002** — Application startup SHALL **verify** that the schema matches the
model and SHALL refuse to start if it does not.

*Source: D-018, P-003*

**Acceptance criteria**
1. A schema mismatch produces a named error and a non-zero exit.
2. The application does not serve requests in a mismatched state.

---

**OPS-MIG-003** — Two database credentials SHALL exist: one with schema-alteration
rights used only by the migration step, and one for the application with row-level
access only.

*Source: D-018*

Nothing in production can alter schema. Same reasoning as the protected-settings
configuration list — controls that would catch a compromise must not be reachable by
it.

**Acceptance criteria**
1. The application credential cannot execute schema-altering statements.
2. The migration credential is not present in application configuration.

---

**OPS-MIG-003a** — A third, **maintenance** credential SHALL exist for scheduled
maintenance that must run outside the pipeline. It has exactly two uses: the audit
partition drop (PRIV-RET-002) and the key re-wrap of OPS-SEC-003 (D-147, D-148). It
SHALL hold no schema-alteration right of its own. For the partition drop it SHALL be
granted `EXECUTE` on `SECURITY DEFINER` functions created by the migration step, each
doing one named thing. For the re-wrap it SHALL hold row-level `SELECT` and `UPDATE`
on the subject-key table (the wrapped keys, PRIV-RIGHT-005a) and read and write on the
rotation progress table of OPS-SEC-003, because the unwrap and wrap happen in the
command's own process with keys that are never in the database; it SHALL hold nothing
else. The worker fetches it from the secrets manager for the partition drop and the
`Janus.Cli` command fetches it for the re-wrap (INF-HOST-003); it is never in
application configuration.

*Source: D-148; D-118, D-147*

**Acceptance criteria**
1. The maintenance credential cannot issue `DROP`, `ALTER` or `CREATE` directly.
2. Each maintenance function is created by a migration, owned by the migration
   role, and listed in the serialized model output for review.
3. The application credential cannot execute any maintenance function.
4. The maintenance credential can read and update rows of the subject-key table and
   the rotation progress table, and can read or write no other table; the grants are
   listed in the serialized model output for review.

---

**OPS-MIG-004** — Migrations SHALL be applied **library first, host second**.

*Source: D-018*

Host tables may reference library tables; the reverse never occurs.

**Acceptance criteria**
1. The pipeline enforces ordering rather than relying on convention.

---

**OPS-MIG-005** — Every migration SHALL follow expand-and-contract: it SHALL work
against the **previous** application version. Renaming or dropping SHALL NOT occur in
the same release that changes the code using it.

*Source: D-018*

Both versions run briefly during rollout.

**Acceptance criteria**
1. A new column is added nullable; the constraint is tightened in a later release.
2. A migration is tested against the previous release's application build.

---

**OPS-MIG-006** — Recovery SHALL be roll-forward. Down-migrations SHALL NOT be
relied upon.

*Source: D-018*

Down-migrations are unreliable once data exists. Point-in-time recovery is the real
safety net.

**Acceptance criteria**
1. The runbook describes fix-forward, not rollback.
2. Point-in-time recovery is configured and tested.

---

**OPS-MIG-007** — The pipeline SHALL apply migrations **twice** against a throwaway
database: once from empty, once from the previous release's schema.

*Source: D-018*

The second catches migrations that work on a fresh database and break on a real one
— the failure that actually occurs.

**Acceptance criteria**
1. Both runs execute on every pipeline run.
2. Either failing fails the pipeline.

---

## 4. Deployment

**OPS-DEP-001** — Additive schema changes SHALL deploy without a gate. **Destructive**
changes — dropping a column or table, narrowing a type, adding a constraint that
could fail against existing rows — SHALL be gated by a **pipeline split**:

- When the gate is enabled and destructive operations are present, the automatic
  deploy **fails with a message**
- A **separate, manually dispatched workflow** applies destructive migrations
- The toggle is a **repository variable**

The gate is **currently disabled**; the pipeline is written with it wired in.

*Source: D-019, D-042.1*

Environment protection rules with required reviewers are unavailable on the current
plan for private repositories, so the gate is implemented as a pipeline split rather
than a deployment approval.

Every other migration failure is fixable in minutes with another migration; a
dropped column is data that no longer exists.

**Acceptance criteria**
1. Enabling the gate requires changing a repository variable, not a pipeline edit.
2. With the gate enabled, a destructive migration fails the automatic deploy and
   names the manual workflow to run.
3. Additive migrations are unaffected either way.
4. The manual workflow applies the same migrations the automatic one would have.

---

**OPS-DEP-002** — Destructive-operation detection SHALL run on every deploy and
report, regardless of whether the gate is enabled.

*Source: D-019*

**Acceptance criteria**
1. Every deploy reports which destructive operations it contains.
2. The report appears whether or not the deploy pauses.

---

**OPS-DEP-003** — When additional people gain deploy access, the gate toggle SHALL be
protected from pull-request modification.

*Source: D-019, D-010, D-042.1*

**Acceptance criteria**
1. The toggle is not modifiable by a pull request.

---

**OPS-DEP-004** — A secret-scanning step SHALL run in the pipeline and fail the build
on detection.

*Source: D-042.3, D-150*

GitHub's own secret scanning and push protection are unavailable on this plan for
private repositories, so OPS-SEC-001 has no platform enforcement. The check moves
into the pipeline. The scanner is **gitleaks**, run from its official action pinned to
a release commit SHA (never a floating tag), over the full history on every push, with
its default rule set and a committed `.gitleaks.toml` holding only allow-list entries
that name the file and the reason (D-150). No other scanner is added.

**Acceptance criteria**
1. A committed credential fails the build.
2. The scanner runs on every push, not only on the default branch.

---

**OPS-DEP-005** — Full integration suites SHALL NOT run on every push to every
branch.

*Source: D-042.4, D-147*

The plan includes 3,000 Actions minutes per month. The double migration run plus
containerised integration tests consume these quickly.

**Acceptance criteria**
1. Unit and analyzer checks run on every push.
2. Full integration and migration suites run on pull request and on the default
   branch.
3. Minute consumption is measured and reviewed against the allowance by the operator,
   monthly, and the review is recorded in the maintenance log of OPS-MAINT-001
   (CONV-GATE-002).

---

## 5. Configuration

### 5.1 Taxonomy

**OPS-CFG-001** — Configuration SHALL be runtime-changeable by default.

*Source: D-010*

**Acceptance criteria**
1. A setting is redeploy-scoped only where listed in OPS-CFG-004 or in the model
   declaration.

---

**OPS-CFG-002** — Direction SHALL determine friction. **Tightening** a security
control at runtime is free. **Loosening** SHALL require step-up authentication, a
written reason, and an audit entry.

*Source: D-010*

**Settings with no direction** — alert destinations, approver counts, template
content — are classified **as loosening**, requiring step-up, a reason and an audit
entry.

*Source: D-079b*

Previously unclassified. Removing an alert destination or reducing an approver count
weakens the system as surely as lengthening a timeout, and neither is obviously a
tightening or a loosening on its face.

**Acceptance criteria**
1. Shortening a session timeout requires no step-up.
2. Lengthening one requires step-up and a reason.
3. A change with no direction requires step-up and a reason.
4. All are audited.

---

**OPS-CFG-003** — Hard floors SHALL be enforced in code. Values outside them SHALL be
**rejected at validation, not silently clamped**.

*Source: D-010, AUTH-SESS-005*

**Acceptance criteria**
1. A password floor below the standard's minimum is rejected.
2. A session absolute timeout above the maximum is rejected with a named error.
3. Rejection occurs at startup or at the point of change, not at first use.

---

**OPS-CFG-004** — The following SHALL NOT be changeable **through the application**.

- Disabling audit logging
- **Disabling export auditing**
- Disabling rate limiting entirely
- Turning off step-up enforcement for an organization
- The relying party identifier
- Token signing algorithm and signature verification
- `legal.governinglanguage`, the default governing language of legal documents
  (PRIV-CONS-005): a restart to change, and the change raises a Normal alert
  (OPS-ALERT-001)

*Source: D-148; D-010, D-020.1, D-045, D-146*

The selection test is not "how sensitive is this setting" but **"does turning this
off blind us to the person turning it off."** The governing language is the one entry
protected on a different ground: it fixes which text of every legal document binds,
so it changes with a restart and a recorded alert rather than from a session
(D-146).

**Acceptance criteria**
1. No runtime API modifies any listed setting.
2. Changing one requires access the application itself does not have; a command-line
   operation restricted to the server satisfies this as well as a redeployment
   (D-071).

---

**OPS-CFG-005** — Configuration changes SHALL be audited exactly as permission grants
are — who, what, from, to, when, why — with the same retention.

*Source: D-010*

**Acceptance criteria**
1. Every change produces an audit record with before and after values.
2. Records are queryable by setting and by actor.

---

### 5.2 System administration

**OPS-CFG-006** — System administration SHALL be a **permission**, never implied by
organizational seniority or ownership.

*Source: D-010*

**Acceptance criteria**
1. An organization owner without the grant cannot change protected settings.
2. The permission is granted, never inherited.

---

**OPS-CFG-007** — Granting or revoking system administration SHALL require system
administration.

*Source: D-010*

Self-referential deliberately. Without it, any administrator who can manage grants is
a system administrator one step removed, and the distinction is decorative.

**Acceptance criteria**
1. A grant-managing administrator without the permission cannot confer it.

---

### 5.3 Where settings live

**OPS-CFG-008** — Runtime-changeable settings SHALL be stored in the library's own
schema and changed through the management application, audited as permission grants
are.

*Source: D-071*

Previously unstated: the specification required settings to be runtime-changeable and
audited without saying what stored them.

**Two bootstrap values, for bootstrapping rather than security:** the database
connection and the secrets-manager access credential are needed *before* anything
else can be read, and are deployment-injected (INF-HOST-003). The key-encryption key
and the fingerprint key are then fetched from the secrets manager at startup, and
only after that can the settings table be read.

*Source: D-071, D-105*

**The named restriction set is runtime configuration** (`restrictions`,
AUTH-ABUSE-004), edited through `GET/PUT/DELETE /admin/restrictions/{name}` exactly as
the public-holiday list is (D-142): the change applies to the next send with no
restart, is audited under OPS-CFG-005 and emits `SendingRestrictionChanged`. A **loosening**
(a higher max, a shorter interval, a removed bucket or a deleted restriction) falls
under OPS-CFG-002 (step-up `restriction:edit`, a reason, an audit entry) and raises a
Normal alert (OPS-ALERT-001); a tightening needs the step-up and the audit entry
only. A grant of credit to a key (`POST /admin/restrictions/{name}/grant`) is not a
configuration change: it is a support action, stepped up, audited with a reason and
alerted (`SendingRestrictionGranted`).

*Source: D-146*

**Acceptance criteria**
1. Changing a runtime setting requires no restart.
2. Every change carries actor, before and after values, timestamp and reason.
3. Neither bootstrap value, nor any key fetched at startup, is stored in the database.
4. Editing a restriction applies to the next send without a restart, is audited with
   before and after values, and a loosening raises the Normal alert.

---

## 5a. Alerting

**OPS-ALERT-001** — The following conditions SHALL raise alerts.

| Condition | Severity | Source |
|---|---|---|
| Sustained authentication failures against one account | High | AUTH-ABUSE-001 |
| Recovery attempts clustering on one account | High | AUTH-RECOV-002 |
| One approver handling unusual recovery volume | High | D-008 |
| Read-volume or export anomaly per actor | High | D-045 |
| Break-glass credential used | High | OPS-BOOT-002 |
| **Configuration change to a protected setting** | High | OPS-CFG-004 |
| **Alert destination changed** — delivered to the *previous* destinations | High | OPS-ALERT-004a |
| **Step-up policy weakened** | High — the same lever as an alert destination, under a different name | D-083 |
| **Implausible concurrent sessions for one account** | High | D-051 |
| Spike in permission denials | Normal | AUTHZ-GATE-004 |
| Gateway balance drain or floor breach | Normal | INT-SMS-004 |
| **Background job failure** | Normal | INF-BG-001 |
| **Erasure or takedown delivery exhausted its retries** | High | IDN-LIFE-003a |
| **Certificate renewal failure** | High — a total-outage precursor | INF-TLS-003 |
| **Host clock drift beyond tolerance** | Normal | INF-HOST-001 |
| **Degradation: blocklist fallback, failed provider push, undelivered notification, reconciliation drift** | Normal | OPS-OBS-002 |
| **Repeated callback verification failure** | Normal | BFF-MACH-003 |
| **Unusual rate of duplicate-identifier notifications** — an enumeration probe | Normal | AUTH-ABUSE-003, D-121 |
| **Privacy-request decision deadline approaching** — `privacy.request.warninglead` before it | Normal | PRIV-RIGHT-002, D-126 |
| **Privacy-request decision deadline reached** — undecided on the day | High | PRIV-RIGHT-002, D-126 |
| **Licence or permit expiry approaching** — within `maintenance.expiry.warninglead` | Normal | OPS-MAINT-001, D-121 |
| **Holiday list running out** — no `privacy.holidays` date lies beyond `maintenance.expiry.warninglead`; deadlines after the last date are counted without holidays (compliant, earlier) | Normal | PRIV-RIGHT-002, D-142 |
| **No emergency credential exists** — non-dismissable until one is generated | High | OPS-BOOT-001, D-133, D-140 |
| **Restore test failed or exceeded the recovery-time objective** | High | DR-007, D-140 |
| **Restriction loosened**: a higher max, a shorter interval, a removed bucket or a deleted restriction | Normal | AUTH-ABUSE-004, OPS-CFG-008, D-146 |
| **Restriction grant issued**: credit added to a key by support | Normal | AUTH-ABUSE-004, D-146 |
| **Domain re-verification failed**: a locked domain's DNS record no longer verifies; nothing is revoked | Normal | REG-DOM-001, D-146 |
| **Domain removed from a lock**: new sign-ins with addresses in it stop | Normal | REG-DOM-001, D-146 |
| **`legal.governinglanguage` changed** | Normal | PRIV-CONS-005, OPS-CFG-004, D-146 |
| **Governing-language text missing**: a document version cannot publish, or a document that must be shown has no governing-language text | Normal | PRIV-CONS-006, D-146 |

*Source: D-048, D-071, D-121, D-146, D-147*

The seven conditions from the duplicate-identifier probe to the restore test were
required elsewhere to "raise an alert", "be surfaced" or
"produce a monitored signal" with no condition defined to consume them. The six after
them are the conditions D-146 introduced.

**The named exception (D-147).** The off-host reachability check of INF-OBS-003
cannot run on the host and therefore cannot be a condition of this table: the
condition it detects is the host being gone. It alerts the same destination lists
(OPS-ALERT-004) and, because the operator may be the unreachable person, the owner
destinations as well (`alerting.owner.email`, `alerting.owner.sms`), regardless of
`alerting.owner.enabled`.

**Acceptance criteria**
1. Each condition produces an alert without anyone watching.
2. No requirement elsewhere references alerting with no corresponding condition here,
   with INF-OBS-003 as the one named exception: it runs off-host, alerts the same
   destination lists and the owner destinations, and is verified by its own criteria.

---

**OPS-ALERT-002** — Alerts SHALL be **deduplicated per condition per window**.

*Source: D-048*

One alert per sustained attack, never one per attempt. Without this an attacker
triggers alerts to drain the prepaid SMS balance — turning the alerting into the
attack.

**Acceptance criteria**
1. A thousand failed attempts against one account produce one alert.

---

**OPS-ALERT-003** — Email SHALL carry all conditions; SMS SHALL additionally carry
high severity. Both SHALL support delivery confirmation.

**Two routing exemptions:**
- **All alert-class sends are exempt from the send hard-stop**, not balance alerts
  alone. Otherwise a low balance silently disables the mail-system alerts that are
  deliberately SMS-first, and break-glass's every-channel alerting — so **SMS
  exhaustion plus a mail outage becomes a total alerting blackout**, which is the
  compound failure two independent channels exist to prevent. Deduplication
  (OPS-ALERT-002) already bounds alert volume.

  *Corrected: an earlier draft narrowed this to balance alerts, against D-048.*
- **Mail-system alerts are SMS-first.** An alert about the mail system arriving by
  mail is a loop — out-of-band routing is the standard rule for exactly this

*Source: D-048, D-071*

**Acceptance criteria**
1. A mail-system failure still produces a reachable alert.
2. A balance-floor breach is reported despite sends being stopped.
3. Alert-class sends continue below the floor while ordinary sends are refused.
4. The residual case — balance actually at zero — is recorded as unreachable by SMS,
   with email carrying alone.

---

**OPS-ALERT-004a** — Changing an alert destination SHALL notify **every previous
destination**, non-suppressibly, and SHALL raise a **High** alert to those previous
destinations.

*Source: D-083*

**This closes a bypass of the entire alerting system.** An attacker holding one
stepped-up administrative session could replace the destinations with their own — a
"loosening" change their session already satisfied — and nothing would fire, because
alerts trigger only on changes to protected settings. From that point every alert in
the system reached the attacker instead of the operator: exfiltration anomalies,
recovery clustering, break-glass use.

The selection test for protection is *"does turning this off blind us to the person
turning it off."* Redirecting alerts does exactly that. It was caught for audit
logging and export auditing and missed here — the same lever under a different name.

**The pattern is the one already used for identifier changes** (REG-IDENT-002,
REG-IDENT-006): the existing security-notice set is always told, whatever the new one is.

**Acceptance criteria**
1. Every previous destination is notified before the change takes effect.
2. The notification cannot be suppressed by any setting.
3. Removing the last destination of a channel is refused.
4. The change alert is delivered to the previous destinations, not the new ones.

---

**OPS-ALERT-004** — Alert destinations SHALL be **lists, not single values**.

*Source: D-071*

**Owner notification of routine alerts is off by default and configurable**
(`alerting.owner.enabled`), so the owner is not sent operational noise. The owner's
destinations themselves are always configured (`alerting.owner.email`,
`alerting.owner.sms` — required), and **break-glass use and replacement generation
reach them regardless of the switch** (OPS-BOOT-002, D-129).

**Acceptance criteria**
1. More than one destination can be configured per channel.
2. Enabling owner notification requires no deploy.
3. With `alerting.owner.enabled` off, a break-glass use still reaches the owner's
   email and phone.

---

**OPS-ALERT-005** — Read volume per actor SHALL be baselined and alerted on deviation
from **that actor's own history**, not a fixed threshold.

*Source: D-045, D-071*

A warehouse clerk's normal differs from an account manager's.

**Acceptance criteria**
1. An actor reading far beyond their own pattern raises an alert.
2. An actor whose normal volume is high does not alert continuously.

---

**OPS-ALERT-006** — Export operations SHALL be defined, gated by step-up,
individually audited, and rate-limited.

*Source: D-045*

Bulk export is the exfiltration mechanism; ordinary browsing is not.

**Acceptance criteria**
1. "Export" is an enumerated set of operations, not a judgement.
2. Export auditing cannot be disabled through the application (OPS-CFG-004).

---

**OPS-ALERT-007** — Concurrent sessions for one account from implausibly distant
origins SHALL raise an alert.

*Source: D-051*

Attribution is load-bearing for volume alerting, approver anomaly detection, audit
records and offboarding verification. Shared credentials break all four silently.

**Acceptance criteria**
1. Simultaneous sessions from implausible origins alert.
2. Ordinary multi-device use does not.

---

## 6. Bootstrap

**OPS-BOOT-001** — A fresh deployment SHALL be initialised by a command-line
operation run against the database, creating the first organization, the first
system administrator, an enrolment link for that administrator, and the reserved
**`emergency`** account (OPS-BOOT-002) with no credential. **It SHALL NOT
generate the break-glass credential**: that is issued from the management application
(OPS-BOOT-004), so the secret never touches a terminal, a log or a file.

*Source: D-028, D-133*

Whoever runs the deployment already holds server access, which is strictly more
privileged than anything the first account can do. No new secret is created, nothing
is transmitted, and there is no race window.

The command needs only the database. The first administrator's mailbox is **queued**
for provisioning through the outbox and created when the mail server is reachable
(INT-MAIL-006 AC1a); `emergency` gets none (INT-MAIL-006 AC1b, D-144).

**The command SHALL require `legal.governinglanguage`** (PRIV-CONS-005) beside the
deployment values of LIB-HOST-001 that have no default: it is the eighth such value
(`10` section 4), and it is protected from then on (OPS-CFG-004).

*Source: D-146*

**Acceptance criteria**
1. Running it twice while a system administrator exists is refused.
2. No default account with a known credential is created at any point.
3. The command emits no break-glass credential; until one is generated, a
   non-dismissable High alert (illustrative wording: *"no emergency credential
   exists"*) is shown to every system administrator and raised on OPS-ALERT-001.
4. Bootstrap without `legal.governinglanguage` is refused with a named error.

---

**OPS-BOOT-002** — The break-glass credential SHALL be single-use, grant a
**time-boxed** system administration session, and trigger maximum-noise alerting on
every available channel when used.

*Source: D-010*

**The session belongs to a reserved account, `emergency`** — created at bootstrap
(OPS-BOOT-001), a member of the administrative organization holding the
`system-administrator` role, with **no sign-in method of any kind**: the sealed
credential is its only way in, and no factor can be enrolled on it. Every action in a
break-glass session is audited with `emergency` as acting and effective identity
(AUTHZ-IMP-001) and the owner's stated reason; it appears in no device list, holds no
mailbox, and cannot be granted anything further, suspended, or deleted. It may
approve any recovery, including the sole administrator's (AUTH-RECOV-002a). The
session SHALL satisfy step-up for its lifetime (AUTH-STEP-004). Lifetime is
configurable with an enforced ceiling (D-138).

**Alerting SHALL reach the owner as well as the operator.** The scenario this exists
for is one in which the operator is unreachable, so alerting only the operator means
the alert reaches nobody present. The owner's destinations are **required
configuration** (`alerting.owner.email`, `alerting.owner.sms`, `10` §4.5) — the
deployment refuses to start without them — and break-glass use and replacement
generation reach them **always**, independent of `alerting.owner.enabled`, which
governs routine alerts only (OPS-ALERT-004).

**The session SHALL be an auth session** (AUTH-SESS-004), so every application
opens from it exactly as from an ordinary sign-in (BFF-SESS-006); the owner reaches
the management application by navigating to it.

**The credential SHALL be presented through a frontend route** (`/break-glass`,
FE-BG-001), reached by the address printed on the envelope: one field, one button.
The endpoint (`POST /auth/break-glass`) sits behind it. The owner is not technical
(`12` §1); an API endpoint is not a procedure they can follow.

*Source: D-065, D-129*

**Acceptance criteria**
1. Use consumes it; a second attempt fails.
2. The session expires automatically at a configured lifetime within the ceiling.
3. Use raises alerts immediately, on every channel, to both owner and operator —
   the owner's destinations receive it with `alerting.owner.enabled` off.
4. The session can approve a recovery and grant system administration.
5. Bootstrap without `alerting.owner.email` and `alerting.owner.sms` is refused.
6. From the break-glass session, opening the management application requires no
   further credential.

---

**OPS-BOOT-004** — The break-glass credential, first issue and every replacement,
SHALL be generated from the management application, by a stepped-up system
administrator **or from within a break-glass session**, and rendered **exactly once**
as a printable page: the code in check-charactered groups of four, a QR code of the
code alone (never a link), the `/break-glass` address, and the two-line instruction
for the owner. The system stores **only a hash**; the code is held **on paper only**,
never in the secrets manager, never emailed, never written to a file.

*Source: D-148; D-065, D-133, D-147*

**Strength and throttle (D-147).** The credential SHALL carry at least 128 bits of
entropy, drawn from a typeable alphabet (no characters that are confused in print or
absent from a common keyboard) and rendered in the check-charactered groups above, so
a transcription error is caught before submission. Attempts at `/auth/break-glass`
SHALL be source-throttled per AUTH-ABUSE-001 and, in addition, limited to at most 5
attempts per hour globally across all sources. With 128 bits behind it, the global
limit exists to make the attack loud, not to make it infeasible.

The four other envelope items (DR-009) live in both the envelope and the secrets
manager because the system needs them daily. The break-glass code is needed by
nobody on an ordinary day, and its whole purpose is to exist outside the operator's
reach; a copy in the operator's vault would defeat it.

The bootstrap command is the only other generator, it runs on the server, and it
refuses while a system administrator exists, so without this, one use exhausts the
mechanism until the operator returns.

**Accepted consequence:** a break-glass holder can mint unlimited future credentials.
They already hold full administrative access at that moment, so this grants nothing
new. Alerting and audit are the controls.

**Acceptance criteria**
1. A break-glass session can generate and seal a replacement.
2. Generation is audited and alerted, to the owner regardless of
   `alerting.owner.enabled` (OPS-BOOT-002).
3. The rendered page cannot be reopened; a second request generates a new
   credential and invalidates the previous one.
4. No plaintext break-glass credential exists in the database, the secrets manager,
   logs, mail, or any file.
5. The `/break-glass` page accepts the code by typing or by scanning the QR.
6. The generated code carries at least 128 bits of entropy from a typeable alphabet;
   a group with a wrong check character is refused before the hash is compared.
7. A sixth attempt within one hour at `/auth/break-glass`, from any source, is
   refused, and the per-source throttle of AUTH-ABUSE-001 applies as well.

---

**OPS-BOOT-003** — The sealed break-glass credential SHALL be held by the company
owner, not by the developer.

*Source: D-029*

The developer is the sole administrator and therefore the single point of failure. A
credential only they can reach is no recovery path for the company.

**Acceptance criteria**
1. The custody arrangement is recorded in the runbook.

---

## 7. Secrets and keys

**OPS-SEC-001** — The key-encryption key and the fingerprint key SHALL come from a
secrets manager at startup. All other secrets SHALL be encrypted at rest under the
key-encryption key. No secret SHALL reside in configuration files or environment
variables in production, **except the two deployment-injected bootstrap values**
named in INF-HOST-003 — the database connection and the secrets-manager credential.

**The key-encryption key, the fingerprint key and the backup private key SHALL
additionally be escrowed** with the break-glass credential (DR-009), so a restore is
possible when the operator is unreachable.

*Source: D-026.3, D-069, D-103, D-105*

**Acceptance criteria**
1. No secret value appears in any repository file or image layer.
2. Startup fails with a named error if the key-encryption key or the fingerprint key
   is unavailable.
3. No secret other than the two bootstrap values is present on the host outside the
   secrets manager.

---

**OPS-SEC-002** — Signing keys, provider credentials, and client secrets SHALL share
one lifecycle with automated rotation and overlap windows. No human step SHALL be
required.

**The key-encryption key and the backup key are rotated separately** (DR-009a,
DR-010): annually, and immediately on suspicion of exposure. They require a human
step because the escrowed copies must be replaced, which is why they belong to the
maintenance exceptions rather than to this automated lifecycle. The key-encryption
key's rotation is the command-line operation OPS-SEC-003 (D-147). Signing-key
cadence, overlap and algorithm are the keys of `10` section 4.9 (AUTH-KEY-001).

*Source: D-148; D-007, D-026.3, D-103, D-147*

**Acceptance criteria**
1. Rotation completes without restart or manual action.
2. Credentials signed or issued under the previous key remain valid through the
   overlap.

---

**OPS-SEC-003** — Rotation of the key-encryption key SHALL be a resumable
command-line operation of `Janus.Cli` (CONV-LAYOUT-001), run under the maintenance
credential (OPS-MIG-003a), and SHALL NOT be an endpoint of the management application.

*Source: D-148; D-147; DR-009a, PRIV-RIGHT-005a, OPS-SEC-001*

**The shape.** The command introduces a new key version in the secrets manager and
records it as current for wrapping. The command itself then re-wraps every subject
key under the new version, in its own process and under the maintenance credential,
which holds the row read and write on the subject-key table and the progress table
that this needs (OPS-MIG-003a, D-148); the old and new key versions are never in the
database, so no database function can do the re-wrap. It runs
independent of the worker (INF-BG-001) and of the application being up, tracking its progress in a table of its own so that
a crash, a restart or a lost connection resumes where it stopped rather than starting
over. The previous version stays usable for unwrapping until the job reports that
every subject key has been re-wrapped; only then is it retired. No stored ciphertext
changes (PRIV-RIGHT-005a: the format marker carries no key version). The same command
produces the escrow copy of the new version for the envelope (DR-009), so the annual
operation in section 9 is one command followed by one physical act.

**Why the command line.** The operation needs the protected maintenance credential
and must not depend on the application being healthy: the case that most needs a
rotation (suspicion of exposure, DR-009a) is also the case in which the application
may be compromised or down.

**Fingerprint key.** Rotation of the fingerprint key (OPS-SEC-001) uses the same
shape (new version, resumable batch job, previous version usable until complete,
escrow copy) but re-computes every stored fingerprint rather than re-wrapping a key.
It is documented as heavy and is expected never to run; it exists so that a suspected
exposure has a procedure rather than an improvisation.

**Acceptance criteria**
1. The operation is a `Janus.Cli` command that requires the maintenance credential
   and is refused with the application's own runtime credential; no endpoint of the
   management application performs it.
2. Killing the process mid-run and starting it again resumes from the recorded
   progress; no subject key is re-wrapped twice and none is left under the old version
   when the job reports complete.
3. Until the job reports complete, values wrapped under the previous version remain
   readable; after it, the previous version is retired and a value still wrapped
   under it (there is none by construction) would fail to unwrap with a named error.
4. The escrow copy of the new version is produced by the same command, and the
   operation is not reported complete until the operator confirms it is sealed
   (DR-009).
5. Start, resume, completion and retirement are audited with the key version, the
   number of subject keys processed and the actor.
6. A test exercises the fingerprint-key rotation in the same shape on a small
   fixture, so the procedure is known to work despite never running in production.

---


## 8. Observability

**OPS-OBS-001** — Every authorization decision SHALL be explicable without a
debugger.

*Source: P-003, AUTHZ-GATE-004*

**Acceptance criteria**
1. A denial resolves to an explanation naming the permission and the missing grant.
2. Explanation requires no elevated privilege for one's own access.

---

**OPS-OBS-002** — Degradations SHALL be visible, never silent. Fallback to an offline
blocklist, a failed provider push, a notification that did not deliver, and a
reconciliation discrepancy SHALL each surface.

*Source: D-011, D-006, D-022, P-003*

**Acceptance criteria**
1. Each listed condition produces a monitored signal.
2. None is recoverable-and-forgotten.

---


**OPS-OBS-003** — Cleanup of expired sessions, consumed tokens, used one-time codes,
and elapsed grace windows SHALL run as background jobs.

*Source: D-007, D-038*

**Acceptance criteria**
1. No recurring human task is required for cleanup.

---

## 9. Maintenance exceptions

Two recurring human tasks exist. Both are named rather than discovered. The
key-encryption key rotation inside the first is the operation OPS-SEC-003 (D-147).
Beside the two tasks, which change the system, three named **reviews** only read
reports and are recorded in the maintenance log (OPS-MAINT-001): the approver report
weekly (`11` section 4), pipeline consumption monthly (OPS-DEP-005), and the
accepted-risk triggers quarterly (`13` RISK-001). A review is not a task: nothing
stops if one is missed, which is why they do not count against the two. Keeping the
public-holiday list current is **not** a third: it is an as-announced edit from the
management app, the system runs without it, and a stale list only makes privacy
deadlines earlier (PRIV-RIGHT-002, D-142).

| Task | Cadence | Source |
|---|---|---|
| Regenerate and reseal the break-glass credential, **rotate the key-encryption key (OPS-SEC-003) and the backup key, inspect the envelope for tampering, and test escrow recovery** | Annual, one operation | D-010, D-084, D-103, D-147 |
| Renew regulatory licence and permits | Licence three years; permits shorter | D-041 |

**OPS-MAINT-001** — The system SHALL track licence and permit expiry dates and warn
in advance. It SHALL keep a **maintenance log**: a dated record, in the management
application, of each recurring human task above when performed and of the monthly
pipeline-consumption review (OPS-DEP-005, CONV-GATE-002), the weekly approver-report
review (`11` section 4) and the quarterly review of accepted-risk triggers (`13`
RISK-001).

*Source: D-148; D-041, D-147*

**Acceptance criteria**
1. Expiry dates are stored and surfaced before they lapse.
2. Warning does not depend on anyone remembering: it is the OPS-ALERT-001 condition,
   raised `maintenance.expiry.warninglead` (default 30 days) before expiry.
3. The maintenance log holds a dated entry, with the actor, for each performed task
   and each review; entries cannot be deleted through the application.

---

## 10. Environment

**OPS-ENV-001** — Development databases SHALL be seeded with realistic principals and
grants.

*Source: D-010*

A development environment where nothing is visible invites a temporary bypass that
reaches production.

**Acceptance criteria**
1. A fresh development database yields a usable, permission-realistic dataset.
2. No bypass flag exists in any environment.

---

**OPS-ENV-002** — Denied-access semantics SHALL be decided once and applied
system-wide, never per endpoint.

*Source: D-016, AUTHZ-CONCEAL-001*

**Acceptance criteria**
1. The behaviour is enforced in shared infrastructure, not endpoint by endpoint.

---

## 11. Open items

None. Cross-references: session and key behaviour in `02-authentication`; the gate
and model builder in `03-authorization`; retention and audit immutability in
`04-privacy`; provider credentials in `05-integrations`; packaging and versioning in
`07-library-contract`.
