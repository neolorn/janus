# 01 — Identity

The account record, organizations, membership, and the lifecycle of each.

**Prerequisite:** `00-overview.md`, sections 1 and 3.

**Scope.** This document covers *who exists*. How they prove who they are is
`02-authentication`. What they may do is `03-authorization`. What may lawfully be
held about them is `04-privacy`. What an account holds and how it is registered is
`20-registration-and-account`.

---

## 1. Accounts

### 1.1 Identity ownership

**IDN-ACCT-001** — An account SHALL be the sole source of truth for a person's
identity within a deployment. An external identity provider SHALL NEVER be the
source of truth.

*Source: D-031 context, original spec*

Google and Apple sign-in attach a **credential** to an existing account. They do not
create an identity record that lives elsewhere.

**Acceptance criteria**
1. Deleting a linked Google credential leaves the account intact and usable via its
   other credentials.
2. No account can exist that has no local record, regardless of how it was created.

---

**IDN-ACCT-002** — Each account SHALL carry a **stable opaque subject identifier**,
generated at creation, never derived from any personal attribute, and **never
reused** after deletion.

*Source: D-005*

This identifier is what audit records, grants, and any future OIDC tokens reference.
It is the reason erasure can anonymise rather than destroy the record (`04-privacy`).

**Acceptance criteria**
1. The identifier contains no substring derived from email, phone, or name.
2. After an account is deleted, creating a new account never reproduces the
   deleted identifier.
3. Audit records written before deletion still resolve to the identifier afterwards,
   with personal data removed.

---

**IDN-ACCT-003** — Accounts SHALL NEVER be merged.

*Source: original spec, amended*

Two accounts remain two accounts. Account linking attaches a credential; it does not
combine identities.

**Acceptance criteria**
1. No API surface accepts two account identifiers and produces one.

---

### 1.2 Identifiers and normalization

**IDN-ACCT-004** — All identifiers SHALL be normalized to a single canonical Unicode
form at write time, before storage or comparison. The normalized form SHALL be
stored and compared; the original SHALL be retained for display.

Applies to: email addresses, phone numbers, organization names, display names,
usernames.

*Source: D-040, D-115, D-146, D-166*

**The canonical form is `NFKC_Casefold`** (Unicode Standard §3.13, the `NFKC_CF`
property): compatibility normalisation, full case folding and removal of
default-ignorable code points as one named operation. It applies to the whole of an
email address, and to organization and display names as their comparison key. An
organization name's comparison key SHALL be stored beside the entered name, derived from
it on every write, and SHALL NOT be null. It implies no uniqueness (D-166). Phone
numbers take no Unicode form: digits of any script (Arabic-Indic included) are
mapped to ASCII and the result is stored as E.164. The **Unicode version** of the
implementing library is pinned in the build and recorded as the canonicalisation
version beside every fingerprint (PRIV-RIGHT-005c).

**Implementation (D-154).** No base class library type implements `NFKC_Casefold`,
PRECIS or `Script_Extensions`, and `string.Normalize` follows the machine's ICU rather
than a pinned version. The library therefore carries its own tables, generated from the
Unicode Character Database at the pinned version (**Unicode 17.0.0**) by
`tools/Janus.UnicodeTables` (CONV-LAYOUT-001) and checked in under `Janus.Core`: the
`NFKC_CF` mapping, canonical decompositions, combining classes and composition
exclusions (so NFC and NFKC are the library's own), full case folding, general
categories and the derived properties PRECIS needs, `Script` and `Script_Extensions`.
The pinned version is a constant beside the tables and is the canonicalisation version
recorded with every fingerprint. The public types are `CanonicalForm` (`NFKC_Casefold`,
E.164 digit mapping), `Precis` (the two profiles below) and `ScriptMixing` (IDN-ACCT-005)
in `Janus.Core`, because Identity and Authentication both canonicalise. No package.
The ASCII form of a domain (REG-DOM-001) is the library's own in the same way: UTS #46
processing over the IDNA mapping table of the pinned version (`IdnaMappingTable.txt`,
carried beside the Unicode Character Database files), with RFC 3492 Punycode, never the
machine's ICU (D-166).

**Usernames** take the PRECIS UsernameCaseMapped profile (RFC 8265) and nothing else:
letters and digits, no spaces, case-folded, NFC, 3 to 32 characters, checked against
the reserved list (REG-IDENT-001, REG-IDENT-009). **Display names** are 1 to 64 bytes
under the PRECIS Nickname profile (RFC 8266); the rules are stated once in
REG-PROF-001 and are not repeated here. D-115 rejected PRECIS as the *canonical form*
for emails and names; D-146 adopted it as the *validation profile* for usernames and
display names. Both hold: `NFKC_Casefold` is the comparison key of every identifier,
PRECIS is what a username or display name must satisfy to be accepted (D-154).

Multiple code-point sequences render identically. Without normalization, two
accounts can hold visually identical addresses or names, indistinguishable to a
human, distinct to the database. D-008's admin-assisted recovery uses a single
approver who confirms identity partly by recognising a name; a lookalike name is
precisely the attack that defeats a human check.

**Acceptance criteria**
1. Two email addresses differing only in Unicode composition resolve to the same
   account; the second registration creates **no second account** and returns the
   same response as a fresh registration (D-076).
2. The display value returned to the user is the form they entered, not the
   normalized form.
3. Normalization is applied on registration, on email change, on phone change, and
   on organization creation.
4. `ａhmed@example.com` (fullwidth) and `ahmed@example.com` resolve to the same
   account.
5. A phone number entered with Arabic-Indic digits stores as the same E.164 value
   as its ASCII-digit form.
6. Changing the pinned Unicode version fails startup until the re-derivation in
   PRIV-RIGHT-005c has run **only where the new version changes the
   `NFKC_Casefold` mapping of a code point already stored**: Unicode's stability
   policy guarantees that assigned characters are stable under normalization and
   case folding (`toCasefold(toNFKC(S))`); the policy states that `NFKC_Casefold`
   is distinct, in that it also removes default-ignorable code points, and the
   guarantee is not claimed for it. The comparison prescribed here is therefore on
   the stored code-point range, which holds regardless: a routine ICU upgrade adds
   newly assigned characters and requires no re-derivation unless a stored code
   point's mapping changed. The build records the pinned version; startup compares
   it and consults the stored code-point range (D-135, D-147).

---

**IDN-ACCT-005** — Identifiers mixing scripts **within a single word** SHALL be
rejected. Whole-word Arabic and whole-word Latin are both permitted.

*Source: D-040, D-115, D-166*

"Single script" is as defined by **UTS #39 §5.1** (mixed-script detection using
`Script_Extensions`): characters of script `Common` or `Inherited` — digits,
punctuation, combining marks — are ignored when deciding. Digits and punctuation are
therefore never a foreign script.

**Acceptance criteria**
1. A display name of `Аhmed` (Cyrillic А) is rejected.
2. A display name of `أحمد Ahmed` is accepted — separate words, each single-script.
3. Rejection returns a named error code, not a generic validation failure.
4. `O'Brien`, `Jean-Luc` and `محمد2` are each accepted.
5. An organization name mixing scripts within a single word, judged on its comparison
   key, is refused with `identity.identifier.mixedscript` at creation and at bootstrap.

---

**IDN-ACCT-006** — Identifier comparison SHALL be case-insensitive.

*Source: D-040*

PostgreSQL compares case-sensitively by default. Without this, two accounts can
exist for one mailbox, and a lowercase-only lookup silently fails for a user who
typed a capital.

**Acceptance criteria**
1. Registering `Ahmed@example.com` when `ahmed@example.com` exists creates **no second
   account** and returns the same response as a fresh registration — never a duplicate
   error, which would be an enumeration oracle (D-076).
2. Signing in with `AHMED@EXAMPLE.COM` succeeds for an account registered in
   lowercase.

---

### 1.3 Account state

**IDN-ACCT-007** — An account SHALL be in exactly one state at any time.

| State | Meaning | Can sign in |
|---|---|---|
| `active` | Normal | Yes |
| `suspended` | Administratively disabled | No |
| `restricted` | Processing restricted at the data subject's request | Yes, read only |
| `deleting` | Deletion requested, grace window (`account.deletion.grace`, default 30 days) running — **cancellable until it elapses** | No |
| `deleted` | Personal data removed or key-destroyed, anonymised record retained | No |

*Source: D-006, D-037, D-026.1, D-113, D-146, D-166*

Entering `deleting` is the customer's exercise of the erasure right (PRIV-RIGHT-001);
no separate privacy request is created for it. There is no `pending` state: an account
is created `active` in one transaction at the end of the registration session
(REG-SESS-001), and an incomplete registration is a session, not an account.

**IDN-ACCT-007a** — *Retired by D-146. See REG-SESS-001.*

`restricted` exists because the right to restrict processing requires a state in
which the account still exists but cannot be acted upon (`04-privacy`). A `restricted`
account signs in and reads, as the table states; every modifying action stays refused
through the gate (AUTHZ-GATE-006). While restricted, rectification of the account's own
fields is a request (`POST /privacy/requests`, type `rectification`); ending sessions,
reporting a credential lost, listing and revoking app passwords, the link-borne undo
of an identifier change, and a credential set by recovery or enrolled where a policy hold
stops its sign-in (AUTH-FACT-017) stay available, since each grants nothing or restores
the sign-in the table grants (D-166). A restriction
the person asked for does not cut them off from their mail: a mailbox is owed `enabled`
while its holder is `active` or `restricted` (INT-MAIL-006), and a `restricted` account
keeps its app passwords but cannot create one (D-166).

**An operation the state does not admit (D-166).** An operation that does not apply to
the state an account is in SHALL be refused with `identity.account.stateconflict` (409),
`details.state` naming the state and, where it is `suspended`, `details.suspendedBy` its
origin, wherever `10` section 1.1 holds no code of its own for the case (as
`identity.takedown.active` and `identity.account.adminsuspended` are). An administrative
operation naming a subject no account bears SHALL be refused with
`identity.account.notfound` (404).

**Acceptance criteria**
1. Every account row has a state; none is null.
2. A `restricted` account signs in by any factor its policy allows, reads its own data
   and exercises data subject rights; every modifying action it asks of the gate, every
   change to its identifiers, credentials, profile and preferences (REG-ACCT-001) and
   every invitation acknowledgement is refused with `authz.restricted`
   (AUTHZ-GATE-006). A credential set by recovery, or enrolled where a policy hold
   (AUTH-FACT-017) stops its sign-in, is admitted.
3. Transitioning to `deleted` leaves the subject identifier resolvable and removes
   personal attributes.
4. `deleting` begun by `self` or `oob-request` is cancellable throughout its window,
   restoring the state the account left: `active`; `restricted` where a restriction is
   held (PRIV-RIGHT-004); or `suspended` with its origin where an out-of-band erasure
   began the window on a suspended account (IDN-LIFE-003). This matches organization
   deletion (IDN-ORG-003), which was cancellable while this was not. A takedown's
   `deleting` is not cancelled but reversed (IDN-LIFE-003).
5. Restricting an account ends its sessions (AUTH-SESS-010); it signs in again,
   `GET /account` answers, and `PUT /account/profile` answers 403 `authz.restricted`.
6. An administrator's suspension of a `deleted` account is refused with
   `identity.account.stateconflict` and `details.state` `deleted`; the same operation
   naming a subject no account bears is refused with `identity.account.notfound`.

---

## 2. Organizations

### 2.1 Nature

**IDN-ORG-001** — An organization SHALL be a domain entity within a single identity
pool, NOT a tenancy or isolation boundary.

*Source: D-001*

One deployment, one database, one identity pool. Organization #1 is the
**administrative organization**; its members are what would elsewhere be called
staff. It is identified by a boolean `administrative` on the organization, set only
by bootstrap (OPS-BOOT-001) and never through the application, with a unique partial
index guaranteeing exactly one such row; the domain reads `Organization.IsAdministrative`
(D-157).

**Acceptance criteria**
1. No query filters by organization for isolation purposes; organization scoping is
   an authorization concern (`03-authorization`).
2. A single account can hold membership in more than one organization at the data
   layer, regardless of policy (IDN-MEM-002).

---

**IDN-ORG-002** — Authentication policy SHALL attach to the organization, not to a
user type or an application.

*Source: D-002, D-020.1*

There is no staff flag. "Passkeys for interactive access" is an organization policy
value. A second organization needing different rules is a configuration row.

**Acceptance criteria**
1. No column, enum, or claim distinguishes staff from customers.
2. Changing an organization's factor policy changes behaviour for its members
   without a deploy.
3. Two organizations with different policies coexist and are enforced independently.

---

### 2.2 Organization lifecycle

**IDN-ORG-003** — Organization deletion SHALL follow request → suspend → grace →
**erasure**, and SHALL be cancellable at any point before the window closes.

**Erasure is not row removal.** The organization record persists; its personal and
identifying data becomes unreadable (PRIV-RIGHT-005). Nothing in this system is ever
physically deleted (IDN-PRIN-003).

| Stage | Effect |
|---|---|
| Deletion requested | Organization suspends immediately: every session of every current member ends in the same transaction, and no grant of the organization, stored, materialised or derived, confers anything until the request is cancelled (every evaluation reads the request live, AUTHZ-CACHE-001); no invitation is issued into it and none is acknowledged. A member may still sign in to their own account |
| Grace window | Configurable, default **30 days** |
| Cancelled | Memberships and grants restored in full, no data loss; the sessions the request ended stay ended |
| Window elapsed | **Erasure executes**: every current membership ends; the organization's name, and every domain it listed, is replaced by its identifier and each domain is marked removed; the grants scoped to it stay unrevoked and confer nothing, as from the request; no account is erased. **No row is removed** (IDN-PRIN-003) |

*Source: D-038, D-090, D-166*

A deletion request and its cancellation SHALL be asked of `organization:manage` in the
administrative organization, SHALL be the `organization:delete` step-up action, and SHALL
carry a `reason` (a free-text member, API-CONV-002); each, and the creation of an
organization, SHALL be recorded under the organization. A request for an organization
already being deleted, and a cancellation where none is pending, SHALL change and record
nothing. A cancellation at or after `organization.deletion.grace` from the request, or
after the erasure, SHALL be refused with `identity.deletion.windowelapsed`. A request or
cancellation whose path names no organization the deployment holds SHALL be refused with
404 `identity.organization.notfound` (D-166).

**Acceptance criteria**
1. Requesting deletion halts member access within one request cycle: the first request
   on any member session that reaches the session record after the commit is refused.
2. Cancelling on day 29 restores all memberships and grants intact.
3. Erasure does not execute before the configured window elapses.
4. The window is runtime-configurable with an enforced minimum.
5. After erasure the organization row still exists and its identifier still resolves.
6. The erasure, its audit record and its `MembershipChanged` and `OrganizationErased`
   events commit together or not at all.
7. A pass that cannot read `organization.deletion.grace` erases nothing.
8. The request ends every session of every current member in the transaction that
   suspends the organization; a member can sign in again and holds nothing through the
   organization while it is suspended.
9. A cancellation restores no ended session.
10. A cancellation at or after the window's end is refused with
    `identity.deletion.windowelapsed`, whether or not the erasure has run.
11. While deletion is requested, a derivation reaching a record of the organization
    admits no one through the check, the filter, the capability page or the
    explanation, materialised or not; cancelling restores the access the host's facts
    then give.
12. While deletion is requested, issuing an invitation into the organization answers
    403 `authz.denied`, since its grants confer nothing, and acknowledging one answers
    `identity.invitation.expired`.
13. A deletion request or cancellation naming an organization the deployment does not
    hold is refused with `identity.organization.notfound` and changes nothing.

---

**IDN-ORG-004** — The administrative organization SHALL NOT be deletable.

*Source: D-038*

Deleting it would leave a system nobody can administer.

**Acceptance criteria**
1. A deletion request naming the administrative organization is rejected with a
   named error.
2. The restriction is enforced in the domain, not only in a UI.

---

**IDN-ORG-005** — Data owned by a deleted organization SHALL be anonymised, not
destroyed.

*Source: D-026.1, D-038, D-166*

Audit history survives the organization.

**Acceptance criteria**
1. After erasure, audit records referencing the organization remain queryable.
2. Personal attributes within those records are held under their subjects' keys and
   become unreadable when each subject is erased (PRIV-RIGHT-005a); the organization's
   own name and domains are replaced at erasure (IDN-ORG-003).

---

### 2.3 Domain lock

**IDN-ORG-006** — An organization MAY restrict its members' email addresses to a list
of domains it has verified by DNS, through the policy field `emailDomains`, off by
default. The rules for verification, scheduled re-verification, alerts and removal are
REG-DOM-001.

*Source: D-146, D-166*

Domain lock is a property of the organization because it is the organization that
owns the domains and the policy that names them (IDN-ORG-002). It changes nothing
about the identity pool (IDN-ORG-001): a member's address is checked against the list,
not partitioned by it.

**Acceptance criteria**
1. With `emailDomains` off, no address is refused on grounds of its domain, except an
   address in a domain removed from the organization's lock and not listed anew
   (REG-DOM-001).
2. With the lock on, a member's sign-in email outside the verified list is refused
   with `identity.identifier.domainnotallowed` (REG-DOM-001 criteria).
3. The lock is a policy value of the organization, inherited from the system policy,
   and changing it requires no deploy.

---

## 3. Membership

**IDN-MEM-001** — Membership SHALL be a first-class record linking an account to an
organization, carrying its own lifecycle independent of both. Ending a membership SHALL
NOT revoke the account's grants in the organization; they are transferred or revoked as
their own step (`16` section 3 step 4), and no grant row is removed (IDN-PRIN-003). A
grant in the administrative organization SHALL confer only while its holder holds a
current membership of that organization.

*Source: D-002, D-003, D-166*

An administrator ends a membership under `membership:manage` in the organization, as the
`membership:end` step-up action; where the path names no organization the deployment
holds, the request is refused with `identity.organization.notfound` (404), and where the
account holds no current membership of the organization, with
`identity.membership.notfound` (404), each before anything is written (`09` section 8a).

**Acceptance criteria**
1. Ending a membership leaves both the account and the organization intact.
2. Membership records carry created and ended timestamps.
3. After a membership ends, the account's grants in the organization are still held
   and revocable.
4. After a membership of the administrative organization ends, no grant the account
   holds there confers.
5. Ending a membership without the `membership:end` step-up is refused with
   `auth.stepup.required`, and the membership stays current.

---

**IDN-MEM-002** — The data model SHALL support an account holding zero or more
memberships. Policy SHALL forbid more than one by default; enabling multiple
memberships SHALL be configuration. The memberships counted are the account's current
ones; one that has ended is not counted. A second current membership of an organization
the account already holds a current membership of SHALL be refused whatever the setting.

*Source: D-003, D-166*

Cheap to build now, expensive to retrofit. Forbidding is a validation rule; allowing
later would be a model change to something that assumed one-to-one.

**Acceptance criteria**
1. The schema permits multiple membership rows per account.
2. With the default setting, creating a second membership is rejected.
3. Enabling the setting permits it without a schema change or deploy.
4. An account whose only membership has ended joins another organization under the
   default setting.
5. With `organization.multiplememberships` on, a second current membership of the same
   organization is refused with `identity.membership.limitreached`, and the refusal's
   `details.organization` names it.
6. Two memberships attached to one account together, where the setting admits one,
   leave one current membership.

---

**IDN-MEM-003** — Authorization SHALL evaluate against the organization owning the
resource in question. The session SHALL NOT carry an "active organization."

*Source: D-003*

No organization switcher, unless multi-membership is later enabled and a UI needs
one.

**Acceptance criteria**
1. No session field names an organization.
2. A permission check on a resource resolves the organization from the resource, not
   from the session.

---

## 4. Account lifecycle

### 4.1 Registration

**IDN-LIFE-001** — *Retired by D-146. See REG-IDENT-001; a duplicate identifier is
handled as REG-SESS-005 states (no second account, the ordinary response, the owner
notified without a code).*

---

**IDN-LIFE-002** — *Retired by D-146. See REG-PROF-002.*

---

**IDN-LIFE-002a** — Where a host processes **sensitive personal data** about a
subject, that subject SHALL hold an account.

*Source: D-073, D-091, D-166*

Every rights mechanism hangs off an account — consent records, the dashboard, erasure,
restriction, takedown. Sensitive data about an identifiable person, attached to no
account, has no route by which any right can be exercised or honoured.

**This is a library rule about sensitive data**, not a rule about any particular host's
product. A host processing no sensitive data is unaffected.

**Data a subject enters about a third party is that subject's personal data.** A third
party's name, phone and address, entered by a customer on a record the host keeps for
that customer, are controlled by the customer and encrypted under the **customer's**
key (PRIV-RIGHT-005a). The third party holds no account, no key and no dashboard; they
remain a data subject in law, and any request they make is handled through the
out-of-band path (PRIV-RIGHT-001) against the customer's records. When the customer is
erased, the third party's details go with them. Third parties as subjects in their own
right (keys, consent and rights for people who never touched the system) is rejected
(D-131).

**Acceptance criteria**
1. No path exists by which a sensitive resource type is created without a subject
   holding an account: the registration of a record of a sensitive type
   (AUTHZ-INHERIT-002) that names no subject is refused with `api.request.malformed`
   naming `subject`, and one naming a subject that holds no account, or whose account
   is `deleting` or `deleted`, with `api.request.invalid` naming `subject`.
2. A third party's fields on a host record are encrypted under the key of the subject
   who entered them and become unrecoverable when that subject is erased.
3. A subject with sensitive data held about them has an account, a history and a
   privacy dashboard.
4. The takedown procedure always has an account to act on.

**Consequence for a host.** A host whose records about a subject are sensitive
(PRIV-SENS-001) gives every such subject an account, so there is **no anonymous path**
into those records. The cost is one screen: phone verification is required by default
(REG-IDENT-001, `registration.phone`), which is the entire friction an anonymous path
would have avoided. Registration, which opens with the age screen (REG-PROF-002), is
the single point at which the adult affirmation is collected.

---

**IDN-LIFE-003** A documented takedown procedure SHALL exist and be followed on
any credible indication that a customer is under 18: account suspended, host-side
processing for the subject stopped, personal data removed, event recorded.

*Source: D-148; D-039, D-127, D-147, D-166*

**Two phases, one operation.** Triggering the takedown does, in one transaction:
suspend the account, terminate its sessions (AUTH-SESS-010), record the event with
its trigger and reason, and write the outbox record that tells the host to stop its
own processing for the subject. That outbox record is the per-subscriber completion
record of that host-side work: it is written in the trigger transaction, the worker
delivers it and each
required subscriber confirms against it (IDN-LIFE-003a), and it is what the takedown
screen reads during the window (D-148). No erasures row exists yet: the erasures table
(IDN-LIFE-003b) describes an erasure, and none has happened. Access and processing stop
**now**. Erasure (key destruction and
fingerprint neutralisation) runs when the takedown's window closes: `takedown.grace`
(default **7 days**) after the trigger, or earlier for an account already `deleting`
(below). It runs through the ordinary erasure transaction (IDN-LIFE-003a), after which
the account is `deleted`. During the window the takedown can be **reversed** by a
holder of `takedown:execute`, for the case where an adult was misjudged, restoring the
state the account held at the trigger (below); host-side actions already taken on
`TakedownExecuted` are not undone by the library. From `active` the state sequence is
`active → suspended → deleting → deleted`, the first two transitions in the trigger
transaction; the account sits in **`deleting`** for the window.

**Where a takedown starts, and what a reversal restores (D-166).** A takedown SHALL be
triggered on an account that is `active`, `restricted`, `suspended`, or `deleting` by
`self` or `oob-request`. The account holds the state it was in (`10` section 5.12b:
`suspensionHeld` for a suspension with its origin, `restrictionHeld` for a restriction,
`deletionHeld` and `deletionHeldSince` for a running deletion's origin and start), and
a reversal SHALL restore it: `active`; `restricted` where a restriction is held
(PRIV-RIGHT-004); `suspended` with its `suspendedBy`, so that an administrator's
suspension is still lifted only under `account:manage` and an owner's deactivation only
by its owner (IDN-LIFE-013); or the deletion it was in, with its `deletingBy` and its
start. Where the account was
already `deleting`, the takedown's erasure falls due at the earlier of that window's end
and the trigger plus `takedown.grace`, so the takedown never erases later than the
subject's own request would have. The reversal and the erasure each read the account
under a lock in their transaction, so of a reversal and an erasure at the window's end
only one commits. A second trigger on a takedown-originated `deleting` is refused with
`identity.takedown.active`; a trigger on a `deleted` account with
`identity.account.stateconflict`; a trigger, a read of its progress or a reversal naming
a subject no account bears with `identity.account.notfound`; and a read or a reversal of
an account holding no takedown with `identity.takedown.notfound`.

**The takedown borrows the deletion timer, not the deletion's emails or buttons.**
The `deleting` state records **why it was entered** (`deletingBy`: `self` ·
`takedown` · `oob-request`), mirroring `suspendedBy` (IDN-LIFE-013); both
enumerations are fixed in `10` section 5.12b (D-147). For `oob-request` (an erasure
request received out of band and entered through the privacy-request queue,
PRIV-RIGHT-002): the subject's security-notice set is sent the out-of-band deletion
notice (`oob-deletion-notice`), which carries **no cancel link**,
because the requester may not control the subject's addresses; cancellation is
through the privacy-request queue only, by the administrator handling the request
(`POST /admin/accounts/{subject}/delete/cancel` during the window, the
`account:deletioncancel` step-up action, recorded against the request: the audit record
of the cancellation names the fulfilled erasure request whose decision began the
window), and the link-borne `POST /account/delete/cancel` has nothing to consume
(D-147). Fulfilling the request is the `privacyrequest:fulfil` step-up action
(PRIV-RIGHT-001). What a fulfilled out-of-band erasure does depends on the state it
finds (D-166): an account `active` or `restricted` enters `deleting` by `oob-request`,
holding a restriction where one is in force; an account `suspended` SHALL begin its
deletion all the same, holding the suspension with its origin (`suspensionHeld`), so
that a cancellation returns the account to `suspended` with its `suspendedBy`; an
account already `deleting`, by any origin, has the request recorded fulfilled against
the running window, and nothing restarts; an account `deleted` has the request recorded
fulfilled, and nothing further happens. For
`takedown`: **no deletion notification and no cancellation link are sent to the
subject**; `POST /account/delete/cancel` and `POST /admin/accounts/{subject}/delete/cancel`
refuse with `identity.takedown.active`; the only way back is
`POST /admin/accounts/{subject}/takedown/reverse` under `takedown:execute`, with a
reason. A takedown the subject could cancel from their inbox would not be a takedown
(D-137). `AccountSuspended` and `TakedownExecuted` fire at every trigger, whatever state
the account held: `AccountSuspended` announces that access stopped (D-166). No
`AccountDeletionRequested` fires. `TakedownExecuted` is the trigger's outbox record
(IDN-LIFE-003a); `AccountSuspended` is written as an event row in the same transaction.
The reversal writes `TakedownReversed` in its own transaction the same way. Neither is
delivered before its transaction commits (CONV-DESIGN-002).

**Why a window.** Immediate key destruction would make a mistaken takedown
unfixable and would leave nothing for the host's follow-up and reporting steps in
`14-takedown-procedure` to contact. Nothing is processed during the window: the
account cannot sign in, staff cannot act on it, and the host has already stopped its
own processing for the subject.

The affirmation verifies nothing. What makes self-declaration proportionate for
sensitive data is that the situation has a defined outcome rather than being decided
on the spot.

**Acceptance criteria**
1. The procedure exists as `14-takedown-procedure.md`.
2. The system provides an operation performing all four steps **reliably, with
   per-subscriber completion visible**, not atomically (IDN-LIFE-003a): the
   host-side work's completion is read from its outbox record from the moment of
   trigger, the erasure's from the erasures row once phase two has run. The progress is
   read at `GET /admin/accounts/{subject}/takedown`.
3. Execution is recorded in the audit trail with the reason.
4. On a trigger on an `active` account, in one transaction, the account passes through
   `suspended` into `deleting` with `deletingBy = takedown`, its sessions are ended,
   `TakedownExecuted` is written as an outbox record carrying per-subscriber
   confirmation and `AccountSuspended` as an event row; no personal field is destroyed
   yet and no erasures row is written.
5. When the takedown's window elapses without reversal, the erasure transaction runs and
   the account is `deleted`; reversal inside the window restores the state the account
   held at the trigger. The window closes at its end whether or not the sweep has
   reached the account; a reversal after it, or of an erased account, is refused with
   `identity.takedown.windowelapsed`.
6. No deletion notification reaches the subject; both `delete/cancel` endpoints refuse
   a takedown-originated `deleting` with `identity.takedown.active`.
7. A trigger on an account `deleting` by `self` or `oob-request` takes it into the
   takedown's window; its erasure falls due no later than its own window's end, both
   `delete/cancel` endpoints then refuse with `identity.takedown.active`, and a reversal
   returns it to its own deletion with its origin and its start.
8. A takedown of an account an administrator suspended, reversed inside its window,
   returns it to `suspended` by `administrator`; an out-of-band erasure fulfilled on a
   `suspended` account begins its window, and cancelling it returns the account to
   `suspended` with its origin.
9. A trigger on an account that is `restricted`, `suspended` or `deleting` writes
   `AccountSuspended` as an event row, as a trigger on an `active` account does.
10. An out-of-band erasure fulfilled on an account already `deleting` leaves its window's
    end unchanged, and one fulfilled on a `deleted` account changes nothing but the
    request, which reads `fulfilled`.

**Atomicity was claimed and is unachievable.** Two steps are library work; two belong
to the host application, in tables the library never touches (LIB-HOST-002). An
operation spanning that boundary cannot be atomic, and an implementer building to the
old criterion would have reported "complete" the moment the library finished, while a
minor's personal data was still present.

---

**IDN-LIFE-003b** — Erasure progress SHALL be tracked in a **dedicated erasures table**,
not in the account record.

*Source: D-094, D-102, D-147, D-166*

| Column | Purpose |
|---|---|
| Subject reference | Whose erasure this is |
| Requested at | When |
| Reason | `erasure-request` · `minor-takedown` (`10` section 5.12a, D-147); an organization erasure erases no account (IDN-ORG-003) |
| Status | `awaiting-subscribers` · `complete` · `failed` |
| Attempts | Retry count across subscribers |

**The row is written in the same transaction as the erasure itself** — the state
change, the overwrite of the subject's wrapped key and the neutralisation of the
fingerprint (PRIV-RIGHT-005c). It therefore never describes the library's own steps,
which have already committed if the row exists; it describes the **host-side work**
still outstanding. `awaiting-subscribers` means required subscribers have not all
confirmed; `failed` means a subscriber exhausted its retries and the manual completion
path (IDN-LIFE-003a) is pending; `complete` means every required subscriber confirmed.

**The account's own states are unchanged** — `active` → `deleting` → `deleted`. An
erasure is a background operation, not a phase of the account's life, and adding a state
for it would oblige every reader of an account to handle a value that describes a job
rather than a person.

**Access has already stopped** before any row appears here: the account entered
`deleting` when its grace window began, by its own request, by an out-of-band request or
by a takedown (IDN-LIFE-003).

**Status is a constrained column, not a lookup table.** The code branches on each value —
*failed* means manual completion, *complete* means stop — so a value added without code
would be inert. A native database enum type SHALL NOT be used; altering one is a
migration with locking behaviour, where a check constraint gives the same guarantee and
changes freely.

**Acceptance criteria**
1. No column is added to the account record for erasure progress.
2. Every incomplete erasure is retrievable in one query.
3. An unrecognised status value is rejected by the database.
4. A crash at any point leaves either **no row** — the transaction rolled back and
   nothing was erased — or a row whose key destruction and fingerprint neutralisation
   committed with it. No third state exists.

---

**IDN-LIFE-003a** — Erasure, restriction and takedown SHALL be delivered by a
**transactional outbox**, and SHALL be **generic** — the library publishes facts about
identity and knows nothing of who consumes them.

*Source: D-148; D-090, D-102, D-166*

**The flow:**

1. **One transaction** — the identity change and an outbox record are written
   together, and, where the operation erases, the erasures row (IDN-LIFE-003b) with
   them. For an erasure, the subject's wrapped key is
   overwritten and the fingerprint neutralised **in that same transaction**
   (PRIV-RIGHT-005a, PRIV-RIGHT-005c). None can exist without the others. A takedown
   runs this step twice: at trigger, with `TakedownExecuted` as the outbox
   record and no erasures row; and when the takedown's window closes, as an ordinary
   erasure (IDN-LIFE-003, D-148). The outbox record carries each required
   subscriber's confirmation, so the completion of the host-side work is visible from
   the trigger onward
2. **The worker publishes** to every registered subscriber
3. **Subscribers act** in their own tables
4. **Each confirms** independently
5. **Complete** only when every required subscriber has confirmed

**Generic by construction.** The event says *"account X was erased"*, never *"redact
X's records in the host's tables."* A second application registers as another
subscriber and the library changes not at all.

**Requirements on the mechanism:**
- **Subscribers SHALL be idempotent.** Delivery is at-least-once; a retry may arrive
  after a successful attempt. Built from the start rather than retro-fitted
- **A subscriber that faults SHALL be treated as one that did not confirm.** The
  publisher catches the fault, counts the attempt and schedules the next under the
  backoff below, exactly as for a subscriber that answered failure; the fault ends
  nothing for another subscriber or another delivery
- **Subscriber names SHALL be distinct**, each following the name rule of INT-SMS-003
  (a name that breaks it is refused at startup with `model.startup.declarationinvalid`),
  and `erasure-ledger` is the library's own (DR-016): startup refuses two subscribers
  under one name, or one under `erasure-ledger`, with `model.startup.subscribername`
- **Retries SHALL use exponential backoff with jitter**, bounded — not indefinite. A
  request failing from a defect fails identically at every interval. The publisher runs
  every `outbox.poll.interval`; the schedule is `outbox.retry.initial` multiplied by
  `outbox.retry.factor` per attempt with full jitter, for `outbox.retry.maxattempts`
  attempts, after which the record is `failed` (D-153)
- **Exhausted retries SHALL alert immediately.** The failed record is a diagnostic
  signal, not somewhere failures go quietly
- **A manual completion path SHALL exist** for the permanent failure of an erasure,
  restriction or takedown delivery, itself recorded
- **Subscribers SHALL be marked required or optional.** An optional subscriber failing
  does not hold a takedown open
- **Key destruction and fingerprint neutralisation are inline, never subscribers.**
  Both are writes to the library's own tables — the wrapped key and the fingerprint
  live in the database (PRIV-RIGHT-005a, PRIV-RIGHT-005c) — so they share the
  transaction with the state change. The outbox carries only the host-side work

**Completeness is wider than the obvious applications.** Any overlooked copy — a
cache, a log, a derived analytics store — means the request was not honoured. Logs
hold identifiers rather than attributes (CONV-LOG-004) and caches hold identifiers
only, so both are in scope by having been considered rather than by assumption.

**Acceptance criteria**
1. The identity change and the outbox record share one transaction, and the erasures
   row shares it wherever the operation erases; a takedown trigger writes no erasures
   row, its erasure, when the takedown's window closes, does.
2. No event names a consumer or a consumer's domain.
3. A subscriber handling the same event twice produces the same result.
4. A request outstanding beyond its retry budget alerts immediately.
5. Key destruction and fingerprint neutralisation commit in the same transaction as
   the state change. A rollback leaves a live subject with readable data — never a
   live subject with unreadable data, and never an erased subject whose data still
   decrypts.
6. Adding a subscriber requires no library change; a missing handler fails startup
   (PRIV-RIGHT-005b).
7. Two subscribers under one name, or one named `erasure-ledger`, fail startup.
8. A subscriber that throws is counted as an attempt and retried under the backoff;
   the other subscribers' deliveries go on.
9. A takedown or restriction delivery whose retries are spent is closed by the manual
   completion path, and the takedown's progress then reads complete.

---

### 4.2 Email change

**IDN-LIFE-004** — *Retired by D-146. See REG-IDENT-004 to REG-IDENT-007.*

---

**IDN-LIFE-005** — *Retired by D-146. See REG-IDENT-004 and REG-IDENT-006 (step-up on adding and removing).*

---

**IDN-LIFE-006** — *Retired by D-146. See REG-IDENT-002 (the security-notice set) and REG-IDENT-004 to REG-IDENT-006.*

---

**IDN-LIFE-007** — *Retired by D-146. See REG-IDENT-006 (the undo window).*

---

**IDN-LIFE-008** — On removal or replacement of a sign-in identifier (REG-IDENT-006,
REG-IDENT-007), the session under which the change completes SHALL rotate and every
other session SHALL terminate; a replacement (REG-IDENT-007) completes when its swap
applies, and one that completes under no session (the old address's confirmation, an
enrolment session) terminates every session.

*Source: D-035, D-033.4, D-146, D-166*

**Acceptance criteria**
1. Sessions on other devices are invalidated within one request cycle: the first request
   on any of them that reaches the session record after the commit is refused.
2. A replacement staged in one session and completed by a code typed in another leaves
   only the completing session live, under a new secret.
3. A replacement completed by the old address's confirmation ends every session of the
   account.

---

**IDN-LIFE-009** — *Retired by D-146. See REG-IDENT-004 (an added identifier counts only once verified).*

---

### 4.3 Phone change

**IDN-LIFE-010** — *Retired by D-146. See REG-IDENT-004 to REG-IDENT-007.*

---

**IDN-LIFE-011** — *Retired by D-146. See AUTH-ABUSE-004 (restrictions).*

---

### 4.3a Staff onboarding

**IDN-LIFE-009a** — Administrative-organization membership SHALL be granted through an
**invitation flow** issuing a time-boxed enrolment link, not by public registration
followed by a grant.

*Source: D-148; D-079a, D-146, D-166*

The only account-creation path was public registration, so a staff member would
register as a customer (the public application's client identifier, marketing consent,
a password) and be granted membership afterwards, with nothing saying when the
organization's policy began to apply to the credentials they already held.

An invitation MAY bind the email, the phone, both or neither; a bound identifier is
pre-filled and locked during registration. Where the organization is the administrative
organization and the deployment has a mail server registered (REG-MAIL-001), the
invitation names a personal email beside the asserted corporate address;
the account is not created until that personal email is verified by the press on the
invitation link, and it stays on the account as a verified non-primary email in the
security-notice set for the whole membership, becoming primary automatically when the
membership ends (REG-MAIL-001, REG-MAIL-003, D-148). Membership attaches on the person's
acknowledgement at the membership step, and the acknowledgement (documents shown,
versions, timestamp) is recorded on the membership (REG-INV-001). A person who already
holds an account accepts by signing in; no second account is created (REG-INV-002).

**IDN-LIFE-009b** — On gaining membership of an organization, any factor that
organization's policy does not permit SHALL be **disabled for interactive use**. Every
live session of the account SHALL be downgraded when the membership attaches
(AUTH-SESS-009), and a factor the policy does not permit SHALL satisfy no step-up.

*The criteria below cover IDN-LIFE-009a and IDN-LIFE-009b together.*

*Source: D-079a, D-166*

A password that was valid for a customer is not valid for an administrative-
organization member under a passkeys-only policy. It stops being usable to sign in;
it is not deleted, since membership may end.

**Acceptance criteria**
1. Staff membership originates from an invitation, never from a bare grant. The one
   exception is the three memberships bootstrap attaches (the first administrator,
   `emergency` and the restore-test canary, OPS-BOOT-001).
2. The enrolment link is time-boxed and single-use: the press that opens it (a
   registration begun with its token, or a signed-in `POST /register` carrying it)
   spends it, and a registration abandoned or expired does not give it back.
3. A password held before membership no longer authenticates after it.
4. The mailbox is provisioned disabled at invitation and enabled by the membership
   (INT-MAIL-006, REG-MAIL-001), never by registration.
5. After the membership attaches, a session the account held before it passes no
   step-up gate until a factor the organization's policy permits is presented, and a
   factor it does not permit is refused at step-up (`auth.factor.notpermitted`).

---

### 4.4 Account linking

**IDN-LIFE-012** — Linking a Google or Apple identity SHALL attach a credential and
SHALL have no effect on permissions or identity.

*Source: D-005, original spec, D-146, D-166*

The provider's email is never a key. It counts as verified by the sign-in only where
the provider operates the mailbox; any other provider-supplied address is verified by
one code like a typed address (REG-IDENT-008). The provider's subject identifier of a
linked identity is held on the linked credential as a keyed fingerprint, unique per
provider, beside that subject encrypted under the account's key (PRIV-RIGHT-005c); a
provider's event finds the credential by it, and it is never shown.

**Values (D-166).** The round trip is the library's own (`09`,
`GET /auth/providers/{provider}`). The library is a confidential client of each provider
the deployment declares (LIB-HOST-001), under the first declared client identifier, and
reads the provider's credential through the secret source (LIB-EXT-001) by the
provider's name when the application starts; a credential the secret source does not
give, or gives empty or unusable, stops the start with `model.startup.secretunavailable`,
`details.key` `socialProvider.<provider>`. Where the credential is a signing credential
(issuer, key identifier and a P-256 private key), the client secret is minted at each
exchange: header `alg` `ES256` and `kid`; claims `iss` the credential's issuer, `iat` now, `exp` 5 minutes later, `aud` the issuer the provider's discovery
document names, `sub` the client identifier; it is never stored. The round trip asks
for the scope `openid email` and nothing else, authenticates at the provider's token
endpoint with `client_secret_post`, asks for `response_mode=form_post` where the
discovery document lists it, and sends a PKCE `S256` challenge where the document lists
`S256` in `code_challenge_methods_supported`; otherwise it relies on the `nonce`,
validated in the identity token from the token endpoint before anything the token says
is used (RFC 9700 sections 2.1.1 and 4.5.3.2). The identity token is accepted only
where its signature verifies under the provider's published keys, its `iss` is the
issuer the discovery document names, its `aud` is the client the round trip was started
under, it has not expired and its `nonce` is the round trip's.

**Acceptance criteria**
1. Linking changes no grant and no membership.
2. Unlinking leaves the account usable via its remaining credentials.
3. Unlinking the only remaining credential is rejected.
4. An identity token whose `iss`, `aud` or `nonce` is not the one this item names, or
   whose signature the provider's published keys do not verify, signs in and links
   nothing.
5. With a signing credential supplied, every code exchange presents a client secret
   minted for it that expires 5 minutes after it is made, and exchanges a year apart
   both succeed with no redeclaration and no restart.

---

**IDN-LIFE-012a** — The library SHALL consume the security events the social providers
send about a linked identity: Google's Cross-Account Protection (RISC, Security Event
Tokens) and Sign in with Apple's server-to-server notifications. On an event that says
the provider account was compromised, disabled, or its sessions revoked, every session
of the linked account SHALL end and the linked credential SHALL be `suspended` until
the person signs in by another factor; on consent revoked or account deleted at the
provider, the credential SHALL be unlinked (IDN-LIFE-012 AC3 still refuses to remove
the last credential, in which case the account is `suspended` with a security notice
to the security-notice set); on an email disable, the provider-verified identifier
SHALL drop to unverified. Every event is verified against the provider's
published keys, is idempotent by its `jti`, and is audited.

*Source: D-164, D-166*

A person's Google account is taken over and Google tells every relying party within
seconds. A system that ignores that is choosing to keep the attacker signed in.

**Event types (D-164).** End every session and hold the credential: Google
`sessions-revoked`, `account-disabled` (any reason), `account-credential-change-required`
and `tokens-revoked`. Unlink, or suspend where the credential is the last: Apple
`consent-revoked` and `account-delete` (also delivered as `account-deleted`). Drop the
named address to unverified: Apple `email-disabled`. Every other type, `account-enabled`
included, is recorded and changes nothing; neither provider sends an event for an email
change.

**What each outcome does (D-166).** The credential is the last where no other usable
credential the account holds, the password included, may begin a sign-in. The account's
suspension is then recorded with `suspendedBy` `administrator`, ends every session in
the same transaction (AUTH-SESS-010) and announces `AccountSuspended`; an account its
owner deactivated is taken over without a new announcement, and one an administrator
suspended, one in its deletion window and one erased are left as they are. The personal
email a membership keeps (REG-MAIL-001, REG-MAIL-003) stays verified, and the event is
recorded. Where the address that drops held the primary role, the role passes to the
earliest verified email that may hold it, and stays vacant where none may. A held
credential is in the `suspended` state with no invalidation instant, which the
loss-report sweep never reaches. The first session begun for the account on factors that
exclude the held credential's restores it in that transaction, recorded as
`auth.credential.restored`; a credential suspended by a loss report is not restored this
way.

**How an event is taken (D-166).** An event is claimed by its `jti` under its
provider's callback name, in the transaction its work runs in; an event that carries no
`jti` is refused as unreadable (RFC 8417 section 2.2). A carried event is recorded as
`auth.providerevent.taken`; a replayed one as `auth.providerevent.rejected` and answered
as a carried one is; one the provider's keys do not verify as
`auth.providerevent.rejected` against the account its unverified claims name, where they
name one, and refused. The provider's subject identifier is never recorded or logged.

**Acceptance criteria**
1. A signed `sessions-revoked` or `account-disabled` event ends every session of the
   linked account and suspends the credential; an unsigned or replayed event changes
   nothing and is audited as rejected.
2. An Apple `consent-revoked` or `account-delete` event unlinks the credential, or
   suspends the account with a notice when it is the last one.
3. The endpoint `POST /callbacks/providers/{provider}` is on the machine profile
   (BFF-MACH-001) and rate-limited like every callback; it answers as `09` section 10
   states for each provider.
4. A `consent-revoked` event on an account whose only other credential is one a
   provider's event already holds suspends the account and leaves the credential linked.
5. An `email-disabled` event naming the personal email a membership keeps leaves it
   verified, and the event is recorded.
6. A credential held by a provider's event stands again at the first session begun on
   other factors, recorded as `auth.credential.restored`.
7. An event carrying no `jti` changes nothing and is refused.

---

### 4.5 Deactivation and deletion

**IDN-LIFE-013** — Deactivation SHALL suspend all grants without removing them.
Deactivation is the account's own entry into the `suspended` state (IDN-ACCT-007), the
same state administrative suspension produces. **The account records who suspended it**
(`suspendedBy`: `self` · `administrator`, `10` section 5.12b), because the two are
reversed differently: a self-deactivated account is reactivated by its owner through a
**link token in the deactivation notice** (link kind `reactivation`, `10` section 5.43,
which acts only on the person's press and never on the page's load; the same shape as
deletion cancellation, since a suspended account cannot sign in); an administratively
suspended account is reactivated only by an administrator (`account:manage`). An
administrator's suspension of an account its owner deactivated SHALL make it an
administrator's suspension (`suspendedBy = administrator`), which the deactivation link
and recovery no longer reverse. An administrator SHALL NOT reactivate an account its owner deactivated
(`identity.account.stateconflict`). An administrator's suspension of an account that is
`deleting` or `deleted` SHALL be refused with `identity.account.stateconflict`;
suspending an account an administrator already suspended changes nothing. Where the
deactivation notice is lost, the owner uses ordinary recovery (`/recovery/begin`), which
is available to a self-suspended account and reactivates it on completion (D-140).
Reactivation, by the link, by recovery or by an administrator, SHALL return an account
that holds a restriction (`restrictionHeld`) to `restricted`, and any other to `active`
(PRIV-RIGHT-004).

The reactivation link in the deactivation notice is valid for as long as the account
is `suspended` with a `self` origin; it has no separate lifetime, as the deletion-cancel
link lives the whole grace window (D-153).

*Source: D-006, D-125, D-135, D-147, D-166*

**Acceptance criteria**
1. Reactivation restores prior access exactly.
2. After an administrator suspends a deactivated account, the deactivation link
   answers `identity.account.adminsuspended` and recovery does not restore it.
3. An administrator's reactivation of an account its owner deactivated is refused and
   changes nothing.
4. An account restricted, suspended by an administrator and reactivated is
   `restricted`, and no `RestrictionChanged` is emitted on either side.

---

**IDN-LIFE-014** — Deletion SHALL remove personal attributes, retain the
anonymised audit trail, and revoke all grants, whose rows are kept (IDN-PRIN-003). It
SHALL execute when the grace window (`account.deletion.grace`) elapses without
cancellation. An erasure request fulfilled out of band begins this window, or is
recorded against one already running, as IDN-LIFE-003 states.

*Source: D-026.1, D-006, D-113, D-166*

**Acceptance criteria**
1. After deletion, no personal attribute is retrievable.
2. Audit records still show which actions occurred and when.
3. The subject identifier is not reissued.

---

**IDN-LIFE-015** — Account state SHALL propagate to Stalwart with a stable idempotency
key, and reconciliation SHALL run daily, flagging drift without auto-correcting. Mail
provisioning follows the state each mailbox is owed, read from its holder's account
state and memberships, and consumes no event (INT-MAIL-006, INT-MAIL-007).

*Source: D-006, D-041, D-166*

A failed suspension leaves a person reading mail after offboarding. Silent
auto-correction conceals a broken pipeline.

**Acceptance criteria**
1. Pushing the same owed state again produces no duplicate in Stalwart.
2. Reconciliation reports a discrepancy without changing either side.
3. A propagation failure is visible in monitoring, not silent.

---

**IDN-PRIN-003** — **A record of something that happened SHALL NOT be physically
removed.** Transient working artefacts SHALL be removed once they have served their
purpose.

*Source: D-090, D-166*

| Never removed | Removed when spent |
|---|---|
| Accounts, organizations, memberships | Expired sessions |
| Grants, including revoked ones | Consumed tokens and one-time codes |
| The host's own records of what happened (the host applies this rule to its tables) | Elapsed grace windows |
| Audit records, within retention | Delivered event records, once every consumer has taken them (the erasure and takedown deliveries are records, IDN-LIFE-003b, and stay) |
| Consent and notice-presentation records — rows kept; personal content key-destroyed at the end of `retention.consent` (PRIV-RET-001, D-135) | Cache entries |
| | Expired audit partitions (PRIV-RET-002) |

**The line is whether it is history.** A record of something that happened has
operational and evidential value; removing it breaks referential integrity and leaves
audit records pointing at nothing. A working artefact that has served its purpose is
clutter, and for expired sessions a small liability — several requirements already
mandate sweeping these.

**For records that stay, removal is expressed as state** — suspended, restricted,
revoked, deleted — and, where personal data is involved, as **erasure**: the record
remains and its identifying fields become unreadable (PRIV-RIGHT-005a).

**Two consequences:**
- Erasure never removes a row. It destroys a key and neutralises a fingerprint
- Compensating an action inserts a record; it never removes one

**Acceptance criteria**
1. No operation removes a row from the left column.
2. An erased subject's identifier still resolves, with no readable personal field.
3. Audit records referring to an erased subject remain resolvable.
4. Sweeps of the right column run without human action (OPS-OBS-003).

---

## 5. Account attributes

### 5.1 Language preference

**IDN-ATTR-001** — The account SHALL carry a **language preference**, user-changeable
and visible to the person.

*Source: D-055, D-146, D-166*

A notification sent by a background job has no request context, so something must
answer what language the person reads. A preference belongs to a person, and people
are what identity holds; left elsewhere, the worker would invent its own store and
two places would claim to know.

Beside the language sit the **time zone** and the **host-declared preferences**, a
typed store the host declares at startup and the library validates without ever
branching on a value (REG-PREF-001).

**Resolution order for any outbound message:**

1. Stored account preference, where set and where it finds a declared language
2. The locale of the current request, where the request is the recipient's own:
   registration, a sign-in, a sign-in link or code, a recovery request, or any request
   the signed-in person makes about their own account; a request made by anyone else is
   never read
3. **Every language in `notification.languages`** (the deployment declares them,
   D-153), only when neither finds one

A preference, or the request's `Accept-Language` priority list, SHALL be matched against
`notification.languages` by the lookup of RFC 4647 section 3.4, each range in priority
order, a range with `q=0` left out; what is found is always a declared tag.

**Every declared language (D-166).** An email resolved to every declared language SHALL
be one message carrying them all, composed by the library from each language's rendered
template in `notification.languages` order, the subject lines joined; no rule for
composing several languages enters the message catalogue. An SMS resolved to every
declared language SHALL be one message per declared language. How each is judged and
counted against the sending restrictions is AUTH-ABUSE-004.

**Registration SHALL set the preference to the declared language the request locale
finds, and to none where it finds none**, so the verification message matches the
language in use and case 3 becomes nearly unreachable.

**Acceptance criteria**
1. The preference is editable by the person and appears in the subject access export.
2. A background-triggered notification resolves language without a request.
3. An account with no preference and no request receives every declared language: one
   email carrying them all, or one SMS for each.
4. A request whose `Accept-Language` is `fr-CH, en;q=0.8`, on a deployment declaring
   `ar` and `en`, resolves to `en`; an administrator's request never decides the
   language of a message to someone else.

---

### 5.2 Profile photo

**IDN-ATTR-002** — A profile photo SHALL be supported for all accounts, with
**availability controlled by organization policy**. Disabled for every organization
until enabled, the administrative organization included; available to enable once the
deployment declares an image codec (LIB-HOST-001).

*Source: D-060, D-002, D-162, D-166*

**Values (D-166).** Availability is the policy field `photos` (`10` section 4.1a), a
boolean whose system default is `false`; setting it to `true` is a loosening
(OPS-CFG-002). It is resolved as AUTH-PRIN-002 resolves every field: an account holding
no membership follows the system policy, and an account of several organizations follows
the strictest, so it shows a photo only where every organization it belongs to enables
photos. The rule governs every read of the photo, an administrator's included; a read
where no photo is shown, because none is set or the policy withholds it, is answered
404 `identity.photo.notfound` alike. Bootstrap cannot see the host's declarations, so it
writes the administrative organization's `photos` as `false` (OPS-BOOT-001); enabling
photos is an administrator's policy change. A change that sets `photos` to `true` in
any policy while the deployment declares no image codec is refused with
`config.value.notallowed`, `details.field` `photos` and `details.requires` `imageCodec`,
and changes nothing. While any stored policy enables photos and no image codec is
declared, startup fails with `model.startup.declarationmissing`, `details.key`
`imageCodec`.

**Acceptance criteria**
1. Availability is a policy value, not a user-type branch.
2. Enabling it for another organization requires no code change.
3. After bootstrap the administrative organization's `photos` is `false`, and a
   deployment that declares no image codec starts.
4. With no image codec declared, a policy change setting `photos` to `true` is refused
   with `config.value.notallowed` and changes nothing.
5. An account of two organizations, one of which does not enable photos, shows no
   photo: `GET /account/photo` and `GET /admin/accounts/{subject}/photo` answer 404
   `identity.photo.notfound`, as for an account with no photo set.

---

**IDN-ATTR-003** — Photo bytes SHALL be stored in the database, in a table of their
own, so ordinary queries never read them.

*Source: D-060*

Storage on the filesystem would sit outside the backup regime, split erasure into two
operations that can disagree, and need its own serving path with its own authorization.
All three are avoided by keeping it in the database, and the size objection does not
apply to re-encoded profile images.

**Photo bytes are per-subject encrypted like any other personal field** (PRIV-RIGHT-005a),
so erasure is key destruction and the row persists (IDN-PRIN-003). This also brings the
photo under PRIV-SENS-002's criterion that a database dump yields no personal field —
which an unencrypted image would have failed.

**Acceptance criteria**
1. Erasing the account renders the photo unreadable in the same transaction
   (IDN-LIFE-003a) — the photo is encrypted under the subject key that transaction
   destroys.
2. Ordinary account queries do not read photo bytes.
3. The photo is served through the access gate, never a public URL.

---

**IDN-ATTR-004** — Uploads SHALL be validated by **content**, size- and
dimension-limited server-side, and **re-encoded on upload**.

**Values (D-153, D-166).** The decoding and re-encoding are the host's (D-162): the
image codec the deployment declares (LIB-HOST-001) receives the uploaded bytes and
`photo.maxdimension`. The formats and the quality are that codec's contract, which it
SHALL meet: it accepts JPEG, PNG and WebP, recognised by content and never by extension
or declared type, and answers the image re-encoded as JPEG at quality 85 with every
metadata segment removed, or nothing for bytes it does not accept. The library stores
what it answers, encrypted, refuses the upload with `identity.photo.invalid` where it
answers nothing, and serves the stored image at `GET /account/photo` as `image/jpeg`. An
upload longer than `photo.maxbytes` is refused with `identity.photo.toolarge` before the
codec reads it.

*Source: D-060, D-153, D-162, D-166*

Re-encoding strips metadata. Photographs carry location and device information by
default; a photo with coordinates is an exposure nobody intended.

**Acceptance criteria**
1. A file with a mismatched extension is rejected.
2. Metadata is absent from the stored image.

---

### 5.3 Addresses

**IDN-ATTR-005** — **Addresses are host data, not identity data.** The library holds
no address.

*Source: D-061, D-079b*

An earlier draft placed a delivery vendor's area identifier in the identity model: a
vendor identifier in a core namespace, which the decoupled-by-default principle
forbids, and domain data in a library that must know nothing of the host's domain.
How the host models the addresses it stores is the host's decision.

**Acceptance criteria**
1. No library table holds an address.
2. No vendor identifier appears in a library namespace.

---

**IDN-ATTR-006** — Coordinates SHALL NOT be stored. Location resolves to an area and
is discarded.

*Source: D-061*

**Acceptance criteria**
1. No schema field holds latitude or longitude for a person.

---

### 5.4 Profile fields

**IDN-ATTR-007** — The profile SHALL consist of the display name, the legal name, the
date of birth and the photo, each with the enablement, rules and editing path stated
in REG-PROF-001. Legal name and date of birth SHALL be off by default and switched by
`profile.legalname` and `profile.dateofbirth`.

*Source: D-146*

The photo keeps IDN-ATTR-002 to IDN-ATTR-004. Postal addresses stay host data
(IDN-ATTR-005).

**Acceptance criteria**
1. No profile field exists outside the four named here and in REG-PROF-001.
2. While `profile.legalname` and `profile.dateofbirth` are off, no request accepts the
   field and no response carries it.

---

### 5.5 Preferred second-step method

**IDN-ATTR-008** — Each account SHALL carry a **preferred second-step method**, chosen
by the person from the second factors enrolled on the account
(`PUT /account/secondstep/preferred`). It SHALL default to the most recently enrolled
second factor and SHALL be the method offered first at a second-step challenge, with
the other enrolled methods reachable from it.

*Source: D-146, D-166*

The preference is a convenience, not a restriction: every enrolled method remains
usable at the challenge, and the person is never held to a method they can no longer
reach.

**Acceptance criteria**
1. Enrolling a second factor on an account with none, or with a preference unset,
   makes it the preferred method.
2. Setting a method that is not enrolled on the account is refused with
   `api.request.invalid`, `details.member` `method`, and changes nothing.
3. Removing the preferred method moves the preference to the most recently enrolled
   remaining second factor, or clears it when none remains.
4. A second-step challenge presents the preferred method first and offers the others.

---

## 6. Non-human principals

**IDN-PRIN-001** — Background jobs, imports and webhooks SHALL each act as a named
principal with a real organization scope and a stated reason. A null principal SHALL
be rejected.

*Source: original spec, D-024*

Never "no user, therefore allow." That reasoning, once written, is a permanent hole
no code review catches because it looks sensible.

**Two principal scopes exist.** An **organization-scoped** system principal acts for
one organization and cannot read across them. A **deployment-scoped** system principal
exists for work that is inherently pool-wide: reconciliation, retention purging,
records-of-processing generation, expiry sweeps, delivery (carrying what has committed
to where it goes: the outboxes, mailbox provisioning and raised alerts), monitoring
(reading the state of something the deployment depends on and raising what the reading
calls for), bootstrap (OPS-BOOT-001), key rotation (OPS-SEC-003), configuration from
the server (a protected key, OPS-CFG-004, or the provider's client registry,
AUTH-OIDC-001) and the replay of the erasure ledger after a restore (DR-016). It is
restricted to the operations of `10` section 5.28. Each scheduled job of INF-BG-001 and
each server command runs as a principal of its own (`10` section 5.29), naming exactly
one of those operations and stating as its reason the item that requires it.

*Source: D-079b, D-166*

**Acceptance criteria**
1. Constructing a system context without a reason throws.
2. An organization-scoped principal cannot read across organizations.
3. A deployment-scoped principal is restricted to an enumerated set of operations and
   cannot serve a request.
4. Actions taken by either are audited with the reason.
5. Every scheduled job refuses to run under a principal that may not run its operation.

---

## 7. Audit

**IDN-AUD-001** — Every identity lifecycle event SHALL be recorded with acting
identity, effective identity, event time, and organization — **where one applies**.
Events on a principal holding no membership (a customer) carry no organization; the
field is nullable and its absence is the recorded fact, not an omission.

*Source: D-014, D-026.2, D-125, D-166*

Acting and effective identity are separate values, identical in every current path.
This is the impersonation seam — two fields where one would do, defensible on its
own terms because it makes "who did this" unambiguous.

**The subject of a record (D-166).** A record SHALL also name the data subject it
concerns, where it concerns one (`subject`, nullable): the account an action was taken
on, whoever took it. The effective identity SHALL equal the acting identity on every
record (AUTHZ-IMP-001); an action on another person's account names that person as the
subject, never as the effective identity. The trail by subject (PRIV-BREACH-002) reads
the records naming the subject as the acting identity or as `subject`.

An action a system principal took (IDN-PRIN-001) SHALL be recorded with the nil subject
as acting and effective identity and with the principal's name and its stated reason; a
record SHALL carry both the name and the reason or neither, and neither where a person
acted. Every read of the trail SHALL return them. A failed authentication establishes no
actor: its acting and effective identity are the nil subject, and its subject is the
account attempted, where one was resolved.

**Acceptance criteria**
1. Both identity fields are populated on every event; an event of background work
   carries the nil subject in both, and the principal's name and reason.
2. The recorded time is the instant the event occurred, not the instant it was
   written.
3. A record of background work reads back through `GET /admin/audit?subject=` with its
   principal and reason.
4. An administrator's suspension of another account is recorded with the administrator
   as acting and effective identity and the account as `subject`, and is returned by the
   trail of either.

---

## 8. Open items

None.

Cross-references for behaviour specified elsewhere: factor policy and verification
mechanics in `02-authentication`; grant semantics in `03-authorization`; consent,
sensitivity and data subject rights in `04-privacy`; Stalwart propagation contract
in `05-integrations`.
