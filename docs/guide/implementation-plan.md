# Janus — Implementation plan

**Status:** supplementary document. The specification (`spec/00` to `spec/20`) is the
authority for *what* is built; this plan states the *order* in which it is built, what
each phase must prove before the next begins, and the rules the implementer works
under. It adds no requirement. Where this plan and a chapter disagree, the chapter wins.

**Audience:** the implementer and the person directing it. The implementer's working
rules are in the working guide; this plan is the order of work.

**Two milestones.** Milestone 1 is the library: everything the implementer can build and
prove alone against the specification with a database, a cache and a shell, with every
external system met at its abstraction. It runs autonomously, phase by phase, on phase
reports alone. Milestone 2 is the deployment: everything that needs the real world or
the owner (a machine, a credential, a purchase, a physical envelope, a decision). It is
a checklist of the owner's actions with the implementer's work between them, and it does not
start until Milestone 1's exit gate is green.

---

## 1. How to read the specification

1. Start with `00-overview` (shape, principles, glossary, conformance), then `08`
   (layout, naming, testing, gates) and `07` (public contract). These three fix the
   skeleton of the solution before any feature is written.
2. Every requirement has an identifier, a SHALL statement and acceptance criteria. Each
   criterion becomes at least one test. A requirement is done when every criterion has a
   passing test, not when the code exists.
3. `10-reference` is the single place for error codes, permission strings, configuration
   keys, enumerations, step-up actions and events. When a chapter names one, its row in
   `10` is authoritative for the spelling and the default.
4. `09-api-contract` is authoritative for endpoint shapes and status codes.
5. The decision log is rationale only. Read an entry when a requirement seems odd; never
   build from the log. Nothing outside `docs/` is part of the specification.
6. Where the specification is silent on something the code needs, that is a spec defect:
   stop, report it in the phase report with the item it touches, and wait. Do not invent
   behaviour.

## 2. Working rules

- **One phase at a time.** A phase ends with a report: items implemented, criteria proven
  (test names), items deferred with reason, spec defects found. In Milestone 1 the implementer
  continues to the next phase on its own when the report lists no defect, no deferred
  item and a green gate; otherwise it stops and waits. Milestone 2 never starts without
  the owner.
- **Fast checks per commit** (build, analysers, unit suites); the **full gate** (integration,
  migration twice, contract tests, conformance suite) runs once per phase before the
  report (CONV-GATE-001, CONV-GATE-002).
- **No measurements, sweeps or re-runs** beyond what a criterion literally asks for.
- **Content rule.** No user-facing wording is decided in library code. Messages surface as
  codes (CONV-CONTENT-001, API-CONV-002); the frontend renders them.
- **Generic library.** No code path, default or fixture assumes a particular host,
  product or market. Hosts declare purposes, factors, policies and preferences (LIB-HOST-001).
- **Secure by default.** Every key ships with its `10` default; a host that configures
  nothing but the eight required values gets a conformant deployment (OPS-CFG-003, `10` section 4).
- **Stop conditions.** Any of the following ends the run with a report instead of a
  guess: an acceptance criterion that cannot be tested as written; two chapters that
  disagree; a dependency the environment does not provide; a standard or product
  behaviour the spec asserts that the implementation cannot confirm.

## 3. Milestone 1: the library

Ordered by dependency. Each row's "proves" column is the phase gate; it is checked by
running the criteria of the items listed, not by inspection.

| # | Phase | Builds | Chapters and items | Proves |
|---|---|---|---|---|
| 0 | Skeleton | Solution per CONV-LAYOUT-001 (Core, Identity, Authentication, Authorization, Privacy, Storage, Hosting, Cli, Conformance, Analyzers) with the setup of `08` sections 1a and 1b and the five analyser rules of CONV-CODE-008; test projects per CONV-TEST-001/002; pipeline gates; lockfile; secret scanning; error and result conventions; structured logging; configuration store and the policy object | `08` all; `06` OPS-CFG-001 to 008, OPS-DEP-001 to 005; `07` LIB-PKG-001/002, LIB-API-002/003; `10` section 4 keys as typed settings with defaults and floors; `10` section 4.1a policy object | Every project compiles with inward-only dependencies; a host that sets only the eight required keys starts; every `10` key resolves with its default; contract test scaffold fails on an undeclared public type |
| 1 | Storage and identity core | Schema and migrations (library-owned, expand and contract, applied library first); accounts, subject identifier, states and transitions; organizations, membership, policy inheritance; per-subject data key and fingerprint storage; identifier list model; audit records | `06` OPS-DATA, OPS-DB, OPS-MIG; `01` sections 1 to 3, IDN-LIFE-003/003a/003b, IDN-LIFE-012 to 015, IDN-ATTR, IDN-PRIN, IDN-AUD; `04` PRIV-RIGHT-005a/005c, PRIV-RET-002/003; `20` REG-ACCT-001, REG-IDENT-001 to 003, REG-PROF-001, REG-PREF-001 | Migration applied twice against a throwaway database; an account exists only in a `10` section 5.1 state; erasure leaves keyed fields unreadable in one transaction; identifiers canonicalise and fingerprint per IDN-ACCT-004 to 006 |
| 2 | Authorization | Grants, roles, groups, model builder, derivations, permission filter (expression and SQL fragment), single evaluation seam, capability arrays, explanation | `03` all; `07` LIB-API-004, LIB-SEAM-001/002, LIB-HOST-002/004; `10` sections 2, 3, 5.5, 5.6 | Every denial is explicable (OPS-OBS-001); host filters apply to host tables; the seam is the only evaluation path |
| 3 | Sessions, passwords, factors | Server-side session spine and types; assurance levels; lifetimes; rotation; per-app sessions; passwords (floors, leaked-list screening with offline fallback, Argon2id); factor catalogue and properties; TOTP; WebAuthn (passkeys, second-factor keys, BE/BS, labels, upgrade); recovery codes; trusted devices; new-device check; grace period; step-up gates and reachable assurance | `02` sections 1 to 5; `05` INT-PWD; `10` sections 4.2, 4.3, 5.2 to 5.4, 5a; `17` sessions and CSRF items | AAL assigned exactly per AUTH-SESS-005a; a 12-character password without a second step is refused; a gate with `phishingResistant` refuses password plus TOTP and accepts password plus security key; new-device check holds a password-only sign-in from an unseen browser |
| 4 | Sending and restrictions | Notification pipeline with retry; email and SMS transports behind abstractions; templates with length budgets; the restriction model (keys, purposes, buckets, grants, runtime edits, alerts); security-notice set routing | `02` section 7 (AUTH-ABUSE-004 and neighbours), AUTH-ABUSE-001 to 003, 005 to 008; `05` INT-SMS, INT-GEN-001 to 005; `06` OPS-ALERT-001 to 004a; `10` sections 4.5, 5.14 to 5.16 | Fourth SMS to one number in 24 hours refused with `retryAt`; a security notice to an existing holder is not blocked by a drained destination; a failed delivery does not count; a restriction edit applies without restart and is audited |
| 5 | Registration and account management | Registration session bound to the pre-authentication cookie; the ten steps; code and link verification with same-browser press and elsewhere-shows-code; age screen and derived affirmation; security step rules; terms step transaction; return contract; identifier add, primary, backup, remove with undo, replace; profile; preferences store; sessions list; credential list and labels; well-known endpoints | `20` all remaining items; `09` sections 2 and 6; `17` BFF-CSRF-005a/005b; `18` FE-REG, FE-VER, FE-PM, FE-SEC, FE-ACCT (server side only; screens are the frontend's) | Abandoned registration leaves no row; a link opened in another browser verifies nothing and shows the code; duplicate identifier gives a byte-identical response and one owner notice; removing an email sends the undo to the remaining set only; `emergency` holds no identifier |
| 6 | Recovery, sign-in links, OIDC | Admin-assisted re-enrolment and enrolment session; public recovery; loss reports and invalidation windows; credential redundancy; sign-in links (email and SMS) and `emailCode`; SMS second step flagged restricted; OIDC provider (discovery, JWKS, userinfo, code flow, refresh rotation, token lifetimes, key rotation); first-party client for the mail server | `02` sections 6, 8, 9, AUTH-FACT-002b/003/016/017; `09` sections 3, 5, 9; `10` section 4.4, 4.9 | Approver cannot approve own recovery; a lost passkey is invalidated after the notified window; a sign-in link never satisfies a gate; tokens signed ES256 rotate with overlap and validate throughout; `/enrol/begin` consumes only the admin-assisted link |
| 7 | Privacy | Purposes and lawful bases, consent and objection records, written consent, notice presentation, cookie list, legal documents with governing language and translations, rights queue with working-day clock and holidays, export routine, erasure and restriction through the outbox, RoPA generation, breach queries, retention per category | `04` all; `09` section 7; `10` sections 1.4, 4.7, 5.7 to 5.12; `13` R-O items as build inputs | Registration completes with every consent unticked; withdrawal takes no more interactions than granting; a document publishes without translations and never without governing text; deadline computed on the declared working week; erasure reaches every subscriber or alerts |
| 8 | Organizations, invitations, domain lock, mail | Bound and open invitations with personal email; acknowledgement; existing-account acceptance; domain lock with DNS verification and scheduled re-check; mail server integration (JMAP provisioning, disabled at invitation, enabled at membership, lifecycle push, reconciliation, app passwords through the first-party client); offboarding outcome; takedown two-phase operation | `20` sections 5; `01` IDN-ORG-006, IDN-LIFE-009a/009b, IDN-LIFE-003 (takedown); `05` INT-MAIL all; `09` sections 6a, 8, 8a; `14`, `16` as procedures the operations support | Staff account is created only after the personal email is verified; personal email becomes primary at membership end without a state change; disabled mailbox exists at invitation and is enabled at acknowledgement; reconciliation flags drift; takedown publishes cancellation at trigger and erases at the window's end |
| 9 | Operations code | Bootstrap command (first organization, administrator, enrolment link, `emergency`); break-glass credential and session; KEK rotation command and re-wrap job; alert routing including owner destinations; maintenance log; background jobs and named principals; the restore-test job and the erasure ledger writer, run against containers | `06` OPS-BOOT, OPS-SEC, OPS-MAINT, OPS-OBS, OPS-ENV; `11` as the procedure the code must make possible; `12` DR-006a, DR-007, DR-008, DR-009a, DR-016 (the code side) | Bootstrap issues no break-glass credential; KEK rotation resumes after a forced crash; the restore-test job restores a container backup and proves a field decrypts; every `06` alert condition fires from a test |
| 10 | Conformance and release readiness | Conformance suite a host can run; contract tests over LIB-API-001; `CHANGELOG.md` with the first version section prepared under `Unreleased`; sample host that declares only the required values, with in-memory fakes for mail and SMS | `07` LIB-TEST-001/002, LIB-VER, LIB-HOST-001/003; `08` CONV-VCS-005; `00` section 9 | Sample host passes the conformance suite with defaults; a public-surface change fails the contract test; the full gate is green on the default branch |

**Milestone 1 exit gate.** All ten phases reported and approved; every acceptance
criterion of every item in chapters 01 to 10, 17 and 20, and the code-side items of 06,
12 and 19, has a passing test carrying its identifier (CONV-TEST-007); the full gate is
green; `PublicAPI.Unshipped.txt` holds the complete surface; no spec defect is open.
The implementer stops here and does not begin Milestone 2 on its own.

What Milestone 1 does **not** touch: a real mail server, SMS gateway, secrets manager,
DNS, certificates, host machine, package feed or frontend. Each is met at its
abstraction (LIB-EXT-001) with a fake that honours the contract, and the fake is what
the integration tests run against.

## 4. Milestone 2: the deployment

Sequential; each step names who acts. "Owner" steps need the person; "implementer" steps
are done by the implementer once the owner step before them is complete. Nothing here is
autonomous end to end.

| # | Step | Who | What | Done when |
|---|---|---|---|---|
| 1 | Repository workflows | implementer | Pipeline definitions for the gates of CONV-GATE-001/002, release workflow (tag, MinVer, changelog check, package publish), dependency alerting, secret scanning | Every gate runs on a pull request; a tagged commit produces a versioned package |
| 2 | Package feed | owner, then implementer | Owner provisions the private feed and its credential (LIB-PKG-003); implementer wires the release workflow to it | The package installs from the feed into the sample host |
| 3 | Infrastructure definition | implementer, owner reviews | Container arrangement, reverse proxy, DNS records, volumes, certificate mechanism, pipeline target, committed per DR-017; PostgreSQL with ICU collation, Redis, secrets manager, clock sync per `19` | Owner approves the definition; DR-007 test builds from it |
| 4 | Host and secrets | owner | Provision the host outside Egypt (INF-HOST-004), the secrets manager (INF-HOST-003), the database credentials (INF-DB-003), TLS (INF-TLS-001 to 003), alert channels (INF-OBS-001), the reachability check (INF-OBS-003) | The implementer's connectivity checks pass from the pipeline |
| 5 | Mail server | owner, then implementer | Owner deploys the mail server and registers the sending domains (INT-MAIL-011); implementer configures the OIDC client, provisioning and reconciliation against it (INT-MAIL-001 to 010) | A staff invitation creates a disabled mailbox; acknowledgement enables it; reconciliation reports no drift |
| 6 | SMS gateway | owner, then implementer | Owner contracts the gateway and sets the balance floor; implementer wires the transport, delivery report and balance polling (INT-SMS) | A verification code arrives on a real number; a failed delivery does not count |
| 7 | Configuration | owner with implementer | The eight required values (`10` section 4), the governing language (`legal.governinglanguage`), the legal documents and their versions, the host's purposes and lawful bases, the preference declaration, the holiday list | Startup succeeds with no default overridden except the eight; compliance texts publish |
| 8 | Bootstrap | owner | Run the bootstrap command on the real host (OPS-BOOT-001): first organization, first administrator's enrolment link, `emergency`; complete the enrolment with a passkey; issue the break-glass credential from the management app and seal the envelope (OPS-BOOT-002 to 004, DR-009) | The envelope holds the code, the three keys and the deployment credentials; the escrow copy exists; the annual reseal is on the maintenance log |
| 9 | Backups and restore | implementer, owner verifies | Continuous archiving, base backups, encryption under the backup key, the erasure ledger off host, the scheduled restore test (`12`) | First restore test passes on the real host; DR-011 rebuild rehearsed once from the envelope and the repository |
| 10 | Frontends | implementer | Authentication, account and management applications per `18` against the real BFF; wording written natively per language (CONV-CONTENT-001) | Every FE item's criteria pass end to end against the deployed BFF |
| 11 | Business actions | owner | The outstanding actions of the decision log (counsel confirmations, DPO, licences, permits) | Each recorded as done in the log's table |
| 12 | Release 1.0.0 | implementer, owner approves | Move `Unreleased` to `1.0.0`, ship the public-surface file, tag, publish (CONV-VCS-005) | Version 1.0.0 on the feed; changelog and migration note published |

The frontend applications (`18`) are built in step 10 against the deployed BFF;
Milestone 1 phases 5 and 8 deliver the server side they need.

## 5. Phase report

Each report is short and has the same shape:

1. Items implemented, each with the test names that prove its criteria.
2. Items in the phase not implemented, each with the reason and the item it waits on.
3. Resolved by rule: slips resolved in code under the conditions the working guide's section 3
   sets, each with the governing item and the rule applied.
4. Open questions: item, what the code needed, what the spec says, the readings seen,
   the smallest fix for each. No fix is applied to a chapter from the code side; it goes
   through the decision log.
5. Gate result: fast checks per commit, full gate once, both green, with the run
   identifiers.
6. Nothing else. No narrative, no measurements not asked for.

In Milestone 2 the same shape is used per step, plus the owner action the next step
waits on, stated in one sentence.

## 6. What is deliberately not here

Screen design and wording (the frontend's, CONV-CONTENT-001). Infrastructure "how"
beyond what DR-017 requires to be committed. Host applications. Anything the
specification defers with a trigger (`00` section 7.3).
