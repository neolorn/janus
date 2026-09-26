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
| `identity.affirmation.required` | The affirmation record derived at the age step is absent at the terms step; 422 | REG-PROF-002, REG-SESS-007, D-146 (was IDN-LIFE-002), D-166 |
| `identity.change.pending` | A change of this kind is already in progress: a replace already staged for the identifier (REG-IDENT-007); 409 | IDN-LIFE-004, D-146, D-166 |
| `identity.identifier.primary` **(new)** | Removal refused: the identifier is the primary of its kind; set another primary first; 409 | REG-IDENT-006, D-146, D-166 |
| `identity.identifier.lastofkind` **(new)** | Removal refused: it would leave fewer than the required minimum of that kind (one verified email always; one verified phone while `registration.phone` is `required`); 409 | REG-IDENT-001, REG-IDENT-006, D-146, D-166 |
| `identity.identifier.domainnotallowed` **(new)** | The email's domain is outside the organization's verified `emailDomains` list. At sign-in it is told only after a factor succeeds; at an invitation's issue it carries no `details.member`; 422 | REG-DOM-001, IDN-ORG-006, D-146, D-166 |
| `identity.identifier.invalid` | The identifier is not one the operation takes: the value is not a well-formed identifier of a kind the operation accepts; a corporate address equals the personal email the same request names (REG-MAIL-001); or the identifier named is not one the account or the registration holds in the state the operation needs (of that kind; in single-address mode, for a replace). An unverified identifier made primary or named by the backup setting is `identity.identifier.unverified`, not this code. Where a body carries more than one identifier, `details.member` names the member; 422 | REG-IDENT-001, REG-IDENT-004, REG-IDENT-005, REG-IDENT-007, REG-INV-001, REG-MAIL-001, D-162, D-166 |
| `identity.identifier.unverified` | An unverified identifier is made primary of its kind or named by the kind's backup setting (`POST /account/identifiers/{id}/primary`, `PUT /account/identifiers/backup`); nothing changes; 409 | REG-IDENT-005, D-166 |
| `identity.identifier.locked` | The identifier is locked and nothing about it is the person's to change: an invitation bound it, a provider operates its mailbox, or it is the personal email a membership keeps, which is not removed, made primary or replaced while the membership lasts; 409 | REG-IDENT-010, REG-MAIL-001, D-162, D-166 |
| `identity.identifier.maximum` | The account or the registration already holds `identifiers.<kind>.max` identifiers of the kind, or an invitation's corporate address would pass `identifiers.email.max` at its acknowledgement; where the maximum is one, the change is `PUT /account/identifiers/{id}/replace`; 409 | REG-IDENT-002, REG-IDENT-007, REG-INV-001, D-162, D-166 |
| `identity.invitation.identifiermismatch` **(new)** | At the acknowledgement, an email or phone the invitation binds is not verified on the accepting account, or the corporate address is already held by an account; at the press that begins a registration, an account already holds the bound email; 422 | REG-INV-001, REG-INV-002, D-146, D-166 |
| `identity.username.taken` **(new)** | The username belongs to another account, or is held after an erasure for `retention.consent`; disclosed by design, throttled per source; 409 | REG-IDENT-009, D-146, D-166 |
| `identity.username.reserved` **(new)** | The username is on the reserved list; 409 | REG-IDENT-009, D-146, D-166 |
| `identity.username.invalid` | The username fails the PRECIS UsernameCaseMapped profile, its length bounds, or holds no letter; 422 | REG-IDENT-009, D-155 |
| `identity.username.coolingoff` **(new)** | A second username change inside `identifiers.username.changecooloff`; `details` carries the cooling-off end; 409 | REG-IDENT-009, D-146, D-166 |
| `identity.profile.underage` **(new)** | The date of birth is under eighteen on a host with `registration.adultaffirmation` = `required`; the registration session ends; 422 | REG-PROF-002, D-146, D-166 |
| `identity.profile.invalid` | A profile value the library does not admit: a display name that is not a PRECIS Nickname of 1 to 64 bytes, or a legal name outside 1 to 200 Unicode scalar values; 422 | REG-PROF-001, D-162, D-166 |
| `identity.profile.notaccepted` | A profile field the deployment does not take from the person: a `legalName` while `profile.legalname` is `off`, a `username` while `identifiers.username.enabled` is off, or any `dateOfBirth`, which is corrected through support; 422 | REG-PROF-001, REG-IDENT-009, D-162, D-166 |
| `identity.preference.undeclared` **(new)** | A preference key the host did not declare at startup; 422 | REG-PREF-001, D-146, D-166 |
| ~~`identity.identifier.duplicate`~~ | **Withdrawn (D-076)** — returning it would disclose account existence. Duplicate registration returns the ordinary accepted response |
| `identity.identifier.mixedscript` | Scripts mixed within a single word; on an invitation's issue `details.member` names the member; 422 | IDN-ACCT-005, D-166 |
| `identity.link.lastcredential` | Cannot unlink the only remaining credential; 409 | IDN-LIFE-012, D-166 |
| `identity.membership.limitreached` **(new)** | A membership would be created, an invitation's acknowledgement included, for an account that already holds a current one while `organization.multiplememberships` is off, or for an organization the account already holds a current membership of, whatever the setting; in the second case `details.organization` names it. A membership that has ended is not counted; 409 | IDN-MEM-002, D-166 |
| `identity.membership.notfound` | The account named holds no current membership of the organization; 404 | IDN-MEM-001, D-166 |
| `identity.organization.protected` **(new)** | The administrative organization cannot be deleted; 409 | IDN-ORG-004, D-166 |
| `identity.organization.notfound` | A path under `/admin` whose `{id}` names no organization the deployment holds (`/admin/organizations/{id}/...`); refused before anything is written; 404 | IDN-ORG-003, IDN-MEM-001, API-CONV-003, D-166 |
| ~~`identity.account.restricted`~~ | *Retired by D-162. A restricted account's refused action answers `authz.restricted` (AUTHZ-GATE-006).* | |
| `identity.account.notfound` | An administrative operation names a subject no account bears; 404 | IDN-LIFE-013, IDN-LIFE-003, PRIV-RIGHT-004, IDN-ATTR-003, D-166 |
| `identity.account.stateconflict` | The operation does not apply to the state the account is in: a takedown of a `deleted` account, a suspension of one `deleting` or `deleted`, a reactivation of one no administrator suspended, a restriction lifted from one not `restricted`, a cancellation where no grace window runs. `details.state` names the state (section 5.1) and, where it is `suspended`, `details.suspendedBy` the origin (section 5.12b); 409 | IDN-ACCT-007, IDN-LIFE-013, IDN-LIFE-003, PRIV-RIGHT-004, D-166 |
| `identity.takedown.windowelapsed` **(new)** | Takedown reversal requested after its window, whether or not the account has been erased; 422 | IDN-LIFE-003, D-127, D-166 |
| `identity.change.windowelapsed` **(new)** | Undo of an identifier removal or replace presented after `identifier.change.coolingoff`; 422 | REG-IDENT-006, REG-IDENT-007, D-140, D-146 (was IDN-LIFE-007, IDN-LIFE-010), D-166 |
| `identity.takedown.active` **(new)** | A deletion cancellation, or a second takedown, attempted on a takedown-originated `deleting`; use `/takedown/reverse`; 409 | IDN-LIFE-003, D-137, D-166 |
| `identity.takedown.notfound` | The account holds no takedown to read or reverse (`GET /admin/accounts/{subject}/takedown`, `POST .../takedown/reverse`); 404 | IDN-LIFE-003 AC2, D-166 |
| `identity.deletion.windowelapsed` **(new)** | The deletion grace window of the account or the organization has closed; cancellation is no longer possible; 422 | IDN-ACCT-007, IDN-ORG-003, D-106, D-166 |
| `identity.invitation.expired` | An invitation token that opens no invitation (never issued, revoked, already used, or past its lifetime); at the acknowledgement, an invitation revoked, already acknowledged, past its lifetime, whose organization's deletion was requested or which was erased, or whose inviter no longer holds what issuing it required; at revocation, one already acknowledged; 422 | IDN-LIFE-009a, REG-INV-001, D-106, D-147, D-166 |
| `identity.invitation.notfound` | No standing invitation answers: `GET /account/invitation` or the acknowledgement names none attached to the signed-in account (one attached to another account is answered alike), or a revocation names one the organization did not issue; 404 | REG-INV-002, IDN-LIFE-009a, D-166 |
| `identity.invitation.addressrequired` | An invitation into an organization whose mail is integrated names no personal `email`, no `corporateEmail`, or the corporate address as the personal one; `details.member` names `email` or `corporateEmail`; 422 | REG-INV-001, REG-MAIL-001, D-166 |
| `identity.invitation.mailboxheld` | An invitation names a corporate address whose mailbox has been held before, by anyone (the invitee and an erased holder included), and its body carries no `formerMailbox` (section 5.44); the issue checks nothing about who the invitee is, and no mailbox changes; 409 | REG-MAIL-001, REG-MAIL-003, INT-MAIL-006, D-166 |
| `identity.mailbox.taken` | The corporate address an invitation asserts is held by a member, or reserved by a standing invitation that has not expired; `details.member` is `corporateEmail`; 409 | REG-MAIL-001, INT-MAIL-006, D-166 |
| `identity.mailbox.notfound` | The account holds no mailbox the mail server is told to enable, or the deployment registers no mail server; the app-password endpoints are not present for it; 404 | INT-MAIL-006, REG-MAIL-002, D-166 |
| `identity.registration.signedin` | `POST /register` from a browser holding a live session: nothing is staged, no account document is answered, and the frontend sends the person to the account application; where the request carries an invitation token, the invitation is first attached to the signed-in account (REG-INV-002); 409 | REG-SESS-002, D-162, D-166 |
| `identity.registration.incomplete` | A registration request for a step the session has not reached: its predecessor is incomplete, or it is complete already; a confirmation (`POST /register/confirm`) while a staged identifier is unverified; also a session that reaches account creation without its terms and notice versions; 409 | REG-SESS-002 AC1, REG-SESS-004, REG-SESS-007, D-162, D-166 |
| `identity.reactivation.tokeninvalid` | Reactivation link token unknown, expired or consumed (`POST /account/reactivate`); 422 | IDN-LIFE-013, D-147, D-166 |
| `identity.photo.invalid` | Upload failed content validation; 422 | IDN-ATTR-004, D-106, D-166 |
| `identity.photo.toolarge` | Upload exceeds the size or dimension limit; 422 | IDN-ATTR-004, D-106, D-166 |
| `identity.photo.notenabled` | The policy in force for the account does not enable photos (`photos`, section 4.1a); 403 | IDN-ATTR-002, D-106, D-166 |
| `identity.photo.notfound` | No photo is shown for the account: none is set, or an organization it belongs to does not enable photos; answered alike at the account's own read and at an administrator's; 404 | IDN-ATTR-002, IDN-ATTR-003, D-147, D-166 |
| `identity.domain.unverified` | Verification of a listed domain found no TXT value at `_identity-verify.<domain>` equal to `identity-domain-verification=<token>`, or the lookup could not be made; nothing is written. A domain is never listed where no DNS resolver is declared (section 4.1a); 422 | REG-DOM-001, D-153, D-163, D-166 |
| `identity.domain.notfound` | A verification names a domain the organization does not list: never listed, or removed; 404 | REG-DOM-001, D-166 |
| `identity.account.adminsuspended` **(new)** | Self-reactivation attempted on an administratively suspended account; moved here from section 1.2 (D-148); 409 | IDN-LIFE-013, D-135, D-166 |
| `identity.preference.wrongtype` | A preference value of a type other than its declaration; 422 | REG-PREF-001, D-153 |
| `identity.preference.toolarge` | The preference set would exceed `preferences.maxsize`; 422 | REG-PREF-001, D-153 |
| `identity.preference.administratoronly` | The person set a preference declared administrator-only; 422 | REG-PREF-001, D-153 |

### 1.2 Authentication

| Code | Meaning | Source |
|---|---|---|
| `auth.code.expired` | A verification code or an authentication code (the `emailCode` code, the `phoneCode` second-step code, the code a sign-in link shows in another browser) past its lifetime, or presented after its attempt cap, the correct code included (AUTH-FACT-004 AC3); 422 | AUTH-FACT-004, D-166 |
| `auth.code.invalid` | A verification code or an authentication code rejected; 422 | AUTH-FACT-004, D-166 |
| `auth.code.replayed` **(new)** | Code already consumed within its window; 422 | AUTH-FACT-005, D-166 |
| `auth.factor.notpermitted` | A factor the account may not use here: a verification-only factor offered as authentication, or a factor the policy in force does not permit in `loginFactors`, presented at sign-in or at step-up; 422 | AUTH-FACT-002, AUTH-STEP-002, IDN-LIFE-009b, D-166 |
| `auth.factor.rejected` | Factor presented and refused; 422 | AUTH-FACT-001, D-166 |
| `auth.factor.required` **(new)** | Further factors needed to reach required assurance; 422 | AUTH-FACT-001, D-166 |
| `auth.lossreport.notpermitted` | Self-service loss report and removal unavailable to this account; 409 | AUTH-RECOV-008, D-141, D-166 |
| `auth.lossreport.pending` **(new)** | This authenticator is already reported lost; `details.invalidatesAt`; 409 | AUTH-RECOV-007, D-141, D-166 |
| `auth.credential.suspended` **(new)** | A reported-lost authenticator was presented; 422 | AUTH-RECOV-007, D-141, D-166 |
| `auth.credential.notfound` | The account holds no active credential by that identifier of the kind the operation acts on, including an app password the mail server does not hold for the person; a credential of another account answers the same; 404 | AUTH-FACT-001, INT-MAIL-010, D-162, D-166 |
| `auth.credential.labelinvalid` | A credential or app-password label that is empty, over 64 characters, or already held by another credential of the same kind on the account in any capitalisation (OPS-DB-001); 422 | AUTH-FACT-001, REG-MAIL-002, D-162, D-166 |
| `auth.credential.notupgradable` | The credential named at `POST /account/credentials/{id}/upgrade` is the account's and is not a second-factor security key; 409 | AUTH-FACT-002b, D-162, D-166 |
| `auth.password.blocklisted` | Password found in the compromised corpus; 422 | AUTH-PASS-004, D-166 |
| `auth.password.tooshort` | Below the floor for this factor count; 422 | AUTH-PASS-001, D-166 |
| `auth.password.toolong` | A password longer than `password.maximum` at registration, change or reset; nothing is truncated; no details; 422 | AUTH-PASS-001 AC3, D-162, D-166 |
| `auth.recovery.channelnotonaccount` | Channel not among the account's recorded channels; 422 | AUTH-RECOV-003, D-166 |
| `auth.recovery.reasonrequired` | Written reason absent; 422 | AUTH-RECOV-002, D-166 |
| `auth.recovery.tokenexpired` | Recovery link past its lifetime; 422 | AUTH-RECOV-002, D-166 |
| `auth.recovery.tokeninvalid` | Recovery link rejected; 422 | AUTH-RECOV-002, D-166 |
| `auth.recovery.selfapproval` **(new)** | An approver attempted to approve recovery for their own account; enforced in the domain; 422 | AUTH-RECOV-002a, D-147, D-166 |
| `auth.screening.unavailable` **(new)** | Blocklist screening could not run; operation refused; 422 | AUTH-PASS-004, D-166 |
| `auth.session.expired` | Session past idle or absolute limit; `details.reauthenticate` is `single-factor` when one factor (passkey or password) restores the session — idle expiry inside the absolute window, **AAL2-policy principals only** — or `full`; the break-glass session answers `full` on any expiry (OPS-BOOT-002); 401 | AUTH-SESS-005, D-123, D-139, D-166 |
| `auth.session.csrfinvalid` **(new)** | A state-changing browser request refused by a layer of `17` section 4: fetch metadata, the custom request header, the origin or the synchronizer token; one code for every layer, which only the log entry names; also a sign-on or provider round trip whose `state` is absent, unbound or not the one the browser was sent out with (BFF-SESS-006, IDN-LIFE-012); 403 | AUTH-SESS-007, BFF-CSRF-001 to BFF-CSRF-004, D-162, D-166 |
| `auth.stepup.required` **(new)** | The gate is not met; `details` carries `required` (`level`, `phishingResistant`, `maxAge` in whole seconds), `outcome` (`present` · `enrol` · `report-loss` · `pending`), `options` (the combinations of catalogue identifiers that would meet it) and `pendingUntil`, null unless the outcome is `pending`. At the invitation acknowledgement `details` carries `outcome` `enrol` and `policyRequirement` `{ field, value }` in place of the gate values. No `details` where no session of the library belonging to the acting account was judged (`09` `/auth/step-up`); 403 | AUTH-STEP-001, AUTH-STEP-002, D-141, D-166 |
| `auth.stepup.unavailable` **(new)** | No assurance provider registered; 403 | AUTH-STEP-003, D-166 |
| `auth.throttled` **(new)** | A progressive delay or a rate limit is in effect; `details.retryAt` is the instant the next attempt is looked at, and the answer carries `Retry-After`; a throttled navigation (a social provider's return) carries `error=auth.throttled` and `retryAt` in its query instead (BFF-ABUSE-001); 429 | AUTH-ABUSE-001, AUTH-ABUSE-002, AUTH-RECOV-002, OPS-ALERT-006, D-166 |
| `auth.restriction.exceeded` **(new)** | A send refused by a named restriction; `details.retryAt` is the earliest time a bucket lifts, identical whether or not the address is registered (AUTH-ABUSE-002). Replaces `integration.sms.windowactive`; 429 | AUTH-ABUSE-004, D-146, D-166 |
| `auth.restriction.notfound` | The restriction named in the path is not in the set (`GET` and `DELETE /admin/restrictions/{name}`, `POST /admin/restrictions/{name}/grant`); 404 | AUTH-ABUSE-004, D-166 |
| ~~`auth.device.verificationrequired`~~ | *Retired by D-166. The held sign-in is the `status` value `deviceVerificationRequired` of `POST /auth/factor` (section 5.33), answered 200, not a code.* | |
| `auth.policy.graceexpired` **(new)** | The account does not meet a raised requirement and `policy.enforcement.grace` has elapsed; sign-in stops at enrolment. `details.outcome` is `enrol`; 403 | AUTH-FACT-017, D-146, D-166 |
| `auth.enrolment.tokeninvalid` **(new)** | Enrolment link token unknown or consumed; 422 | AUTH-RECOV-002, IDN-LIFE-009a, D-140, D-166 |
| `auth.breakglass.invalid` **(new)** | Emergency credential rejected: malformed, a check symbol that does not hold, or any code but the issue last used, a replaced issue included; 422 | D-065, OPS-BOOT-004, D-166 |
| `auth.credential.lastsecondfactor` | Removing this credential would lower the account's reachable assurance; it is suspended now and invalidated after the window (AUTH-RECOV-007). A 202 status, not a refusal: `details.invalidatesAt` carries the instant of invalidation. In process the removal answers it as the failure outcome of its `Result` (CONV-DESIGN-005) | D-092, D-135, D-141, D-166 |
| `auth.breakglass.consumed` **(new)** | The code of the issue last used, presented again, the second of two concurrent uses included; 409 | D-065, OPS-BOOT-002, D-166 |
| `auth.webauthn.algorithmnotallowed` | Signature algorithm outside the allow-list; 422 | AUTH-FACT-014, D-166 |
| `auth.webauthn.countermismatch` **(new)** | Signature counter moved backwards — possible cloned credential; 422 | AUTH-FACT-014, D-166 |
| `auth.webauthn.rpidchanged` **(new)** | Credential enrolled under a different relying party identifier; 422 | AUTH-FACT-011, D-166 |
| `auth.webauthn.userverificationrequired` | User verification did not occur; 422 | AUTH-FACT-014, D-166 |
| `auth.challenge.required` | A bot-defence signal fired and the host declared a challenge verifier; the step completes only with a passing challenge token (AUTH-ABUSE-008); 403 | AUTH-ABUSE-008, D-153, D-166 |
| `auth.oidc.nonconformant` | A conformance finding, raised by no request: the provider admitted a form AUTH-OIDC-006 retires, a pushed request naming a destination other than the client's registered one included, or its discovery document lists one. `details` carry `probe` (`discovery` · `push` · `token`), `field`, `sent`, `expected`, `status` and `error`, or, for the document, `probe`, `member` and `listed` | AUTH-OIDC-006 AC1, LIB-TEST-001, D-164, D-166 |

### 1.3 Authorization

| Code | Meaning | Source |
|---|---|---|
| `authz.denied` **(new)** | Permission absent; used where existence is not concealed. Under `/admin`, an identifier naming no row whose organization the permission is asked in (a group, a grant, a registered record) is refused with it exactly as a missing permission is (`09` section 8); so are the refusals OPS-BOOT-002 gives on the reserved account, and bootstrap or a key rotation run where it may not; 403 | AUTHZ-CONCEAL-005, OPS-BOOT-001 AC1, OPS-SEC-003 AC1, D-166 |
| `authz.grant.duplicate` | An identical live grant exists; 409 | AUTHZ-GRANT-003, D-166 |
| `authz.grant.expired` **(new)** | A grant written with an expiry that is not after the present; 409 | AUTHZ-GRANT-003, D-166 |
| `authz.grant.notfound` | A revocation names a grant already revoked, or one a derivation wrote, and the caller holds `grant:manage` where it is scoped; an identifier naming no grant is `authz.denied` (`09` section 8); 404 | AUTHZ-GRANT-001, AUTHZ-SCOPE-001, D-166 |
| `authz.grant.unresolved` | A grant names a role the deployment does not hold, or a group that does not exist or belongs to another organization; `details.member` names `role` or `subjectId`. Also an invitation whose `roles` names a role the deployment does not hold, `details.member` `roles`; 422 | AUTHZ-GRANT-001, REG-INV-001, D-166 |
| `authz.grant.reasonrequired` | A grant created or revoked without a non-empty `reason`; 422 | AUTHZ-GRANT-003, D-153 |
| `authz.role.inuse` | A grant (live, expired or revoked), a declared derivation or a standing invitation names the role, so it is not removed; its permissions can be changed instead; 409 | AUTHZ-GRANT-004, AUTHZ-GRANT-003 AC3, AUTHZ-DERIVE-001, REG-INV-001, D-166 |
| `authz.role.notfound` | A role named in the path (`DELETE /admin/roles/{name}`) is not one the deployment holds; a role a body names that the deployment does not hold is `authz.grant.unresolved` (422); 404 | AUTHZ-GRANT-004, D-166 |
| `authz.group.cycle` **(new)** | Adding the member would make a group contain itself; 409 | AUTHZ-GROUP-001, D-106, D-166 |
| `authz.group.inuse` | The group holds a member, belongs to a group, or was given a grant (live, expired or revoked), so it is not removed; 409 | AUTHZ-GROUP-001, AUTHZ-GRANT-003 AC3, D-166 |
| `authz.policy.unregistered` **(new)** | Entity has no registered policy: a fault, not a denial, answered as `system.fault` (500). The conformance suite reports such an entity under this code with `details.entity` | AUTHZ-GATE-001, LIB-TEST-001, D-166 |
| `authz.restricted` | The subject's processing is restricted: a modifying action, or a change to the account's identifiers, credentials, profile or preferences, or an invitation acknowledgement, asked by a restricted account, the creation of a mail app password included. What IDN-ACCT-007 keeps available is admitted: ending sessions, reporting a credential lost, listing and revoking app passwords, the link-borne undo of an identifier change, and a credential set by recovery or enrolled where a policy hold stops its sign-in (AUTH-FACT-017); 403 | AUTHZ-GATE-006, IDN-ACCT-007, INT-MAIL-010, D-166 |
| `authz.resource.notfound` | One not-found answer where nothing is to be told, meaning "nothing here" to the frontend whatever the case: a request in which the gate refused a record of a type that conceals its records, whether or not the record exists and whatever the endpoint wrote after the refusal, answered by the browser profile's stage 11 and never by an operation, `details.correlation` the audit record of the refusal and nothing else; a record named on an `/account` route that is not the caller's (a device or a session of another account, or none), with empty `details`; a request under the mount that no endpoint serves, a path no endpoint matches or a method a matched path does not take, with empty `details` on both profiles; 404 | AUTHZ-CONCEAL-001, AUTHZ-CONCEAL-004, BFF-ERR-003, BFF-ORDER-001 stage 11, LIB-API-003 AC4, API-CONV-003, CONV-DESIGN-002, D-166 |
| `authz.derivation.sourcesmissing` | A check, capability query or explanation on a type that a non-materialised derivation is declared on or reaches through containment was made without the host-supplied sources, whatever the role the derivation confers allows; a fault, not a denial, answered as `system.fault` (500) | AUTHZ-DERIVE-001, D-161, D-162, D-166 |
| `authz.truthtable.disagreement` | A conformance finding, raised by no request: a case of the host's truth table that the single check or the list filter decides otherwise than the table states. `details` carry `type`, `scenario` (section 5.30), `permission`, `expected`, `check` and `filter`, and for a derived case `derivation`, the relationship | LIB-TEST-001 AC2, AUTHZ-TEST-001, D-166 |

### 1.4 Privacy

| Code | Meaning | Source |
|---|---|---|
| `privacy.consent.required` **(new)** | Processing attempted without recorded consent; 403 | PRIV-SENS-002, D-166 |
| `privacy.consent.superseded` **(new)** | Notice version no longer current; re-consent needed; 422 | PRIV-CONS-007, D-166 |
| `privacy.consent.writtenrequired` **(new)** | Sensitive data requires written consent; 422 | PRIV-BASIS-003, D-166 |
| `privacy.purpose.noconsent` | A consent is granted or withdrawn, at the dashboard or at the terms step, on a purpose the deployment did not declare or whose basis does not carry `IsConsent`; 422 | PRIV-BASIS-001, PRIV-CONS-011, REG-SESS-007, D-162, D-166 |
| `privacy.notice.unpublished` | A consent is granted, or an objection recorded, before any version of the document that governs its purpose is published (the privacy notice where the purpose names none); 409 | PRIV-CONS-001, PRIV-CONS-005, PRIV-CONS-007, PRIV-RIGHT-001a, D-162, D-166 |
| `privacy.document.notfound` | A legal document, or a named version of one, that does not exist or was never published is read, or has a translation filed against it, by a path naming it; a document an invitation's `documents` names that is not published is `api.request.invalid` (422); 404 | PRIV-CONS-005, PRIV-CONS-006, D-162, D-166 |
| `privacy.request.duplicate` **(new)** | An identical request is already open; 409 | PRIV-RIGHT-001, D-166 |
| `privacy.request.notfound` | A decision names no privacy request, told only to a caller holding `privacyrequest:manage`; 404 | PRIV-RIGHT-001, D-162, D-166 |
| `privacy.request.decided` | A decision on a privacy request already decided; the standing decision is not replaced; 409 | PRIV-RIGHT-002, D-162, D-166 |
| `privacy.purpose.notobjectable` **(new)** | The purpose's basis carries no right to object; 422 | PRIV-RIGHT-001a, D-145, D-166 |
| `privacy.notice.onelanguage` | *Retired by D-146. See `privacy.notice.governingtextmissing`.* | |
| `privacy.notice.governingtextmissing` **(new)** | A document version was submitted without its governing-language text; translations are optional and never suffice; 422 | PRIV-CONS-005, PRIV-CONS-006, D-146, D-166 |
| `privacy.request.receivedfuture` **(new)** | `receivedAt` later than now; 422 | PRIV-RIGHT-002, D-136, D-166 |
| `privacy.erasure.notfailed` **(new)** | Manual completion requested for a delivery (an erasure, a takedown or a restriction) that has not exhausted its retries; 409 | IDN-LIFE-003a, D-106, D-166 |
| `privacy.erasure.notfound` | An erasure is read at `/admin/erasures/{id}` and none is held under the identifier, the identifier of a delivery of another kind included, or a manual completion names no erasure, takedown or restriction delivery; 404 | IDN-LIFE-003a, IDN-LIFE-003b, D-166 |

### 1.5 Configuration and model

| Code | Meaning | Source |
|---|---|---|
| `config.key.protected` **(renamed)** | Setting is not changeable through the application; 422 | OPS-CFG-004, D-166 |
| `config.value.belowfloor` | Value below the enforced minimum; 422 | OPS-CFG-003, D-166 |
| `config.value.aboveceiling` | Value above the enforced maximum; 422 | AUTH-SESS-005, D-166 |
| `config.value.notallowed` | Value outside the key's enum or set, or of the wrong JSON type. For `restrictions`, a restriction whose name breaks the rule of section 4.5, whose `host:<name>` key has no registered supplier (`details.supplier`), or whose bucket list is empty. A system policy naming a locked domain (`details.field` `emailDomains`). A value whose feature needs a host declaration the deployment has not made: a policy's `photos` set to `true` without an image codec, or a domain added to a lock without a DNS resolver, `details.field` naming the field and `details.requires` the declaration (`imageCodec`, `dnsResolver`); 422 | section 4 value types, OPS-CFG-008, LIB-HOST-001, D-151, D-166 |
| `config.change.reasonrequired` **(renamed)** | A configuration change submitted without a reason: a key at `/admin/config/{key}`, an organization's policy or its locked domains, any edit of the restriction set, a tightening included, or a restriction grant; and a protected key set by `configure` from the server without `--reason`; `details.key` names the setting where one is changed; 422 | OPS-CFG-002, OPS-CFG-004, OPS-CFG-005, OPS-CFG-008, AUTH-ABUSE-004, D-146, D-166 (renamed from `auth.restriction.reasonrequired`) |
| ~~`config.change.stepuprequired`~~ | *Retired by D-166. A loosening without step-up is refused with `auth.stepup.required` (`09` `PUT /admin/config/{key}`).* | |
| `config.value.lastdestination` | A change would leave an `alerting.*.destinations` list empty; 422 | OPS-ALERT-004a, D-153 |
| `config.policy.belowsystem` **(new)** | An organization policy field the replacement states is looser than the system default; `details.field` names the first such field in the order of section 4.1a; a field the replacement leaves out is not judged; 422 | AUTH-STEP-002a, D-143, D-166 |
| `api.request.malformed` | The request could not be read: its body is not the shape the endpoint takes; a member it requires is absent or empty; a free-text member is outside 1 to 1024 characters after trimming (API-CONV-002; a blank reason whose absence has a code of its own answers that code); or a member or path segment holds a word outside the closed vocabulary this chapter or the declared model fixes for it (a takedown trigger outside section 5.12d, a configuration key the route does not serve, an undeclared permission). `details.member` names the member the reader stopped at, or the one the endpoint required, and carries nothing of its value; where the body failed before any member, the refusal carries the code alone. Also: an authorization request the provider refused and cannot return to a client, `details.error` then carrying the protocol's code and nothing else (LIB-API-003); a registration through `IResources` whose member is absent or unreadable, a sensitive type naming no `subject` included (AUTHZ-INHERIT-002, IDN-LIFE-002a); and, for a `Janus.Cli` command, a command, argument or input it cannot read, answered as one JSON line (OPS-BOOT-001, OPS-SEC-001, DR-016); 400 | API-CONV-002, API-CONV-003, D-162, D-166 |
| `api.request.invalid` | A well-formed request refused on its meaning where no more specific code of section 1 exists: a body naming something that does not exist or cannot be acted on (an invitation's `documents` naming a document not published, `details.member` `documents`; a group member that does not exist or belongs to another organization; a preferred second step naming a method the account has not enrolled, `details.member` `method`); a compliance record dated after now, or two licences under one identifier; a registration or move through `IResources` that the declaration or the library's records refuse. `details.member` names the member; 422 | API-CONV-003, REG-INV-001, AUTHZ-GROUP-001, IDN-ATTR-008, AUTHZ-INHERIT-002, IDN-LIFE-002a, OPS-MAINT-001, D-166 |
| `model.containment.cycle` **(new)** | Containment declaration forms a cycle | AUTHZ-MODEL-004 |
| `model.derivation.unindexed` **(new)** | Derivation names an unindexed column | AUTHZ-DERIVE-004 |
| `model.purpose.missingassessment` **(new)** | Legitimate interest declared without an assessment | PRIV-BASIS-002 |
| `model.type.noorganizationpath` **(new)** | Resource type has no path to an organization | AUTHZ-MODEL-004 |
| `model.type.reserved` | Startup: a host resource type takes the name `organization`, which the library reserves for the whole organization; `details.key` names it | AUTHZ-MODEL-002, AUTHZ-MODEL-004, D-166 |
| `model.purpose.hostingconsent` | Startup: a purpose named for the hosting or its cross-border transfer rests on consent: `hosting`, `transfer`, `hosting-transfer` or `cross-border-transfer`, compared ignoring case and every character other than a letter or a digit; the same purpose on another basis is accepted; `details.key` names `<type>.<purpose>` | INT-HOST-002, PRIV-CONS-010, D-166 |
| `model.startup.rpid` **(new)** | Startup: the relying party identifier is not a registrable suffix of a configured origin | AUTH-FACT-011, D-147 |
| `model.startup.labellimit` **(new)** | Startup: the configured origins exceed the five-label limit of related origins | AUTH-FACT-012, D-147 |
| `model.startup.schemamismatch` **(new)** | Startup: the database's migration history lacks a migration the build declares; `details.pending` lists the migrations owed; non-zero exit. A history holding migrations the build does not declare is not a mismatch (OPS-MIG-005) | OPS-MIG-002, D-147, D-166 |
| `model.startup.secretunavailable` **(renamed)** | Startup: a secret the library reads through the host's secret source (LIB-EXT-001) was not supplied, or was supplied empty or unusable: a key-encryption key or fingerprint key version (a fingerprint key version shorter than 32 bytes included), the maintenance credential, the mail server's secret where the library's mail-server adapter is used, or a social provider's credential, read by the provider's name; `details.key` names the secret: `keyEncryptionKeys`, `fingerprintKeys`, `maintenanceCredential`, `mailServerSecret` or `socialProvider.<provider>`. A `Janus.Cli` command gives it where its standard input is a terminal (`details.key` `input`) or the document it reads there holds no usable key (`details.key` naming the member). Also raised where a stored value is wrapped under a key version no longer held, `details.version` naming it | OPS-SEC-001, AUTH-KEY-002, OPS-MIG-003a, OPS-SEC-003, LIB-EXT-001, D-147, D-166 (renamed from `model.startup.kekunavailable`) |
| `model.startup.governinglanguage` **(new)** | Startup: `legal.governinglanguage` is unset | PRIV-CONS-005, LIB-HOST-001, D-147 |
| `model.startup.preferencedeclaration` **(new)** | Startup: a host preference declaration is malformed | REG-PREF-001, D-147 |
| `model.startup.declarationmissing` | Startup: a required deployment value or host declaration is absent: a key of section 4 that names the deployment, or a conditional one whose condition holds; a declaration of LIB-HOST-001 (the passkey pages, the authentication addresses, the sign-on client, the landing origins, the mail server client where a mail server is registered, and each mail and SMS transport the deployment does not take from the library); a subject-event handler (none covering a type declared sensitive), a purpose handler (none naming an objectable purpose) or a restriction key supplier; a declaration a stored value or the model needs (the image codec while a policy's `photos` is `true`, the DNS resolver while a domain is listed, the relationship source of every declared derivation, materialised or not). Or the model declares an item without what it requires: an encrypted field without its subject column, a type without its purposes, a purpose without its data categories, a category without its retention floor, a consent-based purpose on a type with no one subject column (`details.key` `<type>.<purpose>`). `details.key`, `details.handler` or `details.supplier` names it. The declarations' `details.key` spellings are `passkeyAddresses` (or its field), `authenticationAddresses.signIn`, `authenticationAddresses.provider`, `signOnClient.clientId`, `landingOrigins.authentication`, `landingOrigins.account`, `mailServerClient.clientId`, `mailTransport`, `smsTransport`, `imageCodec` and `dnsResolver`; a relationship source is named by its relationship | LIB-HOST-001, AUTHZ-MODEL-003, PRIV-PRIN-001, PRIV-RIGHT-005a, PRIV-RIGHT-005b, PRIV-RIGHT-001a, PRIV-SENS-002, IDN-ATTR-002, REG-DOM-001, AUTHZ-DERIVE-005, AUTHZ-DERIVE-007, D-153, D-162, D-166 |
| `model.startup.declarationinvalid` | Startup: a declaration is present but malformed: a name held to the rule of INT-SMS-003 that breaks it (a governing document's, a subject-event subscriber's: 1 to 64 lower-case letters and digits separated by single `.`, `-` or `_`); a text-message template naming a place section 5.26 does not list (`details.declaration` the message kind, `details.field` the place); an encrypted field or its subject column that is not a member of the declared type, or a subject column that is not of the library's subject identifier type; an encrypted field whose data category no purpose declared on its type names (`details.declaration` the type, `details.field` the field); a social provider declared twice or with a member outside its rule. `details.declaration` and `details.field` name it. A landing origin that is not the origin of a registered browser client's return address, or an authentication landing origin that is not the origin of the sign-in address, is refused with `details.key` `landingOrigins.authentication` or `landingOrigins.account` | LIB-HOST-001, INT-SMS-003, PRIV-PRIN-001, PRIV-RIGHT-005a, AUTHZ-MODEL-003, IDN-LIFE-012, D-166 |
| `model.startup.redirectclient` | Startup: a registered client's return address is not an absolute `https` address with a host (or `http` on a loopback IP literal), `details.client` naming the client; or `redirect.defaultclient` names no registered client of kind `browser-application`, `details.key` naming the key | API-REDIR-001 AC3, D-162, D-166 |
| `model.startup.subscribername` | Startup: two subject-event subscribers are registered under one name, or one under `erasure-ledger`, the name the library records the off-host ledger's confirmation under; `details.handler` names it | IDN-LIFE-003a, DR-016, D-166 |
| `model.rotation.notready` | A key rotation's seal (`rotate-kek --sealed`, `rotate-fingerprint-key --sealed`) was confirmed where no rotation of that kind awaits one, where the latest has retired, or while values stand under a previous version, a held username, an unlapsed reservation or an abuse ledger line that still counts included; `details.pending` counts them. The command exits 1 | OPS-SEC-003 AC3, AC4, AC6, D-166 |
| `model.type.undeclaredreference` | Startup: a resource type references a type that is not declared, or an action is bound to a purpose no resource type declares; `details.permission` names the action | AUTHZ-MODEL-004, AUTHZ-GATE-005, D-153, D-166 |
| `model.role.undeclaredpermission` | Startup: a role grants a permission that is not declared | AUTHZ-MODEL-004, D-153 |
| `model.derivation.undeclaredreference` | Startup: a derivation references a type or relationship that is not declared | AUTHZ-MODEL-004, D-153 |
| `system.fault` | An unhandled fault; 500, body per API-CONV-002 carrying the correlation identifier and nothing else | BFF-ERR-002, D-153 |

### 1.6 Integration

| Code | Meaning | Source |
|---|---|---|
| `integration.callback.rejected` **(new)** | A callback refused. 429 with `Retry-After`, the error carrying `details.retryAt`, where `integration.callback.ratelimit` refused it; 422 with no `Retry-After` for every other cause: signature, window, event identifier, source range, reference, confirmation, an unreadable or unheld delivery report, a provider event its keys do not verify. The Google security-event route answers a Security Event Token that fails validation as RFC 8935 section 2.3 does (400 with `err` and `description`), not with this code | INT-GEN-003, BFF-MACH-002, BFF-MACH-003, IDN-LIFE-012a, D-166 |
| `integration.callback.inprogress` | A delivery of an event whose earlier delivery is still being carried: its claim is unsettled and younger than `integration.callback.claimtimeout`. Not recorded as a rejection and not counted toward `alerting.callback.threshold`; 409 | BFF-MACH-002 AC3, D-166 |
| `integration.endpoint.insecure` **(new)** | Startup, or a change by `configure` (OPS-CFG-004): a key naming an endpoint the library calls (`integration.mail.endpoint`, `integration.sms.endpoint`, `integration.mailserver.endpoint`, `password.blocklist.selfhosted.address`) holds a value that is not an absolute `https` address; `details.key` names the key | INT-GEN-001, INF-TLS-004, D-162, D-166 |
| `integration.sms.windowactive` | *Retired by D-146. See `auth.restriction.exceeded` (AUTH-ABUSE-004).* | |
| `integration.sms.balancefloor` **(new)** | Gateway balance below the configured floor; 422 | INT-SMS-004, D-166 |

---

## 2. Permission strings

Format `resource:action` — lowercase, singular resource.

*Source: CONV-NAME-002, D-166*

### 2.1 Library-owned

Shipped by the library because they govern its own endpoints and operations
(LIB-API-005). Hosts do not declare these.

| Permission | Governs |
|---|---|
| `grant:read` | Viewing grants (`GET /admin/grants`) and the "who can access this?" view |
| `grant:manage` | Creating and revoking grants |
| `role:manage` | Reading, creating and changing roles, in the administrative organization |
| `group:manage` | Creating groups, nesting them, and changing their members (D-106); asked in the organization the group belongs to (AUTHZ-SCOPE-001) |
| `organization:manage` | Organization lifecycle, policy, and deletion cancellation; asked in the administrative organization, whatever organization the request names |
| `membership:manage` | Adding and removing members; issuing and revoking invitations, bound or open (IDN-LIFE-009a, REG-INV-001) |
| `domain:manage` | Adding, verifying and removing an organization's locked email domains (REG-DOM-001, IDN-ORG-006, D-146). Asked in the administrative organization. Adding and verifying a domain, and removing the last one, are loosenings under OPS-CFG-002 and also need `system:administer`. A domain is added only where a DNS resolver is declared (`emailDomains`, section 4.1a) |
| `restriction:edit` | Reading and editing the named restriction set (`GET/PUT/DELETE /admin/restrictions/{name}`, AUTH-ABUSE-004, D-146). Every edit is a step-up action and needs a reason; a loosening also needs `system:administer`, and alerts |
| `restriction:grant` | Granting credit to one key under a restriction (`POST /admin/restrictions/{name}/grant`, AUTH-ABUSE-004, D-146): the support role's permission, stepped up, audited with a reason |
| `account:manage` | Suspending and reactivating an account, lifting a restriction, cancelling a deletion on a subject's behalf (D-106) |
| `takedown:execute` | The minor takedown operation: the trigger, the reversal and the read of its progress (IDN-LIFE-003). Its own permission because of what it does |
| `recovery:approve` | Administrative re-enrolment |
| `session:revoke` | Emergency revocation of all sessions, system-wide |
| `session:revoke-account` | Revoking one account's sessions — offboarding, suspension |
| `config:read` | Reading configuration |
| `config:manage` | Changing configuration |
| `audit:read` | Reading the audit trail, querying it by subject, and resolving a concealed denial's correlation identifier (AUTHZ-GATE-004) |
| `ropa:read` | Generating records of processing |
| `privacyrequest:manage` | The data-subject request queue, erasure progress, and the manual completion path of an erasure, takedown or restriction delivery (IDN-LIFE-003a) |
| `notice:publish` | Publishing a compliance-text version (PRIV-CONS-006) |
| `compliance:manage` | Licence and permit dates, assessment references, and the declared human-input fields of the records of processing |
| `system:administer` | **Loosening** any runtime configuration (OPS-CFG-002), the named restriction set, the alert destinations, an organization's policy and its locked domains included, asked in the administrative organization beside the route's own permission; granting, revoking or changing a role that carries it, and changing the members of a group that holds one (OPS-CFG-007); generating the break-glass credential and reading whether one stands (`GET /admin/break-glass`, OPS-BOOT-001, OPS-BOOT-004). The protected settings in OPS-CFG-004 are not changeable through the application at all, by anyone |

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

All three are editable after bootstrap, except that `system-administrator` keeps every
library-owned permission while the reserved `emergency` account holds it (OPS-BOOT-002),
and that account's grant of it is not revoked; a change that would take one out, or a
revocation of that grant, is refused with `authz.denied`.

*Source: D-028, OPS-BOOT-001, OPS-BOOT-002, D-146, D-166*

---

## 4. Configuration keys

Part of the stable public contract (LIB-API-001). Grouped by area. **R** =
runtime-changeable, **P** = protected (not changeable through the application; changed from the server by `configure` (OPS-CFG-004) or by redeployment).

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
size in bytes is a bare integer with the binary unit stated in the Scope column. A value
already stored that does not read under its key's type is a fault, not a refusal: the
read throws and no default stands in for it (OPS-CFG-008, D-162).

**Direction (D-152).** OPS-CFG-002 needs a loosening direction for every key. Where a
row names one, that governs. The `restrictions` row governs by AUTH-ABUSE-004: a deleted
or renamed restriction, a changed key, a purpose changed to anything but `any`, a higher
maximum, a shorter interval, a `fixed` window where a `sliding` one stood, a removed
bucket and a channel changed to anything but `any` each loosen; a new restriction does
not (D-166). Otherwise a key with a ceiling and no floor loosens
upward, a key with a floor and no ceiling loosens downward, a boolean loosens away
from its default, and any other key (both bounds, neither bound, enum, list, set,
string) loosens on any change, as D-079b classifies settings with no direction.

**Every key carries a safe default (P-001) except the twelve that name the deployment**
— origins (`webauthn.origins`), hosting location (`hosting.location`), the two alert
destination lists, the owner's email and SMS destinations (D-129), the SMS balance
floor, the governing language of legal documents (`legal.governinglanguage`,
D-146), the calendar time zone (`privacy.calendar.timezone`), the message languages
(`notification.languages`), the email sending domain
(`notification.email.sendingdomain`) (D-153) and the hosting environment
(`hosting.environment`, PRIV-ROPA-001), which every deployment needs because every
deployment generates the records of processing (D-166). **Six further keys are
conditional**: `hosting.crossborderbasis`, required only when `hosting.location` is
`outside` (INT-HOST-002, D-147); `service.name`, required only when `context` is in
`password.blocklist.sources` (D-153); `password.blocklist.selfhosted.address`, required
only when `password.blocklist.source` is `selfHosted`; `integration.mail.endpoint` and
`integration.sms.endpoint`, each required only while the library's shipped transport for
it is registered (D-162); and `integration.mailserver.endpoint`, required only where the
library's mail-server adapter is used (D-166). Those are
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
| `privacy.export.ratelimit` | 3 per `P1D` | R, integer exports per account in the 24 hours ending now, sliding; past it 429 `auth.throttled` with `Retry-After` and `details.retryAt`, the instant the oldest counted export leaves the window | D-086, D-107, API-CONV-003, D-166 |
| `policy.enforcement.grace` | 0 | R, ceiling 90 d | AUTH-FACT-017, D-146: the run-up an account gets when a policy's `requiredAssurance` or `credentialRedundancy` is raised; during it a non-compliant sign-in is told the requirement and the deadline and may continue, after it the sign-in stops at enrolment (`auth.policy.graceexpired`). Lengthening is loosening. The one place the grace is defined |

### 4.1a The policy object

The one thing AUTH-PRIN-002 resolves per principal. Seven fields; an organization
stores only what it overrides. Every rule that says "where the policy requires AAL2"
reads `requiredAssurance` — nothing is inferred from which factors are enabled.

| Field | Meaning — read by | System default (customers) | Administrative organization, at bootstrap | Direction (OPS-CFG-002) |
|---|---|---|---|---|
| `requiredAssurance` | `aal1` · `aal2`. The stated floor — session lifetimes (AUTH-SESS-005), the staff floor (AUTH-SESS-005b), the trusted-device offer (AUTH-FACT-015), the single-factor idle restore (D-139) | `aal1` | `aal2` | lowering is loosening |
| `loginFactors` | The set of catalogue identifiers (`02` AUTH-FACT-002: `password`, `passkey`, `emailLink`, `emailCode`, `phoneLink`, `google`, `apple`, `totp`, `securityKey`, `phoneCode`, `recoveryCodes`) a principal may sign in with, primary and second; `breakGlass` and the verification code are not entries and cannot appear here (D-151). The entries `emailLink`, `emailCode`, `phoneLink` and `phoneCode` are **off by default** and a host enables them here (D-146, D-147); `phoneLink` and `phoneCode` are restricted factors (AUTH-FACT-002b) | `password`, `passkey`, `google`, `apple`, `totp`, `securityKey`, `recoveryCodes` (that is, the catalogue less `emailLink`, `emailCode`, `phoneLink`, `phoneCode`) | `passkey` only | adding is loosening |
| `gates` | Per §5a action, keyed by the action name: `level` (`aal1` · `aal2` · `reachable`), `phishingResistant`, `maxAge` (AUTH-STEP-002/002a); a gate a host names is not a key here and costs the strictest of this field's gates (AUTH-STEP-002) | `reachable`, floor `aal1` · no · `session.stepup.recency` | `aal2` · yes · `session.stepup.recency` | lowering a level, dropping phishing-resistance or lengthening `maxAge` is loosening |
| `credentialRedundancy` | `advisory` · `enforced` — whether a second credential is required after a device-bound enrolment (AUTH-RECOV-001) | `advisory` | `enforced` | to `advisory` is loosening |
| `selfServiceRecovery` | Whether email recovery and self-service loss reports are available (AUTH-RECOV-004, AUTH-RECOV-008) | `true` | `false` | to `true` is loosening |
| `emailDomains` | Domain lock (REG-DOM-001, IDN-ORG-006): `off`, or the list of domains the organization has listed; an address is admitted only in a listed domain **verified by DNS**, so a list holding only unverified domains admits no address, and a removed domain's addresses stay refused until it is listed anew. Written only through `/admin/organizations/{id}/domains`, never through `PUT .../policy` or `PUT /admin/config/policy.default`: the system policy's value is always `off`, and a system policy naming a domain is refused with `config.value.notallowed`, `details.field` `emailDomains`. Adding a domain where no DNS resolver is declared is refused with `config.value.notallowed`, `details.requires` `dnsResolver`, and startup refuses a listed domain without one (`model.startup.declarationmissing`). A listed domain is re-verified on a schedule | `off` | `off` | adding or verifying a domain, and removing the last one, is loosening (OPS-CFG-002); removing one alerts (OPS-ALERT-001) |
| `photos` | Whether the principal's account shows a profile photo (IDN-ATTR-002, IDN-ATTR-004). Set to `true` where no image codec is declared, it is refused with `config.value.notallowed`, `details.field` `photos`, `details.requires` `imageCodec`; startup refuses a stored `true` without one (`model.startup.declarationmissing`, LIB-HOST-001) | `false` | `false`: enabling it is an administrator's policy change once a codec is declared | to `true` is loosening |

An organization MAY tighten any field and SHALL NOT loosen below the system default
(AUTH-STEP-002a).

In a written policy (`policy.default`, `policy.<organization>`, and the `GET` of an
organization's policy) `emailDomains` is a list of domains in their ASCII form, and `off`
is the empty list.

*Source: AUTH-PRIN-002, AUTH-STEP-002a, D-116, D-141, D-143, D-146, D-162, D-166*

### 4.2 Passwords

| Key | Default | Scope | Source |
|---|---|---|---|
| `password.floor.singlefactor` | 15 | R, floor 15 (NIST SP 800-63B-4 SHALL) | AUTH-PASS-001, D-140 |
| `password.floor.withmfa` | 10 | R, integer, floor 8 (NIST minimum with MFA); SHALL NOT exceed `password.floor.singlefactor` | AUTH-PASS-001, D-140 |
| `password.maximum` | 128 | R, floor 64 | AUTH-PASS-001, D-146 (was 64) |
| `password.blocklist.source` | `rangeApi` | R; `rangeApi` · `offline` · `selfHosted` | AUTH-PASS-004: where the leaked-password list comes from (range API, offline fallback, self-hosted corpus) |
| `password.blocklist.sources` | `[leaked]` | R; set of `leaked` · `dictionary` · `context`; `leaked` cannot be removed | AUTH-PASS-004, D-146, D-166: the **rejection** sources. MAY add `dictionary` (the two word lists the package ships, English and Arabic transliteration, which a host extends by declaration, LIB-HOST-001, and never by a deployment file) and `context` (the person's own identifiers, profile fields and the service name); both off by default, a recorded deviation from NIST SP 800-63B-4 section 3.1.1.2 (`13-risk-register`). Adding a source is tightening |
| `password.blocklist.selfhosted.address` | none; **required when `password.blocklist.source` is `selfHosted`** | R, string, an absolute `https` address; startup refuses another with `integration.endpoint.insecure`, and a screening that reads one is refused with `auth.screening.unavailable` | AUTH-PASS-004, INT-GEN-001, D-162, D-166: where the deployment's own corpus answers the ranges the range API answers |
| `password.blocklist.corpusmaxage` | 30 days | R | AUTH-PASS-004, D-011, D-107, D-166: the age past which the offline leaked list answers no screening; a list whose date cannot be read is treated as past it |
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
| `recovery.ratelimit.account` | 3 per `P1D` | R, integer approvals given for one account in the day before the moment of asking, sliding; raising is loosening | AUTH-RECOV-002, D-153, D-166: recovery requests accepted for one account |
| `recovery.ratelimit.approver` | 5 per `P1D` | R, integer approvals one approver gave in the day before the moment of asking, sliding; raising is loosening | AUTH-RECOV-002, D-153, D-166: approvals one approver may give |
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
| `abuse.source.ratelimit` | 300 per `PT1M` | R, integer requests per minute per source (an IPv4 address or an IPv6 /64, AUTH-ABUSE-001), sliding, counted per instance; 429 `auth.throttled`; raising is loosening | BFF-ORDER-001 stage 4, D-153, D-166: the flood limit on every request before any expensive work; sized for many people behind one address |
| `abuse.source.sitelimit` | 3000 per `PT1M` | R, integer requests per minute per IPv6 /48, sliding, counted per instance; 429 `auth.throttled`; raising is loosening | BFF-ORDER-001 stage 4, AUTH-ABUSE-001, D-166: one site holds a /48 or a /56 (RFC 6177, RIPE-690), so walking its /64s is bounded at ten sources' worth |
| `abuse.botdefence.repeatedattempts` | 3 per `PT1H` | R, integer registration sessions per source per hour; raising is loosening | AUTH-ABUSE-008, D-153: the count behind the `repeatedAttempts` signal |
| `integration.callback.ratelimit` | 60 per `PT1M` | R, integer per source per minute, fixed window; 429 `integration.callback.rejected` before any lookup; raising is loosening | INT-GEN-003, BFF-MACH-003, D-153 |
| `integration.callback.claimtimeout` | `PT5M` | R, duration, floor `PT1M` | BFF-MACH-002, D-166: how long an unsettled claim on a host callback's event stands; a redelivery meeting a younger claim is answered 409 `integration.callback.inprogress`, and one meeting an older claim takes it over and is carried |
| `integration.mail.endpoint` | empty | **P**, string, an absolute `https` address; startup refuses another with `integration.endpoint.insecure` naming the key | INT-GEN-001, LIB-EXT-001, D-162, D-166: where the library's shipped mail transport calls; empty while the deployment registers a transport of its own |
| `integration.sms.endpoint` | empty | **P**, string, an absolute `https` address; startup refuses another with `integration.endpoint.insecure` naming the key | INT-GEN-001, INT-SMS-001, LIB-EXT-001, D-162, D-166: where the library's shipped SMS transport calls; empty while the deployment registers a transport of its own |
| `integration.mailserver.endpoint` | empty | **P**, string, an absolute `https` address, required only where the library's mail-server adapter is used; startup refuses another with `integration.endpoint.insecure` naming the key | INT-MAIL-001, INT-GEN-001, LIB-EXT-001, D-166: where the library's mail-server adapter reaches the mail server (JMAP at `<endpoint>/jmap`, INT-MAIL-001); while it is set and the host registers no `IMailServer`, the adapter is the deployment's mail server integration (LIB-HOST-001) |
| `restrictions` | the four shipped restrictions below | R, edited through `GET/PUT/DELETE /admin/restrictions/{name}` (step-up `restriction:edit`, a reason on every edit, OPS-CFG-008); a restriction's name is 1 to 64 lower-case letters and digits separated by single `.`, `-` or `_`, any other refused with `config.value.notallowed`; a loosening (the changes section 4's Direction paragraph names for this row: a higher max, a shorter interval, a removed bucket, a deleted restriction, a narrowed channel among them) falls under OPS-CFG-002, needs `system:administer` and raises a Normal alert | AUTH-ABUSE-004, OPS-CFG-008, D-146, D-166: the **named restriction set** governing every send except an alert, which answers to the deduplication of OPS-ALERT-002 alone. Each restriction is a key (§5.14), an optional purpose (§5.15), an optional channel (§5.15a) and one or more buckets of (max, interval, `sliding` · `fixed`, §5.16). Security notices to an existing holder answer only to restrictions whose purpose is `notification`. Replaces `abuse.sms.window`, INT-SMS-002 and IDN-LIFE-011 |
| `restrictions` · `sms.destination` | key `destination`, purpose `any`, channel `sms`, 3 per 24 h sliding | R, as above | AUTH-ABUSE-004, D-146, D-166 |
| `restrictions` · `sms.source` | key `source`, purpose `any`, channel `sms`, 10 per 1 h sliding | R, as above | AUTH-ABUSE-004, D-146, D-166 |
| `restrictions` · `email.destination` | key `destination`, purpose `any`, channel `email`, 5 per 1 h sliding and 1 per 60 s fixed | R, as above | AUTH-ABUSE-004, D-146, D-166: also bounds the new-device check code (AUTH-FACT-016) |
| `restrictions` · `notification.destination` | key `destination`, purpose `notification`, channel `any`, 5 per 24 h sliding | R, as above | AUTH-ABUSE-004, D-146, D-166: the only restriction that applies to security notices |
| `code.verification.lifetime` | 10 minutes | R, ceiling 30 min | AUTH-FACT-004, D-132, D-166: phone and email verification codes, including the new-device check code (AUTH-FACT-016); an authentication code is held to `code.signin.lifetime`, or to `link.magic.lifetime` where a sign-in link's page shows it |
| `code.verification.attempts` | 5 | R, ceiling 10 | AUTH-FACT-004, REG-SESS-003, D-146: wrong tries after which a verification code is invalidated and a correct one refused; a replacement draws on the restrictions |
| `code.signin.lifetime` | 10 minutes | R, ceiling 30 min | AUTH-FACT-002, AUTH-FACT-004, D-166: the life of the authentication codes that are sent, held apart from verification codes: the `emailCode` sign-in code (message kind `sign-in-code`) and the `phoneCode` second-step code (message kind `secondstep-code`). The code a sign-in link's page shows in another browser lives as long as its link (`link.magic.lifetime`) |
| `code.signin.attempts` | 5 | R, ceiling 10 | AUTH-FACT-004, D-166: wrong tries after which an authentication code (the `emailCode` code, the `phoneCode` code, the code a sign-in link shows) is invalidated; the correct code presented after them is refused with `auth.code.expired` |
| `link.magic.lifetime` | 15 minutes | R, ceiling 1 h | AUTH-FACT-003, AUTH-FACT-004, D-132, D-146, D-166: email and SMS sign-in links (`emailLink`, `phoneLink`, `POST /auth/link`), and the code a link's page shows where it is opened in another browser; the link completes only in the requesting browser and only on a press (REG-SESS-003) |
| `link.invitation.lifetime` | 7 days | R, ceiling 30 d | IDN-LIFE-009a, D-132 — single use |
| `photo.maxbytes` | 2097152 | R, integer bytes (2 MiB), ceiling 10485760 (10 MiB) | IDN-ATTR-004, D-132, D-152 |
| `photo.maxdimension` | 1024 | R, integer pixels on the longest side; larger images are downscaled | IDN-ATTR-004, D-132 |
| `abuse.sms.balancefloor` | — **required** | R, decimal in the currency the gateway reports | INT-SMS-004, D-153 |
| `abuse.sms.pollinterval` | `PT15M` | R | INT-SMS-004, AUTH-ABUSE-006, D-153: how often the gateway balance is read |
| `abuse.sms.drainfactor` | 3.0 | R, decimal, floor 1.0; raising is loosening | INT-SMS-004, D-153: the `sms-balance` alert fires when the last hour's spend exceeds this factor times the trailing seven-day hourly mean, or when the balance would reach `abuse.sms.balancefloor` within 24 hours at the current rate |
| `abuse.botdefence.signals` | `[datacenterRange, repeatedAttempts]` | R, set over the two members named; the set is closed until a decision adds a member; removing a member is loosening | AUTH-ABUSE-008, D-107, D-152 |
| `exfiltration.readvolume.alerting` | `true` | R | D-045 |
| `exfiltration.export.stepuprequired` | true | R | D-045, D-148, D-166: **staff bulk export only**. `/privacy/export` is not governed by this key; it is gated at the account's reachable assurance by AUTH-STEP-002a instead (D-141). Turning it off raises `stepup-policy-weakened` (OPS-ALERT-001) |
| `exfiltration.export.ratelimit` | 5 per hour | R | D-045, D-107 |
| `exfiltration.export.auditing` | `true` | **P** | D-045 |
| `alerting.email.destinations` | — **required** | R, **list** | D-048, D-071 |
| `alerting.sms.destinations` | — **required** | R, **list** | D-048, D-071 |
| `alerting.owner.enabled` | false | R | D-071, D-129, D-166: routine alerts to the owner; `breakglass-used` and `breakglass-generated` bypass it |
| `alerting.owner.email` | — **required** | R | OPS-BOOT-002, D-129 |
| `alerting.owner.sms` | — **required** | R | OPS-BOOT-002, D-129 |
| `alerting.destinationchange.notify` | *Retired by D-152. The notice to the previous destinations is non-suppressible (D-083, OPS-ALERT-004a); a switch for it was the hole D-083 closed, so no key exists.* | | |
| `exfiltration.readvolume.baselinewindow` | 30 days | R | D-071, D-107 |
| `exfiltration.readvolume.factor` | 3.0 | R, decimal, floor 1.0; raising is loosening | OPS-ALERT-005, D-153: `read-volume-anomaly` fires when the actor's records returned today exceed this factor times their daily mean over the baseline window and exceed `exfiltration.readvolume.minimum` |
| `exfiltration.readvolume.minimum` | 500 | R, integer; raising is loosening | OPS-ALERT-005, D-153: the floor under which no read-volume alert fires |
| `alerting.authfailures.threshold` | 20 | R, integer failures on one account inside `alerting.dedupe.window`; raising is loosening | OPS-ALERT-002, D-153: `auth-failures-sustained` |
| `alerting.recovery.accountthreshold` | 3 per `P1D` | R, integer; raising is loosening | OPS-ALERT-001, D-153: `recovery-clustering` |
| `alerting.recovery.approverthreshold` | 3 per `P1D` | R, integer; raising is loosening | OPS-ALERT-001, D-153: `approver-volume` |
| `alerting.denials.threshold` | 50 per `PT10M` | R, integer denials per actor in a fixed ten-minute window counted from the Unix epoch in UTC; the actor is the acting subject the refusal records, and every refusal naming no acting subject is counted as one actor; raised once the window holds more than this; raising is loosening | AUTHZ-GATE-004, D-153, D-166: `denial-spike` |
| `alerting.sessions.distance` | 500 | R, integer kilometres; raising is loosening | OPS-ALERT-007, D-153, D-166: `concurrent-sessions-implausible` fires when two sessions of one account are both used inside `alerting.sessions.window` and their cities are further apart than this, or their known countries differ, whatever their cities; distance is measured only where both places name a city, and a place with no country never fires |
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
| `derivation.materialised.driftcheck` | `P1D` | R, duration; lengthening is loosening | AUTHZ-DERIVE-005, AUTHZ-GRANT-003, D-161, D-166: how often the drift check re-evaluates every materialised derivation, over the relationship sources the host declares for every declared derivation (LIB-HOST-001), corrects drift and raises `degradation` naming the derivation; it runs as the system principal `derivation-driftcheck` (section 5.29), and a grant it writes records the nil subject as granter with the reason `AUTHZ-DERIVE-005` |

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
| `registration.events.pollinterval` | `PT1S` | R, duration, floor `PT1S` | REG-SESS-003, FE-VER-001, D-162, D-166: how often an open `GET /register/events` stream reads the registration state back where the database channel has not woken it |
| `identifiers.email.max` | 10 | R, integer, floor 1, no ceiling; raising is loosening (each verified address is a send destination and a recovery channel) | REG-IDENT-002, REG-IDENT-007, D-146, D-152: verified emails per account; `1` is single-address mode (replace in one operation) |
| `identifiers.phone.max` | 10 | R, integer, floor 1, no ceiling; raising is loosening | REG-IDENT-002, REG-IDENT-007, D-146, D-152: verified phones per account; `1` is single-address mode |
| `identifiers.username.enabled` | false | R | REG-IDENT-001, REG-IDENT-009, D-146: while off no request accepts a username and no response carries the field |
| `identifiers.username.changecooloff` | `P30D` | R, floor `P1D` | REG-IDENT-009, D-146, D-152: minimum interval between username changes (`identity.username.coolingoff`) |
| `profile.legalname` | `off` | R: `off` · `optional` · `required` | REG-PROF-001, D-146: a proofing attribute, collected only with a declared purpose |
| `profile.dateofbirth` | `off` | R: `off` · `optional` · `required` | REG-PROF-001, REG-PROF-002, D-146: whether the date entered at the age step is retained; with `off` only the derived affirmation is kept. The date is immutable to the person |
| `domain.reverify.interval` | `P1D` | R, duration, floor `PT1H`; lengthening is loosening | REG-DOM-001, D-153, D-166: the sweep re-verifies the TXT record of every listed, verified domain last checked this long ago or more; a failure is recorded, raises `domain-reverification-failed` and unverifies nothing |
| `outbox.poll.interval` | `PT5S` | R, duration, ceiling `PT1M` | IDN-LIFE-003a, INT-MAIL-006a, AUTH-ABUSE-004, OPS-ALERT-001, D-153, D-162, D-166: the cadence of the outbox and event publishers, send delivery and its retries, mailbox provisioning and the carrying of raised alerts; the ceiling bounds how long any change can hold an alert back |
| `outbox.retry.initial` | `PT30S` | R, duration | IDN-LIFE-003a, AUTH-ABUSE-004, D-153, D-162, D-166: first retry delay, full jitter |
| `outbox.retry.factor` | 2.0 | R, decimal, floor 1.0 | IDN-LIFE-003a, AUTH-ABUSE-004, D-153, D-162, D-166: multiplier per further attempt |
| `outbox.retry.maxattempts` | 10 | R, integer, floor 1 | IDN-LIFE-003a, AUTH-ABUSE-004, D-153, D-162, D-166: attempts before an erasure or takedown record is `failed` with `erasure-delivery-exhausted`, an event is `failed` with `degradation`, or a message is removed with `degradation` scoped `send:<channel>` (about eight hours end to end at the defaults) |
| `sweep.interval` | `PT5M` | R, duration, ceiling `PT15M` | OPS-OBS-003, AUTH-KEY-003, PRIV-RIGHT-002, PRIV-RET-005, OPS-ALERT-006, D-153, D-166: one sweep for expired sessions, tokens and codes, identifier verifications whose codes have all expired unused, elapsed grace and cooling-off windows, privacy request deadlines (PRIV-RIGHT-002), sending-restriction records and every other abuse ledger line its own check no longer reads (PRIV-RET-005), export-limit records an hour old (OPS-ALERT-006) and domain re-verification; a deadline therefore fires within this interval of its instant |
| `notification.languages` | none; **required** | R, **list** of BCP 47 tags, at least one | IDN-ATTR-001, AUTH-ABUSE-004, AUTH-ABUSE-005, D-031, D-153, D-166: the deployment's message languages. Step 3 of the recipient-language resolution sends in all of them: by email one message carrying every language in this order, subject lines joined, judged and counted once; by text message one message per language, admitted only where every applicable bucket has room for all of them, each counted. Every template is validated in each language at startup |
| `notification.email.sendingdomain` | — **required** | R, string | INT-MAIL-011, D-153 |
| `notification.email.relayregistered` | empty | R, **set** of domains | INT-MAIL-011, D-153: domains registered with the Apple private relay; when `apple` is in any effective `loginFactors` and the sending domain is not in this set, the Normal condition `relay-domain-unregistered` fires |
| `location.database.refresh` | `P7D` | R, duration | INT-GEN-006, D-153: IP location database refresh cadence |
| `location.database.maxage` | `P30D` | R, duration | INT-GEN-006, D-153, D-166: beyond this age, judged from the date the file states, the file is stale, no location is shown, and a Normal degradation is raised |
| `service.name` | — **required when `context` is in `password.blocklist.sources`** | R, string | AUTH-PASS-004, D-153: the word the `context` source forbids in passwords |
| `preferences.maxsize` | 8192 | R, integer bytes (8 KiB), ceiling 65536 (64 KiB) | REG-PREF-001, D-146, D-152: cap on the whole set of host-declared preference values per account |

### 4.7 Privacy

| Key | Default | Scope | Source |
|---|---|---|---|
| `privacy.request.decision` | 6 | R, integer working days from submission, ceiling 6 (the statutory period PRIV-RIGHT-002 cites; only a shorter deadline is configurable) | PRIV-RIGHT-002, D-126, D-136, D-152 — the decision deadline; lapse is a deemed rejection |
| `privacy.calendar.timezone` | — **required** | **P**, IANA time zone name | PRIV-RIGHT-002, LIB-HOST-001, D-153: the zone in which calendar days, working days and holidays are determined; `Africa/Cairo` for the default deployment |
| `privacy.workingdays` | `[sunday, monday, tuesday, wednesday, thursday]` | R, set of weekday names, at least one | PRIV-RIGHT-002, D-136 — the week on which "working days" are counted |
| `privacy.holidays` | **empty** — public-holiday dates, added and moved as they are announced | R, **loosening** (OPS-CFG-002: step-up, reason, audit) | PRIV-RIGHT-002, D-136, D-142, D-166 — never required, never a startup condition: an unlisted holiday counts as a working day and makes a deadline *earlier*, which is always compliant. A Normal alert (`holiday-list-exhausted`) fires, from a daily look, when no listed date falls after the calendar day, in `privacy.calendar.timezone`, that now plus `maintenance.expiry.warninglead` falls on; an empty list fires it (OPS-ALERT-001) |
| `privacy.request.warninglead` | 2 working days before the deadline | R | PRIV-RIGHT-002, OPS-ALERT-001, D-126 |
| `retention.audit.security` | `P7Y` | R, duration, floor `P5Y` | PRIV-RET-001, D-132, D-152, D-166: security events, permission changes, financial actions and privacy actions (the `security` audit category, section 5.24); five-year tax retention plus a margin for claims |
| `retention.audit.routine` | 90 days | R, floor 30 days | PRIV-RET-001, D-132 — routine access logging |
| `retention.consent` | `P3Y` | R, duration counted from the end of the processing the consent covered, floor `P1Y` | PRIV-RET-001, D-132, D-152 — evidential period |
| `retention.<category>` | the floor the host declares for that category (LIB-HOST-001); no library default, startup fails for a declared category without one | R, duration, one key per host-declared data category, floor enforced; changed through `PUT /admin/config/retention.<category>`, shortening is loosening | PRIV-RET-001, D-107, D-166 |
| `hosting.environment` | none; **required** | R, string | PRIV-ROPA-001, LIB-HOST-001, D-153, D-166: the free-text hosting environment cell of the register; required of every deployment, since every deployment generates the records of processing |
| `backup.restoretest.objective` | `PT8H` | R, duration, ceiling `PT8H` | DR-007, D-044, D-153: `restore-test-failed` fires when the automatic restore test exceeds this; the upper bound of the accepted objective |
| `backup.restoretest.interval` | `P90D` | R, duration, ceiling `P90D` | DR-007, D-110, D-153, D-166: a day count, so the interval never exceeds the shortest calendar quarter; D-152's months rule, written for floors, would hold `P3M` at 93 days |
| `backup.restoretest.canary` | set at bootstrap | R, subject identifier | DR-007, OPS-BOOT-001, D-153, D-166: the canary account bootstrap seeds as a member of the administrative organization holding no role and no credential: display name `Restore canary` (its encrypted field) and verified email `canary@restore-test.invalid`; the restore test decrypts the field and resolves the email's fingerprint |
| `backup.retention` | 35 days | R, floor 14 days | DR-003, DR-006a, DR-010, D-132 — also bounds how long a pre-erasure backup survives |
| `hosting.location` | none; **required** | **P**; `inside` · `outside` (section 5.40) | INT-HOST-001, D-162, D-166: read at generation into the records of processing |
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
| `audit.enabled` | *Retired by D-166. Audit logging has no switch: the library records every event unconditionally (OPS-CFG-004).* |
| `exfiltration.export.auditing` | D-045 |
| `abuse.throttle.enabled` | OPS-CFG-004 |
| `stepup.enforcement.<organization>` | *Retired by D-166. Step-up enforcement has no switch; the emergency route past step-up is the break-glass session (AUTH-STEP-004, OPS-CFG-004).* |
| `webauthn.rpid` | OPS-CFG-004 |
| `token.signing.algorithm` | OPS-CFG-004; value and rationale in §4.9 (D-147) |
| `token.signature.verification` | *Retired by D-166. Token signatures are always verified; no switch exists (OPS-CFG-004).* |
| `privacy.calendar.timezone` | OPS-CFG-004; a legal clock that moves at runtime is a clock nobody can audit (D-153) |
| `legal.governinglanguage` | PRIV-CONS-005, D-146 (protected for a different reason: the language a document binds in is not a runtime toggle) |
| `webauthn.origins` | OPS-CFG-004; AUTH-FACT-010, D-166 |
| `webauthn.algorithms` | OPS-CFG-004; AUTH-FACT-014, D-120, D-166 |
| `hosting.location` | OPS-CFG-004; INT-HOST-001, D-166 |
| `hosting.crossborderbasis` | OPS-CFG-004; INT-HOST-002, D-166 |
| `integration.mail.endpoint` | OPS-CFG-004; INT-GEN-001, D-162, D-166 |
| `integration.sms.endpoint` | OPS-CFG-004; INT-GEN-001, D-162, D-166 |
| `integration.mailserver.endpoint` | OPS-CFG-004; INT-MAIL-001, INT-GEN-001, D-166 |
| `redirect.defaultclient` | OPS-CFG-004; API-REDIR-001, D-162, D-166 |

### 4.9 Tokens and keys

The lifetimes, algorithm and rotation cadence of the OIDC provider (`02` sections 8
and 9). The access-token lifetime is recorded as the accepted revocation latency for
any relying party that validates offline against the JWKS (AUTH-OIDC-004, D-147).

| Key | Default | Scope | Source |
|---|---|---|---|
| `oidc.accesstoken.lifetime` | 10 minutes | R, ceiling 1 h; lengthening is loosening (OPS-CFG-002) | AUTH-OIDC-004, D-147: for an offline validator this is the revocation latency |
| `oidc.code.lifetime` | 60 seconds | R, ceiling 10 min | AUTH-SESS-012, D-147: replaces the hard-coded sixty seconds |
| `token.signing.algorithm` | `ES256` | **P** (§4.8) | AUTH-KEY-001, D-147, D-148: verified against the mail server's source, file `crates/directory/src/backend/oidc/lookup.rs` of its repository, inspected 2026-09-18, which accepts P-256 keys from a JWKS as ES256 and refuses symmetric keys. The one place the value is held |
| `token.signing.rotation` | 90 days | R | AUTH-KEY-001, OPS-SEC-002, D-147, D-166: the cadence of the signing keys and of every registered client's secret, which the library generates and holds wrapped under the deployment's data key; overlap is `oidc.accesstoken.lifetime` plus 5 minutes, not a key of its own |
| `redirect.defaultclient` | empty | **P**, string, the identifier of a registered client of kind `browser-application`; startup refuses one the registry does not hold, or one of kind `protocol`, with `model.startup.redirectclient` | API-REDIR-001, API-REDIR-002, D-162, D-166: the client a browser falls back to where the identifier a request carried is not one the registry holds; while empty, no fallback destination is carried and the frontend chooses its own |

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
`VerificationOnly` · `Restricted`

`Restricted` marks a factor riding a channel AUTH-FACT-002b restricts (`phoneLink`,
`phoneCode`); the library's own rules for restricted factors read it.

*Source: AUTH-FACT-001, AUTH-FACT-002b, D-141, D-162, D-166 — `CanSatisfyStepUp` removed; eligibility follows from
`AssuranceLevel` and `IsPhishingResistant` against the gate*

### 5.3a Authenticator states

`active` · `suspended` · `invalidated`

A credential a social provider's security event holds is `suspended` with no
invalidation instant; the loss-report sweep never reaches it, and a session begun on
another factor restores it (IDN-LIFE-012a).

*Source: AUTH-RECOV-007, IDN-LIFE-012a, D-141, D-166*

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

Egypt's six, shipped as `LawfulBases.Default`, a declaration the host passes to the model
builder; the library applies no basis the host did not declare, and another jurisdiction
declares its own (D-108). **Not** the European set — there is no vital-interests basis and no
public-task basis. Each carries the properties `IsConsent`,
`RequiresWrittenConsentForSensitive`, `RequiresAssessment`, `IsObjectable`
(PRIV-BASIS-001); the code reads those and never the key.

`consent` · `contractual-obligation` · `legal-obligation` · `legitimate-interest` ·
`legal-right-claim-or-defence` · `court-judgment-or-order`

The library ships this list in this order, each entry carrying the four properties as
PRIV-BASIS-001 tables them.

*Source: PRIV-BASIS-001, D-108, D-162, D-166*

### 5.8 Data subject rights

`be-informed` · `access` · `withdraw-consent` · `erasure` · `restrict-processing` ·
`portability` · `object` · `rectification` · `breach-notification`

*Source: PRIV-RIGHT-001*

### 5.9 Sensitive data categories — default declaration

Egypt's list, shipped as `SensitiveCategories.Default`, a declaration the host passes to
the model builder, in this order. Labels, with one exception: the library reads
`children` by name, and the children's column of the records of processing is true for a
purpose exactly where a type it is declared on declares `children` (PRIV-ROPA-001,
D-162); nothing else branches on a category.

`health` · `genetic` · `biometric` · `financial` · `religious-belief` ·
`political-view` · `criminal-record` · `children`

*Source: PRIV-SENS-001, PRIV-ROPA-001, D-108, D-162, D-166*

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

`erasure-request` · `minor-takedown`

The `Reason` column of the erasures table (IDN-LIFE-003b) and the third field of the
off-host erasure record (DR-016) use these spellings and no others. An organization
erasure erases no account (IDN-ORG-003).

*Source: IDN-LIFE-003b, DR-016, D-147, D-166*

### 5.12b Suspension and deletion origin

`suspendedBy`: `self` · `administrator` (IDN-LIFE-013). `deletingBy`: `self` ·
`takedown` · `oob-request` (IDN-LIFE-003). Recorded when the state is entered; a
`takedown` deletion is reversed only through `/takedown/reverse`, which restores the
state held at the trigger, and an `oob-request` deletion sends no cancel link to the
subject (it sends `oob-deletion-notice`, section 5.25).

**Held state.** An account in `deleting` holds what it left, and a reversal or a
cancellation restores it: `suspensionHeld` (`self` · `administrator`), the suspension an
account was in when a takedown or a fulfilled out-of-band erasure request began its
deletion; `deletionHeld` (`self` · `oob-request`) and `deletionHeldSince` (an instant),
the origin and start of the deletion a takedown found running, whose erasure then falls
due at the earlier of that window's end and the trigger plus `takedown.grace`; and
`restrictionHeld` (boolean), a restriction of processing held while the account is
`suspended` or `deleting`, so that it returns `restricted`. A takedown's reversal
restores, in this order, a held deletion, a held suspension with its origin,
`restricted` where a restriction is held, and `active` otherwise. An out-of-band erasure
fulfilled on an account already `deleting`, by any origin, is recorded fulfilled against
the running window and holds nothing; on a `deleted` account it is recorded fulfilled
and nothing further happens (IDN-LIFE-003).

*Source: IDN-LIFE-003, IDN-LIFE-013, PRIV-RIGHT-004, D-137, D-147, D-166*

### 5.12c Privacy request status and type

Status: `open` · `fulfilled` · `refused` · `granted-by-lapse` ·
`deemed-refused-by-lapse` (PRIV-RIGHT-002). Type: `erasure` · `restriction` ·
`rectification` (PRIV-RIGHT-001; rectification of editable data is account editing and
raises no request, except while the account is `restricted`, IDN-ACCT-007).

*Source: PRIV-RIGHT-001, PRIV-RIGHT-002, IDN-ACCT-007, D-126, D-147, D-166*

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

A `source` key counts sends by the source of the request that asked for them, as
AUTH-ABUSE-001 defines it (a connection with no address is the one source `unknown`); a
send no request asked for (a background job's, an alert) carries no source, and no
`source` restriction counts it, as an `account` restriction counts no send without a
subject.

*Source: AUTH-ABUSE-004, D-146, D-166*

### 5.15 Restriction purposes

`verification` · `signin` · `secondfactor` · `notification` · `any`

A restriction with a purpose applies to sends of that purpose only; `any` applies to
every send except a security notice to an existing holder, which answers only to
restrictions whose purpose is `notification` (AUTH-ABUSE-004). The `notification`
purpose, and so `notification.destination`, counts security notices and other notices
only: no link or code a person or an administrator asked for carries it. A recovery link
(REG-IDENT-002) and an invitation link (IDN-LIFE-009a) carry `signin`, so neither counts
against `notification.destination`. An alert is outside every restriction and answers to
the deduplication of OPS-ALERT-002 alone.

*Source: D-148; AUTH-ABUSE-004, D-146, D-166*

### 5.15a Restriction channels

`sms` · `email` · `any`

A restriction with a channel governs sends on that channel only; `any`, the default,
governs sends on either, and a stored restriction without one reads as `any`. A channel
changed to anything but `any` is a loosening (section 4, Direction).

*Source: AUTH-ABUSE-004, D-146, D-166*

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

### 5.19a Second-step choice

`totp` · `securityKey` · `passkey` · `none`

The `secondStep` member of `PUT /register/security` (`09` section 2): the enrolment the
frontend shows next. Validated against this set, refused outside it with
`api.request.malformed` naming `secondStep`, and recorded nowhere; the security step
completes on what is enrolled (REG-SESS-006).

*Source: REG-SESS-006, D-162, D-166*

### 5.20 Capability residuals

The closed set a capability's `requires` may carry (API-CAP-001, AUTHZ-GATE-005):
`stepup` · `reauthenticate` (a downgraded session, AUTH-SESS-009) · `restricted`
(AUTHZ-GATE-006) · `consent` (PRIV-SENS-002, PRIV-CONS-007) · `accountstate`.

*Source: AUTHZ-GATE-005, D-153*

### 5.21 Consent and objection mechanism

The `mechanism` of a consent or objection record: `registration` (the terms step,
REG-SESS-007) · `dashboard` (`/privacy/consents/*`, `/privacy/objections/*`) ·
`reconsent` (the PRIV-CONS-007 prompt; recorded by the library for a dashboard grant over
a superseded, unwithdrawn consent) · `administrator` (entered on the subject's behalf).

*Source: PRIV-CONS-001, PRIV-RIGHT-001a, D-153, D-162, D-166*

### 5.22 Age group

Recorded by the age screen when `registration.adultaffirmation` = `off`: `minor` ·
`adult`. Finer bands are host work when a host needs them.

*Source: REG-PROF-002, D-153*

### 5.23 Alert conditions

One identifier per OPS-ALERT-001 row, carried by `AlertRaised` and used as the
deduplication key of OPS-ALERT-002: `auth-failures-sustained` · `recovery-clustering`
· `approver-volume` · `read-volume-anomaly` · `breakglass-used` ·
`breakglass-generated` · `protected-setting-changed` · `alert-destination-changed` ·
`stepup-policy-weakened` · `concurrent-sessions-implausible` · `denial-spike` ·
`sms-balance` · `background-job-failed` · `erasure-delivery-exhausted` ·
`certificate-renewal-failed` · `clock-drift` · `degradation` ·
`callback-verification-failed` · `nonexistent-notice-rate` ·
`privacy-deadline-approaching` · `privacy-deadline-reached` · `expiry-approaching` ·
`holiday-list-exhausted` · `no-emergency-credential` · `restore-test-failed` ·
`restriction-loosened` · `restriction-granted` · `domain-reverification-failed` ·
`domain-removed` · `governing-language-changed` · `governing-text-missing` ·
`relay-domain-unregistered`.

`breakglass-used` and `breakglass-generated` (a first issue or a replacement) are High,
and reach the owner's destinations whatever `alerting.owner.enabled` holds (OPS-BOOT-002,
OPS-BOOT-004).

**Scopes.** Where a condition is raised for one of several things, a scope names the
thing. The scopes the chapters name:

| Condition | Scope | Raised when | Source |
|---|---|---|---|
| `degradation` | `mailbox.push:<mailbox id>` | A mailbox push spent `outbox.retry.maxattempts`; details `{ mailbox, state, attempts }`, the mailbox named by its identifier and never by its address | INT-MAIL-007 |
| `degradation` | `mailbox.reconciliation` | Reconciliation found a difference, details `{ mailboxes, unknown }`, or could not have the listing, `{ listed: false }` | INT-MAIL-007 |
| `degradation` | `send:<channel>` | A message spent `outbox.retry.maxattempts` (section 4.6) and was removed | AUTH-ABUSE-004, D-162 |
| `degradation` | `password.blocklist.fallback` | Screening fell back to the offline list; `details.configured` and `details.used` (`offline`) | OPS-OBS-002, AUTH-PASS-004 |
| `degradation` | `clock.reference.absent` · `clock.reference.unread` | No clock reference is declared, or the declared one could not answer | OPS-OBS-002, INF-HOST-001 |
| `degradation` | `certificate.renewal.absent` · `certificate.renewal.unread` | No certificate renewal outcome is declared, or the declared one could not answer | OPS-OBS-002, INF-TLS-003 |
| `expiry-approaching` | `envelope-rotation` | The annual envelope operation falls due within `maintenance.expiry.warninglead` of a year after the log's latest `envelope-rotation` entry | OPS-MAINT-001 |
| `expiry-approaching` | `kek-cryptoperiod` | The key-encryption key's cryptoperiod ends within `maintenance.expiry.warninglead`; `details.version`, `details.rotatedAt`, `details.dueAt` | DR-009a |

A lost registration channel raises `degradation` with `details.component`
`registration-channel` (OPS-OBS-002, REG-SESS-003). A mail-server account the adapter
will not adopt raises `degradation` at once, naming the mailbox by its identifier
(INT-MAIL-001, REG-MAIL-003); a drift the drift check finds raises it naming the
derivation (AUTHZ-DERIVE-005); a stale or absent location file raises it (INT-GEN-006).

*Source: OPS-ALERT-001, OPS-ALERT-002, OPS-OBS-002, D-153, D-166*

### 5.24 Audit actions

The actions the library writes to the audit trail, stable as the error codes are: an
action is an identifier, and changing one is breaking (CONV-NAME-003). The set is closed:
the library writes these and no other, and a new one is added here in the same change
(REF-001). The category is the partition the row is routed to and the retention it
follows (PRIV-RET-001, PRIV-RET-002): `security` · `routine`; every privacy action is
`security`.

Every record carries an acting identity and an effective identity equal to it
(AUTHZ-IMP-001); work of a system principal carries the nil subject in both, with the
principal's name (section 5.29) and its stated reason. A record that concerns an account
names it as `subject`, whoever acted (IDN-AUD-001, PRIV-BREACH-002). Every record a
break-glass session writes carries the reason given at its use (OPS-BOOT-002).

| Action | Category | Written when | Source |
|---|---|---|---|
| `auth.authentication.failed` | security | A factor presented at sign-in, at a sign-in link press (a link token unknown or expired included, when pressed) or on a social provider's return was refused; the new-device check's verification code was refused; or the break-glass credential was refused. The acting identity is the nil subject; `subject` is the account the attempt was made against, and is absent where the identifier, handle or link token resolved to none, the provider's identity is linked to no account or did not hold up, or the break-glass code was refused before the reserved account was read. `details.factor` names the factor, or `details.verification` is `device` where the refused value was the new-device check's code. Nothing typed is written, and no row is written while a delay of AUTH-ABUSE-001 stands; no organization | CONV-LOG-005, IDN-AUD-001, AUTH-ABUSE-001 |
| `auth.stepup.failed` | security | A factor presented to step a live session up was refused, including against a challenge that is not the asker's, behind the sign-in delay. The session's account is the acting identity and `subject`; `details.session` and `details.factor`; no organization | CONV-LOG-005, AUTH-ABUSE-001 |
| `auth.botdefence.signalled` | security | The bot defence answered a registration with a signal, recorded without the signal's own detail | AUTH-ABUSE-008, LIB-HOST-001 |
| `auth.breakglass.generated` | security | The break-glass credential was issued or replaced. The acting identity is who generated it; `details.credential`, and `details.replaced` where there was one; no organization | OPS-BOOT-004 |
| `auth.breakglass.used` | security | The break-glass credential opened the emergency session. The reserved account is the acting identity and `subject`; `details.credential` and `details.session`; no organization | OPS-BOOT-002, CONV-LOG-005 |
| `auth.credential.countermismatch` | security | An authenticator presented a signature counter that did not advance | AUTH-FACT-014 |
| `auth.credential.enrolled` | security | A credential reached `active` on an account | AUTH-FACT-001, AUTH-STEP-007 |
| `auth.credential.invalidated` | security | A suspended credential was invalidated at the end of its window | AUTH-RECOV-007 |
| `auth.credential.invalidationheld` | security | The window of a suspended credential ended with none of its notices delivered, so the invalidation was held and the credential stays suspended | AUTH-RECOV-007 AC3 |
| `auth.credential.labelled` | routine | The holder gave a credential a label or renamed it | AUTH-FACT-001 |
| `auth.credential.removed` | security | A credential was removed from an account | AUTH-FACT-001 |
| `auth.credential.reportcancelled` | security | A suspension by loss report was cancelled inside its window | AUTH-RECOV-007 |
| `auth.credential.reportedlost` | security | A credential was reported lost and suspended, which starts its window | AUTH-RECOV-007 |
| `auth.credential.restored` | security | A credential a provider's security event suspended stands again, because a session began on another factor; `details.credential`. Not the loss-report cancellation, which is `auth.credential.reportcancelled` | IDN-LIFE-012a |
| `auth.mailcredential.created` | security | The mail server generated an app password at its holder's request; `details.credential` is the server's identifier, and neither secret nor label is written; the account is the acting identity and `subject`; no organization | REG-MAIL-002, INT-MAIL-010 |
| `auth.mailcredential.revoked` | security | The mail server revoked an app password at its holder's request; `details.credential`; the account is the acting identity and `subject`; no organization | REG-MAIL-002, INT-MAIL-010 |
| `auth.oidc.clientregistered` | security | A client was registered in the provider's registry, or a registered one changed, from the server; principal `register-client`, reason `AUTH-OIDC-001`; `details.client`, `details.kind`, `details.changed`, and nothing of its secret; no subject, no organization | AUTH-OIDC-001, OPS-SEC-002 |
| `auth.oidc.refreshreused` | security | A consumed refresh token was presented again, which revokes its session family | AUTH-OIDC-003 |
| `auth.phonesignal.considered` | security | A phone signal (section 5.36) was consulted before a send, recorded without the number; `unavailable` where no provider is declared | AUTH-FACT-002b, LIB-HOST-001 |
| `auth.providerevent.rejected` | security | A social provider's security event about a linked identity was refused: not verified by the provider's keys, or carried before. `details.credential`, `details.event` (the provider's spelling), `details.outcome` (`unsigned` · `replayed`); the account is `subject` | IDN-LIFE-012a AC1 |
| `auth.providerevent.taken` | security | A social provider's security event about a linked identity was carried. `details.credential`, `details.event`, `details.outcome` (`sessionsEnded` · `credentialUnlinked` · `accountSuspended` · `addressUnverified` · `recorded`); the account is `subject` | IDN-LIFE-012a |
| `auth.recovery.approved` | security | An admin-assisted recovery was approved, naming the approver and the reason | AUTH-RECOV-002, AUTH-RECOV-002a |
| `auth.restriction.edited` | security | A named sending restriction was created, edited or deleted; `details.restriction`, `details.loosening`, `details.before`, `details.after` and `details.reason` | AUTH-ABUSE-004, OPS-CFG-008 |
| `auth.restriction.granted` | security | Support granted credit to one key under a named restriction; `details.restriction`, `details.credit`, `details.reason`; the key is never written | AUTH-ABUSE-004 |
| `auth.session.presented` | security | A session began, or was stepped up, on the factors presented; `details.session` and `details.factors` | CONV-LOG-005, IDN-AUD-001 |
| `authz.access.denied` | security | A permission was refused; the row a refusal's correlation identifier resolves to. Written outside the caller's transaction and committed at once, so a refusal inside an operation that rolls back is still recorded and counted | AUTHZ-CONCEAL-004, AUTHZ-GATE-004 |
| `authz.access.exported` | security | An export operation was admitted at the gate; the exporter, or the nil subject with a system principal's name and reason; `details.permission`, `details.resourceType`, and `details.resource` where one record was checked. Not written while `exfiltration.export.auditing` is off | OPS-ALERT-006 |
| `authz.group.created` | security | A group was created; `details.group`, `details.name`, `details.reason`; filed under the group's organization | AUTHZ-GROUP-001 |
| `authz.group.memberadded` | security | An account or a group was added to a group; `details.group`, `details.name`, `details.memberType`, `details.memberId`, `details.reason` | AUTHZ-GROUP-001, OPS-CFG-007 |
| `authz.group.memberremoved` | security | An account or a group was taken out of a group; the same details | AUTHZ-GROUP-001, OPS-CFG-007 |
| `authz.group.removed` | security | A group nothing named was removed; `details.group`, `details.name`, `details.reason` | AUTHZ-GROUP-001 |
| `authz.role.defined` | security | A role was created, or its permissions changed; `details.role`, `details.before` (empty for a role bootstrap defines), `details.after`, `details.reason`; no organization | AUTHZ-GRANT-004, OPS-CFG-007, OPS-BOOT-001 |
| `authz.role.removed` | security | A role nothing named was removed; `details.after` is null | AUTHZ-GRANT-004 |
| `identity.account.deactivated` | routine | The owner deactivated the account | IDN-LIFE-013 |
| `identity.account.reactivated` | routine; security where an administrator acted | A suspended account was stood back up: by its owner from a deactivation, or by an administrator from an administrator's suspension (the administrator acting, the account the `subject`) | IDN-LIFE-013 |
| `identity.account.suspended` | security | An administrator suspended an account, or took over the suspension of one its owner deactivated; the administrator acting, the account the `subject`; no organization | IDN-LIFE-013, AUTH-SESS-010 |
| `identity.deletion.cancelled` | routine; security where an administrator acted | A deletion was cancelled inside its grace window: by the subject from the link, or by an administrator on the subject's behalf, with `details.request` where an out-of-band request began the window | IDN-ACCT-007, IDN-LIFE-003, IDN-LIFE-014 |
| `identity.deletion.requested` | routine | The owner requested deletion, which opens the grace window | IDN-ACCT-007, IDN-LIFE-014 |
| `identity.invitation.acknowledged` | security | An invitation was acknowledged and its membership attached; `details.invitation`; the person acknowledging is the actor; filed under the organization | REG-INV-001, IDN-LIFE-009a |
| `identity.invitation.issued` | security | An invitation into an organization was issued; `details.invitation` and nothing it binds, and `details.formerMailbox` with its reason where the invitation names one because the corporate address's mailbox has been held before (section 5.44); filed under the organization | IDN-LIFE-009a, REG-INV-001, REG-MAIL-001 |
| `identity.invitation.revoked` | security | An unacknowledged invitation was revoked, or replaced by a later one for the same corporate address; `details.invitation` | IDN-LIFE-009a, REG-MAIL-001 |
| `identity.membership.ended` | security | An administrator ended a membership; `details.membership`; the administrator acting, the member the `subject`; filed under the organization | IDN-MEM-001, REG-MAIL-003 |
| `identity.organization.created` | security | An organization was created with no policy override; `details.reason`; filed under the organization; bootstrap's is recorded under the principal `bootstrap` | IDN-ORG-002, OPS-BOOT-001 |
| `identity.organization.deletioncancelled` | security | An organization's deletion was cancelled inside its window; `details.reason`; filed under the organization | IDN-ORG-003 |
| `identity.organization.deletionrequested` | security | An organization's deletion was requested: it is suspended and every member session ended; `details.reason`; filed under the organization | IDN-ORG-003 |
| `identity.organization.domainadded` | security | A domain was listed in an organization's lock, unverified; `details.domain`, `details.reason`; filed under the organization | REG-DOM-001, IDN-ORG-006 |
| `identity.organization.domainremoved` | security | A domain was removed from an organization's lock; its addresses stay refused until it is listed anew; `details.domain`, `details.reason`; filed under the organization | REG-DOM-001 |
| `identity.organization.domainverified` | security | A listed domain was verified by its TXT record; `details.domain`, `details.reason`; filed under the organization | REG-DOM-001 |
| `identity.organization.erased` | security | An organization's deletion window elapsed and the erasure executed; principal `organization-erasure`, reason `IDN-ORG-003`; `details.organization`, `details.deletingSince`, `details.membershipsEnded`; no subject; filed under the organization | IDN-ORG-003 |
| `identity.preferences.changed` | routine | The account's preferences changed, recorded by key and never by value | REG-PREF-001, IDN-ATTR-001 |
| `identity.profile.changed` | routine | A profile field of the account changed, or its photo was set or removed | REG-PROF-001, IDN-ATTR-002 |
| `identity.secondstep.preferred` | routine | The account's preferred second step changed | IDN-ATTR-008 |
| `identity.takedown.executed` | security | Phase one of a takedown committed; `details.takedown`, `details.trigger` (section 5.12d), `details.reason`, `details.erasureDue` | IDN-LIFE-003 AC3 |
| `identity.takedown.reversed` | security | A takedown was reversed inside its window; `details.reason` | IDN-LIFE-003 |
| `identity.username.changed` | routine | The account's username changed | REG-IDENT-009 |
| `ops.auditpartitions.maintained` | security | A run of the audit retention job completed; principal `audit-partitions`, reason `PRIV-RET-002`; `details.created`, `details.dropped`, `details.securityRetentionDays`, `details.routineRetentionDays`; no subject, no organization | PRIV-RET-002, INF-BG-002 |
| `ops.configuration.changed` | security | A runtime setting was changed through the one configuration operation, or a value was set by bootstrap or by `configure` from the server under that command's principal; `details.key`, `details.before` (the value in force, null where the key has no default and no value stood), `details.after`, `details.loosening` and `details.reason`, which every change carries | OPS-CFG-002, OPS-CFG-004, OPS-CFG-005, OPS-CFG-008 |
| `ops.keyrotation.completed` | security | A rotation of the key-encryption key or the fingerprint key reached every value under its version; principal `rotate-kek` or `rotate-fingerprint-key`, reason `OPS-SEC-003`; `details.kind` (section 5.41), `details.version`, `details.processed`; no subject, no organization | OPS-SEC-003 AC5 |
| `ops.keyrotation.resumed` | security | A stopped rotation was taken up again from its recorded progress; the same principal and details | OPS-SEC-003 AC2, AC5 |
| `ops.keyrotation.retired` | security | The versions before a completed rotation's were retired once its escrow copy was confirmed sealed; the same details and `details.retired` | OPS-SEC-003 AC3, AC4, AC5 |
| `ops.keyrotation.started` | security | A rotation started; the same principal and details | OPS-SEC-003 AC5 |
| `ops.restoretest.completed` | security | A run of the automated restore test ended; principal `restore-test`, reason `DR-007`; `details.outcome` (`passed` · `unrestored` · `undecrypted` · `unresolved` · `overrun`), `details.elapsedSeconds`, `details.objectiveSeconds`, `details.outlived`; no subject, no organization | DR-007 AC2, DR-008 AC2 |
| `privacy.consent.granted` | security | A consent was granted for a purpose, naming the document version it stands against | PRIV-CONS-001, PRIV-CONS-011 |
| `privacy.consent.withdrawn` | security | A consent was withdrawn for a purpose | PRIV-CONS-008 |
| `privacy.document.published` | security | A version of a legal document was published in its governing language | PRIV-CONS-005 |
| `privacy.document.translated` | security | A translation was filed against a published version | PRIV-CONS-005, PRIV-CONS-006 |
| `privacy.erasure.completed` | security | A delivery whose retries were spent was completed by hand, with its erasures row where it is an erasure's; `details.erasure` (the delivery's identifier), `details.kind` (`erasure-requested` · `takedown-executed` · `restriction-changed`) and `details.outstanding` (the required subscribers that had not confirmed, by name); the operator acting, the one the delivery concerns the `subject` | IDN-LIFE-003a |
| `privacy.erasure.executed` | security | An erasure was carried out; on a replay of the off-host ledger, principal `replay-erasures`, reason `DR-016`, with `details.reason` in its section 5.12a spelling and `details.erasedAt` | PRIV-RIGHT-005, DR-016 |
| `privacy.export.assembled` | security | A subject export was assembled for the subject | PRIV-RIGHT-003 |
| `privacy.objection.recorded` | security | An objection to a purpose was recorded | PRIV-RIGHT-001a |
| `privacy.objection.withdrawn` | security | An objection was withdrawn and the purpose resumed | PRIV-RIGHT-001a |
| `privacy.request.entered` | security | A data subject request was entered on the subject's behalf | PRIV-RIGHT-001, PRIV-RIGHT-002 |
| `privacy.request.fulfilled` | security | A data subject request was fulfilled | PRIV-RIGHT-002 |
| `privacy.request.lapsed` | security | A data subject request reached its deadline undecided | PRIV-RIGHT-002 AC3, AC4 |
| `privacy.request.refused` | security | A data subject request was refused, with the reason recorded | PRIV-RIGHT-002 |
| `privacy.request.submitted` | security | The subject submitted a data subject request | PRIV-RIGHT-001, PRIV-RIGHT-002 |
| `privacy.restriction.lifted` | security | An administrator lifted a restriction of processing and the subscribers were told; the administrator acting, the account the `subject`; no organization | PRIV-RIGHT-004 |

*Source: IDN-AUD-001, PRIV-RET-001, PRIV-RET-002, CONV-NAME-003, D-162, D-166*

### 5.25 Message kinds

What the library asks a deployment's catalogue for when it sends a message. The set is
closed; the library holds no words (CONV-CONTENT-001), and every send of a kind fills the
same places (section 5.26), so one text serves every send of it. A kind that carries a
link carries it in the place `link`; section 5.43 names the link kind each such message
kind lands under.

| Kind | Asked for when | Places | Source |
|---|---|---|---|
| `verification-code` | A code alone proves control of an address: the new-device check | `code` | AUTH-FACT-004, AUTH-FACT-016 |
| `verification-link` | A code and a link prove control of an address or a number being registered, added or replaced | `code`, `link` | REG-SESS-003, REG-IDENT-004, REG-IDENT-007 |
| `sign-in-code` | The `emailCode` sign-in code, an authentication code held to `code.signin.lifetime` and `code.signin.attempts`, never sent as a verification code | `code` | AUTH-FACT-003, AUTH-FACT-004 |
| `signin-link` | A link that signs the person in; the code is shown only by the page opened elsewhere | `link`, `code` | AUTH-FACT-003, REG-SESS-003 |
| `secondstep-code` | A code presented as a second step (`phoneCode`), an authentication code held to `code.signin.lifetime` and `code.signin.attempts` | `code` | AUTH-FACT-002 |
| `security-notice` | Something happened to the account that the security-notice set is told of and that carries no link | none | REG-IDENT-002, AUTH-RECOV-002, REG-MAIL-002, IDN-LIFE-012a |
| `credential-suspended` | A credential was suspended by a loss report, or by a removal that would lower the account's reachable assurance; sent at the report, every `recovery.invalidation.noticeinterval` and 24 hours before invalidation, each carrying the cancel link | `link` | AUTH-RECOV-007 |
| `enrolment-link` | The link that carries an admin-assisted enrolment | `link` | AUTH-RECOV-002 |
| `recovery-link` | The link a person asked for to set a new password; it restores nothing else, and carries the `signin` purpose (section 5.15) | `link` | AUTH-RECOV-004, AUTH-RECOV-005, REG-IDENT-002 |
| `alert` | An OPS-ALERT-001 condition the operator has to see | `condition`, `raisedAt`, and the places of section 5.26 its condition carries | OPS-ALERT-001 |
| `no-account` | The answer to a request made for an address no account holds | none | AUTH-ABUSE-003 |
| `account-exists` | The answer to a registration or a change made with an address an account holds, sent to the holder and never to the person who tried | none | REG-SESS-005, REG-IDENT-008 |
| `identifier-added` | An identifier was added to the account, a corporate address taken on at an invitation's acknowledgement included; sent to the security-notice set as it stood before | none | REG-IDENT-004, REG-MAIL-001 |
| `identifier-removed` | An identifier was removed; sent to the remaining security-notice set with the undo link | `link` | REG-IDENT-006, REG-IDENT-007 |
| `identifier-detached` | The removed identifier no longer reaches the account; no link, no powers | none | REG-IDENT-006 |
| `identifier-settings-changed` | The primary of a kind, or the kind's backup setting, changed | none | REG-IDENT-005, REG-MAIL-003 |
| `identifier-change-confirm` | The address a replace displaces is asked to confirm, only where the account has no other channel | `link` | REG-IDENT-007 |
| `credential-enrolled` | A credential was enrolled on the account | none | AUTH-STEP-007 |
| `invitation-link` | The link of an invitation into an organization, sent to the email it binds and to nothing else, under the `signin` purpose (section 5.15) with no subject | `link` | IDN-LIFE-009a, REG-INV-001, REG-MAIL-001 |
| `deactivation-notice` | The account deactivated itself; carries the link that stands it back up | `link` | IDN-LIFE-013 |
| `deletion-notice` | The account's own deletion started its grace window; carries the cancel link | `link` | IDN-ACCT-007, IDN-LIFE-014 |
| `oob-deletion-notice` | An erasure request received out of band was fulfilled and the grace window started; sent to the security-notice set, with no cancel link | none | IDN-LIFE-003 |
| `privacy-request-received` | The automatic receipt of a data subject request entering the queue; not a decision | none | PRIV-RIGHT-002 AC1 |
| `privacy-request-lapsed` | A subject's out-of-band erasure or rectification request reached its deadline undecided | none | PRIV-RIGHT-002 AC4 |
| `recovery-codes-reminder` | The one reminder a set of recovery codes gets once it is older than `recovery.codes.reminder`, sent to the security-notice set of an active account, carrying no link; the set is closed once a channel takes it | none | AUTH-FACT-008 AC5 |

*Source: CONV-CONTENT-001, INT-SMS-003, D-162, D-166*

### 5.26 Message places

The places a text leaves for the library's values, and the width each is measured at
when a text-message template is checked against its budget at startup (INT-SMS-003,
AUTH-ABUSE-005): a text that carries `{link}` is budgeted at two segments of its
alphabet, 306 characters of the GSM 7-bit default alphabet or 134 otherwise, and every
other text at one, 160 or 70. The set is closed: startup refuses a text-message template
naming a place not listed here with `model.startup.declarationinvalid`.

| Place | Width | Carries |
|---|---|---|
| `code` | the verification code's digits | The code to enter |
| `link` | the longer declared landing origin, `/link#`, the widest kind of section 5.43, `.` and a drawn token, one token size for every link | The address to open, `<origin>/link#<kind>.<token>` (section 5.43) |
| `token` | *Retired by D-166. Every link-bearing kind carries `link`, which holds the token; no text carries a bare token, and a text-message template naming `{token}` names a place with no width and is refused at startup.* | |
| `condition` | the widest spelling of section 5.23 | The alert condition |
| `raisedAt` | the width of an instant | When it was raised |
| `restriction` | 64, the bound its name is held to | The restriction a grant or a loosening names |
| `key` | the widest settings key, each family at its widest parameter | The setting a change names |
| `destinationsBefore` | the width of a count | Alert destinations before a change |
| `destinationsAfter` | the width of a count | Alert destinations after it |
| `balance` | the width of an amount | The gateway balance |
| `floor` | the width of an amount | `abuse.sms.balancefloor` |
| `spentLastHour` | the width of an amount | The last hour's spend |
| `document` | 64, the bound its name is held to | The governing document with no text |
| `delivery` | the width of an identifier | The erasure delivery that exhausted its attempts |
| `kind` | 32 | The section 5b event that delivery carries |
| `attempts` | the width of a count | Its attempts |
| `outstanding` | the joined width of the registered required subscribers' names, measured at startup | The subscribers that have not confirmed it |
| `request` | the width of an identifier | The privacy request whose deadline was reached |
| `type` | the widest spelling of section 5.12c types | What was asked for |
| `status` | the widest spelling of section 5.12c statuses | Where it had got to |
| `decisionDue` | the width of an instant | When the decision was due |

*Source: INT-SMS-003, AUTH-ABUSE-005, D-162, D-166*

### 5.27 Register findings

What the records of processing report as missing rather than leave blank. The set is
closed.

| Finding | Raised when | Source |
|---|---|---|
| `data-owner-missing` | The deployment named no data owner | PRIV-ROPA-001 AC2 |
| `organizational-measures-missing` | The deployment stated no organizational security measures | PRIV-ROPA-001 AC2 |
| `assessment-links-missing` | The deployment named no links to the LIA, DPIA or TIA | PRIV-ROPA-001 AC2 |
| `assessment-missing` | A purpose rests on a basis that requires an assessment and names none | PRIV-ROPA-001 AC3, PRIV-BASIS-002 |
| `agreement-missing` | A processor is declared without a data protection agreement reference | PRIV-ROPA-002 |
| `retention-missing` | A purpose is over a data category whose retention period cannot be read: none declared, or the stated one refused | PRIV-ROPA-001, PRIV-RET-001 |
| `children-undeclared` | `registration.adultaffirmation` is `off` and no resource type declares the `children` category, so the children's column is empty | PRIV-ROPA-001, PRIV-MINOR-001, D-162 |

*Source: PRIV-ROPA-001, PRIV-ROPA-002, D-162, D-166*

### 5.28 System operations

`reconciliation` · `retention-purge` · `records-of-processing` · `expiry-sweep` ·
`delivery` · `monitoring` · `bootstrap` · `key-rotation` · `configuration` ·
`erasure-replay`

The operations a deployment-scoped system principal may run, and nothing else
(IDN-PRIN-001 AC3); each principal of section 5.29 names exactly one. `delivery` carries
what has committed to where it goes (the outboxes, mailbox provisioning, raised alerts);
`monitoring` reads the state of something the deployment depends on and raises what the
reading calls for; `configuration` changes the deployment's configuration from the
server, a protected key or the provider's client registry.

*Source: IDN-PRIN-001, INF-BG-001, INF-BG-002, OPS-BOOT-001, OPS-SEC-003, OPS-CFG-004, AUTH-OIDC-001, DR-016, D-166*

### 5.29 System principals

The name each background job and server command acts under, written as the principal of
the audit rows it writes, with the reason below, and carried by `background-job-failed`.
Every job refuses to run under a principal that may not run its operation
(IDN-PRIN-001).

| Principal | Kind | Operation | Reason |
|---|---|---|---|
| `expiry-sweep` | job | `expiry-sweep` | OPS-OBS-003 |
| `registration-sweep` | job | `expiry-sweep` | REG-SESS-001 |
| `invitation-sweep` | job | `expiry-sweep` | PRIV-RIGHT-005a |
| `account-deletion` | job | `expiry-sweep` | IDN-LIFE-014 |
| `organization-erasure` | job | `expiry-sweep` | IDN-ORG-003 |
| `privacy-deadlines` | job | `expiry-sweep` | PRIV-RIGHT-002 |
| `loss-reports` | job | `expiry-sweep` | AUTH-RECOV-007 |
| `recovery-code-reminder` | job | `expiry-sweep` | AUTH-FACT-008 |
| `domain-reverification` | job | `expiry-sweep` | REG-DOM-001 |
| `outbox` | job | `delivery` | IDN-LIFE-003a |
| `events` | job | `delivery` | IDN-LIFE-003a |
| `sends` | job | `delivery` | INF-BG-001 |
| `mailbox-provisioning` | job | `delivery` | INT-MAIL-006a |
| `alert-dispatch` | job | `delivery` | OPS-ALERT-001 |
| `mail-reconciliation` | job | `reconciliation` | INT-MAIL-007 |
| `derivation-driftcheck` | job | `reconciliation` | AUTHZ-DERIVE-005 |
| `location-database` | job | `monitoring` | INT-GEN-006 |
| `read-volume-baseline` | job | `monitoring` | OPS-ALERT-005 |
| `holiday-list` | job | `monitoring` | PRIV-RIGHT-002 |
| `emergency-credential` | job | `monitoring` | OPS-BOOT-001 |
| `clock-drift` | job | `monitoring` | INF-HOST-001 |
| `certificate-renewal` | job | `monitoring` | INF-TLS-003 |
| `restore-test` | job | `monitoring` | DR-007 |
| `audit-partitions` | job | `retention-purge` | PRIV-RET-002 |
| `licence-expiry` | job | `monitoring` | OPS-MAINT-001 |
| `envelope-rotation` | job | `monitoring` | DR-009a |
| `sms-balance` | job | `monitoring` | INT-SMS-004 |
| `bootstrap` | command | `bootstrap` | OPS-BOOT-001 |
| `configure` | command | `configuration` | OPS-CFG-004 |
| `register-client` | command | `configuration` | AUTH-OIDC-001 |
| `rotate-kek` | command | `key-rotation` | OPS-SEC-003 |
| `rotate-fingerprint-key` | command | `key-rotation` | OPS-SEC-003 |
| `replay-erasures` | command | `erasure-replay` | DR-016 |

*Source: IDN-PRIN-001, INF-BG-001, INF-BG-002, AUTHZ-DERIVE-005, AUTHZ-GRANT-003, D-166*

### 5.30 Conformance checks and truth-table scenarios

Checks, the part of the suite a finding came from: `policies` · `truth-table` ·
`declaration` · `provider`.

Scenarios, the values of `authz.truthtable.disagreement`'s `details.scenario`:
`grant-on-record` · `grant-on-container` · `grant-above-container` ·
`grant-on-organization` · `grant-on-sibling` · `no-grant` · `grant-to-group` ·
`grant-to-nested-group` · `deny-over-grant` · `deny-on-container-over-grant` ·
`expired-grant` · `revoked-grant` · `grant-in-another-organization` ·
`role-without-permission` · `derived-grant` · `derived-grant-on-container` ·
`deny-over-derived-grant`. A derived scenario runs once for each derivation its type
declares.

*Source: LIB-TEST-001, AUTHZ-TEST-001, D-166*

### 5.31 Explanation outcome

`allowed` · `denied`, the `outcome` of `GET /admin/explanations/{correlationId}`.

*Source: AUTHZ-GATE-004, AUTHZ-CONCEAL-004, D-166*

### 5.32 Device kinds

`trusted` (skips the second factor, AUTH-FACT-015) · `remembered` (passed the new-device
check, AUTH-FACT-016), the `kind` of `GET /account/devices`.

*Source: AUTH-FACT-015, AUTH-FACT-016, `09` `GET /account/devices`, D-166*

### 5.33 Sign-in status

`complete` · `factorRequired` · `deviceVerificationRequired`, the `status` of
`POST /auth/factor` and `/auth/step-up`; the last is answered 200 and is not a refusal.

*Source: AUTH-FACT-016, `09` `/auth/factor`, D-166*

### 5.34 Registration stream events

`identifier-verified` · `step-completed` · `session-ended`, the `event:` names of
`GET /register/events`; each carries the `GET /register` state as its `data`.

*Source: REG-SESS-003, `09` `GET /register/events`, D-166*

### 5.35 Licence kinds and maintenance tasks

Licence kinds: `licence` · `permit`. Maintenance tasks: `envelope-rotation` ·
`licence-renewal` · `approver-review` · `pipeline-consumption-review` ·
`risk-trigger-review`.

*Source: OPS-MAINT-001, D-166*

### 5.36 Phone signal

`none` · `clear` · `risk`, the answers of the host's phone signal provider; the record
says `unavailable` where none is declared.

*Source: AUTH-FACT-002b, LIB-HOST-001, D-166*

### 5.37 Send kinds and preference kinds

Send kinds, the `Kind` of `SendContext` (LIB-HOST-001): `email` · `sms`. Preference
kinds: `string` · `boolean` · `integer` · `enum`.

*Source: LIB-HOST-001, REG-PREF-001, D-162, D-166*

### 5.38 Recipient characterisation

`processor` · `recipient`

*Source: PRIV-ROPA-002, LIB-HOST-001, D-166*

### 5.39 Consent and membership changes

`ConsentChanged` carries `granted` · `withdrawn` · `superseded`; `MembershipChanged`
carries `began` · `ended`.

*Source: PRIV-CONS-007, PRIV-CONS-008, IDN-MEM-001, section 5b, D-166*

### 5.40 Hosting location

`inside` · `outside` (Egypt), the value set of `hosting.location` and the `location` of
a declared recipient.

*Source: INT-HOST-001, PRIV-ROPA-001, D-162, D-166*

### 5.41 Key rotation kinds

`key-encryption-key` · `fingerprint-key`, the `kind` of the `ops.keyrotation.*` rows.

*Source: OPS-SEC-003, D-166*

### 5.42 Loosening direction

`increase` · `decrease` · `any-change`

Which way a change to a key loosens (section 4, Direction), as `GET /admin/config/{key}`
writes it in `direction`.

*Source: OPS-CFG-002, D-152, D-153, D-166*

### 5.43 Link kinds

The `<kind>` of every link the library sends, `<origin>/link#<kind>.<token>`: the origin
is the landing origin the host declares for the application the kind belongs to
(LIB-HOST-001), and the token travels in the fragment, so no server receives it in the
address. The set is closed; each application's one landing component dispatches on the
kind (`18` FE-VER-001).

Authentication application: `sign-in` · `registration` · `recovery` · `enrolment` ·
`invitation`.

Account application: `identifier` · `identifier-confirm` · `undo` · `deletion-cancel` ·
`reactivation` · `loss-report`.

Every kind acts only on a press, never on load, so a mail scanner's prefetch changes
nothing. A message kind of section 5.25 lands under one link kind: `signin-link` under
`sign-in`; `verification-link` under `registration` in a registration and under
`identifier` on an account; `recovery-link` under `recovery`; `enrolment-link` under
`enrolment`; `invitation-link` under `invitation`; `identifier-change-confirm` under
`identifier-confirm`; `identifier-removed` under `undo`; `deletion-notice` under
`deletion-cancel`; `deactivation-notice` under `reactivation`; `credential-suspended`
under `loss-report`.

*Source: API-LAND-001, FE-VER-001, LIB-HOST-001, D-166*

### 5.44 Former mailbox

`transfer` (the invitee receives the mailbox and the mail in it) · `replace` (the old
mailbox is removed and a new one reserved)

The `formerMailbox` member of `POST /admin/organizations/{id}/invitations`, required
where the corporate address's mailbox has been held before, by anyone, the invitee and
an erased holder included (`identity.invitation.mailboxheld`); the issue checks nothing
about who the invitee is. Either value is stepped up with the issue, carries the reason
and is recorded. No mailbox anyone has held passes to a holder without it, and the
library removes such a mailbox only under `replace`. The mail-server adapter adopts an
existing server account only where its description carries the library's mailbox
identifier; a refusal to adopt raises `degradation` at once (section 5.23).

*Source: REG-MAIL-001, REG-MAIL-003, INT-MAIL-001, INT-MAIL-006, D-166*

### 5.45 Mailbox states

`disabled` · `enabled` · `removed`

The state the library owes a mailbox and pushes to the mail server (`IMailServer`,
LIB-HOST-001; INT-MAIL-001, INT-MAIL-007). `disabled` is a server account whose holder
cannot authenticate, so no app password works, and which still receives mail; `enabled`
is one whose holder can; `removed` is the server account destroyed. A mailbox is owed
`enabled` only while its holder's account is `active` or `restricted` and holds a current
membership of the administrative organization, and `disabled` otherwise, a reservation
for an invitation included (INT-MAIL-006).

*Source: INT-MAIL-001, INT-MAIL-006, INT-MAIL-007, LIB-HOST-001, D-166*

### 5.46 Export formats and sections

The `format` of `GET /privacy/export`: `human` (grouped, labelled and ordered for
reading) · `machine` (flat, complete and stable-named). The `format` of
`GET /admin/ropa`: `template`. Each is case-sensitive; an absent or other value is
refused with `api.request.malformed` naming `format`.

The sections of the export, in this order (PRIV-RIGHT-003): `account` · `profile` ·
`identifiers` · `identifier-backup` · `credentials` · `recovery-codes` · `devices` ·
`preferences` · `memberships` · `membership-acknowledgements` · `group-memberships` ·
`grants` · `assurance` · `sessions` · `consents` · `objections`.

*Source: PRIV-RIGHT-003, PRIV-ROPA-001, `09` `GET /privacy/export`, `09` `GET /admin/ropa`, D-166*

### 5.47 Provider round-trip intents

`signin` · `register` · `link`, the `intent` of `GET /auth/providers/{provider}`. A
`link` round trip is bound to the browser's session, the others to its
pre-authentication session.

*Source: IDN-LIFE-012, REG-IDENT-008, `09` `GET /auth/providers/{provider}`, D-166*

### 5.48 Application kinds

`Management` · `Public`, the `ApplicationKind` a host declares at registration
(`AddJanus`) for the application a process serves; it decides `SameSite` alone
(BFF-CSRF-005). The unset value is `Management`.

*Source: LIB-HOST-001, BFF-CSRF-005, D-166*

### 5.49 Reading actions

`read` · `list` · `export`

A permission action named so is reading; every other action is modifying unless the
model builder declares it reading. A restricted account is admitted its own reading
actions and refused every modifying one with `authz.restricted` (AUTHZ-GATE-006). A
host-declared permission whose action is `export` is an export operation (OPS-ALERT-006);
the library declares none.

*Source: AUTHZ-GATE-006, OPS-ALERT-006, D-160, D-166*

---

## 5a. Step-up actions — library-owned

The actions on the library's own surface that require step-up under the principal's
policy (AUTH-STEP-001, AUTH-STEP-002). Listed once here so a builder does not have to
infer them endpoint by endpoint; `09` marks each. Every `/admin` operation that touches
another person's account or loosens a control has one (`09` section 8a).

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
| `invitation:issue` | Issue an invitation | `POST /admin/organizations/{id}/invitations`; an invitation naming a role also passes `grant:manage`, and a `formerMailbox` (section 5.44) is stepped up with the issue |
| `membership:end` | End a membership | `DELETE /admin/organizations/{id}/memberships/{subject}` (IDN-MEM-001, REG-MAIL-003) |
| `grant:manage` | Grant, revoke or change roles and grants; change a group's members | `/admin/grants/*`, `/admin/roles/*`, `POST` · `DELETE /admin/groups/{id}/members` |
| `account:suspend` | Suspend an account | `POST /admin/accounts/{subject}/suspend` |
| `account:reactivate` | Reactivate a suspended account | `POST /admin/accounts/{subject}/reactivate` |
| `account:restrictionlift` | Lift a processing restriction | `POST /admin/accounts/{subject}/restriction/lift` (PRIV-RIGHT-004) |
| `account:deletioncancel` | Cancel a deletion on the subject's behalf | `POST /admin/accounts/{subject}/delete/cancel` (IDN-LIFE-003, IDN-ACCT-007) |
| `account:sessionsrevoke` | End another person's sessions | `POST /admin/accounts/{subject}/sessions/revoke` |
| `session:revokeall` | End every session | `POST /admin/sessions/revoke-all` |
| `account:takedown` | Execute a takedown | `POST /admin/accounts/{subject}/takedown` |
| `account:takedownreverse` | Reverse a takedown | `POST /admin/accounts/{subject}/takedown/reverse` |
| `privacyrequest:fulfil` | Fulfil a privacy request | `POST /admin/privacy/requests/{id}/fulfil`, for every request type (section 5.12c): an erasure's fulfilment starts a grace window, a restriction's restricts another person's account; refusing is not gated (PRIV-RIGHT-001) |
| `erasure:complete` | Complete a stuck erasure, takedown or restriction delivery manually | `POST /admin/erasures/{id}/complete` |
| `config:loosen` | Loosen any security setting | `PUT /admin/config/{key}` where the change is a loosening (OPS-CFG-002); a tightening is not gated |
| `alerting:destinations` | Change alert destinations | `PUT /admin/config/{key}` for the `alerting.*.destinations` keys (OPS-ALERT-004a) |
| `policy:change` | Change an organization's policy | `PUT /admin/organizations/{id}/policy`: every replacement, a tightening included |
| `organization:delete` | Request or cancel an organization's deletion | `POST /admin/organizations/{id}/delete`, `/delete/cancel` (IDN-ORG-003); creating an organization is not gated |
| `domain:manage` | Add, verify or remove a locked domain | `/admin/organizations/{id}/domains/*` (REG-DOM-001) |
| `restriction:edit` | Edit a sending restriction | `PUT/DELETE /admin/restrictions/{name}` (AUTH-ABUSE-004): every edit, with a reason |
| `restriction:grant` | Grant sends to a key | `POST /admin/restrictions/{name}/grant` (AUTH-ABUSE-004): the support role, a reason required |
| `breakglass:replace` | Generate a replacement break-glass credential | from a break-glass session, which satisfies step-up for its lifetime (AUTH-STEP-004), or by a stepped-up system administrator from the management application (OPS-BOOT-004, D-147) |

**Not gated** (D-166): revoking an invitation, which touches no account; creating an
organization; publishing legal documents and their translations; compliance records;
refusing a privacy request.

The name is the key of the policy's `gates` field (section 4.1a) and the value the
`09` endpoint declares; it follows the `resource:action` shape of CONV-NAME-002 but is
a gate name, not a permission string (D-151).

*Source: AUTH-STEP-001, D-132, D-146, D-147, D-151, D-166*

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
(INT-MAIL-007). No event names a consumer or a consumer's domain (IDN-LIFE-003a). Every
event is written to the outbox in the transaction that makes it true (CONV-DESIGN-002,
IDN-LIFE-003a).

| Event | Raised when | Consumers |
|---|---|---|
| `AccountRegistered` | The one transaction of the terms step has committed (REG-SESS-001, REG-SESS-007): the account exists, `active`, once per account; nothing fires for a registration session that is abandoned or expires | Host welcome — **no mailbox**: provisioning follows the invitation and enabling the membership (REG-MAIL-001) (INT-MAIL-006, IDN-LIFE-009b) |
| `TakedownReversed` | A takedown reversed inside its window; the account is back in the state it held at the trigger (section 5.12b) | Host: whatever the host did on `TakedownExecuted`, or on the trigger's `AccountSuspended`, is the host's to reconsider against that state |
| `OrganizationErased` | An organization's grace window elapsed and its erasure committed; carries the organization and the number of memberships ended, and names no subject | Host |
| `IdentifierChanged` | *Retired by D-146. See `IdentifierAdded`, `IdentifierRemoved`, `IdentifierPrimaryChanged`.* | |
| `IdentifierAdded` | An added email or phone was verified and counts (REG-IDENT-004); also a replace that completed (REG-IDENT-007), and a corporate address taken on at an invitation's acknowledgement (REG-INV-001) | Host |
| `IdentifierRemoved` | An identifier was removed (REG-IDENT-006), or a corporate address was retired when a membership ended (REG-MAIL-003); a later undo fires `IdentifierAdded` | Host |
| `IdentifierPrimaryChanged` | The primary of a kind changed (REG-IDENT-005), including on invitation acknowledgement when the corporate address becomes primary (REG-INV-001) and when a membership ends (REG-MAIL-003) | Host |
| `AccountSuspended` · `AccountReactivated` | State enters or leaves `suspended`, by the subject or an administrator. `AccountSuspended` is also written at every takedown trigger, whatever state the account held, announcing that access stopped (IDN-LIFE-003) | Host |
| `AccountDeletionRequested` · `AccountDeletionCancelled` | The grace window starts or is cancelled | Host (pause its own processing for the subject) |
| `ErasureRequested` | The erasure transaction has committed; host-side redaction is due (PRIV-RIGHT-005b) | Every registered subject-event handler — **required** |
| `RestrictionChanged` | `restricted` set or lifted | Every registered subject-event handler — **required** |
| `SendingRestrictionChanged` | A named restriction was created, edited or deleted through `/admin/restrictions/{name}` (AUTH-ABUSE-004); carries the restriction name, the actor and whether the change was a loosening. | Audit, alerting (OPS-ALERT-001) |
| `SendingRestrictionGranted` | Support added credit to one key under a restriction (AUTH-ABUSE-004); carries the restriction name, the credit, the actor and the reason, never the plain key value | Audit, alerting (OPS-ALERT-001) |
| `DeviceVerified` | A new-device check completed (AUTH-FACT-016); carries the browser identifier and no personal data | Host (optional) |
| `ExportRequested` | A subject export is assembled | Every registered subject-event handler — **required** |
| `TakedownExecuted` | Phase one of a takedown has committed; fires with `AccountSuspended`; `AccountDeletionRequested` does **not** fire for a takedown | Host (stop its own processing for the subject, as a required subscriber) |
| `MembershipChanged` | A membership begins or ends, one bootstrap attaches included; carries `change` (`began` · `ended`, section 5.39) | Host |
| `ConsentChanged` | A consent granted, withdrawn or superseded; carries the purpose and `change` (`granted` · `withdrawn` · `superseded`, section 5.39); on `withdrawn`, handlers erase data held solely for the purpose (PRIV-CONS-008) | Every registered handler for the purpose — **required** |
| `ObjectionChanged` | An objection recorded or withdrawn for a purpose on an objectable basis (PRIV-RIGHT-001a) | Every registered handler for the purpose — **required** |
| `CredentialEnrolled` | An authenticator reached `active`, or a password was set on an existing account, by the person, by recovery or by an invitation, with the enrolment notification to every other recorded channel (AUTH-STEP-007); carries the credential identifier (absent for the password), the catalogue entry and the subject | Host (optional) |
| `CredentialSuspended` · `CredentialRestored` · `CredentialInvalidated` | A loss report started, by a report or by a removal that would lower reachable assurance, was cancelled, or completed after the window (AUTH-RECOV-007); each carries the credential identifier, the catalogue entry and the subject, and `CredentialSuspended` the instant the window ends | Host (optional) |
| `NotificationRequested` | The library needs a message delivered — verification, recovery, deletion, change notifications, alerts | The notification transport (INT-MAIL-008); retried by the outbox |
| `AlertRaised` | An OPS-ALERT-001 condition fires; carries the condition identifier (section 5.23), the severity and the structured details of the row. Written in the transaction that raised it, at every raise site; the alert channels carry it from that row after commit (OPS-ALERT-001) | Alert channels |

Mail provisioning consumes no event: it reads the state these events announce
(INT-MAIL-006).

*Source: LIB-API-001, IDN-LIFE-003a, PRIV-RIGHT-005b, CONV-DESIGN-002, D-022, D-132, D-141, D-146, D-162, D-166*

## 6. Status code usage

| Code | Used for |
|---|---|
| 200 | Success with a body |
| 202 | Accepted, outcome deliberately not disclosed; also `auth.credential.lastsecondfactor`, a status and not a refusal |
| 204 | Success, no body |
| 400 | Malformed request: not the shape the endpoint takes, a required member absent or empty, a free-text member outside 1 to 1024 characters after trimming, or a word outside a closed vocabulary fixed in this chapter or at startup (`api.request.malformed`) |
| 401 | No valid session — **session death only** |
| 403 | Authenticated, not permitted, existence not concealed; **also step-up required** (D-092). `authz.denied` means that a permission is absent, and carries only the other refusals `09` API-CONV-003 names |
| 404 | Not found: a path naming a runtime record the deployment does not hold, answered with a named code (under `/admin` nothing is concealed), **or** concealed denial; also a path no endpoint serves, or a method a served path does not take (`authz.resource.notfound`). 405 is not used |
| 409 | Conflict — duplicate, or state precondition failed |
| 422 | Well-formed, semantically rejected: a body naming something that does not exist or cannot be acted on, answered with a named code, `api.request.invalid` where none more specific exists |
| 429 | Throttled, carries `Retry-After` |
| 500 | An unhandled fault: `system.fault` with the correlation identifier and nothing else (BFF-ERR-002, D-153) |

**The status belongs to the code.** Each code of section 1 SHALL answer with the one
status its row names, at every endpoint that raises it; no endpoint gives a code a status
of its own. The one exception is `integration.callback.rejected`: 429 where the callback
rate limit refused the request and the refusal carries `details.retryAt`, 422 otherwise.
A code whose row names a fault, and a code no row names, SHALL be answered as
`system.fault` with 500 (BFF-ERR-002). A `model.*` code is a startup or command refusal,
and a conformance finding is a report of the suite (LIB-TEST-001); neither is answered by
a request.

*Source: API-CONV-003, D-162, D-166*

---

## 7. Maintenance

**REF-001**: Adding an error code, permission, configuration key, audit action, message
kind or any other value this document catalogues SHALL update this document in the same
change.

*Source: LIB-API-001, CONV-NAME-003, D-162, D-166*

**Acceptance criteria**
1. A contract test fails when a code or key exists in source but not here.
2. At each release commit the release gate compares the contract lists of the previous
   release commit with its own, read from their committed contract files: configuration
   keys with type, scope and constraints, and key families; the library-owned schema;
   error codes with their status; audit actions; permissions; endpoints with method,
   route, body members and statuses; and the browser profile's stage order. A removed or
   changed entry without a major version, or an added entry with only a patch version,
   fails the gate, and so does a catalogue whose declarations and read entries do not
   number the same.
3. A contract test fails when a code the library raises has no status in section 1.
4. A contract test fails when a permission the library declares is not a row of
   section 2.1.
