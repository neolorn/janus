# 18 — Frontend Integration

How the Angular applications connect to the BFF, and the constraints they must
satisfy.

**Prerequisite:** `17-bff.md`, `09-api-contract.md`.

**Scope — read this first.** This document specifies **wiring and constraints**. It
does not specify UI or UX, which are independent and downstream: shaped by these
constraints, never dictated by them.

The test for whether something belongs here: *would getting it wrong break security,
compliance, or correctness?* If the answer is "it would look bad," it belongs in
design, not here.

---

## 1. Platform

**FE-PLAT-001** — Angular 22 or later, **zoneless**, standalone components,
signals for state.

*Source: D-062*

Zoneless change detection reached **stability in v20.2** and became the **default for
new projects in v21** — Zone.js is no longer included by default. Verified 2026-08-29. Signals own component and
application state; RxJS remains for time-based async orchestration. The working
division: **RxJS to fetch and transform, signals to store and display.**

**Acceptance criteria**
1. No `NgModule` in application code.
2. Zone.js is absent from polyfills and no zone-based change-detection provider is
   registered — zoneless is the v21+ default and is not re-declared (D-135).
3. `inject()` is used rather than constructor injection in new code.

---

**FE-PLAT-002** — Session and capability state SHALL live in **signals in a
root-provided service**. No global store library.

*Source: D-062*

That state is read-mostly and small — who the person is, what assurance they reached,
what they may do here. A store adds ceremony without solving anything present.

**Acceptance criteria**
1. Session state is reachable without prop-drilling and without a store dependency.

---

**FE-PLAT-003** — Testing is **Vitest** for unit and **Playwright** for end-to-end.

End-to-end coverage is required for the flows that cannot be verified any other way:
registration through account creation, step-up, recovery, consent capture, and
locale switching.

**Acceptance criteria**
1. Each listed flow has an end-to-end test.
2. Unit tests run without a browser.

---

## 2. Talking to the BFF

**FE-API-001** — The frontend SHALL hold no credential of any kind. Session identity
travels only in the cookie the BFF sets.

*Source: AUTH-SESS-003, BFF-SESS-001*

**Acceptance criteria**
1. No token, claim, permission or role name is stored in `localStorage`,
   `sessionStorage`, a variable, or a non-`httpOnly` cookie.
2. No request attaches an authorization header.

---

**FE-API-002** — Every state-changing request SHALL carry the CSRF token and the
required custom header.

*Source: BFF-CSRF-001, BFF-CSRF-003*

Both are applied by a single HTTP interceptor. **No request builds them by hand** —
one place, so no call site can omit them.

**Acceptance criteria**
1. A state-changing request issued without going through the interceptor fails.
2. The token is refreshed when the session rotates.

---

**FE-API-003** — The frontend SHALL render errors from **codes**, never from server
prose.

*Source: LIB-API-003, D-054*

Every code in `10-reference.md` has a message in every configured locale. A code with
no message is a build failure, not a runtime surprise.

**Acceptance criteria**
1. No error is displayed by echoing a server string.
2. A missing message for any known code fails the build.
3. An unrecognised code renders a generic message and is reported.

---

**FE-API-004** — On receiving the step-up code **or `auth.session.expired`**, the
frontend SHALL reauthenticate **in place** and **retry the original request**, without
requiring the person to re-enter what they had already provided. An expired session
SHALL NOT redirect the person to a sign-in page.

*Source: BFF-STEP-001, AUTH-STEP-001, AUTH-SESS-005, D-123*

The BFF rejects rather than redirecting mid-request. Recovering is the frontend's
job. For an expiry, `details.reauthenticate` is `single-factor` or `full`, and the
frontend acts on the value as sent (AUTH-SESS-005 AC3a, BFF-STEP-001, D-139):
`single-factor` is returned only for an idle expiry inside the absolute window of a
principal whose policy requires `aal2` (staff), and one eligible factor (passkey or
password) restores the session; every other expiry, including an idle expiry under
the system policy (every customer session), returns `full` and needs full
authentication. The frontend never infers the value from the kind of expiry. Either
way the interrupted request completes afterwards with its data intact.

*Source: D-148; D-147; AUTH-SESS-005, BFF-STEP-001, D-139*

**The prompt SHALL offer every combination that reaches the gate**: exactly the
`details.options` the step-up response lists (`09` `/auth/step-up`): for a customer
who can reach AAL2, password + TOTP, password + recovery code, password + security
key, or a passkey, the subject choosing among them; for a password-only customer, the
password. A combination that does not reach the gate is never shown, so a bare
password is never offered on an account that reaches AAL2 (AUTH-STEP-002). The
prompt has **two non-dead-end outcomes** beside presenting a factor: `enrol` (the
frontend initiates enrolment in place, and the original request completes afterwards);
`report-loss` (the frontend offers to report the missing factor lost,
`POST /recovery/report-loss`, explaining that the action becomes available when the
window completes). A `pending` outcome shows the completion time and the cancel
option; it is a status, not an error.

*Source: D-148; AUTH-STEP-002, AUTH-RECOV-007, D-086, D-128, D-141*

**Acceptance criteria**
1. A form submission interrupted by step-up **or by session expiry** completes after
   re-authentication with its data intact.
1a. A staff member returning after an idle expiry restores the session with a single
   passkey tap and lands on the page they left.
2. The prompt states what is required in terms of assurance, never a factor name;
   the options it lists are the server's `details.options`, unfiltered and unextended.
3. A subject who can reach AAL2 chooses among every combination that reaches it; a
   subject holding password and TOTP is shown password + TOTP, never the password
   alone.
4. A subject with no usable combination is offered enrolment or a loss report, and
   the original request completes afterwards where the gate can still be met.
5. Social sign-in never appears as a step-up option.
6. A pending loss report is shown with its completion time and a cancel option, not
   as an error.

---

**FE-API-005** — Throttling responses SHALL be surfaced with their retry interval,
and the frontend SHALL NOT retry automatically before it elapses. A send refused by a
restriction (`auth.restriction.exceeded`, AUTH-ABUSE-004) SHALL be rendered with its
`retryAt` and the route to support, and the rendering SHALL be identical whether or
not the address is registered.

*Source: AUTH-ABUSE-002, BFF-ABUSE-001, D-146*

The wording is the frontend's (CONV-CONTENT-001), with one constraint: the refusal for
a registered and for an unregistered address must read the same, because a difference
would make the restriction an enumeration oracle (AUTH-ABUSE-002, one of the three
exceptions to the content rule).

**Acceptance criteria**
1. The interval is shown.
2. No automatic retry occurs within it.
3. A restriction refusal shows `retryAt` and a way to reach support; the screen for a
   registered and for an unregistered address is identical in text and layout.

---

## 3. Capabilities

**FE-CAP-001** — Controls SHALL render from the **capability array on the record**.
The frontend SHALL NOT contain a role name, and SHALL NOT infer permission from one.

*Source: AUTHZ-GATE-005, API-CAP-001*

This is the requirement that stops a button appearing while the endpoint refuses.

**Acceptance criteria**
1. A search of frontend source finds no role name.
2. Removing a capability from a response removes the corresponding control with no
   frontend change.
3. Every control that triggers a state change is gated by a capability.

---

**FE-CAP-002** — Absence of a capability SHALL hide or disable the control. A
capability carrying `requires` SHALL be **shown and prompt** for what is needed.

*Source: AUTHZ-GATE-005, D-078*

Not permitted → hidden. Permitted but gated → shown, and selecting it triggers the
step-up or consent flow. This resolves the previous conflict between "never show a
control that will fail" and "reject with a step-up code and let the frontend retry":
a gated action is not a failure, it is a prompt.

**Acceptance criteria**
1. No control is presented whose action the capability array does not permit.
2. A control requiring step-up is visible and initiates step-up when selected.
3. A control requiring consent is visible and initiates the consent flow.

---

**FE-CAP-003** — Capability-driven rendering SHALL NOT be treated as a security
control. It is presentation. Authorization is enforced server-side.

*Source: AUTHZ-PRIN-003*

Stated so nobody later reasons that a hidden button makes an endpoint safe.

---

## 4. Localization

**FE-LOC-001** — All user-facing text SHALL render through the localization library.
No string SHALL be hardcoded in a template or component.

*Source: D-031, D-054*

**Acceptance criteria**
1. The pseudo-locale's markers reveal no untransformed strings.
2. Build-time completeness checking passes for every configured locale.

---

**FE-LOC-002** — Layout SHALL use **CSS logical properties** throughout. Physical
properties SHALL NOT be used for anything direction-dependent.

`margin-inline-start`, not `margin-left`. `padding-inline`, not `padding-left` and
`padding-right`. `text-align: start`, not `left`.

*Source: D-031*

The localization library supplies `dir` and correct bidi; **it does not mirror
layout.** Physical properties are what silently break RTL, and they are invisible
until someone switches locale.

**Acceptance criteria**
1. A lint rule bans physical direction-dependent properties and **runs from the first
   commit**.
2. The rule cannot be suppressed without an explicit annotation.

---

**FE-LOC-003** — Localization readiness SHALL be verified with the **pseudo-locales**,
testing both **contraction and expansion**.

*Source: D-031*

Expansion finds fixed-width containers, truncation and overflow. **Contraction finds
the opposite failures** — text no longer filling its container, alignment that looked
deliberate at English length and looks broken shorter, buttons sized to copy that is
no longer there. Arabic against English typically contracts, so the contracted
pseudo-locale is the one that matters here.

The lint rule detects physical properties; the pseudo-locale **exposes** layouts that
follow `dir` correctly and still look wrong. They are complementary, not alternatives.

**Acceptance criteria**
1. Both pseudo-locales are exercised before a screen is considered complete.
2. Neither is reachable in a production build.

---

**FE-LOC-004** — Values arriving canonicalised from the localization library SHALL
NOT be re-parsed by validators.

*Source: D-031*

An Arabic-Indic numeral already converted to a canonical value must not then be
examined by a validator expecting Latin digits.

**Acceptance criteria**
1. A form field accepting Arabic-Indic numerals validates and submits successfully.

---

**FE-LOC-005** — Locale switching SHALL preserve application state and history.

*Source: D-031*

The localization library switches in place without a reload, rewrites URLs including
translated path segments, and preserves history state. The application must not
defeat that by re-initialising on locale change.

**Acceptance criteria**
1. Switching locale mid-form preserves entered data.
2. Browser back after a switch restores the previous position.

---

## 5. Compliance screens

The screens carrying legal weight. Constraints here are not stylistic.

**FE-COMP-001** — Every legal document (terms of service, the privacy notice, consent
texts) SHALL be readable in its **governing language** and in **any attached
translation inside the document view**, without changing the interface language and
without navigating away. The document view SHALL state which text is the governing one;
where a translation and the governing text diverge, the governing text governs.

*Source: PRIV-CONS-005, D-031, D-146*

Each document version carries exactly one governing language, supplied with the
compliance-text response (FE-COMP-002). For the default deployment that language is
Arabic; a host in another jurisdiction declares its own, so the frontend reads it from
the data and never assumes it. How the reader moves between the governing text and a
translation is the frontend's choice; the localization library's per-region locale
capability is one mechanism, since a document region can render in one language while
the interface stays in another.

**Acceptance criteria**
1. From the screen that shows a document, the governing text and each attached
   translation are reachable while the interface language stays as it was.
2. The governing text is identified as such on the screen, from the response's
   governing-language field, not from a constant.
3. A version with no translation attached renders in its governing language alone.

---

**FE-COMP-002** — Compliance text SHALL come from the API as **versioned data**, never
through the localization library. The compliance-texts response carries, per version,
the **governing language** and the languages of the attached translations
(PRIV-CONS-005), and the frontend SHALL render from those fields.

*Source: PRIV-CONS-006, PRIV-CONS-005, D-031, D-146*

The requested locale determines which text is shown first, where a translation in it
exists; it never removes the governing text from reach (FE-COMP-001).

Because this text is API data rather than localization messages, the integrity of a
consent record does not depend on the localization library at all.

**Acceptance criteria**
1. No compliance text exists as a localization message.
2. The version identifier is submitted with the consent, never the rendered text.
3. The governing language shown for a version is the one the response carries.

---

**FE-COMP-003** — The privacy notice SHALL be **presented, not accepted**. No
acceptance control SHALL appear for it.

*Source: PRIV-CONS-008a, D-056*

Terms of service **are** accepted. The notice is shown.

**Acceptance criteria**
1. No checkbox, button or control implies agreement to the privacy notice.
2. The presentation is recorded with its version.

---

**FE-COMP-004** — Consent controls SHALL be **unticked by default**, one control per
purpose, and SHALL NOT be bundled.

*Source: PRIV-CONS-002, PRIV-CONS-003*

**Acceptance criteria**
1. No consent control is pre-checked.
2. No single control grants consent for more than one purpose.
3. Optional consents are visually separable from required actions.

---

**FE-COMP-005** — Withdrawing consent SHALL take **no more interactions than granting
it**, with no interstitial attempting to dissuade.

*Source: PRIV-CONS-008*

**Acceptance criteria**
1. Interaction count for withdrawal does not exceed that for granting.
2. No retention or confirmation flow intervenes.

---

**FE-COMP-006** — The **privacy dashboard** SHALL provide self-service access,
portability, rectification, objection, and consent management.

*Source: PRIV-CONS-011, PRIV-RIGHT-001*

**Acceptance criteria**
1. Every consent held is visible to its subject and withdrawable there.
1a. Every purpose on an objectable basis shows an objection switch labelled as such,
   calling `POST` / `DELETE /privacy/objections/{purpose}` (PRIV-RIGHT-001a); no
   purpose is assumed to exist or not exist.
1b. No cookie consent prompt is shown while only strictly necessary cookies are set;
   the notice lists them (PRIV-CONS-006a).
2. Export is available in both formats without contacting support.
3. The readable export is **rendered by the frontend from structured data** — the
   server produces no document (D-058).

---

## 6. Registration

**FE-REG-001** — The originating application SHALL send its **client identifier** when
starting registration. No return destination SHALL be sent at any step.

*Source: API-REDIR-002, D-057*

**Acceptance criteria**
1. The link to registration carries the identifier from the application's own
   configuration.
2. No registration step accepts or forwards a destination parameter.

---

**FE-REG-002** — Password strength feedback SHALL be **advisory**. The frontend SHALL
NOT block submission on a heuristic score.

*Source: AUTH-PASS-005*

Rejection happens server-side, for blocklist hits and floor violations only.

**Acceptance criteria**
1. A password meeting the floor and absent from the blocklist submits, with a warning
   at most.

---

**FE-REG-003** — The security screen SHALL present **both floors and the trade between
them** before the person types: fifteen characters alone, or ten with a second step
enrolled on the same screen. Choosing a password below fifteen SHALL make the second
step **mandatory in place**, and lengthening the password on the same screen SHALL lift
that requirement in place. There is no going back: password and second step are one
screen (REG-SESS-006).

*Source: AUTH-PASS-001a, REG-SESS-006, D-114, D-146*

The account is not created until every requirement is met, so nothing provisional
exists: a short password without a second step is simply an incomplete registration.
The requirement appears and disappears as the password field changes, without leaving
the screen.

**Acceptance criteria**
1. The trade is stated on the security screen before the password is typed.
2. With a password below fifteen characters, the screen does not complete without a
   second step.
3. Lengthening the password to fifteen on the same screen removes the second-step
   requirement without leaving the screen.
4. Enrolling a second step after setting a password never invalidates it.

---

**FE-REG-004** — Registration SHALL allow correcting a mistyped phone number or email
through its Change control without being blocked by the sending restrictions.

*Source: AUTH-ABUSE-004, REG-SESS-004, D-146*

The restriction counts per destination. A corrected identifier is a different
destination and receives a fresh send, within the source restriction that still
applies to the browser.

**Acceptance criteria**
1. Correcting the number after a failed delivery permits a new send.
2. Repeated sends to the **same** destination remain restricted, and the refusal is
   rendered per FE-API-005.

---

**FE-REG-005** — The registration wizard SHALL present the **ten steps of REG-SESS-002
in order**: age, email, phone, confirm, security, terms and consents, about you,
preferences, membership, done. It SHALL offer **no Back navigation** and no control that
returns to a completed step. Corrections SHALL be made in place: a **Change** control on
each unlocked identifier (REG-IDENT-010), none on a locked one. The confirm screen SHALL
show every identifier with its verification state and SHALL allow adding further emails
and phones within `identifiers.email.max` and `identifiers.phone.max` (REG-SESS-004).
The security screen SHALL follow REG-SESS-006 and FE-REG-003. The final step SHALL show
a summary of what was set up and what was left for later and SHALL return the person per
REG-SESS-008. The wizard SHALL read the registration session's state from the server
(`GET /register`, `GET /register/events`) and SHALL NEVER hold it only in the browser.

*Source: D-146; REG-SESS-002, REG-SESS-004, REG-SESS-006, REG-SESS-008, REG-IDENT-010*

The first six steps run on the authentication application against the registration
session; the account exists from the end of step 6; steps 7 to 10 run on the account
application in the new session. A wizard that kept its own copy of the state would
disagree with the server the moment a link verified an identifier in another tab, or a
session expired; the server's state is the only state. The age screen is neutral and
precedes every identifier field (REG-PROF-002).

**Acceptance criteria**
1. The steps appear in the order of REG-SESS-002; no step is reachable before its
   predecessor completes and no control returns to a completed step.
2. A locked identifier shows no Change; an unlocked one does, and using it resets that
   identifier's verification.
3. The confirm screen lists every staged identifier with its state and completes only
   when all are verified; an extra identifier can be added up to the maxima and an
   unverified extra removed.
4. Reloading the browser at any step restores the same step and state from the server;
   no registration state survives in browser storage.
5. The final step shows the summary and lands on the client's registered address with
   an established session; no destination parameter is sent (FE-REG-001).

---

## 6a. Break-glass

**FE-BG-001** — The authentication application SHALL provide a route `/break-glass`:
one field for the sealed credential, one button, no other controls. On success it
SHALL land the person on the management application. Its address is what the sealed
envelope names.

*Source: OPS-BOOT-002, D-129*

The owner is not technical. The endpoint behind this page (`POST /auth/break-glass`)
is not a procedure a non-technical person can follow; this page is.

**Acceptance criteria**
1. Pasting a valid credential and pressing the button yields a usable session and
   lands on the management application without further input.
2. A consumed or invalid credential renders a localized message from its code.
3. The page works with a stale session cookie present for the domain.

---

## 6b. Link landings

**FE-VER-001** — One landing component SHALL serve every verification and sign-in
link (identifier verification at registration, identifier add and replace on an
account, and sign-in links), landing on a frontend route (API-LAND-001), and SHALL
behave by **where it is opened**. In the **originating browser** (the one holding the
session cookie that sent the link) it SHALL show a control whose **press** verifies
(`POST /register/verify/{id}`, `POST /account/identifiers/{id}/verify`) or signs in
(`POST /auth/factor`); the waiting screen in that browser SHALL advance by itself on the
session's state, read from `GET /register/events` with `GET /register` polling as the
fallback. **Anywhere else** it SHALL show the **code to type** where the flow is
waiting and a control that ends the attempt: `POST /register/abandon` for a
registration link, `POST /auth/link/abandon` for a sign-in link, and
`POST /account/identifiers/{id}/abandon` for an identifier add or replace link, each
link-borne with the token from the message (D-148). The landing SHALL NEVER
verify or sign in **on load**.

*Source: D-148; D-146; REG-SESS-003, AUTH-FACT-003, API-LAND-001, BFF-CSRF-005b*

The server decides which case applies: the landing call without `press` returns
`sameBrowser` and, where false, the code (`09` `POST /register/verify/{id}`). The
frontend renders the two outcomes and never guesses from the user agent. Acting on load
would be completed by a mail scanner's prefetch, and would make the registration
pre-hijack of `15` section 3.8 work. *Illustrative*: in the originating browser, a page
with one control that confirms the address; elsewhere, the code in large type, a line
saying where to type it, and a control to end the attempt.

**Acceptance criteria**
1. Loading the landing route issues no state-changing request; a press does.
2. In the originating browser, a press verifies and the waiting screen advances without
   a reload; with the event stream blocked, polling advances it.
3. In another browser, the page shows the code and the abandon control, and pressing
   the control ends the attempt (the registration session, the pending sign-in link or
   the staged identifier); nothing is verified from that browser.
4. The same component, with the same behaviour, serves identifier add, replace and
   sign-in links, calling the abandon operation that matches the link's kind.
5. No token from a link appears in a URL the frontend constructs for the event stream or
   for polling.

---

## 6c. Password managers and passkeys

The rules that make managers and passkeys work first time. The server-side facts are
REG-PM-001; these are the frontend's.

**FE-PM-001** — Every authentication step SHALL be **one form** with labelled inputs
carrying the WHATWG autofill tokens: `username` on the identifier field, which is
the token an email field carries where the email is the identifier (`email` goes only
on an email field that is not the identifier), `new-password` on both fields at sign-up
and change, `current-password` at sign-in and for the old password at change,
`one-time-code` for verification and TOTP codes, `email` and `tel` on the matching
fields that are not the identifier.

*Source: D-148; D-146; REG-PM-001*

**Acceptance criteria**
1. Each step's inputs are inside one form element with an associated label each.
2. Every field listed carries the named token and no other autofill token, except as
   FE-PM-004 states for the sign-in identifier field (`username webauthn`).
3. An email field that is the identifier carries `username`, not `email`; an email
   field that is not the identifier carries `email`.

---

**FE-PM-002** — Password change and reset forms SHALL carry a **hidden username field**
holding the primary identifier. Fields SHALL NEVER be injected into or removed from a
form dynamically; a field that is not needed is hidden and reused.

*Source: D-146*

A manager pairs the new password with an account through the username field; without
it the saved entry has no identifier. A form whose fields appear after load is one a
manager fills before they exist.

**Acceptance criteria**
1. The change and reset forms contain a username field, hidden, populated with the
   primary identifier.
2. The set of form fields is identical before and after any interaction on the step.

---

**FE-PM-003** — Submission SHALL be a **real form submit**, so the browser's offer to
save the credential fires. Paste SHALL NEVER be blocked on any field. Nothing on the
client SHALL reject a password for any reason other than the floor and the blocklist
the server enforces (AUTH-PASS-001, AUTH-PASS-004).

*Source: D-146; AUTH-PASS-002, AUTH-PASS-005*

**Acceptance criteria**
1. Completing the security step in a browser with a password manager produces the
   manager's save offer.
2. A password pasted into either field submits; a manager-generated password of 128
   characters meeting the floor and absent from the blocklist is accepted.

---

**FE-PM-004** — The sign-in identifier field SHALL carry
`autocomplete="username webauthn"`, and the sign-in page SHALL issue a WebAuthn
request with **conditional mediation** and **no `allowCredentials`**, so a manager
can offer a passkey inside the field.

*Source: D-146; AUTH-FACT-002 (conditional UI)*

**Acceptance criteria**
1. The identifier field carries both tokens.
2. The conditional request is issued on page load without a prompt of its own and
   carries an empty or absent `allowCredentials`.
3. Selecting the offered passkey signs in without the identifier being typed.

---

**FE-PM-005** — The routes named by `/.well-known/change-password` and
`/.well-known/passkey-endpoints` (REG-PM-001) SHALL exist in the account application
and be linked from it: the password page, the passkey enrolment page and the credential
list. Sign-up and sign-in SHALL be served from the **same origin** (the authentication
application) and SHALL use **distinct field names** from each other. The frontend SHALL
NOT emit an Apple `passwordrules` attribute.

*Source: D-146; REG-PM-001, AUTH-PASS-002*

Janus has no composition rules (AUTH-PASS-002), so there is no rule to declare; an
attribute that stated one would make managers generate to a rule that does not exist.
The same origin is what lets a saved entry from sign-up match at sign-in; the distinct
names are what stop the manager filling the sign-up form with the sign-in entry.

**Acceptance criteria**
1. Following `/.well-known/change-password` lands on a working password page;
   `enroll` and `manage` from `/.well-known/passkey-endpoints` land on the enrolment
   page and the credential list.
2. Sign-up and sign-in pages share an origin, and no field name or id is shared between
   them.
3. No `passwordrules` attribute appears in any template.

---

**FE-PM-006** — TOTP enrolment SHALL show an **`otpauth://` QR code** and the
**Base32 secret as text** from the `begin` response (`09`
`POST /account/factors/totp/begin`), with the parameters of AUTH-FACT-005 (30-second
steps, 6 digits), and SHALL require one code before activation (AUTH-FACT-007).

*Source: D-146; AUTH-FACT-005, AUTH-FACT-007*

**Acceptance criteria**
1. The QR encodes the `otpauth` URI the server returned; the secret is shown as text
   beside it and can be copied.
2. The enrolment does not activate until a code is confirmed.

---

## 6d. Security step

**FE-SEC-001** — The **passkey** dialog SHALL request `residentKey: "required"` and
`userVerification: "required"`; the **security-key** (second-factor) dialog under
two-step SHALL request `residentKey: "discouraged"`. Every WebAuthn and TOTP enrolment
SHALL prompt for a **label**, defaulting to the client's description of the device
(AUTH-FACT-001). A second-factor security key SHALL offer an **upgrade to a passkey**
(`POST /account/credentials/{id}/upgrade`). The two-step controls SHALL be **disabled
until a password exists** on the account. `phoneCode` SHALL be shown with its
**less-secure flag** wherever it is listed. **Recovery codes** SHALL be shown **once**
with copy, download and print, and a **confirm-saved** control SHALL gate continuation
(AUTH-RECOV-006).

*Source: D-146; AUTH-FACT-002b, AUTH-FACT-001, AUTH-RECOV-006, AUTH-FACT-008*

The two dialogs are the one place the frontend chooses between a credential that signs
in and one that is a second factor; the server cannot make that choice for it. Where
the passkey is held, on a device or on a hardware key, is the person's choice inside
the passkey dialog and the frontend does not pre-empt it.

**Acceptance criteria**
1. The creation options for a passkey carry `residentKey: "required"` and
   `userVerification: "required"`; for a second-factor security key,
   `residentKey: "discouraged"`.
2. Every WebAuthn and TOTP enrolment shows a label field pre-filled with the device
   description and submits it.
3. A second-factor security key in the credential list offers the upgrade; on success
   the passkey is listed and the second-factor entry is gone.
4. With no password on the account, TOTP, security-key and `phoneCode` enrolment
   controls are disabled and say why.
5. `phoneCode` carries the less-secure marker in the enrolment list, the credential
   list and the challenge.
6. Recovery codes are shown once with copy, download and print; the flow does not
   continue until the confirm-saved control is used; the codes are not shown again
   except by regeneration.

---

## 6e. Account

**FE-ACCT-001** — The account application SHALL render from `GET /account` and the
account endpoints: a **credential list** showing per credential its kind, label
(editable, `PATCH /account/credentials/{id}`), **synced** or **this device only** from
`backupState`, added and last-used times; a **sessions list** (`GET /account/sessions`)
with the current session marked, city-level location, device description, sign-in and
last-use times and a per-session revoke (`DELETE /account/sessions/{id}`); an
**identifiers list** showing per identifier whether it is primary, verified and
locked, with add, change primary, backup setting and remove, and the removal undo
reached from the link in the notice; **preferences** rendered from the host's
declaration (REG-PREF-001); and the **profile** fields the policy enables
(REG-PROF-001).

*Source: D-146; AUTH-FACT-001, AUTH-SESS-013, REG-IDENT-002, REG-IDENT-004 to
REG-IDENT-006, REG-PREF-001, REG-PROF-001*

Nothing about a credential beyond these fields is shown (AUTH-FACT-001). The undo is
link-borne (`POST /account/identifiers/{id}/undo`) because after a hostile removal the
account has no session of its own that could reach it; the account application renders
the landing for that link. Preferences are typed from the declaration: a `boolean`
renders a switch, an `enum` its values, and an administrator-only key is read-only for
the person.

**Acceptance criteria**
1. Each credential shows kind, label, synced or this device only, added and last used;
   editing the label calls `PATCH /account/credentials/{id}` and changes nothing else.
2. Exactly one session is marked current; each shows a location no finer than city;
   revoking another session removes it from the list and leaves the current one.
3. Each identifier shows primary, verified and locked state; adding, setting primary,
   changing the backup setting and removing call their endpoints; a removed identifier
   disappears at once and the undo landing restores it within
   `identifier.change.coolingoff`.
4. A preference key absent from the declaration is never rendered; an undeclared key is
   never sent.
5. Legal name and date of birth appear only where their policies are on; date of birth
   is read-only.

---

## 7. Addresses

Postal and delivery addresses are host data (IDN-ATTR-005); the address flow, its
location control and its district handling are the host's screens (D-061 records the
first host's design, handed to the host). Two library constraints reach them: no
coordinate is stored for a person (IDN-ATTR-006), and location is never requested
during registration or on page load of any library screen.

---

## 8. Accessibility

**FE-A11Y-001** — Compliance screens SHALL meet WCAG 2.2 AA.

*Source: D-062*

Consent that a person cannot perceive or operate is not informed consent. The rest of
the application should meet the same bar; on these screens it is a requirement.

**Acceptance criteria**
1. Consent and notice screens pass automated accessibility checks.
2. Every consent control is keyboard-reachable and correctly labelled.
3. Contrast holds in both languages and both directions.

---

## 9. What is deliberately not here

| Area | Owner |
|---|---|
| Visual design, layout, spacing, colour | Design, downstream of these constraints |
| Component library choice | Frontend, unconstrained |
| Copy and tone | Design and legal review |
| Navigation structure | Product |
| Business screens beyond identity and privacy, including addresses | The host application |

Constraints shape these. They do not specify them.

**Wording is a frontend deliverable** (CONV-CONTENT-001). Every screen's text is written
here, in each language natively, and reviewed for UX; the specifications state what a
message or screen must achieve and never its words. Where a specification quotes a
sentence it is an example marked *illustrative*, not a conformance target. The three
exceptions, where the words are the requirement, are the identical-regardless responses
of AUTH-ABUSE-002 and AUTH-ABUSE-003 (FE-API-005), legal texts (FE-COMP-001,
FE-COMP-002) and never-disclose rules.

---

## 10. Open items

None.
