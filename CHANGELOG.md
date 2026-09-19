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
