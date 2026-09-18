# Phase 0: Skeleton

Status: stopped at open questions. Five keys of `10` section 4 are absent from the
catalogue and one criterion of OPS-CFG-001 cannot be read as written.

## 1. Items implemented

| Item | Criteria | Tests |
|---|---|---|
| CONV-LAYOUT-001 | AC2, AC3 | `LibraryStructureTests.CONV_LAYOUT_001_AC2_CoreCarriesNoPackage`, `LibraryStructureTests.CONV_LAYOUT_001_AC3_DependenciesAreExactlyTheOnesTheTableGives` |
| CONV-LAYOUT-002 | AC1 | `LibraryStructureTests.CONV_LAYOUT_002_AC1_InternalsAreVisibleOnlyWhereThePermittedGrantsSay` |
| CONV-LAYOUT-003 | AC1 | `LibraryStructureTests.CONV_LAYOUT_003_AC1_EveryNamespaceMatchesItsFolder` |
| CONV-SETUP-001 | AC1 | `LibraryStructureTests.CONV_SETUP_001_AC1_NoProjectOverridesTheInheritedProperties` |
| CONV-SETUP-002 | AC1 | `LibraryStructureTests.CONV_SETUP_002_AC1_NoProjectFileCarriesAPackageVersion` |
| CONV-DESIGN-005 | AC1, AC2 | `ResultContractTests.CONV_DESIGN_005_AC1_EveryContractMethodReturnsAnOutcome`, `ResultContractTests.CONV_DESIGN_005_AC2_NoContractReturnsNullForNotFound` |
| CONV-DESIGN-007 | AC3 | `StartupConfigurationTests.LIB_HOST_001_AC2_AMissingDeclarationNamesTheKey` |
| CONV-DESIGN-008 | AC1 | `LibraryStructureTests.CONV_DESIGN_008_AC1_ThePackageSetIsExactlyTheAllowList` |
| CONV-NAME-003 | AC1, AC2 | `ErrorCodesTests.CONV_NAME_003_AC1_EveryCodeCarriesMeaningAndRemediation`, `ErrorCodesTests.CONV_NAME_003_AC2_ChangingACodeFailsTheContractTest` |
| CONV-CODE-001 | AC1 | `UnsealedTypeAnalyzerTests.CONV_CODE_008_AC1_ReportedOnAnUnsealedClassAsync`, `...SilentOnASealedClassAndAnAbstractBaseAsync` |
| CONV-CODE-002 | AC1 | `BlockingAndCancellationAnalyzerTests.CONV_CODE_008_AC1_ReportedOnBlockingOnATaskAsync`, `...ReportedOnAnAsynchronousMethodWithoutACancellationTokenAsync`, `...SilentOnAnAwaitWithACancellationTokenAsync` |
| CONV-CODE-003 | AC1 | `PublicSurfaceTests.CONV_CODE_003_AC1_NoContractMemberExposesAMutableCollection` |
| CONV-CODE-004 | AC1, AC2 | `PublicSurfaceTests.CONV_CODE_004_AC1_NoStaticFieldIsWritableAfterConstruction`, `PublicSurfaceTests.CONV_CODE_004_AC2_NoShippedFileUsesReflection` |
| CONV-CODE-008 | AC1, AC2 | The fifteen tests of the six analyser test classes; `LibraryStructureTests.CONV_CODE_008_AC2_TheAnalyserProjectIsReferencedAsAnAnalyser` |
| CONV-ERR-001 | AC2, AC3 | `ResultContractTests.CONV_ERR_001_AC2_TheOutcomeIsReachableOnlyByHandlingBothCases`, `ResultContractTests.CONV_ERR_001_AC2_BothBranchesAreRequired`, `StartupConfigurationTests.OPS_CFG_003_AC3_AValueOutsideItsBoundsStopsStartup` |
| CONV-ERR-002 | AC1 | `PermittedOutcomeFromCatchAnalyzerTests.CONV_CODE_008_AC1_ReportedOnAPermissionReturnedFromACatchAsync`, `...ReportedOnASuccessfulOutcomeReturnedFromACatchAsync`, `...SilentOnARefusalReturnedFromACatchAsync` |
| CONV-ERR-003 | AC1 | `SwallowedExceptionAnalyzerTests.CONV_CODE_008_AC1_ReportedOnAnEmptyCatchAsync`, `...ReportedOnACatchThatOnlyCarriesOnAsync`, `...SilentOnACatchThatDealsWithTheExceptionAsync` |
| LIB-API-001 | AC2 | `SettingsCatalogueTests.LIB_API_001_AC2_TheKeyNamesAreTheContract`, `SettingsCatalogueTests.LIB_API_001_AC2_TheFamiliesAreTheContract`, `VocabularyContractTests.LIB_API_001_AC2_TheStepUpActionNamesAreTheContract`, `VocabularyContractTests.LIB_API_001_AC2_TheFactorIdentifiersAreTheContract`, `VocabularyContractTests.LIB_API_001_AC2_ThePolicyFieldValuesAreTheContract` |
| LIB-API-003 | AC1, AC2 | `ErrorTests.LIB_API_003_AC1_TheFailureCarriesNoProse`, `ErrorCodesTests.LIB_API_003_AC2_EveryCodeCarriesMeaningAndRemediation` |
| LIB-HOST-001 | AC1, AC2, AC3, AC4 first sentence | `SettingsCatalogueTests.LIB_HOST_001_AC1_EveryOtherKeyHasADefault`, `SettingsCatalogueTests.LIB_HOST_001_AC3_NoKeyOutsideTheDeclarationsIsRequired`, `StartupConfigurationTests.LIB_HOST_001_AC1_NamingOnlyTheDeclarationsStarts`, `StartupConfigurationTests.LIB_HOST_001_AC2_AMissingDeclarationNamesTheKey`, `StartupConfigurationTests.LIB_HOST_001_AC4_TheGoverningLanguageFailsWithItsNamedError` |
| LIB-PKG-001 | AC2 | `LibraryStructureTests.LIB_PKG_001_AC2_NoAreaDependsOnAnotherArea` |
| LIB-PKG-002 | AC1 | `LibraryStructureTests.CONV_LAYOUT_001_AC2_CoreCarriesNoPackage` |
| OPS-CFG-003 | AC1, AC2, AC3 | `SettingsTests.OPS_CFG_003_AC1_APasswordFloorBelowTheStandardsMinimumIsRejected`, `SettingsTests.OPS_CFG_003_AC2_ASessionAbsoluteTimeoutAboveTheMaximumIsRejected`, `StartupConfigurationTests.OPS_CFG_003_AC3_AValueOutsideItsBoundsStopsStartup` |
| AUTH-STEP-002a | AC3 | `PolicyTests.AUTH_STEP_002a_AC3_TheSystemPolicyGatesEveryStepUpAction`, `PolicyTests.AUTH_STEP_002a_AC3_TheAdministrativePolicyGatesEveryStepUpAction` |
| INT-HOST-001 | AC2 | `StartupConfigurationTests.INT_HOST_001_AC2_HostingOutsideEgyptRequiresTheCrossBorderBasis`, `StartupConfigurationTests.INT_HOST_001_AC2_HostingInsideEgyptNeedsNoCrossBorderBasis` |
| `10` section 4, the key catalogue | Ninety-one keys and three families: every live key of the section less the five of open questions 2 to 4 | `SettingsCatalogueTests.LIB_API_001_AC2_TheKeyNamesAreTheContract`, `SettingsCatalogueTests.LIB_API_001_AC2_TheFamiliesAreTheContract`, `SettingsCatalogueTests.All_TheCatalogue_NamesEveryKeyOnce`, `SettingsCatalogueTests.Scope_TheCatalogue_ProtectsTheKeysSectionFourMarks`, the eleven tests of `SettingsTests` |
| `10` section 4.1a, the policy object | The six fields, the system defaults and the administrative organization's | `PolicyTests.SystemDefault_EveryField_IsWhatSectionFourOneAStates`, `PolicyTests.AdministrativeOrganization_EveryField_IsWhatSectionFourOneAStates`, `PolicyTests.SystemDefault_EveryGate_IsReachableWithoutPhishingResistance`, `PolicyTests.AdministrativeOrganization_EveryGate_IsAal2AndPhishingResistant`, `PolicyTests.RequiredAssurance_ALevelOutsideTheField_Refused`, `PolicyTests.LoginFactors_TheEmergencyCredential_Refused` |
| `10` section 4.2, the two cross-key rules | The password floor pair and the Argon2id strength classes | `StartupConfigurationTests.AcceptPasswordFloorPair_WithMfaAboveSingleFactor_Refused`, `...AcceptPasswordFloorPair_EqualFloors_Accepted`, `...AcceptPasswordFloorPair_TheShippedDefaults_Accepted`, `...AcceptArgon2Cost_AStrengthClass_Accepted`, `...AcceptArgon2Cost_BelowEveryStrengthClass_Refused`, `...AcceptArgon2Cost_AboveAStrengthClass_Accepted`, `...AcceptArgon2Cost_TheShippedDefaults_Accepted` |

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
| CONV-SETUP-004 | AC3 | Two suppressions exist: `src/Janus.Core/Error.cs`, CA1716, because CONV-DESIGN-005 names the failure an `Error`; `src/Janus.Core/Policy.cs`, CA1724, because section 4.1a names the type the policy object. Each carries its justification in the attribute. No `#pragma warning disable` exists anywhere |
| CONV-DESIGN-002 | AC2 | The `Dependency allow-list` gate: no dispatch, pipeline or mapping package is in `Directory.Packages.props` |
| CONV-NAME-001 | AC1 | The naming rules of `.editorconfig` at error, enforced by the build and by the `Format` gate |
| CONV-CODE-005 | AC1 | `GenerateDocumentationFile` with warnings as errors: CS1591 |
| CONV-CODE-005 | AC2 | The `Forbidden markers and commented-out code` gate |
| CONV-DEP-001 | AC1, AC2 | Twelve committed `packages.lock.json` files and the `Locked restore` gate |
| CONV-DEP-002 | AC1, AC2 | Dependency vulnerability alerting and automated security updates are on for `neolorn/janus`; the `Dependency vulnerability alerting` gate refuses a resolved package with a known vulnerability |
| CONV-DEP-003 | AC1, AC2 | The `Dependency allow-list` gate and the one-logical-change-per-commit rule of CONV-VCS-003 |
| CONV-ERR-002 | AC2 | CONV-SETUP-004 AC3 above |
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
| LIB-HOST-001 | AC1, second half | Every key that is not one of the nine declarations hands back a default rather than throwing, which `SettingsCatalogueTests.LIB_HOST_001_AC1_EveryOtherKeyHasADefault` reads for each in turn; that the nine are the right nine is AC3 |
| OPS-DEP-002 | AC1, AC2 | The `Destructive-operation detection report` gate reports whether or not the gate variable is set |
| OPS-DEP-004 | AC1, AC2 | The `Secret scanning` gate runs gitleaks over the full history on every push, from its official action pinned to the release commit `e0c47f4f8be36e29cdc102c57e68cb5cbf0e8d1e`, with a committed `.gitleaks.toml` extending the default rule set and holding no allow-list entry |
| OPS-DEP-005 | AC1, AC2 | CONV-GATE-002 above |
| OPS-DEP-005 | AC3 | A monthly operator review; the first month has not elapsed |

## 2. Items in the phase not implemented

| Item | Reason | Waits on |
|---|---|---|
| `10` section 4: `photo.maxbytes`, `preferences.maxsize`, `identifiers.email.max`, `identifiers.phone.max`, `abuse.botdefence.signals` | The chapter states a default the code cannot resolve to a value | Open questions 2, 3, 4 |
| OPS-CFG-001 | AC1 names two places a redeploy-scoped setting may be listed, and one key is marked protected in neither | Open question 1 |
| OPS-CFG-002, OPS-CFG-005 | A change needs step-up and an audit record | Phases 1 and 3 |
| OPS-CFG-004 | AC1 is stated over the runtime configuration endpoint | Phase 5 |
| OPS-CFG-006, OPS-CFG-007 | System administration is a permission | Phase 2 |
| OPS-CFG-008 | Settings are stored in the library's own schema | Phase 1 |
| CONV-DESIGN-007 AC1, AC2 | `AddJanus` registers services that do not exist, and no clock or randomness source is used yet | Phase 1 |
| LIB-HOST-001 AC4 second sentence, AC5 | The preference declaration and the restriction key suppliers are declarations rather than configuration keys | Phases 1 and 4 |
| LIB-PKG-001 AC1, CONV-DESIGN-001 | The area projects hold no code | Phases 1 to 8 |
| CONV-DESIGN-002 AC1, AC3 | No service contract and no operation exist | Phase 1 |
| CONV-DESIGN-003, CONV-DESIGN-004, CONV-ENUM-001 | Stated over persistence, entities and the schema | Phase 1 |
| CONV-DESIGN-006, CONV-CODE-006 | Stated over endpoints and their shape validation | Phase 5 |
| CONV-CODE-007 | Stated over secret material | Phase 3 |
| CONV-NAME-002 | Stated over permission strings and model validation | Phase 2 |
| CONV-ERR-001 AC1 | Stated over authentication and authorization denials | Phases 2 and 3 |
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
| `src/Janus.Core/Configuration/` | `10` section 4 is one chapter section and `08` names no folder for it | CONV-DESIGN-001, which places a feature's types in a folder named for the feature | The catalogue, the setting types, the key and the store abstraction sit in one `Configuration` folder inside `Janus.Core`; the vocabularies of section 5 and the policy object stay at the project root, because every area reads them |
| `src/Janus.Core/Configuration/SettingOfT.cs` | A generic type and its non-generic base cannot share a file name | The file layout already in the repository, where `Result` and `Result<T>` are `Result.cs` and `ResultOfT.cs` | `Setting.cs` and `SettingOfT.cs`, the same way |
| `10` section 4, every key whose row states no direction | OPS-CFG-002 needs a direction per key; most rows state none | D-079b, which classifies a setting with no direction as loosening | Each such key carries `AnyChange`, so every change to it takes the friction of a loosening |
| `webauthn.rpid` | The row states no default, and LIB-HOST-001 lists the origins rather than the identifier among the declarations | AUTH-FACT-010, which derives the identifier from the origins and checks it is a registrable suffix of one | The default is the empty string, which means derived at startup from `webauthn.origins` |
| `abuse.sms.balancefloor` | The row states a prepaid amount and no default | LIB-HOST-001, which lists the SMS balance floor among the declarations because the library cannot guess it | A decimal setting the deployment names, with no default |
| `alerting.sms.severitythreshold` | The row states a severity, and section 4 names no severity vocabulary | OPS-ALERT-001, which names the two severities | A choice over `High` and `Normal`, defaulting to `High` |
| `10` section 4, every row whose default is written `on` | The word is not one of the boolean literals the chapter writes elsewhere | The D-151 preamble, which derives a row's type from its default | `on` is the boolean true |
| `audit.enabled`, `token.signature.verification` | Neither row states a default | P-001, which puts a default at the safe end of its range | Both default to true |
| `hosting.location` | The row states no value vocabulary | INT-HOST-001, which reflects the value in generated records as inside or outside Egypt | A choice over `inside` and `outside` |
| `10` section 4.1a, `emailDomains` | The system default is written `off`, and the field is otherwise a list of domains | The field's own description, which reads `off` as the absence of a domain lock | The empty list |
| `10` section 4.1a, the system default gate | The gate is written `reachable`, floor `aal1`, which is four values where a gate has three | AUTH-STEP-002, which fixes a gate at level, phishing-resistance and maximum age | The floor belongs to the meaning of `reachable`, which resolves to the account's reachable assurance and never below AAL1 |
| `src/Janus.Core/Policy.cs` | The analyser refuses a type name that matches a namespace | `10` section 4.1a, which names the type the policy object, and the fact that `System.Security.Policy` does not exist on this framework | CA1724 suppressed with that justification on the attribute |
| `tests/Janus.Core.Tests/PublicSurfaceTests.cs` | CONV-CODE-004 AC1 read every static field, including the delegate caches the compiler emits for a lambda | CONV-CODE-004, which is stated over mutable static state a person writes | The test skips a type the compiler generated |
| `tests/Janus.Core.Tests/ResultContractTests.cs` | CONV-DESIGN-005 AC2 read an unconstrained type parameter in a contract's return type as a lookup that can return null | CONV-DESIGN-005, whose rule is that a service method never returns null for not found | A type parameter a contract leaves open carries the nullability of the caller's own type argument, so the walk stops there |
| `Settings.AcceptPasswordFloorPair`, `Settings.AcceptArgon2Cost` | Two rules of section 4.2 hold over a pair of keys, and a setting validates one value | OPS-CFG-003, which requires the rejection and names no place for it | The two rules sit on the catalogue rather than on either setting, and the failure names the key the chapter states the rule on |

## 4. Open questions

### 1. One key is protected where nothing lists it (Tier 3)

**Item.** `10` section 4.5, `alerting.destinationchange.notify`, against OPS-CFG-001
AC1, OPS-CFG-004 and `10` section 4.8.

OPS-CFG-001 AC1 reads: a setting is redeploy-scoped only where listed in OPS-CFG-004
or in the model declaration. Section 4.8 opens with "Every key here is on the
OPS-CFG-004 list" and holds eight keys. Section 4.5 marks
`alerting.destinationchange.notify` **P**, and the key is on neither list.

The two readings contradict each other rather than leave the point open. Under the
first, the key is protected and OPS-CFG-001 AC1 is false as written, because a key is
redeploy-scoped where its own row says so. Under the second, the mark is the defect
and the key is runtime-changeable, which would let an administrator turn off the
notice that a destination changed before changing one.

The catalogue carries the mark each row gives it, and
`SettingsCatalogueTests.Scope_TheCatalogue_ProtectsTheKeysSectionFourMarks` holds all
thirteen keys section 4 marks **P**.

### 2. Two size keys have no byte unit (Tier 2)

**Item.** `10` sections 4.5 and 4.6, `photo.maxbytes` and `preferences.maxsize`.

**What the code needs.** An integer default and an integer ceiling for each.

**What the specification says.** `photo.maxbytes` is "2 MB" with ceiling "10 MB";
`preferences.maxsize` is "8 KB" with ceiling "64 KB". `password.argon2.memory` is the
only other size in section 4 and is written "19456" with the unit "KiB" stated
separately.

**The readings we see.** MB is 1000000 and KB is 1000, as the prefixes and the key
names read; or MB is 1048576 and KB is 1024, as the neighbouring KiB suggests.

**The smallest fix.** Section 4 writes the two defaults and the two ceilings as bare
integers with the unit stated, as it does for the Argon2id memory.

### 3. Two identifier limits have no representation (Tier 2)

**Item.** `10` section 4.6, `identifiers.email.max` and `identifiers.phone.max`.

**What the code needs.** An integer default.

**What the specification says.** The default is "unlimited", with floor 1, and the
note that 1 is single-address mode.

**The readings we see.** Unlimited is a sentinel the type carries, for example 0 or
the largest integer; or the key admits no value and absence is the default, which no
other key in section 4 does.

**The smallest fix for each.** Section 4 writes the value that means unlimited, or
states that absence is the default and what absence means.

### 4. One signal set has no members (Tier 2)

**Item.** `10` section 4.5, `abuse.botdefence.signals`, against AUTH-ABUSE-008.

**What the code needs.** A value set and a default subset of it.

**What the specification says.** The default is "datacenter ranges, repeated
attempts". AUTH-ABUSE-008 describes those two signals and names neither, and section 4
names no other member the set could hold.

**The readings we see.** The set is closed at the two the row writes, which makes it a
pair of flags rather than a set; or the set is open and section 4 has not yet written
its members, as it had not for the blocklist sources before D-151.

**The smallest fix.** Section 4 names the members, as it now does for
`password.blocklist.sources`.

### 5. One ceiling is a statutory period with no number (Tier 2)

**Item.** `10` section 4.7, `privacy.request.decision`.

**What the code needs.** An integer ceiling, or none.

**What the specification says.** The default is 6 working days from submission and the
scope reads "ceiling enforced (the statutory period)". The length of the statutory
period appears nowhere in `docs/spec/`.

**The readings we see.** The ceiling is a number the chapter has not yet written; or
the ceiling belongs to the jurisdiction the host declares, the way the lawful-basis
list does (PRIV-BASIS-001), and is therefore a declaration rather than a library
constant.

**The smallest fix for each.** Section 4.7 writes the number, or LIB-HOST-001 adds the
statutory decision period to the declarations.

The key carries its default and no ceiling, so a value above the statutory period is
accepted today.

### 6. Five floors are enforced with no number (Tier 2)

**Item.** `10` section 4.6: `organization.deletion.grace`,
`takedown.grace`, `account.deletion.grace`, `identifier.change.coolingoff` and
`identifiers.username.changecooloff`.

**What the code needs.** A floor for each, or none.

**What the specification says.** Each row's scope reads "R, floor enforced" and states
no number. IDN-ORG-003 AC4 confirms a minimum exists for the organization deletion
grace and does not number it either.

**The readings we see.** The floor is the shipped default, so each value may be raised
and never lowered; or the floor is a number the chapter has not yet written.

**The smallest fix for each.** Each row states its floor, or states that the floor is
the default.

All five carry their default and no floor, so a deployment can set any of them to zero
today.

### 7. Two retention periods are counted in years (Tier 2)

**Item.** `10` section 4.7, `retention.audit.security` and `retention.consent`.

**What the code needs.** A duration for each default and each floor.

**What the specification says.** `retention.audit.security` is 7 years with a floor of
5 years; `retention.consent` is `P3Y` with a floor of `P1Y`. The D-151 preamble
derives a duration from an ISO 8601 value.

**What the code does.** The framework's duration type has no calendar, so a year is
held as 365 days: the security floor is 1825 days rather than five calendar years, and
falls short by one or two days over any five-year span holding a leap day.

**The readings we see.** The drift is immaterial and the value is a duration; or a
retention floor stated in years is a calendar period, which needs a type `08` does not
name and for which CONV-DESIGN-008 admits no package.

**The smallest fix for each.** Section 4.7 writes the two defaults and the two floors
in days, or `08` names how a calendar period is held.

## 5. Gate result

Fast checks on every commit of the phase: `dotnet build Janus.slnx` with warnings as
errors and analysers at latest-all, `dotnet format Janus.slnx --verify-no-changes`,
`dotnet restore Janus.slnx --locked-mode`, and the test suite. Green on each.

`dotnet test` on the development machine reports that no tests ran: the test host ends
during its start-up when the command line drives it in server mode. The pipeline runs
`dotnet test` unchanged and green, so the suite was run locally by executing the test
binaries, which runs the same tests.

Full gate: GitHub Actions runs `35396808643` (push) and `35396820403` (pull request)
on branch `phase-00-configuration`, all seventeen jobs green; the commit carrying this
sentence re-runs them before the merge. The integration, migration and conformance
suites do not exist in this phase; the three rows of CONV-GATE-001 that run them are
listed in section 2.

Tests: 116, all passing. `tests/Janus.Core.Tests` 101, `tests/Janus.Analyzers.Tests`
15.

Repository, per the working guide's section 5: private repository `neolorn/janus`,
default branch `main`, squash and rebase merges off, merge commits on, delete branch on
merge on, dependency vulnerability alerting and automated security updates on, platform
secret scanning unavailable on this plan as `08` section 10 records, so the scanning
OPS-DEP-004 requires is the gitleaks gate (D-150). Branch protection on `main`
requires the seventeen status checks, applies to administrators, requires no approval,
and refuses force pushes and deletion. The instruction files and `tmp/` are excluded
through `.git/info/exclude`.
