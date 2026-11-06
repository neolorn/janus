# Round H19: a key the deployment names, before it is first set

## 1. Items implemented

**`06` OPS-CFG-008 and `09` `GET|PUT /admin/config/{key}` (D-277), 28659b39**

- `ConfigurationAdministration.Unwritten`: a read of a runtime key the deployment names
  (LIB-HOST-001) that answered `model.startup.declarationmissing` is the key never
  written, a state and not a fault. It is the one definition both routes read.
- `GET /admin/config/{key}` (`SettingReading`): such a key answers 200 with `value`
  null and `default` null, its `protected` and `direction` as for any read.
- `PUT /admin/config/{key}` (`ConfigurationAdministration.ChangeAsync` and
  `AllowedAsync`): the first value is written under the key's type, constraints and
  audit rules. Every key the deployment names has no direction, so any change of it
  loosens (D-079b), its first value included: the change asks `system:administer`,
  the `config:loosen` step-up and a reason, as any change of the key does. The audit
  entry names no value before it. The store's write answers what was in force before,
  which for such a key is the same never-written state, and that is no longer taken
  for a failure.
- The library's own reads of such a key are untouched: a required key's start check
  (LIB-HOST-001) and the authenticator app's issuer read stay as they were.
- Tests:
  - `NamedKeyTests.OPS_CFG_008_AKeyTheDeploymentNamesReadsNullAndTakesItsFirstValueAsync`,
    on PostgreSQL, over a deployment that names every key it needs but `service.name`:
    the key reads `value` and `default` null; a member of the administrative
    organization holding `config:read`, `config:manage` and `system:administer`, on a
    session that proved a phishing-resistant factor just now, sets it with a reason;
    it reads back as that value; the trail holds one `ops.configuration.changed` entry
    for it with `before` null, the value after, `loosening` true and the reason; an
    authenticator app's enrolment then carries the name in its address, as issuer and
    in its label;
  - `ConfigurationEndpointTests.OPS_CFG_008_AKeyTheDeploymentNamesReadsNullAndTakesItsFirstValueOverTheRouteAsync`:
    over HTTP, the same read, `PUT` answering 204, the read after it, the change
    written down, and `POST /account/factors/totp/begin` answering an address that
    names the deployment;
  - `ConfigurationEndpointTests.OPS_CFG_008_TheFirstValueOfAKeyTheDeploymentNamesIsALooseningAsync`:
    `config:manage` alone is refused 403 `authz.denied`, and the key stays unwritten;
  - with the change taken out, all three fail;
  - `StartupValidationTests.LIB_HOST_001_AC2_ScreeningAgainstTheContextWithoutTheServiceNameIsRefusedAsync`,
    unchanged, still passes: the start check is the same.
- `CHANGELOG.md`: one line under Unreleased, `Fixed`.

## 2. Items not implemented

None.

## 3. Resolved by rule

None.

## 4. Open questions

**Q1 (Tier 2).** A protected key the deployment names, never written, still answers
`GET /admin/config/{key}` with 500 `system.fault`.

- D-277 and OPS-CFG-008 as amended name a runtime key. `hosting.crossborderbasis` is a
  protected key the deployment names only where it hosts outside Egypt
  (`Settings.ThrowIfIncomplete`), so a deployment hosted inside never writes it, and its
  read through the route fails on `model.startup.declarationmissing`, as `service.name`'s
  did. A `PUT` of it is refused `config.key.protected` before any read, as OPS-CFG-004
  gives, so only the read is at issue.
- Built: the rule for runtime keys alone, as the decision is written. Nothing is parked.
- Reading A: the route answers such a protected key as it now answers a runtime one,
  `value` null. Smallest fix: drop the scope from `ConfigurationAdministration.Unwritten`
  for the read, and keep `PUT` refusing it as protected.
- Reading B: it stays as it is, the read of a protected key never written being out of
  the route's scope. Smallest fix: none.

## 5. Gate result

- Fast checks on every commit: green. These are the build with warnings as errors, the
  analysers, `dotnet format --verify-no-changes`, and the unit tests of the projects
  changed.
- Checks before the push: green. Every unit test passes (5225 in eleven projects), every
  contract test passes (214), and the commit-message, changelog, release, truth-table
  change, acceptance-criterion test name and forbidden-marker gates pass over
  `origin/main..HEAD`. The full-history secret scan finds nothing. `NamedKeyTests` and
  `StartupValidationTests` were run here against PostgreSQL.
- Gate of record: the pipeline's run on this report's commit, whose identifier and
  result the round's last message gives.
