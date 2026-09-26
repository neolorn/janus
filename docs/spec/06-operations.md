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

*Source: D-017, D-166*

Without this, a hand-written query on a separate connection silently misses rows
written earlier in the same transaction — a nasty and easily-missed bug in financial
code.

**Acceptance criteria**
1. A hand-written query inside a transaction sees uncommitted writes from that
   transaction.
2. Direct connection retrieval is unreachable from the service layer. In
   `Janus.Storage` only the accessor retrieves a connection, besides the listen/notify
   connection of OPS-DATA-003, which runs nothing but `LISTEN`.
3. A test asserts transaction visibility across both tools: it lives in
   `Janus.Storage.Tests` and uses an entity `Janus.Storage` itself owns (a settings
   row), written through the context and read through the accessor inside one
   transaction (D-154).

---

**OPS-DATA-003** — Direct ADO.NET use SHALL be reserved for database features with
no higher-level abstraction: binary bulk copy, advisory locks, listen/notify,
server-side cursors. It SHALL NOT be used for query optimisation.

*Source: D-041, D-166*

Query optimisation stops at Dapper. Below that is a capability question, not a
performance one.

**Acceptance criteria**
1. Each direct use carries, in the comment nearest above it, the database feature
   requiring it.

---

## 2. Database

**OPS-DB-001** — The database SHALL use ICU collation, with a case-insensitive
collation for identifier columns.

**Values (D-153, D-166).** Database locale `und-x-icu`. The case-insensitive collation
is `identity_ci`, created in the library's schema (OPS-DB-002) as `identity.identity_ci`
`(provider = icu, locale = 'und-u-ks-level2', deterministic = false)`; a column names it
`COLLATE identity.identity_ci`, which the migration placing the column on it writes
itself. It is applied to the plaintext text columns a person spells and the library
compares or sorts: organization names, locked domain names, group names and credential
labels today; a column added later that meets that description takes it (D-155).
Identifiers and personal fields are fingerprints and ciphertext and take no collation.

*Source: D-040, D-166*

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
4. The collation exists in the `identity` schema and in no other.

---

**OPS-DB-002** — The library SHALL own its own schema with its own migration history,
separate from the host's. The history table is `identity.__migrations_history`.

*Source: D-018, D-166*

**Acceptance criteria**
1. Library and host migrations never collide.
2. The library never reads or writes a host table.

---

**OPS-DB-003** — Grant lookup SHALL be supported by a partial index excluding
revoked rows, and reverse lookup ("who can access this?") by its own index.

**Values (D-153).** "Production-scale volume" is the integration fixture of
AUTHZ-TEST-002: 1,000,000 resources, 1,000,000 grants of which 10% are revoked,
100,000 principals, 10,000 groups.

*Source: D-015, AUTHZ-TEST-002, D-166*

**Acceptance criteria**
1. The primary permission predicate uses an index rather than a sequential scan at
   production-scale volume.
2. Reverse lookup completes without a full scan: no table it reads is read
   sequentially, and no index is read end to end; every index read carries an index
   condition.
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

The model is the set of migrations the build declares. A database whose migration
history lacks one of them is behind the model and SHALL be refused; a history holding
a migration the build does not declare is the state of the previous application version
during a rollout (OPS-MIG-005) and SHALL NOT be refused. The check SHALL apply nothing
(OPS-MIG-001).

*Source: D-018, P-003, D-166*

**Acceptance criteria**
1. A schema mismatch produces `model.startup.schemamismatch`, naming the migrations
   owed, and a non-zero exit.
2. The application does not serve requests in a mismatched state.
3. A database whose history holds one migration more than the build declares starts.

---

**OPS-MIG-003** — Two database credentials SHALL exist: one with schema-alteration
rights used only by the migration step, and one for the application with row-level
access only.

**Values (D-157).** The roles are `identity_migrate`, `identity_app` and, for OPS-MIG-003a,
`identity_maintenance`. The migration creates the two runtime roles if absent (`NOLOGIN`;
the deployment attaches credentials, INF-HOST-003) and writes every `GRANT` and `REVOKE`
against those names.

*Source: D-018, D-157*

Nothing in production can alter schema. Same reasoning as the protected-settings
configuration list — controls that would catch a compromise must not be reachable by
it.

**Acceptance criteria**
1. The application credential cannot execute schema-altering statements.
2. The migration credential is not present in application configuration.

---

**OPS-MIG-003a** — A third, **maintenance** credential SHALL exist for scheduled
maintenance that must run outside the pipeline. It has exactly two uses: the audit
partition job, which creates the months ahead and drops the expired (PRIV-RET-002),
and the key rotations of OPS-SEC-003 (D-147, D-148). It SHALL hold no
schema-alteration right of its own. For the partition job it SHALL be granted
`EXECUTE` on `SECURITY DEFINER` functions created by the migration step, each doing one
named thing. For the key-encryption key's rotation it SHALL hold row-level `SELECT` and
`UPDATE` on the subject-key table (the wrapped keys, the deployment's data key among
them, PRIV-RIGHT-005a), read and write on the rotation progress table of OPS-SEC-003,
and the append to the audit trail its records need, because the unwrap and wrap happen
in the command's own process with keys that are never in the database. For the
fingerprint key's rotation it SHALL hold the rights AC4 lists. It SHALL hold nothing
else. The library reads it through the host's secret source at startup (OPS-SEC-001)
for the partition job, and a `Janus.Cli` command reads it from the key document on its
standard input for a rotation (INF-HOST-003); it is never in application
configuration, and a process whose secret source cannot supply it does not start
(`model.startup.secretunavailable`, `details.key` `maintenanceCredential`).

*Source: D-148; D-118, D-147, D-166*

**Acceptance criteria**
1. The maintenance credential cannot issue `DROP`, `ALTER` or `CREATE` directly.
2. Each maintenance function is created by a migration, owned by the migration
   role, and listed in the serialized model output for review.
3. The application credential cannot execute any maintenance function.
4. The maintenance credential can read and update rows of the subject-key table and
   the rotation progress table, and can append to the audit trail (OPS-SEC-003 AC5).
   For the fingerprint key's rotation it can read the key, subject, fingerprint,
   version and encrypted value of the identifiers, the identifier removals, the
   authenticators and the mailboxes and write their fingerprint and version, can read
   the version of each abuse ledger line and of each held username, and can delete
   unspent restriction credit and released username holds under a retired version; it
   never reads a hash. It can read or write no other table. The grants are listed in
   the serialized model output for review.

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

*Source: D-018, D-166*

Both versions run briefly during rollout.

**Acceptance criteria**
1. A new column is added nullable; the constraint is tightened in a later release.
2. A migration is tested against the previous release's application build.
3. Before the first release no previous version exists: a column added by a migration
   of the first release carries its final constraint from that migration.

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
- The toggle is a **repository variable**, `DESTRUCTIVE_DDL_GATE`, set to `enabled` or
  `disabled`; a run that finds it unset, empty or holding another value prints its
  report and fails, naming the variable; the manual workflow is
  `.github/workflows/deploy-destructive.yml` (D-153)

A destructive operation is `DROP TABLE`, `DROP COLUMN`, `DROP SCHEMA`, any
`ALTER ... TYPE`, `TRUNCATE`, `DELETE FROM`, and an added constraint, `SET NOT NULL`,
unique index or column added `NOT NULL` without a default on a table the same
migrations did not create or have filled. Any other match of `08` section 1b is
reported and does not stop the deploy. The migrations judged are those pending on the
target database, read from its migration history table; a pull request reports the
migrations its range adds.

The gate is **currently disabled**; the pipeline is written with it wired in.

*Source: D-019, D-042.1, D-166*

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
5. A run with `DESTRUCTIVE_DDL_GATE` unset, empty or holding a value other than
   `enabled` or `disabled` prints its report and fails, naming the variable.
6. A migration an earlier deploy left pending is reported, and gated, by the next
   deploy.

---

**OPS-DEP-002** — Destructive-operation detection SHALL run on every deploy and
report, regardless of whether the gate is enabled.

*Source: D-019, D-166*

**Acceptance criteria**
1. Every deploy reports which destructive operations the migrations pending on its
   target contain.
2. The report appears whether or not the deploy pauses.

---

**OPS-DEP-003** — When additional people gain deploy access, the gate toggle SHALL be
protected from pull-request modification.

*Source: D-019, D-010, D-042.1*

**Acceptance criteria**
1. The toggle is not modifiable by a pull request.

---

**OPS-DEP-004** — A secret-scanning step SHALL run in the pipeline over the full
history on every push and fail the build on detection.

*Source: D-042.3, D-150, D-167*

GitHub's own secret scanning and push protection are unavailable on this plan for
private repositories, so OPS-SEC-001 has no platform enforcement. The check moves
into the pipeline. The scanner is **gitleaks**, run as its own release at a pinned
version: the step downloads the release archive and checks it against a SHA-256
written in the pipeline beside the version before it runs anything. The official
action is not used, because on a push it scans only the pushed commits and it does not
check what it downloads (D-167). The scan covers every commit of every branch and tag,
on every push, with the default rule set and a committed `.gitleaks.toml` holding only
allow-list entries. Each entry names one file and the one value it exempts, both of
which must match, and gives the reason; an entry never exempts a whole file, a
directory or a shape of value (D-150, D-167). The one exception is the offline
leaked-password list (AUTH-PASS-004): its entry names the file and the list's line form,
a whole line of exactly 40 upper-case hexadecimal characters, because every such line is
the hash of a leaked password and the list is drawn again at every release (D-167). No
other scanner is added.

**Acceptance criteria**
1. A committed credential fails the build.
2. The scanner runs on every push, not only on the default branch.
3. A credential committed in an earlier commit, on any branch, fails the build of a
   later push that does not touch it.
4. An archive whose SHA-256 differs from the pinned one fails the step before the
   scanner runs.
5. A credential added to a file whose allow-list entry names an exact value fails the
   build, unless it is exactly that value. The offline leaked-password list is governed
   by criterion 6.
6. In the offline leaked-password list, a whole line of 40 upper-case hexadecimal
   characters passes the scan; any other matched text in that file, and the same hash in
   any other file, fails the build.

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
control at runtime needs no step-up and no `system:administer`; like every change it
carries a reason and an audit entry (OPS-CFG-008). **Loosening** SHALL require step-up
authentication, a written reason, an audit entry, and `system:administer` in the
administrative organization beside the route's own permission (`10` section 2.1). A
change of an organization's policy is the `policy:change` step-up action whatever its
direction, as every edit of the named restriction set is `restriction:edit`
(OPS-CFG-008); a loosening of it also requires `system:administer`. A change without a
reason is refused with `config.change.reasonrequired`, `details.key` naming the setting.

*Source: D-010, D-166*

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
4. All are audited, and each carries a reason; one without is refused with
   `config.change.reasonrequired`.
5. A loosening by a caller without `system:administer` in the administrative
   organization is refused with `authz.denied`, whatever other permission the caller
   holds.
6. The direction of a change is decided on the value in force when it is written; a
   concurrent change cannot turn a tightening into a loosening.

---

**OPS-CFG-003** — Hard floors SHALL be enforced in code. Values outside them SHALL be
**rejected at validation, not silently clamped**.

A value whose feature needs a host declaration (the policy field `photos` needs an
image codec; a domain listed in an organization's lock needs a DNS resolver) SHALL be
refused at the change while the declaration is absent, with `config.value.notallowed`
and `details.requires` naming the declaration (`imageCodec`, `dnsResolver`). Bootstrap
never writes such a value on (OPS-BOOT-001), and startup refuses a stored one whose
declaration is absent with `model.startup.declarationmissing` naming the declaration.

*Source: D-010, AUTH-SESS-005, D-166*

**Acceptance criteria**
1. A password floor below the standard's minimum is rejected.
2. A session absolute timeout above the maximum is rejected with a named error.
3. Rejection occurs at startup or at the point of change, not at first use.
4. With no image codec declared, a policy change setting `photos` to `true` is refused
   with `config.value.notallowed` and `details.requires` naming the codec, and a
   deployment whose stored policy shows photos does not start; the same holds for a
   domain listed in a lock with no DNS resolver registered.

---

**OPS-CFG-004** — The following SHALL NOT be changeable **through the application**.

- **Disabling export auditing** (`exfiltration.export.auditing`)
- Disabling rate limiting entirely (`abuse.throttle.enabled`)
- The relying party identifier (`webauthn.rpid`)
- Token signing algorithm (`token.signing.algorithm`)
- `privacy.calendar.timezone`, the time zone of the legal clock
- `legal.governinglanguage`, the default governing language of legal documents
  (PRIV-CONS-005): changed from the server or by redeployment, and the change raises a
  Normal alert as well as the protected-setting alert (OPS-ALERT-001)
- The facts the deployment declares about itself: the WebAuthn origins
  (`webauthn.origins`) and algorithms (`webauthn.algorithms`), the hosting location
  (`hosting.location`) and cross-border basis (`hosting.crossborderbasis`), the shipped
  transports' endpoints (`integration.mail.endpoint`, `integration.sms.endpoint`), the
  mail server adapter's endpoint (`integration.mailserver.endpoint`) and the default
  client (`redirect.defaultclient`)

Audit logging, token signature verification and step-up enforcement have no switch:
the library performs them unconditionally. The emergency route past step-up is the
break-glass session (AUTH-STEP-004).

This list and `10` section 4.8 are the same list; a key marked protected in `10`
appears in both or in neither (D-152).

*Source: D-148; D-010, D-020.1, D-045, D-146, D-152, D-166*

The selection test is not "how sensitive is this setting" but **"does turning this
off blind us to the person turning it off."** The governing language is protected on a
different ground: it fixes which text of every legal document binds, so it changes
from the server with a recorded alert rather than from a session (D-146). The legal
clock's time zone (D-153) and the facts the deployment declares about itself (D-162)
are protected by their own decisions and are on this list because the list and `10`
section 4.8 are one (D-152).

**Acceptance criteria**
1. No runtime API modifies any listed setting.
2. Changing one requires access the application itself does not have; a command-line
   operation restricted to the server satisfies this as well as a redeployment
   (D-071).
3. The command-line operation is `configure` of `Janus.Cli`: it takes protected keys
   only, each as `--<key> <value>`, and a non-empty `--reason` (a change without one is
   refused with `config.change.reasonrequired`); it asks no step-up; each change is
   recorded under the system principal `configure` (reason `OPS-CFG-004`) and raises
   `protected-setting-changed` per key; a change that the start's own checks would
   refuse (the relying-party rule, the endpoint rule of INT-GEN-001, the signing
   algorithm the signing keys hold, the client registry) is refused with their code and
   nothing of it is written.

---

**OPS-CFG-005** — Configuration changes SHALL be audited exactly as permission grants
are — who, what, from, to, when, why — with the same retention.

Every change is recorded as `ops.configuration.changed` with `key`, `before` (the value
in force: the key's default where no value stood, null only for a key with no default
and no value), `after`, `loosening` and `reason`. A value set by bootstrap
(OPS-BOOT-001) or from the server (OPS-CFG-004) is written by the one writer of
protected keys and recorded under that command's system principal; a value set where
the key held neither a value nor a default is no loosening.

*Source: D-010, D-166*

**Acceptance criteria**
1. Every change produces an audit record with before and after values.
2. Records are queryable by setting and by actor, a system principal by its name.
3. Every value bootstrap sets appears in the audit trail under the `bootstrap`
   principal.

---

### 5.2 System administration

**OPS-CFG-006** — System administration SHALL be a **permission**, never implied by
organizational seniority or ownership.

*Source: D-010, D-166*

**Acceptance criteria**
1. A member holding every library-owned permission in an organization other than the
   administrative organization, and no grant of `system:administer` in the
   administrative organization, cannot make a loosening change (OPS-CFG-002). No one
   changes a protected setting (OPS-CFG-004) through the application.
2. The permission is granted, never inherited: it is held only through a grant, to the
   account or to a group the account belongs to, of a role allowing it on the
   administrative organization itself. Membership of that organization, a grant on a
   record within it, and any grant in another organization confer nothing.

---

**OPS-CFG-007** — Granting or revoking a role that carries `system:administer`, an
allow or a deny, in any organization, SHALL require `system:administer` in the
administrative organization. A role that cannot be read counts as carrying it.
Changing a role that carries `system:administer` before or after the change, and
changing the members of a group that holds, itself or through a group holding it, a
live grant (allow or deny) of such a role or of a role that cannot be read, SHALL
require it likewise.

*Source: D-010, D-166*

Self-referential deliberately. Without it, any administrator who can manage grants is
a system administrator one step removed, and the distinction is decorative.

**Acceptance criteria**
1. A grant-managing administrator without the permission cannot confer it.
2. A deny of such a role, and a grant of it in an organization other than the
   administrative one, are refused to a caller without `system:administer`.
3. A role manager without the permission cannot add it to a role, take it out of one,
   or change a role that carries it.
4. A group manager without the permission cannot change the members of a group that
   reaches a grant of a role carrying it.

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
and the fingerprint key are then read through the host's secret source by the startup
hosted service, before the server serves (OPS-SEC-001), and only after that can the
settings table be read.

*Source: D-071, D-105, D-166*

**Reading a stored value (D-162, D-166).** A stored value SHALL be read under its key's
type and constraints (`10` section 4). A value that does not read SHALL be a fault
(CONV-ERR-001): the read throws, naming the key and never the stored text, and no
default and no other value stands in for it. A request fails as `system.fault`
(BFF-ERR-002) and a job fails its run (`background-job-failed`).

**The named restriction set is runtime configuration** (`restrictions`,
AUTH-ABUSE-004), edited through `GET/PUT/DELETE /admin/restrictions/{name}` exactly as
the public-holiday list is (D-142): the change applies to the next send with no
restart, is audited under OPS-CFG-005 and emits `SendingRestrictionChanged`. A **loosening**
(any change to the set that `10` section 4.5 does not classify as a tightening) falls
under OPS-CFG-002 (step-up `restriction:edit`, `system:administer`, a reason, an audit
entry) and raises a Normal alert (OPS-ALERT-001); a tightening needs the step-up, a
reason and the audit entry. A grant of credit to a key
(`POST /admin/restrictions/{name}/grant`) is not a
configuration change: it is a support action, stepped up, audited with a reason and
alerted (`SendingRestrictionGranted`).

*Source: D-146, D-166*

**One writer (D-162, D-166).** Every runtime change SHALL be written through one
configuration operation, which takes the access context and the reason, classifies the
change's direction by `10` section 4 on the value in force (OPS-CFG-002 AC6),
applies OPS-CFG-002, and writes the value and the audit record of OPS-CFG-005 in one
transaction. The named restriction set, the alert destinations and an organization's
policy are changed through it; nothing else in the application writes a runtime
setting. From the server, bootstrap and `configure` write through the one writer of
protected keys (OPS-CFG-005).

**Acceptance criteria**
1. Changing a runtime setting requires no restart.
2. Every change carries actor, before and after values, timestamp and reason.
3. Neither bootstrap value, nor any key fetched at startup, is stored in the database.
4. Editing a restriction applies to the next send without a restart, is audited with
   before and after values, and a loosening raises the Normal alert.
5. A settings row whose value does not read under its key fails, as a fault, the
   operation or job that reads it; nothing proceeds on the key's default.
6. A runtime setting changed in process and one changed over `PUT /admin/config/{key}`
   meet the same rule and produce the same audit record.
7. In the application, nothing but the one configuration operation writes a runtime
   setting, for a key or for a member of a key family (`policy.<organization>`).

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
| **Break-glass credential generated**, a first issue or a replacement; delivered to the owner's destinations whatever `alerting.owner.enabled` holds | High | OPS-BOOT-004 |
| **Configuration change to a protected setting** | High | OPS-CFG-004 |
| **Alert destination changed** — delivered to the *previous* destinations | High | OPS-ALERT-004a |
| **Step-up policy weakened** (a policy's gates, or `exfiltration.export.stepuprequired` turned off) | High — the same lever as an alert destination, under a different name | D-083, OPS-ALERT-006 |
| **Implausible concurrent sessions for one account** | High | D-051 |
| Spike in permission denials | Normal | AUTHZ-GATE-004 |
| Gateway balance drain or floor breach | Normal | INT-SMS-004 |
| **Background job failure** | Normal | INF-BG-001 |
| **Erasure or takedown delivery exhausted its retries** | High | IDN-LIFE-003a |
| **Certificate renewal failure** | High — a total-outage precursor | INF-TLS-003 |
| **Host clock drift beyond tolerance** | Normal | INF-HOST-001 |
| **Degradation: blocklist fallback, failed provider push, undelivered notification, reconciliation drift, registration channel lost, a watch the environment does not supply** | Normal | OPS-OBS-002 |
| **Repeated callback verification failure** | Normal | BFF-MACH-003 |
| **Unusual rate of duplicate-identifier notifications** — an enumeration probe | Normal | AUTH-ABUSE-003, D-121 |
| **Privacy-request decision deadline approaching** — `privacy.request.warninglead` before it | Normal | PRIV-RIGHT-002, D-126 |
| **Privacy-request decision deadline reached** — undecided on the day | High | PRIV-RIGHT-002, D-126 |
| **Licence, permit, key-encryption-key cryptoperiod or annual envelope operation approaching** — within `maintenance.expiry.warninglead` | Normal | OPS-MAINT-001, DR-009a, D-121 |
| **Holiday list running out** — no `privacy.holidays` date lies beyond `maintenance.expiry.warninglead`; deadlines after the last date are counted without holidays (compliant, earlier) | Normal | PRIV-RIGHT-002, D-142 |
| **No emergency credential exists** — non-dismissable until one is generated | High | OPS-BOOT-001, D-133, D-140 |
| **Restore test failed or exceeded the recovery-time objective** | High | DR-007, D-140 |
| **Restriction loosened**: any change to the named restriction set that `10` section 4.5 does not classify as a tightening | Normal | AUTH-ABUSE-004, OPS-CFG-008, D-146 |
| **Restriction grant issued**: credit added to a key by support | Normal | AUTH-ABUSE-004, D-146 |
| **Domain re-verification failed**: a locked domain's DNS record no longer verifies; nothing is revoked | Normal | REG-DOM-001, D-146 |
| **Domain removed from a lock**: new sign-ins with addresses in it stop | Normal | REG-DOM-001, D-146 |
| **`legal.governinglanguage` changed**, raised beside `protected-setting-changed` | Normal | PRIV-CONS-005, OPS-CFG-004, D-146 |
| **Governing-language text missing**: a document version cannot publish, or a document that must be shown has no governing-language text | Normal | PRIV-CONS-006, D-146 |

*Source: D-048, D-071, D-121, D-146, D-147, D-153, D-166*

**Identifiers and thresholds (D-153).** Every row carries the identifier `10` section
5.23 lists, in table order; `AlertRaised` carries it and OPS-ALERT-002 deduplicates on
it. Where a row's condition is a rate, the number is a key in `10` section 4.5:
sustained failures `alerting.authfailures.threshold`; recovery clustering
`alerting.recovery.accountthreshold`; approver volume
`alerting.recovery.approverthreshold`; read volume `exfiltration.readvolume.factor`
and `.minimum` (OPS-ALERT-005); implausible sessions `alerting.sessions.distance` and
`.window` (OPS-ALERT-007); denial spike `alerting.denials.threshold`, per actor;
duplicate-identifier notices `alerting.nonexistent.threshold`; callback failures
`alerting.callback.threshold`; balance drain `abuse.sms.drainfactor`; restore test
`backup.restoretest.objective`. A job that has not run raises `background-job-failed`
when its last success is older than twice its interval (INF-BG-001). The condition
`relay-domain-unregistered` (INT-MAIL-011, Normal) is also on the list.

**Carrying (D-166).** A raised condition SHALL be written, with the structured details
of its row, in the transaction that raised it, together with its `AlertRaised` event
row (`10` section 5b); failing to write either fails the raise, and every raise site
goes through the alert channels, bootstrap and `configure` included. The alert
channels SHALL carry it to the destinations after that transaction commits, oldest
first, on the job `alert-dispatch` every `outbox.poll.interval` (ceiling `PT1M`), each
removed in the transaction that records its delivery. A transaction that rolls back
raises nothing. The notice of OPS-ALERT-004a is delivered before its change takes
effect, and the lapse of `alert-dispatch` itself (`background-job-failed`) is delivered
by the alert channels directly from the worker, so a stalled carrier still reports
itself.

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
3. A condition raised in a transaction that rolls back reaches no destination; one
   raised in a transaction that commits reaches them on the next pass of the alert
   channels, once, and writes one `AlertRaised`.
4. With `alert-dispatch` stalled, its lapse still reaches the destinations.

---

**OPS-ALERT-002** — Alerts SHALL be **deduplicated per condition per window**.

**Values (D-153).** "Sustained" is `alerting.authfailures.threshold` failures against
one account inside `alerting.dedupe.window`; the deduplication key is the condition
identifier of `10` section 5.23 plus the account or actor the row names.

*Source: D-048, D-166*

One alert per sustained attack, never one per attempt. Without this an attacker
triggers alerts to drain the prepaid SMS balance — turning the alerting into the
attack.

**Acceptance criteria**
1. A thousand failed attempts against one account produce one alert.
2. Two overlapping passes of the alert channels that reach one condition inside one
   window deliver it once: the deduplication key is claimed by one conditional write.

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

  Alert-class sends are likewise outside every named sending restriction
  (AUTH-ABUSE-004): no restriction counts or refuses one, and deduplication
  (OPS-ALERT-002) alone governs them, so nobody can silence an alert by exhausting a
  limit. An alert carries no source address, so no `source` restriction could count it.
- **Mail-system alerts are SMS-first.** An alert about the mail system arriving by
  mail is a loop — out-of-band routing is the standard rule for exactly this

*Source: D-048, D-071, D-166*

**Acceptance criteria**
1. A mail-system failure still produces a reachable alert.
2. A balance-floor breach is reported despite sends being stopped.
3. Alert-class sends continue below the floor while ordinary sends are refused.
4. The residual case — balance actually at zero — is recorded as unreachable by SMS,
   with email carrying alone.
5. An alert is sent to a destination whose every named restriction is exhausted; only
   deduplication folds it.

---

**OPS-ALERT-004a** — Changing an alert destination SHALL notify **every previous
destination**, non-suppressibly, and SHALL raise a **High** alert to those previous
destinations.

The change SHALL be made through the configuration operation of OPS-CFG-008, as a
change with no direction (OPS-CFG-002), and audited with its before and after values
(D-162).

*Source: D-083, D-166*

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
5. A change that would leave a channel's destination list empty is refused with `config.value.lastdestination` (D-153).
6. A destination change produces an audit record with before and after values, as
   every runtime change does.

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

**Values (D-153).** A read is one record returned to the actor by a gate-filtered
query or an export, counted per calendar day in `privacy.calendar.timezone`. The
alert fires when today's count exceeds `exfiltration.readvolume.factor` times the
actor's mean daily count over `exfiltration.readvolume.baselinewindow` **and** exceeds
`exfiltration.readvolume.minimum`; the minimum keeps a low-volume actor's first busy
day silent. The host reports the records each gate-filtered query and each export
returned through `IReadVolume`; the library's own routes run no gate-filtered query.
The actor is the acting person; work of a system principal is not counted. The mean is
the sum over the days of the window before today divided by the window's length, a day
without reads counting as zero; both comparisons are strict. Counts older than the
window are forgotten by the daily recount.

*Source: D-045, D-071, D-166*

A warehouse clerk's normal differs from an account manager's.

**Acceptance criteria**
1. An actor reading far beyond their own pattern raises an alert.
2. An actor whose normal volume is high does not alert continuously.

---

**OPS-ALERT-006** — Export operations SHALL be defined, gated by step-up,
individually audited, and rate-limited.

**Values (D-166).** An export operation is a host-declared permission whose action is
`export`; the library declares none, and `/privacy/export` is not one. Each admitted
check, list filter or SQL fragment exercising one is one export. The gate applies, in
order, restriction, grants, the bound gate (or, while
`exfiltration.export.stepuprequired` is on, a gate named by the export, costing the
strictest of the policy's gates), consent, the limit (`exfiltration.export.ratelimit`
per actor over a rolling hour, refused with `auth.throttled` and `retryAt`) and the
audit row `authz.access.exported`. A system principal meets no gate and cannot export
while the key is on. The limit's records of an actor are cleared by the expiry sweep
once they are an hour old (OPS-OBS-003).

*Source: D-045, D-166*

Bulk export is the exfiltration mechanism; ordinary browsing is not.

**Acceptance criteria**
1. "Export" is an enumerated set of operations, not a judgement.
2. Export auditing cannot be disabled through the application (OPS-CFG-004).
3. Each admitted export is recorded individually, naming the permission and the
   resource type.
4. The sixth export of one actor inside an hour is refused with `auth.throttled`.
5. Turning `exfiltration.export.stepuprequired` off raises `stepup-policy-weakened`.

---

**OPS-ALERT-007** — Concurrent sessions for one account from implausibly distant
origins SHALL raise an alert.

**Values (D-153, D-166).** Implausible means two sessions of one account both used
inside `alerting.sessions.window` whose resolved places (AUTH-SESS-013, INT-GEN-006)
are further apart than `alerting.sessions.distance` or lie in different countries. Two
places whose known countries differ are implausible whatever their cities; distance is
the great-circle distance between the two cities' coordinates, measured only where
both places name a city. The same city, the same country with a city unknown on either
side, or a place with no country, never alerts. Two sessions are compared when a use
begins a stretch (a session begun, a use from another city than its last, or a use
after a pause longer than the window); sessions on one record are one session. The
alert names the account and the two sessions and neither place.

*Source: D-051, D-166*

Attribution is load-bearing for volume alerting, approver anomaly detection, audit
records and offboarding verification. Shared credentials break all four silently.

**Acceptance criteria**
1. Simultaneous sessions from implausible origins alert.
2. Ordinary multi-device use does not.

---

## 6. Bootstrap

**OPS-BOOT-001** — A fresh deployment SHALL be initialised by a command-line
operation run against the database, creating the first organization, the first
system administrator, an enrolment link for that administrator, the reserved
**`emergency`** account (OPS-BOOT-002) with no credential, and the restore-test canary
subject of DR-007 (recorded in `backup.restoretest.canary`). **It SHALL NOT
generate the break-glass credential**: that is issued from the management application
(OPS-BOOT-004), so the secret never touches a terminal, a log or a file.

**Values (D-153, D-166).** The command is `bootstrap`, the first argument of the
`Janus.Cli` executable, which a deployment installs under any name (CONV-NAME-001).
Every other argument is `--<name> <value>`: `--organization`, `--email`, `--phone`,
`--dateofbirth` (`YYYY-MM-DD`, required whatever `registration.adultaffirmation`
says) and each required deployment value by its key name (`10` section 4 preamble). A
key that is not required is refused, and so is a name given twice, without a value or
not beginning `--`, each with `api.request.malformed` naming it; a value its key does
not admit is refused with that key's code before the database is reached, and a
`webauthn.*` value that the relying-party rule of AUTH-FACT-010 and AUTH-FACT-012
refuses is refused with the code startup gives; the set is judged complete by the rule
the host's startup applies. The administrator's answer to the age screen is derived as
REG-PROF-002 derives it: under `required` a date under eighteen refuses the command with
`identity.profile.underage` and nothing is committed; under `off` the age group is
recorded; the date is kept only where `profile.dateofbirth` is on. No terms step runs,
and the account names no terms or notice version.

`--mailbox <address>` is given exactly where the deployment integrates the mail server
(REG-MAIL-001): the address becomes the administrator's primary, locked, verified
corporate email, and a mailbox is reserved at it and queued (INT-MAIL-006 AC1a). An
address equal to `--email` is refused with `identity.identifier.invalid` naming
`mailbox`. Without it no mailbox is queued.

The enrolment link is printed once to the command's standard output and sent nowhere,
as `<origin>/link#enrolment.<token>`: the origin is the first entry of
`webauthn.origins`, which the deployment lists first as the authentication
application's origin, since the command reads no host declaration; the token sits in the
fragment, so no request, log or referrer carries it. It lives for
`recovery.link.lifetime`. Exit code 0 on success, with the address alone on standard
output; 1 on refusal, with one JSON line `{"code": ..., "details": {...}}` on standard
error and nothing on standard output.

*Source: D-028, D-133, D-166*

Whoever runs the deployment already holds server access, which is strictly more
privileged than anything the first account can do. No new secret is created, nothing
is transmitted, and there is no race window.

The command needs only the database. Where `--mailbox` is given, the first
administrator's mailbox is **queued** for provisioning through the outbox and created
when the mail server is reachable (INT-MAIL-006 AC1a); `emergency` gets none
(INT-MAIL-006 AC1b, D-144).

Bootstrap attaches its three memberships (the first administrator, `emergency` and the
restore-test canary) directly and emits `MembershipChanged` for each. The grants it
makes name no person as granter: the granter recorded is the nil subject and the
reason is `OPS-BOOT-001`. The alert of AC3 is written to the raised-alerts outbox in the
bootstrap transaction and carried by the delivery job (OPS-ALERT-001); the command
sends nothing. Bootstrap never writes on a value whose feature needs a host
declaration (OPS-CFG-003): the administrative organization's policy is written with
`photos` false (`10` section 4.1a), and enabling photos is an administrator's policy
change once an image codec is declared.

Bootstrap is audited under the deployment-scoped system principal `bootstrap` (reason
`OPS-BOOT-001`, operation `bootstrap`): `identity.organization.created` for the
administrative organization, `authz.role.defined` for each role it defines with
`before` empty, and `ops.configuration.changed` for each value it sets (OPS-CFG-005),
all in the bootstrap transaction.

**The command SHALL require `legal.governinglanguage`** (PRIV-CONS-005) beside the
deployment values of LIB-HOST-001 that have no default: it is one of the values that
name the deployment (`10` section 4), and it is protected from then on (OPS-CFG-004).

*Source: D-146, D-166*

**Acceptance criteria**
1. Running it on a deployment already stood up (the administrative organization
   exists, or an allow grant of a role holding `system:administer` stands unrevoked,
   expired or not) is refused with `authz.denied`, and nothing is written.
2. No default account with a known credential is created at any point.
3. The command emits no break-glass credential. While no credential stands (none
   generated, or the last one used), a non-dismissable High alert (illustrative
   wording: *"no emergency credential exists"*) is raised on OPS-ALERT-001 at every
   window of OPS-ALERT-002, and `GET /admin/break-glass` answers `standing: false` so
   the management application shows it to every system administrator.
4. Bootstrap without `legal.governinglanguage` is refused with
   `model.startup.declarationmissing` naming it.
5. Bootstrap refuses a `webauthn.*` value that the relying-party rule of AUTH-FACT-010
   and AUTH-FACT-012 refuses, with the code startup gives, before the database is
   reached.
6. Bootstrap under `registration.adultaffirmation` = `required` with a date of birth
   under eighteen is refused with `identity.profile.underage` and creates nothing.
7. Both `system-administrator` grants bootstrap makes name the nil subject as granter
   and `OPS-BOOT-001` as reason; no grant names its holder as granter.

---

**OPS-BOOT-002** — The break-glass credential SHALL be single-use, grant a
**time-boxed** system administration session, and trigger maximum-noise alerting on
every available channel when used.

*Source: D-010*

**The session belongs to a reserved account, `emergency`** — created at bootstrap
(OPS-BOOT-001), a member of the administrative organization holding the
`system-administrator` role, with **no sign-in method of any kind**: the sealed
credential is its only way in, and no factor can be enrolled on it. The account SHALL
carry the reserved-account mark, set by bootstrap and by nothing else; the database
SHALL admit at most one account so marked. Every action in a break-glass session is
audited with `emergency` as acting and effective identity (AUTHZ-IMP-001), the account
the action concerns as the record's subject (IDN-AUD-001), and the reason the owner
states with the credential at `/break-glass`; it appears in no device
list, holds no mailbox, and cannot be granted anything further, suspended, or deleted;
its `system-administrator` grant cannot be revoked, and the role it holds keeps every
library-owned permission (`10` section 3). Each of those changes, and deactivating the
account, taking it down or adding it to a group, SHALL be refused with 403
`authz.denied`, and so SHALL, from the break-glass session, the step-up actions
`password:set`, `identifier:add`, `username:change`, `factor:enrol`, `provider:link`,
`recoverycodes:generate`, `mailcredential:create`, `account:deactivate` and
`account:delete`, whatever the reserved account's policy lists. It holds a subject key
like any account, so what a break-glass session records under it is sealed as any
account's is. It may approve any recovery, including the sole administrator's
(AUTH-RECOV-002a). The session SHALL
satisfy step-up for its lifetime (AUTH-STEP-004). Lifetime is configurable with an
enforced ceiling (D-138): the session ends `breakglass.session.lifetime` after the use
that opened it, whatever the policy's absolute lifetime, and its inactivity window is
the one AUTH-SESS-005 gives the reserved account's policy (`session.aal2.inactivity`
under the administrative organization's `aal2`), never longer than that lifetime. An
inactivity expiry ends it for good: the reserved account holds no factor to restore it
with.

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
FE-BG-001), reached by the address printed on the envelope: one field for the
credential, one field for the reason, one button. The endpoint
(`POST /auth/break-glass`) sits behind it and takes both; the reason is required, a
free-text member under API-CONV-002, and kept with the session. The owner is not technical
(`12` §1); an API endpoint is not a procedure they can follow.

*Source: D-065, D-129, D-166*

**Acceptance criteria**
1. Use consumes it; a second attempt fails.
2. The session expires automatically at a configured lifetime within the ceiling, and
   earlier after its inactivity window without use; either expiry is answered
   `auth.session.expired` with `details.reauthenticate` `full`.
3. Use raises alerts immediately, on every channel, to both owner and operator —
   the owner's destinations receive it with `alerting.owner.enabled` off.
4. The session can approve a recovery and grant system administration.
5. Bootstrap without `alerting.owner.email` and `alerting.owner.sms` is refused.
6. From the break-glass session, opening the management application requires no
   further credential.
7. A change that would take a library-owned permission out of the role the
   `emergency` account holds, or revoke its `system-administrator` grant, is refused
   with `authz.denied` and changes nothing.
8. A second account carrying the reserved-account mark is refused by the database.
9. From a break-glass session each step-up action listed above is refused with
   `authz.denied`; an administrator's suspension, takedown, grant or group addition
   naming the reserved account is refused the same way.
10. Every audit record written in a break-glass session carries the reason given at
    its use.

---

**OPS-BOOT-004** — The break-glass credential, first issue and every replacement,
SHALL be generated from the management application, by a stepped-up system
administrator **or from within a break-glass session**, and rendered **exactly once**
as a printable page: the code in check-charactered groups of four, a QR code of the
code alone (never a link), the `/break-glass` address, and the two-line instruction
for the owner. The system stores **only a hash**: Argon2id over the code's canonical
form (the 36 symbols with case and the confusable letters folded, hyphens and spaces
removed) under the `password.argon2.*` parameters in force at generation, which the hash
carries; the code is held **on paper only**, never in the secrets manager, never
emailed, never written to a file.

*Source: D-148; D-065, D-133, D-147, D-166*

**Strength and throttle (D-147, D-153).** The credential SHALL carry at least 128 bits of
entropy, drawn from a typeable alphabet (no characters that are confused in print or
absent from a common keyboard) and rendered in the check-charactered groups above, so
a transcription error is caught before submission. The alphabet is Crockford base32
(digits and upper-case letters without I, L, O and U; input folds case and maps i and
l to 1 and o to 0). The code is 27 data symbols (135 bits) in 9 groups; each group is
3 data symbols and 1 check symbol equal to the weighted sum of the group's symbol
values (weights 1, 2, 3) modulo 32; printed as 36 symbols in groups of four separated
by hyphens. Attempts at `/auth/break-glass`
SHALL be source-throttled per AUTH-ABUSE-001 and, in addition, limited to at most 5
attempts per hour globally across all sources. Every attempt SHALL count toward the
global limit when it arrives, before the per-source delay, the check symbols or any
hash is looked at, an attempt the limit refuses included; a refusal by the limit is 429
`auth.throttled` with `details.retryAt` one hour after the refused attempt. The first
attempt the limit refuses SHALL raise `auth-failures-sustained` for the reserved
account. With 128 bits behind it, the global limit exists to make the attack loud, not
to make it infeasible.

The four other envelope items (DR-009) live in both the envelope and the secrets
manager because the system needs them daily. The break-glass code is needed by
nobody on an ordinary day, and its whole purpose is to exist outside the operator's
reach; a copy in the operator's vault would defeat it.

The bootstrap command generates no credential (OPS-BOOT-001) and refuses on a
deployment already stood up, so without this, one use exhausts the mechanism until the
operator returns.

**Accepted consequence:** a break-glass holder can mint unlimited future credentials.
They already hold full administrative access at that moment, so this grants nothing
new. Alerting and audit are the controls.

**Acceptance criteria**
1. A break-glass session can generate and seal a replacement.
2. Generation is audited and alerted as `breakglass-generated`, to the owner
   regardless of `alerting.owner.enabled` (OPS-BOOT-002).
3. The rendered page cannot be reopened; a second request generates a new
   credential and invalidates the previous one.
4. No plaintext break-glass credential exists in the database, the secrets manager,
   logs, mail, or any file.
5. The `/break-glass` page accepts the code by typing or by scanning the QR.
6. The generated code carries at least 128 bits of entropy from a typeable alphabet;
   a group with a wrong check character is refused before the hash is compared.
7. A sixth attempt within one hour at `/auth/break-glass`, from any source, is
   refused and counted, and the per-source throttle of AUTH-ABUSE-001 applies as well;
   the first such refusal raises `auth-failures-sustained` for the reserved account.

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
secrets manager at startup; both are versioned, a set of versions with one current.
Every other secret the library holds SHALL be encrypted at rest under a key the
key-encryption key wraps as a row of the subject-key table (PRIV-RIGHT-005a): a secret
that belongs to an account, its TOTP secret among them, under that account's subject
key, destroyed with it; every other (the signing keys, the registry's client secrets,
any value that belongs to no subject) under the deployment's data key (AUTH-KEY-002).
No secret SHALL reside in configuration files or environment
variables in production, **except the two deployment-injected bootstrap values**
named in INF-HOST-003 — the database connection and the secrets-manager credential.

**The key-encryption key, the fingerprint key and the backup private key SHALL
additionally be escrowed** with the break-glass credential (DR-009), so a restore is
possible when the operator is unreachable.

**The secret path (D-166).** The library SHALL read every secret it needs through the
host's secret source (`ISecretSource`, LIB-EXT-001), asynchronously, in its startup
hosted service before the server serves: the key-encryption key versions, the
fingerprint key versions, the maintenance credential (OPS-MIG-003a), the mail server's
secret where the shipped mail-server adapter is used (INT-MAIL-001), and each declared
social provider's credential by provider name. No secret is an argument of `AddJanus`.
A secret the source cannot supply stops startup with `model.startup.secretunavailable`,
`details.key` naming it (`keyEncryptionKeys`, `fingerprintKeys`,
`maintenanceCredential`, `mailServerSecret`, `socialProvider.<provider>`, or `input`
for a command's document).

A `Janus.Cli` command SHALL read its database connection and every key version it needs
from one JSON document on standard input, the same document the secret source serves,
SHALL refuse to run where standard input is a terminal, SHALL take none of them as an
argument, a file or an environment variable, and SHALL clear every key when it ends. A
command the executable does not carry is refused with one JSON line
`api.request.malformed` naming it on standard error, and exit code 1.

*Source: D-026.3, D-069, D-103, D-105, D-166*

**Acceptance criteria**
1. No secret value appears in any repository file or image layer.
2. Startup fails with `model.startup.secretunavailable`, `details.key` naming the
   secret, where a secret the deployment needs cannot be read, or where any version of
   the fingerprint key is shorter than 32 bytes; the server serves no request before
   every secret is read.
3. No secret other than the two bootstrap values is present on the host outside the
   secrets manager.
4. A command whose standard input is a terminal, or whose document holds no usable
   key, is refused with `model.startup.secretunavailable`, `details.key` naming `input`
   or the missing member; a document that is not JSON or exceeds 64 KiB is refused with
   `api.request.malformed` naming `input`; no refusal carries anything of the document.

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

**Client secrets (D-166).** The library generates the secret of every client in the
registry when the client is registered (32 random bytes, base64url), holds it wrapped
under the deployment's data key (PRIV-RIGHT-005a) as it holds a signing key, and
rotates it with the signing keys: the first read of a secret issued
`token.signing.rotation` ago or more replaces it, and the replaced secret is accepted
for `oidc.accesstoken.lifetime` plus 5 minutes. The library's own client middleware
reads its application's current secret from the registry at each code exchange
(BFF-SESS-006). No deployment supplies, stores or restarts for a client secret, and no
sign-on secret exists; the key-encryption key's rotation re-wraps the key it sits
under (OPS-SEC-003). A provider credential that expires is renewed by the library
where the provider lets the client sign it (IDN-LIFE-012); no calendar renewal is a
maintenance task.

*Source: D-148; D-007, D-026.3, D-103, D-147, D-166*

**Acceptance criteria**
1. Rotation completes without restart or manual action.
2. Credentials signed or issued under the previous key remain valid through the
   overlap.
3. A client secret older than `token.signing.rotation` is replaced at its next use with
   no restart and no command, and the secret it replaced authenticates the client until
   `oidc.accesstoken.lifetime` plus 5 minutes have passed.
4. No client secret is held in the clear in the database, the secrets manager,
   configuration or a command's arguments.

---

**OPS-SEC-003** — Rotation of the key-encryption key SHALL be a resumable
command-line operation of `Janus.Cli` (CONV-LAYOUT-001), run under the maintenance
credential (OPS-MIG-003a), and SHALL NOT be an endpoint of the management application.

**Values (D-153, D-166).** The commands are `rotate-kek` and `rotate-fingerprint-key`
of `Janus.Cli`. The batch is 500 subject keys per transaction ordered
by subject identifier; the progress row holds the key version, the last subject
identifier processed, the processed count, and the started, completed and retired
instants.

*Source: D-148; D-147; DR-009a, PRIV-RIGHT-005a, OPS-SEC-001, D-166*

**The shape.** The operator adds the new key version to the secrets manager as
current, keeping the previous one, and restarts the application on it; `rotate-kek`,
handed the key document, records the rotation to that version and re-wraps. The
command itself re-wraps every row of the subject-key table under the new version: every
subject key, and the deployment's data key, under which every value that belongs to
no subject is held (PRIV-RIGHT-005a), the signing keys and the registry's client
secrets among them. A row is written back only where it still holds the value the
command read. The re-wrap runs in the command's own process and under the maintenance
credential, which holds the row read and write on the subject-key table and the
progress table that this needs (OPS-MIG-003a, D-148); the old and new key versions are
never in the database, so no database function can do the re-wrap. It runs
independent of the worker (INF-BG-001) and of the application being up, tracking its progress in a table of its own so that
a crash, a restart or a lost connection resumes where it stopped rather than starting
over. The previous version stays usable for unwrapping until the job reports that
every row has been re-wrapped. No stored ciphertext
changes (PRIV-RIGHT-005a: the format marker carries no key version). The same command
prints the escrow copy of the new version for the envelope (DR-009), and prints it
again on a later run until the seal is confirmed.

**Retirement.** Retirement is confirmed by `rotate-kek --sealed` once the escrow copy
is sealed; it is refused with `model.rotation.notready` while no rotation awaits a
seal, or while values stand under a previous version (`details.pending` counts them).
It records the retirement and reports the versions retired and `keepUntil`, the
rotation's completion plus `backup.retention` (the default, or a longer one the
operator names with `--retention`). A retired version leaves the application's key
document at once, and stays in the envelope and the secrets manager, outside the
application's document, until every backup taken under it has expired, as a previous
backup key does (D-103). The annual operation in section 9 is therefore the command,
the physical act of sealing, and the confirmation.

**Why the command line.** The operation needs the protected maintenance credential
and must not depend on the application being healthy: the case that most needs a
rotation (suspicion of exposure, DR-009a) is also the case in which the application
may be compromised or down.

**Fingerprint key.** Rotation of the fingerprint key (OPS-SEC-001) uses the same
shape (new version, resumable batch job, previous version usable until complete,
escrow copy) but re-computes every stored fingerprint rather than re-wrapping a key.
Every stored fingerprint whose value is held (identifiers, live reservations,
mailboxes, provider links) is computed again. What no value stands behind (a held
username, an erased subject's reservation, the lines of the abuse ledgers) is read
under its version until it lapses; retirement waits for it, and is refused with
`model.rotation.notready` and `pending` while a username is held, an erased subject's
reservation has not lapsed, or an abuse ledger line under a previous version still
counts. The expiry sweep removes each abuse ledger line once its own check no longer
reads it (OPS-OBS-003). At retirement, unspent restriction credit under a previous
version is dropped. A keyed hash kept with no plaintext to recompute it from (a sign-in
in progress, a throttle ledger line) is forgotten when the version it was computed
under is retired.
It is documented as heavy and is expected never to run; it exists so that a suspected
exposure has a procedure rather than an improvisation.

**Acceptance criteria**
1. The operation is a `Janus.Cli` command that requires the maintenance credential
   and is refused with the application's own runtime credential; no endpoint of the
   management application performs it.
2. Killing the process mid-run and starting it again resumes from the recorded
   progress; no subject key is re-wrapped twice and none is left under the old version
   when the job reports complete.
3. Until the previous version is retired, values wrapped under it remain readable;
   after retirement, a value still wrapped under it (there is none by construction:
   every value under the key-encryption key is a row of the subject-key table,
   PRIV-RIGHT-005a) fails to unwrap with `model.startup.secretunavailable`,
   `details.key` `keyEncryptionKeys`, naming the version.
4. The escrow copy of the new version is produced by the same command, and the
   previous version is not retired until the operator confirms with `--sealed` that
   the copy is sealed (DR-009); `--sealed` before the rotation completes is refused
   with `model.rotation.notready`.
5. Start, resume, completion and retirement are recorded as `ops.keyrotation.started`,
   `ops.keyrotation.resumed`, `ops.keyrotation.completed` and `ops.keyrotation.retired`
   under the system principal `rotate-kek` or `rotate-fingerprint-key` (reason
   `OPS-SEC-003`), with the kind (`key-encryption-key` · `fingerprint-key`), the key
   version and the number of values processed.
6. A test exercises the fingerprint-key rotation in the same shape on a small
   fixture, so the procedure is known to work despite never running in production.

---


## 8. Observability

**OPS-OBS-001** — Every authorization decision SHALL be explicable without a
debugger.

*Source: P-003, AUTHZ-GATE-004, D-166*

**Acceptance criteria**
1. A denial resolves to an explanation naming the permission and the missing grant,
   or the deny grant that decided; a live and a recorded refusal are explained by the
   same builder (CONV-LOG-006).
2. Explanation requires no elevated privilege for one's own access.

---

**OPS-OBS-002** — Degradations SHALL be visible, never silent. Fallback to an offline
blocklist, a failed provider push, a notification that did not deliver, a
reconciliation discrepancy and a lost database channel (the registration signal,
REG-SESS-003) SHALL each surface. The absence of a watch the library needs from the
environment (a clock reference, a certificate renewal outcome, the location file) is a
degradation too.

**Values (D-166).** Each degradation raises `degradation` (OPS-ALERT-001) under a scope
naming it; the scopes include `password.blocklist.fallback`, `clock.reference.absent`,
`clock.reference.unread`, `certificate.renewal.absent` and
`certificate.renewal.unread`. A lost registration channel is raised with
`details.component` `registration-channel` by the event stream that finds it lost,
deduplicated by the window of OPS-ALERT-002. A fall back to the offline blocklist is raised as
`password.blocklist.fallback` with `details.configured` (the corpus configured) and
`details.used` (`offline`) before the offline corpus is asked; a fall back that cannot
be raised refuses the operation with what refused the raise.

*Source: D-011, D-006, D-022, P-003, D-166*

**Acceptance criteria**
1. Each listed condition produces a monitored signal.
2. None is recoverable-and-forgotten.

---


**OPS-OBS-003** — Cleanup of expired sessions, consumed tokens, used one-time codes,
and elapsed grace windows SHALL run as background jobs.

**Values (D-153, D-166).** One sweep every `sweep.interval` (default five minutes)
covers expired sessions, tokens and codes (a consumed refresh token kept until no
session it could derive from can still exist, AUTH-KEY-003), identifier verifications
whose codes have all expired unused (REG-IDENT-004, REG-IDENT-007), elapsed grace and
cooling-off windows, privacy request deadlines (PRIV-RIGHT-002), sending-restriction
records and every other abuse ledger line its own check no longer reads (PRIV-RET-005),
export-limit records an hour old (OPS-ALERT-006), and domain re-verification
(`domain.reverify.interval`); a deadline therefore takes effect within that interval
of its instant. The DR-016 replay is the `replay-erasures <ledger path>` command of
`Janus.Cli`.

*Source: D-007, D-038, D-166*

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

**Values (D-153).** A licence record is `{ id, kind: licence · permit, name, expiresAt,
renewedAt }`. A log entry is `{ task, performedAt, actor, note }` with `task` one of
`envelope-rotation` · `licence-renewal` · `approver-review` ·
`pipeline-consumption-review` · `risk-trigger-review`, the five section 9 tasks. `10`
collects both vocabularies.

The annual operation of section 9 is warned of the same way from the log: from
`maintenance.expiry.warninglead` before a year after its latest `envelope-rotation`
entry, `expiry-approaching` is raised under the scope `envelope-rotation`, and a log
with no such entry has it due now. The key-encryption key's own cryptoperiod is
watched from its rotation record, not from the log (DR-009a).

*Source: D-148; D-041, D-147, D-166*

**Acceptance criteria**
1. Expiry dates are stored and surfaced before they lapse.
2. Warning does not depend on anyone remembering: it is the OPS-ALERT-001 condition,
   raised `maintenance.expiry.warninglead` (default 30 days) before expiry. A licence
   past its expiry and not renewed stays raised.
3. The maintenance log holds a dated entry, with the actor, for each performed task
   and each review; entries cannot be deleted through the application. The
   application's database role can read and append log entries and can neither update
   nor delete one.

---

## 10. Environment

**OPS-ENV-001** — Development databases SHALL be seeded with realistic principals and
grants.

*Source: D-010, D-166*

A development environment where nothing is visible invites a temporary bypass that
reaches production.

**Values (D-166).** A development database is made ready as a production one is: the
migrations, then the `bootstrap` command of `Janus.Cli`, which seeds the administrative
organization, the administrative roles and their permissions, the first
administrator, `emergency` and the canary. The library ships no seed of its own; every
further principal and grant is made through the library's own operations, and a host
seeds its own domain the same way. No code path reads which environment it runs in,
and no key of `10` section 4 bypasses, skips or disables a check.

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
