# 10 — Reference

Catalogues of every enumerated value the specification depends on: error codes,
permission strings, configuration keys, and the fixed enumerations scattered across
documents 01 to 09 and 20.

**Prerequisite:** `00-overview.md`, section 1.

**Why this exists.** LIB-API-001 makes error codes and configuration keys part of the
stable public contract, and CONV-NAME-003 requires every code documented. Both were
referenced without ever being listed. This is that list.

**Status of values first defined here.** Codes and keys already appearing in
documents 01 to 09 are collected. Where a requirement implied a code or key without
naming one, it is named here for the first time and marked **(new)**. Those are
naming decisions within the conventions already agreed, not new behaviour.

---

## 1. Error codes

Hierarchical, lowercase, dot-separated, **stable**. A code is an identifier, not a
message — rewording the human-facing text is free, changing the code is breaking.

*Source: CONV-NAME-003, LIB-API-003*

### 1.1 Identity

| Code | Meaning | Source |
|---|---|---|
| `identity.affirmation.required` | The affirmation record derived at the age step is absent at the terms step | REG-PROF-002, REG-SESS-007, D-146 (was IDN-LIFE-002) |
| `identity.change.pending` | A change of this kind is already in progress: a replace already staged for the identifier (REG-IDENT-007) | IDN-LIFE-004, D-146 |
| `identity.identifier.primary` **(new)** | Removal refused: the identifier is the primary of its kind; set another primary first | REG-IDENT-006, D-146 |
| `identity.identifier.lastofkind` **(new)** | Removal refused: it would leave fewer than the required minimum of that kind (one verified email always; one verified phone while `registration.phone` is `required`) | REG-IDENT-001, REG-IDENT-006, D-146 |
| `identity.identifier.domainnotallowed` **(new)** | The email's domain is outside the organization's verified `emailDomains` list | REG-DOM-001, IDN-ORG-006, D-146 |
| `identity.invitation.identifiermismatch` **(new)** | An identifier bound to the invitation is verified on a different account than the one accepting | REG-INV-001, REG-INV-002, D-146 |
| `identity.username.taken` **(new)** | The username belongs to another account, or is held after an erasure for `retention.consent`; disclosed by design, throttled per source | REG-IDENT-009, D-146 |
| `identity.username.reserved` **(new)** | The username is on the reserved list | REG-IDENT-009, D-146 |
| `identity.username.invalid` | The username fails the PRECIS UsernameCaseMapped profile, its length bounds, or holds no letter; 422 | REG-IDENT-009, D-155 |
| `identity.username.coolingoff` **(new)** | A second username change inside `identifiers.username.changecooloff`; `details` carries the cooling-off end | REG-IDENT-009, D-146 |
| `identity.profile.underage` **(new)** | The date of birth is under eighteen on a host with `registration.adultaffirmation` = `required`; the registration session ends | REG-PROF-002, D-146 |
| `identity.preference.undeclared` **(new)** | A preference key the host did not declare at startup | REG-PREF-001, D-146 |
| ~~`identity.identifier.duplicate`~~ | **Withdrawn (D-076)** — returning it would disclose account existence. Duplicate registration returns the ordinary accepted response |
| `identity.identifier.mixedscript` | Scripts mixed within a single word | IDN-ACCT-005 |
| `identity.link.lastcredential` | Cannot unlink the only remaining credential | IDN-LIFE-012 |
| `identity.membership.limitreached` **(new)** | Multiple memberships not enabled | IDN-MEM-002 |
| `identity.organization.protected` **(new)** | The administrative organization cannot be deleted | IDN-ORG-004 |
| `identity.account.restricted` **(new)** | Processing restricted at the subject's request | IDN-ACCT-007 |
| `identity.takedown.windowelapsed` **(new)** | Takedown reversal requested after its window | IDN-LIFE-003, D-127 |
| `identity.change.windowelapsed` **(new)** | Undo of an identifier removal or replace presented after `identifier.change.coolingoff` | REG-IDENT-006, REG-IDENT-007, D-140, D-146 (was IDN-LIFE-007, IDN-LIFE-010) |
| `identity.takedown.active` **(new)** | Deletion cancellation attempted on a takedown-originated `deleting`; use `/takedown/reverse` | IDN-LIFE-003, D-137 |
| `identity.deletion.windowelapsed` **(new)** | The deletion grace window has closed; cancellation is no longer possible | IDN-ACCT-007, D-106 |
| `identity.invitation.expired` | Invitation link past its lifetime or already used | IDN-LIFE-009a, REG-INV-001, D-106, D-147 |
| `identity.reactivation.tokeninvalid` | Reactivation link token unknown, expired or consumed (`POST /account/reactivate`) | IDN-LIFE-013, D-147 |
| `identity.photo.invalid` **(new)** | Upload failed content validation | IDN-ATTR-004, D-106 |
| `identity.photo.toolarge` **(new)** | Upload exceeds the size or dimension limit | IDN-ATTR-004, D-106 |
| `identity.photo.notenabled` **(new)** | Photos not enabled by the subject's organization policy | IDN-ATTR-002, D-106 |
| `identity.account.adminsuspended` **(new)** | Self-reactivation attempted on an administratively suspended account; moved here from section 1.2 (D-148) | IDN-LIFE-013, D-135 |
| `identity.preference.wrongtype` | A preference value of a type other than its declaration; 422 | REG-PREF-001, D-153 |
| `identity.preference.toolarge` | The preference set would exceed `preferences.maxsize`; 422 | REG-PREF-001, D-153 |
| `identity.preference.administratoronly` | The person set a preference declared administrator-only; 422 | REG-PREF-001, D-153 |

### 1.2 Authentication

| Code | Meaning | Source |
|---|---|---|
| `auth.code.expired` | Verification code past its lifetime | AUTH-FACT-004 |
| `auth.code.invalid` | Verification code rejected | AUTH-FACT-004 |
| `auth.code.replayed` **(new)** | Code already consumed within its window | AUTH-FACT-005 |
| `auth.factor.notpermitted` | Verification-only factor offered as authentication | AUTH-FACT-002 |
| `auth.factor.rejected` | Factor presented and refused | AUTH-FACT-001 |
| `auth.factor.required` **(new)** | Further factors needed to reach required assurance | AUTH-FACT-001 |
| `auth.lossreport.notpermitted` | Self-service loss report and removal unavailable to this account | AUTH-RECOV-008, D-141 |
| `auth.lossreport.pending` **(new)** | This authenticator is already reported lost; `details.invalidatesAt` | AUTH-RECOV-007, D-141 |
| `auth.credential.suspended` **(new)** | A reported-lost authenticator was presented | AUTH-RECOV-007, D-141 |
| `auth.password.blocklisted` | Password found in the compromised corpus | AUTH-PASS-004 |
| `auth.password.tooshort` | Below the floor for this factor count | AUTH-PASS-001 |
| `auth.recovery.channelnotonaccount` | Channel not among the account's recorded channels | AUTH-RECOV-003 |
| `auth.recovery.reasonrequired` | Written reason absent | AUTH-RECOV-002 |
| `auth.recovery.tokenexpired` | Recovery link past its lifetime | AUTH-RECOV-002 |
| `auth.recovery.tokeninvalid` | Recovery link rejected | AUTH-RECOV-002 |
| `auth.recovery.selfapproval` **(new)** | An approver attempted to approve recovery for their own account; enforced in the domain | AUTH-RECOV-002a, D-147 |
| `auth.screening.unavailable` **(new)** | Blocklist screening could not run; operation refused | AUTH-PASS-004 |
| `auth.session.expired` | Session past idle or absolute limit; `details.reauthenticate` is `single-factor` when one factor (passkey or password) restores the session — idle expiry inside the absolute window, **AAL2-policy principals only** — or `full` | AUTH-SESS-005, D-123, D-139 |
| `auth.session.csrfinvalid` **(new)** | CSRF token missing or rejected | AUTH-SESS-007 |
| `auth.stepup.required` **(new)** | The gate is not met; `details` carries the three gate values, the `outcome` (`present` · `enrol` · `report-loss` · `pending`) and the combinations that would satisfy it (`09` `/auth/step-up`) | AUTH-STEP-001, AUTH-STEP-002, D-141 |
| `auth.stepup.unavailable` **(new)** | No assurance provider registered | AUTH-STEP-003 |
| `auth.throttled` **(new)** | Progressive delay in effect; carries `Retry-After` | AUTH-ABUSE-001 |
| `auth.restriction.exceeded` **(new)** | A send refused by a named restriction; `details.retryAt` is the earliest time a bucket lifts, identical whether or not the address is registered (AUTH-ABUSE-002). Replaces `integration.sms.windowactive` | AUTH-ABUSE-004, D-146 |
| `auth.restriction.reasonrequired` **(new)** | A restriction grant, or a loosening edit, submitted without a reason | AUTH-ABUSE-004, OPS-CFG-002, D-146 |
| `auth.device.verificationrequired` **(new)** | The sign-in is held by the new-device check until the code emailed to the primary address is entered at `POST /auth/device/verify`; a status, not a refusal | AUTH-FACT-016, D-146 |
| `auth.policy.graceexpired` **(new)** | The account does not meet a raised requirement and `policy.enforcement.grace` has elapsed; sign-in stops at enrolment. `details.outcome` is `enrol` | AUTH-FACT-017, D-146 |
| `auth.enrolment.tokeninvalid` **(new)** | Enrolment link token unknown or consumed | AUTH-RECOV-002, IDN-LIFE-009a, D-140 |
| `auth.breakglass.invalid` **(new)** | Emergency credential rejected | D-065 |
| `auth.credential.lastsecondfactor` | Removing this credential would lower the account's reachable assurance; suspended now, invalidated after the window (AUTH-RECOV-007) — a 202 status, not a refusal | D-092, D-135, D-141 |
| `auth.breakglass.consumed` **(new)** | Emergency credential already used | D-065 |
| `auth.webauthn.algorithmnotallowed` | Signature algorithm outside the allow-list | AUTH-FACT-014 |
| `auth.webauthn.countermismatch` **(new)** | Signature counter moved backwards — possible cloned credential | AUTH-FACT-014 |
| `auth.webauthn.rpidchanged` **(new)** | Credential enrolled under a different relying party identifier | AUTH-FACT-011 |
| `auth.webauthn.userverificationrequired` | User verification did not occur | AUTH-FACT-014 |
| `auth.challenge.required` | A bot-defence signal fired and the host declared a challenge verifier; the step completes only with a passing challenge token (AUTH-ABUSE-008) | AUTH-ABUSE-008, D-153 |

### 1.3 Authorization

| Code | Meaning | Source |
|---|---|---|
| `authz.denied` **(new)** | Permission absent; used where existence is not concealed | AUTHZ-CONCEAL-005 |
| `authz.grant.duplicate` | An identical live grant exists | AUTHZ-GRANT-003 |
| `authz.grant.expired` **(new)** | Grant past its expiry | AUTHZ-GRANT-003 |
| `authz.grant.notfound` | No such grant | AUTHZ-GRANT-001 |
| `authz.policy.unregistered` **(new)** | Entity has no registered policy — a fault, not a denial | AUTHZ-GATE-001 |
| `authz.restricted` **(new)** | Subject's processing is restricted | AUTHZ-GATE-006 |
| `authz.group.cycle` **(new)** | Adding the member would make a group contain itself | AUTHZ-GROUP-001, D-106 |
| `authz.grant.reasonrequired` | A grant created or revoked without a non-empty `reason`; 422 | AUTHZ-GRANT-003, D-153 |

### 1.4 Privacy

| Code | Meaning | Source |
|---|---|---|
| `privacy.consent.required` **(new)** | Processing attempted without recorded consent | PRIV-SENS-002 |
| `privacy.consent.superseded` **(new)** | Notice version no longer current; re-consent needed | PRIV-CONS-007 |
| `privacy.consent.writtenrequired` **(new)** | Sensitive data requires written consent | PRIV-BASIS-003 |
| `privacy.request.duplicate` **(new)** | An identical request is already open | PRIV-RIGHT-001 |
| `privacy.purpose.notobjectable` **(new)** | The purpose's basis carries no right to object | PRIV-RIGHT-001a, D-145 |
| `privacy.notice.onelanguage` | *Retired by D-146. See `privacy.notice.governingtextmissing`.* | |
| `privacy.notice.governingtextmissing` **(new)** | A document version was submitted without its governing-language text; translations are optional and never suffice | PRIV-CONS-005, PRIV-CONS-006, D-146 |
| `privacy.request.receivedfuture` **(new)** | `receivedAt` later than now | PRIV-RIGHT-002, D-136 |
| `privacy.erasure.notfailed` **(new)** | Manual completion requested for an erasure that has not exhausted its retries | IDN-LIFE-003a, D-106 |

### 1.5 Configuration and model

| Code | Meaning | Source |
|---|---|---|
| `config.key.protected` **(renamed)** | Setting is not changeable through the application | OPS-CFG-004 |
| `config.value.belowfloor` | Value below the enforced minimum | OPS-CFG-003 |
| `config.value.aboveceiling` | Value above the enforced maximum | AUTH-SESS-005 |
| `config.value.notallowed` | Value outside the key's enum or set, or of the wrong type | section 4 value types, D-151 |
| `config.change.stepuprequired` **(new)** | Loosening a control requires step-up | OPS-CFG-002 |
| `config.value.lastdestination` | A change would leave an `alerting.*.destinations` list empty; 422 | OPS-ALERT-004a, D-153 |
| `config.policy.belowsystem` **(new)** | An organization policy field is looser than the system default | AUTH-STEP-002a, D-143 |
| `model.containment.cycle` **(new)** | Containment declaration forms a cycle | AUTHZ-MODEL-004 |
| `model.derivation.unindexed` **(new)** | Derivation names an unindexed column | AUTHZ-DERIVE-004 |
| `model.purpose.missingassessment` **(new)** | Legitimate interest declared without an assessment | PRIV-BASIS-002 |
| `model.type.noorganizationpath` **(new)** | Resource type has no path to an organization | AUTHZ-MODEL-004 |
| `model.startup.rpid` **(new)** | Startup: the relying party identifier is not a registrable suffix of a configured origin | AUTH-FACT-011, D-147 |
| `model.startup.labellimit` **(new)** | Startup: the configured origins exceed the five-label limit of related origins | AUTH-FACT-012, D-147 |
| `model.startup.schemamismatch` **(new)** | Startup: the database schema does not match the model; non-zero exit | OPS-MIG-002, D-147 |
| `model.startup.kekunavailable` **(new)** | Startup: the key-encryption key or the fingerprint key could not be obtained from the secrets manager | OPS-SEC-001, AUTH-KEY-002, D-147 |
| `model.startup.governinglanguage` **(new)** | Startup: `legal.governinglanguage` is unset | PRIV-CONS-005, LIB-HOST-001, D-147 |
| `model.startup.preferencedeclaration` **(new)** | Startup: a host preference declaration is malformed | REG-PREF-001, D-147 |
| `model.startup.declarationmissing` | Startup: a required deployment value, subject-event handler or restriction key supplier is absent; `details.key`, `details.handler` or `details.supplier` names it | LIB-HOST-001, D-153 |
| `model.type.undeclaredreference` | Startup: a resource type references a type that is not declared | AUTHZ-MODEL-004, D-153 |
| `model.role.undeclaredpermission` | Startup: a role grants a permission that is not declared | AUTHZ-MODEL-004, D-153 |
| `model.derivation.undeclaredreference` | Startup: a derivation references a type or relationship that is not declared | AUTHZ-MODEL-004, D-153 |
| `system.fault` | An unhandled fault; 500, body per API-CONV-002 carrying the correlation identifier and nothing else | BFF-ERR-002, D-153 |

### 1.6 Integration

| Code | Meaning | Source |
|---|---|---|
| `integration.callback.rejected` **(new)** | Callback reference invalid or rate-limited | INT-GEN-003 |
| `integration.endpoint.insecure` **(new)** | Configured endpoint is not TLS | INT-GEN-001 |
| `integration.sms.windowactive` | *Retired by D-146. See `auth.restriction.exceeded` (AUTH-ABUSE-004).* | |
| `integration.sms.balancefloor` **(new)** | Gateway balance below the configured floor | INT-SMS-004 |

---

## 2. Permission strings

Format `resource:action` — lowercase, singular resource.

*Source: CONV-NAME-002*

### 2.1 Library-owned

Shipped by the library because they govern its own endpoints and operations
(LIB-API-005). Hosts do not declare these.

| Permission | Governs |
|---|---|
| `grant:read` | Viewing grants and the "who can access this?" view |
| `grant:manage` | Creating and revoking grants |
| `role:manage` | Creating and changing roles |
| `group:manage` | Creating groups, nesting them, and changing their members (D-106) |
| `organization:manage` | Organization lifecycle, policy, and deletion cancellation |
| `membership:manage` | Adding and removing members; issuing and revoking invitations, bound or open (IDN-LIFE-009a, REG-INV-001) |
| `domain:manage` | Adding, verifying and removing an organization's locked email domains (REG-DOM-001, IDN-ORG-006, D-146). Adding is a loosening under OPS-CFG-002 |
| `restriction:edit` | Reading and editing the named restriction set (`GET/PUT/DELETE /admin/restrictions/{name}`, AUTH-ABUSE-004, D-146). Every edit is a step-up action; a loosening also needs a reason and alerts |
| `restriction:grant` | Granting credit to one key under a restriction (`POST /admin/restrictions/{name}/grant`, AUTH-ABUSE-004, D-146): the support role's permission, stepped up, audited with a reason |
| `account:manage` | Suspending and reactivating an account, lifting a restriction, cancelling a deletion on a subject's behalf (D-106) |
| `takedown:execute` | The minor takedown operation (IDN-LIFE-003). Its own permission because of what it does |
| `recovery:approve` | Administrative re-enrolment |
| `session:revoke` | Emergency revocation of all sessions, system-wide |
| `session:revoke-account` | Revoking one account's sessions — offboarding, suspension |
| `config:read` | Reading configuration |
| `config:manage` | Changing configuration |
| `audit:read` | Reading the audit trail, querying it by subject, and resolving a concealed denial's correlation identifier (AUTHZ-GATE-004) |
| `ropa:read` | Generating records of processing |
| `privacyrequest:manage` | The data-subject request queue, erasure progress, and the manual completion path |
| `notice:publish` | Publishing a compliance-text version (PRIV-CONS-006) |
| `compliance:manage` | Licence and permit dates, assessment references, and the declared human-input fields of the records of processing |
| `system:administer` | **Loosening** configuration changes (OPS-CFG-002) and granting the seeded administrative role. The protected settings in OPS-CFG-004 are not changeable through the application at all, by anyone |

`system:administer` is self-referential — granting or revoking it requires holding it
(OPS-CFG-007). A **break-glass session holds the `system-administrator` role**, which
includes it (D-065).

### 2.2 Host-declared

Hosts declare their own in the model builder. The library ships no domain
permissions, and none of the above may be redefined.

Keep them coarse. Twenty is healthy; two hundred means data scope is being encoded
into permission names, which produces role explosion.

---

## 3. Default roles

The library ships **no domain roles**. Roles are host-declared data
(AUTHZ-GRANT-004).

Three administrative roles are seeded at bootstrap so the system is usable
immediately (D-148):

| Role | Permissions |
|---|---|
| `system-administrator` | All library-owned permissions |
| `auditor` | `audit:read`, `grant:read`, `ropa:read` |
| `support` | `restriction:grant`, `audit:read` (D-146): the support role AUTH-ABUSE-004 names, able to add credit to an exhausted key and to read what happened, and nothing else |

All three are editable after bootstrap.

*Source: D-028, OPS-BOOT-001, D-146*

---

## 4. Configuration keys

Part of the stable public contract (LIB-API-001). Grouped by area. **R** =
runtime-changeable, **P** = protected (not changeable through the application; mechanism is an infrastructure choice).

**Value types (D-151).** Every key has exactly one type, read from its default: a
number with a unit of time is a **duration** (ISO 8601, `PT10M`, `P90D`); a bare number is
an **integer** unless the default carries a decimal point (**decimal**); `true`/`false` is
a **boolean**; a backticked word from a stated set is an **enum**; a bracketed list is a
**list** or **set** of the stated element type; anything else is a **string**. Where a
default is written as prose for readability, the row's Scope column names the type. A
value outside a key's enum or set is refused with `config.value.notallowed`; a value
outside a floor or ceiling with `config.value.belowfloor` or `config.value.aboveceiling`.
A duration written in years or months is held at 366 days a year and 31 days a month,
so the held value is never shorter than any calendar span of that length (D-152); a
size in bytes is a bare integer with the binary unit stated in the Scope column.

**Direction (D-152).** OPS-CFG-002 needs a loosening direction for every key. Where a
row names one, that governs. Otherwise a key with a ceiling and no floor loosens
upward, a key with a floor and no ceiling loosens downward, a boolean loosens away
from its default, and any other key (both bounds, neither bound, enum, list, set,
string) loosens on any change, as D-079b classifies settings with no direction.

**Every key carries a safe default (P-001) except the eleven that name the deployment**
— origins (`webauthn.origins`), hosting location (`hosting.location`), the two alert
destination lists, the owner's email and SMS destinations (D-129), the SMS balance
floor, the governing language of legal documents (`legal.governinglanguage`,
D-146), the calendar time zone (`privacy.calendar.timezone`), the message languages
(`notification.languages`) and the email sending domain
(`notification.email.sendingdomain`) (D-153). **Three further keys are conditional**: `hosting.crossborderbasis`, required only when
`hosting.location` is outside Egypt (INT-HOST-002, D-147); `service.name`, required only
when `context` is in `password.blocklist.sources`; `hosting.environment`, required only
when the records-of-processing generator is used (PRIV-ROPA-001) (D-153). Those are
shown with no default, are listed in LIB-HOST-001, and startup **fails** with a named
error if any is unset (the conditional one, when its condition holds). The public-holiday list is **not** among them (D-142): its
safe default is empty. Defaults sit
at the safe end of their range, so a host that never touches a key gets a secure
deployment and the only deliberate change is a loosening, which OPS-CFG-002 audits
(D-107).

### 4.1 Sessions

| Key | Default | Scope | Source |
|---|---|---|---|
| `session.aal2.inactivity` | 1 hour | R, ceiling 12 h | AUTH-SESS-005, D-123, D-130 — where the policy's `requiredAssurance` is `aal2` |
| `session.aal2.absolute` | 24 hours | R, ceiling 24 h | AUTH-SESS-005, D-123 |
| `session.default.inactivity` | 90 days, refreshed by use | R, ceiling 365 d | AUTH-SESS-005, D-123 — the system policy |
| `session.default.absolute` | 365 days | R, ceiling 365 d | AUTH-SESS-005, D-130 — the definite overall timeout NIST §2.1.3 requires |
| `session.stepup.recency` | 15 minutes | R | AUTH-STEP-001, D-107 |
| `policy.default` | the policy object of §4.1a with its system defaults | R, each field classified per §4.1a | AUTH-PRIN-002, D-116, D-143 — the **system** policy, for principals with no membership. Replaces `login.policy.default`, `stepup.policy.default`, `stepup.shape.<action>` / `stepup.gate.<action>` |
| `policy.<organization>` | `{}` (no override: every field inherits `policy.default`) | R, one key per organization identifier, created empty when the organization is | AUTH-PRIN-002, D-116, D-143 — written by `PUT /admin/organizations/{id}/policy`; the administrative organization's is set at bootstrap (§4.1a). Replaces `login.policy.<organization>`, `stepup.policy.<organization>` |
| `privacy.export.ratelimit` | 3 per day | R | D-086, D-107 |
| `policy.enforcement.grace` | 0 | R, ceiling 90 d | AUTH-FACT-017, D-146: the run-up an account gets when a policy's `requiredAssurance` or `credentialRedundancy` is raised; during it a non-compliant sign-in is told the requirement and the deadline and may continue, after it the sign-in stops at enrolment (`auth.policy.graceexpired`). Lengthening is loosening. The one place the grace is defined |

### 4.1a The policy object

The one thing AUTH-PRIN-002 resolves per principal. Six fields; an organization
stores only what it overrides. Every rule that says "where the policy requires AAL2"
reads `requiredAssurance` — nothing is inferred from which factors are enabled.

| Field | Meaning — read by | System default (customers) | Administrative organization, at bootstrap | Direction (OPS-CFG-002) |
|---|---|---|---|---|
| `requiredAssurance` | `aal1` · `aal2`. The stated floor — session lifetimes (AUTH-SESS-005), the staff floor (AUTH-SESS-005b), the trusted-device offer (AUTH-FACT-015), the single-factor idle restore (D-139) | `aal1` | `aal2` | lowering is loosening |
| `loginFactors` | The set of catalogue identifiers (`02` AUTH-FACT-002: `password`, `passkey`, `emailLink`, `emailCode`, `phoneLink`, `google`, `apple`, `totp`, `securityKey`, `phoneCode`, `recoveryCodes`) a principal may sign in with, primary and second; `breakGlass` and the verification code are not entries and cannot appear here (D-151). The entries `emailLink`, `emailCode`, `phoneLink` and `phoneCode` are **off by default** and a host enables them here (D-146, D-147); `phoneLink` and `phoneCode` are restricted factors (AUTH-FACT-002b) | `password`, `passkey`, `google`, `apple`, `totp`, `securityKey`, `recoveryCodes` (that is, the catalogue less `emailLink`, `emailCode`, `phoneLink`, `phoneCode`) | `passkey` only | adding is loosening |
| `gates` | Per §5a action, keyed by the action name: `level` (`aal1` · `aal2` · `reachable`), `phishingResistant`, `maxAge` (AUTH-STEP-002/002a) | `reachable`, floor `aal1` · no · `session.stepup.recency` | `aal2` · yes · `session.stepup.recency` | lowering a level, dropping phishing-resistance or lengthening `maxAge` is loosening |
| `credentialRedundancy` | `advisory` · `enforced` — whether a second credential is required after a device-bound enrolment (AUTH-RECOV-001) | `advisory` | `enforced` | to `advisory` is loosening |
| `selfServiceRecovery` | Whether email recovery and self-service loss reports are available (AUTH-RECOV-004, AUTH-RECOV-008) | `true` | `false` | to `true` is loosening |
| `emailDomains` | Domain lock (REG-DOM-001, IDN-ORG-006): `off`, or the list of domains **verified by DNS** whose addresses members may sign in with. Written only through `/admin/organizations/{id}/domains`, never through `PUT .../policy`; a listed domain admits nothing until verified, and is re-verified on a schedule | `off` | `off` | adding a domain is loosening (OPS-CFG-002); removing one alerts (OPS-ALERT-001) |

An organization MAY tighten any field and SHALL NOT loosen below the system default
(AUTH-STEP-002a). `stepup.enforcement.<organization>` (§4.8) is not a field of this
object: it is the protected kill switch, unreachable from the application.

*Source: AUTH-PRIN-002, AUTH-STEP-002a, D-116, D-141, D-143, D-146*

### 4.2 Passwords

| Key | Default | Scope | Source |
|---|---|---|---|
| `password.floor.singlefactor` | 15 | R, floor 15 (NIST SP 800-63B-4 SHALL) | AUTH-PASS-001, D-140 |
| `password.floor.withmfa` | 10 | R, integer, floor 8 (NIST minimum with MFA); SHALL NOT exceed `password.floor.singlefactor` | AUTH-PASS-001, D-140 |
| `password.maximum` | 128 | R, floor 64 | AUTH-PASS-001, D-146 (was 64) |
| `password.blocklist.source` | `rangeApi` | R; `rangeApi` · `offline` · `selfHosted` | AUTH-PASS-004: where the leaked-password list comes from (range API, offline fallback, self-hosted corpus) |
| `password.blocklist.sources` | `[leaked]` | R; set of `leaked` · `dictionary` · `context`; `leaked` cannot be removed | AUTH-PASS-004, D-146: the **rejection** sources. MAY add `dictionary` (a word list) and `context` (the person's own identifiers, profile fields and the service name); both off by default, a recorded deviation from NIST SP 800-63B-4 section 3.1.1.2 (`13-risk-register`). Adding a source is tightening |
| `password.blocklist.corpusmaxage` | 30 days | R | D-011, D-107 |
| `password.argon2.memory` | 19456 | R, integer KiB; the floor is the rule that (`memory`, `iterations`) is at or above one of (19456, 2), (12288, 3), (9216, 4), (7168, 5), evaluated across both keys | AUTH-PASS-007, D-120, D-135 |
| `password.argon2.iterations` | 2 | R, integer; floor as the rule on `password.argon2.memory` | AUTH-PASS-007, D-120, D-135 |
| `password.argon2.parallelism` | 1 | R | AUTH-PASS-007, D-120 |

### 4.3 Factors

| Key | Default | Scope | Source |
|---|---|---|---|
| `factor.<name>.enabled` | *Retired by D-148. See `loginFactors` (section 4.1a): the policy's `loginFactors` is the only switch that enables a factor for a principal (AUTH-FACT-002)* | | |
| `factor.totp.drift` | 1 step | R | AUTH-FACT-005 |
| `factor.recoverycodes.count` | 10 | R | AUTH-FACT-008 |
| `factor.trusteddevice.lifetime` | 30 days | R, ceiling 90 d | AUTH-FACT-015, D-124 — second-factor skip on a trusted browser; not offered where the policy's `requiredAssurance` is `aal2` |
| `factor.trusteddevice.failurelimit` | 3 consecutive wrong passwords | R, ceiling 5 | AUTH-FACT-015, D-134 — revokes the device's trust |
| `device.verification.enabled` | true | R | AUTH-FACT-016, D-146: the new-device check for accounts whose reachable assurance is AAL1; a code to the primary email before the sign-in completes. Turning it off is loosening |
| `device.verification.lifetime` | 90 days | R, ceiling 365 d | AUTH-FACT-016, D-146: how long a browser that passed the check is remembered. Lengthening is loosening |
| `recovery.codes.reminder` | 365 days | R | AUTH-FACT-008, D-146: age of a recovery-code set at which one reminder fires in the account and to the security-notice set; none after until the set is regenerated |
| `webauthn.rpid` | derived | **P** | AUTH-FACT-010 |
| `webauthn.origins` | — **required** | **P** | AUTH-FACT-010 |
| `webauthn.relatedorigins` | empty | R | AUTH-FACT-012, D-107 |
| `webauthn.algorithms` | `[-8, -7, -257]` (EdDSA, ES256, RS256) | **P**, list of COSE algorithm integers; −7 SHALL be a member | AUTH-FACT-014, D-120 |

### 4.4 Recovery

| Key | Default | Scope | Source |
|---|---|---|---|
| `recovery.approvers.required` | 1 | R | AUTH-RECOV-002 |
| `recovery.link.lifetime` | 1 hour | R | AUTH-RECOV-002, D-107 |
| `recovery.invalidation.window` | 7 days | R | AUTH-RECOV-007, D-141 — from loss report (or assurance-lowering removal) to invalidation; formerly `recovery.mfaremoval.window` |
| `recovery.invalidation.noticeinterval` | `P1D` | R | AUTH-RECOV-007, D-153: the loss-report notice repeats at this interval across the window, plus once at the report and once 24 hours before invalidation |
| `recovery.ratelimit.account` | 3 per `P1D` | R, integer per day; raising is loosening | AUTH-RECOV-002, D-153: recovery requests accepted for one account |
| `recovery.ratelimit.approver` | 5 per `P1D` | R, integer per day; raising is loosening | AUTH-RECOV-002, D-153: approvals one approver may give |
| `breakglass.session.lifetime` | 4 hours, ceiling 12 | R, ceiling enforced | D-065, D-107 |

### 4.5 Abuse controls

| Key | Default | Scope | Source |
|---|---|---|---|
| `abuse.throttle.enabled` | true | **P** | OPS-CFG-004 |
| `abuse.throttle.threshold` | 3 consecutive failures | R | AUTH-ABUSE-001, D-140 — failures before the first delay |
| `abuse.throttle.delay.initial` | 1 second | R | AUTH-ABUSE-001, D-132 — first delay after the threshold |
| `abuse.throttle.delay.factor` | 2.0 | R, decimal multiplier per further failure, floor 1.0 | AUTH-ABUSE-001, D-132 |
| `abuse.throttle.delay.max` | 60 seconds per source | R, ceiling 10 min | AUTH-ABUSE-001, D-132 |
| `abuse.throttle.account.cap` | 30 seconds | R, ceiling 60 s | AUTH-ABUSE-001, D-132 — the per-account component's cap; keeps the denial-of-service lever small |
| `abuse.throttle.decay` | `PT10M` | R, duration: the half-life of the accumulated delay while no failure occurs | AUTH-ABUSE-001, D-132 |
| `abuse.nonexistent.window` | 1 hour per address | R | AUTH-ABUSE-003, D-132, D-148: one notice per address per window, for both the non-existence message sent to an unknown address at a sign-in path (AUTH-ABUSE-003) and the notice sent to the owner of an address someone else tried to register or add (REG-SESS-005, REG-IDENT-008) |
| `abuse.sms.window` | *Retired by D-146. See `restrictions` below and AUTH-ABUSE-004.* | | |
| `abuse.source.ratelimit` | 300 per `PT1M` | R, integer requests per minute per source address, sliding; 429 `auth.throttled`; raising is loosening | BFF-ORDER-001 stage 4, D-153: the flood limit on every request before any expensive work; sized for many people behind one address |
| `abuse.botdefence.repeatedattempts` | 3 per `PT1H` | R, integer registration sessions per source per hour; raising is loosening | AUTH-ABUSE-008, D-153: the count behind the `repeatedAttempts` signal |
| `integration.callback.ratelimit` | 60 per `PT1M` | R, integer per source per minute, fixed window; 429 `integration.callback.rejected` before any lookup; raising is loosening | INT-GEN-003, BFF-MACH-003, D-153 |
| `restrictions` | the four shipped restrictions below | R, edited through `GET/PUT/DELETE /admin/restrictions/{name}` (step-up `restriction:edit`); a loosening (a higher max, a shorter interval, a removed bucket, a deleted restriction) falls under OPS-CFG-002 and raises a Normal alert | AUTH-ABUSE-004, OPS-CFG-008, D-146: the **named restriction set** governing every send. Each restriction is a key (§5.14), an optional purpose (§5.15) and one or more buckets of (max, interval, `sliding` · `fixed`, §5.16). Security notices to an existing holder are outside destination restrictions and governed by `notification.destination` only. Replaces `abuse.sms.window`, INT-SMS-002 and IDN-LIFE-011 |
| `restrictions` · `sms.destination` | key `destination`, purpose `any`, 3 per 24 h sliding | R, as above | AUTH-ABUSE-004, D-146 |
| `restrictions` · `sms.source` | key `source`, purpose `any`, 10 per 1 h sliding | R, as above | AUTH-ABUSE-004, D-146 |
| `restrictions` · `email.destination` | key `destination`, purpose `any`, 5 per 1 h sliding and 1 per 60 s fixed | R, as above | AUTH-ABUSE-004, D-146: also bounds the new-device check code (AUTH-FACT-016) |
| `restrictions` · `notification.destination` | key `destination`, purpose `notification`, 5 per 24 h sliding | R, as above | AUTH-ABUSE-004, D-146: the only restriction that applies to security notices |
| `code.verification.lifetime` | 10 minutes | R, ceiling 30 min | AUTH-FACT-004, D-132 — phone and email verification codes, including the new-device check code (AUTH-FACT-016) |
| `code.verification.attempts` | 5 | R, ceiling 10 | AUTH-FACT-004, REG-SESS-003, D-146: wrong tries after which a verification code is invalidated and a correct one refused; a replacement draws on the restrictions |
| `link.magic.lifetime` | 15 minutes | R, ceiling 1 h | AUTH-FACT-003, AUTH-FACT-004, D-132, D-146: email and SMS sign-in links (`emailLink`, `phoneLink`, `POST /auth/link`); the link completes only in the requesting browser and only on a press (REG-SESS-003) |
| `link.invitation.lifetime` | 7 days | R, ceiling 30 d | IDN-LIFE-009a, D-132 — single use |
| `photo.maxbytes` | 2097152 | R, integer bytes (2 MiB), ceiling 10485760 (10 MiB) | IDN-ATTR-004, D-132, D-152 |
| `photo.maxdimension` | 1024 | R, integer pixels on the longest side; larger images are downscaled | IDN-ATTR-004, D-132 |
| `abuse.sms.balancefloor` | — **required** | R, decimal in the currency the gateway reports | INT-SMS-004, D-153 |
| `abuse.sms.pollinterval` | `PT15M` | R | INT-SMS-004, AUTH-ABUSE-006, D-153: how often the gateway balance is read |
| `abuse.sms.drainfactor` | 3.0 | R, decimal, floor 1.0; raising is loosening | INT-SMS-004, D-153: the `sms-balance` alert fires when the last hour's spend exceeds this factor times the trailing seven-day hourly mean, or when the balance would reach `abuse.sms.balancefloor` within 24 hours at the current rate |
| `abuse.botdefence.signals` | `[datacenterRange, repeatedAttempts]` | R, set over the two members named; the set is closed until a decision adds a member; removing a member is loosening | AUTH-ABUSE-008, D-107, D-152 |
| `exfiltration.readvolume.alerting` | `true` | R | D-045 |
| `exfiltration.export.stepuprequired` | true | R | D-045, D-148: **staff bulk export only**. `/privacy/export` is not governed by this key; it is gated at the account's reachable assurance by AUTH-STEP-002a instead (D-141) |
| `exfiltration.export.ratelimit` | 5 per hour | R | D-045, D-107 |
| `exfiltration.export.auditing` | `true` | **P** | D-045 |
| `alerting.email.destinations` | — **required** | R, **list** | D-048, D-071 |
| `alerting.sms.destinations` | — **required** | R, **list** | D-048, D-071 |
| `alerting.owner.enabled` | false | R | D-071, D-129 — routine alerts to the owner; break-glass events bypass it |
| `alerting.owner.email` | — **required** | R | OPS-BOOT-002, D-129 |
| `alerting.owner.sms` | — **required** | R | OPS-BOOT-002, D-129 |
| `alerting.destinationchange.notify` | *Retired by D-152. The notice to the previous destinations is non-suppressible (D-083, OPS-ALERT-004a); a switch for it was the hole D-083 closed, so no key exists.* | | |
| `exfiltration.readvolume.baselinewindow` | 30 days | R | D-071, D-107 |
| `exfiltration.readvolume.factor` | 3.0 | R, decimal, floor 1.0; raising is loosening | OPS-ALERT-005, D-153: `read-volume-anomaly` fires when the actor's records returned today exceed this factor times their daily mean over the baseline window and exceed `exfiltration.readvolume.minimum` |
| `exfiltration.readvolume.minimum` | 500 | R, integer; raising is loosening | OPS-ALERT-005, D-153: the floor under which no read-volume alert fires |
| `alerting.authfailures.threshold` | 20 | R, integer failures on one account inside `alerting.dedupe.window`; raising is loosening | OPS-ALERT-002, D-153: `auth-failures-sustained` |
| `alerting.recovery.accountthreshold` | 3 per `P1D` | R, integer; raising is loosening | OPS-ALERT-001, D-153: `recovery-clustering` |
| `alerting.recovery.approverthreshold` | 3 per `P1D` | R, integer; raising is loosening | OPS-ALERT-001, D-153: `approver-volume` |
| `alerting.denials.threshold` | 50 per `PT10M` | R, integer denials per actor in a fixed ten-minute window; raising is loosening | AUTHZ-GATE-004, D-153: `denial-spike` |
| `alerting.sessions.distance` | 500 | R, integer kilometres; raising is loosening | OPS-ALERT-007, D-153: `concurrent-sessions-implausible` fires when two sessions of one account are both used inside `alerting.sessions.window` and their resolved cities are further apart than this, or their countries differ; an unresolved location never fires |
| `alerting.sessions.window` | `PT1H` | R, duration; lengthening is loosening | OPS-ALERT-007, D-153 |
| `alerting.nonexistent.threshold` | 20 per `PT1H` | R, integer, system-wide; raising is loosening | AUTH-ABUSE-003, D-121, D-153: `nonexistent-notice-rate` |
| `alerting.callback.threshold` | 10 per `PT1H` | R, integer rejected callbacks per source; raising is loosening | BFF-MACH-003, D-153: `callback-verification-failed` |
| `alerting.dedupe.window` | 1 hour | R | D-048, D-107 |
| `alerting.sms.severitythreshold` | `high` | R: `high` · `normal` (OPS-ALERT-001 severities); to `normal` sends more, to `high` fewer | D-048, D-152 |
| `maintenance.expiry.warninglead` | 30 days | R | OPS-MAINT-001, OPS-ALERT-001, D-121 |

### 4.5a Authorization

| Key | Default | Scope | Source |
|---|---|---|---|
| `authz.reverselookup.budget` | 2 seconds | R | AUTHZ-SEAM-001, R-A09, D-125 — the migration trigger's measurable bound |

### 4.6 Organizations and lifecycle

| Key | Default | Scope | Source |
|---|---|---|---|
| `organization.deletion.grace` | `P30D` | R, floor `P7D` | IDN-ORG-003, D-152 |
| `takedown.grace` | `P7D` | R, floor `P7D` (the default; D-127 chose the period) | IDN-LIFE-003, D-127, D-152 — access stops at trigger; erasure runs at the end of the window |
| `account.deletion.grace` | `P30D` | R, floor `P7D` | IDN-ACCT-007, D-113, D-152 |
| `organization.multiplememberships` | false | R | IDN-MEM-002 |
| `identifier.change.coolingoff` | `PT72H` | R, floor `PT72H` (the default; D-134 chose the window) | IDN-LIFE-007, IDN-LIFE-010, D-107, D-134, D-146 — email and phone alike (was `email.change.coolingoff`); since D-146 the **undo window** after an identifier removal or replace, during which the remaining security-notice set holds a one-click restore (REG-IDENT-006, REG-IDENT-007) |
| `registration.phone` | `required` | R: `required` · `optional`; to `optional` is loosening (OPS-CFG-002) | REG-IDENT-001, D-146: whether an account must hold a verified phone; phone is never the sole identifier |
| `registration.adultaffirmation` | `required` | R: `required` · `off` | REG-PROF-002, D-146: `required` ends the session on an under-age date; `off` records the age group and no affirmation (a host that serves minors) |
| `registration.session.lifetime` | 24 hours | R, ceiling 72 h | REG-SESS-001, D-146: life of a registration session; an expired one is swept and leaves nothing |
| `identifiers.email.max` | 10 | R, integer, floor 1, no ceiling; raising is loosening (each verified address is a send destination and a recovery channel) | REG-IDENT-002, REG-IDENT-007, D-146, D-152: verified emails per account; `1` is single-address mode (replace in one operation) |
| `identifiers.phone.max` | 10 | R, integer, floor 1, no ceiling; raising is loosening | REG-IDENT-002, REG-IDENT-007, D-146, D-152: verified phones per account; `1` is single-address mode |
| `identifiers.username.enabled` | false | R | REG-IDENT-001, REG-IDENT-009, D-146: while off no request accepts a username and no response carries the field |
| `identifiers.username.changecooloff` | `P30D` | R, floor `P1D` | REG-IDENT-009, D-146, D-152: minimum interval between username changes (`identity.username.coolingoff`) |
| `profile.legalname` | `off` | R: `off` · `optional` · `required` | REG-PROF-001, D-146: a proofing attribute, collected only with a declared purpose |
| `profile.dateofbirth` | `off` | R: `off` · `optional` · `required` | REG-PROF-001, REG-PROF-002, D-146: whether the date entered at the age step is retained; with `off` only the derived affirmation is kept. The date is immutable to the person |
| `domain.reverify.interval` | `P1D` | R, duration, floor `PT1H`; lengthening is loosening | REG-DOM-001, D-153: the sweep re-verifies every locked domain's TXT record at this interval |
| `outbox.poll.interval` | `PT5S` | R, duration | IDN-LIFE-003a, INT-MAIL-006a, D-153: the outbox publisher cadence |
| `outbox.retry.initial` | `PT30S` | R, duration | IDN-LIFE-003a, D-153: first retry delay, full jitter |
| `outbox.retry.factor` | 2.0 | R, decimal, floor 1.0 | IDN-LIFE-003a, D-153: multiplier per further attempt |
| `outbox.retry.maxattempts` | 10 | R, integer, floor 1 | IDN-LIFE-003a, D-153: attempts before `failed` and the `erasure-delivery-exhausted` alert (about eight hours end to end at the defaults) |
| `sweep.interval` | `PT5M` | R, duration, ceiling `PT15M` | OPS-OBS-003, AUTH-KEY-003, D-153: one sweep for expired sessions, tokens, codes, elapsed grace windows and domain re-verification; a deadline therefore fires within this interval of its instant |
| `notification.languages` | — **required** | R, **list** of BCP 47 tags, at least one | IDN-ATTR-001, AUTH-ABUSE-005, D-031, D-153: the deployment's message languages; step 3 of the recipient-language resolution sends in all of them, and every template is validated in each at startup |
| `notification.email.sendingdomain` | — **required** | R, string | INT-MAIL-011, D-153 |
| `notification.email.relayregistered` | empty | R, **set** of domains | INT-MAIL-011, D-153: domains registered with the Apple private relay; when `apple` is in any effective `loginFactors` and the sending domain is not in this set, the Normal condition `relay-domain-unregistered` fires |
| `location.database.refresh` | `P7D` | R, duration | INT-GEN-006, D-153: IP location database refresh cadence |
| `location.database.maxage` | `P30D` | R, duration | INT-GEN-006, D-153: beyond this age the file is stale, no location is shown, and a Normal degradation is raised |
| `service.name` | — **required when `context` is in `password.blocklist.sources`** | R, string | AUTH-PASS-004, D-153: the word the `context` source forbids in passwords |
| `preferences.maxsize` | 8192 | R, integer bytes (8 KiB), ceiling 65536 (64 KiB) | REG-PREF-001, D-146, D-152: cap on the whole set of host-declared preference values per account |

### 4.7 Privacy

| Key | Default | Scope | Source |
|---|---|---|---|
| `privacy.request.decision` | 6 | R, integer working days from submission, ceiling 6 (the statutory period PRIV-RIGHT-002 cites; only a shorter deadline is configurable) | PRIV-RIGHT-002, D-126, D-136, D-152 — the decision deadline; lapse is a deemed rejection |
| `privacy.calendar.timezone` | — **required** | **P**, IANA time zone name | PRIV-RIGHT-002, LIB-HOST-001, D-153: the zone in which calendar days, working days and holidays are determined; `Africa/Cairo` for the default deployment |
| `privacy.workingdays` | `[sunday, monday, tuesday, wednesday, thursday]` | R, set of weekday names, at least one | PRIV-RIGHT-002, D-136 — the week on which "working days" are counted |
| `privacy.holidays` | **empty** — public-holiday dates, added and moved as they are announced | R, **loosening** (OPS-CFG-002: step-up, reason, audit) | PRIV-RIGHT-002, D-136, D-142 — never required, never a startup condition: an unlisted holiday counts as a working day and makes a deadline *earlier*, which is always compliant. A Normal alert fires when no listed date lies beyond `maintenance.expiry.warninglead` (OPS-ALERT-001) |
| `privacy.request.warninglead` | 2 working days before the deadline | R | PRIV-RIGHT-002, OPS-ALERT-001, D-126 |
| `retention.audit.security` | `P7Y` | R, duration, floor `P5Y` | PRIV-RET-001, D-132, D-152 — security events, permission changes, financial actions; five-year tax retention plus a margin for claims |
| `retention.audit.routine` | 90 days | R, floor 30 days | PRIV-RET-001, D-132 — routine access logging |
| `retention.consent` | `P3Y` | R, duration counted from the end of the processing the consent covered, floor `P1Y` | PRIV-RET-001, D-132, D-152 — evidential period |
| `retention.<host-category>` | the floor the host declares for that category (LIB-HOST-001); no library default, startup fails for a declared category without one | R, duration, one key per declared category, floor enforced | PRIV-RET-001, D-107 |
| `hosting.environment` | — **required with the records-of-processing generator** | R, string | PRIV-ROPA-001, D-153: the free-text hosting environment cell of the register |
| `backup.restoretest.objective` | `PT8H` | R, duration, ceiling `PT8H` | DR-007, D-044, D-153: `restore-test-failed` fires when the automatic restore test exceeds this; the upper bound of the accepted objective |
| `backup.restoretest.interval` | `P3M` | R, duration, ceiling `P3M` | DR-007, D-110, D-153 |
| `backup.restoretest.canary` | set at bootstrap | R, subject identifier | DR-007, OPS-BOOT-001, D-153: the canary subject bootstrap seeds in the administrative organization, holding one encrypted field and one verified email; the restore test decrypts the field and resolves the fingerprint |
| `backup.retention` | 35 days | R, floor 14 days | DR-003, DR-006a, DR-010, D-132 — also bounds how long a pre-erasure backup survives |
| `hosting.location` | — **required** | **P** | INT-HOST-001 |
| `hosting.crossborderbasis` | — **required when outside Egypt** | **P** | INT-HOST-002 |
| `legal.governinglanguage` | — **required** | **P** (a restart to change); a change raises a Normal alert (OPS-ALERT-001) | PRIV-CONS-005, LIB-HOST-001, D-146: the default governing language of every legal document version; each version carries its own, translations attach and never govern. Arabic for the default (Egyptian) deployment (D-031) |

### 4.8 Security — protected (not changeable through the application)

Every key here is on the OPS-CFG-004 list. The selection test is not "how sensitive is
this setting" but **"does turning this off blind us to the person turning it off."**

**The requirement is that these are unreachable from the application.** A
command-line operation restricted to the server satisfies it as well as a
redeployment. Every change is recorded and alerted (OPS-ALERT-001).

| Key | Source |
|---|---|
| `audit.enabled` | OPS-CFG-004 |
| `exfiltration.export.auditing` | D-045 |
| `abuse.throttle.enabled` | OPS-CFG-004 |
| `stepup.enforcement.<organization>` | OPS-CFG-004; boolean, default `true`, one key per organization identifier (D-151) |
| `webauthn.rpid` | OPS-CFG-004 |
| `token.signing.algorithm` | OPS-CFG-004; value and rationale in §4.9 (D-147) |
| `token.signature.verification` | OPS-CFG-004 |
| `privacy.calendar.timezone` | OPS-CFG-004; a legal clock that moves at runtime is a clock nobody can audit (D-153) |
| `legal.governinglanguage` | PRIV-CONS-005, D-146 (protected for a different reason: the language a document binds in is not a runtime toggle) |

### 4.9 Tokens and keys

The lifetimes, algorithm and rotation cadence of the OIDC provider (`02` sections 8
and 9). The access-token lifetime is recorded as the accepted revocation latency for
any relying party that validates offline against the JWKS (AUTH-OIDC-004, D-147).

| Key | Default | Scope | Source |
|---|---|---|---|
| `oidc.accesstoken.lifetime` | 10 minutes | R, ceiling 1 h; lengthening is loosening (OPS-CFG-002) | AUTH-OIDC-004, D-147: for an offline validator this is the revocation latency |
| `oidc.code.lifetime` | 60 seconds | R, ceiling 10 min | AUTH-SESS-012, D-147: replaces the hard-coded sixty seconds |
| `token.signing.algorithm` | `ES256` | **P** (§4.8) | AUTH-KEY-001, D-147, D-148: verified against the mail server's source, file `crates/directory/src/backend/oidc/lookup.rs` of its repository, inspected 2026-09-18, which accepts P-256 keys from a JWKS as ES256 and refuses symmetric keys. The one place the value is held |
| `token.signing.rotation` | 90 days | R | AUTH-KEY-001, D-147: overlap is `oidc.accesstoken.lifetime` plus 5 minutes, not a key of its own |

---

## 5. Fixed enumerations

Collected from across the specification.

### 5.1 Account states

`active` · `suspended` · `restricted` · `deleting` · `deleted`

`pending` was removed by D-146: an account is created `active` in one transaction at
the terms step of the registration session (REG-SESS-001), and an incomplete
registration is a session, not an account.

*Source: IDN-ACCT-007, D-146*

### 5.2 Session types

`per-app` · `auth` · `oidc-token`

*Source: AUTH-SESS-004*

### 5.3 Factor properties

`CanBePrimary` · `CanBeSecondFactor` · `IsPhishingResistant` · `AssuranceLevel` ·
`VerificationOnly`

*Source: AUTH-FACT-001, D-141 — `CanSatisfyStepUp` removed; eligibility follows from
`AssuranceLevel` and `IsPhishingResistant` against the gate*

### 5.3a Authenticator states

`active` · `suspended` · `invalidated`

*Source: AUTH-RECOV-007, D-141*

### 5.4 Assurance levels

`delegated` · `aal1` · `aal2` · `aal3`

`delegated` — no asserted AAL: a social-only session (AUTH-SESS-005a, D-128, D-141);
signed in on the provider's word, below `aal1` wherever a tier is needed. Formerly
`none`.

The same values name an **account's reachable assurance** (AUTH-STEP-006).

*Source: AUTH-SESS-002, D-140, D-141*

### 5.5 Subject types

`user` · `group`

*Source: AUTHZ-GRANT-001*

### 5.6 Grant kinds

`stored` · `derived` · `materialised`

*Source: AUTHZ-GRANT-001, AUTHZ-DERIVE-005*

### 5.7 Lawful bases — default declaration

Egypt's six, shipped as the default host declaration; another jurisdiction declares
its own (D-108). **Not** the European set — there is no vital-interests basis and no
public-task basis. Each carries the properties `IsConsent`,
`RequiresWrittenConsentForSensitive`, `RequiresAssessment`, `IsObjectable`
(PRIV-BASIS-001); the code reads those and never the key.

`consent` · `contractual-obligation` · `legal-obligation` · `legitimate-interest` ·
`legal-right-claim-or-defence` · `court-judgment-or-order`

*Source: PRIV-BASIS-001, D-108*

### 5.8 Data subject rights

`be-informed` · `access` · `withdraw-consent` · `erasure` · `restrict-processing` ·
`portability` · `object` · `rectification` · `breach-notification`

*Source: PRIV-RIGHT-001*

### 5.9 Sensitive data categories — default declaration

Egypt's list, shipped as the default host declaration; labels only, nothing branches
on them (D-108).

`health` · `genetic` · `biometric` · `financial` · `religious-belief` ·
`political-view` · `criminal-record` · `children`

*Source: PRIV-SENS-001, D-108*

### 5.10 Consent kinds

`ordinary` · `written`

*Source: PRIV-BASIS-003*

### 5.11 Concealment behaviour

`conceal` (default, returns not-found) · `disclose` (returns forbidden)

*Source: AUTHZ-CONCEAL-001*

### 5.12 Erasure status

`awaiting-subscribers` · `complete` · `failed`

*Source: IDN-LIFE-003b, D-102*

### 5.12a Erasure reason

`erasure-request` · `minor-takedown` · `organization-erasure`

The `Reason` column of the erasures table (IDN-LIFE-003b) and the third field of the
off-host erasure record (DR-016) use these spellings and no others.

*Source: IDN-LIFE-003b, DR-016, D-147*

### 5.12b Suspension and deletion origin

`suspendedBy`: `self` · `administrator` (IDN-LIFE-013). `deletingBy`: `self` ·
`takedown` · `oob-request` (IDN-LIFE-003). Recorded when the state is entered; a
`takedown` deletion is reversed only through `/takedown/reverse`, and an `oob-request`
deletion sends no cancel link to the subject.

*Source: IDN-LIFE-003, IDN-LIFE-013, D-137, D-147*

### 5.12c Privacy request status and type

Status: `open` · `fulfilled` · `refused` · `granted-by-lapse` ·
`deemed-refused-by-lapse` (PRIV-RIGHT-002). Type: `erasure` · `restriction` ·
`rectification` (PRIV-RIGHT-001; rectification of editable data is account editing
and raises no request).

*Source: PRIV-RIGHT-001, PRIV-RIGHT-002, D-126, D-147*

### 5.12d Takedown trigger

`staff-report` (a member of staff observed the condition) · `customer-report`
(another customer reported it) · `automated-signal` (a host-declared rule or
screening raised it) · `authority-request` (a competent authority asked). Recorded by
`POST /admin/accounts/{subject}/takedown` beside the written reason (`14` section 3).

*Source: IDN-LIFE-003, D-127, D-147*

### 5.12e Step-up rule shape

A gate is three values, as AUTH-STEP-002 defines it: **level** (`aal1` · `aal2`, or
the account's reachable assurance under the system policy), **phishing-resistance**
(required or not) and **maximum age** (a duration, default `session.stepup.recency`).
Every step-up rule, library-owned or host-declared, is expressed in this shape and no
other.

*Source: AUTH-STEP-002, AUTH-STEP-002a, D-147*

### 5.13 Client kinds

`protocol` (receives access and refresh tokens) · `browser-application` (code flow
once, no refresh token)

*Source: AUTH-SESS-012, D-104*

### 5.14 Restriction keys

`destination` (an HMAC of the canonical address) · `account` · `source` · `global` ·
`host:<name>` (a per-send value supplied by a host-registered callback, LIB-HOST-001)

*Source: AUTH-ABUSE-004, D-146*

### 5.15 Restriction purposes

`verification` · `signin` · `secondfactor` · `notification` · `any`

A restriction with a purpose applies to sends of that purpose only; `any` applies to
every send except a security notice to an existing holder, which is outside destination
restrictions with purpose `any` and is governed by `notification.destination` only
(AUTH-ABUSE-004). A recovery link carries the `notification` purpose (REG-IDENT-002).

*Source: D-148; AUTH-ABUSE-004, D-146*

### 5.16 Bucket windows

`sliding` · `fixed`

A bucket is (max, interval, window). A sliding bucket counts sends in the interval
ending now; a fixed bucket resets at the interval boundary.

*Source: AUTH-ABUSE-004, D-146*

### 5.17 Identifier kinds and backup setting

Kinds: `email` · `phone` · `username`

Backup setting, per kind: `all-verified` (default) · `primary-only` · the id of one
named verified identifier. The security-notice set is the primary plus what the
setting adds.

*Source: REG-IDENT-001, REG-IDENT-002, D-146*

### 5.18 Credential `backupState` display

`backupEligible` · `backupState` are the WebAuthn flags recorded at registration
(AUTH-FACT-013). The credential list renders them as **synced** (eligible and backed
up) or **this device only** (otherwise); nothing else about a credential is shown
(AUTH-FACT-001, `18` FE-ACCT-001).

*Source: AUTH-FACT-001, AUTH-FACT-013, D-146*

### 5.19 Registration steps

`age` · `email` · `phone` · `confirm` · `security` · `terms` · `about` · `preferences`
· `membership` · `done`, in that order, no Back (REG-SESS-002). The account exists from
the end of `terms`.

*Source: REG-SESS-002, D-146*

### 5.20 Capability residuals

The closed set a capability's `requires` may carry (API-CAP-001, AUTHZ-GATE-005):
`stepup` · `reauthenticate` (a downgraded session, AUTH-SESS-009) · `restricted`
(AUTHZ-GATE-006) · `consent` (PRIV-SENS-002, PRIV-CONS-007) · `accountstate`.

*Source: AUTHZ-GATE-005, D-153*

### 5.21 Consent and objection mechanism

The `mechanism` of a consent or objection record: `registration` (the terms step,
REG-SESS-007) · `dashboard` (`/privacy/consents/*`, `/privacy/objections/*`) ·
`reconsent` (the PRIV-CONS-007 prompt) · `administrator` (entered on the subject's
behalf).

*Source: PRIV-CONS-001, PRIV-RIGHT-001a, D-153*

### 5.22 Age group

Recorded by the age screen when `registration.adultaffirmation` = `off`: `minor` ·
`adult`. Finer bands are host work when a host needs them.

*Source: REG-PROF-002, D-153*

### 5.23 Alert conditions

One identifier per OPS-ALERT-001 row, carried by `AlertRaised` and used as the
deduplication key of OPS-ALERT-002: `auth-failures-sustained` · `recovery-clustering`
· `approver-volume` · `read-volume-anomaly` · `breakglass-used` ·
`protected-setting-changed` · `alert-destination-changed` · `stepup-policy-weakened`
· `concurrent-sessions-implausible` · `denial-spike` · `sms-balance` ·
`background-job-failed` · `erasure-delivery-exhausted` · `certificate-renewal-failed`
· `clock-drift` · `degradation` · `callback-verification-failed` ·
`nonexistent-notice-rate` · `privacy-deadline-approaching` ·
`privacy-deadline-reached` · `expiry-approaching` · `holiday-list-exhausted` ·
`no-emergency-credential` · `restore-test-failed` · `restriction-loosened` ·
`restriction-granted` · `domain-reverification-failed` · `domain-removed` ·
`governing-language-changed` · `governing-text-missing` · `relay-domain-unregistered`.

*Source: OPS-ALERT-001, OPS-ALERT-002, D-153*

---

## 5a. Step-up actions — library-owned

The actions on the library's own surface that require step-up under the principal's
policy (AUTH-STEP-001, AUTH-STEP-002). Listed once here so a builder does not have to
infer them endpoint by endpoint; `09` marks each.

| Name | Action | Endpoint(s) |
|---|---|---|
| `password:set` | Set or change a password | `POST /account/password` |
| `identifier:add` | Add an identifier; replace in single-address mode | `POST /account/identifiers`, `PUT /account/identifiers/{id}/replace` (REG-IDENT-004, REG-IDENT-007); setting the primary or the backup setting needs no gate (REG-IDENT-005) |
| `identifier:remove` | Remove an identifier | `DELETE /account/identifiers/{id}` (REG-IDENT-006); the undo is link-borne and not gated |
| `username:change` | Change the username | `PUT /account/profile` with a `username` (REG-IDENT-009); display name and legal name are not gated |
| `factor:enrol` | Enrol a factor (AUTH-STEP-007); upgrade a security key to a passkey | `/account/factors/*`, `/auth/webauthn/register/*`, `POST /account/credentials/{id}/upgrade`; the label (`PATCH /account/credentials/{id}`) and the preferred second step (`PUT /account/secondstep/preferred`) are not gated |
| `factor:remove` | Remove an `active` factor | `DELETE /account/factors/{id}`, `DELETE /account/credentials/{id}` |
| `recoverycodes:generate` | Generate or regenerate recovery codes | `POST /account/recoverycodes` |
| `mailcredential:create` | Create a mail app password | `POST /account/mail/apppasswords` (REG-MAIL-002, INT-MAIL-010) |
| `mailcredential:revoke` | Revoke a mail app password | `DELETE /account/mail/apppasswords/{id}` |
| `privacy:export` | Export personal data | `GET /privacy/export` |
| `account:delete` | Request account deletion | `POST /account/delete` |
| `account:deactivate` | Deactivate the account (`suspendedBy = self`) | `POST /account/deactivate` (IDN-LIFE-013, D-147); reactivation at `POST /account/reactivate` accepts the link token from the deactivation notice and is not gated |
| `provider:link` | Link a social provider | `POST /account/link/{provider}` |
| `provider:unlink` | Unlink a social provider | `DELETE /account/link/{provider}` |
| `recovery:approve` | Approve a recovery | `POST /admin/recovery/approve` |
| `invitation:issue` | Issue an invitation | `POST /admin/organizations/{id}/invitations` |
| `grant:manage` | Grant, revoke or change roles and grants | `/admin/grants/*`, `/admin/roles/*` |
| `account:suspend` | Suspend an account | `POST /admin/accounts/{subject}/suspend` |
| `account:reactivate` | Reactivate a suspended account | `POST /admin/accounts/{subject}/reactivate` |
| `account:takedown` | Execute a takedown | `POST /admin/accounts/{subject}/takedown` |
| `account:takedownreverse` | Reverse a takedown | `POST /admin/accounts/{subject}/takedown/reverse` |
| `erasure:complete` | Complete a stuck erasure manually | `POST /admin/erasures/{id}/complete` |
| `config:loosen` | Loosen any security setting | `PUT /admin/config/{key}` where the change is a loosening (OPS-CFG-002); a tightening is not gated |
| `alerting:destinations` | Change alert destinations | `PUT /admin/config/{key}` for the `alerting.*.destinations` keys (OPS-ALERT-004a) |
| `policy:change` | Change an organization's policy | `PUT /admin/organizations/{id}/policy` |
| `domain:manage` | Add, verify or remove a locked domain | `/admin/organizations/{id}/domains/*` (REG-DOM-001) |
| `restriction:edit` | Edit a sending restriction | `PUT/DELETE /admin/restrictions/{name}` (AUTH-ABUSE-004): every edit; a loosening also needs a reason |
| `restriction:grant` | Grant sends to a key | `POST /admin/restrictions/{name}/grant` (AUTH-ABUSE-004): the support role, a reason required |
| `breakglass:replace` | Generate a replacement break-glass credential | from a break-glass session, which satisfies step-up for its lifetime (AUTH-STEP-004), or by a stepped-up system administrator from the management application (OPS-BOOT-004, D-147) |

The name is the key of the policy's `gates` field (section 4.1a) and the value the
`09` endpoint declares; it follows the `resource:action` shape of CONV-NAME-002 but is
a gate name, not a permission string (D-151).

*Source: AUTH-STEP-001, D-132, D-146, D-147, D-151*

Every gate is three values — level, phishing-resistance, maximum age
(AUTH-STEP-002). Under the system policy the level is **the account's reachable
assurance** (floor `aal1`), phishing-resistance is not required, and the maximum age
is `session.stepup.recency`; under the administrative organization's policy the
level is `aal2`, phishing-resistant (AUTH-STEP-002a, D-141). Any combination of the
account's usable factors that reaches the gate satisfies it; a person who cannot is
offered enrolment or a loss report, never refused.

## 5b. Emitted events — the public contract

The events the library emits, per LIB-API-001 ("Emitted events"), guarded by the
same contract tests. Each carries the subject reference, the event time, the acting
and effective identities where a person acted, and an idempotency key
(INT-MAIL-007). No event names a consumer or a consumer's domain (IDN-LIFE-003a).

| Event | Raised when | Consumers |
|---|---|---|
| `AccountRegistered` | The one transaction of the terms step has committed (REG-SESS-001, REG-SESS-007): the account exists, `active`, once per account; nothing fires for a registration session that is abandoned or expires | Host welcome — **no mailbox**: provisioning follows the invitation and enabling the membership (REG-MAIL-001) (INT-MAIL-006, IDN-LIFE-009b) |
| `TakedownReversed` | A takedown reversed inside its window | Host — the person is back; cancelled orders are not restored |
| `OrganizationErased` | An organization's grace window elapsed | Host, mail provisioning |
| `IdentifierChanged` | *Retired by D-146. See `IdentifierAdded`, `IdentifierRemoved`, `IdentifierPrimaryChanged`.* | |
| `IdentifierAdded` | An added email or phone was verified and counts (REG-IDENT-004); also a replace that completed (REG-IDENT-007) | Host, mail provisioning |
| `IdentifierRemoved` | An identifier was removed (REG-IDENT-006); a later undo fires `IdentifierAdded` | Host, mail provisioning |
| `IdentifierPrimaryChanged` | The primary of a kind changed (REG-IDENT-005), including on invitation acknowledgement when the corporate address becomes primary (REG-INV-001) and when a membership ends (REG-MAIL-003) | Host, mail provisioning |
| `AccountSuspended` · `AccountReactivated` | State enters or leaves `suspended`, by the subject or an administrator | Mail provisioning, host |
| `AccountDeletionRequested` · `AccountDeletionCancelled` | The grace window starts or is cancelled | Host (hold fulfilment) |
| `ErasureRequested` | The erasure transaction has committed; host-side redaction is due (PRIV-RIGHT-005b) | Every registered subject-event handler — **required** |
| `RestrictionChanged` | `restricted` set or lifted | Every registered subject-event handler — **required** |
| `SendingRestrictionChanged` | A named restriction was created, edited or deleted through `/admin/restrictions/{name}` (AUTH-ABUSE-004); carries the restriction name, the actor and whether the change was a loosening. | Audit, alerting (OPS-ALERT-001) |
| `SendingRestrictionGranted` | Support added credit to one key under a restriction (AUTH-ABUSE-004); carries the restriction name, the credit, the actor and the reason, never the plain key value | Audit, alerting (OPS-ALERT-001) |
| `DeviceVerified` | A new-device check completed (AUTH-FACT-016); carries the browser identifier and no personal data | Host (optional) |
| `ExportRequested` | A subject export is assembled | Every registered subject-event handler — **required** |
| `TakedownExecuted` | Phase one of a takedown has committed — fires with `AccountSuspended`; `AccountDeletionRequested` does **not** fire for a takedown | Host (cancel open orders) |
| `MembershipChanged` | A membership begins or ends | Mail provisioning, host |
| `ConsentChanged` | A consent granted, withdrawn or superseded; on withdrawal, handlers erase data held solely for the purpose (PRIV-CONS-008) | Every registered handler for the purpose — **required** |
| `ObjectionChanged` | An objection recorded or withdrawn for a purpose on an objectable basis (PRIV-RIGHT-001a) | Every registered handler for the purpose — **required** |
| `CredentialEnrolled` | An authenticator reached `active` — with the enrolment notification to every other recorded channel (AUTH-STEP-007) | Host (optional) |
| `CredentialSuspended` · `CredentialRestored` · `CredentialInvalidated` | A loss report started, was cancelled, or completed after the window (AUTH-RECOV-007) | Host (optional) |
| `NotificationRequested` | The library needs a message delivered — verification, recovery, deletion, change notifications, alerts | The notification transport (INT-MAIL-008); retried by the outbox |
| `AlertRaised` | An OPS-ALERT-001 condition fires; carries the condition identifier (section 5.23), the severity and the structured details of the row | Alert channels |

*Source: LIB-API-001, IDN-LIFE-003a, PRIV-RIGHT-005b, D-022, D-132, D-141, D-146*

## 6. Status code usage

| Code | Used for |
|---|---|
| 200 | Success with a body |
| 202 | Accepted, outcome deliberately not disclosed |
| 204 | Success, no body |
| 400 | Malformed request |
| 401 | No valid session — **session death only** |
| 403 | Authenticated, not permitted, existence not concealed; **also step-up required** (D-092) |
| 404 | Not found, **or** concealed denial |
| 409 | Conflict — duplicate, or state precondition failed |
| 422 | Well-formed, semantically rejected |
| 429 | Throttled, carries `Retry-After` |
| 500 | An unhandled fault: `system.fault` with the correlation identifier and nothing else (BFF-ERR-002, D-153) |

*Source: API-CONV-003*

---

## 7. Maintenance

**REF-001**: Adding an error code, permission, or configuration key SHALL update
this document in the same change.

*Source: LIB-API-001, CONV-NAME-003*

**Acceptance criteria**
1. A contract test fails when a code or key exists in source but not here.
2. A contract test fails when a code changes without a major version bump.
