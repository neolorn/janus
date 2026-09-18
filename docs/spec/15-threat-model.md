# 15 — Threat Model

Who would attack this system, what they want, how they would try, and whether the
design stops them.

**Prerequisite:** none. Read alongside `13-risk-register.md` — the register records
positions taken during design; this asks systematically what an adversary would do.

**Status: complete.** All §6 gaps closed. Two commercial-priority questions are recorded as **answered "unknown"** — that is the answer, not an outstanding item. Everything not marked is
derived from the specification or from stated facts about the business.

---

## 1. What is being defended

| Asset | Why it matters |
|---|---|
| **Customer order history** | Health data. An insulin needle purchase discloses a diagnosis |
| **Customer contact data** | Names, phones, home addresses |
| **B2B customer list and pricing** | Judged the most commercially valuable asset here — see §3.1 |
| **Supplier relationship data** | Exclusive KDL importer status is the business's core advantage |
| **Staff credentials** | Access to everything above |
| **System availability** | Downtime blocks orders; confirmed, with the nuance in §7 |
| **Payment references** | Low value alone; card data is never held |
| **Registration session store** | Staged identifiers, verification state, a password hash and enrolled authenticators for accounts that do not yet exist; bound to one browser, swept at `registration.session.lifetime` (REG-SESS-001) |
| **Restriction records** | An HMAC of a destination address and send timestamps, for addresses that may belong to no account; deleted when the buckets empty (AUTH-ABUSE-004, R-A21) |
| **Session location** | A city-level location per live session, resolved from a local IP database and kept only with the session record (AUTH-SESS-013) |
| **Preference store** | Host-declared typed values under the subject key; the library never reads their meaning (REG-PREF-001) |

---

## 2. What is not being defended

Stated so the boundary is deliberate.

- **The courier knowing a medical company shipped something.** Unavoidable, accepted
  (R-A06).
- **Nation-state adversaries.** Out of scope. Nothing here is designed to resist one.
- **Physical access to the hosting provider.** Delegated to the provider.
- **A compromised customer device.** Outside the boundary.

---

## 3. Threat actors

### 3.1 Competitors — *ranked first on reasoning, not on confirmation*

**Want:** the B2B customer list, pricing, stock levels, supplier terms.

**Why they rank first:** the company is the exclusive Egyptian importer for KDL with
dominant share in its products. The customer list *is* the market position. A
competitor who learns which clinics and pharmacies buy what, at what price, can
target them directly.

**How they would try:**

| Method | Covered? |
|---|---|
| Register as a customer, browse the storefront | Partial — prices are public to customers by design |
| Compromise a staff account | Covered — passkeys for interactive access |
| Social-engineer recovery on a staff account | **Weakest point** — one approver (R-A03) |
| Bribe or recruit a staff member | **Not covered** — see §6.1 |
| Scrape B2B data through the management app | Covered if permissions are correctly scoped |

**Assessment.** The technical paths are reasonably closed. The human paths are not,
and are the more likely route.

---

### 3.2 Someone targeting a specific person

**Want:** to learn whether a named individual buys insulin needles — and therefore
has diabetes.

**Who:** an employer, an insurer, a family member in a dispute, a blackmailer.

**Why this matters more than volume theft:** it needs no breach. It needs one
successful lookup.

**How they would try:**

| Method | Covered? |
|---|---|
| Guess an order URL and observe the response | Covered — concealment returns not-found, timing-identical |
| Probe registration to confirm the person is a customer | Covered — enumeration resistance |
| Recover the person's account via email | **Partially covered.** Recovery restores the password only and never removes MFA — but MFA is advisory for customers, so for a customer without it, a mailbox compromise yields the order history directly. Where MFA is enrolled, the recovered password can only *report* the second factor lost (AUTH-RECOV-007): the account keeps AAL2 for seven days, every gate stays closed, and the owner is warned on every channel — the phone included — with a one-click cancel (D-141) |
| Compromise the person's mailbox | Partial — email is the recovery channel by design |
| Swap or port the person's SIM, where the host has enabled `phoneLink` or `phoneCode` | Partial. Both are off by default and flagged as restricted factors (AUTH-FACT-002b): a link is AAL1 and never a second step, an SMS code is never phishing-resistant and never passes a gate that asks for it, and SIM-change and porting signals are evaluated before a send where the gateway supplies them. The carrier-side attack itself is outside the boundary (R-A18) |
| Observe a delivery | Not covered — physical, outside the boundary |

**Assessment.** This is the threat the concealment and enumeration rules were written
for, and they hold. AUTHZ-CONCEAL-002's timing requirement is load-bearing here, not
pedantry.

---

### 3.3 Fraudsters

**Want:** goods without paying, or resale value.

**How they would try:**

| Method | Covered? |
|---|---|
| Bulk fake accounts | Covered — mandatory phone verification |
| SMS pumping to drain prepaid balance | Covered — named restrictions per destination and per source (AUTH-ABUSE-004), failed deliveries not counted, balance floor |
| **Cash-on-delivery abuse** — order, refuse delivery, cost absorbed | Out of scope — owned by the order system (D-049) |
| Stolen card | Delegated to the payment provider |
| Account takeover of a real customer | Covered — the authentication controls |

---

### 3.4 Opportunistic attackers

**Want:** anything. Not targeting this business specifically.

| Method | Covered? |
|---|---|
| Credential stuffing | Covered — blocklist screening, progressive throttling, and the new-device check on single-factor accounts: a leaked password from an unrecognised browser gets no session until a code sent to the primary email is entered (AUTH-FACT-016) |
| Automated vulnerability scanning | Partial — depends on patching discipline, not design |
| Ransomware on the host | **Not covered in Phase 1** — backups are on the same host (R-A01) |
| Exposed secrets in the repository | Covered — pipeline secret scanning |
| Compromised dependency | Partial — D-046 |

**Ransomware deserves emphasis.** It specifically targets attached backups. During
Phase 1 the backups are on the same host as the database, so a single compromise
takes both.

---

### 3.5 Insiders

**Want:** varies — commercial gain, curiosity, grievance.

**Confirmed:** staff are few but **not all personally known**, with **periodic
turnover**. This is a materially higher insider risk than a stable, known team.

| Method | Covered? |
|---|---|
| Staff browsing customer health data out of curiosity | Partial — permissions limit reach; volume alerting detects patterns (D-045) |
| Exporting the customer list before leaving | Covered — exports gated and audited (D-045) |
| Approving their own recovery | Covered — self-approval blocked; but see R-A03 |
| **Access surviving departure** | Covered — offboarding procedure (D-050) |
| **Shared logins destroying attribution** | Covered — prohibited and detected (D-051) |

---

### 3.6 The operator

Not an accusation. A structural observation the model would be dishonest to omit.

The developer is the sole system administrator, has full production access, and
operates without oversight. Every control in this specification is enforced *by code
they wrote and deploy*.

| Reality | Mitigation |
|---|---|
| Can change any control | None technical. Break-glass custody sits with the owner (D-029) so the company is not locked out |
| Can read all customer data | None technical |
| Actions are audited | Audit is append-only at the database level, so tampering is not trivial — but the operator holds the credentials |

**This is inherent to a one-person technical function.** It is resolved by a second
person, not by a control. Worth stating so the owner understands what trust is being
placed and where. The one technical answer recorded for the audit row,
hash-chained tamper-evident audit records, is deferred with a reactivation trigger
(D-071; `00` section 7.3): the operator holds the credentials, so a chain the
operator can recompute buys little until a second person holds the anchor.

---

### 3.7 Supply chain

| Vector | Covered? |
|---|---|
| Compromised NuGet dependency | Partial — D-046 covers known advisories |
| Compromised GitHub account | **Partial** — a compromised account can deploy to production |
| Compromised provider (payment, shipping, SMS, mail) | Partial — minimisation limits what each holds |
| Malicious provider callback | Covered — INT-GEN-003 |

**The pipeline is a path to production that bypasses every application control.**
Someone who controls the repository controls the system. Currently protected by one
person's account credentials.

---

### 3.8 Registration and account takeover through identifiers

Added with D-146, when the account gained several identifiers per kind and registration
became a session. The attacker here wants an identifier attached to an account they
control, or the owner's identifier detached from the owner.

| Method | Covered? |
|---|---|
| **Registration pre-hijack**: the attacker starts a registration with the victim's address; the victim receives a genuine verification message and clicks the link | Covered (REG-SESS-003). The link completes only in the browser that started the registration and only on a press. Opened anywhere else, the landing page shows the code to type and a control that ends the registration session at once (`POST /register/abandon`); a plain open, including a mail scanner's prefetch, changes nothing. The victim's click therefore verifies nothing for the attacker, and the session-ending control destroys the attacker's session |
| Registering with an address that already belongs to an account, to learn that it does | Covered (REG-SESS-005). The response is identical to the fresh case, no code is sent to the address and its owner is notified without a link or code |
| **Removed-address undo**: an attacker who holds a compromised mailbox waits for the owner to remove it, then uses an undo to re-attach it | Covered (REG-IDENT-006). The undo goes to the remaining members of the security-notice set and never to the removed address, which receives a notice with no link and no powers and behaves as unknown at every recovery path afterwards |
| **Stolen-session identifier removal**: a stolen session removes the owner's address and adds the attacker's | Covered (REG-IDENT-004, REG-IDENT-006). Both are step-up actions; removal is immediate and every other session ends, but the owner's remaining addresses hold a one-click undo for `identifier.change.coolingoff`, and the addition is notified to the whole security-notice set. In single-address mode with no other channel at all, the old address confirms before a swap (REG-IDENT-007) |
| **Restriction bypass to silence the owner**: the attacker drains a number's or an address's sending allowance so that the notice of a hostile change cannot be delivered | Covered (AUTH-ABUSE-004). Security notices to an existing holder are outside destination restrictions and governed by `notification.destination` only; exhausting `sms.destination` or `email.destination` refuses further codes and links, not the notice |
| Provider address collision: signing in with a provider whose email is already on another account | Covered (REG-IDENT-008). The provider identity stays a credential matched by `sub`; the address is never a key; the owner is notified and no code is sent |
| SIM swap against `phoneLink` or `phoneCode` | Partial, see section 3.2 and R-A18: off by default, restricted, never phishing-resistant, risk signals where available |

**Assessment.** The browser binding of REG-SESS-003 is the load-bearing control: without
it every verification link is an invitation the attacker can send on the victim's behalf.
The direction of the undo is the second: an undo that reached the removed address would
let a compromised mailbox veto its own removal.

---

## 4. Attack surfaces

| Surface | Exposure | Primary control |
|---|---|---|
| Storefront | Public internet | Authentication, throttling, enumeration resistance |
| Management app | Public internet, staff only | Passkeys, step-up, organization policy |
| Auth endpoints | Public | Throttling, uniform responses, CSRF |
| Registration session and link landings | Public | Registration session bound to the pre-authentication cookie; links complete only in the originating browser on a press (REG-SESS-003); server-sent events on the session cookie with no token in any URL (`17`) |
| Send paths (SMS, email) | Public, prepaid | Named restrictions per destination, source and account; security notices outside destination restrictions (AUTH-ABUSE-004) |
| Provider callbacks | Public, weakly authenticated | Unguessable references, rate limits, never advance state alone |
| Mail (IMAP/SMTP) | Public | App passwords — **weakest credential** (R-A02) |
| Database | Private network | Application credential cannot alter schema |
| CI/CD pipeline | GitHub | One person's account |
| Operator's machine | — | Credentials in a vault behind a security key (D-047) |

---

## 5. Where the design holds well

Stated so effort is not spent re-solving these.

- **Enumeration and concealment** — the §3.2 scenario is well covered, including
  timing
- **Session security** — opaque cookies, rotation, CSRF, global logout, instant
  revocation
- **Credential strength** — passkeys, blocklist screening, no composition rules, no
  forced rotation
- **Data minimisation to processors** — the courier, payment provider, and SMS
  gateway each receive the minimum
- **Fail-closed discipline** — enforced by analyzer rather than by vigilance
- **Audit integrity** — append-only at the database level, queryable by subject
- **Identifier lifecycle** (D-146): verification links bound to the originating
  browser, removal undone only from the remaining channels, security notices outside
  destination restrictions (section 3.8)

---

## 6. Gaps this exercise found

Not previously identified. **All are now closed** — resolutions recorded under each.

### 6.1 Insider data exfiltration is undetected

A staff member with legitimate access can read or export customer data at volume and
nothing notices. Permissions limit *reach*; nothing limits *rate* or flags unusual
patterns.

For a business whose customer list is its market position, and whose customer data is
health data, this is the most consequential gap found.

**Resolved — D-045.** Per-actor read volume counted with alerting on deviation from
that actor's own baseline; exports require step-up, are individually audited, and are
rate-limited. Export auditing is protected from application-level change, since an insider who could disable it
would then export.

**Honest limit:** detects and deters, does not prevent.

---

### 6.2 Cash-on-delivery abuse has no control

**Confirmed:** cash on delivery is used.

Order, refuse delivery, and the company absorbs the shipping cost. Repeatable, and
attractive against a competitor as harassment rather than fraud.

**Resolved — D-049: out of scope for this library, owned by the order system.**

The person ordering is a legitimate, verified account holder exercising a permission
they properly hold — nothing in authentication or authorization is circumvented. The
effective controls are commercial: order limits for new customers, prepayment above a
threshold, refusal history tracked per account, deposits for repeat offenders.

**What this library provides to build on:** a stable subject identifier surviving
account changes, so refusal history attaches to a person rather than an email
address.

---

### 6.3 Dependency supply chain is unaddressed

Nothing in the specification covers dependency review, pinning, vulnerability
scanning, or provenance. A compromised package executes with full application
privilege.

**Resolved — D-046.** Lockfile pinning, automated vulnerability alerting, deliberate
dependency addition, prompt security updates.

**Honest limit:** covers *known* vulnerabilities. A freshly compromised package with
no advisory yet is not caught. Minimising dependency count is the only mitigation
there.

---

### 6.4 The operator's machine is an unaddressed attack surface

It holds production credentials, deployment access, and — if the personal-machine
backup copy is ever used — customer health data.

Compromising it compromises everything. Nothing in the specification addresses it.

**Resolved — D-047.** All credentials live in a password manager protected by a
security key plus a password, with a backup. No credential is stored in a file on any
machine, production or otherwise.

**Residual, unavoidable:** an unlocked vault on a compromised machine is an open
vault. Mitigated by a short auto-lock timeout.

---

### 6.5 No monitoring of authentication anomalies

Individual events are logged. Nothing watches for patterns — a spike in failed
sign-ins, recovery attempts clustering on one account, sign-ins from unusual
locations.

**Resolved — D-048, D-071, D-121.** The alert conditions are the table in
OPS-ALERT-001 (`06-operations`), which is the single authoritative list — every
requirement elsewhere that says "alert" has a row there. Email for all, SMS
additionally for high severity, both with delivery confirmation. Alerts are
deduplicated per condition per window so alert flooding cannot be used to drain the
prepaid SMS balance; **all alert-class sends** are exempt from the send hard-stop
(OPS-ALERT-003); mail-system alerts are SMS-first.

---

### 6.6 Shared logins destroy attribution

Identified when staffing was confirmed as high-turnover and not personally known.

Attribution is load-bearing: per-actor volume alerting (D-045), per-approver anomaly
detection (D-008), audit records naming acting and effective identity (D-014), and
offboarding verification (D-050) all assume an account corresponds to a person.
Shared credentials break all four at once, silently.

**Resolved — D-051.** Prohibited explicitly; structurally hard already, since
administrative-organization members use passkeys and a passkey cannot be forwarded
to a colleague; detected through concurrent implausible sessions, alerting via D-048.

**Honest limit:** detection catches implausible patterns, not two people sharing a
laptop in one office. The passkey requirement is what makes that hard.

---

## 7. Assumptions — resolved

| # | Assumption | If wrong |
|---|---|---|
| 1 | Competitors are the top commercial threat | **Answered: unknown.** The user's position is "plausible, but I don't know." Recorded as their answer, not as a pending question. No control depends on it — the insider controls follow from the health-data obligation, which is fixed |
| 2 | The B2B customer list is the most commercially sensitive asset | **Answered: unknown.** Same. Ranked first on reasoning about the business, not on confirmation |
| ✓ 3 | **Confirmed** — cash on delivery is used | D-049 applies |
| ✓ 4 | **Confirmed differently** — few staff, not all known, periodic turnover | §3.5 reworked; D-050 added |
| ✓ 5 | **Confirmed, with nuance** — phone and WhatsApp still carry sales, which the system is intended to replace | R-A04 gains a second trigger |

---

## 8. Review

**THREAT-001** — This model SHALL be revisited when the business gains staff with
system access, when a new external integration is added, or at each licence renewal.

**All gaps in §6 are closed** — 6.1 (D-045), 6.2 (D-049, out of scope), 6.3 (D-046),
6.4 (D-047), 6.5 (D-048), 6.6 (D-051, D-147 corrected the count).

**Section 7 is resolved.** Three assumptions confirmed, two answered "unknown" — which is an
answer. Nothing in §7 is outstanding.
