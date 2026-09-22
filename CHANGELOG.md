# Changelog

All notable changes to this project are documented in this file.

The format follows [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/),
and the project follows [Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html)
against the public contract of LIB-API-001.

## [Unreleased]

### Added

- Startup now refuses a declaration whose encrypted field names no subject column, a
  column the declared type does not hold, or one holding something that is not a
  subject. Ciphertext an erasure could not reach stops the deployment instead of
  reaching production.

- Startup now refuses a deployment whose declared data categories have no retention
  period, naming the `retention.<category>` key nobody set, and one that is open to
  minors (`registration.adultaffirmation` off) without a written-consent lawful basis
  to hold a child's data under.

- The audit trail now answers "who was affected" after an erasure. Reading one
  subject's records goes through the index that carries the subject, and a record
  whose subject key has been destroyed comes back anonymised (what happened, when, to
  whom by opaque identifier) instead of failing the whole read.

- Records of processing are now generated rather than kept. `GET /admin/ropa?format=template`
  answers with the regulator's template: a row a declared purpose carrying its data and
  subject categories, its lawful basis, the non-sensitive, sensitive and children's
  columns, the retention of each category longest first, the recipients, the disposal
  measures, the roles holding a permission that serves it, and the technical security
  measures, beside the hosting environment, location and cross-border basis the
  deployment configured. A purpose added to the model is in the next register with no
  separate edit, and nothing of the inventory is stored. The three cells no query can
  answer (the data owner, the organisational security measures and the assessment
  links) are stated through `PUT /admin/compliance/assessments` and flagged until they
  are; so are a purpose missing an assessment its basis requires, a processor missing
  an agreement reference, and a data category the deployment named no retention for.
  Recipients are declared on the model builder, and `ProviderRegister.Default` ships
  the rows of the provider register to edit rather than write.

- An account can now take a copy of what is held about it. `GET /privacy/export`
  answers in two arrangements of one assembly: `format=human` is grouped and labelled
  for reading, `format=machine` is one flat object whose names are stable across
  exports, and both carry the same data. The export covers the account's standing,
  its profile, every identifier with its role and verification state, the backup
  settings, the preferences in force, the live sessions with the locations resolved at
  sign-in and at last use, and the consent and objection records. It is gated at the
  account's own reachable assurance, limited to `privacy.export.ratelimit` a rolling
  day (the refusal carries `Retry-After` and the instant the limit lifts), and it
  raises `ExportRequested` so that each host can produce its own half.

- A deletion grace window that runs out is now carried through: the sweep erases every
  account whose window elapsed without a cancellation, in one transaction per account,
  and puts the erasure on the outbox in the same transaction. The subject identifier
  stays, the personal fields go with the subject's key, the audit trail and the
  deployment's own records are untouched, and the username is held for
  `retention.consent` before anyone can claim it.

- An account now takes itself down and puts itself back up. `POST /account/deactivate`
  suspends it with `suspendedBy = self`, ends every session it holds, and sends the
  deactivation notice with the link `POST /account/reactivate` consumes; an account an
  administrator suspended answers `identity.account.adminsuspended` instead, and only
  an administrator stands it up. `POST /account/delete` starts the grace window
  (`account.deletion.grace`), answers with when the erasure runs, ends every session,
  and sends the deletion notice with the link `POST /account/delete/cancel` consumes
  anywhere inside the window; afterwards it answers
  `identity.deletion.windowelapsed`, and a deletion the deployment began as a takedown
  answers `identity.takedown.active`. Both requests require step-up.

- The outbox worker now publishes erasure, restriction and export to every handler a
  host registers, keeps each confirmation on the delivery's own row, and closes the
  delivery only when every required handler has confirmed. A handler that refuses, or
  whose own store fails it, is offered the event again on an exponentially growing
  delay with full jitter (`outbox.retry.initial`, `outbox.retry.factor`) until
  `outbox.retry.maxattempts` is spent, at which point the delivery is marked failed
  and the exhaustion is alerted; an operator who has done the work by hand closes it,
  and the erasure's own row is closed with it. A handler already confirmed is never
  offered the event twice, and a retry carries the key the first attempt carried.

- A host now declares which of its resource types each subject-event handler does the
  work for, and which purposes each consent and objection handler covers. A
  deployment that declares a resource type sensitive, or a purpose on an objectable
  basis, and registers nothing that names it does not start: the failure is
  `model.startup.declarationmissing` with `details.handler` naming what is missing.

- A data subject request now enters a queue with a statutory clock on it. A subject
  submits a restriction or a rectification for themselves at `POST /privacy/requests`
  and is answered with the request identifier, the receipt timestamp and the date the
  decision is due by; an authorised human enters a request that arrived out of band at
  `POST /admin/privacy/requests`, recording how it arrived, what confirmed the
  requester is the subject, and the date it reached the company. The deadline is six
  working days counted on the deployment's own week (`privacy.workingdays`), its
  holidays as currently listed (`privacy.holidays`) and its zone
  (`privacy.calendar.timezone`), never on a Monday to Friday assumption. Undecided
  requests raise a Normal alert `privacy.request.warninglead` before the deadline and
  a High alert on the deadline day, without anyone watching; a restriction still
  undecided when the deadline passes is granted and the account is restricted, and a
  request the system cannot grant by itself is recorded as deemed refused by lapse,
  with the subject told honestly and the record kept. Fulfilling a restriction
  restricts the account and tells the registered subscribers; fulfilling an
  out-of-band erasure starts the deletion grace window.

- A host can now bind one of its actions to the purpose it is done for, and where
  that purpose rests on consent the gate refuses the action until the subject has
  consented to it: missing, withdrawn, superseded or of the ordinary kind where the
  written one is required, each answered by the code that names what is wanted. The
  capability carries `consent` as something the action still requires, so a control
  prompts rather than failing silently. Consent gates the purpose and not the record,
  so an action on the same record done for a purpose resting on another basis is
  untouched.
- The terms step of registration now records one consent per control the person
  ticked, naming the purpose, the version of the notice presented and the registration
  mechanism. A control left unticked records nothing and holds nothing up.

- A subject can now read and change their own consents and objections through a
  privacy dashboard: `GET /privacy/consents`, `POST /privacy/consents/{purpose}/grant`
  and `.../withdraw`, `GET /privacy/objections`, `POST /privacy/objections/{purpose}`
  and `DELETE /privacy/objections/{purpose}`. Each record names the purpose, the
  version of the privacy notice that was shown, where the decision was made and when.
  Withdrawal takes the one request granting took and nothing stands in its way. A
  purpose that rests on a basis other than consent takes no consent record, and one
  whose basis carries no right to object refuses the objection by name. Publishing a
  materially revised privacy notice ends every live consent given against an earlier
  version, so the subject is asked again, and leaves the records standing as evidence.

- The privacy notice and every other legal document a deployment publishes are now
  served over `GET /privacy/notice` and `GET /privacy/documents/{document}`, public
  and without a sign-in, each answer carrying the governing language, the text that
  binds and every attached translation. A version is named in the query to read the
  text that was shown at the time.
- A deployment can now publish its legal documents through the library: the privacy
  notice, the terms of service and anything else it holds. Each version carries one
  governing language, defaulting to the one the deployment configured, and the text
  that binds in it. Translations attach to a published version and correct it without
  making a new one, and a read returns the governing text together with every
  translation so a screen can show either without changing the interface language. A
  version submitted without its governing text does not publish, and the condition is
  raised for an operator to see.
- The error catalogue now carries the privacy codes: a consent that is required,
  superseded or has to be written; a purpose whose basis carries no right to object; a
  document version submitted without its governing text; a duplicate request; a
  received date in the future; and an erasure that has not exhausted its retries. Each
  answers the status its endpoint states.
- Which capture path a consent runs through now follows from the lawful basis and the
  sensitivity of the type: a consent-based purpose over sensitive data, on a basis that
  requires it, takes the written path with nothing further to configure. A deployment
  may state the written path itself and never the ordinary one where the written one is
  required, which startup refuses.
- A declared purpose now carries the categories of data it requires and the categories
  of person it is about, and startup refuses a purpose that names no data category, so
  the declaration states what is collected rather than what happens to be held. A
  resource type declared sensitive in a category the deployment did not declare is
  refused at startup for the same reason.
- A deployment can now register a callback that says what its gateway knows about a
  phone number. Before a sign-in link or a second-step code goes to a number, the
  callback is asked and the answer is written to the audit trail against the factor it
  was asked for; where no callback is registered the absence is recorded instead. The
  send goes either way.
- A second-factor security key offered for upgrade when it is not one now answers with
  its own code rather than the one a refused factor answers with, and a credential of
  another account answers as one that does not exist.
- Registration now resolves the client identifier it is given against the registry as
  it takes it, and what a completed registration reports as the return is the address
  that client registered. An identifier the registry does not hold registers the person
  exactly as a registered one does and leaves the return to the deployment's own
  default; no step after the first takes a destination at all.

- A deployment that cannot reach its secrets manager now stops as it starts, with the
  code that says which of the two values was not there, rather than failing at the
  first request that would have read a person's field. A fingerprint key shorter than
  the hash it computes is refused as an absent one is.
- What has expired is now removable without anyone's attention: a session past its
  absolute expiry, an authorization code past its lifetime, a refresh token past the
  expiry its session gave it and a signing key past its overlap each go in one call.
- The deployment is now an OpenID Connect provider for the clients it registers
  itself: it advertises what it answers, publishes the keys a relying party validates
  against, hands a browser that already holds a session a code without asking anyone
  anything, and exchanges that code over the back channel for tokens signed with a key
  that rotates on its own. A client registered as a browser application is handed no
  refresh token; a protocol client is handed one that rotates on use, and presenting a
  spent one ends the session everything stood on. A destination that is not the
  client's registered one is replaced by it rather than refused. The token and
  userinfo routes are carried on a second pipeline profile that reads no cookie and
  asks for no synchronizer token, and refuses a request that arrives with one.
- The library's schema gains the tables the OpenID Connect provider keeps its
  registered clients, its authorization codes, its refresh-token families and its
  signing keys in, so a deployment applies one new migration. A code and a refresh
  token are held as what they hash to and never as themselves, and a signing key's
  private half is wrapped under the deployment's key-encryption key.
- Every message the library sends now goes down one path, and the named restrictions
  decide whether it goes: a fourth text message to one number inside a day is refused
  with the time the restriction lifts, a message a transport would not take is not
  counted against anything, and a security notice to the holder of an existing address
  is not held back by a destination whose allowance is gone. Support can add credit to
  one exhausted key, and a restriction can be created, changed or deleted while the
  deployment runs; both need step-up, a loosening needs a written reason, and both are
  audited without the key ever being written down.
- Every authentication, registration and recovery attempt now waits out a progressive
  delay that grows with the failures counted against the source, the account and the
  identifier, and the delay is the same whether or not an account holds the identifier
  that was typed. An address no account holds is told so once per window, in a message
  that names no one, and a registration from a datacenter range or from a source that
  has just made many attempts is put behind the deployment's own challenge where one is
  registered.
- The operator is now alerted when the conditions of the operations chapter fire, once
  per sustained attack rather than once per attempt, by email and, for the severe ones
  or where email reached nobody, by text message. Changing where those alerts go tells
  the previous destinations first.
- A text message is refused before it is sent when the gateway balance is at the floor,
  alerts excepted, and a balance that is draining faster than it has been raises its own
  alert.
- The library's schema gains the tables the sending restrictions, the progressive
  delay, the alerting and the delivery reports are kept in, so a deployment applies
  one new migration. No plain address, account or source is in any of them: each is
  held under the deployment's fingerprint key.
- A deployment whose catalogue is missing a message in a configured language, whose
  text message is longer than one message in it, whose restriction names a key
  supplier nothing supplies, or that declares an endpoint reached over plain HTTP now
  fails to start, rather than at the moment someone is waiting for a code.

- The message catalogue, the mail and text transports, the addresses the deployment
  calls out to, the recipients its data reaches and the keys a sending restriction
  counts under are now the host's to declare. A deployment that has declared no
  catalogue cannot answer in any language and does not start; the rest are optional,
  and a deployment that declares none of them starts.

- Registering the library now registers the sessions, passwords, factors, trusted
  browsers and policies of the authentication chapter as well, so a host resolves
  them from its own container. Passing the new-device check is announced as
  `DeviceVerified`, carrying the browser and nothing about the person.

- The audit entry for a restriction change now carries what the restriction was and
  what it became, so an operator reading the trail sees the change and not only that
  one was made.

- Every runtime-changeable configuration key is now read from the library's own
  `settings` table, so a value changed anywhere in the deployment is in force for the
  next read of it without a restart. A key the deployment never wrote reads as its
  default, a stored value a tightened floor or ceiling no longer admits comes back as
  a failure naming the constraint, and a key the application may not change is
  refused whatever the caller asks.

- The organizations a principal belongs to now are read from the database, so a
  policy resolves against live memberships and not against ended ones.
- Erasing a subject ends every session they hold before the key their fields are
  under is destroyed, so no request survives on a session whose account is gone.
- The browser-facing pipeline is mounted with one call and protects whatever the
  host mounts after it: a cross-site state change, a state change without the custom
  request header, one claiming another origin, and one whose synchronizer token is
  absent or belongs to another session are each refused before any endpoint runs, and
  no key, attribute or route excludes an endpoint from any of it. The session and its
  token are set in two cookies whose attributes no configuration can weaken, strict
  for the management application and lax elsewhere.
- Every session carries a synchronizer token of its own, bound to that session and
  to no other, and reissued whenever the session's secret is. Neither value is ever
  read back: the record holds only what each fingerprints to.
- A password is screened against the compromised-password corpus over the range
  API, which is sent five characters of a hash and never the password or the whole
  hash. Where the service cannot answer the deployment's offline list answers and
  the fall back is logged; where neither can, the password is refused rather than
  accepted unscreened, and so is one judged against a corpus older than the
  deployment admits. Naming the self-hosted corpus is the whole of switching to it.
- Sessions, passwords, enrolled credentials, recovery codes and known browsers
  are stored in PostgreSQL. A session's record carries what it reached and the
  fingerprint of its cookie, never the cookie, and where it was used from is held
  under the person's key, so a database dump yields no location and erasure leaves
  none readable. A code generator's shared secret is held under the same key, and
  a recovery code is stored as a password is.
- A second step is second to a password: a code generator or a security key under
  two-step is refused to an account that holds none, and an account signing in with a
  passkey alone is offered no second step to enrol. A set of recovery codes is not a
  second step and stays available either way.
- A password is set and presented through one path: the length floor that the
  account's reachable assurance decides, the screening that never silently skips, the
  hashing, and the silent rehash on the next sign-in after the parameters are raised.
  A password that matches one of the person's own words only after it was set carries
  a prompt to change into the sign-in that completes.
- Raising the assurance floor or the redundancy requirement of a policy gives the
  accounts already under it a run-up: their sign-ins carry the new requirement and
  the date it falls due and continue until then, and stop at enrolment afterwards. A
  run-up of nothing holds them at once, and an account created after the change was
  created under the new requirement and is held at its first sign-in.
- The relying party a passkey is bound to is settled when the deployment starts, not
  at the first enrolment: an identifier that does not sit over every configured
  origin stops the deployment, and one left unset is derived as the parent domain the
  origins share rather than taken from the first of them. The related-origins
  document lists exactly the additional origins configured, and a set of them wider
  than a browser reads stops the deployment too.
- A browser can be trusted after a two-factor sign-in, which spares it the second
  factor and nothing else: the session that follows records only the password, the
  offer is absent where the policy requires two factors or the password is too short
  to stand alone, and the trust goes when it lapses, when the account signs out
  everywhere, when the person removes it from their device list, or after enough
  failed sign-ins on it in a row.
- A sign-in to an account that can reach only one factor, from a browser the account
  has not seen, is held until the person enters a code sent to their primary email;
  the browser is then remembered and not held again for as long as the deployment
  says. A sign-in that reached two factors is never held, and the check can be turned
  off.
- A step-up gate is answered from the session record and what the account can reach
  with the credentials it holds, and never from a list of what it has enrolled. Where
  the session falls short, every combination of the account's own factors that would
  reach the gate is offered and the person chooses among them; where none would, the
  answer is to enrol, to report the loss, or that the loss report already made
  completes at a stated time. A reported loss lowers what an account reaches only
  once its window has run, an emergency session passes every gate while it lasts, and
  enrolling a credential costs the lower of what the account reaches and what the new
  credential itself would contribute.
- A WebAuthn credential records the relying party it was created under, whether it
  may be synced and whether it currently is, so a credential left behind by a
  configuration change is found from the account's own record rather than at a
  sign-in that fails without explanation. Enrolment refuses an algorithm outside the
  configured allow-list and a ceremony that verified nobody, asks for no attestation,
  and a signature counter that fails to advance is refused and audited as the cloned
  credential it indicates. A second-factor security key can be re-registered as a
  passkey, which retires the entry it came from.
- Recovery codes are issued ten at a time, each ten symbols of an alphabet that omits
  the letters a reader confuses with digits, and are read back leniently: case,
  spacing and those confusions make no difference to whether a code is accepted. Only
  hashes are stored, a code is spent on first use, and generating a set retires the
  previous one whole. The account shows how many remain and its owner is reminded
  once when a set has gone a long time untouched.
- Time-based codes run on thirty-second steps at six digits, with a drift tolerance
  the deployment sets and no code accepted twice: a code whose step has been spent is
  refused as replayed, so an observer has no window to reuse one in. An enrolment
  becomes usable only once a valid code has been presented, so a mis-scanned secret
  locks nobody out, and an enrolment abandoned before that leaves nothing behind.
- A session is a server-side record every credential derives from, holding the
  properties an authentication reached and never the factor names that reached them.
  Ending the record ends the per-application sessions and the tokens standing on it.
  The browser carries thirty-two drawn bytes and the row holds their fingerprint, so a
  dump of the table yields no usable session.
- Session lifetimes follow the assurance the principal's policy requires and never the
  level a particular sign-in happened to reach, so a customer who signs in with a
  passkey keeps the customer lifetimes. A new secret is issued whenever a combination
  is presented and on any privilege change, and the one before it stops working.
- After an inactivity expiry inside the absolute window, a policy that requires two
  factors accepts one factor bound to the session secret the browser still holds. The
  allowance is not offered under the system policy, and a second factor alone, a
  social credential and an email factor each restore nothing.
- Authentication policy is resolved from the principal's organization membership and
  from nothing else: the system policy where there is no membership, the
  organization's where there is one, and the strictest of several where a principal
  belongs to more than one organization. An organization may tighten any field and a
  value that would loosen one below the system default has no effect, whenever it was
  written.
- A sign-in link, whichever channel carries it, and an emailed code sign a person in
  at AAL1 and count for nothing afterwards: neither is a second step, neither passes a
  step-up gate, and neither restores a session that lapsed. The mailbox or the number
  behind them is also the recovery channel, so one compromise would otherwise yield
  both steps.
- Passwords are held under Argon2id at the parameters the deployment configures, with
  the parameters carried by each hash, so raising them leaves every stored password
  verifiable and marks it for a silent rehash. The floor is fifteen characters where
  the password could sign in alone and ten where it never could, and the shorter floor
  is reached by holding a second factor rather than by choosing it. There are no
  composition rules and no scheduled expiry.
- Every password is screened before it is accepted. Only a five-character hash prefix
  leaves the deployment and the range that comes back is matched locally, so neither
  the password nor its full hash is ever sent. A corpus that cannot answer falls back
  to the offline one and records the degradation; with nothing able to answer the
  operation is refused rather than accepting a password nothing screened.
- `ISessions` in `Janus.Core`: an account sees its live sessions with the time of
  sign-in, the time of last use, the device and a city-level location, ends one of
  them on its own, or signs out everywhere. An administrator ends one account's
  sessions, and the emergency operation ends every session in the deployment.
- `IAccessGate` in `Janus.Core`: the one place a permission is evaluated. A check and a
  list filter are the same rule rendered two ways, an expression a host composes into
  its own LINQ query and a parameterised PostgreSQL fragment a hand-written query
  composes into its `WHERE` clause, so a list screen cannot come to show what a check
  would refuse. Neither rendering enumerates permitted records.
- `MapJanusAuthorization` in `Janus.Hosting`: a host maps the ancestry closure and the
  effective grants into its own context, so a filtered listing is one query against its
  own tables and the library reads nothing of the host's.
- `AddJanus` in `Janus.Hosting`: the one method a host calls to register the library.
  What the host declares about its own domain is built and checked here, at startup.
- The gate explains itself: an explanation names the grant that decided, the container
  it was inherited from, and the principal it was decided for, or states that no grant
  matched. Capabilities for a page of records are computed in one query.
- A refusal on one record answers as a record that does not exist unless the type says
  otherwise, and a type says so in one place for every record of it. A type that
  conceals has no self-service explanation, because saying that no grant matched says
  that the record is there.
- Every refusal carries a correlation identifier, which is the audit row it was
  recorded as. A role holding `audit:read` resolves it to the permission and the
  principal; it says nothing about whether the record exists. A permission that names
  no record is refused as a permission the caller does not hold, with nothing concealed.
- Inheritance is resolved through an ancestry closure maintained in the same
  transaction as the create or the move that changes it, so a permission query joins
  one table rather than walking the tree, and permission data and business data cannot
  diverge. Moving a record carries everything beneath it.
- Groups nest to any depth, and the groups a principal belongs to are read once per
  request from a closure maintained beside the memberships. Adding or removing a
  member, or writing a grant to a group, raises the counter of every account it
  reaches, in the same transaction.
- `AncestryEntry` and `EffectiveGrant` in `Janus.Core`: the two rows a host maps into
  its own context so that a permission filter is one query against its own tables.
- A grant is one sentence, subject has role on resource, and one row carries every kind
  of it: an account's and a group's, an allow and a deny, a grant on one record and a
  grant on a whole organization. What a role allows is read where a grant naming it is
  evaluated, so editing a role takes effect at once.
- Every grant records who granted it, when and why, and a revocation records the same.
  A grant or a revocation stating no reason is refused with `authz.grant.reasonrequired`.
  An expired grant confers nothing at the instant it is read, whether or not a sweep
  has run.
- A check and a capability page take the same host-supplied rows the filter takes, so a
  type whose access follows in part from a fact in the host's own data answers the same
  way whichever of them is asked. Asked without those rows, they refuse with
  `authz.derivation.sourcesmissing`, a fault and not a denial, rather than answering from
  the stored grants alone.
- A derivation confers a role from a fact in the host's own data. The host declares the
  relationship and hands its rows to the filter beside the ancestry and the grants, and
  a listing then reaches everything that fact reaches, on the record or on anything
  containing it, with no grant written and nothing to keep in sync. Removing the fact
  removes the access on the next request, and a deny defeats a derived grant as it
  defeats a written one.
- `IDerivationMaterialiser` in `Janus.Core`: a derivation a deployment declares
  materialised is precomputed into ordinary grant rows, marked as such wherever they
  are read. The host refreshes it from the operation that changes the relationship,
  inside the same unit of work, so the fact and the rows computed from it are written
  together or not at all. A refresh run later reports what it had to change and
  corrects it in the same run, which is how drift is found where the refresh was
  missed. `derivation.materialised.driftcheck` sets how often that runs.
- `AccessContext` in `Janus.Core`: who is acting, whom they are acting for, and the
  named principal a background job runs as.
- `GrantId`, `GroupId`, `GrantSubject` and `ResourceReference` in `Janus.Core`: what a
  grant, a group and one of the host's records are named by. A record's identifier is
  the host's own text, so an integer, a UUID or a code all serve.
- `AuthorizationDeclarationBuilder` in `Janus.Core`: a host declares its own kinds of
  thing, their containment, what they are processed for, which fields are encrypted and
  under whose key, and the relationships in its own data that confer a role. Every
  reference to one of the host's fields is an expression the compiler checks.
- The authorization model is built and checked once, at startup: a containment cycle, a
  reference to a type that was never declared, a type that reaches no organization, a
  type with no purpose, a purpose whose basis needs an assessment and names none, and a
  derivation from a relationship that was never declared each stop the deployment with
  their own code.
- The two checks the declaration alone cannot decide run as the deployment starts and
  before it serves a request: a role someone wrote allowing a permission the model does
  not declare, and a derivation naming a column no index reaches, each stop the process
  with their own code.
- The built model is written to `model.json` in one order, so two runs of one
  configuration produce the same bytes and a change to the model is a diff in review.
- `Permission` and `Permissions` in `Janus.Core`: a permission is a lowercase
  `resource:action` that cannot be constructed in another shape, and the library's own
  twenty-two are listed where a host can read them.
- `SubjectType`, `GrantKind` and `ConcealmentBehaviour` in `Janus.Core`: what a grant
  is held by, where it came from, and what a denial on a record discloses.
- The authorization error codes: a denial, the four grant refusals, a group that would
  contain itself, an entity with no registered policy, a restricted subject, and the
  three model validations a startup fails on.
- `JAN0006`: a caught exception that is neither handled nor reported now fails the
  build.
- `Result`, `Result<T>`, `Error` and `ErrorCode` in `Janus.Core`: an expected outcome
  is handled through `Match` or `Switch` and carries a code from the catalogue.
- `NeverLoggedAttribute` in `Janus.Core`: a value marked with it cannot be passed to
  a logging call.
- `Settings` in `Janus.Core.Configuration`: every configuration key with its type,
  its default, the floors, ceilings and value sets it admits, and the two rules that
  hold over a pair of keys rather than over one.
- `IConfigurationStore` in `Janus.Core.Configuration`: the keys that change at runtime
  are read through it, so a change made through the management application takes
  effect without a restart.
- `StartupException` in `Janus.Core`: a deployment that omits a value it has to name,
  or names one outside its key's bounds, now fails at startup rather than at the first
  request that needs the value.
- `Policy`, `Policies` and the step-up, factor and assurance vocabularies in
  `Janus.Core`: the policy a principal resolves to, with the system and
  administrative defaults. A policy refuses an assurance floor outside `aal1` and
  `aal2`, and refuses the emergency credential as a login factor.
- `config.value.notallowed`: a configuration value outside its key's set, or of the
  wrong type, is now refused with its own code.
- The rest of the error catalogue: a missing deployment declaration, a model
  reference to something the host never declared, the three preference refusals, a
  required challenge, a grant with no reason, the last alert destination, and an
  unhandled fault.
- `CapabilityResidual`, `ConsentMechanism`, `AgeGroup`, `AlertCondition` and
  `BotDefenceSignal` in `Janus.Core`: what still stands between a principal and an
  action, where a consent record was made, what the age screen recorded, which
  condition raised an alert, and what bot defence counts.
- The rest of the configuration keys of `10` section 4: the alert thresholds, the
  per-source flood limit, the outbox schedule and retry, the sweep interval, the
  message languages and sending domain, the calendar time zone, the domain
  re-verification interval, the location database cadence, the restore-test
  objective and interval, the two identifier maximums, the two size caps and the
  bot-defence signal set.
- `SubjectId` and `OrganizationId` in `Janus.Core`: an account's opaque identifier,
  drawn from randomness alone so that it carries nothing about the person, and the
  organization's, ordered by the instant it was issued.
- `AccountState`, `SuspensionOrigin`, `DeletionOrigin`, `TakedownTrigger`,
  `ErasureStatus` and `ErasureReason` in `Janus.Core`: the state an account is in,
  why it entered the one it is in, and how far an erasure's host-side work has got.
- `ISecretSource` and `KeyEncryptionKeys` in `Janus.Core`: the host supplies the
  key-encryption key, the fingerprint key and the maintenance credential, and the
  library ships no secrets-manager client and no default.
- `IUnitOfWork` in `Janus.Core`: an operation runs in one transaction and commits
  once, so a failure part way through leaves nothing written.
- Per-subject encryption of personal fields: one data key per subject, wrapped in
  the database under the deployment's key-encryption key, with every value bound to
  the subject, table and column it was written to, so a value moved elsewhere no
  longer decrypts. Erasure overwrites the wrapped key and everything encrypted
  under it becomes unreadable at once, including values in the host's own tables.
- Keyed fingerprints for searchable identifiers, computed under a key held outside
  the database, neutralised by erasure and never matched once neutralised.
- Account states and the transitions between them: an account is created active,
  deactivated by its owner or suspended by an administrator, restricted at the
  subject's request, and removed only through a grace window it can be brought
  back from. A takedown passes through suspension into that window in one step
  and is reversed, never cancelled. The record itself is never deleted, so an
  audit trail keeps resolving after the personal data is gone.
- The library's own database schema and its first migration: one schema the
  library owns, with a migration history table of its own so a host's migrations
  never collide with it, a case-insensitive ICU collation for the columns
  identifiers are compared under, and every fixed vocabulary stored as the
  spelling it carries on the wire and constrained by a check rather than a
  native enum type, so the admitted set changes without a locking migration.
- The transaction an operation runs in, and the one accessor hand-written SQL
  takes its connection from: a query written by hand runs on the same connection
  and inside the same transaction as the rest of the operation, so it can never
  miss a write the operation has already made, and an operation that fails part
  way through leaves nothing behind.
- The package now carries its own Unicode tables, at a pinned version. Composition,
  decomposition, case folding, the PRECIS properties and the script data all come
  from those tables rather than from whichever library the machine happens to have,
  so a canonical form computed on one host is the canonical form computed on every
  other, and the version the fingerprints were derived under is recorded.
- `CanonicalForm`, `Precis` and `ScriptMixing` in `Janus.Core`: the canonical form an
  identifier is stored and compared under, the digit mapping a phone number takes
  instead, the two PRECIS profiles a username and a display name must satisfy, and the
  mixed-script rule that holds within a word. Two addresses that differ only in how
  they are composed, in width or in case are one account, and a word that mixes
  scripts is refused while whole-word Arabic beside whole-word Latin is not.
- The library's schema now carries a settings table. A runtime-changeable
  configuration value lives there rather than in a file, so a change made through the
  management application takes effect without a restart, and a key the deployment
  never changed keeps the default the catalogue gives it.
- `IdentifierKind`, `IdentifierId`, `EmailAddress`, `PhoneNumber` and `Username` in
  `Janus.Core`: the three kinds of identifier an account holds, and the forms each is
  stored and compared under. An address, a number or a username that the rules of its
  kind do not admit cannot be constructed, so it never reaches a row.
- An account's identifiers are modelled: each one keeps both the form the person
  entered and the form it is compared under, exactly one identifier of a kind is
  primary once the account has a verified one of that kind, and each kind's backup
  setting decides who a security notice reaches beyond the primary.
- An account and its per-subject data key are now written and read back through
  ports of their own, so the account a caller holds carries the transitions and the
  row carries the columns, and neither knows the other's shape.
- The schema now carries an account's identifiers and each kind's backup setting.
  Both forms of an identifier are encrypted under the subject's own key, what is
  looked up is the keyed fingerprint of the canonical form, and the Unicode version
  that form was computed under is recorded beside it. One live fingerprint of a kind
  exists across the deployment, so an identifier belongs to at most one account;
  erasure neutralises the fingerprint and leaves the row, and a neutralised one is
  outside that rule.
- `IdentifierKinds` in `Janus.Core`: one field takes every identifier and the kind is
  read from the value. An address carries the sign, a number is digits once the
  separators a person writes are taken out, in whichever script they were typed, and
  anything else is a username where the deployment admits one. A username now holds at
  least one letter, so that no value is both a number and a username, and an all-digit
  choice is refused with `identity.username.invalid`.
- A subject key now carries its re-wrapping to the row. A rotation of the
  key-encryption key changes the wrapping and no stored value, and an erasure of the
  key leaves every field written under it unreadable wherever that field is held.
- An account's identifiers are now written and read back through a port of their own.
  Both forms are encrypted under the subject's own key, the canonical form is
  fingerprinted under the deployment's fingerprint key, and a lookup by value matches
  on that fingerprint, so an address entered in another casing finds the account that
  already holds it and a neutralised fingerprint finds nobody. Reading an account's
  identifiers unwraps its key once however many columns it decrypts, and reading them
  after erasure refuses rather than yielding anything.
- `DisplayName` and `LegalName` in `Janus.Core`: the two names of a profile, each in
  the form it is held in. A display name takes the Nickname profile and is bounded in
  bytes; a legal name takes Normalization Form C and is bounded in scalar values; both
  are of one script per word.
- The schema now carries an account's profile, and the profile is written and read
  back through a port of its own. The display name, the legal name and the date of
  birth are each held under the subject's own key, so a dump yields none of them and
  erasure leaves none of them readable; a field the account gives up clears its column,
  and a field it did not touch is not written again.
- An account's photo is now held in a table and a port of its own, under the subject's
  own key. Nothing that reads an account reads image bytes, a dump yields no
  photograph, and erasure of the key leaves the image unrecoverable.
- An account carries a language, a time zone and the values of the preference keys the
  host declares at startup. A declaration names a type, a default and whether only an
  administrator may set the key; a malformed one fails startup with
  `model.startup.preferencedeclaration`. The declared values are stored under the
  subject key as one document, capped by `preferences.maxsize`; the language and the
  time zone are not, so a notice still reaches an erased account in a language it
  reads.
- Organizations and memberships. An organization is an entity in the one identity
  pool and no isolation boundary; its deletion suspends it at once, runs for
  `organization.deletion.grace` and is cancellable until the window closes, after
  which the row stays and the identifier goes on resolving. A membership is a record
  of its own that ends without touching either side, and an account may hold more
  than one. Nothing in the schema separates staff from customers.
- An audit trail. Every record names who acted, whose identity the action was taken
  under, the instant it occurred and the organization where one applies; an event about
  a principal holding no membership carries none, and the absence is the recorded fact.
  What happened is a code and never a sentence. The table is partitioned by retention
  category and then by calendar month, three months kept open ahead by
  `audit_ensure_partitions`, and an attribute an event must record is held under the
  subject's own key, so erasure reaches it without a row being touched.
- Named principals for background jobs, imports and webhooks. One acts for a single
  organization and reaches no other; the other exists for pool-wide work, runs only
  the operations it names and acts for nobody. Neither can be constructed without a
  stated reason.
- The erasure, as one operation and one transaction. The account reaches `deleted`,
  the subject's wrapped key is overwritten with the irreversible value, its
  fingerprints are neutralised, and an erasures row records why and how far the
  host-side work has got. The photo row stays where it is and its bytes stop being
  readable with everything else the key covered. A transaction that does not commit leaves no
  row and erases nothing; there is no third state. Erasure progress is on that row and
  on no column of the account, and every outstanding erasure is read in one query.
- The administrative organization is marked on its own row, set once when the
  deployment is bootstrapped and by nothing else, and the database holds the mark to
  exactly one organization. Requesting its deletion is refused with
  `identity.organization.protected`; every other organization takes the window as
  before.
- The three database roles the deployment attaches credentials to. `janus_migrate`
  owns the schema and is the only role that alters it, `janus_app` reads and writes
  rows, and `janus_maintenance` executes the two audit partition functions and reads
  and updates the wrapped keys. The migration creates the two runtime roles where they
  are absent and writes every grant, so an audit row cannot be updated or deleted by
  the application at all, and no credential that alters schema reaches the running
  system.
- `audit_drop_expired_partitions`: the scheduled job drops a month of one retention
  category once its end has passed that category's retention. The two retention
  periods are passed in, because a key left at its default has no stored row the
  database could read, and either below the floor its key carries is refused.
- A host binds one of its actions to a step-up gate in the model builder, and the gate
  is then read wherever the action is: a capability for it carries `stepup` beside what
  the grants confer, and a check of it is refused until the session satisfies the gate.
  A deployment that registers no assurance provider is refused with
  `auth.stepup.unavailable`, told apart from an ordinary denial.
- An account under a processing restriction keeps its reading actions and is refused
  every action that would change anything, with `authz.restricted`, in a check, a
  listing filter and a capability alike. `read`, `list` and `export` are reading by
  name, a host declares which of its own actions are reading, and everything else
  modifies.

- Registration is now served end to end. A browser that reaches the library is given a
  pre-authentication session, and the registration it starts is bound to that session
  and reachable from no other browser: the age screen, the email and phone steps, the
  confirm screen, the security step and the terms step, each refusing to run before the
  one before it has finished. An address or a number that already belongs to somebody
  else is answered exactly as a fresh one is, and its holder is told once that somebody
  tried. A registration that is abandoned leaves nothing behind.

- An identifier is verified by the code in the message or by pressing the link. The
  press verifies only in the browser that asked for the message; opened anywhere else
  the same request changes nothing and hands back the code to type, and a control there
  ends the attempt. Merely loading the link, which is what a mail scanner does, changes
  nothing at all. A waiting screen follows the state on a stream of server-sent events
  carrying exactly what the polling endpoint answers, so a frontend that loses the
  stream misses nothing.

- An account now reads and changes itself: its identifiers, its credentials and their
  labels, its profile, its preferences and its sessions. An identifier can be added up
  to the deployment's maximum, made primary, set as the backup destination, removed
  with an undo the remaining addresses are sent, and, where only one of a kind is
  allowed, replaced in one operation. A removed identifier stays out of reach of every
  other account until its undo window closes. The session list marks the one asking and
  says no more about where each was used than the city.

- A profile field the deployment has switched off is neither accepted from a request
  nor carried in an answer, and the date of birth is never the person's to change. A
  preference key the host never declared is refused and never returned. A username, once
  chosen, is held against every other account for the cooling-off period after it is
  given up, and after erasure for the same period.

- The library now serves `/.well-known/change-password`, `/.well-known/passkey-endpoints`
  and `/.well-known/webauthn` at the site root. The first two answer only where the host
  has declared the frontend pages behind them; the third is the deployment's own
  related-origin allowlist.

- The message catalogue is asked for one more kind. Where an account replaces its
  only address of a kind and holds no other channel at all, the address being
  displaced is asked to confirm the change, so a deployment declares a template for
  `identifier-change-confirm` in every language it configures or it does not start.

- A person can sign in. A sign-in is begun against an identifier and answered with the
  entries the deployment enables, never with what the account holds, so an identifier
  nobody holds answers as one somebody holds does. A password, a passkey, a security
  key, a generated code, a recovery code and a link or a code the library sends are
  each judged by the service that owns them, and the answer carries the assurance the
  attempt has reached, whether it is phishing-resistant, and what it still needs. A
  second step is asked for whenever the account holds one, whatever the policy floor
  is, and a device the account has trusted is remembered for as long as the policy
  allows.

- A sign-in that would complete at a single factor from a browser the account has not
  been seen on is held, a code goes to the account's primary address, and the sign-in
  completes when that code is typed. A passkey sign-in and a sign-in that already took
  two steps are never held this way.

- A sign-in link completes the sign-in in the browser that asked for it. Opened in any
  other browser it changes nothing and shows the code to type back where the sign-in
  was begun, and a link the account abandons is spent at once.

- An account that does not yet meet a requirement its organization raised is told the
  requirement and the deadline and signs in as it did before until the run-up ends;
  after it, the sign-in stops at enrolment. An account created after the raise is held
  at enrolment at its first sign-in, and lowering a requirement starts no run-up.

- The account lists the browsers it knows, the ones it trusts for the second step and
  the ones the new-device check remembers, and forgets any of them: a trusted browser
  is asked for the second step again, a remembered one faces the check again.

- The library's schema gains the tables a sign-in in flight, a link or code sent for
  one, and a requirement a policy raised are kept in, so a deployment applies one new
  migration. Neither the handle a browser carries nor the link it was sent is held as
  it was issued: each is kept as its fingerprint, and the code beside it is held under
  the account's own key, where an erasure leaves it unreadable.

- An account whose policy allows it can recover a forgotten password from a link sent
  to the address or number it holds, and an address no account holds is answered the
  same way as one that does. Completing the recovery sets the password, stands a
  self-suspended account back up and ends every session the account held, and it
  clears no second step: the account still passes one at the next sign-in.

- An account that can no longer be recovered by itself is re-enrolled by approvers,
  who must each write a reason, pass step-up, and confirm the person on a channel the
  account already holds; the number required is configurable, nobody can approve their
  own recovery, and an approver recovering many accounts, or many approvals of one
  account, is surfaced to the operator. The link the last approval sends is the only
  one that opens an enrolment session, and where the mailbox is the thing that was
  lost, that session may replace the address it is held on.

- The holder of a lost credential can report it, which refuses it from that instant
  without ending anything else the account can do, and invalidates it only after a
  window in which every notice sent carries a link that cancels the report. A window
  whose notices reached nobody holds the invalidation rather than completing it, an
  invalidation that leaves the account on a password alone requires that password to
  be changed at the next sign-in where it does not meet the single-factor floor, and
  one that takes the last second step takes the recovery codes with it.

- The library's schema gains the tables a recovery link, an approval standing behind a
  re-enrolment and a running loss report are kept in, and the passwords table gains the
  mark that the next sign-in has to set a new one, so a deployment applies one new
  migration. The link is held as its fingerprint; the channel an approver confirmed on
  and the token the loss notices carry are each held under the account's own key, where
  an erasure leaves them unreadable.

- The recovery endpoints answer: asking for a link, completing one, reporting a
  credential lost and cancelling that report, approving a re-enrolment, and opening
  the enrolment session an approved link stands for. That session is bound to the
  browser that opened the link exactly as a registration is, so nothing else reaches
  what it may do.

- A person can now manage their own credentials: setting or changing a password,
  enrolling a passkey or a security key against a challenge the server issued,
  upgrading a security key to one the authenticator keeps, enrolling a generator and
  confirming it with a code, taking a fresh set of recovery codes, and removing a
  credential. A second step is refused on an account that holds no password, a second
  step beside a password brings a set of recovery codes with it, an enrolment that
  leaves the account on one credential says whether a second is asked for or required,
  and a removal that would lower what the account reaches runs the notified window
  instead of taking effect at once. Each of these reaches every recorded channel, and
  each is gated at the lower of what the action asks for and what the account can
  reach. The enrolment session an approved link opens reaches the same operations
  without a session, and ends when the enrolment completes.

- The library's schema gains the table a key ceremony in flight is kept in, one row
  per account, so a deployment applies one new migration. The row holds the challenge
  the server issued, what it is upgrading where it upgrades anything, and when it
  stops answering.

- The credential endpoints answer: setting a password, opening and completing a key
  ceremony, upgrading a security key, enrolling and confirming a generator, taking a
  set of recovery codes, and removing a credential. Each answers to the session the
  browser holds or to the enrolment session an approved link opened, and to nothing
  else; which of the two it is, is what the request's own session resolution
  established and never what the request says.

- A customer whose mailbox is gone can move their account to a new address from the
  enrolment session an approver opened for them: the new address confirms alone, the
  displaced one is not asked, and the approver's confirmation on a channel the account
  already holds is what stands in its place. Everywhere else the rule is unchanged, so
  an address is still displaced only by a session that has stepped up, and still asks
  the old address where the account has no other channel at all. A deployment applies
  one further migration, which lets a staged verification record no browser.

### Changed

- An explanation can now be asked with the host's own rows, and on a type a derivation
  reaches it names the grant the fact produced: no identifier, the derived kind, the
  role the derivation confers, and the container it was inherited from. Asked without
  those rows such a type is refused rather than answered from the stored grants alone.
  The identifier an explained grant carries is optional for the same reason: a derived
  grant is a fact being true and no row holds it.
- A page of capabilities now costs one query over the host's own rows however many
  permissions it asks for. Every derivation reaching the type is evaluated in that one
  query, and what the role each confers allows is read from the model, so a page that
  offers three actions costs what a page offering one costs.
- A sign-in whose password an invalidation left below the single-factor floor now
  completes and says so, so the person is asked for a new password at the next
  sign-in rather than being locked out.
- The case-insensitive collation is created in the default schema, because a column
  names a collation by one identifier and cannot reach one held in another schema.
- A configuration key loosens the way its row states. Where a row states nothing, a
  key with only a ceiling loosens upward, a key with only a floor loosens downward,
  and a flag loosens away from its default, so a tightening no longer costs the
  friction a loosening does.
- A duration written in years or months is held at 366 days a year and 31 days a
  month, so a retention floor stated in years is never shorter than the calendar
  span it names.
- `ThrowIfIncomplete` now takes the screening sources and whether the records of
  processing are generated, because `service.name` and `hosting.environment` are
  named only by a deployment that uses them. A missing declaration now fails with
  `model.startup.declarationmissing` naming the key.

### Fixed

- A request that names an identifier kind (`email`, `phone`) is read. Adding an
  identifier to a registration or to an account, and setting a backup identifier, were
  answered as malformed requests whatever was sent.
- A send a restriction refused is answered 429 with the interval, as the API contract
  gives it, rather than 422.
- The interval a throttled answer carries is measured on the deployment's clock.
- A per-organization configuration key is accepted whatever the organization
  identifier begins with. A key such as `policy.<organization>` was refused whenever
  the identifier began with a digit, which is about half of them.

### Removed

- `alerting.destinationchange.notify`. The notice to the previous destinations is
  not suppressible, so no switch for it exists.
