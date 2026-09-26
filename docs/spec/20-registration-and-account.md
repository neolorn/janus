# 20 — Registration and the account

What an account holds, how it comes into being, and how its identifiers, profile and
preferences are managed afterwards.

**Prerequisite:** `00-overview.md`; `01-identity.md` sections 1 to 3.

**Scope.** This document covers the *shape* of an account and the *path* into one. Who
exists, organizations and membership are `01-identity`. Credentials, sessions, factors,
recovery and abuse controls are `02-authentication`. Consent, rights and legal documents
are `04-privacy`. The screens themselves are the frontend's (`18-frontend-integration`
section 1); this document states what each step must achieve and what the server does.

**Convention.** Where this document gives the text of a message or a screen, it is an
example marked *illustrative*, never a requirement on the words (CONV-CONTENT-001).

---

## 1. The account

**REG-ACCT-001** — An account SHALL consist of the following groups of fields, and each
field SHALL have exactly one rule for who fills it, whether it is required, where it is
edited and what erasure does to it.

*Source: D-146, D-166*

Legend. **Required**: the account cannot exist without it. **Policy**: switched by the
system or organization policy (`10` section 4.1a) or a configuration key. **Gate**: the
action is a step-up action (`10` section 5a). **Key**: stored under the subject's data
key (PRIV-RIGHT-005a) and unreadable after erasure.

| Group | Fields | Required | Edited through | Key |
|---|---|---|---|---|
| Identifiers (section 2) | emails, phones, username | at least one verified email; phone by policy; username by policy | `/account/identifiers`, `PUT /account/profile` (username) | key + fingerprint |
| Credentials (`02`) | password, passkeys, security keys, authenticator app, recovery codes, provider links, trusted devices | at least one primary sign-in method (section 4.5) | `/account/credentials/*` | as `02` states |
| Profile (section 3.1) | display name, legal name, date of birth, photo | none; legal name and date of birth by policy | `PUT /account/profile`, photo endpoints | yes |
| Preferences (section 3.3) | language, time zone, host-declared preferences | none | `GET/PUT /account/preferences` | declared preferences yes; language and time zone no |
| Standing (`01`, `03`, `04`, `06`) | state, memberships, grants, reachable assurance, consents, objections, notice records, affirmation record, reserved-account mark (OPS-BOOT-002) | as those chapters state | as those chapters state | as those chapters state |

A `restricted` account reads every group and changes nothing in the first four: each
change is refused with `authz.restricted`, save what IDN-ACCT-007 admits.

**Acceptance criteria**
1. `GET /account` returns every field in the first four groups the person may see, and
   nothing the person may not.
2. No field exists in the account schema that is absent from this table or from the
   chapter the table points to.
3. Erasure leaves every field marked Key unreadable and every other field either
   removed or retained exactly as `04-privacy` states.

---

## 2. Identifiers

### 2.1 Kinds

**REG-IDENT-001** — Three identifier kinds SHALL exist: **email**, **phone** and
**username**. Email SHALL always be enabled and an account SHALL always hold at least
one verified email, or a provider credential that supplies one. Phone SHALL be required
by default (`registration.phone` = `required` · `optional`) and SHALL NEVER be the sole
identifier of an account. Username SHALL be disabled by default
(`identifiers.username.enabled`) and absent from every screen and response while
disabled. These rules apply to **human accounts**: the `emergency` account (D-138,
OPS-BOOT-002) and the non-human principals of IDN-PRIN-001 SHALL hold no identifier of
any kind, SHALL never sign in interactively (`emergency` acts only through `/auth/break-glass`;
IDN-PRIN-001 principals are constructed in code and have no sign-in) and SHALL have no
security-notice set (REG-IDENT-002); every use of `emergency` alerts the owner
destinations (D-129), which are its only channel.

*Source: D-146, D-147; supersedes IDN-LIFE-001 (phone verification) and the phone part of D-013*

Email, or a provider that vouches for one, is the anchor the account cannot lose. Phone
is the registration anti-abuse control (one verified number belongs to at most one
account) and, since D-111, a recovery channel. Loosening `registration.phone` to
`optional` is a recorded loosening (OPS-CFG-002) and leaves the account with one recovery
channel.

| | Email | Phone | Username |
|---|---|---|---|
| Enabled | always | always | `identifiers.username.enabled`, default off |
| Required | at least one verified, always | at least one verified while `registration.phone` = `required` | never |
| How many | several, one primary (`identifiers.email.max`) | several, one primary (`identifiers.phone.max`) | one |
| Signs in | every verified one | every verified one | yes |
| Belongs to at most one account | yes | yes | yes |
| Canonical form | `NFKC_Casefold`; at most 254 octets, local part at most 64 (RFC 5321) | E.164, at most 15 digits, digits of any script mapped | PRECIS UsernameCaseMapped (RFC 8265): letters and digits, no spaces, case-folded, NFC; 3 to 32 characters; reserved list; mixed-script rule per IDN-ACCT-005 |
| Storage | key + fingerprint | key + fingerprint | fingerprint only (public by nature) |
| Verified before it counts | code or link (section 4.3) | code or link | not applicable |

**Acceptance criteria**
1. No path exists by which a **human** account is created or left with zero verified
   emails and no provider credential supplying one; `emergency` and the non-human
   principals are not human accounts and are not counted by this criterion.
2. With `registration.phone` = `required`, no account reaches `active` without a verified
   phone; a number verified on one account cannot verify on another.
3. While `identifiers.username.enabled` is off, no request accepts a username and no
   response carries the field.
4. Changing `registration.phone` from `required` to `optional` requires the loosening
   procedure of OPS-CFG-002.
5. No request adds an identifier to `emergency` or to a non-human principal; neither
   appears at any identifier sign-in path; a security notice is never addressed to
   either, and every use of `emergency` reaches the owner destinations regardless of
   `alerting.owner.enabled` (OPS-ALERT-004).

---

### 2.2 Several per kind, one primary, one backup setting

**REG-IDENT-002** — An account MAY hold several verified identifiers of each kind. Per
kind, exactly one SHALL be **primary**, and a **backup setting** (`all-verified`, the
default · `primary-only` · one named identifier) SHALL define the **security-notice
set**: the primary plus whatever the setting adds. Ordinary communications SHALL go to
the primary; security notices and recovery links SHALL go to the security-notice set.
A recovery link SHALL carry the `signin` sending purpose (`10` section 5.15), as a
sign-in link and an invitation link (REG-INV-001) do: no link or code a person or an
administrator asked for carries the `notification` purpose, so that the `notification`
restrictions, `notification.destination` among them, count notices only
(AUTH-ABUSE-004).

*Source: D-148; D-146, D-162, D-166, NIST SP 800-63B-4 sections 4.2.1.2 and 4.6*

The primary cannot be the backup and cannot be removed while primary. Changing the
backup setting is notified to every identifier in the *current* set before the change
applies. `identifiers.email.max` and `identifiers.phone.max` default to ten per kind (D-152);
`1` gives the single-address mode of REG-IDENT-007. An identifier beyond the maximum of
its kind is refused with `identity.identifier.maximum`.

**Acceptance criteria**
1. Exactly one primary exists per kind whenever at least one verified identifier of that
   kind exists.
2. A security notice (`02` AUTH-RECOV-007 and every rule that names one) is sent to
   every member of the security-notice set and to nothing else.
3. Changing the backup setting produces one notice to each member of the set as it was
   before the change.
4. A recovery link counts against the restrictions a sign-in link counts against, and
   never against `notification.destination`.

---

**REG-IDENT-003** — Every verified identifier SHALL be usable to sign in.
`POST /auth/begin` SHALL take **one** `identifier` field, detect its kind, canonicalise it
and answer identically whether or not it resolves to an account.

**Kind detection (D-155).** After trimming: a value containing `@` is an email; a value
that, with spaces, hyphens, dots and parentheses removed and a leading `+` or `00`
allowed, is digits of any script is a phone; anything else is a username where
`identifiers.username.enabled`, and otherwise an unknown identifier that takes the
concealed path. A username therefore always contains at least one letter
(REG-IDENT-009), so no value is both a phone and a username.

*Source: D-146, AUTH-ABUSE-003*

**Acceptance criteria**
1. Signing in with a non-primary verified email or phone succeeds exactly as with the
   primary.
2. The response to `/auth/begin` for an unknown identifier is byte- and timing-identical
   to the response for a known one; timing verified by construction (one code path, fixed-time comparison, identical bytes), asserted by the byte identity, and named in the report as verified by construction (CONV-TEST-007, D-153).

---

### 2.3 Adding, changing roles, removing

**REG-IDENT-004** — Adding an identifier SHALL be a step-up action, after which the new
identifier SHALL be verified (section 4.3) before it counts. The security-notice set
SHALL be notified of the addition. An add whose codes have all expired unused SHALL be
swept (OPS-OBS-003) together with the unverified identifier it staged; the person adds
the identifier again.

*Source: D-146, D-166; amends D-035*

**Acceptance criteria**
1. `POST /account/identifiers` without step-up at the gate's level returns the step-up
   code.
2. An added identifier appears as unverified until a code or same-browser link confirms
   it, and cannot sign in or receive recovery links until then.
3. Every member of the security-notice set receives one notice per addition.
4. An add left past the expiry of its code leaves no identifier after the next sweep,
   and adding the same address again sends a new code.

---

**REG-IDENT-005** — Setting the primary and changing the backup setting SHALL require
neither step-up nor confirmation, and SHALL produce one notice to the security-notice
set. An unverified identifier SHALL NOT be made primary or named by the backup setting;
either is refused with `identity.identifier.unverified`.

*Source: D-146, D-166*

Only identifiers the person has already verified are involved; nothing new enters the
account and nothing leaves it.

**Acceptance criteria**
1. `POST /account/identifiers/{id}/primary` and `PUT /account/identifiers/backup` succeed
   in an ordinary session at the account's assurance level.
2. Setting an unverified identifier primary, or naming one as the backup, is refused
   with `identity.identifier.unverified` and changes nothing.

---

**REG-IDENT-006** — Removing an identifier SHALL be a step-up action and SHALL take
effect **immediately**. It SHALL be refused while the identifier is primary
(`identity.identifier.primary`), and that refusal SHALL be decided before any other;
otherwise it SHALL be refused when removal would leave fewer than the required minimum
of its kind (`identity.identifier.lastofkind`). The last verified identifier of a kind
is its primary, so its removal answers `identity.identifier.primary`;
`identity.identifier.lastofkind` answers the discard of a staged identifier during
registration (REG-SESS-004, `09` section 2). The **remaining** members of
the security-notice set SHALL receive an **undo** link valid for
`identifier.change.coolingoff`; the removed identifier SHALL receive a notice with no
link and no powers and SHALL NEVER be usable for recovery of that account again. Until
`identifier.change.coolingoff` ends, the removed value SHALL stay reserved to the
account it was removed from: no other account may add it and no registration session
may stage it. An attempt SHALL be answered exactly as an attempt on a value another
account holds (REG-SESS-005, API-CONV-005), and nobody SHALL be notified. A sign-in
link or email code sent to the identifier before its removal SHALL NOT sign in; it is
refused with `auth.factor.rejected`. Other sessions SHALL end when a sign-in identifier
is removed.

*Source: D-146, D-162, D-166; amends D-035 (old-address confirmation retired except in
REG-IDENT-007); IDN-LIFE-008 applies*

Why the undo goes to the remaining set and never to the removed address: an undo that
reaches the removed address lets a compromised mailbox re-attach itself. A stolen session
that removes the owner's address therefore leaves the owner's other addresses holding a
one-click restore, and an attacker who holds a compromised address receives nothing
usable.

**Acceptance criteria**
1. Removal without step-up returns the step-up code; removal of the primary returns
   `identity.identifier.primary`, including where it is the last verified identifier of
   a kind the deployment requires.
2. On removal the identifier stops resolving at once; the undo link restores it within
   `identifier.change.coolingoff` and is refused after.
3. The notice to the removed identifier contains no link, and presenting that
   identifier to any recovery path afterwards behaves as an unknown identifier.
4. Every other session of the account is terminated on removal.
5. Within the undo window, adding the removed value to another account or staging it in
   a registration answers as for a held value, sends no code and notifies nobody; the
   undo then restores it. After the window the same attempt stages a verification.
6. A sign-in link or email code sent to the identifier before its removal does not sign
   in and is refused with `auth.factor.rejected`.

---

**REG-IDENT-007** — Where a kind's maximum is `1`, a change SHALL be a **replace** in
one operation: step-up, the new identifier verifies, the swap applies at once, and the
undo goes to the remaining channels of the account. **Only where no other channel
exists at all** SHALL the old address confirm before the swap, except within an
admin-assisted enrolment session (AUTH-RECOV-002), where the approver's recorded
confirmation stands in for the old address and the new address confirms alone. A
replace whose codes have all expired unused SHALL be swept (OPS-OBS-003), leaving the
identifier as it stood.

*Source: D-148; D-146, D-166, amends D-035 and restates IDN-LIFE-004, IDN-LIFE-007, IDN-LIFE-010*

The degenerate case is one email, phone optional and never added: nothing else could
undo a hostile change. If that address is lost or compromised, administrative recovery
(AUTH-RECOV-002) is the route, and it is the one place the old address is not asked.

**Acceptance criteria**
1. With `identifiers.email.max` = 1 and a verified phone, an email change completes
   without the old address acting, and the undo arrives by SMS.
2. With one email and no phone, outside an enrolment session, the swap does not apply
   until the old address confirms.
3. Within an enrolment session opened for a lost mailbox (AUTH-RECOV-002), the swap
   applies when the new address verifies; the old address is not asked.
4. A replace left past the expiry of its codes is swept, after which a new replace of
   the same identifier is accepted.

---

### 2.4 Providers

**REG-IDENT-008** — A Google or Apple identity SHALL remain a **credential**, matched
by the provider's `sub`; the provider's email SHALL NEVER be a key (IDN-ACCT-001,
IDN-LIFE-012). A provider email SHALL count as **verified by the sign-in itself** when
the provider operates the mailbox: Google for `gmail.com` and for Workspace domains
asserted by the `hd` claim; Apple for its relay addresses and for `icloud.com`,
`me.com` and `mac.com` (the provider-domain list is as D-146 states it). Apple's relay
domain is `privaterelay.appleid.com`; no subdomain of a listed domain is
provider-operated. `email_verified` counts only as the boolean `true` or the string
`"true"`; any other value, or none, leaves the address to one code whoever operates it.
Any other provider-supplied address SHALL be verified by one code like a typed address.
A provider `sub` already linked to an account SHALL make the attempt a **sign-in**, not
a registration. A provider address already on another account SHALL produce no code and
a notice to the owner, and is not verified by the sign-in: a third-party address answers
exactly as a fresh one does; a provider-operated one is staged unverified, a difference
seen only by the person the provider has just shown controls that mailbox.

*Source: D-148; D-146, D-076, D-166*

A provider-operated address is locked at registration (REG-IDENT-010): there is nothing
the person could change about an address the provider gave them.

**Acceptance criteria**
1. Continue with Google on a `gmail.com` address reaches the confirm step with the email
   already verified and no code sent.
2. Continue with Apple on a third-party address sends one code and requires it.
3. A `sub` already linked signs the person in and returns them; the registration
   session the attempt was made in is abandoned.
4. A provider address already on another account: no code, owner notified once per
   `abuse.nonexistent.window`; a third-party address answers identically to the fresh
   case; a provider-operated address is staged unverified.

---

### 2.5 Username

**REG-IDENT-009** — Where enabled, a username SHALL be optional, chosen at the
"about you" step or later, and changed through `PUT /account/profile` as a step-up
action with a cooling-off of `identifiers.username.changecooloff` between changes.
"Taken" and "reserved" SHALL be disclosed at choice time (`identity.username.taken`,
`identity.username.reserved`), throttled per source. After erasure of its account a
username SHALL be **held** for the period of `retention.consent` and released
afterwards. The hold SHALL be a record of its own that names no subject and holds no
value under the erased subject's key: the username's fingerprint and the instant the
hold ends.

A username SHALL contain at least one letter, so that no username is also a phone
number under REG-IDENT-003's detection; an all-digit choice is refused with
`identity.username.invalid` (D-155).

**Values (D-153).** The reserved list is the library's (`admin`, `administrator`, `root`,
`support`, `security`, `postmaster`, `abuse`, `noreply`, `emergency`, `system`, `help`,
`api`, `www`, `mail`) plus the host's declared additions (LIB-HOST-001). "Throttled
per source" means each `taken` or `reserved` answer counts as one failure in the
AUTH-ABUSE-001 per-source throttle. `identity.username.coolingoff` carries
`details.retryAt`.

*Source: D-146, D-162, D-166*

Usernames are the one identifier whose existence is disclosed to anyone, because they
are public by nature; the only other disclosure is of the email an invitation binds, to
the holder of that invitation's link alone (REG-INV-001). The hold after erasure stops
an erased person from being impersonated at once under their former name.

**Acceptance criteria**
1. With `identifiers.username.enabled` on, registration completes with no username.
2. A second change inside the cooling-off is refused with the cooling-off end time.
3. A username freed by erasure cannot be claimed until `retention.consent` has elapsed
   from the erasure.
4. After erasure, the hold of the username names no subject and is readable with the
   subject key destroyed.

---

### 2.6 Locked identifiers

**REG-IDENT-010** — An identifier SHALL be **locked** against change during registration
when it was supplied by a bound invitation (section 5.1) or verified by a
provider-operated mailbox (REG-IDENT-008). Every other identifier SHALL have its own
Change, and a changed identifier SHALL be re-verified. Any other value given to a locked
identifier SHALL be refused with `identity.identifier.locked`.

*Source: D-146, D-162, D-166*

**Acceptance criteria**
1. The confirm step (REG-SESS-004) offers no Change on a locked identifier.
2. Changing an unlocked identifier resets its verification.
3. A request giving a locked identifier another value is refused with
   `identity.identifier.locked` and changes nothing.

---

## 3. Profile and preferences

### 3.1 Profile

**REG-PROF-001** — The profile SHALL carry the following fields with the following
rules.

*Source: D-146, D-162, D-166; IDN-ATTR-002 to 004 for the photo*

| Field | Enabled | Rules | Edited |
|---|---|---|---|
| Display name | always, optional | any script; PRECIS Nickname (RFC 8266); 1 to 64 **bytes**; mixed-script rule per word (IDN-ACCT-005); shown where a human needs to know who an account is, the primary email when absent | `PUT /account/profile`, no gate, audited |
| Legal name | `profile.legalname` = off (default) · optional · required | 1 to 200 Unicode scalar values after NFC (D-153); mixed-script rule per word | same |
| Date of birth | `profile.dateofbirth` = off (default) · optional · required | the date entered at the age step (REG-PROF-002); **immutable to the person**, corrected through support; never used to infer anything but age | support only |
| Photo | as IDN-ATTR-002 | as IDN-ATTR-002 to 004; `photo.maxbytes`, `photo.maxdimension`; validated client-side for convenience and server-side by content | photo endpoints |

Legal name is off because identity needs no name: no proofing is done, and a name is a
proofing attribute (NIST SP 800-63A). A host that invoices or ships turns it on with a
declared purpose. Date of birth is off because the answer is kept, not the date: with the
key off the age step records only the derived outcome.

**Acceptance criteria**
1. A display name of 65 bytes is refused with `identity.profile.invalid`; one of 64 bytes
   in any script is accepted.
2. While `profile.legalname` and `profile.dateofbirth` are off, no request accepts the
   field (`identity.profile.notaccepted`) and no response carries it.
3. `PUT /account/profile` with a date of birth is refused for the person with
   `identity.profile.notaccepted` even when the key is on.

---

**REG-PROF-002** — Registration SHALL open with a **neutral age screen** that asks for
the date of birth. The adult affirmation SHALL be **derived** from it and recorded, with
a timestamp, when the account is created. The date itself SHALL be retained only where
`profile.dateofbirth` is not off. On a host with `registration.adultaffirmation` =
`required`, a date under eighteen SHALL end the registration session before any
identifier is collected and SHALL lock the fields against retry in that session. A host
that serves minors SHALL use the same screen to set the age group.

*Source: D-146; amends D-027 and D-039; supersedes IDN-LIFE-002 and PRIV-MINOR-001's
"date of birth SHALL NOT be collected"*

The screen is neutral so that it does not announce what answer passes. The affirmation
verifies nothing; what makes self-declaration proportionate is that a wrong answer has a
defined outcome (`14-takedown-procedure`).

**Acceptance criteria**
1. No identifier field is shown or accepted before the age screen is answered.
2. On an adults-only host, an under-age date ends the session (`identity.profile.underage`),
   and the same session accepts no further date.
3. The account record carries the affirmation with its timestamp; the date is present
   only where `profile.dateofbirth` is on.
4. With `registration.adultaffirmation` = `off`, the screen records the age group
   (`minor` · `adult`, `10` section 5.22) and no
   affirmation.

---

### 3.2 What stays out

Postal addresses are host data (IDN-ATTR-005). Notification *choices* are the consent
purposes on the privacy dashboard; security and lifecycle notices are never optional, so
no separate object exists.

### 3.3 Preferences

**REG-PREF-001** — The account SHALL carry a **language** (BCP 47), a **time zone**
(IANA zone identifier) and the values of the **host-declared preferences**. The host
SHALL declare its preference keys at startup: name, type (`string` · `boolean` ·
`integer` · `enum` with values), default, and whether the person or only an
administrator may edit it. The library SHALL validate values against the declaration,
reject undeclared keys (`identity.preference.undeclared`), cap the whole set at
`preferences.maxsize` (default 8 KiB), store declared values under the subject key,
include them in the subject export and erase them with the rest. The library SHALL
NEVER branch on a declared preference.

**Values (D-153).** A value of the wrong type is refused with `identity.preference.wrongtype`,
a set over `preferences.maxsize` with `identity.preference.toolarge`, and a person
setting an administrator-only preference with `identity.preference.administratoronly`.

*Source: D-146; amends D-055; IDN-ATTR-001 for the language*

Theme, text size, reduced motion, date format and the like belong to the person, not to
one application; every application in a deployment reads the same object. What the
library must not do is understand them, because the set differs per host. The time zone
is first-class because notifications and account pages render times in it; it is
pre-filled from the client.

**Acceptance criteria**
1. Startup fails with a named error when a preference declaration is malformed.
2. `PUT /account/preferences` with an undeclared key is refused; with a value of the
   wrong type is refused; with a set over `preferences.maxsize` is refused.
3. An administrator-only preference is refused when set by the person.
4. The export of PRIV-RIGHT-003 contains the preferences; erasure leaves them
   unreadable.

---

## 4. Registration

### 4.1 The registration session

**REG-SESS-001** — `POST /register` SHALL create a **registration session**: an opaque
server-side record, bound to the requesting browser through the pre-authentication
cookie (BFF-CSRF-005a), living `registration.session.lifetime` (default 24 hours,
ceiling 72), that **stages** everything registration collects and **reserves nothing**.
The account SHALL be created in **one transaction** at the terms step (REG-SESS-007) or
not at all. An expired or abandoned session SHALL be swept and SHALL leave nothing
behind.

*Source: D-146, D-166; amends D-114 (the `pending` state is removed)*

Staged: identifiers and their verification state, the password hash and floor flag,
enrolled authenticators (WebAuthn ceremonies use the session's provisional user handle,
which becomes the subject identifier), provider `sub`, the age answer, consents, the
notice presentation record, the invitation its token opened (the token itself is never
staged), the originating client identifier (API-REDIR-002) and the locale.

**Acceptance criteria**
1. Abandoning after verifying both identifiers leaves no account and no reservation; a
   later registration with the same identifiers is a fresh one.
2. A request against a registration session from a browser without its pre-authentication
   cookie is refused.
3. The session store contains no registration session older than
   `registration.session.lifetime`.
4. Exactly one `AccountRegistered` event fires per created account, and no account exists
   in a state that is not `active` immediately after creation, save where an invitation's
   policy holds it at enrolment (AUTH-RECOV-001 enforced).

---

### 4.2 The steps

**REG-SESS-002** — Registration SHALL consist of the following ten steps in this order.
The first six SHALL run on the authentication application against the registration
session; the account SHALL exist from the end of step 6; steps 7 to 10 SHALL run on the
account application in the new session. There SHALL be **no Back navigation**; every
correction is made in place through a per-identifier Change (REG-IDENT-010) or by editing
the field later.

*Source: D-148; D-146, D-162, D-166*

| # | Step | Kind | What it must achieve |
|---|---|---|---|
| 1 | Age | required | REG-PROF-002 |
| 2 | Email | required | an email is entered, or Continue with Google / Apple supplies one (REG-IDENT-008); a code and a link are sent unless provider-verified |
| 3 | Phone | required by policy | a phone is entered; a code and a link are sent |
| 4 | Confirm | required | REG-SESS-004: every identifier on one screen, each verified; Change per identifier; more may be added |
| 5 | Security | required | REG-SESS-006: sign-in methods and second step on one screen |
| 6 | Terms and consents | required | REG-SESS-007: the account is created and the person is signed in |
| 7 | About you | optional | REG-PROF-001 fields whose policies are on; username where enabled |
| 8 | Preferences | optional | REG-PREF-001 person-editable values |
| 9 | Membership | invitation only | REG-INV-001 acknowledgement |
| 10 | Done | required | REG-SESS-008 summary and return |

A person who is already signed in and opens registration SHALL be sent to their account;
no session is created. `POST /register` from such a browser SHALL be refused with
`identity.registration.signedin` and SHALL stage nothing; where it carries an invitation
token, the invitation SHALL first be attached to the signed-in account (REG-INV-002).
The privacy notice SHALL be reachable before anything is collected (PRIV-CONS-008a). The
originating application's client identifier SHALL be recorded at session creation
(`POST /register`, API-REDIR-002), before step 1 (FE-REG-001). Step 3 SHALL be skippable
where `registration.phone` is `optional` and no invitation bound a phone (REG-MAIL-001);
a skip otherwise is refused with `identity.registration.incomplete`.

**Acceptance criteria**
1. No step can be reached before the one preceding it is complete, and no control returns
   to a completed step; a request for such a step is refused with
   `identity.registration.incomplete`.
2. Before step 6 completes, no account, identifier, credential or consent exists outside
   the session store; the only other writes are the records of the abuse controls
   (AUTH-ABUSE-001, AUTH-ABUSE-004), the messages sent, and the spending of an
   invitation's link (REG-INV-001).
3. Steps 7 and 8 can each be skipped and the account is unaffected; step 3 can be
   skipped where `registration.phone` is `optional` and cannot where it is `required` or
   an invitation bound a phone.
4. `POST /register` from a browser holding a live session is refused with
   `identity.registration.signedin` and creates no registration session.

---

### 4.3 Verification of an identifier

**REG-SESS-003** — Every unverified identifier SHALL be verifiable in two ways at once:
by **typing the code** the message carried into the waiting screen, or by **opening the
link** in the message. The link SHALL behave according to **where it is opened**: in the
browser that started the registration, a **press** on the landing page verifies the
identifier and the waiting screen advances by itself; **anywhere else**, the page SHALL
show the code to type and a control that **ends the registration session at once**.
Nothing SHALL change on a plain open. Five wrong codes SHALL invalidate the code
(`code.verification.attempts`); a new code draws on the sending restrictions
(AUTH-ABUSE-004). The message SHALL be `verification-link` (`10`, message kinds),
carrying both the code and the link. A wrong code, and a link token that opens nothing,
SHALL be counted as a failure against the source and the identifier by the throttle of
AUTH-ABUSE-001, and every ask for a code or a link SHALL first be held to that
throttle's delay.

**Values (D-162, D-166).** `GET /register/events` is woken by a PostgreSQL notification
on the channel `identity_registration`, raised inside the transaction that changes the
session (an identifier verified, a step completed) and so released only on commit, and
reads the state back every `registration.events.pollinterval`, whichever comes first.
Channels are database-wide, so a host SHALL NOT use that name. A lost channel SHALL
raise the `degradation` condition with `details.component` `registration-channel`
(OPS-OBS-002). Every link the library sends has the form of API-LAND-001,
`<origin>/link#<kind>.<token>`, the origin being the landing origin the host declares
for the application (`LandingOrigins`, LIB-HOST-001) and the kind one of `10`'s link
kinds. The links of this chapter are `registration` (a registration's identifiers),
`invitation` (an invitation) and `sign-in` (a sign-in link), which land on the
authentication application, and `identifier` (an identifier's add or replace),
`identifier-confirm` (the old address's confirmation of REG-IDENT-007) and `undo` (the
undo of REG-IDENT-006), which land on the account application. Every kind acts only on
a press, never on load (FE-VER-001). The token travels in the fragment, so no request,
log or referrer carries it.

*Source: D-148; D-146, D-162, D-166, supersedes the code-only design of IDN-LIFE-004; API-LAND-001*

Without the browser binding, an attacker who starts a registration with a victim's
address gets it verified the moment the victim clicks the verification link. Mail scanners
prefetch links, which is why a plain open does nothing. While a verification is pending
the screen listens for the session's state over server-sent events on the session cookie
(no token in any URL), with polling as fallback (FE-VER-001).

The same rule applies to adding an identifier later (REG-IDENT-004), to the replace flow
(REG-IDENT-007) and to sign-in links (`02` AUTH-FACT-002). There the ending control has
no registration session to end: for a sign-in link it calls `POST /auth/link/abandon`,
which ends the pending link; for an identifier add or replace it calls
`POST /account/identifiers/{id}/abandon`, which ends the pending verification (D-148).
Each takes the link token, answers 204 always, and leaves the account otherwise unchanged.
For a sign-in link, the code the page shows elsewhere is an authentication code: it
lives as long as its link (`link.magic.lifetime`) and is invalidated after
`code.signin.attempts` wrong tries (AUTH-FACT-004), not held to the verification code's
keys.

**Acceptance criteria**
1. Opening the link in the originating browser and pressing verifies; opening it without
   pressing verifies nothing.
2. Opening the link in another browser shows the code and never verifies; the
   session-ending control ends the session and the code stops working.
3. The sixth wrong code is refused and so is a correct code entered after it; the person
   can request a new one within the restrictions.
4. A verification completing by link advances the waiting screen without a reload.
5. A registration message carries both the code and the link.
6. Wrong registration codes are counted by the throttle of AUTH-ABUSE-001 against the
   source and the identifier; while its delay stands, a further code or ask is refused
   with `auth.throttled` and `retryAt`.
7. With the database channel lost, a verification still advances the waiting screen
   within `registration.events.pollinterval`, and `degradation` is raised.

---

### 4.4 The confirm step

**REG-SESS-004** — The confirm step SHALL show every identifier collected on one screen
with its verification state, a Change on each unlocked identifier, and the option to add
further emails and phones within `identifiers.email.max` and `identifiers.phone.max`.
The step SHALL complete only when every identifier on the screen is verified;
`POST /register/confirm` while a staged identifier is unverified is refused with
`identity.registration.incomplete`.

*Source: D-146, D-166*

**Acceptance criteria**
1. The step does not complete with an unverified identifier present, and the attempt is
   refused with `identity.registration.incomplete`; removing an unverified extra
   identifier is allowed and completes the step if the rest are verified.
2. Correcting a mistyped number sends to the corrected number; the restriction counts
   per destination (FE-REG-004).

---

**REG-SESS-005** — An identifier that already belongs to another account SHALL NOT let
the session complete. The person SHALL see the ordinary sent outcome,
no code SHALL be sent to the identifier, and its owner SHALL be notified once per
`abuse.nonexistent.window` that someone tried to register with it. The session SHALL
carry a sign-in exit. A value reserved for an undo (REG-IDENT-006) SHALL be answered
the same, and nobody SHALL be notified. Whether a staged identifier is held or reserved
SHALL be judged again inside the terms step's transaction, before the account is
written: one taken or reserved since it was staged ends the session, and no account is
created. The email an invitation binds is the one exception: where an account holds it,
the press that begins the registration is refused (REG-INV-001).

**Values (D-153).** The sign-in exit is a static link on every registration screen, drawn
by the frontend; `GET /register` carries no field for it, because a field present only
for a duplicate would be an oracle.

*Source: D-146, D-162, D-166; D-076, D-112*

**Acceptance criteria**
1. The response to a duplicate, the email an invitation binds excepted, is byte- and
   timing-identical to the fresh case; timing verified by construction (one code path,
   fixed-time comparison, identical bytes), asserted by the byte identity, and named in
   the report as verified by construction (CONV-TEST-007, D-153).
2. The owner's notice contains no link and no code.
3. The session expires without creating an account.
4. An identifier another account takes, or that becomes reserved, after it was staged
   ends the session at the terms step, and no account is created.

---

### 4.5 The security step

**REG-SESS-006** — The security step SHALL offer every method the applicable policy's
`loginFactors` allows, on one screen, and SHALL complete only when the account would
have **at least one primary sign-in method**: a password, a passkey, a linked provider,
or a confirmed email or phone where the matching link or one-time-code factor
(`emailLink`, `emailCode`, `phoneLink`) is enabled. Passwords SHALL be refused for the
floor (AUTH-PASS-001, AUTH-PASS-001a), for a length over `password.maximum`
(`auth.password.toolong`, nothing truncated) and for a hit of the screening of
AUTH-PASS-004 only; strength feedback is advisory (AUTH-PASS-005). A password below the
single-factor floor SHALL make a second step mandatory on the same screen, and
lengthening it SHALL lift that in place. When a second step is enrolled beside a
password, recovery codes SHALL be generated and shown (AUTH-RECOV-006). Every WebAuthn
and TOTP enrolment SHALL take a label (AUTH-FACT-001).

*Source: D-146, D-162, D-166; AUTH-PASS-001a, AUTH-RECOV-006*

A provider-only registration yields a `delegated` account (D-141). A passkey-only
account has no password and no recovery codes.

**Acceptance criteria**
1. With the default policy, the step completes with a password alone of 15 characters,
   a passkey alone, or a linked provider alone; it does not complete with a 12-character
   password and no second step.
2. With `emailLink` or `emailCode` enabled, the step completes with a verified email and
   nothing else; with both disabled, it does not.
3. Lengthening the password to 15 on the same screen removes the second-step requirement
   without leaving the step.
4. Enrolling a second step beside a password shows recovery codes before the step can
   complete (AUTH-RECOV-006 criteria).

---

### 4.6 The terms step

**REG-SESS-007** — The terms step SHALL record, each with its document and version,
timestamp and action: terms of service **accepted** (required); the privacy notice **presented**
(PRIV-CONS-008a); the affirmation derived at step 1; one unticked control per
consent-based purpose the host declares (PRIV-CONS-002 to 004), none of which blocks
registration. Every document SHALL be readable in its governing language and any
available translation without changing the interface language (PRIV-CONS-005). On
submit the one transaction of REG-SESS-001 SHALL run and the person SHALL be signed in
at the assurance the security step proved (AUTH-SESS-005a). Completing the step SHALL
remember the registering browser as seen for `device.verification.lifetime`, so that the
new-device check (AUTH-FACT-016) does not hold the account's next sign-in from it. The
account SHALL NOT be created without the terms version accepted and the notice version
presented; a session that reaches the transaction without either is refused with
`identity.registration.incomplete`.

*Source: D-148; D-146, D-166, PRIV-CONS-005, PRIV-CONS-008a*

**Acceptance criteria**
1. Registration completes with every consent control left unticked.
2. The account record carries the terms version accepted and the notice version
   presented.
3. The session after step 6 carries the assurance and phishing-resistance properties
   the enrolled methods support, nothing more.
4. A password-only account's next sign-in from the registering browser, within
   `device.verification.lifetime`, is not held for a new-device code; from another
   browser it is.
5. A session that reaches the terms step's transaction without a terms or notice version
   creates no account and is refused with `identity.registration.incomplete`.

---

### 4.7 Finish and return

**REG-SESS-008** — The final step SHALL show a summary of what was set up and what was
left for later, and SHALL return the person, signed in, to the **origin of the
registered return address** of the application named by the client identifier captured
at step 1, never to an address supplied in any request (API-REDIR-001, API-REDIR-002).
The client captured at step 1 SHALL be kept on the session the terms step establishes,
and the done step SHALL read its origin from `GET /auth/session` (`landing`); where no
client was stored (API-REDIR-002), `landing` is absent and the frontend chooses its own
(API-REDIR-001). The landing address SHALL receive a signed-in session and nothing else;
restoring what the person was doing is the application's responsibility.

*Source: D-146, D-162, D-166; D-057*

**Acceptance criteria**
1. No registration step accepts or forwards a destination parameter.
2. The return lands on the origin of the client's registered return address, read from
   `GET /auth/session`, with an established session.

---

## 5. Invitations, domain lock and the corporate mailbox

### 5.1 Invitations

**REG-INV-001** — An invitation MAY name the email, the phone, both, or neither. A named
identifier SHALL be pre-filled and locked at registration (REG-IDENT-010); an open one is
the person's choice. An invitation into an organization whose mail server is integrated
(REG-MAIL-001) SHALL name a **personal email** beside the asserted corporate address; the
invitation link goes to the personal email, and its press in the registering browser
verifies it (REG-MAIL-001). Beginning a registration with the token (`POST /register`
carrying `invitationToken`) SHALL be the press. Where an account already holds the bound
email, the press SHALL be refused with `identity.invitation.identifiermismatch` and
SHALL leave the invitation unspent; a bound phone SHALL be staged locked and verified at
its step like any phone (REG-SESS-005). The organization's policy SHALL govern every
step from the moment the token attaches (IDN-LIFE-009a). During registration it SHALL
decide which `loginFactors` the security step offers and completes with, and which
domains the lock admits for every email the person chooses (at step 2, as an extra at
step 4, or by a Change); an email the invitation bound was judged when it was issued.
`requiredAssurance` and `credentialRedundancy` SHALL be judged at the acknowledgement
(REG-INV-002). At the membership step the person SHALL be shown who invited them, the
organization, the roles and grants that will attach, and the documents the organization
attached to the invitation, each with a version, and SHALL **acknowledge**. On
acknowledgement the membership SHALL attach, factors the policy does not permit SHALL be
disabled for interactive use (IDN-LIFE-009b), the corporate address (where one exists)
SHALL become the primary email with the personal email kept as a verified non-primary
email, and the acknowledgement (document list, versions, timestamp) SHALL be recorded on
the membership. Each role the invitation names SHALL be
granted as a stored grant across the organization, recorded as granted by the inviter at
the acknowledgement with the reason `invitation:<id>`, and not again where the account
already holds the same live grant without expiry. The acknowledgement SHALL be refused
(`identity.invitation.expired`) where the inviter no longer holds `membership:manage` in
the organization or, for the roles, what a grant of them requires (OPS-CFG-007). A role
a standing invitation names SHALL NOT be removed (`authz.role.inuse`). A `restricted`
account SHALL NOT acknowledge (`authz.restricted`, IDN-ACCT-007). Until then the account
exists with no membership and no grants.

*Source: D-148; D-146, D-166, IDN-LIFE-009a, IDN-LIFE-009b*

Binding the phone means the link alone is not enough to accept, and gives the invitation
a second, personal channel.

**Acceptance criteria**
1. A bound identifier cannot be changed in the session; an open one can.
2. Before acknowledgement the account holds no membership and no grant of the inviting
   organization.
3. The membership record carries the acknowledgement with the document versions shown.
4. An invitation into an integrated-mail organization cannot be issued without a
   personal email; one without a personal email, without a corporate address, or naming
   the corporate address as the personal one is refused with
   `identity.invitation.addressrequired`; after acknowledgement the account holds the
   corporate address as primary and the personal email as a verified non-primary email.
5. A registration begun with the token of an invitation whose bound email an account
   holds is refused with `identity.invitation.identifiermismatch`, and the invitation
   stays unspent.
6. The acknowledgement is refused with `identity.invitation.expired`, and nothing is
   granted, where the inviter has since lost `membership:manage` in the organization.
7. A `restricted` account's acknowledgement is refused with `authz.restricted`.

---

**REG-INV-002** — A person who already holds an account SHALL accept an invitation by
**signing in**, after which they SHALL be taken directly to the membership step. A
corporate address named by the invitation SHALL be added to the existing account as a
verified identifier where the organization asserts it (REG-MAIL-001), or verified as
any added identifier otherwise. The organization's credential policy (`requiredAssurance`,
`credentialRedundancy`, `loginFactors`) SHALL be satisfied before the membership
attaches, counting only credentials whose factor that policy's `loginFactors` permits;
an account that does not satisfy it is held at enrolment with no grace (AUTH-FACT-017).
A refusal the account cannot meet by enrolling (the membership limit,
`identity.membership.limitreached`, IDN-MEM-002; the email maximum,
`identity.identifier.maximum`) SHALL be decided before the credential policy. No second
account SHALL be created.

*Source: D-146, D-166; IDN-ACCT-003*

**Acceptance criteria**
1. Opening an invitation while signed in, or signing in from the invitation landing page,
   reaches the membership step without a registration session.
2. An existing account below the organization's required assurance is held at enrolment
   until it satisfies it; the membership attaches afterwards.
3. The account count is unchanged by the acceptance.
4. An account the membership limit refuses is answered `identity.membership.limitreached`
   and is not sent to enrol, whatever its credentials.

---

### 5.2 Domain lock

**REG-DOM-001** — An organization MAY restrict member email addresses to listed
domains: policy field `emailDomains`, default off, inheriting the system policy. Each
domain SHALL be **verified by a DNS TXT record** before it counts and SHALL be
**re-verified on a schedule**. A failed re-verification SHALL raise an alert
(OPS-ALERT-001) and SHALL revoke nothing by itself. While the lock is on, every member's
sign-in email and any open email chosen at invitation acceptance SHALL be in the list
(`identity.identifier.domainnotallowed`). At acknowledgement the lock SHALL be judged as
it then stands on the address the member will sign in with: the corporate address where
one is taken on, else the bound email, else at least one verified email of the
accepting account. An invitation SHALL be refused at issue with
`identity.identifier.domainnotallowed` where the address its member will sign in with
(the corporate address where the mail is integrated, REG-MAIL-001; the bound email
otherwise) is outside the lock of the organization it invites into; the personal email
of an integrated invitation is not judged at issue.

A member's sign-in email SHALL be the email address a sign-in is opened with and the
email address a sign-in link or email code is sent to; a sign-in opened with a phone
number, a username, a provider or a discoverable passkey uses none. The lock SHALL reach
an account through its current memberships only, and each organization's lock SHALL be
judged on its own. The lock SHALL be told only after a factor of the sign-in has
succeeded (AUTH-ABUSE-003). A sign-in link or email code requested for an address the
lock refuses SHALL be answered as any other request and SHALL NOT be sent; one sent
earlier SHALL be judged again when it is used.

Adding or verifying a domain, and removing the last one listed, is a loosening under
OPS-CFG-002. The lock SHALL be on from the first domain listed, verified or not; while
no listed domain is verified it SHALL admit no address. Removing a domain SHALL stop new
sign-ins with addresses in it and SHALL raise an alert; the removed domain SHALL go on
refusing addresses in it for every member of the organization until it is listed anew,
whether or not the list still holds another domain, and a domain listed anew draws a new
token and admits nothing until verified. Every change of the list SHALL be asked under
`domain:manage` in the administrative organization, SHALL be that step-up action and
SHALL carry a reason (`config.change.reasonrequired` where it is absent or blank); a
loosening also requires `system:administer`. A domain SHALL NOT be listed while the host
has declared no DNS resolver (LIB-HOST-001): the addition is refused with
`config.value.notallowed`, `details.requires` `dnsResolver`, and a deployment whose
stored lock lists a domain SHALL NOT start without one (`model.startup.declarationmissing`,
`details.key` `dnsResolver`).

**Values (D-153).** Re-verification runs every `domain.reverify.interval` on the sweep. The
record is `_identity-verify.<domain>` TXT with value
`identity-domain-verification=<32 random bytes, base64url>`, one token per organization
and domain, never reused.

**Values (D-166).** A domain counts from its first successful verification until it is
removed. A failed scheduled re-verification is recorded as the domain's last check and
does not unverify it. Only listed, verified domains are re-verified; a listed domain
never verified waits for the administrator's verify. A domain is listed, stored and
compared in one form: its canonical form (IDN-ACCT-004) converted to its IDNA ASCII form
under the STD3 rules, by the library's own UTS #46 mapping tables at the pinned Unicode
version and RFC 3492 Punycode, never the machine's (D-154); of at least two labels, with
no trailing dot, and at most 236 octets so that `_identity-verify.<domain>` is a valid
name. Because the canonical form folds case fully, `ß` reads as `ss`. An address's
domain is read the same way and admits only where it equals a listed domain; a
subdomain is a different domain. An address whose domain does not read is admitted only
where no lock applies.

*Source: D-146, D-166*

In a single-organization deployment the administrator is trusted anyway; the library is
generic, and DNS verification is what every comparable product requires.

**Acceptance criteria**
1. A domain listed but not yet verified does not admit any address.
2. Choosing an address outside the list at an open invitation is refused with the named
   code.
3. A failed scheduled re-verification produces an alert and leaves every membership and
   session intact.
4. After a domain is removed, sign-in with an address in it is refused and an alert is
   raised.
5. With only unverified domains listed, a member's sign-in email is refused whatever its
   domain.
6. After the last listed domain is removed, an address in it is refused and an address
   in any other domain is admitted.
7. A domain listed anew draws a token it never had and admits nothing until verified.
8. A wrong password against a locked address answers `auth.factor.rejected`; the right
   one answers `identity.identifier.domainnotallowed`.
9. A sign-in link requested for a locked address answers 202 and sends nothing; a link
   sent before its domain was removed does not sign in.
10. An existing account holding no verified email inside a locked organization's list
    cannot acknowledge an open invitation into it.
11. An invitation whose member's sign-in address is outside the lock is refused at issue
    with `identity.identifier.domainnotallowed`.
12. Listing a domain while no DNS resolver is declared is refused with
    `config.value.notallowed` and `details.requires` `dnsResolver`; a deployment whose
    stored lock lists a domain does not start without one.

---

### 5.3 The corporate mailbox

**REG-MAIL-001** — Where the organization is the administrative organization and the
deployment has a mail server registered (the shipped adapter or the host's own; `05`
INT-MAIL-006, INT-MAIL-009), the invitation SHALL name a **personal email**, to which
the invitation link is sent, and the account SHALL NOT be created until that address is
verified: the press on the invitation link in the registering browser (REG-SESS-003) is
the verification, and the person does nothing further for it. The personal email SHALL
stay on the account as a verified, non-primary email for the whole membership, a
provider's event included (IDN-LIFE-012a), and SHALL be in the security-notice set
(REG-IDENT-002) whatever the backup setting; while the membership lasts the
organization's domain lock (REG-DOM-001) prevents sign-in with it. While the membership
lasts, removing the personal email, making it primary or replacing it SHALL be refused
(`identity.identifier.locked`), and the account view SHALL show it locked. The corporate
address SHALL be **asserted by the administrator and created by the system**: it SHALL
be skipped at verification, and what proves the person SHALL be the personal channels
of the invitation (the link to the personal email; the bound phone by SMS). The mailbox
SHALL be provisioned **disabled** when the invitation is sent and **enabled** when the
membership attaches (INT-MAIL-006). When the membership attaches, taking the corporate
address on SHALL be notified once to the security-notice set as it stood before
(REG-IDENT-004); the corporate address SHALL NOT be sent that notice. An expired
invitation SHALL leave the mailbox reserved and disabled until the administrator
re-invites the address or revokes the invitation, which releases a mailbox nobody has
held. At most one invitation SHALL stand over a mailbox. An invitation for an address a
member holds, or one a standing invitation that has not expired reserves, SHALL be
refused (`identity.mailbox.taken`); re-inviting an address whose invitation expired
SHALL revoke that invitation in the same transaction and keep the mailbox. In every
other case no corporate address is asserted, and every email of the member SHALL be
verified by the person like any other.

*Source: D-148; D-146, D-166, amends D-144 (provisioning moves from acceptance to invitation)*

A temporary mailbox password for reading a code is rejected: staff reach mail through
the library's OIDC provider and app passwords created after sign-in (INT-MAIL-005), the
person has no passkey yet at that moment, and verifying a mailbox the system created
proves nothing. The personal email is what the account keeps when the membership ends
(REG-MAIL-003), and it is why a compromised corporate mailbox cannot silence the person:
security notices reach the personal address as well.

**Acceptance criteria**
1. Sending a staff invitation creates a disabled mail account; acknowledgement enables it;
   expiry leaves it disabled and reserved.
2. The corporate address is never sent a verification code.
3. A bound phone is verified before the membership step can be reached.
4. No account is created for an invitation into an integrated-mail organization before
   the personal email is verified; pressing the invitation link in the registering
   browser verifies it without a code.
5. During the membership the personal email is verified and non-primary, is a member of
   the security-notice set whatever the backup setting, and is refused at sign-in by the
   domain lock (`identity.identifier.domainnotallowed`).
6. During the membership, removing the personal email, making it primary or replacing
   it is refused with `identity.identifier.locked`.
7. Acknowledgement sends one notice of the corporate address to each member of the
   security-notice set as it stood before, and none to the corporate address.
8. An invitation for a corporate address a member holds, or that a standing invitation
   not yet expired reserves, is refused with `identity.mailbox.taken`; re-inviting an
   address whose invitation expired revokes that invitation and keeps the mailbox.

---

**REG-MAIL-002** — Mail app passwords SHALL be created, listed and revoked **from the
account application through the library**: the library holds a first-party OIDC client
for the mail server (AUTH-OIDC-001), obtains a token for the signed-in person, makes the
server's app-password call, shows the generated secret **once** and stores nothing.
Creation and revocation SHALL be step-up actions, notified to the security-notice set
and audited. Each app password SHALL carry a label and MAY carry an expiry. The three
operations SHALL be present only where the account holds a mailbox the mail server is
told to enable (INT-MAIL-006); otherwise, and where the deployment registers no mail
server, each SHALL answer `identity.mailbox.notfound`. A mailbox is owed `enabled`
while its holder is `active` or `restricted`: a restriction the person asked for does
not cut them off from their mail or end their app passwords. A `restricted` account
SHALL list and revoke its app passwords and SHALL be refused creation with
`authz.restricted` (IDN-ACCT-007).

*Source: D-146, D-166; amends D-006 (managed through the library, stored by the mail server)*

A mail credential is the weakest credential in the system (R-A02). The membership step
ends on this section so that a new staff member can set up a phone client before leaving
the wizard.

**Acceptance criteria**
1. No app-password secret or hash exists in the library's schema.
2. Creation and revocation return the step-up code without step-up, and each produces a
   notice and an audit record.
3. Without a mailbox the mail server is told to enable, listing, creation and revocation
   each answer `identity.mailbox.notfound`.
4. A `restricted` account's mailbox stays enabled and its app passwords keep working;
   it lists and revokes them, and its creation is refused with `authz.restricted`.

---

**REG-MAIL-003** — When a membership ends, the corporate address SHALL stop being a valid
identifier of the account and the mailbox SHALL be disabled (INT-MAIL-006a), which ends
every app password. The verified personal email the account has held since the
invitation (REG-MAIL-001) SHALL become the primary email **automatically, in the same
operation**, and the account SHALL continue as an ordinary account without a pause and
without support; where a verified personal phone exists it remains, or becomes, the
primary phone. The organization's domain lock (REG-DOM-001) SHALL no longer apply to the
account once the membership has ended. Membership end SHALL NOT by itself change the
account's state; suspension is a separate administrator action (IDN-LIFE-013,
`suspendedBy = administrator`). A retired corporate address SHALL be available to a
later invitation that names `formerMailbox` (below). Only the end of the membership
that gave the account its corporate address, a membership of the administrative
organization (INT-MAIL-006), retires the address. The change SHALL be notified once to
the security-notice set as it stands after the change, and the retired address SHALL
be sent nothing.

A mailbox anyone has held SHALL never pass silently. An invitation for a corporate
address whose mailbox has been held before, by anyone, the same person and an erased
holder included, SHALL be refused with `identity.invitation.mailboxheld` unless it
names `formerMailbox`: `transfer`, under which the invitee receives the mailbox and the
mail in it, or `replace`, under which the old mailbox is removed and a new one
reserved. Either SHALL pass the invitation's step-up, carry a reason and be recorded.
No check of who the invitee is SHALL be made at issue, since issuing an invitation
tells nothing about accounts. The library SHALL remove a mailbox anyone has held only
under `replace`, and SHALL NOT adopt for a new reservation a mail-server account it did
not create; that refusal raises `degradation` at once, naming the mailbox by its
identifier (INT-MAIL-001).

*Source: D-148; D-146, D-166, `16-offboarding-procedure`*

Every account into an integrated-mail organization is created with a verified personal
email (REG-MAIL-001), so the account never holds zero verified emails (REG-IDENT-001) and
no phone-only continuation and no suspension path are needed. The retired corporate
address behaves as unknown at every path, including recovery.

**Acceptance criteria**
1. After membership ends, sign-in with the corporate address behaves as an unknown
   identifier.
2. After membership ends the account is unchanged in state, its personal email is the
   primary email, and sign-in with it succeeds even where it is outside the former
   organization's `emailDomains` list.
3. No path exists by which membership end leaves an account with zero verified emails
   or changes its state.
4. Ending a membership of any other organization leaves the corporate address, the
   primary email and the mailbox as they were.
5. Membership end sends one notice to each member of the security-notice set as it
   stands after the change and none to the retired address.
6. An invitation of a corporate address whose mailbox has been held before, naming no
   `formerMailbox`, is refused with `identity.invitation.mailboxheld` and changes
   nothing, whoever it invites, its last holder included; under `transfer` the
   invitee's mailbox is the old one with its mail; under `replace` the old mailbox is
   removed and a new one reserved.

---

## 6. Password managers and passkeys

The rules that make managers and passkeys work first time are frontend requirements
(`18` FE-PM-001 to FE-PM-006). Two server-side facts belong here.

**REG-PM-001** — The WebAuthn user handle SHALL be the subject identifier; `name`
SHALL be the primary email and `displayName` the display name or empty. An assertion's
user handle SHALL name the credential's owner, and SHALL be present where the sign-in
named no account (WebAuthn Level 3 section 7.2); any other is refused with
`auth.factor.rejected`. The library SHALL serve `/.well-known/change-password` as a
redirect to the password page and `/.well-known/passkey-endpoints` as JSON naming the
enrol and manage addresses. The three addresses (`changePassword`, `enrol`, `manage`)
are a host declaration with no default (LIB-HOST-001); without it, or with a field of it
empty or blank, startup fails with `model.startup.declarationmissing`, `details.key`
naming `passkeyAddresses` or the field.

*Source: D-146, D-162, D-166; AUTH-FACT-014*

**Acceptance criteria**
1. No user handle contains personal data.
2. `/.well-known/change-password` answers 30x; `/.well-known/passkey-endpoints` answers
   200 with `enroll` and `manage`; the probe path
   `/.well-known/resource-that-should-not-exist-whose-status-code-should-not-be-200`
   answers 404.
3. A deployment that declares no passkey pages, or leaves one of the three empty or
   blank, does not start, and the refusal names what is missing.
4. An assertion whose user handle names another account, or has none where the sign-in
   named no account, is refused with `auth.factor.rejected`.

---

## 7. Open items

None. Multi-market legal document sets are recorded in `04-privacy` section on legal
documents and in `13-risk-register`.
