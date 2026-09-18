# Phase 0: Skeleton

Status: stopped at open questions. The configuration work of the phase is incomplete.

## 1. Items implemented

| Item | Criteria | Tests |
|---|---|---|
| CONV-LAYOUT-001 | AC2, AC3 | `LibraryStructureTests.CONV_LAYOUT_001_AC2_CoreCarriesNoPackage`, `LibraryStructureTests.CONV_LAYOUT_001_AC3_DependenciesAreExactlyTheOnesTheTableGives` |
| CONV-LAYOUT-002 | AC1 | `LibraryStructureTests.CONV_LAYOUT_002_AC1_InternalsAreVisibleOnlyWhereThePermittedGrantsSay` |
| CONV-LAYOUT-003 | AC1 | `LibraryStructureTests.CONV_LAYOUT_003_AC1_EveryNamespaceMatchesItsFolder` |
| CONV-SETUP-001 | AC1 | `LibraryStructureTests.CONV_SETUP_001_AC1_NoProjectOverridesTheInheritedProperties` |
| CONV-SETUP-002 | AC1 | `LibraryStructureTests.CONV_SETUP_002_AC1_NoProjectFileCarriesAPackageVersion` |
| CONV-DESIGN-005 | AC1, AC2 | `ResultContractTests.CONV_DESIGN_005_AC1_EveryContractMethodReturnsAnOutcome`, `ResultContractTests.CONV_DESIGN_005_AC2_NoContractReturnsNullForNotFound` |
| CONV-DESIGN-008 | AC1 | `LibraryStructureTests.CONV_DESIGN_008_AC1_ThePackageSetIsExactlyTheAllowList` |
| CONV-NAME-003 | AC1, AC2 | `ErrorCodesTests.CONV_NAME_003_AC1_EveryCodeCarriesMeaningAndRemediation`, `ErrorCodesTests.CONV_NAME_003_AC2_ChangingACodeFailsTheContractTest` |
| CONV-CODE-001 | AC1 | `UnsealedTypeAnalyzerTests.CONV_CODE_008_AC1_ReportedOnAnUnsealedClassAsync`, `...SilentOnASealedClassAndAnAbstractBaseAsync` |
| CONV-CODE-002 | AC1 | `BlockingAndCancellationAnalyzerTests.CONV_CODE_008_AC1_ReportedOnBlockingOnATaskAsync`, `...ReportedOnAnAsynchronousMethodWithoutACancellationTokenAsync`, `...SilentOnAnAwaitWithACancellationTokenAsync` |
| CONV-CODE-003 | AC1 | `PublicSurfaceTests.CONV_CODE_003_AC1_NoContractMemberExposesAMutableCollection` |
| CONV-CODE-004 | AC1, AC2 | `PublicSurfaceTests.CONV_CODE_004_AC1_NoStaticFieldIsWritableAfterConstruction`, `PublicSurfaceTests.CONV_CODE_004_AC2_NoShippedFileUsesReflection` |
| CONV-CODE-008 | AC1, AC2 | The fifteen tests of the six analyser test classes; `LibraryStructureTests.CONV_CODE_008_AC2_TheAnalyserProjectIsReferencedAsAnAnalyser` |
| CONV-ERR-001 | AC2 | `ResultContractTests.CONV_ERR_001_AC2_TheOutcomeIsReachableOnlyByHandlingBothCases`, `ResultContractTests.CONV_ERR_001_AC2_BothBranchesAreRequired` |
| CONV-ERR-002 | AC1 | `PermittedOutcomeFromCatchAnalyzerTests.CONV_CODE_008_AC1_ReportedOnAPermissionReturnedFromACatchAsync`, `...ReportedOnASuccessfulOutcomeReturnedFromACatchAsync`, `...SilentOnARefusalReturnedFromACatchAsync` |
| CONV-ERR-003 | AC1 | `SwallowedExceptionAnalyzerTests.CONV_CODE_008_AC1_ReportedOnAnEmptyCatchAsync`, `...ReportedOnACatchThatOnlyCarriesOnAsync`, `...SilentOnACatchThatDealsWithTheExceptionAsync` |
| LIB-API-003 | AC1, AC2 | `ErrorTests.LIB_API_003_AC1_TheFailureCarriesNoProse`, `ErrorCodesTests.LIB_API_003_AC2_EveryCodeCarriesMeaningAndRemediation` |
| LIB-PKG-001 | AC2 | `LibraryStructureTests.LIB_PKG_001_AC2_NoAreaDependsOnAnotherArea` |
| LIB-PKG-002 | AC1 | `LibraryStructureTests.CONV_LAYOUT_001_AC2_CoreCarriesNoPackage` |

Criteria no test can decide, and how each was verified:

| Item | Criterion | Verified by |
|---|---|---|
| CONV-LAYOUT-001 | AC1 | The compiler: no area project references another (AC3 above) and no area grants another its internals (CONV-LAYOUT-002 AC1 above) |
| CONV-LAYOUT-002 | AC2 | `src/Janus.Core/PublicAPI.Unshipped.txt` holds the whole surface; every other source project's file is empty |
| CONV-SETUP-001 | AC2 | `TreatWarningsAsErrors` with `AnalysisLevel` latest-all; the `Public surface files up to date` gate builds the solution |
| CONV-SETUP-002 | AC2 | The `Locked restore` gate: `dotnet restore Janus.slnx --locked-mode` |
| CONV-SETUP-003 | AC1 | The same gate: a public member absent from the unshipped file is RS0016 |
| CONV-SETUP-003 | AC2 | A release-time rule; no release has happened |
| CONV-SETUP-004 | AC1 | The `Format` gate: `dotnet format Janus.slnx --verify-no-changes` |
| CONV-SETUP-004 | AC2 | `.editorconfig` is the only place a style rule is configured |
| CONV-SETUP-004 | AC3 | One suppression exists: `src/Janus.Core/Error.cs`, CA1716, justified on the same line, because CONV-DESIGN-005 names the failure an `Error`. No `#pragma warning disable` exists anywhere |
| CONV-DESIGN-002 | AC2 | The `Dependency allow-list` gate: no dispatch, pipeline or mapping package is in `Directory.Packages.props` |
| CONV-NAME-001 | AC1 | The naming rules of `.editorconfig` at error, enforced by the build and by the `Format` gate |
| CONV-CODE-005 | AC1 | `GenerateDocumentationFile` with warnings as errors: CS1591 |
| CONV-CODE-005 | AC2 | The `Forbidden markers and commented-out code` gate |
| CONV-DEP-001 | AC1, AC2 | Twelve committed `packages.lock.json` files and the `Locked restore` gate |
| CONV-DEP-002 | AC1, AC2 | Dependency vulnerability alerting and automated security updates are on for `neolorn/janus`; the `Dependency vulnerability alerting` gate refuses a resolved package with a known vulnerability |
| CONV-DEP-003 | AC1, AC2 | The `Dependency allow-list` gate and the one-logical-change-per-commit rule of CONV-VCS-003 |
| CONV-ERR-002 | AC2 | CONV-SETUP-004 AC3: a suppression carries a justification on the same line and an entry in this report |
| CONV-GATE-001 | AC1, AC2 | Seventeen jobs in `.github/workflows/gates.yml`, all seventeen required by the branch protection of `main` |
| CONV-GATE-002 | AC1, AC2 | Every job runs on push; `Destructive-operation detection report` and `Dependency vulnerability alerting` run on pull request and on `main` only, and `Secret scanning` on push only |
| CONV-TEST-001 | AC1 | `tests/Janus.Core.Tests` and `tests/Janus.Analyzers.Tests`, each mirroring its source project |
| CONV-TEST-002 | AC1 | The suite runs with no container |
| CONV-TEST-002 | AC3 | `--filter-trait kind=unit` and `--filter-trait kind=contract`, run separately by the `Unit tests` and `Contract tests` gates |
| CONV-TEST-007 | AC1 | No mocking package is in `Directory.Packages.props` (CONV-DESIGN-008 AC1 above) |
| CONV-TEST-007 | AC2 | The `Acceptance-criterion test names` gate resolves every such name to an item and a criterion in `docs/spec/` |
| CONV-VCS-001 | AC1 | The phase's branches were opened and merged the same day |
| CONV-VCS-002 | AC1, AC2 | Branch protection on `main` requires the seventeen checks, applies them to administrators, and requires no approval |
| CONV-VCS-003 | AC1 | The `Commit message format` gate |
| CONV-VCS-005 | AC1 | The `Changelog line present` gate |
| CONV-VCS-005 | AC2 | MinVer derives the version from the tag; no project file carries a version (CONV-SETUP-002 AC1 above) |
| CONV-DEP-004 | AC1, AC2 | No alert is open; no recurring calendar entry exists |
| OPS-DEP-002 | AC1, AC2 | The `Destructive-operation detection report` gate reports whether or not the gate variable is set |
| OPS-DEP-004 | AC1, AC2 | The `Secret scanning` gate runs gitleaks over the full history on every push, from its official action pinned to the release commit `e0c47f4f8be36e29cdc102c57e68cb5cbf0e8d1e`, with a committed `.gitleaks.toml` extending the default rule set and holding no allow-list entry |
| OPS-DEP-005 | AC1, AC2 | CONV-GATE-002 above |
| OPS-DEP-005 | AC3 | A monthly operator review; the first month has not elapsed |

## 2. Items in the phase not implemented

| Item | Reason | Waits on |
|---|---|---|
| `10` section 4 keys as typed settings with defaults and floors | Eight of the keys have no stated type, two have no stated value vocabulary, and three are families rather than keys | Open questions 3, 4, 6 |
| `10` section 4.1a policy object | Two of its six fields are keyed by vocabularies the specification does not complete | Open questions 1, 2 |
| OPS-CFG-001, OPS-CFG-003 | Their criteria are stated over the settings above | Open questions 3, 4, 6 |
| OPS-CFG-002, OPS-CFG-005 | A change needs step-up and an audit record | Phases 1 and 3 |
| OPS-CFG-004 | AC1 is stated over the runtime configuration endpoint | Phase 5 |
| OPS-CFG-006, OPS-CFG-007 | System administration is a permission | Phase 2 |
| OPS-CFG-008 | Settings are stored in the library's own schema | Phase 1 |
| CONV-DESIGN-007 | `AddJanus` registers services that do not exist, and AC3 is stated over the required keys of LIB-HOST-001, which are settings | Open questions 3, 4, 6 |
| LIB-HOST-001 | Startup validation over the settings above | Open questions 3, 4, 6 |
| LIB-PKG-001 AC1, CONV-DESIGN-001 | The area projects hold no code | Phases 1 to 8 |
| CONV-DESIGN-002 AC1, AC3 | No service contract and no operation exist | Phase 1 |
| CONV-DESIGN-003, CONV-DESIGN-004, CONV-ENUM-001 | Stated over persistence, entities and the schema | Phase 1 |
| CONV-DESIGN-006, CONV-CODE-006 | Stated over endpoints and their shape validation | Phase 5 |
| CONV-CODE-007 | Stated over secret material | Phase 3 |
| CONV-NAME-002 | Stated over permission strings and model validation | Phase 2 |
| CONV-ERR-001 AC1, AC3 | Stated over authentication and authorization denials, and over startup configuration | Phases 2, 3, and open questions 3, 4, 6 |
| CONV-ERR-003 AC2 | No security path exists | Phase 3 |
| CONV-LOG-001 | Every log call goes through a generated method; no log call exists | Phase 1 |
| CONV-LOG-002 | A correlation identifier is carried by a request | Phase 5 |
| CONV-LOG-003 AC2, CONV-LOG-004 | Stated over the log output of a request | Phase 5 |
| CONV-LOG-005 | Stated over failed authentication, denied authorization, step-up and break-glass | Phases 2, 3, 9 |
| CONV-LOG-006 | Stated over the gate's explanation | Phase 2 |
| CONV-TEST-002 AC2 | No integration test exists | Phase 1 |
| CONV-TEST-003, CONV-TEST-004, CONV-TEST-005, CONV-TEST-006 | Stated over policy suites, the truth table and fixtures | Phases 1 and 2 |
| CONV-VCS-004 | Stated over permission logic and the truth table | Phase 2 |
| CONV-CONTENT-001 | A review of documents 01 to 20 and of frontend wording | Milestone 2 |
| LIB-API-002, LIB-TEST-002 AC2 | Stated over internal types and the ancestry closure, neither of which exists | Phases 1 and 2 |
| OPS-DEP-001, OPS-DEP-003 | Stated over the deploy | Milestone 2 step 1 |
| `Policy coverage test`, `Truth-table suite` and `Double migration run` rows of CONV-GATE-001 | Each runs a suite that arrives with the item it proves; a job cannot name a test project that does not exist | Phases 1 and 2 |
| `Janus.Cli` commands | Bootstrap and key rotation | Phase 9 |

## 3. Resolved by rule

| Place | What was out of step | Governing item | Rule applied |
|---|---|---|---|
| The working tree handed over | The instruction files and the `docs/` tree sat one directory deeper than every path in the specification resolves to | the working guide's section 5, which places `docs/spec/`, `docs/guide/` and `docs/decision-log.md` at the repository root | The wrapper directory was removed and its contents moved up one level; no file content changed |
| `src/Janus.Analyzers` | CONV-CODE-008 does not state the accessibility of an analyser type, and the usual practice is public | CONV-LAYOUT-002, which permits a public type only in `Janus.Core`, `Janus.Hosting` and `Janus.Conformance` | The six analysers are `internal sealed`, which keeps the analyser project's public surface empty; the compiler still discovers and runs them, which we confirmed by building a deliberately unsealed class and observing JAN0003 fail the build |
| `.gitignore` | the working guide's section 5 names `.gitignore` only to forbid the instruction files from appearing in it | CONV-VCS-001, and the rule never to commit generated noise | Build output, test output and local editor state are excluded in a committed `.gitignore`; the instruction files and `tmp/` are excluded in `.git/info/exclude` only |
| `src/Janus.Core/Error.cs` | No chapter states the type of the structured details a failure carries | CONV-CODE-003 (a contract member exposes a read-only collection), CONV-DESIGN-006 (serialization is `System.Text.Json` source generation) and API-CONV-002, whose `details` example holds nested objects | `IReadOnlyDictionary<string, JsonElement>`, the one shape that satisfies all three |
| `tests/Janus.Analyzers.Tests` | CONV-TEST-007 names an acceptance-criterion test `ITEM_ACn_Outcome`, and CONV-NAME-001 requires the `Async` suffix on an asynchronous method | CONV-NAME-001, enforced as IDE1006 | An asynchronous test carries the suffix after its outcome, satisfying both |
| `.github/` | the working guide's section 9 permits files only under the source tree, `docs/` and `tmp/`, and CONV-GATE-001 requires a pipeline, which the platform reads only from `.github/workflows/` | CONV-GATE-001 | The workflow and the six gate scripts live under `.github/`; nothing else does |
| `.github/workflows/gates.yml`, the secret-scanning job | The job ran on the pull-request event as well, where the action scans that request's commits rather than the history, and asks for a token permission the workflow does not grant | OPS-DEP-004, which defines the scan as the full history on every push | The job runs on the push event, where the full-history scan the item requires is the one that runs; every commit of a pull request is scanned by the push that carried it |
| `global.json` | CONV-SETUP-001 fixes the framework; the software development kit that builds it is fixed nowhere | CONV-DEP-001 AC2, that a build with no source change produces the same dependency set | `global.json` pins the kit to 10.0.300 with patch roll-forward and selects the test runner CONV-TEST-007 names, which the 10.0 kit otherwise refuses to run from `dotnet test` |

## 4. Open questions

### 1. The step-up actions have no complete set of names (Tier 3)

**Item.** `10` section 4.1a, the `gates` field, against `10` section 5a.

Section 4.1a states that `gates` holds, per section 5a action, a level, a
phishing-resistance flag and a maximum age. Section 5a lists nineteen rows, one of
them retired. Eight actions carry a name (`identifier:add`, `identifier:remove`,
`username:change`, `mailcredential:create`, `mailcredential:revoke`,
`restriction:edit`, `restriction:grant`, `domain:manage`); the rest are described in
prose ("Set or change a password", "Export personal data", "Request account deletion",
"Link or unlink a social provider", and so on). Section 5a says `09` marks each; `09`
marks which endpoints require step-up and names no further action.

The field cannot be keyed without naming the remaining actions, and those names are
both part of the public contract and the vocabulary of the gates.

### 2. The factor catalogue entries have no complete set of names (Tier 3)

**Item.** `10` section 4.1a, the `loginFactors` field, against `02` AUTH-FACT-002.

Section 4.1a states that `loginFactors` holds the catalogue entries of AUTH-FACT-002 a
principal may sign in with, and AUTH-FACT-002 makes that field the only switch that
enables a factor. The catalogue has fourteen rows. Four carry a name (`emailLink`,
`emailCode`, `phoneLink`, `phoneCode`); the other ten are described in prose
("Password", "Passkey (discoverable credential)", "Cross-device sign-in (hybrid)",
"Google", "Apple", "TOTP", "Security key as second factor (non-discoverable WebAuthn
credential)", "Recovery codes", "Verification code (email or SMS)", "Break-glass
credential").

The field is a set of catalogue entries and cannot be typed without naming them.

### 3. The password blocklist sources have no stated values (Tier 2)

**Item.** `10` section 4.2, `password.blocklist.source` and
`password.blocklist.sources`.

**What the code needs.** A default value, and the set where the key's type is a set.

**What the specification says.** `password.blocklist.source` has the default "range
API" and the note "where the leaked-password list comes from (range API, offline
fallback, self-hosted corpus)". `password.blocklist.sources` has the default "leaked
list only" and the note that it MAY add `dictionary` and `context`. Two values are
written as names; the other four are written as prose.

**The readings we see.**

1. The prose is the value, so the default of `password.blocklist.source` is the text
   `range API`. Against this: no other key of section 4 carries a value with a space,
   and AUTH-PASS-004 describes the three as mechanisms rather than as text.
2. The prose stands for a name the specification has not yet written, as it does for
   the ten factors of question 2.

**The smallest fix for each.** Under reading 1, nothing changes and the values are
taken verbatim. Under reading 2, section 4.2 states the names, as it already does for
`dictionary` and `context`.

### 4. Eight keys have no stated type (Tier 2)

**Item.** `10` section 4, the keys below, against the phase's instruction to hold
section 4 as typed settings with defaults and floors.

| Key | Declared default | What is missing |
|---|---|---|
| `privacy.request.decision` | 6 working days from submission | A working day is not a length of time; the value resolves against `privacy.workingdays` and `privacy.holidays` |
| `privacy.workingdays` | Sunday–Thursday | Whether the value is a set of days of the week, and how a day is named |
| `retention.consent` | life of the processing + 3 years | Two parts, the first of which is not a length of time |
| `abuse.throttle.delay.factor` | ×2 per further failure | A multiplier, so not a count; whether it may be fractional |
| `abuse.throttle.decay` | halves every 10 minutes without failures | A half-life, so one duration with the halving fixed, or two values |
| `webauthn.algorithms` | `[-8, -7, -257]`, and −7 cannot be removed | A list of signed integers with one member that cannot be removed; that rule is a membership rule, not a bound |
| `password.argon2.memory` and `password.argon2.iterations` | 19456 KiB and 2 | The floor is "enforced as a (memory, iterations) strength class", which is a rule over two keys and not a bound on either |

**The smallest fix.** Section 4 states the type of each, as it does for the keys whose
default is a plain duration, count, flag or list.

### 5. A value outside a key's named set has no code (Tier 2)

**Item.** `10` section 1.5 and `09` `PUT /admin/config/{key}`, against OPS-CFG-003.

**What the code needs.** The code a change carries when the value is well-formed but
is not one of the key's stated values, for example `registration.phone` set to
anything other than `required` or `optional`.

**What the specification says.** Section 1.5 defines `config.value.belowfloor` and
`config.value.aboveceiling` and no other value code. `09` lists
`config.value.belowfloor`, `config.key.protected` and
`auth.restriction.reasonrequired` on 422 for that endpoint, and `10` section 6 makes
422 "well-formed, semantically rejected". Neither names a code for a value outside a
named set.

We also note, as part of the same question, that `config.value.aboveceiling` is
defined in section 1.5 and enforced by the ceilings section 4 declares, but is absent
from that endpoint's 422 list.

**The readings we see.**

1. The set is shape, so a value outside it is refused by the boundary validation of
   CONV-CODE-006 and carries whatever code that validation carries.
2. The set is a bound, so it carries a code of its own.

**The smallest fix for each.** Under reading 1, `09` names the validation code on that
endpoint. Under reading 2, section 1.5 gains one code and `09` lists it, in both cases
alongside `config.value.aboveceiling`.

### 6. Three keys are families, not keys (Tier 2)

**Item.** `10` section 4: `policy.<organization>` (section 4.1),
`retention.<host-category>` (section 4.7) and `stepup.enforcement.<organization>`
(section 4.8).

**What the code needs.** The phase gate is that every `10` key resolves with its
default. A family has no single name, and two of the three have no default of their
own: `policy.<organization>` holds only what the organization overrides, and
`stepup.enforcement.<organization>` is a per-organization kill switch.

**The readings we see.**

1. A family is not a key of the catalogue: it is a per-organization or per-category
   value read through its own operation, and the catalogue holds only the keys with a
   literal name.
2. A family is a key whose name takes a parameter, and the catalogue holds it as such.

**The smallest fix for each.** Under reading 1, section 4 marks the three as families
rather than keys. Under reading 2, section 4 states how a parameterised key is named
and what it resolves to when the parameter names nothing.

## 5. Gate result

Fast checks on every commit of the phase: `dotnet build Janus.slnx` with warnings as
errors and analysers at latest-all, `dotnet format Janus.slnx --verify-no-changes`,
`dotnet restore Janus.slnx --locked-mode`, and the test suite. Green on each.

From the last commits of the phase, `dotnet test` on the development machine reports
that no tests ran: the test host ends during its start-up when the command line drives
it in server mode. The pipeline runs `dotnet test` unchanged and green, so the suite
was run locally by executing the test binaries, which runs the same tests.

Full gate: GitHub Actions runs `35391824706` (push) and `35391830140` (pull request)
on branch `phase-00-report`, all seventeen jobs green; the commit carrying this
sentence re-runs them before the merge. The integration, migration and conformance
suites do not exist in this phase; the three rows of CONV-GATE-001 that run them are
listed in section 2.

Tests: 69, all passing. `tests/Janus.Core.Tests` 54, `tests/Janus.Analyzers.Tests` 15.

Repository, per the working guide's section 5: private repository `neolorn/janus`,
default branch `main`, squash and rebase merges off, merge commits on, delete branch on
merge on, dependency vulnerability alerting and automated security updates on, platform
secret scanning unavailable on this plan as `08` section 10 records, so the scanning
OPS-DEP-004 requires is the gitleaks gate (D-150). Branch protection on `main`
requires the seventeen status checks, applies to administrators, requires no
approval, and refuses force pushes and deletion. The instruction files
and `tmp/` are excluded through `.git/info/exclude`.
