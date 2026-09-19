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
implementing agent, that the choices be the current mainstream best fit for a security
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
  `minor` · `adult`). Cookies are `__Host-janus-session`, `-preauth`, `-csrf`, `-device`,
  `-browser`; the CSRF header is `X-Janus-Request`. The DNS record is
  `_janus-verify.<domain>`. The CLI verbs are `bootstrap`, `rotate-kek`,
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
3. **`janus_ci` applies to what is plaintext and spelled by a person**: organization
   names and locked domain names today. Identifiers are fingerprints and personal fields
   are ciphertext; a collation cannot see through either, and D-153's wording that named
   display names and usernames was wrong.

**Propagated to:** `08` CONV-DESIGN-003 · `06` OPS-DB-001 · `20` REG-IDENT-003,
REG-IDENT-009 · `10` section 1.1.

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
