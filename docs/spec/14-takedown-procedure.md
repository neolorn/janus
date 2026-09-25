# 14 — Minor Account Takedown Procedure

What happens when a customer turns out to be under 18.

**Referenced by:** IDN-LIFE-003, PRIV-MINOR-002, D-039.

**Audience:** whoever handles customer support. Currently the operator.

---

## 1. Why this exists

The service is 18+ only. Age is **self-declared** on a neutral age screen at registration
(REG-PROF-002); the affirmation is derived from the answer. Every act of processing the
host performs belongs to an account (D-073), so this procedure always has an account to
act on. The date itself is retained only where `profile.dateofbirth` is on.

A self-declared date verifies nothing. Someone under 18 can enter one.

The regulator's requirement for sensitive data is **proportionate** age verification.
Real verification would mean holding identity documents for every customer, which
creates a larger data protection problem than it solves. What makes self-declaration
proportionate instead is that the situation has a **defined outcome** rather than
being decided on the spot by whoever notices.

**This document is that outcome.** Its existence is the control.

---

## 2. What counts as discovery

Act on a **credible indication**. That means:

- The customer states their age
- A parent or guardian contacts you about the account
- Something specific surfaces in a support conversation

**Not:** a guess from a name, a purchase pattern, a writing style, or a photograph.
Those are speculation, and acting on them means suspending adults' accounts on a
hunch.

You are not hunting for this. You are not ignoring it when it lands.

---

## 3. Procedure

Four steps, in **two phases**. The system provides one operation — the takedown
(IDN-LIFE-003) — so they do not need to be done by hand or in sequence.

**Phase one — the moment you trigger it.** In one transaction the identity system:

**1. Suspends the account.** Access stops immediately: every session ends, sign-in
is refused, staff cannot act on the account. No further processing.

**4 — Records the event.** What triggered it, what was done, when. This record is what
demonstrates the procedure was followed.

And it tells the host's system, through the `TakedownExecuted` outbox record, to:

**2. Stop what it holds for the subject.** The host's own procedure says what that
means for its records (anything in flight is stopped where it still can be; where it
cannot, the host notes it and moves on). This step happens in the host's system moments
later. The identity system's outbox record, written in the trigger transaction, is the
completion record of this step from the moment you press the button (IDN-LIFE-003,
D-148): the takedown screen reads it and shows the host's step as still outstanding
until the host confirms against it (the label shown is illustrative). No erasures row
exists yet; that record belongs to phase two.

**Phase two — seven days later, automatically.**

**3 — Remove personal data.** When the takedown window (`takedown.grace`, default
seven days) elapses, the identity system erases: it destroys the subject's encryption
key (PRIV-RIGHT-005a) and **neutralises** their searchable fingerprint
(PRIV-RIGHT-005c) — overwritten, never removed. Every personal field becomes
unrecoverable at once; the anonymised record and the audit trail survive
(PRIV-RIGHT-005). The account is then `deleted`.

**Why the window.** Nothing is processed during it (the account is locked and the host
has stopped what it held) but a mistake can still be undone. If it turns out an adult
was misjudged, **reverse the takedown** from the account's admin page inside the seven
days: the account returns to `active` and the person can sign in again. The host
receives `TakedownReversed`; whether it restores what it stopped is the host's rule.
After the window, there is nothing to reverse.

**Do not record the takedown as finished until every step shows complete**: the
host's step confirmed against its outbox record and, after the window, the
erasure complete in the erasures row (IDN-LIFE-003b). If a step
fails permanently you will be alerted immediately (OPS-ALERT-001); you do not need to
watch for it.

---

## 4. What not to do

**Do not ask for proof of age.** By default no date of birth is held, only the derived
affirmation (PRIV-MINOR-001, REG-PROF-002), and collecting one to resolve a takedown
would create exactly the data the policy exists to avoid.

**Do not ask for identity documents.** Same reason, worse.

**Do not keep the account open pending investigation.** There is nothing to
investigate. A credible indication is sufficient; the cost of being wrong is a
reversal inside the window, or a customer who must re-register after it, and the cost
of being slow is processing a minor's data, possibly of a sensitive category, without
lawful basis.

**Do not delete the audit trail.** Anonymisation removes the personal data. The record
that the account existed and what happened to it must survive — that is what proves the
procedure ran.

---

## 5. If they were a customer

Two follow-ups.

**Settle anything the host owes the person** through the host's normal process. The
account being suspended does not affect that obligation. The host's business records
survive erasure as anonymised records (PRIV-RIGHT-005), and the host's processors hold
their own references, so settlement remains possible after the erasure too; the window
simply makes it easier.

**Consider whether it is a reportable incident.** Processing a minor's data, of any
category the host has declared sensitive, without guardian consent is a processing
failure. Whether it requires regulator
notification depends on the circumstances — how long, how much, whether it was
disclosed anywhere. Record the facts; take advice if uncertain.

**The breach clock starts at detection** (PRIV-BREACH-001) and is not deferrable
pending investigation.

For this procedure, **detection means the moment you have a credible indication that a
minor's data has been processed** — which is the same moment this procedure begins.
Not the later moment you conclude it was notifiable.

If you are uncertain whether it is notifiable, the clock is already running while you
decide.

---

## 6. If a guardian contacts you

The guardian is not automatically entitled to the account's contents. They are asking
you to stop processing, which you should do regardless.

**Do:** confirm the account is suspended and that the data will be removed at the end
of the window (or has been, if it has passed).

**Do not:** hand over the host's records about the account, contact details, or
anything else about it without being satisfied the person is the guardian and is
entitled to it. The account
holder is a data subject, and a minor's data protection rights are not suspended by
being a minor.

If uncertain, take advice before disclosing.

---

## 7. Review

Every execution of this procedure is recorded. If it runs three or more times in a
rolling twelve months, counted by the operator from the takedown records at the
quarterly review of RISK-001, the self-declaration control is not proportionate in
practice, and R-A05 in the risk register is reconsidered (D-147).

**Currently accepted on the basis that the realistic case is rare**: a 17-year-old
acting for their household, not a minor covertly using the service. Frequency is the
evidence that would overturn that.
