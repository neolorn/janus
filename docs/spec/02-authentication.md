# 02 — Authentication

Factors, sessions, step-up, recovery, abuse controls, and the OIDC provider.

**Prerequisite:** `00-overview.md`, sections 1 and 3.3.

**Scope.** This document covers *how a person proves who they are* and *how that
proof persists*. Who exists is `01-identity`. What they may do is
`03-authorization`.

---

## 1. Principles

**AUTH-PRIN-001** — Authentication SHALL fail closed. Any error, unavailable
dependency, or indeterminate state SHALL deny.

*Source: P-003*

**Acceptance criteria**
1. A cache outage **never permits**. Where the durable store can answer, the request
   proceeds; denial is required only when it cannot (INF-CACHE-001).
2. A blocklist service outage does not skip screening (AUTH-PASS-004).
3. No code path returns "allow" on exception.

---

**AUTH-PRIN-002** — Authentication policy — login and step-up alike — SHALL be
resolved from the **principal's organization membership**, never from a user type,
an application identity, or the resource acted on:

- A principal holding **no membership** (an individual user) follows the **system
  policy**.
- A principal holding a membership follows **their organization's policy**, which
  **inherits the system policy** for anything the organization has not overridden.
- A principal holding several memberships (multi-membership, IDN-MEM-002 — built,
  switched off) follows the **strictest** of their organizations' policies.

Today the only organization is the administrative organization (D-001).

**"Policy" is one object with seven fields** (`requiredAssurance`, `loginFactors`,
`gates`, `credentialRedundancy`, `selfServiceRecovery`, `emailDomains`, `photos`)
defined in `10` §4.1a and carried by `PUT /admin/organizations/{id}/policy`. The system policy is
`policy.default`; an organization's policy stores only the fields it overrides.
Every rule in this document that says "where the policy requires AAL2" reads
`requiredAssurance`; nothing is inferred from which factors happen to be enabled.

*Source: D-148; D-002, D-020.1, D-086, D-116, D-143, D-166*

**Acceptance criteria**
1. No enum, flag or claim distinguishes staff from customers as a property of a
   principal. The takedown trigger of `10` section 5.12d, which records who raised a
   report, is not such a property.
2. Two organizations with differing policies are enforced independently in one
   deployment.
3. A principal with no membership is unaffected by any organization's overrides.
4. An organization that overrides nothing behaves exactly as the system policy.

---

## 2. Factors

### 2.1 The factor model

**AUTH-FACT-001** — Factors SHALL be registered with **properties**, and every rule
SHALL read properties rather than factor identity.

Registered properties:

| Property | Meaning |
|---|---|
| `CanBePrimary` | May begin an authentication |
| `CanBeSecondFactor` | May satisfy a second-factor requirement |
| `IsPhishingResistant` | Resists credential relay |
| `AssuranceLevel` | Highest AAL this factor can contribute to |
| `VerificationOnly` | Proves control of a channel; never authenticates |
| `Restricted` | Rides a channel that AUTH-FACT-002b restricts; the library's own rules for restricted factors read it |

**Values (D-153).** The device description a credential or session records is derived
server side from the `User-Agent` header as `{ browser, os }`, each at most 64
characters; the default credential label is the two joined by a space. The frontend
renders it; nothing else is read from the header.

*Source: D-012, D-141, D-146, D-166*

There is no separate "may satisfy step-up" property. Whether a factor, alone or in a
combination, can pass a step-up gate follows from `AssuranceLevel` and
`IsPhishingResistant` against the level the gate declares (AUTH-STEP-002); a second
axis would let two rules disagree about the same factor.

Twelve catalogue entries at launch make per-factor branching untenable. The property model is
what keeps a tenth factor a registration entry rather than an edit to every step-up
rule, recovery flow, and policy check.

**Credential records.** Every enrolled credential SHALL carry `backupEligible` and
`backupState` (AUTH-FACT-013), `addedAt`, `lastUsedAt` and a **label** of 1 to 64
characters, unique per kind per account under the case-insensitive comparison of
OPS-DB-001, defaulting to the client's description of
the device and editable through `PATCH /account/credentials/{id}`. The account's
credential list (`18` FE-ACCT-001) renders these fields; nothing else about a
credential is shown to the person (D-146).

**Acceptance criteria**
1. No conditional anywhere in the library tests for a factor by name.
2. Registering a new factor with `IsPhishingResistant = true` satisfies existing
   step-up rules without those rules being modified.
3. A factor with `VerificationOnly = true` is rejected if offered as primary or
   second factor.
4. A credential record persists `backupEligible`, `backupState`, `addedAt`,
   `lastUsedAt` and `label`; `lastUsedAt` advances on every successful use.
5. A label of 0 or of 65 characters is refused; a label already held by another
   credential of the same kind on the account, in any capitalisation, is refused with
   `auth.credential.labelinvalid`.
6. A WebAuthn or TOTP enrolment that supplies no label receives the client's device
   description; `PATCH /account/credentials/{id}` changes the label and nothing else.

---

**AUTH-FACT-002** — The factor catalogue SHALL be as follows, and each entry's
availability SHALL be runtime-configurable per organization through the policy's
`loginFactors` (`10` section 4.1a), which SHALL be the only switch that enables a
factor for a principal; no configuration key does so (`factor.<name>.enabled` is
retired, D-148).

| Factor | Identifier | Primary | Second | Phishing-resistant | Contributes (AUTH-SESS-005a) | Notes |
|---|---|---|---|---|---|---|
| Password | `password` | ✓ | — | No | AAL1 alone; AAL2 with a second factor | |
| Passkey (discoverable credential) | `passkey` | ✓ | — | Yes | AAL2 alone | Signs in; created with `residentKey: required` (AUTH-FACT-002b); conditional UI where supported |
| Cross-device sign-in (hybrid) | `passkey` (a transport of the same entry) | ✓ | — | Yes | AAL2 alone | FIDO2 hybrid transport |
| Sign-in link by email | `emailLink` | ✓ | — | No | AAL1 at sign-in only; never a second step (AUTH-FACT-003) | Off by default; mailbox compromise would yield both |
| Email code | `emailCode` | ✓ | — | No | AAL1 at sign-in only; never a second step (AUTH-FACT-003) | Off by default, as `emailLink`; mailbox compromise would yield both |
| Sign-in link by SMS | `phoneLink` | ✓ | — | No | AAL1 at sign-in only; never a second step (AUTH-FACT-003) | Off by default; restricted factor (AUTH-FACT-002b) |
| Google | `google` | ✓ | — | No | `delegated` — no asserted AAL | Credential only; freshness unverifiable (AUTH-STEP-005) |
| Apple | `apple` | ✓ | — | No | Same | Same |
| TOTP | `totp` | — | ✓ | No | Nothing alone; AAL2 beside a password | |
| Security key as second factor (non-discoverable WebAuthn credential) | `securityKey` | — | ✓ | Yes | Nothing alone; AAL2 phishing-resistant beside a password | Created with `residentKey: discouraged`; upgradable to a passkey (AUTH-FACT-002b) |
| SMS code | `phoneCode` | — | ✓ | No | Nothing alone; AAL2 beside a password, never phishing-resistant | Off by default; flagged less secure wherever listed; restricted factor (AUTH-FACT-002b) |
| Recovery codes | `recoveryCodes` | — | ✓ | No | Nothing alone; AAL2 beside a password | Single-use; not counted in reachable assurance (AUTH-STEP-006) |
| Verification code (email or SMS) | none (not an entry) | — | — | — | — | **Verification only** (AUTH-FACT-004) |
| Break-glass credential | `breakGlass` (never in `loginFactors`) | ✓ | — | No | Satisfies every gate for the session's lifetime | Emergency only; AUTH-STEP-004 |

The identifier is the value used in `loginFactors` (`10` section 4.1a), in `/auth/factor`
requests and in credential records; eleven identifiers may appear in `loginFactors`
(D-151).

*Source: D-151; D-148, D-012, D-009, D-013, D-141, D-146, D-147, D-166*

Conditional UI (autofill) is a presentation mode of passkey sign-in, not a separate
factor. A hardware security key is one way to hold a WebAuthn credential, appearing
under either heading depending on registration (AUTH-FACT-002b).

**`emailLink`, `emailCode`, `phoneLink` and `phoneCode` are off by default** in
`loginFactors` (`10` section 4.1a): every text costs money and reaches a number, not a
person, and a sign-in link or an emailed code is only as strong as the mailbox or the
SIM behind it. `emailCode` and `emailLink` are the same capability (an email alone signs
a person in), so they share the default (D-147). A host enables them by policy (D-146).

**SMS still carries verification codes and, under AUTH-RECOV-002, an admin-assisted
enrolment link.** Neither is authentication: a verification code proves control of a
number (AUTH-FACT-004), and the enrolment link is authorised by a human approver's
out-of-band confirmation, not by possession of the phone (D-111). Where SMS *is* an
authentication factor (`phoneLink`, `phoneCode`) it is a **restricted** factor under
NIST SP 800-63B-4 section 3.1.3.3 (AUTH-FACT-002b).

**Acceptance criteria**
1. `emailLink`, `emailCode`, `phoneLink` and `phoneCode` are absent from every
   enrolment and sign-in screen until the applicable policy's `loginFactors` enables
   them.
2. Disabling a factor for an organization prevents new enrolment and blocks its use
   for authentication, without a deploy.
2a. Removing an entry from `loginFactors` is the only way to disable a factor for an
   organization; no configuration key enables or disables one.
3. Disabling a factor does not delete existing enrolments.
4. No sign-in path accepts a verification code as a credential; an SMS code accepted
   as a second step is one issued for `phoneCode` on that sign-in.
5. A sign-in by `phoneLink` alone records AAL1; password plus `phoneCode` records
   AAL2 and not phishing-resistant (AUTH-SESS-005a).
6. A second-step challenge that offers `phoneCode` sends its code to the number as the
   message kind `secondstep-code` under the purpose `secondfactor` (AUTH-ABUSE-004),
   an authentication code under AUTH-FACT-004, and that code, presented, completes the
   sign-in.

---

**AUTH-FACT-002b** — The security step SHALL follow one model. A **passkey signs
in**: it is a discoverable credential created with `residentKey: required` and
`userVerification: required`, held on a device or on a hardware key as the person
chooses in the passkey dialog. A **security key under two-step** is a non-discoverable
second factor created with `residentKey: discouraged`. **Two-step SHALL be
unavailable until a usable password exists**; a passkey-only account has no second
step. A second-factor security key MAY be **upgraded to a passkey** by re-registration
(`POST /account/credentials/{id}/upgrade`); on success the second-factor entry SHALL
be retired. Each account SHALL carry a **preferred second-step method**
(IDN-ATTR-008). `phoneLink` and `phoneCode` SHALL be treated as **restricted
factors**: their catalogue entries carry `Restricted` (AUTH-FACT-001), which criterion
6 reads; the limitation SHALL be shown at enrolment, `phoneCode` SHALL be flagged
less secure wherever it is listed, and SIM-change and porting risk signals, where
available, SHALL be considered before an SMS factor is used and before a recovery link
is sent by text.

**Values (D-153).** The SIM-change or porting signal reaches the library through an
optional host callback declared on the model builder (LIB-HOST-001), returning `none`
· `clear` · `risk` for a number; where no callback is declared the record says
`unavailable` and the rule of criterion 6 does not apply. The less-secure marker the
person sees is the frontend's, keyed on the catalogue identifier (`18` FE-SEC-001); no
response of `09` carries a marker field.

*Source: D-146; NIST SP 800-63B-4 section 3.1.3.3; IDN-ATTR-008; `18` FE-SEC-001; D-166*

Two-step is a second factor *beside a password*; without a password there is nothing
for it to be second to, and a passkey is already two factors (AUTH-SESS-005a). The
upgrade exists because the same hardware key can hold either kind of credential, and a
person who bought one for two-step should not have to buy another to go passwordless.

**Acceptance criteria**
1. The passkey dialog requests `residentKey: required` and `userVerification:
   required`; the security-key dialog under two-step requests `residentKey:
   discouraged`.
2. Enrolment of TOTP, a second-factor security key or `phoneCode` is refused on an
   account with no password; a passkey-only account shows no second-step section.
3. `POST /account/credentials/{id}/upgrade` on a second-factor security key completes
   a passkey registration; on success the passkey is listed and the second-factor
   entry is retired; on failure nothing changes.
4. A second-step challenge offers the preferred method first and the others from it
   (IDN-ATTR-008).
5. Enrolling `phoneLink` or `phoneCode` shows the limitation before the number is
   confirmed; `phoneCode` carries a less-secure marker in the enrolment list, the
   credential list and the challenge.
6. Where a SIM-change or porting signal is available for the number, it is evaluated
   before an SMS factor is sent and before a recovery link is sent by text, and the
   outcome is audited; where none is available, the absence is recorded. On `risk`: at
   a sign-in the restricted entries are withheld and the challenge offers the
   account's other factors, and a sign-in with none left is refused with
   `auth.factor.rejected`; a sign-in link asked for by text is not sent, and
   `POST /auth/link` answers 202 as it always does; at a step-up the restricted entries
   are withheld from the combinations offered (AUTH-STEP-002); a recovery link is not
   sent to the number, and `/recovery/begin` answers 202 as it always does. The signal
   is asked of the number, so every answer is the same whether or not an account holds
   it (AUTH-ABUSE-003 AC1).

---

**AUTH-FACT-002a** — Social sign-in SHALL NOT require a second factor.

*Source: D-086*

Social sign-in is **delegated authentication** — another party establishes who this
is. If the answer is yes, that is an absolute yes; requiring our own second factor
afterwards second-guesses a decision deliberately outsourced.

This is the federation model NIST SP 800-63C describes — the relying party relies on
the provider's assertion and does not re-run the verifier — and nothing in 800-63B/C,
OWASP or OpenID Connect requires a relying party to stack its own second factor on a
federated sign-in. What the model does require is that the relying party claim no
assurance it cannot see and **gate sensitive actions itself**: step-up, which social
sign-in can never satisfy (AUTH-STEP-005), so a socially signed-in person still proves
the level the gate declares, with the account's own factors, at an email change or an
export (AUTH-STEP-002).

**Disclosed once, at link time.** When a subject links a social provider to an
account that holds a second factor, the flow states in one sentence that signing in
with that provider will not ask for the authenticator, and that the provider's own
account security protects them there. Recorded as accepted position R-A16.

Administrative-organization members are unaffected: their policy does not permit
social sign-in.

*Source: D-086, D-128, D-166*

**Acceptance criteria**
1. Completing a social sign-in yields a usable session with no further challenge.
2. A subject who has also enrolled a second factor is not challenged for it at sign-in.
3. The resulting session records `delegated` — no asserted AAL (AUTH-SESS-005a).
4. The step-up prompt on that session offers only combinations of the account's own
   factors that reach the level the gate declares; the social credential contributes
   nothing to them.
5. Linking a provider to an account holding a credential that is a second step in the
   catalogue (AUTH-FACT-002) shows the disclosure before the round trip starts; the
   frontend renders it from the credential list (FE-ACCT-001 AC6).

---

**AUTH-FACT-003** — Email-based factors SHALL NEVER count as a second factor,
satisfy step-up, or raise an account's assurance level. **No link, by email or by
SMS, SHALL ever count as a second step.** Neither SHALL be offered as a second-step
option in enrolment. A sign-in link SHALL be requested through `POST /auth/link`,
SHALL live `link.magic.lifetime`, and SHALL complete **only in the browser that
requested it and only on a press**; opened anywhere else it SHALL show a code to
type where the sign-in began, an authentication code under AUTH-FACT-004, and a plain
open SHALL change nothing (REG-SESS-003).

**Values (D-166).** A sign-in link is the authentication application's landing origin,
`LandingOrigins.Authentication` (LIB-HOST-001), followed by `/link#sign-in.<token>`. The
other links this chapter sends take the same form with a kind of their own (API-LAND-001,
the link kinds of `10`): the recovery link a person asks for, `recovery`
(AUTH-RECOV-005), and the admin-assisted enrolment link, `enrolment` (AUTH-RECOV-002), on
the authentication application; the cancel link of a loss notification, `loss-report`
(AUTH-RECOV-007), on the account application's origin, `LandingOrigins.Account`. The
token travels in the fragment, so it never reaches a server in an address. Every link
acts only on a press, never on load, so a mail scanner's prefetch changes nothing.

*Source: D-009, D-146, D-166*

Password plus email OTP, where email is also the recovery channel, is single-factor
wearing an MFA badge: one mailbox compromise yields both the reset link and the
"second" factor. A link, whichever channel carries it, is a primary that proves
control of a channel; an SMS *code* beside a password is the one SMS use that is a
second step (AUTH-FACT-002, `phoneCode`).

**Acceptance criteria**
1. `emailCode`, `emailLink` and `phoneLink` do not appear in the second-step enrolment
   interface.
2. A session authenticated by a sign-in link alone, email or SMS, records AAL1.
3. A step-up requirement is not satisfied by an email factor or by a link.
4. A sign-in link opened in the requesting browser signs in on a press and not on a
   plain open; opened in another browser it shows the code and signs nothing in; the
   code typed where the sign-in began completes it.
5. `POST /auth/link` refuses a channel whose link factor the policy has not enabled,
   with the response of AUTH-ABUSE-003.

---

### 2.2 Verification codes are not authentication codes

**AUTH-FACT-004** — Channel verification codes and authentication codes SHALL be
modelled as distinct concepts with independent lifetimes, storage, and validation
rules. A verification code SHALL NEVER be usable as an authentication credential.
A verification code SHALL live `code.verification.lifetime` and SHALL be invalidated
after `code.verification.attempts` (default **5**) wrong tries; a replacement code
draws on the sending restrictions (AUTH-ABUSE-004). A code SHALL validate once: the
correct code, once accepted, is refused if presented again. Every channel verification
code (a registration's, an identifier's, the new-device check's) SHALL be issued and
answered through one verification-code record, in a table of its own that no
credential is reachable through. A wrong try and the spending of the right code SHALL
be decided under a lock on that record, so that concurrent tries count as the same
number of sequential ones and the right code answers once.

**Authentication codes.** The `emailCode` sign-in code, the code of a `phoneCode`
second step (AUTH-FACT-002), and the code a sign-in link shows where it is opened in
another browser (AUTH-FACT-003) are authentication codes, held apart from verification
codes. The `emailCode` and `phoneCode` codes SHALL live `code.signin.lifetime` (default
**10 minutes**, ceiling 30 minutes); the code a sign-in link shows SHALL live as long as
its link, `link.magic.lifetime`. Every authentication code SHALL be invalidated after
`code.signin.attempts` (default **5**, ceiling 10) wrong tries, SHALL validate once, and
SHALL be decided under a lock as a verification code is. The `emailCode` code SHALL be
sent as the message kind `sign-in-code` and the `phoneCode` code as `secondstep-code`,
never as a verification code.

*Source: D-034, D-146, D-166*

Collapsing them is a common source of bugs in which a verification code becomes a
login credential. The attempt cap makes a six-digit code unguessable within its
lifetime without a lockout: the code dies, the person asks for another, and the
restrictions bound how many they can ask for.

**Acceptance criteria**
1. A verification code presented at a sign-in endpoint is rejected.
2. The two use separate storage and separate expiry policies.
3. With the attempt cap at 5, each of five wrong attempts is refused with
   `auth.code.invalid`; a sixth attempt is refused with `auth.code.expired`, the
   correct code included, as is a code past its lifetime. A new code can be requested
   within the restrictions and the old one never validates again. An authentication
   code behaves alike under `code.signin.attempts`.
4. Ten wrong codes presented concurrently against one code, with
   `code.verification.attempts` at 5, end it, and the correct code is refused after
   them; two concurrent presentations of the correct code succeed once.
5. A code accepted once is refused when presented a second time.
6. An `emailCode` sign-in code and a `phoneCode` second-step code live
   `code.signin.lifetime`, and the code a sign-in link shows elsewhere lives
   `link.magic.lifetime`; each dies after `code.signin.attempts` wrong tries, whatever
   `code.verification.lifetime` and `code.verification.attempts` hold. The `emailCode`
   code is sent as `sign-in-code`.

---

### 2.3 TOTP

**AUTH-FACT-005** — TOTP SHALL use 30-second time steps and 6 digits, accept a drift
tolerance of **one step either side**, and reject reuse of a consumed code within
its own window.

*Source: D-034*

Wider drift tolerance is a genuine weakening. Without replay prevention, an observer
has up to 30 seconds to reuse an observed code.

**Acceptance criteria**
1. A code from two steps ago is rejected.
2. A code from one step ago is accepted.
3. Submitting the same valid code twice succeeds once and fails once.

---

**AUTH-FACT-006** — A TOTP secret is the account's credential secret: it SHALL be
encrypted at rest under the account's subject key (PRIV-RIGHT-005a) and SHALL be
destroyed with that key.

*Source: D-034, D-026.3, D-166*

**Acceptance criteria**
1. Secrets are not readable from a database dump alone.
2. Once the account's subject key is destroyed, its TOTP secret cannot be read.

---

**AUTH-FACT-007** — TOTP enrolment SHALL require submission of one valid code before
the factor becomes active.

*Source: D-034*

Prevents lockout from a mis-scanned QR code.

**Acceptance criteria**
1. An enrolment abandoned before confirmation leaves no active factor.

---

### 2.4 Recovery codes

**AUTH-FACT-008** — Recovery codes SHALL be issued in sets of 10, single-use,
displayed once at generation, and stored hashed. The set SHALL record `viewedAt`, set
when the response that returns it is produced, and `exportedAt`, set when the frontend
reports a copy, download or print (`POST /account/recoverycodes/exported`), and a
**reminder** SHALL be sent through the account and as a notice when
`recovery.codes.reminder` (default **365 days**) has elapsed since the set was
generated.

**Values (D-153).** A recovery code is 10 symbols from the Crockford base32 alphabet
(about 50 bits), shown as two groups of five and hashed as a password is.

The reminder is the message kind `recovery-codes-reminder`, carrying no link, sent by a
daily pass to every channel of the security-notice set of an **active** account. The
set records `remindedAt` once at least one channel took the reminder, or where the set
holds no channel it can reach; a set whose every notice was refused stays owed and is
reminded at a later pass. A set of an account that is not active stays owed and is
reminded if the account becomes active again. `remindedAt` is shown in the account and
carried in the export.

*Source: D-034, D-146, D-166*

Hashed as passwords are: verifiable, never recoverable. The timestamps let the
account say whether the person ever saved the codes; the reminder exists because a set
saved a year ago is often on a device or a sheet of paper the person no longer has.

**Acceptance criteria**
1. A used code is rejected on second presentation.
2. Codes are not retrievable after the generation screen.
3. A database dump does not yield usable codes.
4. `viewedAt` is set when the response that returns the set is produced; `exportedAt`
   is set by `POST /account/recoverycodes/exported` and stays unset otherwise; both are
   visible in the account.
5. A set older than `recovery.codes.reminder` produces one reminder in the account
   and one notice to the security-notice set, and no further reminder until the set
   is regenerated.

---

**AUTH-FACT-009** — Regenerating recovery codes SHALL invalidate the entire previous
set, and the remaining unused count SHALL be visible in the account.

*Source: D-034*

**Acceptance criteria**
1. After regeneration, no code from the previous set validates.
2. The account displays the remaining count.

---

### 2.5 WebAuthn

**AUTH-FACT-010** — The relying party identifier SHALL be consumer configuration,
validated at startup as a registrable suffix of every configured origin. Where unset,
the library SHALL derive the common registrable parent domain rather than defaulting
to the first origin.

**Values (D-166).** Registrable suffixes and labels are judged against the Public
Suffix List, its ICANN and its private sections alike, as browsers apply them. The list
ships unmodified, with its header, as a resource embedded in the package and dated,
downloaded from publicsuffix.org when each release is built (CONV-VCS-005); NOTICE
names the list, its licence (Mozilla Public License 2.0) and its source address. The
library uses it only to validate the deployment's own configured origins (this item and
AUTH-FACT-012 AC2).

*Source: D-004, D-166*

A passkey created for `example.com` works across its subdomains; the reverse does
not. The parent domain is the widest door and keeps a future mobile app usable with
existing credentials.

A copy of the list that has aged cannot weaken WebAuthn: the browser applies its own
current list to every ceremony, so the worst case is a disagreement about the
deployment's own domains, and it surfaces at startup. That is why a refresh at each
release is the proportionate update.

**Acceptance criteria**
1. A relying party identifier that is not a registrable suffix of a configured
   origin fails startup with a named error.
2. With origins across subdomains and no explicit setting, the derived value is the
   common parent.
3. Startup failure occurs before any credential can be enrolled.

---

**AUTH-FACT-011** — The relying party identifier in force SHALL be recorded against
each stored credential.

*Source: D-004*

Makes a later configuration change detectable, so re-enrolment can be prompted
rather than sign-in failing with no explanation.

**Acceptance criteria**
1. A credential enrolled under a previous identifier is detected and the user is
   prompted to re-enrol.
2. Detection does not require comparing configuration to credential at every
   authentication.

---

**AUTH-FACT-012** — The library SHALL serve a related-origins allowlist from
configured additional origins.

*Source: D-004, D-166*

**Acceptance criteria**
1. The well-known document lists exactly the configured origins.
2. Exceeding the browser limit of **five distinct labels** — the name immediately
   before the public suffix, so `shop.com` and `shop.co.uk` count once — fails
   validation at startup (WebAuthn Level 3, Related Origin Requests). The public suffix
   is read from the list of AUTH-FACT-010.

---

**AUTH-FACT-013** — Backup eligibility and backup state SHALL be recorded at
registration.

*Source: D-008, D-034*

These drive the adaptive second-credential prompt (AUTH-RECOV-001).

**Acceptance criteria**
1. Both flags are persisted with the credential.
2. A synced credential and a device-bound credential are distinguishable after
   registration.

---

**AUTH-FACT-014** — WebAuthn SHALL require user verification, SHALL NOT require
attestation, SHALL restrict signature algorithms to a configured allow-list, and
SHALL verify the signature counter where the authenticator provides one.

*Source: D-034, D-120*

**The allow-list is configuration** (`webauthn.algorithms`, `10` §4.3): default
**EdDSA (−8), ES256 (−7), RS256 (−257)**, offered in that preference order — the
three every major passkey provider produces, and the set the W3C specification's own
registration example accepts. ES256 cannot be removed, so the list can never be
emptied or reduced below what all authenticators support.

Requiring attestation excludes legitimate authenticators for no benefit at this risk
level. A counter moving backwards indicates a cloned credential; synced passkeys do
not supply one, so the check applies only where present.

**Acceptance criteria**
1. An authentication without user verification is rejected.
2. A credential from an unattested authenticator enrols successfully.
3. A counter lower than the stored value is rejected and the event audited.
4. A credential using an algorithm outside the allow-list is rejected at enrolment.
5. The default allow-list is exactly −8, −7, −257; a configuration omitting −7 is
   refused at startup.

---

**AUTH-FACT-015** — After a completed two-factor sign-in, a principal under a policy
whose `requiredAssurance` is below AAL2 MAY mark the browser as a **trusted device**, and the
second factor SHALL then be skipped on that browser for `factor.trusteddevice.lifetime`
(default **30 days**, ceiling enforced). The primary factor is always still required.

**Values (D-153).** The trust token lives in `__Host-identity-device`: 32 random bytes,
base64url, stored server side against the account.

*Source: D-124, D-166*

**Mechanics.** A separate, opaque, single-purpose device token in its own `__Host-`
cookie, stored server-side against the account with created-at, last-used, and a
human-readable device label; never a claim, never a session. Presenting it satisfies
the second-factor requirement of the sign-in only; it raises no assurance property on
the resulting session (AUTH-SESS-002) and never satisfies step-up. Each trusted device
appears in the account's device list (`GET /account/devices`) and can be removed
there. **All trusted devices are revoked** on password change, on "sign out
everywhere" (AUTH-SESS-008), on any recovery, and when the account leaves `active`.

**Not offered where the policy's `requiredAssurance` is AAL2** (the administrative organization):
their members hold passkeys, which need no second factor to skip, and the floor
(AUTH-SESS-005b) would in any case be violated by a device-trust shortcut.

**A trusted device writes exactly what it presented.** The session records AAL1 —
the password — and nothing downstream assumes more (D-141).

**Not offered to a `restricted` account** (IDN-ACCT-007): a trusted device is a
credential of the account (REG-ACCT-001), which a restricted account does not change,
so its sign-in is not offered the option and records no trusted device (D-166).

**Acceptance criteria**
1. A customer signing in with password + TOTP on a trusted device is asked for the
   password only.
2. The resulting session records AAL1, and step-up on it requires a fresh factor.
3. The option is absent for a principal whose policy's `requiredAssurance` is AAL2.
4. A password change, recovery, or "sign out everywhere" clears every trusted device
   for the account.
5. A trusted device older than `factor.trusteddevice.lifetime` is asked for the
   second factor again.
6. **Three consecutive wrong passwords on a trusted device revoke that device's
   trust** (`factor.trusteddevice.failurelimit`, default 3); the next sign-in on it
   requires the second factor. The count is kept per trusted device and resets on a
   successful sign-in on it.
7. The offer is **withheld while the account's password is below the single-factor
   floor** (AUTH-PASS-001a): on a trusted device that password would complete a
   sign-in alone. The offer appears once the password meets the floor; an existing
   trust is revoked if the password is later changed to one below it. R-A14 records
   that this exposure no longer exists.
8. A `restricted` account's sign-in is not offered the option, and a request to trust
   the browser on it records no trusted device.

---

### 2.6 New-device check

**AUTH-FACT-016** — Where `device.verification.enabled` is on (default **true**), a
sign-in to an account whose reachable assurance is AAL1 (AUTH-STEP-006) from a browser
the account has not seen SHALL NOT complete until the person enters a code sent to the
account's **primary email**. The browser SHALL then be remembered for
`device.verification.lifetime` (default **90 days**). Completing the terms step of
registration (REG-SESS-007) SHALL record the registering browser as seen for the same
period, so the sign-in that registration performs and the next one from that browser
are not held for a code. A passkey sign-in and a
completed two-step sign-in SHALL never trigger the check. The send SHALL be subject to
the email destination restriction (AUTH-ABUSE-004). The pending state SHALL be
reported as the sign-in status `deviceVerificationRequired` of `POST /auth/factor`,
answered 200 and not a refusal; completion SHALL raise `DeviceVerified`.

**Values (D-153).** A browser that passed the check is remembered by a
`__Host-identity-browser` cookie: 32 random bytes, base64url, stored server side, valid
for `device.verification.lifetime`.

*Source: D-148; D-146; AUTH-FACT-004, AUTH-ABUSE-004; D-166*

A single-factor account's only protection against a leaked password is that the
attacker is usually somewhere else. The check turns that into a rule without making
email a second factor: the session that results still records AAL1 (AUTH-FACT-003),
and the code is a verification code (AUTH-FACT-004), not a credential.

**Acceptance criteria**
1. A password-only sign-in from an unrecognised browser answers 200 with `status`
   `deviceVerificationRequired` and no session until the emailed code is entered; the
   resulting session records AAL1.
2. The same browser signs in without the check inside
   `device.verification.lifetime` and is asked again after it.
3. A passkey sign-in, and a password plus second-step sign-in, from an unrecognised
   browser complete without the check.
4. The code goes to the primary email only, counts against `email.destination`, and
   is refused with `auth.restriction.exceeded` when that restriction is exceeded.
5. `DeviceVerified` is emitted once per completed check, with the browser identifier
   and no personal data.
6. With `device.verification.enabled` off, no sign-in triggers the check.
7. A password-only account's first sign-in after registration, from the browser that
   completed the terms step, is not held for a code inside
   `device.verification.lifetime`.

---

### 2.7 Grace when a requirement is raised

**AUTH-FACT-017** — When a policy's `requiredAssurance` or `credentialRedundancy` is
raised, affected accounts SHALL have `policy.enforcement.grace` (default **0**) to
comply. During the grace, a sign-in by an account that does not yet meet the new
requirement SHALL be told the requirement and the deadline and MAY continue; after
it, the sign-in SHALL stop at enrolment (`auth.policy.graceexpired`) until the
requirement is met. An account created after the change SHALL be held at enrolment
at once. Live sessions SHALL be handled by AUTH-SESS-009.

**Values (D-153).** `policyRequirement` is `{ field, value, deadline }` with `field`
one of `requiredAssurance` · `credentialRedundancy` (the two policy fields that can be
raised), `value` the new requirement and `deadline` the instant the grace ends. At an
invitation's acknowledgement, where no grace applies to the joining account, it
carries no `deadline` (REG-INV-002, D-166).

*Source: D-146; AUTH-SESS-009, AUTH-STEP-002; D-166*

A default of zero is the safe default for a generic library: an organization that
raises its floor gets the floor at once unless it asks for a run-up. The run-up exists
so that a host with many members can announce the change and let people enrol on
their own schedule rather than on their next morning's sign-in.

**Acceptance criteria**
1. With the grace at 0, raising `requiredAssurance` holds every non-compliant member
   at enrolment on their next sign-in.
2. With a non-zero grace, a non-compliant sign-in inside it succeeds and carries the
   requirement and the deadline in the response; one after it returns
   `auth.policy.graceexpired` and offers enrolment.
3. An account created during the grace is held at enrolment at its first sign-in.
4. Lowering a requirement never produces a grace state or a hold.

---

## 3. Passwords

**AUTH-PASS-001** — Password length floors SHALL be **15 characters** where the
password is the only factor and **10 characters** where combined with MFA. Up to
**128** characters (`password.maximum`) SHALL be supported.

*Source: D-011, D-146, D-166*

10 rather than the standard's 8: at that length the blocklist does the real work,
and pushing floors higher drives pattern reuse.

**Acceptance criteria**
1. A 14-character password is rejected for a single-factor account.
2. A 10-character password is accepted where MFA is enrolled.
3. A 128-character password is accepted and not truncated; a 129-character one is
   refused with `auth.password.toolong`, and nothing is truncated.

---

**AUTH-PASS-001a** — A password below the single-factor floor SHALL exist on an
account only while that password **can never complete a sign-in by itself**: the
account's reachable assurance is AAL2 (AUTH-STEP-006 — a second factor in state
`active`) and no trusted device is offered (AUTH-FACT-015 AC7). The floor is not
chosen by the person; the **second factor is the price of the shorter floor**.

*Source: AUTH-PASS-001, D-114, D-141*

The rule is a property of the password, not a fact about enrolment, so every path by
which a password could come to authenticate alone is covered by the one sentence.
It holds at every point a password is set: at registration, where a short password
makes second-factor enrolment a required part of the security step and the account is
not created until it is enrolled (REG-SESS-006); at `POST /account/password`, where a
short password is accepted only if the account's reachable assurance is AAL2; and
when invalidation of a second factor lowers the reachable assurance, where
AUTH-RECOV-007a forces a change. Whether the stored password meets the
single-factor floor SHALL be recorded alongside the hash, since it cannot be
recomputed from the hash later.

Email-based authenticators do not count as a second factor here, as nowhere else
(AUTH-FACT-003, D-009).

**Acceptance criteria**
1. A registration with a 10-character password and no second factor never reaches
   `active`.
2. `POST /account/password` with a 10-character password is rejected with
   `auth.password.tooshort` on an account whose reachable assurance is AAL1, and
   accepted on one whose reachable assurance is AAL2.
3. The single-factor-floor flag is stored with the hash and updated on every set or
   change.
4. The trusted-device offer is absent on an account whose password is below the
   floor (AUTH-FACT-015 AC7).

---

**AUTH-PASS-002** — Composition rules SHALL NOT be imposed. No character-class
requirements, no forbidden patterns.

*Source: D-011*

**Acceptance criteria**
1. A 15-character all-lowercase password not on the blocklist is accepted.

---

**AUTH-PASS-003** — Passwords SHALL NOT expire on a schedule. Rotation SHALL be
required only on evidence of compromise.

*Source: D-011*

**Acceptance criteria**
1. No scheduled expiry field exists.

---

**AUTH-PASS-004** — Every password SHALL be screened against a compromised-password
corpus at set and at change. Screening failure SHALL fail loudly, never silently
skip.

Primary source is the range API; a curated offline list ships as fallback; a
self-hosted corpus is available as configuration.

**The leaked-password list is the only rejection source by default.**
`password.blocklist.sources` MAY add `dictionary` (a word list) and `context` (the
person's own identifiers and profile fields and the service name), both off by
default. Context words SHALL be checked at set and at change only; a profile field
added later that matches the password SHALL be caught at the next sign-in with a
prompt to change, never a lockout.

**Values (D-153).** The offline leaked list is the 100,000 most prevalent SHA-1 hashes
of the Pwned Passwords downloadable corpus (the range API's provider), refreshed at
each release (CONV-VCS-005) and dated so `password.blocklist.corpusmaxage` can judge
it. The provider's published terms set no licensing or attribution requirement on the
Pwned Passwords API and welcome attribution; the NOTICE file names the source (Have I
Been Pwned, Pwned Passwords) and the date the list was drawn. The draw is taken through
the range API and identifies itself with an accurate user agent, as the provider's
acceptable use requires; so does every range request made at screening, naming the
library and its version (INT-PWD-001). The `context` source reads the service name from
`service.name` and the person's own identifiers and display name. A sign-in that
completes on an account whose password now fails `context` (criterion 5) returns
`passwordChangeRequired: true` beside `status: complete` (AUTH-RECOV-007a).

**Values (D-166).** The leaked list is a resource embedded in the package, its first
line the date it was drawn; a deployment holds no file of its own. A list older than
`password.blocklist.corpusmaxage`, or whose date cannot be read, SHALL NOT answer a
screening; screening then proceeds as with the list unavailable (criterion 3). The
self-hosted corpus answers the range protocol of INT-PWD-001 at
`password.blocklist.selfhosted.address`, over TLS; where it cannot answer, the
package's list answers and the degradation is raised, as for the range API.

The `dictionary` source ships two lists embedded in the package. The English list is
Alan Beale's 3esl list from the 12dicts 6.0.2 package, which its author releases to the
public domain with acknowledgment requested (one NOTICE line), filtered when the
package is built to single lower-case words of four letters or more; the build counts
the result and refuses fewer than 10,000. The Arabic transliteration list is the
library's own work: about 2,500 to 4,000 lower-case entries (given names, Coptic names
included; family names; religious and everyday words; slang and profanity; football
clubs and players; places; and Egyptian Arabizi forms written with the digits 2, 3, 5
and 7), its variants generated by rules when the package is built; like the default
message texts, it is reviewed by the owner before release. A host may extend either
list by declaration. The matcher keeps digits, so an Arabizi entry matches; it counts
every character, digits included, toward the four-character minimum a match needs; and
it never echoes the word it matched.

*Source: D-011, D-041, D-146, D-166*

**A recorded deviation.** NIST SP 800-63B-4 section 3.1.1.2 lists dictionary words
and context-specific words in the blocklist as a SHALL. Janus rejects on the leaked
list alone by default and makes the other two sources opt-in. The reasons: the profile
fields are collected after the password, so a context check at set time has little to
work on; no comparable consumer product rejects on either source; and a password of
fifteen characters that happens to contain a dictionary word is not the failure the
list exists to catch. The deviation is recorded in `13-risk-register`.

**Acceptance criteria**
1. A known-breached password is rejected regardless of length.
2. With the API unreachable, the offline fallback runs and the degradation is logged
   and raised (OPS-OBS-002).
3. With both unavailable, the operation fails rather than accepting unscreened.
4. With `password.blocklist.sources` at its default, a password containing the
   person's own email local part or a dictionary word, meeting the floor and absent
   from the leaked list, is accepted.
5. With `context` enabled, that password is refused at set and at change; a display
   name later set to match an existing password produces a prompt to change at the
   next sign-in, and the sign-in completes.
6. The package's leaked list holds the 100,000 hashes of highest count in the corpus,
   dated on its first line, and NOTICE names its source.
7. An offline list with no readable date answers no screening: with the range API
   unreachable, the operation fails as in criterion 3.
8. The package's English word list holds at least 10,000 entries, each a single
   lower-case word of four letters or more; no feedback, log entry or refusal for a
   `dictionary` match carries the word matched.

---

**AUTH-PASS-005** — Password strength feedback SHALL be advisory. Rejection SHALL
occur only for blocklist hits or floor violations. The feedback SHALL take into
account the person's own name, email and the service name, and SHALL never block on
them.

*Source: D-011, D-146*

Heuristic rejection generates workarounds. Telling the person that their password
contains their own name is useful; refusing it is a composition rule by another name
(AUTH-PASS-002), unless the host has opted into `context` under AUTH-PASS-004.

**Acceptance criteria**
1. A password scoring poorly on a strength heuristic, meeting the floor and absent
   from the blocklist, is accepted with a warning.
2. A password containing the person's display name, their email local part or the
   service name receives feedback naming that, and is accepted unless
   `password.blocklist.sources` includes `context`.

---

**AUTH-PASS-006** — Password hints and knowledge-based recovery questions SHALL NOT
exist.

*Source: D-011*

Prohibited outright by the standard. Without an explicit "never," security questions
reappear in the recovery flow.

**Acceptance criteria**
1. No hint field exists in the schema.
2. No API accepts a security question or answer.

---

**AUTH-PASS-007** — Passwords SHALL be hashed with Argon2id at versioned parameters,
with the parameter version stored per hash and rehashing performed transparently on
next successful sign-in when parameters are raised.

*Source: D-026.3, D-120*

**Parameters are configuration** (`password.argon2.*`, `10` §4.2): default
**m = 19 456 KiB (19 MiB), t = 2, p = 1** — one of OWASP's recommended equal-strength
configurations, chosen over the 46 MiB set because several applications share one
host and a sign-in burst is bounded by memory. **The floor is a strength class, not a
per-parameter number**: any of OWASP's listed equal-strength configurations
(m=47104/t=1, m=19456/t=2, m=12288/t=3, m=9216/t=4, m=7168/t=5, all p=1) or
stronger is accepted; anything weaker than that class is refused
(`config.value.belowfloor`). Raising strength increments the version and triggers
the rehash.

**Acceptance criteria**
1. Raising parameters does not invalidate existing passwords.
2. A user signing in after a parameter change has their hash upgraded silently.
3. The default hashes at m = 19 456, t = 2, p = 1; a configuration below the floor is
   refused at startup.

---

## 4. Sessions

### 4.1 Structure

**AUTH-SESS-001** — A server-side session record SHALL be the spine from which every
credential derives. Revoking it SHALL invalidate everything derived from it.

The record holds: subject, created, last seen, **assurance level attained**,
**phishing-resistance attained**, timestamp of each, origin address, device, idle
expiry, absolute expiry.

Redis for speed, PostgreSQL as the durable copy.

*Source: D-007, D-020.2, D-147*

**Acceptance criteria**
1. Revoking the session invalidates the app session, the auth session, and any
   issued tokens within one request cycle wherever validation consults the record.
   A token a relying party validates offline stays valid until it expires; that
   latency is bounded by `oidc.accesstoken.lifetime` (AUTH-OIDC-004, D-147).
2. The record survives a Redis flush by reload from PostgreSQL.

---

**AUTH-SESS-002** — The session SHALL record **properties reached**, never factor
names. Factor identity SHALL be recorded in the audit trail only.

*Source: D-020.2, D-012*

Otherwise step-up rules match on names, and adding a new phishing-resistant factor
requires editing every rule mentioning passkeys.

**Acceptance criteria**
1. No session field names a factor.
2. The audit record for the authentication does name the factor.
3. A step-up rule is expressed as a required level, whether phishing-resistance is
   required, and a maximum age (AUTH-STEP-002) — never as a factor name.

---

**AUTH-SESS-003** — Browser applications SHALL hold an opaque random session
identifier in a cookie marked `httpOnly`, `Secure`, `SameSite`, with the `__Host-`
prefix. Tokens SHALL NEVER be placed in browser storage.

*Source: D-007*

A token in browser storage is readable by injected script and remains valid until
expiry regardless of server action. An opaque cookie is unreadable by script and
revocable instantly.

**Acceptance criteria**
1. No token appears in `localStorage`, `sessionStorage`, or a non-`httpOnly` cookie.
2. The cookie carries all four attributes.
3. The cookie value is opaque — it yields no information when decoded.

---

**AUTH-SESS-004** — Three session types SHALL exist and SHALL all derive from the
same session record.

| Type | Held by | Purpose |
|---|---|---|
| Per-app session | Each app's BFF | The user's session with that app |
| Auth session | The authentication app | Makes silent SSO between apps possible |
| OIDC tokens | Protocol clients | Stalwart, future native clients |

*Source: D-033.3, D-007*

**Acceptance criteria**
1. A compromised session in one app does not grant access to another.
2. Moving between apps re-establishes silently without re-authentication
   (AUTH-SESS-012).
3. Revoking the underlying record terminates all three.

---

**AUTH-SESS-012** — A per-app session SHALL be established from the auth session by
the **OIDC authorization code flow with PKCE**, with each browser application's BFF a
**confidential client** in the client registry. The BFF SHALL retain no token.

*Source: D-104, D-007, D-033.3, D-147, D-166*

**The flow.** A BFF holding no session pushes an authorization request with
`prompt=none` to `POST /oidc/par` over the back channel and redirects the browser to
`/oidc/authorize` with the returned `request_uri` alone (AUTH-OIDC-006, BFF-SESS-006).
The authentication application answers from its auth-session cookie: a one-time
authorization code where a session exists, `login_required` where none does, in which
case the BFF pushes again without `prompt=none` and the person signs in. The BFF
exchanges the code **back-channel** on the machine profile (BFF-MACH-001) with its
client secret and PKCE verifier, reads the `sid` claim that names the session record,
creates its per-app session bound to that record, and discards the ID token. The per-app session inherits the record's assurance properties
(AUTH-SESS-002).

**What this reuses rather than invents:** the client registry (AUTH-OIDC-001,
API-REDIR-002), exact-match `redirect_uri` (API-REDIR-001), single-use short-lived
codes bound to client and verifier, and client authentication at the token endpoint.
Every one is a standard requirement restated, not a mechanism designed here.

**Acceptance criteria**
1. Every browser application is a registered confidential client with an exact
   `redirect_uri`; an unregistered client cannot obtain a code.
2. With a live auth session, navigation to a second application completes without
   any user interaction.
3. Without one, `prompt=none` yields `login_required`, and only `prompt=none` does; a
   request that is not silent is forwarded to the sign-in address the host declared
   (LIB-HOST-001, `AuthenticationAddresses.signIn`) and never silently issued a
   session. Startup fails with `model.startup.declarationmissing` naming
   `authenticationAddresses.signIn` where it is absent or empty.
4. A code is single-use, expires within `oidc.code.lifetime` (`10` section 4.9;
   default 60 seconds), and is rejected with 400 and no token when presented again,
   after its lifetime, by a client other than the one it was issued to, or without the
   matching PKCE verifier: a spent, lapsed or foreign code and a verifier that does not
   match are answered `invalid_grant` (RFC 7636 section 4.6); a missing verifier is
   answered `invalid_request`.
5. The code is exchanged back-channel; no token ever travels through the browser.
6. No refresh token is issued to a browser-application client, and the BFF stores no
   access or ID token after the exchange.
7. The per-app session references the session record named by `sid`, so revoking the
   record ends it (AUTH-SESS-004).

---

### 4.2 Lifetimes

**AUTH-SESS-005a** — Assurance levels SHALL be assigned as follows.

| Authentication | Level | Phishing-resistant |
|---|---|---|
| Password alone | AAL1 | No |
| Sign-in link alone (email or SMS) | AAL1 | No |
| Email code (`emailCode`) alone | AAL1 | No |
| **Password + SMS code (`phoneCode`)** | **AAL2** | **Never** |
| Google or Apple alone | **`delegated`**, no asserted AAL; see below | No |
| **Passkey alone** (on a device or held on a hardware key) | **AAL2** | **Yes** |
| Password + TOTP | AAL2 | No |
| Password + non-discoverable WebAuthn credential (security key as second factor) | AAL2 | **Yes**: WebAuthn is phishing-resistant whether or not the credential is discoverable (AUTH-FACT-002, D-148) |
| Password + recovery code | AAL2 | No |
| Social + any second factor | **`delegated`**: a second factor contributes nothing without a first factor of ours beside it; sign-in never challenges a social principal for one (AUTH-FACT-002a), and at step-up only a combination of the account's own factors that reaches AAL2 by itself counts (AUTH-STEP-002) | No |
| Sign-in link or email OTP + TOTP or non-discoverable WebAuthn credential | AAL2: the second factor raises it; AUTH-FACT-003 bars the *link or email* factor from counting, not the factor beside it | Yes with the WebAuthn credential, no with TOTP |
| Passkey + password | AAL2: the passkey alone already reaches it; the password adds nothing | Yes |
| Password + non-discoverable WebAuthn credential + TOTP | AAL2: the WebAuthn credential already gives it; TOTP adds nothing | Yes |

The phishing-resistance column is read from the catalogue (AUTH-FACT-002): a
combination is phishing-resistant when a factor marked phishing-resistant is among
those presented. The catalogue's second-factor security key is not a sign-in and has
no row of its own; a hardware key that signs in alone is a passkey held on a hardware
key (AUTH-FACT-002b) and is covered by the passkey row (D-148).

**When the second factor is asked for.** An account holding a second factor is
challenged for it after **every** primary — password, sign-in link, email OTP, passkey
excepted (a passkey is already multi-factor) and social excepted (AUTH-FACT-002a) —
unless a trusted device skips it (AUTH-FACT-015).

*Source: D-148; D-086, D-135, D-141, D-146*

**The same table serves three purposes.** It assigns what a sign-in records
(AUTH-SESS-001), what a combination presented at step-up contributes
(AUTH-STEP-002), and what an account can reach with its usable factors
(AUTH-STEP-006). There is one arithmetic, read from one place.

**No document previously assigned these.** Session lifetimes and step-up both derive
from assurance, and the values existed nowhere — only sign-in-link-alone was pinned, in
AUTH-FACT-003.

**A passkey alone reaches AAL2** because it is a multi-factor cryptographic
authenticator: possession of the credential plus user verification. An implementer
reasoning "one factor presented, therefore AAL1" would give every staff session the
30-day lifetime.

**Social sign-in carries no asserted AAL.** NIST SP 800-63C: the relying party
relies on the identity provider's assertion; where the provider does not assert the
AAL of its session — Google and Apple do not — "no default value is assigned" and the
relying party claims nothing. Janus therefore records `delegated` for a social
session — signed in on the provider's word, level not vouched for by us — treats it
below AAL1 wherever a tier is needed, never counts it toward step-up (AUTH-STEP-005)
and never lets it raise assurance. The word is chosen over "none" because the person
*is* signed in; what is absent is our own evidence of how. Relying on the
provider for sign-in itself is the federation model, not a departure from it
(AUTH-FACT-002a, D-128).

**Acceptance criteria**
1. A passkey-only session records AAL2; its lifetime follows the principal's policy,
   not this table (AUTH-SESS-005).
2. A social-only session records `delegated` — no asserted AAL (`10` §5.4).
2a. Presenting a TOTP code on a `delegated` session leaves it `delegated`.
2b. A password plus `phoneCode` session records AAL2 with phishing-resistance false.
3. No assurance level is derived from a claim asserted by an external provider.

---

**AUTH-SESS-005b** — Administrative-organization sessions SHALL be held to a **stated
assurance floor of AAL2** — the policy's `requiredAssurance` field (AUTH-PRIN-002,
`10` §4.1a), set to `aal2` at bootstrap — independent of factor arithmetic.

No change of the administrative organization's policy SHALL leave its members
resolving `requiredAssurance` below `aal2`; a replacement that would is refused with
`config.value.belowfloor`, `details.field` `requiredAssurance`, before any step-up, and
changes nothing.

*Source: D-089, D-143, D-166*

AUTH-SESS-005's guard — that an organization's configured values SHALL NOT exceed the
table — does not catch this on its own: for an AAL1 session "the table" permits 365
days, and nothing said which row an administrative session belongs to. The floor makes
it explicit rather than emergent.

In practice the administrative organization is passkeys-only and a passkey alone
reaches AAL2 (AUTH-SESS-005a), so the floor is already met. It is stated so that
enabling any additional factor cannot silently drop staff sessions to a 30-day
lifetime.

**A break-glass session is exempt for its lifetime**, mirroring AUTH-STEP-004. It is
created by presenting one printed secret — single-factor and not phishing-resistant — so
enforcing the floor at establishment would refuse it, and the emergency credential could
once again perform none of the actions it exists for. **The compensating control is the
alerting**, as it is for step-up.

*Source: D-089, D-093*

**Acceptance criteria**
1. An administrative-organization session below AAL2 is refused.
2. Enabling a further primary factor for that organization does not lower the floor.
3. **A break-glass session is established successfully despite the floor**, and is
   audited and alerted as such.
4. A replacement of the administrative organization's policy that states `aal1`, or
   omits `requiredAssurance` while the system policy's is `aal1`, is refused with
   `config.value.belowfloor` and changes nothing.

---

**AUTH-SESS-005** — Session lifetimes SHALL be derived from the assurance level the
principal's **policy requires** — its `requiredAssurance` field (AUTH-PRIN-002) —
never from the level a particular sign-in happened to reach, and never from user type
or application identity.

| `requiredAssurance` | Inactivity | Absolute |
|---|---|---|
| **AAL1** (the system policy — customers) | **90 days** without use; each use refreshes it | **365 days** — a definite overall timeout, as NIST §2.1.3 requires; in practice a once-a-year sign-in |
| **AAL2** (the administrative organization) | **1 hour** | **24 hours** |

*Source: D-033.1, D-020.2, D-123, D-130*

**Why the sign-in method does not set the lifetime.** A customer who signs in with a
passkey reaches AAL2, but their policy requires only AAL1; giving them the staff
lifetimes would punish the person who chose the stronger method. The session still
records what was reached (AUTH-SESS-002) — step-up recency and the AAL2 floor for
staff (AUTH-SESS-005b) use that, lifetimes do not.

**Customers are not signed out on a calendar they would notice.** NIST §2.1.3
requires that "a definite reauthentication overall timeout SHALL be established" and
recommends (SHOULD) no more than 30 days at AAL1. The 365-day absolute lifetime
satisfies the SHALL; the 30 days is the one departure, from a SHOULD, made because
consumer practice (Google, every major consumer service) is a session that persists while
used and is revocable in one action. The security of the customer session rests on
the controls that actually matter: an
opaque server-side cookie revocable instantly (AUTH-SESS-001), rotation on every
privilege change (AUTH-SESS-006), step-up for account changes (REG-IDENT-004,
REG-IDENT-006, `POST /account/password`), other sessions terminated on identifier
removal or password change (IDN-LIFE-008), a device list with "sign out everywhere"
(AUTH-SESS-008), and anomaly alerting (OPS-ALERT-001).

**Staff re-authenticate once a day and after an hour away — without losing their
work.** Both figures are NIST SHOULDs at AAL2, kept as defaults because staff see
sensitive data and change grants; the organization may lengthen them up to the
enforced ceilings. After an **inactivity** expiry that is still inside the 24-hour
window, the verifier SHALL accept **a single factor bound to the existing session
secret** — a passkey (biometric or PIN user verification) or the password, never a
TOTP code alone — consistent with NIST §2.2.3 ("a successful password or biometric
comparison in conjunction with the session secret"; a passkey with PIN verification is
possession plus knowledge bound to the session secret, the same class). For a passkey holder
that is one tap. **This single-factor restore applies only where the policy requires
AAL2** (D-139): the allowance is written for a short gap, and a customer's idle limit
is 90 days — a customer whose session lapsed after three months of silence signs in
normally (their trusted device, if any, still applies). The
frontend reauthenticates **in place** and retries the interrupted request with its
data intact (FE-API-004); an expiry never lands the person on a sign-in page with
their work gone.

Runtime-configurable. Ceilings: `session.aal2.absolute` ≤ 24 hours,
`session.aal2.inactivity` ≤ 12 hours, `session.default.inactivity` ≤ 365 days,
`session.default.absolute` ≤ 365 days. A configuration exceeding a ceiling SHALL be
rejected at validation, not silently clamped.

**Acceptance criteria**
1. A customer session used at least once every 90 days expires only at 365 days
   from sign-in.
2. A customer session unused for 90 days expires; a session signed in with a passkey
   has the same lifetime as one signed in with a password.
2a. After an idle expiry, a TOTP code alone does not restore the session; a passkey
   or the password does.
3. An administrative-organization session expires after 1 hour idle or 24 hours
   absolute, whichever comes first.
3a. A customer session expired for inactivity returns `details.reauthenticate =
   full`; the single-factor restore is never offered under the system policy.
4. After an idle expiry inside the 24-hour window, presenting one eligible factor
   restores the same session; after the 24-hour window, full authentication is
   required.
5. Configuration setting the AAL2 absolute maximum to 48 hours is rejected at
   startup with a named error.
6. Organization policy setting a shorter timeout is accepted and applied.

---

**AUTH-SESS-006** — A new session identifier SHALL be issued on authentication, on
step-up, and on any privilege change.

*Source: D-033.4*

Without this, an identifier planted in a victim's browser before sign-in remains
valid afterwards.

**Acceptance criteria**
1. The identifier before and after sign-in differs.
2. The pre-authentication identifier is invalidated, not merely orphaned.

---

**AUTH-SESS-007** — Cookie-based sessions SHALL be protected against cross-site
request forgery by a token unreadable by the attacking origin, required on every
state-changing request and enforced in the BFF layer.

*Source: D-033.2*

`SameSite` alone is not sufficient.

**Acceptance criteria**
1. A state-changing request without a valid token is rejected.
2. No endpoint can opt out — enforcement is not per-endpoint configuration.
3. Rejection is logged.

---

### 4.3 Logout

**AUTH-SESS-008** — Logout SHALL terminate every app session and the auth session
behind them.

*Source: D-033.3*

Per-app logout alone means signing out then navigating to another app silently signs
the user back in — logout appears broken, and genuinely is on a shared device.

**Acceptance criteria**
1. After logout, no app session remains active.
2. Navigating to another app after logout requires authentication.

---

**AUTH-SESS-013** — The account SHALL list its live sessions (`GET /account/sessions`)
with, per session, the time of sign-in, the time of last use, the device description,
a **city-level location** resolved at sign-in and at last use from a **local IP
database** (no external lookup), and a marker on the **current session**. Any listed
session SHALL be revocable on its own (`DELETE /account/sessions/{id}`). The location
SHALL be stored under the person's key (with the coordinates of the city from the
location file, never shown or exported, used only by OPS-ALERT-007) and retained with
the session record, no longer. The library SHALL resolve the location itself, from the
whole client address the session records (not the coarser source AUTH-ABUSE-001 counts
by); no operation SHALL take a location from its caller.

**Values (D-153).** `location` is `{ city, country }` with `country` an ISO 3166-1 alpha-2
code, both nullable and absent when INT-GEN-006 degrades; the device description is
that of AUTH-FACT-001.

*Source: D-146; AUTH-SESS-001, AUTH-SESS-008, AUTH-SESS-011, PRIV-RIGHT-005a; D-166*

Per-account revocation of everything is AUTH-SESS-011 and "sign out everywhere" is
AUTH-SESS-008; this item is the finer grain that lets a person end the one session
they do not recognise and keep working in the one they are in. The trusted-device list
of AUTH-FACT-015 is a different list: a trusted device is a remembered browser, not a
session. The location is city-level because a street-level answer from an IP is wrong
often enough to mislead, and it is resolved locally so that no third party learns the
addresses the person signs in from.

**Acceptance criteria**
1. `GET /account/sessions` returns every live session of the account and marks
   exactly one as current.
2. Each entry carries sign-in time, last-use time, device description and a location
   no finer than city; the location is resolved without a network call.
3. `DELETE /account/sessions/{id}` ends that session and its derived tokens within one
   request cycle and leaves the others intact; deleting the current session behaves as
   logout. An `{id}` naming no session of the account's, another account's included, is
   answered 404 `authz.resource.notfound`.
4. The location fields are unreadable after erasure and are gone when the session
   record is swept (AUTH-KEY-003).
5. No operation of the public contract takes a location as a parameter; a listed
   location is the local database's reading of the address the session was used from.
6. A session opened from an IPv6 address records the whole address.

---

### 4.4 Policy changes against live sessions

**AUTH-SESS-010** — Any transition of an account out of `active`, and any
deactivation, SHALL terminate **all sessions and derived tokens for that account** in
the same operation.

*Source: D-077, D-166*

Previously unstated. Suspension, deactivation and the transition to `deleting` said
nothing about sessions, so a suspended account retained a live session for up to its
absolute lifetime — up to a year for a customer (AUTH-SESS-005). The takedown procedure's claim that "access
stops immediately" was false.

**Acceptance criteria**
1. Suspending an account ends its sessions within one request cycle: the first request
   on any of them that reaches the session record after the commit is refused.
2. The minor takedown ends sessions **in the same transaction as suspension** — both
   are library work, so this part genuinely is atomic.
3. Deletion ends sessions before personal data is removed.
4. Restricting an account ends its sessions and derived tokens in the operation that
   restricts it.

---

**AUTH-SESS-011** — Per-account session revocation SHALL exist as an operation
distinct from system-wide revocation. It is
`POST /admin/accounts/{subject}/sessions/revoke`, the step-up action
`account:sessionsrevoke`, since it ends another person's sessions.

*Source: D-077, D-166*

**Acceptance criteria**
1. Revoking one account's sessions leaves every other session intact.
2. Offboarding uses the per-account operation.

---

**AUTH-SESS-009** — When an organization's authentication policy tightens, existing
sessions SHALL be re-evaluated on next use and **downgraded**, not terminated. A
downgraded session retains read access; any step-up-requiring action forces
re-authentication under the new policy.

Gaining a membership tightens the policy in force for that principal (AUTH-PRIN-002):
every live session of the account SHALL be downgraded in the operation that attaches
the membership, and a factor the organization's policy does not permit SHALL satisfy
no step-up (IDN-LIFE-009b, AUTH-STEP-002).

A separate explicit "revoke all sessions now" operation SHALL exist:
`POST /admin/sessions/revoke-all`, the step-up action `session:revokeall`.

Where the tightening raises `requiredAssurance` or `credentialRedundancy` and
`policy.enforcement.grace` is non-zero (AUTH-FACT-017), a downgraded session's
step-up during the grace SHALL be told the requirement and the deadline and SHALL be
offered enrolment; the expiry of the grace SHALL end no live session, and the hold
applies at the account's next sign-in.

*Source: D-026.5, D-146, D-166*

Terminating outright logs everyone out mid-work for a non-emergency, training users
to expect random logouts.

**Acceptance criteria**
1. Tightening policy does not immediately end active sessions.
2. A session no longer meeting policy is denied a step-up action and prompted to
   re-authenticate.
3. The explicit revocation operation terminates all sessions immediately.
4. The end of a grace period ends no session; the next sign-in of a non-compliant
   account is held at enrolment (AUTH-FACT-017).
5. A session the account held before a membership attaches keeps read access and passes
   no step-up gate until a factor the organization's policy permits is presented.

---

## 5. Step-up and assurance

**AUTH-STEP-001** — Step-up SHALL be required when a session first exercises a
permission for which the principal's policy (AUTH-PRIN-002) demands it.

*Source: D-020.1, D-116*

The anchor is the policy and the permission, never the application. An application
is a deployment artifact; the security property must not move when management is
split across two apps or folded into another.

**Acceptance criteria**
1. Exercising an administrative permission via an API, without opening any
   management interface, triggers step-up.
2. Splitting an application in two changes no step-up behaviour.
3. A second organization sets its own step-up policy independently.

---

**AUTH-STEP-002** — Every step-up gate SHALL be declared as **three values**: the
**assurance level required**, whether **phishing-resistance is required**, and the
**maximum age** of the evidence, set by the principal's policy per action
(AUTH-STEP-002a). A gate SHALL be evaluated against the session record
(AUTH-SESS-001) and the account's reachable assurance (AUTH-STEP-006) only; it SHALL
NOT read the account's list of enrolled factors, and no rule text SHALL name a factor.

*Source: D-148; D-020.2, D-067, D-086, D-125, D-141, D-146, D-166*

**Evaluation order:**
1. **Check what the session already proved.** If the attained level and
   phishing-resistance meet the gate and were earned within the maximum age,
   **require nothing**.
2. **Otherwise offer every combination of the account's usable factors** (state
   `active`, AUTH-RECOV-007, of a factor the policy in force permits in
   `loginFactors`, and not a restricted entry withheld because its number's signal
   answers `risk`, AUTH-FACT-002b AC6) whose contribution (AUTH-SESS-005a) lifts the
   session to the gate, and let the subject **choose among them**. A password alone
   reaches AAL1; a passkey alone reaches AAL2 phishing-resistant; password +
   non-discoverable WebAuthn credential (a security key as second factor) reaches AAL2
   phishing-resistant, because WebAuthn is phishing-resistant whether or not the
   credential is discoverable (AUTH-FACT-002, D-148); password + TOTP and
   password + recovery code reach AAL2 without phishing resistance; password + SMS
   code (`phoneCode`) reaches AAL2 and never
   phishing-resistance, so it satisfies a gate whose level is `aal2` and never one
   that requires phishing resistance. A second factor alone contributes nothing; a
   social credential, an email factor or a sign-in link contributes nothing
   (AUTH-STEP-005, AUTH-FACT-003). Presenting a combination writes exactly what it
   reached into the session record.
3. **If no usable combination can reach the gate**, the answer is one of three,
   never a bare refusal:
   - the account's reachable assurance (AUTH-STEP-006) is **below** the gate (the
     account has never held what the gate needs) → **enrol**, at that moment;
   - the reachable assurance meets the gate but a factor that would satisfy it is
     no longer in the subject's hands → **report a loss** (AUTH-RECOV-007);
   - a loss report is already pending → **say so**, with the completion time and
     the cancel option; the gate stays closed until invalidation completes.

A factor the policy in force does not permit in `loginFactors` is never offered, and
presenting it at step-up is refused with `auth.factor.notpermitted`, as at sign-in
(IDN-LIFE-009b).

A gate bound to a host's action (AUTHZ-GATE-005) is judged against the library's
session the request carries, where that session is the acting person's own; a
host-named gate that no policy states values for costs the strictest of the policy's
gates, field by field. Where no session of the library carries the request, the host's
assurance provider (LIB-HOST-004) reports the attained level, whether it was
phishing-resistant, when it was attained and the account's reachable assurance, and the
gate is judged from those.

**Why "reaches the level", not "strongest held".** A person who enrolled a second
factor did so to protect exactly these actions. That protection is expressed by the
gate demanding AAL2 on an account that can reach AAL2: a bare password then cannot
pass, whoever holds the live session. It is **not** expressed by reading the
enrolment list and demanding the strongest entry: that ties the ability to pass a
gate to the continued possession of whatever raised it, and a lost passkey then locks
its owner out of removing it. Whoever holds only a password loses nothing: their
account reaches AAL1, the gate asks for AAL1, they present the password.

**Standards grounding, stated precisely.** Gates as (level, phishing-resistance,
recency) are RFC 9470's `acr_values` + `max_age` and NIST SP 800-63B-4's expression
of reauthentication. The AAL1 gate satisfied by a password beside a live session is
NIST §2.2.3's allowance (a single factor in conjunction with the session secret)
applied to a sensitive action rather than an inactivity timeout; that application is
this design's own extension, recorded as a choice (D-125).

**Acceptance criteria**
1. A gate is expressed as a required level, a phishing-resistance requirement and a
   maximum age, and nothing else; no rule text names a factor.
2. Evaluating a gate reads the session record and the account's reachable
   assurance; it never enumerates enrolled factors.
3. A subject whose session meets the gate within the maximum age is not challenged.
4. On an account whose reachable assurance is AAL2, a bare password passes no gate;
   password + TOTP, password + recovery code, password + security key, or a passkey
   does, and the subject chooses among those they can present.
4a. Password + SMS code passes a gate declared `aal2` without phishing resistance
   and is not offered at a gate that requires phishing resistance.
4b. Password + non-discoverable WebAuthn credential, and a passkey alone, pass a gate
   declared `aal2` with phishing resistance required; password + TOTP and
   password + recovery code are not offered at such a gate.
5. A customer holding only a password passes every customer gate with the password.
6. Every combination that reaches the gate is offered; none that does not is.
7. A subject with no usable combination is told to enrol or to report a loss; a
   pending loss report is shown with its completion time; nothing is a bare refusal.
8. Registering a new factor type changes no gate.
9. A factor the policy's `loginFactors` does not permit is not offered at a gate, and
   presenting it is refused with `auth.factor.notpermitted`.

---

**AUTH-STEP-002a** — **Login policy and step-up policy SHALL be configured
independently.** Both resolve per AUTH-PRIN-002: the **system policy** for
principals holding no organization membership, the organization's policy —
inheriting the system policy — for members.

| Policy | Question it answers |
|---|---|
| Login | What may this person sign in with? |
| Step-up | What proves it is still them, now? |

*Source: D-067, D-086, D-141*

**The policy's `gates` field sets the three gate values (AUTH-STEP-002) per action;
its `loginFactors` field is the login policy** (`10` §4.1a, D-143). Defaults:

| Policy | Level required | Phishing-resistant | Maximum age |
|---|---|---|---|
| System policy (customers) | **the account's reachable assurance** (AUTH-STEP-006), floor AAL1 | No | `session.stepup.recency` (15 min) |
| Administrative organization (staff) | AAL2 | Yes | `session.stepup.recency` (15 min) |

"Reachable assurance" as the required level is NIST SP 800-63B-4 §4.1.2.1's
"whichever is lower": the gate asks for the most the account can do and never more.
A social-only account reaches `delegated`, below the AAL1 floor, so its first
protected action is an enrolment (AUTH-STEP-005 AC2). An organization MAY tighten a
value; it SHALL NOT loosen below the system defaults.

**Step-up policy applies identically regardless of how the subject signed in.**
Tangling the two is what produced the unreachable-step-up defect: customers inherited
the administrative organization's passkeys-only login rule and were left with no way to
satisfy step-up at all.

The earlier wording resolved policy "from the organization owning the resource";
read literally, that handed customers the administrative organization's rules by
accident, which is how the unreachable-step-up defect arose. D-116 replaced it with
membership-based resolution.

**Acceptance criteria**
1. A principal with no membership resolves to the system policy.
2. Changing the administrative organization's policy does not change customer
   behaviour.
3. The three gate values for every action are readable from the resolved policy;
   none is inferred from the factor catalogue.

---

**AUTH-STEP-006** — The library SHALL maintain, per account, its **reachable
assurance**: the highest assurance level, and whether phishing-resistant, that the
account can reach using only its authenticators in state `active` (AUTH-RECOV-007),
computed by the table in AUTH-SESS-005a. It SHALL be recomputed when an authenticator
is enrolled or **invalidated** — never when one is merely suspended.

*Source: D-141*

Password only → AAL1. Password + TOTP → AAL2. Password + SMS code → AAL2, not
phishing-resistant. Any passkey → AAL2 phishing-resistant.
Social credentials contribute `delegated`; email factors and sign-in links, by email
or by SMS, contribute nothing.
**Recovery codes contribute nothing** here: they stand in for a second factor at
sign-in and at a gate (AUTH-SESS-005a), but an account whose only second factor is a
dwindling set of single-use codes is not an AAL2 account, and treating it as one
would spend a code at every gate.

This is the account-side record that step-up (§5) and factor loss (§6) share. Gates
read it to know what to ask for; loss reports change it, slowly and with notice.

**Acceptance criteria**
1. Password-only → AAL1; password + TOTP → AAL2, not phishing-resistant; a passkey
   → AAL2, phishing-resistant; social-only → `delegated`.
2. Suspending an authenticator leaves the value unchanged; invalidating it
   recomputes it.
3. Generating or spending recovery codes never changes it.

---

**AUTH-STEP-007** — Enrolling a new authenticator SHALL be gated (AUTH-STEP-002) at
**the lower of** the account's reachable assurance and the new authenticator's own
contribution, and SHALL be **notified to every recorded channel** independently of
the enrolling session. Recovery (AUTH-RECOV-002, AUTH-RECOV-005) is the enrolment of
a password under this rule, with the recovery artefact standing in for the session;
it SHALL NOT change the state of any other authenticator.

*Source: NIST SP 800-63B-4 §4.1.2.1, D-141, D-166*

A password-only customer adds a passkey after a fresh password. A customer holding
password + TOTP adds a passkey with password + TOTP. A social-only customer sets a
password with no further gate — `delegated` is the lower value — and that password
then reaches AAL1 at once; the notification to every channel is the control. Under
the administrative policy the gate is AAL2 phishing-resistant, and the invitation or
re-enrolment link stands in for the first credential.

**Acceptance criteria**
1. Enrolment of any authenticator produces a notification on every recorded channel
   that is not the enrolling session.
2. A password-only customer enrols a passkey after presenting the password; a
   customer reaching AAL2 must present AAL2.
3. A recovered password never alters another authenticator's state.
4. Setting a password on an existing account, by the person, by recovery or by an
   invitation, raises `CredentialEnrolled` with the catalogue entry `password` and no
   credential identifier.

---

**AUTH-STEP-008** — The following invariants SHALL hold and SHALL be written as
tests; a spec change that breaks one is a defect in the change.

1. No requirement text in §5 or §6 names a factor.
2. No gate reads the enrolment list.
3. From every account state and for every gate, a path of at most two moves exists —
   satisfy; or enrol / report a loss, then satisfy.
4. Removing an authenticator is never gated on presenting that authenticator.
5. Reachable assurance (AUTH-STEP-006) never decreases without notification to at
   least two channels and a completed `recovery.invalidation.window`.
6. The session record never exceeds what was presented (AUTH-SESS-005a; trusted
   device, social and idle-restore write only what they proved).
7. Session lifetime reads exactly one anchor — the policy-required level
   (AUTH-SESS-005).

*Source: D-141*

---

**AUTH-STEP-004** — A **break-glass session SHALL satisfy the step-up requirement**
for the duration of its lifetime. It satisfies gates only: the step-up actions
OPS-BOOT-002 refuses from the break-glass session (giving the reserved account a
sign-in method among them) stay refused with `authz.denied`, whatever the reserved
account's policy lists.

*Source: D-065, D-166*

A printed single-use secret is not phishing-resistant, so under the ordinary rule a
break-glass session could perform none of the actions it exists for — approving a
recovery, granting system administration, changing alert destinations, loosening a
setting. This is a deliberate exception to the strongest control in the design, which
is what an emergency credential is.

**The compensating control is the alerting**, not a weaker exception. Use raises
maximum-noise alerts immediately (OPS-BOOT-002), and every action in the session is
audited.

**Acceptance criteria**
1. A break-glass session can approve a recovery, grant `system:administer`, and
   change alert destinations.
2. Every action in the session is audited as break-glass-originated, carrying the
   reason given with the credential at its use (OPS-BOOT-002).
3. The exception applies only for the session lifetime and does not persist.

---

**AUTH-STEP-005** — Social sign-in SHALL NOT satisfy step-up, even where it was the
subject's sign-in method.

*Source: D-086, D-147*

**Not a preference: the mechanism does not work in practice.** Forcing a fresh
authentication at a provider requires the `max_age` parameter, which obliges the
provider to re-authenticate and return an `auth_time` claim the relying party can
verify. Without it, redirecting the subject bounces them straight through on their
existing provider session and returns an assertion demonstrating that **nobody was
present**.

**The providers do not honour it.** Google supports only `none`, `consent` and
`select_account` for the prompt parameter, not `login`. Google's own OIDC
documentation does not document `max_age` at all; the claim that Google has not
implemented `max_age` correctly rests on secondary (third-party vendor)
documentation, not on a primary source (D-147). Either way the conclusion holds: a
relying party cannot rely on provider freshness.
`prompt=login` alone travels through the browser, is tamperable, and has no
spec-defined validation; published guidance is explicit that it must not be relied on
as a security guarantee.

**Authentication can be delegated; presence cannot.**

**Acceptance criteria**
1. A social credential is never offered as a step-up option.
2. A social-only subject reaching a step-up gate is prompted to enrol a password or
   passkey.
3. No step-up decision depends on a claim asserted by an external provider.

---

**AUTH-STEP-003** — Where authorization is consumed without authentication, step-up
checks SHALL fail closed. A consumer requiring step-up SHALL supply an assurance
provider.

*Source: D-041, R-17*

**Acceptance criteria**
1. With no assurance provider registered, a permission requiring step-up is denied.
2. The denial is distinguishable in diagnostics from an ordinary denial.

---

## 6. Recovery

### 6.1 Credential redundancy

**AUTH-RECOV-001** — Credential redundancy SHALL be adaptive. A second credential
SHALL be required only when the first is **device-bound** (`backupState` false,
AUTH-FACT-013); a **synced passkey alone satisfies** the requirement.

**Advisory** for public users (dismissible). **Enforced** for
administrative-organization members and wherever a policy's `credentialRedundancy`
requires it.

*Source: D-008, D-020.3, D-146*

The underlying fact is identical for anyone: a device-bound passkey on a lost device
is gone. The consequences differ: a locked-out customer recovers by setting a
password through email recovery (AUTH-RECOV-005), or through admin-assisted recovery
if the mailbox is gone too (AUTH-RECOV-002), then reports the lost passkey with that
password (AUTH-RECOV-007); a locked-out staff member has only admin-assisted
recovery.

**Acceptance criteria**
1. Enrolling a synced credential (`backupState` true) produces no second-credential
   prompt, under any policy.
2. Enrolling a device-bound credential prompts; a public user may dismiss it.
3. An administrative-organization member cannot complete enrolment with a single
   device-bound credential; the prompt reads `backupState`, not the authenticator's
   make.

---

### 6.2 Administrative recovery

**AUTH-RECOV-002** — Admin-assisted re-enrolment SHALL be available to **every
human account** (`emergency` has no channel and no enrollable factor, REG-IDENT-001), SHALL issue a time-boxed enrolment link following out-of-band identity
confirmation, require one approver (configurable upward), require a written reason
(API-CONV-002), notify the account owner on a channel separate from the recovery flow, and be subject
to rate limiting and anomaly detection **per account and per approver**.

**Values (D-153).** Rate limiting is `recovery.ratelimit.account` approvals per account
and `recovery.ratelimit.approver` approvals per approver, each counted over the day
before the moment of asking (sliding), refused with `auth.throttled` beyond; the
refusal carries `retryAt`, the instant the `limit`-th most recent counted approval is a
day old, the later of the two where both limits are reached, and a day from the moment
of asking where a limit is 0; the anomaly alerts fire at
`alerting.recovery.accountthreshold` and `alerting.recovery.approverthreshold`
(OPS-ALERT-001).

*Source: D-008, D-041, D-111, D-166*

**Where the link goes.** To a channel **already recorded on the account** and chosen by
the approver — for a customer whose mailbox is gone, the phone. Delivering the link by
SMS does not make the link an authentication factor (AUTH-FACT-002): the control is the
approver's confirmation on a recorded channel, and the link is the same time-boxed,
single-use artefact in every case.

**What the link permits.** A re-enrolment session in which the person sets a password
or passkey and, where the old mailbox is unreachable, **sets a new email address
confirmed by the new address only** — the approver's confirmation stands in for the
old one. The remaining security-notice set is still notified and receives the undo
(REG-IDENT-006, REG-IDENT-007), and all other sessions terminate (IDN-LIFE-008).

**Acceptance criteria**
1. The link expires and cannot be reused.
2. The reason is mandatory and recorded: an absent or blank reason is refused with
   `auth.recovery.reasonrequired`, and one over the free-text bound with
   `api.request.malformed` (API-CONV-002).
3. Approver count is configurable without a deploy.
4. Unusual approval frequency by one approver is surfaced without human monitoring.
5. A customer with an unreachable mailbox can regain access and move their account
   to a new address through this path, with the old address notified.
6. The link is never sent to a channel supplied in the request.

---

**AUTH-RECOV-002a** — An approver SHALL NOT approve recovery for their own account.
Enforced in the domain, with the error code `auth.recovery.selfapproval` (`10`
section 1.2).

*Source: D-079a, D-147*

The threat model claimed self-approval was blocked; no requirement said so. Moot while
there is one approver, false the day a second is configured — which is exactly when
nobody would think to check.

The `emergency` account (OPS-BOOT-002) may approve any recovery, including the sole
administrator's — it is never the account being recovered, and enabling exactly that
approval is its purpose (D-138).

**Acceptance criteria**
1. An approver's own subject is rejected with `auth.recovery.selfapproval`.
2. The rejection is enforced in the domain, not the interface.
3. A break-glass session approves the sole administrator's recovery successfully.

---

**AUTH-RECOV-003** — Out-of-band identity confirmation SHALL contact the person on a
channel **already recorded on the account** — never one supplied in the request — and
confirm details the account holds.

*Source: D-041, R-15*

The pre-existing channel is the control. An attacker-supplied channel proves nothing.

**Acceptance criteria**
1. The recovery interface presents the account's recorded channels and does not
   accept a channel entered by the requester.
2. The confirmation is recorded with which channel was used.

---

**AUTH-RECOV-004** — Email or SMS recovery SHALL NEVER be available to
administrative-organization members.

*Source: D-008*

Otherwise a staff account's real security equals its mailbox, silently undoing the
passkey policy.

**Acceptance criteria**
1. The recovery flow for an administrative-organization member offers no email or
   SMS route.

---

### 6.3 Public recovery

**AUTH-RECOV-005** — Recovery SHALL **set or reset the password** only. It SHALL NOT
remove an enrolled second factor.

**Values (D-166).** The recovery link a person asks for at `POST /recovery/begin` is of
the link kind `recovery` on the authentication application (AUTH-FACT-003 Values), sent
as the message kind `recovery-link` under the purpose `signin` (AUTH-ABUSE-004), and
consumed only by `POST /recovery/complete`.

*Source: D-009, D-111, D-166*

Otherwise every strong factor is defeatable by mailbox access. A customer whose only
primary factor was a device-bound passkey, now lost, recovers by **setting** a
password — the security level every customer already has, since email was always
their recovery channel.

Recovery is the enrolment of a password under AUTH-STEP-007. What it may do next is
*report* a lost factor from the recovered session (AUTH-RECOV-007) — suspended now,
gone after a notified window — never remove one outright.

**Acceptance criteria**
1. After password recovery, the second factor is still required to sign in.
2. An account with no password can set one through recovery; an enrolled passkey is
   not removed.
3. From the recovered session the person can report the lost passkey with the new
   password alone.
4. A recovery link is `<origin>/link#recovery.<token>` with the authentication
   application's landing origin; opening it changes nothing until a press presents its
   token, with the new password, to `POST /recovery/complete`.

---

**AUTH-RECOV-006** — Recovery codes SHALL **always** be generated when a second
step is enrolled beside a password, and shown **once** with copy, download and print
(AUTH-FACT-008). The person SHALL confirm they have saved them before the enrolment
completes. The set MAY be regenerated later (`POST /account/recoverycodes`,
AUTH-FACT-009). A passkey-only account SHALL receive none.

*Source: D-009, D-146; amends the earlier "encouraged, never blocking" position*

The earlier position held that a hard block reduces adoption. What changed is the
shape of the flow: the codes appear on the same screen as the enrolment, saving them is
one press, and the alternative is a person with a lost authenticator, no codes and only
the seven-day path of AUTH-RECOV-007. A passkey-only account has no second step for the
codes to stand in for (AUTH-FACT-002b).

**Acceptance criteria**
1. Enrolling TOTP, a second-factor security key or `phoneCode` beside a password
   generates a set and displays it before the enrolment can complete; the flow does
   not complete until the person confirms they have saved it.
2. Copy, download and print are each offered and each sets `exportedAt`
   (AUTH-FACT-008).
3. `POST /account/recoverycodes` regenerates the set under AUTH-FACT-009.
4. A passkey-only account holds no recovery code set and is offered none.

---

**AUTH-RECOV-007** — Every authenticator SHALL be in one of three states: `active`,
`suspended`, `invalidated`. **Reporting an authenticator lost** SHALL require one
usable factor **or** one unused recovery code presented in a live session — no
step-up — and SHALL move it to `suspended` at once. A suspended authenticator SHALL
NOT be accepted for sign-in or at a gate. Every recorded channel SHALL be notified
immediately and repeatedly, each notification carrying a cancel link; cancellation
from the link or from any session of the account returns it to `active`.
**Invalidation** completes after `recovery.invalidation.window` (default **7 days**,
configurable) and SHALL NOT complete if none of the notifications delivered. Only on
invalidation is the account's reachable assurance recomputed (AUTH-STEP-006).

**Values (D-153).** "Repeatedly" is once at the report, once every
`recovery.invalidation.noticeinterval`, and once 24 hours before invalidation. Each
notification is the message kind `credential-suspended`, carrying the cancel link
(D-166).

*Source: D-009, D-022, D-141, D-166*

**The waiting period is the control** — long enough that a real owner notices, short
enough that a locked-out customer does not give up. During it the account still
reaches its old assurance, so every gate that needed the lost factor stays closed to
**everyone**: to the owner, who is told for seven days what is happening and can
cancel with one click, and to whoever stole a mailbox and reset the password. Admin-
assisted recovery (AUTH-RECOV-002) remains the fast path when a human can confirm
the person.

**Reporting is not removal.** Removing an `active` authenticator the person still
holds is an ordinary gated action (`10` section 5a) that completes as soon as the gate is passed
— *unless* it would lower the account's reachable assurance, in which case it takes
this path: suspended now, invalidated after the window, notified throughout
(`auth.credential.lastsecondfactor`). Assurance never drops silently or fast.

**Recovery codes** are invalidated when the last second factor is invalidated; they
have nothing left to stand in for.

**Acceptance criteria**
1. Reporting a loss with only a password, or only one recovery code, succeeds and
   suspends the authenticator immediately; no step-up is demanded.
2. A suspended authenticator is rejected at sign-in and at every gate.
3. Invalidation does not complete before the window elapses; cancellation during the
   window returns the authenticator to `active`; with all notifications failing
   delivery, invalidation is held and flagged.
4. Reachable assurance is unchanged while suspended and recomputed on invalidation.
5. Removing an active authenticator that leaves reachable assurance unchanged
   completes immediately after the gate; one that would lower it is suspended and
   follows the window.
6. Invalidating the last second factor invalidates the recovery code set.
7. A customer whose only passkey is lost and who has recovered a password can report
   the passkey with that password, and after the window enrol a new one with it.

---

**AUTH-RECOV-007a** — When an invalidation lowers the account's reachable assurance
below AAL2, a password set under the with-MFA floor SHALL be marked below-floor and
a change required at next sign-in.

*Source: D-079a, D-141*

A ten-character password is permitted while the account reaches AAL2 and not
otherwise (AUTH-PASS-001a). Losing the second factor leaves a single-factor account
below its own floor.

**Acceptance criteria**
1. Invalidation re-evaluates the password against the single-factor floor.
2. A below-floor password forces a change at next sign-in rather than locking out.

---

**AUTH-RECOV-008** — Self-service loss reporting and removal (AUTH-RECOV-007) SHALL
NOT be available to administrative-organization members; they hold two credentials
(AUTH-RECOV-001) and otherwise use admin-assisted recovery (AUTH-RECOV-002).

*Source: D-009, D-141*

**Acceptance criteria**
1. The option is absent for such accounts.

---

## 7. Abuse controls

**AUTH-ABUSE-001** — Failed authentication SHALL be met with progressive delay per
account and per source, escalating with failures and decaying over time. Fixed-count
lockout SHALL NOT be used.

Independent limits on source address, account, and identifier. Applied to sign-in,
registration, recovery and step-up alike. A factor refused at step-up, including one
presented against a challenge that is unknown or not the asker's, SHALL be counted
against the source and against the session's account in the one account count sign-in
failures are counted in, and the delay SHALL be asked before the challenge is opened. A
verification refused in a registration session (a wrong email or phone code, or a link
token that opens nothing) SHALL be counted against the source and the identifier, and
each ask of a code or a link in a registration session SHALL ask the delay first.

The identifier component SHALL be keyed by the keyed hash, under the fingerprint key
(OPS-SEC-001), of the identifier's canonical form (an address, a number or a username
in its kind's form, anything else trimmed), computed for every identifier whether or
not an account holds it. A sign-in carries it with its key version from begin, and
every refused factor of that sign-in counts under it; every ask of a link, an email
code or a recovery is delayed under it.

**Values (D-166).** A **source** is the address the connection arrived on, after the
proxies the host names to the framework as trusted: an IPv4 address; an IPv4-mapped
IPv6 address, read as its IPv4 address; the /64 prefix of any other IPv6 address; or
the whole address of an IPv6 address whose first three bits are 000. A connection with
no address is the one source `unknown`. Every per-source count uses this source: this
item, AUTH-ABUSE-008, the restriction key `source` (AUTH-ABUSE-004), INT-GEN-003 and
BFF-ORDER-001 stage 4, which also counts each IPv6 source's enclosing /48 under
`abuse.source.sitelimit`. A session records the whole address (AUTH-SESS-013).

A failure SHALL be written as the count standing at that moment plus one, the standing
count being the count last written halved once per `abuse.throttle.decay` since it was
written and rounded to the nearest whole failure, half away from zero. The delay a
failure earns is the one the count it wrote earns under `abuse.throttle.threshold`,
`abuse.throttle.delay.initial`, `abuse.throttle.delay.factor` and the component's cap,
and it SHALL run from that failure: an attempt is looked at once the failure's instant
plus that delay has passed, and a refusal carries that instant as `retryAt`.

*Source: D-013, D-166*

Lockout is a denial-of-service weapon usable by anyone who knows a user's email, at
no cost to the attacker.

**The per-account component SHALL be capped**, and a source presenting a recognised
device or prior session SHALL be exempt from it. The exemption applies at sign-in; it
SHALL NOT apply at step-up. A successful sign-in or step-up SHALL clear the account
component only; the source and identifier components decay with time alone.

A recognised device or prior session is a remembered token read from its browser
cookie, or a trusted token read from its device cookie, that resolves to a device of
the account the identifier resolves to, of the cookie's kind, neither lapsed nor
revoked; resolving it changes nothing about the device, and it is resolved alike
whether or not the identifier resolves. Recognition exempts the browser from being held
by the account and identifier components, never by the source component; its failures
are counted against every component. Asks of a link, a code or a recovery are never
exempt.

A provider's return SHALL ask the source component's delay before the code is traded;
while a delay stands no provider is called, and the browser is returned to its
destination with `error=auth.throttled` and `retryAt` (AUTH-ABUSE-002).

*Source: D-079a, D-166*

Otherwise the per-account limit is a milder form of the denial-of-service the
fixed-count lockout was rejected for: an attacker who knows an address drives the
delay up from many sources, and the victim inherits it. The cap and the exemption keep
the control without handing over the lever.

**Acceptance criteria**
1. Repeated failures increase delay rather than disabling the account.
2. Delay decays with time.
3. A distributed attack across many addresses is caught by the per-account limit.
4. The per-account delay does not exceed its cap regardless of attempt volume.
5. A returning legitimate device is not held by an attack on that account.
6. The identifier component uses the same threshold, delays, decay and cap as the
   account component (`abuse.throttle.account.cap`); it has no keys of its own (D-153).
7. Sign-in and step-up failures raise one account delay; a correct factor presented
   at step-up inside it is refused unchecked with 429 `auth.throttled` carrying
   `retryAt`.
8. Failures made as each delay runs out escalate the delay; after a quiet spell the
   next failure earns less.
9. From a fresh source, an identifier no account holds earns the same delay for the
   same failures as one an account holds.
10. Failures from a recognised browser count towards the account's
    `auth-failures-sustained` condition (OPS-ALERT-002) and hold other sources, and do
    not hold that browser.
11. Wrong codes presented in a registration session are counted and held by the delay
    as refused factors at sign-in are.
12. A provider's return from a source under a delay calls no provider and returns the
    browser with `error=auth.throttled` and `retryAt`.
13. Two addresses within one IPv6 /64 share one source delay, and an IPv4-mapped
    address shares its IPv4 address's.

---

**AUTH-ABUSE-002** — The user SHALL be told they are throttled and for how long. The
message and the delay SHALL be **identical whether or not the account exists**. Where
a **send** is refused by a restriction (AUTH-ABUSE-004), the message SHALL state
**when the next send is possible** (`retryAt`) and **how to reach support**, and
SHALL be identical whether or not the address is registered.

*Source: D-013, D-146, D-166*

Silent failure produces retry storms; a differentiated message makes the throttle an
enumeration oracle. This item is one of the three exceptions to the content rule
(CONV-CONTENT-001): the identity of the two messages is the requirement.

**Acceptance criteria**
1. Throttling an existing account and a non-existent one produces identical
   responses and identical timing.
2. The remaining delay is communicated.
3. A refused send for a registered and for an unregistered address produces identical
   responses whose `details` carry `retryAt` and nothing else; the frontend shows the
   route to support (FE-API-005).

---

**AUTH-ABUSE-003** — No endpoint SHALL confirm account existence, including via
response timing. Where **recovery, a sign-in link request or an email OTP request** is attempted for an
address that does not exist, **that address SHALL be emailed to say so**.

**Registration is excluded.** For registration a non-existent address is the *normal*
case — as previously written, every legitimate new registrant would have been emailed
"you have no account here." Duplicate registration instead notifies the existing owner
(D-076) — by email for a duplicate address, by SMS **without a code** for a duplicate
phone number (REG-IDENT-001, REG-SESS-005, D-112).

*Source: D-013*

The real owner gets their answer; an enumerating attacker learns nothing, because
they do not control the mailbox.

**The non-existence email SHALL be limited to one per destination address per
window**, the window being `abuse.nonexistent.window` (`10` section 4.3; the same key
bounds the duplicate-owner notice of REG-SESS-005), and the rate SHALL be alerted on.
The non-existence email is judged under the purpose of the message that was asked for,
and a refusal of it by a restriction is the answer to the ask. An ask of recovery, a
sign-in link or an email code SHALL be answered before any transport is called, whether
its message is sent or not (AUTH-ABUSE-004).

*Source: D-148; D-079b; D-166*

Without a per-destination limit, a distributed attacker makes the mail server send
"you have no account here" to arbitrary addresses at scale — damaging the deliverability
of the security mail the design depends on. Existing limits are per source and per
identifier, neither of which constrains this.

**Acceptance criteria**
1. Responses for existing and non-existent addresses are byte-identical.
2. Response timing does not vary measurably with existence: verified by construction
   (one code path, fixed-time comparison, identical bytes), asserted by criterion 1, and
   named in the report as verified by construction (CONV-TEST-007, D-153).
3. The non-existence email is sent and its content does not name the requester.
4. A second non-existence email to the same address within
   `abuse.nonexistent.window` is suppressed.
5. More than `alerting.nonexistent.threshold` such sends system-wide in an hour raises
   the `nonexistent-notice-rate` alert (OPS-ALERT-001, D-121, D-153).
6. The "someone tried to register or change to this address — nothing has changed"
   notification points the recipient to sign-in and to recovery, so a forgetful
   returning customer is not left at a dead end (D-140).
7. With the mail transport blocked, an ask for an existing address and one for a
   non-existent address are both answered 202 without waiting for it; with a transport
   that refuses everything, both answer the same bytes.

---

**AUTH-ABUSE-004** — Every send of every kind (verification code, sign-in link,
second-step code, notice) SHALL be governed by **named restrictions**, except an alert
(OPS-ALERT-001), which is outside every restriction and governed by the deduplication
of OPS-ALERT-002 alone. A restriction SHALL have a **key** (`destination`: an HMAC of
the canonical address; `account`; `source`: the source of AUTH-ABUSE-001 of the request
that asked for the send; `global`; or `host:<name>` supplied by the host), an optional
**purpose** filter (`verification` · `signin` · `secondfactor` · `notification` ·
`any`), an optional **channel** filter (`sms` · `email` · `any`, default `any`), and
one or more **buckets** of (max, interval, `sliding` | `fixed`); its name follows the
rule of INT-SMS-003. A send no request asked for (a background job's) carries no
source, and no `source` restriction counts it. Every send SHALL evaluate every
applicable restriction; any exceeded bucket SHALL refuse with
`auth.restriction.exceeded` carrying `retryAt`, the earliest time a bucket lifts. A
**failed delivery SHALL NOT count**. Restrictions SHALL be **runtime configuration**
edited through `GET/PUT/DELETE /admin/restrictions/{name}` (step-up action
`restriction:edit`, audited, `SendingRestrictionChanged`); every edit SHALL carry a
reason (OPS-CFG-008, API-CONV-002), a loosening SHALL also require `system:administer`
in the administrative organization (OPS-CFG-002) and SHALL raise a Normal alert, and a
name the set does not hold is answered 404 `auth.restriction.notfound`. Support MAY
**grant** credit to a key (`POST /admin/restrictions/{name}/grant`, step-up action
`restriction:grant`, support role, audited with a reason (API-CONV-002),
`SendingRestrictionGranted`); a grant is credit, never a bypass. **Security notices to
an existing holder** SHALL be governed only by restrictions whose purpose is
`notification` (the shipped one is `notification.destination`); no restriction whose
purpose is `any` counts or refuses one, whatever its key. No link or code a person or an
administrator asked for carries the purpose `notification`: a recovery link and an
invitation link carry `signin`, as a sign-in link does (REG-IDENT-002, `10` section
5.15). `notification` and `notification.destination` therefore count security notices
and other notices only. The per-destination record
SHALL hold the HMAC, the fingerprint key version it was computed under (OPS-SEC-003)
and the send timestamps only, kept apart from the records of the other key kinds.
Before a send's counters are read, and on every run of the expiry sweep, every
destination record whose newest timestamp is older than the longest interval the
current destination restrictions declare SHALL be deleted.

**Sending order.** The restrictions and the gateway floor (AUTH-ABUSE-006) SHALL be
judged inside the transaction of the operation that undertakes the send, and a refusal
SHALL return before anything is written. An admitted send SHALL write its row to the
library's own outbox in that transaction. One immediate attempt SHALL run after the
outermost transaction commits, and SHALL be discarded if it rolls back; whatever that
attempt does not carry is the outbox publisher's, under `outbox.retry.*` (D-022,
INF-BG-001). No transport SHALL be called inside an open transaction, and an operation
that rolls back SHALL send nothing. A retried send SHALL be judged by the restrictions
as they stand when it is retried. A row SHALL be removed once every language it owes is
taken (IDN-PRIN-003); a send whose attempts are exhausted raises the `degradation`
condition. This holds on every send path: invitations, recovery, sign-in and notices.

**Every declared language.** A send resolved to every declared language (IDN-ATTR-001
step 3) SHALL be judged exactly, with no bucket overrun. By email it SHALL be one
message carrying every declared language, which the library composes from each
language's rendered template in the order of `notification.languages`, their subject
lines joined in the same order; it is judged and counted once. By SMS it SHALL be one
message per declared language, admitted only where every applicable bucket has room for
all of them (judged once, with the weight of their number), and each SHALL count. No
multilingual rule enters the catalogue: it holds one text per language.

**An ask that sends nothing.** An ask of a sign-in link, an email code or a recovery
that sends nothing, for any reason, SHALL be judged and counted against the
restrictions as its message would be, in the message's destination, kind, purpose and
language, and SHALL be refused by them alike. For an address no account holds, the
`account` key does not apply. The ask is answered before any transport is called
(AUTH-ABUSE-003).

*Source: D-146; replaces the fixed window of D-013, INT-SMS-002 and IDN-LIFE-011;
OPS-CFG-002; D-142 for the editing model; D-166*

Shipped defaults:

| Restriction | Key | Purpose | Channel | Buckets |
|---|---|---|---|---|
| `sms.destination` | `destination` | `any` | `sms` | 3 per 24 h, sliding |
| `sms.source` | `source` | `any` | `sms` | 10 per 1 h, sliding |
| `email.destination` | `destination` | `any` | `email` | 5 per 1 h, sliding; 1 per 60 s, fixed |
| `notification.destination` | `destination` | `notification` | `any` | 5 per 24 h, sliding |

The gateway account is prepaid, so looped sends drain money directly without any
registration completing; a fixed one-per-window rule stranded a person whose carrier
dropped the message and could not express a per-account or budget-shaped limit without
a redeploy. Restrictions are edited like the holiday list (D-142): the change applies
at once, is audited, and a loosening alerts. Security notices answer only to
restrictions whose purpose is `notification`, and alerts to none, because an attacker
who could exhaust an address's bucket, or any limit, would otherwise silence the notice
or the alert that tells someone something is wrong.

**Acceptance criteria**
1. A fourth SMS to one number inside 24 hours is refused with
   `auth.restriction.exceeded` and a `retryAt` equal to the earliest bucket lift; a
   second email to one address inside 60 seconds is refused likewise. A send in every
   declared language is judged once: by email it is one message and counts once; by
   SMS, with two declared languages and two of `sms.destination`'s three sends spent
   inside 24 hours, it is refused whole, and otherwise each language counts.
2. A send whose delivery report indicates failure does not count against any bucket.
3. `PUT /admin/restrictions/{name}` without `restriction:edit` step-up returns the
   step-up code; an edit without a reason is refused with
   `config.change.reasonrequired`, and a loosening by a caller without
   `system:administer` with `authz.denied`; a successful edit applies to the next send
   without a restart, is audited and emits `SendingRestrictionChanged`; raising a max,
   shortening an interval, removing a bucket, narrowing a channel or deleting a
   restriction raises a Normal alert. `GET` or `DELETE` of a name the set does not
   hold answers 404 `auth.restriction.notfound`.
4. A grant without a reason is refused with `config.change.reasonrequired`, and one
   naming a restriction the set does not hold with 404 `auth.restriction.notfound`; a
   grant adds credit to the named key and is audited and emitted as
   `SendingRestrictionGranted`; a granted key is still refused once the credit is spent.
5. A security notice to an existing holder is sent when `sms.destination` or
   `email.destination` is exhausted and is refused only by `notification.destination`.
6. The destination record contains an HMAC, the fingerprint key version it was
   computed under and timestamps, and nothing else, and is kept apart from the records
   of the other key kinds. Before a send's counters are read, and on every run of the
   expiry sweep, every destination record whose newest timestamp is older than the
   longest interval the current destination restrictions declare is deleted, so a
   shortened interval reaches sends already counted and no record outlives that
   interval without a further send.
7. A host-supplied `host:<name>` key is evaluated like the built-in keys.
8. A send undertaken inside an operation that then rolls back reaches no transport and
   leaves no row; one undertaken inside an operation that commits is carried after the
   commit, and no transport is called while its transaction is open.
9. A retried send is judged by the restrictions as they stand when it is retried.
10. A fourth mail to one address inside 24 hours is sent where only `sms.destination`
    would have counted it.
11. A security notice to an existing holder is neither counted nor refused by
    `sms.source`.
12. A send no request asked for is counted under no `source` key.
13. An alert is sent whatever the buckets of every restriction hold; only the
    deduplication of OPS-ALERT-002 limits it.
14. An ask of a sign-in link, an email code or a recovery that sends nothing is counted
    against the restrictions as its message would be, and is refused by them alike.
15. A recovery link and an invitation link are counted under the purpose `signin`, and
    neither is counted or refused by `notification.destination`.

---

**AUTH-ABUSE-005** — SMS message templates SHALL carry a per-language length budget,
tested, with Arabic binding.

**Values (D-153).** "Every configured language" is the set `notification.languages`.

**Values (D-166).** A text message that carries a link is budgeted at two segments of
its alphabet: 306 characters of the GSM 7-bit default alphabet, or 134 otherwise. Every
other text message is budgeted at one: 160 or 70. Each place the library fills is
measured at its defined width, and `{link}` at its composed width: the landing origin
declared for the link's application, `/link#`, the link's kind, `.` and a drawn token,
of the one token size every channel uses (INT-SMS-003). A text-message template naming a place with no defined
width is refused at startup with `model.startup.declarationinvalid`.

*Source: D-013, D-031, D-166*

Unicode messages are 70 characters for a single SMS and 67 per part concatenated;
Latin gets 160 and 153. An Arabic message one character over 70 costs double.

**The recipient's language is unknown until the recipient is resolved** (D-055), so
budgets **cannot** be validated per template in isolation. Every template is validated
in **every configured language** at startup — any of them might be the one sent.

**Acceptance criteria**
1. A test fails if any rendered template exceeds its budget: one segment of its
   alphabet, or two where it carries a link.
2. The test covers every template in every supported language.
3. Validation runs at startup, not at send, over the catalogue in force: the
   deployment's where it registered one, the shipped one otherwise. Startup refuses
   only a message with no text in a declared language, a text message over budget, or
   a text message naming a place with no defined width; declaring no catalogue is not a
   refusal.

---

**AUTH-ABUSE-006** — Gateway balance SHALL be polled, drain rate monitored with
alerting, and sends hard-stopped below a configured floor.

*Source: D-013*

**Acceptance criteria**
1. The balance is read every `abuse.sms.pollinterval`; spend in the last hour above
   `abuse.sms.drainfactor` times the trailing seven-day hourly mean, or a balance that
   would reach the floor within 24 hours at the current rate, raises the `sms-balance`
   alert without human monitoring (D-153).
2. Below the floor, sends are refused and the condition surfaced.

---

**AUTH-ABUSE-007** — The SMS delivery-report callback SHALL be treated as hostile
input: correlation references unguessable, endpoint rate-limited, and a callback
SHALL NEVER by itself advance a verification state.

**Values (D-153, D-166).** The callback endpoints accept `integration.callback.ratelimit`
requests per source (AUTH-ABUSE-001) per minute; a correlation reference is 128 random
bits, base64url, held only as its SHA-256 and looked up by it (INT-GEN-003,
BFF-MACH-003).

*Source: D-013, D-166*

The gateway calls over plain HTTP with parameters in the query string,
unauthenticated.

**Acceptance criteria**
1. A forged callback with a guessed reference is rejected.
2. A callback marking delivery does not itself mark a phone verified.

---

**AUTH-ABUSE-008** — Bot defence at registration SHALL be signal-driven, not
universal. An additional challenge SHALL appear only on adverse signals.

*Source: D-013*

Phone verification already imposes attacker cost; showing every customer a puzzle is
friction without proportionate benefit.

**Acceptance criteria**
1. An ordinary registration presents no challenge.
2. A registration from a datacenter range, or repeated attempts, presents one.
3. `repeatedAttempts` means more than `abuse.botdefence.repeatedattempts` registration
   sessions from one source in an hour; `datacenterRange` matches a bundled range file
   refreshed like the IP location database (INT-GEN-006) (D-153).
4. The challenge is the host's: a challenge verifier callback declared on the model
   builder (LIB-HOST-001) takes a token and answers pass or fail. When a signal fires
   and a verifier is declared, the step answers `auth.challenge.required` and completes
   only with a passing token; when none is declared the signal is audited and no
   challenge is shown. The library ships no challenge (D-153).

---

## 8. OIDC provider

**AUTH-OIDC-001** — The library SHALL act as an OpenID Connect provider scoped to
first-party, manually registered clients.

**In scope:** authorization code with PKCE, discovery document, JWKS, userinfo, a
manually managed client registry. Where a mail server is integrated, the registry SHALL
include a **first-party client for the mail server**, through which the library obtains
a token for the signed-in person to manage mail app passwords (REG-MAIL-002); the
library stores no app password.

**Out of scope:** consent screens, dynamic client registration, public client
self-service, developer portal, **token introspection**.

**Values (D-166).** A client is registered or changed from the server with the
`register-client` command of `Janus.Cli`, taking `--client <id>`, `--name <name>`,
`--kind protocol|browser-application`, `--redirect <address>` and
`--scopes "<scope> ..."`, which prints `{"registered":"<id>"}` and is audited as
`auth.oidc.clientregistered`; it takes no secret. The library generates each client's
secret when the client is first registered (32 random bytes, base64url), holds it
under the deployment's data key (AUTH-KEY-002) and rotates it with the signing keys
(AUTH-KEY-001, OPS-SEC-002); a registration that changes a client leaves its secret
alone. A running host takes a changed destination at its next start (API-REDIR-001
AC3).

*Source: D-005, D-041, D-146, D-166*

Introspection is excluded because Stalwart validates offline via JWKS and does not
use it; an endpoint nothing calls is attack surface for no benefit.

**Acceptance criteria**
1. The discovery document is served and is valid.
2. A client not in the registry cannot obtain a token.
3. No dynamic registration endpoint exists.
4. Where a mail server is integrated, its client is registered with the
   `register-client` command as the deployment is stood up and declared to the library
   as `MailServerClient` (LIB-HOST-001); its secret is generated by the library and
   presented by no party; the token the library issues to it in process for the
   signed-in person is scoped to that person and names that client as `aud`
   (AUTH-OIDC-006), and no app-password secret is persisted by the library.

---

**AUTH-OIDC-002** — The browser SHALL never receive a token. First-party browser
applications use the cookie session of AUTH-SESS-003; their BFFs use the
authorization code flow **once**, to establish that session (AUTH-SESS-012), and
store no token afterwards. A pushed authorization request (AUTH-OIDC-006) from a
`browser-application` client that asks for `offline_access` SHALL be refused at
`POST /oidc/par` with `invalid_request`; only a `protocol` client holds the refresh
grant (`09` section 9).

*Source: D-007, D-104, D-166*

**Acceptance criteria**
1. No browser application receives an access token.
2. No BFF holds an access, ID or refresh token beyond the code exchange.
3. A browser application's pushed request asking for `offline_access` is refused with
   `invalid_request` and no `request_uri` is issued.

---

**AUTH-OIDC-003** — Refresh tokens SHALL rotate on every use. Reuse of a consumed
refresh token SHALL revoke the entire session family. A refresh token's lifetime
SHALL be bounded by the session it derives from: it never outlives the record's idle or
absolute expiry, and no separate lifetime exists to set.

*Source: D-007, D-147*

How a stolen token is caught. A refresh token is a handle on the session record
(AUTH-SESS-001), not a credential of its own: it cannot outlive the record's idle or
absolute expiry, and revoking the record consumes it.

**Acceptance criteria**
1. A refresh token presented twice revokes all derived sessions.
2. The revocation is audited.
3. A refresh token presented after the session's idle or absolute expiry, or after the
   record is revoked, is refused; no refresh token is valid longer than the session.

---

**AUTH-OIDC-004** — Access tokens SHALL be minted from the session record, so
revoking the session invalidates them. Access tokens SHALL be issued with the
lifetime `oidc.accesstoken.lifetime` (`10` section 4.9; default 10 minutes, ceiling
1 hour).

*Source: D-007, D-147*

**The recorded trade-off (D-147).** A relying party that validates a token offline
against the published JWKS (the mail server, INT-MAIL-004) does not consult the
session record on each request, so a token it already holds stays valid until it
expires. For such a party the access-token lifetime *is* the revocation latency.
AUTH-SESS-001's "within one request cycle" means the first request that reaches the
session record after the revoking transaction commits is refused (D-153); it applies to
the session and to every validation that reaches the record, not to tokens already
issued and validated offline. The default of 10 minutes is the accepted latency; an operator who raises it
towards the ceiling accepts a longer one.

**Acceptance criteria**
1. Revoking the session causes subsequent token validation against the record to
   fail, and no new access token is minted from a revoked record.
2. No access token is issued with a lifetime longer than `oidc.accesstoken.lifetime`,
   and the key cannot be set above its ceiling.
3. A relying party validating offline rejects the token no later than its expiry; the
   documented revocation latency for that party equals the configured lifetime.

---

**AUTH-OIDC-006** — The provider SHALL conform to the OAuth 2.0 Security Best Current
Practice (RFC 9700) and to OAuth 2.1 semantics, and SHALL prove it: only the
authorization code grant with PKCE `S256` and the refresh grant exist; the implicit,
password and plain-PKCE forms are refused; every redirect is an exact registered match
(a pushed request naming another `redirect_uri` is refused with `invalid_request`, and
every registered return address is `https` except on a loopback IP literal); no public
client receives a refresh token. The implicit forms are every `response_type` but
`code`; the password form covers every grant but `authorization_code` and
`refresh_token`; a challenge that names no method is plain (RFC 7636 section 4.3) and is
refused with it, as is a request with no challenge. **Every authorization request SHALL
be a Pushed Authorization Request (RFC 9126):** the client posts the parameters to
`POST /oidc/par` over the back channel and the browser carries only the returned
`request_uri`; a direct `/oidc/authorize` with parameters is refused with
`invalid_request`. **Access tokens SHALL follow RFC 9068**: header `typ: at+jwt`, claims
`iss`, `exp`, `aud`, `sub`, `client_id`, `iat`, `jti`; `aud` is the identifier of the
client the token was issued to (the default resource indicator of RFC 9068 section 3),
and the mail server verifies `aud` as well as the signature (INT-MAIL-004). No error
the provider answers carries `error_description` or `error_uri` (LIB-API-003).

*Source: D-164, D-166*

Janus owns both ends of every flow, so a pushed request costs no interoperability and
removes the authorization parameters from the browser entirely rather than protecting
them one by one. RFC 9068 makes an access token unmistakable for an ID token and binds
it to its audience, which convention alone does not.

**Acceptance criteria**
1. The conformance suite of LIB-TEST-001 asserts, against the host's deployment, each
   refusal named above that a registered client can provoke without a signed-in
   person, the refusal of a mismatched destination at the push included; the
   library's own integration tests assert the same refusals, the exact-match rule, and
   that a code is exchanged only by naming its destination exactly.
2. `/oidc/authorize` without a `request_uri` from `/oidc/par` is refused; the
   `request_uri` expires 60 seconds after issue and is spent by the first answer
   `/oidc/authorize` gives it (a code, a refusal or a forward to sign in); presented
   again it is refused and forwards nowhere. A push whose `redirect_uri` is not exactly
   the client's registered destination is refused with `invalid_request` and issues no
   `request_uri`.
3. Every access token, on a code and on a refresh, carries `typ: at+jwt` and the seven
   claims, with `aud` the client it was issued to; a verifier configured as the mail
   server is (issuer and keys from discovery, type `at+jwt`, audience the mail server's
   client identifier, lifetime) takes the mail server's token and refuses one issued
   to any other client.
4. Discovery advertises `pushed_authorization_request_endpoint` and
   `require_pushed_authorization_requests: true`.

---

## 9. Keys and secrets

**AUTH-KEY-001** — Signing keys SHALL rotate on schedule with an overlap window: the
new key begins signing, the previous remains published in JWKS until outstanding
tokens expire, then is retired. No human step SHALL be involved. The cadence SHALL be
`token.signing.rotation` (`10` section 4.9; default 90 days); the overlap SHALL be the
access-token lifetime (`oidc.accesstoken.lifetime`) plus 5 minutes. The algorithm
SHALL be `token.signing.algorithm` (default ES256, protected). The secret of every
client in the registry rotates on the same cadence with the same overlap (AUTH-OIDC-001,
OPS-SEC-002).

*Source: D-148; D-007, D-147, D-166*

ES256 was verified against the mail server's source, which accepts P-256 keys from a
JWKS as ES256 and refuses symmetric keys: the file
`crates/directory/src/backend/oidc/lookup.rs` of the mail server's repository,
inspected 2026-09-18 (D-147, D-148). The overlap covers the longest
token any key may have signed, plus a margin for clock skew and JWKS caching.

**Acceptance criteria**
1. Rotation completes without restart or manual action, at the interval
   `token.signing.rotation`.
2. Tokens signed by the previous key validate throughout the overlap, which lasts
   `oidc.accesstoken.lifetime` plus 5 minutes from the moment the new key begins
   signing.
3. The previous key leaves JWKS after the overlap.
4. Every published key and every issued token uses `token.signing.algorithm`;
   changing the key takes effect only after a restart (protected).

---

**AUTH-KEY-002** — Secrets SHALL NOT reside in configuration files or environment
variables in production, except the two deployment-injected bootstrap values named
in INF-HOST-003. The key-encryption key and the fingerprint key SHALL come from a
secrets manager at startup; both are versioned, a set of versions with one current.
Every other secret the library holds (the signing keys, the registry's client secrets,
and any value that belongs to no subject) SHALL be encrypted at rest under the
deployment's data key, which the key-encryption key wraps as a row of the subject-key
table (PRIV-RIGHT-005a). The provider's authorization codes and refresh tokens are
encrypted under the key derived below, and a TOTP secret under its account's subject
key (AUTH-FACT-006).

**Values (D-166).** The library reads every secret it needs through the host's
`ISecretSource` (LIB-EXT-001), asynchronously, in its startup hosted service before the
server serves: the versions of the key-encryption key, the versions of the fingerprint
key, the maintenance credential (OPS-MIG-003a), the mail server's secret where a mail
server is integrated, and each social provider's credential by provider name. No
secret is an argument of `AddJanus`. A `Janus.Cli` command reads the same values from
one JSON document on standard input (OPS-SEC-001). A secret that cannot be read stops
startup with `model.startup.secretunavailable`, `details.key` naming the secret:
`keyEncryptionKeys`, `fingerprintKeys`, `maintenanceCredential`, `mailServerSecret` or
`socialProvider.<provider>`, and `input` where a `Janus.Cli` command cannot read its
document. Every version of the fingerprint key SHALL be at least 32 bytes; a shorter
one SHALL be refused, never padded.

The key the provider encrypts its authorization codes and refresh tokens under SHALL be
derived from each held version of the key-encryption key by HKDF-SHA256 with the info
string `identity:oidc:token-protection:v1`, the current version encrypting; no key of
its own SHALL be created or stored. A code or refresh token encrypted under a version
that has been retired (OPS-SEC-003) is refused.

*Source: D-026.3, D-105, D-166*

**Acceptance criteria**
1. No secret value appears in any configuration file in the repository.
2. Startup fails with `model.startup.secretunavailable`, `details.key` naming the
   secret, where a secret the deployment needs cannot be read, or where any version of
   the fingerprint key is shorter than 32 bytes; the server serves no request before
   every secret is read.
3. A refresh token issued before a rotation of the key-encryption key is honoured
   across restarts until the version it was encrypted under is retired.
4. No secret is taken as an argument of `AddJanus`, and no value that belongs to no
   subject is wrapped directly under the key-encryption key.

---

**AUTH-KEY-003** — Expired sessions, consumed refresh tokens, and used one-time
codes SHALL be swept by a background job.

*Source: D-007, D-166*

A consumed refresh token SHALL be kept until no session it could derive from can still
exist, which is the ceiling of `session.default.absolute`, so that a second
presentation is caught (AUTH-OIDC-003 AC1). Every other expired or consumed token and
code SHALL be swept once it is past its own expiry.

**Acceptance criteria**
1. No recurring human task is required for cleanup.

---

## 10. Open items

None. Cross-references: identity lifecycle in `01-identity`; permission semantics in
`03-authorization`; consent capture in `04-privacy`; SMS gateway and Stalwart
contracts in `05-integrations`; key storage and configuration taxonomy in
`06-operations`; the account's shape, registration and identifier verification in
`20-registration-and-account`.
