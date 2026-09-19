# Changelog

All notable changes to this project are documented in this file.

The format follows [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/),
and the project follows [Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html)
against the public contract of LIB-API-001.

## [Unreleased]

### Added

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

### Changed

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

- A per-organization configuration key is accepted whatever the organization
  identifier begins with. A key such as `policy.<organization>` was refused whenever
  the identifier began with a digit, which is about half of them.

### Removed

- `alerting.destinationchange.notify`. The notice to the previous destinations is
  not suppressible, so no switch for it exists.
