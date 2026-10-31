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

**BFF-OWN-001** — The library SHALL ship the BFF pipeline as middleware the host mounts.
The host SHALL NOT implement session handling, CSRF enforcement, capability projection,
or error translation itself. The host mounts the browser profile with
`UseBrowserProfile`, the machine profile (BFF-MACH-001) with `UseMachineProfile`, and
the library's routes with `MapIdentityEndpoints` and `MapIdentityWellKnown`; nothing
else is required to mount it, save that the host places its routing before the browser
profile for an enrolment session to reach the enrolment routes (BFF-ORDER-001, D-210).
The host's own endpoints, mounted after the browser profile, read who is asking from the
request (BFF-HOST-001), need a session unless marked as answering anyone (BFF-HOST-002),
carry their records' capabilities through `CapabilityFields` (BFF-CAP-002) and answer
the library's results through the library's own answers (BFF-HOST-003), so the host
builds none of the four for them either (D-202).

**Values (D-162).** Which application a process serves is declared once, at
registration: `AddJanus` takes an `ApplicationKind` (`Management` · `Public`), and the
mount takes nothing. It decides `SameSite` and nothing else (BFF-CSRF-005). The unset
value is `Management`, so an undeclared kind yields `SameSite=Strict`.

*Source: D-052, D-162, D-166, D-202, D-210*

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
4. The application kind is an argument of `AddJanus`, not of the mount, and its unset
   value issues `SameSite=Strict` cookies.

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
| Who is asking, read for the host's endpoints (BFF-HOST-001) | Which of its endpoints answer anyone, and which permission each asks (BFF-HOST-002) |

*Source: D-052, LIB-HOST-003, D-202*

**Acceptance criteria**
1. No library code contains a host domain type name.
2. No host code sets a session cookie or validates a CSRF token.

---

**BFF-OWN-003** — The middleware pipeline SHALL be part of the public contract.

*Source: D-052, LIB-API-001, D-166*

Consumers depend on it, so it cannot change freely.

**Acceptance criteria**
1. The pipeline's mounting API and ordering guarantees are documented.
2. A contract test guards them; the browser profile's stage order is one of the contract
   lists the release gate compares (REF-001 AC2).

---

## 2a. Host endpoints

**BFF-HOST-001** — A host's own endpoint SHALL learn who is asking from the request
through the public surface, and SHALL never read or resolve the session itself.

**Values (D-202).** `IRequestAccess` (`Janus.Core`), registered by `AddJanus` in each
request scope, answers `Current()`: a `Result<RequestAccess>` holding the access of the
request being served, or the failure `auth.session.expired` where there is none
(CONV-DESIGN-005). A `RequestAccess` carries `Context`, the `AccessContext` the
library's own endpoints pass for the same request, its break-glass reason included where
the session is a break-glass session or one another application opened from it
(OPS-BOOT-002); and `Session`, the `SessionId` the library's own endpoints pass to an
operation for it, which names the session record and survives a rotation: at a step-up
or a privilege change BFF-SESS-004 rotates the identifier the session's cookie carries,
never the `SessionId` (D-206). A `RequestAccess` carries no assurance: a host never
judges assurance, and the gate it asks reads the session record and the account's
reachable assurance when it judges (AUTH-STEP-002, AUTHZ-GATE-005), as it does for the
library's own endpoints (D-205). There is no `RequestAccess` where stage 5 left the
request without a live sign-in session (none presented, one that has ended, or only a
pre-authentication, registration or enrolment session), and none outside the browser
profile or outside a request (the machine profile, a job). It is what stage 5
established, read and never looked up again (BFF-ORDER-001). A host endpoint passes
`Context` and `Session` to every operation and gate it asks for the person, and keeps
neither past the request it read them for, so a break-glass session's reason never
reaches later work (D-170). A host whose requests carry the library's sessions takes a
person's context from here and never builds one; `AccessContext.Of(SubjectId)` is for a
host that consumes authorization without the library's authentication (LIB-HOST-004),
for the conformance suite and for tests (AUTHZ-IMP-001).

*Source: D-202, D-205, D-206*

The library's own endpoints read the session the pipeline resolved; a host's endpoint is
the same kind of endpoint and reads the same thing. A context the host built for the
person itself would not carry a break-glass session's reason; the step-up the session
proved is judged against the session itself, wherever the context's acting subject is
the session's (AUTHZ-GATE-005).

**Acceptance criteria**
1. In a request carrying a live sign-in session, `Current()` holds the context and the
   `SessionId` the library's own endpoints use for the same request, and an operation
   called with them answers as it answers the library's endpoint.
2. `Current()` answers `auth.session.expired` for a request carrying no session, an ended
   one, or only a pre-authentication, registration or enrolment session, and in a job.
3. In a break-glass session the context carries the break-glass reason, and a record a
   host's call writes in it carries the reason.
4. No public member of the library makes a context whose acting and effective identities
   differ.
5. After a step-up rotates the identifier the session's cookie carries (BFF-SESS-004),
   the next request's `Current()` names the same `SessionId`, and a request carrying the
   cookie from before the rotation has no `RequestAccess`.
6. A gate asked from a host endpoint judges the session as it judges for the library's
   own endpoints: a session downgraded since it reached `aal2` does not meet an `aal2`
   gate on the strength of the instant before the downgrade.

---

**BFF-HOST-002** — A host endpoint SHALL need a live sign-in session unless it says
where it is mounted that it answers anyone, and the pipeline SHALL refuse it as it
refuses the library's own.

**Values (D-202, D-211).** Every endpoint the host maps after the browser profile
requires a live sign-in session, whatever routing selects it: a route the host maps, and
an endpoint MVC or Razor Pages resolves a dynamic route to (`MapDynamicControllerRoute`,
`MapFallbackToController`, `MapDynamicPageRoute`, `MapFallbackToPage`), alike: stage 8
refuses a request that reaches it without one with 401 `auth.session.expired`, carrying
`details.reauthenticate` where a sign-in session ended (BFF-STEP-001) and the code alone
where none was held, before the endpoint runs. `AllowNoSession()` (`Janus.Hosting`), on
an endpoint's convention builder, or `[AllowNoSession]` (`AllowNoSessionAttribute`,
`Janus.Hosting`) on a controller, an action or a Razor page, marks one that answers
anyone (a catalogue page, a checkout open to everyone); stage 8 reads the mark of the
endpoint routing selects, the nearer mark standing. The only endpoints stage 8 holds to
no session are those ASP.NET Core's matcher policies make themselves for a path routing
matched with a method or a content type the path does not take
(`HttpMethodMatcherPolicy`'s 405 endpoint, `AcceptsMatcherPolicy`'s 415), each an
`Endpoint` with no metadata, recognised by that and by the display name the framework
gives it: they answer as they do today (API-CONV-003), and one the framework renamed
would ask for a session, which fails closed. A marked endpoint still passes stages 2 to
6: it is a pre-session endpoint, so the browser profile issues the pre-authentication
session to a request that reaches it carrying none (BFF-CSRF-005a), and a state-changing
request to it carries that session's synchronizer token or is refused at stage 6
(BFF-CSRF-001). The library's own endpoints keep their own marks. **Step-up stays on the
action, never on the endpoint:** a host binds a gate to its permission (`StepUpGate`,
AUTHZ-GATE-005), and `IAccessGate` answers `auth.stepup.required` with its `details`
when the endpoint asks for that permission (`RequireAsync`, `FilterAsync`,
`FragmentAsync`) on a record its grants admit and the request's session does not meet
the gate. A host asks a bound permission of the gate before it begins the unit of work
its context joins (`UseUnitOfWorkAsync`), since the step-up may ask the phone signal, a
host callback that never runs inside an open transaction (`08` CONV-DESIGN-003, D-207).
`IStepUpGate` is the gate one library area asks of another (LIB-API-001) and names only
the library's actions. Stage 8 judges the endpoint routing matched for the request, and
SHALL hold whatever the order in which the host adds routing (BFF-OWN-001): where the
host's routing runs after the browser profile, so that a request passes stage 8 with no
endpoint matched yet, the endpoint routing then selects is judged as stage 8 judges it,
before it runs, by a matcher policy `AddJanus` registers (an `IEndpointSelectorPolicy`
of ASP.NET Core's routing), which applies wherever the candidates hold an endpoint that
needs a session or a dynamic one (`ContainsDynamicEndpoints`), and replaces an endpoint
the request may not reach with the profile's refusal (BFF-ORDER-001, D-209, D-211).

*Source: D-202, D-207, D-209, D-211*

A forgotten mark fails closed: a host endpoint left unmarked answers 401, never anyone
(P-003; protection comes from where an endpoint is mounted, D-070). A step-up mark on the
endpoint would state a second time a rule the permission's binding already states; the
two could disagree, and an endpoint asking two permissions could not express it.

**Acceptance criteria**
1. A request without a live sign-in session to an unmarked host endpoint is answered 401
   `auth.session.expired` before the endpoint runs, with `details.reauthenticate` where a
   sign-in session ended.
2. A host endpoint marked with `AllowNoSession()` is reached by a `GET` carrying no
   session, which is issued the pre-authentication session, and by a state-changing
   request carrying that session and its token; one without the token is refused at
   stage 6.
3. A host endpoint asking `IAccessGate` for a permission bound to a gate the request's
   session does not meet, on a record its grants admit, is answered
   `auth.stepup.required` with the gate's `details` (BFF-STEP-001).
4. The library's own endpoints answer a request carrying no session as before.
5. With the host's routing added after the browser profile, a request without a live
   sign-in session to an unmarked host endpoint is answered 401 `auth.session.expired`
   before the endpoint runs, and one to an endpoint marked with `AllowNoSession()`
   reaches it (D-209).
6. With the host's routing before the browser profile and after it, a request without a
   live sign-in session to an unmarked controller action reached through
   `MapFallbackToController` is answered 401 `auth.session.expired` before it runs, one
   to an action marked with `[AllowNoSession]` reaches it, and one with a method a
   served host path does not take is answered 404 `authz.resource.notfound` (D-211). A
   Razor page reached through `MapFallbackToPage` is judged by the same rule, which
   reads the endpoint's metadata whatever its type, verified in review (D-215).

---

**BFF-HOST-003** — A host endpoint SHALL answer the library's results in the library's
wire format through the public surface, never by translating a code itself.

**Values (D-202).** `Answers` (`Janus.Hosting`) answers a `Result` or a `Result<T>` as
the library's own endpoints do: the success the endpoint gives, or the failure in the
API-CONV-002 body, with the status `10` section 1 gives its code, its `details` and the
request's correlation identifier (BFF-ERR-001); `Answers.Refused` answers an `Error`
alone. A refusal of a record of a concealing type is still answered by stage 11 whatever
the endpoint writes (BFF-ERR-003). A code `10` section 1 does not list is a fault of the
calling code (CONV-ERR-001), answered as every fault is (BFF-ERR-002). A host's own codes,
for its own business, are the host's to answer.

*Source: D-202*

**Acceptance criteria**
1. A failure answered through `Answers` carries the code, its `details`, the status `10`
   section 1 gives and the request's own correlation identifier, and is otherwise byte for
   byte what the library's own endpoint answers for the same failure.
2. A gate refusal of a record of a concealing type in a host endpoint is answered as
   BFF-ERR-003 states, whatever the endpoint wrote.
3. A code `10` section 1 does not list is answered 500 `system.fault`.

---

**BFF-HOST-004** — A host endpoint SHALL pass an operation the request's source as the
library's own endpoints do.

**Values (D-202).** `RequestSource.Of(HttpRequest)` (`Janus.Hosting`) answers the source
an operation takes for the request, keyed exactly as AUTH-ABUSE-001 keys it: the address
the connection arrived on as the framework's trusted proxies resolve it, an IPv4-mapped
address as its IPv4 address, an IPv6 address as its /64 save one whose first three bits
are `000`, which is keyed whole, and `unknown` where there is none.

*Source: D-202*

**Acceptance criteria**
1. For the same request, `RequestSource.Of` answers what the library's own endpoints
   pass; two addresses of one IPv6 /64 answer one source, an IPv6 address whose first
   three bits are `000` its whole address, an IPv4-mapped address its IPv4 address, and a
   request with no address `unknown`.

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

**Values (D-153).** The cookies the library sets are `__Host-identity-session`,
`__Host-identity-preauth` (BFF-CSRF-005a), `__Host-identity-csrf` (BFF-CSRF-006),
`__Host-identity-device` (the trusted device, AUTH-FACT-015) and
`__Host-identity-browser` (the new-device check, AUTH-FACT-016), one set per application
origin; the `__Host-` prefix already scopes them. The library sets no other cookie. The
pre-authentication cookie ends with the browser's session. The session cookie, and the
CSRF cookie beside it, last until the session's absolute expiry where the session's
policy requires `aal1`, so a customer stays signed in across a browser's restart as
AUTH-SESS-005 intends, and end with the browser's session where it requires `aal2`, so a
staff session also ends when its browser closes; a CSRF cookie bound to a
pre-authentication session ends with the browser's session too, and a cookie issued
again at a rotation follows the policy then in force. The trusted-device cookie lives
`factor.trusteddevice.lifetime` and the remembered-browser cookie
`device.verification.lifetime`; no third party can read any of them. The privacy notice
lists them so, the session and CSRF cookies at `session.default.absolute`, the longest
they last (PRIV-CONS-006a, D-203). Whatever a cookie carries, the session record's own
lifetimes decide whether it is honoured (AUTH-SESS-005).

*Source: AUTH-SESS-003, D-053, D-166, D-203*

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
3. Under a policy requiring `aal1` the session and CSRF cookies carry an expiry at the
   session's absolute expiry; under one requiring `aal2` they carry none, and neither
   does the pre-authentication cookie nor a CSRF cookie bound to a pre-authentication
   session.

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
**OIDC authorization code flow with PKCE**, its authorization request pushed (RFC 9126,
AUTH-OIDC-006), acting as a **confidential client** of the library's provider, and SHALL
retain no token afterwards.

*Source: AUTH-SESS-012, D-104, D-162, D-164, D-166, D-171, D-172, D-192, D-193, D-194,
D-195, D-196, D-198, D-199, D-229*

**What the BFF does.** Both halves are the library's. With no per-app session, the
frontend sends the browser to `GET /auth/signon?returnTo=<path>` (FE-API-006), which
pushes the authorization request to `POST /oidc/par` at the provider address the host
declares (LIB-HOST-001), **back-channel** on the machine profile (BFF-MACH-001), with
its client secret, `prompt=none`, its registered `redirect_uri`, an `S256` PKCE
challenge and a `state` bound to the pre-authentication session (BFF-CSRF-005a,
AUTH-OIDC-006). It holds the verifier on the pre-authentication session, encrypted under
the deployment's data key (PRIV-RIGHT-005a), and redirects the browser to
`/oidc/authorize` with the client identifier and the returned `request_uri` alone. On
return, `GET /auth/signon/return` forgets the attempt, validates `state`, exchanges the
code back-channel with its client secret and PKCE verifier, reads `sid` from the ID
token, creates the per-app session bound to that session record, rotates the cookie
(BFF-SESS-004), discards the token and sends the browser to the stored return address,
the `returnTo` its start judged and kept, followed only as a path of this application
(`09` section 3, API-REDIR-001, D-198); a `returnTo` the start replaces with `/` is
logged by the start alone (API-REDIR-001, D-199). The per-app session takes the record's
instants and last downgrade, never the instant of derivation (AUTH-SESS-012, D-192), and
carries its break-glass reason where it has one (OPS-BOOT-002, D-171). On
`login_required` it pushes again without `prompt=none`, so the person signs in at the
authentication application and the flow repeats. The sign-on is a navigation route
(BFF-ERR-001): a `state` that is absent, unbound or not the one the browser was sent out
with is refused with `auth.session.csrfinvalid` and sent nowhere; any other refusal
returns the browser with 302, to `returnTo` at the start and to the stored return
address at the return, with `error` `auth.session.expired`, the `error` or the library's
own code it carried inside logged beside it (BFF-LOG-001, D-194), while a fault stays a
fault: where no pre-authentication session can be issued, or where a push or an exchange
cannot reach the authentication application, is answered a 5xx, or reads no answer in
its protocol's shape (BFF-ERR-002, D-193); every error the push reads, every error of
the exchange but a 400 `invalid_grant`, and an authorization response's `server_error`
or `temporarily_unavailable` are faults too (D-194), and so are the application's client
in no registry, its secret that cannot be read, an identity token from the
authentication application that does not hold up (its signature under that application's
published keys, its issuer, its audience or its expiry) or carries no session identifier
in `sid` (D-196), that application's published keys that cannot be read, and a
derivation code whose row names a fault or that no row names; a return carrying neither
`code` nor `error`, and a token whose `sid` names a session that has ended or is not
found, are refusals (D-195, D-196), and so is a return whose `error` does not read as
RFC 6749 Appendix A.7 defines one or is longer than 64 characters, its inside code
`api.request.malformed` and the value in no entry the library writes (D-196); a refusal
is logged once, and a fault the push, the exchange or the authorization response
answered logs, at Error, the status and `error` read (D-195); a fault of the sign-on is
logged at Error and at no other level, the fault of a secret or keys that cannot be read
carrying the code and details the read answered, that of a token that does not hold up
the validation's own fault as its inner fault, and a client in no registry and a token
carrying no session identifier in `sid` each adding one entry at Error naming it
(BFF-LOG-001, D-196); a start from a browser with no pre-authentication session issues
one (BFF-CSRF-005a, D-192).

**Values (D-166).** The client is the one the host declares for this application
(`SignOnClient`, LIB-HOST-001). Its secret is the one the library generated for that
client, read from the registry at each call that presents it (OPS-SEC-002); the
application holds no secret of its own and caches none. The same half makes the provider
probes of the conformance suite (LIB-TEST-001) as this client and under the same rule
(D-172). The back-channel calls use the named HTTP client `identity-signon`, which a host
may configure.

**What it does not do.** It never receives a refresh token, never stores an access or
ID token, never exchanges a code from the browser, and never accepts a `redirect_uri`
from a request — the registered value is the only one.

**Where it runs (D-229).** The sign-on belongs to an application at an origin apart from
the authentication application's. An application served at that application's own
origin is served by the sign-in session, the session that origin's cookie carries
(BFF-SESS-002), and derives none; its frontend starts no sign-on (FE-API-006).

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

On a step-up and on a privilege change the identifier rotated is the one the session's
cookie carries, and the session record and its `SessionId` are kept (BFF-HOST-001); on
authentication the pre-authentication session rotates into the sign-in session
(BFF-CSRF-005a, D-206).

*Source: AUTH-SESS-006, D-206*

**Acceptance criteria**
1. The identifier the session's cookie carries differs before and after each event.
2. The previous such identifier is invalidated, not merely orphaned.

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

**One answer.** Every layer of this section (BFF-CSRF-001 to BFF-CSRF-004) SHALL refuse
with `auth.session.csrfinvalid`, 403 (API-CONV-004), and with nothing that tells the
layers apart; the log entry each refusal writes SHALL name the layer that refused.

**BFF-CSRF-001** — A **synchronizer token** bound to the server-side session SHALL be
required on every state-changing request **in the browser profile**. Enforcement SHALL
be in the pipeline, not per endpoint.

**Values (D-162, D-163).** The token is presented in the `X-Identity-Csrf` request
header and compared with the token bound to the session, or to the pre-authentication
session before one exists (BFF-CSRF-005a). It is not carried in `X-Identity-Request`,
whose presence alone BFF-CSRF-003 checks.

*Source: AUTH-SESS-007, D-053, D-162, D-166*

OWASP's guidance is that **stateful software should use the synchronizer token
pattern**; double-submit cookies are for stateless software. This BFF holds
server-side session records (AUTH-SESS-001), so it is stateful and the token binds to
the session rather than being self-validating.

**Acceptance criteria**
1. A state-changing request without a valid session-bound token is rejected and
   logged.
2. No endpoint can be excluded by configuration or attribute.
3. Adding a new endpoint inherits enforcement without action.
4. A state-changing request refused by fetch metadata, by the custom header, by the
   origin or by the token answers the same 403 `auth.session.csrfinvalid`; only the log
   entry names the layer.

---

**BFF-CSRF-002** — A **Resource Isolation Policy** based on Fetch Metadata headers
SHALL reject cross-site state-changing requests.

Reject where `Sec-Fetch-Site` is `cross-site` and the request is state-changing.
Allow `same-origin`, `same-site`, and `none`. Allow simple top-level navigations. One
cross-site `POST` is answered other than by refusal: where `Sec-Fetch-Mode` is
`navigate`, `Sec-Fetch-Dest` is `document` and the request carries no session cookie,
it is answered 303 with its own path and query as a relative `Location`; its body is not
read and nothing of it reaches a later stage or an endpoint (BFF-CSRF-005).

*Source: D-053, D-166*

`Sec-Fetch-*` are forbidden request headers — **a browser will not let JavaScript set
them**, which makes them a stronger signal than anything the page can influence.

**On absence: fail closed, not open.** A missing `Sec-Fetch-Site` means a legacy
browser or a non-browser client. Fall back to token and Origin validation; do not
treat absence as permission.

**Acceptance criteria**
1. A cross-site POST reaches no endpoint: it is refused with `auth.session.csrfinvalid`,
   or, as a top-level document navigation carrying no session cookie, answered 303 to
   its own address.
2. A request with no Fetch Metadata headers is still subject to token validation.
3. Same-site navigation between the applications is unaffected.

---

**BFF-CSRF-003** — State-changing requests SHALL require a custom request header, and
its absence SHALL reject.

**Values (D-153).** The header is `X-Identity-Request`; its presence is checked and its value
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

*Source: D-053, D-166*

Current guidance is Strict where possible, Lax where necessary. The management
application has no external entry points, so Strict costs nothing. Public
applications receive inbound links — sign-in and verification links arrive as top-level
navigations, and Strict would withhold the session cookie on that first request.

**SameSite is defence in depth here, never the primary control.** OWASP is explicit
that it should not be relied on as a primary defence, and that a cookie must not be
scoped to a parent domain — which the `__Host-` prefix already enforces
(BFF-SESS-002).

**If a host's processor returns the browser by POST** (a hosted page that posts back,
for example), `Lax` withholds the cookie and the session appears lost on return. The
host verifies each such processor's return method; where one posts, the host's return
route must be a GET that then continues, rather than relying on the session being
present on the POST itself. The library's own flows return by GET, except the social
provider's return, which may arrive as a cross-site form post and is carried by the
machine-profile route `/callbacks/providers/{provider}/return` to a GET continuation on
the browser profile (BFF-MACH-001).

**Acceptance criteria**
1. The management application's cookie carries `SameSite=Strict`.
2. A sign-in link from email or SMS lands successfully on a public application.
3. No cookie is issued with `SameSite=None`.
4. A cross-site POST return carrying no session cookie is answered 303 to the same
   address, so the browser continues on the host's GET route with its lax cookie; the
   host's route there is a GET that asks the processor for the outcome, and the library
   documents the pattern on `UseBrowserProfile`.

---

**BFF-CSRF-005a** — A **pre-authentication session** SHALL be issued on first contact,
serving as the CSRF token's binding target before a real session exists.

**Values (D-153).** The pre-authentication session lives `registration.session.lifetime`,
the registration session it carries being bound to it; no separate key. Its cookie ends
with the browser's session, as the notice lists it (BFF-SESS-002, PRIV-CONS-006a, D-203).

*Source: D-092, D-146, D-166, D-202, D-203, D-224, D-226*

`/register`, `/auth/begin`, `/auth/link`, `/auth/factor` and `/recovery/begin` are
state-changing browser endpoints reached before any session exists. Without this, an implementer must
either invent an anonymous session or quietly exempt them — and exempting is precisely
what "no endpoint can opt out" prohibits.

**It rotates into the real session on authentication** (AUTH-SESS-006), which also gives
the fixation defence a defined starting point. It is also the binding target for the
`state` value of the sign-on redirect (BFF-SESS-006), for the `state` of a provider
round trip started to sign in or register (IDN-LIFE-012; one started to link is bound to
the session), for the registration session (BFF-CSRF-005b), for each sign-in challenge
opened from it (`09` `POST /auth/begin`, D-226), and for the browser's pending
authorization (AUTH-SESS-012, D-224), which the rotation carries over to the session it
establishes (D-226).

**Requests that carry no cookie (D-224).** Two requests that carry no cookie hold
nothing that ties them to one browser, so each is issued a pre-authentication session
and a CSRF token of its own, and the later answer's cookies replace the earlier's. A
frontend holding none of the library's cookies therefore makes its first request alone
and sends the rest once its answer has set them (`18` FE-API-002). A token accepted from
any browser within a window would bind nothing.

**Acceptance criteria**
1. A pre-authentication session is issued on first contact with any pre-session
   endpoint, a host endpoint marked with `AllowNoSession()` among them (BFF-HOST-002).
2. Its token is validated identically to an authenticated one.
3. Authentication rotates it rather than issuing a second session alongside.
4. It carries no identity and grants no access.

---

**BFF-CSRF-005b** — The **registration session** (REG-SESS-001) SHALL be bound to the
pre-authentication cookie of BFF-CSRF-005a, and every `/register/*` request SHALL be
refused from a browser that does not carry it, except a request presenting a message's
`linkToken` (`POST /register/verify/{id}`, `POST /register/abandon`), which is taken so
that a press can be counted against its source (REG-SESS-003). `GET /register/events`
SHALL stream server-sent events authenticated by that cookie alone, with **no token in
any URL or query parameter**; where the stream is unavailable the frontend polls
`GET /register`. Every link, whatever its kind, SHALL land on a frontend route
(API-LAND-001) and SHALL reach the BFF **only on a press**, never on load. For a sign-in
link or an identifier-verification link, the BFF SHALL complete the verification only
when the request carries the cookie of the session that sent the link, and SHALL
otherwise answer with the code to type and change nothing. Every link lands on `/link`
with its token in the fragment (FE-VER-001); a fragment is no part of a request's target
(RFC 9110), so the token reaches the BFF only in the body of the call the landing makes.

*Source: D-146; REG-SESS-001, REG-SESS-003, AUTH-FACT-003, API-LAND-001, D-166, D-183,
D-187*

The cookie is what makes "the browser that started the flow" a checkable fact rather
than a hope. A token in the URL of an event stream would appear in proxy logs, browser
history and referrer headers, so the stream authenticates the same way every other
browser request does. A landing route that acted on load would be completed by a mail
scanner's prefetch; the press is what turns an open into an intent, and the cookie is
what turns the press into the right person's intent.

**Acceptance criteria**
1. A `/register/*` request without the pre-authentication cookie of the session that
   created it is refused, except a request presenting a message's `linkToken`;
   `GET /register/events` without it answers 404 `authz.resource.notfound` (`09`
   `GET /register/events`) and streams nothing.
2. No request or response of the registration flow carries a token in a URL; the event
   stream's only credential is the cookie.
3. Disconnecting the stream and polling `GET /register` yields the same state.
4. Loading a link landing route issues no state-changing request; a press from the
   originating browser verifies or signs in, a press from any other browser returns the
   code and changes nothing, save the kinds that act from any browser
   (`identifier-confirm`, `undo` and the other link-borne kinds of `18` FE-VER-001).

---

**BFF-CSRF-006** — The synchronizer token SHALL be obtainable by the first-party
frontend without a separate authenticated round trip, and SHALL rotate with the
session.

**Values (D-153).** The token is a script-readable `__Host-identity-csrf` cookie set beside the
session cookie and validated server side against the session.

*Source: AUTH-SESS-006, D-053*

**Acceptance criteria**
1. The frontend obtains the token on load.
2. Session rotation invalidates the previous token.

---

**BFF-CSRF-007** — Where the framework provides Fetch Metadata or antiforgery support,
it SHALL be used rather than reimplemented. The framework's antiforgery component SHALL
NOT be used for the synchronizer token (BFF-CSRF-001). The token SHALL be drawn by the
framework's `RandomNumberGenerator`, compared by `CryptographicOperations.FixedTimeEquals`,
and bound to the session record.

*Source: P-003, D-053, D-162, D-166*

The *standards over invention* test. Hand-rolled CSRF is a well-known source of
subtle failure.

**Acceptance criteria**
1. No hand-written token generation or comparison where framework support exists.
2. The synchronizer token is drawn and compared by the framework's primitives, and no
   framework antiforgery token is issued or read.

---

## 4a. Machine profile

**BFF-MACH-001** — A second pipeline profile SHALL exist for **non-browser traffic**,
carrying no session and no CSRF protection.

*Source: D-070, D-166, D-188, D-189*

A protocol client calling the token endpoint, the BFFs pushing their sign-on requests
and exchanging the codes (BFF-SESS-006), the social providers posting their security
events (`09` section 10), and any callback a host mounts on this profile for its own
processors, send no cookie, no synchronizer token, no custom header and no Fetch
Metadata. Under the browser profile every one of them is rejected: mail authentication,
sign-on and all callbacks fail on day one.

**The no-opt-out property is preserved.** An endpoint's protection derives from **where
it is mounted**, not from a flag or attribute. A developer cannot accidentally place a
browser endpoint on this profile; it is a deliberate act. A machine route that refuses a
request for carrying the session cookie answers as its protocol refuses a bad request: a
callback, once the rate limit admits it, 422 `integration.callback.rejected`, counted as
a rejection (`09` section 10); an OIDC route its protocol error `invalid_request`
(D-188, D-189).

**No source rate limit of its own.** The profile has no stage 4 (BFF-ORDER-001); each
machine endpoint carries its own limit. A callback is admitted
`integration.callback.ratelimit` requests per source per minute before any lookup
(BFF-MACH-003, INT-GEN-003); the provider's endpoints that act for a client
authenticate it with the secret the library generated for it (OPS-SEC-002); the
break-glass endpoint is held by its global limit and the source delay (OPS-BOOT-004). A
volumetric flood is the reverse proxy's to absorb, not the library's.

**Acceptance criteria**
1. A browser endpoint cannot be moved to this profile by configuration or attribute.
2. Machine endpoints reject browser-originated requests carrying a session cookie (a
   callback, once the rate limit admits it, 422 `integration.callback.rejected`, an OIDC
   route `invalid_request`, D-188, D-189), **except the break-glass endpoint**, which is
   reached from a browser and SHALL ignore any cookie present rather than refusing.
   Otherwise a stale cookie for the domain would produce an inexplicable refusal, during
   an emergency, for the system's one non-technical user. The provider return
   `/callbacks/providers/{provider}/return` likewise ignores any cookie, and only
   re-addresses the browser by `303` to its browser-profile continuation.
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

**The mount.** A host mounts each of its own callbacks with
`UseCallback(path, callback)` before `UseBrowserProfile`, at a path it chooses; the
callback is an `ISignedCallback` or an `IUnsignedCallback`, and nothing a host passes
turns a check off. The machine profile runs, in order: the rate limit per source, then
the refusal of a request carrying the session cookie (BFF-MACH-001, D-189), the
published ranges where declared, then for a signed callback the signature, the window
where the scheme carries an instant, verification under the current secret and, for 24
hours after a rotation, the previous one, and the event claim; the host's route runs
last. A signed callback a host mounts is a keyed-hash (HMAC) scheme under the hash the
provider publishes; a provider that signs with a public key is mounted as unsigned and
confirmed through its API (BFF-MACH-003). The social providers' security events, the
library's own, are verified under the provider's published keys (IDN-LIFE-012a).

**Values (D-153).** A callback signing secret rotates with a 24 hour overlap during which
both secrets verify. A delivery whose event was carried is answered 200 without reaching
the route. A claim is settled when the route answers 2xx and given back when it does not
or throws. A delivery meeting an unsettled claim younger than
`integration.callback.claimtimeout` is answered 409 `integration.callback.inprogress`, is
logged at Information, is not recorded as a rejection and is not counted toward
`alerting.callback.threshold`; one meeting an unsettled claim that old or older takes it
over and is carried. A verified delivery carrying no event identifier is refused. A
refusal is `integration.callback.rejected`: 429 with `Retry-After` and `details.retryAt`
where `integration.callback.ratelimit` refused the request, 422 for every other cause
(`09` section 10 states the social providers' own answers).

*Source: D-070, D-166, D-189*

Signature verification is the accepted baseline for webhook security and is not
optional where available. Parsing before verification breaks it — re-serialised JSON
does not match the signed bytes.

**Acceptance criteria**
1. An unsigned or mis-signed request is rejected before parsing.
2. A replayed request outside the timestamp window is rejected.
3. A duplicate event identifier is processed once.
4. Comparison is constant-time; a test asserts it.
5. A delivery whose route never finished is carried when the provider delivers it again
   once `integration.callback.claimtimeout` has passed, and a delivery meeting one still
   being carried is answered 409 `integration.callback.inprogress` and not acknowledged.

---

**BFF-MACH-003** — Where signing is **not available**, the callback SHALL be treated as
a **hint that triggers verification against the provider's API** — never as a fact.

**Values (D-153).** The endpoints accept `integration.callback.ratelimit` requests per
source per minute; `alerting.callback.threshold` rejected callbacks from one source in
an hour raise `callback-verification-failed`. The reference is issued through
`ICallbackReferences`: 128 random bits, base64url, held as its SHA-256 and looked up by
it; the host's route is reached only once the host's confirmation succeeds.
`ICallbackReferences` is a seam, not an operation of LIB-API-005: it joins the host's
transaction, so it has no endpoint and takes no access context.

*Source: D-070, INT-GEN-003, D-166, D-197, D-207, D-225*

The library's own callbacks are signed (`09` section 10); a host records for each of its
own processors whether it signs (D-225).

**This is the primary control for unsigned callbacks**, not a secondary one. A record is
not advanced because something said so; the provider is asked.

Supporting controls: unguessable correlation references, rate limiting, and source
restriction where the provider publishes ranges.

**Acceptance criteria**
1. No unsigned callback advances authoritative state without independent confirmation.
2. A forged callback with a guessed reference is rejected, recorded against its source's
   fingerprint and logged, the log line naming no source (INT-GEN-003, D-207).
3. Repeated verification failures raise an alert.

---

## 5. Capability projection

**BFF-CAP-001** — Resource responses SHALL carry the capabilities the caller holds
for that record, **each with the requirements that remain** (`requires`).

*Source: AUTHZ-GATE-005, API-CAP-001, D-078*

A capability means "permitted by grants, subject to session gates" — see API-CAP-001
for the wire format and the reasoning.

**Acceptance criteria**
1. A list of 50 records returns capabilities without a query per record.
2. A capability with an empty `requires` always succeeds.
3. A capability with `requires` carries the gates that remain, never an empty list.

---

**BFF-CAP-002** — The library SHALL provide the projection plumbing; the host SHALL
declare which capabilities are relevant per resource type.

**Values (D-202).** The host declares which of its permissions are capabilities of each
of its types: `ResourceTypeDeclarationBuilder<T>.Capabilities`, each a declared
permission; one not declared fails startup with `model.startup.declarationinvalid`,
`details.declaration` naming the type and `details.field` `capabilities`.
`IAccessGate.CapabilitiesAsync` on a host type answers only for the permissions declared
as its capabilities: asking it for any other, a declared permission the type does not
list among them included, is a fault of the calling code (CONV-ERR-001). It refuses a
host job's context `authz.denied`, in a request and outside one: the capability page is
a person's surface (`01` IDN-PRIN-001, D-207). `CapabilityFields` (`Janus.Hosting`) is a
sealed record made by `CapabilityFields.Of(Capability)`: `Can`, an
`IReadOnlyList<string>`, and `Requires`, an
`IReadOnlyDictionary<string, IReadOnlyList<string>>`, the `can` list and the `requires`
map API-CAP-001 writes. Each entry is a permission's action, the part of its identifier
after the `:` (CONV-NAME-002), so no permission name reaches the browser (BFF-SESS-001),
and each residual is its `10` section 5.20 name; `Can`, the keys of `Requires` and each
residual list are in ordinal order, and a permission with no residual is absent from
`Requires`. A type whose capabilities hold two permissions of one action fails startup
with `model.startup.declarationinvalid`, `details.declaration` the type and
`details.field` `capabilities`. A host's record carries the two as its members `can` and
`requires` (D-206). A type that declares none answers none.

An organization's capabilities are read with the organization as the resource (D-202,
D-211): `CapabilitiesAsync` given the type `organization`, which no host declares, and
organization identifiers as the resources answers, for each, which of the permissions
asked the organization-wide check admits for the caller: a grant whose resource is the
whole organization, through roles and groups, denies applied, as `RequireAsync` judges
it on the organization. Any declared permission may be asked, the library's included,
since the organization declares no capabilities of its own; the permissions are judged
in one statement however many are asked, no refusal is recorded and nothing counts
toward the denial spike (OPS-ALERT-001). `requires` holds what remains as for a record
(`stepup`, `reauthenticate`, `restricted`, `accountstate`), and never `consent`, since
the check names no data subject; consent is asked of each record. A host job's context
is refused as for any capability page. A host names each of these capabilities in its
own words for the browser: `CapabilityFields` writes a record's actions, and two
permissions of one action would read alike, while no permission name reaches the browser
(BFF-SESS-001).

*Source: D-052, AUTHZ-MODEL-003, D-202, D-206, D-207, D-211*

The library cannot know that a document has a "share" action. The host declares it;
the library computes and attaches it.

**Acceptance criteria**
1. Declaring a new capability requires no library change.
2. An undeclared capability never appears in a response.
3. Capabilities read for a host's records and written through `CapabilityFields` carry
   `can` and `requires` exactly as API-CAP-001 writes them.
4. A declaration naming as a capability a permission it does not declare, or two
   capabilities of one action, fails startup with `model.startup.declarationinvalid`.
5. `CapabilitiesAsync` given a host job's context is refused `authz.denied`, in a
   request and outside one (D-207).
6. `CapabilitiesAsync` on the type `organization` answers, for a caller holding a role
   granted on the whole organization, each permission the role allows with its
   `requires` and no other, a deny on the whole organization taking its permission out,
   in one statement for every permission asked, and records no refusal (D-211).


---

**BFF-CAP-003** — Capabilities SHALL be computed for a whole page at once: in the
resource's own query or in one statement beside it, never per record.

*Source: AUTHZ-GATE-005, D-202*

Per-row computation produces N+1 queries, which is why this is a requirement rather
than an implementation note. For a host's records the capabilities of a page are read
with one `IAccessGate.CapabilitiesAsync` call, one statement beside the page's query,
whatever the page's size (AUTHZ-GATE-005, D-202); the host places what `CapabilityFields`
answers on each record (BFF-CAP-002).

**Acceptance criteria**
1. Query count is independent of result size.

---

## 6. Error translation

**BFF-ERR-001** — Errors SHALL cross the boundary as machine-readable codes with
structured data and, where answered in a body, a correlation identifier. Rendered prose
SHALL NOT cross it, and the BFF SHALL NOT return HTML intended for a person to read —
including framework default error pages.

*Source: LIB-API-003, API-CONV-002, D-054, D-166, D-192, D-193, D-194, D-195, D-196,
D-208*

A navigation route (the social provider round trip, IDN-LIFE-012, and the sign-on round
trip, BFF-SESS-006) answers a refusal of the person's attempt by returning the browser
to where it started with the code in the query parameter `error`, placed before any
fragment; a return whose binding or `state` fails is answered
`403 auth.session.csrfinvalid` and sent nowhere. The codes each carries in `error` are
those `09` lists for it, which its routes declare (CONV-DESIGN-006), and nothing else of
a refusal's `details` crosses, save `retryAt` (BFF-ABUSE-001, D-192). A fault on a
navigation route is no refusal: it is answered as BFF-ERR-002 answers every fault, never
returned in `error` (D-193). The log records the route, the provider where there is one,
and the trace, never the provider's authorization `code`, the `state` or anything a
social provider wrote; the sign-on logs the `error`, or the library's own code, that its
refusal carried inside, its provider being the library's own (BFF-SESS-006, D-194). An
authorization response's `error` reaches the sign-on through the browser, so it is
logged only where it reads as RFC 6749 Appendix A.7 defines one and is at most 64
characters; otherwise it does not read: the return is a refusal, the browser returned
with `auth.session.expired`, `api.request.malformed` logged as the code it carried
inside, and the value in no entry the library writes (D-196).

**Acceptance criteria**
1. No response body contains a user-facing sentence.
2. Every error answered in a body carries the request's correlation identifier,
   resolving to that request's log entries, and a refusal the gate records carries its
   audit record's identifier in `details.correlation` as well (AUTHZ-CONCEAL-004,
   D-208).
3. Every code appears in `10-reference.md`.
4. A navigation's redirect carries `error`, `retryAt` beside a throttled or restriction
   refusal, and nothing else of the refusal's `details`.
5. A fault on a navigation route answers 500 `system.fault`, never `error`. At the
   sign-on every error the push reads, every error of the exchange but a 400
   `invalid_grant`, and an authorization response's `server_error` or
   `temporarily_unavailable` are faults, each adding, beside the fault's own entry, one
   entry at Error carrying the status the push or the exchange read and the `error` it
   read, and nothing else it carried, and so are the application's client in no
   registry, its secret that cannot be read, an identity token from the authentication
   application that does not hold up (its signature under that application's published
   keys, its issuer, its audience or its expiry) or carries no session identifier in
   `sid`, that application's published keys that cannot be read, and a derivation code
   whose row names a fault or that no row names; a 400 `invalid_grant`, an authorization
   response's other `error` but `login_required`, a return carrying neither `code` nor
   `error`, a token whose `sid` names a session that has ended or is not found, and any
   other derivation the library refuses return `error` `auth.session.expired`, the code
   carried inside (`api.request.malformed` for a return carrying neither, and for an
   `error` that does not read as RFC 6749 Appendix A.7 defines one or is longer than 64
   characters, whose value no entry the library writes carries; `auth.session.expired`
   for a session ended or not found) logged once at Information beside it. A fault of
   the sign-on is logged at Error and at no other level, and its log names what failed:
   a secret or keys that cannot be read make a fault carrying the code and details the
   read answered, an identity token that does not hold up a fault carrying the
   validation's own fault as its inner fault, and a client in no registry and a token
   carrying no session identifier in `sid` each add one entry at Error naming it.

---

**BFF-ERR-002** — The BFF SHALL NOT leak internal fault detail. Faults SHALL surface
as a generic code with the detail logged against the correlation identifier.

**Values (D-153).** The generic code is `system.fault`, status 500, the body per
API-CONV-002 carrying the correlation identifier and nothing else.

A fault SHALL be caught inside concealment and outside every other stage of each
profile. An answer already begun SHALL NOT be replaced, and a request whose caller has
gone SHALL NOT be answered.

*Source: CONV-ERR-001, P-003, D-166, D-183*

**Acceptance criteria**
1. No stack trace, connection string, or internal type name reaches a response.
2. The detail is retrievable from logs by correlation identifier: the fault's type and
   stack frames, and those of each inner fault, the `10` code and details a fault
   carries where it carries one, and never a message (CONV-LOG-003).

---

**BFF-ERR-003** — Concealment responses SHALL be produced by the pipeline, uniformly
per resource type, indistinguishable in body, headers **and timing** from a genuine
not-found.

*Source: AUTHZ-CONCEAL-001, AUTHZ-CONCEAL-002, D-166*

Timing is the part that leaks. A concealed denial arriving later because it ran a
permission check first has disclosed the answer regardless of its content.

**Values (D-166).** The answer is 404 `authz.resource.notfound` with
`details.correlation` and nothing the endpoint wrote. Stage 11 answers every request in
which the gate refused a record of a concealing type, whatever the endpoint went on to
write; the stages' own cookies stay. An answer the endpoint began before the refusal is
broken off, not finished. The refusal of a record the library holds no row for runs the
same statements as the refusal of one it holds, so the two differ in nothing but the
identifier.

**Acceptance criteria**
1. Bodies are byte-identical.
2. Timing distributions overlap within noise: verified by construction (one code path, fixed-time comparison, identical bytes), asserted by the byte identity, and named in the report as verified by construction (CONV-TEST-007, D-153).
3. Uniformity is enforced by the pipeline, not by endpoint discipline.

---

## 7. Step-up

**BFF-STEP-001** — Where a request requires step-up, the BFF SHALL reject it with the
step-up code rather than performing it.

*Source: AUTH-STEP-001, D-166, D-191, D-192, D-193, D-224, D-226*

The frontend initiates the step-up flow and retries. The BFF does not redirect
mid-request.

**The same shape applies to session expiry** (`auth.session.expired`, AUTH-SESS-005,
D-123, D-139): the BFF rejects with the code and `details.reauthenticate` —
`single-factor` for an idle expiry inside the absolute window where the principal's
policy's `requiredAssurance` is `aal2`, except in a break-glass session, whose account
holds no factor to restore it with (OPS-BOOT-002); `full` otherwise (always `full` for a
customer) — and never redirects.

**The restore in place (D-224, D-226).** On a `single-factor` expiry the browser profile
keeps the expired session's cookie and reads it on `POST /auth/restore/begin` and
`POST /auth/restore` alone (`09`, stage 5 of section 10), on every application, so the
frontend restores the session where it met the expiry and retries the interrupted
request (`18` FE-API-004). The two routes are carried by that cookie and by the CSRF
token bound to the browser's pre-authentication session, which first contact issues to a
browser holding no live session and whose token then takes the CSRF cookie
(BFF-CSRF-005a); a completed restore ends that pre-authentication session as a completed
sign-in does. Every other request on that cookie is answered as stage 8 answers it.

The `details` of the identity API's `auth.stepup.required` response — the three gate
values, the `outcome`, the `options` and `pendingUntil` (`09` `/auth/step-up`, D-141) —
pass through to the frontend unchanged; the BFF adds nothing and removes nothing. On a
navigation (BFF-ERR-001) the step-up code crosses in `error` with nothing of its
`details` (D-192).

**Acceptance criteria**
1. A rejection answered in a body names what is required — level, phishing-resistance
   and maximum age, null where the gate asks none — never a factor; the `options` list
   of presentable combinations is passed through as the identity API supplied it.
2. Retrying once a step-up 200 reports what meets the gate (`18` FE-API-004) succeeds
   without re-submitting business data the user would have to re-enter.
3. An expired-session rejection carries `details.reauthenticate`; retrying after
   reauthentication succeeds with the original data.
4. A session expired for inactivity under a policy requiring AAL2 is restored on the
   application where it expired, by `POST /auth/restore/begin` and `POST /auth/restore`
   carried by its cookie and the CSRF token of the browser's pre-authentication session;
   the cookie survives the answer that reports the expiry, and no other route accepts it
   until it is restored.

---

## 8. Throttling and abuse

**BFF-ABUSE-001** — Throttling responses SHALL carry the retry interval and SHALL be
identical whether or not the account exists. A send refused by a restriction
(AUTH-ABUSE-004) whose refusal the operation answers (a chapter or the operation's `09`
row gives that answer, CONV-DESIGN-002, AUTH-ABUSE-004) SHALL cross the boundary as
`auth.restriction.exceeded` with `retryAt`, identical whether or not the address is
registered; a refused send that neither a chapter nor the operation's `09` row answers
fails nothing and crosses nothing. A throttled answer, or a restriction's refusal, to a
navigation (a social provider's round trip) SHALL carry `retryAt` as a query member
beside `error` (BFF-ERR-001, D-192).

*Source: AUTH-ABUSE-002, AUTH-ABUSE-003, D-146, D-166, D-186, D-192*

**Acceptance criteria**
1. Responses and timing are indistinguishable across existence.
2. The remaining interval is communicated.
3. A refused send for a registered and for an unregistered address produces identical
   responses carrying `retryAt`.
4. A throttled provider return carries `error=auth.throttled` and `retryAt`, and a
   restriction's refusal on a navigation carries `error=auth.restriction.exceeded` and
   `retryAt`.

---

**BFF-ABUSE-002** — The BFF SHALL NOT expose enumeration through differential
responses, timing, or error granularity.

*Source: AUTH-ABUSE-003, API-CONV-005, D-183*

**Acceptance criteria**
1. Every endpoint API-CONV-005 names returns uniform responses: registration, sign-in
   initiation, recovery initiation, sign-in link and email code request, and adding,
   replacing and verifying an identifier.

---

## 9. Logging

**BFF-LOG-001** — Every request SHALL carry a correlation identifier through
logging, audit, and any error response.

*Source: CONV-LOG-002, AUTHZ-CONCEAL-004, D-166*

**Acceptance criteria**
1. A denial's correlation identifier resolves to that request's log entries.
2. Every refusal is logged at Information by its code, and a fault at Error with its
   details, each naming the identifier.

---

**BFF-LOG-002** — The BFF SHALL NOT log request or response bodies for endpoints
carrying the host's sensitive data.

**Values (D-153).** Such an endpoint is one the host marks with the `SensitiveBody`
endpoint metadata (LIB-HOST-001); the library has no knowledge of what the body holds.
Body logging is off for a marked endpoint whatever the setting. Every endpoint the
library maps carries the mark. A request whose endpoint is not known when the request
logging reads it is treated as marked. The library turns no body logging on.

**The framework's request entries (D-197).** The server's own entries for each request
("Request starting" and "Request finished", category
`Microsoft.AspNetCore.Hosting.Diagnostics`, at Information) write the request's query
string, which on a navigation's return carries an authorization `code` and `state`
(CONV-LOG-003, BFF-ERR-001). `AddJanus` sets that category's minimum level to Warning,
which keeps the request scope that carries `RequestId` (CONV-LOG-002), opened while the
category is enabled at any level; a host whose logging still enables the category at
Information, through any provider, fails startup with `model.startup.requestlogging`,
and so does a host whose logging disables the category in every provider, which would
drop the scope carrying `RequestId`.

*Source: CONV-LOG-003, PRIV-SENS-001, D-166, D-197*

A marked body carries data the host has declared sensitive. Body logging at the
boundary is the most likely place for it to leak, because it looks like ordinary
diagnostics.

**Acceptance criteria**
1. Body logging is off by default and cannot be enabled for a marked endpoint.
2. A test asserts no log entry contains a field of a marked endpoint's body.
3. A request logged before its endpoint is known has no body in any log entry.
4. With `AddJanus` and no other logging rule, no entry of category
   `Microsoft.AspNetCore.Hosting.Diagnostics` is written at Information and every
   request's entries still carry `RequestId`; a host rule enabling that category at
   Information, or disabling it in every provider, fails startup with
   `model.startup.requestlogging`.

---

## 10. Pipeline order

**BFF-ORDER-001** — The stages SHALL execute in this order, and the ordering SHALL be
part of the public contract. Stage 7 is not mounted middleware: it SHALL be applied by
each library operation that names an account, after that operation's gate and before it
reads or changes the account's state.

| # | Stage | Why here |
|---|---|---|
| 1 | Correlation identifier: the server's request identifier (`HttpContext.TraceIdentifier`), assigned before any middleware runs | Everything after must be traceable |
| — | *(machine profile diverges here — see §4a)* | No session, no CSRF |
| 2 | Fetch Metadata resource isolation | Cheapest rejection; no session lookup needed |
| 3 | Origin validation | Same — cheap, no state |
| 4 | Source-based rate limiting | Before any expensive work, including session lookup |
| 5 | Session resolution | Identity established. A cookie that no longer resolves is cleared, and the request continues as one that carried none (D-162), save the cookie of a session expired for inactivity that the restore admits (`details.reauthenticate` `single-factor`): it is kept and recorded for `POST /auth/restore/begin` and `POST /auth/restore`, which alone read it, the request otherwise continuing as one that carried none, and it is cleared once the restore no longer admits it; this needs no route, so it holds where the host's routing runs after the profile (D-226). An enrolment session is resolved only on the routes `POST /enrol/begin` lists; elsewhere the request continues as one that carried none, its cookie kept (`09`, D-189). Where the host's routing runs after the profile, no route is known yet, so an enrolment session is resolved on none (D-210) |
| 6 | CSRF synchronizer token validation | Requires the session; must precede any state change |
| 7 | Account-based throttling (AUTH-ABUSE-001, AUTH-RECOV-002) | Requires the account the request names, which only the operation reads |
| 8 | Assurance and step-up gating | Requires the session's assurance properties. An endpoint that requires a session (a library endpoint marked as needing one, or a host endpoint not marked as answering anyone, whatever routing selected it, BFF-HOST-002) refuses a request stage 5 left without one: 401 `auth.session.expired`, with `details.reauthenticate` where a sign-in session ended (BFF-STEP-001 AC3), the code alone where none was held or a registration or enrolment session ended (D-186, D-188); `POST /auth/restore/begin` and `POST /auth/restore` take a session expired for inactivity instead (BFF-STEP-001, D-224). Where the host's routing runs after the profile, the endpoint it selects is judged then, before it runs (BFF-HOST-002, D-209) |
| 9 | Host endpoint | |
| 10 | Capability projection | Requires the result set. A host endpoint places what `CapabilityFields` answers on its records itself (BFF-CAP-002); the stage adds nothing to its answer |
| 11 | Error translation and concealment | **Last**, so nothing earlier has disclosed existence |

**Values (D-153).** Stage 4 admits `abuse.source.ratelimit` requests per source
(AUTH-ABUSE-001) per minute and, for an IPv6 source, `abuse.source.sitelimit` requests
per enclosing /48 per minute, both sliding, and answers 429 `auth.throttled` with
`retryAt` beyond either; sized for many people behind one address. Each instance counts
in its own memory, so a deployment of several instances sets both keys to its share of
the limit it wants. A source or /48 already over its limit is refused without reading
configuration or any store, and without a log line for each refused request.

*Source: D-052, D-053, D-162, D-166, D-188, D-189, D-202, D-209, D-210*

**Routing before the profile (D-210).** Stage 8 holds whatever the order of the host's
routing (BFF-HOST-002). Stage 5 reads the endpoint before the request goes on, so where
routing runs after the profile it finds none and fails closed: an enrolment route
answers as for a request that carried no enrolment session. A host mounts its routing
before the profile, as the sample host does (`07` LIB-TEST-004), for an enrolment
session to reach the library's enrolment routes. The refusal of a request the framework
cannot bind is made on the way out, after routing has run, so it is answered as
API-CONV-002 gives, whatever the order.

**Throttling is two stages, not one.** Source-based limiting must precede session
lookup to protect against resource exhaustion; account-based limiting cannot, because
it needs an identity. An earlier draft collapsed them and placed both after CSRF,
which would have let an unauthenticated flood reach session resolution.

**Acceptance criteria**
1. A host cannot reorder or insert middleware between security stages.
2. Host middleware is added only before the call that mounts the browser profile or
   after it. Middleware added before it runs after stage 1 and before stage 2 and
   receives the answer the profile settled; middleware added after it runs only for a
   request that passed stages 2 to 8, beside the host endpoint, inside stages 10 and 11;
   where the host's routing comes after it, middleware between the two runs before the
   endpoint is known, the endpoint routing then selects is still judged before it runs,
   and middleware after that routing runs for a request whose endpoint was replaced with
   the refusal, no host endpoint running for it (BFF-HOST-002, D-209). With routing
   after the profile, an enrolment route answers as for a request that carried no
   enrolment session, and a request the framework cannot bind is refused 400
   `api.request.malformed` as it is with routing before the profile (API-CONV-002,
   D-210).
3. A flood of unauthenticated requests is rejected before session lookup.
4. A request carrying the cookie of an ended session reaches an endpoint that requires
   no session; an endpoint that requires one answers 401 `auth.session.expired`.
5. A request refused at stages 2 to 4 reaches no operation and is counted against no
   account.
6. Addresses within one IPv6 /64 are counted as one source, and an IPv4-mapped address
   as its IPv4 address.
7. Requests from different /64s of one /48 are refused past `abuse.source.sitelimit`.

---

## 11. Open items

None. Frontend obligations arising from this boundary are `18-frontend-integration`.
