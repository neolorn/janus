# 09 — API Contract

Endpoints, request and response shapes, status codes, and headers.

**Prerequisite:** `00-overview.md`, section 1. Behaviour is documents 01 to 04 and
20; this defines the HTTP surface that exposes it.

**Scope.** The endpoints the library ships and the host mounts. Host-owned business
endpoints are out of scope; they consume the authorization gate directly rather than
through HTTP.

---

## 1. Conventions

**API-CONV-001** — All endpoints SHALL be mounted under a host-configured prefix. No
absolute path SHALL be hardcoded, including inside the discovery document.

*Source: LIB-HOST-003*

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

*Source: LIB-API-003, CONV-NAME-003*

The host renders the message in the user's language from the code. `details` carries
structured context: never a sentence. Every free-text request field (`reason`, `detail`,
`channelUsed`, `note`) is 1 to 1024 characters after trimming, one rule (D-153). Where
an endpoint below describes a response in prose and names no field, the field is the
camelCase of the noun in the vocabulary this chapter already uses: `id`, `label`,
`createdAt`, `lastUsedAt`, `expiresAt`, `signedInAt`, `device`, `location`, `current`
(D-153). A refused send (`auth.restriction.exceeded`,
AUTH-ABUSE-004) carries `retryAt` in `details`: the earliest time a bucket lifts, and
the same value whether or not the address is registered (AUTH-ABUSE-002).

**Acceptance criteria**
1. No error body contains a user-facing sentence.
2. Every error carries a correlation identifier resolving to audit and logs.

---

**API-CONV-003** — Status codes SHALL be used as follows.

| Code | Meaning here |
|---|---|
| 200 | Success with a body |
| 204 | Success, no body |
| 400 | Malformed request |
| 401 | No valid session — **session death only** |
| 403 | Authenticated, not permitted, **and existence is not concealed**. Also carries `auth.stepup.required` — step-up is a 403, never a 401 |
| 404 | Not found, **or concealed denial** |
| 409 | Conflict — duplicate identifier, state precondition failed |
| 422 | Well-formed but semantically rejected — blocklisted password, mixed-script identifier |
| 429 | Throttled — carries `Retry-After` |

*Source: D-016, AUTHZ-CONCEAL-001*

**Acceptance criteria**
1. A concealed denial is byte-identical and timing-identical to a genuine 404.
2. 403 is used only for failures not tied to a specific record.

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

*Source: AUTH-ABUSE-003*

Applies to: every registration endpoint (`/register/*`, REG-SESS-005), sign-in
initiation, recovery initiation, sign-in link request (`POST /auth/link`), email OTP
request, and adding an identifier to an account (`POST /account/identifiers`). No
registration endpoint discloses whether an identifier exists; the one identifier whose
existence is disclosed is a username at choice time (REG-IDENT-009).

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
{ "clientId": "..." }             // originating application, per API-REDIR-002
```

**201**: session created; the cookie is set; body is the state document of
`GET /register` at its first step
**429**: throttled

*Source: D-146; REG-SESS-001, REG-SESS-002, API-REDIR-002*

A signed-in person is not offered registration; a request that nonetheless arrives
with a live session creates no registration session and is answered with the account
landing (REG-SESS-002). The client identifier is validated at capture and stored on
the session; no destination parameter is accepted here or at any later step
(API-REDIR-002).

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

| Endpoint | Step | Does |
|---|---|---|
| `PUT /register/age` | 1 | Records the date of birth (REG-PROF-002). **422** `identity.profile.underage` on an adults-only host: the session ends and accepts no further date |
| `PUT /register/email` · `PUT /register/phone` | 2, 3 | Stages the identifier, canonicalised, and dispatches a code and a link (REG-SESS-003) unless a provider verified it (REG-IDENT-008). **202** always. A provider sign-in that resolves to a linked `sub` is a sign-in, not a registration |
| `POST /register/identifiers` · `PUT /register/identifiers/{id}` · `DELETE /register/identifiers/{id}` | 4 | Adds a further email or phone within `identifiers.email.max` and `identifiers.phone.max`; Change on an unlocked identifier (re-verification follows); removes an unverified extra. **409** `identity.identifier.lastofkind` where removal would leave the required minimum unmet; **422** `identity.identifier.domainnotallowed` under a domain lock (REG-DOM-001) |
| `POST /register/confirm` | 4 | Completes the confirm step; refused while any staged identifier is unverified (REG-SESS-004) |
| `PUT /register/security` | 5 | Password (**422** `auth.password.blocklisted`, `auth.password.tooshort`) and the second-step choice. WebAuthn and TOTP enrolment (`/auth/webauthn/register/*`, `/account/factors/totp/*`) accept the registration session in place of an account session for this step (REG-SESS-006); a label is required (AUTH-FACT-001). Recovery codes are returned once when a second step is enrolled beside a password (AUTH-RECOV-006) |
| `POST /register/terms` | 6 | Records terms accepted, notice presented, the derived affirmation and the consent controls (REG-SESS-007), runs the one transaction that creates the account `active`, emits `AccountRegistered` once, and signs the person in at the assurance the security step proved. **201** with the session cookie set. **422** `identity.affirmation.required` where the affirmation record is absent |

*Source: D-146; REG-SESS-002 to REG-SESS-007, REG-IDENT-010, REG-DOM-001*

Steps 7 to 10 (about you, preferences, membership, done) run on the account
application in the new session through `PUT /account/profile`,
`PUT /account/preferences` and the membership acknowledgement (`GET /account/invitation`,
`POST /account/invitation/acknowledge`, section 6a); they are not registration endpoints. No step endpoint accepts a request for a step whose
predecessor is incomplete, and none returns to a completed step (REG-SESS-002 AC1).

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
**429**: throttled; a replacement code draws on the sending restrictions
(AUTH-ABUSE-004, `auth.restriction.exceeded` with `retryAt`)

*Source: D-146; REG-SESS-003, AUTH-FACT-004, AUTH-ABUSE-004, API-LAND-001*

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
an identifier is verified, when a step completes and when the session ends: `event:`
names `identifier-verified` (`{ id, kind }`), `step-completed` (`{ step }`) and
`session-ended` (`{}`), each with the `GET /register` state as its `data`, so polling
and streaming are one shape (D-153). Where the stream is unavailable the frontend polls
`GET /register` (FE-VER-001).

**200**: event stream
**401** is never used here (API-CONV-003); a request without the session cookie
receives **404**

*Source: D-146; REG-SESS-003, `17-bff` (server-sent events on the session cookie)*

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
enabled the matching link factor (AUTH-FACT-003 AC5, AUTH-ABUSE-003); a non-existent
address receives the ordinary non-existence message (AUTH-ABUSE-003), once per
`abuse.nonexistent.window`. The link lives `link.magic.lifetime`.

```json
{ "identifier": "..." }
```

**202** · **429**: throttled, or the send refused by a restriction
(`auth.restriction.exceeded` with `retryAt`, identical for a registered and an
unregistered identifier, AUTH-ABUSE-002)

*Source: D-146; AUTH-FACT-002, AUTH-FACT-003, AUTH-ABUSE-003, AUTH-ABUSE-004*

The link completes **only in the browser that requested it and only on a press**
(REG-SESS-003): the landing route (API-LAND-001) calls `POST /auth/factor` with
`factor: "emailLink"` or `"phoneLink"` and the link token, and the server accepts it
only with the requesting browser's pre-authentication cookie. Opened anywhere else,
the same call without that cookie returns the code to type where the sign-in began,
and signs nothing in. A session established by a link alone records AAL1 and a link is
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
202**, whether or not the account exists (AUTH-ABUSE-003); a non-existent address
receives the ordinary "no account" email, once per `abuse.nonexistent.window`. The
code is consumed at `/auth/factor` with `factor: "emailCode"` and lives
`code.verification.lifetime`.

```json
{ "identifier": "..." }
```

**202** · **429**: throttled

*Source: AUTH-FACT-002, AUTH-FACT-003, AUTH-ABUSE-003, D-135*

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
second factor, and `required` omits it.

**422** — `auth.factor.rejected`, `auth.factor.notpermitted` (verification-only
factor offered). Never 401 — that code is session death only (API-CONV-003, D-125).
**429** — throttled, with `Retry-After`

**New-device check** (AUTH-FACT-016). Where the factors presented would complete a
sign-in at reachable assurance AAL1 from a browser the account has not seen, the call
that would complete it instead returns **200** with `status: "deviceVerificationRequired"`
and the code `auth.device.verificationrequired`; no session is set. A code goes to the
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
AUTH-ABUSE-001, D-146*

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
createdAt, lastUsedAt }`, and removes one.
Removing a trusted device requires the second factor again on that browser; removing
a remembered browser makes it face the check again. **Sessions are not listed here**:
they are `GET /account/sessions` (AUTH-SESS-013).

**204** · **404**

*Source: AUTH-SESS-008, AUTH-FACT-015, AUTH-FACT-016, D-124, D-146*

---

### `POST /auth/step-up`

Raises an existing session's assurance. Same request and response shape as
`/auth/factor`; called once per factor until the session reaches the gate.

*Source: AUTH-STEP-001, AUTH-STEP-002, D-141*

Rotates the session identifier on success.

**What the gate tells the frontend.** Every `403` `auth.stepup.required` carries in
`details` the three gate values and what the subject can do about them:

```json
{
  "code": "auth.stepup.required",
  "details": {
    "required": { "level": "aal2", "phishingResistant": false, "maxAge": 900 },
    "outcome": "present",            // present | enrol | report-loss | pending
    "options": [ ["password", "totp"], ["password", "recovery-code"], ["passkey"] ],
    "pendingUntil": null             // set when outcome is pending
  }
}
```

`options` lists **every combination of the account's usable factors that reaches the
gate** (AUTH-STEP-002 step 2); the subject picks one and presents it factor by factor.
`enrol` means no usable combination exists and the account has never reached the
level — the frontend offers enrolment in place (AUTH-STEP-005 AC2). `report-loss`
means the account can reach the level but a factor that would do it is gone — the
frontend offers `POST /recovery/report-loss`. `pending` means a loss report is
already running; `pendingUntil` is when it completes, and the response is not a
refusal but a status. Factor names appear here as presentation data for the prompt;
the session record itself stores properties only (AUTH-SESS-002).

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
  "expiresAt": "..."
}
```

**No organization is returned.** Authorization resolves the organization from the
resource, never from the session (IDN-MEM-003, AUTHZ-SCOPE-001) — returning one here
would invite the frontend to reason from it.

**401** — no valid session

Never returns permissions or role names (AUTHZ-CACHE-002).

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

**422**: `auth.webauthn.algorithmnotallowed`,
`auth.webauthn.userverificationrequired`

*Source: AUTH-FACT-001, AUTH-FACT-002b, AUTH-FACT-011, AUTH-FACT-013, AUTH-FACT-014,
D-146*

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

*Source: D-146; REG-PM-001*

Both are public and unauthenticated. The addresses they name are frontend routes
(API-LAND-001), resolved from the configured application origins (API-REDIR-001).

---

## 5. Recovery

### `POST /recovery/begin`

```json
{ "identifier": "..." }
```

**202** — always, regardless of existence.

*Source: AUTH-ABUSE-003*

Where the identifier does not exist, **that address is emailed to say so**. The real
owner gets their answer; an enumerating attacker learns nothing.

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

Administrative re-enrolment. Requires the approver permission and step-up.

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
  "recoveryCodes": { "remaining": 8, "generatedAt": "...", "viewedAt": "...", "exportedAt": null },
  "profile": { "displayName": "...", "legalName": "...", "dateOfBirth": "...", "photo": "..." },
  "preferences": { "language": "...", "timeZone": "...", "declared": { } }
}
```

Credentials are reported by property and label, never by secret material; `legalName`
and `dateOfBirth` are present only where `profile.legalname` and `profile.dateofbirth`
are on (REG-PROF-001). The credential list renders `backupState` as synced or this
device only (`18` FE-ACCT-001).

*Source: D-146; REG-ACCT-001, AUTH-FACT-001, AUTH-FACT-008, AUTH-FACT-009*

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
**422**: `identity.identifier.mixedscript`; a display name over 64 bytes; a
`dateOfBirth` field (immutable to the person, corrected through support); a
`legalName` or `username` while its key or `identifiers.username.enabled` is off

*Source: D-146; REG-PROF-001, REG-IDENT-009*

Display name and legal name need no gate and are audited. A username change is a
step-up action and re-verifies nothing: a username is never verified.

---

### `GET /account/preferences` · `PUT /account/preferences`

The language (BCP 47), the time zone (IANA identifier) and the values of the
host-declared preferences (REG-PREF-001). `PUT` replaces the set.

**200** / **204**
**422**: `identity.preference.undeclared` (a key the host did not declare), a value of
the wrong type for its declaration, a set over `preferences.maxsize`, an
administrator-only preference set by the person

*Source: D-146; REG-PREF-001*

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
**409**: the kind's maximum (`identifiers.email.max`, `identifiers.phone.max`)
reached; in single-address mode use `PUT /account/identifiers/{id}/replace`
**422**: `identity.identifier.mixedscript`, `identity.identifier.domainnotallowed`
(REG-DOM-001)

*Source: D-146; REG-IDENT-004, REG-SESS-005, REG-DOM-001*

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
**409**: the identifier is unverified; the named identifier is the primary (the
primary cannot be the backup)

*Source: D-146; REG-IDENT-002, REG-IDENT-005*

Emits `IdentifierPrimaryChanged` on a primary change.

---

### `DELETE /account/identifiers/{id}`

Removes an identifier, **immediately** (REG-IDENT-006). Step-up action
`identifier:remove`.

**204**: removed; the remaining security-notice set receives an **undo** link valid
for `identifier.change.coolingoff`; the removed identifier receives a notice with no
link; every other session of the account ends. Emits `IdentifierRemoved`.
**403**: `auth.stepup.required`
**409**: `identity.identifier.primary` (set another primary first),
`identity.identifier.lastofkind` (the required minimum of the kind would be unmet)

*Source: D-146; REG-IDENT-006*

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
an admin-assisted enrolment session (AUTH-RECOV-002, REG-IDENT-007).

```json
{ "value": "..." }
```

**202**: always, whether or not the new value belongs to another account
(API-CONV-005)
**403**: `auth.stepup.required`
**409**: `identity.change.pending` (a replace is already staged for this identifier)
**422**: `identity.identifier.mixedscript`, `identity.identifier.domainnotallowed`

*Source: D-148; D-146, REG-IDENT-007*

Also the endpoint an enrolment session opened for a lost mailbox uses to set a new
address confirmed by the new address alone (`POST /enrol/begin`, AUTH-RECOV-002).

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

Requires step-up (`10` §5a). Linking to an account that holds a second factor shows
the R-A16 disclosure (AUTH-FACT-002a).

**204**
**403** — `auth.stepup.required`
**409** — `identity.link.lastcredential` — unlinking the only remaining credential is
refused.

*Source: IDN-LIFE-012, D-128, D-140*

---

### `POST /account/password`

Sets or changes a password while signed in. Requires step-up at the gate the
subject's policy declares (AUTH-STEP-002).

**204**
**422** — `auth.password.blocklisted`, `auth.password.tooshort`

*Source: AUTH-PASS-004, AUTH-PASS-001a, AUTH-RECOV-007a, D-092*

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
**202** — `auth.credential.lastsecondfactor` — removing this credential would lower
the account's reachable assurance (AUTH-STEP-006); it is `suspended` now and
invalidated after the window (AUTH-RECOV-007), notified throughout. Body carries
`invalidatesAt`.

*Source: D-092, D-141*

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
**422**: a label of 0 or over 64 characters, or one already held by another credential
of the same kind on the account

*Source: D-146; AUTH-FACT-001*

---

### `POST /account/credentials/{id}/upgrade`

Upgrades a second-factor security key to a **passkey** by re-registration
(AUTH-FACT-002b): opens a WebAuthn registration ceremony for the same authenticator
with `residentKey: required`; on completion the passkey is listed and the second-factor
entry is retired; on failure nothing changes. Gated as an enrolment (AUTH-STEP-007) and
notified like one.

**200**: the ceremony options, completed at `/auth/webauthn/register/complete`
**403**: `auth.stepup.required`
**409**: the credential is not a second-factor security key

*Source: D-146; AUTH-FACT-002b, AUTH-STEP-007*

---

### `PUT /account/secondstep/preferred`

Sets the account's **preferred second-step method** from the second factors enrolled
(IDN-ATTR-008). No gate.

```json
{ "credentialId": "..." }
```

**204**
**409**: the method is not enrolled on the account

*Source: D-146; IDN-ATTR-008*

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
download and print and confirms they were saved. The set records `viewedAt` when shown
and `exportedAt` on copy, download or print; both are visible in `GET /account`.
**403**: `auth.stepup.required`
**409**: the account has no password (a passkey-only account has no recovery codes,
AUTH-FACT-002b)

*Source: D-146; AUTH-FACT-008, AUTH-FACT-009, AUTH-RECOV-006*

Regeneration invalidates the entire previous set. A reminder fires in the account and
to the security-notice set when `recovery.codes.reminder` has elapsed since generation.
Replaces `POST /account/factors/recovery-codes/generate`.

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

**200** / **204** · **404**

*Source: D-146; AUTH-SESS-013, INT-GEN-006*

Trusted devices and remembered browsers are `GET /account/devices`; revoking everything
is `POST /auth/logout` (AUTH-SESS-008).

---

### `GET /account/mail/apppasswords` · `POST /account/mail/apppasswords` · `DELETE /account/mail/apppasswords/{id}`

Mail **app passwords**, managed through the library's first-party OIDC client for the
mail server (REG-MAIL-002, INT-MAIL-010). The library obtains a token for the signed-in
person, makes the server's app-password call and stores **nothing**. Present only where
the account holds a mailbox (INT-MAIL-006).

```json
{ "label": "...", "expiresAt": "..." }        // POST; expiresAt optional
```

**200**: `GET`: what the server holds (id, label, created, expiry where set), never a
cached copy. `POST`: the generated **secret, returned once**, with its id.
**204**: `DELETE`: revoked at the server; a client using it fails on its next
connection.
**403**: `auth.stepup.required` (`mailcredential:create`, `mailcredential:revoke`)

*Source: D-146; REG-MAIL-002, INT-MAIL-010*

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

Cancels a pending deletion during its grace window and restores the account to
`active` (IDN-ACCT-007). A `deleting` account cannot sign in, so this **accepts a
link token from the deletion notification**, the same shape as MFA-removal
cancellation.

**204**
**422** — `identity.deletion.windowelapsed`
**409** — `identity.takedown.active` — the deletion was a takedown; only
`/admin/accounts/{subject}/takedown/reverse` undoes it (D-137)

*Source: IDN-ACCT-007, D-106, D-137*

---

### `POST /account/reactivate`

Reverses a self-deactivation. A suspended account cannot sign in, so this **accepts
the link token from the deactivation notice** (the same shape as deletion
cancellation) and restores `active` only where `suspendedBy` is `self`
(IDN-LIFE-013). Not gated (`10` section 5a). Where the notice is lost, the owner uses
ordinary recovery (`/recovery/begin`), which restores `active` on completion (D-140).

```json
{ "token": "..." }
```

**204**
**422**: `identity.reactivation.tokeninvalid` (token unknown, expired or consumed)
**409**: `identity.account.adminsuspended`: the account was suspended by an
administrator and only an administrator reactivates it (D-135)

*Source: D-147; IDN-LIFE-013, D-135, D-140*

---

### `GET /account/photo` · `PUT /account/photo` · `DELETE /account/photo`

`GET` returns the caller's own photo as image bytes, **served through the access gate
and never from a public URL** (IDN-ATTR-003 AC3): the response carries no
cache-shareable identifier, and there is no address at which the photo can be fetched
without the session. Staff photos shown in the management application are read
through `GET /admin/accounts/{subject}/photo` (section 8a) under the same rule.

**200** (`GET`): the image, with its content type; **404** where none is set or the
policy does not enable photos

Upload is validated by content, size- and dimension-limited, and re-encoded
(IDN-ATTR-004). Availability follows organization policy (IDN-ATTR-002).

**204**
**422** — `identity.photo.invalid`, `identity.photo.toolarge`
**403** — `identity.photo.notenabled`

*Source: IDN-ATTR-002 to IDN-ATTR-004, D-106, D-147*

---

### `PUT /account/language`

*Retired by D-146. See `PUT /account/preferences` (language, time zone and the
host-declared preferences in one object, REG-PREF-001).*

---

## 6a. Invitation acknowledgement

### `GET /account/invitation`

Returns, for the invitation attached to the signed-in person's registration or
sign-in (REG-INV-001, REG-INV-002): who invited them, the organization, the roles and
grants that will attach, and the documents attached to the invitation with their
versions. **404** when no invitation is attached.

### `POST /account/invitation/acknowledge`

Attaches the membership, disables factors the organization's policy does not permit
(IDN-LIFE-009b), makes the corporate address primary where one exists, enables the
pre-provisioned mailbox (REG-MAIL-001) and records the acknowledgement (document list,
versions, timestamp) on the membership; where a corporate address becomes primary,
the personal email of the invitation stays as a verified non-primary email
(REG-MAIL-001). **204**. **422** `identity.invitation.expired`,
`identity.invitation.identifiermismatch` (a bound identifier is verified on a different
account than the one accepting); **403** `auth.stepup.required` with outcome
`enrol` when the account does not yet satisfy the organization's `requiredAssurance` or
`credentialRedundancy` (REG-INV-002). Emits `MembershipChanged` and, where the primary
email changed, `IdentifierPrimaryChanged`.

*Source: D-148; D-146, REG-INV-001, REG-INV-002, REG-MAIL-001*

---

## 7. Privacy

### `GET /privacy/consents`

**200** — every consent record held for the subject: `{ purpose, noticeVersion,
mechanism, grantedAt, withdrawnAt }` (objections: `recordedAt` for `grantedAt`) (D-153).

*Source: PRIV-CONS-011*

---

### `POST /privacy/consents/{purpose}/grant` · `POST /privacy/consents/{purpose}/withdraw`

**204**

*Source: PRIV-CONS-008, PRIV-CONS-011, D-135*

Grant records the current notice version and the consent kind; it is how a subject
opts in after registration or re-consents after a notice revision (PRIV-CONS-007).
Withdrawal: no confirmation interstitial, no retention flow. Both take effect without
human approval.

---

### `GET /privacy/objections` · `POST /privacy/objections/{purpose}` · `DELETE /privacy/objections/{purpose}`

Objection — for purposes whose declared basis is objectable (PRIV-BASIS-001).
`GET` lists the subject's objection records (purpose, notice version, timestamp,
withdrawal state). `POST` records an objection and raises `ObjectionChanged`;
`DELETE` withdraws it. Both take effect without human approval; no grounds are asked.

**204**
**422** — `privacy.purpose.notobjectable`

*Source: PRIV-RIGHT-001a, D-145*

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

*Source: PRIV-CONS-005, PRIV-CONS-006, D-146*

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
account editing; **rectification of non-editable data** (the recorded details of a
host business record, say) is a request here, decided under the six-working-day rule
like restriction.
Restriction enters the request queue; erasure reaches it only through
`POST /admin/privacy/requests` (out of band).

---

### `GET /privacy/export?format=human|machine`

**200** — one export routine, two formats, **both structured data**.

**Requires step-up at the account's reachable assurance** (AUTH-STEP-002a — never
phishing-resistant under the system policy), and is **rate-limited**.

*Source: D-086, D-141*

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
stable-named. **Neither is a rendered document.** The frontend renders the readable
view and localizes it through the frontend localization library.

No server-generated PDF. A rendered page satisfies the obligation, browsers print to
PDF anyway, and a downloadable file of health data is a larger exposure than a page
read while authenticated.

*Source: PRIV-RIGHT-003, D-054, D-058*

---

## 8. Administration

All endpoints under `/admin` require the corresponding permission. Denials return
403 — no record existence is concealed at this level.

*Source: AUTHZ-CONCEAL-005*

### `GET /admin/access?resourceType=...&resourceId=...`

**200** — who can access this resource, and through which grant or container.

Stored and derived grants are reported **distinctly**. Where declared derivations
make the answer unbounded, the response states the limitation rather than returning
a partial answer silently: when evaluation exceeds `authz.reverselookup.budget` the
response carries `partial: true` and `unevaluated`, the derivations not evaluated
(AUTHZ-DERIVE-007, D-153).

*Source: AUTHZ-GATE-004, AUTHZ-DERIVE-007*

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

**201** / **204**
**409** — `authz.grant.duplicate`

*Source: AUTHZ-GRANT-001, AUTHZ-GRANT-003*

`reason` is recorded. Revocation records the revoking actor.

---

### `GET|POST|DELETE /admin/roles`

Runtime role management.

*Source: AUTHZ-GRANT-004*

---

### `POST /auth/break-glass`

Presents the sealed emergency credential. **Mounted on the machine profile**
(BFF-MACH-001): no session, outside the session-bound CSRF layer, source-throttled.
**Any cookie present is ignored rather than refused**, since this is reached from a
browser that may hold a stale session for the domain.

```json
{ "credential": "..." }
```

**200** — a time-boxed **auth session** (`breakglass.session.lifetime`) whose subject
is the reserved `emergency` account (D-138), which holds the `system-administrator`
role; the audit trail shows `emergency` as the actor, and every use alerts the owner
destinations (D-129, D-147). The applications open from it as from any sign-in
(BFF-SESS-006)
**422** — `auth.breakglass.invalid` (never 401 — session death only, API-CONV-003)
**409** — `auth.breakglass.consumed`
**429** — throttled: per source under AUTH-ABUSE-001 and, in addition, at most 5
attempts per hour globally across all sources (OPS-BOOT-004, D-147)

*Source: D-065, D-129, D-138, D-147; OPS-BOOT-004*

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

**200** — `{ "credential": "...", "address": "...", "issuedAt": "..." }`, once
**403** — `auth.stepup.required`

*Source: D-065, D-133*

Without this, one emergency exhausts the mechanism until the operator returns — which
is the scenario it exists for.

---

### `POST /admin/accounts/{subject}/sessions/revoke`

Terminates every session and derived token for **one account**.

**204**

*Source: D-077*

Required by offboarding (`16-offboarding-procedure` step 1), which previously had only
the system-wide operation available — so signing out one departing clerk would have
signed out every customer.

---

### `POST /admin/sessions/revoke-all`

**204** — the explicit emergency revocation.

*Source: AUTH-SESS-009*

Distinct from the automatic downgrade that follows a policy tightening.

---

### `GET|PUT /admin/config/{key}`

`GET` returns `{ key, value, default, protected, direction }` (D-153).
`PUT` sets it:

```json
{ "value": ..., "reason": "..." }
```

`value` takes the key's type (`10` section 4); `reason` is required on every change
and recorded in the audit entry (OPS-CFG-005, OPS-CFG-008; D-147).

**200** / **204**
**403** — `auth.stepup.required`, for a **loosening** change
**422** — `config.value.belowfloor`, `config.value.aboveceiling`,
`config.value.notallowed`, `config.key.protected`, `auth.restriction.reasonrequired`
where a loosening arrives without a reason

*Source: OPS-CFG-002, OPS-CFG-003, OPS-CFG-004, D-147*

Tightening requires no step-up. Loosening requires step-up, a reason, and produces an
audit entry. Protected keys (OPS-CFG-004) are rejected: they are not changeable through the application.

---

### `GET /admin/restrictions` · `GET|PUT|DELETE /admin/restrictions/{name}`

The **named restriction set** that governs every send (AUTH-ABUSE-004): runtime
configuration edited like the holiday list (D-142). `GET` lists the restrictions with
their keys, purposes and buckets, the shipped defaults included.

```json
{
  "key": "destination",                       // destination | account | source | global | host:<name>
  "purpose": "any",                           // verification | signin | secondfactor | notification | any
  "buckets": [ { "max": 5, "interval": "PT1H", "window": "sliding" },
               { "max": 1, "interval": "PT60S", "window": "fixed" } ]
}
```

**200** / **204**
**403**: `auth.stepup.required` (`restriction:edit`, every edit); a **loosening** (a
higher max, a shorter interval, a removed bucket or a deleted restriction) also requires
a reason (OPS-CFG-002) and raises a Normal alert (OPS-ALERT-001)
**422**: a `host:<name>` key with no registered supplier (LIB-HOST-001); an empty
bucket list

*Source: D-146; AUTH-ABUSE-004, OPS-CFG-002, OPS-CFG-008*

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
**422**: `auth.restriction.reasonrequired`

*Source: D-146; AUTH-ABUSE-004*

`keyValue` is the plain address, account, source or host value; the library derives
the stored key (the HMAC for a destination) and never returns it. A granted key is
refused again once the credit is spent.

---

### `GET /admin/ropa?format=template`

**200** — generated records of processing in the regulator's template shape.

*Source: PRIV-ROPA-001, D-036*

Flags any purpose lacking a required assessment reference, and any processor lacking
an agreement reference.

---

## 8a. Administration — operations previously without a surface

Every endpoint below is a rendering of an operation in the **operations contract**
(LIB-API-005) — the same service a host calls in-process. Neither is written
separately. Permissions are those in `10-reference` §2.1; step-up applies where the
operation loosens a control (OPS-CFG-002) or touches another person's account.

*Source: D-106*

### Organizations — `organization:manage`

| Endpoint | Does |
|---|---|
| `POST /admin/organizations` | Creates an organization with its policy row (IDN-ORG-002) |
| `PUT /admin/organizations/{id}/policy` | Replaces the organization's policy overrides — the policy object of `10` §4.1a: `requiredAssurance`, `loginFactors`, `gates`, `credentialRedundancy`, `selfServiceRecovery` and `emailDomains` (managed through the domain endpoints below, never written here); omitted fields inherit `policy.default`. **422** `config.policy.belowsystem` where a field is looser than the system default (AUTH-STEP-002a); loosening any field requires step-up and a reason (OPS-CFG-002). `GET` returns the resolved policy with each field marked inherited or overridden (D-143) |
| `POST /admin/organizations/{id}/delete` · `/delete/cancel` | Request → suspend → grace → erasure, cancellable (IDN-ORG-003). **409** `identity.organization.protected` for the administrative organization |
| `POST /admin/organizations/{id}/domains` · `POST /admin/organizations/{id}/domains/{domain}/verify` · `DELETE /admin/organizations/{id}/domains/{domain}` | Domain lock (REG-DOM-001, IDN-ORG-006): adds a domain to the policy field `emailDomains` (unverified, admitting nothing), verifies it by the DNS TXT record the add response names, removes it. Adding is a loosening (OPS-CFG-002: step-up `domain:manage`, reason, audit); removal stops new sign-ins with addresses in the domain and raises an alert (OPS-ALERT-001). Re-verification runs every `domain.reverify.interval` on the sweep; a failure alerts and revokes nothing. The record is `_identity-verify.<domain>` TXT with value `identity-domain-verification=<32 random bytes, base64url>`, one token per organization and domain, never reused (D-153). `GET` returns each domain with its verification state and last check |

### Memberships and invitations — `membership:manage`

| Endpoint | Does |
|---|---|
| `POST /admin/organizations/{id}/invitations` | Issues a time-boxed, single-use enrolment link (IDN-LIFE-009a). MAY bind `email`, `phone`, both or neither (REG-INV-001); a bound identifier is pre-filled and locked at registration, and a bound phone is verified before the membership step. Where the mail server is integrated, the invitation names a personal `email` (required; the link goes to it and its press verifies it, so the account is created with a verified personal email), and a corporate `email` is asserted and its mailbox provisioned disabled (REG-MAIL-001, D-148). Requires step-up. An integrated-mail invitation without a personal `email` is refused as a validation error (**422**). The identifier-mismatch refusal belongs to acceptance (`POST /account/invitation/acknowledge`), not to issue |
| `DELETE /admin/organizations/{id}/invitations/{invitationId}` | Revokes an unused invitation |
| `DELETE /admin/organizations/{id}/memberships/{subject}` | Ends a membership; the account and organization persist (IDN-MEM-001) |

### Groups — `group:manage`

| Endpoint | Does |
|---|---|
| `GET|POST|DELETE /admin/groups` | Groups nest; membership is transitive (AUTHZ-GROUP-001) |
| `POST|DELETE /admin/groups/{id}/members` | Adds or removes a user or a group |

### Accounts — `account:manage`

| Endpoint | Does |
|---|---|
| `POST /admin/accounts/{subject}/suspend` · `/reactivate` | Suspension ends sessions in the same operation (AUTH-SESS-010); reactivation restores grants exactly (IDN-LIFE-013). Requires step-up |
| `POST /admin/accounts/{subject}/restriction/lift` | Lifts a processing restriction (PRIV-RIGHT-004) |
| `POST /admin/accounts/{subject}/delete/cancel` | Cancels a pending deletion on the subject's behalf. **409** `identity.takedown.active` where `deletingBy = takedown` — a takedown is reversed only through `/takedown/reverse` (D-137) |
| `GET /admin/accounts/{subject}/photo` | The subject's profile photo as image bytes, for the management application; served through the access gate, never a public URL (IDN-ATTR-003 AC3). **404** where none is set or the policy does not enable photos (D-147) |

### Takedown — `takedown:execute`

| Endpoint | Does |
|---|---|
| `POST /admin/accounts/{subject}/takedown` | Phase one, in one transaction: suspends the account, ends its sessions, records `reason` and `trigger` (`staff-report` · `customer-report` · `automated-signal` · `authority-request`, `10` section 5.12d), and writes the `TakedownExecuted` outbox record for the host's own handling. Starts `takedown.grace` (7 days); erasure (key destruction, fingerprint neutralisation) runs when it elapses and the account becomes `deleted` (IDN-LIFE-003, D-127). Requires step-up. **202** `{ takedownId, erasureDue }` |
| `POST /admin/accounts/{subject}/takedown/reverse` | Reverses a takedown inside its window (an adult misjudged). Restores `active`; host-side actions taken on `TakedownExecuted` are not undone by the library. Records the reason. Requires step-up. **204** · **422** `identity.takedown.windowelapsed` |

### Privacy requests and erasures — `privacyrequest:manage`

| Endpoint | Does |
|---|---|
| `GET /admin/privacy/requests` | The queue, each request with its decision deadline and status — `open` · `fulfilled` · `refused` · `granted-by-lapse` · `deemed-refused-by-lapse` (PRIV-RIGHT-002) |
| `POST /admin/privacy/requests` | Enters an out-of-band request on a subject's behalf, `type` one of `erasure` · `restriction` · `rectification` (`10` section 5.12c; rectification of editable data is account editing and is entered here only where the subject cannot edit it themselves); an `erasure` fulfilled enters `deleting` with `deletingBy = oob-request` (IDN-LIFE-003). Records the channel, the identity confirmation performed (D-113), and **`receivedAt` — required, the calendar date (`YYYY-MM-DD`, in `privacy.calendar.timezone`) the request reached the company, never later than today there; the decision clock runs from the end of it** (D-136, D-153). **422** `privacy.request.receivedfuture` |
| `POST /admin/privacy/requests/{id}/fulfil` · `/refuse` | The decision. Fulfilment of erasure starts the grace window, of restriction sets the state; refusal records the reason. Receipt is automatic at creation (D-126) — no acknowledge endpoint |
| `GET /admin/erasures` · `GET /admin/erasures/{id}` | Every incomplete erasure in one query; per-subscriber state for one (IDN-LIFE-003b): `{ id, subject, reason, status, attempts, subscribers: [ { name, required, confirmedAt } ] }` (D-153) |
| `POST /admin/erasures/{id}/complete` | The manual completion path after exhausted retries — itself recorded (IDN-LIFE-003a). Requires step-up |

### Audit and explanations — `audit:read`

| Endpoint | Does |
|---|---|
| `GET /admin/audit?subject=...` | Every audit record for one subject, without a full scan (PRIV-BREACH-002) |
| `GET /admin/explanations/{correlationId}` | Resolves a concealed denial's correlation identifier to the permission and principal (AUTHZ-GATE-004, AUTHZ-CONCEAL-004) |

Self-service explanation for **non-concealed** types is `GET /account/explanations/{correlationId}`, requiring only the subject's own session.

### Compliance text — `notice:publish`

| Endpoint | Does |
|---|---|
| `POST /admin/notices` · `POST /admin/documents/{document}/versions` | Publishes a new version: the governing-language text, its governing language (defaulting to `legal.governinglanguage`) and any translations (PRIV-CONS-005). A version without governing-language text is refused, **422** `privacy.notice.governingtextmissing`, and the condition is raised on OPS-ALERT-001 (PRIV-CONS-006). Takes a required boolean `material`; `true` supersedes every live consent on the purposes the document covers and marks those subjects for re-consent, `false` publishes and touches no consent (PRIV-CONS-007, D-153) |
| `PUT /admin/documents/{document}/versions/{version}/translations/{language}` | Attaches or corrects a translation on a published version without creating a new one (PRIV-CONS-006) |

### Compliance records — `compliance:manage`

| Endpoint | Does |
|---|---|
| `PUT /admin/compliance/licences` | Licence and permit expiry dates the system warns on (OPS-MAINT-001) |
| `PUT /admin/compliance/assessments` | LIA, DPIA and TIA references, and the declared human-input fields of the records of processing (PRIV-ROPA-001) |

---

## 9. OIDC provider

Standard endpoints for first-party, manually registered clients.

| Endpoint | Purpose |
|---|---|
| `GET /.well-known/openid-configuration` | Discovery |
| `GET /oidc/jwks` | Signing keys |
| `POST /oidc/par` | Pushed Authorization Request (RFC 9126): the client posts the authorization parameters over the back channel and receives a single-use `request_uri` valid for 60 seconds; required for every client (AUTH-OIDC-006, D-164) |
| `GET /oidc/authorize` | Authorization code with PKCE, by `request_uri` from `/oidc/par` only; direct parameters are refused with `invalid_request` — **redirects to the authentication application; never renders a page**. Honours `prompt=none` for silent sign-on (AUTH-SESS-012) |
| `POST /oidc/token` | Token issuance and refresh — machine profile (BFF-MACH-001) |
| `GET /oidc/userinfo` | Claims, by scope (D-153): `openid` gives `sub`; `email` gives `email` and `email_verified` (the primary email); `profile` gives `name` (display name), `preferred_username` where `identifiers.username.enabled`, and `locale` (language preference). Nothing else is issued: no phone, legal name, date of birth or photo |

**Deliberately absent:** token introspection, dynamic client registration, consent
screens.

**`/oidc/authorize` SHALL redirect** to the authentication application's route rather
than rendering a sign-in page. A conventional OIDC provider renders one; this one does
not, because the library never renders user-facing text (D-054).

*Source: AUTH-OIDC-001, D-041*

**Two kinds of client share the registry.** Protocol clients (Stalwart, future native
clients) receive access and refresh tokens (AUTH-OIDC-003, AUTH-OIDC-004). **Each
browser application's BFF is a confidential client** that uses the code flow once to
establish its per-app session and receives **no refresh token** (AUTH-SESS-012,
BFF-SESS-006, D-104). The kind is recorded on the client and determines what the
token endpoint will issue. Every client carries an exact-match `redirect_uri`
(API-REDIR-001).

Refresh tokens rotate on use; reuse of a consumed token revokes the session family
(AUTH-OIDC-003).

---

## 10. Callbacks

Inbound from providers. Each follows INT-GEN-003: hostile input, unguessable
references, rate-limited, and never advancing authoritative state alone. The library
defines one callback endpoint of its own; a host mounts its own providers' callbacks on
the same machine-profile pipeline (BFF-MACH-002, INT-GEN-003) under paths of its
choosing, and those paths are not part of this contract.

| Endpoint | Source | Never does |
|---|---|---|
| `GET /callbacks/sms/dlr` | SMS gateway | Mark a phone verified |

*Source: AUTH-ABUSE-007, INT-GEN-003*

**Acceptance criteria**
1. A forged callback with a guessed reference is rejected and logged.
2. State advances only after independent confirmation.

---

## 11. Return destinations

**API-REDIR-001** — Any return destination arriving as a request parameter SHALL be
validated by **exact match against a configured list of known application origins**.
An unrecognised destination SHALL fall back to a configured default.

*Source: D-057*

Applies to **every endpoint that forwards a browser**: sign-in link landings,
post-sign-in redirects, and any other flow carrying a destination. RFC 9700 prohibits
exposing arbitrary-URI redirectors anywhere, not only at authorization endpoints.

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
3. The configured list is validated at startup; an entry that is not an absolute
   origin fails.
4. Rejected destinations are logged.

---

**API-REDIR-002**: Registration SHALL record the **originating client identifier**
from the client registry on the registration session (REG-SESS-001). Completion SHALL
resolve the redirect from that stored reference, never from a request parameter.

*Source: D-057, D-146*

Validation occurs at capture: an unrecognised identifier resolves to nothing and the
configured default applies. By completion there is nothing left to validate.

**This is stronger than exact-match URI validation.** RFC 9700 requires a passed
`redirect_uri` to be exact-matched against registered values, and documents attacks
arising from loose matching. Passing an identifier rather than a URI means there is no
matching step to get wrong.

Carrying the identifier as a query parameter on the initial navigation is standard —
OAuth carries `client_id` the same way. It is an identifier, not a credential.

**Acceptance criteria**
1. `POST /register` accepts a client identifier and resolves it against the registry.
2. An unrecognised identifier is stored as the default, not rejected.
3. No destination parameter is accepted at any subsequent registration step.
4. Completion redirects to the stored client's origin.

---

## 12. Browser landings

**API-LAND-001**: Links delivered to a user: sign-in links, identifier verification,
undo, recovery, enrolment: SHALL land on a **frontend route**, never on a library-rendered
page.

The library validates the token behind that route and returns a code. The frontend
renders the outcome and localizes it.

*Source: D-054*

Those pages are then server-rendered **and** localized, rather than server-rendered
and not.

**Acceptance criteria**
1. No link sent to a user resolves to HTML produced by the library.
2. An expired or invalid token yields a code, and the frontend renders the message.
3. The landing route works with server-side rendering.

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
