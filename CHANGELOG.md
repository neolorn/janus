# Changelog

All notable changes to this project are documented in this file.

The format follows [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/),
and the project follows [Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html)
against the public contract of LIB-API-001.

## [Unreleased]

### Added

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

### Changed

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

### Removed

- `alerting.destinationchange.notify`. The notice to the previous destinations is
  not suppressible, so no switch for it exists.
