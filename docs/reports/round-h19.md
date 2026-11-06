# Round H19: a key the deployment names, before it is first set

## 1. Items implemented

**`06` OPS-CFG-008 and `09` `GET|PUT /admin/config/{key}` (D-277 and its amendment),
28659b39 and 53754fd7**

- `GET /admin/config/{key}` (`SettingReading`): a key the deployment names
  (LIB-HOST-001), runtime or protected, whose read answered
  `model.startup.declarationmissing` is the key never written, a state and not a fault.
  It answers 200 with `value` null and `default` null, its `protected` and `direction`
  as for any read. The protected case is the amendment's (53754fd7).
- The write's condition is its own (`ConfigurationAdministration.Unwritten`, private):
  a runtime key the deployment names alone. A protected key is refused
  `config.key.protected` before any read, as OPS-CFG-004 gives, and `configure` writes
  it.
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
  - `ConfigurationEndpointTests.OPS_CFG_008_AProtectedKeyTheDeploymentNamesReadsNullAndIsStillNotWrittenAsync`
    (53754fd7): over HTTP, `hosting.crossborderbasis`, never written, reads 200 with
    `value` and `default` null and `protected` true; its `PUT` is still refused 422
    `config.key.protected`, and it stays unwritten;
  - `StartupValidationTests.LIB_HOST_001_AC2_ScreeningAgainstTheContextWithoutTheServiceNameIsRefusedAsync`,
    unchanged, still passes: the start check is the same.
- `CHANGELOG.md`: two lines under Unreleased, `Fixed`: the runtime key's read and first
  write, and the protected key's read.

## 2. Items not implemented

None.

## 3. Resolved by rule

None.

## 4. Open questions

**Q1 (Tier 2).** A protected key the deployment names, never written, still answered
`GET /admin/config/{key}` with 500 `system.fault`.

- `hosting.crossborderbasis` is named only where the deployment hosts outside Egypt, so
  one hosted inside never writes it; D-277 named runtime keys alone.
- Readings asked: A, the read answers it `value` null and `PUT` keeps refusing it; B,
  it stays as it is.
- Answered: D-277's amendment (8237bf5f), reading A. Built (section 1, 53754fd7).

## 5. Gate result

- Fast checks on every commit: green. These are the build with warnings as errors, the
  analysers, `dotnet format --verify-no-changes`, and the unit tests of the projects
  changed.
- Checks before the push: green. Every unit test passes (5226 in eleven projects), every
  contract test passes (214), and the commit-message, changelog, release, truth-table
  change, acceptance-criterion test name and forbidden-marker gates pass over
  `origin/main..HEAD`. The full-history secret scan finds nothing. `NamedKeyTests` and
  `StartupValidationTests` were run here against PostgreSQL.
- The pipeline's runs on fc310753, before the answer, were green.
- Gate of record: the pipeline's run on this report's commit, whose identifier and
  result the round's last message gives.
