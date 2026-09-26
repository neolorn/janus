# 09 — API Contract

Endpoints, request and response shapes, status codes, and headers.

**Prerequisite:** `00-overview.md`, section 1. Behaviour is documents 01 to 04 and
20; this defines the HTTP surface that exposes it.

**Scope.** The endpoints the library ships and the host mounts. Host-owned business
endpoints are out of scope; they consume the authorization gate directly rather than
through HTTP.

The method, path, request and response members, statuses and error codes of every
endpoint below are part of the public contract (LIB-API-001). They are held in a
committed contract file generated from the endpoint data source, which a contract test
checks and the release gate judges (REF-001 AC2, D-166).

---

## 1. Conventions

**API-CONV-001** — All endpoints SHALL be mounted under a host-configured prefix, except
the three site-root documents of section 4 (`/.well-known/webauthn`,
`/.well-known/change-password`, `/.well-known/passkey-endpoints`), which are mounted at
the site root (LIB-HOST-003). No absolute path SHALL be hardcoded, including inside the
discovery document.

*Source: LIB-HOST-003, D-166*

Paths below are shown relative to that prefix.

**Acceptance criteria**
1. Mounting under a non-default prefix requires no code change.
2. The discovery document reflects the configured prefix.

---

**API-CONV-002** — Errors SHALL return a machine-readable code with structured data,
never rendered prose.

```json
{
  "code": "auth.session.expired",
  "correlationId": "...",
  "details": { }
}
```

*Source: LIB-API-003, CONV-NAME-003, D-166*

The host renders the message in the user's language from the code. `details` carries
structured context: never a sentence. Every free-text request field (`reason`, `detail`,
`channelUsed`, `note`) is 1 to 1024 characters after trimming, one rule (D-153), at every
endpoint. A free-text member that is blank or longer is refused **400**
`api.request.malformed` naming the member; a reason the operation requires, absent or
blank, is refused with that reason's own code where `10` names one
(`config.change.reasonrequired`, `auth.recovery.reasonrequired`,
`authz.grant.reasonrequired`) (D-166). Where
an endpoint below describes a response in prose and names no field, the field is the
camelCase of the noun in the vocabulary this chapter already uses: `id`, `label`,
`createdAt`, `lastUsedAt`, `expiresAt`, `signedInAt`, `device`, `location`, `current`
(D-153). A refused send (`auth.restriction.exceeded`,
AUTH-ABUSE-004) carries `retryAt` in `details`: the earliest time a bucket lifts, and
the same value whether or not the address is registered (AUTH-ABUSE-002). A throttled
refusal (`auth.throttled`) carries `retryAt` in `details` the same way, the instant the
next attempt is looked at, built by one builder wherever it is answered (D-166).

**Acceptance criteria**
1. No error body contains a user-facing sentence.
2. Every error carries a correlation identifier resolving to audit and logs.
3. A free-text member that is blank, or longer than 1024 characters after trimming, is
   refused 400 `api.request.malformed` naming it, or, where it is a required reason that
   is blank, with that reason's code.

---

**API-CONV-003** — Status codes SHALL be used as follows.

| Code | Meaning here |
|---|---|
| 200 | Success with a body |
| 204 | Success, no body |
| 400 | Malformed request: the body is not the shape the endpoint takes, a required member is absent or empty, a free-text member is outside the bound of API-CONV-002, or a word lies outside a closed vocabulary fixed in `10` or at startup (a configuration key the route does not serve, a takedown trigger, an undeclared permission, a role name's form). Carries `api.request.malformed` with `details.member` naming the member and nothing of its value; where the body failed before any member, the code alone |
| 401 | No valid session: **session death only** |
| 403 | Authenticated, not permitted, **and existence is not concealed**. `authz.denied` means only that a permission is absent (section 8 states when an identifier naming no row is answered so); the reserved account's refusals of OPS-BOOT-002 are answered with it too. Also carries `auth.stepup.required` (step-up is a 403, never a 401), `authz.restricted` and `auth.session.csrfinvalid` |
| 404 | Not found: a path naming a runtime record the deployment does not hold, answered with a named code (under `/admin` nothing is concealed; section 8 states the one case answered as a missing permission instead); **or concealed denial** (`authz.resource.notfound`); also a path under the prefix that no endpoint serves, and a method a served path does not take. 405 is not used and no `Allow` header is sent |
| 409 | A state precondition failed, a duplicate identifier included; answered with a named code |
| 422 | Well-formed but refused on meaning: a body referring to something that does not exist or cannot be acted on, a blocklisted password, a mixed-script identifier. Answered with a named code, and with `api.request.invalid` (`details.member`) where `10` names none more specific |
| 429 | Throttled, or refused by a rate limit or a sending restriction (`auth.throttled`, `auth.restriction.exceeded`, and `integration.callback.rejected` for the callback rate limit only); carries `Retry-After` and `details.retryAt` |

*Source: D-016, AUTHZ-CONCEAL-001, D-162, D-166*

**Acceptance criteria**
1. A concealed denial is byte-identical and timing-identical to a genuine 404.
2. 403 is used only for failures not tied to a specific record.
3. A path naming a runtime record the deployment does not hold answers 404 with a named
   code, except the case section 8 answers as a missing permission; a well-formed body
   naming one answers 422 with a named code; neither answers `api.request.malformed`.
4. No endpoint answers 405; a method a served path does not take answers 404
   `authz.resource.notfound`.

---

**API-CONV-004** — State-changing requests SHALL require a CSRF token. Session
identity SHALL be carried only by the session cookie.

*Source: AUTH-SESS-003, AUTH-SESS-007*

**Acceptance criteria**
1. A state-changing request without a valid token returns 403 and is logged.
2. No endpoint accepts a session identifier in a header, query string, or body.

---

**API-CONV-005** — Endpoints that could reveal account existence SHALL return
identical responses and identical timing regardless of existence.

*Source: AUTH-ABUSE-003, D-166*

Applies to: every registration endpoint (`/register/*`, REG-SESS-005), sign-in
initiation, recovery initiation, sign-in link request (`POST /auth/link`), email OTP
request, and adding an identifier to an account (`POST /account/identifiers`). No
registration endpoint discloses whether an identifier exists, except that two
identifiers' existence is disclosed: a username at choice time (REG-IDENT-009), and the
email an invitation binds, to the holder of the invitation's link alone, which was sent
to that email (`POST /register` with `invitationToken`, REG-INV-001).

**Acceptance criteria**
1. Responses for existing and non-existent identifiers are byte-identical.
2. Timing distributions overlap within noise: verified by construction (one code path,
   fixed-time comparison, identical bytes), asserted by criterion 1, and named in the
   report as verified by construction (CONV-TEST-007, D-153).

---

## 2. Registration

### `POST /register`

Creates a **registration session** (REG-SESS-001): an opaque server-side record bound
to the requesting browser through the pre-authentication cookie (BFF-CSRF-005a),
living `registration.session.lifetime`, that stages everything the steps collect and
**reserves nothing**. No account exists until the terms step.

```json
{ "clientId": "...", "invitationToken": "..." }   // originating application, per API-REDIR-002; invitationToken optional, the token of an invitation link (REG-INV-001)
```

**201**: session created; the cookie is set; body is the state document of
`GET /register` at its first step
**409**: `identity.registration.signedin`
**422**: `identity.invitation.expired`, a token that opens no invitation;
`identity.invitation.identifiermismatch`, an account already holds the email the
invitation binds
**429**: throttled

*Source: D-146; REG-SESS-001, REG-SESS-002, API-REDIR-002, D-162, D-166*

A signed-in person is not offered registration; a request that nonetheless arrives
with a live session creates no registration session and is refused **409**
`identity.registration.signedin`; the frontend navigates to the account application, and
no account document crosses a registration route (REG-SESS-002). Where such a request
carries `invitationToken`, the invitation the token opens is first attached to the
signed-in account (REG-INV-002 AC1) and the answer is the same 409; the landing page
presents the token again after a sign-in. Beginning a registration with the token is
the invitation link's press and spends it (REG-INV-001, IDN-LIFE-009a); where an account
already holds the bound email, the press is refused and the invitation stays unspent.
The client identifier is
validated at capture and stored on the session; no destination parameter is accepted
here or at any later step (API-REDIR-002).

Every `/register/*` endpoint below requires the registration session's cookie; a
request from a browser without it is refused (REG-SESS-001 AC2). Every response is
identical whether or not an identifier presented already belongs to an account
(API-CONV-005, REG-SESS-005).

---

### `GET /register` · the step endpoints

`GET /register` returns the session's state: the current step, every identifier staged
with its verification state and whether it is locked (REG-IDENT-010), the security
methods enrolled so far, and `expiresAt`. It is the polling fallback for
`GET /register/events`.

```json
{
  "step": "email",                       // 10 section 5.19
  "expiresAt": "...",
  "identifiers": [ { "id": "...", "kind": "email", "value": "...", "verified": true, "locked": false } ],
  "security": { "password": true, "secondStep": ["totp"] }
}
```

Request bodies (D-153): `PUT /register/age` `{ "dateOfBirth": "YYYY-MM-DD" }`;
`PUT /register/security` `{ "password": "...", "secondStep": "totp" | "securityKey" |
"passkey" | "none" }`; `POST /register/terms` `{ "termsVersion": "...", "noticeVersion":
"...", "consents": { "<purpose>": true } }`. The state carries no sign-in exit field:
every registration screen shows a static sign-in link (REG-SESS-005), because a field
present only for duplicates would be an oracle (API-CONV-005).

`secondStep` names the enrolment the frontend shows next (the second-step choices of
`10`: `totp` · `securityKey` · `passkey` · `none`) and is recorded nowhere: the security
step completes on what is enrolled (REG-SESS-006), and `GET /register` lists the second
steps enrolled under `security.secondStep` (D-162, D-166).

| Endpoint | Step | Does |
|---|---|---|
| `PUT /register/age` | 1 | Records the date of birth (REG-PROF-002). **422** `identity.profile.underage` on an adults-only host: the session ends and accepts no further date |
| `PUT /register/email` · `PUT /register/phone` | 2, 3 | Stages the identifier, canonicalised, and dispatches a code and a link (REG-SESS-003) unless a provider verified it (REG-IDENT-008). **202**, whether or not the identifier belongs to an account. **429** `auth.throttled` with `retryAt` while the delay of AUTH-ABUSE-001 stands for the source or the identifier, every ask being delayed under it (REG-SESS-003 AC6); **429** `auth.restriction.exceeded` with `retryAt` for a send a restriction refuses (AUTH-ABUSE-004). A provider sign-in that resolves to a linked `sub` is a sign-in, not a registration. An email an invitation bound is verified by the press, so step 2 is complete when step 1 is. A phone an invitation bound is staged locked at the press; at step 3 `PUT /register/phone` takes that number and no other and sends its code, and step 3 cannot be skipped while it stands (REG-MAIL-001 AC3) |
| `POST /register/phone/skip` | 3 | Passes over the phone step where `registration.phone` is `optional` and no invitation bound a phone (REG-SESS-002 AC3, REG-MAIL-001); nothing is staged. **200** with the `GET /register` state at its next step. **409** `identity.registration.incomplete` where `registration.phone` is `required`, an invitation bound a phone, or the session is not at step 3 |
| `POST /register/identifiers` · `PUT /register/identifiers/{id}` · `DELETE /register/identifiers/{id}` | 4 | Adds a further email or phone within `identifiers.email.max` and `identifiers.phone.max`; Change on an unlocked identifier (re-verification follows); removes an unverified extra. On a locked phone not yet verified, `PUT /register/identifiers/{id}` takes only its own number and sends a new code. **409** `identity.identifier.maximum` where the kind's maximum is reached; `identity.identifier.lastofkind` where removal would leave the required minimum unmet; `identity.identifier.locked` for any other value on a locked identifier; **422** `identity.identifier.domainnotallowed` under a domain lock (REG-DOM-001) |
| `POST /register/confirm` | 4 | Completes the confirm step. **409** `identity.registration.incomplete` while any staged identifier is unverified (REG-SESS-004) |
| `PUT /register/security` | 5 | Password (**422** `auth.password.blocklisted`, `auth.password.tooshort`, `auth.password.toolong`) and the second-step choice (**400** `api.request.malformed` naming `secondStep` where the choice is none of the four). WebAuthn and TOTP enrolment (`/auth/webauthn/register/*`, `/account/factors/totp/*`) accept the registration session in place of an account session for this step (REG-SESS-006); a label is required (AUTH-FACT-001). Recovery codes are returned once when a second step is enrolled beside a password (AUTH-RECOV-006) |
| `POST /register/terms` | 6 | Records terms accepted, notice presented, the derived affirmation and the consent controls (REG-SESS-007), runs the one transaction that creates the account `active`, emits `AccountRegistered` once, and signs the person in at the assurance the security step proved. The client captured at `POST /register` is kept on the session this step establishes and read back as `landing` at `GET /auth/session` (REG-SESS-008). **201** with the session cookie set. **422** `identity.affirmation.required` where the affirmation record is absent; **422** `privacy.purpose.noconsent` for a ticked control whose purpose takes no consent; **409** `privacy.notice.unpublished` for a ticked control whose governing document has no published version; **409** `identity.registration.incomplete` where the account would be created without a terms version or a notice version (REG-SESS-007). Nothing is written on any refusal |

*Source: D-146; REG-SESS-002 to REG-SESS-007, REG-IDENT-010, REG-DOM-001, D-162, D-166*

Steps 7 to 10 (about you, preferences, membership, done) run on the account
application in the new session through `PUT /account/profile`,
`PUT /account/preferences` and the membership acknowledgement (`GET /account/invitation`,
`POST /account/invitation/acknowledge`, section 6a); they are not registration endpoints. No step endpoint accepts a request for a step whose
predecessor is incomplete, and none returns to a completed step (REG-SESS-002 AC1): such a
request is refused **409** `identity.registration.incomplete`.

---

### `POST /register/verify/{id}`

Verifies one staged identifier, by the typed code or by the link, with the behaviour of
REG-SESS-003.

```json
{ "code": "..." }                                   // typed where the flow is waiting
{ "linkToken": "...", "press": true }               // the landing page, on a press
```

**204**: verified; the waiting screen learns of it through `/register/events`
**200**: a `linkToken` presented **without** `press`, or from a browser that does not
hold the originating session's cookie: nothing changes; the body carries `sameBrowser`
and, where it is false, the `code` to type where the flow is waiting
**422**: `auth.code.invalid`, `auth.code.expired`; after `code.verification.attempts`
wrong codes the code is invalidated and a correct one is refused too (AUTH-FACT-004)
**429**: `auth.throttled` with `retryAt` while the delay of AUTH-ABUSE-001 stands: a
wrong code, or a pressed `linkToken` that opens nothing, is counted against the source
and the identifier (REG-SESS-003 AC6); a replacement code draws on the sending
restrictions (AUTH-ABUSE-004, `auth.restriction.exceeded` with `retryAt`)

*Source: D-146; REG-SESS-003, AUTH-FACT-004, AUTH-ABUSE-001, AUTH-ABUSE-004, API-LAND-001, D-166*

A `linkToken` with `press` verifies only when the request carries the cookie of the
session that staged the identifier: the link completes in the browser that started the
flow and only on a press. A plain open of the landing route changes nothing, which is
what defeats mail scanners that prefetch links. The verification code is not an
authentication credential and is refused at every sign-in endpoint (AUTH-FACT-004).
The same endpoint shape serves adding an identifier to an existing account
(`POST /account/identifiers/{id}/verify`).

---

### `GET /register/events`

Server-sent events for the registration session, authenticated by the **session cookie
alone**: no token in the URL, no query parameter (BFF-CSRF-005a). Emits an event when
an identifier is verified, when a step completes and when the session ends. `event:`
names the change (the registration stream events of `10`): `identifier-verified` (an
identifier of the state became verified), `step-completed` (`step` advanced) or
`session-ended` (the registration session no longer exists). Every event,
`session-ended` included, carries the `GET /register` state as its `data`, so polling
and streaming are one shape (D-153); the `data` of `session-ended` is the last state the
session held. Where the stream is unavailable the frontend polls `GET /register`
(FE-VER-001).

**200**: event stream
**401** is never used here (API-CONV-003); a request without the session cookie
receives **404**

*Source: D-146; REG-SESS-003, `17-bff` (server-sent events on the session cookie), D-166*

---

### `POST /register/abandon`

Ends the registration session at once and leaves nothing behind (REG-SESS-001): the
session-ending control of a link opened in another browser (REG-SESS-003), and the
person's own cancel. Accepts either the session cookie or the `linkToken` of a message
the session sent.

```json
{ "linkToken": "..." }          // optional; absent when called from the session itself
```

**204**: always; a token that resolves to no live session is answered identically

*Source: D-146; REG-SESS-001, REG-SESS-003*

After abandonment every code and link the session issued stops working and a later
registration with the same identifiers is a fresh one.

---

### `POST /register/verify-phone`

*Retired by D-146. See `POST /register/verify/{id}`.*

---

### `POST /register/verify-email`

*Retired by D-146. See `POST /register/verify/{id}`.*

---

### `PUT /register/password`

*Retired by D-146. See `PUT /register/security` (REG-SESS-006: a password below the
single-factor floor makes a second step mandatory in place, and lengthening it lifts
that on the same screen). No `pending` account exists to hold a password.*

---

## 3. Authentication

### `POST /enrol/begin` · the enrolment session

Consumes an **enrolment link token**, the admin-assisted recovery link
(AUTH-RECOV-002), delivered to a recorded channel and landing on a frontend route
(API-LAND-001). This is the only endpoint that consumes it. The customer's
self-service recovery link (`/recovery/begin`) is a different token, consumed only by
`POST /recovery/complete` (D-147). A staff invitation token is not an enrolment token: it opens a
registration session (`POST /register` with the token, REG-INV-001) or, for a person who
already holds an account, a sign-in followed by the membership step (REG-INV-002).

```json
{ "token": "..." }
```

**200** — an **enrolment session**: no application access, valid for the remainder
of the link's own lifetime (`recovery.link.lifetime`, default 1 hour; no separate
constant, D-147), usable only against the endpoints below
**422** — `auth.recovery.tokenexpired`, `auth.enrolment.tokeninvalid`

Within the enrolment session the person MAY call: `POST /account/password`,
`/auth/webauthn/register/*`, `/account/factors/totp/*` (set a password or enrol a
passkey; for a recovery, replacing what was lost), and, for a recovery whose
approver recorded the mailbox as lost, `PUT /account/identifiers/{id}/replace`
completing on the **new address alone** (AUTH-RECOV-002: the approver's recorded
confirmation stands in for the old address; the exception is stated in REG-IDENT-007). Completing the enrolment ends the
enrolment session and requires an ordinary sign-in with the new credential.

*Source: D-148; AUTH-RECOV-002, AUTH-RECOV-005, API-LAND-001, D-140, D-146, D-147*

---

### `POST /auth/link`

Requests a **sign-in link** for an identifier, by email (`emailLink`) or by SMS
(`phoneLink`) according to the identifier's kind. Replaces `/auth/magic-link`.
**Always 202**, whether or not the account exists and whether or not the policy has
enabled the matching link factor (AUTH-FACT-003 AC5, AUTH-ABUSE-003); an address a
domain lock refuses (REG-DOM-001) is answered the same and sent nothing; a number the
SIM-change signal reports as `risk` is sent nothing and answered the same, and the
consideration is audited (AUTH-FACT-002b); a non-existent address receives the ordinary
non-existence message (AUTH-ABUSE-003), once per `abuse.nonexistent.window`. The link
lives `link.magic.lifetime`.

```json
{ "identifier": "..." }
```

**202** · **429**: throttled, or the send refused by a restriction
(`auth.restriction.exceeded` with `retryAt`, identical for a registered and an
unregistered identifier, AUTH-ABUSE-002)

*Source: D-146; AUTH-FACT-002, AUTH-FACT-003, AUTH-ABUSE-003, AUTH-ABUSE-004, D-162, D-166*

The link completes **only in the browser that requested it and only on a press**
(REG-SESS-003): the landing route (API-LAND-001) calls `POST /auth/factor` with
`factor: "emailLink"` or `"phoneLink"` and the link token, and the server accepts it
only with the requesting browser's pre-authentication cookie. Opened anywhere else,
the same call without that cookie returns the code to type where the sign-in began,
and signs nothing in. That code is an authentication code, not a verification code: it
lives as long as its link (`link.magic.lifetime`) and is capped by
`code.signin.attempts` wrong tries, after which it is refused `auth.code.expired`, a
correct one included (AUTH-FACT-004). A session established by a link alone records AAL1 and a link is
never a second step (AUTH-FACT-003).

---

### `POST /auth/link/abandon`

Ends a pending sign-in link: the ending control of a sign-in link opened in a browser
other than the one that requested it (REG-SESS-003, FE-VER-001). **Link-borne**: it
accepts the link token from the message, needs no cookie, and belongs to no
registration session.

```json
{ "linkToken": "..." }
```

**204**: always; a token that resolves to no pending link is answered identically

*Source: D-148; REG-SESS-003, AUTH-FACT-003*

After the call the link and its code stop working; nothing else about the account or
the sign-in attempt changes, and the response reveals nothing about whether an account
exists.

---

### `POST /auth/magic-link` · `POST /auth/email-otp` (`emailCode`)

`POST /auth/magic-link`: *Retired by D-146. See `POST /auth/link`.*

`POST /auth/email-otp` requests an email one-time code (catalogue entry `emailCode`,
off by default; refused as if the account did not exist while the policy's
`loginFactors` does not enable it). **Always
202**, whether or not the account exists (AUTH-ABUSE-003); an address a domain lock
refuses (REG-DOM-001) is answered the same and sent nothing; a non-existent address
receives the ordinary "no account" email, once per `abuse.nonexistent.window`. The
code is sent as the message kind `sign-in-code` and consumed at `/auth/factor` with
`factor: "emailCode"`; it is an authentication code, with a lifetime, storage and
attempt cap of its own, independent of the verification code (AUTH-FACT-004): it lives
`code.signin.lifetime` and is spent after `code.signin.attempts` wrong tries.

```json
{ "identifier": "..." }
```

**202** · **429**: throttled

*Source: AUTH-FACT-002, AUTH-FACT-003, AUTH-FACT-004, AUTH-ABUSE-003, D-135, D-166*

---

### `POST /auth/begin`

Starts authentication. Takes **one** `identifier` field of any kind (email, phone or,
where enabled, username); the kind is detected and the value canonicalised
(REG-IDENT-003). Returns the factors available, or an indistinguishable response where
the identifier resolves to nothing.

```json
{ "identifier": "..." }
```

**200**
```json
{
  "challengeId": "...",
  "available": ["password", "passkey", "emailLink"],
  "webauthn": { }      // always present
}
```

*Source: AUTH-FACT-002, AUTH-ABUSE-003, D-076, D-146*

Every verified identifier of an account signs in, and a non-primary one behaves
exactly as the primary (REG-IDENT-003). While `identifiers.username.enabled` is off a
username is not a kind the detection knows.

**The factor list is the policy's enabled primary set, never the account's.** It is
identical for every identifier, existing or not, so the response cannot be compared.

A WebAuthn challenge is **always** returned: discoverable credentials need no
allow-list, so issuing one reveals nothing about whether credentials exist.

**Second-factor requirements are never disclosed here.** They surface only after a
first factor succeeds. Returning "this account has no second factor" before any
credential is presented hands an attacker exactly the targeting information they
want.

---

### `POST /auth/factor`

Presents one factor. Called repeatedly until the session reaches the required
assurance.

```json
{ "challengeId": "...", "factor": "password", "value": "..." }
```

**200** — factor accepted
```json
{
  "status": "complete",              // or "factorRequired"
  "assuranceLevel": "aal2",          // delegated | aal1 | aal2 | aal3 (10 §5.4)
  "phishingResistant": true,
  "required": [],                    // populated when status is factorRequired
  "trustDeviceOffered": true         // AUTH-FACT-015: policy permits trusting this browser
}
```

The request MAY carry `"trustDevice": true` on the call that completes a two-factor
sign-in; where `trustDeviceOffered` is true the response sets the device-trust cookie
(AUTH-FACT-015). A trusted-device cookie presented on a later sign-in satisfies the
second factor, and `required` omits it. For a `restricted` account `trustDeviceOffered`
is false and `trustDevice` is not honoured: the account signs in and reads, and records
no credential (IDN-ACCT-007).

**422** — `auth.factor.rejected`, `auth.factor.notpermitted` (verification-only
factor offered), `identity.identifier.domainnotallowed` (a factor succeeded and the
sign-in email is outside a domain lock the account is under, REG-DOM-001; a pending
link or open challenge whose email the account no longer holds is `auth.factor.rejected`),
`auth.code.invalid` and `auth.code.expired` (a sign-in code, below).
Never 401: that code is session death only (API-CONV-003, D-125).
**429** — throttled, with `Retry-After`

**Link tokens.** A call carrying a sign-in link's token names `factor` `emailLink` or
`phoneLink`; any other factor with a `linkToken` is **400** `api.request.malformed`
naming `factor`. A pressed token that opens no pending link is recorded as a failed
authentication behind the source delay of AUTH-ABUSE-001 and answered `auth.code.expired`
(**429** `auth.throttled` while the delay stands); an unpressed one changes and records
nothing (CONV-LOG-005, D-166).

**Sign-in codes.** A code presented as `emailCode`, as the `phoneCode` second step, or
where a sign-in link was opened in another browser is an authentication code
(AUTH-FACT-004). The `emailCode` and `phoneCode` codes live `code.signin.lifetime`; the
code a sign-in link shows lives as long as its link (`link.magic.lifetime`). Each is
capped by `code.signin.attempts`: a wrong code is `auth.code.invalid`, and a code past its
lifetime or presented after the cap is `auth.code.expired`, a correct one included
(D-166).

**Passkey assertions.** The assertion carries the user handle the authenticator
returned. A handle naming an account other than the credential's owner, or none the
library issued, is refused `auth.factor.rejected`; a passkey sign-in opened without an
identifier refuses an assertion with no handle the same way (WebAuthn Level 3 section
7.2, REG-PM-001, D-166).

**New-device check** (AUTH-FACT-016). Where the factors presented would complete a
sign-in at reachable assurance AAL1 from a browser the account has not seen, the call
that would complete it instead returns **200** with `status: "deviceVerificationRequired"`
(the sign-in status of `10`, a status and not a refusal code); no session is set. A code goes to the
primary email under `email.destination`, and the sign-in completes at
`POST /auth/device/verify`. A passkey sign-in and a completed two-step sign-in never
see this status.

**Grace period** (AUTH-FACT-017). Where the account does not yet meet a raised
requirement and `policy.enforcement.grace` has not elapsed, the completing response
carries `policyRequirement` (`{ field, value, deadline }`, AUTH-FACT-017) beside
`status: "complete"`; a password that now fails the `context` blocklist adds
`passwordChangeRequired: true` there (AUTH-PASS-004, D-153); after the grace the response is **403** `auth.policy.graceexpired` with
`outcome: "enrol"` in `details`, and the sign-in stops at enrolment until the
requirement is met.

*Source: AUTH-FACT-001, AUTH-FACT-003, AUTH-FACT-016, AUTH-FACT-017, AUTH-SESS-002,
AUTH-ABUSE-001, D-146, D-166*

The response reports **properties reached**, never which factor produced them. On
completion the session cookie is set and the identifier rotates (AUTH-SESS-006).

Throttling responses are identical whether or not the account exists
(AUTH-ABUSE-002).

---

### `POST /auth/device/verify`

Completes a sign-in held by the new-device check (AUTH-FACT-016).

```json
{ "challengeId": "...", "code": "..." }
```

**200**: the sign-in completes with the response shape of `/auth/factor`; the session
records AAL1 (AUTH-FACT-003) and the browser is remembered for
`device.verification.lifetime`. Emits `DeviceVerified`.
**422**: `auth.code.invalid`, `auth.code.expired`; `code.verification.attempts` wrong
codes invalidate the code (AUTH-FACT-004)
**429**: a replacement code refused by `email.destination`
(`auth.restriction.exceeded` with `retryAt`)

*Source: D-146; AUTH-FACT-016, AUTH-FACT-004, AUTH-ABUSE-004*

The code is a verification code (AUTH-FACT-004), not a credential: it proves the
browser, not the person, and raises no assurance property. The remembered browser is a
separate record from a trusted device (AUTH-FACT-015), which skips a second factor;
this one skips nothing.

---

### `GET /account/devices` · `DELETE /account/devices/{id}`

Lists the account's **trusted devices** (AUTH-FACT-015) and the browsers remembered by
the new-device check (AUTH-FACT-016), each `{ id, kind: trusted · remembered, label,
createdAt, lastUsedAt }` (the device kinds of `10`), and removes one.
Removing a trusted device requires the second factor again on that browser; removing
a remembered browser makes it face the check again. **Sessions are not listed here**:
they are `GET /account/sessions` (AUTH-SESS-013).

**204** · **404**: `authz.resource.notfound`, for an `{id}` naming no device of the
account's, another account's included, one answer for both

*Source: AUTH-SESS-008, AUTH-FACT-015, AUTH-FACT-016, D-124, D-146, D-166*

---

### `POST /auth/step-up`

Raises an existing session's assurance. Same request and response shape as
`/auth/factor`; called once per factor until the session reaches the gate. A factor the
policy in force for the account does not permit is refused `auth.factor.notpermitted`,
as at sign-in (IDN-LIFE-009b).

*Source: AUTH-STEP-001, AUTH-STEP-002, D-141, D-166*

Rotates the session identifier on success.

**What the gate tells the frontend.** Every `403` `auth.stepup.required` that judged a
gate on a session of the library belonging to the acting account carries in `details`
the three gate values and what the subject can do about them. Where no such session
carries the request (an in-process call without one, or a context the session does not
belong to), no gate is judged and the refusal carries no `details`. A gate a host binds
to its own action and judges through its assurance provider (LIB-HOST-004) carries
`required`, `outcome` `present`, empty `options` and a null `pendingUntil`:

```json
{
  "code": "auth.stepup.required",
  "details": {
    "required": { "level": "aal2", "phishingResistant": false, "maxAge": 900 },
    "outcome": "present",            // present | enrol | report-loss | pending
    "options": [ ["password", "totp"], ["password", "recoveryCodes"], ["passkey"] ],
    "pendingUntil": null             // set when outcome is pending
  }
}
```

`options` lists **every combination of the account's usable factors that reaches the
gate** (AUTH-STEP-002 step 2); the subject picks one and presents it factor by factor.
`enrol` means no usable combination exists and the account has never reached the
level — the frontend offers enrolment in place (AUTH-STEP-005 AC2). `report-loss`
means the account can reach the level but a factor that would do it is gone — the
frontend offers `POST /recovery/report-loss`. `pending` means a loss report is already
running and `pendingUntil` is when it completes; the answer is still a 403, since the
operation did not proceed, and the frontend presents it as a status. `pendingUntil` is
present in every refusal and null unless the outcome is `pending`. `maxAge` is the gate's
maximum age in whole seconds, truncated. Factor names appear here as presentation data
for the prompt, spelled as the catalogue spells them (AUTH-FACT-002); the session
record itself stores properties only (AUTH-SESS-002).

---

### `POST /auth/logout`

**204**

*Source: AUTH-SESS-008*

Terminates **every** app session and the auth session. Not scoped to the calling
application.

---

### `GET /auth/session`

**200**
```json
{
  "subject": "...",
  "assuranceLevel": "aal2",
  "phishingResistant": true,
  "lastStrongAuthAt": "...",
  "expiresAt": "...",
  "landing": "..."        // the origin of the client captured at registration; absent otherwise
}
```

**No organization is returned.** Authorization resolves the organization from the
resource, never from the session (IDN-MEM-003, AUTHZ-SCOPE-001) — returning one here
would invite the frontend to reason from it.

`landing` is the origin (scheme, host and port) of the registered address of the client
a registration captured (API-REDIR-002), kept on the session the terms step established;
the done step of registration reads it here and never from a request (REG-SESS-008,
D-166).

**401** — no valid session

Never returns permissions or role names (AUTHZ-CACHE-002).

---

### `GET /auth/signon` · `GET /auth/signon/return`

The browser profile's sign-on (BFF-SESS-006): both halves are the library's.
`GET /auth/signon?returnTo=` pushes the authorization request to `POST /oidc/par` and
answers **302** to the provider's `/oidc/authorize` carrying `client_id` and
`request_uri` only; a browser already holding a per-app session is sent to `returnTo`,
which is followed only as a path of this application. `GET /auth/signon/return` answers
**302** to the stored return address with the session cookies set.

**403**: `auth.session.csrfinvalid`, for an absent, unbound or mismatched `state`
**401**: `auth.session.expired`, for any other failure

*Source: BFF-SESS-006, AUTH-OIDC-006, D-162, D-166*

---

## 4. WebAuthn

### `POST /auth/webauthn/register/begin` · `POST /auth/webauthn/register/complete`

Registration ceremony. `begin` takes the **kind** being created, `{ "kind": "passkey" |
"securityKey", "label": "..." }` (the AUTH-FACT-002 identifiers): a passkey
(`residentKey: required`, `userVerification: required`) or a second-factor security
key (`residentKey: discouraged`), per AUTH-FACT-002b; a security key under two-step is
refused on an account with no password. `complete` takes the credential and a
**label** (1 to 64, unique per kind per account, defaulting to the client's device
description, AUTH-FACT-001) and records the relying party identifier, `backupEligible`
and `backupState`, `addedAt` and the label. During registration step 5 the ceremony
runs against the registration session and its provisional user handle (REG-SESS-001).
`begin` answers the creation options with `user`: `{ "id": "<the subject identifier as a
WebAuthn user handle>", "name": "<the primary email>", "displayName": "<the display name,
or empty>" }`; during registration `id` is the registration session's provisional
subject identifier, which becomes the account's, and `name` the staged email; the
credential is staged on the registration session and no account row is written before
the terms step (REG-PM-001, REG-SESS-006).

**422**: `auth.webauthn.algorithmnotallowed`,
`auth.webauthn.userverificationrequired`

*Source: AUTH-FACT-001, AUTH-FACT-002b, AUTH-FACT-011, AUTH-FACT-013, AUTH-FACT-014,
D-146, D-162, D-166*

Where the credential is device-bound, the response indicates a second enrolment is
prompted — advisory for public users, required for administrative-organization
members (AUTH-RECOV-001).

Enrolment is gated at the lower of the account's reachable assurance and AAL2
(AUTH-STEP-007) and is notified to every recorded channel other than the enrolling
session.

---

### `GET /.well-known/webauthn`

**200**: the related-origins allowlist.

*Source: AUTH-FACT-012*

Served from configured additional origins. Public, unauthenticated.

---

### `GET /.well-known/change-password` · `GET /.well-known/passkey-endpoints`

`/.well-known/change-password` answers **302** to the frontend's password page, so a
password manager can send the person straight there. `/.well-known/passkey-endpoints`
answers **200** with JSON naming `enroll` (the passkey enrolment page) and `manage`
(the credential list). The probe path
`/.well-known/resource-that-should-not-exist-whose-status-code-should-not-be-200`
answers **404**, never a catch-all page, so the redirect is trusted.

*Source: D-146; REG-PM-001, D-162, D-166*

Both are public and unauthenticated, and are mounted at the site root (API-CONV-001).
The addresses they name are frontend routes (API-LAND-001), declared by the host
(LIB-HOST-001, `PasskeyAddresses`).

---

## 5. Recovery

### `POST /recovery/begin`

```json
{ "identifier": "..." }
```

**202** — always, regardless of existence.

*Source: AUTH-ABUSE-003, AUTH-FACT-002b, D-166*

Where the identifier does not exist, **that address is emailed to say so**. The real
owner gets their answer; an enumerating attacker learns nothing. A number the SIM-change
signal reports as `risk` receives nothing, whether or not an account holds it, and the
consideration is audited (AUTH-FACT-002b).

Administrative-organization members receive no email or SMS route
(AUTH-RECOV-004).

---

### `POST /recovery/complete`

Consumes the self-service recovery link token issued by `/recovery/begin` (the only
endpoint that does; the admin-assisted enrolment link goes to `/enrol/begin`, D-147)
and restores access to the **password only**. Does not remove an enrolled second
factor. For a self-deactivated account, completing recovery also restores `active`
(IDN-LIFE-013, D-140).

```json
{ "token": "...", "password": "..." }
```

*Source: AUTH-RECOV-005, D-140, D-147*

**422** — `auth.recovery.tokeninvalid`, `auth.recovery.tokenexpired`

---

### `POST /recovery/report-loss` · `POST /recovery/report-loss/{id}/cancel`

Reports an authenticator lost — any authenticator: passkey, security key, TOTP —
and suspends it at once; or cancels a pending report.

```json
{ "credentialId": "..." }
```

**Accepts a session that has presented one usable factor, or one unused recovery
code** — no step-up (AUTH-RECOV-007). The request exists for someone who has lost the
factor a gate would ask for and cannot hold a complete session. Cancellation accepts
the same, any full session of the account, or the link carried in every
notification.

**202** — accepted; the authenticator is `suspended`; body carries `invalidatesAt`
**409** — `auth.lossreport.notpermitted` (administrative-organization member);
`auth.lossreport.pending` (already reported)

*Source: AUTH-RECOV-007, AUTH-RECOV-008, D-141*

Invalidation is automatic after `recovery.invalidation.window` and is **held** if no
notification delivered. Reachable assurance (AUTH-STEP-006) changes only then.

---

### `POST /admin/recovery/approve`

Administrative re-enrolment. Requires the approver permission (`recovery:approve`) and
step-up (`recovery:approve`).

```json
{ "subject": "...", "reason": "...", "channelUsed": "..." }
```

**200** `{ enrolmentLinkExpiresAt }`; the link itself goes to the channel
**422** — `auth.recovery.reasonrequired`, `auth.recovery.channelnotonaccount`

*Source: AUTH-RECOV-002, AUTH-RECOV-003*

`channelUsed` SHALL be one of the account's **recorded** channels. A
requester-supplied channel is rejected. Available for **every account**, not only
staff; the link is delivered to a recorded channel the approver names — the phone,
where the mailbox is unreachable — and the re-enrolment session it opens may set a
new email confirmed by the new address alone (AUTH-RECOV-002, D-111).

---

## 6. Account

A `restricted` account signs in and reads its own data through the endpoints below.
Every change it asks to its identifiers, credentials, profile and preferences is refused
**403**
`authz.restricted` (IDN-ACCT-007, AUTHZ-GATE-006), except ending a session, reporting a
credential lost, listing and revoking app passwords, the link-borne undo of an identifier
change, and a credential set by recovery or enrolled where a policy hold stops its
sign-in (AUTH-FACT-017) (D-166).

### `GET /account`

**200**: the account in the groups of REG-ACCT-001, every field the person may see
and nothing else:

```json
{
  "state": "active",
  "identifiers": {
    "emails": [ { "id": "...", "value": "...", "verified": true, "primary": true, "locked": false } ],
    "phones": [ { "id": "...", "value": "...", "verified": true, "primary": true, "locked": false } ],
    "username": "...",                       // absent while identifiers.username.enabled is off
    "backup": { "emails": "all-verified", "phones": "primary-only" }
  },
  "credentials": [ { "id": "...", "kind": "...", "label": "...", "state": "active",
                     "backupEligible": true, "backupState": true,
                     "addedAt": "...", "lastUsedAt": "..." } ],
  "secondStep": { "preferred": "..." },
  "recoveryCodes": { "remaining": 8, "generatedAt": "...", "viewedAt": "...", "exportedAt": null, "remindedAt": null },
  "profile": { "displayName": "...", "legalName": "...", "dateOfBirth": "...", "photo": "..." },
  "preferences": { "language": "...", "timeZone": "...", "declared": { } }
}
```

Credentials are reported by property and label, never by secret material; `legalName`
and `dateOfBirth` are present only where `profile.legalname` and `profile.dateofbirth`
are on (REG-PROF-001). The credential list renders `backupState` as synced or this
device only (`18` FE-ACCT-001). `recoveryCodes.remindedAt` is the instant a channel took
the set's one reminder, or null (AUTH-FACT-008).

*Source: D-146; REG-ACCT-001, AUTH-FACT-001, AUTH-FACT-008, AUTH-FACT-009, D-166*

---

### `PUT /account/profile`

Edits the profile fields of REG-PROF-001 and, where enabled, the username
(REG-IDENT-009).

```json
{ "displayName": "...", "legalName": "...", "username": "..." }
```

**204**
**403**: `auth.stepup.required`, for a username change only (`username:change`)
**409**: `identity.username.taken`, `identity.username.reserved` (disclosed by design:
a username is public, REG-IDENT-009); `identity.username.coolingoff` where a username
change falls inside `identifiers.username.changecooloff`, with the cooling-off end in
`details`
**422**: `identity.identifier.mixedscript`; `identity.profile.invalid` (a display name
over 64 bytes, or a legal name outside its bound); `identity.profile.notaccepted` (a
`dateOfBirth` field, immutable to the person and corrected through support; a
`legalName` or `username` while its key or `identifiers.username.enabled` is off)

*Source: D-146; REG-PROF-001, REG-IDENT-009, D-162, D-166*

Display name and legal name need no gate and are audited. A username change is a
step-up action and re-verifies nothing: a username is never verified.

---

### `GET /account/preferences` · `PUT /account/preferences`

The language (BCP 47), the time zone (IANA identifier) and the values of the
host-declared preferences (REG-PREF-001). `PUT` replaces the set.

**200** / **204**
**422**: `identity.preference.undeclared` (a key the host did not declare),
`identity.preference.wrongtype` (a value of the wrong type for its declaration),
`identity.preference.toolarge` (a set over `preferences.maxsize`),
`identity.preference.administratoronly` (an administrator-only preference set by the
person)

*Source: D-146; REG-PREF-001, D-166*

The library validates against the declaration and stores; it never branches on a
declared value. Replaces `PUT /account/language`.

---

### `POST /account/identifiers`

Adds an email or phone (REG-IDENT-004). Step-up action `identifier:add`.

```json
{ "kind": "email", "value": "..." }
```

**202**: always, whether or not the identifier belongs to another account
(API-CONV-005): the identifier is staged unverified, a code and a link are sent
(REG-SESS-003) and the security-notice set is notified. A duplicate sends no code and
notifies the identifier's owner (REG-SESS-005, D-076).
**403**: `auth.stepup.required`
**409**: `identity.identifier.maximum`, the kind's maximum (`identifiers.email.max`,
`identifiers.phone.max`) reached; in single-address mode use
`PUT /account/identifiers/{id}/replace`
**422**: `identity.identifier.mixedscript`, `identity.identifier.domainnotallowed`
(REG-DOM-001), `identity.identifier.invalid` (a value that is not a well-formed
identifier of its kind)

*Source: D-146; REG-IDENT-004, REG-SESS-005, REG-DOM-001, D-162, D-166*

An unverified identifier cannot sign in and receives no recovery link until verified.

---

### `POST /account/identifiers/{id}/verify`

Verifies an added identifier. Same request, responses and browser binding as
`POST /register/verify/{id}` (REG-SESS-003): a typed code, or a link pressed in the
browser that added it.

**204** · **200** (link opened elsewhere: nothing changes, the code is shown) ·
**422**: `auth.code.invalid`, `auth.code.expired` · **429**

*Source: D-146; REG-IDENT-004, REG-SESS-003*

Emits `IdentifierAdded` on success.

---

### `POST /account/identifiers/{id}/primary` · `PUT /account/identifiers/backup`

Sets the primary of a kind; sets the backup setting of a kind (`all-verified` ·
`primary-only` · the id of one verified identifier). Neither needs step-up nor
confirmation; each produces one notice to the security-notice set **as it was before
the change** (REG-IDENT-005, REG-IDENT-002).

```json
{ "kind": "email", "setting": "primary-only" }      // PUT /account/identifiers/backup
```

**204**
**409**: `identity.identifier.unverified` where the identifier made primary, or named as
the backup, is unverified; the named identifier is the primary (the primary cannot be
the backup)

*Source: D-146; REG-IDENT-002, REG-IDENT-005, D-166*

Emits `IdentifierPrimaryChanged` on a primary change.

---

### `DELETE /account/identifiers/{id}`

Removes an identifier, **immediately** (REG-IDENT-006). Step-up action
`identifier:remove`.

**204**: removed; the remaining security-notice set receives an **undo** link valid
for `identifier.change.coolingoff`; the removed identifier receives a notice with no
link; every other session of the account ends. Emits `IdentifierRemoved`.
**403**: `auth.stepup.required`
**409**: `identity.identifier.primary` (set another primary first; decided before any
other refusal, so the last verified identifier of a kind answers this),
`identity.identifier.lastofkind` (the required minimum of the kind would be unmet)

*Source: D-146; REG-IDENT-006, D-166*

The undo goes to the remaining set and never to the removed address, so a compromised
mailbox cannot re-attach itself. The removed identifier is powerless from then on and
behaves as unknown at every recovery path.

---

### `POST /account/identifiers/{id}/undo`

Restores a removed identifier from the undo link (REG-IDENT-006). **Link-borne**: the
account has no session that could reach this on its own after a hostile removal, so it
accepts the link token from the undo notice, the same shape as deletion cancellation.

```json
{ "linkToken": "..." }
```

**204**: restored, verified as it was, and the security-notice set notified
**422**: `identity.change.windowelapsed` (after `identifier.change.coolingoff`)

*Source: D-146; REG-IDENT-006*

---

### `PUT /account/identifiers/{id}/replace`

Single-address mode (`identifiers.<kind>.max` = 1): the change of an identifier in one
operation (REG-IDENT-007). Step-up; the new value is verified by
`POST /account/identifiers/{id}/verify`; on verification the swap applies at once and
the undo goes to the remaining channels of the account. **Only where the account has
no other channel at all** does the old address confirm before the swap, except within
an admin-assisted enrolment session (AUTH-RECOV-002, REG-IDENT-007); the old address's
message (`identifier-change-confirm`) carries a link of kind `identifier-confirm`
(API-LAND-001).

```json
{ "value": "..." }
```

**202**: always, whether or not the new value belongs to another account
(API-CONV-005)
**403**: `auth.stepup.required`
**409**: `identity.change.pending` (a replace is already staged for this identifier)
**422**: `identity.identifier.mixedscript`, `identity.identifier.domainnotallowed`

*Source: D-148; D-146, REG-IDENT-007, D-166*

Also the endpoint an enrolment session opened for a lost mailbox uses to set a new
address confirmed by the new address alone (`POST /enrol/begin`, AUTH-RECOV-002). The
session under which the swap completes rotates and every other session of the account
ends, the staging session included where it is not the completing one; a swap completed
under no session (the old address's confirmation, an enrolment session) ends every
session (IDN-LIFE-008).

---

### `POST /account/identifiers/{id}/abandon`

Ends the pending verification of an identifier staged by `POST /account/identifiers`
or `PUT /account/identifiers/{id}/replace`: the ending control of an add or replace
link opened in a browser other than the one that staged it (REG-SESS-003, FE-VER-001).
**Link-borne**: it accepts the link token from the message and needs no session.

```json
{ "linkToken": "..." }
```

**204**: always; a token that resolves to no pending verification is answered
identically

*Source: D-148; REG-SESS-003, REG-IDENT-004, REG-IDENT-007*

The staged identifier is discarded, or the staged replace withdrawn, and its code and
link stop working; the account's verified identifiers are unchanged.

---

### `POST /account/email/change` · `/confirm-old` · `/confirm-new` · `/revert`

*Retired by D-146. See `POST /account/identifiers`, `DELETE /account/identifiers/{id}`,
`POST /account/identifiers/{id}/undo` and, in single-address mode,
`PUT /account/identifiers/{id}/replace` (REG-IDENT-004 to REG-IDENT-007). The
old-address confirmation survives only where no other channel exists (REG-IDENT-007);
the four-step ceremony is retired.*

---

### `POST /account/phone/change` · `/confirm` · `/revert`

*Retired by D-146. See `POST /account/identifiers`, `DELETE /account/identifiers/{id}`,
`POST /account/identifiers/{id}/undo` and `PUT /account/identifiers/{id}/replace`
(REG-IDENT-004 to REG-IDENT-007). The one-delivery-per-window limit is replaced by the
sending restrictions (AUTH-ABUSE-004).*

---

### `POST /account/link/{provider}` · `DELETE /account/link/{provider}`

`POST` answers whether the account may link the provider now (a session, the
`provider:link` step-up passed, the provider enabled by the policy) and changes nothing;
the link is made by the round trip `GET /auth/providers/{provider}?intent=link` (below),
which asks the same again and links on the return. `DELETE` unlinks (`provider:unlink`).
The last-credential refusal is decided before the step-up gate, so an account whose only
way in is the provider is answered 409 and not asked for a step-up a delegated session
cannot give (D-128). Linking to an account that holds a second factor shows the R-A16
disclosure (AUTH-FACT-002a).

**204**
**403**: `auth.stepup.required`
**404**: `auth.credential.notfound`, unlinking a provider the account does not hold
**409**: `identity.link.lastcredential`, unlinking the only remaining credential

An identity another account holds, or a second identity at a provider the account
already holds, is refused `auth.factor.rejected` on the round trip's return (the browser
is returned with it in `error`).

*Source: IDN-LIFE-012, D-128, D-140, D-166*

---

### `GET /auth/providers/{provider}` · `GET|POST /callbacks/providers/{provider}/return` · `GET /auth/providers/{provider}/return`

The social provider round trip (IDN-LIFE-012, REG-IDENT-008).
`GET /auth/providers/{provider}?intent=signin|register|link&returnTo=...` starts it on
the browser profile: `returnTo` is sanitised to a local path (anything else is `/`), the
attempt is bound to the browser's session for `link` and to its pre-authentication
session otherwise, and the browser is sent to the provider. The provider returns the
browser to `/callbacks/providers/{provider}/return` on the machine profile, by query or
form post; that route ignores any cookie and answers only **303** to
`/auth/providers/{provider}/return` carrying `code`, `state` and `error` in the query.
The continuation finishes the round trip on the browser profile. A refusal of the
person's attempt returns the browser to where it started with the code in the query
member `error`, placed before any fragment; a throttled refusal carries `retryAt` beside
it (`error=auth.throttled&retryAt=<instant>`, ISO 8601 in UTC), and a provider's return
asks the source's delay before the code is traded (AUTH-ABUSE-001).

**303**: to the provider, to the continuation, or back to `returnTo`
**403**: `auth.session.csrfinvalid`, where no attempt is bound to the browser, it names
another provider, or its `state` does not match; the browser is sent nowhere

*Source: IDN-LIFE-012, REG-IDENT-008, BFF-MACH-001, BFF-CSRF-005a, BFF-ABUSE-001, D-166*

---

### `POST /account/password`

Sets or changes a password while signed in. Requires step-up at the gate the
subject's policy declares (AUTH-STEP-002).

**204**
**422**: `auth.password.blocklisted`, `auth.password.tooshort`, `auth.password.toolong`

*Source: AUTH-PASS-004, AUTH-PASS-001a, AUTH-RECOV-007a, D-092, D-162, D-166*

The floor applied follows the account's reachable assurance **now**: ten where it is
AAL2, fifteen where it is AAL1 (AUTH-PASS-001a).

Previously absent, though screening is required "at set **and at change**" and forced
change at next sign-in had no endpoint to change through.

---

### `GET /account/credentials` · `DELETE /account/credentials/{id}`

Lists enrolled credentials, each with its state (`active` · `suspended` ·
`invalidated`, AUTH-RECOV-007) and, where suspended, `invalidatesAt`; each carries
`label`, `backupEligible`, `backupState`, `addedAt` and `lastUsedAt` (AUTH-FACT-001);
removes one.

**204**
**202**: `auth.credential.lastsecondfactor`: removing this credential would lower
the account's reachable assurance (AUTH-STEP-006); it is `suspended` now and
invalidated after the window (AUTH-RECOV-007), notified throughout. The body is the
API-CONV-002 body, `details.invalidatesAt` carrying the instant of invalidation.

*Source: D-092, D-141, D-166*

Removal is gated at the account's reachable assurance (AUTH-STEP-002a). Removing one
of several passkeys, or un-enrolling TOTP while a passkey remains, leaves reachable
assurance unchanged and completes immediately. Removing a credential the person no
longer holds is not this endpoint — it is `POST /recovery/report-loss`, which needs
no gate.

---

### `PATCH /account/credentials/{id}`

Changes the credential's **label** and nothing else (AUTH-FACT-001).

```json
{ "label": "..." }
```

**204**
**404**: `auth.credential.notfound` (the account holds no such credential)
**422**: `auth.credential.labelinvalid` (a label of 0 or over 64 characters, or one
already held by another credential of the same kind on the account, in any
capitalisation)

*Source: D-146; AUTH-FACT-001, D-162, D-166*

---

### `POST /account/credentials/{id}/upgrade`

Upgrades a second-factor security key to a **passkey** by re-registration
(AUTH-FACT-002b): opens a WebAuthn registration ceremony for the same authenticator
with `residentKey: required`; on completion the passkey is listed and the second-factor
entry is retired; on failure nothing changes. Gated as an enrolment (AUTH-STEP-007) and
notified like one.

**200**: the ceremony options, completed at `/auth/webauthn/register/complete`
**403**: `auth.stepup.required`
**404**: `auth.credential.notfound` (the account holds no such credential)
**409**: `auth.credential.notupgradable` (the credential is not a second-factor security
key)

*Source: D-146; AUTH-FACT-002b, AUTH-STEP-007, D-162, D-166*

---

### `PUT /account/secondstep/preferred`

Sets the account's **preferred second-step method** from the second factors enrolled
(IDN-ATTR-008). No gate.

```json
{ "method": "..." }        // the id of a second factor enrolled on the account (GET /account credentials)
```

**204**
**422**: `api.request.invalid`, `details.member` `method`, where the method is not
enrolled on the account (API-CONV-003)

*Source: D-146; IDN-ATTR-008, D-166*

---

### `POST /account/factors/totp/begin` · `/confirm`

Enrolment requires one valid code before activation and a **label** (AUTH-FACT-001).
`begin` returns the `otpauth` URI and the Base32 secret (`18` FE-PM-006). Refused on an
account with no password (AUTH-FACT-002b). Gated at the lower of the account's
reachable assurance and AAL2 (AUTH-STEP-007); confirmation is notified to every
recorded channel other than the enrolling session.

*Source: AUTH-FACT-001, AUTH-FACT-002b, AUTH-FACT-007, AUTH-STEP-007, D-146*

---

### `POST /account/recoverycodes`

Generates or **regenerates** the recovery-code set (AUTH-FACT-008, AUTH-RECOV-006).
Step-up action (generate recovery codes, `10` §5a).

**200**: ten codes, returned **once**, with `generatedAt`; the frontend offers copy,
download and print and confirms they were saved. The set records `viewedAt` when this
200 is produced, the one time the codes are shown, and `exportedAt` when the frontend
reports a copy, download or print at `POST /account/recoverycodes/exported`; both are
visible in `GET /account`.
**403**: `auth.stepup.required`
**409**: the account has no password (a passkey-only account has no recovery codes,
AUTH-FACT-002b)

*Source: D-146; AUTH-FACT-008, AUTH-FACT-009, AUTH-RECOV-006, D-162, D-166*

Regeneration invalidates the entire previous set. A reminder fires in the account and
to the security-notice set when `recovery.codes.reminder` has elapsed since generation.
Replaces `POST /account/factors/recovery-codes/generate`.

---

### `POST /account/recoverycodes/exported`

Records that the person copied, downloaded or printed the account's current
recovery-code set (AUTH-FACT-008, AUTH-RECOV-006 AC2). No body. No gate.

**204**
**409**: the account holds no recovery-code set

*Source: AUTH-FACT-008, AUTH-RECOV-006, D-162, D-166*

---

### `POST /account/factors/recovery-codes/generate`

*Retired by D-146. See `POST /account/recoverycodes`.*

---

### `GET /account/sessions` · `DELETE /account/sessions/{id}`

Lists the account's live sessions (AUTH-SESS-013): per session the sign-in time, the
last-use time, the device description, a **city-level location** from a local IP
database (INT-GEN-006, no external lookup) and a marker on the **current** session.
`DELETE` ends one session and its derived tokens; deleting the current session behaves
as logout.

**200** / **204** · **404**: `authz.resource.notfound`, for an `{id}` naming no session of
the account's, another account's included, one answer for both

*Source: D-146; AUTH-SESS-013, INT-GEN-006, D-166*

Trusted devices and remembered browsers are `GET /account/devices`; revoking everything
is `POST /auth/logout` (AUTH-SESS-008).

---

### `GET /account/mail/apppasswords` · `POST /account/mail/apppasswords` · `DELETE /account/mail/apppasswords/{id}`

Mail **app passwords**, managed through the library's first-party OIDC client for the
mail server (REG-MAIL-002, INT-MAIL-010). The library obtains a token for the signed-in
person, makes the server's app-password call and stores **nothing**. Present only where
the account is `active` or `restricted` and holds a mailbox the mail server is told to
enable (INT-MAIL-006); otherwise, and where the deployment registers no mail server,
each answers **404** `identity.mailbox.notfound`. A `restricted` account lists and
revokes its app passwords and creates none (IDN-ACCT-007).

```json
{ "label": "...", "expiresAt": "..." }        // POST; expiresAt optional
```

**200**: `GET`: what the server holds (`id`, `label`, `createdAt`, `expiresAt` where
set), never a cached copy. `POST`: the generated **secret, returned once**, with its
`id`, under `Cache-Control: no-store`.
**204**: `DELETE`: revoked at the server; a client using it fails on its next
connection.
**400**: `api.request.malformed` naming `label` where it is absent
**403**: `auth.stepup.required` (`mailcredential:create`, `mailcredential:revoke`);
`authz.restricted` for a creation by a restricted account
**404**: `identity.mailbox.notfound`; `auth.credential.notfound` where the server holds
no app password of the person under the identifier
**422**: `auth.credential.labelinvalid`

*Source: D-146; REG-MAIL-002, INT-MAIL-010, D-166*

Creation and revocation are notified to the security-notice set and audited; no secret
or hash appears in the library's database, logs or audit records. Ending a membership
disables the mailbox and with it every app password (REG-MAIL-003).

---

### `POST /account/deactivate` · `POST /account/delete`

Both require step-up (`10` §5a); **403** `auth.stepup.required`.

Deactivate moves the account to `suspended` with `suspendedBy = self` (IDN-LIFE-013);
the deactivation notice carries a reactivation link, consumed by
`POST /account/reactivate` (its own entry below, D-147).

**202** — accepted. Delete moves the account to `deleting` and starts the grace
window (`account.deletion.grace`, default 30 days); the deletion notification carries
the cancellation link. This **is** the exercise of the erasure right — no privacy
request is created. When the window elapses, deletion **anonymises** — every field
permitting identification is removed or key-destroyed (PRIV-RIGHT-005); the audit
trail survives.

*Source: IDN-LIFE-013, IDN-LIFE-014, PRIV-RIGHT-005, D-113*

---

### `POST /account/delete/cancel`

Cancels a pending deletion during its grace window and restores the state the account
left: `active`, or `restricted` where a restriction is held (IDN-ACCT-007 AC4). A
`deleting` account cannot sign in, so this **accepts a link token from the deletion
notification**, the same shape as MFA-removal cancellation.

**204**
**422** — `identity.deletion.windowelapsed`
**409** — `identity.takedown.active` — the deletion was a takedown; only
`/admin/accounts/{subject}/takedown/reverse` undoes it (D-137)

*Source: IDN-ACCT-007, D-106, D-137, D-166*

---

### `POST /account/reactivate`

Reverses a self-deactivation. A suspended account cannot sign in, so this **accepts
the link token from the deactivation notice** (the same shape as deletion
cancellation) and restores `active` only where `suspendedBy` is `self`
(IDN-LIFE-013). Not gated (`10` section 5a). Where the notice is lost, the owner uses
ordinary recovery (`/recovery/begin`), which restores `active` on completion (D-140).

```json
{ "linkToken": "..." }
```

**204**
**400**: `api.request.malformed` naming `linkToken` where it is absent
**422**: `identity.reactivation.tokeninvalid` (token unknown, expired or consumed)
**409**: `identity.account.adminsuspended`: the account was suspended by an
administrator and only an administrator reactivates it (D-135)

*Source: D-147; IDN-LIFE-013, D-135, D-140, D-162, D-166*

---

### `GET /account/photo` · `PUT /account/photo` · `DELETE /account/photo`

`GET` returns the caller's own photo as image bytes, **served through the access gate
and never from a public URL** (IDN-ATTR-003 AC3): the response carries no
cache-shareable identifier, and there is no address at which the photo can be fetched
without the session. Staff photos shown in the management application are read
through `GET /admin/accounts/{subject}/photo` (section 8a) under the same rule.

**200** (`GET`): the image, with its content type, `Cache-Control: no-store` and no
entity tag; **404** `identity.photo.notfound` where none is set or the policy does not
enable photos, alike

`PUT` takes the image as the request body; its declared type is ignored. Upload is
validated by content, size- and dimension-limited, and re-encoded by the host's image
codec (IDN-ATTR-004). Availability is the policy field `photos` (`10` section 4.1a),
resolved as AUTH-PRIN-002 resolves every field (IDN-ATTR-002). `DELETE` is not held to
the policy: an account removes its image whatever the policy shows.

**204**
**422**: `identity.photo.invalid`, `identity.photo.toolarge`
**403**: `identity.photo.notenabled`

*Source: IDN-ATTR-002 to IDN-ATTR-004, D-106, D-147, D-162, D-166*

---

### `PUT /account/language`

*Retired by D-146. See `PUT /account/preferences` (language, time zone and the
host-declared preferences in one object, REG-PREF-001).*

---

## 6a. Invitation acknowledgement

### `GET /account/invitation`

Returns the standing invitation (neither acknowledged nor revoked, expired or not) whose
link the signed-in account opened last (REG-INV-001, REG-INV-002).

**200**: `{ "id", "organization", "organizationName", "invitedBy", "roles": [ ... ],
"documents": [ { "document", "version" } ], "expiresAt" }`. `invitedBy` is the
inviter's display name, or their primary email where the account shows none
(REG-PROF-001), or null where neither is readable; `roles` are granted across the
organization.
**404**: `identity.invitation.notfound` when no standing invitation is attached

### `POST /account/invitation/acknowledge`

Attaches the membership for the invitation named in the body, `{ "invitationId": "..." }`
(required; **400** `api.request.malformed` where absent), disables factors the
organization's policy does not permit (IDN-LIFE-009b), makes the corporate address
primary where one exists, enables the pre-provisioned mailbox (REG-MAIL-001) and
records the acknowledgement (document list, versions, timestamp) on the membership;
where a corporate address becomes primary, the personal email of the invitation stays
as a verified non-primary email (REG-MAIL-001). Every live session of the account is
downgraded as the membership attaches (AUTH-SESS-009). Everything the acknowledgement
writes is one transaction; a refusal writes nothing. The refusals that can never be met
(the membership limit, the email maximum) are judged before the credential policy.

**204**
**403**: `authz.restricted` for a restricted account (IDN-ACCT-007);
`auth.stepup.required` with `outcome` `enrol` and `policyRequirement` `{ field, value }`
(AUTH-FACT-017, with no `deadline`: no grace applies to an account joining) when the
account does not satisfy `requiredAssurance` or `credentialRedundancy` of the policy in
force once the membership attaches, counting only credentials whose factor that
policy's `loginFactors` permits (REG-INV-002)
**404**: `identity.invitation.notfound` where the invitation does not exist or is
attached to another account
**409**: `identity.membership.limitreached` where `organization.multiplememberships`
leaves no room (IDN-MEM-002); `identity.identifier.maximum` where the corporate address
would pass `identifiers.email.max`
**422**: `identity.invitation.expired` where it was revoked or acknowledged, is past
`expiresAt`, its organization's deletion was requested or it was erased, or its inviter
no longer holds what issuing it required (REG-INV-001);
`identity.invitation.identifiermismatch` (a bound email or phone is not verified on the
account accepting, or the corporate address is already held by an account);
`identity.identifier.domainnotallowed` where the organization's lock is on and the
address the member will sign in with is outside it (REG-DOM-001)

Emits `MembershipChanged` and, where a corporate address is taken on,
`IdentifierAdded` and `IdentifierPrimaryChanged`.

*Source: D-148; D-146, REG-INV-001, REG-INV-002, REG-MAIL-001, D-166*

---

## 7. Privacy

### `GET /privacy/consents`

**200**: every consent record held for the subject, a withdrawn or superseded one
included, each grant a record of its own: `{ purpose, document, noticeVersion,
mechanism, grantedAt, withdrawnAt, supersededAt }` (objections: `recordedAt` for
`grantedAt`, and no `supersededAt`) (D-153). `document` is the legal document the
consent was given against (the privacy notice where the purpose names none) and
`noticeVersion` the version of `document`. `supersededAt` is the instant a material
revision (PRIV-CONS-007) ended the consent, or null; a consent carrying it is asked for
again (PRIV-CONS-007 AC4).

*Source: PRIV-CONS-011, PRIV-CONS-001, D-166*

---

### `POST /privacy/consents/{purpose}/grant` · `POST /privacy/consents/{purpose}/withdraw`

**204** · **422** `privacy.purpose.noconsent` · **409** `privacy.notice.unpublished`
(grant only). A withdrawal where the subject holds no consent for the purpose answers
204 and records nothing.

*Source: PRIV-CONS-008, PRIV-CONS-011, D-135, D-162, D-166*

Grant records the current version of the document the purpose's consent is governed
by and the consent kind, and the mechanism `reconsent` where the subject holds a
superseded, unwithdrawn consent for the purpose, `dashboard` otherwise; the operations
contract applies the same rule to a grant named `dashboard` and records any other
mechanism as named. It is how a subject opts in after registration or re-consents after
a notice revision (PRIV-CONS-007). Withdrawal: no confirmation interstitial, no
retention flow. Both take effect without human approval.

---

### `GET /privacy/objections` · `POST /privacy/objections/{purpose}` · `DELETE /privacy/objections/{purpose}`

Objection — for purposes whose declared basis is objectable (PRIV-BASIS-001).
`GET` lists every objection record held for the subject, a withdrawn one included, each
recorded objection a record of its own (purpose, `document`, notice version, timestamp,
withdrawal state). `POST` records an objection and raises `ObjectionChanged`;
`DELETE` withdraws it. Both take effect without human approval; no grounds are asked.

**204**
**422**: `privacy.purpose.notobjectable`
**409**: `privacy.notice.unpublished` (`POST` only). A `DELETE` where no objection is
held answers 204 and records nothing.

*Source: PRIV-RIGHT-001a, D-145, D-162, D-166*

---

### `GET /privacy/notice?version=...` · `GET /privacy/documents/{document}?version=...`

**200**: the document version: its **governing language**, the governing-language
text, and every **translation** attached to it, each named by language
(PRIV-CONS-005, PRIV-CONS-006). Without `version`, the current version. `/privacy/notice`
is the privacy notice; `/privacy/documents/{document}` is any other legal document the
host publishes (terms of service, consent texts).

```json
{
  "document": "privacy-notice",
  "version": "...",
  "governingLanguage": "ar",
  "text": "...",
  "translations": [ { "language": "en", "text": "..." } ]
}
```

**404**: `privacy.document.notfound`, the document, or the named version, was never
published.

*Source: PRIV-CONS-005, PRIV-CONS-006, D-146, D-162, D-166*

Public, unauthenticated. The governing text is always returned with the translations,
so a screen can show either without changing the interface language (PRIV-CONS-005
AC3); a translation is never served as if it governed.

---

### `POST /privacy/requests`

Creates a data subject request.

```json
{ "type": "restriction|rectification", "detail": "..." }
```

Erasure is not a request type here — a signed-in customer exercises it with
`POST /account/delete`. Erasure requests arriving out of band are entered by an
authorised human through `POST /admin/privacy/requests` (§8a). Access and
portability are the self-service export.

**202**
```json
{ "requestId": "...", "receiptSentAt": "...", "decisionDue": "..." }
```

*Source: PRIV-RIGHT-001, PRIV-RIGHT-002, D-113, D-126*

`receiptSentAt` equals creation time; `decisionDue` is `privacy.request.decision` (six
working days) after **submission** — creation time for an in-app request — computed
on the deployment's working-day calendar (`privacy.workingdays`, `privacy.holidays`).
It is the statutory decision deadline, after which lapse is a deemed rejection
(PRIV-RIGHT-002, D-136).

Access and portability are the self-service export; objection completes immediately
through `POST /privacy/objections/{purpose}` (PRIV-RIGHT-001a). **Rectification of editable data** is
account editing, and a request here while the account is `restricted` (IDN-ACCT-007,
D-166); **rectification of non-editable data** (the recorded details of a
host business record, say) is a request here, decided under the six-working-day rule
like restriction.
Restriction enters the request queue; erasure reaches it only through
`POST /admin/privacy/requests` (out of band).

---

### `GET /privacy/export?format=human|machine`

**200** — one export routine, two formats, **both structured data**.

**Requires step-up at the account's reachable assurance** (AUTH-STEP-002a — never
phishing-resistant under the system policy), and is **rate-limited**.

The limit is `privacy.export.ratelimit` exports in the 24 hours ending at the request.
Past it the answer is **429** `auth.throttled` with `Retry-After` and `details.retryAt`,
the instant the oldest counted export leaves the window; a request at that instant is
served.

`format` is required and is exactly `human` or `machine`, case-sensitive. A request
without it, or with any other value, is **400** `api.request.malformed` naming `format`;
nothing is assembled.

*Source: D-086, D-141, D-162, D-166*

Without it, anyone holding a live session (a borrowed phone, a shared computer)
pulled a complete export, sensitive records included, in one request, from a session that
can persist while used (AUTH-SESS-005). That is the threat model's central attacker, who needs one
successful lookup.

D-067's argument for leaving it open — that gating a legal right behind a second
factor locks out those least able to work around it — remains valid against a
**phishing-resistant** requirement only. A gate at the account's own reachable
assurance asks for nothing the customer cannot present; and one who has lost a
factor reports it and is not refused (AUTH-STEP-002 step 3).

The staff bulk-export controls in D-045 remain scoped to staff and do not apply here.

`human` is grouped, labelled and ordered for reading; `machine` is flat, complete and
stable-named. Each membership carries `acknowledgedAt` where an invitation attached it,
and the section `membership-acknowledgements` holds one record per document
acknowledged: `membership`, `document`, `version`, `acknowledgedAt` (REG-INV-001 AC3);
the sections and their order are PRIV-RIGHT-003's. **Neither is a rendered document.**
The frontend renders the readable view and localizes it through the frontend
localization library.

No server-generated PDF. A rendered page satisfies the obligation, browsers print to
PDF anyway, and a downloadable file of health data is a larger exposure than a page
read while authenticated.

*Source: PRIV-RIGHT-003, D-054, D-058*

---

## 8. Administration

All endpoints under `/admin` require the corresponding permission. Denials return
403 — no record existence is concealed at this level.

*Source: AUTHZ-CONCEAL-005, D-162, D-166*

Where the organization a permission is asked in is read from the row a request names (a
group, a grant, a registered record), an identifier naming no row SHALL be refused
**403** `authz.denied` exactly as a missing permission: the row belongs to no
organization, so no grant reaches it (AUTHZ-SCOPE-001). The refusal carries
`correlationId` and is recorded against no organization. What a row answers of itself
(**404**, **409**) is told only to a caller the permission check admitted. Where the
permission is asked in the administrative organization or in the organization the path
names, a path naming a runtime record the deployment does not hold is answered **404**
with its named code once the permission check has admitted the caller (API-CONV-003).
A path whose organization `{id}` names no organization the deployment holds is answered
**404** `identity.organization.notfound`, whichever organization the permission is asked
in (D-166). An operation's step-up is judged after every other refusal it can give.

### `GET /admin/access?resourceType=...&resourceId=...`

**200** — who can access this resource, and through which grant or container.

Stored and derived grants are reported **distinctly**. Where declared derivations
make the answer unbounded, the response states the limitation rather than returning
a partial answer silently: when evaluation exceeds `authz.reverselookup.budget` the
response carries `partial: true` and `unevaluated`, the derivations not evaluated
(AUTHZ-DERIVE-007, D-153).

Asked under `grant:read` in the organization the record sits in; `resourceType`
`organization` with the organization's identifier asks for the whole of it. **200**
`{ resource: { resourceType, resourceId }, grants: [ { id, kind, subjectType, subjectId,
role, deny, inheritedFrom } ], partial, unevaluated }`: grants on the record, then on
each container nearest first, then on the whole organization; a derived grant carries no
`id`; a grant whose role allows nothing is not reported. Derivations that are not
materialised are evaluated over the rows the host declares for each relationship
(`07` LIB-HOST-001, relationship sources).

**400**: `api.request.malformed` naming `resourceType` where the model declares no such
type, or `resourceId` where the type is `organization` and the identifier is not a UUID
**403**: `authz.denied` without `grant:read` in the organization the record belongs to,
and for a record the deployment holds no registration for, which belongs to no
organization (AUTHZ-SCOPE-001): one answer for both, recorded and counted as every
refusal is

*Source: AUTHZ-GATE-004, AUTHZ-DERIVE-007, D-166*

---

### `POST /admin/grants` · `DELETE /admin/grants/{id}`

```json
{
  "subjectType": "user|group",
  "subjectId": "...",
  "resourceType": "...",
  "resourceId": "...",
  "role": "...",
  "deny": false,
  "expiresAt": "...",
  "reason": "..."
}
```

A grant on the whole organization is written `resourceType` `organization` with
`resourceId` the organization's identifier, and is stored with no resource
(AUTHZ-GRANT-001 AC2). `grant:manage` is asked in the organization the grant is scoped
to: the one the record was registered in, or the one named. `reason` is recorded.
Revocation records the revoking actor; `DELETE` takes `{ "reason": "..." }`. A grant or
revocation of a role that carries `system:administer` also requires `system:administer`
in the administrative organization (OPS-CFG-007). Nothing is granted to the reserved
`emergency` account, and its `system-administrator` grant is not revoked (OPS-BOOT-002).

**201**: `{ "id": "..." }` · **204**
**400**: `api.request.malformed` naming `reason` where it is longer than 1024 characters
after trimming
**403**: `authz.denied` where `grant:manage` is not held in the grant's organization,
where the identifier names no grant or no registered record (section 8), where a role
carrying `system:administer` is granted or revoked without it, and for a grant to the
reserved account or a revocation of its `system-administrator` grant;
`auth.stepup.required` (`grant:manage`)
**404**: `authz.grant.notfound`, to a caller holding `grant:manage` there, for a grant
already revoked, or one a derivation wrote or materialised
**409**: `authz.grant.duplicate`; `authz.grant.expired` where `expiresAt` is not after
the present
**422**: `authz.grant.reasonrequired`; `authz.grant.unresolved` where `role` names no role
the deployment holds, or `subjectId` names a group that does not exist or belongs to
another organization (`details.member`)

*Source: AUTHZ-GRANT-001, AUTHZ-GRANT-003, D-166*

---

### `GET /admin/grants?organization=...&subjectType=user|group&subjectId=...`

The live grants the user or group holds in its own name in the organization, oldest
first; a grant reaching a user through a group is read under the group. Asked under
`grant:read` in that organization.

**200**: `[ { id, kind, subjectType, subjectId, resourceType, resourceId, role, deny,
expiresAt, grantedBy, grantedAt, reason } ]`; an organization-wide grant reads as
`resourceType` `organization` with the organization's identifier; `kind` `materialised`
marks a grant a revocation refuses
**400**: `api.request.malformed` naming the member absent or not well formed
**403**: `authz.denied`

*Source: AUTHZ-GRANT-003 AC3, `16` section 3 step 4, D-166*

---

### `GET|POST|DELETE /admin/roles`

Runtime role management, under `role:manage` in the administrative organization for
reading and changing alike. Every change carries `reason`, recorded, and is the
`grant:manage` step-up action. A change to a role that carries `system:administer`
before or after it requires that permission in the administrative organization. A
change that would take a library-owned permission out of the role the reserved
`emergency` account holds is refused (OPS-BOOT-002).

`GET /admin/roles`: **200** `[ { name, permissions } ]`.
`POST /admin/roles` `{ "name": "...", "permissions": [ "..." ], "reason": "..." }`:
**201** creates the role; **204** gives the role of that name the permissions stated.
`DELETE /admin/roles/{name}` `{ "reason": "..." }`: **204**.

**400**: `api.request.malformed` naming `name` where it is not a role name,
`permissions` where one is declared neither by the library nor by the host
(AUTHZ-MODEL-004), `reason` where it is absent, blank or longer than 1024 characters
after trimming
**403**: `authz.denied` without `role:manage`, without `system:administer` for a role
that carries it, or for a change taking a library-owned permission out of the role the
reserved account holds; `auth.stepup.required` (`grant:manage`)
**404**: `authz.role.notfound` where `DELETE` names a role the deployment does not hold
**409**: `authz.role.inuse` for a `DELETE` of a role a grant (live, expired or revoked),
a declared derivation or a standing invitation names

*Source: AUTHZ-GRANT-004, OPS-CFG-007, OPS-BOOT-002, D-166*

---

### `POST /auth/break-glass`

Presents the sealed emergency credential. **Mounted on the machine profile**
(BFF-MACH-001): no session, outside the session-bound CSRF layer, source-throttled.
**Any cookie present is ignored rather than refused**, since this is reached from a
browser that may hold a stale session for the domain.

```json
{ "credential": "...", "reason": "..." }
```

`reason` is required: the owner's words, trimmed, at most 1024 characters; absent or
blank is **400** `api.request.malformed` naming `reason`. It is kept with the session and
recorded on every audit record the session writes (OPS-BOOT-002).

**200** — a time-boxed **auth session** (`breakglass.session.lifetime`) whose subject
is the reserved `emergency` account (D-138), which holds the `system-administrator`
role; the audit trail shows `emergency` as the actor, and every use alerts the owner
destinations (D-129, D-147). The applications open from it as from any sign-in
(BFF-SESS-006)
**400**: `api.request.malformed` naming `reason`
**422**: `auth.breakglass.invalid`, for a code that is not 36 symbols of the alphabet
once hyphens and spaces are set aside, whose groups do not all hold their check symbol,
or that matches no issue but the one last used, a replaced issue included; never 401
(session death only, API-CONV-003)
**409**: `auth.breakglass.consumed`, for the code of the issue last used, including the
second of two concurrent uses
**429**: `auth.throttled` with `retryAt`: per source under AUTH-ABUSE-001 and, in
addition, at most 5 attempts per hour globally across all sources, every arrival
counted, a refused one included, and the global refusal's `retryAt` one hour after the
refused attempt (OPS-BOOT-004, D-147)

*Source: D-065, D-129, D-138, D-147; OPS-BOOT-004, D-166*

Called by the frontend route `/break-glass` (FE-BG-001), which is what the envelope
names; the owner never calls this endpoint directly.

*Source: D-065, OPS-BOOT-002, D-129*

Use is single-use and consumes the credential. It raises maximum-noise alerts on
every available channel immediately, **to the owner as well as the operator**. The
session satisfies step-up for its lifetime (AUTH-STEP-004).

---

### `POST /admin/break-glass/generate`

Generates the break-glass credential — first issue or replacement; a previous one is
invalidated. Available to a stepped-up system administrator **and to a break-glass
session itself**, so the owner can leave a fresh credential before the session
expires. The response feeds the one-time printable page (OPS-BOOT-004); the
credential is returned exactly once and never stored in plaintext.

**200**: `{ "credential": "...", "address": "...", "issuedAt": "..." }`, once.
`address` is absolute: the origin of the authentication application's declared address
(`AuthenticationAddresses.provider`, LIB-HOST-001) followed by `/break-glass` (FE-BG-001).
It is composed from the declaration and never from the request.
**403**: `auth.stepup.required` (`breakglass:replace`); `authz.denied` without
`system:administer`

*Source: D-065, D-133, D-166*

Without this, one emergency exhausts the mechanism until the operator returns — which
is the scenario it exists for.

Every generation is audited and raises `breakglass-generated`, to the owner whatever
`alerting.owner.enabled` holds (OPS-BOOT-004 AC2).

---

### `GET /admin/break-glass`

Whether a break-glass credential stands: generated, and neither used nor replaced. Read
by the management application for a holder of `system:administer`, which shows the
non-dismissable alert of OPS-BOOT-001 AC3 while none stands. No step-up. Nothing of the
credential or its hash is answered.

**200**: `{ "standing": true, "issuedAt": "..." }` or `{ "standing": false, "issuedAt": null }`
**403**: `authz.denied`

*Source: OPS-BOOT-001, D-133, D-166*

---

### `POST /admin/accounts/{subject}/sessions/revoke`

Terminates every session and derived token for **one account**. Under
`session:revoke-account`; it ends another person's sessions, so it is the step-up action
`account:sessionsrevoke`.

**204**
**403**: `authz.denied`; `auth.stepup.required` (`account:sessionsrevoke`)
**404**: `identity.account.notfound` where no account bears the subject

*Source: D-077, D-166*

Required by offboarding (`16-offboarding-procedure` step 1), which previously had only
the system-wide operation available — so signing out one departing clerk would have
signed out every customer.

---

### `POST /admin/sessions/revoke-all`

**204**: the explicit emergency revocation. Under `session:revoke`; it ends every
session, so it is the step-up action `session:revokeall`.
**403**: `authz.denied`; `auth.stepup.required` (`session:revokeall`)

*Source: AUTH-SESS-009, D-166*

Distinct from the automatic downgrade that follows a policy tightening.

---

### `GET|PUT /admin/config/{key}`

`GET` returns `{ key, value, default, protected, direction }` (D-153).
`value` and `default` are written in the key's own JSON type: a duration, text or enum
member as a string in the form `10` section 4 writes it, a boolean, a number, a list or
set as an array, a policy as its object. `default` is null for a key the deployment
names (LIB-HOST-001). `direction` is `increase` · `decrease` · `any-change`: which way a
change to the key loosens (`10` section 4, Direction). A `value` of another JSON type is
refused with `config.value.notallowed`.
`PUT` sets it:

```json
{ "value": ..., "reason": "..." }
```

`value` takes the key's type (`10` section 4); `reason` is required on every change,
a tightening included, and recorded in the audit entry (OPS-CFG-005, OPS-CFG-008;
D-147, D-166).

The route serves every key of `10` section 4 that exists once for the deployment except
`restrictions` (served by `/admin/restrictions`), and every `retention.<host-category>` of a
declared category (read with its floor as `default` where no value is written;
shortening loosens). `policy.<organization>` is served by
`/admin/organizations/{id}/policy`. A protected key (OPS-CFG-004, `10` section 4.8) is
read with `protected: true` and refused on `PUT`. A `policy.default` whose `photos` is
`true` while the host declares no image codec is refused `config.value.notallowed`,
`details.field` `photos`, `details.requires` `imageCodec`; one that names a domain in
`emailDomains` is refused `config.value.notallowed`, `details.field` `emailDomains`
(the system policy's lock is always `off`).

**200** / **204**
**400**: `api.request.malformed` naming `key` where `{key}` is none of the keys the route
serves; naming `reason` where it is longer than 1024 characters after trimming
**403**: `auth.stepup.required`, for a **loosening** change (`config:loosen`) and for
any change of an `alerting.*.destinations` key (`alerting:destinations`); `authz.denied`
for a loosening by a caller without `system:administer` in the administrative
organization
**422**: `config.value.belowfloor`, `config.value.aboveceiling`,
`config.value.notallowed`, `config.key.protected`; `config.change.reasonrequired`,
`details.key` naming the key, where a change arrives without a reason or with a blank one

*Source: OPS-CFG-002, OPS-CFG-003, OPS-CFG-004, D-147, D-166*

Every change carries a reason and produces an audit entry. Tightening requires no
step-up; loosening requires step-up and `system:administer`. Protected keys (OPS-CFG-004)
are rejected: they are not changeable through the application.

---

### `GET /admin/restrictions` · `GET|PUT|DELETE /admin/restrictions/{name}`

The **named restriction set** that governs every send (AUTH-ABUSE-004): runtime
configuration edited like the holiday list (D-142). `GET` lists the restrictions with
their keys, purposes, channels and buckets, the shipped defaults included, each with
the channel it carries (`sms.destination` and `sms.source` `sms`, `email.destination`
`email`, `notification.destination` `any`).

```json
{
  "key": "destination",                       // destination | account | source | global | host:<name>
  "purpose": "any",                           // verification | signin | secondfactor | notification | any
  "channel": "email",                         // sms | email | any
  "buckets": [ { "max": 5, "interval": "PT1H", "window": "sliding" },
               { "max": 1, "interval": "PT60S", "window": "fixed" } ],
  "reason": "..."
}
```

`purpose` and `channel` are optional, and an absent one reads as `any`. A restriction's
name is 1 to 64 lower-case letters and digits separated by single `.`, `-` or `_`; any
other is refused `config.value.notallowed`. `reason` is required on every edit, a
tightening included (OPS-CFG-008); `DELETE` takes `{ "reason": "..." }`. A **loosening**
is any change `10` section 4.5 does not classify as a tightening (a higher max, a
shorter interval, a removed bucket, a deleted or renamed restriction, a change of
`channel` to anything but `any`, among others); it requires `system:administer` and
raises a Normal alert (OPS-ALERT-001).

**200** / **204**
**400**: `api.request.malformed` naming `key`, `purpose`, `channel` or `buckets` where a
key, purpose, channel or window is outside `10` section 5, a `max` is negative or an
`interval` is not a positive ISO 8601 duration; naming `reason` where it is longer than
1024 characters after trimming
**403**: `auth.stepup.required` (`restriction:edit`, every edit); `authz.denied` for a
loosening by a caller without `system:administer` in the administrative organization
**404**: `auth.restriction.notfound` where `{name}` is not in the set (`GET`, `DELETE`,
and `POST /admin/restrictions/{name}/grant`)
**422**: `config.value.notallowed`, `details.key` `restrictions`, for a `host:<name>` key
with no registered supplier (LIB-HOST-001, `details.supplier` naming it) or an empty
bucket list; `config.change.reasonrequired` where an edit arrives without a reason or
with a blank one

*Source: D-146; AUTH-ABUSE-004, OPS-CFG-002, OPS-CFG-008, D-162, D-166*

A change applies to the next send without a restart, is audited and emits
`SendingRestrictionChanged`. Deleting a shipped default is a loosening, not an error.

---

### `POST /admin/restrictions/{name}/grant`

Grants **credit** to one key under a restriction: a support action for a person whose
address has been exhausted by a loop or an attacker, never a bypass.

```json
{ "keyValue": "...", "credit": 3, "reason": "..." }
```

**204**: credit added; audited with the reason; emits `SendingRestrictionGranted`; raises a
Normal alert (OPS-ALERT-001)
**403**: `auth.stepup.required` (`restriction:grant`, the support role)
**404**: `auth.restriction.notfound` where `{name}` is not in the set
**422**: `config.change.reasonrequired` where `reason` is absent or blank;
`config.value.notallowed` for a `credit` at or below zero

*Source: D-146; AUTH-ABUSE-004, D-166*

`keyValue` is the plain address, account, source or host value; the library derives
the stored key (the HMAC for a destination) and never returns it. A granted key is
refused again once the credit is spent.

---

### `GET /admin/ropa?format=template`

**200** — generated records of processing in the regulator's template shape.

*Source: PRIV-ROPA-001, D-036, D-166*

Flags any purpose lacking a required assessment reference, and any processor lacking
an agreement reference. `format` is required and is exactly `template`,
case-sensitive. A request without it, or with any other value, is **400**
`api.request.malformed` naming `format`.

---

## 8a. Administration — operations previously without a surface

Every endpoint below is a rendering of an operation in the **operations contract**
(LIB-API-005) — the same service a host calls in-process. Neither is written
separately. Permissions are those in `10-reference` §2.1; step-up applies where the
operation loosens a control (OPS-CFG-002) or touches another person's account, and
`10` section 5a names the gate of each; each row below that requires step-up names its
gate. Not gated, each for its reason: revoking an invitation (it touches no account),
creating an organization, publishing documents and translations, the compliance
records, and refusing a privacy request.

*Source: D-106, D-166*

### Organizations — `organization:manage`

`organization:manage` and `domain:manage` are asked in the administrative organization,
whatever organization the path names. **400** `api.request.malformed` names `name` or
`reason` where it is absent, or blank or past 1024 characters after trimming (a
configuration change's missing reason excepted, below). **404**
`identity.organization.notfound` where `{id}` names no organization the deployment holds
(API-CONV-003).

| Endpoint | Does |
|---|---|
| `POST /admin/organizations` | `{ name, reason }`: creates an organization and writes its policy key `policy.<organization>` as `{}` in the same transaction, recorded as a configuration change (OPS-CFG-005) and as `identity.organization.created`. Not gated. **201** `{ id }` (IDN-ORG-002). **422** `identity.identifier.mixedscript` for a name that mixes scripts within a word, judged on its comparison key (IDN-ACCT-005) |
| `PUT /admin/organizations/{id}/policy` | Replaces the organization's policy overrides: the policy object of `10` §4.1a without `emailDomains` (`requiredAssurance`, `loginFactors`, `gates`, `credentialRedundancy`, `selfServiceRecovery`, `photos`), and `reason`. Omitted fields inherit `policy.default`; the domain lock in force is kept. Every replacement, a tightening included, is the `policy:change` step-up action and carries `reason` (**422** `config.change.reasonrequired`, `details.key` `policy.<organization>`, where it is absent or blank). A replacement under which the organization's members resolve to a policy granting more on any field, by the direction column of `10` §4.1a, is a loosening (OPS-CFG-002) and also requires `system:administer` (**403** `authz.denied`, before the step-up). **422** `config.policy.belowsystem` where a stated field is looser than the system default, `details.field` naming the first in the order of `10` §4.1a (AUTH-STEP-002a); **422** `config.value.belowfloor`, `details.field` `requiredAssurance`, where the administrative organization's members would resolve below `aal2` (AUTH-SESS-005b); **422** `config.value.notallowed` for a value a field does not take, and for `photos` `true` while the host declares no image codec (`details.field` `photos`, `details.requires` `imageCodec`); **400** `api.request.malformed` for a body that is no object (the code alone) or a member the object does not have, `emailDomains` included (naming it), refused at the endpoint before the service. **204**. `GET` returns the resolved policy: **200** `{ requiredAssurance, loginFactors, gates, credentialRedundancy, selfServiceRecovery, photos, emailDomains }`, each field `{ value, overridden }`, `gates` an object keyed by the `10` §5a action name whose members are `{ value: { level, phishingResistant, maxAge }, overridden }`; `overridden` is true where the organization states the field or the action and the value in force is its own, and false where the system policy's value is in force; `emailDomains` is a list of domains in their ASCII form, and `off` is the empty list (D-143) |
| `POST /admin/organizations/{id}/delete` · `/delete/cancel` | `{ reason }`: request, suspend, grace, erasure, cancellable (IDN-ORG-003). The request ends every session of every current member in the same transaction, and the organization's grants confer nothing until it is cancelled. Each requires step-up (`organization:delete`) and is recorded (`identity.organization.deletionrequested`, `identity.organization.deletioncancelled`). **204**, also for a request on an organization already being deleted and a cancellation where none is pending, which change and record nothing. **409** `identity.organization.protected` for the administrative organization. **422** `identity.deletion.windowelapsed` for a cancellation at or after `organization.deletion.grace` from the request, or after the erasure, whether or not the erasure has run. The erasure erases no account |
| `POST /admin/organizations/{id}/domains` · `POST /admin/organizations/{id}/domains/{domain}/verify` · `DELETE /admin/organizations/{id}/domains/{domain}` | Domain lock (REG-DOM-001, IDN-ORG-006): adds a domain to the policy field `emailDomains`, verifies it by the DNS TXT record the add response names (one TXT value equal to the record value, compared exactly; a lookup that cannot be made proves nothing; **422** `identity.domain.unverified`, nothing written; **404** `identity.domain.notfound` for a domain not listed, never listed or removed; verifying a verified domain or adding a listed one answers it as it stands and changes nothing; removing an unlisted one is **204** and changes nothing), removes it. A domain that does not read as a domain is **400** `api.request.malformed` naming `domain`. Every change is the `domain:manage` step-up action and carries `reason` (**422** `config.change.reasonrequired` where it is absent or blank), and is recorded. Adding and verifying a domain are loosenings, and so is a removal that leaves the list empty (OPS-CFG-002): each also requires `system:administer` (**403** `authz.denied`). Adding a domain while the host registers no DNS resolver is refused **422** `config.value.notallowed`, `details.requires` `dnsResolver`. The lock is on from the first domain listed, verified or not, and while no listed domain is verified it admits no address. Removal stops new sign-ins with addresses in the domain until it is listed anew, whether or not the list holds another domain, and raises an alert (OPS-ALERT-001). Re-verification runs every `domain.reverify.interval` on the sweep; a failure alerts and revokes nothing. The record is `_identity-verify.<domain>` TXT with value `identity-domain-verification=<32 random bytes, base64url>`, one token per organization and domain, never reused (D-153). `GET` answers an array of `{ domain, recordName, recordValue, addedAt, verifiedAt, checkedAt, lastCheckPassed }`, the domain in its ASCII form (REG-DOM-001). `POST .../domains` takes `{ domain, reason }` and answers **201** with one such object; `POST .../{domain}/verify` takes `{ reason }` and answers **200** with it; `DELETE .../{domain}` takes `{ reason }` and answers **204**. Each change writes `identity.organization.domainadded`, `.domainverified` or `.domainremoved` with details `{ domain, reason }`; each change to the list is also a configuration change of `policy.<organization>` (OPS-CFG-005) |

### Memberships and invitations — `membership:manage`

`membership:manage` is asked in the organization the path names (IDN-MEM-001). **404**
`identity.organization.notfound` where `{id}` names no organization the deployment holds
(section 8) (D-166).

| Endpoint | Does |
|---|---|
| `POST /admin/organizations/{id}/invitations` | Issues a time-boxed, single-use enrolment link (IDN-LIFE-009a). The body members are `email`, `phone`, `corporateEmail`, `roles` and `documents`, and `formerMailbox` with `reason` where a mailbox is taken over (below). MAY bind `email`, `phone`, both or neither (REG-INV-001); a bound identifier is pre-filled and locked at registration, and a bound phone is verified before the membership step. Where the mail server is integrated, the invitation names a personal `email` (required; the link goes to it and its press verifies it, so the account is created with a verified personal email) and a `corporateEmail`, asserted, whose mailbox is provisioned disabled (REG-MAIL-001, D-148); `corporateEmail` where the mail is not integrated is **400** `api.request.malformed`. `roles` (optional; none where absent) names roles granted across the organization when the membership attaches; naming a role also requires `grant:manage` in the organization and the `grant:manage` gate, and a role carrying `system:administer` also requires `system:administer` in the administrative organization (OPS-CFG-007). `documents` (optional; none where absent) names legal documents, each fixed at the version published when the invitation is issued, which the membership step shows and the acknowledgement records. Requires step-up (`invitation:issue`). The link is sent to the bound email only, never to the bound phone; its send is written in the transaction that issues the invitation and first attempted once that transaction commits (AUTH-ABUSE-004), and a send a restriction refuses issues nothing and answers **429** `auth.restriction.exceeded` with `retryAt`. A corporate address whose mailbox was held before, by anyone, the person invited included, never passes silently: the issue is refused **409** `identity.invitation.mailboxheld` unless the body carries `formerMailbox` (`10` section 5.44), `transfer` (the invitee receives the mailbox with its mail) or `replace` (the old mailbox is removed and a new one reserved), with a `reason`; either is stepped up with the issue and audited. The issue makes no check of who the invitee is, since issuing tells nothing about accounts; the mail-server adapter's adoption of an existing server account is REG-MAIL-003's (D-166). An organization whose deletion was requested takes no invitation: its grants confer nothing, so issuing answers **403** `authz.denied`. **201** `{ id, expiresAt, token }`: `token` is the link's token where no email is bound, answered once for the administrator to hand over and never shown again, and `null` wherever the link was sent; only its fingerprint is kept (IDN-LIFE-009a). **409** `identity.mailbox.taken` (`details.member` `corporateEmail`) where the corporate address is held by a member or reserved by a standing invitation that has not expired; an expired one is revoked in the issuing transaction and its mailbox kept. **422** `identity.invitation.addressrequired`, `details.member` naming `email` or `corporateEmail`, for an integrated-mail invitation without a personal `email`, without a `corporateEmail`, or naming the corporate address as the personal one; **422** `identity.identifier.domainnotallowed` where the address the member will sign in with (the corporate address where the mail is integrated, the bound email otherwise) is outside the organization's lock (REG-DOM-001); `identity.identifier.mixedscript` and `identity.identifier.invalid` carry `details.member` naming `email`, `phone` or `corporateEmail`; **422** `authz.grant.unresolved` (`details.member` `roles`) for a role the deployment does not hold; **422** `api.request.invalid` (`details.member` `documents`) for a document never published. The identifier-mismatch refusal belongs to acceptance (`POST /account/invitation/acknowledge`), not to issue |
| `DELETE /admin/organizations/{id}/invitations/{invitationId}` | Revokes an invitation not yet acknowledged, whether or not its link was opened; what it bound is forgotten and a mailbox nobody ever held is released (REG-MAIL-001). **204**, also for one already revoked. **404** `identity.invitation.notfound` where the organization issued no such invitation; **422** `identity.invitation.expired` where it was acknowledged. Not a step-up action |
| `DELETE /admin/organizations/{id}/memberships/{subject}` | Ends a membership; the account and organization persist (IDN-MEM-001), and the account's grants in the organization are not removed (`16` section 3 step 4); a grant in the administrative organization confers only while its holder holds a current membership there (IDN-MEM-001). Requires step-up (`membership:end`). **204** · **404** `identity.membership.notfound` where the account holds no current membership of the organization, a second end included. Recorded as `identity.membership.ended`; emits `MembershipChanged`, and `IdentifierRemoved` where the end retires the corporate address (REG-MAIL-003) |

### Groups — `group:manage`

| Endpoint | Does |
|---|---|
| `GET /admin/groups?organization={id}` | The organization's groups, each with the members it holds directly: **200** `[ { id, name, members: [ { subjectType, subjectId } ] } ]`. Membership is transitive (AUTHZ-GROUP-001) |
| `POST /admin/groups` | `{ organization, name, reason }`: creates a group in the organization. **201** `{ id }` |
| `DELETE /admin/groups/{id}` | `{ reason }` in the body: removes a group nothing names. **204**. **409** `authz.group.inuse` where the group holds a member, belongs to a group, or was given a grant, live, expired or revoked |
| `POST /admin/groups/{id}/members` · `DELETE /admin/groups/{id}/members` | `{ subjectType, subjectId, reason }`: adds or removes an account or a group of the group's own organization. **204**, also where the member is already held or already absent, which changes and records nothing. **409** `authz.group.cycle`. Requires step-up (`grant:manage`); where the group, or a group holding it, holds a live grant (allow or deny) of a role carrying `system:administer` or of a role that cannot be read, also requires `system:administer` in the administrative organization (OPS-CFG-007) |

`group:manage` is asked in the organization the group belongs to, read from the group's
row, or for `GET` and `POST` in the organization the request names (AUTHZ-SCOPE-001). An
`{id}` that names no group is refused **403** `authz.denied`, as a missing permission is
(section 8). **400** `api.request.malformed` names `organization`, `name`, `reason`,
`subjectType` or `subjectId` where it is absent or unreadable or, for free text, blank or
past 1024 characters after trimming. **422** `api.request.invalid`
(`details.member` `subjectId`) where a member group does not exist or belongs to another
organization (API-CONV-003). The reserved `emergency` account is refused as a member,
**403** `authz.denied` (OPS-BOOT-002). Every change carries its reason and is recorded
(AUTHZ-GROUP-001) (D-166).

### Accounts — `account:manage`

| Endpoint | Does |
|---|---|
| `POST /admin/accounts/{subject}/suspend` · `/reactivate` | Suspension ends sessions in the same operation (AUTH-SESS-010); reactivation restores grants exactly (IDN-LIFE-013), and returns an account suspended while `restricted` to `restricted`. Suspending an account its owner deactivated makes it an administrator's suspension. Requires step-up (`account:suspend`, `account:reactivate`). **204**; suspending an account an administrator already suspended changes nothing and answers **204**. **404** `identity.account.notfound` where no account bears the subject. **409** `identity.account.stateconflict` (`details.state`, and `details.suspendedBy` where it is `suspended`) where the account is `deleting` or `deleted` (suspend) or is not suspended by an administrator (reactivate), an account its owner deactivated included |
| `POST /admin/accounts/{subject}/restriction/lift` | Lifts a processing restriction (PRIV-RIGHT-004): the account returns to `active` and `RestrictionChanged` is emitted; the privacy request stays as decided. Requires step-up (`account:restrictionlift`). **204** · **404** `identity.account.notfound` · **409** `identity.account.stateconflict` where the account is not `restricted`, one holding a restriction while `suspended` or `deleting` included |
| `POST /admin/accounts/{subject}/delete/cancel` | Cancels a pending deletion on the subject's behalf, whether the subject (`self`) or an out-of-band request (`oob-request`) began it, restoring the account as it stood (`suspended` with its origin where a suspension is held, `restricted` where a restriction is held) and spending the link a deletion notice carried. Requires step-up (`account:deletioncancel`). **204** · **404** `identity.account.notfound` · **409** `identity.account.stateconflict` where the account is in no grace window · **422** `identity.deletion.windowelapsed` where the window has closed. **409** `identity.takedown.active` where `deletingBy = takedown`: a takedown is reversed only through `/takedown/reverse` (D-137) |
| `GET /admin/accounts/{subject}/photo` | The subject's profile photo as image bytes, for the management application; served through the access gate, never a public URL (IDN-ATTR-003 AC3). **200** the stored image as `image/jpeg` with `Cache-Control: no-store` · **404** `identity.photo.notfound` where the account shows none or an organization it belongs to does not enable photos, alike (D-147) · **404** `identity.account.notfound` where no account bears the subject |

### Takedown — `takedown:execute`

| Endpoint | Does |
|---|---|
| `POST /admin/accounts/{subject}/takedown` | Phase one, in one transaction: suspends the account and takes it into `deleting` by `takedown`, holding the state it was in, ends its sessions, records `reason` (required) and `trigger` (required: `staff-report` · `customer-report` · `automated-signal` · `authority-request`, `10` section 5.12d), and writes the `TakedownExecuted` outbox record for the host's own handling and `AccountSuspended` as an event row, at every trigger whatever state the account held, since it announces that access stopped (IDN-LIFE-003). Admits an account that is `active`, `restricted`, `suspended`, or `deleting` by `self` or `oob-request`. Starts `takedown.grace` (7 days), or for an account already `deleting` the earlier of its own window's end and the trigger plus `takedown.grace`; erasure (key destruction, fingerprint neutralisation) runs when the window closes and the account becomes `deleted` (IDN-LIFE-003, D-127). Body `{ "trigger": "...", "reason": "..." }`. Requires step-up (`account:takedown`), judged after every other refusal. **202** `{ takedownId, erasureDue }`: `takedownId` is the identifier of the `TakedownExecuted` outbox record the trigger writes (IDN-LIFE-003a); `erasureDue` is the instant the takedown's window closes. **400** `api.request.malformed` where `reason` is absent, blank or longer than 1024 characters after trimming, or `trigger` is not one of the four spellings. **404** `identity.account.notfound` where no account bears the subject. **409** `identity.takedown.active` for a takedown-originated `deleting`; `identity.account.stateconflict` (`details.state` `deleted`) for a `deleted` account |
| `GET /admin/accounts/{subject}/takedown` | The account's latest takedown, read from its outbox record from the moment of trigger (IDN-LIFE-003 AC2): **200** `{ takedownId, subject, triggeredAt, erasureDue, reversed, status, attempts, subscribers: [ { name, required, confirmedAt } ] }`, one line per registered subject-event subscriber, `confirmedAt` absent until that subscriber confirms, `status` spelled as `10` section 5.12; `reversed` is `true` and `erasureDue` null once the takedown was reversed, and `status` reads `complete` after a manual completion (IDN-LIFE-003a). **404** `identity.takedown.notfound` where the account holds no takedown; **404** `identity.account.notfound` where the subject names no account |
| `POST /admin/accounts/{subject}/takedown/reverse` | Reverses a takedown inside its window (an adult misjudged). Restores the state the account held at the trigger (IDN-LIFE-003): the deletion it was in, with its origin and start; else `suspended` with its origin; else `restricted` where a restriction is held; else `active`. Host-side actions taken on `TakedownExecuted` are not undone by the library; `TakedownReversed` is written in the reversal's transaction. Records the `reason`, required: body `{ "reason": "..." }`, **400** `api.request.malformed` where it is absent, blank or longer than 1024 characters after trimming. Requires step-up (`account:takedownreverse`), judged after every other refusal. **204** · **404** `identity.takedown.notfound` where the account holds no standing takedown; `identity.account.notfound` where the subject names no account · **422** `identity.takedown.windowelapsed`, from the window's end whether or not the account has been erased |

### Privacy requests and erasures — `privacyrequest:manage`

| Endpoint | Does |
|---|---|
| `GET /admin/privacy/requests` | The queue, each request with its decision deadline and status — `open` · `fulfilled` · `refused` · `granted-by-lapse` · `deemed-refused-by-lapse` (PRIV-RIGHT-002) |
| `POST /admin/privacy/requests` | Enters an out-of-band request on a subject's behalf, `type` one of `erasure` · `restriction` · `rectification` (`10` section 5.12c; rectification of editable data is account editing and is entered here only where the subject cannot edit it themselves); what an `erasure` fulfilled does follows the account's state (the fulfil row below, IDN-LIFE-003). Records the channel, the identity confirmation performed (D-113), and **`receivedAt`: required, the calendar date (`YYYY-MM-DD`, in `privacy.calendar.timezone`) the request reached the company, never later than today there; the decision clock runs from the end of it** (D-136, D-153). **422** `privacy.request.receivedfuture`. `detail` is optional; where present it is 1 to 1024 characters after trimming (API-CONV-002), as `channel` and the identity confirmation are |
| `POST /admin/privacy/requests/{id}/fulfil` · `/refuse` | The decision. Fulfilment of restriction sets the state; refusal records the reason. Fulfilment of erasure follows the state it finds (IDN-LIFE-003): an account `active` or `restricted` enters `deleting` by `oob-request`, holding a restriction where one is in force; an account `suspended` begins its deletion all the same, holding the suspension with its origin, so that a cancellation returns it to `suspended`; an account already `deleting`, by any origin, has the request recorded fulfilled against the running window, and nothing restarts; an account `deleted` has the request recorded fulfilled, and nothing further happens. Where a window starts, the security-notice set is sent `oob-deletion-notice`, which carries no cancel link. Fulfilment of every request type, rectification included, requires step-up (`privacyrequest:fulfil`); refusing is not gated. Receipt is automatic at creation (D-126); no acknowledge endpoint. **403** `authz.denied` without `privacyrequest:manage`, before the request is read; **404** `privacy.request.notfound` where the identifier names no request; **409** `privacy.request.decided` where it is already decided. Nothing is concealed (section 8, AUTHZ-CONCEAL-005) |
| `GET /admin/erasures` · `GET /admin/erasures/{id}` | Every incomplete erasure in one query; per-subscriber state for one (IDN-LIFE-003b): `{ id, subject, reason, status, attempts, subscribers: [ { name, required, confirmedAt } ] }` (D-153). `id` is the identifier of the outbox record the erasure's host-side work travels on. **200** · **404** `privacy.erasure.notfound` where no erasure is held under it |
| `POST /admin/erasures/{id}/complete` | The manual completion path after exhausted retries, itself recorded (IDN-LIFE-003a): closes a `failed` erasure, takedown (`takedownId`) or restriction delivery named by its identifier. Requires step-up (`erasure:complete`). **204** · **404** `privacy.erasure.notfound` where no such delivery is held under the identifier · **409** `privacy.erasure.notfailed` where it is `awaiting-subscribers` or `complete` |

### Audit and explanations — `audit:read`

| Endpoint | Does |
|---|---|
| `GET /admin/audit?subject=...` | Every audit record naming one subject, as the acting identity or as the data subject the record concerns (`subject`), most recent first, without a full scan (PRIV-BREACH-002): each entry carries the record's identifier, category, action, occurrence, acting and effective subjects, data subject, organization and plain details, and never a value held under a subject's key (PRIV-RET-002), so it reads the same before and after erasure; a record of background work carries `principal` and `principalReason` (IDN-AUD-001) |
| `GET /admin/explanations/{correlationId}` | Resolves a concealed denial's correlation identifier to the permission and principal (AUTHZ-GATE-004, AUTHZ-CONCEAL-004), with the outcome `allowed` or `denied` (the explanation outcomes of `10`); any recorded refusal resolves |

Self-service explanation for **non-concealed** types is `GET /account/explanations/{correlationId}`, requiring only the subject's own session: it resolves a refusal whose acting and effective principal are both the caller, on a type that discloses or on no record (AUTHZ-CONCEAL-005); any other identifier answers `authz.denied` (D-166).

### Compliance text — `notice:publish`

| Endpoint | Does |
|---|---|
| `POST /admin/notices` · `POST /admin/documents/{document}/versions` | Publishes a new version: the governing-language text, its governing language (defaulting to `legal.governinglanguage`) and any translations (PRIV-CONS-005). A version without governing-language text is refused, **422** `privacy.notice.governingtextmissing`, and the condition is raised on OPS-ALERT-001 (PRIV-CONS-006). Takes a required boolean `material`; `true` supersedes every live consent recorded against an earlier version of that document and marks those subjects for re-consent, `false` publishes and touches no consent (PRIV-CONS-007, D-153, D-166). Not gated |
| `PUT /admin/documents/{document}/versions/{version}/translations/{language}` | Attaches or corrects a translation on a published version without creating a new one (PRIV-CONS-006). **404** `privacy.document.notfound` where the version does not exist |

### Compliance records — `compliance:manage`

| Endpoint | Does |
|---|---|
| `GET /admin/compliance/licences` | The licence and permit records as stored (OPS-MAINT-001) |
| `PUT /admin/compliance/licences` | Replaces the licence and permit records the system warns on (OPS-MAINT-001), each keyed by the caller's `id`; two records under one `id` are refused **422** `api.request.invalid` (`details.member` `id`) |
| `GET` · `POST /admin/compliance/maintenance` | Reads and appends the maintenance log (OPS-MAINT-001); the entry's actor is the signed-in subject; an entry dated after now is refused **422** `api.request.invalid` (`details.member` `performedAt`); no route changes or removes an entry |
| `PUT /admin/compliance/assessments` | The three human-input fields of the records of processing (PRIV-ROPA-001): the data owner, the implemented organizational security measures, and the links to LIA, DPIA and TIA. A statement replaces the three whole: a field it omits is cleared, and the links are the list it carries. The deployment holds one statement. **204** |

---

## 9. OIDC provider

Standard endpoints for first-party, manually registered clients.

| Endpoint | Purpose |
|---|---|
| `GET /.well-known/openid-configuration` | Discovery |
| `GET /oidc/jwks` | Signing keys |
| `POST /oidc/par` | Pushed Authorization Request (RFC 9126): the client posts the authorization parameters over the back channel and receives a single-use `request_uri` valid for 60 seconds; required for every client (AUTH-OIDC-006, D-164); a `redirect_uri` that is not exactly the client's registered destination is refused with `invalid_request` and no `request_uri` is issued (RFC 9126 section 2.1), and an absent one takes the registered destination; a browser application's request asking for `offline_access` is refused with `invalid_request` (AUTH-OIDC-002) |
| `GET /oidc/authorize` | Authorization code with PKCE, by `request_uri` from `/oidc/par` only; direct parameters are refused with `invalid_request` — **redirects to the authentication application; never renders a page**. Honours `prompt=none` for silent sign-on (AUTH-SESS-012) |
| `POST /oidc/token` | Token issuance and refresh — machine profile (BFF-MACH-001) |
| `GET /oidc/userinfo` | Claims, by scope (D-153): `openid` gives `sub`; `email` gives `email` and `email_verified` (the primary email); `profile` gives `name` (display name), `preferred_username` where `identifiers.username.enabled`, and `locale` (language preference). Nothing else is issued: no phone, legal name, date of birth or photo |

**Deliberately absent:** token introspection, dynamic client registration, consent
screens.

Every error of these endpoints is answered in the shape its protocol fixes (RFC 6749
section 5.2, RFC 9126 section 2.3, RFC 6750 section 3), carrying `error` alone and never
`error_description` or `error_uri`, in a body, a `WWW-Authenticate` header or a
`Location` alike; an authorization error the provider cannot return to a client is
answered to the browser in the API-CONV-002 body (LIB-API-003, D-166).

**`/oidc/authorize` SHALL redirect** to the authentication application's route rather
than rendering a sign-in page. A conventional OIDC provider renders one; this one does
not, because the library never renders user-facing text (D-054).

*Source: AUTH-OIDC-001, D-041, D-166*

**Two kinds of client share the registry.** Protocol clients (Stalwart, future native
clients) receive access and refresh tokens (AUTH-OIDC-003, AUTH-OIDC-004). **Each
browser application's BFF is a confidential client** that uses the code flow once to
establish its per-app session and receives **no refresh token** (AUTH-SESS-012,
BFF-SESS-006, D-104). The kind is recorded on the client and determines what the
token endpoint will issue. Every client carries one registered `redirect_uri`, an
absolute `https` address (or `http` on a loopback IP literal), matched exactly at the
push (AUTH-OIDC-006, API-REDIR-001 AC3).

Refresh tokens rotate on use; reuse of a consumed token revokes the session family
(AUTH-OIDC-003).

---

## 10. Callbacks

Inbound from providers. Each follows INT-GEN-003: hostile input, unguessable
references, rate-limited; an unsigned callback never advances an authoritative state by
itself, and a verified, signed provider security event acts as IDN-LIFE-012a states. The library
defines two callback endpoints of its own; a host mounts its own providers' callbacks on
the same machine-profile pipeline (BFF-MACH-002, INT-GEN-003) under paths of its
choosing, and those paths are not part of this contract.

| Endpoint | Source | Never does |
|---|---|---|
| `GET /callbacks/sms/dlr` | SMS gateway | Mark a phone verified |
| `POST /callbacks/providers/{provider}` | Google (Cross-Account Protection) and Apple (server-to-server notifications); `{provider}` is `google` or `apple` | Act on an event its provider's published keys do not verify |

A delivery report taken is answered **200** with no body: the acknowledgement a gateway
expects, rather than API-CONV-003's success with a body. The transport reads the report
(INT-SMS-005); a report of anything other than a final failure, an intermediate state
included, is read as not failed and changes nothing.

Google delivers the Security Event Token as the request body and follows RFC 8935: a
carried or repeated event is answered **202** with no body; a token that fails
validation (unreadable, carrying no `jti`, or not verified by the provider's keys) is
answered **400** with a JSON body carrying `err`, an error code of RFC 8935 section 2.4,
and `description` (RFC 8935 section 2.3); the rate limit is answered **429** with
`Retry-After`. Apple posts `{ "payload": "<token>" }` and is answered **200** with no
body.

Every other refusal on a callback, the host's included, is `integration.callback.rejected`:
**429** with `Retry-After` and `details.retryAt` where `integration.callback.ratelimit`
refused it, **422** for every other cause, with no `Retry-After`. A delivery of an event
whose earlier delivery is still being carried, its claim younger than
`integration.callback.claimtimeout`, is answered **409** `integration.callback.inprogress`,
is not counted as a rejection, and is carried when the provider delivers it again; one
meeting an older unsettled claim takes it over and is carried (BFF-MACH-002).

*Source: AUTH-ABUSE-007, INT-GEN-003, IDN-LIFE-012a, BFF-MACH-002, D-164, D-166*

**Acceptance criteria**
1. A forged callback with a guessed reference is rejected and logged.
2. An unsigned callback advances state only after independent confirmation; a provider
   event advances it only once its provider's published keys verify it.

---

## 11. Return destinations

**API-REDIR-001** — Any return destination arriving as a request parameter SHALL be
validated by **exact match against the origins of the registered clients' return
addresses** (the client registry, D-057, D-162). An unrecognised destination SHALL fall
back to a configured default: the browser application named by
`redirect.defaultclient`; where the deployment names none, no destination is carried
and the frontend chooses its own.

*Source: D-057, D-162, D-166*

Applies to **every endpoint that forwards a browser**: sign-in link landings,
post-sign-in redirects, and any other flow carrying a destination. RFC 9700 prohibits
exposing arbitrary-URI redirectors anywhere, not only at authorization endpoints. An
authorization request's `redirect_uri` is not such a destination: it arrives at
`POST /oidc/par`, which forwards no browser, and is refused there with `invalid_request`
unless it is exactly the client's registered one (AUTH-OIDC-006, RFC 9126);
`/oidc/authorize` forwards only to the destination the push kept.

**Registration is exempt because it carries no destination.** See API-REDIR-002.

**Pattern or prefix matching SHALL NOT be used.** A check of the form "starts with the
expected domain" is defeated by `example.com.attacker.net`.

Without this, a link to the genuine registration page carrying an attacker-controlled
destination presents a real domain, real branding, and a real flow — then delivers the
person to the attacker at the final step, already trusting it.

**Acceptance criteria**
1. A destination not in the configured list is replaced with the default, not
   rejected with an error revealing the check.
2. A destination that merely contains a known origin as a substring is not accepted.
3. The registry is read at startup: a registered return address that is not an absolute
   `https` address with a host (or `http` on a loopback IP literal), or a
   `redirect.defaultclient` naming no registered browser application, fails with
   `model.startup.redirectclient`.
4. Rejected destinations are logged.

---

**API-REDIR-002**: Registration SHALL record the **originating client identifier**
from the client registry on the registration session (REG-SESS-001). Completion SHALL
resolve the redirect from that stored reference, never from a request parameter.

*Source: D-057, D-146, D-166*

Validation occurs at capture: an unrecognised identifier resolves to nothing and the
configured default applies. By completion there is nothing left to validate. The
completion answers the origin (scheme, host and port) of the stored client's registered
address, which the done step reads as `landing` from `GET /auth/session` (REG-SESS-008).

**This is stronger than exact-match URI validation.** RFC 9700 requires a passed
`redirect_uri` to be exact-matched against registered values, and documents attacks
arising from loose matching. Passing an identifier rather than a URI means there is no
matching step to get wrong.

Carrying the identifier as a query parameter on the initial navigation is standard —
OAuth carries `client_id` the same way. It is an identifier, not a credential.

**Acceptance criteria**
1. `POST /register` accepts a client identifier and resolves it against the registry.
2. An unrecognised identifier is stored as `redirect.defaultclient`, not rejected; where
   none is configured, nothing is stored.
3. No destination parameter is accepted at any subsequent registration step.
4. Completion redirects to the stored client's origin.

---

## 12. Browser landings

**API-LAND-001**: Links delivered to a user: sign-in links, registration and
identifier verification, the old address's confirmation of a replace, undo, recovery,
enrolment, invitation, deletion cancellation, reactivation and loss-report cancellation:
SHALL land on a **frontend route**, never on a library-rendered page, and SHALL act only
on a press, never on load.

The library validates the token behind that route and returns a code. The frontend
renders the outcome and localizes it.

*Source: D-054, D-166*

Those pages are then server-rendered **and** localized, rather than server-rendered
and not.

**Values (D-166).** Every link the library sends is `<origin>/link#<kind>.<token>`. The
kind is one of the link kinds of `10` and decides the application: `sign-in`,
`registration`, `recovery`, `enrolment` and `invitation` land on the authentication
application; `identifier`, `identifier-confirm`, `undo`, `deletion-cancel`,
`reactivation` and `loss-report` on the account application. The origin is the landing
origin the host declares for that application (LIB-HOST-001, `LandingOrigins`:
`Authentication`, `Account`), and the library fills a message's `{link}` place with the
whole address. Every kind acts only on a press: loading the landing page changes nothing,
so a mail scanner's prefetch changes nothing.
The token travels in the fragment, so it never reaches a server in an address (RFC 9110
excludes the fragment from the request target); the landing page reads it, removes the
fragment from the address bar, and presents it to the endpoint that consumes it in the
request body (`linkToken` or `token`), never in a path or query (FE-VER-001).

**Acceptance criteria**
1. No link sent to a user resolves to HTML produced by the library.
2. An expired or invalid token yields a code, and the frontend renders the message.
3. The landing route works with server-side rendering.
4. Every link a message carries is `<origin>/link#<kind>.<token>`, with the declared
   landing origin of its kind's application; no endpoint takes a link token in a path or
   query.
5. Loading the landing page with a link of any kind issues no state-changing request; a
   press does.

---

## 13. Capabilities

**API-CAP-001** — Resource responses SHALL carry the capabilities the caller holds
for that record, computed in the same query.

```json
{
  "id": "...",
  "...": "...",
  "can": ["read", "edit", "share"],
  "requires": { "edit": ["stepup"] }
}
```

*Source: AUTHZ-GATE-005, D-078, D-087*

A capability means **"permitted by grants, subject to session gates."** It is not a
promise the action will succeed: an action may still be refused for step-up, a
downgraded session, the subject's processing restriction, or missing consent — none of
which the per-row grant query evaluates.

`requires` names what remains for each capability, from the closed set of `10` section
5.20 (`stepup` · `reauthenticate` · `restricted` · `consent` · `accountstate`, D-153),
so the frontend prompts rather than
hiding a control the person is entitled to use or showing one that fails without
explanation.

The frontend renders controls from `can` and SHALL NOT infer permissions from role
names.

**Acceptance criteria**
1. A list of 50 records returns capabilities without additional queries.
2. A capability with an empty `requires` always succeeds.
3. A capability with `requires` prompts for what is named rather than failing.
4. No frontend code contains a role name.

---

## 14. Open items

None. The host's own business endpoints are out of scope — they consume the gate
directly (`03-authorization`) rather than through this surface.
