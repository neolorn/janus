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
| `identity.change.pending` | A change of this kind is already in progress: a replace of another value already staged for the identifier; a repeated replace of the staged value is a resend from the session that staged it, and from another session stages the replace afresh (REG-IDENT-007, D-189, D-190); 409 | IDN-LIFE-004, D-146, D-166, D-189, D-190 |
| `identity.identifier.primary` **(new)** | Removal refused: the identifier is the primary of its kind; set another primary first; 409 | REG-IDENT-006, D-146, D-166 |
| `identity.identifier.lastofkind` **(new)** | Removal refused: it would leave fewer than the required minimum of that kind (one verified email always; one verified phone while `registration.phone` is `required`); 409 | REG-IDENT-001, REG-IDENT-006, D-146, D-166 |
| `identity.identifier.domainnotallowed` **(new)** | The email's domain is outside the organization's verified `emailDomains` list. At a sign-in, and at a step-up for an email code, it is told only after a factor succeeds and counts no failure (AUTH-FACT-004, D-190); at an invitation's issue it carries no `details.member`; 422 | REG-DOM-001, IDN-ORG-006, D-146, D-166, D-190 |
| `identity.identifier.invalid` | The identifier is not one the operation takes: the value is not a well-formed identifier of a kind the operation accepts; a corporate address equals the personal email the same request names (REG-MAIL-001); or the identifier named is not one the account or the registration holds in the state the operation needs (of that kind; in single-address mode, for a replace), among them a `phoneCode` enrolment naming no verified phone identifier of the account, or naming one that no longer holds the number `begin` read before its unit of work began (D-197); a code at a verify route whose `{id}` names nothing held is `auth.code.invalid` (D-189). An unverified identifier made primary or named by the backup setting is `identity.identifier.unverified`, not this code. Where a body carries more than one identifier, `details.member` names the member; 422 | REG-IDENT-001, REG-IDENT-004, REG-IDENT-005, REG-IDENT-007, REG-INV-001, REG-MAIL-001, D-162, D-166, D-189, D-197 |
| `identity.identifier.unverified` | An unverified identifier, a pending add listed on the account included (REG-IDENT-004), is made primary of its kind or named by the kind's backup setting (`POST /account/identifiers/{id}/primary`, `PUT /account/identifiers/backup`); nothing changes; 409 | REG-IDENT-005, D-166, REG-IDENT-004, D-187 |
| `identity.identifier.locked` | The identifier is locked and nothing about it is the person's to change: an invitation bound it, a provider operates its mailbox, or it is the personal email a membership keeps, which is not removed, made primary or replaced while the membership lasts; 409 | REG-IDENT-010, REG-MAIL-001, D-162, D-166 |
| `identity.identifier.maximum` | The account or the registration already holds `identifiers.<kind>.max` identifiers of the kind, an account's pending adds counted (REG-IDENT-004), an invitation's corporate address would pass `identifiers.email.max` at its acknowledgement, or a pending add's verification finds the account's verified identifiers already filling the kind (REG-IDENT-004, D-188); an undo that restores a removed identifier counts verified identifiers alone, and one that moves a value back onto a standing identifier is never refused for it (REG-IDENT-006, D-189); where the maximum is one, the change is `PUT /account/identifiers/{id}/replace`; 409 | REG-IDENT-002, REG-IDENT-004, REG-IDENT-006, REG-IDENT-007, REG-INV-001, D-162, D-166, D-187, D-188, D-189 |
| `identity.invitation.identifiermismatch` **(new)** | At the acknowledgement, an email or phone the invitation binds is not verified on the accepting account, or the corporate address is held by an account or reserved for an undo to another account; at the press that begins a registration, an account already holds the bound email; 422 | REG-INV-001, REG-INV-002, D-146, D-166, D-187 |
| `identity.username.taken` **(new)** | The username belongs to another account, or is held after an erasure for `retention.consent`; disclosed by design, throttled per source; 409 | REG-IDENT-009, D-146, D-166 |
| `identity.username.reserved` **(new)** | The username is on the reserved list; 409 | REG-IDENT-009, D-146, D-166 |
| `identity.username.invalid` | The username fails the PRECIS UsernameCaseMapped profile, its length bounds, or holds no letter; 422 | REG-IDENT-009, D-155 |
| `identity.username.coolingoff` **(new)** | A second username change inside `identifiers.username.changecooloff`; `details` carries the cooling-off end; 409 | REG-IDENT-009, D-146, D-166 |
| `identity.profile.underage` **(new)** | The date of birth is under eighteen on a host with `registration.adultaffirmation` = `required`; the registration session, or the collection session (`21` REC-COL-001), ends; 422 | REG-PROF-002, D-146, D-166, D-221 |
| `identity.record.notfound` | A path names a subject no person record bears, or one outside the caller's reach (`21` REC-ADM-001); in a record access session, a record the session does not reach, and in an account's session one not attached to the account, alike; 404 | REC-REC-002, REC-ACC-001, REC-ADM-001, D-221 |
| `identity.record.stateconflict` | The operation does not apply to the state the person record is in (section 5.51): a deletion of one not `active` or `restricted`, a cancellation where no window of its own runs, a lift of one not `restricted`, an erasure of one `deleting` or `deleted`, a takedown of one `deleted`; `details.state` names the state; 409 | REC-REC-002, REC-ADM-002, REC-ADM-003, REC-TKD-001, D-221 |
| `identity.record.followsaccount` | The person record is attached to an account and follows it: an operation acting on its lifecycle by the record's own path (a lift, a cancellation, an erasure, a takedown, an out-of-band request); `details.account` names the account to act on; 409 | REC-ATT-002, D-221 |
| `identity.record.signedin` | A collection session begun, or a code for a person's records asked, from a browser holding a live sign-in session: nothing is staged or sent; 409 | REC-COL-001, REC-ACC-001, D-221 |
| `identity.record.nooffer` | An attachment by reference asked where no offer stands on the account's session (`21` REC-ATT-001); 409 | REC-ATT-001, D-221 |
| `identity.collection.incomplete` | A collection request out of its order: a contact before the age the session asks, an age the session does not ask or already holds; or, at `IRecordCollection.CollectAsync`, a staged contact unverified, an age the session asks unanswered, or no contact at all; 409 | REC-COL-001, REC-COL-002, D-221 |
| `identity.collection.expired` | `IRecordCollection.CollectAsync` called where the browser of the request being served holds no collection session: none begun, or one expired, ended or spent; nothing is written; 422 | REC-COL-002, D-221 |
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
| `identity.account.notfound` | An administrative operation names in its path a subject no account bears (`/admin/accounts/{subject}/...`), save the contact and standing reads, which take a person record's subject too (`21` REC-ADM-001); an out-of-band privacy request whose body names a subject neither an account nor a person record bears is `api.request.invalid` (422) instead (PRIV-RIGHT-001, D-183, D-221); 404 | IDN-LIFE-013, IDN-LIFE-003, PRIV-RIGHT-004, IDN-ATTR-003, D-166, D-183 |
| `identity.account.stateconflict` | The operation does not apply to the state the account is in: a takedown of a `deleted` account, a suspension of one `deleting` or `deleted`, a reactivation of one no administrator suspended, a restriction lifted from one not `restricted`, a cancellation where no grace window runs. `details.state` names the state (section 5.1) and, where it is `suspended`, `details.suspendedBy` the origin (section 5.12b); 409 | IDN-ACCT-007, IDN-LIFE-013, IDN-LIFE-003, PRIV-RIGHT-004, D-166 |
| `identity.takedown.windowelapsed` **(new)** | Takedown reversal requested after its window, whether or not the account has been erased; 422 | IDN-LIFE-003, D-127, D-166 |
| `identity.change.windowelapsed` **(new)** | Undo of an identifier removal or replace presented after `identifier.change.coolingoff`, or once a write of the value to the account has ended its reservation; 422 | REG-IDENT-006, REG-IDENT-007, D-140, D-146 (was IDN-LIFE-007, IDN-LIFE-010), D-166, D-187 |
| `identity.takedown.active` **(new)** | A deletion cancellation, or a second takedown, attempted on a takedown-originated `deleting`; use `/takedown/reverse`; 409 | IDN-LIFE-003, D-137, D-166 |
| `identity.takedown.notfound` | The account, or the person record, holds no takedown to read or reverse (`GET /admin/accounts/{subject}/takedown`, `GET /admin/records/{subject}/takedown`, `POST .../takedown/reverse`); 404 | IDN-LIFE-003 AC2, REC-TKD-001, D-166, D-221 |
| `identity.deletion.windowelapsed` **(new)** | The deletion grace window of the account, the person record or the organization has closed; cancellation is no longer possible; 422 | IDN-ACCT-007, IDN-ORG-003, REC-ACC-002, D-106, D-166, D-221 |
| `identity.invitation.expired` | An invitation token that opens no invitation (never issued, revoked, already used, or past its lifetime); at the acknowledgement, an invitation revoked, already acknowledged, past its lifetime, whose organization's deletion was requested or which was erased, or whose inviter no longer holds what issuing it required; at revocation, one already acknowledged; 422 | IDN-LIFE-009a, REG-INV-001, D-106, D-147, D-166 |
| `identity.invitation.notfound` | No standing invitation answers: `GET /account/invitation` or the acknowledgement names none attached to the signed-in account (one attached to another account is answered alike), or a revocation names one the organization did not issue; 404 | REG-INV-002, IDN-LIFE-009a, D-166 |
| `identity.invitation.addressrequired` | An invitation into an organization whose mail is integrated names no personal `email`, no `corporateEmail`, or the corporate address as the personal one; `details.member` names `email` or `corporateEmail`; 422 | REG-INV-001, REG-MAIL-001, D-166 |
| `identity.invitation.mailboxheld` | An invitation names a corporate address whose mailbox has been held before, by anyone (the invitee included; the address of an erased holder is not recognised, REG-MAIL-003), and its body carries no `formerMailbox` (section 5.44); the issue checks nothing about who the invitee is, and no mailbox changes; 409 | REG-MAIL-001, REG-MAIL-003, INT-MAIL-006, D-166, D-178 |
| `identity.mailbox.taken` | The corporate address an invitation asserts is held by a member, or reserved by a standing invitation that has not expired; `details.member` is `corporateEmail`; 409 | REG-MAIL-001, INT-MAIL-006, D-166 |
| `identity.mailbox.notfound` | The account holds no mailbox the mail server is told to enable, or the deployment registers no mail server; the app-password endpoints are not present for it; a creation from the break-glass session, or a session another application opened from it, is `authz.denied` instead, decided at the gate step before the mailbox is looked up (OPS-BOOT-002); 404 | INT-MAIL-006, REG-MAIL-002, OPS-BOOT-002, D-166, D-179 |
| `identity.registration.signedin` | `POST /register` from a browser holding a live session: nothing is staged, no account document is answered, and the frontend sends the person to the account application; where the request carries an invitation token, the invitation is first attached to the signed-in account (REG-INV-002), the attachment committed with the refusal, a kept write (CONV-DESIGN-003, D-207); also a provider round trip started to register from such a browser, carried in `error` (`09`, D-192); 409 | REG-SESS-002, D-162, D-166, D-192, D-207 |
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
| `auth.code.expired` | A verification code or an authentication code (the `emailCode` code, the `phoneCode` second-step code, the code of a `phoneCode` enrolment, the code a sign-in link shows in another browser) past its lifetime, or presented after its attempt cap, the correct code included (AUTH-FACT-004 AC3); also a registration link token, or an account's identifier link token (`POST /account/identifiers/{id}/verify`, D-188), that, pressed, opens nothing (REG-SESS-003 AC6), and an old address's confirmation of a replace pressed past its lifetime (REG-IDENT-007); and the right code or press of an add, or the presentation that would apply a replace's swap, where an identifier has come to hold its value, or an undo to reserve it, since it was staged (REG-IDENT-004, REG-IDENT-007); and, in an enrolment session, a press naming any verification but that of the replace it staged (`09`, D-189); and a `phoneCode` enrolment's code presented where no enrolment is pending, as once its number has left the account (D-197); 422 | AUTH-FACT-004, REG-SESS-003, REG-IDENT-004, REG-IDENT-007, D-166, D-183, D-187, D-188, D-189, D-197 |
| `auth.code.invalid` | A verification code or an authentication code rejected; also a code at a verify route whose `{id}` names nothing the registration session holds staged or the account holds pending, or, in an enrolment session, names any verification but that of the replace it staged (`09`, D-189); 422 | AUTH-FACT-004, D-166, D-189 |
| `auth.code.replayed` **(new)** | Code already consumed within its window; 422 | AUTH-FACT-005, D-166 |
| `auth.factor.notpermitted` | A factor the account may not use here: a verification-only factor offered as authentication, or a factor the policy in force does not permit in `loginFactors`, presented at sign-in, at step-up or to link; also, at a provider round trip's start, a provider the deployment does not declare, or for a link one the policy in force does not permit, carried in `error` (`09`, D-192, D-193); also, at a sign-in, a second step presented under its own name before a first factor has been accepted on the challenge, refused before it is verified, counting and recording nothing (AUTH-FACT-002b, D-197), and, before a first factor, an assertion presented as `passkey` whose credential is a second-factor security key of the account the sign-in is for, refused once it verifies, counting and recording nothing and keeping none of its verification's writes, while one whose credential that account does not hold, or one presented at a sign-in whose identifier names no account, is `auth.factor.rejected` (D-198, D-199); and a `phoneCode` enrolment where the policy in force does not permit `phoneCode` or the number's signal answers `risk` (D-197); 422 | AUTH-FACT-002, AUTH-FACT-002b, AUTH-STEP-002, IDN-LIFE-009b, D-166, D-192, D-193, D-197, D-198, D-199 |
| `auth.factor.rejected` | Factor presented and refused; also, on a provider's return for every intent, a provider's own `error` (a cancel included) or a return with no `code`, which present nothing and are neither counted nor recorded, carried in `error` (`09`, D-193); also a WebAuthn credential or assertion refused at a step the `Fido2` verification adds (AUTH-FACT-014, D-197); 422 | AUTH-FACT-001, AUTH-FACT-014, D-166, D-193, D-197 |
| `auth.factor.required` **(new)** | Further factors needed to reach required assurance; 422 | AUTH-FACT-001, D-166 |
| `auth.lossreport.notpermitted` | Self-service loss report and removal unavailable to this account; 409 | AUTH-RECOV-008, D-141, D-166 |
| `auth.lossreport.pending` **(new)** | This authenticator is already suspended, by a loss report, by a removal that would lower the account's reachable assurance, or, for a `phoneCode` credential, by the number it is bound to leaving the account (AUTH-FACT-002, D-197); `details.invalidatesAt`; 409 | AUTH-RECOV-007, AUTH-FACT-002, D-141, D-166, D-190, D-197 |
| `auth.credential.suspended` **(new)** | A suspended authenticator (reported lost, suspended by a removal that would lower the account's reachable assurance, or a social credential a provider's security event holds) was presented and what it presented verified (a social credential's provider vouched, answered in the provider return's `error`), or a `phoneCode` ask after a first factor or under a session named a suspended number where the policy permits `phoneCode` (one before a first factor, or where the policy does not permit it, is answered 202); a right `phoneCode` code whose credential's number left the account is `auth.factor.rejected` instead (AUTH-FACT-004, D-197), and a second step presented at a sign-in before a first factor, including an assertion presented as `passkey` whose credential is a security key of the account the sign-in is for, is `auth.factor.notpermitted` (AUTH-FACT-002b, D-197, D-198), an assertion whose credential the account the ceremony is for does not hold being `auth.factor.rejected` before it is verified (D-199, D-200); a presentation counts as a failed attempt, an ask counts nothing (AUTH-ABUSE-001); 422 | AUTH-RECOV-007, AUTH-FACT-004, D-141, D-166, D-190, D-191, D-197, D-198, D-199, D-200 |
| `auth.credential.notfound` | The account holds no active credential by that identifier of the kind the operation acts on, including an app password the mail server does not hold for the person; a credential of another account answers the same; 404 | AUTH-FACT-001, INT-MAIL-010, D-162, D-166 |
| `auth.credential.labelinvalid` | A credential or app-password label that is empty, over 64 characters, or already held by another credential of the same kind on the account in any capitalisation (OPS-DB-001); 422 | AUTH-FACT-001, REG-MAIL-002, D-162, D-166 |
| `auth.credential.notupgradable` | The credential named at `POST /account/credentials/{id}/upgrade` is the account's and is not a second-factor security key; 409 | AUTH-FACT-002b, D-162, D-166 |
| `auth.factor.passwordrequired` **(new)** | A second step (a TOTP generator, a second-factor security key, a `phoneCode` credential, D-197) or a recovery-code set asked for on an account that holds no password (AUTH-FACT-002b); 409 | AUTH-FACT-002b, AUTH-FACT-008, D-183, D-197 |
| `auth.factor.notenrolled` **(new)** | The account holds no enrolment of the kind the operation acts on (no recovery-code set to record as exported); 409 | AUTH-FACT-008, D-183 |
| `auth.password.blocklisted` | Password found in the compromised corpus; 422 | AUTH-PASS-004, D-166 |
| `auth.password.tooshort` | Below the floor for this factor count; 422 | AUTH-PASS-001, D-166 |
| `auth.password.toolong` | A password longer than `password.maximum` at registration, change or reset; nothing is truncated; no details; 422 | AUTH-PASS-001 AC3, D-162, D-166 |
| `auth.recovery.channelnotonaccount` | Channel not among the account's recorded channels; 422 | AUTH-RECOV-003, D-166 |
| `auth.recovery.reasonrequired` | Written reason absent; 422 | AUTH-RECOV-002, D-166 |
| `auth.recovery.tokenexpired` | Recovery link past its lifetime; 422 | AUTH-RECOV-002, D-166 |
| `auth.recovery.tokeninvalid` | Recovery link rejected; 422 | AUTH-RECOV-002, D-166 |
| `auth.recovery.selfapproval` **(new)** | An approver attempted to approve recovery for their own account; enforced in the domain; 422 | AUTH-RECOV-002a, D-147, D-166 |
| `auth.screening.unavailable` **(new)** | Blocklist screening could not run; operation refused; 422 | AUTH-PASS-004, D-166 |
| `auth.session.expired` | Session past idle or absolute limit; `details.reauthenticate` is `single-factor` when one factor (passkey or password) restores the session — idle expiry inside the absolute window, **AAL2-policy principals only** — or `full`; the break-glass session answers `full` on any expiry (OPS-BOOT-002); also a registration session that has ended, among them one ended at the terms step because a staged identifier was taken or reserved since (REG-SESS-005), answered with no `details`, and an enrolment session that has ended, alike (D-188), or that is presented on a route that requires a session and is not one it reaches (D-189); on a navigation (a sign-on refused other than for its `state`, the `error` or the library's own code it carried inside logged once beside it, a return carrying neither `code` nor `error` and one whose `error` does not read as RFC 6749 Appendix A.7 defines one or is longer than 64 characters, each logged as `api.request.malformed` and that `error` in no entry the library writes (D-195, D-196), and an identity token whose `sid` names a session that has ended or is not found among them; an error of the push, an error of the exchange other than a 400 `invalid_grant`, an authorization response's `server_error` or `temporarily_unavailable`, the client in no registry, its secret or the keys that cannot be read, an identity token that does not hold up or carries no session identifier in `sid` (D-196), or a derivation code that is a fault's being a fault; a provider round trip begun with no session or registration session, or a registration's return whose registration session has ended) it is carried in `error` instead, a fault staying `system.fault` (BFF-ERR-001, D-192, D-193, D-194, D-195, D-196); 401 | AUTH-SESS-005, REG-SESS-005, D-123, D-139, D-166, D-186, D-188, D-189, D-192, D-193, D-194, D-195, D-196 |
| `auth.session.csrfinvalid` **(new)** | A state-changing browser request refused by a layer of `17` section 4: fetch metadata, the custom request header, the origin or the synchronizer token; one code for every layer, which only the log entry names; also a sign-on or provider round trip whose `state` is absent, unbound or not the one the browser was sent out with, or whose attempt is not found, a link's return whose session has ended among them (BFF-SESS-006, IDN-LIFE-012, D-194); 403 | AUTH-SESS-007, BFF-CSRF-001 to BFF-CSRF-004, D-162, D-166, D-194 |
| `auth.stepup.required` **(new)** | The gate is not met; `details` carries `required` (`level`, `phishingResistant`, `maxAge` in whole seconds, null where the gate asks none, D-193), `outcome` (`present` · `enrol` · `report-loss` · `pending`), `options` (the combinations of catalogue identifiers that would meet it) and `pendingUntil`, null unless the outcome is `pending`. At the invitation acknowledgement `details` carries `outcome` `enrol` and `policyRequirement` `{ field, value }` in place of the gate values. A gate judged from a host's assurance report (LIB-HOST-004), where no session of the library that is the acting person's own carries the request, that is not met (a provider failure included) carries `required`, `outcome` `present`, empty `options` and a null `pendingUntil`. Otherwise no `details` where no session of the library belonging to the acting account was judged (`09` `/auth/step-up`); on a navigation it is carried in `error` alone, with no `details` (BFF-STEP-001, D-192); 403 | AUTH-STEP-001, AUTH-STEP-002, LIB-HOST-004, D-141, D-166, D-183, D-192, D-193 |
| `auth.stepup.unavailable` **(new)** | No assurance provider registered; 403 | AUTH-STEP-003, D-166 |
| `auth.provider.unavailable` **(new)** | A social provider's round trip could not reach or read the provider: its discovery document (one naming no HTTPS authorization or token endpoint included), its published keys or its token endpoint (every answer but a success holding an identity token and an `invalid_grant` refusal included: a 5xx, a 429, `invalid_client`); the browser is returned with it in `error` (BFF-ERR-001), and no failed attempt is counted; 502 | IDN-LIFE-012, D-192, D-193 |
| `auth.throttled` **(new)** | A progressive delay or a rate limit is in effect; `details.retryAt` is the instant the next attempt is looked at, and the answer carries `Retry-After`; a throttled navigation (a social provider's return) carries `error=auth.throttled` and `retryAt` in its query instead (BFF-ABUSE-001); 429 | AUTH-ABUSE-001, AUTH-ABUSE-002, AUTH-RECOV-002, OPS-ALERT-006, D-166 |
| `auth.restriction.exceeded` **(new)** | A send refused by a named restriction; `details.retryAt` is the earliest instant at which every bucket the send exceeds has room for it, identical whether or not the address is registered (AUTH-ABUSE-002, D-197). Replaces `integration.sms.windowactive`; on a navigation it is carried in `error`, with `retryAt` in the query (BFF-ABUSE-001, D-192); 429 | AUTH-ABUSE-004, D-146, D-166, D-192, D-197 |
| `auth.restriction.notfound` | The restriction named in the path is not in the set (`GET` and `DELETE /admin/restrictions/{name}`, `POST /admin/restrictions/{name}/grant`); 404 | AUTH-ABUSE-004, D-166 |
| ~~`auth.device.verificationrequired`~~ | *Retired by D-166. The held sign-in is the `status` value `deviceVerificationRequired` of `POST /auth/factor` (section 5.33), answered 200, not a code.* | |
| `auth.policy.graceexpired` **(new)** | The account does not meet a raised requirement and `policy.enforcement.grace` has elapsed; sign-in stops at enrolment. `details.outcome` is `enrol`; 403 | AUTH-FACT-017, D-146, D-166 |
| `auth.enrolment.tokeninvalid` **(new)** | Enrolment link token unknown or consumed, at `POST /enrol/begin` alone; an ended enrolment session presented elsewhere is 401 `auth.session.expired` (D-188); 422 | AUTH-RECOV-002, IDN-LIFE-009a, D-140, D-166, D-188 |
| `auth.breakglass.invalid` **(new)** | Emergency credential rejected: malformed, a check symbol that does not hold, or any code but the issue last used, a replaced issue included; 422 | D-065, OPS-BOOT-004, D-166 |
| `auth.credential.lastsecondfactor` | Removing this credential would lower the account's reachable assurance; it is suspended now and invalidated after the window (AUTH-RECOV-007). A 202 status, not a refusal: `details.invalidatesAt` carries the instant of invalidation. In process the removal answers it as the failure outcome of its `Result` (CONV-DESIGN-005), and the suspension is committed in the removal's own unit of work, under the lock its decision read, as the operation's success (CONV-DESIGN-003, D-207) | D-092, D-135, D-141, D-166, D-207 |
| `auth.breakglass.consumed` **(new)** | The code of the issue last used, presented again, the second of two concurrent uses included; 409 | D-065, OPS-BOOT-002, D-166 |
| `auth.webauthn.algorithmnotallowed` | Signature algorithm outside the allow-list; an assertion is answered it only for a credential the account the ceremony is for holds (D-200); 422 | AUTH-FACT-014, D-166, D-200 |
| `auth.webauthn.countermismatch` **(new)** | Signature counter did not advance, where it or the stored value is non-zero — possible cloned credential; only for a credential the account the ceremony is for holds (D-200); 422 | AUTH-FACT-014, D-166, D-191, D-200 |
| `auth.webauthn.rpidchanged` **(new)** | Credential enrolled under a different relying party identifier; an assertion is answered it only for a credential the account the ceremony is for holds (D-200); 422 | AUTH-FACT-011, D-166, D-200 |
| `auth.webauthn.userverificationrequired` | User verification did not occur; an assertion is answered it only for a credential the account the ceremony is for holds (D-200); 422 | AUTH-FACT-014, D-166, D-200 |
| `auth.challenge.required` | A bot-defence signal fired at `POST /register` and the host declared a challenge verifier; no session is created until the request is repeated with a passing `challengeToken` (AUTH-ABUSE-008); 403 | AUTH-ABUSE-008, D-153, D-166, D-188 |
| `auth.oidc.nonconformant` | A conformance finding, raised by no request: the provider admitted a form AUTH-OIDC-006 retires, a pushed request naming a destination other than the client's registered one included, or its discovery document lists one. `details` carry `probe` (`discovery` · `push` · `token`), `field`, `sent`, `expected`, `status` and `error`, or, for the document, `probe`, `member` and `listed` | AUTH-OIDC-006 AC1, LIB-TEST-001, D-164, D-166 |

### 1.3 Authorization

| Code | Meaning | Source |
|---|---|---|
| `authz.denied` **(new)** | Permission absent; used where existence is not concealed. Under `/admin`, an identifier naming no row whose organization the permission is asked in (a group, a grant, a registered record) is refused with it exactly as a missing permission is (`09` section 8); so are the refusals OPS-BOOT-002 gives on the reserved account, bootstrap or a key rotation run where it may not, and, in process, an enrolment session's authority given to an operation that can take it and that the session does not reach (over HTTP it is no session there, `auth.session.expired`); and the export's report in an enrolment session whose second step has shown no codes (`09`, D-188, D-189, D-190); and a host job's context where IDN-PRIN-001 refuses it: the capability page, the explanation, a library operation that meets a gate, and any permission while a request is served (D-206, D-207); and a grant on `resourceType` `organization` whose `resourceId` names no organization the deployment holds, recorded against no organization (AUTHZ-SCOPE-001, D-209). A refusal the gate records (AUTHZ-CONCEAL-004) carries `details.correlation`, its audit record's identifier, which the explanation routes take (D-208); 403 | AUTHZ-CONCEAL-005, OPS-BOOT-001 AC1, OPS-BOOT-002, OPS-SEC-003 AC1, CONV-DESIGN-002, IDN-PRIN-001, D-166, D-188, D-189, D-190, D-202, D-206, D-207, D-208, D-209 |
| `authz.grant.duplicate` | An identical live grant exists; 409 | AUTHZ-GRANT-003, D-166 |
| `authz.grant.expired` **(new)** | A grant written with an expiry that is not after the present; 409 | AUTHZ-GRANT-003, D-166 |
| `authz.grant.notfound` | A revocation names a grant already revoked, or one a derivation wrote, and the caller holds `grant:manage` where it is scoped, or, for a grant in an organization other than the administrative organization, `system:administer` in the administrative organization (AUTHZ-SCOPE-001, D-207); an identifier naming no grant is `authz.denied` (`09` section 8); 404 | AUTHZ-GRANT-001, AUTHZ-SCOPE-001, D-166, D-207 |
| `authz.grant.unresolved` | A grant names a role the deployment does not hold, or a group that does not exist or belongs to another organization; `details.member` names `role` or `subjectId`. Also an invitation whose `roles` names a role the deployment does not hold, `details.member` `roles`, and a role whose `handsOut` names one, `details.member` `handsOut`; 422 | AUTHZ-GRANT-001, AUTHZ-GRANT-005, REG-INV-001, D-166, D-212 |
| `authz.grant.reasonrequired` | A grant created or revoked without a non-empty `reason`; 422 | AUTHZ-GRANT-003, D-153 |
| `authz.role.notassignable` **(new)** | The actor may not hand out a role the change touches (a grant made or revoked, allow or deny, an invitation's roles, a change of a group's members), or a role change reaches beyond what the actor may give or hand out; 403 | AUTHZ-GRANT-005, D-212 |
| `authz.grant.self` **(new)** | The change would widen the actor's own access: an allow grant to the actor or a group it belongs to, the revocation of a deny on them, adding them to a group, removing them from a group that reaches a deny, or acknowledging an invitation they issued; 403 | AUTHZ-GRANT-005, D-212 |
| `authz.role.inuse` | A grant (live, expired or revoked), a declared derivation or a standing invitation names the role, so it is not removed; its permissions can be changed instead; 409 | AUTHZ-GRANT-004, AUTHZ-GRANT-003 AC3, AUTHZ-DERIVE-001, REG-INV-001, D-166 |
| `authz.resource.inuse` **(new)** | A registration is removed while a record is registered inside it (`IResources.RemoveAsync`); its contents are removed first; 409 | AUTHZ-INHERIT-002, D-203 |
| `authz.role.notfound` | A role named in the path (`DELETE /admin/roles/{name}`) is not one the deployment holds; a role a body names that the deployment does not hold is `authz.grant.unresolved` (422); 404 | AUTHZ-GRANT-004, D-166 |
| `authz.group.cycle` **(new)** | Adding the member would make a group contain itself; 409 | AUTHZ-GROUP-001, D-106, D-166 |
| `authz.group.inuse` | The group holds a member, belongs to a group, or was given a grant (live, expired or revoked), so it is not removed; 409 | AUTHZ-GROUP-001, AUTHZ-GRANT-003 AC3, D-166 |
| `authz.policy.unregistered` **(new)** | Entity has no registered policy: a fault, not a denial, answered as `system.fault` (500). The conformance suite reports such an entity under this code with `details.entity` | AUTHZ-GATE-001, LIB-TEST-001, D-166 |
| `authz.restricted` | The acting account's processing is restricted (D-207): a modifying action, or a change to the account's identifiers, credentials, profile or preferences, or an invitation acknowledgement, asked by a restricted account, the creation of a mail app password included. What IDN-ACCT-007 keeps available is admitted: ending sessions, reporting a credential lost, listing and revoking app passwords, the link-borne undo of an identifier change, a credential set by recovery or enrolled where a policy hold stops its sign-in (AUTH-FACT-017), and the report of a recovery-code export that completes an enrolment session (AUTH-RECOV-006, D-189); 403 | AUTHZ-GATE-006, IDN-ACCT-007, INT-MAIL-010, D-166, D-189, D-207 |
| `authz.resource.notfound` | One not-found answer where nothing is to be told, meaning "nothing here" to the frontend whatever the case: a request in which the gate refused a record of a type that conceals its records, whether or not the record exists and whatever the endpoint wrote after the refusal, answered by the browser profile's stage 11 and never by an operation, `details.correlation` the audit record of the refusal and nothing else; a record named on an `/account` route that is not the caller's (a device or a session of another account, or none), with empty `details`; a request under the mount that no endpoint serves, a path no endpoint matches or a method a matched path does not take, with empty `details` on both profiles; `GET /register/events` without the registration session's cookie (BFF-CSRF-005b), with empty `details`; 404 | AUTHZ-CONCEAL-001, AUTHZ-CONCEAL-004, BFF-ERR-003, BFF-ORDER-001 stage 11, LIB-API-003 AC4, API-CONV-003, CONV-DESIGN-002, BFF-CSRF-005b, D-166, D-187 |
| `authz.derivation.sourcesmissing` | A check, capability query or explanation on a type that a non-materialised derivation is declared on or reaches through containment was made without the host-supplied sources, whatever the role the derivation confers allows; a fault, not a denial, answered as `system.fault` (500) | AUTHZ-DERIVE-001, D-161, D-162, D-166 |
| `authz.truthtable.disagreement` | A conformance finding, raised by no request: a case of the host's truth table that the single check or the list filter decides otherwise than the table states. `details` carry `type`, `scenario` (section 5.30), `permission`, `expected`, `check` and `filter`, for a derived case `derivation`, the relationship, and for a step-up case `gate`, the action's gate name | LIB-TEST-001 AC2, AUTHZ-TEST-001, D-166, D-183 |

### 1.4 Privacy

| Code | Meaning | Source |
|---|---|---|
| `privacy.consent.required` **(new)** | Processing attempted without recorded consent; 403 | PRIV-SENS-002, D-166 |
| `privacy.consent.superseded` **(new)** | Notice version no longer current; re-consent needed; also a registration's terms step or a collection naming a `noticeVersion` or `termsVersion` that is not the current version of its document (section 5.52), `details.member` naming it (REG-SESS-007, `21` REC-COL-002); 422 | PRIV-CONS-007, REG-SESS-007, REC-COL-002, D-166, D-221, D-222 |
| `privacy.consent.writtenrequired` **(new)** | Sensitive data requires written consent; 422 | PRIV-BASIS-003, D-166 |
| `privacy.purpose.noconsent` | A consent is granted or withdrawn, at the dashboard, at the terms step, at a collection or a staff entry of a person record (`21` REC-COL-002, REC-COL-003), on a purpose the deployment did not declare or whose basis does not carry `IsConsent`; 422 | PRIV-BASIS-001, PRIV-CONS-011, REG-SESS-007, D-162, D-166 |
| `privacy.notice.unpublished` | A consent is granted, or an objection recorded, before any version of the document that governs its purpose is published (the privacy notice where the purpose names none); also a terms step or a collection naming a version of `terms` or of `privacy-notice` where none is published (section 5.52), `details.document` naming it (D-222); 409 | PRIV-CONS-001, PRIV-CONS-005, PRIV-CONS-007, PRIV-RIGHT-001a, D-162, D-166 |
| `privacy.document.notfound` | A legal document, or a named version of one, that does not exist or was never published is read, or has a translation filed against it, by a path naming it; a document an invitation's `documents` names that is not published is `api.request.invalid` (422); 404 | PRIV-CONS-005, PRIV-CONS-006, D-162, D-166 |
| `privacy.request.duplicate` **(new)** | An identical request is already open; 409 | PRIV-RIGHT-001, D-166 |
| `privacy.request.notfound` | A decision names no privacy request, told only to a caller holding `privacyrequest:manage`; 404 | PRIV-RIGHT-001, D-162, D-166 |
| `privacy.request.decided` | A decision on a privacy request already decided; the standing decision is not replaced; 409 | PRIV-RIGHT-002, D-162, D-166 |
| `privacy.purpose.notobjectable` **(new)** | The purpose's basis carries no right to object; 422 | PRIV-RIGHT-001a, D-145, D-166 |
| `privacy.notice.onelanguage` | *Retired by D-146. See `privacy.notice.governingtextmissing`.* | |
| `privacy.notice.governingtextmissing` **(new)** | A document version was submitted without its governing-language text; translations are optional and never suffice; 422 | PRIV-CONS-005, PRIV-CONS-006, D-146, D-166 |
| `privacy.request.receivedfuture` **(new)** | `receivedAt` later than now; 422 | PRIV-RIGHT-002, D-136, D-166 |
| `privacy.erasure.notfailed` **(new)** | Manual completion requested for a delivery of any kind of section 5.50 that has not exhausted its retries; 409 | IDN-LIFE-003a, D-106, D-166, D-203 |
| `privacy.erasure.notfound` | A delivery is read at `/admin/erasures/{id}` and none, of any kind, is held under the identifier, or a manual completion names no delivery of any kind of section 5.50; 404 | IDN-LIFE-003a, IDN-LIFE-003b, D-166, D-203 |
| `privacy.subject.erased` **(new)** | A host's field is encrypted or read for a subject whose key is erased (`SubjectFields.Encrypt`, `Decrypt`, `DecryptBytes`); no personal data is written or read for a person who has been erased; 409 | PRIV-RIGHT-005a, D-203 |

### 1.5 Configuration and model

| Code | Meaning | Source |
|---|---|---|
| `config.key.protected` **(renamed)** | Setting is not changeable through the application; 422 | OPS-CFG-004, D-166 |
| `config.value.belowfloor` | Value below the enforced minimum; 422 | OPS-CFG-003, D-166 |
| `config.value.aboveceiling` | Value above the enforced maximum; 422 | AUTH-SESS-005, D-166 |
| `config.value.notallowed` | Value outside the key's enum or set, or of the wrong JSON type. For `restrictions`, a restriction whose name breaks the rule of section 4.5, whose `host:<name>` key has no registered supplier (`details.supplier`), or whose bucket list is empty. A system policy naming a locked domain (`details.field` `emailDomains`). A value whose feature needs a host declaration the deployment has not made: a policy's `photos` set to `true` without an image codec, or a domain added to a lock without a DNS resolver, `details.field` naming the field and `details.requires` the declaration (`imageCodec`, `dnsResolver`). From `configure` (OPS-CFG-004), a protected key's value outside its set, `token.signing.algorithm` other than `ES256` among them (AUTH-KEY-001); 422 | section 4 value types, OPS-CFG-004, OPS-CFG-008, LIB-HOST-001, D-151, D-166, D-181 |
| `config.change.reasonrequired` **(renamed)** | A configuration change submitted without a reason: a key at `/admin/config/{key}`, an organization's policy or its locked domains, any edit of the restriction set, a tightening included, or a restriction grant; and a protected key set by `configure` from the server without `--reason`; `details.key` names the setting where one is changed; 422 | OPS-CFG-002, OPS-CFG-004, OPS-CFG-005, OPS-CFG-008, AUTH-ABUSE-004, D-146, D-166 (renamed from `auth.restriction.reasonrequired`) |
| ~~`config.change.stepuprequired`~~ | *Retired by D-166. A loosening without step-up is refused with `auth.stepup.required` (`09` `PUT /admin/config/{key}`).* | |
| `config.value.lastdestination` | *Retired by D-225. Alert destinations are the host's.* | |
| `config.change.superseded` | *Retired by D-225. Alert destinations are the host's.* | |
| `config.policy.belowsystem` **(new)** | An organization policy field the replacement states is looser than the system default; `details.field` names the first such field in the order of section 4.1a; a field the replacement leaves out is not judged; 422 | AUTH-STEP-002a, D-143, D-166 |
| `api.request.malformed` | The request could not be read: its body is not the shape the endpoint takes; a member it requires is absent or empty; a free-text member is outside 1 to 1024 characters after trimming (API-CONV-002; a blank reason whose absence has a code of its own answers that code); or a member or path segment holds a word outside the closed vocabulary this chapter or the declared model fixes for it (a takedown trigger outside section 5.12d, a configuration key the route does not serve, an undeclared permission), or a route or query value or a body member that does not read as its type (an identifier, an app password's identifier outside its form, or a role, restriction or document name outside its rule), `details.member` naming it. `details.member` names the member the reader stopped at, or the one the endpoint required, by its name as the request writes it: a member inside another by the member names from the body's top joined by dots; an element of a list, or a member inside one, by the list's name; a member inside an element of a body that is itself a list by its own name; and never with a `$` root or a list index (API-CONV-002); it carries nothing of the member's value; where the body failed before any member, the refusal carries the code alone. Also: an authorization request the provider refused and cannot return to a client, `details.error` then carrying the protocol's code and nothing else (LIB-API-003); a registration through `IResources` whose member is absent or unreadable, a sensitive type naming no `subject` included (AUTHZ-INHERIT-002, IDN-LIFE-002a); for a `Janus.Cli` command, a command, argument or input it cannot read, answered as one JSON line (OPS-BOOT-001, OPS-SEC-001, DR-016); and, at the sign-on's return, the code a refusal logs as carried inside and never answers, for a return carrying neither `code` nor `error` or one whose `error` does not read as RFC 6749 Appendix A.7 defines one or is longer than 64 characters, the browser returned with `auth.session.expired` (BFF-SESS-006, D-195, D-196); 400 | API-CONV-002, API-CONV-003, INT-SMS-003, D-162, D-166, D-179, D-183, CONV-DESIGN-004, D-187, D-195, D-196 |
| `api.request.invalid` | A well-formed request refused on its meaning where no more specific code of section 1 exists: a body naming something that does not exist or cannot be acted on (an invitation's `documents` naming a document not published, `details.member` `documents`; an invitation's `formerMailbox` where no held mailbox stands for its corporate address, `details.member` `formerMailbox`; a group member that does not exist or belongs to another organization; an out-of-band privacy request naming a subject no account bears; a preferred second step naming a method the account has not enrolled, `details.member` `method`); a registration, move or removal through `IResources` that the declaration or the library's records refuse (a removal naming a record not registered, `details.member` `resource`); a host's field opened for a subject the deployment holds no key for (`IPersonalFields`, `details.member` `subject`). `details.member` names the member in the form `api.request.malformed` gives; 422 | API-CONV-002, API-CONV-003, REG-INV-001, REG-MAIL-003, AUTHZ-GROUP-001, IDN-ATTR-008, AUTHZ-INHERIT-002, IDN-LIFE-002a, PRIV-RIGHT-001, PRIV-RIGHT-005a, D-166, D-178, D-179, D-183, D-203 |
| `model.containment.cycle` **(new)** | Containment declaration forms a cycle | AUTHZ-MODEL-004 |
| `model.derivation.unindexed` **(new)** | Derivation names an unindexed column | AUTHZ-DERIVE-004 |
| `model.purpose.missingassessment` **(new)** | Legitimate interest declared without an assessment | PRIV-BASIS-002 |
| `model.type.noorganizationpath` **(new)** | Resource type has no path to an organization | AUTHZ-MODEL-004 |
| `model.type.reserved` | Startup: a host resource type takes the name `organization`, which the library reserves for the whole organization; `details.key` names it | AUTHZ-MODEL-002, AUTHZ-MODEL-004, D-166 |
| `model.purpose.hostingconsent` | Startup: a purpose named for the hosting or its cross-border transfer rests on consent: `hosting`, `transfer`, `hosting-transfer` or `cross-border-transfer`, compared ignoring case and every character other than a letter or a digit; the same purpose on another basis is accepted; `details.key` names `<type>.<purpose>` | INT-HOST-002, PRIV-CONS-010, D-166 |
| `model.startup.rpid` **(new)** | Startup: the relying party identifier is not a registrable suffix of a configured origin, or a configured or related origin's host or the configured identifier is one the library's UTS #46 conversion refuses | AUTH-FACT-010, D-147, D-189 |
| `model.startup.labellimit` **(new)** | Startup: the configured origins exceed the five-label limit of related origins | AUTH-FACT-012, D-147 |
| `model.startup.schemamismatch` **(new)** | Startup: the database's migration history lacks a migration the build declares; `details.pending` lists the migrations owed; non-zero exit. A history holding migrations the build does not declare is not a mismatch (OPS-MIG-005) | OPS-MIG-002, D-147, D-166 |
| `model.startup.secretunavailable` **(renamed)** | Startup: a secret the library reads through the host's secret source (LIB-EXT-001) was not supplied, or was supplied empty or unusable: a key-encryption key or fingerprint key version (a fingerprint key version shorter than 32 bytes included), the maintenance credential, the mail server's secret where the library's mail-server adapter is used, or a social provider's credential, read by the provider's name; `details.key` names the secret: `keyEncryptionKeys`, `fingerprintKeys`, `maintenanceCredential`, `mailServerSecret` or `socialProvider.<provider>`. A `Janus.Cli` command gives it where its standard input is a terminal (`details.key` `input`) or the document it reads there holds no usable key (`details.key` naming the member); `migrate`, whose document carries the connection alone, gives it with `details.key` `input` where the document carries no member (one carrying any other member is `api.request.malformed` naming `input`, first); a `connection` absent, blank or not a string is `api.request.malformed` naming `connection` for every command, save `migrate`'s absent member (OPS-MIG-001, OPS-SEC-001, D-204, D-209). Also raised at the start where a row of the subject-key table that is not erased stands under a key-encryption key version the source does not supply, and, as a fault a request answers `system.fault` (section 6), by an unwrap of a stored value under a version no longer held; `details.version` names the version | OPS-SEC-001, AUTH-KEY-002, OPS-MIG-003a, OPS-SEC-003, LIB-EXT-001, D-147, D-166 (renamed from `model.startup.kekunavailable`), D-183, D-204, D-209 |
| `model.startup.governinglanguage` **(new)** | Startup: `legal.governinglanguage` is unset | PRIV-CONS-005, LIB-HOST-001, D-147 |
| `model.startup.preferencedeclaration` **(new)** | Startup: a host preference declaration is malformed, a preference's name holding a `.` or `/` or being `language` or `timeZone` among them (D-206) | REG-PREF-001, D-147, D-206 |
| `model.startup.declarationmissing` | Startup: a required deployment value or host declaration is absent: a key of section 4 that names the deployment, or a conditional one whose condition holds; a declaration of LIB-HOST-001 (the secret source, the passkey pages, the authentication addresses, the sign-on client, the landing origins, the mail server client where a mail server is registered, the notification handler and the alert handler, D-225); a subject-event handler (none covering a type declared sensitive), an export contributor (none covering a type that declares an encrypted field or a sensitive category, PRIV-RIGHT-003, D-203), a purpose handler (none naming a purpose resting on consent or on an objectable basis, PRIV-RIGHT-001a, D-203) or a restriction key supplier; a declaration a stored value or the model needs (the image codec while a policy's `photos` is `true`, the DNS resolver while a domain is listed, the relationship source of every declared derivation, materialised or not). Or the model declares an item without what it requires: an encrypted field without its subject column, a type without its purposes, a purpose without its data categories, a category without its retention floor, a consent-based purpose on a type with no one subject column (`details.key` `<type>.<purpose>`). `details.key`, `details.handler` or `details.supplier` names it. The declarations' `details.key` spellings are `secretSource`, `passkeyAddresses` (or its field), `authenticationAddresses.signIn`, `authenticationAddresses.provider`, `signOnClient.clientId`, `landingOrigins.authentication`, `landingOrigins.account`, `mailServerClient.clientId`, `notificationHandler`, `alertHandler`, `imageCodec` and `dnsResolver`; a relationship source is named by its relationship | LIB-HOST-001, AUTHZ-MODEL-003, PRIV-PRIN-001, PRIV-RIGHT-005a, PRIV-RIGHT-005b, PRIV-RIGHT-001a, PRIV-RIGHT-003, PRIV-SENS-002, IDN-ATTR-002, REG-DOM-001, AUTHZ-DERIVE-005, AUTHZ-DERIVE-007, D-153, D-162, D-166, D-180, D-203 |
| `model.startup.declarationinvalid` | Startup: a declaration is present but malformed: a name held to the rule of INT-SMS-003 that breaks it (a governing document's, a subject-event subscriber's, a purpose handler's: 1 to 64 lower-case letters and digits separated by single `.`, `-` or `_`); an encrypted field or its subject column that is not a member of the declared type, or a subject column that is not of the library's subject identifier type; two encrypted fields of one type under one declared name, a member's or the one `Encrypted` is given (`details.declaration` the type, `details.field` the name, PRIV-RIGHT-005a, D-206); an encrypted field whose data category no purpose declared on its type names (`details.declaration` the type, `details.field` the field); a social provider declared twice or with a member outside its rule (`details.declaration` `socialProvider.<provider>`, for example `socialProvider.apple`, the name `model.startup.secretunavailable` gives the provider's credential; `details.field` the member, `provider` where the provider is declared twice or names a factor that is not a social provider); a relationship source given twice, naming no declared relationship, answering rows of another type than its derivation's, or naming a context the container does not give in a scope or whose model does not map the contract tables (`details.declaration` `relationshipSource.<relationship>`, `details.field` `relationship`, `rows` or `context`); a lawful basis list naming one key twice, or a basis with an empty key or label (`details.declaration` `lawfulBases`, `details.field` `key` or `label`); a type declaring as a capability a permission it does not declare, or two capabilities of one action (`details.declaration` the type, `details.field` `capabilities`, BFF-CAP-002, D-202, D-206); a job declared with a permission not declared, a library-owned one, one bound to a step-up gate or one whose action is `export` (`details.declaration` `job.<name>`, `details.field` `permissions`), or under a name of section 5.29 or `migrate` (`details.field` `name`) (IDN-PRIN-001, D-202, D-206); a resource type declared twice under one name (`details.declaration` the type, `details.field` `name`, AUTHZ-MODEL-004, D-206); a declared role naming a permission not declared or `system:administer` (`details.field` `permissions`), or named as a role of section 3 (`details.field` `name`) (`details.declaration` `role.<name>`, AUTHZ-GRANT-004, D-204); a step-up gate or a purpose bound to a library-owned permission of section 2.1 (`details.declaration` `binding.<permission>`, `details.field` `stepUpGate` or `purpose`, AUTHZ-GATE-005, D-209); a declared cookie naming a purpose not declared or not resting on consent (`details.field` `purpose`), or named as a cookie the library sets (`details.field` `name`) (`details.declaration` `cookie.<name>`, PRIV-CONS-006a, D-203); a job's or an export contributor's name that breaks the rule of INT-SMS-003, a declared role's name outside the rule `09` `POST /admin/roles` gives, a declared cookie's name that is no cookie name (RFC 6265), or any of these given twice (`details.declaration` `job.<name>`, `contributor.<name>`, `role.<name>` or `cookie.<name>`, `details.field` `name`, D-206). `details.declaration` and `details.field` name it. A landing origin that is not the origin of a registered browser client's return address, or an authentication landing origin that is not the origin of the sign-in address, is refused with `details.key` `landingOrigins.authentication` or `landingOrigins.account` | LIB-HOST-001, INT-SMS-003, PRIV-PRIN-001, PRIV-RIGHT-005a, AUTHZ-MODEL-003, AUTHZ-MODEL-004, IDN-LIFE-012, AUTHZ-DERIVE-005, PRIV-BASIS-001, AUTHZ-GATE-005, D-166, D-175, D-183, D-202, D-203, D-204, D-206, D-209 |
| `model.startup.redirectclient` | Startup: a registered client's return address is not an absolute `https` address with a host (or `http` on a loopback IP literal), `details.client` naming the client; or `redirect.defaultclient` names no registered client of kind `browser-application`, `details.key` naming the key | API-REDIR-001 AC3, D-162, D-166 |
| `model.startup.subscribername` | Startup: two subject-event subscribers or purpose handlers are registered under one name, or one under `erasure-ledger`, the name the library records the off-host ledger's confirmation under; `details.handler` names it | IDN-LIFE-003a, DR-016, D-166, D-203 |
| `model.startup.requestlogging` **(new)** | Startup: the host's logging enables the category of the framework's per-request entries, `Microsoft.AspNetCore.Hosting.Diagnostics`, which carry a request's query string, at Information through any provider, or disables that category in every provider, which would drop the scope carrying `RequestId` (BFF-LOG-002) | BFF-LOG-002, D-197 |
| `model.rotation.notready` | A key rotation's retirement (`rotate-kek --retire`, `rotate-fingerprint-key --retire`) was asked where no rotation of that kind awaits one, where the latest has retired, or while values stand under a previous version, a held username, an unlapsed reservation, an abuse ledger line that still counts or a sign-in in progress included; `details.pending` counts them. The command exits 1 | OPS-SEC-003 AC3, AC4, AC6, D-166, D-183, D-225 |
| `model.type.undeclaredreference` | Startup: a resource type references a type that is not declared, or an action is bound to a purpose no resource type declares; `details.permission` names the action | AUTHZ-MODEL-004, AUTHZ-GATE-005, D-153, D-166 |
| `model.role.undeclaredpermission` | Startup: a role the deployment holds grants a permission that is not declared; a declared role naming one fails with `model.startup.declarationinvalid` (AUTHZ-GRANT-004) | AUTHZ-MODEL-004, D-153, D-204 |
| `model.derivation.undeclaredreference` | Startup: a derivation references a type or relationship that is not declared | AUTHZ-MODEL-004, D-153 |
| `system.fault` | An unhandled fault; 500, body per API-CONV-002 carrying the correlation identifier and nothing else; for a `Janus.Cli` command, a fault that carries no code of its own, a database's refusal at `migrate` included, answered as one JSON line `{"code": "system.fault", "details": {}}` on standard error, nothing on standard output, exit code 1 | BFF-ERR-002, OPS-SEC-001, OPS-MIG-001, D-153, D-207 |

### 1.6 Integration

| Code | Meaning | Source |
|---|---|---|
| `integration.callback.rejected` **(new)** | A callback refused. 429 with `Retry-After`, the error carrying `details.retryAt`, where `integration.callback.ratelimit` refused it; 422 with no `Retry-After` for every other cause: signature, window, event identifier, source range, reference, confirmation, a provider event its keys do not verify, a request carrying the session cookie once the rate limit admits it (BFF-MACH-001, D-189). The Google security-event route answers a Security Event Token that fails validation as RFC 8935 section 2.3 does (400 with `err` and `description`), not with this code. A provider's document the library cannot have or read is no cause of this code: the delivery is answered `system.fault` (500) on either provider route | INT-GEN-003, BFF-MACH-001, BFF-MACH-002, BFF-MACH-003, IDN-LIFE-012a, D-166, D-183, D-189 |
| `integration.callback.inprogress` | A delivery of an event whose earlier delivery is still being carried: its claim is unsettled and younger than `integration.callback.claimtimeout`. Not recorded as a rejection and not counted toward `alerting.callback.threshold`; 409 | BFF-MACH-002 AC3, D-166 |
| `integration.endpoint.insecure` **(new)** | Startup, a change by `configure` (OPS-CFG-004), or a change of `password.blocklist.selfhosted.address` through `PUT /admin/config/{key}` (422, D-198): a key naming an endpoint the library calls (`integration.mailserver.endpoint`, `password.blocklist.selfhosted.address`) holds a value that is not an absolute `https` address; `details.key` names the key | INT-GEN-001, INF-TLS-004, D-162, D-166, D-198 |
| `integration.mailserver.conflict` **(new)** | A mail server's answer to a mailbox push, never a route's: the server holds an account at the mailbox's name that does not carry the mailbox's identifier, so the push adopts nothing and changes nothing there. The push is marked failed at that attempt and raises `degradation` scoped `mailbox.conflict:<mailbox id>` | INT-MAIL-001, INT-MAIL-007, LIB-HOST-001, REG-MAIL-003, D-177 |
| `integration.sms.windowactive` | *Retired by D-146. See `auth.restriction.exceeded` (AUTH-ABUSE-004).* | |
| `integration.sms.balancefloor` | *Retired by D-225. The library keeps no gateway floor.* | |

---

## 2. Permission strings

Format `resource:action` — lowercase, singular resource.

*Source: CONV-NAME-002, D-166*

### 2.1 Library-owned

Shipped by the library because they govern its own endpoints and operations
(LIB-API-005). Hosts do not declare these; a host's job may declare the four reads `01`
IDN-READ-004 names, `record:read` and `record:erase`, and no other (D-218, D-221).

| Permission | Governs |
|---|---|
| `grant:read` | Viewing grants (`GET /admin/grants`), the "who can access this?" view, and which accounts may exercise a permission (`GET /admin/access/admitted`, AUTHZ-GATE-007, D-218) |
| `grant:manage` | Creating and revoking grants; asked in the organization the grant is scoped to, and for a grant in an organization other than the administrative organization also met by `system:administer` held in the administrative organization, save for an invitation's roles, which need `grant:manage` held in the invitation's own organization (AUTHZ-SCOPE-001, D-207) |
| `role:manage` | Reading, creating and changing roles, in the administrative organization |
| `group:manage` | Creating groups, nesting them, and changing their members (D-106); asked in the organization the group belongs to (AUTHZ-SCOPE-001) |
| `organization:manage` | Organization lifecycle, policy, and deletion cancellation; asked in the administrative organization, whatever organization the request names |
| `membership:manage` | Adding and removing members; issuing and revoking invitations, bound or open (IDN-LIFE-009a, REG-INV-001) |
| `organization:read` | Reading the organizations the deployment holds (`01` IDN-READ-001): in the administrative organization every one, elsewhere the organization itself. A host's job may declare it (IDN-READ-004, D-218) |
| `membership:read` | Reading an organization's current members, one account's membership and the organization's standing invitations (`01` IDN-READ-002), asked in the organization named. A host's job may declare it (IDN-READ-004, D-218) |
| `domain:manage` | Adding, verifying and removing an organization's locked email domains (REG-DOM-001, IDN-ORG-006, D-146). Asked in the administrative organization. Adding and verifying a domain, and removing the last one, are loosenings under OPS-CFG-002 and also need `system:administer`. A domain is added only where a DNS resolver is declared (`emailDomains`, section 4.1a) |
| `restriction:edit` | Reading and editing the named restriction set (`GET/PUT/DELETE /admin/restrictions/{name}`, AUTH-ABUSE-004, D-146). Every edit is a step-up action and needs a reason; a loosening also needs `system:administer`, and alerts |
| `restriction:grant` | Granting credit to one key under a restriction (`POST /admin/restrictions/{name}/grant`, AUTH-ABUSE-004, D-146): the support role's permission, stepped up, audited with a reason |
| `account:manage` | Suspending and reactivating an account, lifting a restriction, cancelling a deletion on a subject's behalf (D-106), and setting another person's preferences (`01` IDN-READ-003, D-218) |
| `account:read` | Reading another person's account and finding an account by an identifier (`01` IDN-READ-003), with the reach of IDN-READ-004; a person's alone (D-218) |
| `contact:read` | Reading another person's display name, primary verified email and phone, language, time zone and state (`01` IDN-READ-003), with the reach of IDN-READ-004. A host's job may declare it (D-218) |
| `consent:read` | Reading another person's state, consents and objections (`01` IDN-READ-003), with the reach of IDN-READ-004. A host's job may declare it (D-218) |
| `takedown:execute` | The minor takedown operation: the trigger, the reversal and the read of its progress, of an account (IDN-LIFE-003) and of a person record (`21` REC-TKD-001, D-221). Its own permission because of what it does |
| `record:create` | Entering a person record for someone who gave their details to staff (`21` REC-COL-003), asked in the organization the record is entered in (D-221) |
| `record:read` | Reading and finding person records and the records attached to an account (`21` REC-ADM-001, REC-ATT-002), with the reach REC-ADM-001 gives. A host's job may declare it (D-221) |
| `record:manage` | Correcting a person record, recording a consent or an objection the person gave staff, exporting a record for the person, lifting its restriction and cancelling its deletion (`21` REC-ADM-002, D-221) |
| `record:erase` | Erasing a person record the host keeps nothing about any longer (`21` REC-ADM-003). A host's job may declare it (D-221) |
| `recovery:approve` | Administrative re-enrolment |
| `session:revoke` | Emergency revocation of all sessions, system-wide |
| `session:revoke-account` | Revoking one account's sessions (offboarding, suspension) and listing them (`01` IDN-READ-003, D-218) |
| `config:read` | Reading configuration |
| `config:manage` | Changing configuration |
| `audit:read` | Reading the audit trail, querying it by subject, and resolving a concealed denial's correlation identifier (AUTHZ-GATE-004) |
| `ropa:read` | Generating records of processing |
| `privacyrequest:manage` | The data-subject request queue, erasure progress, the list of incomplete deliveries, and the manual completion path of a delivery of any kind of section 5.50 (IDN-LIFE-003a, D-203) |
| `notice:publish` | Publishing a compliance-text version (PRIV-CONS-006) |
| `compliance:manage` | Assessment references and the declared human-input fields of the records of processing |
| `system:administer` | **Loosening** any runtime configuration (OPS-CFG-002), the named restriction set, an organization's policy and its locked domains included, asked in the administrative organization beside the route's own permission; granting, revoking or changing a role that carries it, and changing the members of a group that holds one (OPS-CFG-007); generating the break-glass credential and reading whether one stands (`GET /admin/break-glass`, OPS-BOOT-001, OPS-BOOT-004); writing and revoking a grant in an organization other than the administrative organization, so an organization `POST /admin/organizations` created receives its first grants (AUTHZ-SCOPE-001, D-207), an invitation's roles excepted (`09`). The protected settings in OPS-CFG-004 are not changeable through the application at all, by anyone |

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
(AUTHZ-GRANT-004): an administrator defines them at runtime, and a host may declare the
roles its deployment starts with, each seeded once at a start and then the
administrators' to change (`Role`, LIB-HOST-001, D-204).

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

*Source: D-028, OPS-BOOT-001, OPS-BOOT-002, D-146, D-166, D-204*

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
bucket, a channel changed to anything but `any` and a `resend` mark added each loosen; a
new restriction does not (D-166, D-197). Otherwise a key with a ceiling and no floor
loosens upward, a key with a floor and no ceiling loosens downward, a boolean loosens
away from its default, and any other key (both bounds, neither bound, enum, list, set,
string) loosens on any change, as D-079b classifies settings with no direction.

**Every key carries a safe default (P-001) except the six that name the deployment** —
origins (`webauthn.origins`), hosting location (`hosting.location`), the governing
language of legal documents (`legal.governinglanguage`, D-146), the calendar time zone
(`privacy.calendar.timezone`), the message languages (`notification.languages`) (D-153)
and the hosting environment (`hosting.environment`, PRIV-ROPA-001), which every
deployment needs because every deployment generates the records of processing (D-166,
D-225). **Four further keys are conditional**: `hosting.crossborderbasis`, required only
when `hosting.location` is `outside` (INT-HOST-002, D-147); `service.name`, required
only when `context` is in `password.blocklist.sources` (D-153);
`password.blocklist.selfhosted.address`, required only when `password.blocklist.source`
is `selfHosted`; and `integration.mailserver.endpoint`, required only where the
library's mail-server adapter is used (D-166). Those are shown with no default, are
listed in LIB-HOST-001, and startup **fails** with a named error if any is unset (a
conditional one, when its condition holds). The public-holiday list is **not** among
them (D-142): its safe default is empty. Defaults sit at the safe end of their range, so
a host that never touches a key gets a secure deployment and the only deliberate change
is a loosening, which OPS-CFG-002 audits (D-107).

### 4.1 Sessions

| Key | Default | Scope | Source |
|---|---|---|---|
| `session.aal2.inactivity` | 1 hour | R, ceiling 12 h | AUTH-SESS-005, D-123, D-130 — where the policy's `requiredAssurance` is `aal2` |
| `session.aal2.absolute` | 24 hours | R, ceiling 24 h | AUTH-SESS-005, D-123 |
| `session.default.inactivity` | 90 days, refreshed by use | R, ceiling 365 d | AUTH-SESS-005, D-123 — the system policy |
| `session.default.absolute` | 365 days | R, ceiling 365 d | AUTH-SESS-005, D-130 — the definite overall timeout NIST §2.1.3 requires; its ceiling, counted from a signing key's replacement, is how long that key's public key is kept (AUTH-KEY-001, D-181) |
| `session.stepup.recency` | 15 minutes | R | AUTH-STEP-001, D-107 |
| `policy.default` | the policy object of §4.1a with its system defaults | R, each field classified per §4.1a | AUTH-PRIN-002, D-116, D-143 — the **system** policy, for principals with no membership. Replaces `login.policy.default`, `stepup.policy.default`, `stepup.shape.<action>` / `stepup.gate.<action>` |
| `policy.<organization>` | `{}` (no override: every field inherits `policy.default`) | R, one key per organization identifier, created empty when the organization is | AUTH-PRIN-002, D-116, D-143 — written by `PUT /admin/organizations/{id}/policy`; the administrative organization's is set at bootstrap (§4.1a). Replaces `login.policy.<organization>`, `stepup.policy.<organization>` |
| `privacy.export.ratelimit` | 3 per `P1D` | R, integer exports per account in the 24 hours ending now, sliding; an export is counted once every export contributor has answered, under a lock on the subject with the window counted again (D-166 X3), and one a contributor failed is not counted; past it 429 `auth.throttled` with `Retry-After` and `details.retryAt`, the instant the oldest counted export leaves the window | D-086, D-107, API-CONV-003, D-166, D-203 |
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
| `password.blocklist.selfhosted.address` | none; **required when `password.blocklist.source` is `selfHosted`** | R, string; startup and a change through `PUT /admin/config/{key}` refuse anything but an absolute `https` address with `integration.endpoint.insecure`, and a stored value that is not one reads and is never called, the package's list answering the screening and the degradation raised (INT-PWD-003, D-198) | AUTH-PASS-004, INT-GEN-001, INT-PWD-003, D-162, D-166, D-198: where the deployment's own corpus answers the ranges the range API answers |
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
| `webauthn.origins` | — **required** | **P**, each origin held in its serialization (AUTH-FACT-010, D-190) | AUTH-FACT-010, D-190 |
| `webauthn.relatedorigins` | empty | R, each origin held in its serialization (AUTH-FACT-010, D-190) | AUTH-FACT-012, D-107, D-190 |
| `webauthn.algorithms` | `[-8, -7, -257]` (EdDSA, ES256, RS256) | **P**, list of COSE algorithm integers; −7 SHALL be a member | AUTH-FACT-014, D-120 |

### 4.4 Recovery

| Key | Default | Scope | Source |
|---|---|---|---|
| `recovery.approvers.required` | 1 | R | AUTH-RECOV-002 |
| `recovery.link.lifetime` | 1 hour | R | AUTH-RECOV-002, D-107 |
| `recovery.invalidation.window` | 7 days | R | AUTH-RECOV-007, D-141, D-197 — from loss report (or assurance-lowering removal, or a `phoneCode` credential's number leaving, AUTH-FACT-002) to invalidation; formerly `recovery.mfaremoval.window` |
| `recovery.invalidation.noticeinterval` | `P1D` | R | AUTH-RECOV-007, D-153, D-197: the loss-report notice (`credential-suspended`, or `credential-unbound` once a `phoneCode` credential's number left the account) repeats at this interval across the window, plus once at the report (for `credential-unbound`, once when the number leaves) and once 24 hours before invalidation |
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
| `abuse.nonexistent.window` | 1 hour per address | R | AUTH-ABUSE-003, D-132, D-148: one notice per address per window, for both the non-existence message sent to an unknown address at a sign-in path (AUTH-ABUSE-003) and the notice sent to the owner of an address someone else tried to register or add (REG-SESS-005, REG-IDENT-008); a notice whose send a restriction refused spends no window (D-188) |
| `abuse.sms.window` | *Retired by D-146. See `restrictions` below and AUTH-ABUSE-004.* | | |
| `abuse.source.ratelimit` | 300 per `PT1M` | R, integer requests per minute per source (an IPv4 address or an IPv6 /64, AUTH-ABUSE-001), sliding, counted per instance; 429 `auth.throttled`; raising is loosening | BFF-ORDER-001 stage 4, D-153, D-166: the flood limit on every request before any expensive work; sized for many people behind one address |
| `abuse.source.sitelimit` | 3000 per `PT1M` | R, integer requests per minute per IPv6 /48, sliding, counted per instance; 429 `auth.throttled`; raising is loosening | BFF-ORDER-001 stage 4, AUTH-ABUSE-001, D-166: one site holds a /48 or a /56 (RFC 6177, RIPE-690), so walking its /64s is bounded at ten sources' worth |
| `abuse.botdefence.repeatedattempts` | 3 per `PT1H` | R, integer registration sessions per source per hour; raising is loosening | AUTH-ABUSE-008, D-153, D-189: the count behind the `repeatedAttempts` signal, which fires where the session a request would create makes more than this many |
| `integration.callback.ratelimit` | 60 per `PT1M` | R, integer per source per minute, fixed window; 429 `integration.callback.rejected` before any lookup; raising is loosening | INT-GEN-003, BFF-MACH-003, D-153 |
| `integration.callback.claimtimeout` | `PT5M` | R, duration, floor `PT1M` | BFF-MACH-002, D-166: how long an unsettled claim on a host callback's event stands; a redelivery meeting a younger claim is answered 409 `integration.callback.inprogress`, and one meeting an older claim takes it over and is carried |
| `integration.mail.endpoint` | *Retired by D-225. The library sends no mail (`05` INT-SMS-006).* | | |
| `integration.sms.endpoint` | *Retired by D-225. The library sends no SMS (`05` INT-SMS-006).* | | |
| `integration.mailserver.endpoint` | empty | **P**, string, an absolute `https` address, required only where the library's mail-server adapter is used; read at the start, so a change takes effect at the next start; startup refuses another with `integration.endpoint.insecure` naming the key | INT-MAIL-001, INT-GEN-001, LIB-EXT-001, D-166, D-176: where the library's mail-server adapter reaches the mail server (JMAP at `<endpoint>/jmap`, INT-MAIL-001); where it is set when the application starts and the host registers no `IMailServer`, the adapter is the deployment's mail server integration until the next start (LIB-HOST-001, CONV-DESIGN-007) |
| `restrictions` | the five shipped restrictions below | R, edited through `GET/PUT/DELETE /admin/restrictions/{name}` (step-up `restriction:edit`, a reason on every edit, OPS-CFG-008); a restriction's name is 1 to 64 lower-case letters and digits separated by single `.`, `-` or `_`; a `{name}` outside it is refused 400 `api.request.malformed` naming `name` on every route that takes one, and a name outside it inside the `restrictions` value with `config.value.notallowed`; a loosening (the changes section 4's Direction paragraph names for this row: a higher max, a shorter interval, a removed bucket, a deleted restriction, a narrowed channel and a `resend` mark added among them) falls under OPS-CFG-002, needs `system:administer` and raises a Normal alert | AUTH-ABUSE-004, OPS-CFG-008, D-146, D-166, D-183, D-197: the **named restriction set** governing every message the library hands to the host (`05` INT-SMS-006); an alert, handed to the alert handler, answers to the deduplication of OPS-ALERT-002 alone. Each restriction is a key (§5.14), an optional purpose (§5.15), an optional channel (§5.15a), one or more buckets of (max, interval, `sliding` · `fixed`, §5.16) and, optionally, the `resend` mark (§5.16a). Security notices to an existing holder answer only to restrictions whose purpose is `notification`. Replaces `abuse.sms.window`, INT-SMS-002 and IDN-LIFE-011 |
| `restrictions` · `sms.destination` | key `destination`, purpose `any`, channel `sms`, 3 per 24 h sliding | R, as above | AUTH-ABUSE-004, D-146, D-166 |
| `restrictions` · `sms.source` | key `source`, purpose `any`, channel `sms`, 10 per 1 h sliding | R, as above | AUTH-ABUSE-004, D-146, D-166 |
| `restrictions` · `sms.resend` | key `destination`, purpose `any`, channel `sms`, 1 per 3 min sliding, marked `resend` (§5.16a) | R, as above | AUTH-ABUSE-004, D-197 |
| `restrictions` · `email.destination` | key `destination`, purpose `any`, channel `email`, 5 per 1 h sliding and 1 per 60 s fixed | R, as above | AUTH-ABUSE-004, D-146, D-166: also bounds the new-device check code (AUTH-FACT-016) |
| `restrictions` · `notification.destination` | key `destination`, purpose `notification`, channel `any`, 5 per 24 h sliding | R, as above | AUTH-ABUSE-004, D-146, D-166: the only restriction that applies to security notices |
| `code.verification.lifetime` | 10 minutes | R, ceiling 30 min | AUTH-FACT-004, D-132, D-166: phone and email verification codes, including the new-device check code (AUTH-FACT-016), and the old address's confirmation of a replace (REG-IDENT-007, D-187); an authentication code is held to `code.signin.lifetime`, or to `link.magic.lifetime` where a sign-in link's page shows it |
| `code.verification.attempts` | 5 | R, ceiling 10 | AUTH-FACT-004, REG-SESS-003, D-146: wrong tries after which a verification code is invalidated and a correct one refused; a replacement draws on the restrictions |
| `code.signin.lifetime` | 10 minutes | R, ceiling 30 min | AUTH-FACT-002, AUTH-FACT-004, D-166, D-197: the life of the authentication codes that are sent, held apart from verification codes: the `emailCode` sign-in code (message kind `sign-in-code`) and the `phoneCode` second-step code and the code of a `phoneCode` enrolment (message kind `secondstep-code`). The code a sign-in link's page shows in another browser lives as long as its link (`link.magic.lifetime`) |
| `code.signin.attempts` | 5 | R, ceiling 10 | AUTH-FACT-004, D-166, D-197: wrong tries after which an authentication code (the `emailCode` code, the `phoneCode` code, the code a sign-in link shows) is invalidated, the code of a `phoneCode` enrolment among them; the correct code presented after them is refused with `auth.code.expired` |
| `link.magic.lifetime` | 15 minutes | R, ceiling 1 h | AUTH-FACT-003, AUTH-FACT-004, D-132, D-146, D-166: email and SMS sign-in links (`emailLink`, `phoneLink`, `POST /auth/link`), and the code a link's page shows where it is opened in another browser; the link completes only in the requesting browser and only on a press (REG-SESS-003) |
| `link.invitation.lifetime` | 7 days | R, ceiling 30 d | IDN-LIFE-009a, D-132 — single use |
| `photo.maxbytes` | 2097152 | R, integer bytes (2 MiB), ceiling 10485760 (10 MiB) | IDN-ATTR-004, D-132, D-152 |
| `photo.maxdimension` | 1024 | R, integer pixels on the longest side; larger images are downscaled | IDN-ATTR-004, D-132 |
| `abuse.sms.balancefloor` | *Retired by D-225. A gateway's balance is the host's.* | | |
| `abuse.sms.pollinterval` | *Retired by D-225. A gateway's balance is the host's.* | | |
| `abuse.sms.drainfactor` | *Retired by D-225. A gateway's balance is the host's.* | | |
| `abuse.botdefence.signals` | `[datacenterRange, repeatedAttempts]` | R, set over the two members named; the set is closed until a decision adds a member; removing a member is loosening; while `datacenterRange` is in the set and no fresh range file is held, it does not fire and `degradation` is raised (D-189) | AUTH-ABUSE-008, D-107, D-152, D-189 |
| `abuse.botdefence.ranges.refresh` | `P1D` | R, duration | AUTH-ABUSE-008, D-189: the datacenter range file's refresh cadence, as `location.database.refresh` is the location file's |
| `abuse.botdefence.ranges.maxage` | `P30D` | R, duration | AUTH-ABUSE-008, D-189: beyond this age, judged from the date the file states, the range file is stale, `datacenterRange` does not fire, and a Normal degradation is raised |
| `exfiltration.readvolume.alerting` | `true` | R | D-045 |
| `exfiltration.export.stepuprequired` | true | R | D-045, D-148, D-166: **staff bulk export only**. `/privacy/export` is not governed by this key; it is gated at the account's reachable assurance by AUTH-STEP-002a instead (D-141). Turning it off raises `stepup-policy-weakened` (OPS-ALERT-001) |
| `exfiltration.export.ratelimit` | 5 per hour | R | D-045, D-107 |
| `exfiltration.export.auditing` | `true` | **P** | D-045 |
| `alerting.email.destinations` | *Retired by D-225. Alert destinations are the host's.* | | |
| `alerting.sms.destinations` | *Retired by D-225. Alert destinations are the host's.* | | |
| `alerting.owner.enabled` | *Retired by D-225. Alert destinations are the host's.* | | |
| `alerting.owner.email` | *Retired by D-225. Alert destinations are the host's.* | | |
| `alerting.owner.sms` | *Retired by D-225. Alert destinations are the host's.* | | |
| `alerting.destinationchange.notify` | *Retired by D-152. The notice to the previous destinations is non-suppressible (D-083, OPS-ALERT-004a); a switch for it was the hole D-083 closed, so no key exists.* | | |
| `exfiltration.readvolume.baselinewindow` | 30 days | R | D-071, D-107 |
| `exfiltration.readvolume.factor` | 3.0 | R, decimal, floor 1.0; raising is loosening | OPS-ALERT-005, D-153: `read-volume-anomaly` fires when the actor's records returned today exceed this factor times their daily mean over the baseline window and exceed `exfiltration.readvolume.minimum` |
| `exfiltration.readvolume.minimum` | 500 | R, integer; raising is loosening | OPS-ALERT-005, D-153: the floor under which no read-volume alert fires |
| `alerting.authfailures.threshold` | 20 | R, integer failures on one account inside `alerting.dedupe.window`; raising is loosening | OPS-ALERT-002, D-153: `auth-failures-sustained` |
| `alerting.recovery.accountthreshold` | 3 per `P1D` | R, integer; raising is loosening | OPS-ALERT-001, D-153: `recovery-clustering` |
| `alerting.recovery.approverthreshold` | 3 per `P1D` | R, integer; raising is loosening | OPS-ALERT-001, D-153: `approver-volume` |
| `alerting.denials.threshold` | 50 per `PT10M` | R, integer denials per actor in a fixed ten-minute window counted from the Unix epoch in UTC; the actor is the system principal the refusal records, by its name, where one acted, and otherwise the acting subject it records; every refusal recording the nil subject and no principal is counted as one actor; raised once the window holds more than this; raising is loosening | AUTHZ-GATE-004, D-153, D-166, D-183: `denial-spike` |
| `alerting.sessions.distance` | 500 | R, integer kilometres; raising is loosening | OPS-ALERT-007, D-153, D-166: `concurrent-sessions-implausible` fires when two sessions of one account are both used inside `alerting.sessions.window` and their cities are further apart than this, or their known countries differ, whatever their cities; distance is measured only where both places name a city, and a place with no country never fires |
| `alerting.sessions.window` | `PT1H` | R, duration; lengthening is loosening | OPS-ALERT-007, D-153 |
| `alerting.nonexistent.threshold` | 20 per `PT1H` | R, integer, system-wide; raising is loosening | AUTH-ABUSE-003, D-121, D-153: `nonexistent-notice-rate` |
| `alerting.callback.threshold` | 10 per `PT1H` | R, integer rejected callbacks per source; raising is loosening | BFF-MACH-003, D-153: `callback-verification-failed` |
| `alerting.dedupe.window` | 1 hour | R | D-048, D-107 |
| `alerting.sms.severitythreshold` | *Retired by D-225. How an alert is delivered is the host's.* | | |
| `maintenance.expiry.warninglead` | 30 days | R | DR-009a, PRIV-RIGHT-002, OPS-ALERT-001, D-121, D-225: the lead of the key-encryption key's cryptoperiod warning and of the holiday list's |

### 4.5a Authorization

| Key | Default | Scope | Source |
|---|---|---|---|
| `authz.reverselookup.budget` | 2 seconds | R | AUTHZ-SEAM-001, R-A09, D-125 — the migration trigger's measurable bound |
| `authz.handout.byholding` | `false` | R, boolean; turning it on is loosening | AUTHZ-GRANT-005, D-212: whether an actor whose grants admit every permission a role carries may hand the role out with no role of theirs listing it (Kubernetes' rule); off, only a role whose `handsOut` names it, or `system:administer` in the administrative organization, does; on, it counts stored grants and materialised derivations, never a live one (D-216) |
| `derivation.materialised.driftcheck` | `P1D` | R, duration; lengthening is loosening | AUTHZ-DERIVE-005, AUTHZ-GRANT-003, D-161, D-166: how often the drift check re-evaluates every materialised derivation, over the relationship sources the host declares for every declared derivation (LIB-HOST-001), corrects drift and raises `degradation` naming the derivation; it runs as the system principal `derivation-driftcheck` (section 5.29), and a grant it writes records the nil subject as granter with the reason `AUTHZ-DERIVE-005`; each grant it writes or takes back is recorded as `authz.grant.materialised` or `authz.grant.retracted` (section 5.24, D-187) |

### 4.6 Organizations and lifecycle

| Key | Default | Scope | Source |
|---|---|---|---|
| `organization.deletion.grace` | `P30D` | R, floor `P7D` | IDN-ORG-003, D-152 |
| `takedown.grace` | `P7D` | R, floor `P7D` (the default; D-127 chose the period) | IDN-LIFE-003, D-127, D-152 — access stops at trigger; erasure runs at the end of the window |
| `account.deletion.grace` | `P30D` | R, floor `P7D` | IDN-ACCT-007, D-113, D-152 |
| `record.deletion.grace` | `P30D` | R, floor `P7D` | REC-REC-002, REC-ACC-002, D-221: the window of a person record's own deletion and of an out-of-band one; a record attached to an account runs its account's |
| `records.collection.lifetime` | `PT1H` | R, duration, ceiling `PT24H` | REC-COL-001, D-221: life of a collection session; an expired one is swept and leaves nothing |
| `records.access.lifetime` | `PT15M` | R, duration, ceiling `PT1H` | REC-ACC-001, REC-ATT-001, D-221: life of a record access session from its opening, never extended, and of an offer to attach from its proof |
| `records.proof.age` | `P90D` | R, duration, floor `P7D`, ceiling `P365D`; lengthening is loosening | REC-COL-002, REC-ACC-001, REC-ATT-001, D-221: a person record whose contact was last verified at a collection longer ago is reached by a code, or offered to an account, only with a reference it holds, and is not reused by a collection |
| `organization.multiplememberships` | false | R | IDN-MEM-002 |
| `identifier.change.coolingoff` | `PT72H` | R, floor `PT72H` (the default; D-134 chose the window) | IDN-LIFE-007, IDN-LIFE-010, D-107, D-134, D-146 — email and phone alike (was `email.change.coolingoff`); since D-146 the **undo window** after an identifier removal or replace, during which the remaining security-notice set holds a one-click restore (REG-IDENT-006, REG-IDENT-007) |
| `registration.phone` | `required` | R: `required` · `optional`; to `optional` is loosening (OPS-CFG-002) | REG-IDENT-001, D-146: whether an account must hold a verified phone; phone is never the sole identifier |
| `registration.adultaffirmation` | `required` | R: `required` · `off` | REG-PROF-002, D-146: `required` ends the session on an under-age date; `off` records the age group and no affirmation (a host that serves minors) |
| `registration.session.lifetime` | 24 hours | R, ceiling 72 h | REG-SESS-001, D-146: life of a registration session; an expired one is swept and leaves nothing |
| `registration.events.pollinterval` | `PT1S` | R, duration, floor `PT1S` | REG-SESS-003, FE-VER-001, D-162, D-166: how often an open `GET /register/events` stream reads the registration state back where the database channel has not woken it |
| `identifiers.email.max` | 10 | R, integer, floor 1, no ceiling; raising is loosening (each verified address is a send destination and a recovery channel) | REG-IDENT-002, REG-IDENT-004, REG-IDENT-007, D-146, D-152, D-187: verified emails per account, its pending adds counted; `1` is single-address mode (replace in one operation) |
| `identifiers.phone.max` | 10 | R, integer, floor 1, no ceiling; raising is loosening | REG-IDENT-002, REG-IDENT-004, REG-IDENT-007, D-146, D-152, D-187: verified phones per account, its pending adds counted; `1` is single-address mode |
| `identifiers.username.enabled` | false | R | REG-IDENT-001, REG-IDENT-009, D-146: while off no request accepts a username and no response carries the field |
| `identifiers.username.changecooloff` | `P30D` | R, floor `P1D` | REG-IDENT-009, D-146, D-152: minimum interval between username changes (`identity.username.coolingoff`) |
| `profile.legalname` | `off` | R: `off` · `optional` · `required` | REG-PROF-001, D-146: a proofing attribute, collected only with a declared purpose |
| `profile.dateofbirth` | `off` | R: `off` · `optional` · `required` | REG-PROF-001, REG-PROF-002, D-146: whether the date entered at the age step is retained; with `off` only the derived affirmation is kept. The date is immutable to the person |
| `domain.reverify.interval` | `P1D` | R, duration, floor `PT1H`; lengthening is loosening | REG-DOM-001, D-153, D-166: the sweep re-verifies the TXT record of every listed, verified domain last checked this long ago or more; a failure is recorded, raises `domain-reverification-failed` and unverifies nothing |
| `outbox.poll.interval` | `PT5S` | R, duration, ceiling `PT1M` | IDN-LIFE-003a, INT-MAIL-006a, AUTH-ABUSE-004, OPS-ALERT-001, D-153, D-162, D-166: the cadence of the outbox and event publishers, send delivery and its retries, mailbox provisioning and the carrying of raised alerts; the ceiling bounds how long any change can hold an alert back |
| `outbox.retry.initial` | `PT30S` | R, duration | IDN-LIFE-003a, AUTH-ABUSE-004, D-153, D-162, D-166: first retry delay, full jitter; also, with no jitter, how long after its admission a new send row that has an immediate attempt is due for a pass, that attempt claiming it before; a row with none is due at its admission (CONV-DESIGN-003, D-188, D-189) |
| `outbox.retry.factor` | 2.0 | R, decimal, floor 1.0 | IDN-LIFE-003a, AUTH-ABUSE-004, D-153, D-162, D-166: multiplier per further attempt |
| `outbox.retry.maxattempts` | 10 | R, integer, floor 1 | IDN-LIFE-003a, AUTH-ABUSE-004, D-153, D-162, D-166: attempts before a delivery of section 5.50 (an erasure's, a takedown's, a restriction's, a consent's or an objection's) is `failed` with `erasure-delivery-exhausted`, an event is `failed` with `degradation`, or a message is removed with `degradation` scoped `send:<channel>` (about eight hours end to end at the defaults) |
| `outbox.claim.timeout` | `PT2M` | R, duration, floor `PT30S`, ceiling `PT10M` | CONV-DESIGN-003, INF-BG-001, AUTH-ABUSE-004, D-183, D-186: how long a claim on a row that a delivery job or a send's immediate attempt carries stands, renewed before each delivery of a row that tracks several; an attempt still running then is abandoned as failed, and the next pass may take the row once it is due |
| `sweep.interval` | `PT5M` | R, duration, ceiling `PT15M` | OPS-OBS-003, AUTH-KEY-003, PRIV-RIGHT-002, PRIV-RET-005, OPS-ALERT-006, D-153, D-166, D-187: one sweep for expired sessions, tokens and codes, identifier adds and replaces whose every record is spent or past its lifetime, elapsed grace and cooling-off windows, privacy request deadlines (PRIV-RIGHT-002), sending-restriction records and every other abuse ledger line its own check no longer reads (PRIV-RET-005), export-limit records an hour old (OPS-ALERT-006) and domain re-verification, and collection sessions, record access sessions, offers to attach and person records' deletion windows (`21`, D-221); a deadline therefore fires within this interval of its instant |
| `notification.languages` | none; **required** | R, **list** of BCP 47 tags, at least one | IDN-ATTR-001, AUTH-ABUSE-004, D-031, D-153, D-166, D-186, D-197, D-225: the deployment's message languages. Step 3 of the recipient-language resolution hands the host one message carrying all of them, in this order (IDN-ATTR-001, `05` INT-SMS-006): by email it is judged and counted once; by SMS it is judged with the weight of their number and counted that many times by every applicable restriction not marked `resend`; a restriction marked `resend` judges and counts the send once (section 5.16a) |
| `notification.email.sendingdomain` | *Retired by D-225. Delivering mail is the host's.* | | |
| `notification.email.relayregistered` | *Retired by D-225. Delivering mail is the host's.* | | |
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
| `backup.restoretest.objective` | *Retired by D-225. Backups are the deployment's.* | | |
| `backup.restoretest.interval` | *Retired by D-225. Backups are the deployment's.* | | |
| `backup.restoretest.canary` | *Retired by D-225. Backups are the deployment's.* | | |
| `backup.retention` | *Retired by D-225. Backups are the deployment's.* | | |
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
| `integration.mailserver.endpoint` | OPS-CFG-004; INT-MAIL-001, INT-GEN-001, D-166, D-176 |
| `redirect.defaultclient` | OPS-CFG-004; API-REDIR-001, D-162, D-166 |

### 4.9 Tokens and keys

The lifetimes, algorithm and rotation cadence of the OIDC provider (`02` sections 8
and 9). The access-token lifetime is recorded as the accepted revocation latency for
any relying party that validates offline against the JWKS (AUTH-OIDC-004, D-147).

| Key | Default | Scope | Source |
|---|---|---|---|
| `oidc.accesstoken.lifetime` | 10 minutes | R, ceiling 1 h; lengthening is loosening (OPS-CFG-002) | AUTH-OIDC-004, D-147: for an offline validator this is the revocation latency |
| `oidc.code.lifetime` | 60 seconds | R, ceiling 10 min | AUTH-SESS-012, D-147: replaces the hard-coded sixty seconds |
| `oidc.continuation.lifetime` | 30 minutes | R, ceiling 1 h | AUTH-SESS-012, D-224: how long a browser's pending authorization waits for the sign-in that completes it; past it the continuation issues nothing |
| `token.signing.algorithm` | `ES256` | **P** (§4.8); `ES256`, the one member in this version | AUTH-KEY-001, D-147, D-148: verified against the mail server's source, file `crates/directory/src/backend/oidc/lookup.rs` of its repository, inspected 2026-09-18, which accepts P-256 keys from a JWKS as ES256 and refuses symmetric keys. The one value admitted in this version; any other is refused with `config.value.notallowed` (D-181). The one place the value is held |
| `token.signing.rotation` | 90 days | R, duration, floor `P1D`; lengthening is loosening | AUTH-KEY-001, OPS-SEC-002, D-147, D-166, D-181, D-197: the cadence of the signing keys and of every registered client's secret, which the library generates and holds wrapped under the deployment's data key; overlap is `oidc.accesstoken.lifetime` plus 5 minutes, for a signing key the longest lifetime it signed under, not a key of its own |
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
signed in on the provider's word, below `aal1` wherever a tier is needed; also what a
step-up reports where the factors accepted reach no level (`09` `POST /auth/step-up`,
D-192). Formerly `none`.

The same values name an **account's reachable assurance** (AUTH-STEP-006).

*Source: AUTH-SESS-002, D-140, D-141, D-192*

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
PRIV-BASIS-001 tables them and the label its Basis column gives (D-032):

| Key | Label |
|---|---|
| `consent` | Data Subject's Consent |
| `contractual-obligation` | Fulfilment of a Contractual Obligation |
| `legal-obligation` | Fulfilment of a Legal Obligation |
| `legitimate-interest` | Legitimate Interest |
| `legal-right-claim-or-defence` | Claim or Defence of a Legal Right |
| `court-judgment-or-order` | Execution of Court Judgments or Orders from Competent Investigative Authorities |

*Source: PRIV-BASIS-001, D-108, D-162, D-166, D-183*

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

`erasure-request` · `minor-takedown` · `retention`

The `Reason` column of the erasures table (IDN-LIFE-003b) and the third field of the
off-host erasure record (DR-016) use these spellings and no others. An organization
erasure erases no account (IDN-ORG-003). `retention` is a host's erasure of a person
record it keeps nothing about any longer (`21` REC-ADM-003, D-221).

*Source: IDN-LIFE-003b, DR-016, D-147, D-166, D-221*

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
`restricted` where a restriction is held, and `active` otherwise. It clears what it
restores and keeps what the state it returns to still holds: returned to a deletion or a
suspension, the account keeps a held restriction, and returned to an `oob-request`
deletion, the suspension that deletion holds; returned `restricted` or `active`, it
holds nothing (D-183). An out-of-band erasure fulfilled on an account already
`deleting`, by any origin, is recorded fulfilled against the running window and holds
nothing; on a `deleted` account it is recorded fulfilled and nothing further happens
(IDN-LIFE-003).

A person record holds `deletingBy`, `restrictionHeld`, `deletionHeld` and
`deletionHeldSince` as an account does, and no `suspensionHeld`, having no `suspended`
state (`21` REC-REC-002, D-221).

*Source: IDN-LIFE-003, IDN-LIFE-013, PRIV-RIGHT-004, D-137, D-147, D-166, D-183, D-221*

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
(REG-IDENT-002) and an invitation link (IDN-LIFE-009a) carry `signin`, and the
confirmation a replace asks of the displaced address (REG-IDENT-007) carries
`verification`, so none counts against `notification.destination`. An alert is outside
every restriction and answers to the deduplication of OPS-ALERT-002 alone.

*Source: D-148; AUTH-ABUSE-004, D-146, D-166, D-183*

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

### 5.16a Resend restrictions

A restriction marked `resend` judges and counts a send once, whatever the number of
languages it goes out in. That one count is released when every text of the send fails
for good (AUTH-ABUSE-004), for a send of purpose `secondfactor` also when any text of it
is reported delivered, and for any send when its code is presented right or its link
pressed, an authentication code's release standing whatever refusal follows, kept with
its spend, and a verification code's or a link's only on its success (CONV-DESIGN-003,
D-198); a failure report of one text releases that text's count from every restriction
not marked `resend`, and a delivered report releases nothing else (AUTH-ABUSE-004,
INT-SMS-005, D-197). A stored restriction without the mark reads as unmarked, and
marking one is a loosening (section 4, Direction).

*Source: AUTH-ABUSE-004, D-197, D-198*

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
`stepup` · `reauthenticate` (a downgraded session, AUTH-SESS-009: the gate would be met
but for proof last reached before the session's last downgrade; otherwise `stepup`) ·
`restricted` (AUTHZ-GATE-006) · `consent` (PRIV-SENS-002, PRIV-CONS-007) ·
`accountstate`.

*Source: AUTHZ-GATE-005, D-153, D-183, D-191*

### 5.21 Consent and objection mechanism

The `mechanism` of a consent or objection record: `registration` (the terms step,
REG-SESS-007) · `collection` (a person record's collection, `21` REC-COL-002, D-221) ·
`dashboard` (`/privacy/consents/*`, `/privacy/objections/*`) · `reconsent` (the
PRIV-CONS-007 prompt; recorded by the library for a dashboard grant over a superseded,
unwithdrawn consent, or over a live one the purpose no longer admits, which the grant
stamps superseded) · `administrator` (entered on the subject's behalf, staff's entry and
record of what a person without an account told them among them, `21` REC-COL-003,
REC-ADM-002).

*Source: PRIV-CONS-001, PRIV-RIGHT-001a, D-153, D-162, D-166, D-183, D-221*

### 5.22 Age group

Recorded by the age screen when `registration.adultaffirmation` = `off`: `minor` ·
`adult`. Finer bands are host work when a host needs them.

*Source: REG-PROF-002, D-153*

### 5.23 Alert conditions

One identifier per OPS-ALERT-001 row, carried by `AlertRaised` and the first part of the
deduplication key of OPS-ALERT-002, which adds the scope (below) and the account or
actor: `auth-failures-sustained` · `recovery-clustering` · `approver-volume` ·
`read-volume-anomaly` · `breakglass-used` · `breakglass-generated` ·
`protected-setting-changed` · `stepup-policy-weakened` ·
`concurrent-sessions-implausible` · `denial-spike` · `background-job-failed` ·
`erasure-delivery-exhausted` · `degradation` · `callback-verification-failed` ·
`nonexistent-notice-rate` · `privacy-deadline-approaching` · `privacy-deadline-reached`
· `expiry-approaching` · `holiday-list-exhausted` · `no-emergency-credential` ·
`restriction-loosened` · `restriction-granted` · `domain-reverification-failed` ·
`domain-removed` · `governing-language-changed` · `governing-text-missing`.

`breakglass-used` and `breakglass-generated` (a first issue or a replacement) are High,
and reach the owner as well as the operator when the host delivers them (OPS-BOOT-002,
OPS-BOOT-004, OPS-ALERT-003).

**Scopes.** Where a condition is raised for one of several things, a scope names the
thing. The scopes the chapters name:

| Condition | Scope | Raised when | Source |
|---|---|---|---|
| `degradation` | `mailbox.push:<mailbox id>` | A mailbox push spent `outbox.retry.maxattempts`; details `{ mailbox, state, attempts }`, the mailbox named by its identifier and never by its address; raised again each time the push, begun again, spends them | INT-MAIL-007, D-177 |
| `degradation` | `mailbox.conflict:<mailbox id>` | A mailbox push met, at the mailbox's name, a server account that does not carry the mailbox's identifier (`integration.mailserver.conflict`); details `{ mailbox, state }`, the mailbox named by its identifier and never by its address; raised again each day the push, begun again, meets it | INT-MAIL-001, INT-MAIL-007, D-177 |
| `degradation` | `mailbox.reconciliation` | Reconciliation found a difference, details `{ mailboxes, unknown }`, or could not have the listing, `{ listed: false }` | INT-MAIL-007 |
| `degradation` | `send:<channel>` | A message the host's notification handler never took spent `outbox.retry.maxattempts` (section 4.6) and was removed (INT-SMS-006) | AUTH-ABUSE-004, D-162 |
| `degradation` | `notice.cookies` | The cookies the deployment sets (the library's and the host's declared ones) differ from those the current version of the privacy notice lists; compared by name, purpose, lifetime and third-party readability at each start and at each change of a key that gives a library cookie's lifetime, and raised until a version is published that lists them; nothing is raised before a first version is published; details `{ added, removed, changed }`, by name | PRIV-CONS-006a, D-203 |
| `degradation` | `password.blocklist.fallback` | Screening fell back to the offline list; `details.configured` and `details.used` (`offline`) | OPS-OBS-002, AUTH-PASS-004 |
| `degradation` | `botdefence.ranges.absent` · `botdefence.ranges.stale` · `botdefence.ranges.refresh` | At a read of the file that fails, the job's or the first a process makes where a registration is judged, the file not opened or read, or refused whole (`.refresh`); and while `datacenterRange` is among `abuse.botdefence.signals`, at each run of the `datacenter-ranges` job and where a registration is judged, no range source declared or no range file read (`.absent`), or the file held older than `abuse.botdefence.ranges.maxage` by its own date (`.stale`) | AUTH-ABUSE-008, D-189, D-190, D-191 |
| `degradation` | `location.database.absent` · `location.database.stale` · `location.database.refresh` | At a read of the file that fails, the job's or the first a process makes where an address is resolved, the file not opened or read, or refused whole (`.refresh`); and where an address is resolved, no location file declared or none read (`.absent`), or the file held older than `location.database.maxage` by its own date (`.stale`) | INT-GEN-006, OPS-OBS-002, D-190, D-191 |
| `degradation` | `provider.unavailable:<provider>` | A social provider's round trip could not reach or read the provider, its discovery document, its published keys or its token endpoint; details `{ provider, part }`, `part` one of `discovery` · `keys` · `token` | IDN-LIFE-012, OPS-OBS-002, D-192 |
| `expiry-approaching` | `kek-cryptoperiod` | The key-encryption key's cryptoperiod ends within `maintenance.expiry.warninglead`; `details.version`, `details.rotatedAt`, `details.dueAt` | DR-009a |

A lost registration channel raises `degradation` with `details.component`
`registration-channel` (OPS-OBS-002, REG-SESS-003). A push the adapter will not apply to
a mail-server account that does not carry the mailbox's identifier is answered
`integration.mailserver.conflict` and raises `degradation` at once under
`mailbox.conflict:<mailbox id>`, naming the mailbox by its identifier (INT-MAIL-001,
INT-MAIL-007, REG-MAIL-003); a drift the drift check finds raises it naming the
derivation (AUTHZ-DERIVE-005); the location file and the datacenter range file raise it
under the scopes above (INT-GEN-006, AUTH-ABUSE-008, D-189, D-190).

*Source: OPS-ALERT-001, OPS-ALERT-002, OPS-OBS-002, D-153, D-166, D-177, D-178, D-189,
D-190*

### 5.24 Audit actions

The actions the library writes to the audit trail, stable as the error codes are: an
action is an identifier, and changing one is breaking (CONV-NAME-003). The set is closed:
the library writes these and no other, and a new one is added here in the same change
(REF-001). The category is the partition the row is routed to and the retention it
follows (PRIV-RET-001, PRIV-RET-002): `security` · `routine`; every privacy action is
`security`.

Every record carries an acting identity and an effective identity equal to it
(AUTHZ-IMP-001); work of a system principal carries the nil subject in both, with the
principal's name (section 5.29, or a host's declared job's, IDN-PRIN-001) and its stated
reason. A record that concerns an account
names it as `subject`, whoever acted (IDN-AUD-001, PRIV-BREACH-002). Every record a break-glass session writes, or a session another application opened from it (BFF-SESS-006), carries the reason given at its use, `auth.breakglass.used`
included, in a field of the record's own beside its identities, never in `details`,
whose `reason` is the action's own; it is plain, as every stated reason is (OPS-BOOT-002, D-170, D-171).

| Action | Category | Written when | Source |
|---|---|---|---|
| `auth.authentication.failed` | security | A factor presented at sign-in, at a sign-in link press (a link token unknown or expired included, when pressed) or on a social provider's return was refused; the new-device check's verification code was refused; or the break-glass credential was refused, a consumed code (`auth.breakglass.consumed`) included (OPS-BOOT-004). The acting identity is the nil subject; `subject` is the account the attempt was made against, and is absent where the identifier, handle or link token resolved to none, the provider's identity is linked to no account or did not hold up, or the break-glass code was refused before the reserved account was read. `details.factor` names the factor, or `details.verification` is `device` where the refused value was the new-device check's code. Nothing typed is written, and no row is written while a delay of AUTH-ABUSE-001 stands; a second step presented at a sign-in before a first factor is accepted writes none, including an assertion presented as `passkey` whose credential is a security key of the account the sign-in is for, while one presented as `passkey` whose credential that account does not hold, or at a sign-in whose identifier names no account, writes one (AUTH-FACT-002b, D-197, D-198, D-199); no organization | CONV-LOG-005, IDN-AUD-001, AUTH-ABUSE-001, OPS-BOOT-004, D-186, D-197, D-198, D-199 |
| `auth.stepup.failed` | security | A factor presented to step a live session up was refused, including against a challenge that is not the asker's, behind the sign-in delay. The session's account is the acting identity and `subject`; `details.session` and `details.factor`; no organization | CONV-LOG-005, AUTH-ABUSE-001 |
| `auth.botdefence.signalled` | security | The bot defence answered a registration with a signal, recorded without the signal's own detail | AUTH-ABUSE-008, LIB-HOST-001 |
| `auth.breakglass.generated` | security | The break-glass credential was issued or replaced. The acting identity is who generated it; `details.credential`, and `details.replaced` where there was one; no organization | OPS-BOOT-004 |
| `auth.breakglass.used` | security | The break-glass credential opened the emergency session. The reserved account is the acting identity and `subject`; `details.credential` and `details.session`; no organization | OPS-BOOT-002, CONV-LOG-005 |
| `auth.credential.countermismatch` | security | An authenticator presented a signature counter that did not advance, for a credential the account the ceremony is for holds; none is written for one it does not hold (D-200) | AUTH-FACT-014, D-200 |
| `auth.credential.enrolled` | security | A credential reached `active` on an account | AUTH-FACT-001, AUTH-STEP-007 |
| `auth.credential.invalidated` | security | A suspended credential was invalidated at the end of its window | AUTH-RECOV-007 |
| `auth.credential.invalidationheld` | security | The window of a suspended credential ended with none of its notices taken by its immediate attempt (AUTH-ABUSE-004), so the invalidation was held and the credential stays suspended | AUTH-RECOV-007 AC3, D-186 |
| `auth.credential.labelled` | routine | The holder gave a credential a label or renamed it | AUTH-FACT-001 |
| `auth.credential.removed` | security | A credential was removed from an account | AUTH-FACT-001 |
| `auth.credential.reportcancelled` | security | A suspension by loss report was cancelled inside its window | AUTH-RECOV-007 |
| `auth.credential.reportedlost` | security | A credential was reported lost and suspended, which starts its window | AUTH-RECOV-007 |
| `auth.credential.restored` | security | A credential a provider's security event suspended stands again, because a session began on another factor; `details.credential`. Not the loss-report cancellation, which is `auth.credential.reportcancelled` | IDN-LIFE-012a |
| `auth.mailcredential.created` | security | The mail server generated an app password at its holder's request; `details.credential` is the server's identifier, and neither secret nor label is written; the account is the acting identity and `subject`; no organization | REG-MAIL-002, INT-MAIL-010 |
| `auth.mailcredential.revoked` | security | The mail server revoked an app password at its holder's request; `details.credential`; the account is the acting identity and `subject`; no organization | REG-MAIL-002, INT-MAIL-010 |
| `auth.oidc.clientregistered` | security | A client was registered in the provider's registry, or a registered one changed, from the server; principal `register-client`, reason `AUTH-OIDC-001`; `details.client`, `details.kind`, `details.changed`, and nothing of its secret; no subject, no organization | AUTH-OIDC-001, OPS-SEC-002 |
| `auth.oidc.refreshreused` | security | A consumed refresh token was presented again, which revokes its session family (the session record's spine, the provider's grant and every token of that grant); written in the unit of work that revokes them. The acting identity and `subject` are the account the token names, or else the one its grant records, or else the account of the session it names, the acting identity the nil subject and `subject` absent where none names one (D-208); `details.client`, the client that presented it, and `details.session`, the session the token names, absent where it names none (D-208); no organization | AUTH-OIDC-003, D-207, D-208 |
| `auth.phonesignal.considered` | security | A phone signal (section 5.36) was consulted before a send, recorded without the number; `unavailable` where no provider is declared | AUTH-FACT-002b, LIB-HOST-001 |
| `auth.providerevent.rejected` | security | A social provider's security event about a linked identity was refused: not verified by the provider's keys, or carried before. `details.credential`, `details.event` (the provider's spelling), `details.outcome` (`unsigned` · `replayed`); the account is `subject` | IDN-LIFE-012a AC1 |
| `auth.providerevent.taken` | security | A social provider's security event about a linked identity was carried. `details.credential`, `details.event`, `details.outcome` (`sessionsEnded` · `credentialUnlinked` · `accountSuspended` · `addressUnverified` · `recorded`); the account is `subject` | IDN-LIFE-012a |
| `auth.recovery.approved` | security | An admin-assisted recovery was approved, naming the approver and the reason | AUTH-RECOV-002, AUTH-RECOV-002a |
| `auth.restriction.edited` | security | A named sending restriction was created, edited or deleted; `details.restriction`, `details.loosening`, `details.before`, `details.after` and `details.reason` | AUTH-ABUSE-004, OPS-CFG-008 |
| `auth.restriction.granted` | security | Support granted credit to one key under a named restriction; `details.restriction`, `details.credit`, `details.reason`; the key is never written | AUTH-ABUSE-004 |
| `auth.session.presented` | security | A session began, or was stepped up, on the factors presented; `details.session` and `details.factors` | CONV-LOG-005, IDN-AUD-001 |
| `authz.access.denied` | security | A permission the gate was asked for was refused `authz.denied`, or concealed as `authz.resource.notfound`; the row a refusal's correlation identifier resolves to. A refusal answering a residual of section 5.20 writes none (D-208). Written outside the caller's transaction and committed at once, so a refusal inside an operation that rolls back is still recorded and counted | AUTHZ-CONCEAL-004, AUTHZ-GATE-004, D-208 |
| `authz.access.exported` | security | An export operation was admitted at the gate; the exporter, or the nil subject with a system principal's name and reason; `details.permission`, `details.resourceType`, and `details.resource` where one record was checked. Not written while `exfiltration.export.auditing` is off | OPS-ALERT-006 |
| `authz.grant.created` | security | A person, an acknowledgement or bootstrap created a grant, other than through a refresh (`authz.grant.materialised`, D-207); its `subject` the grant's holder where the holder is an account and none where it is a group, whoever acted; `details.grant`, `details.role`, `details.reason`; written in the operation's transaction | AUTHZ-GRANT-003, D-197, D-206, D-207 |
| `authz.grant.materialised` | security | The drift check wrote a derivation's grant it found missing (principal `derivation-driftcheck`, reason `AUTHZ-DERIVE-005`), or a host's refresh wrote a derivation's grant (acting the person who called it, or a system principal's name and its reason, D-207, D-208); its `subject` the grant's holder where the holder is an account and none where it is a group; `details.grant`, `details.role` | AUTHZ-DERIVE-005, AUTHZ-GRANT-003, D-187, D-206, D-207, D-208 |
| `authz.grant.retracted` | security | The drift check took back a derivation's grant no relationship supports (principal `derivation-driftcheck`, reason `AUTHZ-DERIVE-005`), or a host's refresh took back one (acting the person who called it, or a system principal's name and its reason, D-207, D-208); its `subject` the grant's holder where the holder is an account and none where it is a group; `details.grant`, `details.role` | AUTHZ-DERIVE-005, AUTHZ-GRANT-003, D-187, D-206, D-207, D-208 |
| `authz.grant.revoked` | security | A person revoked a grant, other than through a refresh (`authz.grant.retracted`, D-207), or the removal of the record it names revoked it (`IResources.RemoveAsync`, the nil subject as revoker, `details.reason` `AUTHZ-INHERIT-002`), or the job `membership-ends` revoked it at an end set in advance (the nil subject as revoker, its principal with reason `IDN-MEM-001`, `details.endSetBy` the administrator who set the end, `details.reason` the reason given, D-217); its `subject` the grant's holder where the holder is an account and none where it is a group, whoever acted; `details.grant`, `details.role`, `details.reason`; written in the operation's transaction | AUTHZ-GRANT-003, AUTHZ-INHERIT-002, D-197, D-203, D-206, D-207, D-217 |
| `authz.group.created` | security | A group was created; `details.group`, `details.name`, `details.reason`; filed under the group's organization | AUTHZ-GROUP-001 |
| `authz.group.memberadded` | security | An account or a group was added to a group; `details.group`, `details.name`, `details.memberType`, `details.memberId`, `details.reason` | AUTHZ-GROUP-001, OPS-CFG-007 |
| `authz.group.memberremoved` | security | An account or a group was taken out of a group; the same details; where the job `membership-ends` took it out, as for `authz.grant.revoked` (D-217) | AUTHZ-GROUP-001, OPS-CFG-007, D-217 |
| `authz.group.removed` | security | A group nothing named was removed; `details.group`, `details.name`, `details.reason` | AUTHZ-GROUP-001 |
| `authz.role.defined` | security | A role was created, or its permissions or its `handsOut` changed; `details.role`, `details.before` and `details.after`, each the permissions and the `handsOut` (`before` empty for a role bootstrap or the role declaration defines), `details.reason` (`AUTHZ-GRANT-004` for one the role declaration defines, principal `role-declaration`); no organization | AUTHZ-GRANT-004, AUTHZ-GRANT-005, OPS-CFG-007, OPS-BOOT-001, D-204, D-212 |
| `authz.role.removed` | security | A role nothing named was removed; `details.after` is null | AUTHZ-GRANT-004 |
| `identity.account.deactivated` | routine | The owner deactivated the account | IDN-LIFE-013 |
| `identity.account.reactivated` | routine; security where an administrator acted | A suspended account was stood back up: by its owner from a deactivation, or by an administrator from an administrator's suspension (the administrator acting, the account the `subject`) | IDN-LIFE-013 |
| `identity.account.suspended` | security | An administrator suspended an account, or took over the suspension of one its owner deactivated; the administrator acting, the account the `subject`; no organization | IDN-LIFE-013, AUTH-SESS-010 |
| `identity.deletion.cancelled` | routine; security where an administrator acted | A deletion was cancelled inside its grace window: by the subject from the link, or from a record access session for a person record, or by an administrator on the subject's behalf, with `details.request` where an out-of-band request began the window | IDN-ACCT-007, IDN-LIFE-003, IDN-LIFE-014, REC-ACC-002, D-221 |
| `identity.deletion.requested` | routine | The owner requested deletion, which opens the grace window; for a person record, the person from a record access session, the record acting and its `subject` | IDN-ACCT-007, IDN-LIFE-014, REC-ACC-002, D-221 |
| `identity.invitation.acknowledged` | security | An invitation was acknowledged and its membership attached; `details.invitation`; the person acknowledging is the actor; filed under the organization | REG-INV-001, IDN-LIFE-009a |
| `identity.invitation.issued` | security | An invitation into an organization was issued; `details.invitation` and nothing it binds, and `details.formerMailbox` with its reason where the invitation names one because the corporate address's mailbox has been held before (section 5.44); filed under the organization | IDN-LIFE-009a, REG-INV-001, REG-MAIL-001 |
| `identity.invitation.revoked` | security | An unacknowledged invitation was revoked, or replaced by a later one for the same corporate address; `details.invitation` | IDN-LIFE-009a, REG-MAIL-001 |
| `identity.membership.ended` | security | An administrator ended a membership, or the job `membership-ends` ended it at the instant one set; `details.membership`, `details.reason`; the administrator acting, the member the `subject`; where the job ended it, its principal `membership-ends` with reason `IDN-MEM-001`, `details.endSetBy` the administrator who set the end and `details.reason` the reason given; filed under the organization | IDN-MEM-001, REG-MAIL-003, D-213, D-217 |
| `identity.membership.endset` | security | An administrator set, moved or cancelled the instant a membership ends; `details.membership`, `details.endsAt` (absent where cancelled), `details.reason`; the administrator acting, the member the `subject`; filed under the organization | IDN-MEM-001, D-213 |
| `identity.organization.created` | security | An organization was created with no policy override; `details.reason`; filed under the organization; bootstrap's is recorded under the principal `bootstrap` | IDN-ORG-002, OPS-BOOT-001 |
| `identity.organization.deletioncancelled` | security | An organization's deletion was cancelled inside its window; `details.reason`; filed under the organization | IDN-ORG-003 |
| `identity.organization.deletionrequested` | security | An organization's deletion was requested: it is suspended and every member session ended; `details.reason`; filed under the organization | IDN-ORG-003 |
| `identity.organization.domainadded` | security | A domain was listed in an organization's lock, unverified; `details.domain`, `details.reason`; filed under the organization | REG-DOM-001, IDN-ORG-006 |
| `identity.organization.domainremoved` | security | A domain was removed from an organization's lock; its addresses stay refused until it is listed anew; `details.domain`, `details.reason`; filed under the organization | REG-DOM-001 |
| `identity.organization.domainverified` | security | A listed domain was verified by its TXT record; `details.domain`, `details.reason`; filed under the organization | REG-DOM-001 |
| `identity.organization.erased` | security | An organization's deletion window elapsed and the erasure executed; principal `organization-erasure`, reason `IDN-ORG-003`; `details.organization`, `details.deletingSince`, `details.membershipsEnded`; no subject; filed under the organization | IDN-ORG-003 |
| `identity.preferences.changed` | routine | The account's preferences changed, recorded by key and never by value; where an administrator set them, the administrator acting, the account the `subject` and `details.reason` (`01` IDN-READ-003, D-218) | REG-PREF-001, IDN-ATTR-001, D-218 |
| `identity.profile.changed` | routine | A profile field of the account changed, or its photo was set or removed | REG-PROF-001, IDN-ATTR-002 |
| `identity.record.accessed` | security | A record access session reached a person record, at its opening or by a reference; the record acting and its `subject`; `details.session`, `details.byReference`; filed under the record's organization | REC-ACC-001, D-221 |
| `identity.record.attached` | security | A person record was attached to an account; the account acting, the record its `subject`; `details.account`; filed under the record's organization | REC-ATT-001, D-221 |
| `identity.record.changed` | routine | A person record's name, language or contacts were changed: by the person (the record acting, or the account it is attached to), or by staff with `details.reason`; recorded by field, never by value; the record its `subject` | REC-ACC-002, REC-ADM-002, D-221 |
| `identity.record.collected` | security | A collection wrote or reused a person record; the record acting and its `subject`; `details.created`; filed under the record's organization | REC-COL-002, D-221 |
| `identity.record.entered` | security | Staff entered a person record; the staff member acting, the record its `subject`; `details.ageAnswered`; filed under the record's organization | REC-COL-003, D-221 |
| `identity.secondstep.preferred` | routine | The account's preferred second step changed | IDN-ATTR-008 |
| `identity.takedown.executed` | security | Phase one of a takedown committed, of an account or of a person record (`21` REC-TKD-001), the one taken down its `subject`; `details.takedown`, `details.trigger` (section 5.12d), `details.reason`, `details.erasureDue` | IDN-LIFE-003 AC3, REC-TKD-001, D-221 |
| `identity.takedown.reversed` | security | A takedown was reversed inside its window; `details.reason` | IDN-LIFE-003, REC-TKD-001 |
| `identity.username.changed` | routine | The account's username changed | REG-IDENT-009 |
| `ops.auditpartitions.maintained` | security | A run of the audit retention job completed; principal `audit-partitions`, reason `PRIV-RET-002`; `details.created`, `details.dropped`, `details.securityRetentionDays`, `details.routineRetentionDays`; no subject, no organization | PRIV-RET-002, INF-BG-002 |
| `ops.configuration.changed` | security | A runtime setting was changed through the one configuration operation, or a value was set by bootstrap or by `configure` from the server under that command's principal; `details.key`, `details.before` (the value in force, null where the key has no default and no value stood), `details.after`, `details.loosening` and `details.reason`, which every change carries | OPS-CFG-002, OPS-CFG-004, OPS-CFG-005, OPS-CFG-008 |
| `ops.keyrotation.completed` | security | A rotation of the key-encryption key or the fingerprint key reached every value under its version; principal `rotate-kek` or `rotate-fingerprint-key`, reason `OPS-SEC-003`; `details.kind` (section 5.41), `details.version`, `details.processed`; no subject, no organization | OPS-SEC-003 AC5 |
| `ops.keyrotation.resumed` | security | A stopped rotation was taken up again from its recorded progress; the same principal and details | OPS-SEC-003 AC2, AC5 |
| `ops.keyrotation.retired` | security | The versions before a completed rotation's were retired by `--retire`; the same details and `details.retired` | OPS-SEC-003 AC3, AC4, AC5 |
| `ops.keyrotation.started` | security | A rotation started; the same principal and details | OPS-SEC-003 AC5 |
| `ops.restoretest.completed` | *Retired by D-225. Backups are the deployment's.* | | |
| `privacy.consent.granted` | security | A consent was granted for a purpose, naming the document version it stands against; where staff acted for a person record (`21` REC-ADM-002), the staff member acting, the record the `subject`, `details.mechanism` `administrator` and `details.reason` the reason given | PRIV-CONS-001, PRIV-CONS-011, D-223 |
| `privacy.consent.withdrawn` | security | A consent was withdrawn for a purpose; where staff acted for a person record (`21` REC-ADM-002), the staff member acting, the record the `subject`, `details.mechanism` `administrator` and `details.reason` the reason given | PRIV-CONS-008, D-223 |
| `privacy.document.published` | security | A version of a legal document was published in its governing language | PRIV-CONS-005 |
| `privacy.document.translated` | security | A translation was filed against a published version | PRIV-CONS-005, PRIV-CONS-006 |
| `privacy.erasure.completed` | security | A delivery whose retries were spent was completed by hand, with its erasures row where it is an erasure's; `details.erasure` (the delivery's identifier), `details.kind` (section 5.50) and `details.outstanding` (the required subscribers or purpose handlers that had not confirmed, by name); the operator acting, the one the delivery concerns the `subject` | IDN-LIFE-003a, D-203 |
| `privacy.erasure.executed` | security | An erasure was carried out; on a replay of the off-host ledger, principal `replay-erasures`, reason `DR-016`, with `details.reason` in its section 5.12a spelling and `details.erasedAt`; for a person record's erasure under `record:erase` (`21` REC-ADM-003), the reason a person or a job gave in `details.statedReason`, `details.reason` staying `retention` | PRIV-RIGHT-005, DR-016, D-223 |
| `privacy.export.assembled` | security | A subject export was assembled for the subject; for a person record, by a record access session, by the account it is attached to, or by staff for the person (the staff member acting, `21` REC-ADM-002) | PRIV-RIGHT-003, REC-ACC-002, REC-ADM-002, D-221 |
| `privacy.objection.recorded` | security | An objection to a purpose was recorded; where staff acted for a person record (`21` REC-ADM-002), the staff member acting, the record the `subject`, `details.mechanism` `administrator` and `details.reason` the reason given | PRIV-RIGHT-001a, D-223 |
| `privacy.objection.withdrawn` | security | An objection was withdrawn and the purpose resumed; where staff acted for a person record (`21` REC-ADM-002), the staff member acting, the record the `subject`, `details.mechanism` `administrator` and `details.reason` the reason given | PRIV-RIGHT-001a, D-223 |
| `privacy.request.entered` | security | A data subject request was entered on the subject's behalf | PRIV-RIGHT-001, PRIV-RIGHT-002 |
| `privacy.request.fulfilled` | security | A data subject request was fulfilled | PRIV-RIGHT-002 |
| `privacy.request.lapsed` | security | A data subject request reached its deadline undecided | PRIV-RIGHT-002 AC3, AC4 |
| `privacy.request.refused` | security | A data subject request was refused, with the reason recorded | PRIV-RIGHT-002 |
| `privacy.request.submitted` | security | The subject submitted a data subject request | PRIV-RIGHT-001, PRIV-RIGHT-002 |
| `privacy.restriction.lifted` | security | An administrator lifted a restriction of processing and the subscribers were told; the administrator acting, the account or the person record the `subject`; no organization for an account, the record's for a person record | PRIV-RIGHT-004, REC-ADM-002, D-221 |

*Source: IDN-AUD-001, PRIV-RET-001, PRIV-RET-002, CONV-NAME-003, D-162, D-166, D-170,
D-187, D-197, D-206, D-207*

### 5.25 Message kinds

The kinds of message the library hands the host's notification handler (`05`
INT-SMS-006, D-225). The set is closed; the library holds no words (CONV-CONTENT-001),
and every message of a kind carries the same values, its places (section 5.26), which
the host's text places. A kind that carries a link carries it in the place `link`;
section 5.43 names the link kind each such message kind lands under. An alert is no
message: it reaches the host's alert handler (OPS-ALERT-003).

| Kind | Asked for when | Places | Source |
|---|---|---|---|
| `verification-code` | A code alone proves control of an address: the new-device check; a contact staged in a collection session; an account's identifier proven to find its person records, under the `verification` purpose (`21` REC-COL-001, REC-ATT-001) | `code` | AUTH-FACT-004, AUTH-FACT-016, REC-COL-001, REC-ATT-001 |
| `verification-link` | A code and a link prove control of an address or a number being registered, added or replaced | `code`, `link` | REG-SESS-003, REG-IDENT-004, REG-IDENT-007 |
| `sign-in-code` | The `emailCode` sign-in code, an authentication code held to `code.signin.lifetime` and `code.signin.attempts`, never sent as a verification code | `code` | AUTH-FACT-003, AUTH-FACT-004 |
| `signin-link` | A link that signs the person in; the code is shown only by the page opened elsewhere | `link`, `code` | AUTH-FACT-003, REG-SESS-003 |
| `secondstep-code` | A code presented as a second step (`phoneCode`) and the code of a `phoneCode` enrolment (D-197), each an authentication code held to `code.signin.lifetime` and `code.signin.attempts` | `code` | AUTH-FACT-002, D-197 |
| `security-notice` | Something happened to the account that the security-notice set is told of and that carries no link | none | REG-IDENT-002, AUTH-RECOV-002, REG-MAIL-002, IDN-LIFE-012a |
| `credential-suspended` | A credential was suspended by a loss report, or by a removal that would lower the account's reachable assurance; sent at the report, every `recovery.invalidation.noticeinterval` and 24 hours before invalidation, each carrying the cancel link; a `phoneCode` credential whose number left the account, whatever suspended it, is told by `credential-unbound` from then on | `link` | AUTH-RECOV-007, D-197 |
| `credential-unbound` | A `phoneCode` credential is suspended and its number has left the account, whatever suspended it (AUTH-FACT-002); sent when the number leaves, then on the schedule its suspension runs (every `recovery.invalidation.noticeinterval` and 24 hours before invalidation), carrying no link, since no cancel returns it | none | AUTH-RECOV-007, AUTH-FACT-002, D-197 |
| `enrolment-link` | The link that carries an admin-assisted enrolment | `link` | AUTH-RECOV-002 |
| `recovery-link` | The link a person asked for to set a new password; it restores nothing else, and carries the `signin` purpose (section 5.15) | `link` | AUTH-RECOV-004, AUTH-RECOV-005, REG-IDENT-002 |
| `no-account` | The answer to a request made for an address no account holds | none | AUTH-ABUSE-003 |
| `record-access-code` | The code that opens a record access session, an authentication code held to `code.signin.lifetime` and `code.signin.attempts`, under the `signin` purpose | `code` | REC-ACC-001, D-221 |
| `record-access-none` | The answer to an ask of a code for an address no reachable person record holds as a contact verified at a collection, once per `abuse.nonexistent.window`, judged under the purpose of the code asked for | none | REC-ACC-001, D-221 |
| `record-deletion-notice` | A person record's own deletion began its window; sent to its notice contacts, carrying no link, the cancellation being reached by a new code; a security notice to an existing holder (AUTH-ABUSE-004, D-222) | none | REC-ACC-002, D-221, D-222 |
| `account-exists` | The answer to a registration or a change made with an address an account holds, sent to the holder and never to the person who tried | none | REG-SESS-005, REG-IDENT-008 |
| `identifier-added` | An identifier was added to the account, a corporate address taken on at an invitation's acknowledgement included; sent to the security-notice set as it stood before | none | REG-IDENT-004, REG-MAIL-001 |
| `identifier-removed` | An identifier was removed; sent to the remaining security-notice set with the undo link | `link` | REG-IDENT-006, REG-IDENT-007 |
| `identifier-detached` | The removed identifier no longer reaches the account; no link, no powers | none | REG-IDENT-006 |
| `identifier-settings-changed` | The primary of a kind, or the kind's backup setting, changed | none | REG-IDENT-005, REG-MAIL-003 |
| `identifier-change-confirm` | The address a replace displaces is asked to confirm, only where the account has no other channel; under the `verification` purpose (section 5.15) | `link` | REG-IDENT-007, D-183 |
| `credential-enrolled` | A credential was enrolled on the account | none | AUTH-STEP-007 |
| `invitation-link` | The link of an invitation into an organization, sent to the email it binds and to nothing else, under the `signin` purpose (section 5.15) with no subject | `link` | IDN-LIFE-009a, REG-INV-001, REG-MAIL-001 |
| `deactivation-notice` | The account deactivated itself; carries the link that stands it back up | `link` | IDN-LIFE-013 |
| `deletion-notice` | The account's own deletion started its grace window; carries the cancel link | `link` | IDN-ACCT-007, IDN-LIFE-014 |
| `oob-deletion-notice` | An erasure request received out of band was fulfilled and the grace window started; sent to the security-notice set, or to a person record's notice contacts (`21` REC-ACC-002), with no cancel link | none | IDN-LIFE-003, REC-ADM-002 |
| `privacy-request-received` | The automatic receipt of a data subject request entering the queue; not a decision | none | PRIV-RIGHT-002 AC1 |
| `privacy-request-lapsed` | A subject's out-of-band erasure or rectification request reached its deadline undecided | none | PRIV-RIGHT-002 AC4 |
| `recovery-codes-reminder` | The one reminder a set of recovery codes gets once it is older than `recovery.codes.reminder`, sent to the security-notice set of an active account, carrying no link; the set is closed once the reminder's send to a channel is admitted, or where the security-notice set holds no channel the pass can reach | none | AUTH-FACT-008 AC5, D-186 |

*Source: CONV-CONTENT-001, INT-SMS-003, D-162, D-166, D-197*

### 5.26 Message places

The values a message carries, each by name, for the host's text to place (`05`
INT-SMS-006, D-225). The set is closed.

| Place | Carries |
|---|---|
| `code` | The code to enter |
| `link` | The address to open, `<origin>/link#<kind>.<token>` (section 5.43) |
| `token` | *Retired by D-166. Every link-bearing kind carries `link`, which holds the token.* |

*Source: D-162, D-166, D-225*

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
`erasure-replay` · `role-seeding`

The operations a deployment-scoped system principal may run, and nothing else
(IDN-PRIN-001 AC3); each principal of section 5.29 names exactly one. `delivery` carries
what has committed to where it goes (the outboxes, mailbox provisioning, raised alerts);
`monitoring` reads the state of something the deployment depends on and raises what the
reading calls for; `configuration` changes the deployment's configuration from the server,
a protected key or the provider's client registry; `role-seeding` defines a host's
declared roles at a start, inside the start step alone (AUTHZ-GRANT-004, D-204).

*Source: IDN-PRIN-001, INF-BG-001, INF-BG-002, OPS-BOOT-001, OPS-SEC-003, OPS-CFG-004, AUTH-OIDC-001, DR-016, AUTHZ-GRANT-004, D-166, D-204, D-206*

### 5.29 System principals

The name each background job, server command but `migrate` (which runs as none,
OPS-MIG-001, D-206) and start step acts under, written as the principal of the audit
rows it writes, with the reason below, and carried by `background-job-failed`. Every job
refuses to run under a principal that may not run its operation (IDN-PRIN-001). A start
step runs once at each start, before the application serves a request. A host's own jobs
are declared by the host and named by it (IDN-PRIN-001, LIB-HOST-001); none takes a name
of this table, nor `migrate`, the command that runs as none (D-206).

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
| `membership-ends` | job | `expiry-sweep` | IDN-MEM-001 |
| `outbox` | job | `delivery` | IDN-LIFE-003a |
| `events` | job | `delivery` | IDN-LIFE-003a |
| `sends` | job | `delivery` | INF-BG-001 |
| `mailbox-provisioning` | job | `delivery` | INT-MAIL-006a |
| `alert-dispatch` | job | `delivery` | OPS-ALERT-003 |
| `mail-reconciliation` | job | `reconciliation` | INT-MAIL-007 |
| `derivation-driftcheck` | job | `reconciliation` | AUTHZ-DERIVE-005 |
| `location-database` | job | `monitoring` | INT-GEN-006 |
| `datacenter-ranges` | job | `monitoring` | AUTH-ABUSE-008 |
| `read-volume-baseline` | job | `monitoring` | OPS-ALERT-005 |
| `holiday-list` | job | `monitoring` | PRIV-RIGHT-002 |
| `emergency-credential` | job | `monitoring` | OPS-BOOT-001 |
| `audit-partitions` | job | `retention-purge` | PRIV-RET-002 |
| `kek-cryptoperiod` | job | `monitoring` | DR-009a |
| `bootstrap` | command | `bootstrap` | OPS-BOOT-001 |
| `configure` | command | `configuration` | OPS-CFG-004 |
| `register-client` | command | `configuration` | AUTH-OIDC-001 |
| `rotate-kek` | command | `key-rotation` | OPS-SEC-003 |
| `rotate-fingerprint-key` | command | `key-rotation` | OPS-SEC-003 |
| `replay-erasures` | command | `erasure-replay` | DR-016 |
| `role-declaration` | start | `role-seeding` | AUTHZ-GRANT-004 |

*Source: IDN-PRIN-001, INF-BG-001, INF-BG-002, AUTHZ-DERIVE-005, AUTHZ-GRANT-003, D-166,
D-189, D-202, D-204, D-206*

### 5.30 Conformance checks and truth-table scenarios

Checks, the part of the suite a finding came from: `policies` · `truth-table` ·
`declaration` · `provider`.

Scenarios, the values of `authz.truthtable.disagreement`'s `details.scenario`:
`grant-on-record` · `grant-on-container` · `grant-above-container` ·
`grant-on-organization` · `grant-on-sibling` · `no-grant` · `grant-to-group` ·
`grant-to-nested-group` · `deny-over-grant` · `deny-on-container-over-grant` ·
`expired-grant` · `revoked-grant` · `grant-in-another-organization` ·
`role-without-permission` · `derived-grant` · `derived-grant-on-container` ·
`deny-over-derived-grant` · `stepup-met` · `stepup-level-unmet` ·
`stepup-phishingresistance-unmet` · `stepup-age-unmet` · `stepup-instant-future` ·
`stepup-provider-failed` · `stepup-provider-absent`. A derived scenario runs once for
each derivation its type declares; a step-up scenario agrees when the check answers the
gate's outcome and the filter answers the same, listing the record where the gate is met
and refused with the check's code where it is not (AUTHZ-TEST-001 AC2); each step-up
scenario runs on a deployment the host's factory builds with the suite's provider, or
with none for `stepup-provider-absent`, under that provider's report, against the gate
the deployment's policy gives the action (the suite's account holds no membership); a
scenario that withholds what that gate does not ask (phishing resistance, a level, an
age) is met, so a host whose gate asks none leaves the scenario out of its table, and
one that states it is answered with the disagreement (D-189); and `stepup-met` is the
one step-up scenario whose case states `Allowed` true (LIB-TEST-001 AC2, D-188).

*Source: LIB-TEST-001, AUTHZ-TEST-001, D-166, D-183, D-187, D-188, D-189*

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

*Retired by D-225, with OPS-MAINT-001.*

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
carries `began` · `ended`. `GrantChanged` carries `made` · `revoked`;
`GroupMembershipChanged` `added` · `removed`; `RoleChanged` `defined` · `changed` ·
`removed` (D-218).

`SessionEnded` carries its `reason`: `signedout`, the person signed out
(`ISessions.EndEverywhereAsync`, `POST /auth/logout`), which ends every session of the
sign-in; `ended`, the person ended it through their session list (`ISessions.EndAsync`,
`DELETE /account/sessions/{id}`), whichever session it names, or a change to their
credentials or identifiers ended their other sessions; `revoked`, an administrator
revoked the account's sessions or every session; `security`, the library ended it on a
sign of compromise: a refresh token presented twice (AUTH-OIDC-003) or a provider's
security event (IDN-LIFE-012a); `account`, the account was restricted, suspended,
deactivated, deleted, taken down or erased; `organization`, its organization's deletion
was requested (IDN-ORG-003) (D-218, D-219).

*Source: PRIV-CONS-007, PRIV-CONS-008, IDN-MEM-001, section 5b, D-166, D-218, D-219*

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
is the landing origin the host declares for the application the link lands on
(LIB-HOST-001), and the token travels in the fragment, so no server receives it in the
address. The set is closed; each application's one landing component dispatches on the
kind (`18` FE-VER-001).

Authentication application: `sign-in` · `registration` · `recovery` · `enrolment` ·
`invitation`.

Account application: `identifier` · `identifier-confirm` · `undo` · `deletion-cancel` ·
`reactivation` · `loss-report`; save the `identifier` link of a replace an enrolment
session staged, which lands on the authentication application (D-190).

Every kind acts only on a press, never on load, so a mail scanner's prefetch changes
nothing. A message kind of section 5.25 lands under one link kind: `signin-link` under
`sign-in`; `verification-link` under `registration` in a registration and under
`identifier` on an account; `recovery-link` under `recovery`; `enrolment-link` under
`enrolment`; `invitation-link` under `invitation`; `identifier-change-confirm` under
`identifier-confirm`; `identifier-removed` under `undo`; `deletion-notice` under
`deletion-cancel`; `deactivation-notice` under `reactivation`; `credential-suspended`
under `loss-report`.

*Source: API-LAND-001, FE-VER-001, LIB-HOST-001, D-166, D-190*

### 5.44 Former mailbox

`transfer` (the invitee receives the mailbox and the mail in it) · `replace` (the old
mailbox is removed and a new one reserved)

The `formerMailbox` member of `POST /admin/organizations/{id}/invitations`, required
where the corporate address's mailbox has been held before, by anyone, the invitee
included (`identity.invitation.mailboxheld`), and refused with `api.request.invalid`
where no held mailbox stands for the address; a mailbox replaced or released no longer
stands for it, and the address of an erased holder is not recognised (REG-MAIL-003). The
issue checks nothing about who the invitee is. Either value is stepped up with the
issue, carries the reason and is recorded. No mailbox anyone has held passes to a holder
without it, and the library removes such a mailbox only under `replace`. The mail-server
adapter adopts an existing server account only where its description carries the
identifier of the mailbox being pushed; a refusal to adopt is answered
`integration.mailserver.conflict`, and the push that met the account is marked failed at
once and raises `degradation` scoped `mailbox.conflict:<mailbox id>` (section 5.23).
Under `replace` the old mailbox's row records the instant it was replaced, is owed
`removed` and keeps its fingerprint until its last holder is erased, the new mailbox's
row stands for the address, and the new mailbox's push waits until the server has
confirmed the old one's removal (INT-MAIL-006, INT-MAIL-007).

*Source: REG-MAIL-001, REG-MAIL-003, INT-MAIL-001, INT-MAIL-006, INT-MAIL-007, D-166, D-177, D-178*

### 5.45 Mailbox states

`disabled` · `enabled` · `removed`

The state the library owes a mailbox and pushes to the mail server (`IMailServer`,
LIB-HOST-001; INT-MAIL-001, INT-MAIL-007). `disabled` is a server account whose holder
cannot authenticate, so no app password works, and which still receives mail; `enabled`
is one whose holder can; `removed` is the server account destroyed. A mailbox is owed
`enabled` only while its holder's account is `active` or `restricted` and holds a
current membership of the administrative organization, and `disabled` otherwise, a
reservation for an invitation included, save that a mailbox replaced (REG-MAIL-003) or a
mailbox nobody has held released with its revoked invitation (REG-MAIL-001) is owed
`removed` whatever its holder's state short of erasure (INT-MAIL-006), and a mailbox
whose holder was erased is owed nothing further (INT-MAIL-007).

*Source: INT-MAIL-001, INT-MAIL-006, INT-MAIL-007, LIB-HOST-001, D-166, D-178*

### 5.46 Export formats and sections

The `format` of `GET /privacy/export`: `human` (grouped, labelled and ordered for
reading) · `machine` (flat, complete and stable-named: one object of values keyed
`<section>.<index>.<value>`, the index counting a section's records from 0, D-206). The
`format` of `GET /admin/ropa`: `template`. Each is case-sensitive; an absent or other
value is refused with `api.request.malformed` naming `format`.

The sections of the export, in this order (PRIV-RIGHT-003): `account` · `profile` ·
`identifiers` · `identifier-backup` · `credentials` · `recovery-codes` · `devices` ·
`preferences` · `memberships` · `membership-acknowledgements` · `group-memberships` ·
`grants` · `assurance` · `sessions` · `consents` · `objections` · `records` (each person
record attached to the account, `21` REC-ATT-002); then each export contributor's
sections, in contributor name order, none taking a name above (PRIV-RIGHT-003, D-203).
No section or value name holds a `.` (D-206).

The sections of a person record's export, in this order (`21` REC-ACC-002): `record`
(state, origin, organization, creation, the age answer and its instant, each notice
presented and terms accepted) · `profile` (the name) · `identifiers` (each contact with
its kind and the instant it was verified at a collection) · `preferences` (the language)
· `consents` · `objections`; then each export contributor's sections for the record's
subject, as above, none taking `record` or `records` either (D-221).

*Source: PRIV-RIGHT-003, PRIV-ROPA-001, `09` `GET /privacy/export`, `09`
`GET /admin/ropa`, D-166, D-203, D-206, D-221*

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

### 5.50 Delivery kinds

`erasure` · `takedown` · `restriction` · `consent` · `objection`

The kind of a delivery of host-side work (IDN-LIFE-003a): the work of an erasure
(`ErasureRequested`), of a takedown's trigger (`TakedownExecuted`) and of a restriction
change (`RestrictionChanged`), delivered to the subject-event subscribers; and the work of
a consent change (`ConsentChanged`) and of an objection change (`ObjectionChanged`),
delivered to the purpose handlers naming its purpose (PRIV-CONS-008, PRIV-RIGHT-001a).
Each delivery carries a status (`awaiting-subscribers` · `complete` · `failed`, section
5.12) and its attempts. `GET /admin/erasures` lists each incomplete delivery with its
kind, and `POST /admin/erasures/{id}/complete` closes any of them (D-203). A delivery's
`change` there spells the change its event announces: a consent's as section 5.39 spells
it (`granted` · `withdrawn` · `superseded`), an objection's `recorded` where
`ObjectionChanged` records one and `withdrawn` where it withdraws one, a restriction's
`set` or `lifted` as `RestrictionChanged` sets or lifts it; an erasure and a takedown
carry none. The events themselves are unchanged.

*Source: IDN-LIFE-003a, D-203*

### 5.51 Person records

States: `active` · `restricted` · `deleting` · `deleted` (`21` REC-REC-002); a record
has no `suspended` state. Origin: `collection` (the person gave the data in a host's own
screen, REC-COL-002) · `staff` (a member of staff entered it for them, REC-COL-003).
`deletingBy` and the held state are an account's, save `suspensionHeld` (section 5.12b).

*Source: REC-REC-001, REC-REC-002, D-221*

### 5.52 Legal document names

`privacy-notice` (the privacy notice, PRIV-CONS-008a; `GET /privacy/notice`) · `terms`
(the terms of service: a registration's terms step and a person record's collection
check `termsVersion` against its current version, REG-SESS-007, `21` REC-COL-002). Every
other legal document is named by the host under INT-SMS-003's rule. The current version
of a document is the highest published (PRIV-CONS-005).

*Source: REG-SESS-007, REC-COL-002, D-222*

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
| `username:change` | Change the username | `PUT /account/profile` with a `username` other than the one the account holds when the edit's step-up is judged (REG-IDENT-009, D-208); display name and legal name are not gated |
| `factor:enrol` | Enrol a factor (AUTH-STEP-007); upgrade a security key to a passkey | `/account/factors/*`, `/auth/webauthn/register/*`, `POST /account/credentials/{id}/upgrade`; the label (`PATCH /account/credentials/{id}`) and the preferred second step (`PUT /account/secondstep/preferred`) are not gated |
| `factor:remove` | Remove an `active` factor | `DELETE /account/factors/{id}`, `DELETE /account/credentials/{id}` |
| `recoverycodes:generate` | Generate or regenerate recovery codes | `POST /account/recoverycodes` |
| `mailcredential:create` | Create a mail app password | `POST /account/mail/apppasswords` (REG-MAIL-002, INT-MAIL-010) |
| `mailcredential:revoke` | Revoke a mail app password | `DELETE /account/mail/apppasswords/{id}` |
| `privacy:export` | Export personal data | `GET /privacy/export` |
| `account:delete` | Request account deletion | `POST /account/delete` |
| `account:deactivate` | Deactivate the account (`suspendedBy = self`) | `POST /account/deactivate` (IDN-LIFE-013, D-147); reactivation at `POST /account/reactivate` accepts the link token from the deactivation notice and is not gated |
| `provider:link` | Link a social provider | `POST /account/link/{provider}`; `GET /auth/providers/{provider}?intent=link`, at its start and on its return (`09`, D-193) |
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
| `record:export` | Export a person record for the person | `GET /admin/records/{subject}/export` (`21` REC-ADM-002, D-221) |
| `record:restrictionlift` | Lift a person record's restriction | `POST /admin/records/{subject}/restriction/lift` (REC-ADM-002) |
| `record:deletioncancel` | Cancel a person record's deletion on the person's behalf | `POST /admin/records/{subject}/delete/cancel` (REC-ADM-002) |
| `record:takedown` | Execute a takedown of a person record | `POST /admin/records/{subject}/takedown` (REC-TKD-001) |
| `record:takedownreverse` | Reverse a takedown of a person record | `POST /admin/records/{subject}/takedown/reverse` (REC-TKD-001) |
| `privacyrequest:fulfil` | Fulfil a privacy request | `POST /admin/privacy/requests/{id}/fulfil`, for every request type (section 5.12c): an erasure's fulfilment starts a grace window, a restriction's restricts another person's account; refusing is not gated (PRIV-RIGHT-001) |
| `erasure:complete` | Complete a stuck delivery of host-side work of any kind of section 5.50 manually | `POST /admin/erasures/{id}/complete` |
| `config:loosen` | Loosen any security setting | `PUT /admin/config/{key}` where the change is a loosening (OPS-CFG-002); a tightening is not gated |
| `alerting:destinations` | *Retired by D-225. Alert destinations are the host's.* | |
| `policy:change` | Change an organization's policy | `PUT /admin/organizations/{id}/policy`: every replacement, a tightening included |
| `organization:delete` | Request or cancel an organization's deletion | `POST /admin/organizations/{id}/delete`, `/delete/cancel` (IDN-ORG-003); creating an organization is not gated |
| `domain:manage` | Add, verify or remove a locked domain | `/admin/organizations/{id}/domains/*` (REG-DOM-001) |
| `restriction:edit` | Edit a sending restriction | `PUT/DELETE /admin/restrictions/{name}` (AUTH-ABUSE-004): every edit, with a reason |
| `restriction:grant` | Grant sends to a key | `POST /admin/restrictions/{name}/grant` (AUTH-ABUSE-004): the support role, a reason required |
| `breakglass:replace` | Generate a replacement break-glass credential | from a break-glass session, which satisfies step-up for its lifetime (AUTH-STEP-004), or by a stepped-up system administrator from the management application (OPS-BOOT-004, D-147) |

**Not gated** (D-166): revoking an invitation, which touches no account; creating an
organization; publishing legal documents and their translations; compliance records;
refusing a privacy request; entering, correcting and erasing a person record, and
recording a consent or an objection the person gave staff, none of which reaches an
account's sign-in and the last of which a host's job calls (`21`, D-221).

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

The events the library emits, per LIB-API-001 ("Emitted events"), guarded by the same
contract tests. Each carries the subject reference, the event time, the acting and
effective identities where a person acted with an access context, as that context gives
them (neither where the event is raised from a link a message carried, with no context,
AUTHZ-IMP-001), and an idempotency key (INT-MAIL-007). No event names a consumer or a
consumer's domain (IDN-LIFE-003a). Every event is written to the outbox in the
transaction that makes it true (CONV-DESIGN-002, IDN-LIFE-003a). The subject events
(`ErasureRequested`, `RestrictionChanged`, `TakedownExecuted`, `TakedownReversed`,
`ConsentChanged`, `ObjectionChanged`) name a person record's subject as they name an
account's, and one for each record attached to an account the change applies to (`21`
REC-ATT-002, D-221).

| Event | Raised when | Consumers |
|---|---|---|
| `AccountRegistered` | The one transaction of the terms step has committed (REG-SESS-001, REG-SESS-007): the account exists, `active`, once per account; nothing fires for a registration session that is abandoned or expires | Host welcome — **no mailbox**: provisioning follows the invitation and enabling the membership (REG-MAIL-001) (INT-MAIL-006, IDN-LIFE-009b) |
| `TakedownReversed` | A takedown reversed inside its window; the account, or the person record (`21` REC-TKD-001), is back in the state it held at the trigger (section 5.12b) | Host: whatever the host did on `TakedownExecuted`, or on the trigger's `AccountSuspended`, is the host's to reconsider against that state |
| `OrganizationErased` | An organization's grace window elapsed and its erasure committed; carries the organization and the number of memberships ended, and names no subject | Host |
| `OrganizationCreated` | An organization was created (`POST /admin/organizations`); carries the organization and names no subject (D-218) | Host |
| `OrganizationDeletionRequested` · `OrganizationDeletionCancelled` | An organization's deletion was requested, suspending it, or cancelled inside its window (IDN-ORG-003); carries the organization and names no subject (D-218) | Host (stop, or resume, its own processing for the organization) |
| `GrantChanged` | A grant was made or revoked, a materialised derivation's grant written or taken back included; carries the grant, its kind, its holder (`subjectType`, `subjectId`), role, whether it denies, its organization and record, and `change` (`made` · `revoked`, section 5.39); the holder is the `subject` where it is an account (D-218) | Host |
| `GroupMembershipChanged` | An account or a group was added to or taken out of a group, by a person or by a membership's end; carries the group, its organization, the member (`subjectType`, `subjectId`) and `change` (`added` · `removed`, section 5.39); the member is the `subject` where it is an account (D-218) | Host |
| `RoleChanged` | A role was defined, changed or removed, a declared role's seeding included; bootstrap's three roles raise none, as bootstrap's organization raises no `OrganizationCreated` (D-219); carries the role and `change` (`defined` · `changed` · `removed`, section 5.39), and names no subject (D-218) | Host |
| `SessionEnded` | A session ended before its expiry; carries the session and `reason` (section 5.39); not raised for a session that expires by time, which ends at its next use (D-218) | Host |
| `IdentifierChanged` | *Retired by D-146. See `IdentifierAdded`, `IdentifierRemoved`, `IdentifierPrimaryChanged`.* | |
| `IdentifierAdded` | An added email or phone was verified and counts (REG-IDENT-004); also a replace that completed (REG-IDENT-007), and a corporate address taken on at an invitation's acknowledgement (REG-INV-001) | Host |
| `IdentifierRemoved` | An identifier was removed (REG-IDENT-006), or a corporate address was retired when a membership ended (REG-MAIL-003); a later undo fires `IdentifierAdded` | Host |
| `IdentifierPrimaryChanged` | The primary of a kind changed (REG-IDENT-005), including on invitation acknowledgement when the corporate address becomes primary (REG-INV-001) and when a membership ends (REG-MAIL-003) | Host |
| `AccountSuspended` · `AccountReactivated` | State enters or leaves `suspended`, by the subject or an administrator. `AccountSuspended` is also written at every takedown trigger, whatever state the account held, announcing that access stopped (IDN-LIFE-003) | Host |
| `AccountDeletionRequested` · `AccountDeletionCancelled` | The grace window starts or is cancelled, whether the owner or a fulfilled out-of-band erasure request began it; not raised for a takedown (D-222) | Host (pause its own processing for the subject) |
| `RecordDeletionRequested` · `RecordDeletionCancelled` | A person record's grace window, begun by the person or by an out-of-band request, starts or is cancelled; not raised for a takedown (`21` REC-ACC-002, REC-ADM-002, D-221) | Host (pause its own processing for the subject) |
| `RecordAttached` | A person record was attached to an account (`21` REC-ATT-001); names the record as `subject` and carries `account`; the record keeps its subject, so nothing the host holds under it moves (D-221) | Host |
| `ErasureRequested` | The erasure transaction has committed; the host clears the copies the destroyed key does not reach (PRIV-RIGHT-005b) | Every registered subject-event handler — **required** |
| `RestrictionChanged` | `restricted` set or lifted | Every registered subject-event handler — **required** |
| `SendingRestrictionChanged` | A named restriction was created, edited or deleted through `/admin/restrictions/{name}` (AUTH-ABUSE-004); carries the restriction name, the actor and whether the change was a loosening. | Audit, alerting (OPS-ALERT-001) |
| `SendingRestrictionGranted` | Support added credit to one key under a restriction (AUTH-ABUSE-004); carries the restriction name, the credit, the actor and the reason, never the plain key value | Audit, alerting (OPS-ALERT-001) |
| `DeviceVerified` | A new-device check completed, or a sign-in by a factor delivered to the primary email passed it (AUTH-FACT-016, D-224); carries the browser identifier and no personal data | Host (optional) |
| ~~`ExportRequested`~~ | *Retired by D-203.* What the host holds reaches an export through its export contributors when the export is assembled (`IExportContributor`, PRIV-RIGHT-003) | |
| `TakedownExecuted` | Phase one of a takedown has committed; for an account it fires with `AccountSuspended`, for a person record alone (`21` REC-TKD-001); `AccountDeletionRequested` and `RecordDeletionRequested` do **not** fire for a takedown | Host (stop its own processing for the subject, as a required subscriber) |
| `MembershipChanged` | A membership begins or ends, one bootstrap attaches included; carries `change` (`began` · `ended`, section 5.39) and `invitation`, the invitation whose acknowledgement began it, null otherwise (`01` IDN-READ-002, D-218) | Host |
| `ConsentChanged` | A consent granted, withdrawn or superseded; carries the purpose and `change` (`granted` · `withdrawn` · `superseded`, section 5.39); on `withdrawn`, handlers erase data held solely for the purpose (PRIV-CONS-008) | Every registered handler for the purpose — **required** |
| `ObjectionChanged` | An objection recorded or withdrawn for a purpose on an objectable basis (PRIV-RIGHT-001a) | Every registered handler for the purpose — **required** |
| `CredentialEnrolled` | An authenticator reached `active`, or a password was set on an existing account, by the person, by recovery or by an invitation, with the enrolment notification to every other recorded channel (AUTH-STEP-007); carries the credential identifier (absent for the password), the catalogue entry and the subject | Host (optional) |
| `CredentialSuspended` · `CredentialRestored` · `CredentialInvalidated` | A loss report started, by a report, by a removal that would lower reachable assurance, or, for a `phoneCode` credential, by the number it is bound to leaving the account (AUTH-FACT-002), was cancelled, or completed after the window (AUTH-RECOV-007); each carries the credential identifier, the catalogue entry and the subject, and `CredentialSuspended` the instant the window ends. `CredentialSuspended`, and `CredentialRestored` raised from a session, carry the acting and the effective identity of the context that raised it, as the context gives them (AUTHZ-IMP-001); `CredentialRestored` raised from the cancel link carries neither | Host (optional) |
| ~~`NotificationRequested`~~ | *Retired by D-183. A message is undertaken through the governed send (`IGovernedSend`, AUTH-ABUSE-004) and carried from the library's send outbox to `INotificationHandler` (LIB-EXT-001); an alert travels on its `AlertRaised` row (OPS-ALERT-001). No event is emitted for either.* | |
| `AlertRaised` | An OPS-ALERT-001 condition fires; carries the condition identifier and, where the alert is raised under one, its scope (section 5.23), the severity and the structured details of the row. Written in the transaction that raised it, at every raise site; `alert-dispatch` hands it to the host's alert handler after commit (OPS-ALERT-001, OPS-ALERT-003) | The host's alert handler |

Mail provisioning consumes no event: it reads the state these events announce
(INT-MAIL-006).

*Source: LIB-API-001, IDN-LIFE-003a, PRIV-RIGHT-005b, CONV-DESIGN-002, D-022, D-132,
D-141, D-146, D-162, D-166, D-168, D-177, D-183, D-187, D-197, D-218*

## 6. Status code usage

| Code | Used for |
|---|---|
| 200 | Success with a body |
| 202 | Accepted, outcome deliberately not disclosed; also `auth.credential.lastsecondfactor`, a status and not a refusal |
| 204 | Success, no body |
| 400 | Malformed request: not the shape the endpoint takes, a required member absent or empty, a free-text member outside 1 to 1024 characters after trimming, a word outside a closed vocabulary fixed in this chapter or at startup, or a route or query value that does not read as its type, `details.member` naming it (`api.request.malformed`) |
| 401 | No valid session — **session death only** |
| 403 | Authenticated, not permitted, existence not concealed; **also step-up required** (D-092). `authz.denied` means that a permission is absent, and carries only the other refusals `09` API-CONV-003 names |
| 404 | Not found: a path naming a runtime record the deployment does not hold, answered with a named code (under `/admin` nothing is concealed), **or** concealed denial; also a path no endpoint serves, or a method a served path does not take (`authz.resource.notfound`). 405 is not used |
| 409 | Conflict — duplicate, or state precondition failed |
| 422 | Well-formed, semantically rejected: a body naming something that does not exist or cannot be acted on, answered with a named code, `api.request.invalid` where none more specific exists |
| 429 | Throttled, carries `Retry-After` |
| 500 | An unhandled fault: `system.fault` with the correlation identifier and nothing else (BFF-ERR-002, D-153) |
| 502 | A dependency outside the deployment could not be reached or read: `auth.provider.unavailable`, which only a navigation's `error` carries, so no response bears this status (D-192) |

**The status belongs to the code.** Each code of section 1 SHALL answer with the one
status its row names, at every endpoint that raises it; no endpoint gives a code a
status of its own. A code a navigation route carries in `error` (BFF-ERR-001) goes with
that route's redirect, not its row's status (`08` CONV-DESIGN-006, D-192). The one
exception among answered statuses is `integration.callback.rejected`: 429 where the
callback rate limit refused the request and the refusal carries `details.retryAt`, 422
otherwise. A code whose row names a fault, and a code no row names, SHALL be answered as
`system.fault` with 500 (BFF-ERR-002). A `model.*` code is a startup or command refusal,
or the code a fault carries into the log (an unwrap under a version no longer held,
OPS-SEC-003); a conformance finding is a report of the suite (LIB-TEST-001);
`integration.mailserver.conflict` is a mail server's answer to the provisioning job
(INT-MAIL-007). None is answered by a request.

*Source: API-CONV-003, D-162, D-166, D-177, D-183, D-192*

---

## 7. Maintenance

**REF-001**: Adding an error code, permission, configuration key, audit action, message
kind or any other value this document catalogues SHALL update this document in the same
change.

*Source: LIB-API-001, CONV-NAME-003, D-162, D-166, D-177, D-183, D-187, D-204, D-206*

**Acceptance criteria**
1. A contract test fails when a code or key exists in source but is not a live row here
   (a struck row, or one whose cell after the code or key opens "Retired" or
   "Withdrawn", emphasis aside, is not live), and when a live row of section 1, or of a
   key table of section 4 (the fields of section 4.1a are not keys), names a code or key
   the source does not hold; a key written with a `<placeholder>` names a family, which
   the source holds where it holds any key of that family. The test reads this document
   alone.
2. At each release commit the release gate compares the contract lists of the previous
   release commit with its own, read from their committed contract files: configuration
   keys with type, scope and constraints, and key families; the library-owned schema;
   error codes with their status; audit actions; permissions; endpoints with method,
   route, body members and statuses; the browser profile's stage order; and the commands
   of `Janus.Cli` with their arguments, standard input and output and exit codes
   (LIB-TEST-002, D-204), rendered into `tests/Janus.Cli.Tests/commands.txt` (D-206). A
   removed or changed entry without a major version, or an added entry with only a patch
   version, fails the gate, and so does a catalogue whose declarations and read entries
   do not number the same.
3. A contract test fails when a code a request can be answered with has no status in
   section 1.
4. A contract test fails when a permission the library declares is not a row of
   section 2.1.
