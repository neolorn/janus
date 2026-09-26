# Authentication, Identity & Authorization — Decision Log

Record of every decision behind the specification (documents `00`–`19`). The
specification is complete on its own and is authoritative for what is built; this
log is the rationale — why each choice was made and what was rejected (`00` §9,
D-119).

Each entry records what was decided, why, and what was rejected. The rejected
options matter — they're what stops a settled question from being reopened later
without new information.

**Status:** specification rendered. Entries from D-102 onward record the resolutions
of the review rounds that followed rendering. Index at the bottom.

---

## D-001 — "Organization" is a domain concept, not a tenancy boundary

**Date:** 2026-08-25 · **Status:** accepted

**Decision.** The system has one identity pool, one deployment, one database.
Organizations are entities within it. The business is organization #1 — the
administrative organization — and staff are its members. Other organizations may
exist alongside it.

**Rationale.** An earlier reading treated "may become multi-tenant" as a request
for tenant isolation between separate business systems. That was wrong. The actual
requirement is organizational structure inside one system, which the authorization
model already expresses: an organization is a container, membership is a group,
grants scope to it. No new machinery.

**Rejected.**
- *Tenant isolation (separate deployments per business)* — solves a problem that
  doesn't exist here, and would prevent one person holding memberships across
  organizations.
- *Dormant tenant column throughout* — unnecessary once the requirement is
  understood as organizational rather than isolation.

**Implications.** Organization identity flows through authorization scoping and
session context. Nothing needs isolating.

---

## D-002 — Staff is a membership, not a user type

**Date:** 2026-08-25 · **Status:** accepted · **Follows from:** D-001

**Decision.** Remove the regular-user / staff-user distinction from the identity
model. Staff means membership in the administrative organization. Authentication
policy — including "passkeys only" — attaches to the organization, not to a user
type flag.

**Rationale.** Same behaviour today, but generalizes for free: a second
organization needing its own authentication rules becomes a configuration row
rather than a code branch. Also removes a user-type flag from the identity model,
which is one less thing to get wrong.

**Rejected.**
- *Keep the user-type flag* — forces a code branch for every policy difference and
  breaks as soon as a person belongs to more than one organization.

**Implications.** The original spec's "Minimum requirements: regular users / staff
users" section is restructured as per-organization authentication policy.

---

## D-003 — Multiple organization memberships: allowed structurally, disallowed by default

**Date:** 2026-08-25 · **Status:** accepted · **Follows from:** D-001

**Decision.** The data model supports one account holding zero or more organization
memberships. Default policy forbids more than one; enabling it is configuration.

**Rationale.** Cheap to build now, expensive to retrofit later — forbidding is a
validation rule, allowing is a model change to something that assumed one-to-one.
Memberships are stored either way, so the structure costs nothing extra.

**Rejected.**
- *One membership, enforced structurally* — no cheaper to build, and closes a door
  for no benefit.
- *Multiple memberships allowed by default* — permissive defaults on an unused
  capability invite accidental use.

**Implications.** Authorization always evaluates against the organization owning
the resource in question. No "active organization" concept in the session, and no
organization switcher, unless multi-membership is later enabled and a UI needs one.

---

## D-004 — Passkey RP ID is consumer configuration; the library owns the safety rails

**Date:** 2026-08-25 · **Status:** accepted

**Decision.** The consumer configures the relying party ID, origins, and related
origins. The library is responsible for making that configuration safe:

- Validate at startup that the RP ID is a registrable suffix of every configured
  origin; fail fast rather than at first enrollment
- Derive the common registrable parent domain when the RP ID isn't set explicitly,
  rather than defaulting to the first origin
- Record the RP ID against each stored credential, so a later configuration change
  is detectable and can prompt graceful re-enrollment instead of silent failure
- Serve the `/.well-known/webauthn` allowlist from the configured related origins

**Rationale.** The value depends on the consumer's domain layout, which the library
can't know. But exposing a setting isn't enough — a wrong value only surfaces after
users have enrolled. The parent domain is the widest default: a passkey for
`example.com` works across all its subdomains, and the reverse does not. It also
keeps a future mobile app usable with existing passkeys, since native apps bind to
the same RP ID via platform association files.

**Correction to an earlier claim.** This was initially assessed as irreversible —
"change it and every user re-enrolls." That is now too strong. WebAuthn Related
Origin Requests provide an escape hatch, with browser support complete as of
Firefox 152 in May 2026, subject to a five-registrable-domain limit. Still a door
worth not needing, but recoverable.

**Rejected.**
- *Hardcode the RP ID* — makes the library single-project, contradicting its purpose.
- *Expose the setting with no validation* — defers a footgun to the consumer, which
  is the failure mode this library exists to prevent.

---

## P-001 — Principle: "configurable" means the library owns the safety rails

**Date:** 2026-08-25 · **Status:** accepted · **Derived from:** D-004

Making a value configurable does not transfer the thinking to the consumer. Every
configurable setting carries: a safe default, validation that rejects unsafe
combinations at startup, and recoverability when the value changes after data
already depends on it.

Applies to the whole configuration surface, not just WebAuthn.

---

## D-005 — Minimal OIDC provider capability in v1

**Date:** 2026-08-25 · **Status:** accepted

**Decision.** The system acts as an OpenID Connect provider, scoped to first-party,
hand-registered clients. In scope: authorization code + PKCE, discovery document,
JWKS, userinfo, token introspection, a manually managed client registry.
Explicitly out of scope: consent screens, dynamic client registration, public
client self-service, developer portal.

**Rationale.** Not built for hypothetical third-party apps — built because Stalwart
is a real consumer already in the stack. The expensive parts of an OIDC provider
are the ones serving untrusted clients; none are needed for a client we configure
ourselves.

**Rejected.**
- *No provider capability* — would force the SQL-directory integration, which
  couples us to Stalwart's internal schema (see D-006).
- *Full OIDC provider with consent and dynamic registration* — weeks of work and
  meaningful attack surface for a consumer that does not exist.

**Implications.** First-party browser apps do NOT use OIDC tokens; see the session
model decision. The provider mints tokens for protocol clients only. Seams
reserved regardless: stable opaque subject identifier per account never reused
after deletion, claims modeled as data, session and credential kept separate, a
place in the model for consent.

---

## D-006 — Stalwart integrates through contracts, not internals

**Date:** 2026-08-25 · **Status:** accepted · **Follows from:** D-005

**Decision.** Four parts:

- **Authentication** — Stalwart is configured as an OIDC client against our provider
- **Provisioning** — account lifecycle events push to Stalwart via `stalwart-cli apply`
  or the JMAP management API; accounts are pre-created so inbound mail is never
  rejected for a user who has not yet signed in
- **App passwords** — owned and managed by Stalwart, not by our identity system
- **Storage** — Stalwart gets its own database; it is not co-located with business data

**Rationale.** Stalwart is pre-1.0 and v0.16 was a near-complete architectural
overhaul with breaking changes. A standards-based contract survives that; coupling
to their storage or schema does not. Stalwart's data store is a key-value namespace
of opaque byte-keyed rows even on PostgreSQL, so co-location would buy nothing —
it is not joinable or queryable. Mail is also write-heavy and would contend with
application workload, and read replicas are Enterprise-only.

**Rejected.**
- *SQL directory backend* — Stalwart reads our Postgres directly. Makes every
  Stalwart release a compatibility review for us.
- *Shared database instance* — no integration benefit, real contention and backup
  coupling costs.
- *App passwords owned by our system* — would mean building a deliberately weak
  credential type we then have to defend, when Stalwart already scopes them natively.

**Implications.**

- The staff authentication policy must be stated accurately: **passkeys for
  interactive access, scoped app passwords for mail protocols.** Mail clients
  including Outlook, Thunderbird and Apple Mail cannot do OAUTHBEARER with
  third-party providers, so a non-phishing-resistant path to mail exists by
  necessity. Since mailboxes receive password-reset links, this is the weakest
  credential in the system and must be named rather than glossed.
- Account lifecycle propagation is a real integration with real failure modes.
  Requires retries plus periodic reconciliation that compares both sides and flags
  drift — a failed suspension leaves someone reading mail after offboarding.
- If mail data is ever needed in our apps, read it through JMAP, never the database.

**Unverified — to confirm before build.** Which directory and security features are
Community vs Enterprise. Read replicas are confirmed Enterprise-only; the rest is
open.

---

## D-007 — Session and token model


> **Superseded in part.** Step-up is per recency window (15 minutes), not once per session — AUTH-STEP-002, D-123, D-135. Session lifetimes are D-123/D-130.
**Date:** 2026-08-25 · **Status:** accepted · **Follows from:** D-005

**Decision.** A server-side session record is the spine; every credential derives
from it, so revoking the session kills everything derived from it at once.

The record holds: subject, created, last seen, factors used, last strong-auth
timestamp, origin IP and device, idle expiry, absolute expiry. Redis for speed,
PostgreSQL as the durable copy.

**Browser apps** — opaque random session identifier in a cookie. `httpOnly`,
`Secure`, `SameSite`, `__Host-` prefix. No JWT in the browser, no token in
`localStorage`, ever. The BFF holds the session; the browser holds nothing of value.

**Cross-app** — each app holds its own session cookie; moving between apps
re-establishes silently through the auth app. Users experience single sign-on, but
a compromised session in one app does not grant access to another. Entry to the
management app requires step-up with a fresh strong factor.

**Protocol and native clients** — OIDC access tokens, short-lived, with refresh
tokens that rotate on every use. Reuse of a consumed refresh token revokes the
entire session family. Minted from the same session record, so revocation
propagates.

**Lifetimes** — sliding idle timeout under a hard absolute cap. Shorter defaults
for administrative-organization members. All runtime-configurable. Sensitive
operations require fresh strong authentication regardless of session age.

**Rationale.** A JWT in browser storage is readable by injected script and stays
valid until expiry no matter what the server does. An opaque cookie is unreadable
by script and revocable instantly. The BFFs already in the topology are exactly
the pattern that makes this work.

Per-app sessions cost slightly more machinery than one shared cookie, but that
machinery is already committed for D-005, and the isolation is worth it: the
management app is the highest-value target and shares a parent domain with a
public storefront.

**Rejected.**
- *JWT or any token in browser storage* — XSS-readable, not revocable.
- *One cookie shared across subdomains* — simpler, but a compromise in any single
  subdomain reaches every app including management.
- *Step-up per action rather than per session* — friction without proportionate
  gain once entry is gated.

**Implications.** Staff moving from a public app into management touch a strong
factor once per session. Accepted deliberately.

---

## D-008 — Staff account recovery

**Date:** 2026-08-25 · **Status:** accepted

**Decision.** Three parts.

**Credential redundancy is adaptive, not mandated.** At passkey registration the
system reads the WebAuthn backup eligibility and backup state flags. A synced
credential already carries its own redundancy, so one enrollment suffices. A
device-bound credential prompts the user to enroll a second. No universal
two-device requirement, and no hardware purchase.

**Admin-assisted re-enrollment** is the break-glass path: a time-boxed enrollment
link issued after out-of-band identity proof.

- **One approver**, configurable upward
- Out-of-band identity check is **specified concretely**, not left to approver judgment
- Written reason required
- Account owner notified on a channel separate from the recovery flow
- Rate limiting and anomaly detection per account **and per approver**

**Never** fall back to email or SMS recovery for administrative-organization members.

**Rationale.** An earlier round assumed passkeys implied buying security keys.
Platform passkeys use the biometric already on the device and cost nothing, and
synced passkeys propagate across a user's devices — so the common case needs no
second enrollment at all. Reading the backup flags targets the requirement at
exactly the users who need it.

Email fallback would make a staff account's real security level equal to its
mailbox, silently undoing the passkeys policy.

**Rejected.**
- *Mandatory second passkey for everyone* — unnecessary friction for the majority
  whose credentials are already synced.
- *Admin-assisted recovery as the routine path* — when recovery is common, friction
  gets worked around and approvers rubber-stamp.
- *Two approvers* — proposed and declined. Noted here because single-approver
  recovery is a known account-takeover vector; the compensating controls above
  exist because of it. Configurable, so it can be raised without a deploy.

---

## D-009 — Email is never a second factor; MFA recovery is delayed, not instant

**Date:** 2026-08-25 · **Status:** accepted

**Decision.** Email-based authenticators (OTP, magic link) are **single-factor,
always**. They may serve as a primary sign-in method — magic links stay for the
storefront — but cannot count as a second factor, satisfy step-up, or raise an
account's assurance level. The enrollment UI does not offer them as MFA options at
all, rather than offering them and discounting them silently.

**Recovery cannot downgrade assurance.** Email recovery restores access to the
password. It does not remove an enrolled second factor.

**Locked-out public users** get both paths:
- Recovery codes offered and strongly encouraged at MFA enrollment, not hard-blocking
- Delayed self-service MFA removal as the safety net: requested via email, completes
  after a waiting period with repeated notification to every channel on the account.
  Default 7 days, configurable.

**Administrative-organization members** get no self-service downgrade, ever. Their
path is the admin-assisted flow in D-008.

**Rationale.** Password plus email OTP, where email is also the recovery channel,
is single-factor wearing an MFA badge — one mailbox compromise yields both the
reset link and the "second" factor. Same reasoning that already restricts SMS to
phone verification.

Hard-blocking MFA enrollment behind a recovery-code ritual measurably reduces MFA
adoption, which is worse for aggregate security than a slower, noisier fallback.
The waiting period is the actual control: long enough that a real owner notices
the notifications, short enough that a locked-out customer does not give up.

**Rejected.**
- *Email OTP as a valid second factor* — circular trust with the recovery channel.
- *Mandatory recovery codes at enrollment* — drop-off pushes users away from MFA
  entirely. Retained as strong encouragement.
- *Instant self-service MFA removal via email* — collapses every strong factor down
  to mailbox access.

---

## D-010 — Configuration taxonomy and the system-admin permission

**Date:** 2026-08-25 · **Status:** accepted · **Applies:** P-001

**Decision.** Configuration is runtime-changeable by default. Three mechanisms
constrain it:

- **Direction matters.** Tightening a security control at runtime is free.
  Loosening one requires step-up authentication, a written reason, and an audit
  entry.
- **Hard floors in code.** Password minimum length can be raised, not lowered past
  the standard's floor. Session absolute lifetime can be shortened, not extended
  past a cap. The rail is fixed; the value inside it is configurable.
- **Config changes are audited exactly like permission grants** — who, what, from,
  to, when, why, same retention.

**Redeploy-only list** (everything not listed is runtime):

- Disabling audit logging
- Disabling rate limiting entirely
- Turning off step-up enforcement for the management app
- The passkey relying party ID (changing it invalidates enrolled credentials, D-004)
- Token signing algorithm and signature verification

**Selection test for that list:** not "how sensitive is this setting" but "does
turning this off blind us to the person turning it off."

**System administration is a permission, not a rank.** These settings are
changeable only by holders of an explicit system-admin permission — never implied
by organizational seniority, never inherited by owners or upper management.
Granting or revoking system-admin **requires system-admin**, self-referentially;
without that rule any admin who can manage grants is a system-admin one step
removed, and the distinction is decorative.

**Break-glass** covers the single-holder lockout risk. A sealed credential is
generated at bootstrap, displayed once, stored only as a hash, and kept physically
offline. Single use, consumed on use. Grants a time-boxed system-admin session,
never permanent. Use triggers maximum-noise alerting on every available channel,
immediately.

**Rationale.** An admin console that can disable MFA system-wide is itself a
privilege escalation path — compromise an admin session and you needn't break
authentication, only switch it off. The redeploy list exists so that the controls
which would detect a compromise cannot be silenced by that compromise.

**Rejected.**
- *All settings equally runtime-changeable* — makes the admin console a bypass for
  the entire security model.
- *A second named system-admin* — offered and declined; break-glass chosen instead,
  keeping the permission genuinely singular while removing the single point of
  failure.

**Accepted exception to the no-recurring-human-task principle.** The sealed
credential must be regenerated and resealed periodically (annually), because
verifying it works consumes it. This is the one place in the system where a
recurring manual task is justified rather than a design failure. Noted explicitly
so it is not silently dropped.

---

## C-001 — Correction: "on-premises" was scoped to identity ownership

**Date:** 2026-08-25 · **Status:** correction · **Affects:** spec framing, D-006, item 3

**What went wrong.** The statement "everything stays on-premises" was made once, in
the context of removing account merging and clarifying that Google and Apple
sign-in are authentication methods only. It was written into the spec as a standing
general principle covering all infrastructure.

**Correct scope.** It is a statement about **identity ownership**: no third-party
identity provider is ever the source of truth for an account, and social sign-in is
a credential rather than the identity record. It is **not** a general prohibition on
external services, which may be used elsewhere on their merits.

**Downstream effects.** Reasoning from the broader reading had already influenced
the Stalwart integration framing and produced a password-blocklist recommendation
that was argued against its own merits. D-006 stands on independent grounds
(coupling to a pre-1.0 internal schema), so it is unaffected. The blocklist
recommendation is revised.

**Standing rule.** A principle promoted from one statement to a general constraint
must be confirmed before it is used to justify a second decision.

---

## D-011 — Password policy

**Date:** 2026-08-25 · **Status:** accepted · **Supersedes:** original spec's
"strong requirements when MFA disabled / relaxed when enabled"

**Decision.** Conform to NIST SP 800-63B-4 (finalized 31 July 2025):

- **15 characters minimum** when the password is the only factor
- **10 characters minimum** when combined with MFA — raised from the standard's
  allowance of 8
- Support up to **64 characters**
- **Mandatory blocklist screening** against known-compromised passwords
- **No composition rules.** No character-class requirements, no forbidden patterns
- **No forced rotation.** Rotation only on evidence of compromise
- **No password hints, no security questions.** Knowledge-based authentication is
  prohibited outright

**Blocklist source:** Have I Been Pwned range API. The first five characters of the
password hash are sent; candidate suffixes are matched locally. The service never
receives the password or the full hash. A curated offline list ships with the
library as fallback when the API is unreachable — the library **fails loudly**
rather than silently skipping screening. A self-hosted corpus remains available as
configuration.

**User experience:**
- Strength meter is **advisory, never blocking**. Rejection happens only for
  blocklist hits or floor violations
- UI copy leads with **passphrases** — four unrelated words clears 15 characters and
  is memorable

**Rationale.** Composition rules and forced rotation produce predictable user
workarounds, which is why the standard removed them. With composition rules gone,
blocklist screening is the load-bearing control.

Scope is narrower than it first appears: administrative-organization members use
passkeys for interactive access, so this policy is effectively storefront policy.

10 rather than 12 because at that length the blocklist is doing the real work — a
12-character password on the breach list is worse than a 9-character one that is
not. Pushing floors higher drives pattern reuse, the exact failure the standard was
rewritten to avoid.

**Rejected.**
- *Self-hosted breach corpus as default* — a refresh pipeline that rots silently
  leaves screening that no longer screens. Available as configuration.
- *Curated list alone* — misses breach-specific credentials.
- *Raising the MFA floor to 12* — defensible, marginal benefit over 10.
- *Blocking on strength-meter score* — heuristic rejection generates workarounds.

---

## D-012 — Factor list corrected; factors modeled by property, not by name

**Date:** 2026-08-25 · **Status:** accepted

**Corrections to the original spec list.**

- *Cross-device sign-in* appeared twice — "scan QR from a signed-in device" and
  "cross-device (hybrid)" are the same FIDO2 hybrid transport. One entry.
- *Passkey sign-in and autofill* appeared in two sections. Autofill is conditional
  UI, a presentation mode, not a separate method.
- *"Security key (passkey or MFA)"* conflated form factor with credential type. What
  determines behaviour is whether the credential is discoverable and whether user
  verification is required. Hardware keys are one way to hold a WebAuthn credential,
  not a separate factor.

**Corrected list — all in scope for v1.**

- **Primary:** password, passkey (discoverable credential, conditional UI where
  supported), cross-device sign-in (hybrid), email magic link, email OTP, Google,
  Apple
- **Second factor:** TOTP, non-discoverable WebAuthn credential, recovery codes
- **Verification only:** SMS OTP, at registration

**Design consequence.** Factors are registered with properties, not handled by name:
can-be-primary, can-be-second-factor, phishing-resistant, assurance level reached,
verification-only. Every rule — step-up, recovery, policy, admin visibility — reads
properties. No rule branches on a factor's identity.

**Rationale.** Nine factors at launch makes per-factor branching untenable; the
property model is what keeps the tenth factor a registration entry rather than a
change to every rule in the system. Directly serves the built-to-grow requirement.

**Rejected.**
- *Narrower v1 (defer email OTP, cross-device, social)* — proposed on scoping
  grounds and declined. All factors have real use cases; recorded so the cost is
  visible if scope is revisited.

---

## D-013 — Abuse controls

**Date:** 2026-08-25 · **Status:** accepted

**Throttling, not lockout.** Progressive delay per account and per source,
escalating with failures, decaying over time. Independent limits on source
address, account, and identifier. Applied equally to sign-in, registration, and
recovery.

The user is **told** they are throttled and for how long — silent failure produces
retry storms. The message and the delay are **identical whether or not the account
exists**, so the throttle does not become an enumeration oracle.

Classic N-strikes lockout is rejected: it is a denial-of-service weapon usable by
anyone who knows a user's email, at no cost to the attacker.

**Enumeration resistance — uniform responses everywhere.** No endpoint confirms
account existence, including via response timing. When recovery or registration is
attempted for an address that does **not** exist, that address is emailed to say
so. The real owner gets their answer; an enumerating attacker learns nothing
because they do not control the mailbox.

**SMS abuse is toll fraud, not just bot defense.** The gateway account is prepaid
with a finite balance, so looped sends drain money directly without any
registration ever completing.

- **One successful delivery per number per window.** Resend permitted only when the
  delivery report indicates failure — a hard one-and-done would strand users whose
  carrier dropped the message.
- **Message length is a cost cliff.** Unicode messages are 70 characters for a
  single SMS and 67 per part concatenated; UTF-8 gets 160 and 153. An Arabic OTP
  one character over 70 costs double. OTP templates need a per-language length
  budget, tested rather than assumed.
- **Balance monitoring is a control, not reporting.** Poll the balance endpoint,
  alert on unusual drain rate, hard-stop sends below a floor. Automated — fits the
  no-manual-maintenance requirement.
- **The delivery-report callback is hostile input.** The gateway calls our endpoint
  over plain HTTP GET with `messageId` and `status` as query parameters,
  unauthenticated. Correlation IDs must be unguessable, the endpoint rate-limited,
  and a callback alone must never advance a verification state.
- Gateway credentials are Basic auth over a static `username:password:accountid`
  string. Stored as a rotatable secret, never in config files.

**Bot defense at registration** is signal-driven, not universal. Phone verification
already imposes attacker cost; an additional challenge appears only on adverse
signals (datacenter ranges, implausible timing, repeat attempts) rather than
showing every customer a puzzle.

---

## C-002 — Correction: impersonation was never in scope

**Date:** 2026-08-25 · **Status:** correction · **Affects:** spec, open items queue

**What went wrong.** Impersonation appeared in the original authorization spec as a
must-have feature before the user had mentioned it. The user's only statement on
the subject listed it as one example among several of things that *might* be built
later — "later we may develop a mobile app, or become multi-tenant, or allow
third-party apps, or implement impersonation, or whatever else we might do."

The subsequent review correctly identified this as an inconsistency, then resolved
it unilaterally ("designed in now, shipped behind a flag") rather than raising it.

**Second occurrence of the same pattern.** C-001 recorded a principle promoted from
one statement into a general constraint. This is a possible future item promoted
into a committed feature — from the same sentence that already produced the
multi-tenancy misreading.

**Corrected.** Impersonation is removed from the spec and returned to the open
queue as an undecided question owned by the user.

**Standing rule, extended.** Items the user names as possible futures are not
requirements. Where a future direction affects a design decision, ask which way
they want it — do not resolve it and record the resolution. An identified
inconsistency between what was written and what the user said is always a question
for the user, never something to settle unilaterally.

---

## P-002 — Principle: possible futures get seams, not features

**Date:** 2026-08-25 · **Status:** accepted

"Architected and designed to grow" means: build **nothing** for a future that is not
in scope, but leave nothing in the way of building it later. For each named
possible future, identify the **minimal structural accommodation** — usually a
shape in the data model or an abstraction boundary — and stop there. No feature
work, no configuration, no UI, no flag.

The test for whether an accommodation is minimal: it should be something a
reasonable engineer would have built anyway for clarity, that merely happens to
also unblock the future.

Applied to the futures named so far:

| Future | Minimal accommodation | Where |
|---|---|---|
| Mobile app | Authorization code + PKCE, stable opaque subject identifier | D-005, D-007 |
| Organizations beyond the first | Organization as a container, membership as a group | D-001, D-003 |
| Third-party apps | Claims as data, session and credential separate, a place for consent | D-005 |
| Impersonation | Acting identity and effective identity, both in audit | D-014 |
| Additional auth factors | Factors registered by property, never by name | D-012 |

**Corollary.** When a future direction would change a current decision, ask which
way — do not decide it and record the resolution. See C-002.

---

## D-014 — Impersonation: out of scope, seam only

**Date:** 2026-08-25 · **Status:** accepted · **Supersedes:** C-002 · **Applies:** P-002

**Decision.** Impersonation is **not in scope**. No session type, no flag, no
initiation flow, no blocked-action list, no notification, no UI.

**Seam retained:** the access context carries an **acting identity** and an
**effective identity** as separate values, identical in every current code path.
Audit records both. That is the whole accommodation.

**Rationale.** Two identity fields where one would do is defensible on its own
terms — it makes "who did this" unambiguous in the audit trail even with no
impersonation feature. Retrofitting a second identity into every audit record and
every permission check afterwards would not be.

**Rejected.**
- *Full impersonation behind a flag* — proposed twice without being asked for.
  Recorded in C-002.
- *No accommodation at all* — the seam costs one field and is awkward to add later.

---

## D-015 — Resource-type registry: fluent model builder

**Date:** 2026-08-25 · **Status:** accepted

**Decision.** The host declares its resource types and containment relationships at
startup through a fluent, generic builder in EF Core's `modelBuilder` idiom. From
that declaration the library maintains ancestry records and produces permission
filters. The library never learns what any host entity *is*.

The built model is **serialized to a file at startup** and committed, so the
permission model diffs in review without booting the application.

**Startup validation, failing loudly at boot rather than on first query:**
containment cycles, types referencing undeclared types, types with no path to an
organization, roles granting undeclared permissions.

**Resource types are strings the host chooses**, never an enum the library ships —
an enum would make every new resource type a library change.

**The host applies the filter to its own queries.** The library returns a filter;
the host places it in its own database query. This is what keeps permission
filtering inside the query and list screens fast.

**Scope:** redeploy, not runtime. The declaration lives with the domain model and
changes when it changes — adding a resource type means adding a table and code, so
a deploy is happening regardless. Roles and grants remain runtime-configurable per
D-010. This is the only redeploy-scoped item not on D-010's security list.

**Rationale.** Property references are compiler-checked. In an external model file
`d.FolderId` is a string, and a rename silently points the model at nothing — a
permission model that fails silently is the worst failure mode in this system.
The serialized artifact recovers the one real advantage of an external file.

**Rejected.**
- *External model file (DSL/YAML), as OpenFGA and Casbin use* — duplicates
  knowledge the C# already holds, and the two drift. Diffability recovered via the
  serialized artifact instead.
- *Attribute-based discovery via reflection* — idiomatic but fails at runtime
  rather than compile time, and cannot express conditional containment.
- *Interface-based (`ISecuredResource`)* — compile-time safe but awkward: an entity
  with two possible parent types needs generic gymnastics.
- *No registry; host owns hierarchy* — thinnest library, but every host then
  reimplements ancestry, which is the hard part.

**Noted risk, unrelated to this choice.** The genuine hazard in this area is
**ancestry maintenance** — writing closure records correctly and transactionally on
every create, move, and bulk operation, including moves of large subtrees. A bug
there means someone sees a record they should not. No registry syntax makes this
easier or harder; it needs dedicated test coverage.

**Reversibility.** This sits behind the `Filter<T>()` seam. If the builder proves
wrong later, what changes is declaration syntax — not the grant tables, not the
ancestry model, not a single call site.

---

## D-016 — Access-denied semantics: per resource type, defaulting to 404

**Date:** 2026-08-25 · **Status:** accepted · **Extends:** D-015

**Decision.** Concealment behaviour is declared **per resource type** in the model
builder, **defaulting to 404**. Opting a type into 403 is a deliberate act.

**Rules that make the 404 real rather than a 403 in disguise:**

- The response must be **indistinguishable from a genuine not-found** — same body,
  same headers, same timing. A concealment 404 arriving slower because it ran a
  permission check first leaks the answer anyway.
- **Uniform within a resource type.** Per-type is fine; per-instance is an oracle.
- The response carries a **correlation identifier** that appears in the audit log,
  so support can diagnose a legitimate permission problem without the response
  itself revealing anything.

**403 remains correct** for authorization failures not tied to a specific record —
calling an admin endpoint without the permission, or a request forbidden regardless
of target. Nothing is being concealed, so 403 is both accurate and more useful.

**Standards position.** RFC 9110 explicitly permits this. Its definition of 403
notes that a server wishing to hide the existence of a forbidden resource may
respond with 404 instead, and its definition of 404 covers a server unwilling to
disclose that a resource exists. The spec distinguishes semantics from disclosure
and leaves the disclosure choice to the implementer, because it depends on what the
resource is.

**Rationale.** The default matters more than the option — whatever a developer gets
without thinking is what most types will have, so the safe answer must be the lazy
one. Per-type granularity allows strictness where it counts (records revealing who
buys what) without paying for it on an internal catalogue or staff directory, where
404 would be friction with no security benefit.

**Rejected.**
- *Always 404* — support burden on every permission misconfiguration, with no
  benefit for non-sensitive types.
- *Always 403* — existence enumerable across the entire system by probing
  identifiers.

---

## P-003 — Architectural qualities

**Date:** 2026-08-25 · **Status:** accepted · **Supersedes:** "aim for perfection"
as a literal spec line

The user's "aim for perfection" was a directive to interpret, not text to record.
Unfolded into the qualities below. Each carries a **test**, so it can be reviewed
rather than merely asserted. Every decision in this log should be checkable against
them.

| Quality | Test — is it violated? |
|---|---|
| **Secure by default** | Can a consumer produce an insecure deployment by not configuring something? |
| **Configurable by default** | Is there a constant in the code a consumer might want different? |
| **Runtime where possible** | Is this redeploy-scoped for a real reason, or because it was easier? |
| **Fail closed and loud** | When a dependency is unavailable, does anything become permitted, or does a check quietly skip? |
| **Separation of concerns** | Can authorization be used without authentication? |
| **Decoupled by default** | Does a vendor name appear in a core namespace? |
| **No host-domain knowledge** | Does the library contain a type name belonging to any business? |
| **Extensible without modification** | Does adding a factor, resource type, role, or provider require editing library source? |
| **One rule, one place** | Can a single check and a list filter ever disagree? |
| **Observable by default** | Can you determine why access was denied without attaching a debugger? |
| **Zero maintenance** | Does anything require a recurring calendar entry? |
| **Standards over invention** | Is there a hand-rolled version of something with an RFC? |
| **Reversible behind seams** | What would this cost to undo in a year? |

**Resolved tensions.** Configurability against security — resolved in D-010
(direction matters, hard floors, redeploy list). Zero maintenance against
break-glass — one accepted exception, recorded in D-010.

**Why not the literal wording.** "Aim for perfection" gives an engineer nothing to
act on, and has two predictable failure modes: gold-plating, because nothing says
when to stop, and paralysis, because no decision feels finished. In security it is
unattainable by construction — there is no perfect, only no-known-weakness plus
fast response when that changes. The qualities above preserve the strictness and
drop the part that cannot be acted on.

---

## D-017 — Data access: EF Core with Dapper, single boundary

**Date:** 2026-08-25 · **Status:** accepted · **Affects:** D-015

**Decision.** Both EF Core and Dapper from the start, with one unambiguous rule:

| Tool | Used for |
|---|---|
| **EF Core LINQ** | Everything normal — reads, writes, relationships, change tracking, migrations, the model driving the D-015 registry |
| **Dapper** | Every hand-written SQL query — reports, recursive CTEs, historical reconstruction, bulk reads |

The rule is *am I writing SQL by hand?* Yes → Dapper. No → LINQ. No third case.
EF Core's own raw-SQL facility is deliberately **not** used as a middle tier — two
mechanisms for the same job produce case-by-case judgment calls.

**Required safeguard.** A single accessor hands out a connection with EF Core's
transaction already attached. Calling `GetDbConnection()` directly is banned by
convention. Without this, a Dapper query on a separate connection silently misses
rows written earlier in the same transaction — a nasty and easily-missed bug in
financial code.

Hand-written SQL lives in one place per aggregate, not scattered inline, so it is
reviewable as a body of work.

**ADO.NET** is already present — Npgsql is an ADO.NET provider and both tools sit on
it. Direct use is reserved for database features with no C# abstraction: binary
`COPY` for bulk loading, advisory locks, `LISTEN`/`NOTIFY`, server-side cursors.
Not for query optimisation — that stops at Dapper. No pattern needs establishing up
front; these are isolated usages.

**Effect on the authorization library.** The library must render each permission
rule **two ways from one definition**: an `Expression<Func<T, bool>>` for LINQ
composition, and a parameterised SQL fragment for hand-written queries. The test
suite runs every truth-table case through both and asserts agreement — drift
between what a list screen shows and what a report includes is exactly the silent
leak P-003's *one rule, one place* exists to prevent.

This also settles the package split: core separate from the EF Core provider, since
there are now two rendering targets and neither should be a bolt-on.

The ancestry closure table becomes part of the library's **public contract** rather
than an internal detail, since hand-written SQL will query it. It cannot be
restructured without a breaking version.

**Rejected.**
- *EF Core only* — recommended initially on "add Dapper when a wall is hit," which
  was avoidance rather than reasoning. The safe integration pattern is far easier to
  establish on day one than to retrofit.
- *Three tiers (LINQ / EF raw SQL / Dapper)* — two boundaries and an ambiguous
  middle.

**Noted for later, out of scope.** Heavy historical reporting eventually points at
separating reads from writes — a read model shaped for those questions. Outside
this library.

---

## C-003 — Correction: manufacturing decisions instead of asking one question

**Date:** 2026-08-25 · **Status:** correction

**What went wrong.** The data-access question was put to the user as a binary — EF
Core or Dapper — when the two are not alternatives. The actual dependency was
narrower: whether permission filtering could be expressed as a LINQ expression. Once
the user described how they wanted to work, the constraint dissolved. The question
that should have been asked first was simply "how does your data access work today?"

**Third occurrence of the pattern.** C-001 promoted one statement into a general
principle. C-002 promoted a possible future into a committed feature. This one
converted a single clarifying question into a menu of options.

**Common failure.** Deriving a constraint from my own design, turning it into a
decision, and presenting it for choice. Options resemble thoroughness but
manufacturing choices the user does not need to make costs their attention and works
against the one-at-a-time process they asked for.

**Standing rule.** Before presenting options, ask whether one factual question about
the user's existing situation would make the choice unnecessary. Ask that first.

---

## D-018 — Migrations run in CI, never at application startup

**Date:** 2026-08-25 · **Status:** accepted · **Follows from:** D-017

**Decision.** A dedicated CI step applies migrations before the new version rolls
out, using an EF Core **migration bundle** — a standalone executable built for this
purpose. Pipeline builds it, runs it against the target database, then deploys.

**Startup at application boot does not migrate.** It **verifies** the schema matches
the model and refuses to start if it does not — fail closed and loud, per P-003.

**Two database credentials.** The migration job holds DDL rights; the running
application can read and write rows only. Nothing in production can alter schema.
Same reasoning as D-010's redeploy list: controls that would catch a compromise
must not be reachable by it.

**Two migration sets, ordered.** The library owns its schema with its own history
table, so library and host migrations never collide. **Library first, host second**
— host tables may reference library tables, never the reverse.

**Expand and contract, always.** Both versions run briefly during rollout, so every
migration must work against the *previous* application version. Add a column
nullable, deploy code that writes it, backfill, tighten the constraint in a later
release. Never rename or drop in the same release that changes the code.

**Roll forward, never back.** EF down-migrations are unreliable once data exists.
Recovery is a fix-forward migration; point-in-time recovery is the real safety net.

**CI applies migrations twice** against a throwaway PostgreSQL instance: once from
empty, once from the previous release's schema. The second catches migrations that
work on a fresh database and break on a real one.

**Rejected.**
- *Migrate at application startup* — instances race each other on deploy, failures
  surface as crash loops rather than failed pipelines, and the application needs
  permission to alter the tables that authorize it.
- *Script-based tooling (DbUp, Grate, Flyway)* — better for gnarly data
  transformations, worse elsewhere: model and schema become two sources of truth
  that drift. The EF model already drives the D-015 registry, so generating schema
  from it keeps one source. Complex steps drop to raw SQL inside a migration.

---

## D-019 — Destructive DDL gate: built, disabled, toggled by repository setting

**Date:** 2026-08-25 · **Status:** accepted · **Follows from:** D-018

**Decision.** Additive migrations — new tables, nullable columns, indexes — flow to
production with no gate. **Destructive DDL** (dropping a column or table, narrowing
a type, adding a constraint that could fail against existing rows) is gated by a
**GitHub environment protection rule**: a repository setting, not pipeline code. The
pipeline is written once with the gate wired in; whether it pauses is a toggle.

**Currently off** — the user is the only person deploying, so the accident this
guards against (someone else's branch merging early) does not yet apply.

**Detection always runs, regardless of the gate.** Every deploy reports which
destructive operations it contains, whether or not it pauses.

**When a team exists**, the toggle must live in repository admin settings, not in a
workflow file a pull request could edit. Same self-protection logic as D-010's
system-admin permission.

**Rationale.** Every other migration failure is fixable in minutes with another
migration; a dropped column is data that no longer exists, recoverable only by
point-in-time restore. And the usual failure is not a badly-written migration but a
*correct* one reaching the wrong environment.

Expand-and-contract (D-018) already means a drop is never urgent — by the time a
column is dropped, code stopped using it releases ago. So the gate costs a pause
every few months, on exactly the operations where being wrong is expensive.

**Rejected.**
- *Permanent approval gate now* — guards against a multi-person accident that does
  not currently exist.
- *Automatic with pre-migration backup instead of a gate* — restores the data but
  not the production traffic written on top of it. Converts an unrecoverable
  failure into an expensive one rather than a cheap one.

---

## D-020 — Consistency corrections from the review pass

**Date:** 2026-08-25 · **Status:** accepted · **Amends:** D-007, D-008, D-010, D-012

Three inconsistencies found by auditing the log against itself. All are corrections
to the existing model, not new scope.

### 20.1 — Step-up anchors to the organization, not the app

**Amends D-007, D-010.** D-007 required step-up on entry to "the management app" and
D-010 listed disabling that as redeploy-only, but D-002 established that
authentication policy attaches to **organizations**. Two anchors for one rule.

**Corrected rule:** step-up is required when a session first exercises a permission
belonging to an organization whose policy demands it. The administrative
organization sets that policy; any future organization sets its own.

An app is a deployment artifact — management could be split in two or folded
elsewhere, and the security property must not move when it does. What warrants
step-up is exercising administrative capability.

**This is stricter than D-007 as written.** Under the app framing, a staff member who
never opened the management app never stepped up. Under the organization framing
they step up on first use of an administrative permission, including via API or an
action initiated elsewhere. The app boundary was never the real boundary.

D-010's redeploy list entry is restated accordingly: *turning off step-up
enforcement for an organization*.

### 20.2 — Sessions record properties; only audit records factor names

**Amends D-007, applies D-012.** D-007 had the session carry "factors used," which
would lead step-up rules to match on factor names — the coupling D-012 exists to
prevent.

**Corrected:** the session records **properties reached** — assurance level attained,
whether phishing-resistant authentication occurred, and a timestamp for each. A
step-up rule reads "requires phishing-resistant, verified within N minutes" and
never names a factor.

Factor identity is still recorded in the **audit trail**, where naming what happened
is the point. Only the authorization path must not see names.

Without this, adding a new phishing-resistant factor would require editing every
rule mentioning passkeys — the tenth-factor problem, re-entering through a side
door.

### 20.3 — Adaptive passkey rule applies to everyone, enforced by organization policy

**Amends D-008.** The backup-flag logic was decided in a staff-recovery context,
leaving public customers unspecified.

**Corrected:** it applies to all users. **Advisory** for public users — a prompt they
may dismiss. **Enforced** for administrative-organization members.

The underlying fact is identical: a device-bound passkey on a lost device is gone
for anyone. The consequences differ — a locked-out customer has D-009's delayed
downgrade; a locked-out staff member has only admin-assisted recovery.

Expressed as organization policy per D-002, not as a user-type rule.

---

## D-021 — Localization deferred to its own design; four seams retained

**Date:** 2026-08-25 · **Status:** accepted · **Applies:** P-002

**Decision.** Localization is **out of scope** for this spec and gets its own
library, architecture, and design discussion. It wires into nearly every piece of
data, so designing it inside an authentication discussion would shape it around
this library's needs rather than around the problem.

**Four seams retained**, because each is decided by default if left alone:

- **Database collation** — set at database creation. Changing it later means
  rebuilding indexes and may alter how unique constraints behave. Needs a value
  before the first migration runs.
- **Unicode normalization of identifiers** — a security property, not a display
  one. Multiple code-point sequences render identically in both Arabic and Latin,
  so without a normalization rule applied at write time two accounts can hold
  visually identical addresses or names. Matters especially where a human approves
  a recovery request by recognising a name (D-008). Retrofitting means reprocessing
  existing data and possibly discovering collisions.
- **Error contract across the library boundary** — machine-readable codes plus
  structured data, never rendered prose. Correct API design regardless of
  localization, and what makes a future localization library's job possible.
  **Amended by D-054: the carve-out for library-served browser pages is removed —
  the library never renders user-facing text at all.**
- **WebAuthn relying party display name** — stored with each credential at
  enrollment, so a later change does not update existing credentials. A value to
  pick, not a design. Same family as D-004.

**Also noted for that future discussion**, not decided here: per-language SMS length
budgets and their cost implications (D-013), Arabic-Indic versus Western digits in
OTP codes, RTL handling in email templates, alphanumeric SMS sender ID constraints,
and per-recipient language resolution for notifications triggered by background
jobs.

**Audit records stay language-neutral** — codes and structured fields, never
rendered sentences. An audit trail whose text varies by locale is not queryable and
does not mean the same thing to two readers.

---

## D-022 — Notifications

**Date:** 2026-08-25 · **Status:** accepted · **Supports:** D-008, D-009, D-013

**Decision.** The library **emits events, not messages**. It raises "recovery was
requested for this account"; the consumer's handler decides delivery. A default
email and SMS handler ships so a new project works out of the box, but it is
replaceable — the *decoupled by default* test in P-003. Templates resolve through
the D-021 localization seam.

**Delivery is persisted, retried, and observable.** The notification is written to a
table in the same transaction as the event that caused it, then delivered by a
background worker with retry and backoff. Status is recorded, so a failing channel
is visible rather than silent. Several of these notifications **are** security
controls, and a control that fails quietly is not one.

**Security notifications are not suppressible.** Marketing and product mail can be
muted; "someone requested recovery on your account" cannot. The distinction must
exist in the model from the start or it collapses into one preference and the
security notifications become opt-out by accident.

**Content is minimal by design** — "a recovery request was made, act now if this was
not you," never details of what was requested. These often reach a mailbox that may
already be compromised, which is frequently the scenario that triggered them.

**Dual channel where a phone is verified.** D-009's waiting-period warnings assume
someone may control the mailbox, so email alone defeats their purpose. Not a
contradiction of "email is never a second factor" — notifying is not authenticating.

**Feedback into D-009.** If none of the waiting-period warnings actually delivered,
the MFA removal does not complete on schedule. Delaying is better than removing a
factor the owner was never told about.

**Consequences accepted, not decisions.**
- A customer cannot turn off security notifications. Support must answer no.
- Dual-channel means SMS cost against the prepaid balance (D-013). Low volume, not
  zero.

---

## D-023 — Egyptian data protection obligations

**Date:** 2026-08-25 · **Status:** accepted · **Amends:** C-001 reasoning

**Research findings.** Egypt's PDPL (Law 151/2020) became operational when its
Executive Regulations were issued on 1 November 2025 (Decree 816/2025). **Compliance
deadline: 31 October 2026.**

**Obligations that apply:**

| Item | Detail |
|---|---|
| Controller license | Required. Fee-exempt below 100,000 records — still must be filed |
| Cross-border permit | Required if hosting outside Egypt. 50% of controller fee, so also zero at current scale |
| DPO | Mandatory, no small-business exemption. Registered with the PDPC, exam-based tiers |
| Breach notification | 72 hours to the PDPC, 3 days to data subjects |
| Records of processing | Purposes, categories, lawful bases, retention, recipients, **hosting locations**, transfer mechanisms |
| Children's data | Under 15 requires explicit written guardian consent; 15–18 either child or guardian |

**DPO decision: hire externally.** Not because the owner-as-DPO question was
resolved — because it does not need resolving. If an owner may serve, external is
still compliant; if not, external is the only option. Same answer either way.

The conflict is real regardless: the DPO must be independent and neutral, and
cannot be the person who determines the purposes and means of processing. The user
is the owner and sole decision-maker. Egypt explicitly contemplates DPOs holding
multiple appointments, so an external DPO is a contractor cost, not a salary — and
it removes an exam from the critical path before the deadline.

**Correction to earlier reasoning.** EU hosting does **not** make GDPR apply to the
user. A processor in the EU is not an establishment of a non-EU controller, and
that alone is insufficient for GDPR to bind the controller directly. GDPR binds
IONOS; the contract carries the processor obligations. The SRB pseudonymisation
ruling remains persuasive analysis, not governing law.

**D-011 stands.** The HIBP range API is settled — whatever permit covers the hosting
covers it, and a five-character hash prefix is not a separate concern alongside the
entire database being hosted abroad.

**Hosting location remains open** as a business decision: hosting in Egypt removes
the cross-border transfer entirely. At current scale the permit is free, so this is
a preference rather than a cost question.

**Not legal advice.** Findings from published summaries, to be confirmed with counsel.

---

## D-024 — Consent and lawful basis

**Date:** 2026-08-25 · **Status:** accepted · **Follows from:** D-023

**Decision.** Every processing purpose declares its **lawful basis**. Consent is one
basis among several and usually not the right one — order processing runs on
contract performance, financial records on legal obligation, fraud prevention and
security on legitimate interest. None of those are withdrawable. Consent is used
only where it genuinely is the basis: marketing, optional features.

**The cross-border transfer does not run on consent.** Consent is available as a
narrow exception under Article 15, but a withdrawal would leave data that cannot
lawfully be hosted. The transfer stands on the PDPC permit instead. This is the
difference between a compliance model that survives a single withdrawal and one
that does not.

**Where consent is used it is a record, never a boolean:**

- The specific purpose — never bundled across purposes
- The **version** of the notice text shown
- Timestamp and mechanism
- Withdrawal timestamp, if withdrawn

Version pinning is load-bearing: when a purpose materially changes, prior consents
do not cover it, and without the version there is no way to identify who must be
re-asked.

**Withdrawal is as easy as granting** — same number of steps, no retention flow, no
dark patterns.

**Consequence.** Consents and lawful bases are structured data, so the records of
processing required by the PDPC are generated rather than hand-maintained. See
D-025.

---

## D-025 — Compliance evidence is generated, not maintained

**Date:** 2026-08-25 · **Status:** accepted · **Extends:** D-015, D-024

**Decision.** Processing purposes and their lawful bases are declared in the **same
fluent model builder** as resource types and containment (D-015). The records of
processing required by the PDPC are then **derived** from configuration rather than
hand-written.

A purpose that is not declared cannot be processed; a declared purpose appears in
the report automatically. The document cannot drift from reality because it is not
a document — it is a query.

**Not generable:** data protection impact assessments and legitimate interest
assessments are written analysis. The system stores references to them and flags
any purpose lacking one.

**Breach response requirement.** 72 hours to the PDPC and 3 days to data subjects
means answering "who was affected" quickly. Audit records must therefore be
**queryable by data subject**, not only chronologically. An indexing decision made
now or a painful one made during an incident.

**Closes the loop on the model builder.** One declaration now carries: resource
types and containment (D-015), concealment behaviour (D-016), and processing
purposes with lawful bases (D-024). Permission filtering, error semantics, and
compliance records all derive from a single description of the host's domain.

---

## D-026 — Audit retention, timestamps, secrets, versioning, policy changes

**Date:** 2026-08-25 · **Status:** accepted

Five small items from the review pass, decided together.

### 26.1 — Audit retention and immutability

Audit records are **append-only enforced at the database level** — no update path,
no delete path, not merely by convention.

Retention is **configurable per category with a floor**: security events, permission
changes and financial actions retained long; routine access logging short.

Erasure requests **pseudonymise rather than delete** — the opaque subject identifier
(the D-005 seam) remains, the personal data behind it goes. This satisfies the right
to erasure without destroying the record that a permission was granted on a given
date.

### 26.2 — Timestamps

Everything stored **UTC as `timestamptz`**, always. Conversion happens at display
time, never at storage time.

Every audit and financial record carries **the instant the event happened**, not the
instant it was written. These diverge under retry and queueing, and reconstructing a
timeline from write times is how audit trails end up lying.

### 26.3 — Secrets

**No secrets in configuration files or environment variables in production.** The
key-encryption key comes from a secrets manager; everything else is encrypted at
rest under it. Signing keys, SMS gateway credentials, and OIDC client secrets share
one lifecycle with automated rotation and overlap windows (D-007).

### 26.4 — Library versioning

**Semantic versioning**, with the public contract stated explicitly: the model
builder API, the permission filter shape, emitted events, the database schema, and
the ancestry closure table (made public by D-017). Breaking changes require a major
version and a migration note. Consumers may skip minor versions, never major ones.

### 26.5 — Policy changes against live sessions

When an organization's authentication policy tightens, existing sessions are
**re-evaluated on next use, not terminated**. A session no longer meeting policy is
**downgraded**: reads continue, but any action requiring step-up forces
re-authentication under the new rules.

Terminating sessions outright would log everyone out mid-work for a change that is
not an emergency, which trains users to expect random logouts. A separate explicit
**"revoke all sessions now"** action exists for when it genuinely is one.

---

## D-027 — Minors: 18+ only, enforced on the order flow

> **Superseded in part.** The host business rules and vendor facts this entry records (orders, payments, shipments, couriers, addresses) moved to the host under D-165; the reasoning stands as the host's inheritance.

**Date:** 2026-08-25 · **Status:** accepted · **Follows from:** D-023, D-024

**Decision.** Accounts and orders require a self-declared **18+ affirmation**. No
date of birth is collected. Anyone under 18 cannot register or order; in practice a
parent or caregiver orders on their behalf.

**Enforced on the order flow, not only the signup form.** Guest checkout collects a
name, phone and address — that is processing, and the children's-data rules apply
to it regardless of whether an account exists. The affirmation therefore sits on
both paths.

**Rationale.** Under Egyptian rules, processing data of under-15s requires explicit
**written** guardian consent, and 15–17 requires consent from the young person or a
guardian, with the consent mechanism itself approved by the regulator. Building
digital guardian verification is disproportionate work for the edge case it serves.

The customer base is overwhelmingly adults, clinics, pharmacies, and caregivers. A
teenager ordering directly is rare, and those who exist have an adult who can order
for them.

Not collecting date of birth is itself a benefit — an 18+ checkbox holds less
personal data than a birth date would, consistent with collecting only what the
function requires.

**Rejected.**
- *15+ self-consent with under-15 blocked* — requires reliably distinguishing a
  14-year-old from a 15-year-old, meaning real dates of birth collected from
  everyone.
- *Full guardian consent flow* — disproportionate; guardian identity cannot be
  reliably established online.

**Cost accepted.** A genuine 16-year-old customer is turned away rather than served.

---

## D-028 — Bootstrap via CLI

**Date:** 2026-08-25 · **Status:** accepted · **Follows from:** D-010

**Decision.** A fresh deployment is initialised by a **command-line bootstrap** run
on the server against the database. It creates the first organization, creates the
first system-admin account, sends that account a passkey enrollment link, and
generates the D-010 break-glass credential — printing it once, for sealing offline.

The command **refuses to run again** while a system-admin exists.

**Rationale.** Whoever runs the deployment already has server access, which is
strictly more privileged than anything the first admin account can do. Using that
as the credential means no new secret is created, nothing is transmitted, and there
is no race window.

**Rejected.**
- *Default admin account with a known password, or credentials in an environment
  variable* — leaves a credential nobody remembers to remove. A recurring cause of
  real breaches.
- *First-run web claim page* — races. Whoever reaches it first claims the system;
  if the app is publicly reachable before the operator gets there, that is a
  stranger.
- *Single-use bootstrap token printed to the deployment log* — workable, but invents
  a secret to solve a problem the CLI approach does not have. Becomes worth the
  machinery only if deployment is ever handed to someone who should not hold admin
  rights.

**Consequence accepted.** Standing up a new project requires shell access and cannot
be done entirely through a web UI.

---

---

## D-029 — Company/developer relationship; DPO obligation sits with the company

**Date:** 2026-08-25 · **Status:** accepted · **Resolves:** R-01 · **Amends:** D-023

**Facts established.** The business is a **registered company** (juridical person),
owned by a close friend of the developer. The developer is an independent
freelancer who is the company's **only technical personnel**, handles ~95% of its
digital infrastructure, and will administer the delivered system in full — the
company has no technical department. No written contract exists between them.

**DPO.** Because the company is a juridical person, a DPO must be appointed
irrespective of processing scale. **D-023 stands.** This is the company's action,
not the developer's. An internal appointment is possible — the Compliance Plan
Checklist permits an existing employee to serve while retaining other duties,
subject to independence conditions and PDPC approval during registration — but a
sole owner cannot satisfy independence, since the DPO guideline requires a contract
stating the DPO cannot be instructed or penalised.

**Sole-administrator decisions confirmed, not separated.** An earlier reading
treated the developer as an arms-length contractor and proposed separating "the
company" from "the developer" across D-008, D-010, D-019 and D-028. That was
wrong: he is the company's entire technical function. **All four stand as
decided** — sole system-admin, destructive-DDL gate off, single approver, CLI
bootstrap.

**One amendment to D-010.** The break-glass credential is **sealed and held by the
company owner**, not by the developer. The developer is the single point of
failure; a credential only he can reach is no recovery path for the company. Same
credential, same generation, different custodian.

**Processor status noted.** The developer processes personal data on the company's
behalf and is therefore a processor under the PDPL. The RoPA template has a Data
Protection Agreement field and the Compliance Checklist asks for one, so the
relationship should be documented for the company's licence application. Raised
once; the user's call.

---

## D-030 — Sensitive data: health-implying purchase history

> **Superseded in part.** The host business rules and vendor facts this entry records (orders, payments, shipments, couriers, addresses) moved to the host under D-165; the reasoning stands as the host's inheritance.

**Date:** 2026-08-25 · **Status:** accepted · **Resolves:** R-02 · **Extends:** D-015, D-024, D-025

**Facts established.** The company is the exclusive Egyptian importer and
distributor for KDL — spinal needles, insulin pins and needles. It sells **both B2B
and B2C**: the storefront serves individual customers, business customers are
handled internally through the management app. Payments go through **OPay** (written
contract, authorised in Egypt); shipping through **Bosta**.

**The finding.** An individual purchasing insulin needles discloses a diabetes
diagnosis through the purchase itself. Under the Key Terms guideline, sensitive
personal data covers *health; biometric information; financial details; religious
beliefs; political views; criminal records; children's data.* The storefront's
order history is therefore **health data held in our own database**.

**Scope, stated accurately.** The processor integrations are ordinary and already
conformant — this is not remediation. The finding concerns **our own storage and
consent**, not OPay or Bosta.

- **Financial data drops out.** Card details go to OPay and never touch our
  servers; we hold a payment reference and an amount.
- **Health data stays.** Inherent to the product. Unavoidable.

**Design.** Sensitivity becomes a **declared property of a resource type** in the
same model builder as containment (D-015), concealment (D-016), and lawful basis
(D-024). A type marked sensitive derives:

- Mandatory **written consent** captured before processing. "Written" includes
  electronic — the Consent guideline permits *"paper form or through electronic
  means"* — so this is buildable in a web flow. It must be distinct, prominent and
  unbundled, not a terms-and-conditions tick, with notice version, timestamp and
  mechanism retained
- Stricter retention defaults
- **Encryption at rest as a hard requirement**, not a default
- Its own entry in the RoPA sensitive column

**Customer privacy dashboard — required, and new scope.** The Consent guideline
requires *"accessible and user-friendly preference management tools (e.g. privacy
dashboards), which enable data subjects to access, manage, and update their consent
settings at any time."* This also houses several of the rights missing per R-05.

**Consent refreshing.** Required whenever the processing activity or purposes
substantially change. D-024's version pinning already models this; it must actually
trigger a re-ask.

**Age verification.** The guideline requires *"proportionate verification processes"*
for sensitive data. D-027's self-declared 18+ is thinner against this bar than
against ordinary data — see R-23.

### Data minimisation to processors

Standard practice, written down so it is not undone later by someone adding a
"helpful" field.

**Bosta** (`Create delivery`, fields verified against their API collection):

| Field | Rule |
|---|---|
| `specs.packageDetails.description` | Fixed generic string. Never product names or any category narrowing to diabetes. Configurable, but never derived from the cart |
| `notes` | Delivery instructions only. Never order contents |
| `businessReference` | Opaque internal reference — meaningless outside our system |
| `receiver.email` | **Omitted.** Phone suffices for a courier |
| `webhookUrl` | Hostile input, as with the SMS delivery report in D-013 — unguessable references, rate limited, never advances state alone |
| Products API | **Not used for storefront orders.** Bosta offers a product catalogue with inventory and search; linking it to deliveries would place product identity beside a named person's home address in a third party's system |

**Residual, accepted:** Bosta knows the sender is a medical supplies company. That
implies "bought something medical," not "is diabetic" — a much weaker inference,
unavoidable if we ship at all, and proportionate. Cash-on-delivery amounts also
cross by necessity.

**Processor relationships now in scope:** OPay, Bosta, the SMS gateway (D-013), and
IONOS. Each needs a data protection agreement and a RoPA entry. Worth confirming
whether OPay's processing stays in Egypt; if not, it joins the cross-border permit
scope.


---

## D-031 — Language: English primary, Arabic first-class, Arabic authoritative on compliance text

**Date:** 2026-08-25 · **Status:** accepted · **Resolves:** R-03 · **Amends:** D-021

**Three separate axes, previously conflated:**

| Axis | Rule |
|---|---|
| **Presentation** | **English is primary everywhere**, including compliance screens |
| **Quality** | **Arabic is first-class everywhere** — written natively, RTL handled properly in layout, never a lagging translation |
| **Legal authority** | **Arabic is authoritative on compliance text.** If the two diverge on a consent request or privacy notice, the Arabic governs |

**Why compliance text cannot wait for the localization library.** D-021 deferred
localization wholesale. But the Privacy Notice guideline requires *"In Arabic: The
information must be provided in Arabic as the primary language. Data users remain
free to add other languages"*, and the Consent guideline requires *"Consent
requests must be provided in Arabic as the primary language."* Deferring these
would mean launching non-compliant.

**Scope split — D-021 stands otherwise.** Compliance text is not application text.
Consent requests, the privacy notice, withdrawal flows and the privacy dashboard
are a small, bounded, legally-specified set — the Privacy Notice guideline
enumerates the eleven elements it must contain. **In scope now, both languages.**
Everything else — errors, product pages, emails, general UI — remains deferred to
the localization library.

**Build rules:**

- On compliance screens, Arabic must be **present and reachable from the same
  screen**, not behind a language switch a user might never find. Presentation
  leads with English; Arabic sits alongside
- Compliance text is **versioned as a unit containing both languages**. Neither
  publishes without the other; a change to either bumps the version and triggers
  re-consent where D-024 requires it
- **SMS length budgets tested per language**, with Arabic binding — the Unicode
  70-character cliff from D-013 is on the critical path, not a future concern

**Interpretation flagged.** Reading the regulator's "primary language" as legal
authority rather than display order is defensible but is an interpretation. Worth a
sentence to whoever reviews the compliance text. It does not change what is built
either way.


---

## D-032 — Lawful bases: Egypt's six, as a closed enumeration

**Date:** 2026-08-25 · **Status:** accepted · **Resolves:** R-04 · **Amends:** D-024

**TL;DR.** The law requires recording *why* we are allowed to hold each piece of
data. Egypt allows six reasons. D-024 never listed them, so an engineer would have
used Europe's list — similar but not the same. Fixed by shipping Egypt's six as a
fixed list in the library.

**The enumeration**, per the PDPC Lawful Basis guideline:

| # | Basis | Typical use here |
|---|---|---|
| 1 | Data Subject's Consent | Marketing; health-implying purchase data (D-030) |
| 2 | Fulfilment of a Contractual Obligation | Order processing, delivery, payment |
| 3 | Fulfilment of a Legal Obligation | Tax and financial record retention |
| 4 | Legitimate Interest | Fraud prevention, security, abuse controls (D-013) |
| 5 | Claim or Defence of a Legal Right | Retaining evidence for a dispute |
| 6 | Execution of Court Judgments or Orders from Competent Investigative Authorities | Responding to a court order |

GDPR's vital-interests and public-task bases **do not exist** in Egyptian law;
bases 5 and 6 are Egypt-specific additions.

**Changes to D-024:**

- Shipped as a **closed enumeration**, not an open string, so a purpose cannot be
  declared against an invented basis and D-025's RoPA emits values the regulator
  recognises
- **Legitimate interest requires a linked assessment.** The Data Users guideline
  requires an LIA where basis 4 is relied on and the RoPA template links to it. The
  model builder requires an assessment reference when basis 4 is declared, the same
  way D-025 flags a purpose lacking a DPIA. This also closes a previously unstated
  gap: the abuse controls in D-013 run on legitimate interest
- **Consent carries a sub-distinction** — ordinary consent versus the written
  consent D-030 requires for sensitive data — since it determines whether the
  written-capture path runs

**Note.** Bases 5 and 6 are cited when circumstances arise, not implemented. They
belong in the enumeration for RoPA completeness; no code branches on them.


---

## D-033 — Session cluster: lifetimes, CSRF, logout, rotation


> **Superseded in part.** The 30-day AAL1 absolute lifetime is replaced by 90-day inactivity / 365-day absolute for customers — D-123, D-130.
**Date:** 2026-08-25 · **Status:** accepted · **Resolves:** R-06, R-07, R-08, R-09 · **Amends:** D-007

**TL;DR.** Four standard things every login system needs that D-007 left out: how
long a session lasts, what logging out actually does, defence against a common web
attack, and changing the session ID at the right moments.

### 33.1 — Lifetimes derived from assurance level

D-007 said "sliding idle timeout under a hard cap" without values. Defaults now
come from NIST SP 800-63B-4 rather than invention:

| Session assurance | Inactivity timeout | Absolute timeout |
|---|---|---|
| AAL2 (MFA present) | 1 hour | 24 hours |
| AAL1 (single factor) | optional | 30 days |
| Administrative organization | shorter, by organization policy | shorter, by organization policy |

Configurable with enforced floors per D-010. **Derived from the assurance level the
session reached** (D-020.2), so values follow the factors used rather than being
hardcoded per user type.

Note: Rev 4 relaxed these from Rev 3's 12h/30min at AAL2. A design copying older
guidance would be stricter than the current standard without benefit.

### 33.2 — CSRF defence

Cookie-based sessions require explicit CSRF protection; `SameSite` alone is not
sufficient, and the OAuth browser-based-apps draft devotes a section to it for
exactly this pattern.

A token the attacking origin cannot read is required on every state-changing
request, **enforced in the BFF layer** so no endpoint can omit it. Fail closed.

### 33.3 — Logout is global

D-007 gave each app its own session with silent SSO between them, and never defined
logout. As written, signing out of one app then navigating to another silently
signs the user back in — logout appears broken, and genuinely is on a shared device.

**Logout terminates every app session and the auth session behind them.** One
action, everywhere. This is both the expected behaviour and the safe default.

**Gap closed: the auth session was never described.** There are three session types,
not two:

| Type | Holder | Purpose |
|---|---|---|
| Per-app session | Each app's BFF, opaque cookie | The user's session with that app |
| **Auth session** | The auth app | What makes silent SSO possible between apps |
| OIDC tokens | Protocol clients (Stalwart) | D-005 |

All three derive from the same session record and die together on revocation.

### 33.4 — Session rotation

A new session identifier is issued on **authentication**, on **step-up**, and on
**any privilege change**. Without this, an identifier planted in a victim's browser
before sign-in remains valid afterwards — session fixation.


---

## D-034 — Second-factor mechanics

**Date:** 2026-08-25 · **Status:** accepted · **Resolves:** R-10 · **Extends:** D-012

**TL;DR.** D-012 modelled factors by property rather than name — correct — but never
specified how any individual factor works. These are the settings with known-wrong
defaults, where a mistake weakens security without breaking the application.

### TOTP

- **30-second time step, 6 digits** — universal compatibility with authenticator apps
- **Drift tolerance of one step either side.** Wider is a genuine weakening
- **Replay prevention within a step** — a consumed code cannot be reused inside its
  own window. Without this, an observer has up to 30 seconds to reuse it
- **Secrets encrypted at rest** under the KEK from D-026.3
- **Confirmation before activation** — the user must submit one valid code before
  TOTP counts as enrolled, preventing lockout from a mis-scanned QR code

### Recovery codes

- **10 codes, single use**, displayed once at generation
- **Stored hashed**, as passwords are — verifiable, never recoverable
- **Regeneration replaces the entire set**, invalidating all previous codes
- **Remaining count surfaced** in the account so depletion is visible

### WebAuthn

- **Signature counter verified where provided.** A counter moving backwards
  indicates a cloned credential. Synced passkeys do not supply one, so the check
  applies only where present
- **No attestation required** — demanding it excludes legitimate authenticators for
  no benefit at this risk level
- **Algorithm allow-list**, modern signature algorithms only, no legacy fallback
- **User verification required** — biometric or PIN must occur, not mere presence
- **Backup eligibility and backup state recorded at registration**, driving D-008's
  adaptive second-credential prompt

### Verification codes are not authentication codes

An SMS code proving control of a phone number and a TOTP code proving identity are
different concepts with different lifetimes and rules. They are modelled
**separately**. Collapsing them is a common source of bugs in which a verification
code becomes usable as a login credential.


---

## D-035 — Email and phone change flows

**Date:** 2026-08-25 · **Status:** accepted · **Resolves:** R-12

**TL;DR.** Changing the email address is the most dangerous operation an account
supports — done wrong, someone with brief access to a live session takes the
account permanently, because password resets then go to them.

**The common mistake:** verifying only the *new* address. An attacker with a live
session enters their own address, confirms from their own inbox, and the real owner
cannot reset their way back in.

### Email change

- **Both addresses verified.** The old address confirms the change was intended; the
  new address confirms it exists and is controlled. The change completes only when
  both have confirmed
- **Step-up required to initiate** — a live session is insufficient; a strong factor
  must be re-presented (D-020.1)
- **The old address is notified regardless**, non-suppressibly (D-022), so the owner
  learns immediately even if the flow somehow completes
- **A cooling-off window** during which the change can be reversed from the old
  address. Same reasoning as D-009's delayed MFA removal — slow and loud beats fast
  and silent
- **Sessions rotate on completion** (D-033.4) and all other sessions terminate, so a
  change made by an attacker costs them their access

### Phone change

Same shape, lighter weight. Phone is not a recovery channel — D-013 restricts SMS to
verification only — so it cannot be used to seize an account. Verify the new number,
notify both the old number and the email address, require step-up to initiate.

**One addition:** because phone is mandatory at registration as an anti-abuse
measure, phone change inherits D-013's one-successful-delivery-per-window limit.
Otherwise it becomes a route to burning SMS credit without registering.

### The lockout trap

**A pending change must never block sign-in**, and an unverified new address must
**never** become the login identifier before confirmation. Otherwise a half-completed
change locks out the legitimate owner — a self-inflicted denial of service and a
common bug.


---

## D-036 — RoPA fields, rewritten against the regulator's template

> **Superseded in part.** The host business rules and vendor facts this entry records (orders, payments, shipments, couriers, addresses) moved to the host under D-165; the reasoning stands as the host's inheritance.

**Date:** 2026-08-25 · **Status:** accepted · **Resolves:** R-13 · **Supersedes:** D-025's field sketch

**TL;DR.** The PDPC publishes an official template for the record of processing
activities. D-025 was written from a sketch; this rewrites it against the actual
template so the system's output can be handed over rather than transcribed.

**Fields the sketch lacked**, from the official English template with worked
examples:

| Field | Source |
|---|---|
| Data Hosting Environment | configuration |
| **Data Hosting Location** (inside/outside Egypt) | configuration |
| Basis of Cross Border Transfer | configuration (D-023) |
| **Three data-category columns** — non-sensitive, sensitive, children's | derived from D-030's sensitivity property |
| Personal Data Disposal Measures | derived from D-026.1 |
| **Data Owner** (named accountable person) | **declared — not derivable** |
| Organisational Roles with Access | derived from D-015 grants |
| Implemented Technical Security Measures | configuration |
| **Implemented Organisational Security Measures** | **declared — not derivable** |
| Legal characterisation of recipients | declared per recipient |
| Data Protection Agreement links | declared per recipient |
| **Links to LIA, DPIA, TIA** | **declared — not derivable** (D-032) |

**Three fields are human input**, not derivable: Data Owner, organisational security
measures, and assessment document links. Stored as declared values, with the system
flagging absence — the pattern D-025 already established for DPIAs.

**Output shape matches the regulator's template**, so generated records are directly
submittable.

**Validation of D-026.1.** The template's own worked example lists *"Anonymisation +
cryptographic erasure"* as a disposal measure — the approach D-026.1 chose
independently.

**Recipient characterisation.** The template requires recipients classified legally.
All four current processors — OPay, Bosta, the SMS gateway (D-013), and IONOS — are
processors requiring an agreement link, restating D-030's paperwork item in the form
the regulator expects.


---

## D-037 — Data subject rights

**Date:** 2026-08-25 · **Status:** accepted · **Resolves:** R-05 · **Extends:** D-024, D-030

**TL;DR.** Egyptian law grants nine rights over personal data. The design covered
three. Most of the remaining six live in the privacy dashboard D-030 already put in
scope, so the additional work is smaller than the count suggests.

**The nine**, per the PDPC Compliance Plan Checklist:

| Right | Before | Now |
|---|---|---|
| Be informed | — | Privacy notice (D-031) plus collection-time disclosure |
| Access | — | Self-service export in the privacy dashboard |
| Withdraw consent | D-024 | unchanged |
| Erasure | D-026.1 | unchanged, request path |
| **Restrict processing** | — | Processing flag, request path |
| **Data portability** | — | Same export, machine-readable format |
| **Object** | — | Dashboard; mostly shares the consent-withdrawal mechanism |
| **Rectification** | — | Account editing, plus a route for non-editable data |
| Be notified of a breach | D-025 | unchanged |

**Deadline.** Requests must be **acknowledged within six working days**. Tracked with
the deadline visible in the system, not remembered.

**Design:**

- **The privacy dashboard is the primary mechanism.** Access, portability,
  rectification, objection and consent management are self-service. Better than a
  request queue — there is nothing to acknowledge within six days if the customer
  simply gets it
- **Erasure and restriction use a request path**, since both carry consequences
  warranting human review. These carry the six-working-day clock
- **Restriction is a processing flag** evaluated wherever the access gate is
  evaluated, declared in the model builder alongside sensitivity (D-030) so
  enforcement is consistent rather than remembered per call site. A restricted
  account still exists but cannot be acted upon
- **One export routine, two formats** — human-readable for access, machine-readable
  for portability


---

## D-038 — Organization lifecycle

**Date:** 2026-08-25 · **Status:** accepted · **Resolves:** R-11 · **Extends:** D-001, D-028

**TL;DR.** D-028 created the first organization; nothing covered creating others or
removing one. Deletion cascades — members, grants, owned data — so it needs a
reversal window rather than an immediate action.

**Decision.**

- **Request deletion** → the organization **suspends immediately**; access stops
- **Grace window** runs, configurable, **default 30 days**
- **Cancellable at any point** during the window; everything restores
- **Hard deletion** executes at the end of the window

**Invariants:**

- The **administrative organization can never be deleted**, only its membership
  changed. Deleting it would leave a system nobody can administer
- **Owned data follows D-026.1** — pseudonymised rather than destroyed, so audit
  history survives the organization

**Rejected.**
- *Suspend only, never delete* — accumulates dead records permanently and sits badly
  with erasure requests, since a customer's history would be held by a structure
  that can never be removed
- *Immediate deletion* — unrecoverable, and a foot-gun where one person holds
  system-admin (D-029)
- *Requiring dormancy before deletion may be requested* — proposed in an earlier
  draft and **withdrawn**. No major platform does this; it prevents decisive action
  on an active organization and would drive people to deactivate things manually
  just to unlock the option

**Basis, stated honestly.** This is **convergent industry practice** — Google
Workspace, Microsoft 365, AWS Organizations, Atlassian and Slack all follow the
request → suspend → grace → hard-delete shape with roughly a 30-day window. It is
not a codified standard like an RFC or a NIST publication. Good evidence, different
kind of authority.


---

## D-039 — Age verification: self-declaration plus a documented takedown route

> **Superseded in part.** The host business rules and vendor facts this entry records (orders, payments, shipments, couriers, addresses) moved to the host under D-165; the reasoning stands as the host's inheritance.

**Date:** 2026-08-25 · **Status:** accepted · **Resolves:** R-23 · **Amends:** D-027

**TL;DR.** Keep the 18+ checkbox. Do not attempt real age verification. But write
down in advance what happens if a customer turns out to be under 18, and follow it.

**Why D-027 needed revisiting.** The checkbox was chosen when the storefront was
assumed to hold ordinary data. D-030 established it holds health data, and the
Consent guideline requires, for sensitive data, *"proportionate verification
processes to determine whether the data subject meets the required age threshold or
whether guardian consent is necessary."*

The operative word is **proportionate** — not "reliable" or "documented." No method
is prescribed.

**Decision.**

- **Self-declared 18+ affirmation retained** on both registration and guest checkout
  (D-027)
- **A written takedown procedure**, defined in advance: on any credible indication
  that a customer is under 18 — self-disclosure, guardian contact, something surfaced
  in support — the account is suspended, open orders cancelled, personal data
  removed, and the event recorded
- The procedure is **followed, not improvised.** An undocumented intention to act is
  not a control; a short written procedure is

**Rejected.**
- *Signal-based review (payment or delivery indicators)* — weak signals, false
  positives against legitimate customers
- *Real verification via ID upload* — disproportionate. Would mean holding identity
  documents for every customer, a larger data protection problem than the one it
  solves, and would damage conversion

**Reasoning on proportionality.** The realistic case is a 17-year-old buying for
their household, not a minor covertly ordering insulin needles. Existing controls —
mandatory phone verification (D-013) and payment — already filter most of it.


---

## D-040 — Collation and Unicode normalization

**Date:** 2026-08-25 · **Status:** accepted · **Resolves:** R-16 · **Closes:** D-021 seams 1 and 2

**TL;DR.** Two database settings that are trivial now and painful later. D-021
flagged both and never assigned values.

### Collation

**ICU collation with the database default, plus a case-insensitive collation for
identifiers.**

Unicode's own sorting rules handle Arabic and English correctly; without them Arabic
sorts by byte order, producing meaningless ordering.

The case-insensitive identifier collation matters more than it appears. PostgreSQL
compares case-sensitively by default, so `Ahmed@x.com` and `ahmed@x.com` would be
distinct — permitting two accounts for one mailbox, and causing a lowercase-only
login lookup to fail silently for anyone who typed a capital.

### Unicode normalization

**All identifiers normalized to a single canonical form at write time**, before
storage or comparison. The normalized form is stored and compared; the original is
retained for display.

Applied to: email addresses, phone numbers, organization names, display names.

**Mixed-script rejection within a single word.** An Arabic word containing a Latin
lookalike character — or the reverse — is rejected. Whole-word Arabic or whole-word
Latin is fine; mixing inside one word is a spoofing signal.

**Why this is security, not presentation.** Multiple code-point sequences render
identically, so without normalization two accounts can hold visually identical
addresses or names — indistinguishable to a human, distinct to the database.
D-008's admin-assisted recovery uses a **single approver** who confirms identity
partly by recognising a name. A lookalike name is precisely the attack that defeats
a human check.

**Why both must be decided now.** Collation cannot be changed without rebuilding
every index. Normalization cannot be applied retroactively without reprocessing all
data and possibly surfacing collisions between accounts that are now duplicates.


---

## D-041 — Minor sweep (R-14, R-15, R-17 to R-22, R-24)

> **Superseded in part.** The host business rules and vendor facts this entry records (orders, payments, shipments, couriers, addresses) moved to the host under D-165; the reasoning stands as the host's inheritance.

**Date:** 2026-08-25 · **Status:** accepted

**TL;DR.** Eight small corrections from the review. Two were promises never
delivered, one was a question silently dropped, the rest are missing detail.

**R-14 — Introspection endpoint removed from D-005.** Stalwart validates tokens
offline via JWKS and does not use introspection. An endpoint nothing calls is
attack surface for no benefit. Reinstate only if a future client requires it.

**R-15 — Out-of-band identity check, now specified.** Promised in D-008 as
compensation for accepting a single approver, then never written. **The approver
contacts the person on a channel already recorded on the account** — the registered
phone, never one supplied in the request — and confirms details the account holds
that a stranger would not know. The pre-existing channel is the control; an
attacker-supplied channel proves nothing.

**R-17 — Authorization without authentication.** Flagged in the first review and
silently dropped; closing it now. When a consumer imports authorization alone,
step-up checks have no assurance state to read, so the library **fails closed** —
any permission requiring step-up is denied, never allowed. A consumer wanting
step-up must supply an assurance provider. Consistent with P-003's fail-closed test.

**R-18 — Licence renewal is a second accepted maintenance exception.** Controller
licences run three years, permits shorter. P-003 requires every recurring human task
be named. This is the second after D-010's break-glass reseal. The system tracks
expiry dates and warns, rather than relying on memory.

**R-19 — SQL fragments are PostgreSQL-only.** D-017 requires each permission rule to
render as both a LINQ expression and a SQL fragment; the fragment is dialect-bound.
Stated explicitly in the public contract (D-026.4) rather than implied.

**R-20 — Stalwart provisioning idempotency and reconciliation.** D-006 specified
"retries plus reconciliation" without defining either. Each lifecycle event carries
a **stable idempotency key** so retries cannot duplicate. Reconciliation runs
**daily**, compares both sides, and **flags drift without auto-correcting** —
silent auto-correction conceals a broken pipeline.

**R-21 — HIBP appears in the RoPA.** Lawful per D-023, but still a cross-border
transfer requiring a row with destination and basis. Easy to omit precisely because
it is buried in a password field.

**R-22 — SMS gateway requires a data protection agreement.** Fourth processor
alongside OPay, Bosta and IONOS. Recorded so it is not overlooked for feeling like
infrastructure rather than a vendor.

**R-24 — Privacy notice versioning.** D-024 versions consent, but consent validity
depends on which notice version was displayed. The notice carries its own version,
with both languages moving together per D-031.


---

## D-042 — GitHub plan constraints; destructive-DDL gate reworked

**Date:** 2026-08-25 · **Status:** accepted · **Amends:** D-019 · **Affects:** conventions 6

**TL;DR.** D-019 specified the destructive-DDL gate as a GitHub environment
protection rule. That feature is not available on GitHub Pro for private
repositories. Reworked to a mechanism that is.

**Plan context:** GitHub Pro, individual account, private repository.

### What is available

Environments, environment secrets and variables, deployment branch restrictions,
protected branches, required pull request reviewers, multiple reviewers, code
owners. 3,000 Actions minutes per month. 2 GB Packages storage.

### What is not

- **Required reviewers and wait timers on environments** — public repositories only
  on Free, Pro and Team; private repositories require Enterprise
- **Secret scanning and push protection on private repositories** — requires
  Advanced Security, unavailable on personal plans at any tier

### 42.1 — Destructive-DDL gate, reworked

**Superseding D-019's mechanism.** The pipeline splits:

- Destructive-operation **detection always runs** and always reports (unchanged
  from D-019)
- When the gate is **enabled** and destructive operations are present, the automatic
  deploy **fails with a message** rather than pausing for approval
- A **separate manually-dispatched workflow** applies destructive migrations
- The toggle is a **repository variable**, not an environment setting

Same semantics — a human decides — using only available features. Currently
disabled, as D-019 specified.

**When a team exists**, the variable must be protected from pull-request
modification, per D-019's original intent.

### 42.2 — Branch protection uses status checks, not approvals

GitHub does not permit approving one's own pull request, so requiring approvals on a
single-developer repository would block all merges.

**Protected branch requires status checks to pass**, not approvals. The automated
gates are the enforcement; the pull request remains for reading the diff as a unit.

**When someone joins:** required approvals are enabled — Pro already supports this
for private repositories.

### 42.3 — Secret scanning moves into the pipeline

OPS-SEC-001 forbids secrets in repository files, and GitHub's own enforcement is
unavailable on this plan. A **secret-scanning step using an open-source scanner runs
in CI** and fails the build on detection.

### 42.4 — Quota constraints to design around

- **Actions minutes (3,000/month).** The double migration run (OPS-MIG-007) plus
  containerised integration tests consume these quickly. Full integration suites
  should not run on every push to every branch. Measure early.
- **Packages storage (2 GB).** Published versions accumulate. Prune pre-release
  versions.


---

## D-043 — Derived grants

**Date:** 2026-08-25 · **Status:** accepted · **Extends:** D-015 · **Amends:** D-007's caching rule

**TL;DR.** Permissions could only exist because someone wrote a row. Now they can
also be **computed** from a fact in the business data — "the assigned rep on an
account can read that account's orders." Same sentence, same rules, no maintenance.
This is what lets the model cover relationship-based access, and it is the
prerequisite for delegation, approval workflows, access reviews and context
conditions later.

**The addition.** A **derivation** is declared in the model builder: whoever holds a
named relationship in the host's own data holds a given role on a given resource
type. Evaluated at query time, composed into the same filter, obeying deny,
inheritance and organization scoping identically.

This is the construct Zanzibar calls a **computed userset**. The design is standard
rather than invented.

**What this completes.** The model now spans the full capability space:

| Model | Expressed as |
|---|---|
| RBAC | Roles over stored grants |
| ACL / fine-grained | A stored grant on a single record |
| ReBAC | Derived grants |
| ABAC | Record-level conditions, which may only remove access |

**What it enables later**, each additive and none requiring a rewrite: delegation
(a constraint on who may write grants), approval workflows (a state machine before a
grant is written), access reviews (a report over stored grants — derived ones
self-correct and need no review), context conditions (another input to the existing
rule evaluator).

**The caching consequence, and why it is a real constraint.** Derived permissions
compose recursively over host data, so a naive subject-action-resource cache cannot
be correctly invalidated — the change that should invalidate it may occur in a table
the cache never observed. This is a known failure in Zanzibar-style systems.
**Derived grants are therefore not cached per triple.** Where evaluation is too
costly, the answer is materialisation, not caching. D-007's caching rule stands for
stored grants and group resolution.

**The optimisation ladder**, recorded as a sequence so it is not reinvented under
pressure:

| Rung | Technique | Cost |
|---|---|---|
| 1 | Index the columns the derivation depends on | None |
| 2 | Denormalise the derivation's key onto the resource | Small write cost |
| 3 | Cache the resolved subject set per request | None beyond memory |
| 4 | Materialise the derivation as stored grants | A synchronisation problem |
| 5 | Migrate authorization out of the library | A project |

Materialisation is what Zanzibar's Leopard index does, for the same reason.

**The genuine ceiling: reverse lookup.** "Who can access this?" is a query against
stored grants and an evaluation against derived ones — there is no row to look up.
Materialisation is the only mitigation, which is precisely why graph-based systems
materialise everything. This is the ceiling the migration trigger exists for.

**One advantage retained.** Zanzibar needs consistency tokens because permission data
and application data live in separate systems and can disagree. Ours share one
database and one transaction, so that class of problem does not arise.

**Guardrail.** Derivations SHALL NOT be used to express what a stored grant expresses
naturally. The known failure mode in relationship-based systems is turning every
permission into a relation, producing contrived relationships that exist only to
satisfy the engine.

**Rejected.**
- *Stored grants only* — cannot express relationship-derived access without someone
  maintaining rows that will drift.
- *Derived grants only* — the failure mode above; also loses cheap reverse lookup.
- *Caching derived grants per triple* — cannot be correctly invalidated.

**Claim discipline.** This spans the capability space and grows into it without a
rewrite. It does **not** guarantee every capability stays fast at every scale. The
migration trigger stands: top-three slow query with index tuning exhausted.


---

## D-044 — Backup and disaster recovery

> **Superseded in part.** The host business rules and vendor facts this entry records (orders, payments, shipments, couriers, addresses) moved to the host under D-165; the reasoning stands as the host's inheritance.

**Date:** 2026-08-25 · **Status:** accepted · **Closes:** the gap under OPS-MIG-006 and D-019

**TL;DR.** Continuous log archiving gives near-zero data loss cheaply. Fast recovery
needs a second server, which was declined on cost, so recovery time is stated
honestly at 4–8 hours rather than the 2–3 originally wanted. Backups stay on the
same host until launch, then must move off it.

**Objectives.**

| | Target |
|---|---|
| Recovery point | Seconds |
| Recovery time | **4–8 hours, accepted deliberately** |

The stated need was 2–3 hours. Meeting that reliably requires a warm standby to fail
over to; restoring a real database takes longer once provisioning, transfer, replay
and verification are counted. **A second server was declined on cost**, so the
objective is stated at what is achievable. A deliberate trade of recovery time for
infrastructure cost.

**Phased.**

| Phase | Backups | Basis |
|---|---|---|
| Through launch and early operation | **On the same host** | User's decision. Covers destructive migrations and accidental deletion — the failures that actually occur |
| At the **VPS tier upgrade** | **Off-machine** | Bundled object storage becomes available; trigger is the upgrade already planned for after release |
| Later | Second provider; warm standby | Triggers recorded |

**Accepted risk, Phase 1.** From launch the database holds health data (D-030). Loss
of the host during this phase means **permanent, unrecoverable loss of customer
health data**, and is a reportable incident under the availability limb of the
breach rules. Raised twice and declined twice; accepted deliberately and bounded by
the DR-005 trigger rather than left open-ended.

**Consequence for compliance records.** Generated records of processing must state
technical security measures **accurately for the current phase** — they must not
claim off-machine backups that do not exist, and must be updated at the upgrade.

**Object storage was proposed for launch and declined** — the user states it is
bundled only with a higher VPS tier and does not want a separate contract.

**A personal machine was proposed as the backup target and rejected.** Availability
(a target only present when its owner is online is not a target), durability (single
disk, and ransomware specifically hunts attached backups), and compliance — copying
a database containing health data to a personal machine makes it a processing
location requiring its own records entry and controls. Retained as an optional
encrypted third copy, never as the plan.

**Testing.** Restore tested quarterly, **timed**, to a separate host, with the
measured time recorded. An untested backup is a hypothesis.

**Recorded consequence.** A restore does not roll back external systems. Mail is
delivered, payments captured, shipments dispatched. Any restore that moves time
backwards requires reconciliation against the payment provider, the courier, and
Stalwart. Restoring is not undo.

**Rejected.**
- *Synchronous replication for literal zero loss* — writes would block on a second
  server, and writes stop entirely if it fails. Near-zero via continuous archiving
  is the correct target.
- *Warm standby now* — declined on cost. Can be added later with no redesign; it
  consumes the same log stream.
- *Personal machine as the backup target* — reasons above.


---

## D-045 — Insider data exfiltration: detect, and gate exports

> **Superseded in part.** The host business rules and vendor facts this entry records (orders, payments, shipments, couriers, addresses) moved to the host under D-165; the reasoning stands as the host's inheritance.

**Date:** 2026-08-25 · **Status:** accepted · **Closes:** threat model §6.1

**TL;DR.** Staff with legitimate access could read or export customer data in bulk
and nothing would notice. Permissions control what they reach, not how much. Now:
volume is counted and anomalies alerted, and exports require step-up and are audited.

**Why this matters more here than generally.** The B2B customer list is the
business's market position, and order history is health data (D-030). The realistic
scenario is unremarkable — someone leaving for a competitor exports the customer list
in their final week.

**Decision.**

**Detection.** Records returned per actor are counted per period. Alerting is on
**deviation from that actor's own baseline**, not a fixed threshold — a warehouse
clerk's normal differs from an account manager's.

**Export gating.** Export operations require step-up, are individually audited (who,
what, when, why), and are rate-limited. Bulk export is the actual exfiltration
mechanism; ordinary browsing is not.

**Configurability.** Thresholds, rate limits and step-up requirements are
runtime-configurable like any other setting.

**Export auditing is redeploy-only.** It joins the OPS-CFG-004 list. The test there
is *"does turning this off blind us to the person turning it off"* — an insider who
can disable export auditing then exports. Thresholds may be tuned at runtime; the
auditing itself may not be switched off.

**Default is the tight configuration** — step-up, audit, and rate limit all on.
Whether staff legitimately need bulk export is unresolved ("maybe"), so the default
is the safe one and loosening follows D-010's rules: step-up, written reason, audit
entry. Tightening remains free.

**Rejected.**
- *Accept and build nothing* — means learning about exfiltration from a competitor.
- *Prevention alone (hard caps on reads)* — breaks legitimate work; staff genuinely
  need to view many records, and a system that fights them gets worked around.

**Honest limit.** This detects and deters; it does not prevent. Someone determined,
with legitimate access, taking data slowly will succeed. True of every system — the
goal is that it is neither casual nor invisible.


---

## D-046 — Dependency supply chain

**Date:** 2026-08-25 · **Status:** accepted · **Closes:** threat model §6.3

**TL;DR.** The system will use 30–40 third-party packages. If one is compromised, its
code runs with full access to customer data. Nothing in the specification addressed
this.

**Decision — three habits, no tooling purchase:**

- **Pin versions** via a committed lockfile. Updates are deliberate; a compromised
  release cannot arrive implicitly
- **Enable automated vulnerability alerting.** Free on the current plan for private
  repositories, unlike secret scanning (D-042.3)
- **Add dependencies deliberately.** Each is another author trusted with full
  application privilege. New direct dependencies arrive in their own commit with a
  stated reason
- **Apply security updates promptly**, routine updates on a cadence — chasing every
  release is churn, never updating accumulates known holes

**Rejected.**
- *Paid supply-chain scanning tooling* — disproportionate at this scale; the free
  alerting covers the realistic cases.
- *Vendoring or auditing dependency source* — not achievable by one developer.

**Honest limit.** This covers *known* vulnerabilities. A freshly compromised package
with no advisory yet is not caught by any of it. Minimising dependency count is the
only real mitigation there.


---

## D-047 — Operator machine and secrets custody

**Date:** 2026-08-25 · **Status:** accepted · **Closes:** threat model §6.4

**TL;DR.** The operator's machine holds deployment access to production, and nothing
in the specification addressed it. It turns out the hard part is already solved.

**Existing arrangement, confirmed.** All credentials — production and
non-production — live in a password manager protected by a **security key plus a
password**, with a backup. **No credential is stored in a file on any machine.**

This is stronger than the hardware-key-for-production option that was about to be
proposed, and it closes the substantive risk: compromising the machine does not
yield production credentials.

**Residual, unavoidable.** An unlocked vault on a compromised machine is an open
vault — malware waits for the unlock rather than reading a file. True of every
secrets manager. Mitigated by a short auto-lock timeout, which is a setting rather
than a project.

**Remaining items, hygiene rather than design:**
- Full-disk encryption
- Operating system kept current
- Vault auto-lock kept short; production credentials not left unlocked

**Not specified as requirements.** These govern a personal machine, not the system,
and the operator already applies stronger practice than a specification would have
imposed.

**Note on scope.** D-044 rejected using a personal machine as a *backup target* on
availability, durability and compliance grounds. That stands and is unrelated —
credentials in a vault and a database copy on a laptop are different things.


---

## D-048 — Security alerting

**Date:** 2026-08-25 · **Status:** accepted · **Closes:** threat model §6.5

**TL;DR.** Every failed login and denied permission is logged; nothing looks at them.
Throttling slows an attacker but nobody learns it happened. Now five conditions raise
an alert.

**Alert conditions:**

| Condition | Severity | Source |
|---|---|---|
| Sustained authentication failures against one account | High | AUTH-ABUSE-001 |
| Recovery attempts clustering on one account | High | AUTH-RECOV-002 |
| One approver handling unusual recovery volume | High | D-008, R-A03 |
| Spike in permission denials | Normal | AUTHZ-GATE-004 |
| Break-glass credential used | High | OPS-BOOT-002 |
| Read-volume or export anomaly | High | D-045 |
| Gateway balance drain or floor breach | Normal | INT-SMS-004 |

**Channels: email for everything, SMS additionally for high severity.** Both support
delivery confirmation — the gateway's delivery report and the mail server's own
status — so an alert that failed to arrive is itself detectable.

**Three failure modes designed around:**

- **Alert flooding is a toll-fraud vector.** One SMS per failed login would let an
  attacker drain the prepaid balance by triggering alerts. Alerts are therefore
  **deduplicated per condition per window** — one message describing a sustained
  attack, never one per attempt.
- **The balance alert cannot use SMS.** INT-SMS-004 hard-stops sends below the floor,
  which would block the message reporting the floor breach. Balance alerts go by
  email, and **alert sends are exempt from the hard stop**.
- **Mail-system alerts cannot rely on email.** If the mail server is the problem, the
  alert lands in the broken thing. Those are SMS-first.

**Completes two earlier promises.** D-045's exfiltration alerting and D-008's
per-approver anomaly detection both specified alerts without a delivery mechanism.
This is it.

**Rejected.**
- *Nothing; read logs when something feels wrong* — detection by luck.
- *Full monitoring stack with dashboards* — disproportionate for one person. A
  dashboard nobody opens is worse than an alert that reaches a phone.


---

## D-049 — Cash-on-delivery abuse: out of scope

> **Superseded in part.** The host business rules and vendor facts this entry records (orders, payments, shipments, couriers, addresses) moved to the host under D-165; the reasoning stands as the host's inheritance.

**Date:** 2026-08-25 · **Status:** accepted · **Closes:** threat model §6.2

**TL;DR.** Someone orders, refuses delivery, the company absorbs the shipping cost.
Repeatable, and usable as harassment as much as fraud. Real, but not an identity or
authorization problem — no permission change prevents it.

**Decision.** Recorded as **out of scope for this library**, and as a requirement on
the order system.

**Why it does not belong here.** The person placing the order is a legitimate,
verified account holder exercising a permission they properly hold. Nothing in
authentication or authorization is being circumvented. The controls that would work
are commercial: order limits for new customers, prepayment above a threshold, refusal
history tracked against an account, deposits for repeat offenders.

**What this library does provide** that the order system can build on: a stable
subject identifier that survives account changes (IDN-ACCT-002), so refusal history
attaches to a person rather than an email address; and audit records queryable by
subject (PRIV-BREACH-002).

**Owner:** the order system, not this specification. Named here so it is a recorded
handoff rather than an omission.


---

## D-050 — Staff offboarding procedure

**Date:** 2026-08-25 · **Status:** accepted

**TL;DR.** Staff are few, not all personally known, with periodic turnover. Every
departure is someone who held access. Done from memory, something gets missed — and
the thing most often missed is the mail app password, because it lives in a different
system.

**Decision.** An eight-step checklist, `16-offboarding-procedure.md`, with a
verification step two days later.

**Order matters.** Sessions revoked first (deactivation alone leaves active sessions
on phones), mail app password last-but-one because it is where notifications land,
verification after.

**The step that exists because of a design decision.** D-006 put app passwords in the
mail server rather than the identity system, which was correct — but it means
deactivating an account **does not** revoke mail access. That is a long-lived bearer
secret held by a former employee. It gets its own step and its own verification.

**Verification is not optional.** INT-MAIL-007 flags propagation drift **without
correcting it**, deliberately, so a broken pipeline is visible. That only works if
someone reads the report.

**Assumption corrected.** The threat model originally assumed a small, personally
known, stable team. The user confirmed otherwise — few staff, not all known, periodic
turnover. That is materially higher insider risk and is why this procedure exists at
all rather than being left to judgment.

**Explicitly not covered:** physical items, and knowledge. Someone who worked with
the customers remembers them; no technical control addresses that, and none is
proposed.


---

## D-051 — Shared credentials: prohibited and detected

**Date:** 2026-08-25 · **Status:** accepted · **Closes:** threat model §6.6

**TL;DR.** In workplaces with turnover, people share accounts. It is the fastest way
to make an audit trail worthless — you can no longer tell who did what, so every
other control that relies on attribution degrades with it.

**Decision — prohibition plus detection.**

**Prohibited explicitly.** Accounts are personal. Sharing credentials is prohibited,
stated where staff will see it rather than assumed.

**Structurally hard already.** Administrative-organization members use passkeys for
interactive access (D-006), and a passkey cannot be texted to a colleague the way a
password can. This was a consequence of an earlier decision rather than its purpose,
but it is the strongest control here and it already exists.

**Detected.** Concurrent sessions for one account from implausibly distant origins,
or an authentication pattern inconsistent with one person, raise an alert through the
D-048 channels.

**Why this matters more than it appears.** Attribution is load-bearing across the
design: D-045's per-actor volume alerting, D-008's per-approver anomaly detection,
audit records naming acting and effective identity (D-014), and the offboarding
verification in D-050 all assume an account corresponds to a person. Shared
credentials break all four at once, silently.

**Consequence for offboarding.** On discovering a shared login during offboarding,
the checklist alone is insufficient — whoever it was shared with still holds the
credential. Every account that touched it requires re-enrolment, not just the
departing one. Recorded in `16-offboarding-procedure.md` §6.

**Rejected.**
- *Accept it as normal practice* — degrades attribution, and with it four other
  controls.
- *Technical prevention beyond passkeys* — device binding or similar would break
  legitimate multi-device use for no proportionate gain.

**Honest limit.** Detection catches obviously implausible patterns, not a colleague
sharing a laptop in the same office. The passkey requirement is what makes that hard,
not the alerting.


---

## D-052 — BFF middleware is shipped by the library

**Date:** 2026-08-25 · **Status:** accepted · **Extends:** LIB-API-001

**TL;DR.** Several requirements named the BFF as their enforcement point while no
document described it. It was carrying real security weight — session cookies, CSRF,
capability projection, error translation — as an unspecified component. The library
now ships it as middleware the host mounts.

**Decision.** The library owns the BFF pipeline. The host mounts it and owns
endpoints and business logic.

| Library owns | Host owns |
|---|---|
| Session cookie: issue, read, rotate, revoke | Which endpoints exist and what they do |
| CSRF enforcement | Business logic and domain queries |
| Error translation and concealment | Which capabilities are relevant per resource |
| Capability projection plumbing | Route layout and mounting point |
| Assurance and step-up gating | |
| Correlation identifier propagation | |

**Why the library rather than the host.** AUTH-SESS-007 requires that **no endpoint
can opt out** of CSRF. That is achievable only with library-owned middleware — a
host-built BFF turns "no endpoint can forget" into something to get right in four
separate applications, and the requirement becomes aspirational. The same reasoning
applies to session handling and error translation, each specified as *enforced* rather
than *followed*.

P-003's secure-by-default test asks whether a consumer can produce an insecure
deployment by not configuring something. Host-built BFFs answer yes, four times.

**Pipeline order is part of the contract.** Correlation → session → CSRF → throttling
→ step-up gating → host endpoint → capability projection → error translation and
concealment. Order is security-relevant: CSRF must precede any state change,
throttling must precede expensive work, and concealment must be last so nothing
earlier has already disclosed existence. Host middleware may be added before the
first stage or after the host endpoint, never between security stages.

**Consequence for the library contract.** The middleware pipeline and its ordering
guarantees join the public surface in LIB-API-001, so they cannot change freely. This
also pulls the library into the ASP.NET Core request pipeline in a way it previously
was not — `Janus.Hosting` is where it lives.

**Cost accepted.** The library becomes heavier and more opinionated. A future consumer
wanting a different session model would have to fight it. Acceptable because all four
applications share identical requirements and the alternative weakens three security
requirements to advisory.

**Rejected.**
- *Host builds the BFF; library provides contracts only* — makes CSRF
  no-endpoint-can-opt-out unenforceable, and repeats session handling four times.
- *Library provides helpers the host composes* — same failure, less obviously.


---

## D-053 — BFF CSRF and pipeline ordering, corrected after research

**Date:** 2026-08-25 · **Status:** accepted · **Amends:** D-052, BFF-CSRF-001/002, BFF-ORDER-001

**TL;DR.** The first BFF draft was written from reasoning rather than verified
guidance. Research found three defects and one accidental strength.

### Defects corrected

**1 — The CSRF mechanism was unspecified.** The draft required "a token unreadable by
a cross-origin attacker," which is ambiguous between synchronizer tokens and
double-submit cookies. OWASP's rule is that **stateful software uses the synchronizer
token pattern**; double-submit is for stateless. This BFF holds server-side session
records, so the token binds to the session.

**2 — Fetch Metadata was absent entirely.** `Sec-Fetch-*` are **forbidden request
headers** — a browser will not let JavaScript set them — which makes them a stronger
signal than anything a page can influence. A Resource Isolation Policy rejecting
cross-site state-changing requests is now the first substantive check, and **fails
closed on absence** rather than treating a missing header as permission.

Also added: **custom request header** (OWASP identifies this as particularly suited to
XHR endpoints, relying on the same-origin policy) and **Origin validation**.

**3 — `SameSite` had no value.** The draft said "SameSite" without saying which.
Current guidance is Strict where possible, Lax where necessary. **Strict for the
management application**, which has no external entry points; **Lax elsewhere**,
because magic links from email arrive as top-level navigations and Strict would
withhold the session cookie on that first request. Never `None`.

### Ordering corrected

The original pipeline collapsed throttling into one stage placed after CSRF, which
would let an unauthenticated flood reach session resolution.

**Throttling is two stages.** Source-based limiting precedes session lookup;
account-based limiting cannot, because it requires an identity. Fetch Metadata and
Origin checks move to the front — they are cheap and stateless.

### Accidentally right, for a better reason

The draft required the `__Host-` prefix as hardening. Research shows it is
load-bearing for a specific reason: it **forbids a `Domain` attribute**, and OWASP
warns that a cookie scoped to a parent domain is shared by every subdomain — so one
weak or CNAME'd subdomain would compromise all four applications. The prefix is what
makes per-application session isolation real rather than nominal.

### Method note

This correction exists because the user asked whether the BFF document had been
researched or written from memory. It had been written from memory. **The same
question is worth asking of any document produced without visible sources.**


---

## D-054 — The library never renders user-facing text

**Date:** 2026-08-25 · **Status:** accepted · **Amends:** D-021, LIB-API-003

**TL;DR.** One rule instead of a rule plus an exception. Everything a person reads is
rendered by the frontend and localized there.

**Why the carve-out existed.** D-021 established that errors cross the boundary as
codes, with an exception for pages the library served directly to a browser —
magic-link landings and similar. Those pages have to say something, and the identity
library's backend is not Angular, so the localization library is not present there.

**Why it is removed.** The link does not have to land on a library-rendered page. It
can land on a **frontend route**; the library validates the token behind it and
returns a code, exactly as everywhere else.

**Consequences:**

- The exception in LIB-API-003 is deleted. No library endpoint returns HTML intended
  for a person to read, including framework default error pages
- `GET /oidc/authorize` **redirects** to the authentication application rather than
  rendering a sign-in page, which a conventional OIDC provider would
- Every link delivered to a user — magic link, verification, recovery, enrolment —
  lands on a frontend route (API-LAND-001)

**It buys more than tidiness.** Those pages become server-rendered **and** localized.
An earlier framing said the localization library "isn't there for server-side
rendering," which was wrong: it does server rendering as a first-class capability and
is present wherever Angular runs, including Angular's server renderer. It is absent
only inside a non-Angular backend serving its own HTML. The distinction is which
stack renders, not whether rendering happens on the server.

**Cost:** one redirect.

**Related requirement retained.** .NET-side text — email and SMS templates — remains
the worker's responsibility. The specification states only what must be true: every
message exists in every configured locale, SMS stays within its language's encoded
length budget, and failures surface at startup rather than at send time. **How** the
worker satisfies that is not specified.


---

## D-055 — Recipient locale resolution

**Date:** 2026-08-25 · **Status:** accepted · **Extends:** D-031, D-022

**TL;DR.** A notification sent by a background job at 3am has no browser and no URL,
so something must answer "what language does this person read?"

**Decision — a precedence order, not a single source:**

1. **Stored account preference**, where set
2. **The current request's locale**, where there is a request
3. **Both languages**, only when there is neither

**Language preference is an account attribute**, owned by identity. It is a property
of a person, and people are what the identity system holds. Left elsewhere, the
worker would invent its own store and two places would claim to know someone's
language.

**Registration sets the preference from the request locale.** During registration
there *is* a request context, so the verification SMS naturally matches the language
the person is using — and using it as evidence of preference means case 3 becomes
nearly unreachable rather than merely rare.

**Consequences:**

- Language preference is personal data and appears in the subject access export
  (PRIV-RIGHT-003)
- It is user-changeable and visible to the person, not a hidden derived value
- **The SMS length budget cannot be validated per template in isolation.** The
  recipient's language is unknown until the recipient is resolved, so every template
  must be validated in **every configured language** at startup — any of them might
  be the one sent

**Rejected.**
- *Derive from the last request* — produces surprises. A shared link opened by a
  colleague in another language would silently switch someone's notifications.
- *Never store it; always send both languages* — retained only as the final fallback.
  Correct when the language is genuinely unknown, needlessly awkward otherwise.


---

## D-056 — The privacy notice is presented, not accepted

> **Superseded in part.** The host business rules and vendor facts this entry records (orders, payments, shipments, couriers, addresses) moved to the host under D-165; the reasoning stands as the host's inheritance.

**Date:** 2026-08-25 · **Status:** accepted · **Extends:** D-024, D-032

**TL;DR.** A privacy notice is something you tell people, not something they agree to.
Asking for acceptance is a small wording choice with a real legal consequence.

**The distinction.** Terms of service are a contract — acceptance is correct. A
privacy notice is a **transparency obligation**: it is satisfied by informing, and
there is nothing to accept, because the right to process does not derive from the
person's agreement.

**Why the wording matters.** If a person "accepts" the notice, that implies **consent
is the lawful basis for everything described in it.** It is not — most processing
runs on contractual obligation (fulfilling an order) or legal obligation (retaining
financial records), neither of which depends on agreement.

Consent is withdrawable. A notice accepted *as consent* means a withdrawal could be
read as withdrawing the basis for holding order history. That is a problem created
entirely by a checkbox label.

**Decision — four items at the account-creation step, recorded differently:**

| Item | Action | Recorded as |
|---|---|---|
| Terms of service | Accept | Agreement, with version |
| Privacy notice | **Presented** | Which version was shown, and when |
| 18+ | Affirm | Declaration, with timestamp |
| Marketing | Consent, unticked | Consent record, withdrawable |

The notice is still shown, still prominent, still before the account exists. What
changes is the record: **"this version was displayed at this time"** rather than
"they agreed to it."

**Secondary benefit.** Separating the notice from the consent items stops marketing
consent appearing in a bundle beside two required items, which visually implied it
was part of the same transaction — the effect PRIV-CONS-002's no-bundling rule
exists to prevent.

**Consequence.** The presentation record carries the same version identifier as a
consent record (PRIV-CONS-006), so "which text did this person see" remains
answerable for the notice as well as for consents.


---

## D-057 — Return destinations are validated against a known list

> **Amended.** A pushed `redirect_uri` that is not the client's registered one is refused, not replaced (D-166).

**Date:** 2026-08-25 · **Status:** accepted

**TL;DR.** Registration completion sends the person back to the application they
started from. That destination travels as a parameter, and an unvalidated parameter
is an open redirect.

**The attack.** A link to the genuine registration page, carrying an
attacker-controlled return destination. The victim sees a real domain, real branding
and a real flow, then lands on the attacker's site at the final step — already
trusting it because everything before was authentic.

**Decision.** Return destinations are validated by **exact match against a configured
list of known application origins**. An unrecognised destination falls back to a
configured default rather than erroring, so the check is not itself a signal.

**Prefix and pattern matching are prohibited.** "Starts with the expected domain" is
defeated by `example.com.attacker.net`.

**Applies wherever a destination is carried as a parameter:** magic-link landings
(D-054), post-sign-in redirects.

### Amendment — registration records the originating client instead

**Registration does not carry a destination at all.** The initiating application sends
its **client identifier** from the D-005 client registry; the server resolves it and
records the reference on the pending registration. At completion the redirect resolves
from that stored client, not from anything in the request.

**Why this is stronger than validating a parameter.** A destination carried as a
parameter travels through every step across two subdomains — nine opportunities for
tampering, with validation deferred to the end where the damage would occur. Recorded
once at the start, it never travels again: the person cannot change it, an attacker
cannot inject it mid-flow, and the open-redirect problem largely ceases to exist
rather than being defended against.

**Validation happens at capture, not at use.** An unrecognised client identifier
resolves to nothing and the configured default applies, so by completion there is
nothing left to check.

**Worst case is benign.** Every registered client is first-party, so sending another
application's identifier lands the person on a different application of ours — not a
security outcome.

**One list, not two.** The client registry already exists for D-005 and is where the
browser applications are registered. It is therefore the source of both the valid
redirect targets and the validated origin list, which cannot drift apart.

**Secondary benefit.** Which application someone registered from is useful information
independent of the redirect, and is the kind of thing otherwise wished for after the
fact.

**Rejected within this amendment.**
- *Deriving the origin from request headers* — `Origin` is generally absent on the
  top-level GET navigation that starts registration, and `Referer` is strippable. The
  physical signal is not dependably available at the moment it is needed.
- *Storing a URL rather than a client reference* — still needs validating at use, and
  goes stale when routes change.

### Research grounding — RFC 9700 (BCP 240)

Verified against the OAuth 2.0 Security Best Current Practice rather than reasoned
from memory.

**The prohibition this design satisfies:** clients and authorization servers MUST NOT
expose URLs that forward the user's browser to arbitrary URIs obtained from a query
parameter. That prohibition applies to **any** endpoint forwarding a browser, not only
authorization endpoints — API-REDIR-001 is broadened accordingly.

**Why passing a client identifier is stronger than the standard's own mechanism.**
RFC 9700 requires exact string matching of a passed `redirect_uri` against
pre-registered values, forbidding wildcards, prefix matching, and extra query
parameters. It documents the failure mode concretely: a registered pattern such as
`https://*.somesite.example/*` lets an attacker who establishes any subdomain
impersonate the legitimate client.

**We pass no URI at all.** A client identifier resolves to a registered origin, so
there is no matching to misconfigure, no pattern to get wrong, and no parameter to
smuggle past a comparison. The class of bug the requirement guards against cannot
occur.

**An earlier concern was misplaced.** Unease that the browser supplies the identifier
on the first navigation was unfounded: OAuth deliberately carries `client_id` as a
public query parameter. It is an identifier, not a credential, and the security comes
from it resolving to something pre-registered. Substituting another identifier
redirects to that client — all of which are first-party.

The link is in any case constructed by our own application from its own configuration.

**Rejected.**
- *Trusting the referrer instead* — absent or spoofable.
- *Allowing any subdomain of the configured domain* — a compromised or CNAME'd
  subdomain becomes a redirect source, the same failure the `__Host-` cookie prefix
  exists to prevent (D-053).


---

## D-058 — Subject access export is data, not a document

**Date:** 2026-08-25 · **Status:** accepted · **Applies:** D-054 · **Closes:** the open item in `09-api-contract`

**TL;DR.** The access and portability export returns **structured data in both
formats**. The frontend renders the readable view. No server-generated PDF.

**Decision.** `human` is grouped, labelled and ordered for reading; `machine` is flat,
complete and stable-named. Neither is a rendered document. Rendering and localization
happen in the frontend, consistent with D-054.

**Why no PDF.** The obligation is that a person can understand what is held about
them, not that it arrives as a particular file type. A rendered page satisfies it, and
browsers print to PDF anyway.

Building generation would mean a templating library, layout code, and Arabic RTL
inside a PDF engine — a distinct category of difficulty from RTL on the web — for a
feature exercised a handful of times a year.

**And it is the safer artifact.** An export is health data leaving the system. A file
gets emailed, left in a downloads folder, and forwarded. A page read while
authenticated is a smaller exposure. Worth declining even if generation were free.


---

## D-059 — Message templates stay in the repository

**Date:** 2026-08-25 · **Status:** accepted · **Closes:** the backend template question

**TL;DR.** No change. Templates live in the repository and change by deploy. The
question of hot-editable message text was a problem this project does not have.

**Decision.** The specification states invariants only — every message exists in every
configured locale, SMS stays within its language's encoded length budget, failures
surface at startup rather than at send. **Where templates live remains the worker's
business, and the repository is the obvious answer.**

No new invariant about deploy-free editing is added.

**Why the question dissolved.** The industry maturity model is explicit: early stage
is repository-based with engineers owning everything; the hybrid stage arrives when
non-engineers — typically marketing — want to edit email in their own tool; a
notification platform comes at scale.

This project is early stage: one developer, no non-engineering editors. Moving
templates to a database or a mounted file solves a problem that does not exist here.

**And the cost of deploying was overstated.** The pipeline is push-to-repository with
automatic deployment. Fixing wording is a file edit and a push, measured in minutes.

**A finding worth retaining.** At least one production system in this space
deliberately makes security emails **non-customizable** — password resets and account
security notifications are fixed, to preserve standard formatting for security
reasons. The wording of a recovery email is part of the security control. Making it
casually editable is a step toward it being editable by whoever compromises an admin
session.

**Rejected.**
- *Templates in the database with an admin editor* — solves for non-engineer editors,
  of whom there are none.
- *Hot-editable file inside the running container* — the specific pattern identified
  as worst practice: the change survives until the next deploy, then vanishes
  silently.
- *File on a mounted volume with drift detection* — workable, but infrastructure built
  for a need that does not exist.

**Trigger to revisit:** when someone who is not an engineer needs to change a message.

**Process note.** This is the fourth occasion in this project where a decision was
manufactured from something that required none — see C-002, C-003, and the membership
acknowledgement discussion. The pattern: taking a real property (fast fixes are good),
inferring a requirement from it, and designing a mechanism before checking whether the
existing arrangement already satisfies it.


---

## D-060 — Profile photo: built for everyone, enabled by organization policy, stored in PostgreSQL

> **Amended.** Photos are off for the administrative organization until the host declares an image codec; enabling them is an administrator's policy change (D-166).

**Date:** 2026-08-25 · **Status:** accepted · **Extends:** D-002

**TL;DR.** Built properly once, switched on for the administrative organization only,
and stored in the database rather than on disk.

**Availability is organization policy**, not a user-type branch — the same pattern as
authentication policy in D-002. Enabled for the administrative organization; enabling
it for customers is a configuration change, not a code change.

**Trigger for customer availability:** when a feature exists that makes a customer
photo meaningful.

### Storage: PostgreSQL

Photo bytes live in a `bytea` column in a table of their own, so ordinary queries
about a person never read them. Metadata — owner, upload time, content type — sits in
normal columns.

**Why not a directory on the VPS**, given no object storage exists and none is planned:

- **Backups cover the database and nothing else.** Continuous archiving (D-044) backs
  up PostgreSQL. A directory on disk sits outside it, so during Phase 1 losing the
  host would lose the photos with no recovery path at all
- **Deletion becomes two operations that can disagree.** In the database, removing the
  photo happens in the same transaction as removing the account. On disk, an erasure
  that succeeds in the database and fails on the filesystem leaves the photo behind —
  the exact failure this design avoids elsewhere
- **Access control comes free.** A photo in the database passes through the gate like
  any other data. A file on disk needs its own serving path with its own
  authorization, a second place to get it wrong

**The usual objection is size**, and it does not apply: a re-encoded profile photo is
tens of kilobytes, for a handful of staff.

**Trigger to reconsider storage:** photos enabled for customers, or any genuinely
large artifact — order documents, scanned prescriptions. That is when object storage
earns its place, plausibly alongside the tier upgrade D-044 already anticipates.

### Requirements, since it is built once

- **Validation by content**, not by file extension or declared type
- **Size and dimension limits enforced server-side**
- **Re-encoding on upload**, which strips metadata. Photographs carry location and
  device information by default; a photo with GPS coordinates is a privacy exposure
  nobody intended
- **Served through the access gate**, never a public URL that works for anyone holding
  it
- **Deletion in the same transaction** as account deletion or erasure

**Rejected.**
- *Drop the feature entirely* — proposed, and declined in favour of building it
  properly once.
- *Filesystem storage on the VPS* — reasons above.
- *Object storage now* — none exists and none is planned until there is a realistic
  need.


---

## D-061 — Location, time zone, and address resolution

> **Superseded in part.** The host business rules and vendor facts this entry records (orders, payments, shipments, couriers, addresses) moved to the host under D-165; the reasoning stands as the host's inheritance.

**Date:** 2026-08-25 · **Status:** accepted · **Closes:** the location-detection gap

**TL;DR.** Time zone comes from the browser, not from location. Location is an opt-in
tool in the address flow that preselects city and district. Boundaries live in our own
database, so coordinates never leave our infrastructure. Free-text address lines
travel to the courier and are what actually gets the delivery made.

### Time zone

**From the browser's own setting**, not derived from location. The browser reports its
time zone directly — no permission prompt, no personal data collected, and more
accurate than IP-derived guesses, which get travellers and VPN users wrong.

**No location data is collected at registration.** The gap closes rather than being
documented.

### Address resolution

**Location is an opt-in tool in the address flow**, not detection during registration.
The person taps it when they want it, at the point they are entering an address —
which includes checkout, where it is most useful.

**It preselects; it does not fill.** Coordinates resolve to a city and district, the
person confirms or corrects, and they type street and landmark themselves. This is a
suggestion, not an authority — a wrong preselection costs one dropdown change.

**Coordinates are never stored.** They resolve to an area and are discarded.

### Resolution happens in our own database

**Area boundaries are stored as polygons and matched with a spatial query.** Exact
containment, not nearest-centre approximation, which gets boundaries wrong for anyone
near an edge.

**Nothing leaves our infrastructure.** No external geocoding service, so no rate
limits, no recipient entry in the records of processing, and no question about whether
coordinates sent to a third party are personal data.

**Rejected — public free geocoding services.** The public OpenStreetMap geocoder is
capped at one request per second, its policy exists to serve the OSM search bar, and
autocomplete is a banned use. More decisively, coverage in developing countries is
patchy — the vendors themselves note that OSM is far from complete house-number
coverage in most countries. Street-level reverse geocoding in Egypt would often return
something vague or wrong.

**Rejected — paid providers.** Not warranted before knowing whether customers struggle
with the address form.

**Boundary mismatch is not a blocker**, for two reasons the user identified: the person
corrects the suggestion before anything is saved, and where our boundaries disagree
with the courier's commercial zones, a person choosing manually produces the same
answer — that is a mismatch between the courier's model and reality, not something the
form can resolve.

**Start with city boundaries**, which are unambiguous and easy to source. District
polygons follow if the dropdown proves annoying.

**Corrections are evidence.** When a person overrides the preselection, log the
override, not only the final answer. Consistent overrides for an area indicate a wrong
boundary, and the signal accumulates without anyone gathering it.

### Free-text address lines

The courier's address model is **city, zone identifier, district identifier** plus
free text: first line, second line, building number, floor, apartment. Their own
example uses the second line for a **landmark** rather than an address component.

**Both are required.** The structured part routes the parcel to the right hub; the free
text is what the courier reads standing in the street. Landmark is prompted for
explicitly rather than left as an optional afterthought — in Egypt it is often what
actually completes the delivery.

**This de-risks the boundary problem entirely.** A wrong district costs routing
efficiency, not a failed delivery, because the free text still says where the person
is.

**Free-text lines get the same treatment as everything else sent to the courier**
(D-030): passed through, never logged, never used for anything but the shipment. They
are the field most likely to contain something unexpected, since people write whatever
they think will help.

### Courier taxonomy

Their city, zone and district lists are **cached locally and refreshed**, since zones
change. Addresses store the **district identifier**, not the name, so a rename does not
orphan existing addresses.

Their district records carry delivery availability flags, so the address form can
refuse an unserved area at entry rather than at dispatch.


---

## D-062 — Frontend platform and scope

**Date:** 2026-08-25 · **Status:** accepted

**TL;DR.** Angular 22, zoneless, signals. No store library. The frontend document
specifies wiring and constraints only — UI and UX stay independent.

**Scope, as the user defined it.** The frontend specification covers **integration and
constraints**. The test for inclusion: *would getting this wrong break security,
compliance, or correctness?* If the answer is "it would look bad," it belongs in
design.

**Platform.** Angular 22 or later, **zoneless**, standalone components, signals for
state, `inject()` over constructor injection. Zoneless became the default for new
projects in v21 (corrected from v22, see F-23 below and D-140); signals own component and application state while RxJS remains for
time-based async orchestration — fetch and transform with RxJS, store and display with
signals.

**No global store library.** Session and capability state is read-mostly and small:
who the person is, what assurance they reached, what they may do with a record. A
store adds ceremony without solving anything present.

An earlier draft proposed a store for a storefront cart. **Withdrawn** — the cart
belongs to the storefront application, not to identity, authentication or
authorization, and reaching for it as an example drifted outside scope.

**Testing.** Vitest for unit, Playwright for end-to-end. End-to-end coverage required
for flows verifiable no other way: registration through account creation, step-up,
recovery, consent capture, locale switching.

**Accessibility.** WCAG 2.2 AA required on compliance screens. Consent a person cannot
perceive or operate is not informed consent. The rest of the application should meet
the same bar; on those screens it is a requirement rather than a goal.

**Deliberately excluded**, with owners named: visual design, component library choice,
copy and tone, navigation structure, and business screens beyond identity and privacy.


---

## D-063 — Infrastructure constraints: domain, host, certificates

**Date:** 2026-08-25 · **Status:** accepted

**TL;DR.** Three environment properties these systems depend on. Everything else about
infrastructure is out of scope and named as such.

### 63.1 — Single registrable parent domain

**Confirmed: all applications share one domain.**

Passkeys bind to a domain at enrolment, so an application on a different registrable
domain cannot use a credential enrolled elsewhere. The relying party identifier is
set to the **parent**, not a subdomain, so credentials work across every application
and remain usable by a future native client.

Adding an application under the shared domain requires no re-enrolment.

### 63.2 — Single host, with the isolation claim scoped honestly

**Confirmed: a single VPS.**

Per-application session isolation (D-007) protects against **web-level compromise** —
a stolen cookie, a cross-site request, an injection in one application. It does
**not** protect against host compromise: someone who has the host has every
application, every process, and the database credential.

**This is the correct trade at current scale.** Web-level attacks are the ones that
occur; host compromise is a different tier of problem, and separate machines would
defend against something unlikely before real scale.

**Recorded so the design is not read as offering more than it does.** The claim now
appears with its scope wherever it is stated.

**Consequence.** Because everything shares a host, the mail server's separate database
(D-006) does more than avoid coupling to a pre-1.0 schema — it also keeps mail out of
the database holding customer health data.

### 63.3 — Certificates: requirements only

**Requirements:** every served origin presents a valid certificate; renewal does not
depend on a person being available; expiry is monitored **independently of whatever
performs renewal**; renewal failure alerts through the D-048 channels.

**The two monitors must not share a component.** Expiry monitoring sees the
consequence weeks late; renewal-failure monitoring sees the cause the same day.
Asking a broken renewer whether renewal works returns a confident wrong answer.

**Mechanism is out of scope** — issuance method, validation approach, client, and DNS
arrangement are infrastructure decisions. An earlier draft prescribed a certificate
shape and was withdrawn: how certificates are arranged is not a requirement of an
identity system.

**Context informing the requirement.** Certificate lifetimes are shortening on a
published schedule — 200 days from March 2026, 100 days from March 2027, 47 days from
March 2029 — so manual renewal grows more burdensome over time rather than staying
constant. Current arrangements are manual, which is workable at two renewals a year
and not at eight.

Automated issuance is available and free; the paid certificate in use provides
organization validation that browsers no longer surface, and a warranty rarely
claimed. The migration is a setup task rather than a data migration.

**Superseded by D-072.** Automated issuance is a **launch requirement**, not a
trigger. The interim manual arrangement described here does not apply — nothing is
live, so there is no migration and no interim period.

### Scope, stated

`19-infrastructure.md` states **requirements the environment must satisfy**, never how
to satisfy them. Container arrangement, reverse proxy choice, certificate mechanism,
DNS, network segmentation, volume layout, process supervision, host OS and sizing are
all explicitly out of scope with the owner named.


---

## D-064 — Marketing and analytics separated; licensing verified against primary sources

**Date:** 2026-08-25 · **Status:** accepted · **Extends:** D-023, D-024, D-032

**TL;DR.** Marketing and analytics were sharing one consent checkbox. They are
different things with different lawful bases and different licensing consequences,
and they are now separated.

### Marketing and analytics are distinct

| | Nature | Lawful basis | Licence |
|---|---|---|---|
| **Marketing** | We send them something | **Consent**, unticked, its own control | Supplementary licence, **only if messages are actually sent** |
| **Analytics** | We observe behaviour in our own system | **Legitimate interest**, with a recorded assessment | None |

**Why they cannot share a control.** PRIV-CONS-002 forbids bundling consent across
purposes. One control granting both means someone wanting product updates also agrees
to behavioural analysis, and someone objecting to analysis loses their product
updates. Neither is a real choice. **Two controls, two records, independently
withdrawable.**

**Why analytics is legitimate interest rather than consent.** Consent is withdrawable,
which would require removing someone from analysis retroactively. Legitimate interest
is not withdrawable but **can be objected to** — one of the nine rights already built
(D-037), handled in the privacy dashboard. Conditional on analytics remaining
internal, which the user confirms.

**Consequence:** analytics carries no licensing obligation and no deadline.

### Licensing verified against the regulator's own text

Re-checked against the primary guidelines rather than summaries, at the user's
request. Every previously stated obligation holds:

- **General licence — required, no exemption.** Article 19: data users may only
  process personal data provided they first obtain a licence or permit. No threshold
  or small-business exemption appears in any guideline. The company is a juridical
  person, so a **licence** (three years), not a permit (three to twelve months)
- **Cross-border permit — required, named explicitly.** The text covers *"engaging
  with cloud service providers where personal data is processed or stored on servers
  located outside Egypt"*
- **DPO — required, no scale exemption.** Stated identically in two guidelines:
  juridical persons must appoint one *"irrespective of the scale of their processing
  activities"*
- **Deadline confirmed.** All data users must **apply** within the one-year grace
  period; processing thereafter without the licence constitutes a violation

**Newly identified:** a **Direct Electronic Marketing** supplementary licence exists
for anyone who *"sends, or intends to send, electronic direct communications for the
purpose of marketing products or services, whether directly or through any third
party acting on their behalf."*

**Performing it in-house does not exempt it** — the trigger is sending the messages,
not sharing data with anyone. Order confirmations, delivery updates and security
notifications are contract performance, not marketing.

**Also flagged:** a **video surveillance** licence applies where cameras capture
identifiable people in public areas. Household monitoring is exempt; premises are
likely not. Applicability unconfirmed.

**Sequencing note.** Supplementary licences require a valid general licence first, so
nothing about marketing is actionable before the general licence exists.

**Nuance worth recording:** the obligation is to **apply** within the grace period,
not to be approved within it.

### Context, recorded honestly

The substantive obligations — consent for marketing, kept separate, unticked,
withdrawable — are ordinary and match international practice. The **prior
authorisation regime is not** international practice; Europe operates on comply-and-
document with inspection afterwards, having abandoned general notification
requirements as paperwork that did not improve protection.

The permission layer is an Egyptian addition. At the current record volume the fees
are nil, so the cost is time rather than money.


---

## D-065 — Break-glass made functional

**Date:** 2026-08-27 · **Status:** accepted · **Amends:** D-010, D-028, D-029, D-048 · **Resolves:** F-01

**TL;DR.** The break-glass credential could not perform a single action it existed
for. Four defects, all from decisions that were individually reasonable and never
traced together.

**What was broken.**

1. **No endpoint.** Nothing in the API accepts the credential. Authentication is
   cookie-based throughout; a printed secret had nowhere to go.
2. **The session could do nothing.** Administrative actions require step-up, and the
   only step-up rule shape defined is "phishing-resistant, verified within N
   minutes." A printed secret is not phishing-resistant, so the session could not
   approve a recovery, grant `system:administer`, change alert destinations, or
   loosen any setting — every action the owner would need.
3. **No regeneration.** Only the bootstrap CLI generates one; it runs on the server
   and refuses while a system administrator exists. The owner has no server access
   (D-047). One use exhausted the mechanism until the operator returned.
4. **"Time-boxed" had no value**, no configuration key, no ceiling.

**Decision.**

**Registered as a factor** with declared properties (D-012), including an explicit
property that **satisfies the administrative organization's step-up requirement for
the duration of the session**. This is a deliberate hole in the strongest control in
the design — which is what an emergency credential is. The maximum-noise alerting is
therefore not a nicety; it is the only thing distinguishing emergency access from
silent total access.

**Its own endpoint** — pre-session, source-throttled, outside the session-bound CSRF
layer (see F-06's machine-endpoint profile).

**Grants the `system-administrator` role** for the session, resolving the
role-versus-permission ambiguity in F-21.

**Self-regenerating.** A break-glass session can generate a **new sealed credential**,
so the owner can leave a fresh one behind before the session expires.

*Rejected: regeneration only by a normal administrator* — one emergency would exhaust
the mechanism until the operator returned, which is the scenario it exists for.
*Rejected: two credentials sealed separately* — the same as self-regeneration with
more envelopes.

**Accepted consequence:** a break-glass holder can mint unlimited future credentials.
They already hold full administrative access at that moment, so this grants nothing
new. Alerting and the audit trail are the controls.

**Lifetime is configurable** with an enforced ceiling.

**Alerting extended — added beyond the review's suggestion.** Break-glass use alerts
the **owner** as well as the operator. The scenario it exists for is one where the
operator is unreachable, so alerting only the operator means the noise reaches nobody
present.


---

## D-066 — Consent gates purposes, not records

> **Superseded in part.** The host business rules and vendor facts this entry records (orders, payments, shipments, couriers, addresses) moved to the host under D-165; the reasoning stands as the host's inheritance.

**Date:** 2026-08-27 · **Status:** accepted · **Amends:** D-024, D-030, D-064 · **Resolves:** F-02

**TL;DR.** An order exists for three legal reasons at once and a customer can withdraw
one of them instantly. Nothing said whether the other two keep working. Consent is now
scoped to **purposes**, not to whole records.

**The contradiction.** PRIV-SENS-002 refuses processing of a sensitive type without
recorded consent. PRIV-CONS-008a says withdrawing consent leaves other lawful bases
unaffected. For an order row carrying contract, legal obligation and consent
simultaneously, both cannot hold. An implementer guessing strictly would refuse a
courier callback mid-delivery; guessing loosely would make the consent gate
decorative.

**The worse interaction.** PRIV-CONS-007 refuses processing under a superseded consent
version, and PRIV-CONS-006 versions the notice as a unit. Publishing a revised notice
— a text edit — would therefore mark **every existing customer's order history
superseded and refused** until each re-consented. A storefront-wide outage caused by
editing a paragraph.

**Decision — purposes carry bases, records do not.**

| Purpose | Basis | Withdrawable |
|---|---|---|
| Fulfilment and delivery | Contractual obligation | No |
| Tax and financial retention | Legal obligation | No |
| **Personalised recommendations** | **Consent** | **Yes** |

Withdrawal removes only the consent-based purposes. Delivery continues, records are
retained, and what stops is discretionary use of the health-implying pattern.

**Re-versioning prompts, never blocks.** A superseded notice produces a re-consent
request at next interaction. Processing under contract and legal obligation is
unaffected.

**Retention is defined per (type, basis)** — the longest legal basis governs storage;
consent governs use. This resolves the previously undefined conflict between
"stricter defaults for sensitive data" and "long retention for financial records" on
the same row.

### Scope established

**Anonymous analytics is out of scope entirely.** The user confirms analytics are
aggregate, for product and service improvement, with no interest in individual
identity. Genuinely non-identifying aggregate analysis is not personal data — no
consent, no lawful basis, no records entry.

**Guard rail, because "anonymous" does heavy lifting:** analysis outputs must never be
granular enough to single out an individual — a single customer in a district buying a
specific product is identifiable without a name. The raw order data feeding the
analysis remains under the order's own basis.

**Recommendations are personal and require consent.** A recommendation derived from
what a specific person bought is aimed at that individual. On-premises processing does
not change this — the question is who is being profiled, not where the computation
runs.

**Product note, recorded not specified:** a recommendation derived from a medical
purchase is visible, and on a shared device it discloses the condition to whoever is
looking. Worth conservative placement; a product decision rather than a requirement.

**Rejected.**
- *Record-level consent gating* — the original design. Cannot coexist with multi-basis
  records and produces either stalled fulfilment or a decorative gate.
- *Blocking on superseded consent* — turns a text edit into an outage.


---

## D-067 — Step-up has two rule shapes; a default policy exists for non-members


> **Superseded in part.** "Strongest available" was reversed by D-086 §86.5, restored by D-128, and replaced by level-declared gates in D-141.
**Date:** 2026-08-27 · **Status:** accepted · **Amends:** D-020.1, D-020.2, D-035, D-045 · **Resolves:** F-03

**TL;DR.** Only one way to satisfy step-up was defined — present a passkey. Customers
without one therefore had **no possible input** at a step-up prompt, so they could
never change their email or phone. Fixed with a second rule shape that scales to
whatever factors a person actually holds.

**What was broken.** AUTH-STEP-002 defined a single rule shape: *phishing-resistant,
verified within N minutes*. Email factors are barred from satisfying step-up
(AUTH-FACT-003) and re-presenting a password raises no assurance, so a storefront
customer signing in by password, magic link, Google or Apple had nothing to present.
IDN-LIFE-005 and IDN-LIFE-010 require step-up with no exception, so email and phone
change were **permanently unreachable** for the typical customer.

**Underneath it, a modelling gap.** Policy resolves "from the organization owning the
resource or action" (AUTH-PRIN-002); every resource type must have a path to an
organization (AUTHZ-MODEL-004); customers hold no membership (IDN-MEM-002). Read
literally, customers inherited the administrative organization's passkeys-only policy
— which is how this happened.

**Decision — two rule shapes**, selected per action by organization policy:

| Shape | Satisfied by |
|---|---|
| **Reauthenticate** | The subject's **strongest available factor**, presented within N minutes |
| **Phishing-resistant** | A phishing-resistant factor within N minutes — unchanged |

**Strongest available, not lowest.** A subject with a second factor presents that
factor; a subject with only a password re-presents the password. Customers who
enrolled MFA get the protection they enrolled it for rather than being asked for a
password like everyone else.

*The reviewer's suggestion, improved by the user.* The initial proposal offered "any
permitted primary factor," which would have asked an MFA-enrolled customer for a
password — weaker than what they had chosen.

**A default policy for principals with no membership**, distinct from the
administrative organization's, so customers no longer inherit staff rules by accident.

**Email and phone change use the reauthenticate shape for customers.** The existing
protections in D-035 remain and are the substantive defence: both old and new
addresses confirm, the old address is notified regardless, a cooling-off window allows
reversal, and all other sessions terminate.

**Administrative-organization members are unchanged** — phishing-resistant, no
substitutes.

**Subject access export requires no step-up.** `exfiltration.export.*` (D-045) is
scoped explicitly to **staff bulk export** and does not apply to `GET
/privacy/export`. Gating a legal right behind a second factor locks out precisely the
people least able to work around it.

**Rejected.**
- *Requiring a second factor for customer email change* — a customer whose old mailbox
  is lost or compromised would have no route to move their account.
- *Password-only reauthentication for everyone* — ignores MFA a customer chose to
  enrol.


---

## D-068 — Erasure reaches host data; per-subject encryption replaces pseudonymisation

> **Superseded in part.** The host business rules and vendor facts this entry records (orders, payments, shipments, couriers, addresses) moved to the host under D-165; the reasoning stands as the host's inheritance.

**Date:** 2026-08-27 · **Status:** accepted · **Amends:** D-026.1, D-037 · **Resolves:** F-04

**TL;DR.** Erasure, restriction and export were routed "through the access gate,"
which cannot answer *which records are about a person* and is forbidden from reading
host tables. So erasure reached the account row and left the customer's name, phone
and address on every order. Fixed by the application owning its own redaction, and by
per-subject encryption so erasure also reaches backups.

### 68.1 — The library announces; the application acts

The library owns accounts, credentials, sessions and permissions. Orders live in the
host application's tables, which the library never reads or writes (LIB-HOST-002) —
that rule is what keeps it reusable.

**The library raises lifecycle events; the host application performs the work in its
own tables and reports completion.**

| Event | The application does |
|---|---|
| `ErasureRequested` | Redacts personal fields wherever it holds them |
| `RestrictionChanged` | Stops acting on that subject's records |
| `ExportRequested` | Returns everything it holds about the subject |

**Each application maintains one curated redaction routine** — a stored procedure or
equivalent — updated whenever a new field touching personal data is added, so erasure
is one call rather than a hunt through the schema.

**Requirements that make it reliable rather than aspirational:**
- The application **confirms completion**; the request stays open until it does
- Unanswered requests **retry**, so a failed handler is not a silently half-erased
  person
- A resource type declared sensitive **must have a handler registered**, checked at
  startup

### 68.2 — Erasure must anonymise, not pseudonymise

**The specification's wording was wrong and would fail a regulator's test.** D-026.1
and PRIV-RIGHT-005 said erasure "pseudonymises rather than deletes."

Pseudonymised data remains personal data. Controls preventing further processing,
combined with pseudonymisation, are **not sufficient** to satisfy an erasure request.
Anonymised data is outside scope, so the request no longer applies to it.

This is a known and widespread failure: a 2025 coordinated enforcement review of 764
controllers found many anonymisation techniques used as a substitute for deletion
were weak and amounted to mere pseudonymisation.

**The test is singling out, not name removal.** After redaction, nothing remaining may
permit identifying the person. **The delivery address and phone on order rows must go
as well as the name** — a single order to a specific apartment identifies someone with
no name attached.

**What is retained:** what was ordered, when, how much, which district. Aggregate
totals and product analysis are unaffected, which was the user's requirement — nothing
of value is deleted, only the "who."

### 68.3 — Per-subject encryption, so erasure reaches backups

**A gap neither the review nor the specification had identified.**

Backups are immutable by design (DR-006). An erasure therefore **cannot reach them** —
the person's data remains in every archived copy, unerasable. The same enforcement
review found half of responding authorities reported controllers had no erasure
procedure for backups at all.

**Decision: personal fields are encrypted under a key unique to each subject, wrapped
by the key-encryption key. Erasure destroys the subject's key.**

The data becomes unreadable everywhere it exists — live tables, backups, archives —
without altering a single backup file. Immutability and the right to erasure stop
being in conflict.

This also gives PRIV-SENS-002's encryption-at-rest requirement an actual mechanism
(see F-14), and the restore procedure must now cover subject keys as well as the KEK
(see F-05).

### 68.4 — Restriction is a freeze, not a hiding

**Clarified after the user correctly objected** to an earlier phrasing of "staff
screens exclude their orders," which would have removed data from analytical totals.

Restriction is **temporary and suspends action, not visibility.** The records remain
visible and continue to count in aggregates; what stops is acting on them — shipping,
refunding, contacting the person — until the dispute resolves.

Neither erasure nor restriction removes anything from historical or analytical
records.

**Rejected.**
- *Library reaches into host tables via the ORM* — breaks the rule that keeps the
  library reusable, and cannot know host-specific obligations such as retaining a
  delivery address for a tax period.
- *Pseudonymisation as the erasure mechanism* — insufficient in law, and specifically
  identified in enforcement as the common failure.


---

## D-069 — Key material in disaster recovery; KEK escrow with break-glass

**Date:** 2026-08-27 · **Status:** accepted · **Amends:** D-044, D-047, D-026.3 · **Resolves:** F-05

**TL;DR.** The restore procedure brought back the database and not the key that makes
it readable. A restored system would hold unusable TOTP secrets, dead provider
credentials, and — since D-068 — unreadable customer fields.

**What was missing.**

- **Where the key-encryption key lives** was never written down. The specification
  said only "the environment shall provide a secrets manager." In practice it is
  1Password (D-047), which appeared in conversation and in no document
- **Who can reach it in an emergency.** In the scenario that matters — operator
  unavailable, database corrupted — the owner holds break-glass and no vault access.
  They could enter the system and not restore it
- **Whether backups are encrypted** was stated nowhere, though they will hold health
  data and will move to third-party storage at the tier upgrade, and the records of
  processing will claim encryption
- **What else a cold restore needs:** the mail server's database, certificate
  material, deployment credentials. The procedure covered PostgreSQL and stopped

**The shape of the defect:** the backups themselves are carefully specified —
point-in-time recovery, immutability, quarterly timed restores — and the single thing
that makes a restored backup *usable* was absent. Protected twice, the adjacent thing
unprotected.

**Decision.**

**The key-encryption key is escrowed with the break-glass credential**, in the same
sealed envelope held by the owner, under the same annual reseal.

*Rejected: the owner holds standing vault access* — a second person with permanent
access to every credential, to solve an emergency that is rare and bounded. Far larger
exposure for the same outcome. *Rejected: split-key custody* — protects against owner
compromise, which is not a threat in this model.

The mechanism exists for the case where the operator is unreachable, so it cannot
depend on the operator being reachable. Same reasoning as D-065.

**Backups are encrypted under a key not held on the server**, so losing the host does
not lose both.

**The restore procedure enumerates every artefact a cold rebuild needs** — database,
KEK, per-subject keys (D-068), the mail server's own database, certificates,
deployment credentials.

**The quarterly restore test decrypts something and proves it.** A restore that brings
back rows and not keys looks successful and is not.

**Compounding accepted risks, recorded.** R-A01 (same-host backups) + R-A04 (4–8h
recovery) + R-O02 (one operator) + this defect meant that during Phase 1, host loss
while the operator was unreachable was total, unrecoverable, and un-notifiable within
72 hours. Each acceptance was reasonable alone. The escrow removes the un-recoverable
part; the rest stands as recorded.


---

## D-070 — Two pipeline profiles; webhook verification raised to standard

> **Superseded in part.** The host business rules and vendor facts this entry records (orders, payments, shipments, couriers, addresses) moved to the host under D-165; the reasoning stands as the host's inheritance.

**Date:** 2026-08-27 · **Status:** accepted · **Amends:** D-052, D-053, D-013, D-030 · **Resolves:** F-06

**TL;DR.** "No endpoint can skip CSRF" was written for browsers and applied to
everything. Four integrations are servers, not browsers — built literally, mail
authentication and every provider callback fail on day one. Fixed with a second
pipeline profile. Researching it also showed the callback protection specified was
**below industry standard**.

### 70.1 — Two profiles, not an exception

CSRF protection works by requiring what only a browser on our own site can supply — a
token, a custom header, a Fetch Metadata signal. Stalwart, the payment provider, the
shipping provider and the SMS gateway supply none of them.

| Profile | Traffic | Authentication |
|---|---|---|
| **Browser** | The four applications | Session cookie, synchronizer token, custom header, Fetch Metadata, Origin — unchanged |
| **Machine** | Token endpoint, provider callbacks | Client secret, or signature verification — no session, no CSRF |

**The property being protected is preserved.** An endpoint's protection comes from
**where it is mounted**, not a flag on it. A developer still cannot accidentally opt a
browser endpoint out; they would have to deliberately mount it elsewhere.

Excluding webhook routes from CSRF is the standard practice, not a workaround —
providers do not send CSRF tokens.

### 70.2 — Callback verification was below standard

**The specification's control was "unguessable reference plus rate limiting."** The
accepted baseline is **signature verification**, described in the field as the single
most important measure for any webhook integration, and not treated as optional.

**Where a provider supports signing, the full standard applies:** HMAC-SHA256,
constant-time comparison, a timestamp window of five minutes or less, verification
against **raw bytes** before any parsing, and idempotency keyed on the event
identifier.

**Where it is not available, compensating controls and an honest record.** Checked
against the providers' own documentation: the shipping provider accepts a callback URL
and offers no signing secret or signature header; the SMS gateway calls over plain
HTTP with parameters in the query string, which cannot carry a signature meaningfully.

For those:
- Unguessable correlation references, rate limiting, source restriction where the
  provider publishes ranges
- **The callback is a hint that triggers verification against the provider's API,
  never a fact.** A parcel is not marked delivered because something said so — the
  provider is asked. This was already in the specification as a secondary measure and
  is now stated as **the primary control** for unsigned callbacks

**Recorded as a risk:** two providers deliver unsigned callbacks. Mitigated by
verification-on-receipt. Revisit if either adds signing — worth asking both, since it
may be undocumented rather than absent.

### 70.3 — Two things the analysis surfaced

**The SMS callback arrives over plain HTTP**, while INF-TLS-001 requires no origin to
serve plaintext. One must give: either the gateway supports an encrypted callback URL,
or a named exception is recorded for that single path.

**That callback is a GET.** CSRF protection ignores GET by design, so it never
protected it. Stating the reference and verification-on-receipt as the actual controls
prevents someone later removing them believing CSRF covers it.


---

## D-071 — Protected settings are a rule, not a mechanism; alerting written as requirements

> **Amended.** The protected switches `audit.enabled`, `token.signature.verification` and `stepup.enforcement.<organization>` are retired (D-166).

**Date:** 2026-08-27 · **Status:** accepted · **Amends:** D-010, D-045, D-048, D-051 · **Resolves:** F-07

**TL;DR.** Three decisions produced configuration keys and no requirements — an
engineer would have built settings with nothing behind them, and the risk register
claimed three mitigations that did not exist. Separately, "redeploy-only" was a
mechanism where a rule was wanted.

### 71.1 — The rule, not the mechanism

**"Redeploy-only" is replaced by "not changeable through the application."**

The user's question was why a restart, or a command-line operation on the server,
would not serve as well. It does. An attacker holding an administrator's credentials
has no server access, and anyone who does can already do worse — so a CLI restricted
to the host gives the same protection as a redeployment with far less friction.

**Whatever mechanism is used must record the change and alert.** Configuration change
to a protected setting is a recognised alert class in the field — *audit trail
interference*, alongside disabled logging and retention edits.

Terminology updated across nine documents; the error code becomes
`config.key.protected`.

### 71.2 — Where settings live, previously unstated

Runtime settings live in the library's own schema, changed through the management
application, audited as grants are. **Two exceptions for bootstrapping rather than
security:** the database connection and the key-encryption key are needed before a
settings table can be read.

### 71.3 — Alerting written as requirements

D-048, D-045 and D-051 existed only in this log. OPS-ALERT-001 to 007 now specify:
twelve conditions, deduplication, channel routing with both exemptions, destinations
as **lists** with owner notification **off by default and configurable**, per-actor
baselining, export definition and gating, and implausible-session detection.

**Four conditions added** that other requirements referenced with nothing to consume
them: background job failure, degradation signals, configuration change to a
protected setting, and repeated callback verification failure.

**Destinations as lists.** With a single destination, every alert during the
operator's absence reaches someone unreachable. Owner notification defaults off —
the operator is reachable in the normal case — and is enabled before a planned
absence.

### 71.4 — Confirmed against practice

**Out-of-band alert routing is the standard rule**, and for exactly our reason: an
alert about the mail system must not require the mail system. Our SMS-first rule for
mail failures matches it.

**Break-glass guidance** confirms our approach: sealed tamper-evident storage,
alerting routed out-of-band, credential rotation after use, post-event review. One
addition worth recording — **any break-glass use not preceded by a documented
emergency should be treated as a breach investigation**, not merely audited.

**Separation of duties is unattainable here and that is the honest position.** The
principle is that whoever can change a system should not be able to alter the record
of the change. With one operator holding both, it cannot hold. Recorded rather than
papered over.

**Considered and not adopted:** hash-chained audit records, where each entry includes
the previous entry's hash so tampering breaks the chain detectably. Append-only
enforced by the database can be undone by whoever administers the database — which is
the operator. Hash chaining would make that detectable. Deferred as
disproportionate now; recorded because it is the control that addresses the
separation-of-duties gap above.


---

## D-072 — Automated certificate issuance is a launch requirement

**Date:** 2026-08-27 · **Status:** accepted · **Supersedes:** D-063.3 · **Resolves:** F-08

**TL;DR.** The infrastructure document required renewal with no human step; the
decision log called automated issuance "a trigger, not a mandate." By our own
conformance rule the log wins, so the requirement was void and manual renewal was a
third unlisted recurring task resting on one person.

**Resolved in favour of the requirement.** Automated issuance from launch. This is not
a new choice — it was decided when Let's Encrypt was chosen and never propagated into
either document, which is why they contradicted each other.

**The interim path is deleted rather than specified.** Nothing is live; the system is
in development. There is no migration from the existing manual certificate, no interim
period, and no manual renewal procedure to document.

### Ownership — where this belongs

The user asked whether the mechanism belongs to these systems, being security-related.
**It does not**, and the test that has held throughout decides it: *would this exist if
these three systems did not?* Certificates protect every request to every application
— product pages as much as sign-in — and would be required with authentication deleted
entirely. They are a property of the deployment.

**But this system has a dependency nothing else does.** The passkey relying party
identifier is bound to a domain and **must match the certificate's coverage**. A
mismatch causes passkeys to stop working on an application, silently.

**So the split:**

| Ours to require | Not ours |
|---|---|
| Every origin serving these endpoints presents a valid certificate | Which authority |
| Coverage matches the domain the relying party identifier is set to | Which client or validation method |
| Renewal requires no human step | How it is wired |
| Expiry monitored **independently** of whatever renews | Where it runs |
| Failure alerts through OPS-ALERT-001 | |

**Where the mechanism touches the pipeline**, this system states what must be true —
certificates valid before deploy, failure blocks the deploy, alerts route here — and
does **not** dictate implementation. The same shape as the migration requirements,
which say "applied before deployment" without saying how.

**Maintenance exceptions remain two:** break-glass reseal and licence renewal.
Certificates do not join them.


---

## D-073 — Guest checkout removed; every order belongs to an account

> **Superseded in part.** The host business rules and vendor facts this entry records (orders, payments, shipments, couriers, addresses) moved to the host under D-165; the reasoning stands as the host's inheritance.

**Date:** 2026-08-27 · **Status:** accepted · **Amends:** D-027, D-039 · **Resolves:** F-09

**TL;DR.** A guest order produced health data about an identifiable person attached to
nothing — no consent record, no dashboard, no erasure path, and a takedown operation
with no account to act on. Guest checkout is removed.

**What was broken.** Every privacy mechanism hangs off an account: consent records
point at one, the dashboard is an account page, erasure targets one, the takedown
suspends one. A guest has none. The specification stated explicitly that guest
checkout is processing and that the children's rules apply to it, then provided no
machinery to act on either.

**Decision: every order requires an account.**

**The cost is one screen, because phone verification is already mandatory**
(IDN-LIFE-001). A "guest" was already surrendering a phone number, receiving a code
and entering it — which is the entire cost of registration. Guest checkout normally
exists to remove friction; here it removed almost none, since the friction *is* the
phone verification.

**And the customer gains** an order history and a privacy dashboard they would
otherwise not have.

**Rejected: a subject record for guests** — a credential-less identity that consent,
audit and rights requests could attach to, with rights exercised by phone plus order
reference. It works, and it costs a parallel identity concept every privacy feature
must handle twice: two paths for consent, erasure, export, restriction and takedown. A
permanent tax on a feature that saved one screen.

**The deciding case.** The takedown procedure (D-039) exists because a minor may slip
past the age affirmation. A guest minor was the worst case — nothing to suspend,
nothing to look up, and their health data resident with no route to remove it.

**Consequential amendments.**
- The 18+ affirmation is required at **registration only** — there is no second
  collection path (D-027 previously required it at guest checkout as well)
- The takedown procedure always has an account to act on
- Breach scoping by data subject (PRIV-BREACH-002) has no accountless gap


---

## D-074 — Mailbox provisioning scoped to staff; hosting separated from delivery

**Date:** 2026-08-27 · **Status:** accepted · **Amends:** D-006, D-022 · **Resolves:** F-10

**TL;DR.** INT-MAIL-006 said "creating an account provisions the mailbox" with no
qualifier — so read literally, every customer received a mailbox on the company's mail
server and registration blocked whenever that server was unavailable. Never intended,
never stated by the user, and written that way anyway.

**This is the dangerous class of defect:** a requirement that is *almost* right reads
as fine in review and builds the wrong thing.

**Scope corrected.** Mailbox provisioning applies to **administrative-organization
members only**. The mail server hosts the company's own mail. Customers hold their own
email elsewhere and only receive messages from us.

**Hosting and delivery separated.** The specification treated them as one thing under
"the mail server." They fail independently and affect different populations:

| Concern | Scope | Failure means |
|---|---|---|
| Mailbox hosting | Staff only | Staff cannot read company mail |
| Outbound delivery | Everyone | Verification, recovery, magic links, notifications stop |

**The runbook was wrong** and is corrected. It claimed mail failure "does not affect
authentication." True of an existing session; false for the storefront, where magic
link and email OTP are **primary** sign-in factors, and for verification, recovery and
email change.

**One silent consequence now documented:** a delayed MFA removal is held if no warning
was delivered (AUTH-RECOV-007, deliberate). An outbound mail outage therefore holds
every pending removal with no visible failure. Pending removals are checked after any
mail incident.


---

## D-075 — Audit immutability, retention and erasure reconciled

**Date:** 2026-08-27 · **Status:** accepted · **Amends:** D-026.1 · **Resolves:** F-11

**TL;DR.** Three requirements could not all be built. Audit records were append-only
with deletes refused by the database; routine audit had short retention, which
requires deleting; and erasure was to remove personal attributes from audit records,
which requires updating. An implementer would have granted delete rights — breaking
immutability — or never purged — breaking retention.

**Resolved by three clarifications.**

**Audit rows hold identifiers and codes, never personal attributes.** That is what
makes append-only compatible with erasure: there is nothing personal in the row to
redact. The acceptance criteria that spoke of "removing personal attributes within
audit records" were describing something that should not exist.

**Where an event must record an attribute** — old and new address on an email change,
origin address on a sign-in — it goes in a **per-subject-encrypted column**. Erasure
destroys the key (D-068), so the row is untouched and the attribute is unreadable.
Immutability and erasure stop competing.

**Retention purges by partition, not by row.** Audit tables are partitioned by period;
expired partitions are dropped by a scheduled job under the migration credential, and
the drop is audited. The application cannot delete a row; the pipeline can drop a
partition.

**Immutability is scoped to the application**, which is the honest statement. A
database-level guarantee is unenforceable against whoever administers the database —
the operator. That gap is recorded under D-071, along with hash chaining as the
control that would address it.


---

## D-076 — Factor discovery and duplicate handling no longer disclose existence

**Date:** 2026-08-27 · **Status:** accepted · **Amends:** D-013, D-040 · **Resolves:** F-12

**TL;DR.** Two enumeration leaks that contradicted the enumeration-resistance
requirements elsewhere. A test suite built from the specifications would have
contradicted itself.

**Leak one — the factor list.** `/auth/begin` returned the factors available *for that
account*. That cannot be byte-identical to a response for a non-existent identifier.
Either the list is constant, in which case it says nothing, or it discloses both that
the account exists and **what it is protected by** — "this account has no second
factor" — before any credential is presented. That is precisely the targeting
information the threat model's §3.2 attacker wants.

**Corrected:** the list is the **policy's enabled primary set**, identical for every
identifier. A WebAuthn challenge is always returned, since discoverable credentials
need no allow-list and issuing one reveals nothing. **Second-factor requirements
surface only after a first factor succeeds.**

**Leak two — duplicate registration.** IDN-ACCT-004 and IDN-ACCT-006 required a
duplicate to be "rejected as a duplicate," while registration returns an accepted
response regardless of existence. Both cannot hold.

**Corrected:** a duplicate creates **no second account** and returns the ordinary
accepted response. The error code `identity.identifier.duplicate` is **withdrawn** —
returning it discloses existence. The real owner learns via the non-existence
notification path already specified.

**Also closed:** changing an email address to one already registered was undefined. It
returns the ordinary accepted response with a notification to that address. An
authenticated duplicate check is still an oracle — it merely costs an account to
operate.


---

## D-077 — Per-account session revocation; state changes end sessions

**Date:** 2026-08-27 · **Status:** accepted · **Amends:** D-050, D-039 · **Resolves:** F-13

**TL;DR.** The only revocation operation was system-wide, and the offboarding
checklist's first step called it — so removing one departing clerk would have signed
out every customer. Separately, nothing said that suspending or deactivating an
account ends its sessions.

**Two defects.**

**No per-account revocation existed.** `POST /admin/sessions/revoke-all` and the
`session:revoke` permission were both system-wide. The offboarding procedure said
"revoke all sessions" as step one, meaning the routine departure of a staff member
would take down every active session in the business.

**State changes did not end sessions.** Suspension, deactivation and the transition to
`deleting` said nothing about sessions or tokens. A suspended account kept a live
session for up to its absolute lifetime — thirty days at AAL1. The minor takedown
procedure's claim that "access stops immediately" was therefore false, and it is the
case where it matters most.

**Corrected.** A per-account revocation operation and permission; and any transition
out of `active`, plus deactivation, terminates that account's sessions and derived
tokens in the same operation.

**Offboarding now revokes that person's sessions**, not everyone's.


---

## D-078 — Encryption threats named; capabilities carry their requirements

**Date:** 2026-08-27 · **Status:** accepted · **Amends:** D-030, D-015 · **Resolves:** F-14, F-15

### 78.1 — Encryption at rest was a flag with no mechanism

The library cannot encrypt columns in tables it does not write, and **disk encryption
protects nothing the specification was worried about** — it defends a stolen disk and
does nothing against a dump or a backup file, which is the threat the specification
itself cites for authenticator secrets. As written, a host satisfied the requirement
by setting a flag, and the records of processing would claim protection that was not
there.

**The threat is now named per control:**

| Threat | Control |
|---|---|
| Stolen disk | Full-disk encryption (infrastructure) |
| Database dump | Per-subject encryption of personal fields (D-068) |
| Backup file | Backup encryption under an off-host key (D-069) |

**Which fields are encrypted is declared, not universal** — encrypting a district
would break the address flow. Personal fields are encrypted; the transaction record is
not.

D-068's per-subject keys supplied the mechanism this requirement had been missing.

### 78.2 — "Capabilities always match what the action permits" was unachievable

An action can be refused for step-up, a downgraded session, the subject's processing
restriction, missing or superseded consent, or account state — **none evaluated by the
per-row grant query**. Two requirements pointed in opposite directions: the frontend
must not show a control that will predictably fail, and the BFF rejects with a step-up
code expecting the frontend to retry.

**A capability now means "permitted by grants, subject to session gates,"** and each
carries a `requires` list naming what remains — step-up, consent.

**Not permitted → hidden. Permitted but gated → shown, and prompts.** A gated action
is not a failure; it is a prompt. That resolves the contradiction rather than picking
a side, and it stops the frontend hiding controls a person is entitled to use.


---

## D-079 — Cache holds inputs, not outcomes

**Date:** 2026-08-27 · **Status:** accepted · **Amends:** D-007 · **Resolves:** F-16

**TL;DR.** The specification said "resolved permissions" are cached under a
per-account counter bumped on grant or membership change. If that means cached
*outcomes*, four common events leave stale access behind — each a silent grant of
permission that no longer exists.

**The four:** editing a role's action set, moving a resource in the hierarchy, a grant
expiring, and restricting an account. None bumps any account's counter, so a cached
"allow" survives all of them.

**Resolved: the cache holds inputs.** The principal's grant rows and transitive group
set — the expensive lookups. Role definitions, ancestry, expiry and account state are
evaluated live on every check.

The counter then only needs to track what it already tracks, and the four events need
no invalidation at all because nothing about them was ever cached.

The decision log had implied this reading; the specification stated the other. Now
explicit, with acceptance criteria for each of the four events.


---

## D-079a — Low-severity sweep: F-17, F-18, F-19, F-20, F-21, F-22

> **Amended.** A recognised device is exempt from the hold, not from the count (D-166).

**Date:** 2026-08-27 · **Status:** accepted

Six findings, each a single defect. Grouped because none warranted its own decision.

**F-17 — Self-service explanation was an existence oracle.** For a concealed resource
type, "no grant matched" discloses that the record exists, defeating the
timing-identical not-found. Self-service explanation is now limited to non-concealed
types; concealed denials resolve only for a support role, via the correlation
identifier.

**F-18 — Self-approval of recovery was claimed but never required.** The threat model
said it was blocked. No requirement said so. Moot with one approver, false the day a
second is configured — precisely when nobody would think to check. Now enforced in the
domain with a named error.

**F-19 — Staff onboarding had no path.** The only account-creation route was public
registration, so a staff member would register as a customer — storefront client
identifier, marketing consent screen, password — and then be granted membership, with
nothing saying when organization policy applies to the password they already hold.

**Resolved:** an invitation flow issuing the same time-boxed enrolment link as
recovery, and on gaining membership, any factor the organization's policy does not
permit is disabled for interactive use.

**F-20 — Per-account throttling was a milder form of the lockout it replaced.** An
attacker knowing an address drives the delay up from many sources and the victim
inherits it. The per-account component is now capped, and a source presenting a
recognised device or prior session is exempt.

**F-21 — `system:administer` gated nothing at runtime.** It governed the protected
settings, which no application interface can change, so its only live effect was
granting itself. Now defined as required for **loosening** changes and for granting
the seeded administrative role. The break-glass session holds the role, resolving the
ambiguity flagged in F-01.

**F-22 — A password could survive below its own floor.** Ten characters is permitted
with a second factor and not without one. Completing a self-service MFA removal left a
single-factor account below the single-factor floor. Removal now re-evaluates and
requires a change at next sign-in.


---

## D-079b — Consistency sweep: F-23 (fourteen items), F-24, F-25, F-26

> **Superseded in part.** The host business rules and vendor facts this entry records (orders, payments, shipments, couriers, addresses) moved to the host under D-165; the reasoning stands as the host's inheritance.

**Date:** 2026-08-27 · **Status:** accepted

Contradictions between documents, each of which would have produced a failing test or
an implementer's guess.

**Requirements that contradicted each other:**

- **"Exactly four things"** in the host declaration was already contradicted by four
  other requirements. The list is corrected rather than the number defended
- **Keys with no default** sat against secure-by-default with no stated behaviour.
  Startup now **fails** with a named error — these cannot be safely defaulted
- **"No configuration for deferred capabilities"** was contradicted by two existing
  keys. Corrected: a key is permitted where the capability is **built and disabled**;
  prohibited where nothing is built
- **"The library never renders user-facing text at all"** versus the library sending
  email and SMS that people read. The rule is about the **HTTP boundary**
- **`/auth/session` returned `organizations`** while two requirements forbade the
  session naming one. Removed
- **Public types only in `Janus.Core`** versus the hosting middleware being public
  contract. `Janus.Hosting` is public for the mounting API alone
- **System principals could not cross organizations**, yet reconciliation, retention
  and records generation are pool-wide. Two scopes now exist: organization-scoped, and
  deployment-scoped restricted to an enumerated set of operations
- **Registration carried one identifier** while phone is mandatory and email is the
  recovery channel. Both are required
- **MFA-removal request** had no defined authentication state — the person making it
  has lost their second factor and cannot hold a complete session. It accepts a
  first-factor session
- **The `deleting` state had no cancellation** while organization deletion did.
  Cancellable throughout its window
- **Configuration changes with no direction** — alert destinations, approver counts —
  were unclassified. Treated as **loosening**: removing a destination or reducing
  approvers weakens the system as surely as lengthening a timeout
- **A courier's district identifier sat in the identity model** — a vendor identifier
  in a core namespace, and domain data in a library that must know nothing of the
  domain. **Addresses are host data;** the library holds none
- **`SameSite=Lax` and a payment return by POST** would lose the session on return.
  The return must land on a GET route
- **Angular zoneless** became the default in v21, not v22. Corrected

**F-24 — the boundary dataset was unnamed**, making its acceptance criterion
untestable, and its licence may make the supplier a recipient in the records of
processing. The dataset must be named and recorded.

**F-25 — non-existence emails were an unsolicited-mail generator.** A distributed
attacker could make the mail server send "you have no account here" to arbitrary
addresses at scale, damaging deliverability of the security mail the design depends
on. Limits were per source and per identifier; neither constrained this. Now limited
per destination address, with the rate alerted.

**F-26 — the threat model overstated coverage.** "Recover the account via email —
covered" holds for MFA-enrolled accounts. MFA is advisory for customers, so for a
typical customer a mailbox compromise yields the order history directly. Corrected to
partial.


---

## D-080 — Provider signing capability confirmed

> **Superseded in part.** The host business rules and vendor facts this entry records (orders, payments, shipments, couriers, addresses) moved to the host under D-165; the reasoning stands as the host's inheritance.

**Date:** 2026-08-27 · **Status:** accepted · **Extends:** D-070

**TL;DR.** Checked rather than left as an open question. The payment provider signs;
the shipping provider does not.

| Provider | Signs | Detail |
|---|---|---|
| **Payment** | **Yes** | HMAC-SHA-512-family signature over the callback payload, signed with the merchant secret. A newer scheme carries `Signature`, `MerchantId` and `RequestTimestamp` in headers, signing timestamp concatenated with body — replay protection natively, and preferred where available. Publishes IP ranges; recommends confirming against the query API |
| **Shipping** | **No** | Collection searched exhaustively for signature headers, HMAC, signing or webhook secrets. `webhookUrl` is the only webhook field present |
| **SMS gateway** | **No** | Plain-HTTP GET with query parameters cannot carry a signature meaningfully |

**Consequences.**

Payment callbacks are **signature-verified** — the full standard applies, and the
highest-value callbacks are the ones that are verifiable.

Shipping and SMS callbacks rely on **verification-on-receipt** as the primary control,
as D-070 established.

**One operational requirement surfaced:** the payment provider treats a non-2xx
response as unacknowledged and **retries for 72 hours**. The endpoint must acknowledge
quickly, process asynchronously, and be idempotent — otherwise a slow handler produces
three days of duplicate deliveries.

**R-A10 narrows** from two unsigned providers to one, plus the SMS gateway which
cannot sign by construction.


---

## D-081 — Hosting location and Stalwart edition, confirmed

**Date:** 2026-08-27 · **Status:** accepted · **Closes:** the last two outstanding items

**Hosting: IONOS, outside Egypt.** Confirmed. Previously stated by the user and
repeatedly re-listed as open — an error on my part, not an unanswered question.

The cross-border permit requirement (D-023) therefore stands, the hosting-location
field in generated records reads *outside Egypt*, and the cross-border basis field is
required.

**Stalwart: Community edition.** Enterprise is out of budget and is not needed.

**Verified against the full documentation** — nothing D-006 depends on is gated:

| Depended on | Edition |
|---|---|
| OIDC authentication | Community |
| Directory backends | Community |
| App passwords with credential scoping | Community |
| JMAP management API and CLI | Community |

**Enterprise covers** multi-tenancy, administrative dashboards, AI models, telemetry
history and live tracing, its own alerting, and masked email addresses — all
operational conveniences we do not use.

**One consequence worth recording:** Stalwart's own alerting is Enterprise-only, so the
mail server will not report its own problems. Our reconciliation drift detection
(INT-MAIL-007) and the alert conditions in OPS-ALERT-001 carry that load, which D-074
already established.

**Note:** the API reports three edition values — `oss`, `community`, `enterprise` — so
"Community" is a distinct tier rather than simply the free build. Worth knowing which
is running; it does not change the integration.


---

## D-082 — Crypto-shredding via OpenBao Transit, replacing keys-in-database

**Date:** 2026-08-27 · **Status:** accepted · **Supersedes:** D-068's key storage · **Resolves:** H1

**TL;DR.** Per-subject keys move out of PostgreSQL and into OpenBao's Transit engine. A
database restore can no longer resurrect an erased person, because the key that
unwraps their data was never in the database.

### Why the previous design failed

D-068 stored per-subject keys in PostgreSQL, wrapped by the key-encryption key.
Point-in-time recovery is **cluster-wide** — PostgreSQL's own documentation states it
"can only support restoration of an entire database cluster, not a subset," so no
schema or database can be excluded from the WAL stream. Restoring to before an erasure
therefore restored the key alongside the data, and the master key still existed
because it is escrowed for exactly that purpose. **The erased person came back
readable — including during the quarterly restore test.**

Mitigations were considered and rejected: a separate PostgreSQL instance (invented,
not a recognised pattern, and creates a store whose loss is total); erasure replay
after restore (works, but leaves a window and depends on a list surviving); accepting
the lag and disclosing it (the field's common answer, but a mitigation rather than a
fix).

### The design

**Envelope encryption with datakey generation.** Per customer:

1. Request a data key from Transit against **that subject's key**. Transit returns it
   twice — plaintext for immediate use, and wrapped
2. Encrypt the subject's fields **locally** with the plaintext key. No network call per
   field
3. Store ciphertext and the **wrapped** key in PostgreSQL. Discard the plaintext
4. To read: send the wrapped key to Transit, receive the plaintext, decrypt locally

**One call per subject, not per field.**

**Erasure destroys the subject's Transit key.** HashiCorp state the property directly:
encrypted artifacts depend on the vault to decrypt their data keys, so if the Transit
key is destroyed the artifact becomes permanently unreadable.

**The Transit key is never in PostgreSQL.** A restore returns ciphertext and a wrapped
key, neither of any use. **The resurrection problem is eliminated rather than
mitigated.**

### Column-level, not row-level

Individual columns are marked encrypted. Name and phone on an order are encrypted;
product, quantity, date, total and district are not. Reports and aggregates are
unaffected because they never read encrypted columns.

### What erasure becomes

**Two steps, both known:** destroy the Transit key, and delete the fingerprint row.

**No redaction routine per application.** D-068's requirement that each application
maintain a curated procedure, updated whenever a personal field is added, is
**withdrawn** — the whole class of problem is gone.

**The fingerprint is the exception and it is inherent.** Email and phone need a
searchable one-way value for sign-in lookup and duplicate detection; anything
searchable cannot be encrypted. It is one known row in one known table, not a search
across the schema.

### Operational shape

| Concern | Resolution |
|---|---|
| Sealed at startup | An unsealer fetches shares from 1Password at boot. The community RFC describes this as "of equivalent security but much improved operational experience" for environments with a secure secret store and no KMS |
| New availability dependency | **None added.** The application already fetches the master key from 1Password at startup; if that is unreachable the system is down either way |
| Storage size | Small — Transit stores key material only; the caller stores the ciphertext. Snapshots are fast and cheap |
| Read cost | One unwrap per subject, and only when displaying personal fields. List views show dates and totals and touch nothing encrypted. Batch endpoints exist |
| Deployment | A container in the compose file, deployed by CI like any other service |

**Backup coupling — a hard rule from day one.** The database backup and the OpenBao
snapshot **always travel together, to the same destination, in one operation.** A
database backup without its matching snapshot is unreadable — worse than useless,
because it looks like a backup. This matters most at the storage upgrade: moving the
database backup off-host while leaving the snapshot behind would produce an off-site
backup that cannot be restored, undiscovered until needed.

**Phase 1 unchanged.** Both artifacts live on the VPS until block and object storage
are available. Losing the host remains total loss, exactly as R-A01 records. The
storage upgrade is a **destination change only** — no code, no redesign.

### Rejected

- *HashiCorp Vault* — BUSL since 2023, not open source, and harder to operate
- *Infisical* — no transit engine; a secrets store rather than a key service
- *Cloud KMS* — per-key cost at customer scale, plus an external cross-border
  dependency
- *Granit framework* — its crypto-shredding module is well designed and worth reading
  for the interceptor ordering and null-on-shredded behaviour, but it is a whole
  framework shipping auth, authorization, multi-tenancy and messaging, overlapping
  almost entirely with what this project builds. Also very new

### Unverified — test before committing

**OpenBao's behaviour with thousands of Transit keys is not documented.** Nothing
suggests a limit and each key is its own storage entry, but no statement confirms it at
that scale. **Test with a few thousand keys before building on it.** This is the one
assumption the design rests on.

**Transit keys require `deletion_allowed` to be set** before they can be destroyed —
a safety default. The erasure path must configure it deliberately.


---

## D-083 — Alert destination changes notify the previous destinations

**Date:** 2026-08-27 · **Status:** accepted · **Amends:** D-048, D-071, D-079b · **Resolves:** H2

**TL;DR.** One stepped-up administrative session could silently redirect every alert in
the system to the attacker, then act freely — with the person who would have noticed
unsubscribed.

**How it worked.** Destination changes were classified as "loosening" (D-079b) —
step-up, a written reason, an audit entry — and left runtime-changeable. Alerts fire
only on changes to **protected** settings, so a destination change raised nothing. No
analogue of IDN-LIFE-006 existed to tell the previous destinations. The attacker then
loosened session ceilings and thresholds freely, and every subsequent alert —
exfiltration, recovery clustering, break-glass use — arrived at their own address.

Everything remained audited. Audit is read **after** suspicion, and the person who
would have become suspicious had been unsubscribed.

**The test that should have caught it** is the one written into OPS-CFG-004:
*"does turning this off blind us to the person turning it off."* Redirecting alerts
does precisely that. It was applied correctly to audit logging and export auditing and
missed here — the same lever under a different name.

**Decision — both mitigations.**

**Every previous destination is notified**, non-suppressibly, before a destination
change takes effect. The pattern already used for email change.

**And "alert destination changed" becomes a High alert condition**, delivered to the
**previous** destinations.

**Removing the last destination of a channel is refused**, so the alerting cannot be
disabled by emptying it.

**Rejected: moving destinations to the protected list.** It would work, and D-071
already permits a server-side mechanism — but changing an alert address is a
legitimate routine action, such as adding the owner before a planned absence.
Requiring server access for that is friction disproportionate to the risk once
notification closes the hole. **Available as an upgrade** if the threat picture
changes.


---

## D-084 — Escrow custody confirmed; rotation is the control

**Date:** 2026-08-27 · **Status:** accepted · **Amends:** D-069 · **Resolves:** H3

**TL;DR.** The review found the escrowed key had none of break-glass's compensating
controls — it can be copied silently and never expires. Investigated properly: the
custody arrangement is sound and stays. The missing control was **rotation**, not
better packaging.

### What the review found, and what it got right

Break-glass is loud by construction: single-use, consumed on use, maximum-noise
alerting. The key-encryption key placed in the same envelope by D-069 has none of
that — it can be photographed and replaced without consuming anything, then used
offline against a stolen backup, with nothing detecting the use.

That analysis is correct. The conclusion drawn from it was not.

### What the guidance actually says

**Escrow is not discouraged.** NIST does not mandate key escrow in SP 800-57; the
decision is organizational.

**The tension is acknowledged as unresolvable.** Highly restricted key access reduces
breach exposure but increases the risk that authorized decryption fails during an
incident — backup key access for disaster recovery conflicts directly with minimising
the parties holding key material. There is no clean answer; an organization picks a
side and compensates.

**The control is the cryptoperiod.** NIST defines a key's cryptoperiod as the span
during which it is authorised for use, and exceeding it without rotation introduces
accumulated risk from exposure events that may not yet be detected.

**That names the real problem.** It was never that the safe is weak. It is that an
**undetected** copy stays valid indefinitely. Rotation bounds that regardless of how a
leak occurred.

### Custody, confirmed and recorded

The owner's arrangement, described by the user and accepted:

- Hardened steel safe, in a locked office, in a building with alarms and cameras
- Physical access would take hours and be noisy, and would trigger an alert
- The response to any such event is **immediate rotation**, before a copied key could
  be used

**This is a legitimate compensating control and stronger than tamper-evidence alone**,
because it does not depend on noticing at the next reseal.

**1Password custody is comparably strong**: account protected by email, secret key, a
security key carrying a static password, and a second security key for MFA. Not signed
in anywhere except the server's access token. Server access requires SSH keys held in
that account, and the provider's console access is disabled.

### Decision

**The key-encryption key stays in the envelope.** Proposals to remove it, split it
between trustees, or replace it with revocable credentials are rejected — splitting
requires three or more trustees to buy anything and there are two people, and removal
costs a recovery step for no gain given the custody above.

**Four controls, three of which are new:**

| Control | Cadence |
|---|---|
| **Rotation on a defined cryptoperiod** | Annually, with the existing break-glass reseal |
| **Rotation on any suspicion** — break-in, alarm, tamper evidence, any incident | On event |
| **Tamper-evident packaging**, inspected at reseal | Annually |
| **Escrow recovery tested** — the restore actually performed from escrowed material | Annually |

**Rotation is cheap under D-082.** The key-encryption key wraps other key material
rather than encrypting data, so rotating it re-wraps and never touches customer data.

**Recovery order, as the user specified:** 1Password first as the fast path; the sealed
envelope as the standalone fallback. **The envelope must therefore contain everything
needed to recover without 1Password**, since a lost or unavailable account takes its
contents with it.

### Envelope contents, recorded explicitly

| Item | Form | Purpose |
|---|---|---|
| Break-glass credential | Printed | One-time administrative session |
| Key-encryption key | Printed | Unwraps key material during restore |
| Deployment credentials | Printed | Redeploy to a new host |
| Key service unseal material | Printed | Lets the key service unseal on a rebuilt host |

All four also held in 1Password. The envelope is the fallback for when that is
unavailable.


---

## D-085 — Restore-during-absence stated honestly; mail store backed up

**Date:** 2026-08-27 · **Status:** accepted · **Amends:** D-044, D-006 · **Resolves:** M1

**TL;DR.** The objectives table claimed a restore during the operator's absence was
"possible." Three things made that untrue. One was already fixed, one is a genuine
backup gap, and one is a claim that overstated what a non-technical person can do.

### 85.1 — Deployment credentials: already resolved

The review flagged that deployment credentials sat only in the operator's vault, so
the owner could not redeploy to a new host. **D-084 placed them in the envelope**
before this finding was worked. No further change.

### 85.2 — The mail server's database had no backup

Continuous archiving covers the application database. **The mail server's separate
store had no backup requirement in any phase, in any document.** Staff mail history
would be lost permanently in every host-loss scenario, and nobody had accepted that in
writing.

**Decision: back it up.** It is another database on the same host, on the same
schedule, to the same destination as the application backup and the key service
snapshot. Cheap, and losing years of business correspondence is a materially worse
outcome than the effort of including it.

### 85.3 — The owner cannot perform the restore, and the table said he could

The procedure assumes someone who can provision a host, run a point-in-time PostgreSQL
restore, and interpret an ancestry integrity check. **The owner is not technical** —
the specification states elsewhere that the developer is the company's entire technical
function. The escrow handed him credentials for a task he cannot perform.

**Decision — phased, per the user:**

**Now: state it honestly.** The objectives table reads *"possible with outside
technical help, following `12-disaster-recovery.md`."* The envelope supplies the
credentials; a competent person uses them. That is true, and the current wording was
not.

**Later, once the system is released and stable: a scripted restore** the owner can
run himself, making the original claim true rather than merely honest. Recorded as
deferred with its trigger, because it is real work and the release comes first.

*Rejected for now: naming a contractor in advance.* Proposed as the middle option —
it removes the search for help during an emergency. Declined in favour of going
straight to the scripted path later.


---

## D-086 — Step-up policy separated from login policy; social sign-in resolved; assurance table added

> **Superseded in part.** The host business rules and vendor facts this entry records (orders, payments, shipments, couriers, addresses) moved to the host under D-165; the reasoning stands as the host's inheritance.


> **Superseded in part.** §86.5 "the person chooses" is reversed by D-128 (strongest class); §86.6 lifetimes are D-123/D-130; social is "no asserted AAL" per D-128.
**Date:** 2026-08-27 · **Status:** accepted · **Amends:** D-020.1, D-020.2, D-067, D-012 · **Resolves:** M2

**TL;DR.** Six related gaps, all in the same area. Login and step-up policy were
tangled together, social sign-in's treatment was never specified, no document assigned
assurance levels at all, and the data export had no friction of any kind.

### 86.1 — Login policy and step-up policy are separate

**They answer different questions and SHALL be configured independently.**

| Policy | Question |
|---|---|
| **Login** | What may this person sign in with? |
| **Step-up** | What proves it is still them, now? |

Previously both resolved through one organization policy, which is how the F-03
defect arose: customers inherited the administrative organization's passkeys-only rule
and were left with no way to satisfy step-up at all.

**Step-up policy works identically regardless of login policy.** A person who signed in
by any permitted means faces the same step-up rule.

### 86.2 — Social sign-in requires no second factor

**Decision: no.** Social sign-in is **delegated authentication** — we asked another
party to establish who this is. If the answer is yes, that is an absolute yes, and
requiring our own second factor afterwards second-guesses a decision we deliberately
outsourced. The person very likely has multi-factor enabled at the provider anyway.

This is a login-policy matter, and administrative-organization members are unaffected
— their policy does not permit social sign-in at all.

### 86.3 — Social sign-in is NOT a valid step-up method

**Not a judgement call — the mechanism to make it work does not exist in practice.**

Forcing a fresh authentication at a provider requires the `max_age` parameter, which
obliges the provider to re-authenticate and to return an `auth_time` claim the relying
party can verify. Without it, sending someone back to the provider bounces them
straight through on their existing session and returns an assertion demonstrating that
**nobody was present**.

**The providers do not honour it.** Google supports only `none`, `consent` and
`select_account` for the prompt parameter, not `login`. Oracle's documentation names
Google and Facebook explicitly as identity providers that have not implemented
`max_age` correctly.

**And `prompt=login` alone proves nothing.** It travels via a browser redirect, is
subject to tampering by the user agent, and has no spec-defined validation mechanism.
Published guidance is explicit that it must not be relied on as a security guarantee.

**Consequence:** authentication can be delegated; **presence cannot**. Step-up requires
a factor we verify ourselves.

### 86.4 — A factor property, not a list of names

`CanSatisfyStepUp` joins the factor properties (D-012), so no rule names a factor.

| Factor | Can satisfy step-up | Why not |
|---|---|---|
| Password, TOTP, passkey, security key, non-discoverable WebAuthn | **Yes** | |
| Google, Apple | No | Freshness cannot be forced or verified (86.3) |
| Email magic link, email OTP | No | D-009 — a mailbox compromise would yield both |
| SMS OTP | No | Verification-only |
| Recovery codes | No | Single-use and finite; spending one on routine step-up erodes recovery |

### 86.5 — How step-up behaves

**First, check what the session already satisfied.** If an eligible factor was
presented within the recency window, **ask for nothing**. This matches established
practice: previously satisfied authenticators are considered before prompting.

**Then offer every eligible method the subject holds, strongest preselected.** The
person chooses. A subject with both a passkey and a security key sees both — they are
both WebAuthn credentials and both phishing-resistant.

*Corrected from an earlier draft that implied the system picks and the person
complies.*

**If no eligible method exists** — a social-only or magic-link-only subject — the
person is **prompted to enrol** a password or passkey at that moment, rather than being
refused or offered something that proves nothing. This also matches established
practice, where step-up prompts enrolment when the subject has no eligible factor.

**The two shapes differ only when the bar cannot be met:**

| Shape | Asks | If unattainable |
|---|---|---|
| **Reauthenticate** | Are you still the same person, recently? | Present whatever eligible factor you hold |
| **Phishing-resistant** | Do you hold a phishing-resistant factor? | Refuse, and prompt to enrol one |

**Standards grounding.** Reauthenticating with a single factor is explicitly permitted
"in conjunction with the session secret" — the session cookie is itself a possession
factor, so password plus session is knowledge plus possession. This is the pattern the
standard describes, not a weakening of it.

### 86.6 — The assurance table, previously missing entirely

**No document assigned assurance levels.** Session lifetimes and step-up both derive
from them, and the values existed nowhere. Only one case was pinned anywhere: magic
link alone records AAL1.

| Authentication | Level |
|---|---|
| Password alone | AAL1 |
| Magic link alone | AAL1 |
| Email OTP alone | AAL1 |
| Google or Apple alone | AAL1 |
| **Passkey alone** | **AAL2** — a multi-factor cryptographic authenticator with user verification |
| **Security key with user verification, alone** | **AAL2** — same |
| Password + TOTP | AAL2 |
| Password + non-discoverable WebAuthn credential | AAL2 |
| Password + recovery code | AAL2 |
| Social + TOTP | AAL2 |

**Social sign-in is AAL1 regardless of what the provider did.** Federation guidance
states that primary authentication at the identity provider and federated
authentication at the relying party are considered separately and are not assumed to
use the same keys or sessions. The relying party decides what it accepts. We cannot
verify the provider's assurance, so we do not claim it.

**Consequence:** a customer using only social or only magic link never reaches AAL2 and
holds the 30-day session. That is correct and follows from the standard.

### 86.7 — The data export requires reauthentication (M2)

`GET /privacy/export` required nothing at all. Anyone holding a live session — a
borrowed phone, a shared computer — pulled a complete health-implying order history in
one request, from a session that can live 30 days.

**That is the threat model's central attacker**, who needs one successful lookup, and
whom every concealment and enumeration control exists to stop.

**The reason it was left open no longer holds.** D-067 removed step-up because it then
meant a passkey, locking out the people least able to work around it. **The same
decision created the reauthenticate shape**, which every customer can satisfy. Two
changes in one decision, and the second undermined the first.

**Now: the reauthenticate shape, never the phishing-resistant shape** — D-067's
lockout argument remains valid against that. Plus a stated rate limit, which the
endpoint never had.


---

## D-087 — Capability wire format aligned across documents

**Date:** 2026-08-27 · **Status:** accepted · **Applies:** D-078 · **Resolves:** M3

**TL;DR.** D-078 redefined a capability as "permitted by grants, subject to session
gates," carrying a `requires` list. That reached the authorization and frontend
documents and **not** the API contract or the BFF specification, which still showed
`can` alone and restated the acceptance criterion D-078 had declared unachievable.

**Why it mattered.** The API contract is where a wire format is built from. An
implementer following it would omit `requires` entirely — and the frontend requirement
that a capability carrying `requires` must be shown and prompt would have nothing to
render from. The contradiction D-078 resolved would have been rebuilt in code.

Four documents specified two incompatible protocols.

**Corrected.** Both now carry the `can` plus `requires` shape and the criteria "a
capability with an empty `requires` always succeeds" and "a capability with `requires`
prompts rather than failing."

**Process note.** This is the same propagation failure recorded several times in this
log: a decision reaching some affected documents and not others. It was caught by
review rather than by the change itself.


---

## D-088 — Takedown document re-rendered to current state

> **Superseded in part.** The host business rules and vendor facts this entry records (orders, payments, shipments, couriers, addresses) moved to the host under D-165; the reasoning stands as the host's inheritance.

**Date:** 2026-08-27 · **Status:** accepted · **Resolves:** M4

**TL;DR.** The operator-facing document for the most sensitive procedure in the system
still described pre-D-068 behaviour, instructing the disposal the design explicitly
rejected. Three defects, all propagation failures.

**Pseudonymisation, which D-068 rejected.** Step 3 said "pseudonymised per
PRIV-RIGHT-005," citing a requirement that says erasure SHALL anonymise and SHALL NOT
merely pseudonymise. The operator following this during a minor's takedown would have
performed — or believed the atomic operation performed — a disposal that would fail a
regulator's test. Corrected to anonymisation, with the two concrete steps named: destroy
the subject key, delete the fingerprint.

**A broken sentence from the guest-checkout removal.** "A checkbox at registration and
at registration" — the seam left by D-073's edit, in the opening paragraph.

**Conflicting breach-clock instructions.** The takedown said the clock starts "at the
point you determine a notifiable incident occurred"; the privacy requirement and the
runbook say it starts at detection and is not deferrable pending investigation. **Two
operator documents gave different instructions on the one deadline with a regulator
behind it.**

**Resolved in favour of detection — confirmed by the user as a deliberate choice, not
a transcription fix.**

Detection is the **moment of credible indication**, which is the same moment the
procedure begins — not the later moment the incident is concluded notifiable. If
notifiability is uncertain, the clock is already running.

**The alternative was to start the clock on determination**, giving room to assess
before the deadline runs. Rejected: "we spent two of those days deciding whether to
tell you" is a weak position with a regulator, and starting early means never being
late.

**Accepted cost:** the deadline runs while an incident is still being assessed, so the
safe operational move is to notify under uncertainty rather than risk lateness.

The runbook now carries the same statement, so the two documents agree.


---

## D-089 — Assurance floor, alert exemption, and a surviving consent fragment

> **Superseded in part.** The host business rules and vendor facts this entry records (orders, payments, shipments, couriers, addresses) moved to the host under D-165; the reasoning stands as the host's inheritance.

**Date:** 2026-08-27 · **Status:** accepted · **Resolves:** M5, M6, M7

Three unrelated defects, each a single correction.

### 89.1 — Administrative sessions had no stated assurance floor (M5)

D-086 supplied the assurance table, which was M5's larger half. The remainder: nothing
said which row an administrative session belongs to.

AUTH-SESS-005's guard — configured values SHALL NOT exceed the table — does not catch
this alone, because for an AAL1 session the table permits 30 days. An implementer
reasoning "one factor presented, therefore AAL1" would give every staff session a
30-day absolute lifetime with no inactivity timeout.

**A stated floor of AAL2 for administrative-organization sessions**, independent of
factor arithmetic. Already met in practice — that organization is passkeys-only and a
passkey alone reaches AAL2 — but stated so that enabling any additional factor cannot
silently lower it.

### 89.2 — The alert exemption was narrowed in transcription (M6)

D-048 reads: **alert sends are exempt from the hard stop.** The specification narrowed
it to **balance alerts** only. By the conformance rule the log governs; the engineer
builds from the specification.

**Built as written**, a low gateway balance would refuse every SMS alert — including
the mail-system alerts that are deliberately SMS-first, and break-glass's every-channel
alerting. **SMS exhaustion plus a mail outage would then be a total alerting
blackout**, which is precisely the compound failure the two-independent-channels
requirement exists to prevent. And the balance can be drained by the attack being
alerted on.

Restored to all alert-class sends. Deduplication already bounds their volume. The
residual case — balance genuinely at zero — is recorded as unreachable by SMS, with
email carrying alone.

### 89.3 — A consent criterion survived D-066 (M7)

PRIV-SENS-003 still required "a written-consent record exists before any order is
stored." Under D-066 consent gates **purposes**, and the only consent-based purpose on
an order is personalised recommendations — optional and withdrawable.

As written it forced one of two impossible outcomes: **consent to an optional purpose
as a condition of checkout**, which the voluntariness and no-bundling rules prohibit
and which would make the "withdrawable" column of D-066's own table false; or **orders
unstorable** for every customer who declines recommendations.

A surviving fragment of the record-gating model D-066 replaced. Re-scoped to
consent-based purposes, with a criterion asserting that an order is stored, fulfilled
and retained for a customer who has granted none.


---

## D-090 — Nothing is deleted; erasure delivered by a transactional outbox

> **Superseded in part.** The host business rules and vendor facts this entry records (orders, payments, shipments, couriers, addresses) moved to the host under D-165; the reasoning stands as the host's inheritance.

**Date:** 2026-08-27 · **Status:** accepted · **Amends:** D-039, D-068 · **Resolves:** M8

### 90.1 — No record is ever physically removed

**Stated as a principle for the first time**, though the design already worked this
way. "Delete" appeared in several requirements meaning different things.

**The principle is about history, not about rows in general** — corrected after an
initial draft stated it absolutely, which contradicted four existing requirements that
mandate sweeping expired sessions, consumed tokens, elapsed grace windows and expired
audit partitions.

| Never removed | Removed when spent |
|---|---|
| Accounts, organizations, memberships, grants, orders, audit records, consent records | Expired sessions, consumed tokens, one-time codes, elapsed grace windows, delivered outbox records, cache entries, expired audit partitions |

**The line is whether it is a record of something that happened.** Those have
operational and evidential value. A working artefact that has served its purpose is
clutter, and an expired session left in place is a small liability.

Accounts, organizations, memberships, grants, orders and audit records persist for
their lifetime. Removal is expressed as **state** — suspended, restricted, revoked,
deleted — and, where personal data is involved, as **erasure**: the record remains and
its identifying fields become unreadable.

**Corrections:** organization deletion said "hard delete"; erasure said it "deletes the
fingerprint." Both now state that the row persists — the fingerprint is **neutralised**,
overwritten irreversibly.

**Why it matters beyond tidiness:** an account that existed and did things is history
with operational and evidential value. Physical removal loses it, breaks referential
integrity, and leaves audit records pointing at nothing. It also aligns with the rule
that compensating an action inserts a record rather than removing one.

### 90.2 — The takedown was claimed atomic and cannot be

Two steps are library work; two belong to the host application, in tables the library
never touches. An operation spanning that boundary through events cannot be atomic.

**Built to the old criterion, the system would have reported "complete" the moment the
library finished** — while a minor's personal data was still present in the shop's
tables. For this procedure, above all others, a false completion claim is the wrong
failure.

**Replaced with: reliably, and with per-subscriber completion visible.**

### 90.3 — The mechanism: a transactional outbox

**The user's design, confirmed by research as the canonical pattern**, and known in
this application as an *erasure pipeline* — services communicating and confirming
deletions asynchronously, with production precedent.

1. **One transaction** — the identity change and an outbox record, together
2. **The worker publishes** to every registered subscriber
3. **Subscribers act** in their own tables
4. **Each confirms** independently
5. **Complete** only when every required subscriber has confirmed

**One correction to the user's first sketch.** It had the identity system handing the
request to the worker. A crash in that gap loses it — account suspended, nobody told to
act. Writing the request **in the same transaction as the change** is what makes it
reliable, and is the defining feature of the pattern.

**Generic by construction, per the user's requirement.** The event states *"account X
was erased"*, never *"cancel orders for X"*. The library knows nothing of who
subscribes; a second platform registers as another subscriber and the library changes
not at all.

**Refinements from research:**

- **Idempotency at the subscriber is non-negotiable**, and built from day one —
  retro-fitting is painful
- **Bounded retries with exponential backoff and jitter**, not indefinite. The user
  proposed retrying daily forever; a request failing from a defect fails identically at
  every interval, so infinite retry hides it
- **Exhausted retries alert immediately** rather than appearing in the daily digest.
  For a takedown, a minor's data still present is a live problem. The failed queue is a
  diagnostic instrument, not a place failures go quietly
- **A manual completion path**, recorded — planned from the start, not improvised
  during an incident
- **Subscribers marked required or optional**, so an analytics subscriber failing does
  not hold a takedown open

**Scope is wider than the obvious applications.** An overlooked copy in a cache, a log
or a derived store means the request was not honoured. Logs hold identifiers rather
than attributes and caches hold identifiers only — both now in scope by having been
considered rather than assumed.

**Clarification recorded:** an earlier remark that the design "wouldn't work across
different servers" was wrong and conflated two options. The outbox works across
servers, databases and networks — it exists precisely because two systems cannot share
a transaction. The shared-database constraint applied only to the rejected alternative.


---

## D-091 — Jurisdiction-specific configuration, recorded separately

> **Superseded in part.** The host business rules and vendor facts this entry records (orders, payments, shipments, couriers, addresses) moved to the host under D-165; the reasoning stands as the host's inheritance.

**Date:** 2026-08-27 · **Status:** accepted · **Extends:** D-032

**TL;DR.** The lawful bases are Egypt's, not a universal set. Folded into the privacy
specification they read as library behaviour; they are configuration, and a project in
another jurisdiction supplies a different list.

**What is jurisdiction-specific:**

| Item | Egypt-specific because |
|---|---|
| **The six lawful bases** | No vital-interests basis, no public-task basis; two bases — claim or defence of a legal right, and court judgments — do not appear in the European set |
| **Sensitive-data categories** | The enumerated list is Egypt's |
| **Response deadlines** | Six working days to acknowledge; 72 hours and three days for breach notification |
| **Licensing** | Prior authorisation is not the international norm; most regimes operate comply-and-document with inspection afterwards |
| **Written consent for sensitive data** | An Egyptian requirement, not universal |

**What is not:** the mechanisms. Consent gating purposes, per-subject encryption,
purposes carrying bases, records of processing, subject rights — these are the shapes
compliance regimes generally take, and they hold wherever the library is used.

**Consequence for reuse.** Another project in Egypt inherits all of the above
unchanged. A project elsewhere replaces the lists and deadlines, and the mechanisms
stand. **The closed enum is a configured list, not a compiled constant.**

### Related: the guest-checkout leak, corrected

`IDN-LIFE-002a` said "every order SHALL belong to an account; guest checkout SHALL NOT
exist" — a rule about one host's checkout, in the identity specification. Another
project might have no orders, or legitimately permit anonymous transactions.

**Restated as a library rule about sensitive data:** where a host processes sensitive
personal data about a subject, that subject holds an account. The storefront is named
as an instance, since order history is sensitive.

### Recorded: the guest-checkout question, resolved

The user asked whether guest checkout without persistence would fall outside the
regime. It would not — collecting, transmitting and discarding is processing three
times over — but the reduction is real: nothing held means nothing to erase, export or
retain.

**And the structural observation is worth keeping:** a guest order is shaped exactly
like an order from an erased account — subject identifier present, personal fields
unreadable, transaction intact. If guest checkout is ever wanted, the erasure machinery
already produces that shape; it would not need a second path.

**On provider liability**, corrected in the user's favour after an initial misreading:
the regulator advises a written data protection agreement precisely as the means of
avoiding liability for another party's non-compliance. Those agreements exist. A
provider's regulatory failure is theirs. The obligations here are to select a compliant
provider, hold a written agreement, and not instruct unlawful processing — all met.


---

## D-092 — Low-severity sweep: L1 to L8

> **Superseded in part.** The host business rules and vendor facts this entry records (orders, payments, shipments, couriers, addresses) moved to the host under D-165; the reasoning stands as the host's inheritance.

**Date:** 2026-08-27 · **Status:** accepted · **Resolves:** L1–L8

**L1 — the plaintext SMS callback was flagged and never resolved.** The delivery report
arrives over plain HTTP while INF-TLS-001 requires no origin to serve plaintext, and
every document reported "Open items: none." Either the deploy fails its own criterion
or the callback silently does not work. **Named exception recorded**, with the
compensating controls and a first step: confirm whether the gateway supports an HTTPS
callback URL, in which case the exception lapses. Added to the risk register as R-A12.

**L2 — the webhook standard could not verify the one provider that signs.**
BFF-MACH-002 mandated HMAC-SHA256 and an unconditional five-minute timestamp window;
the payment provider signs with an HMAC-SHA-512-family scheme whose older variant
carries no timestamp. A conformant implementation could not verify the only signed
callbacks received. Restated as **the provider's published algorithm**, with the
timestamp window applying where the scheme carries one. Constant-time comparison, raw
bytes and idempotency remain universal.

**L3 — a cache outage was required to deny and to degrade.** AUTH-PRIN-001 said
"denies rather than permits"; INF-CACHE-001 said "degrade rather than deny," falling
through to PostgreSQL. Compatible in intent, contradictory as written — a team building
from one document fails the other's test. Reworded to **never permits**; denial is
required only when the durable store cannot answer either.

**L4 — the non-existence email was specified backwards for registration.** As written,
every legitimate new registrant would have been emailed "you have no account here,"
since for registration a non-existent address is the normal case. Scoped to recovery,
magic-link and email-OTP requests; duplicate registration notifies the existing owner
(D-076).

**L5 — pre-session endpoints had no CSRF binding target.** Registration, sign-in
initiation and recovery are state-changing browser endpoints reached before a session
exists, and BFF-CSRF-001 forbids any endpoint opting out. An implementer would have had
to invent an anonymous session or quietly exempt them — the second being what the rule
prohibits. **A pre-authentication session** is issued on first contact and rotates into
the real session on authentication, which also gives the fixation defence a defined
starting point.

**L6 — the API contract omitted password change and credential management.** No
endpoint existed to change a password while signed in, though screening is required "at
set and at change" and recovery forces "a change at next sign-in." Nor was there any
surface for removing one of several passkeys or un-enrolling TOTP. Added, with removal
of the **last** second factor routing through the delayed flow while removing one of
several completes immediately.

**L7 — the alert table violated its own completeness criterion.** Certificate-renewal
failure and clock drift were both required to alert, with no corresponding condition.
Added — renewal failure at High, as a total-outage precursor.

**L8 — editing residue in normative text.** Two requirements carried **two acceptance
criteria blocks**, the second stale — PRIV-RIGHT-005b and INT-PAY-002, both from
earlier edits in this session. LIB-HOST-001 still said "only these four" against a
larger table. OPS-CFG-004 still said "settable only through deployment configuration,"
contradicting D-071. `401` meant both session-death and step-up-required — **step-up is
now 403**, since frontends conventionally treat 401 as session death and redirect,
which would have broken the retry flow. And `session:revoke.account` broke the
lowercase-hyphenated permission pattern its own validator enforces.


---

## D-093 — The two-store seam: reconciling the key service with the database

> **Superseded in part.** The host business rules and vendor facts this entry records (orders, payments, shipments, couriers, addresses) moved to the host under D-165; the reasoning stands as the host's inheritance.

**Date:** 2026-08-29 · **Status:** accepted · **Amends:** D-082, D-089, D-090 · **Resolves:** third review, all 21 findings

**TL;DR.** D-082 introduced a second stateful store and every document carried on
reasoning as if there were one. All five High findings are that single root cause seen
from different sides.

### 93.1 — The fingerprint was one adjective short of secure (H-2)

PRIV-RIGHT-005c said "one-way fingerprint" and **never said keyed**. An implementer
building a plain hash satisfies every written word — and national mobile numbers occupy
a few hundred million candidates, exhaustible in minutes. A database dump would then
yield **every customer's phone number** and confirm whether any named person is a
customer: the threat model's central attacker, served offline, with no key-service
compromise needed. The adjacent encrypted column becomes decorative, and PRIV-SENS-002's
dump criterion passes to the letter while failing in substance.

**Now a keyed PRF under a dedicated secret held outside the database**, over a **pinned,
versioned canonical form**. The key joins DR-011's artefact list — losing it means nobody
can sign in.

**Rotation posture — confirmed by the user as a deliberate choice.** The key is
**rotatable**, via bulk re-derivation: decrypt each live subject's identifiers from their
own encrypted columns, re-derive under the new key, write back. Slow, and unlikely ever
to run.

*Rejected: fixed for life.* Defensible — the key never leaves the key service and is
never in a database dump — but it costs nothing until the day it costs everything, and on
that day every customer's identifier is permanently recoverable from any old copy of the
database. A written procedure is cheap insurance against an outcome with no other remedy.

*Rejected: dual-key transition.* Solves a coordination problem that does not exist at one
server and one deployment; a maintenance window is acceptable.

**One key for the whole system, not one per subject** — sign-in must fingerprint an input
before it knows whose account it is. Erasure therefore neutralises the value rather than
destroying the key.

**Consequence for M-3:** database-level case-insensitivity is impossible over a hash, so
D-040's criterion is rescoped to plaintext columns and identifier case-folding moves into
the pinned canonical form — which is now load-bearing and versioned so a future
normalization change re-derives rather than silently stranding every fingerprint.

### 93.2 — The two stores have independent timelines (H-1, H-3)

The database is continuously archived; the key service is snapshotted periodically.
**Restoring both leaves them disagreeing, in two directions that fail opposite
requirements.**

- **Key snapshot older than the database:** erasures since the snapshot come back —
  their keys restored, their data readable again. The restore procedure contained the
  sentence *"subjects erased after the backup was taken stay erased"*, which is true only
  while the live key service survives, **written inside the procedure for when it does
  not.**
- **Database restored to a point before an erasure, key service current:** the subject
  appears `active` with an intact fingerprint, cannot decrypt, and their erasure record no
  longer exists.

Neither is detectable by inspection: the first looks like a healthy restore, the second
like a working account until a screen renders a name.

**DR-014 adds a reconciliation pass in both directions before cut-over**, idempotent,
driven by the audit trail the restore brings back. D-082 rejected erasure replay because
it "leaves a window and depends on a list surviving" — applied here that objection does
not hold, since the list is in the restored audit trail by construction.

**DR-015 states the second recovery point honestly.** Keys created since the last
snapshot are lost on rebuild, so **personal data's recovery point is the snapshot
interval, not seconds** — and the loss is silent, since the rows restore and only fail
when something needs a name. Snapshots hold key material only and are cheap, so they run
far more often than base backups.

### 93.3 — Erasure spans two stores with no transaction (H-4)

**D-090's own insight, unapplied to D-082's mechanism.** Key destruction is a call to
another store; the fingerprint, state and outbox record are one database transaction.
Nothing said which commits first.

Destroy first and fail to commit: a **live, non-erased subject with permanently
unreadable data** — the accident the grace window exists to prevent. Commit first and
fail to destroy: a subject **reported erased whose data still decrypts** — the
false-completion failure D-090 called the wrong failure above all others, reintroduced one
store to the left.

**Key destruction becomes a required outbox subscriber:** commit first, destroy after,
retried, idempotent, confirmed, and visible in the same per-step view. The design's own
pattern, costing one more subscriber-shaped step.

### 93.4 — The key service is a per-request dependency and was operationally invisible (H-5)

**The claim that it added no availability dependency was wrong in kind.** The secrets
manager is consulted once at boot; the key service is consulted on **every read or write
of a personal field.**

Its failure produces a distinctive partial outage — sign-in works, browsing works, but no
order can be placed, no name renders, no message resolves an address. There was **no
runbook entry and no alert condition**, and OPS-ALERT-001's own completeness criterion was
violated by INF-KEY-002's unseal-failure alert having no row.

Two alert conditions added, a runbook section written, and the false claim corrected —
including the instruction **not to work around it by disabling encryption**, which would
write plaintext no later fix removes.

### 93.5 — Every alert originated on the host being monitored (M-6)

Both channels are sends initiated on the single VPS. **The highest-severity scenario the
recovery chapter plans for — loss of the host — produced no alert at all.** Detection
would be a customer telephoning, and the 4–8 hour objective acquired an unbounded delay in
front of it.

INF-OBS-003 requires an **off-host reachability check**. Among the cheapest requirements
in the set, and now named as the detection path for every scenario in the recovery
chapter.

### 93.6 — Remaining findings

**M-1:** the AAL2 floor added by D-089 would have **refused break-glass sessions** —
single-factor by construction — recreating the F-01 failure through a requirement written
after it was fixed. Exempted, mirroring AUTH-STEP-004.

**M-2:** order history is browsable on the same session whose *export* is gated.
Recorded as accepted position R-A15 in `13` (D-125).

**M-4:** erasure claimed no combination permits singling out, while the analytics guard
rail says the opposite in almost the same words. **Both cannot be absolute.** Recorded as
an accepted residual on the same footing as the courier inference, and noted as permanent:
erasure never rewrites rows, so the plaintext precision fixed at write time is the
precision every future erasure retains.

**M-5:** data-key granularity, batching and caching were unspecified. Per subject; batch
unwrap on list paths; **plaintext keys never retained beyond request scope**, since a
cached key would keep decrypting after erasure — an undocumented latency in a cache the
erasure flow does not know exists.

**M-7:** three residues of the D-082/D-090 rewrites, including the takedown document
saying erasure "deletes" the fingerprint — **introduced in the very edit that corrected
that document**, and the propagation-failure class named twice already in this log.

**L-1 to L-9:** numbering, duplicate source lines, an Angular version statement that did
not survive checking, a stray table row, a misfiled requirement, the break-glass endpoint's
mounting stated only in this log plus a cookie-rejection rule that could refuse it in an
emergency, mail-suspension semantics that two documents implied differently, the
unverified key-count assumption never reaching the specs, and step-up-policy weakening not
alerting.

### On the value of the pass

Three of the five High findings are in mechanisms written the same day as the review that
found them. The reviewer's diagnosis — *the specification treats the one boundary it
recently created as if it were not there* — is the kind of observation that only comes
from reading cold.


---

## D-094 — Erasure records intent first; enumerated values have a rule

> **Superseded in part.** The host business rules and vendor facts this entry records (orders, payments, shipments, couriers, addresses) moved to the host under D-165; the reasoning stands as the host's inheritance.

**Date:** 2026-08-29 · **Status:** accepted · **Amends:** D-093 §93.3 · **Origin:** user, rejecting both options offered

**TL;DR.** Both orderings I proposed were wrong for the same reason: neither recorded
**intent** before acting, so a crash left either a false record or an irreversible loss.
The user's sequence records intent first, and any crash then leaves an honest one.

### 94.1 — The sequence

1. **Record the erasure as in progress** — durable intent
2. **Destroy the subject key**
3. **Neutralise the fingerprint and mark complete**

**Why both of my options were wrong.**

*Commit the state change first, destroy after* — a crash leaves a record saying **erased**
while the data still decrypts. The state is not merely incomplete, it is **false**, and
recovery depends on a queue entry surviving.

*Destroy the key first, commit after* — a crash leaves a **live subject, who may have
cancelled, whose data is permanently unreadable.** Irreversible.

*Destroy inline within the transaction* — offered as a third option and **not a third
way**. The database can roll back; the key service cannot un-destroy a key. A successful
call followed by a failed commit produces the irreversible outcome, so it is the second
option with faster feedback and the same risk.

**Recording intent first makes every crash state honest.** The row says what was meant to
happen and which steps have not finished, and it survives independently of any retry
mechanism because it is a row rather than a message.

**Recovery:** retry — key destruction is idempotent; alert on exhausted retries, since a
stuck erasure carries a regulatory clock; and every incomplete row is one query, so an
outage yields a list rather than a hunt.

**A correction recorded.** Duration was raised as a factor in choosing an ordering. It is
not: the key destruction is required either way and nobody waits on it. Crash ordering is
the only input to this decision.

### 94.2 — A dedicated erasures table

Progress lives in **its own table**, not on the account record. Account states remain
`active` → `deleting` → `deleted`.

*Rejected: a new `erasing` account state.* An erasure is a background operation, not a
phase of the account's life. Adding a state would oblige every reader of an account to
handle a value describing a job rather than a person — the separation-of-concerns
objection the user raised, and correctly.

Access has already stopped before any row exists: `deleting` begins at the grace window.

### 94.3 — A rule for enumerated values, since this recurs

**Code branches on it → constrained column. Changes without a deploy → table.**

| Kind | Storage | Examples |
|---|---|---|
| Branched on | Constrained column | Account state, erasure status, lawful basis, concealment behaviour |
| Changes without a deploy | Table | Roles, permissions, courier districts |

**The test:** could someone add a value and have it work without an engineer? If not, a
lookup table buys nothing — the value is inert until code handles it, and every query pays
a join.

**Native database enum types are prohibited.** Altering one is a migration with locking
behaviour; a check constraint gives the same guarantee and changes freely.

**Lawful bases decided as the mixed case:** branched on, yet jurisdiction-specific
(D-091). Constrained column — a different jurisdiction changes the branching logic too,
and is a code change however the values are stored.


---

## D-095 — Reconciliation after restore, and what it cannot fix

**Date:** 2026-08-29 · **Status:** **superseded by D-097** · **Extends:** D-093 §93.2

> **Superseded.** Reconciliation existed because keys lived in a separate service with
> its own backup timeline. D-097 moved the wrapped keys into the database, so there is
> one store and nothing to reconcile — DR-014 was removed with it.
>
> **The limit identified here survives and is the reason DR-016 exists**: restoring past
> an erasure recovers the wrapped key and the erasure record together, and only an
> off-host ledger catches that. Retained for the reasoning, not as current design.

**TL;DR.** The database and the key service are backed up separately, so their restore
points differ and they disagree. A reconciliation pass before cut-over resolves the
disagreement. It does **not** resolve the case where both were restored past an erasure —
that is data loss and needs a separate answer (DR-016).

### What was decided

**Reconcile after both stores are up, before cut-over.** Compare the restored erasures
table against the keys actually present in the key service:

| Case | Meaning | Action |
|---|---|---|
| Erasure complete, key present | The key service snapshot predates the erasure | Destroy the key again |
| No erasure record, key absent | The database was restored further back than the key service | Complete the erasure and record it |

**It cannot run earlier.** There is nothing to compare until both systems are loaded.
Nobody is using the system in that window, so the mismatch never reaches a customer.

**Rejected: disclose and do nothing.** Defensible on the basis that restores are rare and
backup windows short — but a person who asked to be erased could silently return, which
is the failure the erasure design exists to prevent.

**Rejected: live replication of the key service so it is never restored from a snapshot.**
Solves it properly and costs a second machine, declined elsewhere for the same reason.

**Honest cost of the chosen option:** a script that must be correct and must actually be
run. A restore performed without it returns to the do-nothing position.

### The limit, identified by the user

**Reconciliation compares two stores and can only find where they disagree.** If **both**
were restored to a point before an erasure, they **agree** — the erasures table has no
record and the key is present — and nothing is flagged. The subject is active and
readable again, as though the request had never been made.

**Nothing inside the system can catch this.** Every record of the erasure lived in the
database, which went back with everything else. Only a record kept somewhere that is not
restored would survive.

**This is data loss, not inconsistency** — the same category as losing an hour of orders,
except what is lost is a fulfilled legal obligation, and the affected person has no reason
to ask again.

**Recorded as a separate open question (DR-016), not folded into this decision.** An
earlier draft presented reconciliation as though it addressed both, which it does not.


---

## D-096 — Off-host erasure ledger, deferred to the tier upgrade

**Date:** 2026-08-29 · **Status:** accepted, deferred · **Closes:** the limit identified in D-095

**TL;DR.** Reconciliation cannot detect an erasure that **both** stores were restored past,
because they agree. Only a record kept off-host survives. Deferred until object storage
exists at the tier upgrade; accepted as R-A13 until then.

**The gap, in the user's words:** both stores lost, both restored, both say the subject is
active — but the version that was lost had erased them. Every trace lived in the database
and went back with it.

**Why it is worse than ordinary data loss.** Nothing surfaces it. A missing order is chased
by the customer; a reversed erasure is not, because the person was told it completed and
has no reason to check. It is a fulfilled legal obligation silently undone.

**The mechanism — a ledger, not a system.** One appended line per erasure: timestamp,
subject identifier, reason. Replayed after any restore; idempotent, so replaying the whole
file is always safe.

**It leaks nothing requiring encryption**, which was the user's question. The subject
identifier is opaque and derived from nothing about the person (IDN-ACCT-002) — without the
database it identifies nobody, and anyone holding the database learns nothing new from it.
What it does disclose is **how many erasures occurred and when**. Not personal data, and
accepted against the alternative.

**Size:** a handful of lines a year.

**Deferred, and the reason is honest.** "Off-host" requires somewhere that is not the VPS,
and before the tier upgrade there is nowhere. The available substitute — emailing each
erasure to the operator — is a record that exists rather than a mechanism, and would not be
reliably replayable during a restore. Building it now would be worse than accepting the
gap and saying so.

*Rejected: accept permanently.* Defensible — three things must coincide, and it may never
occur. Declined because the affected person cannot discover it and has no reason to ask
again, which is not true of any other loss the design accepts.

*Rejected: the customer's own confirmation as the record.* That is discovery through a
complaint, not detection.

**Recorded as R-A13**, bounded by the backup interval, closed at the tier upgrade.


---

## D-097 — Wrapped per-subject keys in the database; the key service is removed


> **Superseded in part.** Key version stored with each ciphertext is replaced by the format marker of D-100.
**Date:** 2026-08-29 · **Status:** accepted · **Supersedes:** D-082 · **Amends:** D-093, D-094, D-095

**TL;DR.** Standard envelope encryption: a per-subject data key, stored **wrapped** in the
database, under a key-encryption key held in the secrets manager. Erasure overwrites the
wrapped key. The separate key service and everything it required are removed.

**Origin: the user's proposal**, offered as an untested idea. It is the textbook pattern —
described in the field as the most practical approach for most systems — and it is better
than both designs it replaces.

### Why D-082 moved keys out, and why that was the wrong trade

D-068 originally put wrapped keys in the database. D-082 moved them to a key service to
close one gap: point-in-time recovery is cluster-wide, so a restore reproduces the wrapped
key and an erased subject becomes readable again.

**That gap is now closed by the ledger (D-096) instead**, which re-applies erasures after
any restore preceding them — and the ledger works identically whichever design is in use,
because it replays the erasure rather than manipulating keys.

**Closing it with a second store cost six failure modes**, all removed here: two stores
with independent timelines requiring reconciliation before every cut-over; customers
registered since the last snapshot left silently unreadable after a rebuild, remediable
only by asking them to re-enter everything; a per-request dependency on every read of a
personal field; a second backup schedule and snapshot cadence; unverified behaviour at
thousands of keys; and a second service for one operator to run.

### What C gains over a single shared key

**Erasure reaches host tables without the host acting.** Overwriting one wrapped key in the
library's own table makes every field encrypted under it unrecoverable — including columns
in host tables the library never touches. A single shared key would require overwriting
fields across every table that holds them, through the subscriber pipeline.

**KEK rotation is cheap** — re-wrap the subject keys, never re-encrypt data.

**A leaked individual key exposes one subject** rather than all.

### What is given up

**A backup taken before an erasure holds the wrapped key as it then was.** Anyone with
both that backup and the KEK can recover the subject's fields. Under D-082 the destroyed
key was never in the backup, so this was closed.

**The scenario presumes the secrets manager is already compromised**, at which point the
master key, deployment credentials and live database access are all available. Accepted,
stated in PRIV-RIGHT-005a rather than implied, and bounded by backup retention.

### Requirements the pattern carries, from research

- **Ciphertext bound to its location by additional authenticated data** — subject, table,
  column. Without it ciphertext can be moved between rows or subjects; with it, moved
  ciphertext fails to decrypt
- **KEK versioned**, prior versions retained until re-wrapping completes, or rotation
  strands everything under the old version
- **Algorithm, key version and initialisation vector stored** with each ciphertext —
  missing metadata is a documented cause of unwrap failure
- **No plaintext data key logged**, nor retained beyond request scope
- **Primitives from a maintained library**, not hand-written

**A correction to an earlier claim.** Blast radius was presented as favouring this design
over the key service. It does not: a KEK compromise unwraps every subject key under it,
exactly as compromising the key service would. Only a leaked *individual* key differs.

### What changed

**Deleted:** four key-service requirements, two disaster-recovery requirements, one
operations requirement, two alert conditions, one runbook section.
**Rewritten:** the encryption requirement, the erasure sequence, the restore paragraph,
one risk note, the artefact list, the pairing rule.
**Retained:** the fingerprint, the ledger, the erasures table, the escrow, the rotation
posture — all independent of where keys live.

**The specification is smaller than before the change.**

**D-082 remains in this log as the recorded alternative**, with its reasoning, if
pre-erasure backup protection ever justifies the operational cost.


---

## D-098 — Per-subject encryption is scoped by schema

**Date:** 2026-08-31 · **Status:** accepted · **Extends:** D-097 · **Origin:** user

**TL;DR.** Per-subject encryption applies to the schemas serving public applications.
Management-only schemas — HR, finance, internal records — are covered by database
encryption at rest. The boundary is a schema, not a field.

**The test is exposure, not ownership.** Public-facing data is reachable by anyone who
reaches the service, so it warrants isolation per person. Management-only data is
entered by staff, read by staff, and never leaves.

**And per-subject encryption would not protect it from the threat that matters there.**
As the user established: database access implies server access implies the secrets
manager implies the master key. Encrypting internal records per subject buys nothing
against an insider, who is the relevant adversary for internal data.

**Who is a subject:** every user of public services — individual customers,
organizations and their members, and the company's own staff, who in this scope are
users like any other.

**A staff member appears on both sides, and it is not ambiguous.** Their identity
profile is in the identity schema and is per-subject encrypted. Their work profile is
in a management schema and is not. Two records, two schemas, one rule.

**Two of my formulations were wrong and are recorded as such.**

*Rejected: contacts as their own subjects with their own keys.* Proposed before the
boundary was clear. It treated the question as "whose data is this" when the operative
question is "how exposed is it."

*Rejected: a per-field test of whether data reaches a public service.* This would have
required evaluating every field, and it solved a leakage problem the architecture does
not have — public users' personal data lives in the schemas serving public
applications, not scattered through HR or finance. The user pointed this out; the
schema boundary is both simpler and already present in the design.

**Erasure on the internal side** is by overwriting the declared fields in one
application with a known schema — feasible precisely because it is internal and
controlled. It reaches the live database, not backups, so it completes when backups
expire. That asymmetry is proportionate to the difference in exposure.


---

## D-099 — The subject of an encrypted field is declared, not stored again

**Date:** 2026-08-31 · **Status:** accepted · **Extends:** D-097 · **Closes:** F-04's declaration gap

**TL;DR.** Each encrypted field names the **existing column** that identifies its
subject. No owner column is added and nothing is embedded in the ciphertext.

**The realisation that settled it.** The owner reference mostly exists already: an order
carries `customer_id` because it is that customer's order, not for encryption's sake,
and it is already indexed. Both options originally offered recorded it a second time.

**The declaration is per field, not per table**, which is what handles a row concerning
two subjects — the case the user raised and neither original option answered cleanly.

```
orders
| order_id | customer_id | contact_id | enc_cust_name | enc_contact_name |

enc_cust_name    → subject is customer_id
enc_contact_name → subject is contact_id
```

**Rejected: an owner column on each row.** Duplicates an existing reference, must be
kept consistent with it, and needs a second column whenever a row concerns two subjects
— at which point something must record which column governs which field, which is the
declaration by another route.

**Rejected: the subject reference inside each ciphertext.** Self-describing and it does
handle multi-subject rows, but it enlarges every encrypted value and — the cost the user
identified — removes the ability to find a subject's rows by query, since there is no
column to filter on. The business foreign key does that today and would stop doing it.

**Validated at startup:** a declared subject column must exist and must reference a
subject. A missing or wrong declaration fails before serving, rather than encrypting
under a guessed key.

**This closes the gap the first review raised as F-04** — a declaration of which subject
a record concerns. It was answered then with lifecycle events, which addressed who acts
on an erasure and never addressed whose data a field is.


---

## D-100 — Ciphertext carries a format marker and an initialisation vector

**Date:** 2026-08-31 · **Status:** accepted · **Extends:** D-097, D-099

**TL;DR.** Each stored value carries a format marker and its initialisation vector,
and the marker is included in the authenticated data. No key version — that was
established as serving nothing here.

**Layout:** `[format marker][initialisation vector][ciphertext + tag]`

### The initialisation vector

Not optional. Without a distinct one per encryption, two subjects with the same value
produce identical ciphertext, which discloses that they match.

### The format marker

**Names the algorithm and parameters**, so a future change is read-old-write-new rather
than a single all-at-once re-encryption with downtime and no way back.

This is **cryptographic agility**, and the guidance is direct: persist enough
information to reconstruct the cryptographic context, so algorithm upgrades become
routine engineering rather than emergency rewrites. Agility is significantly easier
when formats carry explicit algorithm identifiers; without them, introducing a new
algorithm requires a costly and slow format change.

**Not hypothetical** — the same shape is being adopted in current production
migrations, a literal version prefix ahead of the vector, ciphertext and tag.

**It cannot be retrofitted.** Existing values would carry no marker, so the problem it
solves would already exist.

### The detail that would have been missed

**The marker must be authenticated.** Unauthenticated it is a **downgrade vector** — an
adversary editing it to force decryption under a weaker scheme, which standards guidance
names as the principal risk of algorithm agility. It therefore joins the authenticated
data already bound in D-097: subject, table, column.

Free to do, and its absence would have turned an agility mechanism into an attack
surface.

### No key version

Established by the user: rotating the KEK re-wraps subject keys and leaves ciphertext
untouched, so nothing about a stored value changes and there is nothing to version. Key
versioning belongs on the **wrapped key record** — which KEK version wrapped it — not on
the data.


---

## D-101 — Internal-side erasure is manual

**Date:** 2026-08-31 · **Status:** accepted · **Extends:** D-098

**TL;DR.** Management-only schemas have no per-subject keys (D-098), so erasure there
cannot work by key destruction. It is handled **manually** — locate the record, clear
the fields, record it. An automated routine is possible later, **when the user decides**,
not on a trigger.

**Why manual is proportionate.** These requests are expected to be rare, and the user's
reading is that a business contact's details sit closer to a business card given to us
than to a customer's private data. That reading is not settled here, and it does not
need to be: manual handling works under either.

**What it avoids.** An automated routine works from a declared list of personal fields.
A field added and not declared is missed **silently** — the erasure reports success and
a phone number remains. A person doing it by hand cannot silently miss.

**Two arguments of mine that did not survive, recorded because the reasoning matters:**

*That maintaining the field list would be a burden.* The owner pointed out that whoever
writes the code maintains it, so declaring a field and listing it happen in the same
work. Correct.

*That each session starting cold makes continuity unreliable.* The user pointed out that
specifications and instruction files persist and are read at the start of any session
touching the code — which is the entire purpose of this document set. Also correct, and
it undercut the objection more thoroughly than the mitigation I was proposing.

**A startup check was proposed and withdrawn.** It would verify that every field marked
personal appears on the erasure list — but the user identified the loop: the check needs
to know which fields are personal, which is itself a list. **Unless it is the same list**,
in which case the check verifies nothing, because one declaration drives both and they
cannot diverge. The right structure removes the seam the check was guarding. This is how
the public side already works: one declaration determines encryption, export and
erasure.

**The residual is unfixable by any mechanism**, on either side: a personal column that is
never declared at all. Nothing can detect it, because nothing knows a column holds a
phone number unless told. That is the floor, not a gap in this decision.

**Deferred, and deliberately not on an automatic trigger:** the user decides whether and
when to build the automated routine. **Personal fields in management schemas are noted as
they are added**, so building it later does not begin with a cold audit.


## D-102 — Erasure text aligned to D-097: key destruction is inline, the erasures table records the request and the host-side work

**Date:** 2026-09-03 · **Status:** accepted · **Amends:** D-093 §93.3, D-094 · **Applies:** D-097 · **Resolves:** final review, finding 1

**TL;DR.** `01-identity` still described erasure under the removed key service — key
destruction as a separate store, run after the commit by a retrying subscriber, with
a `key-destruction-failed` status. `04-privacy` described the current design. One
transaction now does it all, and the erasures table is simplified to match.

**What was wrong.** D-097 moved the wrapped per-subject key into the database and
stated that the erasure sequence was rewritten. It was rewritten in `04` only.
IDN-LIFE-003a kept "key destruction is a required subscriber … cannot share the
database transaction … commits after"; IDN-LIFE-003b kept step-confirmation columns
and a `key-destruction-failed` status; DR-011, DR-012, R-A13 and `19` §9 kept
"key snapshot" and "both stores" phrasing. A builder following `01` would rebuild the
crash window `04` closed. This is the propagation-failure class recorded in D-087,
D-088 and D-093 §93.6.

**Decision.**

- **One transaction** at the end of the grace window: account state → `deleted`,
  the subject's wrapped key overwritten, the fingerprint neutralised, the erasures row
  inserted, the outbox record written. All commit or none do. Key destruction and
  fingerprint neutralisation are **inline writes to the library's own tables**, never
  outbox subscribers.
- **The erasures table stays** as the record of the request and of the host-side
  work: subject, requested-at, reason, status, attempts. Status is
  `awaiting-subscribers` · `complete` · `failed` (retries exhausted, manual completion
  pending). The step-confirmation columns and `key-destruction-failed` are removed —
  with the writes inside the transaction, they cannot be false while the row exists.
- **Per-subscriber confirmation lives in the outbox** (PRIV-RIGHT-005b), as before.
  The manual completion path, the retry count, the "every incomplete erasure in one
  query" view (D-094) and the DR-016 ledger source all key on the erasures row.

**Rejected.**
- *Fold the erasures table into the outbox and retire IDN-LIFE-003b* — smaller
  schema, but loses the one-query incomplete-erasures view and leaves the manual
  completion path and the ledger nothing stable to key on.
- *Keep the step columns as evidence* — they would be constant `true`; a column that
  cannot vary records nothing.

**Crash semantics, stated.** A rollback leaves a live subject with readable data and
no erasures row — the request simply has not happened yet and is retried. There is
no longer any state in which a live subject has unreadable data, or an erased
subject's data still decrypts.

**Propagated to:** IDN-LIFE-003a, IDN-LIFE-003b (`01`); PRIV-RIGHT-005c step 3
(`04`); DR-011, DR-012 (`12`); R-A13 (`13`); `19` §9.


---

## D-103 — The envelope carries every key a cold restore needs; backups are encrypted under a dedicated asymmetric key

**Date:** 2026-09-03 · **Status:** accepted · **Amends:** D-069, D-084 · **Resolves:** final review, finding 2

**TL;DR.** The escrow envelope was one item short of a working restore twice over,
and the key that encrypts the backups themselves had never been named. Fixed: the
envelope lists five items, backups use their own asymmetric key, and the rebuild
list matches.

**What was wrong.**
- DR-009 said "all four items" while its table listed three — the fourth was the key
  service unseal material D-097 removed; the count survived the deletion
- D-093 put the **fingerprint key** on DR-011's rebuild list ("nobody can sign in
  without it") and nobody put it in the envelope. DR-009's own criterion — a restore
  without the secrets manager — therefore produced a system nobody could log in to
- D-069 required backups "encrypted under a key not held on the server" and no
  document said what that key was, where it lived, or that it was escrowed. A rebuild
  following the documents could not open the backup it was restoring

**Decision.**

**Backups are encrypted under a dedicated asymmetric backup key.** The public half is
on the host, so the unattended backup job encrypts without holding a secret; the
private half is in the secrets manager and printed in the envelope, and is never on
the host. This is the only arrangement that satisfies DR-010's "key not present on the
host filesystem" while letting an unattended job run.

*Rejected: reuse the key-encryption key for backups.* The backup job on the host would
then hold the master key, contradicting DR-010 and putting the KEK in a second process;
and every KEK rotation would either re-encrypt every retained backup or keep old KEK
versions alive for the backup retention window, coupling two cadences that should be
independent.

**The envelope holds five items**, all also in the secrets manager: break-glass
credential, key-encryption key, fingerprint key, backup private key, deployment
credentials.

**Rotation.** The backup key rotates at the annual reseal alongside the KEK. Previous
private keys are retained — in the secrets manager and the envelope — until every
backup encrypted under them has expired, then discarded. The fingerprint key keeps the
posture D-093 set: rotatable by bulk re-derivation, expected never to run.

**Propagated to:** DR-009, DR-010, DR-011 (`12`); the annual reseal in `06` §9 and
`11` §3.2–3.3.


---

## D-104 — Cross-application sign-on is the OIDC authorization code flow; each BFF is a confidential client that keeps no token

**Date:** 2026-09-03 · **Status:** accepted · **Amends:** D-007, D-033.3, D-005 · **Resolves:** final review, finding 3

**TL;DR.** Four documents said a per-app session is "re-established silently through
the authentication application" and none said how. It is now the authorization code
flow with PKCE against the library's own provider, with each browser application a
registered confidential client. Nothing new is built.

**What was wrong.** Coherence review 1 (R-08) noted the SSO mechanism was never
described; D-033.3 named the auth session and stopped. A builder was left to invent
the code-and-redirect protocol that hands out sessions — where a guessable, reusable,
front-channel-exchanged or unbound code is a session-theft primitive.

**Decision.**

- **Each browser application's BFF is a confidential client** in the registry that
  already serves Stalwart (D-005) and registration redirects (D-057). Its
  `redirect_uri` is exact-matched (RFC 9700).
- **Silent re-establishment** is `prompt=none` against `/oidc/authorize`. The auth
  app answers from its auth-session cookie: a one-time code if a session exists, a
  `login_required` error if not — in which case the BFF redirects again without
  `prompt=none` and the person signs in.
- **The BFF exchanges the code back-channel** on the machine profile (D-070) with its
  client secret and PKCE verifier, reads the `sid` claim naming the session record,
  and creates its per-app session bound to that record. The per-app session inherits
  the record's assurance properties (D-020.2).
- **No token is retained and no refresh token is issued** to a browser-application
  client. The ID token is read once and discarded. AUTH-OIDC-002 is refined from
  "browser applications SHALL NOT use OIDC tokens" to "the browser SHALL never receive
  a token; a BFF uses the code flow once to establish its session and stores none."
- **Codes** are single-use, bound to the client and the PKCE verifier, and expire in
  sixty seconds or less — all standard requirements, restated rather than invented.
- **Logout and revocation are unchanged**: every per-app session points at the one
  record (D-033.3), so revoking it ends all of them.

**Rejected.**
- *A bespoke first-party handshake* — a signed one-time nonce minted by the auth app
  and exchanged back-channel. A hand-rolled version of what the code flow already
  specifies, failing P-003's standards-over-invention test, and needing its own
  binding, replay and expiry rules written from scratch.
- *A shared cookie across subdomains* — already rejected in D-007.

**Propagated to:** AUTH-SESS-012 (new) and AUTH-OIDC-002 (`02`); BFF-SESS-006 (new,
`17`); `09` §9 client registry note.


---

## D-105 — Two deployment-injected bootstrap values; the secrets manager's properties stated

**Date:** 2026-09-03 · **Status:** accepted · **Amends:** D-026.3, D-069, D-071 §71.2, D-093 §93.1 · **Resolves:** final review, finding 4

**TL;DR.** "No secret in files, images or environment variables" was unbuildable as
written: the application must hold something to prove itself to the secrets manager,
and the database password already came from deployment configuration without the
requirement admitting it. The carve-out is now explicit — exactly two values — and
the secrets manager is characterised without being named.

**What was wrong.**
- INF-HOST-003, OPS-SEC-001 and AUTH-KEY-002 forbade every place secret zero could live
- OPS-CFG-008 conceded the database connection "comes from deployment configuration"
  — a password in a file — in a document whose neighbouring requirement forbids it
- The secrets manager is a hard boot dependency (startup fails without the KEK) that
  no document characterised. The log records it is 1Password (D-069) reached by an
  access token (D-084); the specs said "a secrets manager"
- D-097 removed the key service without saying where the fingerprint key — which
  D-093 had placed there — now lives. `04` said "outside the database", `06` said only
  two values come from outside it

**Decision.**

**Exactly two bootstrap values** are permitted outside the secrets manager, and only as
**deployment-injected configuration** — present on the host at runtime, never in the
repository or the container image: the **database connection** and the
**secrets-manager access credential** (secret zero). Nothing else. The backup public
key may also sit on the host; it is not a secret (D-103).

**Everything else is fetched from the secrets manager at boot** — the key-encryption
key and the **fingerprint key** — or is encrypted under the KEK in the database, as
provider credentials and signing keys already are. The fingerprint key's home is the
secrets manager; it is fetched beside the KEK and never written to the database.

**The secrets manager's required properties**, stated in `19` without naming a product:
off-host; reachable at boot; holds the KEK, the fingerprint key and the backup private
key; authenticates the application by a **scoped** credential that can be revoked and
rotated without a code change; its own access is protected by hardware-backed
multi-factor authentication (D-084).

**The boot dependency is recorded** as an operational dependency, R-O04: a restart while
the secrets manager is unreachable keeps the application down until it returns. That is
the fail-closed position and is accepted; the alternative — caching the KEK on the host
to survive an outage — would put the master key on the disk the design keeps it off.

**Rejected.**
- *Platform identity for secret zero* (the host proves itself without a stored
  credential) — correct where a cloud provider offers it; a plain VPS does not, and the
  secrets manager in use authenticates by token. Keeping the absolute wording would have
  left the requirement unbuildable.
- *Naming the product in the specification* — the property list is what a builder needs;
  the product is a deployment choice recorded here.

**Propagated to:** INF-HOST-003 (`19`); OPS-SEC-001, OPS-CFG-008 (`06`); AUTH-KEY-002
(`02`); PRIV-RIGHT-005c (`04`); R-O04 (`13`).


---

## D-106 — Library operations are a contract rendered two ways: in-process services and HTTP endpoints

**Date:** 2026-09-03 · **Status:** accepted · **Extends:** D-026.4, D-052, D-092 · **Resolves:** final review, finding 5

**TL;DR.** Seventeen operations the library owns had no surface a host could call —
neither an endpoint in `09` nor a contract in `07`. They now exist once, as service
contracts in `Janus.Core`, and are exposed as `/admin` and `/account` endpoints
mapped over them.

**What was wrong.** Each of these was required by a SHALL and reachable through
nothing: staff invitation (IDN-LIFE-009a), the takedown operation (IDN-LIFE-003),
administrative suspend and reactivate (AUTH-SESS-010), groups (AUTHZ-GROUP-001),
organization lifecycle and cancellation (IDN-ORG-003), ending a membership, the
data-subject-request queue (PRIV-RIGHT-002), manual erasure completion and the
per-subscriber progress view (IDN-LIFE-003a), audit query by subject
(PRIV-BREACH-002), explanation and concealed-denial resolution (AUTHZ-GATE-004),
publishing a compliance-text version (PRIV-CONS-006), photo upload (IDN-ATTR-004),
language preference (IDN-ATTR-001), cancelling a deletion (IDN-ACCT-007), licence and
permit dates (OPS-MAINT-001), lifting a restriction (PRIV-RIGHT-004). D-092 L6 had
fixed the same gap for passwords and credentials and stopped there.

**Decision.**

**One definition, two renderings** — the pattern the gate already uses (D-017). Every
library-owned operation is a **service contract in `Janus.Core`**, callable in-process
by a host that hosts the library (the management application does; so do the takedown
and outbox paths). `Janus.Hosting` exposes the same contracts as HTTP endpoints for the
frontends. Neither is written separately; the endpoint is a mapping.

**The operations contract joins LIB-API-001** as a public surface, guarded by contract
tests like the rest.

**Permissions added**, coarse per CONV-NAME-002: `account:manage` (suspend, reactivate,
lift restriction, cancel a deletion on a subject's behalf), `takedown:execute` (its
own, because of what it does), `group:manage`, `notice:publish`, `compliance:manage`
(licence and permit dates, assessment references). Existing permissions absorb the
rest: `membership:manage` covers invitations, `privacyrequest:manage` covers the queue
and manual erasure completion, `audit:read` covers subject queries and concealed-denial
resolution, `organization:manage` covers lifecycle and cancellation.

**Rejected.**
- *HTTP only* — in-process callers would carry a system principal through loopback
  HTTP, the takedown and outbox paths already run inside the host process, and the
  contract test would guard a wire shape while the real dependency is the service
  beneath it.
- *Leave the surface to the host* — the operations are the library's; a host inventing
  them puts library behaviour outside the stable contract.

**Propagated to:** LIB-API-001, LIB-API-005 (new) in `07`; `09` §6 and new §8a; `10`
§1 and §2.1.


---

## D-107 — Fourteen keys gain safe defaults; six deployment-naming keys are required and listed

**Date:** 2026-09-03 · **Status:** accepted · **Amends:** D-079b · **Applies:** P-001 · **Resolves:** final review, finding 6

**TL;DR.** `07` said a host declares eight things and no more; `10` said twenty keys
have no default and fail startup; P-001 said every setting carries a safe default.
The twenty split cleanly: six name the deployment and cannot be defaulted; fourteen
are durations, rates and policies with well-understood safe values. The fourteen now
have them, the six are required, and LIB-HOST-001 lists exactly the six.

**Decision.**

**Required — no safe default exists**, because the library cannot know where it runs:
`webauthn.origins`, `hosting.location`, `hosting.crossborderbasis` (only when outside),
`alerting.email.destinations`, `alerting.sms.destinations`, `abuse.sms.balancefloor`.
All six are in LIB-HOST-001's table; startup fails with a named error if any is unset.

**Defaulted — recommended-practice values at the safe end of their range**, so a host
that never touches the key gets a secure deployment and the only deliberate change is
a loosening, which the direction rule (OPS-CFG-002) audits:

| Key | Default |
|---|---|
| `session.stepup.recency` | 15 minutes |
| `stepup.policy.default` | reauthenticate, for every step-up action |
| `login.policy.<organization>` | the catalogue's non-administrative defaults; the administrative organization's passkeys-only policy is written at bootstrap |
| `webauthn.relatedorigins` | empty |
| `recovery.link.lifetime` | 1 hour |
| `breakglass.session.lifetime` | 4 hours, ceiling 12 |
| `abuse.sms.window` | 10 minutes |
| `abuse.botdefence.signals` | datacenter ranges, repeated attempts |
| `privacy.export.ratelimit` | 3 per day |
| `exfiltration.export.ratelimit` | 5 per hour |
| `exfiltration.readvolume.baselinewindow` | 30 days |
| `alerting.dedupe.window` | 1 hour |
| `email.change.coolingoff` | 72 hours |
| `password.blocklist.corpusmaxage` | 30 days |
| `retention.<category>` | the category's floor |

**Conservative means recommended, not punitive** — confirmed with the user. Each value
is one found in practice; none is tightened past usefulness.

**LIB-HOST-001's resource-types row** now says it includes the encrypted-field subject
columns (PRIV-RIGHT-005a), which were startup-blocking and unlisted. The lawful-basis
list is deliberately left to D-108.

**Rejected.**
- *Keep all twenty startup-blocking and list them* — consistent, but contradicts P-001
  and makes "wire and configure" a twenty-line prerequisite for values the library
  understands better than the host.

**Propagated to:** LIB-HOST-001 (`07`); `10` §4 preamble and tables.


---

## D-108 — Lawful bases are declared with properties; the code branches on the properties, never the name

**Date:** 2026-09-03 · **Status:** accepted · **Amends:** D-032, D-091, D-094 §94.3 · **Applies:** the D-012 property pattern · **Resolves:** final review, finding 7

**TL;DR.** `04` promised another jurisdiction could supply its own basis list with the
library unchanged; `08` said a different jurisdiction changes the branching logic and
is a code change however the values are stored. Both were right about their half,
because the code branched on names. It now branches on declared properties — the
same treatment factors have had since D-012 — and the promise holds.

**Where the library cares which basis it is** — four yes/no questions, nothing else:
is it consent (withdrawable, dashboard control, capture path); must consent be
written for sensitive data; does it require a linked assessment; can it be objected
to. Every other basis is a label the records of processing print.

**Decision.**

- **A host declares its jurisdiction's basis list at startup**, each entry carrying
  `IsConsent`, `RequiresWrittenConsentForSensitive`, `RequiresAssessment`,
  `IsObjectable`. **No rule anywhere names a basis.** Egypt's six ship as the default
  declaration with the flags below; a project elsewhere declares its own and changes
  no code.

  | Basis | IsConsent | Written for sensitive | RequiresAssessment | IsObjectable |
  |---|---|---|---|---|
  | Consent | yes | yes | no | no |
  | Contractual obligation | no | — | no | no |
  | Legal obligation | no | — | no | no |
  | Legitimate interest | no | — | yes | yes |
  | Claim or defence of a legal right | no | — | no | no |
  | Court judgment or order | no | — | no | no |

- **Storage is a library table seeded from the declaration**, not a check constraint.
  D-094's rule stands — a value the code branches on is a constrained column — and the
  code no longer branches on the value; it branches on the properties, which the table
  carries. That is precisely the case D-094 says warrants a table. Purposes reference
  the basis by key; the records of processing emit its label. Startup validates that
  every purpose's basis is in the declared list.
- **Sensitive-data categories** are a declared list in the same way. Nothing branches
  on them, so they carry labels only; Egypt's list ships as the default.
- **Deadlines** were already configuration keys (D-107) and are unchanged.

**Why this and not the concession.** Conceding "another jurisdiction is a code change"
was honest and cheaper today, but it gave up the generic claim on the one axis most
likely to differ between countries, and it left a name-branching pattern the rest of
the design deliberately avoids.

**Rejected.**
- *Concede the code change* — reasons above.
- *Keep the check constraint and add properties to code* — the constraint would still
  be a library migration per jurisdiction, which is the code change by another route.

**Propagated to:** PRIV-BASIS-001 to 004, PRIV-SENS-001 (`04`); CONV-ENUM-001 (`08`);
LIB-HOST-001 (`07`); `10` §5.7, §5.9.


---

## D-109 — Phase 1 is open-ended, and the documents say so

**Date:** 2026-09-03 · **Status:** accepted · **Amends:** D-044 · **Origin:** user · **Resolves:** final review, finding 8

**TL;DR.** `12` §8 called Phase 2 "a launch gate" while DR-004 and the outstanding
actions said post-launch; and the risk register described the same-host period as
"bounded by the tier upgrade" when the upgrade has no date. The contradiction is
fixed and the acceptance is recorded as what it is: open-ended.

**The user's position, recorded.** One VPS per project now. After launch, once things
are stable, the plan is to migrate to a VPS tier with object and block storage, at
which point backups move off the host. **When that happens is not known**, so no date
can be set — and writing one into the specification would invent a commitment that
has not been made. Time and budget are the constraints.

**Decision.**
- `12` §8 corrected to post-launch.
- DR-005 keeps the event trigger and states that the event has no date.
- R-A01 and RISK-001 say **open-ended until the tier upgrade** rather than "bounded
  by" it. The "revisit as soon as schedule allows" wording stands; the register no
  longer implies a bound it does not have.

**Rejected.**
- *A date bound* ("the tier upgrade, or N days after launch, whichever first") —
  proposed by the reviewer, declined by the user: the upgrade date is unknown, so a
  bound would be fiction.
- *Off-host storage at launch* — already declined twice (D-044); not reopened.

**Noted, not decided.** DR-016 defers the erasure ledger because "before the tier
upgrade there is nowhere off-host". Since D-105 the secrets manager is an off-host
store the application already reaches. Whether a handful of ledger lines a year could
live there now, closing R-A13 early, is left for the user to raise if wanted.

**Propagated to:** DR-005, `12` §8; R-A01, RISK-001 (`13`).


---

## D-110 — The quarterly restore test is automated; dependency updates are event-driven; the human exceptions stay at two

**Date:** 2026-09-03 · **Status:** accepted · **Amends:** D-044, D-046 · **Resolves:** final review, finding 9

**TL;DR.** DR-007's quarterly timed restore and CONV-DEP-004's "updates on a schedule"
were recurring human tasks the set did not count among its two exceptions. The
restore test is now an automated job that alerts on failure; dependency updates are
reworded as event-driven. The claim of two exceptions holds.

**Decision.**

**Quarterly restore test — automated.** A scheduled job, at least quarterly:
1. Restores the latest base backup and log to a **separate, throwaway database
   instance** — never over the running database (DR-008). In Phase 1 a second
   PostgreSQL container on the same VPS satisfies that; no second server is needed.
2. Decrypts a canary subject's personal field with the live KEK, and verifies a
   canary account's fingerprint resolves — proving keys and data restored together.
3. Records the elapsed time against the recovery-time objective.
4. Tears the instance down.
5. Alerts on any failure or on time exceeding the objective (OPS-ALERT-001,
   background job failure).

**The annual escrow test stays manual** (DR-009b): its purpose is to use the envelope
rather than live access, which no job can do.

**Dependency updates — event-driven.** Vulnerability alerting already opens a change
when an advisory lands; other updates arrive as they are published. CONV-DEP-004 is
reworded from "on a schedule" to "as alerts and updates arrive, merged when checks
pass" — a habit at the point of a pull request, not a calendar entry.

**Rejected.**
- *Admit a third recurring human task* — honest and no build work, but it puts a
  quarterly afternoon on the one person whose availability is the constraint, for a
  check that is entirely automatable.

**Propagated to:** DR-007, DR-008 (`12`); INF-DB-004, INF-BG-001 (`19`); CONV-DEP-004
(`08`).


---

## D-111 — Admin-assisted recovery covers customers; the enrolment link may travel by SMS; recovery sets or resets the password

> **Superseded in part.** The host business rules and vendor facts this entry records (orders, payments, shipments, couriers, addresses) moved to the host under D-165; the reasoning stands as the host's inheritance.

**Date:** 2026-09-03 · **Status:** accepted · **Amends:** D-008, D-009, D-013, D-035, D-067 · **Resolves:** final review, finding 10

**TL;DR.** A customer whose old mailbox was gone could never change their email
(both addresses must confirm), and if they then lost their password the account was
gone with its order history. A customer whose only sign-in method was a lost
device-bound passkey had no defined recovery either. Both now have the path staff
already have.

**What was wrong.** D-067 rejected a second factor on email change so that a customer
"whose old mailbox is lost or compromised" would still have a route — and the
both-addresses rule (D-035) removed that route anyway. AUTH-RECOV-002 issued an
enrolment link without saying to whom it applied or where it went; the only other
recorded channel is the phone, and D-013 restricts SMS to verification. AUTH-RECOV-001
pointed a locked-out customer at "the delayed downgrade path", which removes a second
factor, not a lost primary one.

**Decision.**

- **Admin-assisted recovery applies to every account**, not only staff. Identity is
  confirmed on a channel already recorded (AUTH-RECOV-003); for a customer with a dead
  mailbox that is the phone.
- **The enrolment link MAY be delivered by SMS.** This does not make SMS an
  authentication factor: the control is the approver's out-of-band confirmation on a
  recorded channel, the link is the same time-boxed single-use artefact staff recovery
  uses, and nothing lets a phone alone sign anyone in. INT-SMS-001 and AUTH-FACT-002
  carry one clause saying so.
- **The re-enrolment session may set a new email address confirmed by the new address
  only** where the old one is unreachable — the approver's confirmation stands in for
  the old address. The old address is still notified (IDN-LIFE-006), the cooling-off
  window still runs (IDN-LIFE-007), and other sessions still terminate.
- **Recovery sets or resets the password** (AUTH-RECOV-005) and never removes a second
  factor. A passkey-only customer who loses the passkey recovers by setting a password
  — the security level every customer already has, since email was always their
  recovery channel. AUTH-RECOV-001's "delayed downgrade path" wording is corrected.
- Administrative-organization members are unchanged: no email or SMS route
  (AUTH-RECOV-004); their link goes through the approver's out-of-band process only.

**Rejected.**
- *A lost mailbox means a new account* — honest and nothing to build, but it strands a
  health-data order history in an unreachable account, turns a mishap into an erasure
  request, and leaves D-067's rationale describing a route that does not exist.

**Propagated to:** AUTH-RECOV-001, AUTH-RECOV-002, AUTH-RECOV-005, AUTH-FACT-002 (`02`);
IDN-LIFE-004, IDN-LIFE-005 (`01`); INT-SMS-001 (`05`); `09` `/admin/recovery/approve`;
`11` §4.


---

## D-112 — One verified phone per account; duplicates handled as duplicate email is

**Date:** 2026-09-03 · **Status:** accepted · **Extends:** D-013, D-076 · **Resolves:** final review, finding 11

**TL;DR.** Every registration requires a phone and nothing said whether one number
could verify many accounts, or what happened when it already belonged to someone.
Now: one verified number per account, and a duplicate follows the D-076 pattern —
no second account, the ordinary response, the owner notified without a code.

**Why uniqueness.** The anti-abuse premise (D-013) is that phone verification costs an
attacker something per account. Unlimited accounts per number makes that cost zero
after the first SIM. Since D-111 the phone is also a recovery channel, which a shared
number cannot serve without ambiguity.

**Decision.**
- **A verified phone number belongs to at most one account.**
- **A registration or phone change naming a number already verified elsewhere**
  creates no second account and no change, and returns the ordinary accepted response
  — byte- and timing-identical to a fresh one.
- **The number receives a notification, never a verification code:** "someone tried
  to register with this number; if this was not you, nothing has changed." The
  requester can complete nothing and learns nothing. The notification is
  non-suppressible (D-022) and rate-limited per destination like the non-existence
  email (D-079b).
- **Households sharing a phone** are already covered by the design's own answer for
  dependants: the account holder orders on their behalf (D-027).

**Rejected.**
- *Several accounts per number, up to a cap* — every account past the first costs an
  attacker nothing, the cap becomes the per-SIM budget, and a shared number cannot
  serve as a recovery channel unambiguously.

**Propagated to:** IDN-LIFE-001, IDN-LIFE-010 (`01`); AUTH-ABUSE-003 (`02`); `09`
`/register`, `/account/phone/change`; `10` (no new code — the response is the
ordinary one).


---

## D-113 — Self-service deletion is the erasure right; the queue is for out-of-band requests

**Date:** 2026-09-03 · **Status:** accepted · **Amends:** D-037 · **Extends:** D-026.1, D-106 · **Resolves:** final review, finding 12

**TL;DR.** `POST /account/delete` and the erasure request path existed side by side
with nothing saying how they related — whether self-service deletion created a queue
entry, whether erasure needed "human review" the customer could not see, and whether
the grace window applied to both. Now: self-service deletion **is** the exercise of
the erasure right, with its own configurable grace window; the queue keeps erasure
only for requests that arrive out of band.

**Decision.**

- `POST /account/delete` moves the account to `deleting` and starts
  `account.deletion.grace` — new key, **default 30 days**, floor enforced, matching
  `organization.deletion.grace`. The deletion notification carries the cancellation
  link (`POST /account/delete/cancel`, D-106). Erasure (IDN-LIFE-014, PRIV-RIGHT-005)
  runs when the window elapses
- No privacy request is created for a self-service deletion. The customer is
  identified by their session; there is nothing for a human to review and no
  six-working-day clock to run
- The privacy-request queue carries erasure only for **out-of-band** requests — a
  letter, a support email, a guardian. An authorised human confirms the requester's
  identity, enters the request through `POST /admin/privacy/requests`, and fulfilment
  starts the same grace window. The six-working-day acknowledgement (PRIV-RIGHT-002)
  applies to these
- Restriction stays on the queue: it is a dispute, and deciding it is human work
- D-037's "human review" for erasure is re-scoped to **identity confirmation for
  out-of-band requests**. PRIV-RIGHT-001 AC1 no longer excepts erasure and
  restriction — every right has a mechanism requiring no support contact

**Rejected.**

- *Route self-service deletion through the queue and acknowledge it within six days.*
  Pointless ceremony for a customer who has already proved who they are, and it made
  the grace window and the acknowledgement clock overlap in unexplained ways
- *Drop erasure from the queue entirely.* Requests do arrive by letter and from
  guardians; the law does not require the subject to have a working login

**Propagated to:** `01` IDN-ACCT-007, IDN-LIFE-014 · `04` PRIV-RIGHT-001 · `09`
`POST /account/delete`, `POST /privacy/requests`, §8a
`POST /admin/privacy/requests` · `10` §4.6 `account.deletion.grace`.

---

## D-114 — A short password is bought with a second factor

**Date:** 2026-09-03 · **Status:** accepted · **Extends:** D-011, D-009, D-092 · **Resolves:** final review, finding 13

**TL;DR.** `POST /register` took the password before any second factor existed, so
either the 10-character floor was reachable by single-factor accounts (violating
AUTH-PASS-001) or FE-REG-003 described a flow that did not exist. Now the rule is a
promise: **a password below fifteen characters may exist only on an account holding
a second factor.** At registration a short password makes the security step
mandatory; the account is not created until it is done.

**Decision** — the user's, improved on the reviewer's proposal.

- The password step presents both floors and the trade before the person types:
  fifteen alone, ten with a second sign-in method in the next step. A 10–14
  character password makes second-factor enrolment a required step. The wizard
  allows going back to lengthen the password and skip enrolment
- The account leaves `pending` only when every requirement is met (IDN-ACCT-007a).
  A registration that sets a short password and never enrols is simply incomplete —
  it cannot sign in, and nothing provisional exists anywhere
- The same rule applies at `POST /account/password`: ten with a second factor
  currently enrolled, fifteen without
- MFA removal is unchanged — AUTH-RECOV-007a already forces a password change at
  next sign-in on a short-password account
- Whether the stored password meets the single-factor floor is recorded with the
  hash, since it cannot be recomputed later
- Email authenticators do not count (D-009)

**Why this and not "always fifteen at registration."** The reviewer proposed judging
the floor by the factors held when the password is set, which at registration is
always fifteen. That is simpler but throws away D-011's reason for the lower floor
— high floors drive pattern reuse — and gives up a nudge toward MFA. The promise
keeps both: the shorter floor is available, and every account using it has a second
factor by construction.

**On D-009's "hard blocks reduce MFA adoption."** This is a hard gate, but one the
person chooses — anyone who does not want MFA types fifteen characters. Recorded so
the tension is visible rather than argued about later.

**Rejected.**

- *Always fifteen at registration.* Above
- *Move the password to a step after the security step.* Changes the `/register`
  shape and adds state for a modest gain
- *Provisional acceptance with a forced change at first sign-in.* Doubles the rules
  and knowingly violates AUTH-PASS-001 in the window

**Propagated to:** `02` AUTH-PASS-001a · `01` IDN-ACCT-007a · `09` `POST /register`,
`PUT /register/password`, `POST /account/password` · `18` FE-REG-003.

---

## D-115 — The canonical form is `NFKC_Casefold`; digits and punctuation are not a script

**Date:** 2026-09-03 · **Status:** accepted · **Extends:** D-040, D-093 · **Resolves:** final review, finding 14

**TL;DR.** Identifiers were normalised "to a single canonical Unicode form" and
fingerprinted over "a pinned, versioned canonical form", but no document named the
form. Because the form is applied before the keyed fingerprint (D-093), it is baked
into every row and a later change is a decrypt-and-re-derive across every subject.
It had to be chosen before the first registration, and the builder was choosing it
alone.

**Decision.**

- **`NFKC_Casefold`** (Unicode Standard §3.13) for email addresses, organization
  names and display names: compatibility normalisation, full case folding and
  removal of default-ignorable code points in one named operation. It folds
  fullwidth and other lookalike forms that NFC leaves distinct, which is the
  problem IDN-ACCT-004 exists for
- **Phone numbers** take no Unicode form: digits of any script mapped to ASCII,
  then E.164
- The **Unicode version** of the implementing library is pinned in the build and is
  the canonicalisation version stored beside each fingerprint. Changing it fails
  startup until the re-derivation has run
- **Mixed-script detection** follows **UTS #39 §5.1**: `Common` and `Inherited`
  characters are ignored, so `O'Brien`, `Jean-Luc` and `محمد2` are accepted and
  `Аhmed` is still rejected

**Rejected.**

- *NFC plus invariant lowercase.* Cheapest with .NET built-ins, but leaves fullwidth
  and compatibility lookalikes distinct — a weaker answer to the stated problem
- *PRECIS `UsernameCaseMapped` (RFC 8265).* Well specified, NFC-based, forbids
  rather than folds, and adds a dependency for little gain. *Rejected as the canonical
  form only; D-146 later adopted PRECIS as the validation profile for usernames and
  display names, and D-154 states that both hold*
- *Leave it to the builder.* Costs nothing now; risks the recompute-everything
  scenario later

**Propagated to:** `01` IDN-ACCT-004, IDN-ACCT-005 · `04` PRIV-RIGHT-005c.

---

## D-116 — Policy follows membership: system policy for individuals, organization policy for members

**Date:** 2026-09-03 · **Status:** accepted · **Amends:** D-002, D-020.1 · **Extends:** D-086 · **Resolves:** final review, finding 15

**TL;DR.** Two sentences said whose authentication policy applies. The older one
resolved it "from the organization owning the resource or action"; the newer one
(D-086) from the principal's membership, with a default for those holding none.
With one organization they agreed; with two they could contradict, and
AUTH-STEP-001's "a second organization sets its own step-up policy independently"
was untestable. The user's rule replaces both.

**Decision** — the user's.

- **Individual users** (no membership) follow the **system policy**
- **Organization members** follow **their organization's policy**, which inherits
  the system policy for anything not overridden
- **Several memberships** (multi-membership, built and switched off): the
  **strictest** of their organizations' policies applies — the one addition, so
  nothing is undefined if the switch is ever turned on
- Today the only organization is the administrative organization
- Applies to login policy and step-up policy alike; the two remain configured
  independently (D-086)

**Why membership, not resource.** The resource-based wording is what produced the
customer lockout: everything traces to some organization for authorization purposes
(AUTHZ-MODEL-004), so "the owning organization's policy" read as the staff policy
for a customer editing her own email. Authentication policy is a property of who
the person is in the system, not of what they touch. Authorization — what they may
do to a resource — still resolves from the resource (IDN-MEM-003), unchanged.

**Propagated to:** `02` AUTH-PRIN-002, AUTH-STEP-001, AUTH-STEP-002a · `10` §4
`stepup.policy.default`, `stepup.policy.<organization>`, `login.policy.default`,
`login.policy.<organization>`.

---

## D-117 — Erasure is key destruction; business records are outside the erasure right

> **Superseded in part.** The host business rules and vendor facts this entry records (orders, payments, shipments, couriers, addresses) moved to the host under D-165; the reasoning stands as the host's inheritance.

**Date:** 2026-09-04 · **Status:** accepted · **Amends:** D-093 §93.6 (M-4 superseded) · **Extends:** D-097, D-102, D-108 · **Resolves:** final review, finding 16

**TL;DR.** PRIV-RIGHT-005's acceptance test promised that after erasure "no
remaining field or combination permits singling out the subject", while the same
document retained order rows and said a lone customer in a district buying a
specific product is identifiable. D-093 accepted the contradiction as a permanent
residual; that never reached `04` or `13`. The user's position replaces the residual
framing: the retained rows are business records, not the subject's personal data
to be erased.

**Decision** — the user's, checked against the law.

- Erasure destroys the subject's key. Every personal field — name, phone, email,
  address, photo — becomes unrecoverable. That is the whole of the erasure
  obligation
- Order and transaction records are **business records** retained under their
  declared lawful basis and retention period: legal obligation (VAT/tax law — books,
  records and invoice copies kept five years from the end of the fiscal year) or
  legitimate interest. They are outside the erasure right
- No singling-out test applies to them. Acceptance criterion 1 is rewritten: no
  personal field recoverable; retained business records unaffected
- The M-4 residual is superseded; no risk-register entry

**Verified 2026-09-04.** PDPL (Law 151/2020) Article 4(7): the controller deletes
personal data on satisfaction of the purpose, but may retain it "for any legitimate
reason" provided it is "retained in a form that does not allow the identification of
the Data Subject". Executive Regulations (Decree 816/2025): sector laws mandating
retention override the deletion timeline; legally retained data is rendered
non-identifiable and deleted when the justification ceases. five-year record
retention — now in the Unified Tax Procedures Law 206/2020 (VAT Law 67/2016 Art. 13,
originally cited, was repealed by it; corrected D-140). GDPR Article 17(3)(b) and (e) are the comparable exceptions
(legal obligation; legal claims).

**The user's framing, adjusted by one point.** "If it could lead to the person, it
is not our concern" is not what the law says — it says retained records must not
identify the person. The design already satisfies that: key destruction leaves a
receipt with no buyer on it, which is the form tax law itself requires of consumer
receipts. The theoretical district-plus-product inference was a test the earlier
spec text imposed on itself, not one the statute's "does not allow identification"
standard requires.

**Propagated to:** `04` PRIV-RIGHT-005.

---

## D-118 — The audit partition drop runs in the worker through a one-purpose `SECURITY DEFINER` function

**Date:** 2026-09-04 · **Status:** accepted · **Extends:** D-018, D-075, D-105 · **Resolves:** final review, finding 17

**TL;DR.** Expired audit partitions were dropped "by a scheduled job running under
the migration credential" — but that credential exists only in the pipeline and is
never present in the application, so the job had nowhere to run except an unstated
scheduled CI workflow with no alert path. Now: the migration step creates one
function, `audit_drop_expired_partitions()`, marked `SECURITY DEFINER`; a dedicated
maintenance role may execute it and nothing else; the worker calls it on schedule
with that role's credential from the secrets manager.

**Decision.**

- One function, created by migration, owned by the migration role, doing exactly
  one thing. `SECURITY DEFINER` runs it with the owner's rights; the caller has none
- A third database credential, **maintenance**: `EXECUTE` on maintenance functions
  only; no `DROP`, `ALTER` or `CREATE` of its own. Fetched by the worker from the
  secrets manager beside the KEK; never in application configuration; not
  escrowed, since a migration recreates it
- The job lives in the ordinary scheduler (INF-BG-001), so a missed run alerts, and
  the drop is audited as the job's own action (INF-BG-002)
- OPS-MIG-003's two-credential rule stands; OPS-MIG-003a adds the third with its
  limits

**Rejected.**

- *A scheduled GitHub Actions workflow holding the migration credential.* Works, but
  it is a second scheduler with a silent-failure mode (a disabled schedule, spent
  minutes) and no alert path into OPS-ALERT-001
- *`pg_cron` inside the database.* Another extension and another scheduler for one
  job; alerting still has to be built

**Propagated to:** `04` PRIV-RET-002 · `06` OPS-MIG-003a · `19` INF-BG-001,
INF-HOST-003 table.

---

## D-119 — The specification is complete on its own; the log ships as rationale

**Date:** 2026-09-04 · **Status:** accepted · **Resolves:** final review, finding 18

**TL;DR.** `00` §9 said SHOULD departures are recorded "in the decision log" and
"where this specification and the decision log conflict, the log wins" — both
assuming the builder has the log, which nothing stated. The user's rule settles it
and goes further: the log ships, but the specs must be understandable without it.
Anything a builder needs the log to understand is a defect in the specs.

**Decision** — the user's.

- Documents `00`–`19` are **complete on their own** and **authoritative for what is
  built**
- `decision-log.md` ships alongside as rationale — why, and what was rejected. It
  is never required to build
- A builder who must consult the log to understand a requirement has found a
  **specification defect**: report it; the spec is corrected
- Spec–log disagreement is a defect to raise, resolved by correcting whichever
  document is wrong — never by building from the log. "The log wins" is withdrawn
- SHOULD departures are still recorded in the log
- Housekeeping: `review-findings.md` removed from `00` §1.1 (never in the set); the
  log header no longer says "in progress … spec rewritten later from this log"

**Verified 2026-09-04, after the fact.** The claim in §9 was first written and only
then checked — the wrong order, noted here. Every inline D-reference outside the
`*Source:*` lines was read in context across all twenty documents (about 280 of
them, heaviest in `10`, `13`, `15`). All but three are citations beside substance
the spec states itself. The three that sent a builder to the log were fixed: `01` §8
("closed in the decision log"), `12` §8 ("tracked in the decision log's outstanding
actions"), and `12`'s introduction, which named D-019 without saying what it was.
Historical asides of the form "corrected per D-0xx" remain; they explain lineage and
are not needed to build.

**Propagated to:** `00` status line, §1.1, §9 · this log's header.

---

## D-120 — Argon2id parameters and the WebAuthn algorithm allow-list are pinned and configurable

**Date:** 2026-09-04 · **Status:** accepted · **Extends:** D-026.3, D-034, D-107 · **Resolves:** final review, finding 19

**TL;DR.** AUTH-PASS-007 required "versioned parameters" without giving any, and
AUTH-FACT-014's allow-list defaulted to the words "modern set". Both were constants
a consumer might reasonably want different (P-001 "configurable by default"), and
both are values where a weak guess weakens security without anything visibly
breaking. Now both are configuration keys with verified defaults and floors.

**Decision.**

- `password.argon2.memory` / `.iterations` / `.parallelism` — default **19 456 KiB,
  2, 1**. Floor: the OWASP minimum; below it is refused at startup. Raising any value
  bumps the hash version and triggers the transparent rehash already specified. The
  19 MiB set is chosen over the equal-strength 46 MiB set because several
  applications share one host and a sign-in burst is bounded by memory
- `webauthn.algorithms` — default **[−8, −7, −257]** (EdDSA, ES256, RS256) in that
  preference order; protected, as before; **−7 cannot be removed**, so the list can
  never be emptied or fall below what every authenticator supports

**Verified 2026-09-04.** OWASP Password Storage Cheat Sheet lists Argon2id
configurations of equal strength: m=47104 t=1 p=1; m=19456 t=2 p=1; m=12288 t=3 p=1;
m=9216 t=4 p=1; m=7168 t=5 p=1. W3C WebAuthn Level 3: the registration example
"will accept either an EdDSA, ES256 or RS256 credential, but prefers an EdDSA
credential"; when `pubKeyCredParams` is empty the client defaults to ES256 and RS256.

**Propagated to:** `02` AUTH-PASS-007, AUTH-FACT-014 · `10` §4.2, §4.3.

---

## D-121 — Three orphaned alerts get their rows; the threat model points at the table

**Date:** 2026-09-04 · **Status:** accepted · **Extends:** D-048, D-071, D-089 · **Resolves:** final review, finding 20

**TL;DR.** OPS-ALERT-001 promises that every "must alert" sentence elsewhere has a
row in its table. Three did not: the duplicate-identifier notification rate
(AUTH-ABUSE-003, added by D-079b), approaching privacy-request deadlines
(PRIV-RIGHT-002), and licence or permit expiry (OPS-MAINT-001). `15` §6.5 still said
"seven conditions" against a seventeen-row table and repeated the narrow hard-stop
exemption D-089 corrected.

**Decision.**

- Three rows added, all **Normal** severity — email, not SMS — using the existing
  channels and deduplication
- Two lead-time keys: `privacy.request.warninglead` (default **2 working days**
  before the acknowledgement deadline) and `maintenance.expiry.warninglead` (default
  **30 days** before expiry)
- `15` §6.5 no longer counts the table; it names OPS-ALERT-001 as the single
  authoritative list and carries the corrected exemption wording ("all alert-class
  sends")

**Propagated to:** `06` OPS-ALERT-001, OPS-MAINT-001 · `04` PRIV-RIGHT-002 · `02`
AUTH-ABUSE-003 · `10` §4.5, §4.7 · `15` §6.5.

---

## D-122 — Privacy requests are acknowledged automatically on receipt

**Date:** 2026-09-04 · **Status:** accepted · **Amends:** D-037, D-121 · **Extends:** D-113 · **Resolves:** final review, finding 21

**TL;DR.** The six-working-day statutory clock is on *acknowledgement*, and the
queue put acknowledgement on a human click. With one operator, an absence of a week
was a missed legal deadline with nobody in the loop. Now the system acknowledges the
instant a request enters the queue; the human owes only fulfilment.

**Decision.**

- Acknowledgement is automatic and equals creation time — at submission for
  in-app requests, at entry for out-of-band requests (the human is present anyway).
  The subject is notified of receipt
- `POST /admin/privacy/requests/{id}/acknowledge` is removed; `acknowledgementDue`
  becomes `acknowledgedAt`
- The D-121 alert is repurposed from "acknowledgement deadline approaching" to
  "request still open beyond `privacy.request.openwarning`" (default six working
  days), so the human is prompted about the work, not the receipt
- Residual, stated in PRIV-RIGHT-002: a paper letter that arrives while nobody can
  enter it is a business-availability matter, not a library control

**Propagated to:** `04` PRIV-RIGHT-002 · `09` `POST /privacy/requests`, §8a · `06`
OPS-ALERT-001 · `10` §4.7.

---

## D-123 — Session lifetimes follow the policy, not the sign-in method; customers are never signed out on a calendar; staff never lose their work

**Date:** 2026-09-04 · **Status:** accepted · **Amends:** D-033.1, D-086 §86.6 · **Resolves:** final review, finding 22; UX review items 1–3

**TL;DR.** Lifetimes were derived from the assurance level a sign-in *reached*, so a
customer using a passkey got the staff lifetimes (1 h idle / 24 h) and a customer
using a password got 30 days — the best method earned the most log-outs. And the
30-day customer sign-in itself, a NIST SHOULD written for government services, is
not what any consumer service does. Staff expiry also had no rule for the page they
were on. Reviewed against NIST SP 800-63B-4 (2026-09-04): every figure involved is a
SHOULD or a MAY.

**Decision** — the user's, on the reviewer's options.

- **Lifetimes follow the level the policy requires** (AUTH-PRIN-002), never the level
  reached. The session still records what was reached for step-up and the staff floor
- **Customers (system policy, AAL1): no absolute lifetime.** The session lasts until
  signed out, revoked, or unused for `session.default.inactivity` — default **90
  days**, refreshed by use, ceiling 365 days. Security rests on the controls that
  matter: instant server-side revocation, rotation, step-up for account changes,
  other sessions ended on email or password change, device list with sign-out
  everywhere, anomaly alerting. This is the consumer-industry model
- **Staff (administrative organization, AAL2): 24 h absolute, 1 h idle**, both
  configurable up to enforced ceilings. After an idle expiry inside the 24 h window,
  one eligible factor bound to the session secret restores it — a passkey tap — as
  NIST §2.2.3 permits
- **Expiry never loses work.** FE-API-004 now covers `auth.session.expired`:
  reauthenticate in place, retry with data intact, never a redirect to a sign-in
  page. `details.reauthenticate` tells the frontend whether one factor or a full
  sign-in is needed
- **Step-up recency stays at 15 minutes.** Examined; the value is right, and the
  no-lost-state rule is what removes the pain

**Verified.** NIST SP 800-63B-4 §2.1.3: at AAL1 an overall timeout SHALL exist and
SHOULD be ≤ 30 days; an inactivity timeout MAY apply. §2.2.3: at AAL2 the overall
timeout SHOULD be ≤ 24 h, the inactivity timeout SHOULD be ≤ 1 h, and after an
inactivity timeout the verifier MAY accept a single factor in conjunction with the
session secret. The AAL1 "SHALL establish a definite overall timeout" is met by the
90-day inactivity expiry, which is definite.

**Rejected.**

- *Keep 30 days for customers.* A calendar sign-out no consumer service imposes;
  buys little against an opaque, instantly revocable cookie
- *Drop the staff idle timeout.* Staff see health data; the idle timeout protects a
  walked-away desk. The fix is not losing work, not removing the control
- *Lifetimes by level reached (status quo).* Punishes the passkey user

**Propagated to:** `02` AUTH-SESS-005, AUTH-SESS-005a · `10` §4.1, §1 `auth.session.expired`
· `18` FE-API-004 · `17` BFF-STEP-001 · `00` §1.2 example.

---

## D-124 — Trusted devices: customers may skip the second factor on a browser they trust

**Date:** 2026-09-04 · **Status:** accepted · **Extends:** D-009, D-012, D-123 · **Resolves:** UX review item 4

**TL;DR.** A customer with password + TOTP was asked for the code at every sign-in;
there was no "remember this device". That is the most common reason people turn MFA
off, and a customer without MFA is worse off than one with MFA and a trusted device.

**Decision.**

- After a completed two-factor sign-in, a principal whose policy does **not**
  require AAL2 may trust the browser for `factor.trusteddevice.lifetime` (default
  **30 days**, ceiling 90). The second factor is then skipped on that browser; the
  password is always still required
- Mechanics: a separate opaque device token in its own `__Host-` cookie, stored
  server-side with label, created-at and last-used; it satisfies only the sign-in's
  second factor, raises no session assurance property, and never satisfies step-up
- Listed in `GET /account/devices` alongside sessions; removable there. All trusted
  devices are cleared on password change, recovery, "sign out everywhere", and when
  the account leaves `active`
- **Not offered to the administrative organization**: passkeys need no second factor
  to skip, and the AAL2 floor (AUTH-SESS-005b) forbids the shortcut
- Accepted position recorded as R-A14

**Rejected.**

- *Offer it to staff too.* Would let a device-trust cookie stand in for the second
  factor of a health-data administrator — the floor exists to prevent exactly this
- *No device trust.* Keeps the code-every-time friction that drives MFA abandonment

**Propagated to:** `02` AUTH-FACT-015 · `09` `POST /auth/factor`, `GET/DELETE
/account/devices` · `10` §4.3 · `13` R-A14.

---

## D-125 — Housekeeping pass: small inconsistencies and two legal precisions

> **Superseded in part.** The host business rules and vendor facts this entry records (orders, payments, shipments, couriers, addresses) moved to the host under D-165; the reasoning stands as the host's inheritance.

**Date:** 2026-09-04 · **Status:** accepted · **Resolves:** final review, findings 23 and 24

**TL;DR.** Eleven small inconsistencies and two points of legal precision, each
with one correct fix, applied in a single pass rather than one at a time.

**Finding 23 — inconsistencies fixed.**

1. `POST /auth/factor` returned **401** for a rejected factor; API-CONV-003 reserves
   401 for session death. Now **422** `auth.factor.rejected` (`09`)
2. "Deactivation" was not a state. Defined: deactivation is the account's own entry
   into **`suspended`** — the same state administrative suspension produces, differing
   only in who initiated it (`01` IDN-LIFE-013, `09` `/account/deactivate`)
3. CONV-LAYOUT-002 AC1 contradicted its own text about `Janus.Hosting`; the criterion
   now names both projects. `Janus.Cli` now depends on Identity and Authentication as
   well, since it creates an organization, an administrator, an enrolment link and a
   break-glass credential (`08`)
4. IDN-AUD-001 required "organization" on events for principals who hold none. The
   field is nullable; absence is the recorded fact (`01`)
5. `11` §7.1 called the held MFA removal "silent"; it is flagged (AUTH-RECOV-007 AC3)
   and surfaces as a degradation alert. Reworded
6. The SMS-gateway HTTPS confirmation was a first step everywhere while every document
   said "Open items: None". It is now the set's **one declared open item**, in `19`
   §11 and R-A12, closed at integration time
7. D-093 M-2's dangling "see open item below" now points at a new accepted position
   **R-A15** (`13`): a compromised customer session can browse that customer's own
   order history; export is rate-limited and account changes need step-up
8. AUTH-SESS-005a's "Social + TOTP = AAL2" row now says it is reached only by a later
   step-up, since sign-in never challenges a social principal (`02`)
9. Two unmeasured triggers made measurable: AUTHZ-SEAM-001's reverse-lookup bound is
   **`authz.reverselookup.budget`, default 2 seconds** (`03`, `10` §4.5a, R-A09
   trigger row); R-A04's "material share" is **more than half of a calendar month's
   orders** through the system (`13`). Both figures are chosen, and say so. `00` §7.3
   and `07` now carry the reverse-lookup trigger `03` already had
10. AUTH-STEP-002's "standards grounding" cited NIST's single-factor-plus-session-secret
    clause as if it covered gating sensitive actions; it covers reauthentication after
    an inactivity timeout (now used for exactly that in AUTH-SESS-005). Restated as
    this design's own extension of the clause (`02`)
11. R-O03 pointed at "the decision log's outstanding actions" — a builder must not
    need the log (D-119). Now tracked in the register itself (`13`)

**Finding 24 — legal precision.**

- **Genetic data** added to the default sensitive-category list, per Law 151/2020
  Article 1 (`04` PRIV-SENS-001, `10` §5.9)
- The three-day subject-notification clock: Article 7 counts it **from notification
  to the Centre**; this specification counts both clocks from **detection**, which is
  stricter and always compliant. Now recorded as a deliberate choice in `04`
  PRIV-BREACH-001 and `11` §8.3 so the operator knows the legal slack exists in an
  incident rather than believing it does not

**Not changed.** `16` step 2 ("Deactivate the account") reads correctly once
deactivation is defined in `01`; `14` references `04`'s rule and needs no separate
note.

**Propagated to:** `01`, `02`, `03`, `04`, `07`, `08`, `09`, `10`, `11`, `13`, `19`,
`00` §7.3 · this log, D-093 §93.6 M-2.

---

## D-126 — The six working days are a decision deadline; lapse is a deemed rejection

**Date:** 2026-09-04 · **Status:** accepted · **Amends:** D-122, D-037, D-121 · **Resolves:** review-2, H-1

**TL;DR.** D-122 read Law 151/2020's six working days as a deadline to *acknowledge*
and made acknowledgement automatic, taking the human off the clock. The statute
(Article 2) sets six working days to **decide**, and "the lapse of the mentioned
period without any decision shall be considered a rejection." A receipt is not a
decision. As specified, the only alert fired on day six — the day the request was
already deemed refused. The misreading was the reviewer's own (D-122) and is
corrected here.

**Decision** — the user's, on the reviewer's recommendation.

- Six working days is the **decision** deadline (`privacy.request.decision`, ceiling
  = the statutory period). The automatic receipt stays — harmless and courteous —
  and is named a receipt, not an acknowledgement
- **Normal** alert `privacy.request.warninglead` (2 working days) before the deadline;
  **High** on the deadline day
- **Restriction undecided at the deadline is granted automatically** and recorded
  *granted by lapse*: restriction suspends action, never visibility or data, so
  granting is always safe, and a deemed rejection becomes a granted request
- **Out-of-band erasure undecided at the deadline is recorded *deemed refused by
  lapse***; the subject is told honestly, invited to resubmit, and informed of the
  right to complain. Erasure never runs without a human confirming identity
- The dead key `privacy.request.acknowledgement` and D-122's `openwarning` are
  replaced by the two keys above

**Verified.** Law 151/2020 Article 2 (ILO English translation): response within six
working days; lapse without decision deemed a rejection. Confirm with counsel that
"working days" and the deemed-rejection reading hold under Decree 816/2025.

**Rejected.** *Alerts only, no automatic restriction.* Strictly worse for the customer
with no security gain.

**Propagated to:** `04` PRIV-RIGHT-002 · `09` `POST /privacy/requests`, §8a · `06`
OPS-ALERT-001 · `10` §4.7 · `00` §6.

---

## D-127 — Takedown stops access now and erases after a seven-day window; one description in all three documents

> **Amended.** A reversal restores the state the takedown found; a takedown of an account already in its own deletion window erases at the earlier of the two instants (D-166).

> **Superseded in part.** The host business rules and vendor facts this entry records (orders, payments, shipments, couriers, addresses) moved to the host under D-165; the reasoning stands as the host's inheritance.

**Date:** 2026-09-04 · **Status:** accepted · **Extends:** D-039, D-102, D-113 · **Resolves:** review-2, H-2

**TL;DR.** The minor takedown was described three ways: `01` had key destruction
inline in the suspension transaction, `09` never mentioned it, and `14` placed the
data removal "in the shop's system". The result — a `suspended` account with no key —
was a state the table did not describe, and IDN-LIFE-013's "reactivation restores
prior access exactly" was impossible for it. `14`'s own text ("a customer who must
re-register", "refund cancelled orders") assumed something reversible.

**Decision** — the user's, on the reviewer's recommendation.

- **Phase one, at trigger, one transaction:** suspend, end sessions, record trigger
  and reason, publish the outbox record for host-side order cancellation. Access and
  processing stop immediately
- **Phase two, after `takedown.grace` (default 7 days, floor enforced):** the
  ordinary erasure transaction (IDN-LIFE-003a) destroys the key and neutralises the
  fingerprint; the account is `deleted`. State: `active → suspended → deleting →
  deleted`
- **Reversible inside the window** by a holder of `takedown:execute`
  (`POST /admin/accounts/{subject}/takedown/reverse`): restores `active`; cancelled
  orders are not restored. For the adult who was misjudged
- `01`, `09` and `14` now say the same thing; `14` §3 is rewritten in two phases

**Rejected.** *Erase immediately.* One step, but a wrong call on an adult is
unfixable, `14`'s reversal and refund text would be false, and the
suspended-without-key state would need a new name.

**Propagated to:** `01` IDN-LIFE-003, IDN-LIFE-003b · `09` §8a takedown, new
`/takedown/reverse` · `10` `takedown.grace`, `identity.takedown.windowelapsed` ·
`14` §3, §4, §5, §6.

---

## D-128 — Step-up requires the strongest factor the account holds; social sign-in stays delegated

**Date:** 2026-09-04 · **Status:** accepted · **Amends:** D-086 §86.5 · **Restores:** D-067 · **Extends:** AUTH-STEP-005, D-124 · **Resolves:** review-2, H-3

> **Superseded in part.** The "strongest factor held" evaluation rule is replaced by level-declared gates satisfiable by any combination reaching the level — D-141. The social carve-out (delegated, never satisfies step-up) stands; the assurance value is now named `delegated`.

**TL;DR.** D-067 chose "strongest available, not lowest" for step-up and rejected
password-only reauthentication because it "ignores MFA a customer chose to enrol".
D-086 §86.5 changed it to "offer every eligible method … the person chooses" to fix
a different lockout, without recording that it overturned D-067. Result: a customer
who enrolled TOTP could be asked for the password instead — whoever held a live
session and the password could export the health-data history and start an email
change with the authenticator never asked for. D-067's rule is restored.

**Decision** — the user's, on the reviewer's recommendation, with one carve-out.

- **Step-up requires a factor from the strongest eligible class the subject holds**:
  phishing-resistant · TOTP · password. Choice exists only among equals (passkey vs
  security key). A password-only subject presents the password — D-086's lockout fix
  is untouched
- **Social sign-in is unchanged.** The user's position, confirmed against NIST SP
  800-63C: federated authentication *is* reliance on the provider's assertion; the
  relying party does not re-run the verifier, and nothing in 800-63B/C, OWASP or OIDC
  requires stacking a second factor on it. What the model requires — claim no
  assurance you cannot see; gate sensitive actions yourself — the spec already does:
  social never satisfies step-up, so the strongest-factor rule applies on that session
- **Precision:** social sessions record **no asserted AAL** (800-63C: "no default
  value is assigned"), not "AAL1"; treated as the lowest tier where one is needed
- **Disclosed once at link time**; accepted position **R-A16**

**Rejected.**

- *Keep "the person chooses".* Enrolling MFA would protect nothing the customer cares
  about
- *Require the account's second factor after a social primary.* Re-does what was
  delegated, for an assurance the relying party cannot measure; the reviewer's
  suggestion, declined by the user on that reasoning

**Propagated to:** `02` AUTH-STEP-002, AUTH-SESS-005a, AUTH-FACT-002a · `18`
FE-API-004 · `13` R-A16.

---

## D-129 — Break-glass works for the person it is written for: owner destinations required, always alerted, a page not an endpoint, and the sole administrator's recovery path

> **Amended.** The break-glass page takes the owner's reason with the credential (D-166).

**Date:** 2026-09-04 · **Status:** accepted · **Extends:** D-010, D-029, D-065, D-071 · **Resolves:** review-2, H-4

**TL;DR.** The emergency path assumed owner alerting that was off by default with no
address to send to, presented a JSON endpoint to an owner the spec itself calls
non-technical, never said how the emergency session reaches the management app, and
sent the sole administrator's own lock-out to a recovery flow that needs a second
approver who does not exist. Verified against Microsoft's emergency-access guidance,
which the design follows on the essentials — alert on every use, sealed storage,
excluded from normal policy — and **departs from deliberately** on two points: validation
annually rather than every 90 days (the accepted maintenance exception), and one
credential rather than two (a one-owner company; corrected D-140) — and "ensure that the people who might need
to perform these steps are trained on the process".

**Decision.**

- `alerting.owner.email` and `alerting.owner.sms` are **required** configuration;
  bootstrap refuses without them. `alerting.owner.enabled` (off) governs routine
  alerts only; break-glass use and replacement generation reach the owner **always**
- **`/break-glass` frontend route** (FE-BG-001): one field, one button, lands on the
  management application. The envelope names that address. The endpoint sits behind
  it and returns 422, not 401, for an invalid credential
- The break-glass session **is an auth session**; applications open from it as from
  any sign-in
- `11` §3.1 corrected: for the sole administrator, losing credentials **is** a
  break-glass case. §3.2 rewritten as a procedure a non-technical owner can follow

**Propagated to:** `06` OPS-BOOT-002, OPS-ALERT-004 · `09` `POST /auth/break-glass`
· `10` §4.5 · `11` §3.1, §3.2 · `18` FE-BG-001.

---

## D-130 — Customer sessions gain a one-year absolute ceiling; the NIST claim is made true

**Date:** 2026-09-04 · **Status:** accepted · **Amends:** D-123 · **Resolves:** review-2, M-1

**TL;DR.** D-123 said the customer session departed only from a NIST SHOULD. The
reviewer checked §2.1.3: "a definite reauthentication overall timeout SHALL be
established" — the 30 days is the SHOULD, the existence of a definite end is the
SHALL. A session refreshed by every use has no definite end. The design was
defensible; the honesty claim was wrong.

**Decision** — the user's, on the reviewer's recommendation.

- `session.default.absolute` — **365 days**, ceiling 365. The SHALL is met; the only
  departure is the 30-day SHOULD, recorded as such. No customer notices a yearly
  sign-in
- After an **idle** expiry, reauthentication inside the absolute window accepts a
  **passkey or the password** bound to the session secret — never a TOTP code alone —
  consistent with §2.2.3's "password or biometric comparison" (a passkey with PIN is
  the same class — possession plus knowledge bound to the session secret; D-140)
- `session.aal2.inactivity` ceiling lowered from 24 h to **12 h**; the 1 h default is
  unchanged

**Rejected.** *No ceiling, recorded as a deliberate SHALL departure.* Defensible, but
trades an honest conformance statement for something no customer would ever notice.

**Propagated to:** `02` AUTH-SESS-005 · `10` §4.1.

---

## D-131 — A delivery recipient's details are the customer's personal data, under the customer's key

> **Superseded in part.** The host business rules and vendor facts this entry records (orders, payments, shipments, couriers, addresses) moved to the host under D-165; the reasoning stands as the host's inheritance.

**Date:** 2026-09-04 · **Status:** accepted · **Extends:** D-027, D-098, D-099 · **Resolves:** review-2, M-2

**TL;DR.** IDN-LIFE-002a says anyone with sensitive data held about them holds an
account; the household answer (D-027) was that the customer orders for others; and
`04`'s canonical encryption example modelled the recipient as a second subject with
its own key — the model D-098 had rejected. A builder could not tell whether a
recipient is a subject.

**Decision** — the user's, on the reviewer's recommendation.

- A recipient's name, phone and address are **data the customer entered**, encrypted
  under the **customer's** key, corrected and erased with the customer. No account,
  no key, no dashboard for the recipient
- The recipient remains a data subject in law; a request from them is handled through
  the out-of-band path against the customer's records
- `04`'s example corrected to a single subject column; the per-field pointer stays
  for the genuine two-account-holder case
- D-098's rejection of recipients-as-subjects stands and is now stated in `01`

**Propagated to:** `01` IDN-LIFE-002a · `04` PRIV-RIGHT-005a example.

---

## D-132 — The missing numbers: retention floors, backup retention, code and link lifetimes, the throttle curve, photo limits, the step-up list and the events catalogue

**Date:** 2026-09-04 · **Status:** accepted · **Extends:** D-107, D-026.1, D-013, D-022 · **Resolves:** review-2, M-3

**TL;DR.** Several SHALLs promised a number and never gave it — "rejected below the
floor" with no floor, "time-boxed" with no time — and two contracts (`07`'s emitted
events; which library actions need step-up) were promised and never listed. Each is
a 00 §9 defect. Values chosen at the safe end of recommended practice, approved as a
table.

**Decision** — the user's approval of the reviewer's table.

| Key | Default · floor/ceiling |
|---|---|
| `retention.audit.security` | 7 years · floor 5 (tax retention plus a margin for claims) |
| `retention.audit.routine` | 90 days · floor 30 |
| `retention.consent` | life of processing + 3 years · floor + 1 |
| `backup.retention` | 35 days · floor 14 — bounds a pre-erasure backup's survival |
| `code.verification.lifetime` | 10 min · ceiling 30 |
| `link.magic.lifetime` | 15 min · ceiling 1 h |
| `link.invitation.lifetime` | 7 days · ceiling 30 |
| `abuse.throttle.delay.*` | 1 s, ×2, max 60 s per source; per-account cap 30 s; halves every 10 min |
| `abuse.nonexistent.window` | 1 hour per address |
| `photo.maxbytes` / `.maxdimension` | 2 MB / 1024 px |

- **Step-up actions** listed once in `10` §5a
- **Emitted events** catalogued in `10` §5b as the public contract `07` promised

**Propagated to:** `10` §4.5, §4.7, §5a, §5b · `04` PRIV-RET-001.

---

## D-133 — The break-glass code exists on paper only; the management app issues it, never the bootstrap command

**Date:** 2026-09-04 · **Status:** accepted · **Amends:** D-028, D-084 (envelope copies) · **Extends:** D-065, D-129 · **Resolves:** review-2, M-5

**TL;DR.** `06` said the break-glass credential is "stored only as a hash"; `12` said
every envelope item is "also held in the secrets manager"; `11` said to "update the
copies" at reseal. Both cannot hold, and a copy in the secrets manager hands the
operator — whose access to it is standing — the credential the design gave to the
owner precisely to exist outside the operator's reach.

**Decision** — the user's, on the reviewer's options.

- **Paper only.** The system stores a hash; the code is never in the secrets
  manager, mail, logs or a file. The four other envelope items stay in both places —
  the system needs them daily; nobody needs the break-glass code on an ordinary day
- **Issued from the management application**, first issue and every replacement, as
  a one-time printable page: code in check-charactered groups, a QR of the code
  alone, the `/break-glass` address, the owner's instruction. Bootstrap no longer
  generates it, so the secret never touches a terminal
- Until one exists, a **non-dismissable High alert** to every system administrator.
  The window between bootstrap and the administrator's first sign-in has nobody but
  the administrator with access, so nothing exists for a break-glass to rescue
- `POST /admin/break-glass/regenerate` renamed `/generate`, since it also issues the
  first

**Rejected.** *Bootstrap writes a printable file.* Better than a terminal, but a
secret on disk until someone deletes it. *A copy in the operator's vault as a hedge
against a lost envelope.* That is what a second sealed envelope in a second place is
for. *Hardware security key.* Noted as a later upgrade if a second administrator
ever exists; a device that can fail, for an owner who is not technical.

**Propagated to:** `06` OPS-BOOT-001, OPS-BOOT-004 · `09` `/admin/break-glass/generate`
· `11` §3.3 · `12` DR-009.

---

## D-134 — Phone change gets the email-change protections; trusted devices lose trust after three wrong passwords

**Date:** 2026-09-04 · **Status:** accepted · **Amends:** D-035 (phone-change rationale), D-124 · **Extends:** D-111, D-114, D-128 · **Resolves:** review-2, M-6 and M-9

> **Amended.** The trusted-device offer is withheld on short-password accounts — D-141 reversed the acceptance of that exposure (R-A14). The three-wrong-passwords revocation stands.

**TL;DR.** IDN-LIFE-010 still said phone change could be lighter "because phone is
not a recovery channel"; since D-111 it is — the admin-assisted enrolment link goes
to it — so a planted number received the next recovery. Separately, D-114 allowed a
10-character password only on accounts holding MFA, and D-124 let a trusted device
skip that MFA for 30 days; combined, the short password stood alone on that browser.

**Decision** — the user's.

- **Phone change carries the email-change protections**: strongest-factor step-up,
  SMS to the previous number with a reversal link, cooling-off
  (`identifier.change.coolingoff`, one key for both, replacing
  `email.change.coolingoff`) during which the previous number still serves recovery,
  other sessions ended. A lost previous number goes through admin-assisted recovery,
  as a lost mailbox does — the request in the management app, identity confirmed by
  a call to a recorded channel, approval with a written reason
- **Trusted devices stay available to every customer.** The user's point: a trusted
  device is a second thing the attacker must hold, and withholding the offer from
  short-password accounts is by-the-book friction. The user's control instead:
  **three consecutive wrong passwords on a trusted device revoke its trust**
  (`factor.trusteddevice.failurelimit`, default 3), using the throttle's existing
  failure counter. The departure from NIST's 15-alone SHALL is recorded honestly in
  R-A14 with its compensating controls, not pretended away

**Rejected.** *Withhold "trust this device" from accounts on the with-MFA password
floor.* The reviewer's proposal; correct on paper, needless in practice once trust is
revoked on guessing.

**Propagated to:** `01` IDN-LIFE-010 · `02` AUTH-FACT-015 · `10` §4.3, §4.6 · `13` R-A14.

---

## D-135 — Housekeeping pass two: the twenty-two mechanical findings of review-2

**Date:** 2026-09-04 · **Status:** accepted · **Resolves:** review-2, M-4, M-7, M-8, M-10, M-11, L-1, L-3, L-5–L-16 (L-2 closed by D-129, L-4 by D-126)

**Decision** — presented to the user as a "what's wrong / the fix" table and approved
as a whole.

- **M-4** DR-009 AC2 now says "without the operator and without the secrets manager,
  with outside technical help"; DR-006a acceptance rewritten for Phase 1 honesty
  (re-apply erasures from the erasures table where it survives; R-A13 otherwise);
  R-A13's reason corrected — the secrets manager is off-host but is not a ledger
- **M-7** `suspendedBy` (`self` · `administrator`) recorded; self-reactivation by a
  link token in the deactivation notice; admin-suspended accounts cannot
  self-reactivate (`identity.account.adminsuspended`)
- **M-8** R-A07 reworded to the 15-minute recency window; D-007 bannered
- **M-10** `POST /privacy/consents/{purpose}/grant` added; objection immediate;
  rectification of non-editable data is a queue request under the six-day rule
- **M-11** Six statutory rights vs nine recognised, stated in `00` and PRIV-RIGHT-001;
  fee tier flagged as unverified (Baker McKenzie: exempt to 100,000; Legal 500:
  1–10,000) and made a company action in R-O03; the developer's processor
  agreement added to R-O03
- **L-1** stale "30 days" text corrected in `02` (×2) and `09`
- **L-3** `auth.credential.lastsecondfactor` added to `10`
- **L-5** `POST /auth/magic-link` and `POST /auth/email-otp` defined, always 202
- **L-6** assurance table completed; when the second factor is asked stated once
- **L-7** Argon2 floor is a strength class; OWASP's equal-strength sets all pass
- **L-8** related-origin limit counted in labels
- **L-9** FE-PLAT-001 AC2: Zone.js absent, no zone provider
- **L-10** processor agreement — see M-11
- **L-11** consent records: rows kept, personal content key-destroyed at the end of
  `retention.consent`
- **L-12** `InternalsVisibleTo("Janus.Cli")` on Identity and Authentication, the only
  such grant
- **L-13** Unicode re-derivation only when a stored code point's `NFKC_Casefold`
  mapping changed; Unicode stability policy cited
- **L-14** LIB-SEAM-002 AC1 matches its body
- **L-15** superseded banners on D-007, D-033, D-067, D-086, D-097
- **L-16** compliance date stated as the conservative reading (`00` §6, R-O03)

**Propagated to:** `00`, `01`, `02`, `04`, `07`, `08`, `09`, `10`, `12`, `13`, `18` ·
this log (banners).

---

## D-136 — The decision clock runs from submission, on Egypt's working-day calendar

**Date:** 2026-09-04 · **Status:** accepted · **Amends:** D-126 · **Resolves:** review-3, H-1

> **Amended.** The holiday list is no longer required or a startup condition; it is an empty-by-default, staff-edited calendar — D-142.

**TL;DR.** D-126 counted the six working days from the moment a request entered the
queue and never defined "working day". Article 2 counts "from the date of
submission"; a letter received Sunday and typed in Tuesday got two extra days the law
does not give, and a builder reaching for a Monday–Friday business-day helper would
compute deadlines days later than the statute on a Sunday–Thursday week.

**Decision** — the user's, on the reviewer's recommendation.

- Out-of-band entry requires **`receivedAt`** — the date the request reached the
  company, never in the future; the clock runs from it. In-app requests: creation time
- **`privacy.workingdays`** (default Sunday–Thursday) and **`privacy.holidays`** (the
  host's declared public holidays, required to cover the coming twelve months, refused
  at startup otherwise) define the calendar for every "working days" computation
- Postmark versus receipt for a letter is for counsel; receipt is used and is never
  later than the truth

**Propagated to:** `04` PRIV-RIGHT-002 · `09` `POST /privacy/requests`, §8a · `10`
§1.4, §4.7.

---

## D-137 — A takedown borrows the deletion timer, not the deletion's emails or buttons

**Date:** 2026-09-04 · **Status:** accepted · **Amends:** D-127 · **Resolves:** review-3, H-2

**TL;DR.** D-127 put the takedown window on the `deleting` state "exactly as for a
customer's own deletion" — which sends the subject a deletion email with a cancel
link and exposes two cancel endpoints. Read literally, the minor cancels their own
takedown from their inbox.

**Decision.**

- `deleting` records **`deletingBy`**: `self` · `takedown` · `oob-request`
- For `takedown`: no deletion notification, no cancel link; both `delete/cancel`
  endpoints refuse with `identity.takedown.active`; reversal only through
  `/takedown/reverse` under `takedown:execute`, with a reason
- The account is in `deleting` for the window, entered from `suspended` in the
  trigger transaction; `AccountSuspended` and `TakedownExecuted` fire,
  `AccountDeletionRequested` does not

**Propagated to:** `01` IDN-LIFE-003 · `09` `POST /account/delete/cancel`, §8a ·
`10` §1.1, §5b.

---

## D-138 — The break-glass session belongs to a reserved `emergency` account

**Date:** 2026-09-05 · **Status:** accepted · **Extends:** D-065, D-129, D-133 · **Resolves:** review-3, M-7

**TL;DR.** The break-glass session was "a session holding the system-administrator
role" — a construct the authorization model does not have. Every grant is
"[subject] has [role] on [resource]" and every audit record names acting and
effective identity; nothing said which subject the emergency session was.

**Decision** — the user's (name), on the reviewer's recommendation.

- A reserved account **`emergency`**, created at bootstrap: member of the
  administrative organization, `system-administrator`, **no sign-in method** — the
  sealed credential is its only way in and no factor can be enrolled on it
- Every break-glass action is audited as `emergency`, with the owner's stated reason;
  no device list, no mailbox, cannot be granted more, suspended or deleted
- It may approve any recovery including the sole administrator's; AUTH-RECOV-002a's
  "not your own account" refers to the approver's account, which this never is

**Propagated to:** `06` OPS-BOOT-001, OPS-BOOT-002 · `02` AUTH-RECOV-002a.

---

## D-139 — The single-factor idle restore is for staff only

**Date:** 2026-09-05 · **Status:** accepted · **Amends:** D-123, D-130 · **Resolves:** review-3, M-9

**TL;DR.** The "one factor restores an idle-expired session" rule was written for
staff back from lunch but reached the wire contract for every session. A customer's
idle limit is 90 days, so a password+TOTP customer returning after three months was
let back in with the password alone.

**Decision** — the user's, on the reviewer's recommendation.

- `details.reauthenticate = single-factor` only where the principal's policy requires
  AAL2. Under the system policy an idle expiry always returns `full`: a normal sign-in,
  once a quarter at most, with trusted devices still applying
- NIST §2.2.3's allowance is for a short gap; three months of silence is when a full
  check is worth it

**Propagated to:** `02` AUTH-SESS-005 · `10` §1.2 · `17` BFF-STEP-001.

---

## D-140 — Housekeeping pass three: the twenty mechanical findings of review-3

**Date:** 2026-09-05 · **Status:** accepted · **Resolves:** review-3, H-3, M-1–M-6, M-8, L-1–L-12

**Decision** — presented as a "what's wrong / the fix" table and approved as a whole.

- **H-3** `09` phone change now carries the D-134 protections: `/revert`, 403/409,
  `identity.change.windowelapsed`; "lighter" removed
- **M-1** `11` §5 rewritten to the two-phase takedown with reversal
- **M-2** `08`: `Janus.Cli` creates the credential-less `emergency` account, never the
  break-glass credential
- **M-3** eight required declarations: owner destinations and the holiday list added
  to LIB-HOST-001 and the `10` §4 preamble
- **M-4** assurance enum gains `none`; the stale "records AAL1" criterion fixed; `09`
  documents the values
- **M-5** `10` §5a paths aligned to `09`; `09` marks delete, deactivate and provider
  linking as step-up
- **M-6** `POST /enrol/begin` and the enrolment session defined — what it may call,
  its lifetime, the recovery email exception, reactivation of a self-suspended account
- **M-8** two alert rows: no emergency credential (High); restore test failed or late
  (High)
- **L-1** events: mailbox provisioning removed from `AccountRegistered`;
  `TakedownReversed`, `OrganizationErased`, `IdentifierChanged` added
- **L-2** `abuse.throttle.threshold` = 3; password floors stated as 15 and 8
- **L-3** stale "erasure enters the queue" sentence corrected
- **L-4** IDN-LIFE-002a renumbered · **L-5** D-125 in the Source line
- **L-6** D-062 corrected to v21 · **L-7** D-117's tax citation corrected to Law
  206/2020; accountant to confirm the article for the RoPA
- **L-8** "precisely" → "consistent with" (`02`, D-130)
- **L-9** R-A16 states the minimum accepted AAL (none for customers; AAL2 for staff)
- **L-10** D-129 now says the design departs from Microsoft's cadence and count,
  deliberately
- **L-11** DR-010: restore-test key from the secrets manager, memory only
- **L-12** the "nothing has changed" notice points to sign-in and recovery; recovery
  restores a self-suspended account

**Propagated to:** `01`, `02`, `06`, `07`, `08`, `09`, `10`, `11`, `12`, `13` · this
log (D-062, D-117, D-129, D-130).

---

## D-141 — Step-up gates declare a level; a lost authenticator is a state, not a removal

**Date:** 2026-09-05 · **Status:** accepted · **Supersedes:** D-128 on the evaluation rule (the social carve-out stands) · **Amends:** D-134 on the trusted-device offer, D-009/D-022 (AUTH-RECOV-007 generalised), D-114 (AUTH-PASS-001a restated as a property) · **Resolves:** review-4, H-1

**TL;DR.** Review-4 H-1: a customer whose only passkey died could not remove it —
the gate asked for "the strongest factor held", the passkey was still on the list,
and nothing in the design knew the difference between *enrolled* and *in the
person's hands*. Research across NIST SP 800-63B-4/63C-4, RFC 9470, OIDC, OWASP
ASVS 5.0, FIDO, Google, Apple, GitHub, Microsoft Entra, Okta and Auth0 found the
same shape everywhere: a gate names an assurance level and a recency, any
combination of factors reaching it satisfies it, and a lost authenticator is
reported with one remaining factor and removed after a notified delay. Five Janus
defects to date (password beside TOTP, trusted-device floor leak, social + MFA
arithmetic, idle-restore anchor, the lost passkey) all came from one rule that read
the enrolment list. That rule is replaced; the controls around it were already right.

**Decision** — the user's, on the reviewer's recommendation; the social-sign-in
option chosen as Option A (`delegated`, earns nothing at a gate).

1. **Gates are three values** — level, phishing-resistance, maximum age — set per
   action by policy and evaluated against the session record and the account's
   **reachable assurance** only; no gate reads the enrolment list, no rule names a
   factor (AUTH-STEP-002, AUTH-STEP-002a).
2. **Reachable assurance** (new AUTH-STEP-006): the highest level the account can
   reach with its `active` authenticators, by the AUTH-SESS-005a table. Recovery
   codes and social credentials do not raise it.
3. **Any combination reaching the level satisfies**; the subject chooses among the
   combinations offered. A bare second factor reaches nothing. System-policy gates
   require the account's reachable assurance (floor AAL1), not phishing-resistant;
   administrative gates require AAL2 phishing-resistant. This keeps D-128's intent
   — no password bypass on an AAL2 account — without D-128's mechanism.
4. **Never a bare refusal**: `enrol` when the account never reached the level,
   `report-loss` when it did but a factor is gone, `pending` when a report is
   running.
5. **Authenticator states** `active` → `suspended` → `invalidated` (AUTH-RECOV-007
   generalised from "MFA removal" to any authenticator). Reporting a loss needs one
   usable factor or one recovery code, no step-up; suspension is immediate;
   invalidation after `recovery.invalidation.window` (7 days, renamed from
   `recovery.mfaremoval.window`), notified to every channel with a cancel link, held
   if nothing delivered. Reachable assurance drops only on invalidation, so gates
   stay closed to everyone — including a mailbox thief — during the window.
   Removing an `active` factor that would lower reachable assurance takes the same
   path; one that would not completes at once. Staff have no self-service path
   (AUTH-RECOV-008) — two credentials plus admin-assisted recovery.
6. **Binding** (new AUTH-STEP-007): enrolling a factor is gated at the lower of
   reachable assurance and the new factor's contribution (NIST §4.1.2.1), notified
   to every other channel. Recovery is binding a password under this rule; it never
   changes another authenticator's state.
7. **Shortcuts write what they presented** (trusted device AAL1, social `delegated`,
   idle-restore the prior level). AUTH-PASS-001a becomes a property — a password
   that can complete a sign-in alone meets the 15-character floor — so the
   trusted-device offer is **withheld on short-password accounts** (AUTH-FACT-015
   AC7), reversing D-134's acceptance of that exposure; the three-wrong-passwords
   revocation is kept.
8. **`none` → `delegated`** for the social-only assurance value; "Social + TOTP =
   AAL2" row deleted; `CanSatisfyStepUp` removed from the factor properties.
9. **Invariants as tests** (new AUTH-STEP-008): no factor names in §5/§6; no gate
   reads the inventory; every gate reachable in ≤ 2 moves from every state; removal
   never gated on the thing removed; reachable assurance never drops without two
   channels notified and a completed window; the session never records more than
   was presented; lifetimes read one anchor.

**Rejected.** *Patching* "lost only passkey" as an exception to the strongest-held
rule — the fifth exception to a rule whose shape produced the first four. *A bare
TOTP code as a reauthenticator* — one possession factor is not AAL2. *Social as
AAL1* (Option B) — nicer for social-only customers, but rests on providers honouring
a fresh-login request, which AUTH-STEP-005 records they do not; revisit if that
changes. *Counting recovery codes toward reachable assurance* — would spend a code
at every gate.

**Propagated to:** `01` (IDN-LIFE-005/010) · `02` (AUTH-FACT-001/002/002a/015,
AUTH-PASS-001a, AUTH-SESS-002/005a, AUTH-STEP-002/002a, new 006/007/008,
AUTH-RECOV-001/005/007/007a/008) · `09` (`/auth/factor`, `/auth/step-up` details,
`/recovery/report-loss`, `/account/credentials`, `/account/password`, enrolment
endpoints, `/privacy/export`) · `10` (§1.2 codes, §4.1 and §4.4 keys, §5.3/5.3a/5.4
enums, §5a, §5b events) · `11` §(mail outage row) · `13` (R-A14, R-A16) · `15`
(mailbox row) · `17` (BFF-STEP-001) · `18` (FE-API-004) · `19` (scheduler list).

---

## D-142 — The holiday list is a living calendar, never a startup condition

**Date:** 2026-09-05 · **Status:** accepted · **Amends:** D-136 (the "required, twelve months" rule), D-140 M-3 (LIB-HOST-001 count) · **Resolves:** review-4, H-2

**TL;DR.** D-136 made `privacy.holidays` a required declaration that must cover the
coming twelve months or the system refuses to start. Egypt's holidays cannot be known
twelve months ahead — four are lunar, and the fixed ones are observed on a day the
cabinet announces shortly before — so the list is always a guess, extending it is an
unlisted third recurring chore, and a restart during the operator's absence with a
short list takes the whole deployment down over a calendar. The safety argument was
backwards: an unlisted holiday counts as a working day and produces an **earlier**
deadline, which is always compliant. The system never needed the list to run.

**Decision** — the user's, on the reviewer's recommendation, with the user's framing:
the list is **dynamic**, edited by staff as holidays are announced or moved.

1. `privacy.holidays` is optional; safe default **empty**. Removed from LIB-HOST-001
   and from the "required at startup" count (eight → seven). Never a startup
   refusal.
2. Edited at runtime from the management app by a holder of the configuration
   permission through `PUT /admin/config/privacy.holidays`; classified as a
   **loosening** (a holiday pushes deadlines later) — step-up, reason, audit
   (OPS-CFG-002).
3. Deadlines are counted on the calendar as it stands when counted; a holiday added
   later is honoured for open requests; a missing one only ever makes a deadline
   earlier.
4. A **Normal** alert when no listed date lies beyond `maintenance.expiry.warninglead`
   (OPS-ALERT-001). The "two recurring human tasks" statements stay true: this is an
   as-announced edit, not a scheduled task.

**Rejected.** *Required per calendar year* — rarer chore, same outage on a 2 January
restart. *An external holiday feed* — no official Egyptian source; third-party feeds
are as wrong about observed dates as a year-ahead guess; a dependency for ten dates a
year a human learns from the news.

**Propagated to:** `04` PRIV-RIGHT-002 · `06` OPS-ALERT-001 table, §9 · `07`
LIB-HOST-001 · `10` §4 preamble, §4.7.

---

## D-143 — The policy is one object with five fields

> **Amended.** The policy object gains the field `photos` (D-166).

**Date:** 2026-09-05 · **Status:** accepted · **Extends:** D-116, D-141 · **Resolves:** review-4, M-1

**TL;DR.** Five behaviours branched on whether a principal's policy "requires AAL2"
— lifetimes, the staff floor, the trusted-device offer, the idle restore, and (since
D-141) the gate values — and AUTH-SESS-005b insisted the floor be *stated*, not
derived. Nothing said where. `10` held four scattered keys (`login.policy.*`,
`stepup.policy.*`, `stepup.gate.<action>`, `stepup.enforcement.*`), none carrying a
required level; `PUT /admin/organizations/{id}/policy` had no body. The central
object of the authentication chapter had no schema — a D-119 defect.

**Decision** — the user's, on the reviewer's recommendation. One policy object,
defined in `10` §4.1a, resolved per AUTH-PRIN-002:

| Field | System default | Administrative organization |
|---|---|---|
| `requiredAssurance` | `aal1` | `aal2` |
| `loginFactors` | catalogue defaults | passkeys only |
| `gates` (per §5a action: level · phishing-resistant · maxAge) | `reachable` · no · 15 min | `aal2` · yes · 15 min |
| `credentialRedundancy` | `advisory` | `enforced` |
| `selfServiceRecovery` | `true` | `false` |

`policy.default` holds the system object; `policy.<organization>` holds only the
overridden fields and is the body of `PUT /admin/organizations/{id}/policy`
(`GET` returns the resolved object, each field marked inherited/overridden). An
organization may tighten, never loosen below the system default
(`config.policy.belowsystem`); loosening a field is a loosening under OPS-CFG-002.
Every "where the policy requires AAL2" now reads `requiredAssurance`.
`stepup.enforcement.<organization>` stays outside the object as the protected kill
switch (OPS-CFG-004).

**Rejected.** *A lone key `assurance.required.<organization>`* — fixes the named gap
and leaves the other four policy facts as scattered keys with no schema.

**Propagated to:** `02` (AUTH-PRIN-002, AUTH-FACT-015, AUTH-SESS-005/005b,
AUTH-STEP-002a) · `09` (organization policy endpoint) · `10` (§1.5, §4.1, new §4.1a,
§4.3) · `17` (BFF-STEP-001).

---

## D-144 — `emergency` has no email and no mailbox; the first administrator's mailbox follows the mail server

**Date:** 2026-09-05 · **Status:** accepted · **Extends:** D-129, D-138 · **Resolves:** review-4, M-2

**TL;DR.** INT-MAIL-006 made a mailbox a precondition of every administrative
membership. Bootstrap creates two such members before a mail server may exist — the
first administrator and `emergency` — so read literally the rule either broke
bootstrap or was silently skipped. The question underneath was not "how to exempt"
but "should `emergency` have an email at all".

**Decision** — the user's, asked as "the right decision, not a fix", on the
reviewer's answer.

1. **`emergency` has no email address and no mailbox — by definition, not
   exemption.** A mailbox is for a person; `emergency` is a role a human steps into
   with the sealed credential. Every message about a break-glass session goes to the
   owner's and operator's recorded destinations (D-129). An unread mailbox on the most
   privileged account would be a phishing target, a place for a warning to be missed,
   and a possible back door to an account that must have no sign-in method.
   INT-MAIL-006 now reads "human administrative-organization members".
2. **The first administrator is a person and gets a normal mailbox**; the membership
   is effective at bootstrap and the provisioning request goes through the retried
   outbox (D-022), completing when the mail server is reachable. Bootstrap depends on
   the database only. Until then the administrator is reached at the personal address
   given at bootstrap.

**Rejected.** *Provisioning `emergency@`* — a destination nobody reads. *Bootstrap
provisioning synchronously* — makes a fresh deployment depend on a service that may
not exist yet.

**Closed by D-147.5:** `emergency` holds no identifiers and sits outside the identifier
rules of `20`; its only channel is the owner alert destinations.

**Propagated to:** `05` INT-MAIL-006 · `06` OPS-BOOT-001.

---

## D-145 — Objection is a first-class right; consent guidance corrected for Egyptian law

**Date:** 2026-09-05 · **Status:** accepted · **Amends:** D-064 (analytics basis stated as guidance, not fact) · **Extends:** D-024, D-030, D-108 · **Resolves:** review-4, M-3

**TL;DR.** The dashboard's "object" switch had no endpoint, record, event or rule
behind it — a statutory right drawn on a screen. Research the user requested (why
"on by default" is common elsewhere; consent vs objection; cookie banners) confirmed
the rest of the privacy chapter and found: Egypt requires prior explicit opt-in for
electronic marketing with no existing-customer exception (Art. 17–18; Decree 816/2025
Art. 18); legitimate interest is not an express basis in Egyptian law; per-person
behaviour on a sensitive type is sensitive-data processing; strictly necessary
cookies need no consent anywhere and Egypt has no cookie rule.

**Decision** — the user's, after asking for the plain meaning rather than the
mechanism; the reviewer's earlier framing that a deployment "has no objectable
purpose at launch" was wrong and is withdrawn: Janus declares no purposes, hosts do.

1. **Objection wired as consent withdrawal's twin** (new PRIV-RIGHT-001a): for any
   purpose on an `IsObjectable` basis, `POST`/`DELETE /privacy/objections/{purpose}`,
   a record like a consent record, `ObjectionChanged` with a **required** handler per
   objectable purpose (startup fails without one), always honoured, no grounds asked,
   no refusal path. Dashboard: one switch per declared purpose, labelled by basis.
2. **PRIV-CONS-002 corrected**: the basis of per-person analytics is the host's
   declaration; guidance for the default Egyptian declaration is consent for
   per-person analytics over sensitive types, legitimate interest for fraud, security
   and abuse controls. Mechanism unchanged.
3. **Cookie transparency** (new PRIV-CONS-006a): the notice lists every cookie set
   with purpose and lifetime and states none needs consent; the library sets only
   strictly necessary cookies; a host adding a non-necessary one must declare a
   consent-based purpose. No banner.
4. **Withdrawal ends the purpose's data** (PRIV-CONS-008): data held solely for the
   withdrawn purpose is erased by the library and by the purpose's handlers
   (`ConsentChanged` handlers become required); the consent record is kept for
   `retention.consent` (Art. 18).
5. **Two items to R-O03**: a counsel question — contract basis versus Art. 12 written
   consent for health-implying orders (D-089 stands until answered); and a note that
   legitimate interest is untested before the Centre.

**Confirmed correct, no change:** unticked opt-in consent (required by Art. 17, not
mere caution), written consent for sensitive data (PRIV-CONS-004), three-year consent
retention, withdrawal as easy as granting, aggregate counts out of scope, controller
and direct-marketing licences (`00` §7, `06` §9, `11`), necessary-cookies-only.

**Rejected.** *Soft opt-in for existing customers* — lawful in the EU/UK (ePrivacy
Art. 13(2)), no basis in Egyptian law. *A "compelling grounds" refusal of objection*
— GDPR's concept, absent from Egyptian law. *Folding the analytics-basis question into
the library* — the library supports both bases; the choice is the host's.

**Propagated to:** `04` (PRIV-CONS-002, new 006a, 008, PRIV-RIGHT-001, new 001a) ·
`07` LIB-API-005 · `09` (`/privacy/objections`, §7) · `10` (§1.4, §5b) · `13` R-O03 ·
`18` FE-COMP-006.

---

## D-146 — The account has a shape and registration is a session; the sending rules become restrictions

> **Amended.** A restriction carries a channel (`sms` · `email` · `any`); a notice to a holder answers to `notification` restrictions only; every edit of the restriction set carries a reason (D-166).

**Date:** 2026-09-17 · **Status:** accepted · **Amends:** D-006, D-009, D-011, D-012, D-013, D-027, D-031, D-035, D-039, D-055, D-114, D-144 · **Resolves:** review-4 M-5 and M-6 · **Renders:** new chapter `20-registration-and-account.md`

**TL;DR.** Review-4 M-5 found that the specification described the pieces of an account
without ever describing the account: what it holds, who fills each field, how it comes
into being and how its identifiers change afterwards. Working that out, and then
walking through the registration screens step by step, surfaced a set of decisions
that were taken one at a time between 2026-09-05 and 2026-09-17. This entry records
them together. Chapter `20` states the resulting requirements; the chapters below are
amended to agree with it.

### 1. The account and its identifiers (`20` sections 1 and 2)

1. **An account holds several verified emails and phones, one primary per kind, with a
   backup setting** that fixes the security-notice set (REG-IDENT-002). Every verified
   identifier signs in (REG-IDENT-003). Email, or a provider that supplies one, is the
   anchor that can never be dropped; phone is required by default
   (`registration.phone`) and never the sole identifier; username exists, off by
   default, optional when on (REG-IDENT-001, REG-IDENT-009).
2. **Verification happens when an identifier is added; removal is the protected move**
   (REG-IDENT-004 to 006). Switching primary or backup is free with a notice. Removal is
   step-up and immediate; the undo goes to the remaining security-notice set, never to
   the removed address, which is powerless from then on. The old-address confirmation of
   IDN-LIFE-004 survives only where no other channel exists (REG-IDENT-007). *Amends
   D-035.*
3. **Providers verify their own mailboxes** (REG-IDENT-008): Gmail and Workspace by the
   `hd` claim, Apple relay and Apple domains, count as verified by the sign-in; any other
   provider-supplied address gets one code. A linked `sub` is a sign-in, not a
   registration. A duplicate provider address gets the ordinary response and the owner is
   notified.
4. **A username freed by erasure is held for `retention.consent` and then released**
   (REG-IDENT-009).

### 2. Profile and preferences (`20` section 3)

5. **The age screen replaces the checkbox** (REG-PROF-002). Registration opens with a
   neutral date-of-birth screen; the adult affirmation is derived and recorded; the date
   is kept only where `profile.dateofbirth` is on, immutable to the person; an under-age
   date on an adults-only host ends the session before any identifier is collected and
   locks the fields. *Amends D-027 and D-039; IDN-LIFE-002 and the second sentence of
   PRIV-MINOR-001 are superseded.*
6. **Legal name and date of birth are policy fields, off by default**; display name is
   1 to 64 bytes under PRECIS Nickname; the photo keeps its rules (REG-PROF-001).
7. **Preferences are a host-declared typed store** beside language and time zone
   (REG-PREF-001): the host declares keys and types at startup, the library validates,
   stores under the subject key, exports and erases, and never branches on a value.
   *Amends D-055.*

### 3. Registration (`20` section 4)

8. **Registration is a session, not a half-account** (REG-SESS-001): everything is
   staged, nothing is reserved, the account is created in one transaction at the terms
   step, an abandoned attempt leaves nothing. The `pending` state is removed. *Amends
   D-114.*
9. **Ten steps, no Back** (REG-SESS-002): age, email, phone, confirm, security, terms
   and consents (account created), about you, preferences, membership (invitations),
   done. Corrections are made in place through a per-identifier Change; bound and
   provider-operated identifiers are locked (REG-IDENT-010).
10. **A link completes only in the browser that started the flow, and only on a press**
    (REG-SESS-003). Opened anywhere else it shows the code to type and a control that
    ends the session; a plain open changes nothing. Five wrong codes invalidate a code.
    The same rule governs adding an identifier later and sign-in links.
11. **The security step is one screen** (REG-SESS-006): at least one primary sign-in
    method; a password below the single-factor floor makes a second step mandatory in
    place; a confirmed email or phone alone suffices exactly when the matching link
    factor is enabled.
12. **The person returns to the application's registered landing address, and the
    application restores its own state** (REG-SESS-008).

### 4. Invitations, domain lock and the corporate mailbox (`20` section 5)

13. **Invitations may bind the email, the phone, both or neither**; acknowledgement
    attaches the membership and is recorded (REG-INV-001). **An existing account accepts
    by signing in**; no second account is created; the organization's credential policy
    is satisfied first (REG-INV-002).
14. **Domain lock** is a policy field with DNS-verified domains, re-verified on a
    schedule; a failed re-check alerts and revokes nothing; removing a domain stops new
    sign-ins with it and alerts (REG-DOM-001).
15. **The corporate mailbox is provisioned disabled at invitation and enabled at
    membership**; the corporate address is asserted by the administrator, never sent a
    code; what proves the person is the personal channels (REG-MAIL-001). *Amends
    D-144.* **App passwords are managed from the account application through the
    library's first-party OIDC client**; the server generates the secret and nothing is
    stored on the library's side (REG-MAIL-002); the mail-server contract is restated as
    JMAP, which is how the server's own interface performs the operation. *Amends
    D-006.* **When membership ends** the account continues on its personal identifiers
    or is deactivated (REG-MAIL-003).

### 5. Passwords (`02` section 3)

16. **A password is refused for two reasons only**: the length floor (15 alone, 10 with
    a second step, at most 128) and a hit on the leaked-password list. Strength feedback,
    including the person's own name, email and the service name, is advisory. A host MAY
    add dictionary and context words as rejection sources through
    `password.blocklist.sources`, off by default; context words are checked at set and
    change, and a profile field added later is caught at the next sign-in with a prompt
    to change, never a lockout. Recorded as a deliberate deviation from NIST SP 800-63B-4
    section 3.1.1.2, whose blocklist includes dictionary and context words as a SHALL.
    `password.maximum` rises from 64 to 128. *Amends D-011.*

### 6. Sending restrictions (`02` section 7)

17. **Sends of every kind are governed by named restrictions, not by one fixed window.**
    A restriction has a key (destination as an HMAC of the address, account, source,
    global, or a host-supplied key), an optional purpose filter, and one or more
    (max, interval) buckets, sliding or fixed. Every send evaluates every applicable
    restriction; any exceeded bucket refuses, and the refusal carries the earliest time a
    bucket lifts, which is what the person is told. Restrictions are runtime
    configuration edited like the holiday list (D-142): immediate, audited, loosening
    alerts. Support may grant credits to a key (support role, step-up, audited, reason).
    Security notices to an existing holder are outside destination restrictions and have
    their own. A failed delivery does not count. Shipped defaults: `sms.destination`
    (3 per 24 h sliding), `sms.source` (10 per hour), `email.destination` (5 per hour and
    1 per 60 s), `notification.destination` (5 per 24 h). AUTH-ABUSE-004, INT-SMS-002,
    IDN-LIFE-011 and `abuse.sms.window` are replaced. *Amends D-013.*

### 7. Factors (`02` section 2)

18. **Sign-in links by email and by SMS, and an SMS code as a second step, exist and are
    off by default**: `loginFactors` gains `emailLink`, `phoneLink` and `phoneCode`. A
    sign-in link is AAL1 and never a second step. **Password plus SMS code is AAL2** at
    sign-in and at any gate that asks for AAL2, never at a gate that asks for phishing
    resistance; it is flagged less secure at enrolment and wherever listed. Both SMS uses
    are restricted per NIST SP 800-63B-4 section 3.1.3.3: the limitation is shown at
    enrolment and SIM-change and porting signals, where the gateway supplies them, are
    considered before use. Email OTP, the same class as `emailLink`, is off by default
    too. Off by default because every text costs money and reaches a number, not a
    person, and because an email-only or phone-only account is a host's choice to
    allow. *Amends D-009 and D-012.*
19. **Recovery codes are always generated when a second step is enrolled beside a
    password**, shown once with copy, download and print, confirmed saved, regenerable
    later; passkey-only accounts get none. Viewed and exported timestamps are kept and a
    reminder fires after `recovery.codes.reminder` (365 days). *Amends D-009.*
20. **Security step model.** Passkeys sign in (device-held or on a hardware key, chosen
    in the passkey dialog); a security key under two-step is a non-discoverable second
    factor; two-step is unavailable until a usable password exists. Every WebAuthn and
    TOTP enrolment takes a label (1 to 64, unique per kind per account, editable). A
    second-factor security key can be upgraded to a passkey by re-registration. Each
    account has a preferred second-step method.
21. **Credential records carry `backupEligible` and `backupState`, added and last-used
    timestamps and the label**; sessions are listed with per-session revoke, the current
    session marked and a city-level location from a local IP database.
22. **New-device check for single-factor accounts**, on by default: an unrecognised
    browser must enter a code emailed to the primary address before the sign-in
    completes; the browser is remembered for `device.verification.lifetime` (90 days).
    Passkey and two-step sign-ins never trigger it.
23. **Grace period when a requirement is raised**: `policy.enforcement.grace`, default
    0. During the grace, affected sign-ins are told the requirement and the deadline and
    may continue; after it, sign-in stops at enrolment. Accounts created after the change
    are held at once.

### 8. Legal documents and content (`04`, `08`)

24. **Every legal document has one governing language per version**, defaulted from
    `legal.governinglanguage` (mandatory at install, restart to change, Normal alert on
    change); translations are optional and attach to a version. Wherever a document is
    shown the reader can view the governing text and any translation without changing
    the interface language. Multi-market document sets are deferred. *Amends D-031.*
25. **Content is a frontend deliverable** (new CONV-CONTENT-001): specs state what a
    message must achieve and may give an example marked illustrative. Three exceptions
    keep a requirement on the words: identical-regardless messages (AUTH-ABUSE-002/003),
    legal texts, and never-disclose rules.

26. **Password managers and passkeys work first time** (`18` FE-PM-001 to 006,
    `20` REG-PM-001): one form per step with the WHATWG autofill tokens (`username`,
    `new-password`, `current-password`, `one-time-code`, `email`, `tel`), a hidden
    username on change and reset forms, no dynamic field injection, a real submit, paste
    never blocked, `username webauthn` with conditional mediation and no
    `allowCredentials`, the `change-password` and `passkey-endpoints` well-known
    addresses, `otpauth` QR plus the Base32 secret, no Apple `passwordrules` (the library
    has no composition rules), sign-up and sign-in on one origin with distinct field
    names. The WebAuthn user handle is the subject identifier.

**Rejected.** *Old-address confirmation on every change* (D-035 as written): lets a
compromised mailbox veto its own removal. *A seven-day undo reaching the removed
address* (Google's model): lets a compromised mailbox re-attach itself. *A composition
rule* and *rejecting context words by default*: no comparable consumer product does
either, the profile fields are collected after the password, and NIST forbids
composition rules. *One fixed sending window* and *a fixed capacity-and-refill pair*: a
host that wants per-account or budget-shaped limits would need a redeploy. *SMS as
AAL1 only at gates*: the standard's own accounting makes password plus SMS a
two-factor authentication; the answer is a flag, not a downgrade. *Copying GitHub's
"authenticator app or SMS before a passkey" rule*: a passkey is AAL2 alone and
redundancy is `credentialRedundancy`. *Copying GitHub's 8-character composition rule*:
see above. *A temporary mailbox password for reading a verification code*: proves
nothing about an address the system created. *A separate notification-preferences
object*: security notices are never optional; the optional ones are consent purposes.
*Requiring several governing languages per document*: no law surveyed mandates it; one
governing text per version, translations optional.

**Propagated to:** `00` (document set, glossary) · `01` (IDN-ACCT-004/007/007a,
IDN-LIFE-001/002/004 to 011, IDN-ATTR-001, IDN-IDENT) · `02` (AUTH-FACT-001/002/003/004/008,
AUTH-PASS-001/004/005, AUTH-SESS, AUTH-STEP, AUTH-RECOV-001/006, AUTH-ABUSE-002/004,
new items for links, SMS, new-device check, grace, restrictions) · `04` (PRIV-CONS-005/006,
PRIV-MINOR-001, preferences in export and erasure, session location) · `05` (INT-MAIL-001
restated for JMAP, INT-MAIL-006, new INT-MAIL-010, INT-SMS-001/002, Apple sending
domain) · `06` (alerts, restriction edits as configuration) · `07` (LIB-HOST-001
preference and governing-language rows) · `08` (CONV-CONTENT-001) · `09` (`/register`,
`/register/events`, `/account/*`, `/admin/restrictions`, `.well-known`, `/auth/begin`) ·
`10` (keys, codes, events, enumerations) · `13` (multi-market documents, SMS as a
restricted factor, HMAC record retention) · `15` (registration pre-hijack, link binding,
restriction bypass) · `16` (REG-MAIL-003) · `17` (SSE on the session cookie) · `18`
(FE-REG-003/004, new FE-REG-005, FE-VER-001, FE-PM-001 to 006, WebAuthn dialog
parameters).

---

## D-147 — Token lifetimes, the KEK rotation operation, the infrastructure definition, `emergency` outside the identifier rules; housekeeping pass four

> **Amended.** A retired key-encryption-key version stays in the secrets manager and the envelope until every backup under it has expired; `model.startup.kekunavailable` is renamed `model.startup.secretunavailable` (D-166).

**Date:** 2026-09-18 · **Status:** accepted · **Extends:** D-007, D-129, D-138, D-144, D-146 · **Resolves:** review-4 M-4, M-7, M-8, M-9, M-10, L-1 to L-22; the question parked in D-144

**TL;DR.** The last review-4 findings that needed a decision, plus the mechanical
leftovers, closed in one pass. Five choices were put to the user with a recommendation
each and approved together.

**Decisions.**

1. **Email OTP is off by default**, like `emailLink` (AUTH-FACT-002), and gains the
   catalogue identifier `emailCode` so that `loginFactors` can name it. The two are the
   same capability, an email alone signs a person in, and a host that wants email-only
   accounts enables one or both. Confirms the default written under D-146.
2. **The infrastructure definition lives in the repository and the restore test rebuilds
   from it** (new DR-017; `19` section 10 amended). Container arrangement, reverse proxy,
   DNS records, volumes, certificate mechanism and the pipeline target are committed;
   DR-007's automated restore exercises them, so the definition is proven current every
   run. The envelope names the repository. *Rejected:* a copy in the envelope, which
   goes stale and cannot be tested.
3. **Token lifetimes, algorithm and rotation are configuration** (`10` section 4.9):
   `oidc.accesstoken.lifetime` 10 minutes (ceiling 1 hour), recorded as the accepted
   revocation latency for anything that validates offline; no refresh-token lifetime key (a refresh token is
   bounded by the session it derives from); `oidc.code.lifetime` 60 seconds (ceiling 10 minutes),
   replacing the hard-coded constant; `token.signing.algorithm` ES256, protected, and
   verified against the mail server's source, which accepts P-256 keys from a JWKS as
   ES256 and refuses HMAC keys; `token.signing.rotation` 90 days with an overlap of the
   access-token lifetime plus five minutes (AUTH-KEY-001).
4. **KEK rotation is a resumable command-line operation** (new OPS-SEC-003; `Janus.Cli`
   gains it beside bootstrap, CONV-LAYOUT-001 amended): run under the maintenance
   credential; introduces a new key version; a batch job re-wraps every subject key and
   tracks progress in its own table so a crash resumes; the previous version stays
   usable until the job reports complete, then is retired; the same command produces the
   escrow copy. Fingerprint-key rotation uses the same shape and is documented as heavy
   and expected never to run. *Rejected:* an endpoint in the management app, because the
   operation needs the protected credential and must not depend on the application being
   healthy.
5. **The identifier rules apply to human accounts** (REG-IDENT-001 amended). `emergency`
   (D-138) and the non-human principals of IDN-PRIN-001 hold no identifiers, sign in
   only through their own path, and have no security-notice set; every use of
   `emergency` alerts the owner destinations (D-129), which are its only channel. This
   closes the question parked in D-144. *Rejected:* giving `emergency` the owner's email,
   which would create a customer-style recovery path for the one account that must never
   have one.

**Housekeeping (review-4 M-4, M-9, L-1 to L-22).** FE-API-004 restated to the D-139 rule.
The enumerations `suspendedBy`, `deletingBy`, privacy request status and type, takedown
trigger, erasure reason and step-up rule shape added to `10` section 5 with one spelling
each. Error codes for self-approval and the named startup failures added. The recovery
link is consumed by one endpoint. The enrolment-session cap follows the link's own
lifetime. `/account/deactivate` and the stepped-up administrator's break-glass
replacement added to `10` section 5a. The required-key count states the conditional key.
The runbook reads `breakglass.session.lifetime` instead of "four hours". `15` section 8
and `13` sections 4 and 5 count the gaps and risks correctly. `00` section 7.3 carries
every deferred item of the log, including hash-chained audit records as the recorded
answer to the separation-of-duties gap. Triggers that depend on host data name who
measures them. The two unnamed recurring reviews are named; the three named reviews (weekly approver
report, monthly consumption, quarterly triggers) are recorded in the maintenance log
that OPS-MAINT-001 now defines and do not count against the two recurring tasks,
because a review only reads. Tax retention wording and
the Article 8 wording are marked as pending counsel. The Unicode stability sentence
claims only what the policy guarantees. The postmark sentence drops its safety claim.
The break-glass credential states its minimum entropy (128 bits) and throttle profile.
`GET /account/photo` and `GET /admin/accounts/{subject}/photo` exist. `oob-request`
deletions send no cancel link to the subject. INF-OBS-003 is the named exception to
OPS-ALERT-001 AC2. The runbook's recovery section covers the lost phone. `/auth/break-glass`
names the `emergency` subject. `PUT /admin/config/{key}` and `POST /account/reactivate`
have bodies and entries. The Google `max_age` sentence says the claim is secondary.

**Propagated to:** `00` (7.3) · `01` (IDN-ACCT-004 AC6, IDN-LIFE-003, IDN-PRIN-001) ·
`02` (AUTH-FACT-002, AUTH-OIDC-003/004, AUTH-KEY-001, AUTH-SESS-012, AUTH-STEP-005,
AUTH-RECOV-002a) · `04` (PRIV-RIGHT-002, PRIV-RIGHT-005, PRIV-RIGHT-005a) · `06`
(OPS-BOOT-004, OPS-ALERT-001, new OPS-SEC-003, section 9) · `08` (CONV-LAYOUT-001,
CONV-GATE-002) · `09` (`/enrol/begin`, `/recovery/complete`, `/auth/break-glass`,
`/account/photo`, `/admin/config`, `/account/reactivate`, `/account/deactivate`) · `10`
(sections 1, 4, 5, 5a) · `11` (3.2, 3.3, 4, 7.1) · `12` (new DR-017, DR-009, DR-011) · `13`
(sections 4, 5, R-A04) · `14` (section 7) · `15` (section 8) · `18` (FE-API-004) · `19`
(sections 6, 10) · `20` (REG-IDENT-001).

---

## D-148 — Staff accounts carry a verified personal email from day one; review-5 closed

> **Superseded in part.** The host business rules and vendor facts this entry records (orders, payments, shipments, couriers, addresses) moved to the host under D-165; the reasoning stands as the host's inheritance.

**Date:** 2026-09-18 · **Status:** accepted · **Amends:** D-146 (REG-MAIL-001, REG-MAIL-003, REG-IDENT-007) · **Extends:** D-147 · **Resolves:** review-5 H-1, H-2, M-1 to M-5, L-1 to L-23

**TL;DR.** The final review of the finished set found two contradictions D-146 had
introduced and five gaps. One needed a decision: what happens to a staff account that
holds only a phone when its corporate address is retired at offboarding, given that
every human account must hold an email. The user proposed the answer written here.

**Decision 1 (the user's).** An invitation into an organization whose mail server is
integrated SHALL name a **personal email**, and the account SHALL NOT be created until
that address is verified. The invitation link already goes to that address, so the press
on the link is the verification and the person does nothing extra. The personal address
stays on the account as a verified, non-primary email for the whole membership. While
the membership lasts, the domain lock of the organization (REG-DOM-001, on for the
administrative organization) already prevents sign-in with it; it remains in the
security-notice set, so a compromised corporate mailbox cannot silence the person. When
the membership ends, the corporate address is retired, the personal email becomes primary
automatically, the lock no longer applies, and the account continues without a pause and
without support. REG-MAIL-001 and REG-MAIL-003 amended; the phone-only continuation and
the suspension path are removed.

*Rejected.* *Suspend the account when only a phone remains and restore through support*:
punishes a person who did everything asked of them. *Allow former staff a phone-only
account as an exception*: creates the one account shape the identifier model forbids, for
the accounts with the most history behind them.

**Decision 2 (mechanical, from the review).** REG-IDENT-007 gains the exception the
recovery chapter already relied on: in an admin-assisted re-enrolment session the
approver's confirmation stands in for the old address, and the new address confirms
alone. Sign-in links and later identifier additions get their own cancel operation
(`POST /auth/link/abandon`, `POST /account/identifiers/{id}/abandon`) instead of reusing
the registration one. The maintenance credential of OPS-MIG-003a is widened to the row
read and write the key re-wrap needs, and INF-HOST-003 names both uses. A takedown's
host-side cancellation gets its per-subscriber completion record at phase one, in the
trigger transaction, not at phase two. Password plus a non-discoverable security key is
phishing-resistant AAL2 everywhere (WebAuthn is phishing-resistant whether or not the
credential is discoverable); AUTH-SESS-005a and AUTH-STEP-002 corrected. `loginFactors`
is the only switch that enables a factor for a principal; `factor.<name>.enabled` is
retired.

**Housekeeping (L-1 to L-23).** Dashes removed from every requirement body sourced
D-146 or D-147. Counts corrected (six policy fields; three seeded roles). R-A10 and
R-A11 added to the trigger table. Runbook and takedown screen words marked illustrative.
The dangling section reference in AUTH-RECOV-007 fixed. Step-up at the acknowledgement
endpoint returns 403. FE-PM-001 AC2 qualified by FE-PM-004. `abuse.nonexistent.window`
named in AUTH-ABUSE-003 and described for both uses. The D-067 note on
`exfiltration.export.stepuprequired` corrected. The client identifier is captured at
session creation. PRIV-MINOR-001 AC3 conditioned on the affirmation policy. INF-BG-001
lists the account-deletion and takedown windows, the outbox publisher, the deadline
alerts and the read-volume baseline. The runbook's impossible phone-only case rewritten.
"Suspend" used for the administrator's action; REG-MAIL-003's suspension path is gone
under Decision 1. The AUTH-SESS-005a row "security key alone" reworded. `any` purpose
excludes security notices to an existing holder; recovery links carry the `notification`
purpose. This log's D-147 sentence on a refresh-token key corrected: no such key exists.
The mail-server claim cites the source file inspected; the provider-domain claim cites
D-146. Completing the terms step remembers the registering browser for the new-device
check. Step 3 is skippable where `registration.phone` is `optional`. The
identifier-mismatch refusal moves from invitation issue to acceptance.
`identity.account.adminsuspended` moves to the identity table.

**Propagated to:** `01` (IDN-LIFE-003, IDN-LIFE-003a, IDN-LIFE-009a, dashes) · `02`
(AUTH-PRIN-002, AUTH-FACT-002, AUTH-FACT-016, AUTH-SESS-005a, AUTH-STEP-002,
AUTH-RECOV-007, AUTH-ABUSE-003, AUTH-KEY-001, dashes) · `04` (PRIV-MINOR-001, dashes) ·
`05` (INT-MAIL-006) · `06` (OPS-MIG-003a, dashes) · `07` (dashes) · `09` (invitations,
`/auth/link/abandon`, `/account/identifiers/{id}/abandon`, acknowledgement status,
dashes) · `10` (sections 1, 3, 4.1a, 4.3, 5.15, 5a) · `11` (sections 3, 4, 7) · `12`
(dashes) · `13` (trigger table) · `14` (section 5) · `16` (steps 2 and 3) · `18`
(FE-PM-001, FE-VER-001, FE-API-004 dashes) · `19` (INF-BG-001, INF-HOST-003) · `20`
(REG-IDENT-007, REG-SESS-002, REG-SESS-007, REG-INV-001, REG-MAIL-001, REG-MAIL-003).

---

## D-149 — Every design and code choice is fixed; the implementer decides nothing

**Date:** 2026-09-18 · **Status:** accepted · **Amends:** D-135 (`InternalsVisibleTo` grants), CONV-LAYOUT-001 (Storage dependencies) · **Extends:** D-017, D-026.4, D-046, D-106

**TL;DR.** The chapters fixed the boundaries of the code (projects, dependency
direction, one operation once, one evaluation seam, two data tools) but left the
choices inside them to the implementer: architectural style, how a use case is
organised, persistence pattern, domain modelling, results, the HTTP layer, dependency
injection, the permitted packages, formatting, and the small conventions that make a
codebase read as one hand's work. The user asked that none of these be left to the
implementer, that the choices be the current mainstream best fit for a security
library of this shape, and that two named conventions govern commits and the
changelog. Chapter `08` now states all of them as requirements enforced by tooling
wherever tooling exists.

**Decisions** (new `08` sections 1a, 1b, 2a; CONV-TEST-007; CONV-VCS-003 and 005;
CONV-GATE-001 rows).

1. **Solution setup.** Current LTS .NET (10, C# 14) set once in `Directory.Build.props`
   with nullable, no implicit usings, warnings as errors, latest-all analysis, style
   enforced in build, documentation file; Central Package Management with locked
   restore; `PublicApiAnalyzers` as the standard form of the public-surface contract
   test; one `.editorconfig` enforced by `dotnet format` in the gate.
2. **Architectural style: layered per area with feature folders.** Each area project
   is organised by feature, never by technical role; the layers exist as the
   dependency direction inside a folder.
3. **Use case = one `internal sealed` service per operation group** implementing the
   `Core` contract, cross-cutting behaviour called explicitly in a fixed order (gate,
   validate, load, decide, persist, audit, publish). No mediator, pipeline, interceptor
   or aspect library.
4. **Persistence = one port per aggregate**, declared in the feature folder,
   implemented in `Storage`; one `DbContext` with one configuration per entity; the
   unit of work is the operation; no generic repository, no `IQueryable` across a port,
   no EF type in an area. **Consequence:** `Storage` depends on the three area projects
   (it implements their ports) and the permitted `InternalsVisibleTo` grants become:
   each area to `Storage`, `Hosting`, `Cli` and its own tests. D-135's "only grant"
   is amended; `Cli`'s grant stands. After the verification pass the grants also
   include every non-Core project to its own test project and `Core` and `Storage` to
   `Hosting` and `Cli`, without which the registration methods cannot be called.
   Migrations apply as an EF Core migration bundle; the destructive-operation report is
   the idempotent script scanned for destructive statements; the serialized model is
   JSON.
5. **Domain types are plain encapsulated classes**: sealed, behaviour methods, no
   public setters, no framework; strongly typed identifiers over UUID version 7;
   values with rules as types that cannot be invalid. `SubjectId` is UUID version 4
   because it is the OIDC `sub` and must stay opaque (IDN-ACCT-002); every other
   identifier is version 7.
6. **Results are the library's own `Result` types**, exposing `Match` and `Switch`
   only so a value cannot be read without handling the failure; a discarded result is
   an analyser error; faults throw; no third-party result library; no `null` for
   not-found.
7. **HTTP = minimal APIs** grouped per area, typed results, the BFF middleware stages
   of `17` for session, CSRF and step-up (never endpoint filters), `sealed record` DTOs with hand-written mapping,
   `System.Text.Json` source generation; no controllers, no reflection mapping.
8. **Dependency injection = built-in container**, one registration method per
   project behind one public `AddJanus`; scoped services and ports; `IOptions` with
   validation on start for static settings, the configuration store for runtime keys;
   `TimeProvider` and `RandomNumberGenerator` injected; secrets come through a
   host-supplied secret source (new LIB-EXT-001 row), the library ships no client.
9. **The permitted package list is closed** (EF Core and Npgsql, Dapper,
   StackExchange.Redis, Konscious Argon2, Fido2NetLib, Otp.NET, OpenIddict, the
   framework OIDC client, PublicApiAnalyzers, MinVer, xunit.v3, Testcontainers). Banned
   by category: mediators and pipelines, object mappers, result libraries, mocking
   frameworks, scheduler libraries, identity frameworks, anything duplicating the base
   class library. MediatR and AutoMapper announced commercial licensing in April 2025,
   which settles that category twice over. Two projects were added to the layout for
   the rules to be enforceable: `Janus.Analyzers` (five Roslyn rules the gates rely on,
   CONV-CODE-008) and `Janus.Conformance` (the shipped suite, the one further public
   project); `Janus.Privacy` was added so chapter `04` has a home like every other
   area; the OIDC provider is a feature of `Authentication` with hand-written stores.
10. **Code conventions**: sealed and internal by default with fixed member order;
    cancellation tokens everywhere, no blocking, `ConfigureAwait(false)`; immutable
    boundary types and read-only collections; no static mutable state, reflection or
    `dynamic`; XML documentation naming the spec item; no `TODO`/`FIXME`/`HACK` or
    commented-out code; validation by guards at the boundary; constant-time comparison
    and cleared secrets.
11. **Tests**: xunit.v3 on Microsoft.Testing.Platform; `Method_Scenario_Outcome`
    naming; hand-written fakes, no mocking framework; one container per test class;
    one test per acceptance criterion carrying the item identifier.
12. **Conventional Commits 1.0.0** for every commit message, checked on push and pull
    request, breaking marker mandatory for contract breaks. **Keep a Changelog 1.1.0**
    for `CHANGELOG.md` (the current version of that specification; no 2.0.0 exists),
    with the release ritual: move `Unreleased`, ship the public-surface file, tag
    `vMAJOR.MINOR.PATCH`; **MinVer** derives the version from the tag and no version
    literal exists anywhere else. LIB-VER-001 names Semantic Versioning 2.0.0 explicitly.

**Rejected.** *Vertical slices with a mediator* (the common template): hides call order
in registration and depends on a now-commercial package. *Clean or onion architecture
as separate projects per layer*: the area split already gives compiler-enforced
boundaries; adding layer projects multiplies projects without adding a boundary that
matters. *Generic repository*: erases the intention-revealing methods that make a port
reviewable and leaks `IQueryable`. *A DDD framework or entity base class*: nothing it
adds is needed by plain sealed classes. *Controllers*: heavier than minimal APIs for a
mapping layer that must contain no logic. *Mocking frameworks*: fakes make the test read
as a scenario and cannot verify implementation details a refactor should be free to
change. *A hand-written OIDC provider*: the one place a security library should not
write its own primitive. *ASP.NET Core Identity*: a different account model, and the
library's identity chapter is the account model. *StyleCop*: the built-in `IDE` rules
under `EnforceCodeStyleInBuild` cover the same ground with no extra package. *Keep a
Changelog "2.0.0"* as requested: the specification's current version is 1.1.0; the
current version is adopted.

**Also produced, outside the specification:** the working guide for whoever implements
(it points at the specification and repeats none of it) and the two-milestone
implementation plan (`guide/implementation-plan.md`).

**Propagated to:** `07` LIB-VER-001 · `08` (header; CONV-LAYOUT-001/002; new 1a, 1b, 2a;
CONV-TEST-007; CONV-VCS-003/005; CONV-GATE-001).

---

## D-150 — Phase 0 questions: test projects under the analysers, the swallowed-exception rule, the secret scanner

> **Amended.** Item 3: the scanner runs as its own checksum-pinned release over the full history, not from the official action, and an allow-list entry names the file and the value (D-167).

**Date:** 2026-09-18 · **Status:** accepted · **Amends:** D-149 (CONV-SETUP-003/004, CONV-CODE-008) · **Extends:** D-042.3

**TL;DR.** The first implementation run stopped at phase 0 with three questions, two of
them defects in D-149's text.

1. **Test projects.** CONV-SETUP-003 enabled public-surface tracking on every project,
   and CONV-SETUP-001's full analysis with warnings as errors applied to test projects.
   Test classes must be public and CONV-TEST-007's names carry underscores, so no test
   project could build. Public-surface tracking now applies to source projects only, and
   CA1707 is set below error in test projects (the fifth and last row of the
   CONV-SETUP-004 table).
2. **Swallowed exceptions.** CONV-ERR-003 AC1 demanded an analyser rule and CONV-CODE-008
   fixed the rules at five, none of which detected an empty catch. JAN0006 is added: a
   catch that is empty, or whose every path neither throws, rethrows, returns a failure
   result nor logs.
3. **Secret scanner.** OPS-DEP-004 required an open-source scanner and named none.
   gitleaks is chosen: the most widely used open-source scanner, run from its official
   action pinned to a release commit SHA, full history on every push, default rules plus
   a committed allow-list that names each entry's file and reason. *Rejected:* trufflehog
   (heavier, verification calls out to providers) and detect-secrets (baseline-file
   workflow suits audits better than a build gate).

4. **Commit bodies.** The first run's bodies narrated the diff in paragraphs. CONV-VCS-003
   now fixes the body form: only when the diff cannot explain itself, dash-prefixed
   fragments of at most 72 characters stating a reason or non-obvious consequence, never
   a description of the change, enforced by the commit check.

**Propagated to:** `06` OPS-DEP-004 · `08` (CONV-SETUP-003, CONV-SETUP-004, CONV-ERR-003,
CONV-CODE-008, CONV-VCS-003, CONV-GATE-001).

---

## D-151 — Phase 0 questions, second stop: names where the reference chapter had prose

**Date:** 2026-09-18 · **Status:** accepted · **Amends:** D-143 (policy object), D-132, D-011 · **Extends:** D-150

**TL;DR.** The reference chapter described six kinds of value in prose where code needs
a name or a type. Each is now a name.

1. **Step-up actions** (`10` section 5a) each carry a `resource:action` name, which is
   the key of the policy's `gates` field and the value the endpoint declares:
   `password:set`, `identifier:add`, `identifier:remove`, `username:change`,
   `factor:enrol`, `factor:remove`, `recoverycodes:generate`, `mailcredential:create`,
   `mailcredential:revoke`, `privacy:export`, `account:delete`, `account:deactivate`,
   `provider:link`, `provider:unlink`, `recovery:approve`, `invitation:issue`,
   `grant:manage`, `account:suspend`, `account:reactivate`, `account:takedown`,
   `account:takedownreverse`, `erasure:complete`, `config:loosen`,
   `alerting:destinations`, `policy:change`, `domain:manage`, `restriction:edit`,
   `restriction:grant`, `breakglass:replace`.
2. **Factor identifiers** (AUTH-FACT-002 gains an Identifier column): `password`,
   `passkey` (the hybrid transport is the same entry), `emailLink`, `emailCode`,
   `phoneLink`, `google`, `apple`, `totp`, `securityKey`, `phoneCode`, `recoveryCodes`;
   `breakGlass` exists but never appears in `loginFactors`; the verification code is not
   an entry. `loginFactors` is a set of these; the system default is the eleven less the
   four off-by-default ones; the administrative organization's is `passkey` only.
3. **Blocklist keys**: `password.blocklist.source` is an enum `rangeApi` · `offline` ·
   `selfHosted`; `password.blocklist.sources` is a set of `leaked` · `dictionary` ·
   `context` with `leaked` unremovable.
4. **Value types.** A rule in the section 4 preamble derives every key's type from its
   default (duration in ISO 8601, integer, decimal, boolean, enum, list or set, string),
   and the eight rows whose defaults were prose now carry a typed default: the working
   week as a set of weekday names, the throttle factor as a decimal, the decay as a
   half-life duration, the WebAuthn algorithm list as COSE integers with −7 required, the
   Argon2 floor as a stated rule over the two keys, the photo dimension as pixels, the
   consent retention as a duration from the end of processing, the MFA floor bounded by
   the single-factor floor.
5. **Error code** `config.value.notallowed` for a value outside a key's enum or set or of
   the wrong type; `PUT /admin/config/{key}` lists it and `config.value.aboveceiling`.
6. **Key families** are one key per organization identifier or declared category:
   `policy.<organization>` defaults to an empty override set; `retention.<host-category>`
   defaults to the floor the host declares and startup fails without one;
   `stepup.enforcement.<organization>` is a boolean defaulting to `true`.

**Propagated to:** `02` AUTH-FACT-002 · `09` `PUT /admin/config/{key}` · `10` (sections
1.5, 4 preamble, 4.1, 4.1a, 4.2, 4.3, 4.5, 4.7, 4.8, 5a).

---

## D-152 — Phase 0 questions, third stop: values where the reference chapter had words

**Date:** 2026-09-18 · **Status:** accepted · **Amends:** D-146 (identifier maximum), D-083 (switch removed) · **Extends:** D-151

**TL;DR.** Seven rows of `10` section 4 gave the code a word where it needed a value.
Each now has a value, and two derivation rules join the D-151 type rule so the next
such row answers itself.

1. **`alerting.destinationchange.notify` is retired.** D-083 made the notice to the
   previous destinations non-suppressible; a switch for it, protected or not, was the
   hole D-083 closed. The key leaves the catalogue; OPS-CFG-004 and `10` section 4.8
   are stated to be one list, so a protected mark that appears in neither is a defect,
   never a third scope.
2. **Sizes are bytes.** `photo.maxbytes` 2097152, ceiling 10485760; `preferences.maxsize`
   8192, ceiling 65536. Binary units, written as bare integers with the unit in the
   Scope column, as the Argon2 memory already is.
3. **Identifier maximums are ten per kind**, floor 1, no ceiling, raising is loosening.
   Unlimited was never a value; every verified address is a send destination and a
   recovery channel, so an unbounded set is an unbounded attack surface. Ten exceeds
   any real need and a host may raise it under OPS-CFG-002.
4. **Bot-defence signals** are a set over two named members, `datacenterRange` and
   `repeatedAttempts`, both on by default; the set is closed until a decision adds a
   member.
5. **The privacy decision deadline** is an integer of working days, default 6, ceiling
   6: the statutory period PRIV-RIGHT-002 cites. Only a shorter deadline can be
   configured. It stays a library constant; a second market with another period is the
   deferred multi-market work, not a declaration.
6. **Grace and cooling-off floors** are written: `organization.deletion.grace` and
   `account.deletion.grace` floor `P7D` (default `P30D`); `takedown.grace` floor `P7D`
   (the default, D-127); `identifier.change.coolingoff` floor `PT72H` (the default,
   D-134, a security window); `identifiers.username.changecooloff` floor `P1D`
   (default `P30D`, an abuse control at host discretion).
7. **Years are held long, not short.** A duration written in years or months is held at
   366 days a year and 31 days a month, so a five-year floor is never shorter than any
   five calendar years. `retention.audit.security` is `P7Y`, floor `P5Y`;
   `retention.consent` keeps `P3Y`, floor `P1Y`. No calendar type, no package.
8. **Direction rule.** Where a row names no loosening direction: ceiling only loosens
   upward, floor only loosens downward, boolean loosens away from its default,
   anything else loosens on any change (D-079b). Replaces the interim reading that
   every direction-less key loosens on any change, which would have charged a
   tightening the friction OPS-CFG-002 makes free.

Housekeeping in the same pass: `on` defaults are written `true`;
`alerting.sms.severitythreshold` is an enum `high` · `normal`.

**Propagated to:** `06` OPS-CFG-004 · `10` (section 4 preamble, 4.5, 4.6, 4.7) · `20`
REG-IDENT-002, REG-PREF-001.

---

## D-153 — Word-shaped values: one pass over every chapter

> **Amended.** A flood limit counts an IPv6 source by its /64 with a /48 site count; the word lists are chosen (the 12dicts 3esl list and an original Arabic transliteration list); `backup.restoretest.interval` is `P90D`; the bootstrap command prints `<origin>/link#enrolment.<token>` (D-166).

**Date:** 2026-09-18 · **Status:** accepted · **Amends:** D-083 (destination list refusal code), D-045 (read-volume rule), D-051 (distance rule), D-008 (recovery limits), D-013 (challenge, drain rule), D-024 (materiality), D-030 (sensitive retention), D-036 (register inputs), D-060 (photo formats), D-097 (primitives) · **Extends:** D-151, D-152

**TL;DR.** Three phase-0 stops in a row were the same defect: a chapter written for a
reader gave a word where a compiler needs a value. Rather than pay one stop per phase,
every chapter was read once for that defect. 113 raw findings, 88 distinct; every one now
has a value. Seven were owner decisions; the rest are mechanical fills.

**Owner decisions.**

1. **Alert thresholds are keys.** Ten OPS-ALERT-001 conditions said "sustained",
   "unusual", "spike", "implausibly distant", "abnormal". Each is now a runtime key in
   `10` section 4.5 with a conservative default (twenty failures an hour on one account;
   three recoveries a day for one account or three approvals for one approver; fifty
   denials in ten minutes per actor; three times the actor's daily read mean and at
   least 500 records; two sessions an hour apart in cities over 500 km apart or in
   different countries; twenty non-existence notices an hour; ten rejected callbacks an
   hour from one source; SMS spend three times the seven-day hourly mean; a restore test
   over eight hours). Raising a threshold is loosening. A fixed constant would mean a
   redeploy to tune an alert.
2. **A flood limit per source**: `abuse.source.ratelimit` 300 requests a minute, sliding.
   Sixty, the number a reviewer reaches for, would lock out an office or a mobile
   carrier behind one address; 300 stops a naive flood and lets a busy shop floor work.
3. **A deployment time zone**: `privacy.calendar.timezone`, required and protected. A
   six-working-day legal clock has to know what day it is where the company is; fixing
   UTC could lose or gain a day against a statutory deadline. The six working days are
   the six calendar days in that zone after the submission's day; `decisionDue` is the
   end of the sixth. `receivedAt` is a calendar date in that zone.
4. **Materiality is the publisher's call.** `POST /admin/documents/{document}/versions`
   takes a required boolean `material`. Code cannot judge whether a legal text changed
   substantially; the human publishing it says so and the audit record keeps the answer.
5. **What the mail server learns**: `sub`, primary `email` and `email_verified`,
   display `name`, `preferred_username` where usernames are on, `locale`. No phone, legal
   name, date of birth or photo. More is the deferred third-party work.
6. **The library ships no challenge.** Bot defence calls a host-declared verifier; with
   none declared the signal is audited and no challenge appears. A CAPTCHA is a
   third-party product with its own privacy notice and would be a dependency `08` does
   not list. `repeatedAttempts` is more than three registration sessions from one source
   in an hour.
7. **The offline password lists** are the 100,000 most prevalent Pwned Passwords SHA-1
   hashes plus a public-domain word list with an Arabic transliteration list; the
   licence is verified from the provider's terms before the file enters the package.

**Lead decisions, by kind.**

- *Undecidable criteria made decidable.* "Within one request cycle" is the first request
  that reaches the session record after the commit. Timing criteria ("overlap within
  noise", "does not vary measurably") are verified by construction (one code path,
  fixed-time comparison, identical bytes) and asserted through byte identity; no
  statistical benchmark runs in CI, because a flaky test is worse than none.
  "Production-scale volume" is one fixture: a million resources, a million grants (10%
  revoked), a hundred thousand principals, ten thousand groups. A job that has not run
  is one whose last success is older than twice its interval. One sweep every
  `sweep.interval` (five minutes) fires every deadline.
- *Names.* Every alert condition has a kebab-case identifier (`10` section 5.23) that
  `AlertRaised` carries and deduplication keys on. Nine error codes were added, among
  them `system.fault` (500), `config.value.lastdestination`,
  `model.startup.declarationmissing` and three preference refusals. Three closed sets
  were added: capability residuals (5.20), consent mechanism (5.21), age group (5.22,
  `minor` · `adult`). Cookies are `__Host-identity-session`, `-preauth`, `-csrf`, `-device`,
  `-browser` (D-163: the `identity-` prefix); the CSRF header is `X-Identity-Request`. The DNS record is
  `_identity-verify.<domain>`. The CLI verbs are `bootstrap`, `rotate-kek`,
  `rotate-fingerprint-key`, `replay-erasures`.
- *Shapes.* The registration state document, the SSE events, `policyRequirement`,
  `passwordChangeRequired`, the device description (`{ browser, os }` from the
  user agent), the location (`{ city, country }`), the explanation record, the
  prose-only list responses (camelCase of the noun in the chapter's vocabulary), the
  recipients declaration, the restriction supplier signature, the ledger line, the
  progress row, monthly audit partitions. Free text is 1 to 1024 characters after
  trimming, one rule.
- *Security parameters.* AES-256-GCM per field with a per-subject key wrapped under the
  KEK with AES key wrap with padding (RFC 5649); marker byte `0x01`, erased key 32 zero
  bytes under `0x00`. HMAC-SHA-256 fingerprints, neutralised to 32 zero bytes. Recovery
  codes 10 Crockford base32 symbols. Break-glass 27 data symbols in nine groups of
  three plus a weighted check symbol modulo 32. Correlation references 128 bits.
  Callback secrets overlap 24 hours. The enrolment link prints once to the bootstrap
  command's output, since the mailbox is only queued.
- *New keys* beyond the thresholds: outbox schedule (`outbox.poll.interval`,
  `outbox.retry.*`), `notification.languages` (required; "both languages" was never a
  value), `notification.email.sendingdomain` (required) and `.relayregistered`,
  `domain.reverify.interval`, `location.database.refresh` and `.maxage`,
  `recovery.ratelimit.*`, `recovery.invalidation.noticeinterval`, `service.name`,
  `hosting.environment`, `backup.restoretest.*`, `integration.callback.ratelimit`,
  `abuse.botdefence.repeatedattempts`, `sweep.interval`. The deployment values are now
  eleven, with three conditional.
- *Privacy semantics.* PRIV-SENS-002's "stricter retention defaults" is dropped from
  code: there is no library default a sensitive category could be stricter than, so
  the declaration is required like any other. `retention.consent` counts from
  withdrawal or, failing that, erasure. Photos accept JPEG, PNG and WebP by content and
  store JPEG at quality 85 with metadata stripped. The reactivation link lives while
  the account is self-suspended.

**What this says about the reviews.** Five review passes checked coherence and
completeness of meaning, never machine-readability of constants. This pass closes that
gap for the values found; some will still surface only when code is written, at a far
lower rate than one per phase.

**Propagated to:** `01` IDN-ORG-003, IDN-LIFE-003a, IDN-LIFE-008, IDN-LIFE-013,
IDN-ATTR-001, IDN-ATTR-004 · `02` AUTH-FACT-001, 002b, 008, 015, 016, 017, AUTH-PASS-004,
AUTH-RECOV-002, 007, AUTH-ABUSE-001, 003, 005, 006, 007, 008, AUTH-SESS-011, 013 · `03`
AUTHZ-MODEL-004, AUTHZ-GATE-004, 005, AUTHZ-GRANT-003, AUTHZ-DERIVE-004, 007,
AUTHZ-CONCEAL-002, AUTHZ-TEST-002 · `04` PRIV-CONS-001, 007, PRIV-SENS-002,
PRIV-RIGHT-002, 005a, 005c, PRIV-RET-001, 002, PRIV-ROPA-001, 002 · `05` INT-GEN-003,
006, INT-MAIL-006a, 011, INT-SMS-004, INT-PWD-001 · `06` OPS-DB-001, 003, OPS-DEP-001,
OPS-ALERT-001, 002, 004a, 005, 007, OPS-BOOT-001, 004, OPS-SEC-003, OPS-OBS-003,
OPS-MAINT-001 · `07` LIB-HOST-001 · `09` API-CONV-002, 005, and the endpoints named ·
`10` sections 1, 4 (preamble, 4.4 to 4.8), 5.20 to 5.23, 5b, 6 · `12` DR-007, DR-016 ·
`17` BFF-SESS-002, BFF-CSRF-003, 005a, 006, BFF-MACH-002, 003, BFF-ORDER-001,
BFF-ERR-002, 003, BFF-LOG-002 · `19` INF-BG-001 · `20` REG-IDENT-003, 009, REG-DOM-001,
REG-PROF-001, 002, REG-PREF-001, REG-SESS-005.

---

## D-154 — Phase 1 questions: the library carries its own Unicode tables

**Date:** 2026-09-19 · **Status:** accepted · **Amends:** D-115, D-149 (layout, `.editorconfig` table, gates) · **Extends:** D-153

**TL;DR.** The identifier rules name three Unicode operations the base class library
does not have. The right answer is not a package and not a weaker form: the library
generates its own tables from the Unicode Character Database at a pinned version and
ships them. Two smaller questions are settled alongside.

1. **`NFKC_Casefold`, PRECIS and `Script_Extensions` come from generated tables.**
   `string.Normalize` follows whatever ICU the machine has, so the version pin
   IDN-ACCT-004 requires (and the fingerprint record depends on) was never real with
   built-ins. No maintained .NET package implements PRECIS or `NFKC_Casefold`, and a
   dependency for the operation that every fingerprint is derived from is the wrong
   place to accept churn. A tool project outside the package, `tools/Janus.UnicodeTables`,
   reads the vendored UCD files of Unicode 17.0.0 and writes the tables into
   `Janus.Core`; a gate regenerates and fails on a diff. The library's NFC and NFKC are
   its own, so the pinned version governs every step. Public types `CanonicalForm`,
   `Precis`, `ScriptMixing` in `Janus.Core`, since two areas canonicalise. This is a
   real piece of work (UAX #15 normalisation, RFC 8264 classes, the bidi rule of RFC
   5893 for right-to-left usernames, UTS #39 §5.1), and it is the correct one.
2. **PRECIS and D-115 are not in conflict.** D-115 rejected PRECIS as the canonical
   comparison form for emails and names; D-146 adopted it as the validation profile
   for usernames and display names. `NFKC_Casefold` remains the comparison key of every
   identifier; PRECIS says what a username or display name must look like to be
   accepted. D-115's rejected item now says so.
3. **CA1515 is off in test projects.** Fixtures and test classes are instantiated by the
   framework and must be public; a justified suppression on every fixture is noise, and
   CONV-SETUP-004 says a recurring deviation is a specification defect, which this was.
4. **OPS-DATA-002's visibility test** lives in `Janus.Storage.Tests` and uses an entity
   Storage itself owns (a settings row), so no `InternalsVisibleTo` grant changes.

**Propagated to:** `01` IDN-ACCT-004 · `06` OPS-DATA-002 · `08` CONV-LAYOUT-001,
CONV-SETUP-004, CONV-GATE-001 · D-115 (rejected item annotated).

---

## D-155 — Phase 1 questions, second stop: EF Core maps records, the port encrypts

**Date:** 2026-09-19 · **Status:** accepted · **Amends:** D-149 (CONV-DESIGN-003), D-153 (OPS-DB-001 value) · **Extends:** D-154

**TL;DR.** Per-subject encryption needs the row's subject identifier, and EF Core's value
converters cannot see the row. The answer is the one that also keeps domain entities
plain: EF Core never maps a domain entity, it maps a persistence record owned by
Storage, and the port implementation that translates between the two is where the
field cipher runs.

1. **Persistence records.** Each table is an `internal sealed class` in `Janus.Storage`
   with one property per column, encrypted columns as `byte[]`, beside its
   `IEntityTypeConfiguration<T>`. The port implementation maps aggregate to records and
   back, calling the field cipher with the subject identifier from the record's declared
   subject column and the table and column as associated data. Rejected: value
   converters (no access to the row), `SaveChanges` and materialization interceptors
   (an implicit security boundary that a reader of the port cannot see), and mapping
   domain entities directly (forces setters or backing-field tricks onto types
   CONV-DESIGN-004 wants plain). The cost is one mapping per aggregate, written by hand
   and read in one place, which for a security boundary is the point.
2. **Kind detection at `/auth/begin`** (REG-IDENT-003): `@` means email; digits of any
   script with the usual separators and a `+` or `00` prefix mean phone; anything else is
   a username where usernames are on, otherwise the concealed path. So that the second
   and third never overlap, a username must contain a letter; an all-digit choice is
   refused with `identity.username.invalid`, a code the profile's other refusals also
   use.
3. **`identity_ci` applies to what is plaintext and spelled by a person**: organization
   names and locked domain names today. Identifiers are fingerprints and personal fields
   are ciphertext; a collation cannot see through either, and D-153's wording that named
   display names and usernames was wrong.

**Propagated to:** `08` CONV-DESIGN-003 · `06` OPS-DB-001 · `20` REG-IDENT-003,
REG-IDENT-009 · `10` section 1.1.

---

## D-156 — Phase 1 questions, third stop: a port is tested against its aggregate; test infrastructure is Tier 1

**Date:** 2026-09-19 · **Status:** accepted · **Amends:** D-149 (CONV-LAYOUT-002 grants), the working guide (rule tiers) · **Extends:** D-155

**TL;DR.** D-155 made the port implementation the one place encryption happens, and the
grant list then left no project able to test it: Storage's tests could see the store but
not the aggregate, an area's tests the reverse. The grant list gains one row. And because
this stop was purely about where a test may live, that whole class of question is moved
to Tier 1 so it never ends a run again.

1. **Each area project grants `InternalsVisibleTo` to `Janus.Storage.Tests`.** A port
   implementation is verified against the aggregate it translates, in Storage's own
   test project, with the real database. Rejected: verifying ports only end to end from
   `Janus.Hosting.Tests` once endpoints exist, which would leave every translation, and
   the field cipher inside it, untested for four phases.
2. **Test infrastructure is Tier 1.** A project reference, a grant to a test project, an
   analyser scope in a test project or a fixture arrangement, when it touches no runtime
   code and no shipped surface, is resolved by the implementer with the least change,
   including to the gate test that enforces the list, and recorded; the owner brings
   `08` into line afterwards. Three of the last eight stops were of this kind, and none
   of them had a second defensible answer.

**Propagated to:** `08` CONV-LAYOUT-002 · the working guide section 3.

---

## D-157 — Phase 1 questions, fourth stop: the photo is unreadable, the administrative flag, the role names, retention by argument

**Date:** 2026-09-19 · **Status:** accepted · **Amends:** D-060 (AC wording), D-038, D-018, D-118 · **Extends:** D-156

**TL;DR.** Three small gaps at the end of phase 1, one of them a genuine wording
contradiction of my own making.

1. **The photo.** IDN-ATTR-003 says the photo row survives erasure with its bytes
   unreadable; PRIV-RIGHT-005 AC4 said the photo is "removed". The first is the design
   (D-060, IDN-PRIN-003: nothing is deleted, keys are destroyed); the second was loose
   wording. AC4 now says unreadable, row persists, and `GET /account/photo` answers as
   for an account with no photo. The eraser touches no photo row and is correct.
2. **The administrative organization** is a boolean `administrative` on the
   organization row, set by bootstrap only, with a unique partial index so exactly one
   exists; `Organization.IsAdministrative` is what IDN-ORG-004 checks. Rejected: a
   well-known fixed identifier (leaks structure into an opaque id) and a settings key
   (a domain invariant does not live in configuration).
3. **Database roles** are `identity_migrate`, `identity_app`, `identity_maintenance`; the
   migration creates the runtime roles `NOLOGIN` if absent and the deployment attaches
   credentials.
4. **`audit_drop_expired_partitions` takes the two retention periods as arguments**,
   passed by the worker from the catalogue's effective values, since a key at its
   default has no settings row for the database to read. The function refuses an
   argument below the PRIV-RET-001 floor, written into it by the migration, so the
   maintenance role cannot shorten retention by argument.

**Propagated to:** `01` IDN-ORG-001 · `04` PRIV-RIGHT-005 AC4, PRIV-RET-002 · `06`
OPS-MIG-003.

---

## D-158 — Phase 1, fifth stop: merge commits are outside the message rule; a gate's own defect is Tier 1

**Date:** 2026-09-19 · **Status:** accepted · **Amends:** D-149 (CONV-VCS-003), the working guide (rule tiers) · **Extends:** D-157

**TL;DR.** The commit-message gate has been red on `main` since the first merge because
it inspects the merge commit the platform writes. A merge commit carries no change and
was never what CONV-VCS-003 is about; the gate skips two-parent commits. And a gate that
fails on something its own chapter does not say is a defect in the gate, which the implementer
fixes and records rather than stopping for.

Rejected: a Conventional message on every merge commit. It would have to be typed at
merge time by whoever merges, it says nothing the pull request title does not, and one
forgotten merge would redden `main` again for a reason with no substance.

**Propagated to:** `08` CONV-VCS-003 · the working guide section 3.

---

## D-159 — Phase 2 question: the filter is a same-context subquery over the contract tables

**Date:** 2026-09-19 · **Status:** accepted · **Amends:** D-017 (made concrete), D-149 (Hosting public surface) · **Extends:** D-158

**TL;DR.** A permission filter over a host's row has to consult the library's ancestry
and grants, which are not on that row. D-017 already made the ancestry closure public
contract because hand-written SQL queries it; the LINQ side now uses the same fact. The
host maps the two contract tables into its own context and hands their sets to the
filter, and the expression is a correlated `EXISTS` subquery EF Core translates on its
own. Nothing new is asked of the host beyond one `ModelBuilder` call.

**The shape.** Two steps, shared by both renderings. The library first resolves the
principal's subject set (account, groups by closure, organization roles): small, bounded,
read once from the library's store. The rendering is then a predicate over the host's
row: does a live, non-denied grant for this permission exist for one of those subjects
on the resource or any ancestor. LINQ: `Expression<Func<TResource, bool>>` built from the
host's identifier selector and the host's `IQueryable<AncestryEntry>` and
`IQueryable<EffectiveGrant>` (public records in `Janus.Core`, mapped by
`MapAuthorizationTables(ModelBuilder)` in `Janus.Hosting`). SQL: the same `EXISTS` over
`identity.ancestry` and `identity.effective_grants` with alias and column from the caller and
everything else parameterised. Both derive from one rule object; the truth table runs
through both.

**Rejected.** *Closing over the permitted identifiers* (reading 2): a grant on a
container makes that set the size of the container, which is the post-filtering
AUTHZ-PRIN-002 forbids under another name. *The expression as the library's own
evaluation path and the fragment as the host's* (reading 3): it reads "composable into
LINQ" out of AUTHZ-GATE-002, and D-017 pairs each rendering with one of the host's two
tools for a reason.

**Propagated to:** `03` AUTHZ-GATE-002 · `07` LIB-HOST-002 · `08` CONV-LAYOUT-002
(Hosting's public surface).

---

## D-160 — Phase 2 questions, second stop: derivations are host-supplied relations; reading and modifying; the gate binding; database-backed validation

**Date:** 2026-09-19 · **Status:** accepted · **Amends:** D-043, D-037, D-078, D-015 (startup) · **Extends:** D-159

**TL;DR.** Four gaps in the authorization chapter, one of them a real contradiction.

1. **Derivations do not break LIB-HOST-002.** The library never queries a host table;
   the host supplies the relationship as a queryable from its own context, exactly as it
   supplies the ancestry and grant sets under D-159, and the composed `EXISTS` runs in
   the host's query. The declaration carries the selectors for the LINQ side and the
   relation and column names for the SQL side. A single check on a derived type is the
   filter applied to one resource through the host's query. Rejected: a library-side
   join into host tables (what AUTHZ-DERIVE-004's wording implied), which would be the
   one thing LIB-HOST-002 exists to forbid.
2. **Reading or modifying** is a property of every action: `read`, `list` and `export`
   are reading by name, everything else is modifying unless declared reading. Fail
   closed: an unclassified action is modifying. Restriction allows the account's own
   reading actions and refuses the rest.
3. **The `stepup` residual comes from the gate bound to the action**, declared in the
   model builder for host actions and listed in section 5a for library actions. A gate
   name was never a permission string (D-151); LIB-HOST-004's wording is corrected.
4. **Startup validation that reads the database** runs in a hosted service registered
   before the web server, failing the process before it serves; `IHostedService` is in
   the shared framework, so no package. `Janus.Cli` runs the same validation first.
   Rejected: a blocking call in `AddJanus` (synchronous database access at registration)
   and a public `ValidateAsync` the host must remember to await (a startup check the
   host can forget is not a startup check).

**Propagated to:** `03` AUTHZ-DERIVE-001, AUTHZ-GATE-005, AUTHZ-GATE-006, AUTHZ-MODEL-004
· `07` LIB-HOST-004.

---

## D-161 — Phase 2, third stop: derived checks take sources; refresh is the host's call; the implementer decides alone through Milestone 1

> **Amended.** The working mode of item 4 ends; Tier 2 and Tier 3 questions stop and ask again. The drift check of item 2 reads the relationship sources the host declares (D-166).

**Date:** 2026-09-19 · **Status:** accepted · **Amends:** D-160, D-043, the working guide (rule tiers) · **Extends:** D-160

**TL;DR.** Three derivation gaps closed, and a change of working mode: the owner cannot
attend the remaining stops, so the implementer decides and records instead of stopping.

1. **Single check and capabilities on a derived type** take the same host-supplied
   sources the filter takes; without them the call is refused with
   `authz.derivation.sourcesmissing`, a fault, so no path answers from stored grants
   alone.
2. **Materialised refresh** is the host's call, `IDerivationMaterialiser.RefreshAsync`,
   inside the host's own write; a daily drift check on the sweep
   (`derivation.materialised.driftcheck`) re-evaluates and corrects, raising
   `degradation` on a difference.
3. **Reverse lookup over derivations** is the phase 8 view: stored and materialised
   grants by query, unmaterialised derivations evaluated over the host-supplied relation
   within `authz.reverselookup.budget`, `partial: true` past it.
4. **Working mode.** Tier 2 questions are decided by the implementer (most consistent reading,
   fail closed, smaller surface, no package) and recorded under **Decided in the owner's
   absence** with the chapter text that should change; Tier 3 questions are decided the
   same way with the strictest reading. The chapters are not edited by the implementer; the
   owner reconciles them from the report entries afterwards. The Milestone 1 exit gate
   and the ban on Milestone 2 stand.

**Propagated to:** `03` AUTHZ-DERIVE-001, 005, 007 · `10` sections 1.3, 4.5a ·
the working guide sections 3 and 6.

---

## D-162 — Review of the 109 decisions taken through phase 7: 27 reversed, 14 settled, the rest kept

> **Amended.** Items 22, 23, 26, 31 and 66, C.55, C.68 at `POST /auth/link`, C.103 and the status of `identity.identifier.invalid` in section E are revised, and the reconciliation pass it promised is done (D-166).

**Date:** 2026-09-22 · **Status:** accepted · **Amends:** D-149, D-153, D-155, D-160, D-161 and the items named below · **Extends:** D-161

**TL;DR.** The ledger of decisions taken in the owner's absence (phases 2 to 7, entries 1
to 109) was audited entry by entry against the chapters, together with the 88 Tier 1
resolutions and the code-to-chapter comparison. 68 decisions stand and their chapter text
is reconciled in a later pass. 27 are reversed here, each with the correct handling. 14
were open choices; each is settled here. Twelve Tier 1 resolutions failed a condition; the
ones that matter are reversed below. Nine error codes and one enumeration the code
defines gain their `10` rows in the same pass. This entry is the specification for every
point it settles; the ledger entries it names are superseded by it.

### A. The repository carries a trace and it comes out

Ledger 1 committed the guide copy of the working instructions. That file, whatever it is called, is an
instruction file for the tooling and is excluded from history by rule; the decision that
committed it was marked Tier 3 and took the weaker reading. **Instruction:** remove
the guide copy of the working instructions from the repository and from every commit of its history with a
history filter, temporarily lifting the force-push protection on `main` for that one
operation and restoring it after; restore the exclusion pattern so a file of that name
is excluded at any depth; verify with a whole-history search that no commit, message or
file names an instruction file, a tool, a model or a vendor. The decision log and the
implementation plan were corrected on the owner's side in this update for the same
reason (they said "agent"; they now say "implementer").

### B. Reversals

Each paragraph names the ledger entry, what was wrong, and what to build. A reversal is
the specification for its point.

**2.** The refusal `authz.derivation.sourcesmissing` applies on any type that a
non-materialised derivation is declared on or reaches through containment, whatever the
derivation confers; the narrowing to "a derivation that confers what is asked" makes a
host call site pass until an administrator edits a role, then fault. Materialised
derivations stay excluded.

**3.** `ExplainAsync` takes the same sources as the check and the page; without them on
a derived type it is refused with `authz.derivation.sourcesmissing`; with them it names
the deciding grant, a derived one as `{ id: null, kind: derived, subjectType, subjectId,
role, deny: false, inheritedFrom }` (`10` 5.6 already has the kind). Refusing every
explanation on such a type removed a SHALL operation.

**4.** The capability page is one host-context query: beside the stored-grant
capabilities it projects one `EXISTS` per derivation reaching the type, and the role each
derivation confers is model data mapped in memory. AUTHZ-GATE-005 AC1 ("without
additional queries") stands as written.

**11.** A password beyond `password.maximum` is refused with `auth.password.toolong`
(422), a new `10` 1.2 row; `config.value.aboveceiling` is a configuration-management code
and would put a configuration sentence on a password field.

**12.** The offline leaked list is a resource embedded in the package, dated, working
with no deployment file (AUTH-PASS-004 Values). The self-hosted corpus address is a new
key `password.blocklist.selfhosted.address` (string, required only when
`password.blocklist.source` is `selfHosted`, the `service.name` pattern).

**17.** The verification-code aggregate of AUTH-FACT-004 and its port are built in
`Janus.Authentication` now (lifetime `code.verification.lifetime`, attempt cap
`code.verification.attempts`, invalidation on the cap, single use, its own storage per
AC2); the device check of AUTH-FACT-016 issues its code through it. AC2 and AC3 are proved.

**19.** `IConfigurationStore` is implemented in `Janus.Storage` over the `settings` table
using the `10` section 4 value grammar (a stored value that does not parse is a fault),
and every authentication service is registered in `AddJanus`. This was phase 0's item.

**20.** The session takes the client address it already records; an internal resolver
port maps address to `{ city, country }`; until INT-GEN-006 is built the implementation
answers no location and raises the degradation. No caller-supplied place on any signature.

**22.** The restriction model (keys, purposes, buckets, grants, evaluation) stays in
`Janus.Authentication`. The notification-handling contract (send a message kind to a
subject in a language) is declared in `Janus.Core` beside the transport ports; the
pipeline with retry, template resolution, the outbox publisher and the alert router live
in `Janus.Hosting` with the hosted worker (CONV-LAYOUT-001, LIB-EXT-001).

**23.** Every send is written as a row in the library's own outbox table inside the
caller's transaction and delivered by the worker under `outbox.retry.*` with status
recorded (D-022, INF-BG-001); until the publisher exists (phase 9) the send path attempts
once at commit and leaves the row in its recorded state. A refused or failed delivery
counts against no bucket. Exhaustion raises the `degradation` condition.

**26.** At startup every template in every language of `notification.languages` is
rendered with the maximum-width value of each placeholder (defined once per placeholder
beside the message kind) and refused where it exceeds the single-message budget (70
non-GSM, 160 GSM-7). No measurement at send. "Any rendered template" stands.

**29.** `IEvents.PublishAsync` returns `ValueTask<Result>`; on failure the outbox row
stays unmarked for the publisher and the degradation is raised; on success the row is
marked. No exemption for ports from CONV-DESIGN-005.

**31.** No settle column on a destination record. Before a send's counters are read,
destination rows whose newest timestamp is older than the longest interval any current
destination restriction declares are deleted (one indexed delete). The record holds the
HMAC and timestamps only, and a tightened interval is honoured for sends already counted.

**32.** `Janus.Hosting` ships the default message catalogue (LIB-EXT-001) in the
languages the library carries; a host may replace it. The startup check refuses only a
message with no text in a declared language or over budget. No deployment fails startup
for declaring no catalogue. The default texts are the owner's to review before release.

**34.** The general configuration audit is built now: every runtime write goes through
one operation taking the access context and a reason, classifying direction per `10`
section 4, requiring step-up and a non-empty reason for a loosening or a no-direction
change, writing actor, key, before, after, timestamp and reason, queryable by setting and
by actor (OPS-CFG-002, OPS-CFG-005). `IConfigurationStore.WriteAsync` is called only from
it; the alert-destination change goes through it.

**46.** `POST /register` from a browser holding a live session creates nothing and
answers 409 with `identity.registration.signedin` (new `10` 1.1 row); the frontend
navigates to the account application. No account document crosses that boundary.

**49.** A request the reader cannot parse answers 400 with the API-CONV-002 body:
`api.request.malformed` (new `10` 1.5 row), `correlationId`, `details` naming the
offending member and nothing of its value.

**50.** The passkey-pages declaration is required with no default and its absence fails
startup with `model.startup.declarationmissing`; it joins LIB-HOST-001's table.

**58 and 59.** Degraded mode is removed. The OpenIddict store interfaces (application,
authorization, token, scope) are implemented in `Janus.Storage` over the library's
tables; the server's own handlers validate clients and issue codes and tokens; the
library adds only the session-bound minting of AUTH-OIDC-004 and the client kind of `09`
section 9. The server's signing credentials come from the library's key store through a
credential source the rotation job updates in process; no ephemeral pair.
`IOidc.FindClientAsync` no longer answers `authz.denied`; the protocol error is the
server's.

**61.** The authentication application's sign-in address is a required declaration
(LIB-HOST-001 row), absent fails startup with `model.startup.declarationmissing`;
`login_required` is answered for `prompt=none` only.

**66.** The client half of BFF-SESS-006 is the library's middleware (BFF-OWN-001): the
host declares each application's client identifier (LIB-HOST-001 row), the client secret
comes through the secrets-manager path of OPS-SEC-001, the flow runs with no token
retained.

**75.** The registration ceremony's `begin` carries `user.id` equal to the subject
identifier (the registration session's provisional handle, reserved as the future
`SubjectId`), `user.name` the primary email, `user.displayName` the display name or empty
(REG-PM-001, REG-SESS-001); the assertion path resolves the account from the returned
handle.

**83.** No `privacy.denied` exists and `authz.denied` is wrong for the three cases. New
`10` 1.4 rows: `privacy.document.notfound` (404, a document or version that does not
exist or was never published), `privacy.purpose.noconsent` (422, a grant or withdrawal on
a purpose that is undeclared or not consent-based), `privacy.notice.unpublished` (409, a
grant before any notice version exists).

**89.** The `consent` residual and `privacy.consent.required` are evaluated against the
consent of the record's data subject, resolved from the subject column the type declares
for its encrypted fields, for the purpose bound to the action; a system or staff principal
is gated identically; a consent-based purpose bound to an action on a type with no subject
column fails startup validation. PRIV-SENS-002 AC1 reads "without the data subject's
recorded written consent, whoever the caller is".

**93.** Under `/admin` nothing is concealed: a missing permission is 403 `authz.denied`,
an identifier naming no row is 404 `privacy.request.notfound`, a request already decided
is 409 `privacy.request.decided` (two new `10` 1.4 rows).

**104.** The children's column of the register is true for a purpose exactly when its
type declares the `children` sensitivity category; when `registration.adultaffirmation` is
`off` and no type declares it, the register carries the flag `children-undeclared`
instead of an invented column.

**Tier 1 reversals.** The `identity_ci` collation is created in the `identity` schema
(`COLLATE identity.identity_ci` is valid; OPS-DB-002 applies). The grants of OPS-MIG-003a are
listed in the serialized model output (`artifacts/model.json`), not only in the
migration. Every gate refusal carries `correlationId`, including one for an anonymous
principal. `AuditAction` is a closed vocabulary: list every member in the ledger for a
`10` section 5 row and add no member without listing it. `MessageKind` is likewise a
closed set: list its members in the ledger for a `10` section 5 row. `PreferenceKind`
spells its members as LIB-HOST-001 does: `string` · `boolean` · `integer` · `enum`.

### C. The fourteen open choices, settled

**10.** The synchronizer token travels in `X-Identity-Csrf`; `X-Identity-Request` stays a
presence check. D-153's sentence naming one header is corrected. **27.** The shipped
default transports take `10` keys `integration.mail.endpoint` and
`integration.sms.endpoint` (protected, required only when the default transport is used);
INT-GEN-001's check names the key; no host-declared endpoint register. **35.** A removed
identifier stays reserved for the undo window; an attempt to take it answers exactly as a
duplicate does. **41.** Stage 5 clears a dead cookie and leaves the request anonymous;
the requirement of a session is asserted once in the pipeline for endpoints that need one
and answers 401 `auth.session.expired` there. **48.** The registration event stream is
driven by PostgreSQL `LISTEN`/`NOTIFY` raised in the transaction that verifies or
completes a step, with a poll fallback every `registration.events.pollinterval` (new key,
default `PT1S`, floor `PT1S`). **55.** The photo stays in the library (D-060) and the
codec does not: a host-declared **image codec** (LIB-HOST-001, optional) validates by
content, bounds and re-encodes to JPEG with metadata removed; the library stores what it
returns, encrypted; while any policy enables photos and no codec is declared, startup
fails with `model.startup.declarationmissing`. The photo endpoints and their three codes
are built. **60.** API-REDIR-001's list is the set of origins of the registered clients'
redirect addresses, and the default is a named first-party client in a new key
`redirect.defaultclient`, validated against the registry at startup. **68.** On a `risk`
answer the SMS factor is withheld for that sign-in and the challenge offers the account's
other factors; `phoneLink` alone is refused with `auth.factor.rejected`. **82.** A purpose
declaration names the document that governs its consent, defaulting to the privacy
notice; a material revision of a document supersedes the live consents of the purposes
that name it; the consent record names that document's version. **87.** A grant for a
purpose on which the subject holds a superseded, unwithdrawn consent records `reconsent`;
any other grant records `dashboard`. **98.** `POST /account/reactivate` takes `linkToken`;
the `09` block is corrected. **102.** The export carries every group `GET /account`
shows the person, credentials included, and the whole Standing group. **103.** The
shipped provider register applies by default only the rows the library itself makes true
(password screening while online screening is configured, the hosting provider from
`hosting.location`, the mail server while mail is configured) and offers the rest;
`location` stays `inside` · `outside`, resolved from `hosting.location` at generation.

### D. Missing from phases already closed

Build now, in the phase whose item it is: `CredentialEnrolled` (AUTH-STEP-007);
`CredentialSuspended`, `CredentialRestored`, `CredentialInvalidated` (AUTH-RECOV-007);
`OrganizationErased` with the grace-window execution it reports (IDN-ORG-003; the
scheduling may wait for phase 9, the operation may not); `AlertRaised` as a 5b event;
`model.startup.schemamismatch` raised by the phase 1 schema check; the
`identity.membership.limitreached` refusal in the membership aggregate; the photo
endpoints and codes (with C.55). `identity.account.restricted` is retired in favour of
`authz.restricted`. IDN-LIFE-012's `identity.link.lastcredential` is built with provider
linking, which lands in phase 8 if it has not landed. Verify with one repository search
that the shipped Egypt default declarations of PRIV-BASIS-001 and PRIV-SENS-001 exist in
source. `MembershipChanged` is emitted where membership begins and ends (phase 8).

### E. New `10` rows from this review

Codes: `auth.password.toolong` (422), `auth.credential.labelinvalid` (422),
`auth.credential.notfound` (404, concealed), `auth.credential.notupgradable` (409),
`identity.identifier.invalid` (400), `identity.identifier.locked` (409),
`identity.identifier.maximum` (409), `identity.profile.invalid` (422),
`identity.profile.notaccepted` (422), `identity.registration.incomplete` (409),
`identity.registration.signedin` (409), `api.request.malformed` (400),
`privacy.document.notfound` (404), `privacy.purpose.noconsent` (422),
`privacy.notice.unpublished` (409), `privacy.request.notfound` (404),
`privacy.request.decided` (409). Keys: `password.blocklist.selfhosted.address`,
`integration.mail.endpoint`, `integration.sms.endpoint`, `registration.events.pollinterval`,
`redirect.defaultclient`. Declarations added to LIB-HOST-001: passkey pages, sign-in
address, application client identifiers, image codec. Vocabularies: `AuditAction`,
`MessageKind` (from the ledger lists). The owner writes these rows and the chapter text
of the 68 kept decisions in the reconciliation pass that follows.

**Propagated to:** `10` (sections 1, 4, 5), `07` LIB-HOST-001, and the chapters the kept
and reversed entries name, in the reconciliation pass.

---

## D-163 — The product name is not a naming element

**Date:** 2026-09-23 · **Status:** accepted · **Amends:** D-153 (cookie, header and DNS names), D-155 and D-157 (collation and roles), D-159 (`MapAuthorizationTables`), D-149 (`AddIdentityArea`)

**TL;DR.** The code had taken to prefixing things with the product name: `JanusDbContext`,
`JanusEvent`, `JanusApplication`, the `janus` schema, `janus_*` roles, `__Host-janus-*`
cookies, `X-Janus-*` headers, `_janus-verify`. A name says what a thing is; the library's
identity is already carried by the namespace, and a product rename would otherwise mean
touching every one of these for no gain. The name stays only where .NET convention ties
it to the package: namespaces, project and package identifiers, and `AddJanus`. Every
other occurrence is renamed for what the thing is (`StoreContext`, `DomainEvent`, and so
on), and where a prefix must separate the library's artefacts from a host's, the neutral
word `identity` is used: schema `identity`, roles `identity_app`, `identity_migrate`,
`identity_maintenance`, collation `identity_ci`, cookies `__Host-identity-*`, headers
`X-Identity-Request` and `X-Identity-Csrf`, DNS `_identity-verify` with value
`identity-domain-verification=`. CONV-NAME-001 gains the rule and a scan enforces it.

**Propagated to:** `08` CONV-NAME-001, CONV-DESIGN-007, CONV-LAYOUT-002 · `02`, `03`,
`06`, `07`, `09`, `17`, `20` where the renamed identifiers appear.

---

## D-164 — Provider profile stated and proved: RFC 9700 and OAuth 2.1, PAR, RFC 9068, provider security events

> **Amended.** The `09` section 10 text this entry propagated to no longer lists payment or shipping callbacks beside `/callbacks/providers/{provider}` (D-165).

> **Amended.** The mail server verifies `aud` itself (`requireAudience`); the provider callback row of `09` section 10 is restored (D-166).

**Date:** 2026-09-23 · **Status:** accepted · **Amends:** D-005 (provider scope), D-007, D-147 · **Extends:** D-162 (the OIDC rebuild)

**TL;DR.** A standards checklist was put against the design. Two thirds of it describes
federated, multi-party or open-banking systems and stays out on principle (surface
nothing calls is attack surface, the reasoning that already excluded introspection).
Four items make the system more correct for what it is and are adopted into version 1,
in the provider rebuild already under way, so nothing is built twice.

1. **RFC 9700 and OAuth 2.1, stated and proved.** The behaviour existed; now a
   conformance suite asserts it (AUTH-OIDC-006 AC1).
2. **Pushed Authorization Requests (RFC 9126), required.** Both ends are the library's,
   so the authorization parameters leave the browser entirely at no interoperability
   cost. JAR is thereby unnecessary; mTLS is not adopted (one host behind one proxy).
3. **RFC 9068 access tokens.** `typ: at+jwt` and the seven claims; the mail server
   adapter verifies `aud`. Token confusion becomes impossible rather than unlikely.
4. **Provider security events consumed** (IDN-LIFE-012a): Google RISC and Apple
   server-to-server notifications end sessions, suspend or unlink the credential, or
   unverify the identifier. Outbound Shared Signals stays out; this is the inbound half
   that a system using social sign-in owes its people.

**Stays out, with a trigger.** RP-initiated and back-channel logout and RFC 7009
revocation become due the day a third-party relying party exists (D-005's deferred
item); every current party is first-party and holds no token to revoke. DPoP, RFC 9728,
SCIM, SAML, AuthZEN, FAPI as a profile, OpenID Federation: they describe systems this
is not.

**Propagated to:** `02` AUTH-OIDC-006 · `01` IDN-LIFE-012a · `09` section 9 and
section 10 (`/callbacks/providers/{provider}`) · `10` (rows in the reconciliation pass).

---

## D-165 — Host business content leaves the specification: the library knows no orders, payments or shipments

> **Amended.** The developer recipient row is a row a host declares, shown as an example; the provider callback row of `09` section 10 and INT-GEN-003's sentence are restored (D-166).

**Date:** 2026-09-24 · **Status:** accepted · **Amends:** D-027, D-030, D-036, D-039, D-041, D-044, D-045, D-049, D-056, D-061, D-066, D-068, D-070, D-073, D-079b, D-080, D-086, D-088, D-089, D-090, D-091, D-092, D-093, D-094, D-111, D-117, D-125, D-127, D-131, D-148, D-164 · **Extends:** D-162 C.103, D-163

**TL;DR.** The chapters were written with the first host in view and carried its
business: two vendors by name, rules about orders, payments, shipments, couriers,
products and cash on delivery, and acceptance criteria that can only be tested against
a store. Chapter `00` section 4 forbids exactly this ("no host-domain knowledge"). The
implementer of the first host found the leak by reading the chapters as a host would.
Every such rule moves to the host; the library keeps the generic seam it hung on.

**The boundary.** The library keeps, generic: the processor register populated from host
declarations plus the four rows the library itself makes true (mail server, SMS gateway,
hosting provider, password screening; D-162 C.103); processing purposes, lawful bases
and sensitivity categories declared through the model builder, with the Egyptian
default lists (law, not business); the privacy event hooks `ErasureRequested`,
`RestrictionChanged`, `TakedownExecuted`, `TakedownReversed`, `ExportRequested`,
`ConsentChanged`, `ObjectionChanged`; the outbound integration rules (INT-GEN-001 to
INT-GEN-005); one host-mountable machine-profile callback pipeline (BFF-MACH-001 to
BFF-MACH-003, INT-GEN-003: signature verification, correlation reference, rate limit,
source restriction) with no named payment or shipping callback of its own; the SMS
delivery report callback (`GET /callbacks/sms/dlr`) because SMS is the library's; the
mail server integration, deliberately library-owned; per-subject field encryption
declared per field by the host, including on host tables.

**Removed from the chapters (135 edits).**

- `04`: PRIV-SENS-003 (order history sensitive), PRIV-SENS-004 (card data), the former
  section 10 (PRIV-MIN-001, PRIV-MIN-002: package description, courier callback); later
  sections renumbered, the chapter now ends at section 10 "Open items". Business
  meanings of the privacy events (hold shipments and refunds, cancel undispatched
  orders, anonymise order rows, order counts unaffected) replaced by generic host
  statements.
- `05`: INT-PAY-001 to INT-PAY-003 and INT-SHIP-001 to INT-SHIP-006 with their sections;
  the chapter is now 1 General, 2 Mail, 3 SMS, 4 Password screening, 5 Hosting,
  6 Provider register, 7 Open items. The payment and shipping rows leave the shipped
  register; the register carries only the four library-true rows.
- `09` section 10: the `POST /callbacks/payment` and `POST /callbacks/shipping` rows,
  replaced by a paragraph and criterion stating that a host mounts its own callbacks on
  the machine profile pipeline and documents their paths itself.
- `13`: R-A06 (courier inference) removed; R-A10 rewritten for the generic pipeline.
- `18` section 7: FE-ADDR-001 to FE-ADDR-006 replaced by a boundary paragraph (addresses
  are host data, IDN-ATTR-005).
- `19`: INF-DB-002 (boundary dataset).
- `00`: vendor names, the deferred rows for district polygons and a geocoding provider
  (D-061 keeps them as the host's).
- `01`, `02`, `03`, `06`, `07`, `08`, `10`, `11`, `12`, `14`, `15`, `17`, `20`, the guide:
  rationale illustrations neutralised to a generic host example; `15` keeps the first
  host's facts only as a marked worked example. Cross-references to every removed
  identifier were followed and repaired in the same pass.

Kept on purpose: "customer" and "staff" as kinds of person, "financial record" as a
legal retention category, Law 151/2020 and Decree 816/2025 references, and the
recipient row for the developer relationship (D-029).

**Code consequences.** The library ships no payment or shipping callback endpoint,
handler, rate limit key, error code or test; the machine-profile pipeline exposes the
seam a host mounts its own callbacks on; the shipped register defaults are the four
library-true rows; no fixture, test name, sample configuration or comment names a
payment or shipping provider, an order, a cart, a courier or cash on delivery; catalogue
rows (`10`) for the removed items are dropped and the renumbered `04` and `05` sections
are cited by their new numbers.

**Handover to the host.** The first host receives these, each hung on a library seam.
They are the host's requirements now, not the library's.

1. Declare order history sensitive in the model builder (PRIV-SENS-001); this derives
   written consent for its consent-based purposes, per-subject encryption of its
   personal fields and separate reporting in the records of processing (PRIV-SENS-002).
   Three-purpose example: fulfilment on contractual obligation, tax retention on legal
   obligation, personalised recommendations on consent. Withdrawal of the consent-based
   purpose must not interrupt fulfilment, the courier callback or refund processing
   (`ConsentChanged`).
2. No guest checkout; every order belongs to an account, so the adult affirmation is
   collected once, at registration (IDN-LIFE-002a, D-073).
3. Delivery recipient fields are the ordering customer's personal data, encrypted under
   the customer's per-subject key through host-declared field encryption
   (PRIV-RIGHT-005a, D-131); recipients get no account.
4. Card data never touches the system: provider reference and amount only; no schema
   field or accepted request body holds a card number, expiry or verification value.
   Stolen-card fraud is the provider's.
5. Payment callbacks are signature-verified and never mark an order paid by themselves;
   confirm against the provider's query API; acknowledge fast, process asynchronously,
   idempotent on the notification identifier. Mount on the machine-profile pipeline
   (BFF-MACH-002, INT-GEN-003).
6. The shipping status callback is unsigned and therefore a hint: verify against the
   courier's API before any order state changes (BFF-MACH-003). A forged callback
   changes nothing.
7. Where a provider returns the browser by POST, land it on a GET route
   (BFF-CSRF-005, `SameSite=Lax`).
8. Shipment creation sends a fixed field set: a fixed generic package description never
   derived from the cart, item count, delivery notes, an opaque reference, receiver
   name, phone, drop-off address, cash-on-delivery amount; no email. One mapping layer,
   the exact field set tested. The courier's catalogue integration is not used.
9. Courier API key in an `Authorization` header as a rotatable secret. Cache the
   courier's city, zone and district taxonomy locally, refreshed by a background job;
   store the district identifier, not its name; refuse unserved districts at address
   entry. Pass address free-text lines through and never log them.
10. Own the address flow (former FE-ADDR-001 to 006, INF-DB-002, D-061): opt-in
    location control, preselection from coordinates resolved against boundaries in the
    host's own database, confirmation before save, coordinates never retained,
    overrides logged, landmark prompted. Nothing in the library except IDN-ATTR-006.
11. Declare the payment provider and the courier as processors through `recipients`
    (LIB-HOST-001): name, `processor`, data received, location (an outside-Egypt
    location enters the cross-border scope, INT-HOST-001, PRIV-ROPA-002), agreement
    reference, `callback: true`, and whether callbacks are signed.
12. On `TakedownExecuted`: cancel every undispatched order, ask the courier to halt what
    it can, hold shipments and refunds, confirm as a required subscriber
    (IDN-LIFE-003a). On `TakedownReversed`: do not restore cancelled orders. Refund
    cancelled orders through the normal process even after erasure.
13. On `AccountDeletionRequested`: hold fulfilment; on `AccountDeletionCancelled`:
    release it.
14. On `RestrictionChanged` (granted): hold shipments and refunds, stop acting on the
    subject's orders, keep them visible and counted (PRIV-RIGHT-004).
15. On erasure: order and transaction rows are retained under legal obligation and
    become anonymous through key destruction; order counts, revenue and product
    analysis are unchanged, which the host tests on its own aggregates
    (PRIV-RIGHT-005, D-117).
16. After any restore that moves time backwards, reconcile orders against the payment
    provider and shipments against the courier, after the operator's mail
    reconciliation (`12` sections 5 and 6, D-044).
17. Mark every endpoint that carries order data `SensitiveBody` (BFF-LOG-002); never log
    order contents, a cart or card data.
18. Cash-on-delivery abuse is a commercial control (order limits, prepayment thresholds,
    refusal history on the stable subject identifier, deposits); no identity control
    applies (D-049).
19. Rectification of an order's recorded details is a privacy request decided under
    the six-working-day rule; the host corrects its own records.
20. Own risk register entries: courier inference (former R-A06), unsigned shipping
    callbacks and the payment provider's signing scheme (former R-A10 rows), the
    plaintext base URL in the shipping provider's sample configuration.
21. Measure quarterly the share of a month's orders arriving through the system versus
    phone and messaging, and give the figure to the operator for OPS-MAINT-001 (R-A04's
    trigger).
22. Counsel question: whether a health-implying order may rest on the contract basis or
    needs written consent at registration (Law 151/2020 Art. 12); D-089 stands until
    answered.
23. The threat-model facts (exclusive importer, B2B customer list, clinics and
    pharmacies, the diagnosis-disclosing purchase, phone and messaging as a parallel
    channel) belong in the host's own model.

**History.** The entries listed under Amends keep their text: they record why each rule
exists and are the reasoning the host inherits. Each carries a note pointing here.
Vendor names survive only in those historic entries.

**Propagated to:** `00` sections 4 and deferred table · `01` IDN-LIFE-002a, IDN-LIFE-003
· `04` sections 3, 6, 8, 10 · `05` whole chapter · `07` LIB-HOST-001 Recipients · `08`
CONV-ENUM-001, CONV-LOG-003 · `09` sections 7 and 10 · `10` catalogue · `11`, `12`,
`13` R-A04, R-A06, R-A10 · `14`, `15` worked example · `17` BFF-MACH-001 to 003 · `18`
section 7 · `19` · `20` · `docs/guide/janus-explained.md`,
`docs/guide/implementation-plan.md`.

---

## D-166 — Review of entries 110 to 423: 101 reversed, 14 settled, the exit gate prepared, the chapters reconciled

> **Amended.** Section B's two Tier 1 allowances become three: an allow-list entry of exactly the form D-167 states, for a flagged value that is specification text, is Tier 1 too (D-167).

**Date:** 2026-09-25 · **Status:** accepted · **Amends:** D-161 (item 4, the working mode; item 2, where the drift check's rows come from), D-162 (item 22, where the governed send path lives; item 23, when the first attempt is made; item 26, the budget of a text message carrying a link; item 31, where destination records are kept and when they are swept; item 66, where a client secret comes from; C.55, where photo availability is held and what bootstrap writes; C.68 at `POST /auth/link`; C.103, the condition of the mail server row; E, the status of `identity.identifier.invalid`), D-153 (owner decision 2, the source a flood limit counts; owner decision 7, the word lists; the `backup.restoretest.interval` default; the address the bootstrap command prints), D-147 (the retirement of a key-encryption-key version; the name of the startup code for an unavailable secret), D-146 (item 17: a restriction's channel, the notices to a holder, a reason on every edit), D-143 (the policy object gains `photos`), D-129 (the break-glass page takes a reason), D-127 (a takedown reversal restores the state the takedown found), D-079a (a recognised device is exempt from the hold, not from the count), D-071 (three protected switches retired), D-060 (photos are off for the administrative organization until a codec is declared), D-057 (an authorization request's `redirect_uri` is refused at the push, not replaced), D-164 (item 3: the mail server verifies `aud` itself), D-165 (the developer recipient row is a declared example; the provider callback row and INT-GEN-003's sentence restored) · **Extends:** D-162, D-164, D-165

**TL;DR.** The ledger entries 110 to 423 were audited entry by entry against the
chapters, the decision log, the code at the end of phase 10 and, where a verdict rested
on one, the primary source of the standard or product named: the corrections that
applied D-162 to D-165 (entries 110 to 167) and every decision taken alone in phases 8 to
10 (entries 168 to 423), 314 entries in all. With them were audited the 34 Tier 1
resolutions of corrections 1 to 3 and phases 8 to 10, the 164 chapter `10` rows the
ledger owes, the closed vocabularies the code carries, section 2 of the phase 10 report
and the open items of the implementer's last message. Of the 314 entries, 120 are kept
as they stand, 76 are kept with their reasoning or chapter text corrected, 101 are
reversed in whole or in part, 14 were open choices and are settled here, and 3 had
already been superseded by D-163, D-164 and D-165. Forty-eight of the kept entries carry
a code fix the audit found. Nine rules that the review found broken in several places
are stated once (section C) and apply to every instance in the code. The working mode of
D-161 ends: the owner is present, and a third of the decisions taken alone did not
stand. The chapters are reconciled in the same pass and now state every decision of
entries 1 to 423, so the implementer builds from the chapters and this entry alone. The
ledger is closed. This entry is the specification for every point it settles; it is
applied in one corrections run before the Milestone 1 exit gate.

### A. What was reviewed and how

Each entry was read in full with the chapter passages it cites and those it does not
cite but that govern its question, the decision-log entries that bear on it, and the
code that implements it, down to the type, the test and the migration. A difference
between an entry and its code was a finding in its own right. A verdict resting on an
external claim was checked against the primary source (the RFCs, WebAuthn Level 3, the
OpenID Connect and RISC specifications, the providers' own documents, the PostgreSQL,
Npgsql, EF Core, ASP.NET Core and OpenIddict sources, the mail server's object
reference); a claim that could not be checked carried no verdict. Every decision had to
agree with the chapters, take the strictest reading on anything touching security
semantics, fail closed, keep the public surface smallest, keep the library generic, and
rest on true reasons. The lead then decided every entry; where the lead's decision
differs from an auditor's recommendation, this entry states the lead's.

The Tier 1 resolutions: 32 of 34 meet the five conditions of the working guide or one
of its two allowances. Two do not. The pinned column list of `ModelTests` is governed by
REG-ACCT-001 AC2 together with the schema choices of ledger entries 115, 119, 158 and 163
(under D-162 items 17, 23, 58, 59 and 66), not by OPS-DATA-001; its outcome is right and
is recorded here, with nothing to build. The CONV-DESIGN-004 AC2 scan exempts whole files
where a package fixes only four members; the correction is in section D, conformance.

The chapter `10` rows owed: of 164 rows, 154 are right, one is renamed
(`identity.credential.labelled` becomes `auth.credential.labelled`), eight carry a wrong
meaning and one is not needed (the `resources.subject` column, which is `03` text). The
code carries two actions, one step-up action, six register findings and ten further
closed vocabularies that neither `10` nor the ledger lists; all are collected in `10`
now (section F).

Three questions needed research before the lead could decide them: the sources and
licences of the dictionary lists (R1), where a link a message carries lands (R2), and
the Public Suffix List (R3). Each decision is stated where it applies.

### B. The working mode ends

D-161 item 4 let the implementer decide Tier 2 and Tier 3 questions alone while the
owner was absent. That mode ends with this entry. The working guide section 3 returns to
its original rule: a Tier 2 question is written up under **Open questions** with the
readings and the smallest fix for each, and the run ends; a Tier 3 question is stated
without a proposal, and the run ends. Tier 1 stays, with its two allowances (test
infrastructure, D-156; a gate that mis-implements its own rule, D-158). The heading
**Decided in the owner's absence** is no longer used.

The ledger of decisions taken in the owner's absence is closed. It takes no new entry.
Every entry this review reverses, settles or revises gains one line under its heading, as
section G lists, in the same change that applies it. Kept entries take nothing.

The corrections of this entry are applied before any other work, in the order the
implementer judges, in one run that ends with the full gate and a report in the usual
shape. Where applying a point of this entry meets a question this entry does not
answer, that question is a Tier 2 stop. The Milestone 1 exit gate follows that run.
Milestone 2 stays closed until the exit gate passes.

### C. Rules that apply everywhere

Each rule below was found broken in more than one place. Each is applied to every
instance in the code, not only the ones section D names; the implementer sweeps for it
and lists in the report every place changed.

**X1. An event row is written in the transaction that makes its fact true.**
CONV-DESIGN-002 orders persist (the writes and the outbox row in one transaction),
audit, commit, then delivery from the committed row. `IEvents.PublishAsync` is the write
of that row, so it is called before the operation's commit, inside its unit of work, and
never after it. A failure to write the row fails the operation, which rolls back. No
publication follows a commit anywhere in the library. Sweep every `PublishAsync` call
site. Instances named here: the credential events (152), the organization erasure
(155), the takedown (171), bootstrap's memberships (313), `AlertRaised` at bootstrap and
at `configure` (157, 290), the corporate address at acknowledgement and at membership end
(248).

**X2. A configuration read never falls back.** A stored value that does not read under
its key is a fault (D-162 item 19, CONV-ERR-001): `ConfigurationStore` throws, a request
fails as `system.fault` and a job fails its run (`background-job-failed`). No read
substitutes the key's default, a constant, `TimeSpan.Zero` or any other value on failure.
Sweep every `.Match(value => value, _ => ...)` over a configuration read. Instances named
here: the 28 sites and the two zero sites of entry 116, the records of processing (132),
the organization and account deletion windows (155, 198), the clock drift watch (330).

**X3. A decision on a row is made under a lock on that row.** A read that decides a
security or state outcome and is followed by a write takes the row with
`SELECT ... FOR UPDATE` inside the transaction, or the write is one conditional update,
or a constraint makes the race impossible. A plain read at the default isolation (Read
Committed) followed by a write is not enough. Sweep every read-decide-write. Instances
named here: verification and sign-in codes (115), memberships (154), the takedown
reversal and the eraser (173), runtime settings (178), the alert deduplication ledger
(290), the callback claim (276), client secret rotation (340).

**X4. Free text is 1 to 1024 characters after trimming.** Every free-text member of
every request (API-CONV-002: `reason`, `detail`, `channelUsed`, `note`, and every other)
is trimmed and then refused where it is empty or longer than 1024 characters: blank or
absent with the code `10` names for that member's absence where one exists
(`config.change.reasonrequired`, `authz.grant.reasonrequired`,
`auth.recovery.reasonrequired`), otherwise, and for every over-long value, 400
`api.request.malformed` naming the member. The endpoint refuses before calling the
service (CONV-CODE-006 AC2), and the service refuses the same for an in-process caller.
Sweep every endpoint and every service method that takes free text. Instances named
here: the takedown (174), the configuration route (179), the restriction routes (183),
role changes (188), the break-glass reason (302), privacy requests (414).

**X5. Status follows one rule.** 400 is for a request that cannot be read, or a word
outside a closed vocabulary that `10` or the startup declaration fixes. 404 with a named
code is for a path naming a runtime record the deployment does not hold; under `/admin`
nothing is concealed. 422 with a named code is for a well-formed body that refers to
something that does not exist or cannot be acted on; where `10` holds no more specific
code, the refusal is the new general code `api.request.invalid` (422, `details.member`).
409 is for a failed state precondition. `authz.denied` is for an absent permission, and
for a context in which no person acts, and for nothing else. `api.request.malformed`
stays for what cannot be read. Sweep every refusal. The body references settled here:
an invitation naming a role the deployment does not hold, 422 `authz.grant.unresolved`
(`details.member` `roles`); an invitation naming a document never published, 422
`api.request.invalid` (`documents`); a member group that does not exist or belongs to
another organization, 422 `api.request.invalid` (`subjectId`); a preferred second step
naming a method not enrolled, 422 `api.request.invalid` (`method`). A path `{id}` under
`/admin` naming no organization is 404 with the new code `identity.organization.notfound`.
Other instances are named in section D under their entries; where the sweep finds a
refusal whose status or code neither this entry nor the reconciled chapters settle, that
is a Tier 2 stop.

**X6. A value whose feature needs a host declaration is refused without it.** Where a
policy or configuration value switches on a feature that needs a declaration the host
may omit, a change that writes the value on while the declaration is absent is refused
with `config.value.notallowed`, `details.requires` naming the declaration; bootstrap
never writes such a value on; startup refuses a stored value on whose declaration is
absent (`model.startup.declarationmissing`, `details.key` naming the declaration). Today
there are two: `photos` needs the image codec (`imageCodec`), and a domain in an
organization's lock needs the DNS resolver (`dnsResolver`).

**X7. A send is judged in the caller's transaction and carried after it.** The
restrictions and the gateway floor are judged inside the caller's transaction, and a
refusal returns (`auth.restriction.exceeded` with `retryAt`) before anything is written.
An admitted send writes its outbox row in the caller's transaction. One immediate
attempt is made after the outermost commit, through an after-commit registration on the
unit of work that is discarded on rollback; what that attempt does not carry is the
publisher's under `outbox.retry.*`. No transport is called while a transaction is open.
An operation that rolls back sends nothing. The asks that must not reveal whether an
account exists (sign-in link, email code, recovery) make no attempt inside the request at
all (entry 423). Every send path follows this rule: invitation, recovery, sign-in,
verification and every notice.

**X8. An administrative operation that changes another person's account or loosens a
control is stepped up.** `09` section 8a's preamble is the rule; each such operation has
its gate, a `StepUpAction` member named as its `10` section 5a row, defaulting in the
policy object's `gates` like every library action. Its service takes the `SessionId` the
step-up is judged on, and judges it after every other refusal. The lead's sweep of
`09` sections 8 and 8a against section 5a adds seven gates: `organization:delete`
(entry 195, built), `membership:end` (250), `account:restrictionlift` (258),
`account:deletioncancel` (260), `account:sessionsrevoke`
(`POST /admin/accounts/{subject}/sessions/revoke`, which ends another person's sessions),
`session:revokeall` (`POST /admin/sessions/revoke-all`, which ends every session) and
`privacyrequest:fulfil` (`POST /admin/privacy/requests/{id}/fulfil`, the fulfilment of
every request type, since it acts on another person's data or account; refusing a request
is not gated).
Not gated, with the reason: revoking an invitation (it touches no account, entry 233),
creating an organization, publishing documents and translations, and the compliance
records. For the three gates no entry built, a test carrying the endpoint's item proves
the operation answers 403 `auth.stepup.required` without the step-up and changes nothing.

**X9. A unit of work is left clean.** A refusal that needs no write is returned before
the unit of work begins. A failure after a write rolls the unit of work back before it
returns. A later operation in the same scope then begins and commits on its own. Sweep
every return after `BeginAsync`. A test carrying CONV-DESIGN-002 proves that an operation
refused after its transaction began, followed by a second operation in the same scope,
commits the second.

### D. Reversals and settlements

Each paragraph names the ledger entries it answers, says what was wrong where that helps,
and gives what to build or remove. A reversal or a settlement here is the specification
for its point. A kept entry appears only where the audit found a code fix. Tests are
named where the audit named them; otherwise the test carries the item's identifier as
CONV-TEST-007 requires.

#### D.1 Authorization

**110.** The capability page with the host's sources ran the stored grants as a second
statement on the library's own connection and decided in memory that a deny defeats a
derived allow, a second writing of the rule AUTHZ-GATE-002 AC1 forbids. In
`AccessGate.CapabilitiesAsync<TResource>` with sources, compose one query in the host's
context from `FilterSources` (`Grants`, `Ancestry`, the relationship rows) through
`PermissionRule`: for each record of the page and each permission asked, the stored
allow term and the stored deny term exactly as `PermissionRule.ToExpression` composes
them, and one term per non-materialised derivation reaching the type as
`ToAdmittedRecords` composes it. Map each derivation term to the permissions its role
confers in memory (`Derivations.ConferringAsync`). A permission is held on a record where
the stored allow or a conferring derivation term holds and the deny term does not.
Remove the `IAccessEvaluator.PageAsync` call from that overload; the overload without
sources keeps it. The sourced `RequireAsync<TResource>` is composed the same way: one
query in the host's context, never a read of stored grants on the library's connection
(AUTHZ-DERIVE-001 Values). Tests: extend
`GateBehaviourTests.AUTHZ_GATE_005_AC1_APageCostsOneStatementOverTheHostsRowsAsync` to
count the commands the library's `StoreContext` sends that read `effective_grants` or
`ancestry` (none) beside the one host statement; add
`GateBehaviourTests.AUTHZ_GATE_005_AC1_AStoredDenyAndADerivationAreDecidedInOneStatementAsync`
(a stored deny and an admitting derivation on one record of a 50-record page: one host
statement, the permission absent from that record and present on the others, equal to
`RequireAsync` with sources on each record); a test carrying AUTHZ-DERIVE-001 proves the
sourced check reads nothing through the library's connection.

**111 (kept, with a fix).** Where several derivations or ancestors admit a record, the
explanation names the first row of an unordered query. Order the derived rows by the
depth of the ancestor, nearest first, as `PermissionRule.ToCandidates` orders stored
ones. A test carrying AUTHZ-GATE-004 proves the nearest container is named.

**136.** A refusal of background work was recorded naming no identity and no principal,
which AUTHZ-CONCEAL-004 AC1 and IDN-PRIN-001 AC4 forbid. `AccessAudit.RecordAsync` for a
`DeniedAccess` whose context carries a `Principal` writes the nil subject under both
identities and `principal` and `principal_reason`, as the export row does; `DeniedAccess`
carries the principal. A new migration raises an exception where any `audit_records` row
has a NULL `acting_subject` or `effective_subject`, then drops
`ck_audit_records_identities` and sets both columns NOT NULL. `AccessAudit.CountAsync`
replaces the count of rows naming nobody with a count by principal name, so each system
principal is its own actor for the denial spike. `ResolveAsync` explains such a row with
`principal` carrying `name` and `reason`, present only for a system principal. Tests:
`ExplanationTests.AUTHZ_CONCEAL_004_AC1_ARefusalOfBackgroundWorkNamesThePrincipalAndItsReasonAsync`
replaces `AUTHZ_CONCEAL_004_AC1_ARefusalUnderNoAccountCarriesAnIdentifierAsync`;
`AuditStoreTests.IDN_AUD_001_AC1_ARefusalNamingNobodyIsRefusedByTheDatabaseAsync`.

**176 (kept, with a fix).** Where the caller lacks `audit:read`, `ResolveAsync` answers
a fresh `authz.denied` in place of the gate's own refusal, which drops its correlation.
Return the gate's refusal, as `AdministrativeScope.RefusedAsync` does. A test in
`ExplanationTests` proves the refusal's `details.correlation` resolves to the recorded
denial.

**184 (settled: option 1).** A host resource type may not be named `organization`, which
the library uses for the whole organization; with such a host type a grant meant for one
host record confers on the whole organization and every refusal on it discloses.
`AuthorizationModel` refuses it at build with the new code `model.type.reserved`
(`details.key` naming the type), mapped to 500 in `ApiStatus` as the other model codes
are. Test: `AuthorizationModelTests.AUTHZ_MODEL_004_ATypeNamedOrganizationFailsStartup`.
Settled: a grant body naming a role the deployment does not hold, or a group that does
not exist or belongs to another organization, is refused 422 with the new code
`authz.grant.unresolved`, `details.member` naming `role` or `subjectId`, in place of
`api.request.malformed`. A test carrying AUTHZ-GRANT-001 proves both cases.

**187, 183.** A path naming a role or a restriction the deployment does not hold is a
record not found, not a malformed request (X5). Add `authz.role.notfound` (404) and
`auth.restriction.notfound` (404) to `ErrorCodes`, `ApiStatus` and the `ErrorCodesTests`
list. `RoleService.RemoveAsync` answers an unknown role with it after the permission and
reason checks, in place of `Malformed("name")`. `RestrictionSetService.ReadAsync` and
`DeleteAsync` answer a name the set does not hold with `auth.restriction.notfound` after
the permission check, and `RestrictionAdministration.GrantAsync` answers the same absence
with it in place of `config.value.notallowed` (a credit at or below zero stays
`config.value.notallowed`). `RestrictionAdministration.EditAsync` and `GrantAsync` refuse
a reason past 1024 characters (X4). Tests:
`RoleEndpointTests.AUTHZ_GRANT_004_AnUnknownRoleIsNotFoundAsync`;
`RestrictionEndpointTests.AUTH_ABUSE_004_AnUnknownNameIsNotFoundAsync` (`GET`, `DELETE`
and the grant) replaces `AUTH_ABUSE_004_DeletingAnUnknownNameIsMalformedAsync`.

**188.** A stepped-up holder of `role:manage` and `system:administer` could take library
permissions out of the role the reserved `emergency` account holds, which empties the
break-glass session OPS-BOOT-002 requires. In `RoleService.DefineAsync`, after the held
role is read and before the step-up, read whether the reserved account
(`IEmergencyAccount.FindAsync`) holds a live grant of the role; where it does and the
defined permissions do not contain every member of `Permissions.All`, answer 403
`authz.denied` and write nothing. Adding a host-declared permission to that role stays
allowed. `GrantService.RevokeAsync` refuses to revoke the reserved account's
`system-administrator` grant with 403 `authz.denied`, for the same reason. Tests:
`RoleEndpointTests.OPS_BOOT_002_TheReservedAccountsRoleKeepsEveryLibraryPermissionAsync`;
a test carrying OPS-BOOT-002 proves the revocation is refused and the grant stands.

**189, 247 (in part).** A role an open invitation names could be removed, and the
acknowledgement then faulted on `fk_grants_role`. Add an internal port `IRoleReferences`
in `Janus.Authorization` with `NamedAsync(RoleName role, CancellationToken)`, implemented
in `Janus.Storage` over `grants` (any grant, live, expired or revoked) and `invitations`
(neither acknowledged nor revoked and not past expiry, whose `roles` holds the name).
`RoleService.RemoveAsync` calls it beside `Derived(role)`, before the step-up, and answers
409 `authz.role.inuse`. Tests:
`RoleEndpointTests.AUTHZ_GRANT_004_ARoleAnOpenInvitationNamesIsNotRemovedAsync` (removal
refused while the invitation stands, 204 once it is revoked);
`RoleEndpointTests.AUTHZ_GRANT_004_ARoleAStandingInvitationNamesIsNotRemovedAsync`.

**252 (settled: option 1).** A grant in the administrative organization confers only
while its holder holds a current membership of that organization. The gate, when asked in
the administrative organization, reads the principal's current membership there beside
its group set, and a grant of a principal holding none confers nothing. Ending a
membership removes no grant; step 4 of `16` still transfers or removes them. The reserved
account, the first administrator and the canary are members, and system principals hold
no grants. Tests:
`GateBehaviourTests.IDN_LIFE_009a_AnAdministrativeGrantConfersNothingWithoutAMembershipAsync`;
`InvitationServiceTests.IDN_MEM_001_EndingTheAdministrativeMembershipStopsItsGrantsAndKeepsThemAsync`.

**265, and the `GET /admin/access` defect (settled: option 1).** `AccessGate.LookedUpAsync`
answered 400 naming `resourceId` for a record with no registration before it asked
`grant:read`, so any signed-in caller learned whether a record is registered. Keep 400
`resourceType` for an undeclared type and 400 `resourceId` for an organization-wide
identifier that is not a UUID (shape; nothing is read). Split `ScopeOfAsync` so that
these two are told apart from "no registration". For a declared type whose identifier
the registry does not hold, return the refusal of `RefuseUnscopedAsync(context,
Permissions.GrantRead, ...)`: 403 `authz.denied`, recorded against no organization with
type `organization`, counted by the denial spike, carrying `correlation`, byte for byte
the refusal of a registered record read without `grant:read`. The check of missing
sources stays after the permission. Change the documented returns of both
`IAccessGate.WhoCanAccessAsync` overloads and the `Unreleased` changelog line for
`GET /admin/access` to say so. Tests: split
`ReverseLookupTests.AUTHZ_DERIVE_007_AnUnknownRecordOrTypeIsRefusedAsMalformedAsync` into
`AUTHZ_DERIVE_007_AnUndeclaredTypeIsRefusedAsMalformedAsync` and
`AUTHZ_DERIVE_007_AnUnregisteredRecordIsRefusedAsTheGateRefusesAsync`;
`AccessEndpointTests.AUTHZ_DERIVE_007_AnUnregisteredRecordReadsAsARefusalAsync`;
`ExplanationTests.CONV_DESIGN_002_AC3_ALookupOfARecordNoRowNamesIsRefusedAsTheGateRefusesAsync`;
`ReverseLookupTests.AUTHZ_SCOPE_001_AnUnregisteredRecordIsRefusedEvenToAHolderOfGrantReadAsync`.
Settled: a new LIB-HOST-001 declaration, **relationship sources**: for the relationship
of each declared derivation, a scoped source of its rows as a queryable from the host's
own context. It is required for every declared derivation, materialised or not: the view
needs the rows of a derivation that is not materialised, and the drift check below needs
those of one that is. Startup fails with `model.startup.declarationmissing` naming the
relationship where one is missing. The
`GET /admin/access` endpoint builds its `FilterSources` from it and answers stored,
materialised and derived grants in full, within `authz.reverselookup.budget`. The daily
drift check of AUTHZ-DERIVE-005 (`derivation.materialised.driftcheck`, D-161 item 2),
which no job runs today, is built as a job over the same declaration: it re-evaluates
every materialised derivation, corrects the difference and raises `degradation` where
it found one. The job runs as the system principal `derivation-driftcheck` (operation
`reconciliation`, reason `AUTHZ-DERIVE-005`), a member of `10` section 5.29. A materialised
grant it writes names the nil subject as granter and `AUTHZ-DERIVE-005` as its reason, as
bootstrap's grants name `OPS-BOOT-001`, and its audit record names the principal
`derivation-driftcheck` (entry 412); `RefreshAsync` accepts that principal's context.
Tests carrying AUTHZ-DERIVE-007 and AUTHZ-DERIVE-005 prove the view reports a derived grant
with no `id`, and the job corrects a materialised grant the host's rows no longer support,
writes it with the nil granter and that reason, records the principal, and raises
`degradation`.

**321 (kept, with a fix).** A denial recorded inside a transaction that then rolls back
leaves no record, no count and no alert. The gate writes its `authz.access.denied`
records on a connection outside any open transaction, committed at once; every action's
own record stays in its transaction. Test:
`GateBehaviourTests.AUTHZ_GATE_004_AC4_ADenialInsideATransactionThatRollsBackIsStillRecordedAndCountedAsync`.

**339.** The refusal of a record the library holds no row for returned before the
queries the refusal of a registered record runs, a timing difference AUTHZ-CONCEAL-002
and BFF-ERR-003 forbid. In `AccessGate.DecideAsync`, where the record lookup finds
nothing, resolve the subject set and run `evaluator.CandidatesAsync` with the rule scoped
to a sentinel organization identifier no row carries (a value, not null, so the command
text is the same), and discard the result; in `RequireAsync<TResource>`, run the
admission query over the sources for the unregistered record against the same sentinel.
Test: `ConcealmentTests.AUTHZ_CONCEAL_002_AC2_AnAbsentRecordAndARefusedOneRunTheSameStatementsAsync`,
with a command-recording interceptor on the fixture (test infrastructure): both
refusals issue the same commands with the same texts, for a stored type and for a
derived type with its sources.

**396 (settled: option A).** A permission the model does not declare, named at any gate
entry point (`CheckAsync`, `RequireAsync`, the filter and fragment renderers,
`CapabilitiesAsync`, `ExplainAsync`), is a programming fault raised before anything is
read, as entry 387 raises for an undeclared type. `Asked` becomes a raise. A test
carrying CONV-ERR-001 proves each entry point throws for an undeclared permission and
reads nothing.

**410 (kept, with a fix).** An operation on the caller's own records answers another
account's device or session as a missing one, but with 403. `DeviceService` and
`SessionService` answer both an unknown and another account's device or session with
`authz.resource.notfound` (404, empty details); `authz.denied` stays for a context naming
no account. Tests:
`DeviceServiceTests.CONV_DESIGN_002_AC3_AnotherAccountsBrowserIsAnsweredAsNoneAsync`;
`SessionServiceTests.CONV_DESIGN_002_AC3_AnotherAccountsSessionIsAnsweredAsNoneAsync`; an
endpoint test proving `DELETE /account/devices/{id}` and `DELETE /account/sessions/{id}`
answer 404 with the same body, apart from `correlationId`, for another account's row and
for an unknown identifier.

**A group's member (X5).** `GroupService.AddMemberAsync` answers a member group that does
not exist or belongs to another organization with 422 `api.request.invalid`,
`details.member` `subjectId`, in place of `Malformed("subjectId")`; a `subjectType` or
`subjectId` that does not read stays 400 `api.request.malformed`. A test carrying
AUTHZ-GROUP-001 proves both cases of the 422 and that nothing is written.

**352 (kept, with a fix).** `IResources` is a seam that joins the host's transaction, not
an operation, and takes no access context. Its refusals follow X5: in `ResourceService`,
a type the model does not declare stays 400 `api.request.malformed` naming `resourceType`
(a word outside a vocabulary fixed at startup); every refusal on meaning answers 422
`api.request.invalid` naming the member: a record already registered, or a record to move
that is not registered (`resourceId`); a container not of the declared type, not
registered, of another organization, or absent where the type requires one
(`containedIn`); a record of a sensitive type naming no subject whose account stands
(`subject`). A batch is still judged whole before anything is written. A test carrying
AUTHZ-INHERIT-002 proves the 400 and each 422, and that a refused batch writes nothing.

#### D.2 Sessions, factors and sign-in

**114 and R1.** The offline leaked list held 54,676 hashes of unrecorded origin where
AUTH-PASS-004 names the 100,000 most prevalent. Replace the content of the embedded list
with the 100,000 hashes of highest count across all 1,048,576 ranges of the Pwned
Passwords range API (upper-case SHA-1 in hexadecimal, one per line, the first line
`# <date drawn>`), drawn with a user agent that names the draw accurately, as the API's
acceptable use asks. The API's terms state no licensing or attribution requirement and
welcome attribution; `NOTICE` gains a paragraph naming the source (Have I Been Pwned,
Pwned Passwords) and the date drawn. The phase report records how and when the list was
drawn and when the terms were read. Tests:
`OfflineCorpusTests.AUTH_PASS_004_TheShippedListIsTheHundredThousandItNames` (the
embedded resource is dated and holds exactly 100,000 distinct 40-character upper-case
hexadecimal hashes); `LibraryStructureTests.AUTH_PASS_004_TheNoticeNamesTheLeakedListsSource`.
The release steps of CONV-VCS-005 refresh the offline leaked list at every release, dated,
as they refresh the Public Suffix List (R3). The range requests of INT-PWD-001 carry a
`User-Agent` naming the library and its version, since the provider's acceptable use asks
callers to identify themselves: the `LeakedPasswordCorpus` client sets it once where it is
registered. A test carrying INT-PWD-001 AC3 asserts every range request carries it.
The `dictionary` source (R1): its lists ship as embedded resources, as the leaked list
does, and nothing is read from the deployment's files. `WordList` stops reading its file
beside the application (remove `WordList.Directory`, `WordsFile` and the path
`AddJanus` builds); a host extends either list only by the optional LIB-HOST-001
declaration of dictionary words, and absent, the shipped lists alone answer. The 12dicts
list is static and is not refreshed at release. The English list is Alan Beale's
3esl list from the 12dicts 6.0.2 package, which its author releases to the public domain
and asks to be acknowledged (one `NOTICE` line), filtered at build time to single
lower-case words of four letters or more; the build counts the result and fails below
10,000. The Arabic transliteration list has no public-domain or CC0 source; the
implementer writes it as original work, about 2,500 to 4,000 lower-case entries (given
names including Coptic names, family names, religious and everyday words, slang and
profanity, football clubs and players, places, and Egyptian Arabizi forms using the
digits 2, 3, 5 and 7), with variants generated by rules in the build script; the owner
reviews it before release, as the default message texts are reviewed. The matcher keeps
digits, so Arabizi entries match; counts characters, digits included, toward the
four-character minimum; and never echoes the word it matched. Tests carrying
AUTH-PASS-004 prove the English list holds at least 10,000 entries, an Arabizi form is
refused, and the refusal carries no matched word.

**115.** The attempt cap and single use of a code were a read then a write with no lock,
so concurrent tries escaped the cap, and two other flows kept private copies of the rule.
(1) In `VerificationCodes.PresentAsync`, begin the unit of work before the read, read
the row with a new store method `FindForUpdateAsync` (`SELECT ... FOR UPDATE`), compare in
fixed time, then delete the row on the right code or increment `attempts` and delete the
row at the cap, and commit; nothing is read outside that transaction (X3). Hold the
pending sign-in row of `SignInLinks.SpendCodeAsync` the same way. (2) Issue and answer
the registration and identifier verification codes through `VerificationCodes`, the
holder being a fingerprint of the registration session and staged identifier, or of the
pending verification; remove the code, expiry, spent flag and counter from
`StagedIdentity` and `PendingVerification`; add to `VerificationCodes` the read of the
outstanding digits the landing page shows away from the registering browser
(REG-SESS-003). (3) The code of `emailCode`, the `phoneCode` second step's code (146) and
the code a sign-in link shows on a page opened elsewhere are authentication codes, not
verification codes (AUTH-FACT-004: independent lifetime, storage and attempt cap). They
stay on the pending sign-in record. The `emailCode` and `phoneCode` codes live
`code.signin.lifetime` (a new key: 10 minutes, ceiling 30 minutes, R); the code a sign-in
link shows lives as long as its link, `link.magic.lifetime`; all three are capped by
`code.signin.attempts` (a new key: 5, ceiling 10, R), in place of `code.verification.*`.
A code presented after its cap is refused `auth.code.expired` (AUTH-FACT-004 AC3). The
`emailCode` code is sent as the new message kind `sign-in-code` in place of
`verification-code`. Tests:
`VerificationCodesTests.AUTH_FACT_004_AC3_ConcurrentWrongTriesAreAllCountedAsync` (ten
concurrent wrong presentations with the cap at 5; the right code is then refused
`auth.code.expired`); `VerificationCodesTests.AUTH_FACT_004_AC3_TwoConcurrentRightTriesSucceedOnceAsync`;
the registration and identifier AC3 tests pointed at the aggregate; a test carrying
AUTH-FACT-004 proves an email sign-in code lives `code.signin.lifetime` and is spent
after `code.signin.attempts`, and a sign-in link's code lives `link.magic.lifetime` and is
spent after `code.signin.attempts`, whatever `code.verification.*` hold. Rename
`AuthenticationServiceTests.AUTH_FACT_016_AC3_WrongCodesInvalidateTheHeldSignInAsync` for
AUTH-FACT-004 AC3, the criterion it proves.

**129.** The registration ceremony D-162 item 75 names was not built, and an assertion
with no user handle was accepted where WebAuthn Level 3 section 7.2 requires one. (1) Add
a registration-session form to `CredentialAuthority`, resolved in `Asking` from the
registration session cookie while the session's step is `security` or `terms`.
`BeginKeyAsync` with it builds `CeremonyUser(WebAuthnService.Handle(session.Provisional),
<the staged primary email>, <the staged display name, or empty>)`; `CompleteKeyAsync` with
it verifies the attestation and stages the credential on the registration session
through `RegistrationService.EnrolAsync`, writing no account row before the terms step;
the TOTP begin and confirm take the same authority. (2) `PresentAsync` is told whether the
ceremony was opened for an identified account; where it was not, an assertion with no
user handle is refused `auth.factor.rejected`. Tests:
`RegistrationFlowTests.REG_SESS_006_AC1_APasskeyAloneCompletesTheSecurityStepOverTheWireAsync`
and its TOTP counterpart for AC4;
`WebAuthnServiceTests.REG_PM_001_AnAssertionWithNoHandleIsRefusedWhereNoAccountWasNamedAsync`;
`WebAuthnServiceTests.REG_PM_001_ASecondStepKeyWithNoHandleIsJudgedAsBeforeAsync`.

**146.** The phone signal was asked at sign-in only; a text recovery link and an SMS
step-up, the paths a SIM swap exists for, were not considered. (1) `RecoveryService`,
where the channel is a phone, asks `PhoneSignals.AllowsAsync` before anything is sent,
whether or not an account holds the number; on `risk` it sends nothing, records the
consideration (`auth.phonesignal.considered`) and answers 202 as always. Add
`recovery-link` to `MessageChannels.Factors`. (2) A step-up challenge drops from the
combinations it offers every restricted entry whose number answers `risk`, as the
sign-in challenge does; where no combination remains, the outcome is the one AUTH-STEP-002
gives an account that cannot reach the gate (`enrol` or `report-loss`), never a pass.
(3) D-162 C.68 is modified at `POST /auth/link`: a `phoneLink` request whose number
answers `risk` sends nothing and is answered 202 exactly as any other, so an anonymous
caller learns nothing of the carrier's signal about a number; the consideration is
recorded. (4) No code path sends the `phoneCode` second step today. Build it: where a
sign-in or step-up challenge offers `phoneCode`, the code is asked for by
`POST /auth/factor` (or `/auth/step-up`) naming `phoneCode` with no `value`, answered 202;
the library then considers the phone signal (C.68 and point 2), issues a code bound to
that challenge on its record, and sends it through the governed send path (X7) as
`secondstep-code` under the purpose `secondfactor`; it is an authentication code under
`code.signin.lifetime` and `code.signin.attempts`, presented with `factor: "phoneCode"` and
the code as `value`. Tests:
`RecoveryServiceTests.AUTH_FACT_002b_AC6_AReportedChangeSendsNoRecoveryLinkByTextAsync`
(202, no send, one `auth.phonesignal.considered` row, the same bytes for a number no
account holds);
`AuthenticationServiceTests.AUTH_FACT_002b_AC6_AReportedChangeWithholdsTheTextCodeFromAStepUpAsync`;
a test carrying AUTH-FACT-002b AC6 proves `POST /auth/link` answers a `risk` number 202
with the bytes it answers any number and sends nothing; a test carrying AUTH-FACT-002 AC4
proves a password plus a sent and presented `phoneCode` completes at AAL2.

**152.** Every credential event was written in a second transaction after its change
committed, a password set or recovered raised no `CredentialEnrolled`, and
`CredentialSuspended` named no actor. (1) Under X1: `CredentialSuspended` is written in
`LossReports.SuspendAsync` after the audit record and before the first commit;
`CredentialRestored` in `LossReports.CancelAsync` before its commit;
`CredentialInvalidated` in the invalidation step before its commit; `CredentialEnrolled`
in `CredentialService.LinkAsync` before its commit, and in `CompleteKeyAsync` and
`ConfirmGeneratorAsync` the unit of work opens before `keys.EnrolAsync` and
`generators.ConfirmAsync` so their commits join it and the event is written before the
one commit. Remove the publication from `SettledAsync`; keep the `Result<int>` carriage
through `CarryAsync` and `InvalidateAsync`. (2) `CredentialEnrolled` is raised wherever a
password is set on an existing account: `SetPasswordAsync`, the self-service and
admin-assisted recovery completions, and the invitation acknowledgement where it sets
one. `CredentialEnrolled.Credential` becomes `AuthenticatorId?`, absent for the password,
whose `Kind` is `Factor.Password`. (3) `CredentialSuspended` raised by a report or a
removal carries `Actor` the reporting context's acting subject and `Effective` where it
differs; `CredentialRestored` from a session carries `Actor = context.Acting` and
`Effective = context.Effective`, and from a link neither. Tests:
`LossReportsTests.AUTH_RECOV_007_ASuspensionWhoseEventRowFailsLeavesTheCredentialActiveAsync`
(a fake `IPendingEvents` throws on `AddAsync`: the credential stays `active`, and no loss
report, audit row or notice exists);
`CredentialServiceTests.AUTH_STEP_007_AnEnrolmentWhoseEventRowFailsCommitsNothingAsync`;
`CredentialServiceTests.AUTH_STEP_007_ASetPasswordIsAnnouncedAsync`;
`RecoveryServiceTests.AUTH_STEP_007_ARecoveredPasswordIsAnnouncedAsync`;
`LossReportsTests.AUTH_RECOV_007_AReportNamesWhoMadeItAsync`.

**208 (kept, with a fix).** A link or code sent to an address the account no longer
holds still signed in, bypassing the lock and REG-IDENT-006 AC2, and an address whose
canonical form does not parse skipped the lock. A pending link or open challenge whose
email the account no longer holds is refused `auth.factor.rejected` and counted
(CONV-LOG-005); an address that does not parse goes to `DomainLock` as a domain that does
not read, which is refused wherever a lock applies. Test:
`REG_IDENT_006_ALinkSentBeforeTheAddressWasRemovedDoesNotSignInAsync`.

**328.** A host's assurance provider was never read, so every bound action of a host
using authorization without authentication was refused for ever. Replace
`IAssuranceProvider.LevelAsync` with `AttainedAsync(AccessContext, CancellationToken)`
answering `ValueTask<Result<AttainedAssurance>>`, where `AttainedAssurance` is a public
sealed record in `Janus.Core` of `Level` (`AssuranceLevel`), `PhishingResistant`
(`bool`), `AttainedAt` (`DateTimeOffset`) and `Reachable` (`AssuranceLevel`). Where no
session of the library judges the context and a provider is registered, `StepUpGates`
resolves the gate's three values as for a session (a section 5a gate from the principal's
policy, a host-named gate at the dearest gate of that policy) and admits the action when
`Level` reaches the level (`reachable` read as `Reachable`, floor `aal1`), phishing
resistance is met and `AttainedAt` lies within `maxAge` of now; otherwise it refuses
`auth.stepup.required` with `required`, `outcome` `present`, `options` empty and
`pendingUntil` null. A provider failure is unmet. With no provider the answer stays
`auth.stepup.unavailable`. Tests:
`StepUpGatesTests.LIB_HOST_004_AProviderReportingTheGateMetAdmitsTheActionAsync`,
`StepUpGatesTests.LIB_HOST_004_AProviderReportingAnOlderProofIsRefusedWithTheGateAsync`,
`StepUpGatesTests.LIB_HOST_004_AProviderReportingNoPhishingResistanceMeetsNoPhishingResistantGateAsync`;
`GateBehaviourTests.LIB_HOST_004_AC2_ABoundActionIsDeniedWithNoAssuranceProviderAsync`
stays.

**401 (kept, with a fix).** `Throttle.Remaining` computes the running delay from the
count decayed to now, so decay shortened a delay already running. It computes it from the
count the failure wrote: `Delay(counted.Failures, terms, cap)`. A test carrying
AUTH-ABUSE-001 proves a source at nine failures stays held the whole 60 seconds and its
`retryAt` is the instant it is next looked at.

**402, 422.** Three cases of failed authentication were recorded wrongly or not at all.
(1) `AuthenticationService.VerifyDeviceAsync` records its refusals, an unknown or expired
handle included, as `auth.authentication.failed` with `details` exactly
`{ "verification": "device" }` and no `factor`, through a new writer on `SessionAudit`; the
delays, the counting and `code.verification.attempts` are unchanged. (2)
`ProviderSignIn.Back` takes the refusal; where its code is `auth.throttled` it appends
`retryAt=` (the `details.retryAt` instant, ISO 8601 UTC, escaped) after `error` and before
any fragment. Every throttled return goes through it: the source delay asked before the
code is traded (422), the post-exchange delay, and a `DelegatedAsync` refusal. (3) In
`AuthenticationService.LandAsync`, where no pending link matches the token and `press` is
true, ask the source delay (`new ThrottleAttempt(source, null)`) and answer
`auth.throttled` while it stands; otherwise record `auth.authentication.failed` with the
nil subject and `details.factor` the factor the request named, count it against the
source, and answer `auth.code.expired` as now. With `press` false nothing changes. The
endpoint passes the request's `factor` and refuses a `linkToken` whose `factor` is not
`emailLink` or `phoneLink` with `api.request.malformed` naming `factor`;
`IAuthentication.LandAsync` takes the factor. Tests:
`AuthenticationServiceTests.CONV_LOG_005_AC1_AWrongDeviceCodeIsRecordedAgainstTheAccountAsync`
(asserting those details);
`AuthenticationServiceTests.CONV_LOG_005_AC1_ADeviceCodeForAHandleThatOpensNothingIsRecordedAsync`;
`ProviderSignInTests.AUTH_ABUSE_002_AC2_AThrottledReturnCarriesItsIntervalAsync` (the
source delay and the account delay of a linked identity);
`ProviderSignInTests.BFF_ABUSE_001_AC2_AThrottledProviderReturnCarriesItsIntervalAsync`
(no exchange is made);
`AuthenticationServiceTests.CONV_LOG_005_AC1_APressedLinkThatIsGoneIsRecordedBehindTheSourceDelayAsync`;
`AuthenticationServiceTests.CONV_LOG_005_AC1_AnUnpressedLinkThatIsGoneWritesNothingAsync`.

**417.** OPS-DB-001 Values (D-155) already put a later column a person spells and the
library compares or sorts under `identity_ci`. An unreleased migration runs
`ALTER TABLE identity.groups ALTER COLUMN name TYPE text COLLATE identity.identity_ci;`
and `ALTER TABLE identity.authenticators ALTER COLUMN label TYPE character varying(64)
COLLATE identity.identity_ci;`, written out as the organization domain migration is;
`ux_authenticators_label` is rebuilt under the collation by the statement. Both
properties take `.UseCollation(StoreContext.CaseInsensitiveCollation)`. Every in-memory
judgement of a held label (in `AccountService` and the enrolment path of
`CredentialService`) is replaced by one query on the authenticator port, for example
`LabelHeldAsync(subject, factor, label, except)`, a `SELECT EXISTS` over
`(subject, factor, label)` excluding the credential renamed, so the database compares
under the column's collation and the refusal is `auth.credential.labelinvalid` exactly
where the index would refuse. The in-memory fakes compare with `CanonicalForm.Of` on both
sides. Tests:
`SchemaTests.INF_DB_001_AC3_ThePlaintextColumnsComparedByValueCarryTheCollationAsync`
(`authenticators.label`, `groups.name`, `organization_domains.domain`,
`organizations.name`); `AccountServiceTests.AUTH_FACT_001_AC5_ALabelHeldInOtherCapitalsIsRefusedAsync`;
`GroupClosureStoreTests.OPS_DB_001_AnOrganizationsGroupsSortWithoutRegardToCaseAsync`.

**419.** The challenge's identifier columns were left nullable for a previous release
that does not exist. Amend the unreleased migration `AddChallengeIdentifiers` so
`identifier` and `fingerprint_version` are added NOT NULL; keep
`ck_signin_challenges_identifier` as `octet_length(identifier) = 32` alone; mark both
properties required; make `Challenge.Identifier` and the `identifier` parameter of
`Challenge.Existing` non-nullable; remove the null path through `ThrottleAttempt`;
regenerate the designer, the snapshot and the committed schema file.
`ChallengeStoreTests.OPS_SEC_003_AnIdentifierIsHeldOnlyBesideItsVersionAsync` asserts the
insert with both columns absent is refused. Registration counts through the throttle as
sign-in and recovery do (AUTH-ABUSE-001): a refused verification in a registration session
(a wrong email or phone code, a link token that opens nothing) is counted through
`ThrottleService.FailedAsync` against the source and the identifier's keyed hash, and
each registration ask of a code or link asks `DelayAsync` first. Test:
`RegistrationServiceTests.AUTH_ABUSE_001_WrongRegistrationCodesAreHeldByTheDelayAsync`.

**421.** A recognised browser's failures were not counted at all, and the token lookup
ran only for a held identifier. (1) Split `ThrottleService.Scopes`: `FailedAsync` counts
source, account and identifier whatever `Recognised` says; `DelayAsync` leaves out account
and identifier for a recognised attempt and always asks the source. (2)
`DeviceService.RecognisesAsync` takes `SubjectId?`, resolves each carried token by its
fingerprint (both kinds, no short-circuit between them) whenever a token is carried, and
compares the device's subject, kind and standing in memory;
`AuthenticationService.RecognisedAsync` calls it whether or not the identifier resolved.
(3) Remove the three-argument `IAuthentication.BeginAsync` from the contract, the public
API file and the implementation; an in-process caller passes no tokens. Tests:
`ThrottleServiceTests.AUTH_ABUSE_001_AC5_ARecognisedBrowsersFailuresCountAgainstTheAccountAsync`;
`AuthenticationServiceTests.AUTH_ABUSE_003_AC2_ACarriedTokenIsLookedUpAlikeForAHeldAndAnUnheldIdentifierAsync`.

**326 (settled).** Option A. A place is resolved for the country comparison when its
country is known. Two sessions whose known countries differ raise
`concurrent-sessions-implausible` whatever their cities; distance is the great-circle
distance between the two cities' coordinates and is measured only where both places name
a city; the same city, the same country with a city unknown on either side, or a place
with no country never raises. `ConcurrentSessions.Resolved` is changed to that rule. The
stored location carries the city's coordinates under the person's key, never shown or
exported, used only by this comparison. A test carrying OPS-ALERT-007 proves that a
session placed in one country with no city raises against a session in another country,
and that two sessions in one country, one without a city, do not.

**The preferred second step (X5).** `PUT /account/secondstep/preferred` takes the member
`09` names, `method`: `PreferredSecondStepRequest` takes `Method` in place of
`Credential`, and a value that does not read is 400 `api.request.malformed` naming
`method`. `AccountService.PreferSecondStepAsync` answers a method that is not an active
second factor enrolled on the account with 422 `api.request.invalid`, `details.member`
`method`, in place of `auth.credential.notfound`. A test carrying IDN-ATTR-008 AC2 proves
the 422 and that the preference is unchanged.

**R3, the Public Suffix List.** AUTH-FACT-012 AC2's multi-label half (`shop.com` and
`shop.co.uk` count once) needs the list, which the package did not carry. Ship the list
unmodified, with its header, as an embedded dated resource refreshed at every release, as
the offline corpus is; the implementer downloads it at build time from publicsuffix.org
itself, no more than once a day, as the site asks. `NOTICE` names the list, its licence
(Mozilla Public License 2.0, stated in its header) and its source address. The library
uses it only to validate the deployment's own configured origins: that the relying party
identifier is a registrable suffix of every origin (AUTH-FACT-010) and the count of
distinct labels of AUTH-FACT-012 AC2, applying both the ICANN and the private sections, as
browsers do. A stale copy cannot weaken a ceremony, since the browser applies its own
current list to every one; the worst case is a disagreement about the deployment's own
domains, surfaced at startup. Tests carrying AUTH-FACT-012 AC2 prove `shop.com` and
`shop.co.uk` count as one label and `a.co.uk` and `b.co.uk` as two.

#### D.3 Sending and restrictions

**118.** The replaceable `INotificationHandler` was the restriction evaluator, so a
deployment that registered its own handler sent ungoverned. The governed send path is a
contract declared in `Janus.Core` and implemented in `Janus.Authentication.Sending`, so
that the identity and privacy areas call it too: it applies the gateway floor, evaluates
the restrictions and the phone signal, writes the outbox row and answers a refusal with
`retryAt`. Every area service that sends calls it. `INotificationHandler` becomes the
carrier alone: its request is one admitted message (message kind, destination, subject,
language, values, and the reference it is carried under) and none of the evaluation's
inputs; it resolves the template and calls the transports. The library counts a message
against its buckets once the handler answers that it was taken. The default handler, the
outbox publisher and the alert router stay in `Janus.Hosting`. Test:
`SendingGovernanceTests.AUTH_ABUSE_004_AReplacedHandlerIsStillGovernedAsync` (with a
registered fake handler that takes everything, the fourth SMS to one number inside 24
hours is refused `auth.restriction.exceeded` with `retryAt` and the fake saw three; below
`abuse.sms.balancefloor` the fake sees only alerts).

**119, 227, 322, 423 (X7).** Sends were attempted inside a caller's open transaction,
and an erased subject's outstanding message stayed readable and carried. (1) Give
`IUnitOfWork` an after-commit registration that runs once the outermost transaction
commits and is discarded on rollback; the send path's one immediate attempt is such a
registration. Where no unit of work is open, the attempt follows the send path's own
commit. (2) `SendingService` gains an internal operation (for example `UndertakeAsync`)
that evaluates the floor and every restriction, writes the outbox row in the caller's
transaction and returns without attempting a transport; a refusal returns before any row
is written. (3) `InvitationService.IssueAsync` opens its transaction before the send,
writes the reservation, the invitation and `identity.invitation.issued`, then undertakes
the link's send in that transaction; a refusal returns without committing, so nothing is
issued, reserved or sent; a transport that fails later leaves the invitation standing,
the publisher retries, and exhaustion raises `degradation` scoped `send:invitation-link`.
`RecoveryService` sends its link in the same order. (4) For the sign-in link, email-code
and recovery asks, the request judges and counts the restrictions, writes one row (the
outbox row for an ask that sends, the draw for one that is held), commits and answers
before any transport is called; the outbox worker carries the message, and a failed
delivery releases its count. (5) Callers that act on a send's outcome send outside any
transaction: `LossReports` already does; `AlertRouter.RouteAsync` commits its
deduplication claim before `DeliverAsync`. (6) The erasure transaction overwrites
`wrapped_key` of every `send_outbox` row whose `subject` is the erased subject with the
erased value of PRIV-RIGHT-005a (marker `0x00`, 32 zero bytes); the publisher and the send
path remove a row whose key is erased without carrying it. (7) No link or code a
person or an administrator asked for carries the purpose `notification`: a recovery link
and an invitation link carry `signin`, as a sign-in link does, and answer to the
restrictions it answers to (the chapter finding of 423). `notification`, and so
`notification.destination`, counts security notices and other notices only.
`RecoveryService` and `InvitationService` send and draw under `signin`. Tests:
`SubjectEraserTests.PRIV_RIGHT_005_AC1_AnOutstandingMessageIsUnreadableAndUncarriedAfterErasureAsync`;
`SendingServiceTests.D_022_AMessageUndertakenInARolledBackOperationIsNeverCarriedAsync`;
`SendingServiceTests.D_022_AMessageIsCarriedOnlyAfterTheCallerCommitsAsync`;
`SendingServiceTests.D_022_AMessageUndertakenInsideATransactionThatRollsBackIsNeverSentAsync`;
`SendingServiceTests.D_022_AMessageUndertakenInsideATransactionIsCarriedAfterItCommitsAsync`;
`IDN_LIFE_009a_ALinkThatCouldNotBeSentIssuesNothingAsync` (also asserting no outbox row);
`IDN_LIFE_009a_ALinkTheTransportRefusesIsCarriedLaterAsync`;
`IDN_LIFE_009a_AnIssueThatDoesNotCommitSendsNothingAsync`; a test carrying AUTH-ABUSE-004
proves an invitation link and a recovery link are counted by no `notification`
restriction;
`ThrottlingTests.AUTH_ABUSE_003_AC2_ALinkAskIsAnsweredBeforeTheTransportIsCalledAsync`
(with a mail transport fake that blocks until released, a held and an unheld ask are both
answered 202 while it blocks, and with a transport that refuses everything both answer the
same bytes); the OPS-ALERT-003 router tests stay green.

**120.** A value a deployment or host chooses for a message place was unbounded, so a
rendered text could exceed the width it was measured at. (1) A restriction name is 1 to
64 characters of lower-case letters and digits separated by single `.`, `-` or `_`;
`RestrictionSetSetting.Accept` refuses any other with `config.value.notallowed`. The
shipped restriction names satisfy the rule. (2) The same rule binds a governing
document's name and a subject-event subscriber's name at declaration; startup refuses a
breach with the new code `model.startup.declarationinvalid`, `details.declaration`
naming the declaration and `details.field` the member. (3) `outstanding` is measured at
the joined width of the registered required subscribers' names. (4) `key` is measured at
the widest key, each family at its widest parameter. (5) `kind` stays 32, and a test
holds every subject-event kind within it. Tests:
`RestrictionSetSettingTests.INT_SMS_003_ARestrictionNameOutsideTheRuleIsRefused`;
`MessagePlaceholdersTests.INT_SMS_003_AC1_TheKeyWidthCoversTheFamilies`;
`SendingValidationTests.INT_SMS_003_AC1_TheSubscribersAreMeasuredAtTheirJoinedWidthAsync`;
`MessagePlaceholdersTests.INT_SMS_003_EveryEventKindFitsItsPlace`.

**122.** Destination records outlived D-162 item 31's bound, because every key kind
shared one table swept by the longest interval of all, and a record whose buckets were
empty stood until another send. Keep destination records in `send_counters` and move
account, source, global and host-key records to a second table `send_key_counters` of the
same two columns (key HMAC, times). Before a send's counters are read, delete from each
table the rows whose newest time is older than the longest interval of the current
restrictions of that table's key kinds; run the same two deletes in the `expiry-sweep`
job every `sweep.interval`. Tests:
`SendLedgerTests.PRIV_RET_005_AC2_ALongerSourceRestrictionKeepsNoDestinationRecordAsync`;
`ExpirySweepTests.PRIV_RET_005_AC2_ARecordIsGoneWithoutAnotherSendAsync`.

**123 and R2 (settled: option 1, as researched).** Every shipped message carrying a link
carried a bare token with no address to open. (1) A new required LIB-HOST-001
declaration, `LandingOrigins`, names the two origins a link lands on: `Authentication`,
the authentication application's, and `Account`, the account application's, each an
absolute `https` origin. Startup refuses a missing one with
`model.startup.declarationmissing` (`details.key` `landingOrigins.authentication` or
`landingOrigins.account`), and with `model.startup.declarationinvalid` an origin that is
not the origin of a registered browser client's return address, or an `Authentication`
origin that is not the origin of the declared sign-in address. (2) Every link the library
sends is `<origin>/link#<kind>.<token>`: the kind decides the application, and the token
travels in the fragment, which no request carries (RFC 9110). The kinds are a closed
vocabulary in `10`: on the authentication application `sign-in`, `registration`,
`recovery`, `enrolment`, `invitation`; on the account application `identifier`,
`identifier-confirm`, `undo`, `deletion-cancel`, `reactivation`, `loss-report`. Every kind
acts only on a press, never on load, so a mail scanner's prefetch changes nothing: the
landing component (FE-VER-001) reads the fragment, removes it from the address bar and
sends the token only when the person presses, the landing page is served with
`Referrer-Policy: no-referrer`, and no library route that answers `GET` acts on a link
token. (3) A new place `{link}` is filled by the library with that address.
The place `token` is retired: every link-bearing kind carries `link`, and the shipped
texts use it. The startup budget check measures `{link}` at its composed width: the
declared origin of the application the message's kind lands on, `/link#`, the kind, `.`
and the token's width. A message kind lands under one link kind: `signin-link` under
`sign-in`, `verification-link` under `registration` in a registration and under
`identifier` on an account, `recovery-link` under `recovery`, `enrolment-link` under
`enrolment`, `invitation-link` under `invitation`, `identifier-change-confirm` under
`identifier-confirm`, `identifier-removed` under `undo`, `deletion-notice` under
`deletion-cancel`, `deactivation-notice` under `reactivation`, `credential-suspended` under
`loss-report`. (4) A text message that carries a
link is budgeted at two segments of its alphabet (306 GSM-7 units, 134 UCS-2 units);
every other text message at one (160, 70). One token size serves every link. (5) The
bootstrap command, which cannot read host declarations, prints
`<first webauthn.origins entry>/link#enrolment.<token>`. Tests carrying INT-SMS-003 and
API-LAND-001 prove every shipped text of a link-bearing kind renders an absolute address
of its application within its budget, no template names `{token}`, a declaration naming
an origin no browser client registered fails startup, and no route answering `GET` changes
anything for a token of any kind.

**235 (settled: exact enforcement, no overrun).** A message resolved to every declared
language let a bucket end up to N-1 messages over its maximum. An email in every declared
language is one message: the library's pipeline composes it from each language's
rendered template in the order of `notification.languages`, the subject lines joined,
and it is judged and counted once; no multilingual rule enters the catalogue contract. A
text message in every declared language is one message per language, admitted only
where every applicable bucket has room for all of them (judged once with the weight N),
and each is counted. The language itself: the hosting layer carries the request's
`Accept-Language` priority list with the access context of every request; an operation
whose message goes to the acting person's own account (identifier add and replace,
step-up and new-device codes, the notices their own action causes) resolves step 2 from
it, and a message to anyone else resolves with no request locale; the new-device check
code resolves step 2 from the sign-in request's locale. `RequestOrigin.Language` passes the
ranges in descending `q` (ranges with `q=0` dropped) to the lookup of RFC 4647 section
3.4 and returns the first declared language any of them finds; registration stores what
the lookup finds. Tests carrying AUTH-ABUSE-004 prove two declared languages put one
email and two text messages in their buckets and refuse where the bucket holds room for
one; `IdentifierServiceTests.IDN_ATTR_001_ThePersonsOwnRequestDecidesWhereNoPreferenceIsHeldAsync`;
`RecipientLanguageTests.IDN_ATTR_001_ALaterRangeFindsTheDeclaredLanguage`.

**335.** A recovery-code set was closed as reminded when every notice was refused, and
every reminder counted under one constant source. In
`RecoveryCodeReminders.RemindedAsync`, send first, then mark the set reminded in the same
transaction only where at least one channel took the reminder or the set holds no channel
a reminder can reach; where every channel refused, mark nothing. The reminder carries no
source (342). Tests:
`RecoveryCodeRemindersTests.AUTH_FACT_008_AC5_ASetWhoseEveryNoticeIsRefusedStaysOwedAsync`;
`RecoveryCodeRemindersTests.AUTH_FACT_008_AC5_TwentySetsDueTogetherAreEachRemindedUnderTheShippedRestrictionsAsync`.

**342.** A restriction had no channel, so `email.destination`'s hourly bucket could never
refuse and `sms.source` counted mail. (1) A restriction gains an optional `channel`:
`sms` · `email` · `any`, default `any`. `Restrictions.Applies` consults a restriction
only for a send on its channel. The shipped restrictions carry `sms` (`sms.destination`,
`sms.source`), `email` (`email.destination`) and `any` (`notification.destination`).
`/admin/restrictions` reads and writes `channel`; a change to anything but `any` is a
loosening, a change to `any` a tightening; a stored restriction without the member reads
as `any`. (2) A security notice to an existing holder is consulted only by restrictions
whose purpose is `notification`; no restriction whose purpose is `any` counts or refuses
it. (3) A send no request asked for carries no source: `SendRequest.Source` and
`SendContext.Source` become nullable, every background sender passes null, and
`KeyOfAsync` answers no key for a `source` restriction where the send has none. (4) An
alert is outside every restriction and is governed by OPS-ALERT-002's deduplication alone,
so that no one can silence an alert by exhausting a limit: `Restrictions.Applies`
consults nothing for an alert, and the alert router passes no source. Tests:
`SendingServiceTests.AUTH_ABUSE_004_ARestrictionGovernsOnlyItsChannelAsync` replaces
`AUTH_ABUSE_004_ARestrictionGovernsEverySendWhateverItsNameAsync`;
`SendingServiceTests.AUTH_ABUSE_004_AC5_ANoticeToAHolderIsNotCountedBySmsSourceAsync`;
`SendingServiceTests.AUTH_ABUSE_004_ASendNoRequestAskedForIsCountedUnderNoSourceAsync`;
`RestrictionEndpointsTests.AUTH_ABUSE_004_AC3_NarrowingAChannelIsALooseningAsync`; a test
carrying OPS-ALERT-003 proves an alert is carried with every restriction's bucket full.

**Message kinds and places (the rows audit).** A template is chosen by kind alone, so a
kind sent sometimes with a link and sometimes without cannot be worded once. (1) Add
`verification-link` (places `code` and `link`) for the flows of
REG-SESS-003 (registration, identifier add and replace), and keep `verification-code` for
the code-only sends (the new-device check). (2) Add `credential-suspended` (place `link`)
for the loss-report and assurance-lowering removal notices, used by `LossReports` in place
of `security-notice`, with a shipped text carrying the cancel link; `security-notice`
keeps the notices that carry nothing. (3) Add `oob-deletion-notice` (no place), sent to
the security-notice set when an out-of-band erasure request is fulfilled; `deletion-notice`
stays for the account's own deletion. (4) Add `sign-in-code` (115). (5) The places `type`
and `status` carry and are measured at the section 5.12c spellings (`WrittenName.Of`),
not the C# names. (6) Startup refuses a text-message template naming a place the width
table does not define, with `model.startup.declarationinvalid` (`details.declaration` the
message kind, `details.field` the place). Tests carrying REG-SESS-003, AUTH-RECOV-007,
IDN-LIFE-003 and INT-SMS-003 prove a registration message renders its code and its link,
a loss-report notice renders its link, a fulfilled out-of-band erasure sends
`oob-deletion-notice` with no link, an alert's details carry `rectification` and not
`Rectification`, and a template naming `{offsetSeconds}` fails startup.

#### D.4 Registration and identifiers

**127 (kept, with a fix).** The passkey and authentication address checks let a field
of white space through. `DeclarationCoverage` uses `string.IsNullOrWhiteSpace`. A test
carrying REG-PM-001 proves a blank field fails startup.

**141.** Registration asked only who holds a value, not whether it is reserved, so a
registration could take an address given up minutes earlier and the owner's undo then
faulted. `IRegistrationDirectory` gains `IsReservedAsync(kind, canonical, now,
cancellationToken)`, delegating to `IIdentifierStore.IsReservedAsync`. `DispatchAsync`
answers a reserved value as a held one: no code, no notice, nothing staged that can
verify. The provider-vouched check does not vouch for a reserved address. The
invitation-bound check refuses a reserved address as a held one
(`identity.invitation.identifiermismatch`). The terms step, inside its transaction and
before the account is written, refuses a staged identifier that is held or reserved, as
REG-SESS-005 AC3 answers. Tests:
`RegistrationServiceTests.REG_IDENT_006_AC2_AReservedAddressIsAnsweredAtRegistrationAsAHeldOneIsAsync`;
`RegistrationFlowTests.REG_IDENT_006_AC2_TheUndoRestoresAnAddressARegistrationTriedToTakeAsync`.

**143.** A lost listening connection turned every open stream into a poll and nobody was
told. `IRegistrationSignals.WaitAsync` answers whether the channel was listening when the
wait began; where it was not, the stream serving `GET /register/events` raises
`degradation` with `details.component` `registration-channel` through `IAlertChannels`,
folded by OPS-ALERT-002's window. The listener reopens on the next wait. Test:
`RegistrationSignalsTests.OPS_OBS_002_ALostChannelIsRaisedAsADegradationAsync`.

**306.** A staged replace that nobody could complete blocked every later replace of the
identifier for good, and an abandoned add counted against the maximum for good. Schedule
`IPendingVerificationStore.SweepAsync` in the `expiry-sweep` job, sweeping a verification
once every code it sent is past its expiry (the new address's and, for a replace whose old
address must confirm, that one as well), as the verification-code record of 115 holds
them. In one statement set the sweep removes the verification and, for an add, the
unverified identifier it staged; for a replace, the staged value, leaving the identifier
as it stood. Tests:
`IdentifierServiceTests.REG_IDENT_007_AnAbandonedReplaceIsSweptAndANewOneIsTakenAsync`;
`IdentifierServiceTests.REG_IDENT_004_AnAbandonedAddLeavesNoIdentifierAsync`; a storage test
that a verification whose code still stands survives the sweep. The token prune is under
D.9.

**363.** The session that staged a replacement survived unrotated, and the code ended the
session it then rotated. The session kept is the one under which the change completes,
and it is rotated; every other session ends, the staging session included where it is not
the completing one. A change that completes under no session (the old address's
confirmation link, an enrolment session) ends every session. `IIdentifiers.VerifyAsync`
gains `SessionId session`; `SettleAsync` passes that session, or none, to
`EndOthersAsync` in place of `waiting.Browser`. Tests:
`IdentifierServiceTests.IDN_LIFE_008_AC1_AReplacementCompletedInAnotherSessionKeepsThatSessionAloneAsync`;
`IdentifierServiceTests.IDN_LIFE_008_AC1_AReplacementTheOldAddressConfirmsEndsEverySessionAsync`;
`AccountApplicationTests.BFF_SESS_004_AC2_TheSessionThatCompletesAReplacementAnswersToItsNewSecretAsync`.

**415 (kept, with a fix).** Account creation wrote empty text for document versions it
may leave unset. The new-account record takes both versions non-null, and a session
reaching creation without them is refused `identity.registration.incomplete`. Test:
`RegistrationServiceTests.REG_SESS_007_AC2_AnAccountIsNeverCreatedWithoutItsDocumentVersionsAsync`.

**`identity.registration.incomplete` (the rows audit).** A step whose predecessor is
incomplete is a failed state precondition: `ApiStatus` maps the code to 409, and
`ApiStatusTests` asserts it. `POST /register/confirm` with a staged identifier not yet
verified is such a step: `RegistrationService.ConfirmAsync` answers it with the code, and a
test carrying REG-SESS-003 proves the 409 over the wire.

**An unverified identifier as primary or backup.** Making an unverified identifier the
primary of its kind, or naming one as the kind's backup, is a failed state precondition:
add `ErrorCodes.IdentifierUnverified`, `identity.identifier.unverified` (409), and answer it
in `IdentifierService.MakePrimaryAsync` and in the backup setting's check (`Named`) in
place of `identity.identifier.invalid`, which stays for an identifier the account does not
hold or of another kind. Tests carrying REG-IDENT-005 and REG-IDENT-002 prove each 409 and
that nothing changes.


#### D.5 Accounts and lifecycle

**144, 315 (settled: option 1, as modified).** Photo availability was a key family that
left an account of no organization with no configurable way to show a photo. It becomes
a seventh field of the policy object, `photos` (boolean; system default `false`; a change
to `true` is a loosening), resolved as AUTH-PRIN-002 resolves every field: an account of
no organization follows `policy.default`, an account of several memberships the
strictest, so it shows a photo only where every policy shows photos. Remove the family
`photo.enabled.<organization>` and every read of it; the photo endpoints read the
resolved policy's `photos`. Bootstrap cannot see host declarations, so it writes the
administrative organization's `photos` as `false`; turning it on is an administrator's
policy change. A change that sets `photos` to `true` while no image codec is declared is
refused with `config.value.notallowed`, `details.field` `photos` and `details.requires`
`imageCodec` (X6). D-162 C.55's startup check stays as the guard: it reads every stored
policy (`policy.default` and each `policy.<organization>`) and fails with
`model.startup.declarationmissing`, `details.key` `imageCodec`, where any shows photos and
no codec is declared. Tests carrying IDN-ATTR-002 prove an account of no organization
shows a photo exactly when `policy.default` does, bootstrap writes `photos` `false`, and
turning it on without a codec is refused and changes nothing.

**169, 170, 254, 255, 257 (170 settled: option (a)).** A takedown found an account
`suspended` and its reversal returned it `active`, a reactivation without
`account:manage`; a takedown refused a running deletion the subject could then cancel from
the inbox; and state and absence were answered with `authz.denied` or
`api.request.malformed`. (1) `Account` gains `SuspensionHeld` (`SuspensionOrigin?`),
`DeletionHeld` (`DeletionOrigin?`) and `DeletionHeldSince` (`DateTimeOffset?`) beside
`RestrictionHeld`; the chapters spell the four `suspensionHeld`, `restrictionHeld`,
`deletionHeld` and `deletionHeldSince`. One migration adds `suspension_held`,
`deletion_held` and `deletion_held_since` to `accounts`, with check constraints spelled as
the existing origin columns are, allowing a held suspension only while the account is
`deleting` (by `takedown` or `oob-request`) and a held deletion only while it is
`deleting` by `takedown`; `AccountRecord`, `AccountStore` and `SubjectEraser` carry them
as they carry `restriction_held`. (2) `Account.Takedown(at)` admits `active`,
`restricted`, `suspended`, and `deleting` by `self` or `oob-request`; from `suspended` it
sets `SuspensionHeld = SuspendedBy`; from `deleting` it sets `DeletionHeld = DeletingBy`
and `DeletionHeldSince = DeletingSince`; `RestrictionHeld` as now; it then enters
`deleting` with `DeletingBy = takedown` and `DeletingSince = at`.
`AccountStates.TakeDownAsync` admits the same states. (3) `Account.ReverseTakedown()`
restores, in this order, a held deletion (`deleting` with its origin and start), else a
held suspension (`suspended` with its origin), else `restricted` where a restriction is
held, else `active`, and clears every held value; `MarkErased()` clears them too. (4) The
erasure instant of a takedown is computed in one place, used by `DeletionSweep`,
`TakedownService.ReverseAsync` and `TakedownService.ReadAsync`: `DeletingSince` plus
`takedown.grace`, and where a deletion is held, the earlier of `DeletionHeldSince` plus
`account.deletion.grace` and the trigger plus `takedown.grace` (settled: option (a); a
reversal returns the account to its own deletion and its clock). (5) Two new codes replace
the misused ones: `identity.account.notfound` (404) where the subject names no account,
and `identity.account.stateconflict` (409, `details.state` the section 5.1 state and,
where it is `suspended`, `details.suspendedBy`) where the operation does not apply to the
account's state. The takedown trigger answers a takedown-originated `deleting` 409
`identity.takedown.active` and a `deleted` account 409 `identity.account.stateconflict`;
the trigger, the read and the reversal answer an unknown subject 404
`identity.account.notfound`; a reversal of an account holding no standing takedown answers
404 `identity.takedown.notfound`. `AccountAdministration` answers the same way:
`Malformed("subject")` becomes `identity.account.notfound` in suspend, reactivate, lift,
cancel and the photo read; the state refusals of suspend (a `deleting` or `deleted`
account), reactivate (an account not suspended by an administrator, a self-deactivated one
included), lift (not `restricted`) and cancel (no grace window) become
`identity.account.stateconflict`. A context naming no person, and the reserved account,
stay `authz.denied`. (6) `TakedownService.ReadAsync` reads the account's standing beside
the latest `TakedownExecuted` delivery: the takedown stands where the account is
`deleting` or `deleted` by `takedown` since the delivery's `raisedAt`, and was reversed
otherwise; `TakedownProgress` and `TakedownProgressView` gain `Reversed`, and `ErasureDue`
becomes nullable, null once reversed. Tests:
`AccountTests.IDN_LIFE_003_AC5_AReversalRestoresTheSuspensionTheTakedownFoundAsync` (both
origins) and `TakedownServiceTests.IDN_LIFE_003_ARunningDeletionIsTakenDownAsync` (self
and `oob-request`; afterwards `POST /account/delete/cancel` answers
`identity.takedown.active`) replace
`TakedownServiceTests.IDN_LIFE_003_AnAccountAlreadyLeavingIsNotTakenDownAsync` and
`AccountStatesTests.IDN_LIFE_003_AnAccountInItsOwnWindowIsNotTakenDownAsync`;
`AccountTests.IDN_LIFE_003_AC5_AReversalReturnsARunningDeletionToItsOwnWindowAsync`;
`DeletionSweepTests.IDN_LIFE_003_ATakenDownDeletionIsErasedAtItsSettledInstantAsync`;
`TakedownServiceTests.IDN_LIFE_003_AnErasedAccountIsNotTakenDownAsync` (409);
`TakedownServiceTests.IDN_LIFE_003_ASubjectWithNoAccountIsNotFoundAsync` (404, all three
operations); `TakedownServiceTests.IDN_LIFE_003_AC2_AReversedTakedownReadsAsReversedAsync`
and a `TakedownEndpointTests` case asserting the JSON;
`AccountTests.IDN_LIFE_013_AReversedTakedownReturnsTheAdministratorsSuspension`;
`AccountTests.IDN_LIFE_013_AReversedTakedownReturnsTheOwnersDeactivation`;
`AccountStatesTests.IDN_LIFE_013_AReversedTakedownLeavesTheAccountToAnAdministratorAsync`;
`AccountAdministrationTests.IDN_LIFE_013_AnAccountBeingDeletedOrUnknownIsNotSuspendedAsync`;
`AccountAdministrationTests.IDN_LIFE_013_OnlyAnAdministratorsSuspensionIsReactivatedAsync`
(409, `details.state` `suspended`, `details.suspendedBy` `self`);
`AccountAdministrationEndpointTests.AUTH_SESS_010_AnAccountIsSuspendedAndReactivatedAsync`;
a storage test that the three columns round-trip and refuse an unknown origin; the
`ModelTests` column list; the `ErrorCodesTests` list.

**171 (X1).** `AccountSuspended` and `TakedownReversed` were written after the takedown's
commit. In `TakedownService.ExecuteAsync`, publish `AccountSuspended` after the audit
write and before the commit, and return the failure where it is refused (the unit of work
rolls back); in `ReverseAsync`, publish `TakedownReversed` before the commit the same way.
`AccountSuspended` is published at every trigger, whatever state the account held
(`active`, `restricted`, `suspended` or `deleting`), since it announces that access
stopped; `TakedownExecuted` is written as before.
Remove `TakedownServiceTests.IDN_LIFE_003_AnUnannouncedTriggerStillStandsAsync`. Tests:
`TakedownServiceTests.IDN_LIFE_003_AC4_TheSuspensionIsWrittenInTheTriggerTransactionAsync`;
`TakedownServiceTests.IDN_LIFE_003_ARefusedAnnouncementLeavesNothingAsync`; the same two
for the reversal; a storage test with the real `EventOutbox` that a trigger's commit
carries one `identity.events` row of kind `AccountSuspended` and a rolled-back trigger
carries none; a test carrying IDN-LIFE-003 AC4 proves a trigger on a `suspended` and on a
`deleting` account each writes one `AccountSuspended`.

**173, 174 (kept, with fixes).** A reversal checked just before the window's end and the
sweep erasing just after it could both commit. Read the account row `FOR UPDATE` inside
the transaction in `AccountStates.ReverseTakedownAsync` and in the eraser's
`MarkErasedAsync`, and check the window again after the lock in the reversal (X3); a
storage test with two connections proves the second of a reversal and an erasure at the
boundary waits and then refuses. A trigger or reversal `reason` past 1024 characters after
trimming is 400 `api.request.malformed` naming `reason` (X4); test
`TakedownServiceTests.API_CONV_002_AReasonPastTheLimitIsMalformedAsync`. `TakedownService`
judges the step-up after every other refusal, as every other stepped-up operation does.

**258, 259, 260, 261.** Lifting a restriction and cancelling a deletion on the subject's
behalf change another person's account and were not stepped up (X8); absence and state
were answered as in 254. (1) Add `StepUpAction.AccountRestrictionLift`
(`account:restrictionlift`) and `StepUpAction.AccountDeletionCancel`
(`account:deletioncancel`) with their section 5a rows. `IAccounts.LiftRestrictionAsync`
and `IAccounts.CancelDeletionAsync` take the `SessionId`; the endpoints pass it. (2) The
lift judges, in order: no person, 403 `authz.denied`; `account:manage`, 403; unknown
subject, 404 `identity.account.notfound`; not `restricted` (a restriction held while
`suspended` or `deleting` included), 409 `identity.account.stateconflict`; the step-up,
403 `auth.stepup.required`; then the transaction as now. (3) The cancellation judges: no
person; `account:manage`; unknown subject, 404; no grace window, 409
`identity.account.stateconflict`; a takedown, 409 `identity.takedown.active`; a closed
window, 422 `identity.deletion.windowelapsed`; the step-up; then the transaction. (4)
`IAccounts.ReadPhotoAsync` answers `identity.photo.notfound` (404) where the account shows
none or its policy withholds photos, alike, and the endpoint maps it as every refusal is
mapped, with the API-CONV-002 body; the account's own `GET /account/photo` answers the
same code. Tests:
`AccountAdministrationTests.PRIV_RIGHT_004_LiftingARestrictionAsksForStepUpAsync`;
`AccountAdministrationTests.PRIV_RIGHT_004_OnlyARestrictionInForceIsLiftedAsync` (409);
`AccountAdministrationEndpointTests.PRIV_RIGHT_004_AC2_ARestrictionIsLiftedAsync` (steps
up first); `AccountAdministrationTests.IDN_LIFE_003_ACancellationOnTheSubjectsBehalfAsksForStepUpAsync`;
`AccountAdministrationTests.IDN_LIFE_003_ATakedownOrAClosedWindowIsNotCancelledAsync`
(extended with the 409); `AccountAdministrationEndpointTests.IDN_ATTR_003_AC3_AnAccountWithoutAPhotoIsAnsweredWithTheCodeAsync`.

**Sessions ended by an administrator (X8).** Add `StepUpAction.AccountSessionsRevoke`
(`account:sessionsrevoke`) for `POST /admin/accounts/{subject}/sessions/revoke` and
`StepUpAction.SessionRevokeAll` (`session:revokeall`) for `POST /admin/sessions/revoke-all`,
with their section 5a rows; each operation takes the `SessionId` and judges the step-up
after every other refusal, and the first answers an unknown subject 404
`identity.account.notfound`. Tests carrying AUTH-SESS-011 and AUTH-SESS-009 prove each
answers 403 `auth.stepup.required` without the step-up and ends nothing.

**362.** IDN-ACCT-007 gives a `restricted` account "Can sign in: Yes, read only", and
AC2 has it read its own data and exercise its rights, every mechanism of which is reached
through a session. The library refused its sign-in. This entry restores the chapter's
rule, with every modifying action still refused through the gate. (1) Admit `restricted`
wherever sign-in admits `active`, and nowhere else: the state test becomes
`is not (AccountState.Active or AccountState.Restricted)` in
`AuthenticationService.PresentAsync`, `AuthenticationService.DelegatedAsync`,
`SignInLinks`, `OidcService.ClaimsAsync` (so the sign-on of BFF-SESS-006 and the mail
server's token answer for the account) and `RecoveryService.RecoverableAsync`; every
other state keeps its refusal. (2) `IAccountStates.RestrictAsync` takes the instant; on
the move from `active` to `restricted` it calls `sessions.EndAccountAsync(subject, at, ...)`
in the same transaction, as `TakeDownAsync` does (AUTH-SESS-010), and
`RestrictionGrant.ApplyAsync` passes its instant. A restriction held on a suspended or
deleting account ends nothing. (3) `CredentialService.ActingAsync` asks
`ISettingsRestriction` only where the authority is a live session; the enrolment session
of an approved recovery and the enrolment an AUTH-FACT-017 hold stops a sign-in at are not
asked, and `RecoveryService` sets a recovered password for a restricted account as for an
active one. (4) `InvitationAcknowledgement.AcknowledgeAsync` asks `ISettingsRestriction`
before it reads or writes anything and refuses a restricted account with
`authz.restricted`. (5) `AppPasswords.HolderAsync` admits a restricted account for
listing and revocation; creation asks `ISettingsRestriction` and is refused
`authz.restricted` (263). A mailbox is owed `enabled` while its holder is `active` or
`restricted`: a restriction the person asked for does not cut them off from their mail or
end their app passwords, and INT-MAIL-006a disables a mailbox on suspension or
deactivation, not on restriction. (6) A restricted
account's sign-in records no trusted device: `trustDevice` is not honoured and
`TrustDeviceOffered` is false. Tests:
`AuthenticationServiceTests.IDN_ACCT_007_AC2_ARestrictedAccountSignsInWithItsFactorAsync`
(a session is issued, no failure counted, no `auth.authentication.failed` row);
`AuthenticationServiceTests.IDN_ACCT_007_AC2_ARestrictedAccountSignsInWithItsProviderAsync`;
`AuthenticationServiceTests.IDN_ACCT_007_AC2_ARestrictedAccountIsSentItsSignInLinkAsync`;
`AuthenticationServiceTests.IDN_ACCT_007_ASuspendedDeletingOrDeletedAccountIsStillRefusedAsync`;
`OidcServiceTests.IDN_ACCT_007_AC2_ARestrictedAccountsClaimsAreAnsweredAsync`;
`RecoveryServiceTests.IDN_ACCT_007_AC2_ARestrictedAccountRecoversItsPasswordAsync`;
`CredentialServiceTests.IDN_ACCT_007_AC2_AnApprovedRecoveryEnrolsForARestrictedAccountAsync`;
`AccountStatesTests.AUTH_SESS_010_RestrictingAnAccountEndsItsSessionsAsync`;
`InvitationServiceTests.IDN_ACCT_007_AC2_ARestrictedAccountAcknowledgesNoInvitationAsync`;
`AppPasswordsTests.IDN_ACCT_007_AC2_ARestrictedAccountListsAndRevokesButCreatesNoAppPasswordAsync`;
`AccountApplicationTests.IDN_ACCT_007_AC2_ARestrictedAccountSignsInReadsAndIsRefusedAChangeAsync`,
end to end: restrict through the queue; the old session answers 401; a password sign-in
completes; `GET /account` answers 200 showing `restricted`; `PUT /account/profile`
answers 403 `authz.restricted`; `POST /privacy/requests` with `rectification` answers 202;
`GET /privacy/export`, after step-up, answers 200.

**An out-of-band erasure, by the account's state (found under 257).**
`PrivacyRequestService` discarded the result of `IAccountStates.BeginDeletionAsync`, which
admits only `active` and `restricted`, so an erasure request fulfilled on a `suspended`
account began no window and read `fulfilled`. Fulfilment acts by the account's state: an
`active` or `restricted` account enters `deleting` by `oob-request`; a `suspended` account
enters `deleting` by `oob-request` holding the suspension, as a takedown holds it (170),
and a cancellation returns it to `suspended` with its origin; a `deleting` account, of any
origin, is recorded fulfilled against the window already running, and nothing restarts;
a `deleted` account is recorded fulfilled and nothing further happens. A refusal of
`BeginDeletionAsync` fails the fulfilment; it is never discarded. Tests carrying
PRIV-RIGHT-001 and IDN-LIFE-003 prove each of the five states: the window opens for the
first three, a cancellation returns the suspended account suspended, the running window
of a `deleting` account keeps its start, and a `deleted` account changes nothing.

**Grants at deletion.** IDN-LIFE-014 revokes every grant of a deleted account and keeps the
rows (IDN-PRIN-003); no erasure path revoked them. In the erasure transaction, every
grant whose subject is the account and that is not revoked is revoked: `revoked_at` the
erasure instant, `revoked_by` the nil subject, `revocation_reason` `IDN-LIFE-014`. A test
carrying IDN-LIFE-014 proves that after erasure the account's grants read revoked and
every row stands.

#### D.6 Organizations, invitations, domain lock and the mail server

**154 (kept, with a fix).** Two acknowledgements committing together could leave two
current memberships. Add the partial unique index `ux_memberships_current` on
`(subject, organization)` where `ended_at IS NULL`, and lock the account's `accounts` row
(`SELECT ... FOR UPDATE`) at the start of `MembershipAttachment.AttachAsync`, before the
held memberships are read (X3). Test:
`MembershipAttachmentTests.IDN_MEM_002_AC2_TwoAttachmentsTogetherLeaveOneMembershipAsync`.

**155 (settled: option 1).** The erasure committed before its events, fell back to the
default window, and left the organization's domains readable. (1) In
`OrganizationErasureSweep.ErasedAsync`, write every `MembershipChanged` and the
`OrganizationErased` before the commit (X1), and delete the comment about a refusing
consumer. (2) A pass that cannot read `organization.deletion.grace` erases nothing and
answers the failure (X2). (3) In `OrganizationStates.EraseAsync`, in the same
transaction, replace the `domain` of every `organization_domains` row of the organization
with the organization's identifier and set `removed_at` to the erasure instant where it is
unset. (4) The `identity.organization.erased` audit row is filed under the erased
organization, category `security`; `IPrivacyAudit.RecordedAsync` takes an organization
for a system principal. Settled: an organization erasure erases no account. Remove
`ErasureReason.OrganizationErasure`; `organization-erasure` leaves `10` section 5.12a,
IDN-LIFE-003b and DR-016. Tests:
`OrganizationErasureSweepTests.IDN_ORG_003_AnErasureWhoseEventRowFailsErasesNothingAsync`;
`OrganizationErasureSweepTests.IDN_ORG_003_APassThatCannotReadItsWindowErasesNothingAsync`;
`OrganizationStatesTests.IDN_ORG_003_TheErasureLeavesNoDomainOfTheOrganizationAsync`;
`OrganizationErasureSweepTests.IDN_ORG_005_TheErasureIsFiledUnderTheOrganizationAsync`.

**200, 204.** A missing or blank reason on an organization's policy or domains is a
configuration change without a reason. `OrganizationEndpoints.ReplacePolicyAsync`,
`AddDomainAsync`, `VerifyDomainAsync` and `RemoveDomainAsync` answer a `reason` that is
absent, empty or white space with 422 `config.change.reasonrequired`, `details.key`
`policy.<organization>`, before calling the service;
`OrganizationService.ReplacePolicyAsync` and `OrganizationDomainService` answer a blank
reason the same way for an in-process caller. A reason that is not a string, or past 1024
characters, stays 400 `api.request.malformed` naming `reason`. Take `emailDomains` out of
`OrganizationPolicyBody.Fields`, so the endpoint refuses it as a member the object does
not have, 400 naming `emailDomains`, before any permission is asked; keep the service's
check for an in-process caller. Tests: the `unreasoned` cases of
`OrganizationPolicyEndpointTests.AUTH_STEP_002a_WhatAReplacementNamesMustBeReadableAsync`
and `OrganizationDomainEndpointTests.REG_DOM_001_WhatAChangeNamesMustBeReadableAsync`
assert 422 `config.change.reasonrequired` with that key and nothing written; the first
also proves a caller without `organization:manage` sending `emailDomains` is answered 400
naming it.

**209, 211, 212, 220.** (1) A verify naming a domain the organization does not list is a
record not found: add `ErrorCodes.DomainNotFound` = `identity.domain.notfound`, 404 in
`ApiStatus`; `VerifyDomainAsync` answers it in place of `Malformed("domain")`; a domain
that does not read stays 400 naming `domain`; removing an unlisted domain stays 204;
update the documentation of `IOrganizationDomains.VerifyDomainAsync`. Tests: the
`unlisted` verify of `OrganizationDomainEndpointTests.REG_DOM_001_WhatAChangeNamesMustBeReadableAsync`
asserts 404 `identity.domain.notfound`; a new case verifies a domain after its removal and
asserts the same; `ErrorCodesTests.CONV_NAME_003_AC2_ChangingACodeFailsTheContractTest`
gains the code. (2) `DomainName` takes the ASCII form of a domain from the library's own
UTS #46 mapping tables at the pinned Unicode version with RFC 3492 Punycode, not from
`IdnMapping`, which follows the machine's ICU (D-154); vendor `IdnaMappingTable.txt` beside
the other Unicode data files and generate the table as the others are. A test carrying
REG-DOM-001 proves a domain's ASCII form is the same whatever ICU the machine holds. (3)
Adding a domain to a lock while no `IDnsResolver` is registered is refused with
`config.value.notallowed`, `details.requires` `dnsResolver`; startup fails with
`model.startup.declarationmissing`, `details.key` `dnsResolver`, where a stored lock lists
a domain and no resolver is registered (X6). (4) `MailboxReconciliation` canonicalises
each address the server lists (`CanonicalForm`, `EmailAddress.TryParse`; one that does not
read counts as unknown) before comparing. Test:
`INT_MAIL_007_AC2_AListedAddressIsComparedInItsCanonicalFormAsync`.

**215.** No mail-server adapter was built, although `08` places it in `Janus.Hosting`,
phase 8 builds it, and the exit gate needs INT-MAIL-001 AC3 tested. (1) Build
`internal sealed class JmapMailServer : IMailServer` in `Janus.Hosting` beside the other
shipped defaults, over `HttpClient` and `System.Text.Json`; no package. (2) A new protected
key `integration.mailserver.endpoint` (string, default empty, required only where the
adapter is used). Where it is set and the host registered no `IMailServer`, `AddJanus`
registers the adapter; a host registration replaces it. Startup refuses a value that is
not an absolute `https` address with `integration.endpoint.insecure` naming the key. The
management credential is the mail server's API key, read through
`ISecretSource.ReadMailServerSecretAsync` only while the key is set (336), cleared after
use and presented as `Authorization: Bearer`; absent while the key is set, startup fails
with `model.startup.secretunavailable`, `details.key` `mailServerSecret`. (3) Every call is
one `POST {endpoint}/jmap` carrying a JMAP request (RFC 8620 section 3.3) whose `using` is
`["urn:ietf:params:jmap:core", "urn:stalwart:jmap"]`. A non-2xx status, a timeout, a body
that does not read, a method-level `error` response and any entry in `notCreated`,
`notUpdated` or `notDestroyed` is a failure; nothing is read as success by default. (4)
`ProvisionAsync(push)`: split `push.Address` into local part and domain; find the domain's
id with `x:Domain/query` by name (none is a failure); find the account with
`x:Account/query` filtered by `name` and `domainId`. `disabled`: where absent,
`x:Account/set` create `{ "@type": "User", name, domainId, "credentials": {}, "roles":
{ "@type": "User" }, "permissions":
{ "@type": "Merge", "enabledPermissions": {}, "disabledPermissions": { "authenticate":
true } }, "encryptionAtRest": { "@type": "Disabled" } }`; where present, update
`permissions` so `authenticate` is in `disabledPermissions`. `enabled`: where absent,
create with `"permissions": { "@type": "Inherit" }`; where present, update `permissions`
so `authenticate` is not in `disabledPermissions`. Every create carries `description`,
the library's identifier of the mailbox. `removed`: `x:Account/set` destroy; an absent
account is success. An account the query finds, or that a refused create (the name
exists) leads the adapter to query, is the mailbox's only where its `description` carries
the mailbox's identifier; then it is updated, so a replayed push converges and creates
nothing twice. One that does not carry it is never adopted: the push fails without retry
and raises `degradation` at once, naming the mailbox by its identifier, since retrying
cannot resolve a conflict (221). (5) `MailboxesAsync`: `x:Account/query` and,
by result reference in the same request, `x:Account/get` of `emailAddress` and
`permissions`; accounts of `@type` `User` only; `Enabled` is false exactly where
`authenticate` is in `disabledPermissions` or, under `Replace`, not in
`enabledPermissions`. (6) App passwords: `x:AppPassword/get`; `x:AppPassword/set` create
`{ description, expiresAt, "permissions": { "@type": "Inherit" }, "allowedIps": {} }`
answering `created[id].secret` and the id; destroy by id, a `notFound` answer being
`auth.credential.notfound`; each call carries the person's token as the bearer and no
management key. The JSON encoding of `Set<Permission>` is read off the mail server's
object reference while building. (7) Tests in `Janus.Hosting.Tests`, against an in-memory
JMAP endpoint (an `HttpMessageHandler` fake holding domains, accounts and app passwords,
refusing a create whose name exists): `INT_MAIL_001_AC3_EveryOperationIsOneJmapRequestAsync`;
`INT_MAIL_006_AC1c_AReservedMailboxIsCreatedWithAuthenticationDisabledAsync`;
`INT_MAIL_006a_AC1_ADisablePushDisablesAuthenticationAsync`;
`INT_MAIL_007_AC1_AReplayedPushCreatesNoSecondAccountAsync`;
`INT_MAIL_006a_AC3_TheListingAnswersEnabledStateAsync`;
`INT_MAIL_010_AC1_AnAppPasswordIsOneCallCarryingThePersonsTokenAsync`;
`JmapMailServer_AnAnswerThatDoesNotRead_IsAFailureAsync`;
`INT_GEN_001_AC1_APlaintextMailServerEndpointStopsStartupAsync`; a test carrying INT-MAIL-006
proves an existing server account without the mailbox identifier is not adopted. The
library's own integration tests keep running against the `IMailServer` fake. Mailbox
provisioning stays the library's own (D-006), whatever messaging library later carries
mail.

**221 (settled: option 1, as modified and simplified).** A mailbox held before never
passes silently. An invitation asserting a corporate address that has a mailbox held
before, by anyone, the same person included, is refused 409 with the new code
`identity.invitation.mailboxheld` unless its body carries `formerMailbox`: `transfer` (the
invitee receives the mailbox with its mail) or `replace` (the old mailbox is removed at the
server and a new one reserved). Either is judged with the issue's step-up, carries the
issue's reason and is recorded in the issue's audit record. No check of who the invitee is
is made at issue, since issuing tells nothing about accounts (entry 232). The adapter
adopts an existing server account only as 215 point 4 says. Tests carrying REG-MAIL-003
prove an invitation of an address whose mailbox was held before is refused 409 without
`formerMailbox`, for another person and for the same person alike; `transfer` keeps the
mailbox and its mail for the invitee; `replace` pushes `removed` and reserves a new
mailbox.

**An organization named by path (X5).** Add `ErrorCodes.OrganizationNotFound`,
`identity.organization.notfound` (404). Every route under `/admin/organizations/{id}`
answers it where `{id}` names no organization, in place of `Malformed("id")`:
`OrganizationService` (the policy read and replacement, the deletion request and its
cancellation), `OrganizationDomainService` (the list and every change) and
`InvitationService.IssueAsync`, where an organization whose deletion was requested stays
403 `authz.denied` (230). Tests carrying IDN-ORG-003, REG-DOM-001 and REG-INV-001 prove the
404 for an unknown identifier on each route and that nothing is written.

**223, 225, 228, 229, 230, 231, 233 (the invitation's issue).** (1) An integrated
invitation without a personal `email`, without a `corporateEmail`, or naming the corporate
address as the personal one, is refused 422 with the new code
`identity.invitation.addressrequired`, `details.member` `email` or `corporateEmail`, in
`BoundAsync`; `identity.identifier.invalid` stays for a value that does not read as an
email or a phone. Tests:
`InvitationServiceTests.REG_INV_001_AC4_AnIntegratedInvitationNeedsAPersonalEmailAsync`
and `InvitationEndpointTests.REG_INV_001_AC4_AnIntegratedInvitationWithoutAPersonalEmailIsRefusedAsync`
assert the code, 422 and the member, with cases for a missing `corporateEmail` and for
the two addresses equal. (2) `registration.phone` takes `required` or `optional` only:
remove the `registration.phone` read and its `Off` refusal from `BoundAsync`, and
`InvitationServiceTests.REG_INV_001_APhoneIsNotBoundWhereTheDeploymentCollectsNoneAsync`.
`Set` in every `ConfigurationInMemory` fake passes the value through the setting's
`Accept` and throws where it is refused, so no test holds a value no deployment can; run
the suites and correct every test this exposes. (3) Where the invitation names at least
one role, `IssueAsync` also asks `StepUpAction.GrantManage` after `invitation:issue`, both
judged last, and returns the first refusal. A role the deployment does not hold, named in
the body, is refused 422 `authz.grant.unresolved` naming `roles` (X5; the roles become
grants at the acknowledgement, as 184's are). Tests:
`InvitationServiceTests.REG_INV_001_ARoleAsksTheGrantGateAsWellAsync`;
`InvitationServiceTests.REG_INV_001_TheRolesAttachedAskWhatAGrantAsksAsync` asserts the
refusal of an undefined role. (4) A document the body names that was never published is
refused 422 `api.request.invalid` naming `documents` (X5), not `api.request.malformed`,
which stays for a blank name. Test:
`InvitationServiceTests.REG_INV_001_AnUnpublishedDocumentIsRefusedAsync`, with an
`InvitationEndpointTests` case asserting 422. (5) The branch refusing an organization whose
deletion was requested answers `authz.denied`, as the gate before it does. (6)
`ReservedAsync` answers an address a member holds, or one a standing unexpired invitation
reserves, 409 with the new code `identity.mailbox.taken`, `details.member`
`corporateEmail`. Test: `InvitationServiceTests.REG_MAIL_001_AnAddressAlreadyTakenIsRefusedAsync`
(both cases), with an endpoint case asserting 409. (7) `RevokeAsync` answers an invitation
the organization did not issue, or none, 404 with the new code
`identity.invitation.notfound`, no `details`. Test:
`InvitationServiceTests.IDN_LIFE_009a_OnlyAnUnacknowledgedInvitationOfTheOrganizationIsRevokedAsync`.

**234 (kept, with a fix).** The ciphertext of what an invitation binds was bound by its
additional authenticated data to table and column with a zero subject, so one row's value
decrypts on another. `InvitationStore` puts the invitation's identifier in the subject
position of the additional authenticated data. A test carrying PRIV-RIGHT-005a proves one
invitation's value and wrapped key do not open on another row.

**242, 244, 245, 246, 247 (the acknowledgement).** (1) `InvitationService.AttachedAsync`
sets `invitedBy` to the inviter's display name, else the inviter's primary email (through
`IIdentifierDirectory.HeldAsync`), and null only where neither reads (an erased inviter).
Test: `InvitationServiceTests.REG_INV_001_AnInviterWithNoDisplayNameIsShownByTheirPrimaryEmailAsync`.
(2) `InvitationAcknowledgement.AcknowledgeAsync` judges, before anything is written, the
refusals that can never be met (the membership limit, the email maximum) before the
credential policy, then the rest (X9). An `invitationId` in the body that names no
invitation, or one attached to another account, is answered 404
`identity.invitation.notfound`, one answer for both (244). (3)
After the mismatch check and before anything is written, while the organization's
`emailDomains` lock is on, it judges through `DomainLock.RefusedInAsync` the address the
member will sign in with: the corporate address where one is taken on, else the bound
email, else at least one verified email the account holds; a refusal is 422
`identity.identifier.domainnotallowed`. Test:
`InvitationServiceTests.REG_DOM_001_AnOpenInvitationIsAcknowledgedOnlyWithAnAddressTheLockAdmitsAsync`.
(4) `InvitationAcknowledgement.Enrol` answers `auth.stepup.required` with `outcome` `enrol`
and `policyRequirement` `{ field, value }` (AUTH-FACT-017, no `deadline`: no grace applies
to an account joining), in place of its flat `field` and `value`. In the acknowledgement's
transaction, every live session of the account is downgraded (AUTH-SESS-009), so its next
gated action asks a presentation; and a step-up presentation of a factor outside the
`loginFactors` of the policy in force answers `auth.factor.notpermitted`, in
`AuthenticationService.AcceptsAsync` and `SessionService.PresentAsync` alike, as a sign-in
does. Tests:
`InvitationServiceTests.REG_INV_002_AC2_AnAccountBelowTheRequiredAssuranceIsHeldAtEnrolmentAsync`
asserts `policyRequirement`;
`AuthenticationServiceTests.IDN_LIFE_009b_ASessionHeldBeforeTheMembershipIsDowngradedAsync`.
(5) Before anything is written, it evaluates through `IAccessGate` with
`AccessContext.Of(invitation.Inviter)` that the inviter still holds `membership:manage` in
the organization, `grant:manage` there where roles are named, and `system:administer` in
the administrative organization for a named role that carries it; otherwise it answers 422
`identity.invitation.expired` and writes nothing. `MembershipAttachment.AttachAsync` skips
a role only where the account holds the same live grant with no expiry. Tests:
`InvitationServiceTests.REG_INV_001_AnInviterWhoLostTheRightToGrantGrantsNothingAsync`;
`MembershipAttachmentTests.REG_INV_001_AnExpiringGrantDoesNotStandInForThePermanentOneAsync`.

**248, 251 (settled: option 1).** Taking the corporate address on at the acknowledgement
publishes `IdentifierAdded`, and retiring it at the end of the membership publishes
`IdentifierRemoved`, each in the transaction that makes the change (X1), keyed
`<identifier>@<ticks>` as `IdentifierPrimaryChanged` is. Tests:
`InvitationServiceTests.REG_INV_001_AC4_TheCorporateAddressIsAnnouncedAsAddedAsync`;
`InvitationServiceTests.REG_MAIL_003_TheRetiredCorporateAddressIsAnnouncedAsRemovedAsync`.

**250.** Ending a membership changes another person's account (it retires an identifier,
changes the primary email, disables the mailbox) and was not stepped up; an account
holding no membership was answered 400. Add `StepUpAction.MembershipEnd`
(`membership:end`) with its section 5a row. `IInvitations.EndMembershipAsync` takes the
`SessionId`; the endpoint passes it. `MembershipEnd.EndAsync` judges, in order: no person,
403 `authz.denied`; `membership:manage` in the organization, 403; the account's current
membership of the organization, read before the unit of work begins through a find on
`IMembershipEnding`, none being 404 with the new code `identity.membership.notfound` and
no transaction begun (X9); the step-up, 403 `auth.stepup.required`; then begin, end,
retire, audit, publish and commit as now. A second end answers 404. Tests:
`InvitationServiceTests.IDN_MEM_001_AnAccountHoldingNoMembershipThereIsNotFoundAsync`
(404, nothing written, a second operation in the same scope commits);
`InvitationServiceTests.IDN_MEM_001_EndingAMembershipAsksForStepUpAsync`;
`InvitationEndpointTests.IDN_MEM_001_AMembershipIsEndedAsync` updated; the `ErrorCodesTests`
and step-up action lists.

**263.** App passwords were answered `authz.denied` where the account holds no mailbox. Add
the code `identity.mailbox.notfound` (404). In `AppPasswords`, where `HolderAsync` finds no
mailbox the server is told to enable (none, retired, an account neither `active` nor
`restricted`, or no `IMailServer` registered), the operations answer
`identity.mailbox.notfound` in place of `authz.denied`; a context naming no account stays
`authz.denied`. A restricted account lists and revokes and is refused creation (362).
Update the remarks of `IAppPasswords`. Tests:
`AppPasswordsTests.INT_MAIL_006_WithoutAnEnabledMailboxThereAreNoAppPasswordsAsync` and
`AppPasswordFlowTests.INT_MAIL_006_AnAccountWithoutAMailboxIsRefusedAsync` assert 404
`identity.mailbox.notfound`; the restricted case of the first asserts `ErrorCodes.Restricted`
for creation.

**413.** The organization name's comparison key was left nullable for a previous release
that does not exist. Amend the unreleased migration `AddOrganizationComparisonKeys` so
`canonical_name` is added NOT NULL with no default; mark the property required; make
`OrganizationRecord.CanonicalName` a non-nullable `string`; regenerate the designer, the
model snapshot and the committed schema file; delete the comments that promise a later
tightening and the clause of `OrganizationStore` about rows written before the key. Test:
`OrganizationStoreTests.IDN_ACCT_004_AC3_AnOrganizationWithoutItsKeyIsRefusedByTheDatabaseAsync`.

#### D.7 Privacy

**130, 131.** Withdrawing a consent never held, objecting before any notice and
withdrawing an objection never made were answered `authz.denied`, which means a missing
permission. (1) `ConsentService.WithdrawAsync`, for a declared consent-based purpose the
subject holds no record for, answers success, writes nothing, raises no `ConsentChanged`
and records nothing; `WithdrawObjectionAsync` does the same for an objectable purpose with
no objection held. (2) `ConsentService.ObjectAsync`, where no version of the privacy
notice is published, answers `privacy.notice.unpublished`. (3) The fake `ConsentsInMemory`
refuses a purpose not taken with `privacy.purpose.noconsent`, and
`RegistrationServiceTests.PRIV_CONS_001_AC1_AControlForAPurposeTakingNoConsentIsRefusedAsync`
asserts `ErrorCodes.PurposeNoConsent`. (4) Remove the first sentence of the comment on
`PrivacyRequestService.DecidableAsync`, which states the rule D-162 item 93 reversed.
Tests: `ConsentTests.PRIV_CONS_008_AC1_WithdrawingAConsentNeverGivenAnswersAsTheWithdrawalAsync`
(204, no record, no event, no audit row);
`ConsentTests.PRIV_RIGHT_001a_AnObjectionBeforeAnyNoticeIsNamedAsSuchAsync`;
`ConsentTests.PRIV_RIGHT_001a_WithdrawingAnObjectionNeverMadeAnswersAsTheWithdrawalAsync`; a
registration test through the real `ConsentService` asserting 422
`privacy.purpose.noconsent` for a ticked control whose purpose takes no consent.

**133, 147.** The list filter and the SQL fragment read no consent, so a list admitted
every unconsented subject's record; a consent record named a version but not the document;
and a grant overwrote the earlier record. (1) Add the view
`identity.consented_resources (resource_type, resource_id, purpose, kind)`: every
`resources` row whose `subject` holds a live `consents` row for `purpose` (neither
`withdrawn_at` nor `superseded_at` set) recorded against the document the purpose now
names. Grant `SELECT` to `identity_app` in the same migration; `DatabaseRoleTests` covers
it. (2) Add the public record `ConsentedResource` in `Janus.Core`, mapped by
`MapAuthorizationTables(ModelBuilder)` beside `AncestryEntry` and `EffectiveGrant`;
`FilterSources<TResource>` takes its `IQueryable` as a third required source. (3)
`PermissionRule`, where the permission's purpose is consent-based, adds to both renderings
an `EXISTS` over the view for the row's type and identifier and that purpose, with
`kind = 'written'` where the purpose requires written consent, built from the one rule
definition and parameterised (AUTHZ-GATE-002 AC1, AC3). Only consent-based purposes are
held back while a subject is asked again (D-066, D-162 item 89, PRIV-CONS-007 AC2); the
view leaves out superseded consents. (4) A migration gives `consents` and `objections` a
`document` column (text), filled with `privacy-notice` for existing rows and then set NOT
NULL. A grant writes the governing document whose version it read; an objection writes
`privacy-notice`. `ConsentRecord` and `ObjectionRecord` gain `Document`, and
`GET /privacy/consents` and `GET /privacy/objections` carry `document`.
`Supersession.OfAsync` and `IConsentStore.LiveAgainstAnotherAsync` select live consents by
document and version. The gate (`AccessGate.Unconsented`) and the view treat a consent
whose document is not the one its purpose now names as superseded
(`privacy.consent.superseded`). (5) Both tables are keyed on an identifier: a grant inserts
a row, a withdrawal or a supersession stamps the live row, and a partial unique index on
`(subject, purpose)` where `withdrawn_at` and `superseded_at` are null keeps one live row;
`GET /privacy/consents` answers every row, and a reader wanting the current state reads the
live row. Tests:
`GateBehaviourTests.PRIV_SENS_002_AC1_AListAdmitsOnlyTheRecordsWhoseSubjectsConsentedAsync`;
`GateBehaviourTests.PRIV_SENS_002a_AC2_AWithdrawalRemovesTheRecordFromTheNextListAsync`; a
consent-bound case in the truth-table suite asserted equal across both renderings;
`PublicSurfaceTests` lists the new record;
`SupersessionTests.PRIV_CONS_001_AC2_AConsentNamesTheDocumentAndVersionItWasGivenAgainstAsync`;
`ConsentGateTests.PRIV_CONS_007_APurposeGivenAnotherDocumentAsksItsSubjectsAgainAsync`;
`ConsentStoreTests.PRIV_CONS_001_AC3_AGrantAfterAWithdrawalKeepsTheWithdrawnRecordAsync`.

**148.** The re-consent rule was carried by the endpoint, so a host calling the contract got
another answer. `ConsentService.GrantAsync`, where the mechanism named is `dashboard` and
the subject's record for the purpose is superseded and not withdrawn (read inside the
grant's transaction), records `reconsent`; every other mechanism is recorded as named.
`PrivacyEndpoints.GrantAsync` passes `ConsentMechanism.Dashboard` and nothing else; remove
`Reasked`. Tests:
`ConsentTests.PRIV_CONS_001_AC1_ADashboardGrantOverASupersededConsentIsRecordedAsReconsentAsync`;
`ConsentTests.PRIV_CONS_001_AC1_AnAdministratorGrantOverASupersededConsentIsRecordedAsNamedAsync`.

**150.** The export left out grants outside the account's memberships, revoked and expired
grants, and group memberships. (1) Add the internal read
`IGrantStore.NamingAsync(SubjectId subject, CancellationToken)`: every `grants` row whose
subject is the account, in every organization and state, by `granted_at`. Replace
`ExportSource.ConferredAsync` with a projection over it that adds `revokedAt` and never
carries `grantedBy`, `revokedBy`, `reason` or `revocationReason`. (2) Add
`IGroupStore.HoldingAsync(SubjectId member, CancellationToken)`: the groups whose
`group_members` row names the account directly; the export gains a section
`group-memberships` after `membership-acknowledgements`, one record per group with
`group`, `name` and `organization`. Tests:
`ExportSourceTests.PRIV_RIGHT_003_TheExportCarriesEveryGrantNamingTheAccountAsync`;
`ExportSourceTests.REG_ACCT_001_TheExportCarriesTheGroupsTheAccountBelongsToAsync`.

**156 (kept, with a build).** PRIV-BASIS-001 and D-108 require a library table seeded
from the declaration and a register that emits the basis's label; neither was built. Add
the lawful basis table (key, label, and the four properties of PRIV-BASIS-001), seeded from
the declaration at startup; purposes reference a basis by key; the records of processing
emit its label. The lists ship as `LawfulBases.Default` and `SensitiveCategories.Default`,
which a host passes to the builder; the library applies no list the host did not declare,
and `children` is the one category it reads by name.
`ProcessingRecordsTests.PRIV_BASIS_001_AC2_TheBasisColumnCarriesTheDeclaredLabelAsync`
asserts the label, not the key.

**The data category of an encrypted field.** PRIV-PRIN-001 has each encrypted field name
the data category it holds. `EncryptedFieldDeclaration` gains the category beside `Field`
and `SubjectColumn`, and the builder's declaration of an encrypted field takes it. At
startup a field whose category no purpose declared on its type names fails with
`model.startup.declarationinvalid`, `details.declaration` the type and `details.field` the
field. A test carrying PRIV-PRIN-001 AC2 proves the refusal and that a field whose
category a purpose names starts.

**264.** Reading (b) left a failed takedown or restriction delivery with no manual path,
which IDN-LIFE-003a requires. `IErasures.CompleteAsync` and
`POST /admin/erasures/{id}/complete` close a `failed` delivery of kind `ErasureRequested`,
`TakedownExecuted` or `RestrictionChanged`, named by its delivery identifier, under the
same permission (`privacyrequest:manage`) and step-up (`erasure:complete`).
`ErasureService.CompleteAsync` accepts the three kinds; the DR-016 ledger line and the
erasures row are written for `ErasureRequested` alone. The audit record
`privacy.erasure.completed` gains `details.kind` (`erasure-requested`,
`takedown-executed`, `restriction-changed`). 404 `privacy.erasure.notfound` where no
delivery of the three kinds is held under the identifier; 409 `privacy.erasure.notfailed`
as now. The reads stay erasures only. Tests:
`ErasureServiceTests.IDN_LIFE_003a_AFailedTakedownDeliveryIsCompletedByHandAsync` and
`ErasureServiceTests.IDN_LIFE_003a_AFailedRestrictionDeliveryIsCompletedByHandAsync`
replace `IDN_LIFE_003a_OnlyAnErasureIsCompletedByHandHereAsync`;
`TakedownServiceTests.IDN_LIFE_003_AC2_ATakedownCompletedByHandReadsCompleteAsync`.

**270 (kept, with fixes).** The shipped register's rows: the mail server row applies
where a mail server is integrated (`IMailServer` registered or
`integration.mailserver.endpoint` set) or the library's default mail transport is in use,
in place of `integration.mail.endpoint` alone; the SMS gateway row applies to every
deployment, because every deployment declares SMS alert destinations. Until the shipped
transports exist, a deployment registering no `IMailTransport` or no `ISmsTransport` fails
startup with `model.startup.declarationmissing`, `details.key` `mailTransport` or
`smsTransport`. The developer relationship is a row a host declares, not a shipped one.
Tests carrying PRIV-ROPA-002 and LIB-EXT-001 prove the mail server row with `IMailServer`
registered and a host transport, and the startup refusal of a missing transport.

**332, 333.** (1) Where a ledger is registered, the outbox pass also reads the
`ErasureRequested` records that are `complete` (a manual completion included) and hold no
`erasure-ledger` confirmation, oldest first, a page at a time; it appends each one's line
(`ErasureLedgerLine.Of`) and records the confirmation, changing neither the record's status
nor its attempts nor the erasures row; a refused append leaves the record for the next
pass. Test:
`OutboxPublisherTests.DR_016_AnErasureCompletedBeforeTheLedgerWasRegisteredIsAppendedOnceAsync`.
(2) `ErasureReplay` and `DeletionSweep` write `details.reason` by its written name
(`minor-takedown`), as `ErasureLedgerLine.Spelling` does. Tests:
`ErasureReplayTests.DR_016_TheAuditCarriesTheReasonInItsWrittenSpellingAsync` and its
equivalent for the sweep.

**406.** An exact match on four names let `Cross Border Transfer` rest on consent, and the
refusal borrowed a code meaning an absence. `AuthorizationModel` compares a consent-based
purpose's name, lowered and stripped of every character that is not a letter or a digit,
against `hosting`, `transfer`, `hostingtransfer` and `crossbordertransfer`, and refuses a
match with the new code `model.purpose.hostingconsent` (in `ErrorCodes`, 500 in
`ApiStatus`), `details.key` `<type>.<purpose>`. Test:
`AuthorizationModelTests.INT_HOST_002_AC1_AConsentPurposeForTheHostingFailsStartup` gains
`Cross Border Transfer` and `hosting_transfer` and asserts the code;
`PRIV_CONS_010_AC1_NoLibrarySourceNamesATransferPurpose` keeps `AuthorizationModel` as the
one source naming them.

**414 (kept, with a fix).** `PrivacyRequestService.SubmitAsync` and `EnterAsync` apply the
trimmed 1 to 1024 rule to `detail`, `channel` and `identityConfirmation`, answering
`api.request.malformed` naming the member (X4); `detail` on the administrative entry stays
optional when absent. Tests:
`PrivacyRequestServiceTests.API_CONV_002_ABlankOrOverlongDetailIsMalformedAsync`;
`PrivacyRequestServiceTests.API_CONV_002_AnEntryWithABlankChannelOrConfirmationIsMalformedAsync`.

**Fulfilling a privacy request (X8).** Add `StepUpAction.PrivacyRequestFulfil`
(`privacyrequest:fulfil`) with its section 5a row: every `POST
/admin/privacy/requests/{id}/fulfil` is the step-up action, for every request type;
refusing a request is not gated. `IPrivacyRequests` fulfilment takes the `SessionId` and
judges the step-up after every other refusal. A test carrying PRIV-RIGHT-002 proves a
fulfilment without the step-up answers 403 `auth.stepup.required` and changes nothing.

#### D.8 Operations: configuration, bootstrap, break-glass, keys, alerts, jobs, audit

**116, 132 (X2).** `ConfigurationStore.ReadAsync`, its family overload and
`ReadWrittenAsync` throw an `InvalidOperationException` naming the key, and never the
stored text, where a stored row does not read under its key. Remove every
configuration-read fallback: the 28 sites and the two that read `TimeSpan.Zero` for
`account.deletion.grace` (entry 116), the `registration.adultaffirmation` and
`hosting.location` fallbacks of `ProcessingRecordsService`, and every other the sweep
finds. Tests: `ConfigurationStoreTests.ReadAsync_AStoredValueThatDoesNotParse_IsAFaultAsync`
asserts the throw;
`AccountLifecycleTests.OPS_CFG_008_AMalformedGraceIsAFaultAndNotAnElapsedWindowAsync`.

**124, 179, 407 (407 settled: option A).** A reason is required on every change to a
runtime setting, a tightening of the restriction set included; OPS-CFG-008's sentence "a
tightening needs the step-up and the audit entry only" is amended. The code
`auth.restriction.reasonrequired` is renamed `config.change.reasonrequired` before any
release (the `ErrorCodes` member renamed to match), and every refusal that carried the old
code carries the new, a restriction grant without a reason included.
`ConfigurationAdministration.RefusalAsync` checks the reason for every key with no
exemption for `restrictions`; `RestrictionAdministration.EditAsync` refuses an edit
without a reason whatever its direction; `ConfigurationEndpoints` refuses a blank reason
with `config.change.reasonrequired` naming the key and one past 1024 characters with 400
`api.request.malformed` naming `reason` (X4). Tests: the `RestrictionAdministrationTests`
case in which a reasonless tightening passed becomes
`OPS_CFG_008_AC2_ARestrictionTighteningWithNoReasonIsRefusedAsync`;
`ConfigurationEndpointTests.OPS_CFG_005_EveryChangeCarriesAReasonAsync` is extended.

**178 (kept, with a fix).** The direction of a change was decided on a value a concurrent
change could move. `ConfigurationAdministration.ChangeAsync` and `ChangeMemberAsync` read
the setting's row `FOR UPDATE` before classifying; a key with no row is inserted, and a
concurrent insert that fails on the primary key is a fault (X3). A test with two
connections proves a change waits for a concurrent one and classifies against its
committed value.

**180, 181.** `retention.<category>` is runtime and had no route. `ConfigurationService`
serves every `retention.<category>` of a category the model declares, read with its floor
as `default` where no value is written, changed under `config:manage` in the
administrative organization with the family's direction (shortening loosens): below the
floor `config.value.belowfloor`; a loosening needs `system:administer`, the `config:loosen`
step-up and a reason; written through `ConfigurationAdministration.ChangeMemberAsync`. Any
other name answers 400 naming `key`. `SettingDirection`'s members carry
`JsonStringEnumMemberName("increase")`, `("decrease")` and `("any-change")`. Tests:
`ConfigurationEndpointTests.PRIV_RET_001_ACategorysRetentionIsChangedThroughTheRouteAsync`;
`ConfigurationEndpointTests.PRIV_RET_001_AnUndeclaredCategoryIsNoKeyAsync`;
`ConfigurationEndpointTests.OPS_CFG_004_AKeyReadsWithItsDefaultAndWhetherItIsProtectedAsync`
asserts the spelling.

**193.** The store's write was public, so any host could write a runtime key with no
step-up, no reason and no record. Remove both `WriteAsync` overloads from
`IConfigurationStore` and their lines from the public API file; declare them on an internal
port `IConfigurationWrites` in `Janus.Authentication`, taken by `ConfigurationAdministration`
alone; `ConfigurationStore` implements both; the test fakes written through implement the
port as well. Test: `PublicSurfaceTests.OPS_CFG_005_TheConfigurationStoreOnlyReads`.

**319 (settled: option A).** Three protected switches were read by nothing. Retire
`audit.enabled`, `token.signature.verification` and `stepup.enforcement.<organization>`
from the settings catalogue and from OPS-CFG-004's list: the library records every event,
verifies every signature and enforces every bound gate unconditionally; `configure`
refuses them as it refuses any key that is not protected. Three fixes: (1)
`ProtectedConfiguration.CompleteAsync` runs over the written values, before the commit,
the checks the start runs: `RelyingParty.Of` for the WebAuthn keys, the endpoint rule of
`SendingValidation` (INT-GEN-001), the algorithm check of `SigningKeys`, and
`RedirectValidation` (`model.startup.redirectclient`), refusing with the code each gives.
Test: `ConfigureTests.OPS_CFG_004_ARelyingPartyIdentifierNoOriginSharesIsRefusedAsync`. (2)
`before` is the written form of the value in force, the default where no row stood, and
null only for a required key with no row. (3) `ConfigurationChange` carries the principal's
name where one acted, and `IConfigurationAudit` gains a read by principal name. Tests
carrying OPS-CFG-005 prove `before` and the read.

**329 (and its sweep).** Turning `exfiltration.export.stepuprequired` from `true` to
`false` raises `stepup-policy-weakened` (High), `details.key` naming the key, in the
change's transaction; a failure to raise fails the change. The expiry sweep deletes
`bulk_exports` lines older than one hour. Tests:
`ConfigurationEndpointTests.OPS_ALERT_001_TurningExportStepUpOffRaisesStepUpPolicyWeakenedAsync`;
`BackgroundJobsTests.IDN_PRIN_003_AC4_AnExportPastItsHourIsClearedWithNobodyAskingAsync`.

**404.** Seven keys marked protected in `10` stood outside OPS-CFG-004's list as a third
scope, which D-152 forbids. `webauthn.origins`, `webauthn.algorithms`, `hosting.location`,
`hosting.crossborderbasis`, `integration.mail.endpoint`, `integration.sms.endpoint` and
`redirect.defaultclient` join the list and `10` section 4.8, and so does the new
`integration.mailserver.endpoint`. `ReferenceRows` reads the rows of section 4.8.
`SettingsCatalogueTests.OPS_CFG_001_AC1_ARedeployScopedKeyIsOnTheOpsCfg004List` replaces
`OPS_CFG_001_AC1_ARedeployScopedKeyIsOnTheOpsCfg004ListOrDeclared`: the protected keys and
families of `Settings` equal that list. Add
`SettingsCatalogueTests.OPS_CFG_004_AKeyMarkedProtectedIsOnTheOneList`; remove
`DeclaredAboutTheDeployment`, the typed `ProtectedBySectionFourEight` list and
`Scope_TheCatalogue_ProtectsTheKeysSectionFourMarks`, which the two tests cover. No runtime
behaviour changes.

**305.** `outbox.poll.interval` paced the alert dispatch with no ceiling, so one loosening
could hold every alert back for ever. `Settings.OutboxPollInterval` takes the ceiling
`PT1M`, refused above it with `config.value.aboveceiling` at startup and at change. The
balance poll fails where no `ISmsTransport` is registered, and its lapse raises
`background-job-failed`. Tests: the catalogue test pins the ceiling;
`ConfigurationAdministrationTests.OPS_CFG_003_AnOutboxIntervalAboveItsCeilingIsRefusedAsync`
(`PT2M` refused); `BackgroundJobsTests.INT_SMS_004_TheBalancePollFailsWithoutATransportAsync`.

**308, 310, 313, 157, 290 (bootstrap and `configure`).** (1) `BootstrapArguments.Read` applies
`RelyingParty.Of` over the named WebAuthn values after the value checks and before the
database, answering its `model.startup.rpid` or `model.startup.labellimit`; remove the
`api.request.malformed` branch for a first origin that is not absolute. Test:
`BootstrapRefusalTests.AUTH_FACT_010_AC1_BootstrapRefusesAnIdentifierNoOriginSharesAsync`.
The printed enrolment address is `<first webauthn.origins entry>/link#enrolment.<token>`
(R2). (2) `DeploymentBootstrap.JoinAsync` passes the nil subject as `grantedBy`, keeping
the reason `OPS-BOOT-001`, so no grant names its holder as its granter. Register `IEvents`
as `EventOutbox` in the command's composition and publish one `MembershipChanged`
(`MembershipChange.Began`) per membership bootstrap attaches (the administrator,
`emergency`, the canary) inside the bootstrap transaction. Tests:
`BootstrapTests.AUTHZ_GRANT_003_TheFirstGrantsNameNoPersonAsTheirGranterAsync`;
`BootstrapTests.D_162_EachMembershipBootstrapAttachesEmitsMembershipChangedAsync`. (3) The
`no-emergency-credential` alert of bootstrap and the alert of
`ProtectedConfiguration.RaiseAsync` are raised through `IAlertChannels.RaiseAsync` in the
same transaction, so `AlertRaised` is written with the row; register `AlertChannels` and
`EventOutbox` in the commands' compositions. Tests:
`BootstrapTests.OPS_ALERT_001_TheMissingEmergencyCredentialIsAnnouncedAsync`;
`ProtectedConfigurationTests.OPS_ALERT_001_AProtectedChangeIsAnnouncedAsync`; `ConfigureTests`
asserts an `AlertRaised` row. (4) A command `Janus.Cli` does not carry is refused with one
JSON line on standard error, `api.request.malformed` naming the command, and exit code 1.

**290 (kept, with fixes).** (1) `AlertLedger.FirstAsync` claims a key by one conditional
update (X3), so two overlapping passes deliver one alert and a new key faults nothing.
(2) The lapse of the `alert-dispatch` job is delivered by the router directly from the
worker, as the destination change is, so a stalled carrier reports itself. (3)
`AlertChannelsTests.CONV_DESIGN_005_AC1_AnEventTheHostRefusedFailsTheRaiseAsync` is renamed
for what it proves: an `IEvents` unable to write its row fails the raise. Tests carrying
OPS-ALERT-002 and OPS-ALERT-001 prove one delivery from two passes and the lapse delivered
with the job stalled.

**320.** The library's `IEvents` gave way to one a host registered before `AddJanus`, so a
host could bypass the committed row CONV-DESIGN-002 requires and the retry of D-162 item
29 without a word. Event publication is not a replaceable default (LIB-EXT-001's list
does not name it): `AddJanus` registers `IEvents` as `EventOutbox` so that no earlier
registration pre-empts it, and a host consumes an event only by registering
`IEventConsumer<TEvent>`. The retention of delivered rows is entry 366's, already applied.
Test: `EventPublisherTests.CONV_DESIGN_002_AnIEventsTheHostRegisteredFirstDoesNotBypassTheRowAsync`
(a fake `IEvents` registered before `AddJanus`; a publication writes an `events` row and
the fake is never called).

**291, 292, 295, 296, 297, 300, 302, 331 (break-glass).** (1) Append
`AlertCondition.BreakGlassGenerated`, written `breakglass-generated`, at the next free
value, with its public API line; `Alerts.Severity` gives it High;
`AlertRouter.AudienceAsync` adds the owner's destinations for `BreakGlassUsed` and
`BreakGlassGenerated` whatever `alerting.owner.enabled` holds.
`BreakGlassService.GenerateAsync` raises `BreakGlassGenerated` and the use raises
`BreakGlassUsed`, each scoped to the issue identifier, with no `event` detail and no
`generated:` or `used:` prefix. Tests:
`BreakGlassEndpointTests.OPS_BOOT_004_AC2_GenerationIsAuditedAndReachesTheOwnerAsync`;
`OPS_BOOT_002_AC3_UseReachesTheOwnerWithOwnerAlertsOffAsync`; the section 5.23 catalogue
test. (2) The first arrival the global limit refuses raises
`Alerts.Of(AlertCondition.AuthFailuresSustained, <the reserved account's subject>, now)`
through `IAlertChannels` in a transaction of its own. Test:
`BreakGlassEndpointTests.OPS_BOOT_004_AC7_TheLimitReachedIsRaisedAsync`. (3) Add
`StepUpAction.ProviderLink` to `StepUpGuard.Unavailable`. Tests in
`BreakGlassEndpointTests`: with `google` in the reserved account's `loginFactors`, `POST
/account/link/google` from the break-glass session answers 403 `authz.denied`;
`OPS_BOOT_002_NoSignInMethodIsGivenToTheReservedAccountAsync` sends one request per member
of the list (`POST /account/password`, `POST /account/identifiers`, `PUT /account/profile`
with a username, `POST /auth/webauthn/register/begin`, `POST /account/recoverycodes`,
`POST /account/mail/apppasswords`, `POST /account/deactivate`, `POST /account/delete`,
`POST /account/link/google`) and asserts 403 `authz.denied` for each. (4) `SessionClock`
answers `full` on any expiry of a session that satisfies every gate, the break-glass
session; test
`SessionServiceTests.OPS_BOOT_002_AnIdleBreakGlassSessionAsksForAFullSignInAsync`. The
fixture of `BreakGlassEndpointTests` gives the reserved account its administrative
membership; add `OPS_BOOT_002_AC2_TheSessionEndsAfterItsInactivityWindowAsync`. (5)
Declare `public interface IBreakGlass` in `Janus.Core` with
`ValueTask<Result<GeneratedBreakGlass>> GenerateAsync(AccessContext context, SessionId
session, CancellationToken cancellationToken)`, and a read of whether a credential stands
and since when (for example `StandingAsync(AccessContext, CancellationToken)`); move
`GeneratedBreakGlass` to `Janus.Core` as `public sealed record
GeneratedBreakGlass([property: NeverLogged] string Credential, Uri Address, DateTimeOffset
IssuedAt)`; `BreakGlassService` implements it and is registered as it; `PresentAsync` and
`SweepAsync` stay internal; the endpoints resolve `IBreakGlass`; remove the generate route
from `IdentityEndpointsTests.Outside`. Tests:
`IdentityEndpointsTests.LIB_API_005_AC3_EveryEndpointResolvesExactlyOneServiceOfTheContractAsync`;
`BreakGlassServiceTests.LIB_API_005_AC2_AnInProcessGenerationIsHeldToTheEndpointsChecksAsync`.
(6) `BreakGlassCode` draws from the injected generator, never the static
`RandomNumberGenerator.GetInt32`; sweep every project for a static draw of randomness or a
read of the clock (CONV-DESIGN-007). (7) Settled 302, option 1: `POST /auth/break-glass`
takes `{ "credential": "...", "reason": "..." }`; the reason is required, trimmed, 1 to
1024 characters (absent or blank is 400 `api.request.malformed` naming `reason`); the
session keeps it, and every audit record the session writes carries it. (8) Add `GET
/admin/break-glass` on the browser profile, session required, through
`IBreakGlass.StandingAsync` under `system:administer` and no step-up: 200 `{ "standing":
true, "issuedAt": "<instant>" }` where `IBreakGlassStore.StandingAsync` finds an issue, `{
"standing": false, "issuedAt": null }` otherwise, 403 `authz.denied` without the
permission; nothing of the credential or its hash. Tests:
`BreakGlassEndpointTests.OPS_BOOT_001_AC3_TheAbsenceIsReadByEverySystemAdministratorAsync`;
`BreakGlassEndpointTests.OPS_BOOT_001_AC3_OnlyASystemAdministratorReadsTheStandingAsync`;
a test carrying OPS-BOOT-002 proves every record of a break-glass session carries the
reason.

**121, 336 (the secret path).** The auditors split on the secret source; this entry
settles it. The library reads every secret it needs through the host's `ISecretSource`
(LIB-EXT-001, CONV-DESIGN-007 as written), asynchronously, in the startup hosted service,
before the server serves; no secret is an argument of `AddJanus`, which loses
`keyEncryptionKeys`, `fingerprintKeys`, `signOnSecret` and `maintenanceCredential`. The
members are `ReadKeyEncryptionKeysAsync`, `ReadFingerprintKeysAsync`,
`ReadMaintenanceCredentialAsync`, `ReadMailServerSecretAsync` (asked only where the mail
server adapter is used) and `ReadProviderCredentialAsync(string provider, ...)` (a social
provider's credential by the provider's name, 343); `ReadSignOnSecretAsync` is removed
(340). Every member, and `IUnitOfWork.BeginAsync` and `CommitAsync`, return
`ValueTask<Result<T>>` or `ValueTask<Result>`, a fault still thrown (D-162 item 29 allows no
exemption for a port); remove `NotOperationContracts`, so
`ResultContractTests.CONV_DESIGN_005_AC1_EveryContractMethodReturnsAnOutcome` covers every
public interface with no list. A secret the source cannot answer, or answers unusable,
fails startup with `model.startup.secretunavailable` (renamed from
`model.startup.kekunavailable` before any release; `ErrorCodes.StartupKeyUnavailable`
renamed to match), `details.key` naming it (`keyEncryptionKeys`, `fingerprintKeys`,
`maintenanceCredential`, `mailServerSecret`, `socialProvider.<provider>` with the
provider's name). A `Janus.Cli` command reads the same values from one JSON document on
standard input (307) and refuses with the same code, `details.key` `input` where the
document itself does not read.
The audit partition job runs `audit_ensure_partitions()` and
`audit_drop_expired_partitions()` under the maintenance credential. Tests carrying
CONV-DESIGN-007 and OPS-SEC-001 prove the server does not serve before the secrets are
read, and a source that cannot answer one fails startup naming it.

**316 (settled: option A).** Values wrapped directly under the key-encryption key that
belong to no subject were left for the rotation to find by a list of columns. One data key
of the deployment is held as a row of `subject_keys` under a reserved identifier no
subject is issued and erasure never touches. Every value the library encrypts that
belongs to no subject (invitations, reserved mailboxes, registration sessions, queued
messages, the sign-on proof, signing keys, provider attempts, client secrets) is encrypted,
or its own data key wrapped, under the deployment key instead of directly under the
key-encryption key. A value that belongs to a subject stays under that subject's key: a
TOTP secret is the account's credential secret, encrypted under the account's subject key
(PRIV-RIGHT-005a) and destroyed with it, as the authenticator store already holds it; one
staged in a registration session is under the deployment key until the account is
written. The rotation re-wraps subject-key rows only; remove the list of held
columns from `KeyRotationStore`; OPS-MIG-003a AC4 stands, with the append to the audit
trail OPS-SEC-003 AC5 needs. Every re-wrap writes a value back only where it still holds
the value read, not only the version read. Token protection keys stay derived from each
held version (159). Tests carrying OPS-SEC-003 prove a rotation leaves every such value
readable, touches no table but the subject-key and progress tables, and does not overwrite
a value rewritten at the same version between its read and its write.

**317.** (1) Add `model.rotation.notready` (`details.pending` where values remain) and use
it for the three refusals of `--sealed` of both `rotate-kek` and `rotate-fingerprint-key`;
`api.request.malformed` stays for arguments that cannot be read. (2) The retired-version
failure in `PersonalFieldCipher.Unwrap` carries `model.startup.secretunavailable` with
`details.key` `keyEncryptionKeys` and `details.version`, as a coded refusal, not a bare
message. (3) The retirement report is `{"version":N,"processed":M,"retired":[...],
"keepUntil":"<completed_at + backup.retention>"}`: a retired version leaves the
application's key document at once and stays in the envelope and the secrets manager until
every backup taken under it has expired, as a previous backup key does (D-103). The command
takes `backup.retention` from its default unless the operator names `--retention
<duration>`, and never prints a date earlier than the default gives. Tests:
`KeyRotationTests.OPS_SEC_003_AC4_ASealBeforeTheRotationCompletesIsRefusedAsync` asserts
`model.rotation.notready`;
`KeyRotationTests.OPS_SEC_003_TheRetirementReportSaysHowLongTheRetiredVersionIsKeptAsync`;
`PersonalFieldCipherTests.OPS_SEC_003_AC3_AKeyUnderARetiredVersionFailsWithANamedError`
asserts the code.

**318.** Retirement of a fingerprint key version deleted abuse counts that still counted.
(1) The expiry sweep removes, under every version, each ledger line its own check no longer
reads: a throttle counter whose standing under `abuse.throttle.decay` is zero; a send
counter holding no time inside the longest interval any current restriction declares; a
`sends` row whose `settles_at` has passed; a registration source, non-existence notice or
callback line outside the window its key names. (2) `FingerprintRotationStore.StandingAsync`
counts as `pending` every line under a previous version in `throttle_counters`,
`send_counters`, `send_key_counters`, `sends`, `registration_sources`,
`nonexistence_notices` and `callbacks`; `--sealed` is refused with `pending` while any
stands. (3) `ForgetAsync` deletes only `send_grants` lines under a previous version and the
released username holds. Test:
`FingerprintRotationTests.OPS_SEC_003_AC6_RetirementWaitsWhileAnAbuseCountUnderThePreviousVersionStillCountsAsync`.

**341.** The key-encryption key's cryptoperiod was measured from a log entry a person
writes. The daily job adds a look driven by the key's own record: the cryptoperiod ends
one year after the latest `ops.keyrotation.completed` whose `details.kind` is
`key-encryption-key`, or, where there is none, one year after bootstrap's
`identity.organization.created` record; from `maintenance.expiry.warninglead` before that
end until a later completion is recorded, each look raises `expiry-approaching` under the
scope `kek-cryptoperiod` with `details.version` (null in the bootstrap case),
`details.rotatedAt` and `details.dueAt`. No maintenance log entry affects it; the log-driven
look under `envelope-rotation` stays. The watch moves to `Janus.Hosting` if the audit
reader is not reachable from `Janus.Authentication`. Tests:
`EnvelopeRotationWatchTests.DR_009a_AC1_ALogEntryWithoutARotationLeavesTheCryptoperiodRaisedAsync`;
`EnvelopeRotationWatchTests.DR_009a_AC1_ACompletedRotationEndsTheWarningAsync`;
`EnvelopeRotationWatchTests.DR_009a_AC1_ADeploymentNeverRotatedCountsFromBootstrapAsync`;
`EnvelopeRotationWatchTests.DR_009a_AC1_AFingerprintKeyRotationDoesNotEndTheWarningAsync`.

**303 (and the audit's subject).** The trail read hid the principal and reason of
background work, and every writer recorded the account an action was taken on as the
effective identity, which AUTHZ-IMP-001 AC3 forbids. (1) `Janus.Core.AuditEntry` appends
`string? Principal` and `string? PrincipalReason` (null where a person acted), mapped in
`AuditTrailStore.OfSubjectAsync` and carried as `principal` and `principalReason` by
`GET /admin/audit`. (2) `audit_records` gains a nullable `subject` column naming the data
subject a record concerns. Every new record writes `effective_subject` equal to
`acting_subject` (the nil subject beside a system principal) and the account acted on as
`subject`; `auth.authentication.failed` names the attempted account as `subject`, both
identities being the nil subject. The PRIV-BREACH-002 index moves to `subject`, and the
trail read returns records naming the subject as `subject` or as the acting identity. No
existing row is rewritten. Tests:
`AuditStoreTests.IDN_PRIN_001_AC4_TheTrailNamesTheBackgroundPrincipalAndItsReasonAsync` (an
erasure by the `account-deletion` job reads back with principal `account-deletion` and
reason `IDN-LIFE-014`); a test carrying AUTHZ-IMP-001 proves an action from a break-glass
session on another account records `emergency` as acting and effective identity and the
account as `subject`.

**304, 334 (jobs).** (1) Every job's work passes its access context to its service
method, which refuses a principal that may not run the job's operation, as `DeletionSweep`
does. Test: `BackgroundWorkerTests.IDN_PRIN_001_AC3_EveryJobRefusesAPrincipalOfAnotherOperationAsync`.
(2) `BackgroundWorker.RunDueAsync` starts a due job's run and goes on to the next without
awaiting it, keeps at most one run of a job in flight in the process, judges each job's
lapse after its own run, and awaits the runs in flight when it stops. (3)
`Settings.BackupRestoreTestInterval` has default and ceiling `P90D`. Tests:
`BackgroundWorkerTests.INF_BG_001_ARunInProgressHoldsNoOtherJobsTurnAsync`;
`RestoreTestTests.DR_007_AC1_NoIntervalExceedsTheShortestQuarter` (`P91D` refused with
`config.value.aboveceiling`).

**323 (kept, with a fix).** A future `performedAt` and two licences under one identifier
are well formed and refused on meaning: each answers 422 `api.request.invalid` naming the
member (X5). A test carrying OPS-MAINT-001 proves both.

**Audit actions (the rows audit).** `identity.credential.labelled` is renamed
`auth.credential.labelled`, and the summary of `auth.credential.invalidationheld` says
what it records: the window ended with none of its notices delivered (AUTH-RECOV-007 AC3).
`auth.authentication.failed` and `auth.stepup.failed` are members of the closed vocabulary
(367). The code carries every action of `10`'s new table and no other.


#### D.9 The OIDC provider and social sign-in

**145, 279.** The provider rewrote a pushed `redirect_uri` that was not the registered
one and issued a code to the registered one; RFC 9126 section 2.1 validates the push as an
authorization request and OAuth 2.1 section 2.3.5 fails a mismatch, and API-REDIR-001's
replacement rule governs only a destination a browser carries. (1) Remove the rewrite from
`RegisteredDestination`. At `POST /oidc/par`, a `redirect_uri` that is present and not
ordinal-equal to the client's registered address is refused with `invalid_request` and no
description, answering 400 with no `request_uri`, and is logged as a refused destination
(rename `OidcLog.DestinationReplaced` for a refusal; it carries `{CorrelationId}`, 368). An
absent `redirect_uri` takes the registered address. (2) `RedirectValidation.Origin` and
the client registry's check refuse an address whose scheme is not `https`, except `http`
on a loopback IP literal (`127.0.0.1`, `[::1]`), with `model.startup.redirectclient`
naming the client, at startup and in `register-client`. (3) `RegistrationService.LandingAsync`
answers the origin of the stored client's registered address (scheme, host and port), not
the full address. (4) Settled 145 (c), option 1: the terms transaction stores the captured
client on the new account session, and the done step reads its origin from
`GET /auth/session` as `landing`, never from a request. The push refusal joins the host-run
probe of `ConformanceSuite.ProviderAsync`, under `auth.oidc.nonconformant`. Tests:
`OidcFlowTests.AUTH_OIDC_006_AC1_APushedRequestNamingAnUnregisteredDestinationIsRefusedAsync`
replaces `OidcFlowTests.API_REDIR_001_AC1_AnUnknownDestinationIsReplacedAndLoggedAsync`;
`ProviderConformanceTests.AUTH_OIDC_006_AC1_APushNamingAnotherDestinationIsRefusedAsync`
(trailing slash, query, path, host case, scheme, port and suffix, each 400
`invalid_request`, no `request_uri`, one log line) replaces
`AUTH_OIDC_006_AC1_OnlyTheExactRegisteredDestinationReceivesTheCodeAsync`;
`AUTH_OIDC_006_AC1_ACodeIsNotExchangedForAnotherDestinationAsync` stays;
`RedirectValidationTests.AUTH_OIDC_006_APlaintextReturnAddressStopsStartupAsync`;
`RegistrationServiceTests.API_REDIR_002_AC4_TheReturnIsTheStoredClientsAndNoOthersAsync`
asserts the origin; `OidcFlowTests.API_REDIR_001_AC2_ADestinationContainingTheRegisteredOneIsRefusedAsync`
restores a test for API-REDIR-001 AC2; a test carrying REG-SESS-008 proves the done step
reads `landing` from the session.

**160 (kept, with a fix).** `IOidc.ClaimsAsync` took no access context, so an in-process
caller read any account's claims. It becomes `ClaimsAsync(AccessContext context, string
scope, CancellationToken)`, answering the claims of `context.Effective` and `authz.denied`
where the context names no effective subject; `ClaimsAnswer` passes
`AccessContext.Of(new SubjectId(value))` for the subject the validated token names;
`KeysAsync` takes an `AccessContext` and admits any, the anonymous one included. Test:
`OidcServiceTests.LIB_API_005_ClaimsAnswerOnlyTheEffectiveSubjectAsync`.

**282 (kept, with fixes).** Restore the provider security-event endpoint's place among the
library's own callbacks. The Google route follows RFC 8935: a carried or repeated event is
answered 202 with no body; a Security Event Token that fails validation is answered 400
with a JSON body carrying `err`, the RFC 8935 section 2.4 code for the failure, and
`description`, which carries the refusal's `10` code, since the library writes no sentence
(LIB-API-003); the rate limit is answered 429 with `Retry-After`. The Apple route answers
200 with no body and refuses as 276 (A) says: 429 with `Retry-After` for the rate limit,
422 otherwise. Tests carrying IDN-LIFE-012a AC3 prove each route's answers.

**286 (kept, with a fix).** A withdrawn identity whose account's other way in was a
credential already held was unlinked, leaving no usable way in. In
`ProviderEvents.WithdrawnAsync`, the credential is the last where no other usable
credential (`HeldFactors.Of(...).Usable`, the password included) may begin a sign-in, not
where `HeldFactors.KeptWithout` finds none. Test:
`ProviderEventTests.IDN_LIFE_012a_AC2_AWithdrawnIdentityWhoseOtherWayInIsHeldSuspendsTheAccountAsync`.

**306 (the token prune).** The token prune kept every token row for the reach a redeemed
refresh token needs. A sweep method of the library's own on the token store removes a row
that is not a refresh token once it is past its expiry or no longer valid, and keeps the
longest-session reach for refresh tokens and authorizations. Test:
`OidcTokenStoreTests.AUTH_KEY_003_AnExpiredCodeGoesAtOnceAndARedeemedRefreshTokenStaysAsync`.

**340.** A client secret was chosen by a person, stored in the secrets manager, passed to
a command and read at a restart, which OPS-SEC-002 forbids; both ends of every registered
flow are the library's. (1) A migration replaces the hash columns of the client registry:
`oidc_clients` holds `secret` (the current secret, wrapped under the deployment data key of
316), `secret_issued_at`, `previous_secret` (wrapped) and `previous_secret_until`, with a
check keeping the `previous_*` columns null together; no hash or plaintext of a secret is
stored. (2) `ClientRegistry.RegisterAsync` takes no secret: a first registration draws 32
bytes from the injected `RandomNumberGenerator`, written base64url as `OpaqueToken.Draw`
writes them, and stores them wrapped with `secret_issued_at` now; registering a client the
registry holds changes its name, kind, destination and scopes and leaves its secret alone.
Remove the key document member `clientSecret`, its refusals and its 32-byte rule. (3)
Rotation, beside `SigningKeys` in `Janus.Authentication`: reading a client's current secret
first replaces it where `secret_issued_at` is `token.signing.rotation` or more in the past;
the current value moves to `previous_*` with `previous_secret_until` now plus
`oidc.accesstoken.lifetime` plus 5 minutes, and a new one is drawn, in one transaction whose
update is conditional on the `secret_issued_at` read, so of two processes rotating together
one wins and the other reads the winner's secret (X3). (4) The client half
(BFF-SESS-006) reads its application's current secret from the registry by its declared
client identifier at each code exchange, through step 3, and caches nothing; remove
`SignOnSecret`, the `signOnSecret` argument of `AddJanus`, its startup refusal and
`ISecretSource.ReadSignOnSecretAsync`. (5) `ClientSecrets.ValidateClientSecretAsync`
compares the presented value in fixed time with the current secret and, before
`previous_secret_until`, the replaced one, both compared whichever matches. (6) The
mail-server client is registered like any other, receives a generated secret and is never
asked for it. (7) `register-client` runs under `SystemOperation.Configuration`, whose
description becomes "Changing the deployment's configuration from the server: a protected
key, or the provider's client registry". Tests:
`ClientSecretLifecycleTests.OPS_SEC_002_AC1_AClientSecretRotatesAtTheCadenceWithoutRestartAsync`;
`ClientSecretLifecycleTests.OPS_SEC_002_AC2_TheReplacedSecretAuthenticatesThroughTheOverlapAndNotAfterAsync`;
`ClientSecretLifecycleTests.OPS_SEC_002_TwoProcessesRotatingTogetherLeaveOneSecretAsync`;
`OidcStoreTests.OPS_SEC_001_NoClientSecretIsHeldInTheClearAsync`;
`KeyRotationTests.OPS_SEC_003_AC1_AKeyRotationReWrapsTheClientSecretsAsync` (the client
secrets read after a rotation of the key-encryption key);
`RegisterClientTests.AUTH_OIDC_001_AC4_TheMailServerClientIsRegisteredWithNoSecretSuppliedAsync`;
`RegisterClientTests.OPS_SEC_002_ARegistrationThatChangesAClientKeepsItsSecretAsync`.
Remove `RegisterClientTests.AUTH_OIDC_001_ARegistrationWithoutAUsableSecretIsRefusedAsync`,
`ClientRegistryTests.OPS_SEC_001_ASecretTheServerWouldNotTakeIsRefusedAsync` and the tests
that register a replacement secret by hand; the overlap tests drive the overlap by the
clock.

**343, 349.** A pre-minted Apple client secret expires within six months and then needs a
person and a restart. `SocialProvider.Secret` is replaced by a credential the host's secret
source answers for the provider by name (`ISecretSource.ReadProviderCredentialAsync`,
336), of one of two forms: `ProviderCredential.Secret(ReadOnlyMemory<byte>)`, a static
secret the provider issued; or `ProviderCredential.Signed(string issuer, string keyId,
ReadOnlyMemory<byte> key)`, the identifier the provider knows the deployment's account by,
the identifier of the key it issued, and that P-256 private key in PKCS #8. For a signed
credential `ProviderSignIn.ExchangedAsync` mints `client_secret` at each exchange: header
`alg` `ES256` and `kid`; claims `iss` the issuer, `iat` now, `exp` now plus 5 minutes, `aud`
the issuer the provider's discovery document names, `sub` the first declared client
identifier; signed in the IEEE P1363 form; never stored, never logged. No code names a
provider. At startup a declaration present but malformed (declared twice, a factor that is
not a social provider, an address that is not absolute `https`, a `return` whose path does
not end as it must, no client or an empty one) is refused with
`model.startup.declarationinvalid`, `details.key` naming the member; a credential the
source cannot answer, or answers empty, or signed with a blank issuer or key identifier or
a key that is not a P-256 private key, is refused with `model.startup.secretunavailable`,
`details.key` `socialProvider.<provider>` (for example `socialProvider.apple`). Tests:
`ProviderSignInTests.OPS_SEC_002_ASignedClientSecretIsMintedForEachExchangeAsync` (the fake
provider's token endpoint receives a `client_secret` that verifies under the declared key's
public half, carries the five claims and an `exp` at most 5 minutes ahead; two exchanges a
year apart on the clock both succeed with no redeclaration);
`StartupValidationTests.IDN_LIFE_012a_ASocialProviderWithoutAUsableCredentialIsRefusedAsync`.

**394.** Every provider error carried OpenIddict's English sentence and documentation
address, which LIB-API-003 forbids and RFC 6749 section 5.2 makes optional. Add one server
handler in `Janus.Hosting`, registered in `OidcRegistration` for every apply-response event
the provider raises for an endpoint it serves (at least the token, pushed-authorization,
userinfo and authorization responses) and ordered before every OpenIddict ASP.NET Core
handler that writes a response or a header (the JSON writer, the `WWW-Authenticate` writer,
the redirect writers, `ProcessLocalErrorResponse`): where `context.Response.Error` is set,
it sets `ErrorDescription` and `ErrorUri` to null. `AuthorizationErrorAnswer` is unchanged.
Test: `LIB_API_003_AC1_NoProviderErrorCarriesADescriptionAsync` in `Janus.Hosting.Tests` (a
token request with a spent code, a pushed request with no `client_id`, a userinfo request
with an invalid token, an authorization error returned to the client's registered address:
each carries `error`, and none carries `error_description` or `error_uri` in its body, its
`WWW-Authenticate` header or its `Location`).

#### D.10 The pipeline profiles

**142 (kept, with a test fix).**
`SessionRequirementTests.BFF_ORDER_001_AnEndedSessionDoesNotRefuseAnEndpointThatNeedsNoneAsync`
asserts the step's own success, not only a status other than 401; a new case in which the
ended session's row has been swept asserts the one-time `auth.session.csrfinvalid` and
success on the retry.

**272.** The correlation reference authenticates an unsigned callback and the library holds
it only as a hash, yet it was excluded from `[NeverLogged]`. Add `[NeverLogged]` to the type
`SendReference`, to the member `SmsDeliveryReport.Reference`, and to the parameters
`reference` of `DeliveryReports.ReportAsync`, `presented` of `SendReferences.Of(string)`,
`presented` of `CallbackReferences.RecognisesAsync` and `reference` of
`CallbackReferences.Hashed`. Tests: `NeverLoggedTests.CONV_LOG_003_AC1_EveryCorrelationReferenceIsMarked`
in `Janus.Authentication.Tests`; a case in
`NeverLoggedValueAnalyzerTests.CONV_LOG_003_AC1_TheLibrarysOwnCarriersAreReportedAsync` in
which logging a `SendReference` is reported as JAN0002.

**276 (A) and (B).** `integration.callback.rejected` was 429 for every cause, and a claim
committed before the route lost an event on a crash or on a concurrent duplicate. (A)
`ApiStatus` maps this one code to 429, with `Retry-After`, where the error carries
`details.retryAt` (the rate limit refused it), and to 422 otherwise, for host callbacks,
the delivery report and the Apple provider route. (B) `callback_events` gains
`settled_at timestamptz NULL` (a migration; the serialized model and the maintenance
grants follow), and a new key `integration.callback.claimtimeout` (`PT5M`, floor `PT1M`, R)
replaces the fixed five minutes. For a host's signed callback: the claim is inserted with
`claimed_at` now and `settled_at` null and committed before the route, as now; when the
route answers 2xx, `settled_at` is set in a scope of its own under a token the request's
abandonment does not cancel; when it does not, or throws, the claim is deleted. A delivery
whose event holds a settled claim is answered 200 without reaching the route. One whose
event holds an unsettled claim younger than `integration.callback.claimtimeout` is answered
409 with the new code `integration.callback.inprogress`, logged at Information, not
recorded as a rejection and not counted toward `alerting.callback.threshold`. One whose
event holds an unsettled claim that old or older takes it over by one conditional update
(`claimed_at` now where `settled_at` is null and `claimed_at` is at or before now less the
timeout; one row changed is a takeover) and is carried (X3). Provider events insert their
claim with `settled_at` equal to `claimed_at` in the transaction their work runs in. Tests:
`HostCallbackTests.BFF_MACH_002_AC1_AnUnsignedOrMisSignedCallbackIsRejectedBeforeParsingAsync`
and `BFF_MACH_003_AC2_AForgedCallbackWithAGuessedReferenceIsRejectedAndLoggedAsync` assert
422 and no `Retry-After`; `INT_GEN_003_AFloodIsAnsweredBeforeAnyLookupAsync` asserts 429
with `Retry-After`; `DeliveryReportEndpointTests.INT_GEN_003_AC1_AForgedOrUnreadableReportIsRejectedAsync`
and `ProviderEventTests.IDN_LIFE_012a_AnEventNothingDeclaredOrReadableVerifiesIsRefusedAsync`
(Apple) assert 422;
`HostCallbackTests.BFF_MACH_002_AC3_ADeliveryWhoseRouteNeverFinishedIsCarriedAfterFiveMinutesAsync`
(at the default timeout);
`HostCallbackTests.BFF_MACH_002_AC3_ADeliveryMeetingOneInProgressIsNotAcknowledgedAsync`;
`CallbackStoreTests.BFF_MACH_002_AC3_AnUnsettledClaimIsTakenOverOnlyAfterFiveMinutesAsync`.

**389.** Keying a source on the whole IPv6 address gave one host up to 2^64 budgets inside
its own /64 (RFC 4291, 8981, 9099), and one site holds a /48 or /56 (RFC 6177, RIPE-690).
(1) `RequestOrigin` gains `Address(HttpRequest)`, the connection address normalised: an
IPv4-mapped IPv6 address is read as its IPv4 address. `Source(HttpRequest)` returns the
counting key from it: an IPv4 address as written; an IPv6 address whose first three bits
are not 000, its /64 with the low 64 bits zeroed, written `<prefix>/64`; any other IPv6
address, whole; no address, `unknown`. (2) Every call that records where a session or a
credential came from uses `Address`; the session keeps the whole address. Every call that
counts uses `Source`: the throttle, the repeated-attempt count, the restriction key
`source` (`SendContext.Source`), the callback limit and stage 4, in
`AuthenticationEndpoints`, `RecoveryEndpoints`, `CredentialEndpoints`,
`RegistrationEndpoints`, `AppPasswordEndpoints`, `ProviderSignIn`,
`ProviderSignInEndpoints`, `ProviderEventIntake`, `DeliveryReportEndpoints` and
`CallbackIntake`; `CallbackIntake`'s source ranges keep matching the whole address. (3)
Stage 4: `SourceAdmissions` also counts each IPv6 source's enclosing /48 under the new key
`abuse.source.sitelimit` (3000 per `PT1M`, sliding, R). `SourceRateLimiting` checks the
/48 hold, then the source hold, both from memory; reads both limits only where neither is
held; admits only where both counts are under their limits; counts the request in both. A
request refused at the /48 creates no /64 entry. (4) A request refused because its source
or its /48 is already held writes no line of its own; the Warning written when the hold
begins stays. (5) The remarks on `PipelineProfiles.UseBrowserProfile` and `SourceAdmissions`
state the source, the /48 count, the per-instance share of both keys and the trusted-proxy
requirement. (6) A changelog line under `Unreleased` (Security); no migration, since ledger
rows keyed on whole addresses decay out. Tests in `SourceRateLimitingTests`:
`BFF_ORDER_001_AC3_AddressesInOneIpv6SubnetAreOneSourceAsync`;
`BFF_ORDER_001_AC3_AnotherSubnetOfTheSiteIsAdmittedUntilTheSiteLimitAsync`;
`BFF_ORDER_001_AC3_AnIpv4MappedAddressIsItsIpv4SourceAsync`;
`BFF_ORDER_001_AC3_AHeldSourceWritesNoLineForEachRefusalAsync`;
`AUTH_SESS_013_TheSessionRecordsTheWholeAddressAsync`; and in the throttle tests
`AUTH_ABUSE_001_TwoAddressesOfOneIpv6SubnetShareTheSourceDelayAsync`.

**393 (settled: option A).** A fault is kept in the log by its full type name and its stack
frames, and by the type and frames of each inner fault, never by a message: built from
`GetType().FullName` and the frames of `StackTrace` while walking `InnerException`, never
from `Message` or `ToString()`. One fault log is shared by the pipeline (`ErrorTranslation`
through `Refusal`), `BackgroundWorker` and `RestoreTest`. The placement stands: inside
concealment, outside every other stage, no answer replaced once begun, no answer to a
caller that has gone. A test carrying BFF-ERR-002 AC2 proves a fault's entry carries the
frames of it and its inner fault and not their messages.

#### D.11 Conformance, contract and release gates

**135 (kept, with a fix).** `DatabaseRoleTests` compares the maintenance role's rights with
the serialized model's listing over `TRUNCATE`, `REFERENCES` and `TRIGGER` as well, over
views, materialised views and sequences (relkinds `v`, `m`, `S`) as well as tables, and in
every schema, not only `identity`.

**167.** The name scan admitted the product name wherever a dot followed it, so a dotted
header or constant passed. Build the set of permitted second segments from the project
folders under `src`, `tests` and `tools` (`Core`, `Hosting`, `Storage`, `Authentication`,
`Authorization`, `Identity`, `Privacy`, `Cli`, `Conformance`, `Analyzers`,
`UnicodeTables`) and `slnx`; permit the name only as the head of a dotted name whose second
segment is one of them, and as `AddJanus`; apply the same, ignoring case, to the lock-file
package identifiers. Test: `ProductNameTests.CONV_NAME_001_AC2_ADottedNameThatIsNoProjectIsFound`
(a dotted header and a dotted string constant are reported; a namespace declaration is
not).

**271 (kept, with a fix).** The test fixture declaring a resource type `invoice` with the
first host's three purposes under another name is renamed to a neutral type (for example
`statement`, the data category it already carries).

**350 (kept; a report line).** AUTH-FACT-002a AC5 (the disclosure shown before a provider
is linked to an account holding a second step) is named in no phase report, and the exit
gate needs every criterion tested or named with how it is verified (CONV-TEST-007). The
corrections run's report names it among the criteria a test cannot decide, verified in
Milestone 2 step 10 by FE-ACCT-001.

**351 (kept, with a fix).** `Microsoft.AspNetCore.Authentication.OpenIdConnect` leaves
`Directory.Packages.props` in the change that rewords CONV-DESIGN-008's row; no project
references it.

**355.** A derived scenario of the host's truth table exercised only the type's first
derivation. In `TruthTable`, a `DerivedGrant`, `DerivedGrantOnContainer` or
`DenyOverDerivedGrant` case runs once for each derivation declared at the level the
scenario uses, each in an organization of its own; `authz.truthtable.disagreement` for a
derived case carries `details.derivation` naming the relationship. No public type changes.
Test: `ConformanceSuiteTests.LIB_TEST_001_AC2_AWrongDerivedRowIsReportedForEachDerivationAsync`.

**359, 382.** The HTTP endpoints of LIB-API-001 were held by no contract test, key types and
constraints and code statuses were not held, and the release gate judged none of them. (1)
`configuration-keys.txt` holds one line per key (name, type, scope `R` or `P`, and each
constraint the catalogue holds: floor, ceiling, allowed values) and one line per family
prefix with its scope; `SettingsCatalogueTests.LIB_API_001_AC2_TheKeysAreTheContract`
compares it with `Settings.All` and `Settings.Families`, replacing
`LIB_API_001_AC2_TheKeyNamesAreTheContract` and `LIB_API_001_AC2_TheFamiliesAreTheContract`.
(2) A committed `error-statuses.txt` in `Janus.Hosting.Tests` holds one line per
`ErrorCodes` member, its code and the status `ApiStatus` maps it to;
`ErrorCodesTests.LIB_API_001_AC2_TheStatusesAreTheContract` holds it. (3) A committed
`endpoints.txt` in `Janus.Hosting.Tests`, generated from the endpoint data source of a host
that mounts every library endpoint, holds for each endpoint its method and route pattern,
then indented lines naming each request and response body member with its JSON name and
type, each status the endpoint answers and the codes each status carries. Every endpoint
carries the metadata this is generated from (`Accepts` and `Produces`, and the statuses and
codes it answers); add it where it is missing. The browser profile's stage order, one stage
per line, sits in the same file under a `pipeline` heading, derived from the order
`BrowserProfileTests.BFF_OWN_003_AC2` asserts.
`EndpointContractTests.LIB_API_001_AC2_TheEndpointsAreTheContract` regenerates it and
compares. (4) `release.sh` lists the three files and reads them in its contract step, each
endpoint's lines named for their endpoint as schema lines are named for their relation.
(5) Scratch-repository scenarios: a key's type changed, a key's ceiling lowered, a family
removed, a code's status changed, an endpoint path changed, a response member removed;
each needs a major version at release and a breaking marker on its commit.

**377.** The fingerprint key's absence was checked after the command flows only; the
library's own ledgers, which compute fingerprints on every live write, were never scanned.
Move `WrittenForms` and `HoldingAsync` into a helper in `Janus.Storage.Tests` used by both
tests. Add `FingerprintKeyTests.INF_HOST_003_AC4_NoStoreWritesTheFingerprintKeyAsync` in
`Janus.Storage.Tests` (its own database; fingerprint keys of known bytes in two versions;
a write through every store that calls `Fingerprint.Compute`: an identifier of each kind,
a mailbox, a provider link, a throttle failure, a non-existence notice, a registration
source, a send under a restriction; the scan over every column of every base table in
`identity`, both versions, all five forms, finds nothing, and finds a value the writes did
store) and `FingerprintKeyTests.INF_HOST_003_AC4_EveryStoreThatComputesAFingerprintIsDriven`
(the files of `Janus.Storage` calling `Fingerprint.Compute(` are exactly those the first test
drives, `FingerprintRotationStore` counted as driven by the command test).
`KeyDocumentTests` stays.

**378 (settled: option 1).** (1) `destructive-operations.sh` fails the run where
`DESTRUCTIVE_DDL_GATE` is unset, empty, or anything but `enabled` or `disabled`, after
printing its report, with a message naming the variable and its two values; create the
repository variable with the value `disabled`. (2) The script takes a second base: a file
listing the migration identifiers the target database has applied, as read from
`identity.__migrations_history`; in that mode the added set is every migration the head
holds that the list lacks. Pull requests and pushes keep the range mode; the deploy job of
Milestone 2 step 1 calls the list mode, as the script's header and the report's hand-over
list say. (3) Settled: every match of `08` section 1b and every constraint form of
OPS-DEP-001 is reported; the enabled gate stops a deploy only on data loss or a constraint
that can fail against rows: `DROP TABLE`, `DROP COLUMN`, `DROP SCHEMA`, any `ALTER ... TYPE`,
`TRUNCATE`, `DELETE FROM`, and an added constraint, `SET NOT NULL`, a unique index or a
column added `NOT NULL` without a default on a table the same migrations did not create or
have filled. Every other match is listed as reported and not destructive. (4)
Scratch-repository scenarios: the variable unset fails; empty fails; `Enabled` fails;
`disabled` reports; an applied list lacking a migration older than the range reports it; a
`TRUNCATE` stops the enabled gate; a `DROP INDEX` on a table the migrations created does
not.

**385.** The gate also refuses with `privacy.consent.required`,
`privacy.consent.superseded` and `privacy.consent.writtenrequired`, which the denial scan
did not read. `LibraryStructureTests` extends `Refusals` to `["auth.", "authz.",
"privacy.consent."]`, and `CONV_ERR_001_AC1_NoDenialIsSignalledByAnException` asserts the
list it reads contains the three consent codes.

**386 (kept, with a fix).** Each entry of `NotServiceContracts` carries a comment naming the
item that makes the host implement it; the seven environment seams (`IClockReference`,
`ICertificateRenewal`, `IDnsResolver`, `ILocationSource`, `IMailServer`,
`IRestoreTestInstance`, `IErasureLedger`) are LIB-HOST-001 declarations, optional, so that
its "the following and no more" stays true.

**405 (kept, with a fix).**
`IntegrationBoundaryTests.LIB_EXT_001_AC3_NoProviderNameAppearsInTheCoreNamespace` scans
`Janus.Core`, `Janus.Identity`, `Janus.Authentication`, `Janus.Authorization` and
`Janus.Privacy`, and matches `AWS` as a case-sensitive whole word.

**416 (kept, with a fix).** `VolumeTests` asserts an `Index Cond:` line under every scan
node on `grants` in the reverse plan, whatever index it reads.

**The Tier 1 corrections.** (1) CONV-DESIGN-004 AC2: in `LibraryStructureTests`, remove the
file-level `Foreign` exclusion. Add `ForeignMembers`, the type and method pairs whose
signature a package's interface fixes: `OidcTokenStore` and `OidcAuthorizationStore`, each
with `FindBySubjectAsync` and `RevokeBySubjectAsync`. For each match, find the enclosing
method; the match is exempt only where the file's type and that method are a pair of the
list, and every other match is reported. The scan reads every project, `Janus.Core`,
`Janus.Hosting` and `Janus.Cli` included. Typed values bind at the HTTP edge: an endpoint
handler takes a typed identifier or value, not a bare `Guid` or `string`, bound through
`IParsable<T>` on the type. `MailboxPush.Address` and `HostedMailbox.Address` take
`EmailAddress`. Test: `CONV_DESIGN_004_AC2_OnlyAMemberAPackagesInterfaceFixesIsExempt`
asserts, by reflection over `Janus.Storage`, that every pair is the target of an interface
map entry whose interface is declared in an assembly named `OpenIddict.*`. (2)
`LibraryStructureTests.CONV_LAYOUT_002_AC1_InternalsAreVisibleOnlyWhereThePermittedGrantsSay`
reads every project under `tests` too: `Janus.Authentication.Tests`,
`Janus.Authorization.Tests` and `Janus.Privacy.Tests` may grant `Janus.Hosting.Tests` and
nothing else; every other test project grants nothing. (3) The destructive gate's unset
variable fails the run (378).

**D-165, what it left.** Chapter text only; nothing to build. `09` section 10 has its
`POST /callbacks/providers/{provider}` row back (282), and INT-GEN-003 no longer calls the
host callback the only callback endpoint the library defines itself; it states that an
unsigned callback never by itself advances an authoritative state, and that a verified,
signed provider security event acts as IDN-LIFE-012a states. `13` section 4's trigger row
for callback signing is R-A12's, not R-A10's, whose entry D-165 folded away, and reads "the
SMS gateway adds callback signing or an HTTPS callback". CONV-LOG-003 AC2 and R-A13 use
neutral host examples in place of an order (271). `05` section 6 shows the developer row
as an example of a row a host declares, not a shipped default (270), and `00` section 8's
Controller / Processor entry names the four rows true of the library.

**Chapter clarifications with nothing to build now.** Mail provisioning follows the
account's state, not events (IDN-LIFE-015, INT-MAIL-007), as entry 217 built it. The
formats and quality of a photo are the contract of the declared image codec (IDN-ATTR-004).
An organization's deletion request is read live by the effective grants view and bumps no
counter (AUTHZ-CACHE-001); no bump is to be added. `00` section 3.2 says an evaluated
derivation cannot drift and a materialised one is checked daily (AUTHZ-DERIVE-005). X4 is
stated once, in API-CONV-002, and the other chapters point to it. For the frontends
Milestone 2 builds, `18` now states: a frontend that finds no per-application session
(`GET /auth/session` anonymous, or a 401 without `details`) navigates to
`GET /auth/signon?returnTo=<route>`; the frontend side of navigation errors (`error` and
`retryAt` on a provider or sign-on return) and of the recovery-code export route; and
BFF-SESS-002 lists every library cookie (session, preauth, csrf, device, browser).


### E. The implementer's open items and the phase 10 defects

**The open items of the implementer's last message.**

1. Pushing, pull requests and merges are granted; the repository settings allow them.
   Rewording `aa8a7dc` is granted: the branch is unpushed and the rewrite touches only
   local history. The reworded message passes the commit-message gate, so a pull request
   from the branch passes `Commit message format`.
2. "PRIV-RESTRICT-005c AC6 tension": no item of that name exists and no report records the
   tension. The implementer states the question precisely under **Open questions** in the
   corrections run's report and changes no code for it.
3. The list of external connections at the end of the message concerns the working
   environment, not the library. Nothing to do.
4. CONV-DESIGN-007 is built now; it is a phase 0 convention still unmet. Each library
   project exposes exactly one `internal static` registration method on
   `IServiceCollection`, named for its area as `AddIdentityArea` is, holding the
   registrations of the types that project defines; `AddJanus` calls each of them and
   keeps only the registrations of `Janus.Hosting`'s own types. No lifetime changes, and
   `IEvents` stays registered so that nothing pre-empts it (320). A test carrying
   CONV-DESIGN-007 proves each library project exposes exactly one such method and
   `AddJanus` calls every one.
5. A Security Event Token without `jti` is refused as unreadable: RFC 8417 section 2.2
   makes the claim REQUIRED, and the claim is what the callback claim is keyed on.
   `ProviderEventIntake` refuses it before any claim or lookup: on the Google route 400
   with `err` `invalid_request` (RFC 8935 section 2.4) and `description`
   `integration.callback.rejected`; on the Apple route 422 `integration.callback.rejected`
   (276 (A)). Nothing is claimed and nothing changes. A test carrying IDN-LIFE-012a
   proves both routes refuse a token without `jti` and change nothing.
6. `BackgroundJobsTests.IDN_PRIN_003_AC4` is fixed in the test: each case owns its job
   state (its own clock and its own job rows), so no case reads another's run, and the
   class passes whole, alone and in any order.
7. The machine profile has no stage 4, and that stays. `17` BFF-MACH-001 states each
   machine endpoint's own limit: a callback's `integration.callback.ratelimit` (429
   `integration.callback.rejected` with `retryAt`); the provider's token and
   pushed-authorization endpoints, client authentication with secrets the library
   generates (340); the break-glass endpoint, its global limit and source delay; and a
   volumetric flood is the reverse proxy's to absorb. No code changes.
8. The throttled refusal has one builder. `ThrottleService.Refusal` moves into
   `Janus.Core` as the one builder of `auth.throttled` carrying `details.retryAt` (the
   instant, ISO 8601 UTC), and `ThrottleService`, `ExportOperations`, `ExportService`,
   `SourceRateLimiting` and every other site that refuses with the code use it; none
   builds the error itself. A structure test proves `ErrorCodes.Throttled` is read only by
   the builder and by `ApiStatus`.
9. OPS-MIG-002 AC1: a test carrying OPS-MIG-002 AC1 proves a migration run that fails
   exits with a non-zero code and applies nothing after the failing migration.
10. The 67 generated designer files of the migrations and the model snapshot are marked
    generated code in `.editorconfig`, so the analysers treat them as generated code and
    their `#pragma warning disable 612, 618` lines need no justification under
    CONV-SETUP-004 AC3. The files are not edited.
11. `09`'s step-up example writes the factor `recoveryCodes`, as `10`'s catalogue does.
    Chapter only.

**Section 2 of the phase 10 report.** What the phase left undone:

- CONV-DESIGN-007: built in the corrections run (item 4 above).
- AUTH-FACT-012 AC2, the multi-label half: built with the Public Suffix List (R3, section
  D.2).
- IDN-ACCT-004 AC3, the organization-name half: `organizations.canonical_name` is held and
  made NOT NULL now (413, section D.6).

The defects, each with its answer:

- `organizations.canonical_name` is nullable: 413 (section D.6).
- `RegistrationService` writes empty document versions: 415 (section D.4).
  `PrivacyRequestService` accepts a blank detail: 414 and X4 (section D.7).
- `groups.name` and `authenticators.label` keep the default collation: 417 (section D.2).
- The collation change is an `ALTER ... TYPE` that the destructive-operations gate
  reports: the report is right. With `DESTRUCTIVE_DDL_GATE` set to `disabled` (378) the
  deploy is not held. No change.
- `ProviderEventIntake` reads a token without `jti`: item 5 above.
- `BackgroundJobsTests.IDN_PRIN_003_AC4` depends on its class: item 6 above.
- `GET /admin/access` answers 400 for an unregistered record before it asks `grant:read`:
  settled with 265 (section D.1). An unregistered record of a declared type is refused as
  the gate refuses, 403 `authz.denied`, recorded and counted.
- `IResources` and `ICallbackReferences` take no access context: kept (352). They are seams
  that join the host's transaction, not operations, so LIB-API-005's access context does
  not apply to them; the chapters now say so. `IResources`' refusals follow X5 (section
  D.1).
- Unrecorded under CONV-LOG-005: an unknown or expired sign-in link token, pressed, is
  recorded behind the source delay (402, section D.2). A provider's own error or a cancel
  presents nothing and is not failed authentication; nothing is recorded (CONV-LOG-005
  Values).
- `OidcLog.DestinationReplaced` becomes the log of a refused destination and carries the
  correlation identifier (145, 279, 368; section D.9). `ScreeningLog`,
  `CallbackLog.Unreadable` and `AlertLog` are not refusals and stand as entry 368 left them.
- An anonymous `GET /auth/session` writes one Information line: stands (368).
- `aa8a7dc` fails the commit-message gate: rewording granted (item 1 above).
- `8665b02` and `be945f6` change the gate without the truth-table tests: stands. The branch
  is judged as a range (380).
- The destructive-operations deploy workflow does not exist yet: it is built in Milestone 2
  step 1, where the deploy job calls the gate's list mode (378). Nothing now.
- The release script's running time (about 130 seconds over the branch, 450 over the
  history): no change.
- Token and pushed-authorization errors carry `error_description`: 394 (section D.9).
- A client rotating addresses inside its IPv6 prefix is a new source each time: 389
  (section D.10).
- The machine profile has no stage 4: item 7 above.
- A fault is logged by its type without frames: 393 (section D.10).
- `authz.resource.notfound` and `api.request.malformed` have no rows in `10`: both have
  rows now, with the meanings 392, 394, X4 and X5 give them (section F).
- INF-HOST-003 AC4 does not scan after live flows: 377 (section D.11).
- Registration does not use `ThrottleService`: 419 (section D.2).
- `InvitationAcknowledgement` writes an enrolment `outcome` of its own: 246 (section D.6).
- `09` writes `recovery-code` in its step-up example: item 11 above.
- `ExportOperations` and `ExportService` build the throttled refusal themselves: item 8
  above.
- `AccessGate.CheckAsync` does not consult the declarations for the permission asked: 396
  (section D.1).
- LIB-API-005 AC1 is held structurally: stands (408).
- OPS-MIG-002 AC1 is decided by no test: item 9 above.
- `SettingsCatalogueTests.Scope_TheCatalogue_ProtectsTheKeysSectionFourMarks` overlaps the
  new OPS-CFG-001 test: removed with 404 (section D.8).
- Names that earlier reports cite and phase 10 renamed or removed: the reports are history
  and are not edited. No change.
- The suppressions the report lists stand as reported; the migration files' pragmas are
  answered by item 10 above.

### F. Chapter 10: new, renamed and retired rows

- **Codes new.** `identity.domain.notfound` (404); `identity.account.notfound` (404);
  `identity.account.stateconflict` (409, `details.state`, `details.suspendedBy`);
  `identity.membership.notfound` (404); `identity.photo.notfound` (404);
  `identity.mailbox.notfound` (404); `identity.mailbox.taken` (409);
  `identity.invitation.addressrequired` (422, `details.member`);
  `identity.invitation.mailboxheld` (409); `identity.invitation.notfound` (404);
  `identity.takedown.notfound` (404); `identity.organization.notfound` (404);
  `identity.identifier.unverified` (409); `authz.role.notfound` (404);
  `auth.restriction.notfound` (404); `authz.grant.unresolved` (422, `details.member`);
  `integration.callback.inprogress` (409); `model.type.reserved`;
  `model.rotation.notready` (`details.pending`); `model.purpose.hostingconsent`;
  `model.startup.declarationinvalid` (a declaration present but malformed: a template
  naming a place with no width, a landing origin no browser client registered, a social
  provider declared wrongly, an encrypted field whose category no purpose names);
  `api.request.invalid` (422, `details.member`).
  With them, every owed row the rows audit found right (154 of 164) and the rows of D-162
  section E.
- **Codes renamed before any release.** `model.startup.kekunavailable` becomes
  `model.startup.secretunavailable`, `details.key` naming the secret: `keyEncryptionKeys`,
  `fingerprintKeys`, `maintenanceCredential`, `mailServerSecret`,
  `socialProvider.<provider>`, and `input` for the command line's document
  (`ErrorCodes.StartupKeyUnavailable` renamed with it). `auth.restriction.reasonrequired`
  becomes `config.change.reasonrequired`, for every runtime change without a reason, the
  restriction set and its tightening included (`ErrorCodes.RestrictionReasonRequired`
  renamed with it).
- **Codes retired.** `config.change.stepuprequired` and `auth.device.verificationrequired`
  (their `ErrorCodes` members, `ConfigurationChangeStepUpRequired` and
  `DeviceVerificationRequired`, are removed); `identity.account.restricted` is struck, as
  the table strikes retired rows.
- **Statuses corrected.** `identity.registration.incomplete` 409;
  `identity.identifier.invalid` 422, as `identity.username.invalid`;
  `integration.callback.rejected` 429 only with `details.retryAt` (the rate limit refused
  it), 422 otherwise.
- **Meanings widened.** `authz.denied` (X5; an identifier naming no row under a row-scoped
  permission; an unregistered record at `GET /admin/access`); `authz.resource.notfound`
  (392, 410); `api.request.malformed` (X4, X5, 308, 394); `config.value.notallowed`
  (`details.requires`, X6); `model.startup.declarationmissing` (X6, R2, 265, 406);
  `auth.code.expired` and `auth.code.invalid` (sign-in codes and the attempt cap, 115);
  `identity.invitation.identifiermismatch` (245).
- **Keys new.** `code.signin.lifetime` (10 minutes, ceiling 30 minutes, R);
  `code.signin.attempts` (5, ceiling 10, R); `integration.mailserver.endpoint` (string, P,
  required only where the mail server adapter is used); `integration.callback.claimtimeout`
  (`PT5M`, floor `PT1M`, R); `abuse.source.sitelimit` (3000 per `PT1M`, sliding, R).
- **Keys changed.** `outbox.poll.interval` takes the ceiling `PT1M`;
  `backup.restoretest.interval` takes the default and ceiling `P90D`; the policy object
  gains `photos` (boolean; system default `false`; bootstrap writes `false`; to `true` is
  loosening; refused at the edit without a declared image codec), and
  `photo.enabled.<organization>` goes with it.
- **Keys retired.** `audit.enabled`, `token.signature.verification`,
  `stepup.enforcement.<organization>`.
- **Protected list.** `webauthn.origins`, `webauthn.algorithms`, `hosting.location`,
  `hosting.crossborderbasis`, `integration.mail.endpoint`, `integration.sms.endpoint`,
  `redirect.defaultclient` and `integration.mailserver.endpoint` join section 4.8 and
  OPS-CFG-004's one list (404).
- **Step-up actions new.** `organization:delete`, `membership:end`,
  `account:restrictionlift`, `account:deletioncancel`, `account:sessionsrevoke`,
  `session:revokeall`, `privacyrequest:fulfil` (X8).
- **Alert condition new.** `breakglass-generated` (High; to the owner regardless of
  `alerting.owner.enabled`).
- **Restriction shape.** A restriction gains `channel` (`sms` · `email` · `any`, default
  `any`); the shipped restrictions carry their channels (342).
- **Vocabularies collected.** Audit actions (with `auth.authentication.failed` and
  `auth.stepup.failed`, and `auth.credential.labelled` renamed from
  `identity.credential.labelled`); message kinds (with `verification-link`,
  `credential-suspended`, `oob-deletion-notice` and `sign-in-code`); message places (with
  `link`; `token` retired, every link-bearing kind carrying `link`); register findings
  (`organizational` spelled so); system operations; system principals (with
  `derivation-driftcheck`, section D.1); conformance checks and truth-table scenarios;
  explanation outcome; device kinds; sign-in status; key rotation kinds; hosting location
  values; link kinds (R2: on the authentication application `sign-in`, `registration`,
  `recovery`, `enrolment`, `invitation`; on the account application `identifier`,
  `identifier-confirm`, `undo`, `deletion-cancel`, `reactivation`, `loss-report`); mailbox
  states (`disabled` · `enabled` · `removed`); loosening direction (`increase` ·
  `decrease` · `any-change`, 181); former mailbox (`transfer` · `replace`, 221);
  restriction channels; and the enumerations spelled in other chapters (licence kinds,
  maintenance tasks, phone signal, send kinds, preference kinds, recipient
  characterisation, consent change, membership change, registration stream events).

### G. The ledger

The ledger is closed and takes no new entries. Each entry below gains one line under its
heading, in the form the ledger already uses, in the change that applies it. Kept entries
take nothing, and the lines already present stand.

**Superseded by D-166.** 110, 114, 115, 116, 118, 119, 120, 121, 122, 123, 129, 130, 133,
136, 141, 143, 144, 145, 146, 147, 148, 150, 152, 155, 167, 169, 170, 171, 180, 181, 183,
184, 187, 188, 189, 193, 200, 209, 215, 221, 223, 225, 227, 228, 229, 231, 233, 235, 242,
245, 246, 247, 248, 250, 251, 252, 254, 255, 257, 258, 259, 260, 261, 263, 264, 265, 272,
276, 279, 291, 295, 297, 302, 303, 305, 306, 313, 316, 317, 318, 319, 320, 322, 326, 328,
329, 331, 332, 334, 335, 339, 340, 341, 342, 343, 355, 362, 363, 377, 378, 382, 385, 389,
393, 394, 396, 402, 404, 406, 407, 413, 417, 419, 421, 422 (115 entries: 101 reversed in
whole or in part, 14 settled).

**Revised by entry n.** 124 by 407; 144 by 315; 175 by 194 and 205; 190 by 411; 280 by
356; 283 by 343 and 349; 284 by 318; 290 by 320; 315 by 319; 320 by 366; 367 by 400
(beside its line for 402).

The lines already present stand: 139 and 151 (D-163 and D-165), 262 (D-164), 186 (411),
328 (399), 367 (402), 400 (420), 402 (422).

**Propagated to:** `00` sections 4, 7.3 and 8 · `01` IDN-ACCT-004, 005, 007,
IDN-ATTR-001, 002, 004, IDN-AUD-001, IDN-LIFE-002a, 003, 003a, 003b, 008, 009a, 009b,
012, 012a, 013, IDN-MEM-001, 002, IDN-ORG-003, 005, 006, IDN-PRIN-001, 003 · `02`
AUTH-ABUSE-001 to 005, AUTH-FACT-001, 002, 002a, 002b, 003, 004, 008, 010, 012, 015, 016,
017, AUTH-KEY-001 to 003, AUTH-OIDC-001, 002, 006, AUTH-PASS-001, 004, AUTH-PRIN-002,
AUTH-RECOV-002, 007, AUTH-SESS-005b, 009 to 013, AUTH-STEP-002, 004, 007 · `03`
AUTHZ-CONCEAL-001, 002, 004, AUTHZ-DERIVE-001, 005, 007, AUTHZ-GATE-001, 002, 004, 005,
006, AUTHZ-GRANT-003, 004, AUTHZ-GROUP-001, AUTHZ-IMP-001, AUTHZ-INHERIT-002,
AUTHZ-MODEL-002 to 005, AUTHZ-PRIN-003, AUTHZ-SCOPE-001 · `04` PRIV-BASIS-001, 003,
PRIV-BREACH-002, PRIV-CONS-001, 005 to 008, 010, PRIV-MINOR-001, PRIV-PRIN-001,
PRIV-RET-001 to 003, 005, PRIV-RIGHT-001, 001a, 002 to 005, 005a, 005c, PRIV-ROPA-001,
002, PRIV-SENS-001, 002, 002a · `05` INT-GEN-001 to 004, 006, INT-HOST-001, 002,
INT-MAIL-001, 004, 006, 007, 008, 010, 011, INT-PWD-002, 003, INT-SMS-001, 003, 004, 005,
005a, 006, section 6 · `06` OPS-ALERT-001 to 003, 004a, 005 to 007, OPS-BOOT-001, 002,
004, OPS-CFG-002 to 008, OPS-DATA-002, 003, OPS-DB-001 to 003, OPS-DEP-001, 002,
OPS-ENV-001, OPS-MAINT-001, OPS-MIG-002, 003a, 005, OPS-OBS-001 to 003, OPS-SEC-001 to
003 · `07` LIB-API-001, 003, 005, LIB-EXT-001, LIB-HOST-001 to 004, LIB-TEST-001, 002 ·
`08` CONV-CODE-006, 007, CONV-DESIGN-002 to 008, CONV-ERR-001, 003, CONV-LAYOUT-001, 002,
CONV-LOG-002, 003, 005, CONV-NAME-001, 003, CONV-SETUP-004, CONV-TEST-001, 002, 004,
CONV-VCS-003 to 005 · `09` scope, API-CONV-001, 002, 003, 005, API-LAND-001,
API-REDIR-001, 002, and the endpoint rows of sections 3 to 10, section 10's
`POST /callbacks/providers/{provider}` restored · `10` sections 1.1 to 1.6, 2, 3, 4, 5,
5a, 5b, 6 and REF-001 · `11` sections 2.2, 3.2, 3.3, 5, 6, 8.1, 9, 10 · `12` DR-006a,
DR-007 to 010, DR-009a, DR-016, section 5 · `13` R-A11, R-A13, R-A18, R-A21, R-M22,
R-M23, R-M26, R-M27 to R-M29 (new), R-O02 to R-O04, section 4 · `14` section 3 · `15`
sections 1, 3.2, 3.3, 3.5, 3.8, 4, 5, 6.2, 6.5 · `16` section 3 steps 3 and 4 · `17`
section 4, BFF-ABUSE-001, BFF-CSRF-001, 002, 005, 005a, 005b, 007, BFF-ERR-001 to 003,
BFF-LOG-001, 002, BFF-MACH-001 to 003, BFF-ORDER-001, BFF-OWN-001, 003, BFF-SESS-006,
BFF-STEP-001 · `18` FE-ACCT-001, FE-API-003 to 005, FE-BG-001, FE-BG-002 (new),
FE-REG-005, FE-SEC-001, FE-VER-001 · `19` INF-BG-001, 002, INF-DB-004, INF-HOST-001, 003,
INF-TLS-003, 004 · `20` REG-ACCT-001, REG-DOM-001, REG-IDENT-002, 004, 006 to 010,
REG-INV-001, 002, REG-MAIL-001 to 003, REG-PM-001, REG-PROF-001, REG-SESS-001 to 003,
005 to 008 · the working guide section 3 · the ledger (section G).

---

## D-167 — Secret scanning over the full history: the scanner's own release, checksum-pinned; an allow-list entry names the file and the value

**Date:** 2026-09-26 · **Status:** accepted · **Amends:** D-150 (item 3, the secret scanner), D-166 (section B, the Tier 1 allowances) · **Extends:** D-042.3

**TL;DR.** OPS-DEP-004 asked for gitleaks from its official action and a scan of the full
history on every push. The action cannot do both: on a push it scans only the pushed
commits, and no input widens that. It also downloads the scanner without checking it.
The step now runs the gitleaks release itself, at a pinned version whose archive is
checked against a SHA-256 written in the pipeline, over every commit of every branch and
tag. The first finding, an item identifier in a documentation comment, showed that an
allow-list entry naming only a file would exempt everything in that file, including a
real secret added later. An entry now names the file and the exact value, and both must
match. The first scan of the full history then found three leaked-password hashes that one
rule reads as tokens. The offline leaked-password list, and it alone, is exempted by its
line form.

**What happened.** Updating `corrections-3` with `main` pushed a range that began before
phase 8, and the `generic-api-key` rule matched `PRIV-BREACH-002` in the comment
`/// Implements LIB-API-005, PRIV-BREACH-002 and chapter 09 section 8a.` of
`src/Janus.Core/IAuditTrail.cs`. The rule reads `API` as a keyword, the comma as an
assignment and the next identifier as a value of enough entropy. It is not a secret. The
implementer stopped instead of writing the entry, which was right: the entry touches a
security gate.

**What the action does** (checked against its source, `src/index.js` and
`src/gitleaks.js`; its v3 changes only the runtime, not inputs or behaviour). On a push
it scans `--no-merges --first-parent <first pushed commit>^..<last pushed commit>`, or
`-1` when they are the same commit; on a pull request, the request's commits. Only a
manual or scheduled run scans without a range. The scanner binary is a version the action
hard-codes (8.24.3 when this was written), downloaded from the release page with no
checksum check. Pinning the action to a commit
SHA fixed the action's code, not the binary it fetches. A full-history scan on every push
is therefore not reachable through the action, and the range it does scan misses commits
that reach a branch only through a merge or through a new branch pointing at commits
already pushed.

**Decisions.**

1. **The scanner.** The pipeline step downloads the gitleaks release archive for the
   runner's platform at a pinned version (the latest stable release when the step is
   written; raised only in a commit of its own) and checks it against its SHA-256, written
   in the pipeline file beside the version and taken from the release's published
   checksums. A mismatch fails the step before the scanner runs. The step checks out the
   full history (`fetch-depth: 0`) and runs `gitleaks git` over every commit of every
   branch and tag, with the committed `.gitleaks.toml`, redacted output, failing the
   build on any finding. It runs on every push. The default rule set, the config holding
   only allow-list entries and the absence of any other scanner are unchanged.
2. **An allow-list entry.** Each is one `[[allowlists]]` table (gitleaks 8.25.0 or later)
   with `description` giving the reason, `paths` naming the one file, `regexes` matching
   the one flagged value exactly (anchored, checked against the finding's secret, which is
   gitleaks' default target) and `condition = "AND"`, so a real secret added to the same
   file is still found. An entry never names a file alone, a directory or a shape of value,
   except as item 4 states for the offline leaked-password list.
   *Rejected:* a pattern exempting item identifiers everywhere (it exempts a shape across
   the repository and covers every future file unseen, where OPS-DEP-004 has each
   exemption named and reasoned); a `.gitleaksignore` fingerprint (it is tied to one
   commit, line and rule, stops matching on the next edit of the line, and lives outside
   the committed configuration OPS-DEP-004 names); an inline `gitleaks:allow` marker (it
   puts scanner vocabulary into library source).
3. **The first entry.** `src/Janus.Core/IAuditTrail.cs`, the value `PRIV-BREACH-002`,
   reason "an item identifier in a documentation comment".
4. **The offline leaked-password list.** The first scan of the full history (728 commits)
   found three matches of the `square-access-token` rule in
   `src/Janus.Hosting/Passwords/leaked-passwords.txt`: SHA-1 hashes that begin `EAAA`,
   which the rule reads as a token's prefix. They are hashes of leaked passwords drawn
   from Pwned Passwords (AUTH-PASS-004), public by nature, and nothing is rotated. The
   list holds 100,000 hashes and is drawn again at every release (CONV-VCS-005), so a
   release whose draw brings in a new hash of a shape some rule reads as a token would
   stop on a question that has one answer. The list's entry therefore names the file and
   its line form, a whole line of exactly 40 upper-case hexadecimal characters, with
   `condition = "AND"` and the reason "SHA-1 hashes of leaked passwords (AUTH-PASS-004)".
   The entry matches the whole line, not only the value a rule captures, so a rule that
   captures part of a hash is covered and nothing short of a full hash line is. The date
   on the first line and anything else in the file are still scanned: a credential of any
   other form added to the file fails, and the same hash in any other file fails. The one
   thing the entry lets through is a value of exactly that form on a line of its own in
   this file, which the draw writes whole and nobody edits by hand. *Rejected:* an entry
   per hash (a stop at any release that draws a new such hash, and no more protection
   than the line form, since the only check anyone can make is that the value is a hash
   line of this list); exempting the file (a credential of any form would pass); storing
   the list in binary (the history still holds the text, and the draw and the reader would
   change for the scanner's sake). Another data file the package embeds (the Public
   Suffix List, the dictionary lists) takes no such entry: a finding in one is a Tier 3
   stop.
5. **Tier.** Writing an entry of exactly this form is Tier 1 when the flagged value is text
   of the specification (an item identifier, or a code, key, action or vocabulary member
   spelled as `10` spells it), recorded under **Resolved by rule**. Any other finding is a
   Tier 3 stop. A finding that is, or may be, a real credential ends the run at once;
   rotating it and any rewrite of history are the owner's decisions.
6. **The implementer's reading.** The full-history mismatch was not Tier 1: the job could
   not be made to check what the chapter said without leaving the official action, which
   the chapter also named. This entry settles it.

**Propagated to:** `06` OPS-DEP-004 (text and acceptance criteria 3 to 6) · the working guide section 3.

---

# Index — all items closed

| Item | Decision |
|---|---|
| Organization model | D-001, D-002, D-003 |
| Passkey RP ID / domain layout | D-004 |
| OIDC provider scope | D-005 |
| Stalwart integration | D-006 |
| Session and token model | D-007 |
| Key management | D-007, D-026.3 |
| Staff recovery path | D-008 |
| Email OTP / magic link as second factor | D-009 |
| Configuration taxonomy / system-admin permission | D-010 |
| Password policy | D-011 |
| Factor list cleanup | D-012 |
| Abuse controls | D-013 |
| Impersonation | D-014 (seam only) |
| Resource-type registry | D-015 |
| Access-denied semantics | D-016 |
| Data access strategy | D-017 |
| Where migrations run | D-018 |
| Destructive DDL gate | D-019 |
| Review-pass inconsistencies | D-020 |
| Localization | D-021 (deferred with seams) |
| Notifications | D-022 |
| Egyptian data protection obligations | D-023 |
| Consent and lawful basis | D-024 |
| Compliance evidence | D-025 |
| Audit retention and immutability | D-026.1 |
| Timestamps and time zones | D-026.2 |
| Secrets storage and rotation | D-026.3 |
| Library versioning | D-026.4 |
| Policy changes vs live sessions | D-026.5 |
| Minors on the storefront | D-027 |
| Bootstrap | D-028 |
| "Aim for perfection" framing | P-003 |
| Company/developer relationship, DPO obligation (R-01) | D-029 |
| Sensitive data — health-implying purchase history (R-02) | D-030 |
| Language and compliance text (R-03) | D-031 |
| Lawful bases enumeration (R-04) | D-032 |
| Session lifetimes, CSRF, logout, rotation (R-06 to R-09) | D-033 |
| Second-factor mechanics (R-10) | D-034 |
| Email and phone change flows (R-12) | D-035 |
| RoPA fields per regulator template (R-13) | D-036 |
| Data subject rights (R-05) | D-037 |
| Organization lifecycle (R-11) | D-038 |
| Age verification (R-23) | D-039 |
| Collation and Unicode normalization (R-16) | D-040 |
| Minor sweep — R-14, R-15, R-17 to R-22, R-24 | D-041 |
| GitHub plan constraints, DDL gate reworked | D-042 |
| Derived grants | D-043 |
| Backup and disaster recovery | D-044 |
| Insider exfiltration detection and export gating | D-045 |
| Dependency supply chain | D-046 |
| Operator machine and secrets custody | D-047 |
| Security alerting | D-048 |
| Cash-on-delivery abuse — out of scope | D-049 |
| Staff offboarding procedure | D-050 |
| Shared credentials prohibited and detected | D-051 |
| BFF middleware shipped by the library | D-052 |
| BFF CSRF and ordering, corrected after research | D-053 |
| Library never renders user-facing text | D-054 |
| Recipient locale resolution | D-055 |
| Privacy notice presented, not accepted | D-056 |
| Return destinations validated | D-057 |
| Subject access export is data, not a document | D-058 |
| Message templates stay in the repository | D-059 |
| Profile photo — policy-gated, stored in PostgreSQL | D-060 |
| Location, time zone, address resolution | D-061 |
| Frontend platform and scope | D-062 |
| Infrastructure constraints | D-063 |
| Marketing/analytics split; licensing verified | D-064 |
| Break-glass made functional (F-01) | D-065 |
| Consent gates purposes, not records (F-02) | D-066 |
| Step-up: two rule shapes, non-member policy (F-03) | D-067 |
| Erasure reaches host data; crypto-shredding (F-04) | D-068 |
| Key material in DR; KEK escrow (F-05) | D-069 |
| Two pipeline profiles; webhook standard (F-06) | D-070 |
| Protected settings as a rule; alerting specified (F-07) | D-071 |
| Automated certificates a launch requirement (F-08) | D-072 |
| Guest checkout removed (F-09) | D-073 |
| Mailbox scope; hosting vs delivery (F-10) | D-074 |
| Audit immutability vs retention vs erasure (F-11) | D-075 |
| Factor discovery and duplicate leaks (F-12) | D-076 |
| Per-account session revocation (F-13) | D-077 |
| Encryption threats; capability semantics (F-14, F-15) | D-078 |
| Cache holds inputs, not outcomes (F-16) | D-079 |
| Low-severity sweep F-17 to F-22 | D-079a |
| Consistency sweep F-23 to F-26 | D-079b |
| Provider signing confirmed | D-080 |
| Hosting and Stalwart edition confirmed | D-081 |
| Crypto-shredding via OpenBao Transit (H1) | D-082 |
| Alert destination changes notify previous (H2) | D-083 |
| Escrow custody confirmed; rotation added (H3) | D-084 |
| Restore-during-absence honest; mail store backed up (M1) | D-085 |
| Step-up/login separation; social; assurance table (M2) | D-086 |
| Capability wire format aligned (M3) | D-087 |
| Takedown document re-rendered (M4) | D-088 |
| Assurance floor, alert exemption, consent fragment (M5–M7) | D-089 |
| No deletion; erasure via transactional outbox (M8) | D-090 |
| Jurisdiction-specific configuration separated | D-091 |
| Low-severity sweep L1–L8 | D-092 |
| The two-store seam (third review, 21 findings) | D-093 |
| Erasure intent-first; enumeration rule | D-094 |
| Reconciliation after restore, and its limit | D-095 |
| Off-host erasure ledger, deferred | D-096 |
| Wrapped keys in the database; key service removed | D-097 |
| Per-subject encryption scoped by schema | D-098 |
| Subject of an encrypted field is declared | D-099 |
| Ciphertext format marker and IV | D-100 |
| Internal-side erasure is manual | D-101 |
| Erasure text aligned to D-097; erasures table simplified (final review F1) | D-102 |
| Envelope contents completed; dedicated backup key (final review F2) | D-103 |
| Cross-application sign-on via OIDC code flow; BFFs as confidential clients (final review F3) | D-104 |
| Two bootstrap values; secrets manager properties stated (final review F4) | D-105 |
| Operations contract rendered as services and endpoints (final review F5) | D-106 |
| Safe defaults for fourteen keys; six required and listed (final review F6) | D-107 |
| Lawful bases declared with properties (final review F7) | D-108 |
| Phase 1 recorded as open-ended (final review F8) | D-109 |
| Quarterly restore test automated (final review F9) | D-110 |
| Admin-assisted recovery covers customers; link by SMS (final review F10) | D-111 |
| One verified phone per account (final review F11) | D-112 |
| Self-service deletion is the erasure right (final review F12) | D-113 |
| A short password is bought with a second factor (final review F13) | D-114 |
| Canonical form is `NFKC_Casefold`; UTS #39 script rule (final review F14) | D-115 |
| Policy follows membership; system policy for individuals (final review F15) | D-116 |
| Erasure is key destruction; business records outside the right (final review F16) | D-117 |
| Audit partition drop via one-purpose SECURITY DEFINER function (final review F17) | D-118 |
| Specification complete on its own; log is rationale (final review F18) | D-119 |
| Argon2id parameters and WebAuthn allow-list pinned (final review F19) | D-120 |
| Three orphaned alerts added; threat model points at the table (final review F20) | D-121 |
| Privacy requests acknowledged automatically on receipt (final review F21) | D-122 |
| Session lifetimes follow policy; no calendar sign-out for customers (final review F22, UX 1–3) | D-123 |
| Trusted devices skip the second factor for customers (UX 4) | D-124 |
| Housekeeping: small inconsistencies and legal precision (final review F23–24) | D-125 |
| Six working days is a decision deadline; lapse deemed rejection (review-2 H-1) | D-126 |
| Takedown: access stops now, erasure after a seven-day window (review-2 H-2) | D-127 |
| Step-up requires the strongest factor; social stays delegated (review-2 H-3) | D-128 |
| Break-glass usable by the owner: destinations, page, auth session (review-2 H-4) | D-129 |
| Customer sessions: one-year absolute ceiling; NIST claim made true (review-2 M-1) | D-130 |
| Recipient details are the customer's data under the customer's key (review-2 M-2) | D-131 |
| The missing numbers, the step-up list and the events catalogue (review-2 M-3) | D-132 |
| Break-glass code on paper only, issued from the management app (review-2 M-5) | D-133 |
| Phone change protections; trusted device revoked after 3 failures (review-2 M-6, M-9) | D-134 |
| Housekeeping pass two: the twenty-two mechanical findings (review-2) | D-135 |
| Decision clock from submission on Egypt's calendar (review-3 H-1) | D-136 |
| Takedown borrows the deletion timer, not its emails or buttons (review-3 H-2) | D-137 |
| Break-glass session belongs to the reserved `emergency` account (review-3 M-7) | D-138 |
| Single-factor idle restore is staff-only (review-3 M-9) | D-139 |
| Housekeeping pass three: the twenty mechanical findings (review-3) | D-140 |
| Step-up gates declare a level; lost authenticator is a state (review-4 H-1) | D-141 |
| Holiday list is a living calendar, never a startup condition (review-4 H-2) | D-142 |
| The policy is one object with five fields (review-4 M-1) | D-143 |
| `emergency` has no email or mailbox; first administrator's mailbox follows the mail server (review-4 M-2) | D-144 |
| Objection is a first-class right; consent guidance corrected for Egyptian law (review-4 M-3) | D-145 |
| The account has a shape; registration is a session; sending rules become restrictions (review-4 M-5, M-6) | D-146 |
| Token lifetimes, KEK rotation operation, infrastructure definition, `emergency` outside identifier rules; housekeeping four (review-4 M-4, M-7 to M-10, L-1 to L-22; D-144 question) | D-147 |
| Staff accounts carry a verified personal email from day one; review-5 closed | D-148 |
| Every design and code choice fixed; Conventional Commits 1.0.0; Keep a Changelog 1.1.0; MinVer | D-149 |
| Phase 0 questions: test projects under the analysers, JAN0006, gitleaks | D-150 |
| Phase 0 questions, second stop: gate names, factor identifiers, enum values, key types, families | D-151 |
| Phase 0 questions, third stop: bytes, maximums, signals, floors, years, direction | D-152 |
| Word-shaped values: one pass over every chapter; alert thresholds as keys, time zone, materiality, userinfo claims, host challenge, offline lists | D-153 |
| Phase 1 questions: generated Unicode tables at a pinned version, PRECIS as validation, CA1515 in tests, the visibility test | D-154 |
| Phase 1 questions, second stop: persistence records and the port encrypt; kind detection; collation scope | D-155 |
| Phase 1 questions, third stop: area grants to Storage.Tests; test infrastructure is Tier 1 | D-156 |
| Phase 1 questions, fourth stop: photo unreadable not removed; administrative flag; role names; retention by argument | D-157 |
| Phase 1, fifth stop: merge commits outside CONV-VCS-003; a gate's own defect is Tier 1 | D-158 |
| Phase 2 question: the filter is a same-context EXISTS over the contract tables; subject set first | D-159 |
| Phase 2, second stop: host-supplied relations; reading vs modifying; gate binding; hosted-service validation | D-160 |
| Phase 2, third stop: derived checks take sources; host-called refresh; the implementer decides alone through Milestone 1 | D-161 |
| Review of the 109 absent-owner decisions: 27 reversed, 14 settled, trace removed | D-162 |
| The product name is not a naming element; neutral `identity` prefix | D-163 |
| Provider profile proved: RFC 9700 and 2.1 tests, PAR required, RFC 9068 tokens, provider security events | D-164 |
| Host business content moved out of the specification; library-true rows and seams only | D-165 |
| Review of entries 110 to 423: 101 reversed, 14 settled; the working mode ends; the chapters reconciled; the ledger closed | D-166 |
| Secret scanning over the full history: the scanner's own release, checksum-pinned; an allow-list entry names the file and the value | D-167 |

**Queue clear.** Next step: rewrite the spec notes from this log.

---

# Outstanding actions — not design decisions

These are business or legal actions, tracked here so they are not lost.

| Action | Owner | Deadline |
|---|---|---|
| Confirm findings in D-023 with legal counsel | user | before 31 Oct 2026 |
| Appoint and register an external DPO | user | before 31 Oct 2026 |
| File the PDPC controller licence (fee-exempt at current scale) | user | before 31 Oct 2026 |
| File the cross-border transfer permit, **if** hosting stays outside Egypt | user | before 31 Oct 2026 |
| ~~Decide hosting location~~ | ~~closed — D-081: IONOS, outside Egypt~~ |
| **Off-machine backups in place** (D-044, DR-005) | user | at the VPS tier upgrade, post-launch |
| **Off-host erasure ledger** (D-096, DR-016) | user | the same upgrade |
| Direct Electronic Marketing licence — **only if marketing messages are sent** (D-064) | user | before first marketing send |
| Confirm whether video surveillance licence applies (D-064) | user | before 31 Oct 2026 |
| ~~Confirm Stalwart edition~~ | ~~closed — D-081: Community; nothing gated~~ |

---

# Deferred, with reactivation triggers

Per P-003 — nothing deferred silently.

| Deferred | Trigger to revisit |
|---|---|
| Localization design and its own library (D-021) | Before any user-facing text is written |
| Impersonation feature (D-014) | When support staff exist who need customer views |
| Third-party app platform, consent and dynamic registration (D-005) | First external client that is not first-party |
| Multiple organization memberships enabled by default (D-003) | First person needing access across organizations |
| Multi-market legal document sets, several governing languages on one deployment (D-146) | Serving a second market whose law names another language |
| Approval gate on destructive DDL (D-019) | First additional person with deploy access |
| Two-approver recovery (D-008) | First additional person with recovery approval rights |
| Self-hosted password breach corpus (D-011) | If counsel objects to the range API |
| Read/write separation for historical reporting (D-017) | When reporting queries outgrow tuning |
| Hash-chained tamper-evident audit records (D-071) | When a second person shares administrative duties, or audit integrity is challenged |
| Automated erasure routine for management schemas (D-101) | **The user's decision** — not an automatic trigger |
| **Scripted one-command restore the owner can run** (D-085) | Once the system is released and stable |
| Migration away from in-library authorization (D-015) | When permission queries are a top-3 slow query and index tuning has stopped helping |
| **External key service with per-subject keys (D-082, superseded by D-097)** | If protecting pre-erasure backups against a compromised secrets manager ever justifies a second stateful store |
| Templates editable outside the repository (D-059) | When a non-engineer needs to change a message |
| Profile photos enabled for customers (D-060) | When a feature makes a customer photo meaningful |
| Object storage for uploads (D-060) | Customer photos, or any genuinely large artifact |
| District-level boundary polygons (D-061) | If city-level preselection proves too coarse |
| Paid geocoding provider (D-061) | If customers demonstrably struggle with the address form |
| Separate hosts per application (D-063) | When host compromise becomes a threat worth defending against |
