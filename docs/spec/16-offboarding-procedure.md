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

**Set the membership's end for their last day.** The membership can be set to end at an
instant you choose (IDN-MEM-001), so access ends on the day without anyone having to
remember; it can be moved or cancelled until then. Transfer what only they hold before
that day (step 4).

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

The account may hold membership beyond the one being left. Ending membership is distinct
from suspending the account (IDN-MEM-001). It revokes every allow grant a person gave
them in the organization (a deny on them stands, D-217) and takes them out of its
groups, and, for the administrative organization, does the same in every organization
they are not a member of (D-213); access they hold through the host's own relationships
is the host's to end. Where an end was set for their last day (section 2), this step has
already happened.

Ending membership is also what retires the corporate address (REG-MAIL-003). The
address stops resolving to the account and the mailbox is disabled, and the address
becomes available to a later invitation. The personal email the account has held
since its invitation (REG-MAIL-001) becomes the primary email automatically, and the
organization's domain lock no longer applies to it. Ending the membership changes
nothing about the account's state: it stays suspended because of step 2, and if step 2
is later reversed it continues as an ordinary account on its personal email. (D-148)

Their mailbox and its mail are never handed on silently. Any later invitation of the
address, one to the same person included, is refused unless the administrator chooses,
with a reason, whether the invitee receives the mailbox and its mail (`formerMailbox`
`transfer`) or the old mailbox is removed and a new one reserved (`replace`)
(REG-MAIL-003, D-166). If the person is later erased, a later invitation of the address
is no longer refused, since nothing left in the library recognises the address; erase
the mail-server account they left as well (D-101), because a new mailbox at the address
is created only once that account is gone (REG-MAIL-003, D-178).

**4 — Transfer what only they held.**

Ending the membership (step 3) revokes their grants, so do this first where you have
notice. `GET /admin/grants` lists what they hold, for the person and for a group, per
organization (D-166). If they were the sole holder of a permission, give it to someone
else before the end; otherwise something quietly stops working and nobody knows why.
After the end, the audit trail's `authz.grant.revoked` records name what was revoked
with them (D-213). Where the person was the last holder of `grant:manage` in an
organization, give it to another first; otherwise the organization's grants wait on a
holder of `system:administer`, who gives `grant:manage` to another (AUTHZ-SCOPE-001,
D-207).

**5 — Check what they approved.**

If they held `recovery:approve`, review their recent approvals. A departing approver
who granted access shortly before leaving is worth a second look.

**6 — Revoke the mail app passwords.**

**Belt-and-braces, and still worth doing.** App passwords live on the mail server, not
in the library (REG-MAIL-002), and they die with the mailbox. A mailbox is owed
`enabled` only while its holder's account is `active` or `restricted` and holds a
current membership of the administrative organization, so it is owed `disabled` from
step 2 and again from step 3; the push carries that state to the mail server, and
app-password authentication stops with it (INT-MAIL-006, INT-MAIL-006a, REG-MAIL-003,
D-166). But a **failed push leaves a former employee reading mail**, and the push is
not verified until step 7. Revoking each one directly, from the mail server's side, closes that window. An
app password is a long-lived bearer secret and the weakest credential in the system
(R-A02).

**7 — Verify propagation.**

The state each mailbox is owed, read from its holder's account state and memberships, is
pushed to the mail server (INT-MAIL-006, INT-MAIL-007, IDN-LIFE-015, D-166), but the
reconciliation job **flags drift without correcting it** (INT-MAIL-007). Read the
report. Do not assume the push succeeded.

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
