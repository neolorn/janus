# 07 — Library Contract

The public surface, what is guaranteed stable, packaging, and versioning.

**Prerequisite:** `00-overview.md`, sections 1 and 2.

**Scope.** This document defines the boundary between the library and a host
project — what a consumer may depend on, what may change beneath them, and what they
must supply.

---

## 1. Packaging

**LIB-PKG-001** — The library SHALL ship as **one package** with strict internal
boundaries between Identity, Authentication, Authorization, Privacy, Storage, and
Hosting. Those areas SHALL NOT reach into one another. The conformance suite
(LIB-TEST-001) and the analysers (`08` CONV-CODE-008) ship as two further packages,
because a host runs the first and the build consumes the second; neither carries
library behaviour.

*Source: D-005, D-149*

Splitting into separate packages up front means maintaining a version matrix between
packages that only ever ship together. Split when a real second consumer needs one
part without the others, not before.

**Acceptance criteria**
1. Each internal area compiles without reference to the others' internals.
2. A test asserts no cross-area dependency outside declared contracts.

---

**LIB-PKG-002** — The core SHALL be separable from the data-access provider. Two
rendering targets exist — expression and SQL fragment — and neither SHALL be a
bolt-on to the other.

*Source: D-017*

**Acceptance criteria**
1. Core contracts compile without a database dependency.
2. Both renderings derive from one rule definition.

---

**LIB-PKG-003** — Distribution SHALL be via a private package feed on the company's
own infrastructure.

*Source: D-005*

**Acceptance criteria**
1. The package is not published to a public registry.

---

## 2. The public contract

**LIB-API-001** — The following constitute the public contract. Changes to any are
breaking.

| Surface | Includes |
|---|---|
| **Model builder API** | Resource type declaration, containment, concealment, sensitivity, purposes with their data and subject categories, roles, the step-up gate and the purpose bound to an action, **derivations**; and the processing read back from them, `DeclaredProcessing` and `DeclaredPurpose` with its `ConsentKind` (`10` section 5.10) |
| **Permission filter shape** | The expression form and the SQL fragment form |
| **Operations contract** | The service contracts for every library-owned operation (LIB-API-005), the strongly typed identifiers they take and return (CONV-DESIGN-004), `PrivacyRequestId` among them, and the gates one area asks of another, `IAccessGate` and `IStepUpGate` |
| **Extension contracts** | The interfaces a host implements or replaces for the library to call: the mail and SMS transports, `INotificationHandler` with `SendRequest`, `SendDestination` and `SendReference`, and `ISecretSource` (LIB-EXT-001); the declarations and environment seams of LIB-HOST-001; the assurance provider of LIB-HOST-004 |
| **Emitted events** | Notification and lifecycle event contracts, including the identifier, restriction and device events of D-146 (`IdentifierAdded`, `IdentifierRemoved`, `IdentifierPrimaryChanged`, `SendingRestrictionChanged`, `SendingRestrictionGranted`, `DeviceVerified`; `10` section 5b), each delivered to every registered `IEventConsumer<TEvent>` from a row written in the emitting transaction, retried under `outbox.retry.*`, and on exhaustion failed with `degradation`; publication is not replaceable (CONV-DESIGN-002). And the host registrations that answer for them: `ISubjectEventSubscriber`, naming the resource types it covers (`Covers`), and `IPurposeHandler`, naming the purposes it handles (`Purposes`) |
| **Database schema** | All library-owned tables |
| **Ancestry closure table** | Structure and semantics of `identity.ancestry`, `identity.effective_grants` and `identity.consented_resources` |
| **Error codes** | Machine-readable codes, their meanings and the status each answers |
| **Audit actions and permissions** | The audit actions and the permissions `10` lists; each is an identifier, stable as a code is (CONV-NAME-003) |
| **Conformance suite** | The public types of `Janus.Conformance` (LIB-TEST-001): the suite's four calls, the report, the finding, the check, the truth-table case and scenario, the host-rows seam and the client |
| **BFF middleware pipeline** | Mounting API and stage ordering (D-052) |
| **HTTP endpoints** | Method, path, body members, status codes and error codes of each endpoint, as `09-api-contract` gives them, held in a committed contract file generated from the endpoint data source |
| **Configuration keys** | Names, types, scopes and value constraints, and the key families |

*Source: D-026.4, D-017, D-041, D-106, D-146, D-166*

The ancestry closure is public because hand-written SQL will query it. It cannot be
restructured without a major version.

**Acceptance criteria**
1. Each surface is documented with its stability guarantee.
2. A change to any is caught before release by a contract test holding it to a committed
   contract file or to its `PublicAPI` file (REF-001 AC2).

---

**LIB-API-002** — Everything not listed in LIB-API-001 SHALL be internal and MAY
change in any release.

*Source: D-026.4*

**Acceptance criteria**
1. Internal types are not publicly accessible.
2. No consumer sample depends on an internal type.

---

**LIB-API-003** — Errors crossing the boundary SHALL be **machine-readable codes with
structured data**, never rendered prose. **There is no exception.**

*Source: D-021, D-054, D-166*

An earlier draft carved out endpoints the library served directly to a browser. That
carve-out is removed: **the library never returns user-facing text over HTTP.**
Anything a person reads in a browser is rendered by the frontend and localized there.

Message content sent by other means — email and SMS — is the worker's concern
(INT-SMS-005a), and is text a person reads. The rule is about the HTTP boundary, not
about all output.

Correct API design regardless of localization, and precisely what makes a future
localization library's job possible rather than impossible.

**Values (D-166).** The provider's protocol endpoints (`/oidc/token`, `/oidc/par`,
`/oidc/userinfo`, and an authorization error returned to a client's registered address)
SHALL answer an error in the shape their protocol fixes (RFC 6749 section 5.2, RFC 9126
section 2.3, RFC 6750 section 3), carrying `error` alone and never `error_description` or
`error_uri`. An authorization error the provider cannot return to a client SHALL be
answered to the browser in the API-CONV-002 body: 400 `api.request.malformed` with
`details.error` the protocol's code, or 500 `system.fault` for `server_error`. The Google
security-event route answers a token that fails validation in the shape RFC 8935 section
2.3 fixes (`09` section 10); the `description` that shape requires, whose content the
RFC leaves to the receiver, SHALL carry the `err` code again and never a sentence.

**Acceptance criteria**
1. No error returned across the boundary contains a user-facing sentence.
2. Each code is documented with its meaning and remediation.
3. The host renders errors in the user's language from codes.
4. No library endpoint returns HTML intended for a person to read, including
   framework default error pages.
5. No response of the provider carries `error_description` or `error_uri`.

---

**LIB-API-004** — The SQL fragment renderer SHALL be documented as
**PostgreSQL-specific**. No claim of dialect portability SHALL appear.

*Source: D-041, AUTHZ-GATE-003*

**Acceptance criteria**
1. The constraint is stated in API documentation.

---

**LIB-API-005** — Every library-owned operation SHALL exist **once**, as a service
contract in `Janus.Core`, and SHALL be exposed **twice**: callable in-process by a
host, and as an HTTP endpoint in `Janus.Hosting` mapped over the same contract. The two
operations named below as called in process only have no endpoint.

*Source: D-106, D-166*

The management application hosts the library in-process, so its natural call is the
service; the frontends reach the same operation over HTTP. This is the pattern the
gate already uses — one rule rendered two ways (AUTHZ-GATE-002) — applied to
operations. An endpoint is a mapping, never a second implementation.

**The operations covered** — each required by a SHALL elsewhere: organization
lifecycle and policy; memberships and invitations; groups; account suspension,
reactivation, restriction lift and deletion cancel; the takedown; the data-subject
request queue; consent and objection records; erasure progress and manual completion; audit query by subject;
explanation resolution; compliance-text publishing; licence, permit and assessment
records; credential and factor management; profile photo and language preference; the
registration session; identifier, preference and session management; the restriction
set and grants; mail app passwords; domain lock (D-146); the generation of the
break-glass credential and whether one stands (OPS-BOOT-004, OPS-BOOT-001 AC3).
The endpoints are enumerated in `09-api-contract`; the permissions in `10-reference`
§2.1.

**Endpoints that are not mappings of an operation.** An endpoint that establishes or
rotates the browser's session maps its operation through the library's internal service,
since the session and its cookie are the browser's and not a result a host calls for: the
social provider round trips (`/auth/providers/{provider}`, their returns and
`/callbacks/providers/{provider}/return`), `/auth/signon` and its return,
`POST /auth/break-glass`, `/auth/factor`, `/auth/device/verify`, `/auth/step-up` and
`/register/terms`. The callbacks a gateway or provider sends (`GET /callbacks/sms/dlr`,
`POST /callbacks/providers/{provider}`), the three well-known documents and the
provider's protocol endpoints are not operations. Beside its contract an endpoint MAY
take the browser's own records: its pre-authentication binding, the rotation of its
session, and the registration signal.

**Every operation takes an access context** (AUTHZ-IMP-001) — a system principal
with a stated reason when called from background work (IDN-PRIN-001) — and is
authorized by the gate exactly as the endpoint would be. Calling in-process is not a
way round the permission.

**Seams that join the host's transaction.** `IResources` (AUTHZ-INHERIT-002) and
`ICallbackReferences` (BFF-MACH-003) are seams, not operations of this contract: each
joins the host's own transaction, so neither has an endpoint, and neither takes an
access context. Placing or moving a record is the host's own action, which the host asks
of the gate before it writes.

**Operations called in process only.** Two operations are called by a host in process
only, and neither has an endpoint or meets a gate (CONV-DESIGN-002 AC3): read-volume
counting (`IReadVolume`, OPS-ALERT-005), which reports what the host's own gate-filtered
queries and exports returned, which no HTTP caller has to report; and derivation refresh
(`IDerivationMaterialiser`, AUTHZ-DERIVE-005), which the host calls from its own gated
operation that changes a relationship.

**Acceptance criteria**
1. Each endpoint in `09` other than those named above resolves to exactly one service in
   the contract; a test asserts no endpoint carries logic the service does not.
2. A host calling a service in-process is subject to the same permission check as the
   endpoint.
3. Adding an operation without adding its service to the contract fails a contract
   test.

---

## 3. What the consumer supplies

**LIB-HOST-001** — A host project SHALL declare the following and no more.

| Declaration | Contents |
|---|---|
| **Connection details** | Database and cache |
| **Enabled authentication factors** and their policy | Per organization |
| **Resource types and containment** | The only place the library learns the host's domain — including, for each encrypted field, the column identifying its subject (PRIV-RIGHT-005a) and the data category the field holds (PRIV-PRIN-001 AC2); a field whose category no purpose on its type names fails startup with `model.startup.declarationinvalid`. Each record is registered with its data subject read from its subject column (PRIV-SENS-002) |
| **Roles** | Named bundles, and the actions that exist, each with the step-up gate and the purpose it is bound to where it has one (AUTHZ-GATE-005) |
| **Processing purposes and lawful bases** | Per resource type (AUTHZ-MODEL-003), each purpose with the data categories and subject categories it requires (PRIV-PRIN-001) and naming the legal document that governs its consent, the privacy notice where it names none; one purpose names one document across every type, and a document's name is 1 to 64 lower-case letters and digits separated by single `.`, `-` or `_` (INT-SMS-003). A retention floor is declared for each data category a purpose is over, and is the default of `retention.<host-category>` (PRIV-RET-001). The jurisdiction's basis list with its properties, and its sensitive-data categories, are declared once; Egypt's ship as `LawfulBases.Default` and `SensitiveCategories.Default`, which the host passes to the builder. The library applies no list the host did not declare, and reads one category by name, `children` (PRIV-BASIS-001, PRIV-SENS-001, D-108) |
| **Hosting location, environment and cross-border basis** | Configuration: `hosting.location` (INT-HOST-001) and `hosting.environment`, the free-text environment cell of the records of processing (PRIV-ROPA-001); the basis only where hosting is outside Egypt |
| **Relying party identifier and origins** | Configuration (AUTH-FACT-010) |
| **Application kind** | Which of the deployment's applications the process serves, `ApplicationKind`: `Management` or `Public`, declared at registration (`AddJanus`) and never at the mount; it decides `SameSite` alone (BFF-CSRF-005). The unset value is `Management` |
| **Landing origins** | Required, no default: `LandingOrigins` (`Authentication`, `Account`), the origin of the authentication application and the origin of the account application, each an absolute `https` origin. Every link the library sends is `<origin>/link#<kind>.<token>`, its kind (the link kinds of `10`) choosing the application (FE-VER-001), so the token never reaches a server in the address. Absent, startup fails with `model.startup.declarationmissing`, `details.key` `landingOrigins.authentication` or `landingOrigins.account`; an origin that is not the origin of a registered browser client's return address, or an authentication origin that is not the origin of the sign-in address, fails it with `model.startup.declarationinvalid` under the same `details.key` |
| **Authentication addresses** | Required, no default: `AuthenticationAddresses` (`signIn`, `provider`): where an authorization request that is not silent and holds no session is forwarded (AUTH-SESS-012 AC3), and the address the library is mounted at on the authentication application, prefix included, where another application finds `/oidc/par`, `/oidc/authorize` and `/oidc/token` (BFF-SESS-006, LIB-HOST-003). Absent, empty or blank, startup fails with `model.startup.declarationmissing` naming `authenticationAddresses.signIn` or `authenticationAddresses.provider` (D-162) |
| **Passkey pages** | Required, no default: `PasskeyAddresses` (`changePassword`, `enrol`, `manage`), the frontend pages `/.well-known/change-password` and `/.well-known/passkey-endpoints` point at (REG-PM-001). Absent, or with a field empty or blank, startup fails with `model.startup.declarationmissing` naming `passkeyAddresses` or the field (D-162) |
| **Sign-on client** | Required, no default: `SignOnClient` (`clientId`), what this application calls itself at the provider when it establishes its own session; the client registry holds the one destination a code returns to under it (BFF-SESS-006). Absent, startup fails with `model.startup.declarationmissing` naming `signOnClient.clientId`. It carries no secret: the library generates the client's secret, holds it in the registry and rotates it (OPS-SEC-002) (D-162) |
| **Alert destinations** | At least one email and one SMS destination for the operator (OPS-ALERT-004), **and the owner's email and SMS destination** — break-glass alerts reach them always (OPS-BOOT-002, D-129) |
| **SMS balance floor** | A prepaid amount the library cannot guess (INT-SMS-004) |
| **Mail and SMS transports** | `IMailTransport` and `ISmsTransport` (LIB-EXT-001), each required unless the library's shipped transport for its channel is in use (`integration.mail.endpoint`, `integration.sms.endpoint`). A channel with neither fails startup with `model.startup.declarationmissing`, `details.key` `mailTransport` or `smsTransport` |
| **Secret source** | Required, no default: `ISecretSource`, through which the library reads every secret it needs, asynchronously, when the application starts and before it serves a request: the key-encryption key versions and the fingerprint key versions, each fingerprint key version at least 32 bytes (OPS-SEC-001, AUTH-KEY-002); the maintenance credential (OPS-MIG-003a); the mail server's secret where the shipped mail-server adapter is used (INT-MAIL-001); and each declared social provider's credential, by the provider's name (IDN-LIFE-012). No secret is an argument of `AddJanus`. Absent, startup fails with `model.startup.declarationmissing`; a secret it cannot answer, or answers empty or short, stops the start with `model.startup.secretunavailable`, `details.key` naming the secret: `keyEncryptionKeys`, `fingerprintKeys`, `maintenanceCredential`, `mailServerSecret` or `socialProvider.<provider>`. A `Janus.Cli` command reads the same values from one JSON document on standard input, and one whose standard input is a terminal is refused under `details.key` `input` (OPS-SEC-001, CONV-DESIGN-007) |
| **Subject-event handlers** | Erasure, restriction and export. Each registered subscriber (`ISubjectEventSubscriber`) names the resource types it covers (`Covers`), and every type declared sensitive SHALL be covered by at least one. **Startup fails otherwise** (PRIV-RIGHT-005b), `details.handler` naming the type. Subscriber names are distinct and `erasure-ledger` is the library's own (`model.startup.subscribername`, IDN-LIFE-003a); a name is 1 to 64 lower-case letters and digits separated by single `.`, `-` or `_`, any other refused with `model.startup.declarationinvalid` (INT-SMS-003) |
| **Purpose handlers** | For every purpose on an objectable basis, a registered `IPurposeHandler` naming it (`Purposes`). **Startup fails otherwise** (PRIV-RIGHT-001a AC3), `details.handler` naming the purpose |
| **Governing language** | `legal.governinglanguage`: the default governing language of every legal document version (PRIV-CONS-005). Required, no default; protected once set (OPS-CFG-004) |
| **Preference declaration** | Each host-declared preference: name, type (`string` · `boolean` · `integer` · `enum` with its values), default, and whether the person or only an administrator may edit it (REG-PREF-001). The declaration MAY be empty; a malformed one fails startup |
| **Restriction key suppliers** | Optional: for each `host:<name>` restriction key the host uses (AUTH-ABUSE-004), a per-send callback registered under that name that returns the key value the restriction is evaluated against. A restriction naming a `host:<name>` key with no registered supplier fails startup, and a change of the restriction set naming one is refused with `config.value.notallowed`, `details.supplier` naming it. The callback is `Func<SendContext, CancellationToken, ValueTask<string>>`; `SendContext` is a sealed record of `Purpose` (`10` section 5.15), `Kind` (`email` · `sms`), `SubjectId` (nullable) and `Source` (nullable: a send no request asked for has none) (D-153) |
| **Calendar time zone** | `privacy.calendar.timezone`: the IANA zone in which the legal clock counts days (PRIV-RIGHT-002). Required, protected (D-153) |
| **Message languages** | `notification.languages`: the BCP 47 tags every outbound message may be sent in (IDN-ATTR-001, AUTH-ABUSE-005). Required, at least one (D-153) |
| **Email sending domain** | `notification.email.sendingdomain`, and the optional set of relay-registered domains (INT-MAIL-011). Required (D-153) |
| **Recipients** | Optional: the processors and recipients the records of processing list (PRIV-ROPA-002), each `{ name, characterisation, dataReceived, location, agreementReference, callback }`; the library ships as the default set only the rows its own processing makes true (mail server, SMS gateway, hosting provider, password screening; `05` section 6, D-153, D-162). Every business processor, payment and shipping included, is the host's declaration |
| **Challenge verifier** | Optional: a callback that takes a challenge token and answers pass or fail (AUTH-ABUSE-008). Absent, bot-defence signals are audited and no challenge is shown (D-153) |
| **Phone signal provider** | Optional: a callback answering `none` · `clear` · `risk` for a number (AUTH-FACT-002b). Absent, the record says `unavailable` (D-153) |
| **Reserved usernames** | Optional: names added to the library's reserved list (REG-IDENT-009) (D-153) |
| **Dictionary words** | Optional: words added to either list the `dictionary` password source ships, the English list or the Arabic transliteration list (AUTH-PASS-004). A host extends the shipped lists by this declaration only, never by a deployment file. Absent, the shipped lists alone answer (D-166) |
| **Sensitive-body endpoints** | Optional: `SensitiveBody` (`Janus.Core`), put on an endpoint as an attribute or as endpoint metadata, which turns body logging off for it (BFF-LOG-002) (D-153). Every endpoint the library maps carries it |
| **Relationship sources** | Required for each relationship a declared derivation is over, materialised or not: a scoped source of the relationship's rows from the host's own context. The library evaluates over it the `GET /admin/access` view (AUTHZ-DERIVE-007) and the daily drift check of materialised derivations, `derivation.materialised.driftcheck` (AUTHZ-DERIVE-005); the rows stay the host's and the query runs in the host's context (LIB-HOST-002). Absent, startup fails with `model.startup.declarationmissing` naming the relationship |
| **Image codec** | Required while any policy's `photos` field is `true` (IDN-ATTR-002, `10` section 4.1a): `ImageCodec` (`Reencode`), `Func<ReadOnlyMemory<byte>, int, CancellationToken, ValueTask<ReadOnlyMemory<byte>?>>`, taking the upload and the longest side in pixels and answering the re-encoded JPEG with every metadata segment removed, or nothing for bytes the deployment does not accept (`identity.photo.invalid`, IDN-ATTR-004). The formats IDN-ATTR-004 accepts and the quality it stores are the contract the codec is declared to meet. Bootstrap writes the administrative organization's `photos` as `false`. A policy change setting `photos` to `true` without a codec is refused with `config.value.notallowed`, `details.field` `photos` and `details.requires` `imageCodec`; a stored policy showing photos without one fails startup with `model.startup.declarationmissing` naming `imageCodec` (D-162) |
| **Social providers** | Optional, once per social provider: `SocialProvider` with `provider` (a factor the catalogue marks as a social provider), `metadata` (the provider's document naming the issuer and key set its security events are signed under), `configuration` (its OpenID Connect discovery document), `return` (the address registered at the provider that the browser comes back to, ending in `/callbacks/providers/{provider}/return`) and `clientIds` (the audiences an event names; the first is the client a sign-in runs under and the only audience its identity token may name). Its credential is read through the secret source by the provider's name: a static client secret the provider issued, or a signing credential (issuer, key identifier and a P-256 private key) from which the library mints the client secret at each exchange (IDN-LIFE-012, OPS-SEC-002). None declared: no round trip to that provider starts and its events are refused. Declared twice, naming a factor that is not a social provider, with an address that is not absolute `https`, a `return` whose path does not end as above, or no client or an empty one: startup fails with `model.startup.declarationinvalid` naming the member. A credential that is empty, or a signing credential whose issuer or key identifier is blank or whose key is not a P-256 private key: startup fails with `model.startup.secretunavailable`, `details.key` `socialProvider.<provider>` (IDN-LIFE-012a, D-164) |
| **Mail server** | Optional: `IMailServer` (`ProvisionAsync`, `MailboxesAsync`, `AppPasswordsAsync`, `CreateAppPasswordAsync`, `RevokeAppPasswordAsync`; INT-MAIL-006, INT-MAIL-008, INT-MAIL-010), or the shipped JMAP adapter, which the library registers while `integration.mailserver.endpoint` is set and the host registered none (LIB-EXT-001). A push carries a key stable until the server confirms it, the canonical address and the mailbox state of `10` (`disabled` · `enabled` · `removed`); the app-password calls carry the person's token. Absent, nothing is pushed or compared (the rows are still written, and the first pass after one is registered pushes every state owed) and every app-password operation answers `identity.mailbox.notfound`; a deployment whose staff mail is hosted elsewhere needs none |
| **Mail server client** | Required where a mail server is registered, no default: `MailServerClient` (`clientId`), the registry's `protocol` client the mail server trusts, to which the library issues the person's token for the app-password calls (INT-MAIL-010, AUTH-OIDC-001 AC4). Absent, startup fails with `model.startup.declarationmissing` naming `mailServerClient.clientId` |
| **DNS resolver** | Optional: `IDnsResolver` (`TextRecordsAsync`), the TXT lookup domain verification reads (REG-DOM-001). Absent, adding a domain to an organization's lock is refused with `config.value.notallowed`, `details.requires` `dnsResolver`, and a stored listed domain fails startup with `model.startup.declarationmissing` naming `dnsResolver`; a deployment that locks no domain needs none |
| **Location file** | Optional: `ILocationSource`, which opens the IP-to-city file in the format INT-GEN-006 gives. Absent, no session carries a location and `degradation` is raised (INT-GEN-006, AUTH-SESS-013) |
| **Clock reference** | Optional: `IClockReference`, the offset the environment measured against its time source (INF-HOST-001). Absent, or unable to answer, `degradation` is raised under `clock.reference.absent` or `clock.reference.unread` |
| **Certificate renewal** | Optional: `ICertificateRenewal`, the outcome of the latest certificate renewal (INF-TLS-003). Absent, or unable to answer, `degradation` is raised under `certificate.renewal.absent` or `certificate.renewal.unread` |
| **Restore-test instance** | Optional: `IRestoreTestInstance`, which builds the throwaway instance the restore test restores into and tears it down (DR-007, DR-008). Absent, every run of the restore test fails as `unrestored` and raises `restore-test-failed` |
| **Off-host erasure ledger** | Optional: `IErasureLedger`, which appends each completed erasure's line off the host (DR-016). Absent, erasures complete without a line, the residual R-A13 accepts; once one is registered, every erasure completed before it is appended too |

Everything else has a safe default the host may override (P-001, D-107), including
the public-holiday list, whose safe default is empty and which staff maintain at
runtime as dates are announced (`privacy.holidays`, D-142). The
declarations above are exactly what the library cannot know for a deployment;
every other key in `10-reference` §4 carries a default at the safe end of its range.
They are of three kinds. Most are required and have no default. Some may be empty: the
preference declaration and the restriction key suppliers. The rest are required only
where a feature the deployment uses needs them (the relationship sources, the image
codec, a transport for a channel whose shipped transport is not in use, the mail server
client), or are optional,
each row stating what its absence does. A minimal configuration therefore needs only the
required declarations and those its own features call for.

*Corrected three times: the list originally said "exactly four things," which four
other requirements already contradicted; the subject-event handlers added by D-068
were still missing after that correction; and `10` then listed twenty keys with no
default, of which fourteen could be defaulted and six belonged here (D-107).*

*Source: D-148; D-005, D-015, D-068, D-107, D-146, D-153, D-162, D-166*

**Acceptance criteria**
1. A minimal working configuration requires only the declarations listed above; every
   other key has a default.
2. Omitting any produces a named startup error identifying which:
   `model.startup.declarationmissing` with `details.key`, `details.handler` or
   `details.supplier` naming the omission (D-153); `legal.governinglanguage` keeps its
   own code. A declaration present but malformed fails startup with
   `model.startup.declarationinvalid`, `details.declaration` and `details.field` naming
   it. A secret the secret source cannot answer fails startup with
   `model.startup.secretunavailable`, `details.key` naming it, before the server serves a
   request.
3. No key outside this list fails startup when unset.
4. Startup without `legal.governinglanguage` fails with a named error; startup with an
   empty preference declaration and no restriction key supplier succeeds.
5. A `host:<name>` supplier is invoked once each time a send is judged, however many of
   the restrictions that apply to it name that key, and never for a send none of them
   applies to; its return value is the key the buckets are counted under. A retried
   delivery is judged again.
6. A declared landing origin that is not the origin of a registered browser client's
   return address, or an authentication origin that is not the origin of the sign-in
   address, fails startup with `model.startup.declarationinvalid`, `details.key`
   `landingOrigins.authentication` or `landingOrigins.account` naming it.
7. Startup succeeds with none of the optional declarations registered, and each absence
   has the effect its row states.

---

**LIB-HOST-002** — The host SHALL apply library-produced filters to its **own**
queries. The library SHALL NOT query host tables.

**Values (D-159).** The host maps the contract tables into its own `DbContext` with
`MapAuthorizationTables(ModelBuilder)` and passes their `DbSet`s to the filter: the
ancestry and the effective grants, and, for a permission bound to a consent-based
purpose, the consented resources (AUTHZ-GATE-002, PRIV-SENS-002). The library reads
nothing of the host's, and the host's query stays one query.

*Source: D-015, AUTHZ-PRIN-002, D-166*

This is what keeps permission filtering inside the host's query and list screens
fast.

**Acceptance criteria**
1. The library contains no query against a host-owned table.
2. Filters compose into a host query without materialising rows.

---

**LIB-HOST-003** — The host SHALL mount library endpoints where it chooses. The
library SHALL NOT assume a route prefix.

The prefix SHALL be the path base of the branch the host mounts the library in
(`IApplicationBuilder.Map(prefix, ...)`): the machine and browser profiles and
`MapIdentityEndpoints`, the OIDC provider's endpoints and its discovery document among
them, are mounted inside the branch. Every address the library answers or publishes
SHALL be built from the request, so each carries the prefix. The provider address the
host declares (`AuthenticationAddresses.provider`, LIB-HOST-001) SHALL be the address the
library is mounted at, prefix included. The three site-root documents,
`/.well-known/webauthn`, `/.well-known/change-password` and
`/.well-known/passkey-endpoints`, SHALL be mounted outside the branch, at the root of
the application, by `MapIdentityWellKnown`, and SHALL NOT carry the prefix.

*Source: D-005, D-162, D-166*

**Acceptance criteria**
1. Mounting under a non-default prefix requires no code change.
2. No absolute path is hardcoded, including in the discovery document.
3. Under a prefix, every path the library answers and every address the discovery
   document publishes carries it, and the three site-root documents answer at the site
   root.

---

**LIB-HOST-004** — Where authorization is consumed without authentication, the host
SHALL supply an assurance provider if step-up is required, reporting for the caller the
attained assurance level, whether it was phishing-resistant, the instant it was attained
and the account's reachable assurance. The gate is judged from that report (AUTH-STEP-002).
Absent one, step-up checks SHALL fail closed.

*Source: D-041, AUTH-STEP-003, D-166*

**Acceptance criteria**
1. Authorization alone compiles and runs.
2. Without an assurance provider, an action bound to a step-up gate is denied, and the
   denial is distinguishable in diagnostics.
3. With an assurance provider whose report meets a bound gate, the action is admitted;
   with one whose report does not, it is refused with `auth.stepup.required` carrying the
   gate.

---

## 4. Extension points

**LIB-EXT-001** — The following SHALL be replaceable without modifying library
source.

| Extension point | Default shipped |
|---|---|
| Mail delivery (`IMailTransport`) | A default handler, calling `integration.mail.endpoint` |
| SMS delivery (`ISmsTransport`) | A default handler, calling `integration.sms.endpoint` |
| Notification handling (`INotificationHandler`) | Email and SMS. A replacement carries a message the library has already admitted under AUTH-ABUSE-004 and written to its outbox; it decides no restriction and counts nothing |
| Message and content templates | A catalogue in `Janus.Hosting`, in English and Arabic: every message kind on every channel it goes out on, one text per language, each text message within its budget at its widest (AUTH-ABUSE-005, INT-SMS-003), and every link as the place `{link}`, which the library fills with the link's address (LIB-HOST-001, landing origins). A deployment that registers none is answered from it. An email owed in every declared language is composed by the library from each language's text (IDN-ATTR-001); no catalogue holds a text in more than one language |
| Audit sink | Database |
| Cache | Redis |
| Authentication factors | The catalogue in `02-authentication` |
| Record-level rules atop the permission model | None |
| Mail server (`IMailServer`: mailbox provisioning and app passwords, INT-MAIL-001) | The JMAP adapter in `Janus.Hosting`, registered while `integration.mailserver.endpoint` is set and the host registered none (LIB-HOST-001) |
| Secret source (`ISecretSource`: the secrets LIB-HOST-001 lists; INF-HOST-003) | None: the host supplies it (D-149). The library reads every secret through it, asynchronously, when the application starts and before it serves a request; no secret is an argument of `AddJanus`, and startup fails without it. A `Janus.Cli` command reads the same values from one key document on standard input (OPS-SEC-001) |
| Environment seams (clock reference, certificate renewal, DNS resolver, location file, restore-test instance, off-host erasure ledger) | None: each is an optional declaration of LIB-HOST-001, whose row states what its absence does |

A core namespace is `Janus.Core` or an area project's (`Janus.Identity`,
`Janus.Authentication`, `Janus.Authorization`, `Janus.Privacy`). A product behind an
extension point SHALL be named only in `Janus.Hosting`, where the shipped defaults and
adapters live.

*Source: D-022, D-012, P-003, D-162, D-166*

**Acceptance criteria**
1. Each is registered through configuration, not inheritance from a library type.
2. Replacing one requires no library change.
3. No provider name appears in a core namespace.
4. With a notification handler the deployment registered, a send an exceeded restriction
   refuses is refused with `retryAt`, and the handler is never asked to carry it.

---

**LIB-EXT-002** — Adding a factor, resource type, role, or provider SHALL be a
**registration**, never an edit to library source.

*Source: D-012, D-015, P-003*

**Acceptance criteria**
1. A new factor registered with `IsPhishingResistant = true` satisfies existing
   step-up rules unmodified.
2. A new resource type requires no library change.

---

## 5. Versioning

**LIB-VER-001** — The library SHALL use **Semantic Versioning 2.0.0** against the
contract in LIB-API-001. The version SHALL be derived from the release tag (`vMAJOR.MINOR.PATCH`)
by the build (CONV-VCS-005) and SHALL appear in no project file.

*Source: D-026.4, D-149*

**Acceptance criteria**
1. A breaking change to any listed surface increments the major version.
2. Additive changes increment the minor version.

---

**LIB-VER-002** — Every major version SHALL ship with a migration note describing
what changed and what a consumer must do.

*Source: D-026.4*

**Acceptance criteria**
1. The note names each breaking change and its remediation.
2. Schema changes name the required migration order.

---

**LIB-VER-003** — Consumers MAY skip minor versions. Consumers SHALL NOT skip major
versions.

*Source: D-026.4*

**Acceptance criteria**
1. Migrations assume the immediately preceding major version.

---

## 6. Conformance testing

**LIB-TEST-001** — The library SHALL ship a conformance suite a host can run against
its own configuration, as the `Janus.Conformance` package (`08` CONV-LAYOUT-001).

*Source: P-003, AUTHZ-TEST-001, D-149, D-166*

The suite answers a report whose findings each carry the check and a `10` section 1 code
with structured data; it writes no sentence (CONV-CONTENT-001).

**Acceptance criteria**
1. The suite verifies every entity the host's context maps has a registered policy as
   AUTHZ-GATE-001 AC3 defines it, reporting each other entity under
   `authz.policy.unregistered` with `details.entity`.
2. It runs the host's truth table, stated as cases over the library's scenarios, through
   both check and filter and asserts agreement; a derived scenario runs once for each
   derivation its type declares, and a finding on one names the relationship
   (`details.derivation`).
3. It validates the model declaration as startup does and reports each kind of failure
   under its own `10` section 1.5 code, stopping at the first; a refusal the model raises
   without a code fails the suite as the startup failure.
4. It asks the deployment's provider, as a registered client and at the endpoints its
   discovery document names, for each form AUTH-OIDC-006 retires that needs no signed-in
   person, a pushed request naming a destination other than the client's registered one
   included, and reports each form admitted, and each retired form the document lists,
   under `auth.oidc.nonconformant`.

---

**LIB-TEST-002** — Contract tests SHALL guard every surface in LIB-API-001, failing
the build on an unintended change.

*Source: D-026.4, D-166*

**Acceptance criteria**
1. A release commit, one that changes a `PublicAPI.Shipped.txt`, fails the release gate
   unless its version section raises the major part where the shipped surface or a
   contract list lost or changed an entry since the previous release, and at least the
   minor part where one gained an entry. The contract lists are the configuration keys
   with type, scope and constraints, and the key families; the library-owned schema; the
   error codes with their status; the audit actions; the permissions; the endpoints with
   method, route, body members and statuses; and the browser profile's stage order.
2. Altering the ancestry closure structure fails the build.

---

## 7. Stability and the migration seam

**LIB-SEAM-001** — All permission evaluation SHALL pass through a single interface so
the implementation behind it can be replaced without changing call sites.

*Source: D-015, AUTHZ-SEAM-001*

**Acceptance criteria**
1. Replacing the implementation requires changes in one place.

---

**Recorded migration trigger.** Move away from in-library authorization when
permission queries are a top-three slow query and the optimisation ladder is
exhausted, **or** when reverse lookup over derived grants exceeds
`authz.reverselookup.budget` (default 2 seconds) for the administrative view at
production volume (AUTHZ-SEAM-001, R-A09) — not when the design feels complex.

*Source: D-015, D-125*

---

## 8. Reserved seams

Present in the contract, unused by any current feature. Each is minimal — something
a reasonable engineer would build anyway for clarity, that happens also to unblock a
deferred capability.

| Seam | Reserves | Source |
|---|---|---|
| Stable opaque subject identifier | Third-party clients, mobile | D-005 |
| Claims modelled as data | Third-party clients | D-005 |
| Session and credential kept separate | Native and protocol clients | D-005 |
| A place for consent in the model | Third-party consent screens | D-005 |
| Acting and effective identity | Impersonation | D-014 |
| Factors registered by property | Additional factors | D-012 |
| Organization as container, membership as group | Further organizations | D-001 |
| Derived grants | Delegation, approval workflows, access reviews, context conditions | D-043 |
| Multiple memberships permitted structurally | Cross-organization access | D-003 |

**LIB-SEAM-002** — Reserved seams SHALL NOT acquire **features or user interface**
until the corresponding capability is in scope.

A configuration key **is** permitted where the capability is built and disabled rather
than absent — multiple organization memberships and the recovery approver count are
both built, both defaulted off or to one, and both switchable without a deploy. A key
for something not built at all remains prohibited.

*Corrected: the earlier wording forbade configuration outright and two existing keys
contradicted it.*

*Source: P-002*

**Acceptance criteria**
1. No configuration key exists for a capability that is not built; a built-but-
   disabled capability's key defaults to off (D-135).
2. No feature reads acting and effective identity as differing.

---

## 9. Open items

None. Cross-references: behaviour in documents 01 to 05; deployment and configuration
in `06-operations`; coding conventions in `08-engineering-conventions`.
