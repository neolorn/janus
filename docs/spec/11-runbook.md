# 11 — Runbook

Operational procedures. Referenced by OPS-MIG-006, OPS-BOOT-003, INT-MAIL-005,
IDN-LIFE-003 and PRIV-MINOR-002, none of which previously had a document to point
at.

**Audience:** whoever operates the system. Currently one person.

**Related documents.** Recovery objectives and the full restore procedure are in
`12-disaster-recovery.md`. The minor takedown procedure is `14-takedown-procedure.md`.
Accepted risks referenced here are in `13-risk-register.md`.

---

## 1. Standing facts

| | |
|---|---|
| System administrators | One |
| Break-glass custodian | The company owner, sealed offline |
| Deployment | Pipeline; no manual production access in the normal path |
| Destructive DDL gate | Built, currently disabled |
| Recovery approvers required | One |

*Source: D-029, D-010, D-019, D-008*

**The operator is the single point of failure.** Every procedure here assumes that
and is written so the company is not stranded if they are unavailable. That is the
reason break-glass custody sits with the owner rather than the operator.

---

## 2. Deployment

### 2.1 Normal deployment

Push to the default branch. The pipeline builds, runs checks, applies migrations,
then deploys. No manual step.

**If the pipeline fails at the migration step:** the deployment has not happened. The
previous version is still running and serving. There is no partial state to unwind —
migrations run before deploy for exactly this reason (OPS-MIG-001).

Fix forward. Do not attempt a manual migration against production.

### 2.2 Destructive schema changes

While the gate is disabled, destructive migrations deploy automatically. Detection
still reports them on every deploy (OPS-DEP-002) — **read that report.**

Once the gate is enabled, the automatic deploy fails when destructive operations are
present, naming the manual workflow to dispatch.

*Source: D-042.1*

### 2.3 Rollback

**There is no rollback.** Recovery is fix-forward (OPS-MIG-006).

Down-migrations are unreliable once data exists, so the procedure is: identify the
fault, write a corrective migration, deploy it. For a fault that has corrupted or
destroyed data, see section 6.

---

## 3. Break-glass

### 3.1 When to use it

When **no system administrator can reach the system** — the operator is unreachable,
or the operator has lost their own credentials. For a lone administrator the second
case is not "administrative recovery" (section 4): recovery needs an approver who is
not the person being recovered, and there is only one administrator. **Break-glass is
the recovery path for the sole administrator.** A staff member who loses a passkey
while the administrator is reachable is section 4.

### 3.2 Procedure — written for the owner

You do not need technical knowledge for this. Read it through once before starting.

1. **Get the sealed envelope.** It holds five printed items (DR-009). You only need
   the first one, the **emergency code**; the other four are for a technical helper
   restoring the system after a disaster, and you can leave them in the envelope.
2. **Open the address printed on the envelope** in any web browser. You will see one
   box and one button.
3. **Type or paste the emergency code and press the button.** The code works once;
   after this it is spent. If the page says the code is invalid or already used, stop
   and call the person named on the envelope.
4. **You are now signed in with full administrative access for a fixed period**
   (`breakglass.session.lifetime`, 4 hours unless the operator has changed it; the
   page shows when it ends), and the management application opens. At that moment an alert goes to the operator and to
   you, by email and SMS — that is expected.
5. **Do what the emergency needs.** Usually one of: approve the pending recovery so
   the operator (or a staff member) can get back in; or give administrative access to
   someone you trust who can reach the system.
6. **Before you finish, make a new emergency code.** In the management application,
   open the emergency-access page and use the control that generates a replacement
   credential (OPS-BOOT-004; the labels shown there are illustrative). Print it, seal it in a new
   tamper-evident envelope with the four other items from the old one, and keep it.
   Without this step there is no emergency code until the operator returns.
7. **Write down what you did and when.** The system records every action taken in the
   session; your note is for the review afterwards.

The session ends on its own when that period is over (`breakglass.session.lifetime`,
default 4 hours). Nothing you did is undone by that.

*Source: OPS-BOOT-002, OPS-BOOT-004, D-010, D-103, D-129, D-147*

### 3.3 Annual reseal — five things in one operation

Regenerate and reseal annually. Verifying that it works consumes it, so verification
and regeneration are one action.

**While the envelope is open, do all five:**

1. **Inspect the packaging for tampering.** If it shows any sign of having been
   opened, treat it as a compromise: rotate everything immediately, do not simply
   reseal.
2. **Regenerate the break-glass credential.**
3. **Rotate the key-encryption key** (DR-009a) with the `Janus.Cli` command of
   OPS-SEC-003, run under the maintenance credential: it introduces the new key
   version, re-wraps every subject key in a resumable batch job, retires the previous
   version when the job reports complete, and produces the escrow copy for the new
   envelope. Cheap in effect: it re-wraps key material and never touches customer
   data (D-147).
4. **Rotate the backup key** (DR-010). New backups encrypt to the new public key;
   keep the previous private key in the envelope until every backup made under it has
   expired, then remove it.
5. **Test escrow recovery** (DR-009b) — perform a restore using the envelope contents
   rather than your own working access: open a backup with the backup private key,
   decrypt a subject's field with the key-encryption key, and sign in with a known
   account. An untested escrow is a hypothesis.

Then reseal in tamper-evident packaging and update the secrets-manager copies of the
four keys and credentials — never of the break-glass code, which exists only on the
sheet you just printed (OPS-BOOT-004).
The fingerprint key is printed but not rotated — its rotation is a bulk re-derivation
(PRIV-RIGHT-005c) and is expected never to run.

**Rotate out of cycle** on any intrusion, alarm, or incident touching the secrets
manager — before a copied key could be used.

This is one of two accepted recurring human tasks (`00-overview`, section 4).

---

## 4. Admin-assisted account recovery

For anyone who has lost their credentials and cannot use self-service recovery: a
staff member (who never can), a customer whose mailbox is gone (D-111), or a customer
whose phone is gone and who cannot use self-service (AUTH-RECOV-002, D-147, D-148).
Every human account holds a verified email (REG-IDENT-001), so a phone is never the
only channel; within the re-enrolment session the approver's confirmation stands in
for the old address (REG-IDENT-007).

1. Confirm identity **out of band, on a channel already recorded on the account** —
   never one supplied in the request (AUTH-RECOV-003).
2. Record which channel was used.
3. Record a written reason. Mandatory.
4. Issue the time-boxed enrolment link **to a recorded channel**: for a customer
   whose mailbox is gone, their recorded phone; for a customer whose phone is gone,
   their recorded email.
5. The account owner is notified on a separate channel automatically.
6. If the person's old mailbox is unreachable, the re-enrolment session lets them set
   a new email address; the old address is notified without a link and the remaining
   channels hold the undo for `identifier.change.coolingoff` (REG-IDENT-006, REG-IDENT-007).
   If the lost channel is the phone, the same session lets them replace the phone
   number, confirmed by the new number alone, with the notice and the undo going to
   the recorded email (REG-IDENT-007, AUTH-RECOV-002).

**Do not** accept a phone number or address provided during the request. The
pre-existing channel is the entire control.

Approver anomaly detection runs per approver (AUTH-RECOV-002). With one approver,
the operator reviews the approver report weekly, rather than relying on it to alert,
and records the review in the maintenance log of OPS-MAINT-001 (D-147).

---

## 5. Minor discovered — takedown

Executed on any credible indication that a customer is under 18: self-disclosure, a
guardian's contact, or something surfaced in support.

**Credible** means a specific statement or a guardian's claim. Not a guess from a
name, a purchase, or a writing style.

### Procedure — two phases, one button (IDN-LIFE-003, `14` §3)

**Phase one, when you trigger it** — the identity system, in one transaction:
suspends the account and ends its sessions; records the event with the reason and
trigger; publishes the outbox record that tells the host to cancel open orders. That
outbox record is the completion record of the cancellation from this moment
(IDN-LIFE-003, D-148): the host confirms against it moments later, and until it does
the takedown screen shows the cancellation as still outstanding (any label is
illustrative). No deletion email and no cancel link go to
the subject.

**Phase two, seven days later, automatically** — the identity system erases:
destroys the subject's key and neutralises the fingerprint (PRIV-RIGHT-005). The
account becomes `deleted`. The audit trail survives.

**Inside the seven days** a misjudged adult is restored with the reverse-takedown
control on the account's admin page (`POST /admin/accounts/{subject}/takedown/reverse`,
`takedown:execute`, reason required; the label is illustrative). Cancelled orders are
not restored. After the window there is nothing to reverse.

**Do not record the takedown as finished** until the shop's cancellation shows
complete and, after the window, the erasure shows complete. A permanent failure
alerts you immediately.

**Do not** ask for proof of age. The date of birth entered at registration is retained
only where `profile.dateofbirth` is on (REG-PROF-002, PRIV-MINOR-001); by default only
the derived affirmation is held, and collecting a date to resolve a takedown would
create the data the policy exists to avoid.

*Source: D-039, IDN-LIFE-003, PRIV-MINOR-002*

---

## 6. Data loss and corruption

Full specification in `12-disaster-recovery.md`. Operational summary:

**Decide first: restore or fix forward.** Data destroyed or corrupted → restore. A
logic fault that wrote wrong values but lost nothing → a corrective migration, which
is faster and loses nothing.

**If restoring:**

1. Stop writes. A partial restore under load produces a worse state.
2. Identify the target timestamp — immediately before the destructive event, not
   when it was noticed.
3. Provision a host, restore the base backup, replay logs to the target.
4. **Verify before cutting over** — row counts, most recent records present, and the
   ancestry integrity check (a restore is exactly when an ancestry inconsistency
   would be invisible and consequential).
5. Cut over. Record the timeline.

**Expect 4–8 hours.** This is the accepted objective, not a failure. There is no
standby to fail over to; that was declined on cost.

**Then reconcile.** A restore does not roll back the world. Mail was delivered,
payments captured, shipments dispatched. Reconcile orders against the payment
provider, shipments against the courier, and run Stalwart reconciliation.

**If records were lost, this may be a personal data incident.** The 72-hour clock
applies to loss of availability, not only disclosure. Record what was lost and for
whom before deciding.

**Current phase: backups on the same host**, through launch and early operation.
This covers destructive migrations and accidental deletion. It does **not** cover
loss of the host — in this phase, losing the server means losing the data
permanently. Backups move off-machine at the VPS tier upgrade (DR-005).

---

## 7. Provider failures

### 7.1 Mail

The mail server is a separate deployment with its own database. Existing sessions are
unaffected — they do not depend on it.

**But outbound mail failure stops a great deal**, and the previous wording understated
this:

| What stops | Who it affects |
|---|---|
| Sign-in link (`emailLink`) and email-code (`emailCode`) sign-in, the new-device check code, undo links and security notices by email | Customers — the first two are **primary** factors where enabled |
| Email verification during registration | New customers |
| Account recovery | Anyone locked out |
| Identifier verification and undo notices | Anyone adding or removing an address |
| **Pending authenticator invalidations** (loss reports) | Held, deliberately — AUTH-RECOV-007 will not complete without a delivered warning |

**The last one is easy to miss.** A mail outage holds every pending removal; the hold
is **flagged** (AUTH-RECOV-007 AC3) and the undelivered warnings surface as a
degradation alert (OPS-OBS-002, OPS-ALERT-001) — it is not silent, but it completes
nothing on its own. Check pending removals after any outbound mail incident.

**Mailbox hosting failure is narrower** — staff cannot read company mail. Customers are
unaffected; they hold no mailbox here (INT-MAIL-006).

**Provisioning failures are the risk.** A failed lifecycle push leaves someone
reading mail after offboarding. Reconciliation runs daily and **flags drift without
correcting it** (INT-MAIL-007) — read the report, then act deliberately.

Auto-correction is deliberately absent: it would conceal a broken pipeline.

### 7.2 SMS gateway

**Balance exhaustion halts registration**, because phone verification is required by
default (REG-IDENT-001, `registration.phone`). Monitor balance and drain rate; sends hard-stop below the floor
(INT-SMS-004).

A drain spike without matching registrations indicates toll fraud. Investigate before
topping up.

### 7.3 Password screening

Unreachable → the offline fallback runs and the degradation is logged. Both
unavailable → password set and change **fail rather than accept unscreened**
(AUTH-PASS-004).

Do not disable screening to restore service. Blocklist screening is the load-bearing
control now that composition rules are gone.

### 7.4 Payment and shipping

Callbacks never advance authoritative state alone (INT-GEN-003). A provider outage
delays confirmation; it does not corrupt order state.

---

## 8. Security incidents

### 8.1 Suspected session compromise

Use the explicit revocation operation (AUTH-SESS-009). This is distinct from the
automatic downgrade that follows a policy change — revocation is immediate and
total.

### 8.2 Suspected credential compromise

Revoke sessions, then require re-enrolment. Password rotation is required **only on
evidence of compromise** (AUTH-PASS-003) — do not impose blanket rotation, which
produces predictable derived passwords.

### 8.3 Personal data breach

**Clock starts at detection**: 72 hours to the regulator, three days to affected
subjects (PRIV-BREACH-001). **Not deferrable pending investigation** — if you are
uncertain whether an incident is notifiable, the clock is already running while you
decide. The three-day subject clock is this system's conservative choice; the law
counts it from the notification to the Centre (D-125) — if the 72 hours are used, the
legal deadline for subjects is later than ours, never earlier.

For a minor takedown, detection is the moment of the credible indication, not the later
conclusion that it was notifiable (`14-takedown-procedure` §5).

1. Determine scope. Audit records are queryable by data subject for this purpose
   (PRIV-BREACH-002).
2. Notify the regulator within 72 hours.
3. Notify affected subjects within three days. **Non-suppressible** — no preference
   silences it.
4. Record the timeline.

Notification is not optional and not deferrable pending investigation. Notify with
what is known.

---

## 9. Configuration changes

**Tightening** a security control is free.

**Loosening** requires step-up, a written reason, and produces an audit entry
(OPS-CFG-002).

**Protected settings** (`10-reference`, section 4.8) cannot be changed through the
application at all — they require access the application does not have. If an incident appears to require disabling audit logging, rate limiting, or
step-up enforcement — that is the moment those restrictions exist for. They are on
the list precisely because turning them off would blind the system to whoever turned
them off.

---

## 10. Compliance calendar

The second accepted recurring human task.

| Item | Cadence | Notes |
|---|---|---|
| Controller licence renewal | Three years | System tracks and warns (OPS-MAINT-001) |
| Cross-border permit renewal | Shorter than the licence | Required while hosting outside Egypt |
| DPO registration | Per the regulator's terms | Company action |
| Break-glass reseal | Annual | Section 3.3 |

*Source: D-041, D-023*

---

## 11. What changes when someone joins

All built and inactive.

| Change | Currently |
|---|---|
| Pull requests require approval | Status checks only |
| Destructive-DDL gate enabled | Disabled |
| Recovery approvers above one | One |
| Gate toggle protected from pull-request edit | Unprotected |

*Source: D-042.2, D-019, D-008*

---

## 12. Open items

None. Every referenced document exists:

| Referenced | Document |
|---|---|
| Recovery objectives and restore procedure | `12-disaster-recovery.md` |
| Risk register (INT-MAIL-005) | `13-risk-register.md` |
| Minor takedown procedure | `14-takedown-procedure.md` |
| Threat model | `15-threat-model.md` |
| Offboarding checklist | `16-offboarding-procedure.md` |
