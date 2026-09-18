# 16 — Staff Offboarding Procedure

What happens when someone with system access leaves.

**Referenced by:** D-050. Companion to `14-takedown-procedure.md`.

**Audience:** the operator.

---

## 1. Why this exists

Staff are few, **not all personally known**, and turnover is periodic. Every
departure is someone who held access and no longer works for the company.

Done from memory each time, something gets missed — and the thing most often missed
is the one that matters, because it isn't in the obvious place.

**Two failure modes this prevents:**

**Access that survives departure.** Not dramatic. Someone leaves, their mail keeps
working for three weeks, and nobody notices because nothing broke.

**Data leaving with them.** The realistic case isn't theft — it's a customer list
exported "to finish something up," which is why exports are gated and audited
(D-045).

---

## 2. Before they go, if you have notice

**Check their recent export activity.** D-045 audits exports individually. An unusual
export in the weeks before a departure is worth knowing about *before* they leave,
not after.

**Do not tip your hand about anything else.** If there is a concern, gather what you
need before access is removed — after removal, the audit trail is all you have.

---

## 3. On departure — the checklist

Order matters. Mail last, because it is where notifications land.

**1 — Revoke that person's sessions.**

Use the **per-account** revocation operation (AUTH-SESS-011) — not the system-wide
one, which would sign out every customer and every other staff member.

Suspending the account also ends its sessions (AUTH-SESS-010), but revoking first
closes the window between the two steps.

**2 — Suspend the account.**

This is the administrator's action (`POST /admin/accounts/{subject}/suspend`, recorded
as `suspendedBy = administrator`), not the account's own deactivation: grants suspend,
nothing is deleted (IDN-LIFE-013), and only an administrator can reverse it. Reversible
if the departure is temporary or disputed. (D-148)

**3 — End organization membership.**

The account may hold membership beyond the one being left. Ending membership is
distinct from suspending the account (IDN-MEM-001).

Ending membership is also what retires the corporate address (REG-MAIL-003). The
address stops resolving to the account and the mailbox is disabled, and the address
becomes available to a later invitation. The personal email the account has held
since its invitation (REG-MAIL-001) becomes the primary email automatically, and the
organization's domain lock no longer applies to it. Ending the membership changes
nothing about the account's state: it stays suspended because of step 2, and if step 2
is later reversed it continues as an ordinary account on its personal email. (D-148)

**4 — Remove or transfer their grants.**

Anything granted directly to them rather than through a group. If they were the sole
holder of a permission, transfer it before removing — otherwise something quietly
stops working and nobody knows why.

**5 — Check what they approved.**

If they held `recovery:approve`, review their recent approvals. A departing approver
who granted access shortly before leaving is worth a second look.

**6 — Revoke the mail app passwords.**

**Belt-and-braces, and still worth doing.** App passwords live on the mail server, not
in the library (REG-MAIL-002), and they die with the mailbox: the lifecycle push
disables the mail account on deactivation and at the end of membership, and
app-password authentication stops with it (INT-MAIL-006a, REG-MAIL-003). But a
**failed push leaves a former employee reading mail**, and the push is not verified until
step 7. Revoking each one directly, from the mail server's side, closes that window. An
app password is a long-lived bearer secret and the weakest credential in the system
(R-A02).

**7 — Verify propagation.**

Account lifecycle pushes to the mail server (INT-MAIL-007, IDN-LIFE-015), but the reconciliation
job **flags drift without correcting it** (INT-MAIL-007). Read the report. Do not
assume the push succeeded.

**8 — Record it.**

Who left, when, what was revoked, and that step 7 was verified.

---

## 4. Verify, don't assume

Two days later, confirm:

- No active sessions for the account
- No mail app password of theirs authenticates
- Reconciliation reports no drift for that account

A checklist that is followed but never verified fails in exactly the same way as no
checklist.

---

## 5. If the departure is hostile

Do everything above **immediately and in one sitting**, before they are told, if that
is possible.

Then additionally:

- Change any credential they may have observed — not only ones they held
- Review their audit trail for the preceding weeks
- If a shared login existed, treat every account it touched as compromised (§6)

---

## 6. Shared logins

If it turns out the departing person shared their account, the checklist above does
not close the risk — whoever they shared with still has the credential.

**This is why shared logins are prohibited** and why detection exists (D-051). On
discovering one during offboarding: force re-enrolment on every account that touched
it, not just the departing one.

---

## 7. What this does not cover

**Physical items** — building access, keys, devices. Outside this system.

**Knowledge.** Someone who worked with your customers remembers them. No control
addresses this, and none is proposed. Commercial arrangements are the answer, not
technical ones.
