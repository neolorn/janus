# 13 — Risk Register

Risks identified during design, what was decided about each, and why.

**Prerequisite:** none. This document stands alone and is the one to hand a regulator
or an auditor alongside the records of processing.

**How this was built.** Extracted from `decision-log.md`. Every decision where a risk
was weighed and a position taken appears here. Nothing is invented — each row cites
the decision that created it.

**Scale.** Likelihood and impact are judgments, not measurements. They exist to sort
the list, not to produce a score.

---

## 1. Accepted — deliberate, with reasons

Risks knowingly carried. Each was raised, considered, and accepted.

### R-A01 · Host loss before the tier upgrade means permanent data loss

*Retired by D-225. Host loss and backups are the deployment's.*

---

### R-A02 · Staff mail is reachable without a phishing-resistant factor

| | |
|---|---|
| **Likelihood** | Medium |
| **Impact** | Not rated — see below |
| **Decision** | Accepted, no alternative exists |
| **Source** | D-006, INT-MAIL-005, D-146, REG-MAIL-002, REG-MAIL-003 |

Staff authenticate interactively with passkeys, but widely deployed mail clients
cannot perform OAuth bearer authentication with third-party providers. App passwords
are therefore required, and an app password is a long-lived bearer secret that
bypasses passkeys and MFA.

**This is the weakest credential in the system.** Not a design choice — the state of
mail clients.

**Impact is deliberately not rated.** A severity rating is only useful where it could
change a decision. There is no alternative here, so what matters is that the risk is
documented and the mitigations are maximal. Both are true.

**Mitigations in place:** app passwords are scoped to mail protocols, owned by the
mail server rather than the identity system, individually revocable, and never usable
for web sign-in or step-up.

**Would change if:** mail clients gain workable support for phishing-resistant
authentication with third-party providers. Outside our control.

**Management (D-146).** App passwords are created, listed and revoked from the account
application through the library's first-party OIDC client for the mail server
(REG-MAIL-002): creation and revocation are step-up actions, notified to the
security-notice set and audited; the secret is shown once and the library stores
nothing. An app password dies with the mailbox: when the membership ends the mailbox is
disabled and every app password with it (REG-MAIL-003). The credential is still the
weakest in the system, but it is now visible to its owner, revocable in one place and
bounded by the membership.

---

### R-A03 · Recovery approval rests on a single person

| | |
|---|---|
| **Likelihood** | Low |
| **Impact** | High — administrative account takeover |
| **Decision** | Accepted; configurable upward |
| **Source** | D-008, AUTH-RECOV-002 |

Admin-assisted re-enrolment requires one approver. Single-approver recovery is a
known account-takeover vector — it was proposed as two and declined.

**Compensating controls, added because of this:** out-of-band identity confirmation
uses only channels already recorded on the account; a written reason is mandatory;
the account owner is notified on a separate channel; anomaly detection runs per
approver as well as per account.

**Trigger to revisit:** a second person with recovery approval rights.

---

### R-A04 · Recovery time is 4–8 hours, not 2–3

*Retired by D-225. Recovery time is the deployment's.*

---

### R-A05 · Age is self-declared

| | |
|---|---|
| **Likelihood** | Medium |
| **Impact** | Medium — processing a minor's data without guardian consent |
| **Decision** | Accepted as proportionate |
| **Source** | D-027, D-039, D-146 |

A self-declared date of birth on a neutral age screen (REG-PROF-002) verifies nothing. Real verification would mean holding identity
documents for every customer — a larger data protection problem than the one it
solves.

**The regulator's standard is "proportionate," not "reliable."**

**Mitigations:** phone verification (required by default, `registration.phone`) already
filters most cases, as does any payment step the host runs; the takedown (`01`
IDN-LIFE-003) is available for any credible indication, and the procedure staff follow
is the host's (D-225).

---

### R-A07 · Staff step up once per recency window, not per action

| | |
|---|---|
| **Likelihood** | Low |
| **Impact** | Medium |
| **Decision** | Accepted for usability |
| **Source** | D-007 (superseded on the window), AUTH-STEP-001, AUTH-STEP-002, D-135 |

A strong factor presented within `session.stepup.recency` (15 minutes) satisfies
every step-up action in that window; after it, the next sensitive action
re-challenges. Neither "once per session" (D-007's original wording) nor per-action:
per fifteen-minute gap. Per-action step-up was considered and rejected as friction
without proportionate gain.

**Mitigation:** administrative sessions carry the 1-hour idle and 24-hour absolute
limits (AUTH-SESS-005); reauthentication never loses the page (FE-API-004).

---

### R-A08 · The destructive schema gate is disabled

*Retired by D-225. A deployment's gate on destructive schema changes is the
deployment's.*

---

### R-A11 · The escrowed key-encryption key can be copied without detection

*Retired by D-225. The custody of the escrowed key is the host's procedure.*

---

### R-A13 · An erasure can be silently reversed by a restore, until the tier upgrade

*Retired by D-225. A restore is the deployment's; the ledger that re-applies erasures
stays (DR-016).*

---

### R-A12 · One provider callback arrives over plain HTTP

*Retired by D-225. The library takes no delivery report from a gateway.*

---

### R-A09 · Reverse lookup over derived grants may become unbounded

| | |
|---|---|
| **Likelihood** | Low now, rising with scale |
| **Impact** | Medium — "who can access this?" becomes unanswerable |
| **Decision** | Accepted, with a recorded exit |
| **Source** | D-043, AUTHZ-DERIVE-007 |

Stored grants answer reverse lookup by query. Derived grants cannot — there is no row
to look up. This is the genuine ceiling of the in-library design.

**Mitigation ladder:** index, denormalise, cache the subject set, materialise.
**Exit:** migrate authorization out of the library.

---

### R-A16 · A compromised social-provider account bypasses the customer's enrolled second factor at sign-in

| | |
|---|---|
| **Likelihood** | Low |
| **Impact** | Medium — a session on a health-data account; bounded by step-up |
| **Decision** | Accepted — the federation model |
| **Source** | AUTH-FACT-002a, AUTH-STEP-005, D-128, D-141 |

Signing in with Google or Apple delegates authentication to that provider (NIST SP
800-63C); Janus does not re-run its own second factor afterwards, and cannot see the
provider's. A customer whose provider account is compromised is therefore signed in
without their authenticator. **Bounded:** social sign-in contributes nothing at a
gate, so every sensitive action — email or phone change, password set, export,
deletion — still requires a combination of the account's own factors reaching the
account's reachable assurance (AUTH-STEP-002, AUTH-STEP-006); on an account that
reaches AAL2, that is AAL2. The customer is told this once, when linking the
provider. Not available to staff.

**Minimum accepted assurance, stated (NIST SP 800-63C §2.5).** A public application's
minimum accepted AAL for a customer session is **`delegated`**, with no asserted AAL: a
social-only session is admitted to the customer's own account and records; sensitive
actions are gated by step-up, not by the sign-in's assurance. The administrative
organization's minimum is AAL2 (AUTH-SESS-005b) (D-140, D-141).

---

### R-A14 · A stolen trusted device skips the second factor for up to 30 days

| | |
|---|---|
| **Likelihood** | Low |
| **Impact** | Low — the password is still required, and the session it yields is AAL1 with step-up on every account change |
| **Decision** | Accepted — standard consumer practice |
| **Source** | AUTH-FACT-015, D-124, D-141 |

Without device trust, a customer with password + TOTP types a code at every sign-in,
which is the most common reason people turn MFA off; a customer with MFA off is worse
protected than one with MFA and a trusted device. The trust is bounded by its
lifetime, appears in the device list, and is cleared on password change, recovery
and "sign out everywhere". Not available to staff.

**No departure on password length (D-141, reversing D-134 on this point).** On a
trusted device the password is, in the standard's terms, used alone. Under
AUTH-PASS-001a a password below the 15-character floor exists only while it can
never complete a sign-in alone, so **the trusted-device offer is withheld on a
short-password account** (AUTH-FACT-015 AC7) and every password that a trusted
device lets through meets the single-factor floor. D-134 had accepted the
10-character exposure with compensating controls; D-141 removed it, because the
short-password rule is a property of the password, not a fact about enrolment, and
the exposure was the property leaking. **Three consecutive wrong passwords still
revoke the trust** (AUTH-FACT-015 AC6) — useful on its own, kept.

---

### R-A17 · The password blocklist rejects on the leaked list alone, a deviation from NIST SP 800-63B-4

| | |
|---|---|
| **Likelihood** | Medium (a password containing a dictionary or context word is accepted by default) |
| **Impact** | Low (the length floor and the leaked list catch the passwords that are guessed in practice) |
| **Decision** | Accepted as a recorded deviation; opt-in sources exist |
| **Source** | D-146, AUTH-PASS-004, AUTH-PASS-005 |

NIST SP 800-63B-4 section 3.1.1.2 lists dictionary words and context-specific words
(the service name, the person's own identifiers) in the blocklist as a SHALL. Janus
rejects a password for two reasons only: the length floor and a hit on the
leaked-password list. Dictionary and context words are advisory feedback by default
(AUTH-PASS-005) and become rejection sources only where a host adds `dictionary` or
`context` to `password.blocklist.sources`.

**Why accepted.** The profile fields a context check would use are collected after the
password, so a check at set time has little to work on; no comparable consumer product
rejects on either source; and a fifteen-character password that happens to contain a
dictionary word is not the failure the list exists to catch. Where `context` is on, a
profile field added later that matches the password is caught at the next sign-in with
a prompt to change, never a lockout.

**Trigger to revisit:** a host or a regulator requires conformance to the section as
written; the switch is configuration, not a redeploy.

---

### R-A18 · SMS is an authentication factor where a host enables it

| | |
|---|---|
| **Likelihood** | Low (off by default; a host must enable `phoneLink` or `phoneCode` by policy) |
| **Impact** | Medium (SIM swap or number porting yields a sign-in, or the second step beside a password) |
| **Decision** | Accepted as a restricted factor, off by default |
| **Source** | D-146, AUTH-FACT-002, AUTH-FACT-002b, AUTH-FACT-003, D-162, D-166, D-197 |

A sign-in link by SMS (`phoneLink`) and an SMS code as a second step (`phoneCode`)
exist in the factor catalogue and are off in `loginFactors` by default. A text reaches a
number, not a person: SIM swap, porting and interception are attacks on the carrier that
the system cannot see. NIST SP 800-63B-4 section 3.1.3.3 treats the PSTN as a restricted
channel, and so does Janus.

**Mitigations:** off by default; the limitation is shown at enrolment; `phoneCode` is
flagged less secure wherever it is listed; neither is ever phishing-resistant and
neither passes a gate that asks for phishing resistance; a link is AAL1 and never a
second step (AUTH-FACT-003); SIM-change and porting risk signals, where the deployment
supplies them, are considered before an SMS factor is used and before a recovery link is
sent by text (AUTH-FACT-002b), and their absence is recorded; on a `risk` answer nothing
is sent to the number: a sign-in offers the account's other factors, a step-up drops the
entry, and a request for a sign-in link or a recovery link by text is answered 202, as
every such request is, so no caller learns the carrier's signal about a number (D-162,
D-166); a `phoneCode` credential is bound to the number it was confirmed on, its
enrolment is refused on `risk`, and the number leaving the account takes it out of use
(AUTH-FACT-002, D-197); every send is governed by the restrictions of AUTH-ABUSE-004.

**Would change if:** the standard withdraws the channel entirely, or the host's
population loses the need for it.

---

### R-A19 · A failed domain-lock re-verification alerts and revokes nothing

| | |
|---|---|
| **Likelihood** | Low |
| **Impact** | Low (a domain whose DNS proof has lapsed keeps admitting its existing members until an administrator acts) |
| **Decision** | Accepted residual |
| **Source** | D-146, REG-DOM-001 |

Domain lock (`emailDomains`) admits member addresses only from domains verified by a
DNS TXT record and re-verified on a schedule. A record can disappear for many reasons
that have nothing to do with ownership: a DNS migration, a provider change, an expired
zone. Revoking memberships or ending sessions on a failed re-check would turn an
operational slip into a lockout of the whole organization.

**Position:** the failed re-check raises an alert (OPS-ALERT-001) and changes nothing
by itself; removing a domain is the administrator's act, stops new sign-ins with it and
alerts. The residual is the interval between the lapse and the administrator's
decision, during which nothing has actually changed about who holds the addresses.

---

### R-A20 · One governing language per legal document version; multi-market document sets deferred

| | |
|---|---|
| **Likelihood** | Not applicable until a second market is served |
| **Impact** | Medium (a second market whose law names another governing language could not be served on one deployment) |
| **Decision** | Deferred with a trigger |
| **Source** | D-146, PRIV-CONS-005 |

Every legal document version has exactly one governing language, defaulted from
`legal.governinglanguage` (required at install, protected, a restart to change).
Translations attach to a version and are optional; wherever a document is shown the
reader can view the governing text and any translation without changing the interface
language. For the default deployment the governing language is Arabic and the law is
Egyptian. Serving a second market brings that market's law alongside the Egyptian one,
and with it a document set whose versions may each need a different governing language
on one deployment. That is not built.

**Why deferred.** No law surveyed mandates several governing texts for one document; one
governing text per version with optional translations satisfies the market being served.
Building a per-market document set now would be speculation about a jurisdiction that
has not been named.

**Trigger to revisit:** serving a second market whose law names another governing
language.

---

### R-A21 · The sending-restriction record is a trace of addresses that may belong to no account

| | |
|---|---|
| **Likelihood** | Certain (the record exists whenever a send has happened) |
| **Impact** | Low (an HMAC, its key version and timestamps; no address, no account, no content) |
| **Decision** | Accepted; retention bounded by the restriction itself |
| **Source** | D-146, AUTH-ABUSE-004, PRIV-RET-005, D-162, D-166 |

Per-destination restrictions need to remember that a send happened to an address, and
the address may belong to nobody registered: a mistyped number, an attacker's target, a
registration that was abandoned. The record holds an HMAC of the canonical address under
a key the deployment holds, the version of that key, and the send timestamps, and nothing
else. It is deleted once the longest interval of the current destination restrictions has
passed since its newest send, when the next send's counters are read or at the next run
of the expiry sweep, whichever comes first. Its life is therefore at most that interval
(24 hours under the shipped defaults) plus one `sweep.interval`, whether or not another
send is made; a restriction on another key kind does not lengthen it.

**Why accepted.** Without the record, an attacker exhausts the host's prepaid gateway
against one number or one address at will; with it, the cost is a keyed pseudonym that
says nothing about a person and expires with the window it enforces. The processing
basis is security necessity.

---

### R-A22 · A number issued again within `records.proof.age` reaches its former holder's person records

| | |
|---|---|
| **Likelihood** | Low (a number given up, issued to another person inside the window, whose new holder asks that host for a code) |
| **Impact** | High for that person (what a host keeps about a guest, sensitive data included) |
| **Decision** | Accepted; bounded by `records.proof.age` |
| **Source** | D-221, REC-ACC-001, REC-ATT-001 |

A code proves who holds an email or a phone now, not who held it when a person record
was collected. A record whose contact was verified at a collection within
`records.proof.age` (90 days by default) is reached by the code alone; an older one asks
for a reference only its collector holds, an order number say, and the offer to attach a
record to an account follows the same rule (`21` REC-ACC-001, REC-ATT-001). A number
given up and issued to someone else inside the window therefore opens its former
holder's recent records to the new holder.

**Why accepted.** Self-service rights without an account need a proof a guest holds, and
a code to the contact they gave is the one they always hold; asking a reference for
every record would turn every access into a support case for the person who kept no
order number. The window keeps the exposure to records collected recently; an operator
whose customers' numbers change hands faster, or who keeps highly sensitive data under
person records, shortens it.

---

## 2. Mitigated

Risks addressed by design rather than accepted.

| # | Risk | Mitigation | Source |
|---|---|---|---|
| R-M01 | Account enumeration | Identical responses and timing; non-existent addresses are emailed | AUTH-ABUSE-003 |
| R-M02 | SMS toll fraud draining prepaid balance | Named sending restrictions per destination and per source (shipped: 3 per number per 24 h, 1 per number per 3 minutes, 10 per source per hour); a failed delivery does not count; the gateway's balance is the host's messaging's (D-225) | D-013, D-146, AUTH-ABUSE-004, D-197 |
| R-M03 | Session fixation | New identifier on authentication, step-up, and privilege change | AUTH-SESS-006 |
| R-M04 | Token theft via injected script | Opaque cookie, `httpOnly`; no token in browser storage | AUTH-SESS-003 |
| R-M05 | Cross-site request forgery | Token enforced in the BFF layer; not per-endpoint | AUTH-SESS-007 |
| R-M06 | Account takeover via identifier change | Adding an identifier is step-up and the new one is verified; removal is step-up and immediate, the undo goes to the remaining security-notice set and never to the removed address; other sessions terminated, and every session, the one that made the change included, at the undo | D-035, D-146, REG-IDENT-004, REG-IDENT-006, D-190 |
| R-M07 | MFA defeated through recovery | Recovery restores the password only; removal is delayed and notified | D-009 |
| R-M08 | Credential stuffing | Progressive delay per account and per source; blocklist screening; new-device check on single-factor accounts (a code to the primary email from an unrecognised browser) | D-011, D-013, D-146, AUTH-FACT-016 |
| R-M09 | Cloned authenticator | Signature counter verified where provided | AUTH-FACT-014 |
| R-M10 | Lookalike identifiers defeating human identity checks | Unicode normalization; mixed-script rejection | D-040 |
| R-M11 | Forged provider callbacks | Signature verification where supported; verification-against-provider where not; an unsigned callback never advances an authoritative state by itself, and a verified, signed provider security event acts only as IDN-LIFE-012a states | INT-GEN-003, IDN-LIFE-012a, D-070, D-166, D-197 |
| R-M12 | Unencrypted personal data to a processor | TLS enforced at startup; plaintext endpoints rejected | INT-GEN-001 |
| R-M13 | Permission drift between check and list | One rule, two renderings, asserted equal in tests | AUTHZ-PRIN-001 |
| R-M14 | Admin console as a privilege escalation path | Protected-settings list unreachable from the application; direction-based friction | D-010, D-071 |
| R-M15 | Stale permissions after revocation | Version counter bumped in the same transaction | AUTHZ-CACHE-001 |
| R-M16 | Offboarded staff still reading mail | The state each mailbox is owed follows its holder's account state and membership, save that a mailbox replaced under `replace` or released with its revoked invitation is owed `removed`, and is pushed under a stable idempotency key; a push that fails is begun again each day while its state is owed, and each failing run raises its alert again; daily reconciliation flagging drift | INT-MAIL-006, INT-MAIL-007, D-166, D-177, D-178 |
| R-M17 | Sensitive data disclosed in logs | Bodies of host-marked sensitive endpoints never logged; analyzer-enforced | CONV-LOG-003, BFF-LOG-002 |
| R-M18 | Fail-open from a defensive catch block | Analyzer rule; no catch returns a permitted outcome | CONV-ERR-002 |
| R-M19 | Insider bulk exfiltration | Per-actor baselined volume alerting; exports enumerated, gated, audited, rate-limited | OPS-ALERT-005, OPS-ALERT-006 |
| R-M20 | Compromised dependency | Lockfile pinning; automated vulnerability alerting; deliberate additions; the runtime's servicing release for what the shared framework serves, which no lockfile or alert sees; what brings that release is a known issue carried into Milestone 2 | D-046, CONV-DEP-004, D-205 |
| R-M21 | Operator machine compromise yielding production credentials | All credentials in a vault behind a security key; none in files | D-047 |
| R-M22 | Attacks proceeding undetected | Alert conditions with dedup, each handed to the host's alert handler, whose delivery keeps what OPS-ALERT-003 lists; alerts are outside every sending restriction and answer to the deduplication of OPS-ALERT-002 alone, so exhausting a limit cannot silence one | OPS-ALERT-001 to OPS-ALERT-003, AUTH-ABUSE-004, D-166, D-225 |
| R-M23 | Access surviving a staff departure | Ending the membership, which can be set in advance for the last day, revokes the person's grants in the organization, and for the administrative organization in every organization they are not a member of; the offboarding procedure is the host's | D-050, IDN-MEM-001, D-166, D-213, D-225 |
| R-M24 | Shared logins destroying attribution | Prohibited; passkeys make it structurally hard; implausible concurrent sessions alert | OPS-ALERT-007 |
| R-M25 | Registration pre-hijack: an attacker starts a registration with the victim's address and the victim's click verifies it | A verification link completes only in the browser that started the flow and only on a press; opened anywhere else it shows the code and a control that ends the session; a plain open changes nothing; the same rule governs adding an identifier and sign-in links | D-146, REG-SESS-003 |
| R-M26 | Draining an address's sending allowance to silence its owner | Security notices to an existing holder answer only to restrictions whose purpose is `notification` (shipped: `notification.destination`); no restriction whose purpose is `any` counts or refuses one, whatever its key, so neither a destination's nor a source's allowance can be drained to silence them | D-146, AUTH-ABUSE-004, D-166 |
| R-M27 | A former holder's mail read by the next holder of a corporate address | An invitation of a corporate address whose mailbox anyone has held before, its last holder included, is refused (`identity.invitation.mailboxheld`) unless it names `formerMailbox`: `transfer` gives the invitee the mailbox and its mail, `replace` removes the old mailbox and reserves a new one; either is stepped up with the invitation, carries a reason and is audited. The refusal does not depend on who is invited, so issuing an invitation tells nothing about accounts and the choice always rests with the administrator. The mail server adapter adopts an existing server account only where it carries the identifier of the mailbox being pushed; otherwise the push is answered `integration.mailserver.conflict` and marked failed at once, and `degradation` is raised, scoped `mailbox.conflict:<mailbox id>` and naming the mailbox identifier, and the push is begun again each day while its state is owed, so a conflict resolved at the mail server clears without action in the library; under `replace` the new mailbox's push waits until the old mailbox's removal is confirmed. An erased holder's address is not recognised at issue, since nothing left may match it; their account at the mail server is never adopted, so the next holder's mailbox is created only after the operator erases that account there | D-166, D-177, D-178, REG-MAIL-003, INT-MAIL-006, INT-MAIL-007 |
| R-M28 | A link's token read from a server log, a proxy or the `Referer` header | Every link the library sends carries its token in the address fragment (`<origin>/link#<kind>.<token>`), which a browser never sends to a server; the landing page removes it from the address bar once read and is served with `Referrer-Policy: no-referrer` | D-166, FE-VER-001 |
| R-M29 | Per-source limits evaded by rotating IPv6 addresses | Every per-source count takes an IPv4 address, or an IPv6 address by its /64 prefix (an IPv4-mapped address as its IPv4 address), as one source; the edge also counts each IPv6 /48 (`abuse.source.sitelimit`), so walking a site's /64s is bounded | D-166, AUTH-ABUSE-001, BFF-ORDER-001 |

---

## 3. Operational dependencies

Not security risks, but single points of failure worth naming.

### R-O01 · SMS balance exhaustion halts registration

*Retired by D-225. Message delivery and its balance are the host's.*

---

### R-O02 · One person holds all technical knowledge

*Retired by D-225. Who holds a company's knowledge is the company's.*

---

### R-O04 · The secrets manager is a boot dependency

Every secret the library needs is read through the host's secret source
(`ISecretSource`, LIB-EXT-001) from the off-host secrets manager at startup, before the
server serves: the key-encryption keys, the fingerprint keys, the maintenance
credential, the mail server's secret where the shipped mail-server adapter is used, and
each social provider's credentials. Startup fails closed without any of them
(`model.startup.secretunavailable`, INF-HOST-003, D-166). A restart while the secrets
manager is unreachable therefore keeps every application down until it returns.

**Accepted.** The alternative — caching the KEK on the host to survive an outage —
would put the master key on the disk the design keeps it off. A running application
is unaffected; only a restart during the outage is.

**Mitigation:** the secrets manager's availability, and any copy of the keys kept for a
prolonged outage, are the deployment's (D-225).

*Source: D-105, D-166, D-176, D-225*

---

### R-O03 · Regulatory deadline

*Retired by D-225. A company's regulatory deadline is the company's.*

---

## 4. Review

**RISK-001** — This register SHALL be reviewed when any accepted risk's trigger fires
(D-225).

**Triggers currently recorded:**

| Risk | Fires when |
|---|---|
| R-A03 | A second person holds recovery approval |
| R-A09 | Optimisation ladder exhausted; or reverse lookup for the administrative view exceeds `authz.reverselookup.budget` (2 s) at production volume |
| R-A17 | A host or regulator requires the NIST blocklist sources as written |
| R-A20 | A second market whose law names another governing language is served |
| R-A22 | A person reports that a record reached by a code was another person's, or a host keeps under person records data whose exposure to a number's next holder it judges too grave for the window |

---

## 5. What is not here

Risks belonging to a threat model rather than a register: who would attack, what they
want, and how. That exercise is `15-threat-model.md`, which found five gaps this
register did not contain (insider exfiltration, dependency supply chain, the
operator's machine, authentication anomaly monitoring, and shared logins, D-051) and
one host-owned gap it handed to the host (D-049). Those become register entries once
decided.
