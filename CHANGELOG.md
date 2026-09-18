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
  its default and the floors, ceilings and value sets it admits.
- `IConfigurationStore` in `Janus.Core.Configuration`: the keys that change at runtime
  are read through it, so a change made through the management application takes
  effect without a restart.
- `Policy`, `Policies` and the step-up, factor and assurance vocabularies in
  `Janus.Core`: the policy a principal resolves to, with the system and
  administrative defaults.
- `config.value.notallowed`: a configuration value outside its key's set, or of the
  wrong type, is now refused with its own code.
