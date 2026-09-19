# 17 — Backend for Frontend

The browser-facing security boundary: session handling, CSRF, capability projection,
and error translation.

**Prerequisite:** `00-overview.md` section 3.3, `09-api-contract.md`.

**Scope.** What the library provides and enforces at the browser boundary, and what
the host owns. Frontend integration constraints are `18-frontend-integration`. UI and
UX are downstream of both and deliberately unspecified.

---

## 1. Why this document exists

Several requirements already name the BFF as their enforcement point and no document
described it. AUTH-SESS-003 places the session cookie there. AUTH-SESS-007 requires
CSRF enforcement "in the BFF layer." AUTHZ-GATE-005's capability projection crosses
it. LIB-API-003's error codes are translated there.

**It was carrying real security weight as an unspecified component.**

---

## 2. Ownership

**BFF-OWN-001** — The library SHALL ship the BFF pipeline as middleware the host
mounts. The host SHALL NOT implement session handling, CSRF enforcement, capability
projection, or error translation itself.

*Source: D-052*

AUTH-SESS-007 requires that no endpoint can opt out of CSRF. That is achievable only
if the library owns the middleware — a host-built BFF makes "no endpoint can forget"
something to get right in four separate applications, and the requirement becomes
aspirational.

The same reasoning applies to session handling and error translation: each is
specified as enforced, not followed.

**Acceptance criteria**
1. Mounting the pipeline requires no security-relevant configuration to be correct.
2. A host cannot disable CSRF enforcement for a subset of endpoints.
3. All four applications share one implementation.

---

**BFF-OWN-002** — The division of responsibility SHALL be as follows.

| Library owns | Host owns |
|---|---|
| Session cookie: issue, read, rotate, revoke | Which endpoints exist |
| CSRF enforcement | What those endpoints do |
| Error translation to the wire format | Business logic and domain queries |
| Capability projection plumbing | Which capabilities are relevant per resource |
| Assurance and step-up gating | Route layout and mounting point |
| Correlation identifier propagation | Frontend served, if any |

*Source: D-052, LIB-HOST-003*

**Acceptance criteria**
1. No library code contains a host domain type name.
2. No host code sets a session cookie or validates a CSRF token.

---

**BFF-OWN-003** — The middleware pipeline SHALL be part of the public contract.

*Source: D-052, LIB-API-001*

Consumers depend on it, so it cannot change freely.

**Acceptance criteria**
1. The pipeline's mounting API and ordering guarantees are documented.
2. A contract test guards them.

---

## 3. Session handling

**BFF-SESS-001** — The BFF SHALL hold the session. The browser SHALL receive an
opaque identifier in a cookie and nothing else.

*Source: AUTH-SESS-003*

**Acceptance criteria**
1. No token, claim, permission or role name reaches the browser.
2. The cookie value yields no information when decoded.

---

**BFF-SESS-002** — The session cookie SHALL carry `httpOnly`, `Secure`, `SameSite`
(per BFF-CSRF-005), and the `__Host-` prefix. These SHALL NOT be configurable.

**Values (D-153).** The cookies the library sets are `__Host-janus-session`,
`__Host-janus-preauth` (BFF-CSRF-005a) and `__Host-janus-csrf` (BFF-CSRF-006), one set
per application origin; the `__Host-` prefix already scopes them.

*Source: AUTH-SESS-003, D-053*

Each is load-bearing. Making them configurable creates a path to an insecure
deployment through omission, which P-003's secure-by-default test prohibits.

**The `__Host-` prefix matters more than as a hardening flag.** It forbids a `Domain`
attribute, which is exactly the failure OWASP warns about: a cookie scoped to a
parent domain is shared by **every subdomain**, so one weak or CNAME'd subdomain
compromises every application. With four applications on subdomains of one domain,
this prefix is what keeps their sessions genuinely separate rather than nominally so.

**Acceptance criteria**
1. All four attributes are present on every issue.
2. No configuration key weakens any of them.

---

**BFF-SESS-003** — Each application SHALL hold its own session. Moving between
applications SHALL re-establish silently through the authentication application
(BFF-SESS-006).

*Source: AUTH-SESS-004, D-033.3*

**Acceptance criteria**
1. A compromised session in one application does not grant access to another.
2. Navigation between applications requires no re-authentication.
3. All sessions derive from one record; revoking it terminates every one.

---

**BFF-SESS-006** — The BFF SHALL establish its session from the auth session by the
**OIDC authorization code flow with PKCE**, acting as a **confidential client** of the
library's provider, and SHALL retain no token afterwards.

*Source: AUTH-SESS-012, D-104*

**What the BFF does.** With no session cookie present on a request that needs one, it
redirects the browser to `/oidc/authorize` with `prompt=none`, its registered
`redirect_uri`, a PKCE challenge, and a `state` bound to the pre-authentication
session (BFF-CSRF-005a). On return it validates `state`, exchanges the code
**back-channel** on the machine profile (BFF-MACH-001) with its client secret and PKCE
verifier, reads `sid` from the ID token, creates the per-app session bound to that
session record, rotates the cookie (BFF-SESS-004), and discards the token. On
`login_required` it redirects again without `prompt=none`, so the person signs in at
the authentication application and the flow repeats.

**What it does not do.** It never receives a refresh token, never stores an access or
ID token, never exchanges a code from the browser, and never accepts a `redirect_uri`
from a request — the registered value is the only one.

**Acceptance criteria**
1. A request to an application with no session and a live auth session completes
   with a per-app session and no user interaction.
2. The code exchange is server-to-server; no token appears in any browser response,
   header or cookie.
3. A mismatched `state`, a reused code, or a wrong PKCE verifier is rejected and the
   attempt logged.
4. After the exchange the BFF holds only its per-app session record; no token is
   persisted anywhere.
5. Revoking the session record ends the per-app session on the next request.

---

**BFF-SESS-004** — The BFF SHALL rotate the session identifier on authentication, on
step-up, and on privilege change.

*Source: AUTH-SESS-006*

**Acceptance criteria**
1. The identifier differs before and after each event.
2. The previous identifier is invalidated, not merely orphaned.

---

**BFF-SESS-005** — Logout SHALL terminate every application session and the
authentication session.

*Source: AUTH-SESS-008*

**Acceptance criteria**
1. After logout, navigating to another application requires authentication.

---

## 4. CSRF

Layered, because no single mechanism is sufficient and the current guidance is
explicit that SameSite is not a primary defence.

**Scope.** Section 4 governs the **browser profile** only. Machine traffic — the token
endpoint and provider callbacks — uses the separate profile in section 4a and carries
no CSRF protection, which is standard practice: providers do not send CSRF tokens.

**BFF-CSRF-001** — A **synchronizer token** bound to the server-side session SHALL be
required on every state-changing request **in the browser profile**. Enforcement SHALL
be in the pipeline, not per endpoint.

*Source: AUTH-SESS-007, D-053*

OWASP's guidance is that **stateful software should use the synchronizer token
pattern**; double-submit cookies are for stateless software. This BFF holds
server-side session records (AUTH-SESS-001), so it is stateful and the token binds to
the session rather than being self-validating.

**Acceptance criteria**
1. A state-changing request without a valid session-bound token is rejected and
   logged.
2. No endpoint can be excluded by configuration or attribute.
3. Adding a new endpoint inherits enforcement without action.

---

**BFF-CSRF-002** — A **Resource Isolation Policy** based on Fetch Metadata headers
SHALL reject cross-site state-changing requests.

Reject where `Sec-Fetch-Site` is `cross-site` and the request is state-changing.
Allow `same-origin`, `same-site`, and `none`. Allow simple top-level navigations.

*Source: D-053*

`Sec-Fetch-*` are forbidden request headers — **a browser will not let JavaScript set
them**, which makes them a stronger signal than anything the page can influence.

**On absence: fail closed, not open.** A missing `Sec-Fetch-Site` means a legacy
browser or a non-browser client. Fall back to token and Origin validation; do not
treat absence as permission.

**Acceptance criteria**
1. A cross-site POST is rejected before reaching any endpoint.
2. A request with no Fetch Metadata headers is still subject to token validation.
3. Same-site navigation between the applications is unaffected.

---

**BFF-CSRF-003** — State-changing requests SHALL require a custom request header, and
its absence SHALL reject.

**Values (D-153).** The header is `X-Janus-Request`; its presence is checked and its value
ignored. The frontend interceptor sets it on every request (FE-API-002).

*Source: D-053*

This relies on the same-origin policy: a cross-origin page cannot add a custom header
without a CORS preflight it cannot satisfy. OWASP identifies it as particularly
well-suited to XHR and API-driven endpoints, which is exactly this architecture.

**Acceptance criteria**
1. A request without the header is rejected regardless of other signals.
2. The frontend sets it on every state-changing call.

---

**BFF-CSRF-004** — The `Origin` header SHALL be validated against the expected target
where present.

*Source: D-053*

Defence in depth. Cheap, and catches cases the others miss.

**Acceptance criteria**
1. A mismatched `Origin` is rejected and logged.

---

**BFF-CSRF-005** — `SameSite` SHALL be **`Strict` for the management application** and
**`Lax` elsewhere**, and SHALL NOT be `None`.

*Source: D-053*

Current guidance is Strict where possible, Lax where necessary. The management
application has no external entry points, so Strict costs nothing. Public
applications receive inbound links — sign-in and verification links arrive as top-level
navigations, and Strict would withhold the session cookie on that first request.

**SameSite is defence in depth here, never the primary control.** OWASP is explicit
that it should not be relied on as a primary defence, and that a cookie must not be
scoped to a parent domain — which the `__Host-` prefix already enforces
(BFF-SESS-002).

**If the payment provider returns the browser by POST**, `Lax` withholds the cookie
and the session appears lost on return. Verify the provider's return method; where it
posts, the return must land on a GET route that then continues, rather than relying on
the session being present on the POST itself.

**Acceptance criteria**
1. The management application's cookie carries `SameSite=Strict`.
2. A sign-in link from email or SMS lands successfully on a public application.
3. No cookie is issued with `SameSite=None`.
4. A payment return preserves the session regardless of the provider's return
   method.

---

**BFF-CSRF-005a** — A **pre-authentication session** SHALL be issued on first contact,
serving as the CSRF token's binding target before a real session exists.

**Values (D-153).** The pre-authentication session lives `registration.session.lifetime`,
the registration session it carries being bound to it; no separate key. The notice
(PRIV-CONS-006a) states that lifetime.

*Source: D-092, D-146*

`/register`, `/auth/begin`, `/auth/link`, `/auth/factor` and `/recovery/begin` are
state-changing browser endpoints reached before any session exists. Without this, an implementer must
either invent an anonymous session or quietly exempt them — and exempting is precisely
what "no endpoint can opt out" prohibits.

**It rotates into the real session on authentication** (AUTH-SESS-006), which also
gives the fixation defence a defined starting point. It is also the binding target
for the `state` value of the sign-on redirect (BFF-SESS-006) and for the registration
session (BFF-CSRF-005b).

**Acceptance criteria**
1. A pre-authentication session is issued on first contact with any pre-session
   endpoint.
2. Its token is validated identically to an authenticated one.
3. Authentication rotates it rather than issuing a second session alongside.
4. It carries no identity and grants no access.

---

**BFF-CSRF-005b** — The **registration session** (REG-SESS-001) SHALL be bound to the
pre-authentication cookie of BFF-CSRF-005a, and every `/register/*` request SHALL be
refused from a browser that does not carry it. `GET /register/events` SHALL stream
server-sent events authenticated by that cookie alone, with **no token in any URL or
query parameter**; where the stream is unavailable the frontend polls `GET /register`.
A sign-in link or identifier-verification link SHALL land on a frontend route
(API-LAND-001) and SHALL reach the BFF **only on a press**; the BFF SHALL complete the
verification only when the request carries the cookie of the session that sent the
link, and SHALL otherwise answer with the code to type and change nothing.

*Source: D-146; REG-SESS-001, REG-SESS-003, AUTH-FACT-003, API-LAND-001*

The cookie is what makes "the browser that started the flow" a checkable fact rather
than a hope. A token in the URL of an event stream would appear in proxy logs, browser
history and referrer headers, so the stream authenticates the same way every other
browser request does. A landing route that acted on load would be completed by a mail
scanner's prefetch; the press is what turns an open into an intent, and the cookie is
what turns the press into the right person's intent.

**Acceptance criteria**
1. A `/register/*` request without the pre-authentication cookie of the session that
   created it is refused; `GET /register/events` without it answers as API-CONV-003
   requires and streams nothing.
2. No request or response of the registration flow carries a token in a URL; the event
   stream's only credential is the cookie.
3. Disconnecting the stream and polling `GET /register` yields the same state.
4. Loading a link landing route issues no state-changing request; a press from the
   originating browser verifies or signs in, a press from any other browser returns the
   code and changes nothing.

---

**BFF-CSRF-006** — The synchronizer token SHALL be obtainable by the first-party
frontend without a separate authenticated round trip, and SHALL rotate with the
session.

**Values (D-153).** The token is a script-readable `__Host-janus-csrf` cookie set beside the
session cookie and validated server side against the session.

*Source: AUTH-SESS-006, D-053*

**Acceptance criteria**
1. The frontend obtains the token on load.
2. Session rotation invalidates the previous token.

---

**BFF-CSRF-007** — Where the framework provides Fetch Metadata or antiforgery support,
it SHALL be used rather than reimplemented.

*Source: P-003, D-053*

The *standards over invention* test. Hand-rolled CSRF is a well-known source of
subtle failure.

**Acceptance criteria**
1. No hand-written token generation or comparison where framework support exists.

---

## 4a. Machine profile

**BFF-MACH-001** — A second pipeline profile SHALL exist for **non-browser traffic**,
carrying no session and no CSRF protection.

*Source: D-070*

Stalwart calling the token endpoint, the BFFs exchanging sign-on codes
(BFF-SESS-006), and the payment, shipping and SMS providers calling their callbacks,
send no cookie, no synchronizer token, no custom header and no Fetch Metadata. Under
the browser profile every one of them is rejected — mail authentication, sign-on and
all provider callbacks fail on day one.

**The no-opt-out property is preserved.** An endpoint's protection derives from **where
it is mounted**, not from a flag or attribute. A developer cannot accidentally place a
browser endpoint on this profile; it is a deliberate act.

**Acceptance criteria**
1. A browser endpoint cannot be moved to this profile by configuration or attribute.
2. Machine endpoints reject browser-originated requests carrying a session cookie —
   **except the break-glass endpoint**, which is reached from a browser and SHALL ignore
   any cookie present rather than refusing. Otherwise a stale cookie for the domain would
   produce an inexplicable refusal, during an emergency, for the system's one
   non-technical user.
3. The token endpoint and every callback authenticate successfully.

---

**BFF-MACH-002** — Where a provider supports signed callbacks, the **full standard**
SHALL be applied.

| Requirement | Detail |
|---|---|
| Signature | **The provider's published algorithm**, verified constant-time over raw bytes — not a fixed algorithm |
| Comparison | Constant-time |
| Timestamp window | Five minutes or less **where the scheme carries a timestamp**; older provider schemes may not |
| Payload | **Raw bytes**, verified before any parsing |
| Idempotency | Keyed on the provider's event identifier |
| Secret | From the secrets manager, rotatable with an overlap window |

**Values (D-153).** A callback signing secret rotates with a 24 hour overlap during which
both secrets verify.

*Source: D-070*

Signature verification is the accepted baseline for webhook security and is not
optional where available. Parsing before verification breaks it — re-serialised JSON
does not match the signed bytes.

**Acceptance criteria**
1. An unsigned or mis-signed request is rejected before parsing.
2. A replayed request outside the timestamp window is rejected.
3. A duplicate event identifier is processed once.
4. Comparison is constant-time; a test asserts it.

---

**BFF-MACH-003** — Where signing is **not available**, the callback SHALL be treated as
a **hint that triggers verification against the provider's API** — never as a fact.

**Values (D-153).** The endpoints accept `integration.callback.ratelimit` requests per
source per minute; `alerting.callback.threshold` rejected callbacks from one source in
an hour raise `callback-verification-failed`.

*Source: D-070, INT-GEN-003*

Verified against the providers' documentation: the shipping provider offers no signing
secret or signature header; the SMS gateway calls over plain HTTP with parameters in
the query string, which cannot carry a signature meaningfully.

**This is the primary control for unsigned callbacks**, not a secondary one. A parcel
is not marked delivered because something said so — the provider is asked.

Supporting controls: unguessable correlation references, rate limiting, and source
restriction where the provider publishes ranges.

**Acceptance criteria**
1. No unsigned callback advances authoritative state without independent
   confirmation.
2. A forged callback with a guessed reference is rejected and logged.
3. Repeated verification failures raise an alert.

---

## 5. Capability projection

**BFF-CAP-001** — Resource responses SHALL carry the capabilities the caller holds
for that record, **each with the requirements that remain** (`requires`).

*Source: AUTHZ-GATE-005, API-CAP-001, D-078*

A capability means "permitted by grants, subject to session gates" — see API-CAP-001
for the wire format and the reasoning.

**Acceptance criteria**
1. A list of 50 records returns capabilities without additional queries.
2. A capability with an empty `requires` always succeeds.
3. A capability with `requires` carries the gates that remain, never an empty list.

---

**BFF-CAP-002** — The library SHALL provide the projection plumbing; the host SHALL
declare which capabilities are relevant per resource type.

*Source: D-052, AUTHZ-MODEL-003*

The library cannot know that a document has a "share" action. The host declares it;
the library computes and attaches it.

**Acceptance criteria**
1. Declaring a new capability requires no library change.
2. An undeclared capability never appears in a response.

---

**BFF-CAP-003** — Capabilities SHALL be computed in the same query as the resource.

*Source: AUTHZ-GATE-005*

Per-row computation produces N+1 queries, which is why this is a requirement rather
than an implementation note.

**Acceptance criteria**
1. Query count is independent of result size.

---

## 6. Error translation

**BFF-ERR-001** — Errors SHALL cross the boundary as machine-readable codes with
structured data and a correlation identifier. Rendered prose SHALL NOT cross it, and
the BFF SHALL NOT return HTML intended for a person to read — including framework
default error pages.

*Source: LIB-API-003, API-CONV-002, D-054*

**Acceptance criteria**
1. No response body contains a user-facing sentence.
2. Every error carries a correlation identifier resolving to audit and logs.
3. Every code appears in `10-reference.md`.

---

**BFF-ERR-002** — The BFF SHALL NOT leak internal fault detail. Faults SHALL surface
as a generic code with the detail logged against the correlation identifier.

**Values (D-153).** The generic code is `system.fault`, status 500, the body per
API-CONV-002 carrying the correlation identifier and nothing else.

*Source: CONV-ERR-001, P-003*

**Acceptance criteria**
1. No stack trace, connection string, or internal type name reaches a response.
2. The detail is retrievable from logs by correlation identifier.

---

**BFF-ERR-003** — Concealment responses SHALL be produced by the pipeline, uniformly
per resource type, indistinguishable in body, headers **and timing** from a genuine
not-found.

*Source: AUTHZ-CONCEAL-001, AUTHZ-CONCEAL-002*

Timing is the part that leaks. A concealed denial arriving later because it ran a
permission check first has disclosed the answer regardless of its content.

**Acceptance criteria**
1. Bodies are byte-identical.
2. Timing distributions overlap within noise: verified by construction (one code path, fixed-time comparison, identical bytes), asserted by the byte identity, and named in the report as verified by construction (CONV-TEST-007, D-153).
3. Uniformity is enforced by the pipeline, not by endpoint discipline.

---

## 7. Step-up

**BFF-STEP-001** — Where a request requires step-up, the BFF SHALL reject it with the
step-up code rather than performing it.

*Source: AUTH-STEP-001*

The frontend initiates the step-up flow and retries. The BFF does not redirect
mid-request.

**The same shape applies to session expiry** (`auth.session.expired`, AUTH-SESS-005,
D-123, D-139): the BFF rejects with the code and `details.reauthenticate` —
`single-factor` for an idle expiry inside the absolute window where the principal's
policy's `requiredAssurance` is `aal2`, `full` otherwise (always `full` for a customer) — and never
redirects.

The `details` of the identity API's `auth.stepup.required` response — the three gate
values, the `outcome` and the `options` (`09` `/auth/step-up`, D-141) — pass through
to the frontend unchanged; the BFF adds nothing and removes nothing.

**Acceptance criteria**
1. The rejection names what is required — level, phishing-resistance and maximum
   age — never a factor; the `options` list of presentable combinations is passed
   through as the identity API supplied it.
2. Retrying after successful step-up succeeds without re-submitting business data
   the user would have to re-enter.
3. An expired-session rejection carries `details.reauthenticate`; retrying after
   reauthentication succeeds with the original data.

---

## 8. Throttling and abuse

**BFF-ABUSE-001** — Throttling responses SHALL carry the retry interval and SHALL be
identical whether or not the account exists. A send refused by a restriction
(AUTH-ABUSE-004) SHALL cross the boundary as `auth.restriction.exceeded` with
`retryAt`, identical whether or not the address is registered.

*Source: AUTH-ABUSE-002, AUTH-ABUSE-003, D-146*

**Acceptance criteria**
1. Responses and timing are indistinguishable across existence.
2. The remaining interval is communicated.
3. A refused send for a registered and for an unregistered address produces identical
   responses carrying `retryAt`.

---

**BFF-ABUSE-002** — The BFF SHALL NOT expose enumeration through differential
responses, timing, or error granularity.

*Source: AUTH-ABUSE-003, API-CONV-005*

**Acceptance criteria**
1. Registration, sign-in initiation, recovery initiation, sign-in link and email code
   request all return uniform responses.

---

## 9. Logging

**BFF-LOG-001** — Every request SHALL carry a correlation identifier through
logging, audit, and any error response.

*Source: CONV-LOG-002, AUTHZ-CONCEAL-004*

**Acceptance criteria**
1. A denial's correlation identifier resolves to that request's log entries.

---

**BFF-LOG-002** — The BFF SHALL NOT log request or response bodies for endpoints
carrying order data.

**Values (D-153).** An endpoint carrying order data is one the host marks with the
`SensitiveBody` endpoint metadata (LIB-HOST-001); the library has no order concept.
Body logging is off for a marked endpoint whatever the setting.

*Source: CONV-LOG-003, PRIV-SENS-003*

Order contents are health data. Body logging at the boundary is the most likely place
for it to leak, because it looks like ordinary diagnostics.

**Acceptance criteria**
1. Body logging is off by default and cannot be enabled for order endpoints.
2. A test asserts no log entry contains an order line item.

---

## 10. Pipeline order

**BFF-ORDER-001** — Middleware SHALL execute in this order, and the ordering SHALL be
part of the public contract.

| # | Stage | Why here |
|---|---|---|
| 1 | Correlation identifier assignment | Everything after must be traceable |
| — | *(machine profile diverges here — see §4a)* | No session, no CSRF |
| 2 | Fetch Metadata resource isolation | Cheapest rejection; no session lookup needed |
| 3 | Origin validation | Same — cheap, no state |
| 4 | Source-based rate limiting | Before any expensive work, including session lookup |
| 5 | Session resolution | Identity established |
| 6 | CSRF synchronizer token validation | Requires the session; must precede any state change |
| 7 | Account-based throttling | Requires identity, so cannot be earlier |
| 8 | Assurance and step-up gating | Requires the session's assurance properties |
| 9 | Host endpoint | |
| 10 | Capability projection | Requires the result set |
| 11 | Error translation and concealment | **Last**, so nothing earlier has disclosed existence |

**Values (D-153).** Stage 4 admits `abuse.source.ratelimit` requests per source address
per minute, sliding, and answers 429 `auth.throttled` beyond; sized for many people
behind one address.

*Source: D-052, D-053*

**Throttling is two stages, not one.** Source-based limiting must precede session
lookup to protect against resource exhaustion; account-based limiting cannot, because
it needs an identity. An earlier draft collapsed them and placed both after CSRF,
which would have let an unauthenticated flood reach session resolution.

**Acceptance criteria**
1. A host cannot reorder or insert middleware between security stages.
2. Host middleware may be added before stage 1 or after stage 9 only.
3. A flood of unauthenticated requests is rejected before session lookup.

---

## 11. Open items

None. Frontend obligations arising from this boundary are `18-frontend-integration`.
