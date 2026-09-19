# Phase 0: Skeleton

Status: complete. Every key of `10` section 4 is in the catalogue, the seven questions
of the previous stop are answered by D-152 and D-153 and implemented, and the full
gate is green.

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
| LIB-API-001 | AC2 | `SettingsCatalogueTests.LIB_API_001_AC2_TheKeyNamesAreTheContract`, `SettingsCatalogueTests.LIB_API_001_AC2_TheFamiliesAreTheContract`, `VocabularyContractTests.LIB_API_001_AC2_TheStepUpActionNamesAreTheContract`, `VocabularyContractTests.LIB_API_001_AC2_TheFactorIdentifiersAreTheContract`, `VocabularyContractTests.LIB_API_001_AC2_ThePolicyFieldValuesAreTheContract`, `VocabularyContractTests.LIB_API_001_AC2_TheClosedSetsOfSectionFiveAreTheContract`, `VocabularyContractTests.LIB_API_001_AC2_TheAlertConditionsAreTheContract`, `VocabularyContractTests.LIB_API_001_AC2_TheBotDefenceSignalsAreTheContract` |
| LIB-API-003 | AC1, AC2 | `ErrorTests.LIB_API_003_AC1_TheFailureCarriesNoProse`, `ErrorCodesTests.LIB_API_003_AC2_EveryCodeCarriesMeaningAndRemediation` |
| LIB-HOST-001 | AC1, AC2, AC3, AC4 first sentence | `SettingsCatalogueTests.LIB_HOST_001_AC1_EveryOtherKeyHasADefault`, `SettingsCatalogueTests.LIB_HOST_001_AC3_NoKeyOutsideTheDeclarationsIsRequired`, `StartupConfigurationTests.LIB_HOST_001_AC1_NamingOnlyTheDeclarationsStarts`, `StartupConfigurationTests.LIB_HOST_001_AC2_AMissingDeclarationNamesTheKey`, `StartupConfigurationTests.LIB_HOST_001_AC4_TheGoverningLanguageFailsWithItsNamedError` |
| LIB-PKG-001 | AC2 | `LibraryStructureTests.LIB_PKG_001_AC2_NoAreaDependsOnAnotherArea` |
| LIB-PKG-002 | AC1 | `LibraryStructureTests.CONV_LAYOUT_001_AC2_CoreCarriesNoPackage` |
| OPS-CFG-001 | AC1 | `SettingsCatalogueTests.Scope_TheCatalogue_ProtectsTheKeysSectionFourMarks`, `SettingsCatalogueTests.Scope_TheOpsCfg004List_IsProtectedInTheCatalogue` |
| OPS-CFG-003 | AC1, AC2, AC3 | `SettingsTests.OPS_CFG_003_AC1_APasswordFloorBelowTheStandardsMinimumIsRejected`, `SettingsTests.OPS_CFG_003_AC2_ASessionAbsoluteTimeoutAboveTheMaximumIsRejected`, `StartupConfigurationTests.OPS_CFG_003_AC3_AValueOutsideItsBoundsStopsStartup` |
| AUTH-STEP-002a | AC3 | `PolicyTests.AUTH_STEP_002a_AC3_TheSystemPolicyGatesEveryStepUpAction`, `PolicyTests.AUTH_STEP_002a_AC3_TheAdministrativePolicyGatesEveryStepUpAction` |
| INT-HOST-001 | AC2 | `StartupConfigurationTests.INT_HOST_001_AC2_HostingOutsideEgyptRequiresTheCrossBorderBasis`, `StartupConfigurationTests.INT_HOST_001_AC2_HostingInsideEgyptNeedsNoCrossBorderBasis` |
| `10` section 1.5, the error catalogue | Eighteen codes, each documented with its meaning and its remedy | `ErrorCodesTests.CONV_NAME_003_AC2_ChangingACodeFailsTheContractTest`, `ErrorCodesTests.CONV_NAME_003_AC1_EveryCodeCarriesMeaningAndRemediation` |
| `10` section 4, the key catalogue | One hundred and thirty keys and three families: every live key of the section, with the type, default, bounds and direction each row states | `SettingsCatalogueTests.LIB_API_001_AC2_TheKeyNamesAreTheContract`, `SettingsCatalogueTests.LIB_API_001_AC2_TheFamiliesAreTheContract`, `SettingsCatalogueTests.All_TheCatalogue_NamesEveryKeyOnce`, `SettingsCatalogueTests.Scope_TheCatalogue_ProtectsTheKeysSectionFourMarks`, the sixteen tests of `SettingsTests` |
| `10` section 4 preamble, the direction rule | A row's stated direction governs; otherwise the bounds and, for a flag, the default give it | `SettingsCatalogueTests.Loosening_AKeyWhoseRowNamesNoDirection_IsReadFromItsBounds`, `SettingsCatalogueTests.Loosening_ABoolean_LoosensAwayFromItsDefault`, `SettingsCatalogueTests.Loosening_AKeyWhoseRowNamesADirection_CarriesTheNamedOne` |
| `10` section 4 preamble, the holding rule | A year is held at 366 days and a month at 31 | `SettingsCatalogueTests.Default_ADurationInYearsOrMonths_IsHeldLong` |
| `10` section 4, the conditional declarations | The cross-border basis, the service name and the hosting environment are named only where their condition holds | `StartupConfigurationTests.INT_HOST_001_AC2_HostingOutsideEgyptRequiresTheCrossBorderBasis`, `...INT_HOST_001_AC2_HostingInsideEgyptNeedsNoCrossBorderBasis`, `...ThrowIfIncomplete_TheContextSourceIsOn_RequiresTheServiceName`, `...ThrowIfIncomplete_TheContextSourceIsOff_NeedsNoServiceName`, `...ThrowIfIncomplete_TheRegisterIsGenerated_RequiresTheHostingEnvironment`, `...ThrowIfIncomplete_NoRegisterIsGenerated_NeedsNoHostingEnvironment` |
| `10` section 4.1a, the policy object | The six fields, the system defaults and the administrative organization's | `PolicyTests.SystemDefault_EveryField_IsWhatSectionFourOneAStates`, `PolicyTests.AdministrativeOrganization_EveryField_IsWhatSectionFourOneAStates`, `PolicyTests.SystemDefault_EveryGate_IsReachableWithoutPhishingResistance`, `PolicyTests.AdministrativeOrganization_EveryGate_IsAal2AndPhishingResistant`, `PolicyTests.RequiredAssurance_ALevelOutsideTheField_Refused`, `PolicyTests.LoginFactors_TheEmergencyCredential_Refused` |
| `10` section 4.2, the two cross-key rules | The password floor pair and the Argon2id strength classes | `StartupConfigurationTests.AcceptPasswordFloorPair_WithMfaAboveSingleFactor_Refused`, `...AcceptPasswordFloorPair_EqualFloors_Accepted`, `...AcceptPasswordFloorPair_TheShippedDefaults_Accepted`, `...AcceptArgon2Cost_AStrengthClass_Accepted`, `...AcceptArgon2Cost_BelowEveryStrengthClass_Refused`, `...AcceptArgon2Cost_AboveAStrengthClass_Accepted`, `...AcceptArgon2Cost_TheShippedDefaults_Accepted` |
| `10` sections 5.20 to 5.23, the closed sets | Capability residuals, consent mechanism, age group, the thirty-one alert conditions | `VocabularyContractTests.LIB_API_001_AC2_TheClosedSetsOfSectionFiveAreTheContract`, `VocabularyContractTests.LIB_API_001_AC2_TheAlertConditionsAreTheContract`, `VocabularyContractTests.WireNames_EveryVocabularyMember_CarriesOne` |

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
| CONV-SETUP-004 | AC3 | Two suppressions exist: `src/Janus.Core/Error.cs`, CA1716, because CONV-DESIGN-005 names the failure an `Error`; `src/Janus.Core/Policy.cs`, CA1724, because `10` section 4.1a fixes the type name and the namespace the rule matches it against is never referenced here. Each carries its justification in the attribute. No `#pragma warning disable` exists anywhere |
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
| LIB-HOST-001 | AC1, second half | Every key that is not one of the fourteen declarations hands back a default rather than throwing, which `SettingsCatalogueTests.LIB_HOST_001_AC1_EveryOtherKeyHasADefault` reads for each in turn; that the fourteen are the right fourteen is AC3 |
| OPS-CFG-001 | AC1, second half | A redeploy-scoped setting that is not on the OPS-CFG-004 list is one LIB-HOST-001 declares: the origins, the hosting location and the cross-border basis. The fourth, `webauthn.algorithms`, is the relying party configuration AUTH-FACT-010 fixes beside `webauthn.rpid`, and section 3 below records the reading |
| OPS-DEP-002 | AC1, AC2 | The `Destructive-operation detection report` gate reports whether or not the gate variable is set |
| OPS-DEP-004 | AC1, AC2 | The `Secret scanning` gate runs gitleaks over the full history on every push, from its official action pinned to the release commit `e0c47f4f8be36e29cdc102c57e68cb5cbf0e8d1e`, with a committed `.gitleaks.toml` extending the default rule set and holding no allow-list entry |
| OPS-DEP-005 | AC1, AC2 | CONV-GATE-002 above |
| OPS-DEP-005 | AC3 | A monthly operator review; the first month has not elapsed |

## 2. Items in the phase not implemented

| Item | Reason | Waits on |
|---|---|---|
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
| `webauthn.rpid` | The row states no default, and LIB-HOST-001 lists the origins rather than the identifier among the declarations | AUTH-FACT-010, which derives the identifier from the origins and checks it is a registrable suffix of one | The default is the empty string, which means derived at startup from `webauthn.origins` |
| `abuse.sms.balancefloor` | The row states a prepaid amount and no default | LIB-HOST-001, which lists the SMS balance floor among the declarations because the library cannot guess it | A decimal setting the deployment names, with no default |
| `alerting.sms.severitythreshold` | The row states a severity, and section 4 names no severity vocabulary | OPS-ALERT-001, which names the two severities | A choice over `high` and `normal`, defaulting to `high` |
| `10` section 4, every row whose default is written `on` | The word is not one of the boolean literals the chapter writes elsewhere | The D-151 preamble, which derives a row's type from its default | `on` is the boolean true |
| `audit.enabled`, `token.signature.verification` | Neither row states a default | P-001, which puts a default at the safe end of its range | Both default to true |
| `hosting.location` | The row states no value vocabulary | INT-HOST-001, which reflects the value in generated records as inside or outside Egypt | A choice over `inside` and `outside` |
| `10` section 4.1a, `emailDomains` | The system default is written `off`, and the field is otherwise a list of domains | The field's own description, which reads `off` as the absence of a domain lock | The empty list |
| `10` section 4.1a, the system default gate | The gate is written `reachable`, floor `aal1`, which is four values where a gate has three | AUTH-STEP-002, which fixes a gate at level, phishing-resistance and maximum age | The floor belongs to the meaning of `reachable`, which resolves to the account's reachable assurance and never below AAL1 |
| `src/Janus.Core/Policy.cs` | The analyser refuses a type name that matches a namespace | `10` section 4.1a, which fixes the type name | CA1724 suppressed with the justification that the name is the chapter's and the `System.Security.Policy` namespace is never referenced in this code base |
| `tests/Janus.Core.Tests/PublicSurfaceTests.cs` | CONV-CODE-004 AC1 read every static field, including the delegate caches the compiler emits for a lambda | CONV-CODE-004, which is stated over mutable static state a person writes | The test skips a type the compiler generated |
| `tests/Janus.Core.Tests/ResultContractTests.cs` | CONV-DESIGN-005 AC2 read an unconstrained type parameter in a contract's return type as a lookup that can return null | CONV-DESIGN-005, whose rule is that a service method never returns null for not found | A type parameter a contract leaves open carries the nullability of the caller's own type argument, so the walk stops there |
| `Settings.AcceptPasswordFloorPair`, `Settings.AcceptArgon2Cost` | Two rules of section 4.2 hold over a pair of keys, and a setting validates one value | OPS-CFG-003, which requires the rejection and names no place for it | The two rules sit on the catalogue rather than on either setting, and the failure names the key the chapter states the rule on |
| `06` OPS-CFG-004 | Its list does not name `privacy.calendar.timezone`, which `10` section 4.8 states is on it | `10` section 4.8, together with OPS-CFG-004's own sentence that the two are one list, and the working guide's section 2.3, which makes `10` authoritative for names | The protected list the catalogue and its test carry is section 4.8's table |
| `webauthn.origins`, `webauthn.algorithms`, `hosting.location`, `hosting.crossborderbasis` | Each row marks the key **P**, and no key appears in section 4.8 or in the OPS-CFG-004 list | OPS-CFG-004's sentence that a key marked protected in `10` appears in both lists or in neither | The catalogue carries the mark each row gives, so all four are protected |
| `07` LIB-HOST-001 | The `10` section 4 preamble says the three conditional keys are listed there, and `service.name` and `hosting.environment` are not | The section 4 preamble, which states both with no default and startup failing when the condition holds | The catalogue marks a key required from its own section 4 row, and the condition is read from AUTH-PASS-004 and PRIV-ROPA-001 |
| `notification.email.relayregistered` | The row states a set of domains, and the setting types held no shape for a set whose members the deployment names | The section 4 value-type rule, which reads the type from the row | A `TextSetSetting` beside `TextListSetting`, of the same shape |
| `backup.restoretest.canary` | The row states "set at bootstrap" and no default, and the key is not one of the deployment declarations | DR-007 and OPS-BOOT-001, where bootstrap seeds the canary subject | The default is the empty string, which means unseeded, as `webauthn.rpid` already reads "derived" |
| `registration.phone` | The row states that moving to `optional` is loosening, and a direction is an increase, a decrease or any change | The row, together with the vocabulary's own order `off`, `optional`, `required` | The loosening is the decrease along that order |
| `.github/gates/commit-message.sh`, `.github/gates/changelog.sh` | Both took the push event's previous commit as the start of the range, which a force-pushed branch leaves unreachable, so the gate failed for a reason that was not a commit message or a changelog line | CONV-GATE-001, under which a gate reports on the work rather than on the push that carried it | Where the base is unreachable the range starts at the fork point from the default branch |
| `docs/guide/implementation-plan.md` | Phase 0 and Milestone 2 step 7 say eight required values | The `10` section 4 preamble and LIB-HOST-001, which now state eleven with three conditional (D-153) | The catalogue marks required from the section 4 rows; the plan is not changed from the code side |

## 4. Open questions

None.

## 5. Gate result

Fast checks on every commit of the phase: `dotnet build Janus.slnx` with warnings as
errors and analysers at latest-all, `dotnet format Janus.slnx --verify-no-changes`,
`dotnet restore Janus.slnx --locked-mode`, and the test suite. Green on each. Every
commit of the branch was also built from a clean worktree to confirm the build is
green at each.

`dotnet test` on the development machine reports that no tests ran: the test host ends
during its start-up when the command line drives it in server mode. The pipeline runs
`dotnet test` unchanged and green, so the suite was run locally by executing the test
binaries, which runs the same tests.

Full gate: GitHub Actions runs `35396808643` and `35396820403` on branch
`phase-00-configuration`, and runs `35430723911` (push) and `35430728718` (pull
request) on branch `phase-00-values`. The push run is green on all seventeen jobs; the
pull-request run is green on sixteen and skips `Secret scanning`, which CONV-GATE-002
runs on the push event only. The commit carrying this sentence re-runs both before the
merge. The integration, migration and conformance suites do not exist in this phase;
the three rows of CONV-GATE-001 that run them are listed in section 2.

Tests: 133, all passing. `tests/Janus.Core.Tests` 118, `tests/Janus.Analyzers.Tests`
15.

Repository, per the working guide's section 5: private repository `neolorn/janus`,
default branch `main`, squash and rebase merges off, merge commits on, delete branch on
merge on, dependency vulnerability alerting and automated security updates on, platform
secret scanning unavailable on this plan as `08` section 10 records, so the scanning
OPS-DEP-004 requires is the gitleaks gate (D-150). Branch protection on `main`
requires the seventeen status checks, applies to administrators, requires no approval,
and refuses force pushes and deletion. The instruction files and `tmp/` are excluded
through `.git/info/exclude`.
