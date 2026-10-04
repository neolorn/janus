# Corrections 4: D-166 and D-167

Status: complete but for the parked items. D-168 settled questions 1 and 2 of the first
stop, D-169 question 3, D-170 questions 4 to 6, D-171 question 7, D-172 questions 8 and
9, D-173 question 10, D-174 question 11, D-175 question 12, D-176 question 13, D-177
question 14, D-178 questions 15 and 16, D-179 question 17, D-180 questions 18 and 19 and
D-181 question 20; all are applied. Under D-182 a question parks its item and not the
run: the rest of D-166 is applied, on the working branch and in seven parts
(`part/privacy`, `part/authorization`, `part/organizations`, `part/sending`,
`part/sessions`, `part/registration-accounts`, `part/gates`) merged into it one at a
time, but for the items the open questions park and 209 (2) (section 2). Questions 21 to
65 are open (section 4); question 66 was withdrawn. The push of `corrections-4` after
`d5a7fc0e` was refused in the session's environment, so the branch is unpushed from
`d5a7fc0e` on and the pull request waits on the push.

## 1. Items implemented

### On `phase-10-release`, merged as `b6d14fe` (pull request #6)

| Instruction | Commit | Items | Tests |
|---|---|---|---|
| The changelog gate and `truth-table-change.sh` fail on output larger than the pipe buffer | `551d6ee` | CONV-VCS-005, CONV-VCS-004, CONV-DEP-002, LIB-VER-001 | No test can decide it. Verified by scenario: in a Linux container, each of the four gates was run against a scratch repository whose `git diff` output is larger than the pipe buffer. Every scenario passed with the fix. The unfixed gates were run for comparison (section 3) |
| Truth-table rows for every permission decision the phase 10 changes touch | `ce19b71` | AUTHZ-TEST-001, AUTHZ-INHERIT-002, AUTHZ-PRIN-003, CONV-DESIGN-002 AC3, AUTHZ-SCOPE-001, IDN-ACCT-007 AC2, AUTHZ-GATE-005 AC1 | `TruthTableTests.AUTHZ_TEST_001_AC2_EveryCaseDecidesTheSameWayThroughBothPathsAsync`, `TruthTableTests.AUTHZ_TEST_001_AC3_EveryCaseDecidesTheSameWayMaterialisedAsync`, `TruthTableTests.AUTHZ_PRIN_001_AC1_TheCheckAndTheFilterAgreeOnEveryCaseAsync`, `TruthTableTests.AUTHZ_GATE_002_AC2_EveryCaseIsEqualAcrossBothRenderingsAsync`, `TruthTableTests.AUTHZ_TEST_001_AC1_EveryOperationCaseDecidesTheWayTheTableSaysAsync` (64 cases) |
| The conformance failure, root cause | `b9fbbbf` | LIB-TEST-001, AUTH-OIDC-006 AC1 | `ConformanceSuiteTests.AUTH_OIDC_006_AC1_TheSampleHostsProviderRefusesEveryRetiredFormAsync`, `ConformanceSuiteTests.AUTH_OIDC_006_AC1_AProviderAdmittingWhatItShouldRefuseIsReportedAsync` and the rest of the suite, which runs the sample host through the command |
| D-166 E.6, applied early | `e794b99` | IDN-PRIN-003, INF-BG-001, OPS-OBS-003 | `BackgroundJobsTests.IDN_PRIN_003_AC4_AnEventEveryConsumerTookIsClearedWithNobodyAskingAsync`, `BackgroundJobsTests.INF_BG_001_AC1_EveryJobRunsWithoutAPersonAsync`, `BackgroundJobsTests.OPS_OBS_003_AC1_WhatHasLapsedIsClearedWithNobodyAskingAsync` |
| REG_SESS_003, applied early | `cdc185a` | REG-SESS-003 | `RegistrationSignalsTests.REG_SESS_003_AWaitNothingSignalsEndsOnTheIntervalAsync`, `RegistrationSignalsTests.REG_SESS_003_AWaitHearsTheCommittedAnnouncementAndNoOtherAsync` |

**Truth-table rows.**
- **Records table.** Every case is run through the check, the expression and the fragment. Three rows were added:
  - a grant on the container the record was moved into is Allowed, and one on the container it was moved out of is Denied (AUTHZ-INHERIT-002, `f5f2b08`);
  - a record of a type the model does not declare is Raised (`56fb839`).
- **Operations table.** Each case is run through the operation's own gate step; nine rows:
  - a revocation of a grant no row names, and a change to a group no row names: Denied for a caller who manages grants, Restricted for a restricted caller (`6a4488e`, IDN-ACCT-007 AC2);
  - a grant on a record no registration names: Denied;
  - a caller's own settings: Allowed, and Restricted for a restricted caller (`e0da33c`);
  - a record on a page: Allowed with the consent the page needs, ConsentRequired without it (`a86b9ba`).

The outcomes are the `Decided` enumeration: Allowed, Denied, Restricted, ConsentRequired, Raised.

**Rows not written, because D-166 reverses the decision the change made:**
- `69fa524` leaves an undeclared permission out of capabilities. D-166 entry 396 raises instead.
- `27ea1c0` rewrites `GrantStore.OnAsync` into a reverse lookup. D-166 entry 265 reverses it.
- `223eda2` refuses a consent basis for the hosting purposes at startup. D-166 supersedes it.

The rows that state the D-166 outcomes (an undeclared permission is Raised; a lookup of an unregistered record is Denied) belong with those fixes (section 2).

**Commits touched by the phase 10 changes that make no permission decision:**
- `333fc4d`: the refusal names the grant that decided it; the outcome is unchanged.
- `ebc304e`: the retention floor.
- `8a93008` and `48e405f`: the maintenance role's column privileges.
- `38268f1`: sensitive registration, a seam with no access context. D-166 entry 352 moves its codes under X5.

**Conformance, root cause.**
- SDK 10.0.303 brings `Microsoft.AspNetCore.App.Ref` 10.0.11, which ships `Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.11 at the same version as the package.
- Conflict resolution keeps the framework's copy, so the package assembly is left out of the conformance test output.
- The command's runtime configuration names only `Microsoft.NETCore.App`, so the command, run from the test output, failed to load the assembly (`FileNotFoundException`).
- Under SDK 10.0.302 (reference pack 10.0.10) the package's copy wins and is copied, which is why the failure followed the SDK.
- The command's own build output holds both assemblies. The test now runs the command from there, a path written into the test assembly's metadata at build time.
- No change was made to `Janus.Cli` or its packaging.
- Proved in a Linux container on SDK 10.0.303 with the pipeline's exact `dotnet test` command: the conformance suite passed.

**REG_SESS_003, applied early.**
- Pull request run 36215901211 on `ce19b71` failed only `REG_SESS_003_AWaitHearsTheCommittedAnnouncementAndNoOtherAsync`: a rolled-back announcement was "heard" at 0.2498 s, against the 0.25 s floor. The push run on the same commit, 36215899821, was green.
- Following the owner's instruction, the floor was not lowered and the run was not re-run.
- The wait's interval is taken on the `TimeProvider` (`Task.Delay(interval, time, ct)`). The tests now move a hand-written fake clock (`ManualTime`) that fires timers only when advanced. A test cannot hang: each wait for a timer is bounded.
- Mutation check: with the delay taken on the wall clock, both tests fail.
- The same failure occurred once before, on pull request run 36201939031 (corrections-3), attempt 1. That attempt was re-run to green before the owner's instruction; the re-run is attempt 2.

**E.6 and `cdc185a` were applied on `phase-10-release`, not on this branch.** E.6 was applied there as the owner directed. `cdc185a` was applied there because pull request run 36215901211 failed on it.

### D-167, on `corrections-3`, merged with pull request #4

| Decision | Commit | Items | Verified by |
|---|---|---|---|
| D-167 item 1, the scanner's own release at a pinned version and SHA-256, over the full history on every push | `da3ab5f` | OPS-DEP-004 | Push run 36201534531, job `Secret scanning` 108289029074: the full history was scanned with no finding |
| D-167 item 3, the entry for `PRIV-BREACH-002` in `src/Janus.Core/IAuditTrail.cs` | `d34f24a` | OPS-DEP-004 | The same job |
| D-167 item 4, the offline leaked-password list's line form | `7a4d4dd` | OPS-DEP-004 | The same job |
| The allow-list paths quoted as literal text | `3d1b5d8` | OPS-DEP-004 | Run 36201534531 failed `ProductNameTests.CONV_NAME_001_AC2_TheProductNameAppearsOnlyInNamespacesIdentifiersAndTheEntryPoint`: the escaped dots in the entries' paths left the product name followed by a backslash. Push run 36201936626 and pull request run 36201939031 were green |

### On `corrections-4`

| Instruction | Commit | Items | Tests |
|---|---|---|---|
| The corrected documentation (stash `d-167b-docs`) | `6866d9c` | none | none |
| D-166 item 114, the draw, committed under `tools/` with its checkpoints and user agent, and a test against a local fake | `d283185` | AUTH-PASS-004, CONV-VCS-005 | `test_draw.DrawTests.test_CONV_VCS_005_AC4_EveryRangeRequestNamesTheDraw`, `test_draw.DrawTests.test_AUTH_PASS_004_TheListIsTheMostPrevalentHashesInOrderUnderItsDate`, `test_draw.DrawTests.test_CONV_VCS_005_AC4_AStoppedDrawResumesFromItsCheckpoint` (`python -B -m unittest discover tools/leaked-passwords`) |
| D-166 item 114, the 100,000 most prevalent hashes in place of the old list, with the `NOTICE` paragraph | `52e4b29` | AUTH-PASS-004, INT-PWD-002 | `OfflineCorpusTests.AUTH_PASS_004_TheShippedListIsTheHundredThousandItNames`, `LibraryStructureTests.AUTH_PASS_004_TheNoticeNamesTheLeakedListsSource` |
| Secret scanning before the push | `9e6f4d3` | OPS-DEP-004 | The pinned scanner run locally, as the pipeline runs it, over every commit (section 3) |
| The documentation of D-168 | `3f1cb1c` | none | none |
| D-168 question 1: the package identifier and version written by the build as internal constants after MinVer sets the version, in the intermediate output, never committed | `0f4c5e1` | CONV-CODE-004, CONV-NAME-001, LIB-VER-001 | `LibraryIdentityTests.CONV_CODE_004_AC3_TheVersionConstantIsThePackageVersionWithoutBuildMetadata`, `LibraryIdentityTests.CONV_CODE_004_AC3_TheConstantsAreGeneratedAndNeverCommitted`, `LibraryIdentityTests.CONV_CODE_004_AC3_NoProjectFileSetsAVersion`, `PublicSurfaceTests.CONV_CODE_004_AC2_AnAssemblyIsReachedOnlyForItsOwnResources`, `PublicSurfaceTests.CONV_CODE_004_AC2_NoShippedFileUsesReflection` |
| D-168 question 1: the range requests' user agent, `<package identifier>/<package version>` | `ad7acd9` | INT-PWD-001 AC3 | `ScreeningTests.INT_PWD_001_AC3_EveryRangeRequestNamesTheLibraryAndItsVersionAsync` |
| D-168 question 2: the library's text on what a host clears on `ErasureRequested` | `0961899` | PRIV-RIGHT-005b | No test can decide it. Verified by reading: the summary of `ErasureRequested` was the one library text that told a host to redact its fields; the library holds no message catalogue |
| D-166 item 114 and R1: the English and Arabic word lists embedded, `WordList.Directory`, `WordsFile` and the path `AddJanus` built removed, the optional `DictionaryWords` declaration, the `NOTICE` line, and the ledger line under entry 114 | `9b77185` | AUTH-PASS-004 AC8, LIB-HOST-001 | `WordListTests.AUTH_PASS_004_AC8_TheEnglishListHoldsTenThousandLowerCaseWords`, `WordListTests.AUTH_PASS_004_TheArabicListCarriesArabiziFormsAndTheirVariants`, `WordListTests.AUTH_PASS_004_AnArabiziFormIsMatchedByTheArabicListAsync`, `WordListTests.AUTH_PASS_004_DigitsCountTowardTheFourCharactersAMatchNeedsAsync`, `WordListTests.AUTH_PASS_004_AWordTheHostDeclaresIsMatchedAsync`, `WordListTests.AUTH_PASS_004_AListThePackageCannotOpenAnswersTheScreeningFailureAsync`, `ScreeningTests.AUTH_PASS_004_AC8_AnArabiziFormIsRefusedAndNothingNamesTheWordAsync`, `ScreeningTests.ScreenAsync_TheDictionarySourceWithAListItCannotOpen_RefusesAsync`, `ScreeningTests.ScreenAsync_TheDictionarySourceAndAListedWord_RefusesAsync`, `LibraryStructureTests.AUTH_PASS_004_TheNoticeAcknowledgesTheEnglishWordList`. The build's refusal below 10,000 words is verified by mutation (below) |
| D-166 R3, the Public Suffix List embedded unmodified with its header and date, read over both sections, for the relying party identifier and the label count | `bb59be1` | AUTH-FACT-010 AC1, AC2, AUTH-FACT-012 AC2 | `RelyingPartyTests.AUTH_FACT_012_AC2_ShopComAndShopCoUkCountAsOneLabel`, `RelyingPartyTests.AUTH_FACT_012_AC2_ACoUkAndBCoUkCountAsTwoLabels`, `RelyingPartyTests.AUTH_FACT_012_AC2_ThePrivateSectionCountsAsTheIcannSectionDoes`, `RelyingPartyTests.AUTH_FACT_010_AC1_AnIdentifierThatIsAPublicSuffixIsRefused`, `RelyingPartyTests.AUTH_FACT_010_AC2_TheDerivedParentIsARegistrableDomain`, `PublicSuffixListTests.AUTH_FACT_010_TheShippedListIsUnmodifiedWithItsHeaderAndDate`, `PublicSuffixListTests.AUTH_FACT_012_AC2_TheLabelIsTheOneBeforeThePublicSuffix`, `PublicSuffixListTests.AUTH_FACT_010_WildcardAndExceptionRulesAreRead`, `PublicSuffixListTests.AUTH_FACT_010_ANameNoRuleCoversEndsAtItsLastLabel`, `LibraryStructureTests.AUTH_FACT_010_TheNoticeNamesThePublicSuffixList` |
| The documentation of D-169 | `e636451` | none | none |
| DR-007 AC2, a run the objective cut short recorded as `overrun` (section 3) | `021060f` | DR-007 AC2, AC3 | `RestoreTestTests.DR_007_AC2_TheMeasuredTimeIsRecordedAgainstTheObjectiveAsync`, now timed on a clock the test moves |
| D-169, the 3esl source committed byte for byte as 12dicts publishes it (section 3) | `539a4dc` | AUTH-PASS-004 | `WordListTests.AUTH_PASS_004_TheEnglishSourceIsTheListAsItsPackagePublishesIt` |
| D-166 D.8, 124, 179 and 407: every runtime change needs a reason, a restriction tightening included, under `config.change.reasonrequired`; past 1024 characters, 400 `api.request.malformed` naming `reason` | `6651cc4`, `da0242b` | OPS-CFG-005, OPS-CFG-008, API-CONV-002 | `RestrictionAdministrationTests.OPS_CFG_008_AC2_ARestrictionTighteningWithNoReasonIsRefusedAsync`, `ConfigurationEndpointTests.OPS_CFG_005_EveryChangeCarriesAReasonAsync`, `ConfigurationAdministrationTests.API_CONV_002_AReasonPastItsBoundIsRefusedAsync`, `ConfigureTests.OPS_CFG_004_AChangeWithoutAReasonIsRefusedAsync` |
| D-166 F: `config.change.stepuprequired` and `auth.device.verificationrequired` retired | `7121a0a` | REF-001 | `ErrorCodesTests` (the catalogue) |
| D-166 F: `model.startup.secretunavailable`, `details.key` naming the secret or `input` | `fccbb6c` | OPS-SEC-001 | `BootstrapRefusalTests.OPS_SEC_001_TheCommandRefusesADocumentThatDoesNotReadAsync`, `BootstrapRefusalTests.OPS_SEC_001_TheCommandRefusesATerminalAsync`, `KeyRotationTests`, `FingerprintKeyRotationTests` |
| D-166 D.8, 319: `audit.enabled`, `token.signature.verification` and `stepup.enforcement.<organization>` retired | `e753434` | OPS-CFG-004 | `ConfigureTests.OPS_CFG_004_ARetiredSwitchIsRefusedAsync` |
| D-166 D.8, 404: the protected keys are chapter 10 section 4.8 alone; `integration.mailserver.endpoint` added with the startup endpoint rule | `c440f0a` | OPS-CFG-001, OPS-CFG-004, INT-GEN-001 | `SettingsCatalogueTests.OPS_CFG_001_AC1_ARedeployScopedKeyIsOnTheOpsCfg004List`, `SettingsCatalogueTests.OPS_CFG_004_AKeyMarkedProtectedIsOnTheOneList`, `SendingValidationTests.INT_GEN_001_AC1_APlaintextMailServerEndpointStopsStartupAsync` |
| D-166 D.8, 305: `outbox.poll.interval` capped at `PT1M`; the balance poll fails with no `ISmsTransport` (section 3); startup reads every key of the catalogue | `1e093aa`, `f64e31b` | OPS-CFG-003, INT-SMS-004 | `SettingsCatalogueTests.OPS_CFG_003_TheOutboxIntervalHasItsCeiling`, `ConfigurationAdministrationTests.OPS_CFG_003_AnOutboxIntervalAboveItsCeilingIsRefusedAsync`, `BackgroundJobsTests.INT_SMS_004_TheBalancePollFailsWithoutATransportAsync`, `StartupValidationTests.OPS_CFG_003_AC3_AStoredValueAboveItsCeilingStopsStartupAsync` |
| D-166 D.8, 334 part (3): `backup.restoretest.interval` default and ceiling `P90D` | `5971c2f` | DR-007 | `RestoreTestTests.DR_007_AC1_NoIntervalExceedsTheShortestQuarter`, `RestoreTestTests.DR_007_AC1_TheTestRunsAtItsIntervalWithoutAPersonAsync`, `SettingsCatalogueTests.Default_ADurationInYearsOrMonths_IsHeldLong` |
| D-166 D.8, 116 and rule X2: a stored value that does not read is a fault naming the key and the code, never the text; every fallback removed (below) | `c3b1120` | OPS-CFG-003, OPS-CFG-008 | `ConfigurationStoreTests.ReadAsync_AStoredValueThatDoesNotParse_IsAFaultAsync`, `AccountLifecycleTests.OPS_CFG_008_AMalformedGraceIsAFaultAndNotAnElapsedWindowAsync` |
| D-166 D.8, 193: `IConfigurationStore` only reads; the write is the internal port `IConfigurationWrites`, taken by `ConfigurationAdministration` alone | `bcdabb0` | OPS-CFG-005 | `PublicSurfaceTests.OPS_CFG_005_TheConfigurationStoreOnlyReads` |
| D-166 D.8, 178, under rule X3: a runtime change is read and classified under its row lock, on every route | `f604174` | OPS-CFG-002 AC6 | `ConfigurationLockTests.OPS_CFG_002_AC6_AChangeWaitsForAConcurrentOneAndClassifiesAgainstItAsync`, `OrganizationPolicyEndpointTests.OPS_CFG_002_AC6_APolicyChangeIsDecidedUnderItsRowsLocksAsync`, `OrganizationDomainEndpointTests.OPS_CFG_002_AC6_AListChangeIsDecidedUnderItsRowLockAsync` |
| D-166 D.8, 180 and 181: `retention.<category>` of each declared category served, read and changed through the route | `0c34a31`, `4d27666` | PRIV-RET-001, OPS-CFG-004 | `ConfigurationEndpointTests.PRIV_RET_001_ACategorysRetentionIsChangedThroughTheRouteAsync`, `ConfigurationEndpointTests.PRIV_RET_001_AnUndeclaredCategoryIsNoKeyAsync`, `ConfigurationEndpointTests.OPS_CFG_004_AKeyReadsWithItsDefaultAndWhetherItIsProtectedAsync`, `ConfigurationEndpointTests.OPS_CFG_008_AChangedKeyReadsBackBesideItsDefaultAsync` |
| D-166 D.8, 319 fixes (1) to (3): the start checks over a protected change; `before` the value in force; the principal named and read by | `aa76682`, `c9f901f`, `1bc398a` | OPS-CFG-004, OPS-CFG-005 | `ConfigureTests.OPS_CFG_004_ARelyingPartyIdentifierNoOriginSharesIsRefusedAsync`, `ConfigureTests.OPS_CFG_004_AC2_AProtectedKeyIsChangedFromTheServerWrittenDownAndRaisedAsync`, `ConfigureTests.OPS_CFG_005_AChangeRecordsTheValueInForceAsWhatItWasAsync`, `ConfigurationAuditTests.OPS_CFG_005_AC2_TheRecordsAreQueryableByPrincipalAsync`, `ConfigurationAuditTests.OPS_CFG_005_AC1_TheRecordCarriesBeforeAndAfterAsync` |
| D-166 D.8, 329: turning the export step-up off raises `stepup-policy-weakened`; the expiry sweep clears `bulk_exports` past the hour | `1d6d0cb` | OPS-ALERT-001, IDN-PRIN-003 | `ConfigurationEndpointTests.OPS_ALERT_001_TurningExportStepUpOffRaisesStepUpPolicyWeakenedAsync`, `ConfigurationAdministrationTests.OPS_ALERT_001_AnExportStepUpTurnedOffWithoutItsAlertIsNotMadeAsync`, `BackgroundJobsTests.IDN_PRIN_003_AC4_AnExportPastItsHourIsClearedWithNobodyAskingAsync` |
| D-166 D.8, 308, 310, 313, 157, 290 (bootstrap and `configure`), parts (1) to (4) | `4e50120`, `f33bd2d`, `ba1c94a`, `82fa429` | AUTH-FACT-010, AUTHZ-GRANT-003, OPS-ALERT-001, OPS-SEC-001 | `BootstrapRefusalTests.AUTH_FACT_010_AC1_BootstrapRefusesAnIdentifierNoOriginSharesAsync`, `RelyingPartyTests.AUTH_FACT_010_AC1_OriginsThatShareNoDomainAreRefused`, `BootstrapTests.AUTHZ_GRANT_003_TheFirstGrantsNameNoPersonAsTheirGranterAsync`, `BootstrapTests.D_162_EachMembershipBootstrapAttachesEmitsMembershipChangedAsync`, `BootstrapTests.OPS_ALERT_001_TheMissingEmergencyCredentialIsAnnouncedAsync`, `ProtectedConfigurationTests.OPS_ALERT_001_AProtectedChangeIsAnnouncedAsync`, `ProtectedConfigurationTests.OPS_ALERT_001_AProtectedChangeWhoseAlertIsNotRaisedIsNotMadeAsync`, `CommandTests.OPS_SEC_001_ACommandTheExecutableDoesNotCarryIsRefusedAsync`, `CommandTests.OPS_SEC_001_AnInvocationNamingNoCommandIsRefusedAsync` |
| D-166 D.8, 290 fixes (1) to (3): a deduplication key claimed by one conditional statement; the lapse of `alert-dispatch` delivered from the worker; the raise test renamed | `f042d8c`, `10b05a3`, `7f10533` | OPS-ALERT-001, OPS-ALERT-002, INF-BG-001 | `AlertLedgerTests.OPS_ALERT_002_TwoOverlappingPassesDeliverOneAlertAsync`, `AlertLedgerTests.OPS_ALERT_002_AKeyIsClaimedAgainOnlyAfterItsWindowAsync`, `BackgroundWorkerTests.OPS_ALERT_001_AC4_TheLapseOfAStalledCarrierReachesTheDestinationsAsync`, `AlertChannelsTests.CONV_DESIGN_002_AnEventWhoseRowCannotBeWrittenFailsTheRaiseAsync` |
| D-166 D.8, 320: `AddJanus` registers `IEvents` as `EventOutbox` whatever the host registered before | `b28ff29` | CONV-DESIGN-002 | `EventPublisherTests.CONV_DESIGN_002_AnIEventsTheHostRegisteredFirstDoesNotBypassTheRowAsync`, `StartupValidationTests.INT_MAIL_011_AC1_AnUndeclaredSendingDomainWarnsAsTheDeploymentStartsAsync` |
| D-166 D.8, break-glass part (1): `breakglass-generated`, High, to the owner regardless | `72f0e85` | OPS-BOOT-004, OPS-ALERT-001, OPS-ALERT-004 | `AlertsTests.OPS_ALERT_001_AGeneratedBreakGlassCredentialTakesTheNextFreeValue`, `AlertRouterTests.OPS_ALERT_004_AC3_ABreakGlassUseReachesTheOwnerRegardlessAsync`, `BreakGlassEndpointTests.OPS_BOOT_004_AC2_GenerationIsAuditedAndReachesTheOwnerAsync`, `BreakGlassEndpointTests.OPS_BOOT_004_AC3_ASecondGenerationInvalidatesTheFirstAsync`, `VocabularyContractTests` |
| D-166 D.8, break-glass part (2): the global limit reached raises `auth-failures-sustained` | `44dfdc5` | OPS-BOOT-004 AC7 | `BreakGlassEndpointTests.OPS_BOOT_004_AC7_TheLimitReachedIsRaisedAsync` |
| D-166 D.8, break-glass part (3): no provider link for the reserved account | `4c63f3d` | OPS-BOOT-002 | `BreakGlassEndpointTests.OPS_BOOT_002_NoSignInMethodIsGivenToTheReservedAccountAsync` |
| D-166 D.8, break-glass part (4): an idle break-glass session asks for a full sign-in | `9acc016` | OPS-BOOT-002 | `SessionServiceTests.OPS_BOOT_002_AnIdleBreakGlassSessionAsksForAFullSignInAsync`, `BreakGlassEndpointTests.OPS_BOOT_002_AC2_TheSessionEndsAfterItsInactivityWindowAsync` |
| D-166 D.8, break-glass part (5): `IBreakGlass` in `Janus.Core`, generation and the standing read as a contract | `1867ce3` | LIB-API-005 | `IdentityEndpointsTests.LIB_API_005_AC3_EveryEndpointResolvesExactlyOneServiceOfTheContractAsync`, `BreakGlassServiceTests.LIB_API_005_AC2_AnInProcessGenerationIsHeldToTheEndpointsChecksAsync` |
| D-166 D.8, break-glass part (6): codes drawn from the injected generator; the sweep for static randomness and clock reads | `0dc0ae0` | CONV-DESIGN-007 AC2 | `LibraryStructureTests.CONV_DESIGN_007_AC2_NoFileOutsideTheTestsReadsTheClockOrDrawsStaticRandomness`, `BreakGlassCodeTests.CONV_DESIGN_007_ACodeIsDrawnFromTheInjectedGenerator`, `RecoveryCodeServiceTests.CONV_DESIGN_007_ACodeIsDrawnFromTheInjectedGenerator`, `VerificationCodesTests.CONV_DESIGN_007_ACodeIsDrawnFromTheInjectedGenerator` |
| The documentation of D-170 | `0483bba` | none | none |
| D-170 item 1, break-glass part (7): the reason given at use kept by the session and written on every record the session writes, in a column of the record's own, read back as `breakGlassReason`; none on background work | `a1f64a1` | OPS-BOOT-002 AC10, PRIV-BREACH-002 AC4, IDN-AUD-001 | `BreakGlassEndpointTests.OPS_BOOT_002_AC10_TheReasonIsRequiredWithTheCredentialAsync`, `BreakGlassEndpointTests.OPS_BOOT_002_AC10_TheUseCarriesTheReasonAsync`, `BreakGlassEndpointTests.OPS_BOOT_002_AC10_ARoleDefinedInTheSessionCarriesTheReasonAsync`, `BreakGlassEndpointTests.OPS_BOOT_002_AC10_ARecoveryApprovedInTheSessionCarriesTheReasonAsync`, `BreakGlassEndpointTests.OPS_BOOT_002_AC10_AConfigurationChangeInTheSessionCarriesTheReasonAsync`, `BreakGlassEndpointTests.OPS_BOOT_002_AC10_BackgroundWorkTheSessionCausedCarriesNoneAsync`, `SessionServiceTests.OPS_BOOT_002_AC10_TheBreakGlassSessionKeepsTheReasonAsync`, `SessionStoreTests.OPS_BOOT_002_AC10_TheSessionKeepsTheReasonGivenAtItsUseAsync`, `AuditStoreTests.OPS_BOOT_002_AC10_TheTrailReadsTheBreakGlassReasonBackAsync`, `AuditStoreTests.OPS_BOOT_002_AC10_NoBackgroundRecordCarriesTheReasonAsync`, `AuditTrailEndpointTests.OPS_BOOT_002_AC10_TheReadReturnsTheBreakGlassReasonAsync` |
| D-170 item 2: bootstrap's seed records the written default as `before` where no row stood, nothing only for a required key | `aec590e` | OPS-CFG-005 | `BootstrapTests.OPS_CFG_005_BootstrapRecordsWhatEachKeyWasBeforeItAsync` |
| D-170 item 3: the limit alert with no scope on a deployment with no reserved account (no code change) | `5ad27b4` | OPS-BOOT-004 AC7 | `BreakGlassEndpointTests.OPS_BOOT_004_AC7_TheLimitReachedIsRaisedWithNoReservedAccountAsync` |
| D-166 D.8, break-glass part (8): `GET /admin/break-glass`; the ledger lines of 291, 295, 297, 302 and 331 | `221654d` | OPS-BOOT-001 AC3 | `BreakGlassEndpointTests.OPS_BOOT_001_AC3_TheAbsenceIsReadByEverySystemAdministratorAsync`, `BreakGlassEndpointTests.OPS_BOOT_001_AC3_OnlyASystemAdministratorReadsTheStandingAsync` |
| The documentation of D-171 | `24bccd9` | none | none |
| The documentation of D-172 | `1653155` | none | none |
| The documentation of D-173 | `5e6faec` | none | none |
| D-172 item 2 as D-173 corrects it, but for the check constraint: the deployment key under the max UUID of RFC 9562, erasure's refusal of it, the field cipher's values for rows of no subject bound to their own row (D-166 234 for invitations), and the migration that moves the key and refuses where a value bound the old way stands | `c656253` | PRIV-RIGHT-005a AC18 | `DeploymentDataKeyTests.PRIV_RIGHT_005a_AC18_TheDeploymentKeyIsHeldUnderTheMaxUuidAsync`, `DeploymentDataKeyTests.PRIV_RIGHT_005a_AC18_TheMigrationMovesTheKeyToTheMaxUuidAsync`, `DeploymentDataKeyTests.PRIV_RIGHT_005a_AC18_TheMigrationRefusesNamingTheTableWhereAValueBoundTheOldWayStandsAsync` (four cases), `SubjectEraserTests.PRIV_RIGHT_005a_AC18_AnErasureNamingTheMaxUuidIsRefusedAndLeavesTheRowAsync`, `InvitationStoreTests.PRIV_RIGHT_005a_AC18_AnInvitationsValueDoesNotOpenOnAnotherRowAsync`, `MailboxStoreTests.PRIV_RIGHT_005a_AC18_AnUnheldAddressDoesNotOpenOnAnotherRowAsync`, `SendOutboxTests.PRIV_RIGHT_005a_AC18_AMessageNamingNoSubjectDoesNotOpenOnAnotherRowAsync`, `RegistrationSessionStoreTests.PRIV_RIGHT_005a_AC18_StagedValuesDoNotOpenOnAnotherRowAsync` |
| The documentation of D-174 | `04841cb` | none | none |
| D-174: `SubjectId` refuses the max UUID; every column of a library-owned table that can hold a subject identifier refuses it by a check constraint (49 columns in the model and the audit record's two identities); the subject-key table's key and the rotation's cursor typed as a row of that table (`SubjectKeyId`) | `ae04c76` | PRIV-RIGHT-005a AC18, OPS-SEC-003, CONV-DESIGN-004 | `SubjectIdTests.PRIV_RIGHT_005a_AC18_NoSubjectIsMadeFromTheMaxUuid`, `SubjectIdTests.PRIV_RIGHT_005a_AC18_TheNilSubjectIsStillMade`, `SubjectIdTests.PRIV_RIGHT_005a_AC18_AWrittenSubjectReadsBackThroughTheRefusal`, `SchemaContractTests.PRIV_RIGHT_005a_AC18_EveryColumnThatCanHoldASubjectRefusesTheMaxUuidAsync`, `SchemaContractTests.PRIV_RIGHT_005a_AC18_TheSubjectKeyTableAndTheRotationCursorNameARowAsync`, `KeyRotationTests.PRIV_RIGHT_005a_AC18_TheRotationsPointReachesTheDeploymentKeysRowAsync`, `SubjectEraserTests.PRIV_RIGHT_005a_AC18_AnErasureNamingTheMaxUuidIsRefusedAndLeavesTheRowAsync`, `WebAuthnServiceTests.PRIV_RIGHT_005a_AC18_AnAssertionWhoseHandleIsTheMaxUuidIsRefusedAsync` |
| D-166 D.8, 340 with D-172 item 1: the library draws, holds wrapped and rotates every client secret; the client half reads it per request; the conformance suite's provider probes made by the sign-on's client half through `IProviderProbes` in `Janus.Core`; `ConformanceClient` removed; ledger line 340 | `1deb8bc` | OPS-SEC-001, OPS-SEC-002, AUTH-OIDC-001, BFF-SESS-006, LIB-TEST-001 AC4, AC5 | `SuiteSurfaceTests.LIB_TEST_001_AC5_NeitherTheSuiteNorTheProbeContractCarriesASecret`, `SuiteSurfaceTests.LIB_TEST_001_AC5_ASurfaceCarryingASecretIsFound`, `ProviderProbesTests.LIB_TEST_001_AC4_TheProbesAskAsTheSignOnClientWithTheRegistrysSecretAsync`, `ProviderProbesTests.LIB_TEST_001_AC4_ASecretReplacedSinceTheLastRunIsTheOnePresentedAsync`, `ProviderProbesTests.LIB_TEST_001_AC4_AnUnregisteredSignOnClientAsksNothingAsync`, `ConformanceSuiteTests.AUTH_OIDC_006_AC1_TheSampleHostsProviderRefusesEveryRetiredFormAsync`, `ConformanceSuiteTests.AUTH_OIDC_006_AC1_AProviderAdmittingWhatItShouldRefuseIsReportedAsync`, `ClientSecretLifecycleTests.OPS_SEC_002_AC1_AClientSecretRotatesAtTheCadenceWithoutRestartAsync`, `ClientSecretLifecycleTests.OPS_SEC_002_AC2_TheReplacedSecretAuthenticatesThroughTheOverlapAndNotAfterAsync`, `ClientSecretLifecycleTests.OPS_SEC_002_TwoProcessesRotatingTogetherLeaveOneSecretAsync`, `OidcStoreTests.OPS_SEC_001_NoClientSecretIsHeldInTheClearAsync`, `KeyRotationTests.OPS_SEC_003_AC1_AKeyRotationReWrapsTheClientSecretsAsync`, `RegisterClientTests.AUTH_OIDC_001_AC4_TheMailServerClientIsRegisteredWithNoSecretSuppliedAsync`, `RegisterClientTests.OPS_SEC_002_ARegistrationThatChangesAClientKeepsItsSecretAsync` |
| D-171 item 3: `IUnitOfWork.BeginAsync` and `CommitAsync` return a result; 436 callers pass the failure up or throw it as a fault naming its code | `f923b8a` | CONV-DESIGN-003 AC7, CONV-DESIGN-005 | `AuthenticationServiceTests.CONV_DESIGN_003_AC7_AServiceReturnsTheFailureItsTransactionAnswersAsync`, `DeletionSweepTests.CONV_DESIGN_003_AC7_ABackgroundPassThrowsTheFailureItsTransactionAnswersAsync` |
| The documentation of D-175 | `8e2d4b9` | none | none |
| The documentation of D-176 | `f9099a2` | none | none |
| D-166 D.9, 343 and 349 with D-175: each social provider's credential read through the secret source by the provider's name into the key ring of D-171 at the start, a static secret or a signing credential from which the client secret is minted at each exchange; the ring filled once, lent per use, cleared at the stop; a malformed declaration refused with `model.startup.declarationinvalid`, `details.declaration` `socialProvider.<provider>` and `details.field` the member; ledger lines 343 and 283 | `2aa2733` | IDN-LIFE-012, IDN-LIFE-012a, OPS-SEC-002, LIB-HOST-001 AC2, LIB-EXT-001, CONV-CODE-007 AC3, CONV-DESIGN-007 | `StartupValidationTests.LIB_HOST_001_AC2_AMalformedSocialProviderIsRefusedNamingItAsync` (six cases: declared twice, a factor that is not a social provider, `metadata`, `configuration`, `return`, `clientIds`), `StartupValidationTests.IDN_LIFE_012a_ASocialProviderWithoutAUsableCredentialIsRefusedAsync`, `StartupValidationTests.CONV_CODE_007_AC3_AReadAfterTheApplicationStopsThrowsAsync`, `StartupValidationTests.AUTHZ_MODEL_004_AC2_TheChecksStartBeforeEverythingElseRegistered`, `ProviderSignInTests.OPS_SEC_002_ASignedClientSecretIsMintedForEachExchangeAsync`, `KeyRingTests.CONV_CODE_007_AC3_AReadBeforeTheRingIsFilledThrows`, `KeyRingTests.CONV_CODE_007_AC3_TheClearingLeavesEveryArrayZeroAndAReadAfterItThrows`, `KeyRingTests.CONV_CODE_007_TheRingLendsItsOwnCopyInTheFormItWasRead`, `KeyRingTests.CONV_CODE_007_ACredentialTheRingDoesNotHoldIsNamedUnavailable`, `KeyRingTests.CONV_CODE_007_TheRingIsFilledOnce` |
| The documentation of D-177 | `3543fec` | none | none |
| D-177 (a) and (b): a push carries the mailbox's identifier (`MailboxId`, now in `Janus.Core`); the listing answers the identifier each account's `description` carries; `integration.mailserver.conflict`, with no status; reconciliation compares each mailbox with the account under its identifier, counts every account carrying no held identifier, and treats an account under a mailbox's identifier at another address as a difference | `3cef29a` | INT-MAIL-001, INT-MAIL-007, CONV-DESIGN-004, REF-001 AC3 | `MailboxReconciliationTests.INT_MAIL_007_AC2_EachMailboxIsComparedWithTheAccountCarryingItsIdentifierAsync`, `MailboxReconciliationTests.INT_MAIL_007_AC2_AListedAddressIsComparedInItsCanonicalFormAsync`, `ApiStatusTests.REF_001_AC3_ACodeARequestCanBeAnsweredWithHasAStatus` |
| D-176 item 1: the mail server in use chosen once at the start by the key ring's service and held in `IMailServerInUse` (public contract, internal implementation in `Janus.Core`); every consumer asks it; `identity.mailbox.notfound` where none is in use | `d5a2b03` | CONV-DESIGN-007 AC5, INT-MAIL-001, INT-MAIL-006 | `StartupValidationTests.CONV_DESIGN_007_AC5_TheMailServerInUseIsChosenAtTheStartAsync`, `MailServerInUseTests.CONV_DESIGN_007_AC5_AReadBeforeTheChoiceThrowsAndTheChoiceIsMadeOnce`, `MailServerInUseTests.CONV_DESIGN_007_AC5_TheChosenMailServerIsAnswered`, `StartupValidationTests.AUTHZ_MODEL_004_AC2_TheChecksStartBeforeEverythingElseRegistered` |
| D-166 D.6 215 with D-176: the JMAP adapter in `Janus.Hosting`, registered as its own type and chosen where `integration.mailserver.endpoint` is set at the start; its secret read through `ISecretSource.ReadMailServerSecretAsync` into the ring in the step after the choice; the `disabled` and `enabled` pushes of INT-MAIL-001 criteria 5 and 6; refusal to adopt an account not carrying the mailbox's identifier, a removal included; app passwords under the person's token | `9e7605d` | INT-MAIL-001 AC3 to AC6, INT-MAIL-006 AC1c, INT-MAIL-006a, INT-MAIL-007 AC1, INT-MAIL-010 AC1, CONV-DESIGN-007 AC5, CONV-CODE-007 AC3, LIB-HOST-001, INT-GEN-001 AC1 | `JmapMailServerTests.INT_MAIL_001_AC3_EveryOperationIsOneJmapRequestAsync`, `JmapMailServerTests.INT_MAIL_006_AC1c_AReservedMailboxIsCreatedWithAuthenticationDisabledAsync`, `JmapMailServerTests.INT_MAIL_006a_AC1_ADisablePushDisablesAuthenticationAsync`, `JmapMailServerTests.INT_MAIL_007_AC1_AReplayedPushCreatesNoSecondAccountAsync`, `JmapMailServerTests.INT_MAIL_001_AC4_AnAccountNotCarryingTheIdentifierIsNeverAdoptedAsync`, `JmapMailServerTests.INT_MAIL_001_ARemovalDestroysTheMailboxsOwnAccountAsync`, `JmapMailServerTests.INT_MAIL_001_AC5_AnEnabledPushToAReplaceAccountEnablesAuthenticationAsync`, `JmapMailServerTests.INT_MAIL_001_AC6_ADisabledPushChangesAuthenticationAloneAsync`, `JmapMailServerTests.INT_MAIL_006a_AC3_TheListingAnswersEnabledStateAsync`, `JmapMailServerTests.INT_MAIL_010_AC1_AnAppPasswordIsOneCallCarryingThePersonsTokenAsync`, `JmapMailServerTests.JmapMailServer_AnAnswerThatDoesNotRead_IsAFailureAsync`, `JmapMailServerTests.CONV_DESIGN_007_AC5_TheAdapterIsChosenWhereTheEndpointIsSetAsync`, `JmapMailServerTests.CONV_DESIGN_007_AC5_TheAdapterIsNotChosenWhereTheHostsOrNoneIsAsync`, `JmapMailServerTests.CONV_CODE_007_AC3_AReadOfTheMailServerKeyBeforeTheChoiceThrowsAsync`, `JmapMailServerTests.LIB_HOST_001_AMailServerKeyThatCannotBeReadStopsTheStartAsync`, `JmapMailServerTests.INT_GEN_001_AC1_APlaintextMailServerEndpointStopsStartupAsync`, `KeyRingTests.CONV_CODE_007_AC3_TheMailServerKeyIsReadOnlyOnceItsStepIsDone`, `KeyRingTests.CONV_CODE_007_AMailServerKeyNotHeldIsNamedUnavailable` |
| D-177 (b) and the reversal of entry 219: a conflict marks the push failed at its first attempt and raises `degradation` `mailbox.conflict:<mailbox id>` in the same transaction; each attempt counted, recorded and committed before it is made; a failed push begun again under its key a day after it was marked failed; a removal of a mailbox never attempted confirmed unsent; migration `RecordWhetherAMailboxPushWasAttempted` | `78b1141` | INT-MAIL-001 AC4, INT-MAIL-007 AC1, AC3, AC6, AC7 | `MailboxPublisherTests.INT_MAIL_001_AC4_AConflictIsMarkedFailedAndRaisedOnItsFirstAttemptAsync`, `MailboxPublisherTests.INT_MAIL_007_AC6_APushMarkedFailedIsBegunAgainADayLaterAsync`, `MailboxPublisherTests.INT_MAIL_007_AC1_EachAttemptIsCountedBeforeItIsMadeAsync`, `MailboxPublisherTests.INT_MAIL_007_AC7_ARemovalNeverAttemptedIsConfirmedUnsentAsync`, `MailboxStoreTests.INT_MAIL_007_AC7_AMailboxWrittenBeforeTheMarkCountsAsAttemptedAsync`, `MailboxPublisherTests.INT_MAIL_007_AC3_APushThatSpendsItsBudgetIsVisibleAsync` |
| D-177, OPS-ALERT-002 criterion 3: `AlertRaised` carries the scope; the deduplication key is the condition, the scope where there is one, and the account or actor; `raised_alerts.scope`, migration `RecordTheScopeOfARaisedAlert` | `e4a2c75` | OPS-ALERT-001, OPS-ALERT-002 AC3 | `AlertRouterTests.OPS_ALERT_002_AC3_OneConditionUnderTwoScopesIsTwoAlertsAsync`, `AlertsTests.OPS_ALERT_002_AC3_TheDeduplicationKeyIncludesTheScope`, `RaisedAlertsTests.OPS_ALERT_002_AC3_ARaisedConditionReadsBackWithItsScopeAsync` |
| The provider name checks read every core namespace `07` defines (`Janus.Core` and the four area projects), not `Janus.Core` alone | `e717980` | INT-MAIL-008 AC1, LIB-EXT-001 AC3 | `IntegrationBoundaryTests.INT_MAIL_008_AC1_NoMailServerIsNamedInTheCoreNamespace`, `IntegrationBoundaryTests.LIB_EXT_001_AC3_NoProviderNameAppearsInTheCoreNamespace` |
| The documentation of D-178 | `cd02727` | none | none |
| D-178 and the rest of 215: a mailbox replaced, or a reservation released, records when its removal became owed (`removal_owed_at`, renamed from `released_at`); it is owed `removed` whatever its holder's state short of erasure, the uniqueness of an address and the lookup by address skip it; once the removal of a released mailbox is confirmed its wrapped key is overwritten and its fingerprint neutralised in that transaction; a `disabled` or `enabled` push waits, neither attempted nor counted, behind an unconfirmed removal at its address; a push owed to an erased holder's mailbox ends unsent; ledger lines 215 and 219; migration `OweRemovalToAMailboxReplacedOrReleased` | `e64141d` | INT-MAIL-006 AC5, AC7, INT-MAIL-007 AC5, AC7, AC8, PRIV-RIGHT-005a AC19, REG-MAIL-003 AC7 | `MailboxStoreTests.INT_MAIL_006_AC7_AReplacedMailboxStandsAsideForItsSuccessorAsync`, `MailboxStoreTests.REG_MAIL_003_AC7_ErasingTheReplacedHolderEndsItsRemovalUnsentAsync`, `MailboxStoreTests.PRIV_RIGHT_005a_AC19_AReleasedMailboxForgetsItsAddressOnceRemovedAsync`, `InvitationStoreTests.REG_MAIL_001_AMailboxIsFoundByItsAddressAsync`, `MailboxPublisherTests.INT_MAIL_006_AC5_AMailboxReplacedIsOwedRemovedWhateverItsHolderAsync`, `MailboxPublisherTests.INT_MAIL_007_AC5_APushWaitsForTheRemovalAtItsAddressAsync`, `MailboxPublisherTests.INT_MAIL_007_AC5_TwoRemovalsAtOneAddressAreBothSentAsync`, `MailboxPublisherTests.INT_MAIL_007_AC8_ARemovalOwedToAnErasedHolderEndsUnsentAsync`, `MailboxReconciliationTests.INT_MAIL_007_AC7_AReplacedMailboxAndItsSuccessorAreComparedApartAsync` |
| D-166 D.6 221 with D-178: the invitation takes `formerMailbox` (`transfer`, `replace`) and `reason`; an issue over a standing mailbox someone held is refused 409 `identity.invitation.mailboxheld` unless it names one; `transfer` reserves the old mailbox again, `replace` marks it and reserves a new one; `formerMailbox` where no held mailbox stands is 422 `api.request.invalid` naming it; an erased holder's address is issued as never held; the issue's record carries `details.formerMailbox` and `details.reason`; ledger line 221 | `1566815` | REG-MAIL-003 AC6 to AC8, REG-MAIL-001, INT-MAIL-006 AC7 | `InvitationServiceTests.REG_MAIL_003_AC6_AMailboxSomeoneHeldIsRefusedWithoutAFormerMailboxAsync`, `InvitationServiceTests.REG_MAIL_003_AC6_UnderTransferTheInviteeReceivesTheOldMailboxAsync`, `InvitationServiceTests.REG_MAIL_003_AC6_UnderReplaceTheOldMailboxIsRemovedAndANewOneReservedAsync`, `InvitationServiceTests.REG_MAIL_003_AC7_AFormerMailboxWhereNoHeldMailboxStandsIsInvalidAsync`, `InvitationServiceTests.REG_MAIL_003_AC8_AnErasedHoldersAddressIsInvitedAsNeverHeldAsync`, `InvitationServiceTests.REG_MAIL_003_AFormerMailboxCarriesAReasonAsync`, `InvitationServiceTests.INT_MAIL_006_AC7_RevokingKeepsAMailboxSomeoneHeldAsync`, `InvitationServiceTests.REG_MAIL_003_AC2_EndingTheMembershipRetiresTheCorporateAddressAsync`, `InvitationEndpointTests.REG_MAIL_003_AC6_AMailboxSomeoneHeldPassesOnlyUnderAFormerMailboxAsync`, `OrganizationDirectoryTests.REG_MAIL_003_AC6_ATakeoverIsRecordedWithTheIssueAsync` |
| The documentation of D-179 | `a0416ae` | none | none |
| D-179: from the break-glass session, or a session another application opened from it, each of the nine step-up actions of OPS-BOOT-002 is refused 403 `authz.denied` at the gate step, after the body is read and before any load (`StepUpGuard.RefusedInBreakGlass`), no longer within the step-up judgement | `8e570fd` | OPS-BOOT-002 AC9, AUTH-STEP-004, CONV-DESIGN-002 AC3 | `BreakGlassEndpointTests.OPS_BOOT_002_NoSignInMethodIsGivenToTheReservedAccountAsync` (every route of the nine actions, from both sessions: password, identifier add and replace, username, passkey and security-key begin, key completion, upgrade, TOTP begin and confirmation, recovery codes, app password, deactivation, deletion, provider link) |
| D-166 D.6 263 with D-179: the app-password operations answer 404 `identity.mailbox.notfound` where the account is not `active` or `restricted`, holds no mailbox the server is told to enable, or no mail server is in use; `authz.denied` only for a context naming no account; a restricted account lists and revokes and creates none (`authz.restricted`); ledger line 263 | `a5a295e` | REG-MAIL-002 AC3, AC4, INT-MAIL-006, INT-MAIL-010, IDN-ACCT-007 | `AppPasswordsTests.INT_MAIL_006_WithoutAnEnabledMailboxThereAreNoAppPasswordsAsync`, `AppPasswordsTests.REG_MAIL_002_AC4_ARestrictedAccountListsAndRevokesAndCreatesNoneAsync`, `AppPasswordFlowTests.INT_MAIL_006_AnAccountWithoutAMailboxIsRefusedAsync`, `BreakGlassEndpointTests.REG_MAIL_002_AC3_TheBreakGlassSessionListsNoAppPasswordsAsync` |
| D-166 D.6 270 with D-176: the mail server row of the records of processing applies where a mail server is in use, and no longer on `integration.mail.endpoint` alone; a start with no `IMailTransport` or no `ISmsTransport` is refused with `model.startup.declarationmissing`, `details.key` `mailTransport` or `smsTransport` | `de42f15` | PRIV-ROPA-002, LIB-EXT-001, INT-MAIL-008 AC3, INT-SMS-006 AC2 | `StartupValidationTests.INT_MAIL_008_AC3_ADeploymentWithNoMailTransportDoesNotStartAsync`, `StartupValidationTests.INT_SMS_006_AC2_ADeploymentWithNoSmsTransportDoesNotStartAsync`, `ProcessingRecordsEndpointTests.PRIV_ROPA_002_ARegisteredMailServerIsInTheRegisterAsync`, `ProcessingRecordsTests.PRIV_ROPA_002_TheRowsTheLibraryMakesTrueAreAppliedWithoutADeclarationAsync`, `ProcessingRecordsTests.PRIV_ROPA_002_AC2_AnEditedRowStandsInPlaceOfTheShippedDefaultAsync`, `ProcessingRecordsTests.PRIV_ROPA_002_AnUncalledProviderIsNotInTheRegisterAsync` |
| The documentation of D-180 | `e9eb9ec` | none | none |
| D-180 question 19: the transport side's registration, `SendingValidation`'s factory among it, in `Sending/DeliveryRegistration.cs`, which names that side only; `KeyRingService`'s factory in `KeyRingRegistration`; no constructor default stands for an absent declaration | `38933fe` | CONV-DESIGN-007 AC6, INT-MAIL-009 AC2, AUTHZ-MODEL-004 AC2 | `PublicSurfaceTests.CONV_DESIGN_007_AC6_AnAbsentDeclarationReachesItsUserThroughAFactory`, `StartupValidationTests.AUTHZ_MODEL_004_AC2_TheChecksStartBeforeEverythingElseRegistered` |
| D-180 question 18: a start with no `ISecretSource` is refused with `model.startup.declarationmissing`, `details.key` `secretSource`, as the ring's start begins and before any secret is read | `d129edc` | LIB-HOST-001 AC2, OPS-SEC-001 AC2, AUTH-KEY-002 AC2 | `StartupValidationTests.LIB_HOST_001_AC2_ADeploymentWithNoSecretSourceDoesNotStartAsync` |
| D-166 D.8 121 and 336 with D-171 and D-176: `AddJanus` takes no key; the key-encryption key, fingerprint key and maintenance credential are read through `ISecretSource` into the key ring at the start, in the steps of CONV-DESIGN-007, and lent from it at each use; every `ISecretSource` member returns a result and `NotOperationContracts` is removed; a `Janus.Cli` command fills a ring of its own from its key document and clears it when it ends; the audit partition job borrows the maintenance credential; ledger line 121 | `7b45d2b`, `1cb2827` | CONV-CODE-007 AC3, CONV-DESIGN-005 AC1, CONV-DESIGN-007, OPS-SEC-001 AC2, AUTH-KEY-002 AC2, OPS-MIG-003a | `KeyMaterialTests.AUTH_KEY_002_AC2_StartupFailsNamedWithoutTheKeyEncryptionKeyAsync`, `KeyMaterialTests.AUTH_KEY_002_AC2_StartupFailsNamedWithoutTheFingerprintKeyAsync`, `KeyMaterialTests.AUTH_KEY_002_AC2_StartupFailsNamedOnAFingerprintKeyShorterThanTheHashAsync`, `KeyMaterialTests.AUTH_KEY_002_AC2_StartupFailsNamedOnARetainedFingerprintKeyShorterThanTheHashAsync`, `KeyMaterialTests.OPS_MIG_003a_StartupFailsNamedWithoutTheMaintenanceCredentialAsync`, `KeyMaterialTests.CONV_CODE_007_AC3_NoServiceReceivesAKeyAtRegistration`, `KeyMaterialTests.CONV_CODE_007_AC3_AKeyIsLentOnlyFromTheStartUntilTheStopAsync`, `KeyMaterialTests.CONV_CODE_007_AC4_TheEncryptionCredentialIsMadeFromTheFilledRingAsync`, `StartupValidationTests.OPS_SEC_001_ASecretTheSourceCannotAnswerStopsTheStartNamingItAsync`, `StartupValidationTests.CONV_DESIGN_007_TheSecretsAreReadBeforeTheServerStartsAsync`, `KeyRingTests.CONV_CODE_007_AC3_TheKeysAreLentFromTheRingsOwnCopyUntilItIsCleared`, `KeyRingTests.CONV_CODE_007_AVersionTheRingDoesNotHoldIsNamedWithItsVersion`, `KeyRingTests.CONV_CODE_007_AKeyTheRingDoesNotHoldIsNamedUnavailable`, `KeyRingTests.CONV_CODE_007_EachKeyIsHeldOnce`, `CommandTests.CONV_CODE_007_AC3_AReadOfTheRingAfterTheCommandEndsThrowsAsync`, `ResultContractTests.CONV_DESIGN_005_AC1_EveryContractMethodReturnsAnOutcome` |
| D-171 item 4: a session opened from the break-glass session carries its reason; one opened by an ordinary sign-in carries none (no code change) | `5ebabcd` | OPS-BOOT-002 AC10 | `BreakGlassEndpointTests.OPS_BOOT_002_AC10_ASessionOpenedFromTheBreakGlassSessionCarriesTheReasonAsync` |
| D-166 D.8, 316: one data key of the deployment, a row of `subject_keys`, under which every value of no subject is held; the rotation re-wraps subject-key rows only, writing back only where the value read still stands; ledger line 316 | `ced2208` | OPS-SEC-003, OPS-MIG-003a AC4, AUTH-KEY-002, PRIV-RIGHT-005a | `KeyRotationTests.OPS_SEC_003_AC3_AfterRetirementEveryValueOfNoSubjectStillReadsAsync`, `KeyRotationTests.OPS_SEC_003_AC3_AProofKeyInFlightStillReadsAfterRetirementAsync`, `KeyRotationTests.OPS_SEC_003_ARotationTouchesNoTableButTheSubjectKeysAndItsProgressAsync`, `KeyRotationTests.OPS_SEC_003_ARotationDoesNotOverwriteAKeyRewrittenAtTheSameVersionAsync`, `DatabaseRoleTests.OPS_MIG_003a_AC4_TheMaintenanceRoleReachesNoValueBesideTheSubjectKeysAsync`, `OidcStoreTests.AUTH_KEY_002_ThePrivateHalfIsWrappedUnderTheDeploymentDataKeyAsync`, `SubjectEraserTests.PRIV_RIGHT_005a_ErasureNeverTouchesTheDeploymentDataKeyAsync`, `SerializedModelTests` (the maintenance grants) |

**How and when the list was drawn.**
- **The draw.** It ran from 2026-09-25 23:54 to 2026-09-26 02:30 UTC over all 1,048,576 ranges of `https://api.pwnedpasswords.com/range/{prefix}`, with 48 workers and gzip requested.
- **Checkpoints.** Each batch of ranges was recorded as done with the kept hashes, so the draw could be stopped and resumed.
- **User agent.** `leaked-password-list-draw/2026-09-26 (drawing the 100,000 most prevalent hashes once for an offline screening list)`.
- **Selection.** The 100,000 hashes of highest count were kept; the lowest kept count is 20,990. Among equal counts the lower hash is kept, and the list is sorted.
- **Verification.** The committed file was checked to be equal to the checkpoint's kept set.
- **The terms**, read on 2026-09-26 at haveibeenpwned.com: the range API has no rate limit, and callers must send a user agent. No licence is required for Pwned Passwords, and attribution is welcomed.

**D-168, question 1.**
- The target `LibraryPackageConstants` in `Directory.Build.props` runs after the target `MinVer` and before compilation, in the projects that set `LibraryPackageConstants` (only `Janus.Hosting`). It writes `LibraryPackage.g.cs`, headed `// <auto-generated/>`, into the intermediate output: `internal const string Identifier` from `PackageId` and `Version` from `PackageVersion`, which MinVer sets without build metadata.
- `tmp/c4/user-agent.patch` is discarded.
- Mutation checks:
  - with the user agent line removed from the registration, the INT-PWD-001 AC3 test fails;
  - with a `typeof(WordList).Assembly.FullName` read added to a shipped file, the CONV-CODE-004 AC2 test fails;
  - with a `<Version>` element added to a project file, `CONV_CODE_004_AC3_NoProjectFileSetsAVersion` fails.

**D-166 item 114 and R1, the word lists.**
- **English.** `12dicts-6.0.2.zip` from `http://downloads.sourceforge.net/wordlist/12dicts-6.0.2.zip`, downloaded on 2026-09-29, SHA-256 `64ac1d35acb66b550c7ebc56e080b62e0bad8f5984d72059dc2e05ac48780e52`. The package's read-me, read the same day, releases the lists to the public domain and asks for acknowledgment.
  - `American/3esl.txt` is vendored whole as `src/Janus.Hosting/Passwords/3esl.txt`, 21,877 lines, byte for byte as published (SHA-256 `eb70e6169534511caff9e06971b3fa71981a83f0355b29532b710ea5a9df253b`), from `539a4dc`.
  - The build keeps the lines that are single lower-case words of four letters or more: 18,693 words. It fails below 10,000.
  - Mutation check: with the floor raised to 20,000, the build fails with the count.
- **Arabic.** `src/Janus.Hosting/Passwords/arabic-transliteration.txt` is original work, 3,101 entries in sections:
  - given names, men's and women's;
  - Coptic and Christian names;
  - family names;
  - religious words;
  - everyday words;
  - slang;
  - profanity;
  - football clubs and players;
  - places.

  Arabizi forms use the digits 2, 3, 5 and 7.
- **Arabic variants.** The build applies each rule on its own to every entry and keeps the results of four characters or more: 5,081 words. The rules:
  - each digit to its letter (7 to h, 5 to kh, 3 to a, 2 to a);
  - the same, with 3 and 2 written as nothing;
  - `ou` and `oo` to `u`;
  - `ee` to `i`;
  - `g` to `j`;
  - a final `a` to `ah`;
  - a leading `el` to `al`.

  The list waits for the owner's review before release, as AUTH-PASS-004 states.
- **Matching.** The matcher keeps digits, counts every character toward the four a match needs, and answers only whether a word matched.
  - Mutation check: with the Arabic list left out of `WordList`, the three Arabizi tests fail.

**D-166 R3, the Public Suffix List.**
- Drawn from `https://publicsuffix.org/list/public_suffix_list.dat` on 2026-09-29 at 02:25 UTC, once: version `2026-09-24_13-26-36_UTC`, commit `a179a48c465e818cfd8d626691cb317985da87fb`, 334,786 bytes, SHA-256 `257b298daca42f6d8ec964e238c2a55518e14f09d3117917ec8acee6f188503e`. It is committed unmodified as `src/Janus.Authentication/Factors/public_suffix_list.dat`.
- `PublicSuffixList` applies the list's own algorithm: an exception rule prevails and gives up its leftmost label, otherwise the rule with the most labels, wildcards included, and a name no rule covers ends at its last label. Names are compared in their ASCII form.
- A relying party identifier that is a public suffix is refused (it was refused only when it had one label); a label is the one before the public suffix (it was the second label from the right).

**DR-007.**
- The rewritten test moves the clock to the objective and no further. With the old code it failed every time with `unrestored`; with the fix it passes with `overrun` and an elapsed time of exactly the objective.
- `ManualTime` (`tests/Janus.Storage.Tests`) is made public and takes the instant it starts at, so the Hosting tests reach it and read back what the run recorded at its own instant.

**D-166 D.8, the configuration reads (116, X2).**
- The store (`ReadAsync`, its family overload, `ReadWrittenAsync`) throws naming the key and the code of the missed constraint. Every read that turned a failure into a value now throws the failure's code, the idiom `DerivationMaterialiser` already used. The places:
  - `Janus.Authentication`: `AccountAdministration.CancelDeletionAsync` and `AccountLifecycle.ElapsedAsync` (`account.deletion.grace`, was zero); `ClockDriftWatch` and `TotpService.DriftAsync` (`factor.totp.drift`); `RedirectValidation` and `RegistrationService.DefaultClientAsync` (`redirect.defaultclient`); `OrganizationService` (`organization.deletion.grace`); `ConcurrentSessions` (`alerting.sessions.window`, `.distance`); `SessionService.ReadAsync` (every duration); `ProtectedSettingValue.LoosensAsync`; and the `notification.languages` reads of `AccountLifecycle`, `CredentialService`, `ProviderEvents`, `RecoveryCodeReminders`, `IdentifierService`, `InvitationAcknowledgement`, `MembershipEnd`, `AppPasswords`, `LossReports`, `RecoveryService`, `AuthenticationService` and `SignInLinks`.
  - `Janus.Authorization`: `DenialSpikes`, `ExportOperations` (three keys), `ReadVolume` (four keys), `ReverseLookup`.
  - `Janus.Cli`: `BootstrapArguments.Complete` (`hosting.location`).
  - `Janus.Hosting`: `AuthenticationEndpoints.ForAsync` (cookie lifetimes), `RestoreTest.RunAsync` (`backup.restoretest.objective`, `.canary`), `SettingNaming.On`, `SubjectNotices`, `RegistrationStream`, `LocationDatabase`.
  - `Janus.Privacy`: `ConfigurationCoverage.ValidateAsync`, `DeletionSweep.ReadAsync`, `OrganizationErasureSweep`, `ExportService`, `OutboxPublisher` (the three retry keys, which were constants), `PrivacyRequestService`, `TakedownService.GraceAsync`, `ProcessingRecordsService.ReadAsync` (its caller-supplied fallback removed).
- Three keys are required only under a condition chapter 10 states. At their reads, `model.startup.declarationmissing` alone is read as "not named" and every other failure throws: `hosting.crossborderbasis` in `ProcessingRecordsService.GenerateAsync`, `password.blocklist.selfhosted.address` in `SendingValidation.InsecureAsync`, `service.name` in `CredentialService.IssuerAsync`.
- Unchanged after review: every read that passes its failure on, `CategoryRetention` (a category with no written value reads its declared floor, the family's default by PRIV-RET-001), and `BackgroundWorker.ConsiderAsync`.

**D-166 D.8, 178 and 180 under X3 and X9.**
- `IConfigurationWrites.HoldAsync` takes the row with `SELECT ... FOR UPDATE`. `ChangeAsync` holds before it reads and classifies; the member routes (the organisation policy, the domain list, a category's retention) begin, hold, read and classify, and `ChangeMemberAsync` joins their unit of work.
- A refusal made under the lock has written nothing. `IUnitOfWork` has no rollback member, so the route ends the unit of work with `CommitAsync`, which commits nothing and releases the lock. The CONV-DESIGN-002 test of X9 belongs to the X9 sweep (section 2).

**D-166 D.8, 290 part (2).** The worker raises every lapse through `IAlertChannels`, as OPS-ALERT-001 has every raise site do, and for `alert-dispatch` also delivers it through `AlertRouter` in the same transaction, as the destination change delivers its notice and writes its row. The router records the deduplication key, so the row is folded into that delivery when the carrier runs again.

**D-170 item 1, how the reason reaches the record.**
- The carrier is the access context the services already hand the writers. `AccessContext` gains a read-only `BreakGlassReason`, set only by an internal factory that the boundary calls from the session; a background job's context has none, and the principal overloads of the writers take none. No host can set it.
- `RestrictionAdministration.EditAsync` and `GrantAsync`, `AlertDestinationChange.ChangeAsync` and `ISettingsRestriction.RefusedAsync` take the access context in place of the bare actor, so the records they write in the session carry the reason. All internal.
- A session derived from the break-glass session (the management application's, a token's) keeps its reason, as it keeps satisfying every gate: an action there is an action in the break-glass session (BFF-SESS-006, OPS-BOOT-002 AC6). D-170's "no other session has one" is read as no session opened another way.
- The database holds the rule too: `ck_sessions_breakglass_reason` (only on a session that satisfies every gate, 1 to 1024 characters after trimming) and `ck_audit_records_breakglass_reason` (never beside a principal). Migration `RecordBreakGlassReasons`.
- X4: the endpoint refuses an absent, blank or longer reason with 400 `api.request.malformed` naming `reason` before the credential is looked at, and the service refuses the same before an attempt is counted.

**D-166 D.8, 316.**
- The order: 340 wraps client secrets under 316's deployment key, so 316 is applied first (D-166 section B, D-171 item 2).
- The deployment key is the `subject_keys` row under the nil subject (question 9; moved to the max UUID by D-172 and D-173 in `c656253`), written on first need by an insert that does nothing on conflict, then read back. `SubjectEraser` refuses the nil subject before anything.
- Invitations, reserved mailboxes, registration sessions, the send outbox, the sign-on proof, signing keys and provider attempts moved under it; their `key_version` columns and the checks on them are dropped. A list read unwraps the deployment key once for the batch (D-171).
- Migration `MoveValuesUnderTheDeploymentKey` refuses where any value is still wrapped under the key-encryption key outside `subject_keys`, since the database cannot re-wrap it, and revokes the maintenance role's grants on those tables except the mailbox reads the fingerprint rotation needs. Its `Down` refuses where values under the deployment key stand.
- A mutation check: with the `wrapped_key = @read` condition removed, `OPS_SEC_003_ARotationDoesNotOverwriteAKeyRewrittenAtTheSameVersionAsync` fails.
- Not done here, as other sections' items: 234 (the invitation's identifier in its additional data, D.6) and the outbox's erasure marker (PRIV-RIGHT-005a).

**D-172 item 2 as D-173 corrects it (`c656253`).**
- Each store builds the subject position of the additional data from its row's own identifier: the invitation's, the unheld mailbox's (the holder's while one holds it), the queued message's where it names no subject (the subject's where it names one), and the registration session's, which was bound to the provisional subject, not its row. RFC 5649 wraps are unchanged.
- The migration `HoldTheDeploymentKeyUnderTheMaxUuid` changes no model. It refuses with an exception naming the table where an invitation with identifiers, an unheld mailbox, a queued message naming no subject, or any registration session stands, then moves the key's row as it is. Its `Down` mirrors it. Nothing is re-encrypted.
- Each row-binding test moves a value together with its wrapped key to another row, which opened under the shared binding and no longer does.
- The run was interrupted by a crash of the machine after this work was written and before it was committed. Every changed file was re-read and checked whole before the commit. The 340 work had been set aside in `tmp/c4/held/340-now/` so that the commit and its checks were its own, and was put back after it, identical to what was saved.

**D-174 (`ae04c76`).**
- The checks are named `ck_<table>_<column>_not_max_uuid` in 44 configurations, and the audit table's two identities, which stand outside the model, take theirs in the migration's SQL; its partitions inherit them. The schema test finds each column from the model: every property typed `SubjectId`, and the identifier beside each subject-type discriminator.
- A stored `SubjectId` reads back through its constructor, so a written subject is held to the refusal as it is read; the stored shape is unchanged.
- `WebAuthnService` compared a user handle the client supplies by making a `SubjectId` of it. It now compares the handle's value with the held subject's, so an all-ones handle is refused as any stranger's is, with `auth.factor.rejected`.
- A consequence left to D.11: a Hosting handler that makes a `SubjectId` from a route or body value now faults on the max UUID, where it answered as for an unknown subject. D-166's correction to CONV-DESIGN-004 AC2, binding each identifier at the edge, answers it with `api.request.malformed`; it is not yet applied.

**340 with D-172 item 1 (`1deb8bc`).**
- `IProviderProbes.RunAsync` takes an access context, as LIB-API-005 has every operation take one, and meets no gate (CONV-DESIGN-002 AC3). The probes ask what the old probe asked, as the declared `SignOnClient`, through the `identity-signon` client; each request that presents the secret reads it from the registry, and the bytes are cleared after use. No finding can carry the secret.
- The sample host registers only its own application. The migration `HoldClientSecretsWrapped` refuses where a client row stands, since a hash cannot become a secret.
- `SignOnTests.AUTH_OIDC_006_AC2_ARequestThePushRefusesIsNotForwardedAsync` provokes the push refusal with a registered destination carrying a fragment, since the application now always presents the registry's own secret.
- The probe of LIB-TEST-001 AC4 for a pushed request naming another destination belongs to D-166 145 and 279 (D.9), not yet applied.
- Its commit message carries two `Implements:` lines where the convention writes one. The history is not rewritten.

**D-171 item 3 (`f923b8a`).**
- Three private helpers answer `Error?` (`AuthenticationService.CountedAsync` and `StepUpRefusedAsync`, `OrganizationErasureSweep.ErasedAsync`). Their answer already is the failure their callers, which return results, pass up, so they hand back the transaction's failure rather than throw it. No signature changed.
- `ResultContractTests.NotOperationContracts` lists only `ISecretSource`, until 336 removes it.

**343 and 349 with D-175 (`2aa2733`).**
- The ring arrives with its first secrets, the provider credentials, as D-171 item 2 orders the work; 215 adds the mail server's secret, and 121 and 336 move the rest into it.
- The ring's start is the first hosted service after the validation services, so it stops last. It refuses a provider's credential with `model.startup.secretunavailable`, `details.key` `socialProvider.<provider>`, where the source answers a failure, no source is registered, the material is empty, or a signing credential has a blank issuer or key identifier or a key that is not a P-256 private key. The refusal of an absent source itself comes with 336.
- A minted client secret carries `iss`, `sub` (the first client), `aud` (the provider's issuer), `iat` and `exp` five minutes later from the deployment's clock; the key is disposed after each exchange.
- Its commit message carries two `Implements:` lines, the slip of `1deb8bc` again. The history is not rewritten.

**215 with D-176 and D-177 (`3cef29a` to `e717980`).**
- `MailboxId` moves from `Janus.Authentication`, where it was internal, to `Janus.Core`, public: D-177 (a) gives the push the mailbox's identifier, CONV-DESIGN-004 criterion 2 keeps a bare `Guid` out of a public contract where a typed identifier exists, and `07`'s Operations contract row puts the typed identifiers the contracts take in `Janus.Core`.
- The ring's service is a lifecycle service: it fills the ring before every check, chooses the mail server after the schema, settings, model and sending checks and before the declaration check, reads the adapter's secret then, and clears the ring once the application has stopped. The host's `IMailServer` is resolved once, when the service is built.
- The adapter reads a page of the listing after another until one is empty, and skips an account whose `@type` is present and not `User`; one whose `@type` is absent is listed, so a non-user is counted as unknown rather than passed over.
- A mailbox whose holder was erased is not compared, and its account is counted unknown, by construction: the store reads no row whose holder was erased, so its identifier is not held.
- INT-MAIL-007 criterion 5 (the wait behind an unconfirmed removal), the first half of criterion 7 (reconciliation after a `replace`) and 215's ledger line came with D-178, in `e64141d`.
- `9e7605d` scoped the mail server naming test to `Janus.Core`, reading INT-MAIL-008 criterion 1's "core namespace" as the existing LIB-EXT-001 criterion 3 test did. `07` defines a core namespace as `Janus.Core` or an area project's, so both tests read too little; `e717980` makes them read all five.

**221 with D-178 (`e64141d`, `1566815`).**
- The old mailbox of a `replace` is marked before the new row is added, so the unique index never sees two standing rows at one address.
- An unreadable body member is answered `api.request.malformed` with `details.member` the reader's JSON path (`$.formerMailbox`), where the `10` row names the member. Every body shares the reader, which has always done this; it is outside 221 and waits for the D.11 and section C sweeps.

**263 and 270 with D-179 (`8e570fd` to `de42f15`).**
- The operations of the nine actions that remove or unlink pass their own actions, which OPS-BOOT-002 does not withhold, so they are not refused at the gate step. A refusal of a change naming the reserved account or its role stays where it was.
- 362 takes no ledger line yet: only its point (5) is applied, with 263.
- 270 takes no ledger line: section G does not list it.
- The transport check of 270 sits in `SendingValidation` and not in `DeclarationCoverage`, which names the mail server's client: no source file may name both mailbox hosting and delivery (the gate of INT-MAIL-009 criterion 2). Question 19 is how its optional transports are resolved.

**D-180, 121 and 336 (`38933fe` to `1cb2827`).**
- The test of CONV-DESIGN-007 criterion 6 reads every type `AddJanus` registers from the shipped assemblies (a descriptor's implementation type, an instance's type, a factory's declared return type): no constructor of any has a parameter defaulting to null, and no type the container activates has a nullable reference parameter. It fails with either default put back.
- The `nosource` case of `StartupValidationTests.IDN_LIFE_012a_ASocialProviderWithoutAUsableCredentialIsRefusedAsync` is removed: D-180 makes that start the refusal of LIB-HOST-001, which its own test now proves.
- The mail server's secret keeps its later step, read only where the adapter is chosen, as D-176 gives it.
- The encryption credential is made when the server's options are first read, after the ring is filled; the derived array is cleared once the credential has taken its copy. That the copy is zero when the making returns is shown indirectly: the credential holds the correctly derived key after the source bytes are cleared, and the clearing is a `finally` of `TokenProtection`.
- `7b45d2b` wrote the ledger line under 336 as well as 121; section G lists 121 and not 336, so `1cb2827` takes the line under 336 off.

**Criteria no test decides.**
- D-166 319 (1), the signing algorithm: `token.signing.algorithm` admits only `ES256` at its reading, so `configure` refuses any other value before the check of `SigningKeys` is reached. The check stands in `CompleteAsync`; no value reaches its refusal. Verified by review.
- D-170 item 1, the writers that take no reason (`CredentialAudit`, `OidcAudit.ReusedAsync`, `BotDefenceAudit`, `PhoneSignalAudit`, `SessionAudit.FailedAsync`): none writes a record a break-glass session can cause, since every credential action is refused in the session (OPS-BOOT-002 AC9) and the others are written where no session acts. Verified by review.
- PRIV-BREACH-002 AC4, the reach through `subject` of an action on another person's account: waits for 303 (the record's subject); the clause returning `breakGlassReason` is tested.
- D-166 116 and X2, the sweep's completeness: every call of `IConfigurationStore` in the code was listed by a script and each one that turned a failure into a value is among the places above. Verified by review.

**Commit type of `c440f0a`.** It adds the key `integration.mailserver.endpoint` and its startup rule, with its changelog line, under the type `test`; the type is `feat`. The history is not rewritten.

### On `corrections-4`: D-181, D-182 and D-166 D.8 beside the parts

| Instruction | Commit | Items | Tests |
|---|---|---|---|
| The documentation of D-181 | `4a23fb4a` | none | none |
| The documentation of D-182 | `b7329af7` | none | none |
| D-181, question 20: the first read of the signing keys 5 minutes before the cadence makes the next key and publishes it; the first read once the cadence has passed and that key has been published 5 minutes makes it current; of two processes one makes each change, in a transaction of its own; the first read after a replaced key's overlap (the longest access-token lifetime it signed under, plus 5 minutes) retires it from the key set and drops its private key, its public key kept apart for validation until the longest a session can last; the credentials held in the library's credential source in `Janus.Authentication`, which the provider's signing, key set and validation read through its event model, the options built once; `token.signing.algorithm` admits `ES256` alone; migration `HoldSigningKeysThroughTheirKeeping` | `211b1d42` | AUTH-KEY-001, CONV-CODE-007 | `SigningKeysTests.AUTH_KEY_001_AC1_TheFirstReadOnceTheCadenceHasPassedMakesTheNextKeyCurrentAsync`, `SigningKeysTests.AUTH_KEY_001_AC1_NoReadChangesAKeyBeforeItsTimeAsync`, `SigningKeysTests.OPS_SEC_002_AC1_TheRunningInstanceRotatesWithoutRestartOrAPersonAsync`, `SigningKeysTests.AUTH_KEY_001_AC2_TheOverlapIsTheLongestLifetimeTheKeySignedUnderAndFiveMinutesAsync`, `SigningKeysTests.AUTH_KEY_001_AC2_NoChangeOfTheLifetimeAfterTheRotationShortensTheOverlapAsync`, `SigningKeysTests.AUTH_KEY_001_AC2_ALongerLifetimeIsCommittedBeforeTheAccessTokenIsSignedAsync`, `SigningKeysTests.OPS_SEC_002_AC2_WhatThePreviousKeySignedVerifiesThroughTheOverlapAsync`, `SigningKeysTests.AUTH_KEY_001_AC3_ThePreviousKeyLeavesThePublishedSetAfterTheOverlapAsync`, `SigningKeysTests.AUTH_KEY_001_AC4_EveryPublishedKeyCarriesTheConfiguredAlgorithmAsync`, `SigningKeysTests.AUTH_KEY_001_AC4_ThePublishedSetCarriesThePublicHalfAloneAsync`, `SigningKeysTests.AUTH_KEY_001_AC5_AReplacedKeySignsNothingAfterTheRotationAsync`, `SigningKeysTests.AUTH_KEY_001_AC5_TheFirstReadAfterTheOverlapRetiresTheKeyAsync`, `SigningKeysTests.AUTH_KEY_001_AC6_AReplacedKeyIsKeptUntilTheLongestASessionCanLastAsync`, `SigningKeysTests.AUTH_KEY_001_AC7_WhereTheDatabaseHoldsNoKeyTheFirstReadMakesOneCurrentAsync`, `SigningKeysTests.AUTH_KEY_001_AC7_OfTwoProcessesFindingTheSameChangeDueOneMakesItAsync`, `SigningKeysTests.AUTH_KEY_001_AC7_ALongerLifetimeAgainstAReplacedKeyIsRefusedAndTheCurrentKeySignsAsync`, `SigningKeysTests.AUTH_KEY_001_AC8_AKeyMadeAfterTheCadenceSignsOnceItHasBeenPublishedFiveMinutesAsync`, `SigningKeysTests.AUTH_KEY_001_AC8_EveryKeyButTheFirstIsPublishedFiveMinutesBeforeItSignsAsync` (400 reads at uneven steps over a one-hour cadence), `SigningKeysTests.CONV_CODE_007_AC4_AReplacedKeyHoldsItsPublicKeyApartFromItsPrivateKeyObjectAsync`, `SigningKeysTests.CONV_CODE_007_AC4_ASetThatReplacesAnotherCarriesTheObjectsItKeepsAsync`, `SigningKeysTests.CONV_CODE_007_AC4_TheBytesAKeyIsMadeFromAreZeroWhenTheMakingReturnsAsync`, `SigningKeyRotationTests.AUTH_KEY_001_AC1_TheRunningProviderSignsWithTheNextKeyAfterTheCadenceAsync`, `SigningKeyRotationTests.AUTH_KEY_001_AC2_TheLongestLifetimeIsStoredWithTheKeyThatSignsAsync`, `SigningKeyRotationTests.AUTH_KEY_001_AC3_AfterTheOverlapTheKeyLeavesTheSetAndItsAccessTokenIsRefusedAsync`, `SigningKeyRotationTests.AUTH_KEY_001_AC3_TheDocumentAndTheTokenHashesAreUnchangedAfterTheStartKeyRetiresAsync`, `SigningKeyRotationTests.AUTH_KEY_001_AC4_EveryIssuedTokenIsSignedWithTheConfiguredAlgorithmAsync`, `SigningKeyRotationTests.AUTH_KEY_001_AC6_ARefreshTokenARetiredKeySignedIsStillAcceptedAsync`, `SigningKeyRotationTests.AUTH_KEY_001_AC7_TheStartMakesAKeyCurrentBeforeTheOptionsAreBuiltAsync`, `SigningKeyRotationTests.AUTH_KEY_001_AC8_TheNextKeyIsPublishedFiveMinutesBeforeItSignsAsync`, `SigningKeyRotationTests.CONV_CODE_007_AC4_TheOptionsAreNeverRebuiltAndHoldOnlyTheSourcesObjectAsync`, `SigningKeyStoreTests.AUTH_KEY_001_AC5_RetirementAndRemovalWaitForTheirTimesAsync`, `SigningKeyStoreTests.AUTH_KEY_001_AC7_TheTableAdmitsOneNextKeyAndOneCurrentKeyAsync`, `SigningKeyStoreTests.AUTH_KEY_001_AC7_APromotionIsWrittenOnlyWhereTheKeysStandAsReadAsync`, `SigningKeyStoreTests.AUTH_KEY_001_AC7_ALongerLifetimeIsNeverLoweredAndRefusedOnceTheKeyIsReplacedAsync`, `SigningKeyConcurrencyTests.AUTH_KEY_001_AC7_OfTwoProcessesStartingOnAnEmptyDatabaseOneMakesTheKeyAsync`, `SigningKeyConcurrencyTests.AUTH_KEY_001_AC7_OfTwoProcessesFindingTheSameChangeDueOneMakesItAsync`, `SigningKeyConcurrencyTests.AUTH_KEY_001_AC7_AChangeDuringARequestThatRollsBackStaysMadeAsync`, `SigningKeyConcurrencyTests.AUTH_KEY_001_AC7_ALongerLifetimeAgainstAKeyAnotherProcessReplacedIsRefusedAsync` (PostgreSQL, two containers over one database, the second process's clock a minute behind), `ConfigureTests.AUTH_KEY_001_AC4_ConfigureRefusesASigningAlgorithmOtherThanES256Async` |
| D-166 D.8, 317 (1) and (3): a seal before the rotation completes refused `model.rotation.notready`; retirement waits while a value is still wrapped under the previous version; the retirement report carries `keepUntil` | `81165fe5` | OPS-SEC-003 | `KeyRotationTests.OPS_SEC_003_AC4_ASealBeforeTheRotationCompletesIsRefusedAsync`, `KeyRotationTests.OPS_SEC_003_AC3_RetirementWaitsWhileValuesAreStillWrappedUnderThePreviousVersionAsync`, `KeyRotationTests.OPS_SEC_003_TheRetirementReportSaysHowLongTheRetiredVersionIsKeptAsync`, `FingerprintKeyRotationTests.OPS_SEC_003_AC6_ASealBeforeTheRotationCompletesIsRefusedAsync`, `FingerprintRotationTests.OPS_SEC_003_AC6_RetirementWaitsForAHeldUsernameAndForgetsWhatTheVersionHashedAsync` (renamed in `98eebc5c`), `ErrorCodesTests` (the catalogue) |
| D-166 D.8, 341: the key-encryption key's cryptoperiod warning read from its own rotation record, and from bootstrap where none stands; ledger line 341 | `c5f86a6e` | DR-009a | `EnvelopeRotationWatchTests.DR_009a_AC1_ALogEntryWithoutARotationLeavesTheCryptoperiodRaisedAsync`, `EnvelopeRotationWatchTests.DR_009a_AC1_ACompletedRotationEndsTheWarningAsync`, `EnvelopeRotationWatchTests.DR_009a_AC1_ADeploymentNeverRotatedCountsFromBootstrapAsync`, `EnvelopeRotationWatchTests.DR_009a_AC1_AFingerprintKeyRotationDoesNotEndTheWarningAsync`, `MaintenanceStoreTests.DR_009a_AC1_TheCryptoperiodIsReadFromTheKeysOwnRotationRecordAsync` |
| D-166 D.8, 303 but for `Subject` on `AuditEntry` (question 35): `audit_records.subject` names the account acted on; every new record's effective identity is the acting one; the trail is found by subject or acting subject; personal details sealed under the subject; `AuditEntry` carries `Principal` and `PrincipalReason`; ledger line 303 | `9cfccf3a` | IDN-AUD-001, AUTHZ-IMP-001, PRIV-BREACH-002 | `AuditRecordTests.IDN_AUD_001_AC4_TheAccountActedOnIsTheSubjectAndNeverTheEffectiveIdentity`, `AuditStoreTests.IDN_PRIN_001_AC4_TheTrailNamesTheBackgroundPrincipalAndItsReasonAsync`, `AuditStoreTests.AUTHZ_IMP_001_AC4_ABreakGlassActionOnAnotherAccountNamesItAsTheSubjectAsync`, `AuditTrailEndpointTests` (the principal fields) |
| D-166 D.8, 304 (2) and 334: each due run started on its own, one run per job in flight; the worker wakes at the next due or at a run's end and awaits the runs in flight when it stops; ledger line 334 | `be8b2140` | INF-BG-001 | `BackgroundWorkerTests.INF_BG_001_ARunInProgressHoldsNoOtherJobsTurnAsync` (25 repeated runs green) |
| D-166 D.8, 323: two licences under one identifier and a future `performedAt` refused 422 `api.request.invalid` naming the member | `30b1c4bc`, `5db004f4` | OPS-MAINT-001 | `MaintenanceRecordsTests.OPS_MAINT_001_TwoLicencesUnderOneIdentifierAreRefusedAsync`, `MaintenanceRecordsTests.OPS_MAINT_001_AC3_ATaskDatedAfterNowIsRefusedAsync`, `MaintenanceEndpointTests.OPS_MAINT_001_ARequestRefusedOnItsMeaningIsInvalidAtItsMemberAsync` |
| D-166 D.8, the audit action rows: `identity.credential.labelled` renamed `auth.credential.labelled`; `auth.credential.invalidationheld` described as `10` describes it; the code carries exactly the 77 actions of `10` section 5 | `f407060d` | IDN-AUD-001, CONV-NAME-003 | `AuditActionsTests.IDN_AUD_001_TheSetOfActionsIsClosed` |
| D-166 D.8, 318 (1) and (2): a sweep of each abuse count under every version (throttle lines, settled sends, registration sources, notices, callbacks), run by the expiry sweep; retirement waits on every line under a previous version in the seven tables | `98eebc5c` | OPS-SEC-003 | `FingerprintRotationTests.OPS_SEC_003_AC6_RetirementWaitsWhileAnAbuseCountUnderThePreviousVersionStillCountsAsync`, `FingerprintRotationTests.OPS_SEC_003_AC6_TheSweepForgetsAThrottleCounterOnceItStandsAtNothingAsync`, `FingerprintRotationTests.OPS_SEC_003_AC6_RetirementWaitsForAHeldUsernameAsync`, `ExpirySweepTests.OPS_SEC_003_AC6_ThePassRemovesEveryAbuseCountItsCheckNoLongerReadsAsync` |
| The secret scanner's entry for the item identifier a migration comment names (section 3) | `814d8901` | OPS-DEP-004 | No test can decide it. The pinned scanner, run locally as the pipeline runs it, over 996 commits: no finding |

**D-181 (`211b1d42`).**
- OpenIddict 7.7.1 was read at its tag, and the same steps on its development branch, before the work. Every token but the identity token takes the first signing credential of the options; the key set, the validation parameters, the list of signing algorithms and the token hashes (`at_hash`, `c_hash`) read the options' credentials. Each such step is removed and replaced at its own order by one that reads the source; the validation keys are set on the request's copy of the parameters, so the options' list is never read.
- OpenIddict requires one asymmetric signing credential at the start, so the start makes a key current before the options are built.
- Migration `HoldSigningKeysThroughTheirKeeping`: a key held before it is taken as signing from when it was made, under the ceiling of `oidc.accesstoken.lifetime` (section 3); a replaced key is kept for the ceiling of `session.default.absolute` from its replacement; the down migration deletes next and retired keys, which the earlier schema cannot hold. Up, down and up again were checked by hand on PostgreSQL 17 with an old current, an old replaced, a next and a retired row.
- The CONV-CODE-007 AC1 scan found two comparisons of a token type with an OpenIddict constant. The code was rewritten (a constant pattern; two `const` fields, compared without regard to case), not the gate.
- Criterion no test decides: `c_hash`. The provider answers `response_type=code` alone, so no identity token is issued beside a code and `c_hash` is never computed; it is the method of `at_hash`, which is tested before and after the start key retires. Verified by review.

**317 (1) and (3) (`81165fe5`).** A `--retention` shorter than the default is read as the default ("never prints a date earlier than the default gives"); `--retention` without `--sealed`, without a value, not a duration or repeated is `api.request.malformed` naming `--retention`. `model.rotation.notready` stands in `ApiStatus`'s fault group beside the startup codes: no request raises it, and the command exits 1. The ledger line of 317 waits with 317 (2) (question 26).

**341 (`c5f86a6e`).** The watch stays in `Janus.Authentication`: D-166 341 moves it to `Janus.Hosting` only where the audit reader is not reachable from `Janus.Authentication`, and it is reached through the watch's own store port (`IMaintenanceStore.KeyEncryptionKeyRotatedAsync`, `BootstrappedAsync`), which `Janus.Storage` implements over `audit_records`. Its reads filter on the nil acting subject and use `ix_audit_records_acting_subject`; no index was added.

**303 (`9cfccf3a`).** No backfill: a record written before keeps the account as its effective identity and names no subject. A record carrying personal details and no subject is refused (`InvalidOperationException`).

**304 (2) and 334 (`be8b2140`).** 334 part (3) was already in the tree (`P90D`, `RestoreTestTests.DR_007_AC1_NoIntervalExceedsTheShortestQuarter`). The in-memory fakes the worker reaches take a lock, since the runs of one round overlap. 304 (1) was built after every part had merged (below).

**323.** No ledger line: section G keeps the entry. The changelog line landed alone in `30b1c4bc` (section 2).

### `part/privacy`, merged as `d768c524`

| Instruction | Commit | Items | Tests |
|---|---|---|---|
| D-166 D.7, 414 under X4, but for the detail of an entered request (question 21): a request's detail, channel, identity confirmation and refusal reason 1 to 1024 characters after trimming, at the endpoint and in the service; a detail kept trimmed | `01c23b22` | API-CONV-002, CONV-CODE-006, PRIV-RIGHT-001 | `PrivacyRequestEndpointTests.API_CONV_002_AnOverlongDetailIsRefusedBeforeTheServiceAsync`, `PrivacyRequestEndpointTests.API_CONV_002_AnEntryWithABlankChannelOrConfirmationIsRefusedAsync`, `PrivacyRequestEndpointTests.API_CONV_002_ARefusalWithABlankReasonIsRefusedAsync`, `PrivacyRequestTests.API_CONV_002_ABlankOrOverlongDetailIsMalformedAsync`, `PrivacyRequestTests.API_CONV_002_ADetailIsHeldTrimmedAsync`, `PrivacyRequestTests.API_CONV_002_AnEntryWithABlankChannelOrConfirmationIsMalformedAsync`, `PrivacyRequestTests.API_CONV_002_ABlankOrOverlongRefusalReasonIsMalformedAsync` |
| D-166 X8: the fulfilment of a privacy request stepped up (`StepUpAction.PrivacyRequestFulfil`), asked after every other refusal | `9c9983cf` | PRIV-RIGHT-001, AUTH-STEP-001 | `PrivacyRequestEndpointTests.PRIV_RIGHT_001_AC5_AFulfilmentWithoutStepUpIsRefusedAsync`, `PrivacyRequestTests.PRIV_RIGHT_001_AC5_AFulfilmentWithoutStepUpChangesNothingAsync`, `PrivacyRequestTests.PRIV_RIGHT_001_AC5_TheStepUpIsAskedAfterEveryOtherRefusalAsync` |
| D-166 D.7, 130: a control for a purpose taking no consent refused; a withdrawal of a consent or an objection never held answered as the withdrawal; an objection before any notice named as such; ledger line 130 | `430eeb6f` | PRIV-CONS-008, PRIV-RIGHT-001a, PRIV-CONS-001 | `RegistrationFlowTests.PRIV_CONS_001_AC1_AControlForAPurposeTakingNoConsentIsRefusedAsync`, `ConsentTests.PRIV_CONS_008_AC1_WithdrawingAConsentNeverGivenAnswersAsTheWithdrawalAsync`, `ConsentTests.PRIV_RIGHT_001a_AnObjectionBeforeAnyNoticeIsNamedAsSuchAsync`, `ConsentTests.PRIV_RIGHT_001a_WithdrawingAnObjectionNeverMadeAnswersAsTheWithdrawalAsync` |
| D-166 D.7, 406: a consent purpose for the hosting refused at the start by any spelling, `model.purpose.hostingconsent`; ledger line 406 | `aca200ba` | INT-HOST-002, PRIV-CONS-010 | `AuthorizationModelTests.INT_HOST_002_AC1_AConsentPurposeForTheHostingFailsStartup` (new cases) |
| D-166 D.7, 264: a failed takedown or restriction delivery completed by hand; ledger line 264 | `ad943871` | IDN-LIFE-003a, IDN-LIFE-003 | `ErasureServiceTests.IDN_LIFE_003a_AFailedTakedownDeliveryIsCompletedByHandAsync`, `ErasureServiceTests.IDN_LIFE_003a_AFailedRestrictionDeliveryIsCompletedByHandAsync`, `ErasureServiceTests.IDN_LIFE_003a_ADeliveryOfAnotherKindIsNotFoundAsync`, `TakedownServiceTests.IDN_LIFE_003_AC2_ATakedownCompletedByHandReadsCompleteAsync` |
| D-166 D.7, 332, first part: erasures completed before the ledger was registered written to it once, read a page at a time | `ce2478a3` | DR-016, IDN-LIFE-003a | `OutboxPublisherTests.DR_016_AnErasureCompletedBeforeTheLedgerWasRegisteredIsAppendedOnceAsync`, `OutboxStoreTests.DR_016_AC5_TheCompletedErasuresWithoutALineAreReadAPageAtATimeAsync` |
| D-166 D.7, 333, with the ledger line of 332: the audit carries an erasure reason in its written spelling | `3654a829` | DR-016, PRIV-RIGHT-005 | `DeletionSweepTests.DR_016_TheAuditCarriesTheReasonInItsWrittenSpellingAsync`, `ErasureReplayTests.DR_016_TheAuditCarriesTheReasonInItsWrittenSpellingAsync` |
| D-166 D.7, 148: a reconsent judged in the service, not at the endpoint; ledger line 148 | `58ce60b9` | PRIV-CONS-001, PRIV-CONS-007 | `ConsentTests.PRIV_CONS_001_AC1_ADashboardGrantOverASupersededConsentIsRecordedAsReconsentAsync`, `ConsentTests.PRIV_CONS_001_AC1_AnAdministratorGrantOverASupersededConsentIsRecordedAsNamedAsync` |
| D-166 D.7, 150: the export carries every grant naming the account and the groups it belongs to; ledger line 150 | `2a2d48ce` | PRIV-RIGHT-003, REG-ACCT-001 | `ExportSourceTests.PRIV_RIGHT_003_TheExportCarriesEveryGrantNamingTheAccountAsync`, `ExportSourceTests.REG_ACCT_001_TheExportCarriesTheGroupsTheAccountBelongsToAsync` |
| D-166 D.7, the data category of an encrypted field: an encrypted field whose category no purpose names stops the start | `f7373d6a` | PRIV-PRIN-001, PRIV-RIGHT-005a | `AuthorizationModelTests.PRIV_PRIN_001_AC2_AnEncryptedFieldWhoseCategoryNoPurposeNamesFailsStartup` |
| D-166 D.7, 133 and 147 part (4): a consent records the document and version it was given against (migration `NameTheDocumentOfAConsent`); a revision ends a consent given against another document | `c573f655` | PRIV-CONS-001, PRIV-CONS-007, PRIV-RIGHT-001a | `SupersessionTests.PRIV_CONS_001_AC2_AConsentNamesTheDocumentAndVersionItWasGivenAgainstAsync`, `SupersessionTests.PRIV_CONS_007_AC1_AConsentGivenAgainstAnotherDocumentIsEndedByARevisionAsync`, `ConsentStoreTests.PRIV_CONS_007_AC1_ALiveConsentAgainstAnotherDocumentIsFoundAsync` |

- Parked: the detail of an entered request (question 21), 156 (question 28), 133 and 147 (1) to (3) (question 29), 133 and 147 (5) (question 30). No ledger line for 133, 147, 156 or 414.
- Merge: no conflict, the model snapshot merged clean. Fast checks after the merge: Analyzers 20, Authentication 849, Authorization 129, Cli 20, Conformance 2, Core 484, Hosting 778, Identity 89, Privacy 252, Storage 33, no failure.

### `part/authorization`, merged as `a8b1f104`

| Instruction | Commit | Items | Tests |
|---|---|---|---|
| D-166 D.1, 396: a permission the model does not declare raised at the request before the gate reads, and answered in no capability; truth-table row "a permission the model does not declare": Raised; ledger line 396 | `cb73c32a` | AUTHZ-PRIN-003, CONV-ERR-001, BFF-CAP-002, AUTHZ-TEST-001 | `GateBehaviourTests.BFF_CAP_002_AC2_AnUndeclaredPermissionIsRaisedAndAnsweredInNoCapabilityAsync`, `GateBehaviourTests.CONV_ERR_001_AC3_AnUndeclaredPermissionRaisesAtTheRequestBeforeTheGateReadsAsync`, `TruthTableTests` (the new row) |
| D-166 D.1, 265, the first half: the lookup of an unregistered record of a declared type refused as the gate refuses, an undeclared type malformed; truth-table row "a lookup of a record no registration names, by a caller reading grants": Denied | `ef62ecdc` | AUTHZ-DERIVE-007, AUTHZ-SCOPE-001, AUTHZ-TEST-001 | `AccessEndpointTests.AUTHZ_DERIVE_007_AnUnregisteredRecordReadsAsARefusalAsync`, `ReverseLookupTests.AUTHZ_DERIVE_007_AnUndeclaredTypeIsRefusedAsMalformedAsync`, `ReverseLookupTests.AUTHZ_DERIVE_007_AnUnregisteredRecordIsRefusedAsTheGateRefusesAsync`, `ReverseLookupTests.AUTHZ_SCOPE_001_AnUnregisteredRecordIsRefusedEvenToAHolderOfGrantReadAsync`, `ExplanationTests.CONV_DESIGN_002_AC3_ALookupOfARecordNoRowNamesIsRefusedAsTheGateRefusesAsync`, `TruthTableTests` (the new row) |
| D-166 D.1, 176: a refused resolution answered with the gate's refusal and its correlation | `503dcbe7` | AUTHZ-CONCEAL-004, AUTHZ-GATE-004 | `ExplanationTests.AUTHZ_CONCEAL_004_AC1_AResolutionRefusedIsTheGatesRefusalWithItsCorrelationAsync` |
| D-166 D.1, 184: a resource type named `organization` stops the start; an unresolved grant refused (`ErrorCodes.GrantUnresolved`); ledger line 184 | `e2069ae7` | AUTHZ-MODEL-004, AUTHZ-GRANT-001 | `AuthorizationModelTests.AUTHZ_MODEL_004_ATypeNamedOrganizationFailsStartup`, `ErrorCodesTests`, `GrantEndpointTests` (changed) |
| D-166 D.1, 183 and 187: an unknown role or restriction answered not found; a restriction reason past its length refused; ledger lines 183 and 187 | `f534c961` | AUTHZ-GRANT-004, AUTH-ABUSE-004, API-CONV-002 | `RoleEndpointTests.AUTHZ_GRANT_004_AnUnknownRoleIsNotFoundAsync`, `RestrictionEndpointTests.AUTH_ABUSE_004_AnUnknownNameIsNotFoundAsync` (renamed), `RestrictionEndpointTests.AUTH_ABUSE_004_AReasonPastItsLengthIsMalformedAsync`, `RestrictionAdministrationTests.AUTH_ABUSE_004_AReasonPastItsLengthIsRefusedAsync` |
| D-166 D.1, 188: the reserved account keeps its role and its grant; ledger line 188 | `49c5bcba` | OPS-BOOT-002, API-CONV-002 | `GrantEndpointTests.OPS_BOOT_002_TheReservedAccountsAdministrationIsNotRevokedAsync`, `RoleEndpointTests.OPS_BOOT_002_TheReservedAccountsRoleKeepsEveryLibraryPermissionAsync` |
| D-166 D.1, 189, and 247 in part: a role a standing invitation names is not removed; ledger line 189 | `650c6c7d` | AUTHZ-GRANT-004, REG-INV-001 | `RoleEndpointTests.AUTHZ_GRANT_004_ARoleAnOpenInvitationNamesIsNotRemovedAsync`, `RoleEndpointTests.AUTHZ_GRANT_004_ARoleAStandingInvitationNamesIsNotRemovedAsync`, `InvitationStoreTests.AUTHZ_GRANT_004_ARoleAStandingInvitationNamesIsNamedAsync` |
| D-166 X5: a member group the group cannot hold answered invalid | `d709ddc2` | AUTHZ-GROUP-001 | `GroupEndpointTests.AUTHZ_GROUP_001_AMemberGroupTheGroupCannotHoldIsInvalidAsync` |
| D-166 D.1, 352: a resource registration refused on its meaning answered invalid; a refused batch writes nothing | `d77844d9` | AUTHZ-INHERIT-002 | `ResourceRegistrationTests.AUTHZ_INHERIT_002_ARefusalOnMeaningIsInvalidAndARefusedBatchWritesNothingAsync` |
| D-166 D.1, 410: another account's session or browser answered as none | `eed5f7f0` | CONV-DESIGN-002, AUTH-SESS-013, AUTH-FACT-015 | `SessionServiceTests.CONV_DESIGN_002_AC3_AnotherAccountsSessionIsAnsweredAsNoneAsync`, `DeviceServiceTests.CONV_DESIGN_002_AC3_AnotherAccountsBrowserIsAnsweredAsNoneAsync`, `AccountApplicationTests.CONV_DESIGN_002_AC3_AnotherAccountsSessionOrBrowserReadsAsNoneAsync` |
| D-166 D.1, 111: the nearest container a derivation admits through is named | `f764ee09` | AUTHZ-GATE-004 | `PermissionRuleTests.AUTHZ_GATE_004_TheNearestAdmittingContainerIsNamed` |
| D-166 D.1, 321: a refusal recorded and counted outside the caller's transaction | `0a488817` | AUTHZ-CONCEAL-004, AUTHZ-GATE-004 | `GateBehaviourTests.AUTHZ_GATE_004_AC4_ADenialInsideATransactionThatRollsBackIsStillRecordedAndCountedAsync` |
| D-166 D.1, 339: an absent record refused with the statements of a present one; ledger line 339 | `05689c65` | AUTHZ-CONCEAL-002, BFF-ERR-003 | `ConcealmentTests.AUTHZ_CONCEAL_002_AC2_AnAbsentRecordAndARefusedOneRunTheSameStatementsAsync` (`Janus.Hosting.Tests`, `Authorization`) |
| D-166 D.1, 252: an administrative grant confers nothing without a membership; truth-table rows "a grant of the administrative organization to a member": Allowed, "to a non-member": Denied; ledger line 252 | `cbd0bb43` | IDN-LIFE-009a, IDN-MEM-001, AUTHZ-TEST-001 | `GateBehaviourTests.IDN_LIFE_009a_AnAdministrativeGrantConfersNothingWithoutAMembershipAsync`, `InvitationServiceTests.IDN_MEM_001_EndingTheAdministrativeMembershipStopsItsGrantsAndKeepsThemAsync` (`Janus.Hosting.Tests`, `Organizations`), `TruthTableTests` (the new rows) |
| D-166 D.1, 110: a check and a page decided over the host's rows in one statement; ledger line 110 | `2ccf3317` | AUTHZ-DERIVE-001, AUTHZ-GATE-005, AUTHZ-GATE-002 | `GateBehaviourTests.AUTHZ_GATE_005_AC1_AStoredDenyAndADerivationAreDecidedInOneStatementAsync`, `GateBehaviourTests.AUTHZ_DERIVE_001_ACheckWithTheHostsRowsReadsNoGrantThroughTheLibraryAsync`, `GateBehaviourTests.AUTHZ_GATE_005_AC1_APageCostsOneStatementOverTheHostsRowsAsync` (extended) |

- Parked: the second half of 265 (question 22) and 136 whole (question 34).
- Limits recorded with the work: `ExplainAsync<TResource>` reads the stored candidates through the library's connection, since 110 names the page and the check only; the denial-spike alert of a refusal inside a transaction that rolls back still rolls back (question 59).
- Merge: one conflict, `src/Janus.Core/PublicAPI.Unshipped.txt`, the lines of both sides kept. Fast checks after the merge: Analyzers 20, Authentication 850, Authorization 131, Cli 20, Conformance 2, Core 484, Hosting 788, Identity 90, Privacy 252, Storage 33, no failure.

### `part/organizations`, merged as `06ec9ba0`

| Instruction | Commit | Items | Tests |
|---|---|---|---|
| D-166 D.6, 413: an organization's comparison key required (`organizations.canonical_name` not null), the unreleased `AddOrganizationComparisonKeys` amended as 413 orders; ledger line 413 | `f709f222` | IDN-ACCT-004 | `OrganizationStoreTests.IDN_ACCT_004_AC3_AnOrganizationWithoutItsKeyIsRefusedByTheDatabaseAsync` |
| D-166 D.6, 154: an attachment decided under the account's row lock; one current membership of an organization held by the database (migration `HoldOneCurrentMembershipOfAnOrganization`) | `46b3534e` | IDN-MEM-002 | `MembershipAttachmentTests.IDN_MEM_002_AC2_TwoAttachmentsTogetherLeaveOneMembershipAsync`, `MembershipAttachmentTests.IDN_MEM_002_TheDatabaseHoldsOneCurrentMembershipOfAnOrganizationAsync` |
| D-166 D.6, 155: the organization erasure's events written before its commit; no domain of an erased organization left readable; the organization erasure reason retired (migration `RetireTheOrganizationErasureReason`); ledger line 155 | `b359fa5c`, `f58a34ad`, `349325d7` | IDN-ORG-003, IDN-ORG-005, IDN-LIFE-003b, DR-016, CONV-DESIGN-002 | `OrganizationErasureSweepTests.IDN_ORG_003_AnErasureWhoseEventRowFailsErasesNothingAsync`, `OrganizationErasureSweepTests.IDN_ORG_003_APassThatCannotReadItsWindowErasesNothingAsync`, `OrganizationErasureSweepTests.IDN_ORG_005_TheErasureIsFiledUnderTheOrganizationAsync`, `OrganizationStatesTests.IDN_ORG_003_TheErasureLeavesNoDomainOfTheOrganizationAsync` |
| D-166 X5 and 230: a path naming no organization answered 404 `identity.organization.notfound`, the path's organization resolved as the gate step | `61b3df11`, `351e2326` | IDN-ORG-003, IDN-MEM-001, REG-DOM-001, REG-INV-001, CONV-DESIGN-002 | `OrganizationEndpointTests.IDN_ORG_003_AC13_AnOrganizationTheDeploymentDoesNotHoldIsNotFoundAsync`, `OrganizationPolicyEndpointTests.IDN_ORG_003_ThePolicyOfAnOrganizationTheDeploymentDoesNotHoldIsNotFoundAsync`, `OrganizationDomainEndpointTests.REG_DOM_001_AnOrganizationTheDeploymentDoesNotHoldIsNotFoundAsync`, `InvitationEndpointTests.REG_INV_001_AnOrganizationTheDeploymentDoesNotHoldIsNotFoundAsync`, `InvitationServiceTests.REG_INV_001_AnOrganizationTheDeploymentDoesNotHoldIsNotFoundAsync`, `PublicSurfaceTests.CONV_DESIGN_002_AC3_EveryOperationMeetsTheGateBeforeItReadsOrWrites` |
| D-166 D.6, 200 and 204: a policy or domain change made in process without a reason refused by its code, judged at the endpoint from `Janus.Core`; ledger line 200 | `acb0b8f2`, `adfe7490` | AUTH-STEP-002a, REG-DOM-001, OPS-CFG-005, LIB-API-005 | `OrganizationPolicyEndpointTests.AUTH_STEP_002a_AReplacementInProcessWithoutAReasonIsRefusedAsync`, `OrganizationDomainEndpointTests.REG_DOM_001_AChangeInProcessWithoutAReasonIsRefusedAsync`, `IdentityEndpointsTests.LIB_API_005_AC1_NoEndpointCarriesLogicItsServiceDoesNotAsync`, the readability tests (updated) |
| D-166 D.6, 209 (1) and (3), 211, 212 and 220: a domain not listed answered 404 (`DomainNotFound`); a lock listing a domain needs `dnsResolver` at the start (`config.value.notallowed`); ledger line 209 | `db60ab71` | REG-DOM-001, LIB-HOST-001, OPS-CFG-003 | `OrganizationDomainEndpointTests.REG_DOM_001_AC12_ADomainIsNotListedWithoutAResolverAsync`, `StartupValidationTests.REG_DOM_001_AC12_ADeploymentWhoseLockListsADomainNeedsAResolverAsync` |
| D-166 D.6, 223: an integrated invitation without a personal email refused 422 (`InvitationAddressRequired`); ledger line 223 | `9e0dbc1d` | REG-INV-001, REG-MAIL-001 | `InvitationServiceTests.REG_INV_001_AC4_AnIntegratedInvitationNeedsAPersonalEmailAsync`, `InvitationEndpointTests.REG_INV_001_AC4_AnIntegratedInvitationWithoutAPersonalEmailIsRefusedAsync` |
| D-166 D.6, 225: a phone bound without reading the registration setting; ledger line 225 | `050d46be` | REG-INV-001 | The invitation service tests, over fakes whose set now validates (section 3) |
| D-166 D.6, 228: a role-naming invitation asks the grant gate as well; ledger line 228 | `f20ce3a0` | REG-INV-001, AUTH-STEP-002 | `InvitationServiceTests.REG_INV_001_ARoleAsksTheGrantGateAsWellAsync` |
| D-166 D.6, 229: an unpublished document refused as an invalid request; ledger line 229 | `b312303a` | REG-INV-001, API-CONV-003 | `InvitationServiceTests.REG_INV_001_AnUnpublishedDocumentIsRefusedAsync`, `InvitationEndpointTests.REG_INV_001_AnUnpublishedDocumentIsRefusedAsync` |
| D-166 D.6, 231: a taken corporate address refused 409 (`MailboxTaken`); ledger line 231 | `1f155712` | REG-MAIL-001, INT-MAIL-006 | `InvitationServiceTests.REG_MAIL_001_AnAddressAlreadyTakenIsRefusedAsync`, `InvitationEndpointTests.REG_MAIL_001_AnAddressAlreadyTakenIsRefusedAsync` |
| D-166 D.6, 233: only an unacknowledged invitation of the organization is revoked, any other answered not found; ledger line 233 | `5e21b292` | IDN-LIFE-009a, API-CONV-003 | `InvitationServiceTests.IDN_LIFE_009a_OnlyAnUnacknowledgedInvitationOfTheOrganizationIsRevokedAsync`, `InvitationEndpointTests.IDN_LIFE_009a_AnUnusedInvitationIsRevokedAsync` |
| D-166 D.6, 242 (1): an inviter with no display name shown by the primary email | `2e5b49df` | REG-INV-002 | `InvitationServiceTests.REG_INV_001_AnInviterWithNoDisplayNameIsShownByTheirPrimaryEmailAsync` |
| D-166 D.6, 242 (2) and 244: an acknowledgement that cannot be met refused before enrolment, the membership limit and the email maximum before the credential policy (`IMembershipAttachment.RefusedAsync`, `Membership.Refused`) | `7737f068` | REG-INV-002, IDN-MEM-002 | `InvitationServiceTests.REG_INV_002_TheMembershipLimitComesBeforeTheCredentialPolicyAsync`, `InvitationServiceTests.REG_INV_002_TheEmailMaximumComesBeforeTheCredentialPolicyAsync` |
| D-166 D.6, 242 (3) and 245: the domain lock judged at acknowledgement, but for an accepting account holding no verified email (question 41); ledger line 245 | `2e8174c1`, `863883c1` | REG-DOM-001, REG-INV-002 | `InvitationServiceTests.REG_DOM_001_AnOpenInvitationIsAcknowledgedOnlyWithAnAddressTheLockAdmitsAsync` |
| D-166 D.6, 242 (4), the first half: the unmet requirement named `policyRequirement` at enrolment | `e5b53dbd` | REG-INV-002, AUTH-FACT-017 | `InvitationServiceTests.REG_INV_002_AC2_AnAccountBelowTheRequiredAssuranceIsHeldAtEnrolmentAsync`, the redundancy test (updated) |
| D-166 D.6, 242 (5) and 247: the inviter judged again at acknowledgement; an expiring grant does not stand in for the permanent one; ledger lines 242 and 247 | `4541d1fc` | REG-INV-001 | `InvitationServiceTests.REG_INV_001_AnInviterWhoLostTheRightToGrantGrantsNothingAsync`, `MembershipAttachmentTests.REG_INV_001_AnExpiringGrantDoesNotStandInForThePermanentOneAsync` |
| D-166 D.6, 248 and 251: the corporate address announced as added and as removed; ledger lines 248 and 251 | `c56ce0c1` | REG-MAIL-001, REG-MAIL-003 | `InvitationServiceTests.REG_INV_001_AC4_TheCorporateAddressIsAnnouncedAsAddedAsync`, `InvitationServiceTests.REG_MAIL_003_TheRetiredCorporateAddressIsAnnouncedAsRemovedAsync` |
| D-166 D.6, 250: the end of a membership stepped up (`StepUpAction.MembershipEnd`); an account holding no membership there answered 404 `identity.membership.notfound`, with its remediation; ledger line 250 | `8510d664`, `d7ad2e63` | IDN-MEM-001, REG-MAIL-003, AUTH-STEP-001, LIB-API-003 | `InvitationServiceTests.IDN_MEM_001_AnAccountHoldingNoMembershipThereIsNotFoundAsync`, `InvitationServiceTests.IDN_MEM_001_EndingAMembershipAsksForStepUpAsync`, `InvitationServiceTests.IDN_MEM_001_OnlyACurrentMembershipIsEndedAsync`, `InvitationEndpointTests.IDN_MEM_001_AMembershipIsEndedAsync`, `ErrorCodesTests` |
| The organization directory's fixture holds one current membership per organization, as 154 requires | `65c03ac2` | IDN-ORG-003, IDN-MEM-002 | `OrganizationDirectoryTests.IDN_ORG_003_AC1_OnlyCurrentMembersAreNamedAsync` |

- Parked: 242 (4), the downgrade and `auth.factor.notpermitted` halves (question 36), with ledger line 246; the case of 242 (3) of an account holding no verified email (question 41); 209 (2), which waits on the owner's approval to download `IdnaMappingTable.txt`. 223 point (3) was built on the working branch after the merges (`ebe969ae`), since `ErrorCodes.GrantUnresolved` came with `part/authorization`.
- Merge: one conflict, `src/Janus.Core/StepUpAction.cs` (section 3), and one semantic conflict in a test (section 3). The model has no pending change. Fast checks after the merge: Analyzers 20, Authentication 860, Authorization 131, Cli 20, Conformance 2, Core 484, Hosting 798, Identity 90, Privacy 253, Storage 33, no failure; the integration suite of `Janus.Storage.Tests` 442, no failure.

### `part/sending`, merged as `057973e4`

| Instruction | Commit | Items | Tests |
|---|---|---|---|
| D-166 D.3, 120 (1): a restriction's name held to the rule of its place | `50016b49` | INT-SMS-003, AUTH-ABUSE-004 | `RestrictionSetSettingTests.INT_SMS_003_ARestrictionNameOutsideTheRuleIsRefused`, `RestrictionSetSettingTests.INT_SMS_003_ARestrictionNameIsAtMostSixtyFourCharacters`, `RestrictionSetSettingTests.INT_SMS_003_ARestrictionNameInsideTheRuleIsAdmitted`, `RestrictionSetSettingTests.INT_SMS_003_TheShippedRestrictionNamesKeepTheRule`, `RestrictionAdministrationTests.INT_SMS_003_ARestrictionNamedOutsideTheRuleIsRefusedBeforeAnythingBeginsAsync` |
| D-166 D.3, 120 (2): a governing document or subscriber named outside the rule refused at the start | `ef19b85f` | INT-SMS-003, LIB-HOST-001 | `StartupValidationTests.INT_SMS_003_AC3_ASubscriberNamedOutsideTheRuleIsRefusedAsync`, `StartupValidationTests.INT_SMS_003_AC3_AGoverningDocumentNamedOutsideTheRuleIsRefusedAsync` |
| D-166 D.3, 120 (3) to (5): the key and outstanding places measured per deployment; ledger line 120 | `25d5dc9c` | INT-SMS-003, AUTH-ABUSE-005 | `MessagePlaceholdersTests.INT_SMS_003_AC1_TheKeyWidthCoversTheFamilies`, `MessagePlaceholdersTests.INT_SMS_003_EveryEventKindFitsItsPlace`, `SendingValidationTests.INT_SMS_003_AC1_TheSubscribersAreMeasuredAtTheirJoinedWidthAsync` |
| D-166 D.3, 342 (1) to (4): each restriction governs its channel (`sms`, `email`, `any`); narrowing a channel is a loosening; ledger line 342 | `61204cc0` | AUTH-ABUSE-004, OPS-ALERT-002, OPS-ALERT-003 | `SendingServiceTests.AUTH_ABUSE_004_ARestrictionGovernsOnlyItsChannelAsync`, `SendingServiceTests.AUTH_ABUSE_004_AC5_ANoticeToAHolderIsNotCountedBySmsSourceAsync`, `SendingServiceTests.AUTH_ABUSE_004_ASendNoRequestAskedForIsCountedUnderNoSourceAsync`, `SendingServiceTests.OPS_ALERT_003_AnAlertIsCarriedWithEveryRestrictionsBucketFullAsync`, `RestrictionEndpointTests.AUTH_ABUSE_004_AC3_NarrowingAChannelIsALooseningAsync`, `RestrictionSetSettingTests.AUTH_ABUSE_004_AC3_NarrowingAChannelIsALoosening`, `RestrictionSetSettingTests.AUTH_ABUSE_004_TheShippedRestrictionsCarryTheirChannels`, `RestrictionSetSettingTests.AUTH_ABUSE_004_AStoredRestrictionWithoutAChannelReadsAsAny`, `AuthenticationServiceTests.AUTH_ABUSE_004_TheCheckCodeIsSentUnderTheSourceThatAskedAsync` |
| D-166 D.3, 122: destination counters kept apart from other keys (migration `CountKeysApartFromDestinations`, `CounterStaleness`, `SendCounterSweep`); ledger line 122 | `fe839f64`, `3d136d1e` | PRIV-RET-005, AUTH-ABUSE-004 | `SendLedgerTests.PRIV_RET_005_AC2_ALongerSourceRestrictionKeepsNoDestinationRecordAsync`, `ExpirySweepTests.PRIV_RET_005_AC2_ARecordIsGoneWithoutAnotherSendAsync`, the schema contract lines of `send_key_counters` |
| D-166 D.3, 123 (1): the landing origins a link lands on required at the start (`LandingOrigins`, public) | `56312065` | LIB-HOST-001, API-LAND-001 | `StartupValidationTests.LIB_HOST_001_ADeploymentThatDeclaredNoLandingOriginsIsRefusedAsync`, `StartupValidationTests.LIB_HOST_001_AC6_ALandingOriginNoBrowserClientReturnsToIsRefusedAsync`, `StartupValidationTests.LIB_HOST_001_AC6_AnAuthenticationOriginThatIsNotTheSignInOriginIsRefusedAsync` |
| D-166 D.3, 123 (2) to (4): every link an address on its landing origin, `{link}` in place of `{token}`, a text carrying a link budgeted at two segments; 123 (5) was already in the tree | `908f5817` | API-LAND-001, INT-SMS-003, LIB-HOST-001 | `LandingLinksTests.API_LAND_001_AC4_EveryLinkIsItsApplicationsOriginThenItsKindAndToken`, `LandingLinksTests.INT_SMS_003_NoLinkIsWiderThanItIsMeasured`, `MessageBudgetTests.INT_SMS_003_ATextCarryingALinkHoldsTwoSegments`, `MessagePlaceholdersTests.INT_SMS_003_TheLinkIsMeasuredAtItsComposedWidth`, `SendingValidationTests.LIB_HOST_001_NoLinkIsMeasuredWithoutTheLandingOriginsAsync`, `SendingValidationTests.INT_SMS_003_ATextCarryingALinkIsBudgetedAtTwoSegmentsAsync`, `ApiConventionTests.API_LAND_001_AC4_NoEndpointTakesALinkTokenInAPathOrQueryAsync`, `DefaultMessageTemplatesTests.API_LAND_001_AC4_EveryShippedLinkRendersAnAbsoluteAddressOfItsApplication`, `DefaultMessageTemplatesTests.API_LAND_001_AC4_NoShippedTemplateNamesTheRetiredTokenPlace`, `DefaultMessageTemplatesTests.AUTH_ABUSE_005_EveryShippedTextMessageFitsItsBudgetAtItsWidest` (renamed) |
| D-166 D.3, the message kinds, (5): a request's type and status spelled as the chapter spells them | `cc804177` | INT-SMS-003 | `MessagePlaceholdersTests.INT_SMS_003_TypeAndStatusAreMeasuredAtTheirWrittenSpellings`, `DeadlineSweepTests.INT_SMS_003_AnAlertCarriesTheTypeAndStatusAsTheChapterSpellsThemAsync` |
| D-166 D.3, the message kinds, (6): a text template naming a place with no width, or the retired token place, stops the start | `b9c5243e` | INT-SMS-003 | `SendingValidationTests.INT_SMS_003_ATemplateNamingAPlaceWithNoWidthStopsStartupAsync`, `SendingValidationTests.INT_SMS_003_ATemplateNamingTheRetiredTokenPlaceStopsStartupAsync` |
| D-166 D.3, 335 but for the twenty-sets test (question 27): a recovery-code reminder stays owed while every channel refuses, and is closed where no channel can reach | `4ea7c61e` | AUTH-FACT-008 | `RecoveryCodeRemindersTests.AUTH_FACT_008_AC5_ASetWhoseEveryNoticeIsRefusedStaysOwedAsync`, `RecoveryCodeRemindersTests.AUTH_FACT_008_AC5_ASetNoChannelCanReachIsClosedAsync` |
| D-166 D.3, 119 (7): recovery and invitation links counted under `signin`, by no notification restriction | `a60f77f5` | AUTH-ABUSE-004 | `RecoveryFlowTests.AUTH_ABUSE_004_ARecoveryLinkIsCountedByNoNotificationRestrictionAsync`, `InvitationEndpointTests.AUTH_ABUSE_004_AnInvitationLinkIsCountedByNoNotificationRestrictionAsync` |
| D-166 D.3, 119 (5): the deduplication claim committed before an alert is sent; no alert sent while a transaction is open | `01a5a2c9` | OPS-ALERT-002, INF-BG-001 | `AlertRouterTests.OPS_ALERT_002_TheClaimIsCommittedBeforeTheAlertIsSentAsync`, `AlertDispatchTests.OPS_ALERT_002_NoAlertIsSentWhileATransactionIsOpenAsync` |

- Parked: 118, 235 and the twenty-sets test of 335 (question 27); 119 (1) to (4) (question 38); 119 (6) (question 39). Question 24 parks nothing. No ledger line for 118, 119, 123 (until `96776b0c`), 227, 235, 322, 335 or 423.
- The message kinds (1) to (3) were built on the working branch after the merges (`a3119f63`, `96776b0c`, `72940c48`): `SignInCode` takes 21 on `part/sessions`, and a gap at 21 trips CA1027.
- A delivery that fails after its committed alert claim is not tried again inside its window (119 (5)); the changelog says so.
- Merge: conflicts in `CHANGELOG.md`, `InvitationService.cs`, `RestrictionAdministration.cs`, `DeclarationCoverage.cs` and `StartupValidationTests.cs`, the lines of both sides kept; two semantic conflicts in tests (section 3). The model has no pending change. Fast checks after the merge green (Authentication 875, Core 509, Hosting 809, Privacy 254); the integration suites `Janus.Storage.Tests` 443, `Janus.Hosting.Tests` 264, `Janus.Conformance.Tests` 10, no failure.

### `part/sessions`, merged as `304254fe`

| Instruction | Commit | Items | Tests |
|---|---|---|---|
| D-166 E.8: the throttled refusal built in one place (`Error.Throttled`, in `Janus.Core`), its code read only by the builder and the status map | `7d9477bc` | AUTH-ABUSE-002, BFF-ABUSE-001 | `ErrorTests.AUTH_ABUSE_002_AC2_TheThrottledRefusalCarriesTheInstantAndNothingElse`, `ErrorTests.Throttled_AnInstantWithAnOffset_IsWrittenInUtc`, `LibraryStructureTests.AUTH_ABUSE_002_AC2_OnlyTheBuilderAndTheStatusMapReadTheThrottledCode` |
| D-166 D.2, 401: a throttle delay run from the count its failure wrote | `aefb6b0f` | AUTH-ABUSE-001 | `ThrottleServiceTests.AUTH_ABUSE_001_ASourceAtNineFailuresIsHeldTheWholeDelayItEarnedAsync` |
| D-166 D.2, the preferred second step named by its method | `f883e827` | IDN-ATTR-008, API-CONV-003 | `AccountFlowTests.IDN_ATTR_008_AC2_AMethodNotEnrolledIsInvalidOverTheWireAsync`, `AccountServiceTests` (changed) |
| D-166 D.2, 421 (1) and (3): a recognised browser's failures counted against every scope; a carried token looked up alike for a held and an unheld identifier; ledger line 421 | `038ce1c7` | AUTH-ABUSE-001, AUTH-ABUSE-003 | `ThrottleServiceTests.AUTH_ABUSE_001_AC5_ARecognisedBrowsersFailuresCountAgainstTheAccountAsync`, `AuthenticationServiceTests.AUTH_ABUSE_003_AC2_ACarriedTokenIsLookedUpAlikeForAHeldAndAnUnheldIdentifierAsync` |
| D-166 D.2, 419, the amendment of the unreleased `AddChallengeIdentifiers`: the identifier hash required on every sign-in challenge | `777086bd` | OPS-SEC-003, AUTH-ABUSE-001 | `ChallengeStoreTests` and `FingerprintRotationTests` (adjusted) |
| D-166 D.2, 417: group names and credential labels compared without case (migration `CollateGroupNamesAndCredentialLabels`); ledger line 417 | `db7b0136` | OPS-DB-001, AUTH-FACT-001, INF-DB-001 | `AccountServiceTests.AUTH_FACT_001_AC5_ALabelHeldInOtherCapitalsIsRefusedAsync`, `CredentialServiceTests.AUTH_FACT_001_AC5_AnEnrolmentUnderALabelHeldInOtherCapitalsIsRefusedAsync`, `AuthenticatorStoreTests.AUTH_FACT_001_AC5_ALabelHeldInOtherCapitalsIsRefusedByTheDatabaseAsync`, `AuthenticatorStoreTests.AUTH_FACT_001_AC5_ALabelIsFoundHeldWithoutRegardToCaseAsync`, `GroupClosureStoreTests.OPS_DB_001_AnOrganizationsGroupsSortWithoutRegardToCaseAsync` |
| D-166 D.2, 115 but for (2) (question 31), under X3: every code try decided under a lock on its row | `6b79266c` | AUTH-FACT-004, AUTH-FACT-003, INT-SMS-001 | `AuthenticationServiceTests.AUTH_FACT_004_AC6_AnEmailSignInCodeIsHeldToItsOwnKeysAsync`, `AuthenticationServiceTests.AUTH_FACT_004_AC6_TheCodeASignInLinkShowsIsHeldToTheLinkAndTheSignInCapAsync`, `AuthenticationServiceTests.AUTH_FACT_004_AC3_WrongCodesInvalidateTheHeldSignInAsync`, `PendingSignInStoreTests.AUTH_FACT_004_AC4_APendingSignInHeldForATryIsReadByTheNextOnlyAfterItAsync`, `VerificationCodesTests.AUTH_FACT_004_AC3_ConcurrentWrongTriesAreAllCountedAsync`, `VerificationCodesTests.AUTH_FACT_004_AC3_TwoConcurrentRightTriesSucceedOnceAsync` |
| D-166 D.2, 419 but for the link-token case (question 32): registration codes and asks held to the progressive delay | `7f350977` | AUTH-ABUSE-001, REG-SESS-003 | `RegistrationServiceTests.AUTH_ABUSE_001_WrongRegistrationCodesAreHeldByTheDelayAsync` |
| D-166 D.2, 402 and 422: refused device codes and pressed links that are gone recorded behind the source delay; a throttled return carries its interval; ledger lines 402 and 422 | `50432574` | CONV-LOG-005, AUTH-ABUSE-001, AUTH-ABUSE-002, BFF-ABUSE-001 | `AuthenticationServiceTests.CONV_LOG_005_AC1_ADeviceCodeForAHandleThatOpensNothingIsRecordedAsync`, `AuthenticationServiceTests.CONV_LOG_005_AC1_APressedLinkThatIsGoneIsRecordedBehindTheSourceDelayAsync`, `AuthenticationServiceTests.CONV_LOG_005_AC1_AnUnpressedLinkThatIsGoneWritesNothingAsync`, `SignInFlowTests.CONV_LOG_005_AC1_ALinkTokenUnderAnotherFactorIsRefusedAsMalformedAsync`, `SessionAuditTests.CONV_LOG_005_AC1_ARefusedDeviceCodeIsRecordedAsItsVerificationAsync`, `ProviderSignInTests.BFF_ABUSE_001_AC2_AThrottledProviderReturnCarriesItsIntervalAsync`, `ProviderSignInTests.AUTH_ABUSE_002_AC2_AThrottledReturnCarriesItsIntervalAsync` |
| D-166 D.2, 208: what went to an address given up since refused; no ledger line, as section G gives none | `9017f0ba` | REG-IDENT-006, REG-DOM-001, CONV-LOG-005 | `DomainLockTests.REG_DOM_001_AnAddressThatDoesNotParseIsRefusedWhereverALockAppliesAsync`, `AuthenticationServiceTests.REG_IDENT_006_ALinkSentBeforeTheAddressWasRemovedDoesNotSignInAsync`, `AuthenticationServiceTests.REG_IDENT_006_AC6_ACodeSentBeforeTheAddressWasRemovedDoesNotSignInAsync`, `AuthenticationServiceTests.REG_IDENT_006_AC6_ASignInOpenedWithAnAddressSinceRemovedDoesNotSignInAsync` |
| D-166 D.2, 146: the second-step code sent by text and the carrier asked first (migration `BindSecondStepCodesToTheirChallenge`); ledger line 146 | `52482ed5` | AUTH-FACT-002, AUTH-FACT-002b, AUTH-FACT-004, AUTH-STEP-002 | `AuthenticationServiceTests.AUTH_FACT_002_AC4_APasswordAndASentTextCodeCompleteAtAal2Async`, `AuthenticationServiceTests.AUTH_FACT_002_AC4_ATextCodeIssuedForAnotherSignInIsRefusedAsync`, `AuthenticationServiceTests.AUTH_FACT_002_AC6_AnAskBeforeAFirstFactorSendsNothingAsync`, `AuthenticationServiceTests.AUTH_FACT_002b_AC6_AReportedChangeAtTheAskSendsNoTextCodeAsync`, `AuthenticationServiceTests.AUTH_FACT_002b_AC6_AReportedChangeWithholdsTheTextCodeFromAStepUpAsync`, `AuthenticationServiceTests.AUTH_FACT_002b_AC6_AReportedChangeSendsNoSignInLinkByTextAsync`, `AuthenticationServiceTests.AUTH_ABUSE_003_AC1_ANumberNoAccountHoldsIsAnsweredInTheSameBytesAsync`, `SignInFlowTests.AUTH_FACT_002_AC4_ATextCodeAskedForAndPresentedCompletesAtAal2Async`, `SignInFlowTests.AUTH_FACT_002b_AC6_ALinkByTextToAReportedNumberIsAnsweredAsAnyAskAsync`, `PendingSignInStoreTests.AUTH_FACT_002_AC4_ASecondStepCodeIsReadBackBoundToItsChallengeAsync`, `StepUpGuardTests.AUTH_FACT_002b_AC6_AReportedChangeWithholdsTheTextCodeFromTheCombinationsAsync`, `StepUpGuardTests.AUTH_FACT_002b_AC6_AStepUpLeftWithNoCombinationIsToldToReportTheLossAsync`, `RecoveryServiceTests.AUTH_FACT_002b_AC6_AReportedChangeSendsNoRecoveryLinkByTextAsync`, `SendingServiceTests.AUTH_FACT_002b_AC6_ARecoveryLinkByTextIsConsideredBeforeItGoesAsync` |
| D-166 D.2, 152 (1) to (3) but for `Effective` (question 40), under X1: each credential event written before its commit | `50cd2637` | AUTH-STEP-007, AUTH-RECOV-007, CONV-DESIGN-002 | `CredentialServiceTests.AUTH_STEP_007_AnEnrolmentWhoseEventRowFailsCommitsNothingAsync`, `CredentialServiceTests.AUTH_STEP_007_ASetPasswordIsAnnouncedAsync`, `RecoveryServiceTests.AUTH_STEP_007_ARecoveredPasswordIsAnnouncedAsync`, `LossReportsTests.AUTH_RECOV_007_ASuspensionWhoseEventRowFailsLeavesTheCredentialActiveAsync`, `LossReportsTests.AUTH_RECOV_007_AReportNamesWhoMadeItAsync` |
| D-166 D.2, 129 (2): an assertion with no handle refused where no account was named; no changelog line, since every caller passes `identified: true` | `6e0c13e8` | REG-PM-001 | `WebAuthnServiceTests.REG_PM_001_AnAssertionWithNoHandleIsRefusedWhereNoAccountWasNamedAsync`, `WebAuthnServiceTests.REG_PM_001_ASecondStepKeyWithNoHandleIsJudgedAsBeforeAsync` |
| D-166 D.2, 328: a host's report judged against the gate it costs (`AttainedAssurance`; `IAssuranceProvider.AttainedAsync` in place of `LevelAsync`; `ISessionGates.CostAsync`, `StepUpGuard.CostAsync`) | `669bac7b` | LIB-HOST-004, AUTH-STEP-002, AUTH-STEP-003 | `StepUpGatesTests.LIB_HOST_004_AProviderReportingTheGateMetAdmitsTheActionAsync`, `StepUpGatesTests.LIB_HOST_004_AProviderReportingAnOlderProofIsRefusedWithTheGateAsync`, `StepUpGatesTests.LIB_HOST_004_AProviderReportingNoPhishingResistanceMeetsNoPhishingResistantGateAsync` |
| D-166 D.2, 326, option A: places compared by country where no city is known; ledger line 326 | `9c55c377` | OPS-ALERT-007 | `SessionServiceTests.OPS_ALERT_007_APlaceWithACountryAndNoCityIsComparedByItsCountryAsync` |

- Parked: 115 (2) (question 31), the link-token case of 419 (question 32), `Effective` on `CredentialSuspended` (question 40), 129 (1) (question 42), the truth-table rows of 328 (question 46); the ledger lines of 115, 129, 152, 328 and 419 with them. No ledger line for 208 or 401. `StepUpAction` gains nothing here; `MessageKind.SignInCode` takes 21.
- Merge: conflicts in `CHANGELOG.md`, the `StepUpGuard` construction of `InvitationServiceTests` and the parameters of the Hosting `Deployment`, the lines of both sides kept; two semantic conflicts in tests (section 3). The model has no pending change. Fast checks after the merge green (Authentication 905, Authorization 134, Core 512, Hosting 815); the integration suites `Janus.Storage.Tests` 451 (one test put right and its class run again, 4 of 4) and `Janus.Hosting.Tests` 264, no failure.

### `part/registration-accounts`, merged as `5caf97e4`

| Instruction | Commit | Items | Tests |
|---|---|---|---|
| D-166 D.4, 127: a blank passkey or sign-in address refused at the start | `67c5dcec` | REG-PM-001, LIB-HOST-001 | `StartupValidationTests.REG_PM_001_AC3_ABlankFieldIsRefusedAsync` |
| D-166 F and D.4, `identity.registration.incomplete`: a step out of order answered 409 | `3ff77d5d` | REG-SESS-002, REG-SESS-003 | `ApiStatusTests.REG_SESS_002_AC1_AStepOutOfOrderIsAConflict`, `RegistrationWizardTests.REG_SESS_003_AConfirmationBeforeTheAddressIsVerifiedIsAConflictAsync`, `ApiStatusTests` (updated) |
| D-166 D.4, an unverified identifier made primary or backup refused 409 | `13513b9f` | REG-IDENT-005, REG-IDENT-002 | `ApiStatusTests.REG_IDENT_005_AC2_AnUnverifiedIdentifierIsAConflict`, `IdentifierServiceTests.REG_IDENT_002_AnUnverifiedIdentifierIsNotNamedTheBackupAsync` |
| D-166 D.4, 415: no account created without its document versions | `0283b42e` | REG-SESS-007 | `RegistrationServiceTests.REG_SESS_007_AC2_AnAccountIsNeverCreatedWithoutItsDocumentVersionsAsync` |
| D-166 D.4, 141: a reserved address answered as a held one; ledger line 141 | `4459da5b` | REG-IDENT-006, REG-SESS-005, REG-INV-002, REG-IDENT-008 | `RegistrationServiceTests.REG_IDENT_006_AC2_AReservedAddressIsNotVouchedForByAProviderAsync`, `RegistrationServiceTests.REG_IDENT_006_AC2_AReservedAddressIsAnsweredAtRegistrationAsAHeldOneIsAsync`, `RegistrationServiceTests.REG_SESS_005_AC4_AnAddressTakenSinceItWasStagedEndsTheSessionAtTheTermsAsync`, `RegistrationServiceTests.REG_SESS_005_AC4_AnAddressReservedSinceItWasStagedEndsTheSessionAtTheTermsAsync`, `RegistrationServiceTests.REG_IDENT_006_AC2_ABoundEmailReservedForAnUndoOpensNoRegistrationAsync`, `RegistrationFlowTests.REG_IDENT_006_AC2_TheUndoRestoresAnAddressARegistrationTriedToTakeAsync` |
| D-166 D.4, 363: the session a replacement completes under is kept; ledger line 363 | `327ba1fe` | IDN-LIFE-008, BFF-SESS-004 | `IdentifierServiceTests.IDN_LIFE_008_AC1_AReplacementCompletedInAnotherSessionKeepsThatSessionAloneAsync`, `IdentifierServiceTests.IDN_LIFE_008_AC1_AReplacementTheOldAddressConfirmsEndsEverySessionAsync`, `AccountApplicationTests.BFF_SESS_004_AC2_TheSessionThatCompletesAReplacementAnswersToItsNewSecretAsync` |
| D-166 D.5, 169, 170, 254, 255 and 257: the state a takedown finds held and restored at its reversal (migration `HoldTheStateATakedownFinds`); ledger lines 169, 170, 254, 255 and 257 | `b1960a2e` | IDN-LIFE-003, IDN-LIFE-013, PRIV-RIGHT-004, IDN-ATTR-003 | `TakedownEndpointTests.IDN_LIFE_003_AC2_AReversedTakedownReadsAsReversedAsync`, `TakedownServiceTests.IDN_LIFE_003_AC2_AReversedTakedownReadsAsReversedAsync`, `AccountTests.IDN_LIFE_003_AC5_AReversalRestoresTheSuspensionTheTakedownFoundAsync`, `AccountTests.IDN_LIFE_003_AC5_AReversalReturnsARunningDeletionToItsOwnWindowAsync`, `AccountTests.IDN_LIFE_013_AReversedTakedownReturnsTheAdministratorsSuspension`, `AccountTests.IDN_LIFE_013_AReversedTakedownReturnsTheOwnersDeactivation`, `AccountTests.IDN_LIFE_003_AnErasedAccountHoldsNothing`, `DeletionSweepTests.IDN_LIFE_003_ATakenDownDeletionIsErasedAtItsSettledInstantAsync`, `TakedownServiceTests.IDN_LIFE_003_ARunningDeletionIsTakenDownAsync`, `AccountStatesTests.IDN_LIFE_003_ARunningDeletionIsTakenDownAsync`, `TakedownServiceTests.IDN_LIFE_003_AnErasedAccountIsNotTakenDownAsync`, `TakedownServiceTests.IDN_LIFE_003_ASubjectWithNoAccountIsNotFoundAsync`, `AccountStoreTests.IDN_LIFE_003_TheRowCarriesWhatATakedownHoldsAsync`, `AccountStatesTests.IDN_LIFE_013_AReversedTakedownLeavesTheAccountToAnAdministratorAsync` |
| D-166 D.5, 171 under X1: the suspension and the reversal written before the commit; ledger line 171 | `d94a6ebe` | IDN-LIFE-003, CONV-DESIGN-002 | `TakedownServiceTests.IDN_LIFE_003_AC4_TheSuspensionIsWrittenInTheTriggerTransactionAsync`, `TakedownServiceTests.IDN_LIFE_003_ARefusedAnnouncementLeavesNothingAsync`, `TakedownServiceTests.IDN_LIFE_003_AC5_TheReversalIsWrittenInItsTransactionAsync`, `TakedownServiceTests.IDN_LIFE_003_ARefusedReversalAnnouncementLeavesNothingAsync`, `TakedownServiceTests.IDN_LIFE_003_AC4_EveryTriggerAnnouncesTheSuspensionAsync`, `AccountStatesTests.IDN_LIFE_003_AC4_TheSuspensionRowCommitsWithTheTriggerAsync` |
| D-166 D.5, 173 and 174: the reversal and the erasure judged under a row lock; a reason past the limit malformed; the step-up judged after every other refusal | `78521dd6` | IDN-LIFE-003, API-CONV-002 | `TakedownServiceTests.API_CONV_002_AReasonPastTheLimitIsMalformedAsync`, `TakedownEndpointTests.API_CONV_002_AReasonPastTheLimitIsMalformedAsync`, `TakedownServiceTests.IDN_LIFE_003_TheStepUpIsJudgedAfterEveryOtherRefusalAsync`, `AccountStatesTests.IDN_LIFE_003_AReversalAtTheBoundaryWaitsForTheErasureAndRefusesAsync`, `AccountStatesTests.IDN_LIFE_003_AnErasureAtTheBoundaryWaitsForTheReversalAndRefusesAsync` |
| D-166 D.5, 258 to 261: the lift of a restriction and a cancellation on the subject's behalf stepped up; an account without a photo answered `identity.photo.notfound`; ledger lines 258 to 261 | `32d19634` | PRIV-RIGHT-004, IDN-LIFE-003, IDN-ATTR-003, AUTH-STEP-001 | `AccountAdministrationTests.PRIV_RIGHT_004_LiftingARestrictionAsksForStepUpAsync`, `AccountAdministrationTests.IDN_LIFE_003_ACancellationOnTheSubjectsBehalfAsksForStepUpAsync`, `AccountAdministrationEndpointTests.IDN_ATTR_003_AC3_AnAccountWithoutAPhotoIsAnsweredWithTheCodeAsync`, the PRIV-RIGHT-004 AC2 test (a stale session case added) |
| D-166 X8: ending sessions that are not the caller's stepped up | `b1f28504` | AUTH-SESS-011, AUTH-SESS-009, AUTH-STEP-001 | `SessionServiceTests.AUTH_SESS_011_RevokingOneAccountAsksForStepUpAsync`, `SessionServiceTests.AUTH_SESS_009_TheExplicitRevocationAsksForStepUpAsync`, `SessionRevocationEndpointTests.AUTH_SESS_011_OneAccountsRevocationAsksForStepUpAsync`, `SessionRevocationEndpointTests.AUTH_SESS_009_EveryRevocationAsksForStepUpAsync` |
| D-166 D.5, 362 (1) to (4) and (6): a restricted account signs in with its factors, provider and links, reads, and is refused a change; its sessions end at the restriction; ledger line 362 | `6a586266` | IDN-ACCT-007, AUTH-SESS-010 | `AuthenticationServiceTests.IDN_ACCT_007_AC2_ARestrictedAccountSignsInWithItsFactorAsync`, `AuthenticationServiceTests.IDN_ACCT_007_AC2_ARestrictedAccountSignsInWithItsProviderAsync`, `AuthenticationServiceTests.IDN_ACCT_007_AC2_ARestrictedAccountIsSentItsSignInLinkAsync`, `AuthenticationServiceTests.IDN_ACCT_007_ASuspendedDeletingOrDeletedAccountIsStillRefusedAsync`, `AuthenticationServiceTests.IDN_ACCT_007_ARestrictedAccountsSignInTrustsNoDeviceAsync`, `OidcServiceTests.IDN_ACCT_007_AC2_ARestrictedAccountsClaimsAreAnsweredAsync`, `RecoveryServiceTests.IDN_ACCT_007_AC2_ARestrictedAccountRecoversItsPasswordAsync`, `CredentialServiceTests.IDN_ACCT_007_AC2_AnApprovedRecoveryEnrolsForARestrictedAccountAsync`, `InvitationServiceTests.IDN_ACCT_007_AC2_ARestrictedAccountAcknowledgesNoInvitationAsync`, `AccountStatesTests.AUTH_SESS_010_RestrictingAnAccountEndsItsSessionsAsync`, `AccountApplicationTests.IDN_ACCT_007_AC2_ARestrictedAccountSignsInReadsAndIsRefusedAChangeAsync` |
| D-166 D.5, an out-of-band erasure by the account's state (question 45) | `0149b6d6` | PRIV-RIGHT-001, IDN-LIFE-003 | `PrivacyRequestTests.PRIV_RIGHT_001_IDN_LIFE_003_AnActiveOrRestrictedAccountEntersTheWindowAsync`, `PrivacyRequestTests.PRIV_RIGHT_001_IDN_LIFE_003_ASuspendedAccountEntersTheWindowAndComesBackSuspendedAsync`, `PrivacyRequestTests.PRIV_RIGHT_001_IDN_LIFE_003_ADeletingAccountKeepsItsRunningWindowAsync`, `PrivacyRequestTests.PRIV_RIGHT_001_IDN_LIFE_003_ADeletedAccountChangesNothingAsync`, `PrivacyRequestTests.PRIV_RIGHT_001_IDN_LIFE_003_AnErasureThatCannotBeginFailsTheFulfilmentAsync`, `AccountTests.IDN_LIFE_003_ASuspendedAccountEntersTheWindowOutOfBandAndComesBackSuspended`, `AccountStatesTests.IDN_LIFE_003_ASuspendedAccountBeginsItsDeletionOutOfBandAsync` |
| D-166 D.5, the grants at deletion: the account's grants revoked in the erasure transaction (`SubjectEraser` takes `DataConnections`; `GrantStore.RaiseAsync`) | `f7757aff` | IDN-LIFE-014, IDN-PRIN-003, AUTHZ-CACHE-001 | `SubjectEraserTests.IDN_LIFE_014_TheAccountsGrantsReadRevokedAndEveryRowStandsAsync` |
| D-166 D.5, 362 (5), the mailbox half: a restricted holder's mailbox stays owed enabled | `0bab2cda` | IDN-ACCT-007, INT-MAIL-006a | `MailboxStoreTests.IDN_ACCT_007_AC2_ARestrictedHolderStandsAsync`; the app-password half is `AppPasswordsTests.REG_MAIL_002_AC4_ARestrictedAccountListsAndRevokesAndCreatesNoneAsync`, from 263 (section 3) |
| The deployment key's migration test writes its rows in the columns of its own migration (section 3) | `60dd9cb9` | PRIV-RIGHT-005a | `DeploymentDataKeyTests` (the PRIV-RIGHT-005a AC18 rows) |

- Parked: 143 (question 23), the removal of the `photo.enabled` family of 144 and 315 (question 25). Committed under questions still open: the reversal of a takedown (question 44), the codes of the fulfilment (question 45). 306 was left to the working branch, then parked (question 31). The patches of 143 and 144 are held aside.
- Merge: conflicts in `CHANGELOG.md`, `CredentialService.ActingAsync` (this part's structure, in which enrolment is not asked about the restriction, with the working branch's access context), `InvitationAcknowledgement` (the gate, the scope, the roles and the restriction), `StepUpAction.cs` (section 3), `InvitationServiceTests` and `ErrorCodesTests` (both sides kept, in order); semantic conflicts in tests (section 3). The model has no pending change. Fast checks after the merge green (Authentication 929, Core 512, Hosting 826, Identity 99, Privacy 274); the integration suites `Janus.Storage.Tests` 460 (one test put right and its class run again, 6 of 6), `Janus.Hosting.Tests` 269, `Janus.Cli.Tests` 69, no failure.

### `part/gates`, merged as `d5a7fc0e`

| Instruction | Commit | Items | Tests |
|---|---|---|---|
| D-166 D.11, 142: the step a swept session reaches succeeds on the retry | `e39bc024` | BFF-ORDER-001 | `SessionRequirementTests.BFF_ORDER_001_ASweptSessionIsRefusedOnceAndTheRetrySucceedsAsync` |
| D-166 D.11, 272: every correlation reference marked never logged; ledger line 272 | `fda23656` | CONV-LOG-003, INT-GEN-003 | `NeverLoggedTests.CONV_LOG_003_AC1_EveryCorrelationReferenceIsMarked` |
| D-166 E.10: the migration tool's files marked generated code | `30a8c32c` | CONV-SETUP-004 | `LibraryStructureTests.CONV_SETUP_004_AC3_OnlyTheMigrationToolsFilesAreGeneratedAndEverySuppressionIsJustified` |
| D-166 E.9: a failed migration run exits non-zero and applies nothing after the failing migration | `58f11da4` | OPS-MIG-002 | `MigrationRunTests.OPS_MIG_002_AC1_AFailedRunExitsNonZeroAndAppliesNothingAfterTheFailingMigrationAsync` |
| D-166 D.11, 167: the product name only before a project name; ledger line 167 | `f94cefec` | CONV-NAME-001 | `ProductNameTests.CONV_NAME_001_AC2_ADottedNameThatIsNoProjectIsFound`, `ProductNameTests.CONV_NAME_001_AC2_ALockedPackageIsHeldToTheProjectsInLowerCase` |
| D-166 D.11, 271: the books fixture declared under a neutral resource type | `e2fc8969` | PRIV-SENS-002a | `ProcessingRecordsTests` (the fixture) |
| D-166 D.11, 351: the unreferenced OpenID Connect handler package dropped | `1edd76e0` | CONV-DESIGN-008 | `LibraryStructureTests` (the allowed list) |
| D-166 D.11, 385: the consent refusals read as denials too; ledger line 385 | `8e360625` | CONV-ERR-001 | `LibraryStructureTests.CONV_ERR_001_AC1_NoDenialIsSignalledByAnException` |
| D-166 D.11, 386: the item behind each interface the host implements named | `d7629d29` | CONV-DESIGN-002, LIB-HOST-001 | `PublicSurfaceTests` (`NotServiceContracts`) |
| D-166 D.11, 405: the vendor initialism found as a whole word | `a54e846b` | LIB-EXT-001 | `IntegrationBoundaryTests.LIB_EXT_001_AC3_NoProviderNameAppearsInTheCoreNamespace` |
| D-166 D.11, 416: the grants sought by condition in every reverse read | `a85e1576` | OPS-DB-003 | `VolumeTests.OPS_DB_003_AC2_TheReverseLookupReadsNoTableWhole` |
| D-166, Tier 1 correction (2): the test projects held to the grants the item permits | `1071f4a6` | CONV-LAYOUT-002 | `LibraryStructureTests.CONV_LAYOUT_002_AC1_InternalsAreVisibleOnlyWhereThePermittedGrantsSay` |
| D-166 D.10, 378, and Tier 1 correction (3): pending migrations judged by list, the deploy stopped only on data loss; `destructive-operations.sh` fails where `DESTRUCTIVE_DDL_GATE` is unset, empty, or neither `enabled` nor `disabled`; ledger line 378 | `6482b960` | OPS-DEP-001, OPS-DEP-002 | No test can decide it. Verified by scenario: the gate was run against scratch repositories for each value of the variable |
| D-166 D.10, 393: a fault logged by its frames and those of each inner fault, never by its message; ledger line 393 | `98b1227d` | BFF-ERR-002, INF-BG-001, CONV-LOG-003 | `ErrorTranslationTests.BFF_ERR_002_AC2_AFaultIsLoggedByTheFramesOfItAndItsInnerFaultAndNoMessageAsync`, the INF-BG-001 AC2 test of `BackgroundWorkerTests` (changed) |
| D-166 D.10, 276 (A): a callback refusal other than the rate limit answered 422 | `682e2521` | INT-GEN-003, BFF-MACH-002, BFF-MACH-003, IDN-LIFE-012a | `HostCallbackTests`, `DeliveryReportEndpointTests` and `ProviderEventTests` (changed); `ProviderEventTests.IDN_LIFE_012a_AnEventNothingDeclaredOrReadableVerifiesIsRefusedAsync` delivers to Apple |
| D-166 D.10, 276 (B): a claim settled once carried; an unsettled claim taken over after `integration.callback.claimtimeout` (`PT5M`, floor `PT1M`); `integration.callback.inprogress` 409; migration `SettleCallbackClaims`, which marks the claims standing settled; ledger line 276 | `c25b633a` | BFF-MACH-002, INT-GEN-003, IDN-LIFE-012a | `CallbackStoreTests.BFF_MACH_002_AC3_AnEventIsClaimedOnceAsync`, `CallbackStoreTests.BFF_MACH_002_AC3_AnEventGivenBackIsClaimedAgainAsync`, `CallbackStoreTests.BFF_MACH_002_AC3_AnUnsettledClaimIsTakenOverOnlyAfterFiveMinutesAsync`, `CallbackStoreTests.IDN_LIFE_012a_AProviderEventIsClaimedSettledOnceAsync`, `HostCallbackTests.BFF_MACH_002_AC3_ADeliveryWhoseRouteNeverFinishedIsCarriedAfterFiveMinutesAsync`, `HostCallbackTests.BFF_MACH_002_AC3_ADeliveryMeetingOneInProgressIsNotAcknowledgedAsync` |
| D-166 D.11, 355: a derived case asked once for each derivation, the sample host carrying a second derivation; ledger line 355 | `bf0cf3c6` | LIB-TEST-001 | `ConformanceSuiteTests.LIB_TEST_001_AC2_AWrongDerivedRowIsReportedForEachDerivationAsync` |
| D-166 D.11, 359 and 382 (1): each configuration key held with its type, scope and constraints in `configuration-keys.txt` | `494807eb` | LIB-API-001 | `SettingsCatalogueTests.LIB_API_001_AC2_TheKeysAreTheContract`, in place of `TheKeyNamesAreTheContract` and `TheFamiliesAreTheContract` |
| D-166 D.11, 359 and 382 (2): each code held with the status it answers with in `error-statuses.txt` | `4d1552d8` | LIB-API-001 | `ErrorCodesTests.LIB_API_001_AC2_TheStatusesAreTheContract` |
| D-166 D.11, 359 and 382 (4) for `error-statuses.txt`: the release script judges the status each code answers with | `0f788c41` | LIB-API-001, REF-001, CONV-VCS-003 | No test can decide it. Verified by 11 scenarios in a scratch repository |
| D-166 D.11, 377: the schema scanned for the fingerprint key after every store; ledger line 377 | `59682a0f` | INF-HOST-003 | `FingerprintKeyTests.INF_HOST_003_AC4_NoStoreWritesTheFingerprintKeyAsync`, `FingerprintKeyTests.INF_HOST_003_AC4_EveryStoreThatComputesAFingerprintIsDriven` |
| D-166, Tier 1 correction (1), but for what questions 49 and 53 park: identifiers bound at the edge as their own types (`IParsable<T>` on twelve identifiers of `Janus.Core`, about fifty handlers); a route naming the max UUID as a subject malformed | `fcd21991` | CONV-DESIGN-004, CONV-DESIGN-006 | `SubjectIdTests.CONV_DESIGN_004_AC2_ASubjectIsReadFromTextAndTheMaxUuidIsNot`, `AccountAdministrationEndpointTests.CONV_DESIGN_004_AC2_ARouteNamingTheMaxUuidAsASubjectIsMalformedAsync`, `CanonicalValueTests` (section 3) |
| D-166, Tier 1 correction (1): `MailboxPush.Address` an `EmailAddress`, `HostedMailbox.Address` an `EmailAddress?` (question 52) | `ae35b431` | CONV-DESIGN-004, INT-MAIL-001, INT-MAIL-007 | `JmapMailServerTests` and `InvitationServiceTests` (changed) |
| D-166, Tier 1 correction (1) (c) 3: the self-hosted corpus address read as a `Uri`, the request unchanged | `afc9d0cb` | CONV-DESIGN-004 | `ScreeningTests.INT_PWD_003_AC1_SwitchingToTheSelfHostedCorpusIsConfigurationOnlyAsync` |

- Parked: 135 (question 37), 389 whole (question 48), the exemption rule of CONV-DESIGN-004 AC2 with `BrowserProfileLog` and `Concealment` (question 49), 359 and 382 (3) with the endpoint lines of (4) and the scenarios of (5) (questions 50 and 51), the four string-bound routes (question 53). No ledger line for 382 or 389. The changelog line of 389 (6) waits with 389 (question 47). The repository variable of 378 is the owner's to create (question 54). The last LIB-TEST-001 AC4 probe was built on the working branch after 145 (`782d471a`).
- Merge: conflicts in `AccountAdministrationEndpoints`, `AccountEndpoints`, `ApiStatus`, `OrganizationEndpoints`, `PrivacyEndpoints`, `SessionRevocationEndpoints`, `configuration-keys.txt` and `AccountAdministrationEndpointTests`. The endpoints keep the working branch's session argument (`browser.Required.Id`) with this part's typed route values, the `new OrganizationId(id)` locals dropped and `id` passed on; `ApiStatus` keeps `MailboxTaken` and `CallbackInProgress`; the test file keeps both sides' tests; the contract files and one fixture are recorded in section 3. The model has no pending change. Fast checks after the merge green; the integration suites `Janus.Storage.Tests` 465, `Janus.Hosting.Tests` 269, `Janus.Cli.Tests` 69, `Janus.Conformance.Tests` 11, no failure.
- Fast checks in a worktree fail `ProductNameTests.CONV_NAME_001_AC2_TheProductNameAppearsOnlyInNamespacesIdentifiersAndTheEntryPoint` alone, since a worktree's `.git` is a file whose `gitdir` path names the product; each part's counts above stand with that one failure, and the fast checks after each merge ran in the repository's own checkout, where it passes.

### On `corrections-4`, after the merges

| Instruction | Commit | Items | Tests |
|---|---|---|---|
| D-166 D.3, the message kinds (1): a verification sent with its link is a kind of its own | `a3119f63` | REG-SESS-003 | `IdentifierServiceTests.REG_SESS_003_AnAddedIdentifierIsSentItsCodeAndItsLinkAsync`, `RegistrationServiceTests.REG_SESS_003_ARegistrationMessageCarriesItsCodeAndItsLinkAsync`, `DefaultMessageTemplatesTests.REG_SESS_003_AVerificationLinkRendersItsCodeAndItsLink` |
| D-166 D.3, the message kinds (2): `CredentialSuspended` (23) carries `{link}`, the cancel link; ledger line 123 | `96776b0c` | AUTH-RECOV-007 | `LossReportsTests.AUTH_RECOV_007_TheNoticeRepeatsAcrossTheWindowAsync`, `DefaultMessageTemplatesTests` (the linked kinds), `RecoveryFlowTests` (the template), `VocabularyContractTests` |
| D-166 D.3, the message kinds (3): `OobDeletionNotice` (24), sent after the fulfilment's commit where it started the window | `72940c48` | IDN-LIFE-003 | `PrivacyRequestTests.IDN_LIFE_003_AnErasureThatStartsTheWindowSendsTheOutOfBandNoticeAsync`, `PrivacyRequestTests.IDN_LIFE_003_AnErasureOnAnAccountAlreadyDeletingSendsNoNoticeAsync` |
| D-166 D.6, 223 point (3): a role the deployment does not hold answered 422 `authz.grant.unresolved` naming `roles` | `ebe969ae` | REG-INV-001 | `InvitationServiceTests.REG_INV_001_TheRolesAttachedAskWhatAGrantAsksAsync` (the unknown case), `InvitationEndpointTests.REG_INV_001_ARoleTheDeploymentDoesNotHoldIsUnresolvedAsync` |
| D-166 D.9, 145 and 279 (1): a pushed `redirect_uri` other than the registered one refused `invalid_request` and logged with the correlation identifier; an absent one takes the registered one | `b0c2d735` | AUTH-OIDC-006, API-REDIR-001 | `OidcFlowTests.AUTH_OIDC_006_AC1_APushedRequestNamingAnUnregisteredDestinationIsRefusedAsync`, `OidcFlowTests.API_REDIR_001_AC2_ADestinationContainingTheRegisteredOneIsRefusedAsync`, `OidcFlowTests.API_REDIR_001_AC4_OnlyTheRefusedDestinationIsRecordedAsync`, `ProviderConformanceTests.AUTH_OIDC_006_AC1_APushNamingAnotherDestinationIsRefusedAsync` |
| LIB-TEST-001 AC4, the last probe: a push naming an unregistered destination; a 400 carrying a `request_uri` is a finding of its own | `782d471a` | LIB-TEST-001, AUTH-OIDC-006 | `ProviderProbesTests` (16 probes), `ConformanceSuiteTests.AUTH_OIDC_006_AC1_TheSampleHostsProviderRefusesEveryRetiredFormAsync` |
| D-166 D.9, 145 and 279: a return address `https`, or `http` on `127.0.0.1` or `[::1]` alone; registration refuses as the start does (`model.startup.redirectclient`) | `a6324418` | AUTH-OIDC-006, API-REDIR-001 | `RedirectValidationTests.AUTH_OIDC_006_APlaintextReturnAddressStopsStartupAsync`, `ClientRegistryTests.AUTH_OIDC_006_AReturnAddressStartupWouldRefuseIsNotRegisteredAsync`, `RegisterClientTests.AUTH_OIDC_006_APlaintextReturnAddressIsNotRegisteredAsync` |
| D-166 D.9, 145 and 279: the done step answers the origin of the registered return address | `59a3b8b6` | API-REDIR-002, REG-SESS-008 | `RegistrationServiceTests` (the API-REDIR-002 and REG-SESS-008 tests, changed) |
| D-166 D.9, 145 and 279: a session keeps the client that registered it (`sessions.client`, migration `KeepTheRegisteringClientOnItsSession`) and answers its landing (`SessionDetail.Landing`); ledger lines 145 and 279 | `2adcbafd` | REG-SESS-008, API-REDIR-002 | `RegistrationFlowTests.REG_SESS_008_TheDoneStepReadsItsReturnFromTheSessionAsync`, `RegistrationFlowTests.REG_SESS_008_ASessionThatCapturedNoClientAnswersNoLandingAsync`, `SessionStoreTests.REG_SESS_008_TheSessionKeepsTheClientTheRegistrationCapturedAsync` |
| D-166 D.9, 160 but for `KeysAsync` (question 55): `IOidc.ClaimsAsync` takes an access context and answers the effective identity alone | `d7a913fe` | LIB-API-005 | `OidcServiceTests.LIB_API_005_ClaimsAnswerOnlyTheEffectiveSubjectAsync` |
| D-166 D.9, 286: a withdrawal suspends where no other usable credential, the password included, may begin a sign-in; ledger line 286 | `e15813b2` | IDN-LIFE-012a | `ProviderEventTests.IDN_LIFE_012a_AC2_AWithdrawnIdentityWhoseOtherWayInIsHeldSuspendsTheAccountAsync` |
| D-166 D.9, 394: every provider error answered with its code alone (`error_description` and `error_uri` cleared on the six response events of the endpoints served); ledger line 394 | `42a27a4a` | LIB-API-003 | `OidcFlowTests.LIB_API_003_AC1_NoProviderErrorCarriesADescriptionAsync` |
| D-166 D.8, 304 (1): each job's service refuses a principal of another operation; `BackgroundJob.RunAsync` takes the access context; ledger line 304 | `a940181b` | IDN-PRIN-001, INF-BG-002 | `BackgroundJobsTests.IDN_PRIN_001_AC3_EveryJobRefusesAPrincipalOfAnotherOperationAsync` (section 3) |
| The integration suites put right after `782d471a` and `98eebc5c` (section 2) | `f3fe8dc1`, `99f7140d` | LIB-TEST-001, AUTH-OIDC-006, OPS-SEC-003 | `ConformanceSuiteTests.AUTH_OIDC_006_AC1_AProviderAdmittingWhatItShouldRefuseIsReportedAsync` (21 findings, the destination finding asserted); three cases of `FingerprintKeyRotationTests` |
| D-166 C, X2 checked again: two reads merged since that still fell back now fault (`AuthenticationService.HeldAsync`, `RecoveryService.RaiseAsync`) | `c174d1b9` | OPS-CFG-008, CONV-ERR-001 | The fault tests of each area |
| D-166 C, X1: a recovery approval's alerts raised before its commit | `4ddea326` | AUTH-RECOV-002, OPS-ALERT-001, CONV-DESIGN-002 | `RecoveryServiceTests.CONV_DESIGN_002_AnApprovalWhoseAlertCannotBeWrittenCommitsNothingAsync` |
| D-166 C, X1: `IPrivacyAlerts.RaiseAsync` and `IAccessAlerts.RaiseAsync` return a result; a raise whose row cannot be written fails its caller | `d75e9e7c` | OPS-ALERT-001, OPS-ALERT-005, CONV-DESIGN-002 | `ReadVolumeTests.CONV_DESIGN_002_AnAnomalyThatCannotBeWrittenIsTheAnswerAsync`, `LegalDocumentTests.CONV_DESIGN_002_ARaiseThatCannotBeWrittenIsTheAnswerAsync`, `DeadlineSweepTests.CONV_DESIGN_002_AWarningThatCannotBeWrittenFailsThePassAsync`, `HolidayListWatchTests.CONV_DESIGN_002_ARaiseThatCannotBeWrittenFailsTheWatchAsync` |
| D-166 C, X1: a spent delivery raised in the transaction that records it | `839efb3a` | IDN-LIFE-003a, CONV-DESIGN-002 | `OutboxPublisherTests.CONV_DESIGN_002_AnExhaustionThatCannotBeRaisedCommitsNothingAsync` |
| D-166 C, X1: a consent change whose event row cannot be written fails | `a6a3aede` | PRIV-CONS-008, PRIV-RIGHT-001a, CONV-DESIGN-002 | `ConsentTests.CONV_DESIGN_002_AChangeWhoseEventCannotBeWrittenCommitsNothingAsync` |
| D-166 C, X1: an alert destination change and its event written together | `65f9da83` | OPS-ALERT-004a, CONV-DESIGN-002 | `AlertDestinationChangeTests.CONV_DESIGN_002_TheChangeAndItsEventCommitTogetherAsync` |
| D-166 C, X7, and 119 (7): the enrolment link of an assisted recovery sent under `signin` | `049f31a6` | REG-IDENT-002, AUTH-ABUSE-004 | `RecoveryServiceTests.REG_IDENT_002_AC4_TheEnrolmentLinkIsSentUnderSignInAsync` |
| D-166 C, X3: a recovery code spent under a lock on its set | `8328a0a1` | AUTH-FACT-008, CONV-DESIGN-003 | `RecoveryCodeStoreTests.AUTH_FACT_008_AC1_TwoConcurrentSpendsOfOneCodeSucceedOnceAsync` |
| D-166 C, X4 and X5 with the correction of D-178: an unreadable member named as the request writes it, by the shared reader | `9fcc85ec` | API-CONV-002 | `ApiConventionTests.API_CONV_002_AC4_AnUnreadableMemberIsNamedAsTheRequestWritesIt` |
| D-166 C, X3: a generator's code judged under a lock on its row | `41e3a302` | AUTH-FACT-005, CONV-DESIGN-003 | `AuthenticatorStoreTests.AUTH_FACT_005_AC3_TheSameCodeTwiceAtOnceSucceedsOnceAsync` |
| D-166 C, X3: a security key's counter judged under a lock on its row | `0417bb1d` | AUTH-FACT-014, CONV-DESIGN-003 | `AuthenticatorStoreTests.AUTH_FACT_014_AC3_TwoAssertionsOfOneCounterAtOnceSucceedOnceAsync` |
| D-166 C, X3: only the hash the password verified against is rehashed | `102ba717` | AUTH-PASS-007, CONV-DESIGN-003 | `PasswordStoreTests.AUTH_PASS_007_AC2_ARehashOfAPasswordSetSinceChangesNothingAsync` |
| D-166 C, X3: a trusted browser's failures counted under a lock on its row | `0286c0ee` | AUTH-FACT-015, CONV-DESIGN-003 | `DeviceStoreTests.AUTH_FACT_015_AC6_FailuresAtOnceAreAllCountedAsync` |
| D-166 C, X3 (F5): a held credential restored only where its row still holds | `3344326a` | IDN-LIFE-012a, CONV-DESIGN-003 | `SessionServiceTests.IDN_LIFE_012a_AHoldEndedSinceTheReadIsNotRestoredAsync` |
| D-166 C, X3 (R1, R2): a recovery link spent under a lock on its row | `492a5ede` | AUTH-RECOV-002, AUTH-RECOV-005, CONV-DESIGN-003 | `RecoveryLinkStoreTests.AUTH_RECOV_002_AC1_TwoOpeningsOfOneLinkAtOnceOpenOnceAsync` |
| D-166 C, X3 (R3): an enrolment session ended under a lock on its link | `1d363161` | AUTH-RECOV-002, CONV-DESIGN-003 | `RecoveryLinkStoreTests.AUTH_RECOV_002_AC1_TwoCompletionsOfOneEnrolmentSessionAtOnceCompleteOnceAsync` |
| D-166 C, X3 (R4): the day limits of approvals counted under a hold | `66d2dea9` | AUTH-RECOV-002, CONV-DESIGN-003 | `RecoveryApprovalStoreTests.AUTH_RECOV_002_ApprovalsAtOnceAreCountedOneAfterAnotherAsync` |
| D-166 C, X3 (R5): a challenge completed under a lock on its row | `22730c01` | AUTH-STEP-001, CONV-DESIGN-003 | `ChallengeStoreTests.CONV_DESIGN_003_AC6_TwoCompletionsOfOneSignInAtOnceCompleteOnceAsync` |
| D-166 C, X3 (R6): an age or a code of a registration decided under a lock on the session | `216b93c1` | REG-SESS-003, REG-PROF-002, CONV-DESIGN-003 | `RegistrationSessionStoreTests.REG_SESS_003_AC3_WrongCodesAtOnceAreAllCountedAsync` |
| D-166 C, X3 (C1): an account's transition decided again under a lock on the account | `c4a6233c` | IDN-LIFE-013, IDN-LIFE-003, PRIV-RIGHT-004, CONV-DESIGN-003 | `AccountLifecycleTests.IDN_LIFE_013_ASuspensionCommittedMeanwhileIsNotReversedByALinkAsync`, `AccountDirectoryTests.IDN_LIFE_013_AReactivationAndASuspensionAtOnceLeaveTheAdministratorsAsync` |
| D-166 C, X3 (C2): a restriction, a deletion or a takedown decided under a lock on the account | `2b1ae54a` | IDN-LIFE-003, PRIV-RIGHT-004, CONV-DESIGN-003 | `AccountStatesTests.IDN_LIFE_003_TwoTakedownsAtOnceTakeTheAccountDownOnceAsync` |
| D-166 C, X3 (C3): what an account keeps judged under locks on its credentials | `0d3b91c3` | IDN-LIFE-012, IDN-LIFE-012a, AUTH-STEP-006, CONV-DESIGN-003 | `AuthenticatorStoreTests.IDN_LIFE_012_AC3_TwoUnlinksAtOnceLeaveAWayInAsync` |
| D-166 C, X3 (C4): a suspension for a withdrawal decided under a lock on the account | `e31030fd` | IDN-LIFE-012a, IDN-LIFE-013, CONV-DESIGN-003 | `ProviderEventTests.IDN_LIFE_012a_AC2_ADeletionBegunMeanwhileIsLeftToItAsync` |
| D-166 C, X3 (C5): a provider linked once under a lock on the account | `7733c787` | IDN-LIFE-012, CONV-DESIGN-003 | `AuthenticatorStoreTests.IDN_LIFE_012_TwoLinksOfOneProviderAtOnceLinkOnceAsync` |
| D-166 C, X3 (C6): a loss report ended under locks on the account and the credential | `af4d915b` | AUTH-RECOV-007, AUTH-RECOV-007a, CONV-DESIGN-003 | `LossReportsTests.AUTH_RECOV_007_ACancellationCommittedMeanwhileStandsAsync` |
| D-166 C, X3 (C7): identifiers changed under a lock on the set | `c37dff7d`, `576b22bb` | REG-IDENT-005, REG-IDENT-006, REG-IDENT-009, CONV-DESIGN-003 | `IdentifierServiceTests.REG_IDENT_006_AC1_AnAddressMadePrimaryMeanwhileIsSparedAsync`, `IdentifierStoreTests.REG_IDENT_005_TwoPromotionsAtOnceLeaveOnePrimaryAsync` |
| D-166 C, X3 (C7): a corporate address taken under the identifier lock | `522fdd19` | REG-MAIL-001, REG-MAIL-003, CONV-DESIGN-003 | `InvitationServiceTests.REG_MAIL_001_AnAddressProvedMeanwhileHearsOfTheCorporateAddressAsync` |
| D-166 C, X3 (C8): a code judged under a lock on its verification | `1944081d` | REG-IDENT-004, REG-IDENT-007, CONV-DESIGN-003 | `IdentifierServiceTests.REG_IDENT_007_AC2_AChangeAbandonedMeanwhileIsNotAppliedAsync`, `PendingVerificationStoreTests.REG_IDENT_004_WrongCodesAtOnceAreAllCountedAsync` |
| D-166 C, X3 (O1): an invitation decided under a lock on its row | `f7eb2ba7` | IDN-LIFE-009a, REG-INV-001, CONV-DESIGN-003 | `InvitationServiceTests.REG_INV_001_AnInvitationRevokedMeanwhileAttachesNothingAsync`, `InvitationStoreTests.IDN_LIFE_009a_AC2_TwoPressesAtOnceAttachTheInvitationOnceAsync` |
| D-166 C, X3 (O2): an organization's deletion decided under a lock on its row | `13d5b76e` | IDN-ORG-003, CONV-DESIGN-003 | `InvitationServiceTests.IDN_ORG_003_AC12_ADeletionRequestedMeanwhileTakesNoInvitationAsync`, `OrganizationErasureSweepTests.IDN_ORG_003_AC2_AWindowCancelledMeanwhileIsLeftBeAsync`, `OrganizationStatesTests.IDN_ORG_003_AC2_ACancellationAndTheErasureAtOnceDoNotBothStandAsync` |
| D-166 C, X3 (O3): a membership ended under a lock on its row | `85a3457e` | IDN-MEM-001, CONV-DESIGN-003 | `MembershipEndingTests.IDN_MEM_001_TwoEndsAtOnceEndTheMembershipOnceAsync` |
| D-166 C, X3 (O4, O5): a role or a grant decided under a lock on the role | `ec6680df` | OPS-CFG-007, AUTHZ-GRANT-002, CONV-DESIGN-003 | `GrantEndpointTests.OPS_CFG_007_AC1_ARoleThatCameToAdministerMeanwhileIsNotConferredAsync`, `GrantStoreTests.AUTHZ_GRANT_002_TwoGrantsSayingOneThingAtOnceWriteOneAsync` |
| D-166 C, X3 (O6): group members changed with the organization's groups held | `5548eff0` | AUTHZ-GROUP-001, OPS-CFG-007, CONV-DESIGN-003 | `GrantEndpointTests.AUTHZ_GRANT_001_AGroupRemovedMeanwhileIsGivenNothingAsync`, `GroupEndpointTests.AUTHZ_GROUP_001_ANestingMadeMeanwhileIsJudgedForACycleAsync`, `GroupEndpointTests.AUTHZ_GRANT_003_AC3_AGroupGivenAGrantMeanwhileIsNotRemovedAsync`, `GroupEndpointTests.OPS_CFG_007_AC1_AGroupThatCameToAdministerMeanwhileGainsNoMemberAsync`, `GroupClosureStoreTests.AUTHZ_GROUP_001_TwoNestingsAtOnceCloseNoCycleAsync` |
| D-166 C, X3 (O7): an export counted with its actor's exports held | `5905a80b` | OPS-ALERT-006, CONV-DESIGN-003 | `ExportOperationsTests.OPS_ALERT_006_AnExportAdmittedMeanwhileIsCountedAsync`, `ExportStoreTests.OPS_ALERT_006_ExportsAtOnceAdmitNoMoreThanTheLimitAsync` |
| D-166 C, X3 (O8): a derivation refreshed with the organization's tree held | `0c930b9a` | AUTHZ-DERIVE-005, CONV-DESIGN-003 | `MaterialisationTests.AUTHZ_DERIVE_005_AC1_TwoRefreshesAtOnceWriteTheGrantOnceAsync` |
| D-166 C, X3 (O9): a restriction edited on the set read under the settings row lock | `b621d5f7` | OPS-CFG-002, AUTH-ABUSE-004, CONV-DESIGN-003 | `RestrictionAdministrationTests.OPS_CFG_002_AC6_ARestrictionTightenedMeanwhileStaysTightenedAsync` |
| D-166 C, X3 (V1): a privacy request decided under a lock on its row; the deadline sweep leaves a decided request | `ecfd7be5` | PRIV-RIGHT-001, PRIV-RIGHT-002, CONV-DESIGN-003 | `DeadlineSweepTests.PRIV_RIGHT_002_AC5_ARequestDecidedMeanwhileDoesNotLapseAsync`, `PrivacyRequestTests.PRIV_RIGHT_002_AC5_AnErasureRefusedMeanwhileBeginsNoDeletionAsync`, `PrivacyRequestStoreTests.PRIV_RIGHT_002_AC5_TwoDecisionsAtOnceDecideOnceAsync` |
| D-166 C, X3 (V2): a privacy request queued with the subject's requests of its type held | `9f4b4d88` | PRIV-RIGHT-001, CONV-DESIGN-003 | `PrivacyRequestTests.PRIV_RIGHT_001_AC1_ARequestQueuedMeanwhileMakesADuplicateAsync`, `PrivacyRequestStoreTests.PRIV_RIGHT_001_AC1_TwoRequestsOfATypeAtOnceQueueOneAsync` |
| D-166 C, X3 (V3): a subject's export counted with their exports held | `a4a4d32e` | PRIV-RIGHT-003, CONV-DESIGN-003 | `ExportServiceTests.PRIV_RIGHT_003_AnExportCountedMeanwhileIsCountedAsync`, `ExportLedgerTests.PRIV_RIGHT_003_ExportsAtOnceCountNoMoreThanTheLimitAsync` |
| D-166 C, X3 (V4): an erasure delivery completed under a lock on its row | `e0394974` | IDN-LIFE-003a, CONV-DESIGN-003 | `ErasureServiceTests.IDN_LIFE_003a_AnErasureClosedMeanwhileIsNotClosedTwiceAsync`, `OutboxStoreTests.IDN_LIFE_003a_TwoCompletionsAtOnceCloseTheErasureOnceAsync` |
| D-166 C, X3 (V5): a consent granted or withdrawn with the subject's records held | `1920008e` | PRIV-CONS-008, PRIV-RIGHT-001a, CONV-DESIGN-003 | `ConsentTests.PRIV_CONS_008_AC5_AConsentWithdrawnMeanwhileIsWithdrawnOnceAsync`, `ConsentStoreTests.PRIV_CONS_008_AC5_TwoWithdrawalsAtOnceWithdrawOnceAsync` |
| D-166 C, X3 (V6): a key rotation run with its progress held, each batch from the committed point | `1cc74763` | OPS-SEC-003, CONV-DESIGN-003 | `KeyRotationTests.OPS_SEC_003_AC2_TwoRunsAtOnceStartAndCompleteTheRotationOnceAsync`, `KeyRotationTests.OPS_SEC_003_AC4_TwoSealsAtOnceRetireTheRotationOnceAsync`, `FingerprintRotationTests.OPS_SEC_003_AC6_TwoRunsAtOnceStartAndCompleteTheRotationOnceAsync`, `FingerprintRotationTests.OPS_SEC_003_AC6_TwoSealsAtOnceRetireTheRotationOnceAsync` |
| D-166 C, X3 (S1), the counting half: counted, spent and released on the ledger rows as they stand; a tracked send's reference compared in constant time | `2835ce2f`, `e664d897` | AUTH-ABUSE-004, INT-SMS-005, CONV-DESIGN-003, CONV-CODE-007 | `SendLedgerTests.AUTH_ABUSE_004_AC1_SendsCountedAtOnceAreEachCountedAsync`, `SendLedgerTests.AUTH_ABUSE_004_AC4_SendsSpendingCreditAtOnceSpendEachCreditOnceAsync`, `SendLedgerTests.AUTH_ABUSE_004_AC4_CreditGrantedAtOnceIsAddedTwiceAsync`, `SendLedgerTests.AUTH_ABUSE_004_AC2_ASendReleasedTwiceAtOnceIsReleasedOnceAsync`; the CONV-CODE-007 gate |
| D-166 C, X3 (S2): a failure counted with the scope's counter held | `5cc54446` | AUTH-ABUSE-001, CONV-DESIGN-003 | `ThrottleServiceTests.AUTH_ABUSE_001_AC1_AFailureCountedMeanwhileIsCountedFromAsync`, `ThrottleLedgerTests.AUTH_ABUSE_001_AC1_FailuresAtOnceAreEachCountedAsync` |
| D-166 C, X3 (S3): a notice judged with the address's notices held | `325262a0` | AUTH-ABUSE-003, REG-SESS-005, CONV-DESIGN-003 | `RecoveryServiceTests.AUTH_ABUSE_003_AC4_AnAddressToldMeanwhileIsToldOnceAsync`, `NoticeLedgerTests.AUTH_ABUSE_003_AC4_AsksAtOnceTellTheAddressOnceAsync` |
| D-166 C, X3 (S4): a callback counted with the source's callbacks held | `f026502d` | INT-GEN-003, CONV-DESIGN-003 | `DeliveryReportsTests.INT_GEN_003_ACallbackCountedMeanwhileIsCountedAsync`, `CallbackLedgerTests.INT_GEN_003_CallbacksAtOnceAdmitNoMoreThanTheLimitAsync` |
| D-166 C, X3: a session begun with the account held; a suspended, deleting or deleted account refused `auth.factor.rejected` | `e282fb1c` | IDN-LIFE-013, CONV-DESIGN-003 | `SessionServiceTests.IDN_LIFE_013_AnAccountSuspendedMeanwhileBeginsNoSessionAsync` |
| D-166 C, X4 and X5: `PUT /admin/compliance/licences` takes the list itself as its body; duplicate identifiers name `id` | `9933a94a` | OPS-MAINT-001, API-CONV-002 | `MaintenanceEndpointTests` (changed) |
| D-166 C, X4: grant and group free text bounded at the endpoint | `264fe2bb` | API-CONV-002, CONV-CODE-006 | `GrantEndpointTests.CONV_CODE_006_AC3_AReasonOutsideTheBoundIsRefusedBeforeTheServiceAsync`, `GroupEndpointTests.CONV_CODE_006_AC3_FreeTextOutsideTheBoundIsRefusedBeforeTheServiceAsync` |
| D-166 C, X4: organization and invitation free text bounded | `00b686d0` | API-CONV-002, CONV-CODE-006, REG-MAIL-003 | `OrganizationEndpointTests.CONV_CODE_006_AC3_FreeTextOutsideTheBoundIsRefusedBeforeTheServiceAsync`, `InvitationEndpointTests.CONV_CODE_006_AC3_AReasonOutsideTheBoundIsRefusedBeforeTheServiceAsync` |
| D-166 C, X4: an approval's free text bounded before the step-up | `bab1a3ba` | API-CONV-002, CONV-CODE-006, AUTH-RECOV-002 | `RecoveryServiceTests.CONV_CODE_006_AC3_AnApprovalsFreeTextIsHeldToItsBoundAsync`, `RecoveryFlowTests.CONV_CODE_006_AC3_AnApprovalsFreeTextOutsideTheBoundIsRefusedBeforeTheServiceAsync` |
| D-166 C, X4: a maintenance log entry's note held to its bound | `8467b119` | API-CONV-002, CONV-CODE-006, OPS-MAINT-001 | `MaintenanceRecordsTests.CONV_CODE_006_AC3_ANoteIsHeldToItsBoundAsync`, `MaintenanceEndpointTests.CONV_CODE_006_AC3_ANoteOutsideTheBoundIsRefusedBeforeTheServiceAsync` |
| D-166 C, X4: an entered request's detail held to its bound (section 2) | `e747407f` | API-CONV-002, CONV-CODE-006 | `PrivacyRequestEndpointTests.API_CONV_002_AnEntryWithADetailOutsideTheBoundIsRefusedBeforeTheServiceAsync`, `PrivacyRequestTests.API_CONV_002_AnEntryWithABlankOrOverlongDetailIsMalformedAsync` |
| D-166 C, X5: an erasure fulfilled while the account entered its window under the lock is recorded fulfilled | `0dca93eb` | CONV-DESIGN-003, PRIV-RIGHT-001, IDN-LIFE-003 | `PrivacyRequestTests.PRIV_RIGHT_001_AnAccountDeletingMeanwhileKeepsItsRunningWindowAsync` |
| D-166 C, X5: an own deactivation or deletion the state does not admit answered `identity.account.stateconflict` naming it, a restricted one `authz.restricted`, both before the step-up | `95e0a12a` | IDN-ACCT-007, AUTHZ-GATE-006, IDN-LIFE-013, IDN-LIFE-014 | `AccountLifecycleTests.IDN_ACCT_007_ADeactivationTheStateDoesNotAdmitIsRefusedBeforeTheStepUpAsync`, `AccountLifecycleTests.IDN_ACCT_007_ASuspensionCommittedMeanwhileIsNamedToADeactivationAsync` |
| D-166 C, X5: a takedown refused under the lock answered for the state found there | `5d60664d` | IDN-LIFE-003, CONV-DESIGN-003 | `TakedownServiceTests.IDN_LIFE_003_AStateCommittedMeanwhileIsTheOneAnsweredAsync` |
| D-166 C, X5: a group gone between the scope read and the lock refused as one no row names | `da61ef1c` | AUTHZ-SCOPE-001, CONV-DESIGN-002 | `GroupEndpointTests.CONV_DESIGN_002_AC3_AGroupRemovedMeanwhileIsRefusedAsOneNoRowNamesAsync` |
| D-166 C, X8: the step-up judged after every other refusal (a username, an identifier added or removed, a recovery approval, the end of a membership) | `a9964c20` | REG-IDENT-004, REG-IDENT-006, REG-IDENT-009, AUTH-RECOV-002, IDN-MEM-001 | `AccountServiceTests.REG_IDENT_009_TheStepUpIsJudgedAfterEveryOtherRefusalAsync`, `IdentifierServiceTests.REG_IDENT_004_TheStepUpIsJudgedAfterEveryOtherRefusalAsync`, the channel assertion of the recovery AC3 test |
| D-166 F: the register finding `organisational-measures-missing` spelled as `10` spells it | `402e6a1c` | PRIV-ROPA-001 | `ProcessingRecordsTests` and `ProcessingRecordsEndpointTests` (the PRIV-ROPA-001 AC2 tests) |
| D-166 G: the "Revised by entry" lines no code commit carries (144, 175, 190, 280, 284, 367) | `d9a8ecb5` | none | none |

**145, 279 and the last probe.** The probe of LIB-TEST-001 AC4 for a pushed request naming another destination waited for 145 and 279 (1), since the provider rewrote such a destination before. The machine answers of 394 now carry their headers (test only).

**304 (1) (`a940181b`).** The expiry sweep, which reaches the stores itself, is guarded in `BackgroundJobs`. `SmsBalance` and `LocationDatabase` keep an unguarded private read for their own callers on the request path. The worker passes `AccessContext.Of(job.Principal)`.

**The section C sweeps.**
- X2: two reads merged since the sweep still turned a failure into a value and now fault (`c174d1b9`). `ReadVolume.TodayAsync`, `WorkingCalendar`, `RestoreTest` and the `SignOn` matches were reviewed and left: each passes a checked failure on, or is not a configuration read.
- X1: the services that publish inside the transaction of the fact were reviewed and left, as was `BreakGlassService.LimitReachedAsync`, which raises in its own transaction as D-166 292 orders.
- X3: one lock primitive per kind of row, with a storage test on two connections each; the commits are listed above. Reviewed and left: the signing key changes of D-181 (an insert that does nothing on conflict, a conditional promotion, a lengthening that never lowers, retirement and removal conditional on the stored times); the cancellations that hold their row before the directory write. Residual and failing closed: two definitions of one new role at once meet the primary key; a revocation from a group while a member is added is judged on the grant as read under the group lock; the factor accumulation of `PresentAsync` can lose an update. Residual: a takedown's reported erasure date is computed from the read before the lock; `InvitationAcknowledgement` checks the personal email and the email maximum on the set read before the lock, and a change meanwhile makes the domain throw and the transaction roll back; `InvitationService.IssueAsync` sends its link before its transaction (119, parked), so a refusal under the lock leaves a sent link that opens nothing; a source's callbacks stay held to the end of the request's transaction, which in `ProviderEventIntake` includes the provider key check; `Supersession` writes `superseded_at` without the consent hold.
- X4 and X5: every member named by hand was listed; none carries a `$` root, a list index or a nested path written otherwise. The routes and services already bounded were reviewed and left, as were the refusals `authz.denied` and `api.request.malformed` the chapters settle. The profile names and credential labels have their own bounds and codes, outside X4.
- X6: the domain half needs nothing: the edit refuses a domain the resolver cannot read before the step-up, the start refuses a listed domain without `dnsResolver`, and bootstrap writes no domains. The photos half is parked (question 25).
- X7: the enrolment link of an assisted recovery goes under `signin` (`049f31a6`); the rest is parked (questions 27, 38 and 60).
- X8: the three gates no entry built have their 403 `auth.stepup.required` test (`SessionRevocationEndpointTests.AUTH_SESS_011_OneAccountsRevocationAsksForStepUpAsync`, `SessionRevocationEndpointTests.AUTH_SESS_009_EveryRevocationAsksForStepUpAsync`, `PrivacyRequestEndpointTests.PRIV_RIGHT_001_AC5_AFulfilmentWithoutStepUpIsRefusedAsync`). All 36 gate names of `10` section 5a are spent in `src`. The ungated `/admin` writes are 5a's list of operations not gated, plus `POST /admin/groups`, `DELETE /admin/groups/{id}` and `POST /admin/privacy/requests`; none touches an account or loosens a control. Left: `AppPasswords.RevokeAsync`, `IdentifierService.StageAsync` and the organization policy, whose refusals after the step-up come from the act itself.
- Section F: every new code is an `ErrorCodes` member with the status `09` gives; the renamed codes carry their new names; no retired code remains; the statuses of `identity.registration.incomplete` (409), `identity.identifier.invalid` (422) and `integration.callback.rejected` (429 only with `details.retryAt`, otherwise 422) are as `10` gives them; the new and changed keys, the protected list, the seven step-up actions, `breakglass-generated` and the restriction shape match. The vocabularies of `10` sections 5.1 to 5.49 that a surface carries are spelled as `10` spells them, but for `402e6a1c` and the two parked members (`derivation-driftcheck`, question 22; `registration-channel`, question 23).
- Section G: every "Superseded by D-166" line section G names is present but for the 23 entries whose items are parked (section 2); the "Revised by entry" lines are present.
- Observed, outside the sweeps: the analyser JAN0005 inspects expression statements only, so an expression-bodied member that discards a `Result` through `=> await X()` escapes it.

### On `corrections-4`: the housekeeping before D-183's items, D-184, and question 58

| Item | Commits | Implements | Tests |
|---|---|---|---|
| Question 43: the messages of two commits reworded (`86e23f9e` is now `863883c1`, type `test`; `82a3ae2b` is now `c25b633a`, its body line 70 characters). 104 commits took new hashes, 210 kept theirs; every tree, parent shape, author and date compared equal; the commit-message check passes from `b6d14fe` | `c1fa62e2` (the hashes in this report) | CONV-VCS-003 | `.github/gates/commit-message.sh b6d14fe HEAD` |
| The SDK at 10.0.401; `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Design` and `dotnet-ef` at 10.0.12; `Dapper` 2.1.89; `StackExchange.Redis` 3.3.1; `Fido2` 4.2.0. No package has a later major version. `Microsoft.CodeAnalysis.CSharp` and `Microsoft.CodeAnalysis.Analyzers` stay at 5.6.0 (latest stable 5.9.0) | `a19591e4`, `3cb3fe5a`, `dff389e5`, `c91ec834`, `d2830b22` | CONV-SETUP-001, CONV-SETUP-002, CONV-DEP-004 | The locked restore, the build, the formatter and the unit tests under the new SDK flagged nothing |
| D-184: `Janus.Storage` references `Microsoft.EntityFrameworkCore.Relational` at the version of `Microsoft.EntityFrameworkCore`; the SDK and the one version held by tests | `ff057e7b`, `d2b9ffac` | CONV-DESIGN-008, CONV-SETUP-001, CONV-DEP-003 | `LibraryStructureTests.CONV_DESIGN_008_AC3_TheRelationalAccessPackagesAndTheirToolCarryOneVersion`, `LibraryStructureTests.CONV_SETUP_001_AC3_TheSdkIsOfTheTargetedReleaseAndRollsForwardByPatch`, `LibraryStructureTests.CONV_DESIGN_008_AC1_ThePackageSetIsExactlyTheAllowList` |
| D-183 question 58, the port: `IUnitOfWork.RollbackAsync`; an inner rollback marks the whole unit of work; a commit after a mark, and a commit that fails, leave it rolled back. JAN0004 exempts the rollback and a member implementing an interface that is not the library's own, and reports one implementing the library's own without a token; `SettingChange`, `SettingNaming` and `SettingReading` pass their token to the asynchronous method | `83819e32` | CONV-DESIGN-003, CONV-DESIGN-005, CONV-CODE-002, CONV-CODE-008 | `UnitOfWorkTests.CONV_DESIGN_003_AC5_ARefusedOperationLeavesNothingForTheNextCommitAsync`, `UnitOfWorkTests.CONV_DESIGN_003_AC8_AnOuterCommitAfterAnInnerRollbackCommitsNothingAndFaultsAsync`, `UnitOfWorkTests.CONV_DESIGN_003_AC8_AnOuterRollbackAfterAnInnerRollbackEndsTheUnitOfWorkAsync`, `BlockingAndCancellationAnalyzerTests.CONV_CODE_008_AC1_SilentOnAFrameworkInterfacesMemberAndOnTheRollbackAsync`, `BlockingAndCancellationAnalyzerTests.CONV_CODE_008_AC1_ReportedOnAnOwnInterfacesMemberWithoutACancellationTokenAsync`, `ResultContractTests.CONV_DESIGN_005_AC1_EveryContractMethodReturnsAnOutcome` |

CONV-DESIGN-008 criterion 3, "no project's build reports MSB3277": no test decides it; the build of `ff057e7b` and of every commit since reports none, and the test above holds every lockfile to the one version, which is what the warning came from.

What earlier runs left: nine worktrees held uncommitted work whose commits were all in the branch; each is kept as a patch outside the repository and removed with its branch. The reason rule of the configuration route that one of them carried is on the branch at the endpoint and in the service (`ConfigurationEndpointTests.OPS_CFG_005_EveryChangeCarriesAReasonAsync`, `ConfigurationAdministrationTests.OPS_CFG_008_AC2_ATighteningWithNoReasonIsRefusedAsync`). Four stashes are dropped: three held earlier states of the docs, and every file of the fourth is in the history (the changelog describes `MapAuthorizationTables`).

**State.** The X9 sweep of question 58 ran in four parts and question 57 in a fifth; all five are merged (below). The five parts of D-183 the owner's split names are merged (below), and four further parts for what they left: question 30, question 29, the mailbox pushes of question 61, and questions 42 and 31. Questions 68 to 125 park the sites they name. Of the work after the merges, the new codes, the retirement of `NotificationRequested`, question 53 and question 62 are merged; questions 50 and 51 wait on question 119; the sites of question 62 in the identifiers and the registration run in a part not yet merged.

### The section C sweeps, place by place

For each sweep: each place changed, each place reviewed and left with the reason, and what is parked (D-166 section C).

#### X2

- Changed: AuthenticationService.HeldAsync (PolicyEnforcementGrace read returned null on
  failure); RecoveryService.RaiseAsync (two threshold reads returned success on failure).
  Tests: the existing fault tests of each area cover the throw idiom.
- Reviewed, left: ReadVolume.TodayAsync (zone refusal is returned, not a fallback);
  WorkingCalendar, RestoreTest, SignOn matches (after a checked failure, or not a
  configuration read).

#### X1

- Changed: RecoveryService.StandAsync (RecoveryClustering and ApproverVolume raised
  before the commit), test RecoveryServiceTests.CONV_DESIGN_002_AnApprovalWhoseAlertCannotBeWrittenCommitsNothingAsync.
- Changed: IPrivacyAlerts.RaiseAsync and IAccessAlerts.RaiseAsync return Result; the
  callers LegalDocumentService.PublishAsync, HolidayListWatch, DeadlineSweep.RaisedAsync,
  DenialSpikes.WatchAsync and ReadVolume.ReturnedAsync fail on a raise that cannot be
  written. Tests: HolidayListWatchTests, DeadlineSweepTests, LegalDocumentTests and
  ReadVolumeTests, each CONV_DESIGN_002_*.
- Changed: OutboxPublisher.DeliveredAsync (ErasureDeliveryExhausted raised before the
  commit that records the spent delivery), test
  OutboxPublisherTests.CONV_DESIGN_002_AnExhaustionThatCannotBeRaisedCommitsNothingAsync.
- Changed: ConsentService.AnnouncedAsync and ObjectedAsync (a refused event write failed
  nothing), test ConsentTests.CONV_DESIGN_002_AChangeWhoseEventCannotBeWrittenCommitsNothingAsync.
- Changed: AlertDestinationChange.ChangeAsync (AlertRaised published after the setting's
  own commit; now one transaction), test
  AlertDestinationChangeTests.CONV_DESIGN_002_TheChangeAndItsEventCommitTogetherAsync.
- Reviewed, left (event in the fact's transaction): AccountAdministration,
  AccountLifecycle, AlertChannels, DeploymentBootstrap.JoinAsync, CredentialService,
  ProviderEvents, DeviceService, IdentifierService, InvitationAcknowledgement,
  MembershipEnd, PasswordService, LossReports, RegistrationService,
  RestrictionAdministration, Supersession, OrganizationErasureSweep, TakedownService,
  ProtectedConfiguration, OrganizationDomainService, CallbackAdmission,
  ConcurrentSessions (inside their callers' transactions).
- Reviewed, left: BreakGlassService.LimitReachedAsync raises in its own transaction, as
  D-166 292 orders.
- Parked: SendingService.CarryAsync publishes NotificationRequested after the send's
  commit (118, 119 (1): question 27, question 38).
- Parked: DenialSpikes.WatchAsync raise joins the caller's transaction while 321 commits
  the denial outside it (question 59).

#### X7

- Changed: admin-assisted recovery enrolment link sent under the sign-in purpose
  (049f31a6, 119 (7)).
- Parked: identifier-change-confirm purpose (question 60); every path that needs the
  after-commit registration of 119 (1) (question 27, question 38).

#### X3 (one lock primitive per row kind, a two-connection storage test each)

- 8328a0a1 RecoveryCodeService spend; 41e3a302 TOTP code; 0417bb1d security key counter;
  102ba717 password rehash; 0286c0ee trusted browser failures.
- 3344326a F5 SessionService.RestoreAsync, FindForUpdateAsync per held credential.
- 492a5ede R1/R2 RecoveryService.CompleteAsync, BeginEnrolmentAsync
  (IRecoveryLinkStore.FindForUpdateAsync by fingerprint).
- 1d363161 R3 CredentialService CompleteKey, ConfirmGenerator, SetPassword (link lock by
  enrolment session).
- 66d2dea9 R4 RecoveryService.StandAsync (advisory lock on the approval counts).
- 22730c01 R5 AuthenticationService.CompleteAsync, RaiseAsync (challenge row).
- 216b93c1 R6 RegistrationService.RecordAgeAsync, VerifyAsync (session row).
- c4a6233c C1 AccountAdministration and AccountLifecycle transitions; RecoveryService
  holds the account (IAccountDirectory.HoldAsync).
- 2b1ae54a C2 AccountStates.RestrictAsync, BeginDeletionAsync, TakeDownAsync.
- 0d3b91c3 C3 CredentialService.RemoveAsync, UnlinkAsync; ProviderEvents.WithdrawnAsync
  (IAuthenticatorStore.OfForUpdateAsync).
- e31030fd C4 ProviderEvents.SuspendedAsync. 7733c787 C5 CredentialService.LinkAsync.
- af4d915b C6 LossReports.InvalidateAsync, CancelAsync; enrolment holds the account.
- c37dff7d C7 IdentifierService Add, MakePrimary, SetBackup, Remove, Undo;
  AccountService.ChooseAsync (IIdentifierStore.HoldAsync: account row, tracked set
  reloaded). 522fdd19 InvitationAcknowledgement.CorporateAsync, MembershipEnd.RetiredAsync.
- 1944081d C8 IdentifierService.ProvedAsync, LandAsync (verification row).
- f7eb2ba7 O1 InvitationOpening.OpenAsync, InvitationService.RevokeAsync,
  InvitationAcknowledgement.AcknowledgeAsync, RegistrationService open and Registered.
- 13d5b76e O2 OrganizationService request and cancel, OrganizationStates.EraseAsync
  (nothing where the window no longer runs), InvitationService.IssueAsync,
  acknowledgement (organization row).
- 85a3457e O3 MembershipEnding.EndAsync, MembershipAttachment, organization erasure
  (membership rows).
- ec6680df O4/O5 GrantService grant and revoke, RoleService define and remove,
  InvitationService.IssueAsync roles (role row, sorted by name; grant row after it).
- 5548eff0 O6 GroupService add member, remove member, remove; GrantService for a group
  holder; role rows in the administering check (per-organization advisory lock on the
  groups, taken before any role row).
- 5905a80b O7 ExportOperations.AdmitAsync (per-actor advisory lock; the admission now
  runs in a unit of work, nested in the caller's where one is open).
- 0c930b9a O8 DerivationMaterialiser.RefreshAsync (ResourceStore's tree lock; nested
  unit of work; holders read after the lock).
- b621d5f7 O9 RestrictionAdministration.EditAsync (set re-read and rebuilt under the
  settings row lock). The lock primitive is the existing settings row lock with its own
  storage test (178); the new test is the service one.
- ecfd7be5 V1 PrivacyRequestService fulfil and refuse, DeadlineSweep.ReachedAsync
  (request row; the sweep re-reads under the lock and leaves a decided request).
- 9f4b4d88 V2 PrivacyRequestService.QueuedAsync (advisory lock per subject and type).
- a4a4d32e V3 ExportService.AssembleAsync (advisory lock per subject; window counted again).
- e0394974 V4 ErasureService.CompleteAsync (outbox row; confirmations reloaded).
- 1920008e V5 ConsentService grant, withdraw, withdraw objection (advisory lock per
  subject; tracked consents and objections reloaded).
- 1cc74763 V6 KeyRotation and FingerprintKeyRotation start, pass, sweep, completion and
  retirement (advisory lock per kind; tracked progress reloaded; each batch starts from
  the committed point). Tests in Cli KeyRotationTests and Storage FingerprintRotationTests.
- 2835ce2f S1 SendLedger counting half: counter upsert in one statement, credit spent under
  the grant row's lock, grant added in SQL, release under the send row's lock; reads
  untracked. e664d897 tracked reference compared in constant time (CONV-CODE-007 gate).
  Tests SendLedgerTests AUTH_ABUSE_004_* (four, two connections each).
- 5cc54446 S2 ThrottleService.FailedAsync and SucceededAsync (IThrottleLedger.HoldAsync,
  advisory lock per scope and current hash; tracked counters reloaded). Storage test
  ThrottleLedgerTests (new).
- 325262a0 S3 NonExistenceNotice, IdentifierService.TellHolderAsync,
  RegistrationService.TellHolderAsync (INoticeLedger.HoldAsync per address). Storage test
  NoticeLedgerTests (new).
- f026502d S4 CallbackAdmission.AdmitAsync and RejectAsync (ICallbackLedger.HoldAsync per
  source). Storage test CallbackLedgerTests (new).
- e282fb1c A-wide SessionService.BeginAsync holds the account and refuses
  auth.factor.rejected for a suspended, deleting or deleted account (the sign-in's answer);
  the break-glass path (Admission.Exempt) is left: it judges no account state of its own.
  The lock primitive is the account row lock with its storage test (C1); the new test is
  the service one.
- Reviewed, left (D-181 and the conditional write): SigningKeyStore.AddAsync (ON CONFLICT,
  one next and one current by unique partial indexes), PromoteAsync (conditional on the
  key still current with the lifetime read, the next still next), LengthenAsync (never
  lowers, only while current), RetireAsync and RemoveAsync (conditional on the stored
  times); SigningKeys.ChangeAsync runs in a unit of work of its own. Its return without a
  commit where another process made the change is X9 (question 58).
- Reviewed, left: AccountAdministration, AccountLifecycle and OrganizationService
  CancelDeletionAsync hold the row before the directory write (C1, O2).
- Reviewed, left: registration wizard steps other than age and code send codes inside the
  transaction (119, parked); a double completion is stopped by the accounts key.
  Recovery approvals at quorum may send two links; ReplaceAsync keeps one.
  PresentAsync's factor accumulation can lose an update and fails closed.
  ProviderEvents.EndedAsync holds on a stale read, harmless.
- Residual: two definitions of a new role at once meet the primary key and the second
  fails closed. A grant revoked from a group while a member is added is judged on the
  grant as read under the group lock (revocation takes no group lock; it only narrows).
- Residual: the takedown's reported erasure date is computed from the standing read before
  the lock. InvitationAcknowledgement checks the personal email and the email maximum on
  the set read before the lock; a change meanwhile makes the domain throw and the
  transaction roll back (fails closed). InvitationService.IssueAsync sends its link before
  the transaction (119, parked), so a refusal under the lock leaves a sent link that opens
  nothing.
- Parked: C9, the restriction read before the transaction (question 62). V7 and S6 (question 61).
- Residual (S1): the admission half (a send judged on counters read outside any
  transaction, counted after the transport took it) is parked on question 63.
- Residual (S4): the source's callbacks stay held to the end of the request's
  transaction, which in ProviderEventIntake includes the provider key check.
- Parked: S5, the alert destination change notified before its lock (question 64).
- Residual (V5): Supersession writes SupersededAt without the consent hold; ObjectAsync
  takes no hold (it decides on nothing it reads).

#### X4 and X5 (re-check with the details.member correction, 9fcc85ec)

details.member: the shared reader (MalformedRequest.Member, 9fcc85ec) names a member as the
request writes it; every hand-named member was listed (grep of Malformed/Invalid/"member")
and none carries a `$` root, a list index or a nested path written otherwise.

- 9933a94a PUT /admin/compliance/licences: the body is the list itself (09 8a, D-178),
  LicencesBody removed, LicenceBody.Read; duplicate identifiers name `id`
  (MaintenanceRecords.ReplaceLicencesAsync). Test MaintenanceEndpointTests.
- 264fe2bb GrantEndpoints.GrantAsync, RevokeAsync (blank reason trimmed to its code, past
  1024 malformed); GroupEndpoints.CreateAsync (name, reason), RemoveAsync, AddMemberAsync,
  RemoveMemberAsync. Tests CONV_CODE_006_AC3_* (caller without the permission).
- 00b686d0 OrganizationEndpoints.CreateAsync (name, reason), RequestDeletionAsync,
  CancelDeletionAsync; InvitationBody.Read (reason only with formerMailbox, bounded).
- bab1a3ba RecoveryEndpoints.ApproveAsync (reason, channelUsed); RecoveryService.ApproveAsync
  (bound for in-process callers, judged before the step-up).
- 8467b119 MaintenanceEndpoints.RecordAsync and MaintenanceRecords.RecordAsync (note
  optional, bounded when given, kept trimmed); IMaintenanceRecords doc (invalid, not
  malformed, for performedAt).
- e747407f PrivacyEndpoints.EnterAsync and PrivacyRequestService.EnterAsync (detail
  optional, bounded when given, kept trimmed).
- 0dca93eb PrivacyRequestService.DoneAsync: an erasure fulfilled while the account entered
  its window under the lock is recorded fulfilled (09 8a), not 409 without details.state.
- 95e0a12a AccountLifecycle.DeactivateAsync, DeleteAsync: a state that does not admit the
  operation is identity.account.stateconflict naming it (IDN-ACCT-007, D-166), a restricted
  deactivation is authz.restricted from the gate (AUTHZ-GATE-006 AC2), both before the
  step-up; AccountAdministration.StateConflict made internal and shared.
- 5d60664d TakedownService.ExecuteAsync: refused under the lock, answered for the state
  found there (takedown.active, stateconflict); the reserved account stays authz.denied
  (OPS-BOOT-002).
- da61ef1c GroupService: a group gone between the scope read and the lock is refused as one
  no row names (09 section 8, authz.denied through the gate), not 400 `id`.
- Reviewed, left: ConfigurationEndpoints, RoleEndpoints, BreakGlassEndpoints,
  TakedownEndpoints, PrivacyEndpoints submit and refuse, OrganizationEndpoints policy and
  domain routes (Unexplained), RestrictionEndpoints grant (already bounded); restriction
  edit and delete (a blank reason goes to the service, which alone knows whether the
  change loosens); services GrantService, GroupService, RoleService, OrganizationService,
  OrganizationDomainService, InvitationService, BreakGlassService,
  ConfigurationAdministration, RestrictionAdministration, PrivacyRequestService,
  TakedownService (already bounded).
- Reviewed, left (authz.denied settled): DeploymentBootstrap on a stood-up deployment
  (OPS-BOOT-001 AC1); InvitationService into an organization whose deletion was requested
  (09 8a); reserved-account refusals in GrantService, GroupService, AccountAdministration
  (OPS-BOOT-002); maintenance-credential refusals (no person acts); AccountService.ReadAsync
  and every `Acting`/`Effective is not SubjectId` refusal (no person acts).
- Reviewed, left (malformed settled): corporateEmail where the mail is not integrated (09
  8a), emailDomains through the policy (10 4.1a), an unreadable domain, documents absent or
  blank.
- Profile displayName and legalName have their own bound and code (09 PUT
  /account/profile); credential labels theirs (auth.credential.labelinvalid); not X4.
- Parked: dataOwner and organisationalSecurityMeasures of PUT /admin/compliance/assessments
  (question 65).
- Residual: AccountLifecycle under the lock answers a restriction committed since the
  gate's read with authz.denied, as before (the C9 window, question 62).
- Residual: TakedownService and the other returns after BeginAsync without a commit are
  X9 (question 58).

#### X6 (photos and imageCodec, the domain lock and dnsResolver)

- Domain half reviewed, nothing owed: the edit refuses a domain the resolver cannot
  read before the step-up (OrganizationDomainService), startup refuses a listed domain
  without `dnsResolver` (DeclarationCoverage), bootstrap writes EmailDomains null. Tests
  OrganizationDomainEndpointTests, StartupValidationTests.
- Photos half parked on question 25: the code still carries the `photo.enabled` family where
  the spec has the policy `photos` field; the startup and edit checks follow from it.

#### X8 (step-up after every other refusal; 09 8 and 8a against 10 5a)

- a9964c20 AccountService username (after cooling-off and taken), IdentifierService add
  (after invalid, mixedscript, maximum) and remove (after unremovable), RecoveryService
  approve (after the channel check), MembershipEnd (standing read before the step-up,
  ScopeOfAsync kept first for CONV-DESIGN-002 AC3). Tests
  REG_IDENT_009_TheStepUpIsJudgedAfterEveryOtherRefusalAsync,
  REG_IDENT_004_TheStepUpIsJudgedAfterEveryOtherRefusalAsync, the channel assertion in
  the recovery AC3 test.
- The three gates have their 403 auth.stepup.required test:
  SessionRevocationEndpointTests AUTH_SESS_011_OneAccountsRevocationAsksForStepUpAsync
  (account:sessionsrevoke), AUTH_SESS_009_EveryRevocationAsksForStepUpAsync
  (session:revokeall), PrivacyRequestEndpointTests
  PRIV_RIGHT_001_AC5_AFulfilmentWithoutStepUpIsRefusedAsync (privacyrequest:fulfil).
- 10 5a against 09 8 and 8a: all 36 gate names are spent in src (StepUpAction members,
  each used at its route; roles under grant:manage in RoleService). The ungated /admin
  writes are 5a's Not gated list plus POST /admin/groups, DELETE /admin/groups/{id}
  (a group nothing names, 409 authz.group.inuse otherwise) and POST
  /admin/privacy/requests (entry; the fulfil is gated): none touches an account or
  loosens a control, so none is owed a gate.
- Residual, left: AppPasswords.RevokeAsync (the server's refusal comes from the act),
  IdentifierService.StageAsync (a send refused inside the transaction),
  OrganizationService policy (write and alert failures after the step-up); none is a
  refusal of the request that could be judged first.
- Defect: a9964c20's footer line is 84 characters, over the 72 of CONV-VCS-003; it cannot
  be fixed without rewriting history.

#### Section F (chapter 10 rows against the code)

- Codes new, renamed, retired: every new code is an ErrorCodes member with the status 09
  gives it (ApiStatus); the two renamed codes carry the new names; no retired code or
  member remains (SignInStatus.DeviceVerificationRequired is the sign-in status of 5.33,
  not the retired code). Statuses corrected: identity.registration.incomplete 409,
  identity.identifier.invalid 422, integration.callback.rejected 429 only with
  details.retryAt, else 422 (ApiStatus). Meanings widened: covered by X4, X5, X6 above.
- Keys new and changed: code.signin.lifetime, code.signin.attempts,
  integration.mailserver.endpoint (P), integration.callback.claimtimeout,
  outbox.poll.interval ceiling, backup.restoretest.interval as the rows say. Retired keys
  absent. abuse.source.sitelimit: parked on question 48 (389 whole). The policy `photos` field and
  the photo.enabled family: parked on question 25.
- Protected list: SettingsCatalogueTests holds it against 4.8 (integration.mailserver.endpoint
  protected).
- Step-up actions new: all seven are StepUpAction members spent at their routes (X8).
- Alert condition breakglass-generated: High, past alerting.owner.enabled (Alerts,
  AlertRouter), raised by BreakGlassService.
- Restriction shape: channel sms, email, any (default any); the shipped restrictions carry
  sms, sms, email, any as section 4.5 gives them.
- Vocabularies (compared in both directions): every member of
  5.1 to 5.49 that a surface carries is spelled as 10 spells it, except:
  - 402e6a1c RegisterFinding `organisational-measures-missing` spelled
    `organizational-measures-missing` (member OrganizationalMeasuresMissing). Tests
    ProcessingRecordsTests and ProcessingRecordsEndpointTests PRIV_ROPA_001_AC2_*.
  - System principal `derivation-driftcheck`: parked on question 22 (265).
  - Degradation component `registration-channel`: parked on question 23 (143).
- Reviewed, left: 5.8 data subject rights is no wire vocabulary (no surface names a right);
  LifecycleLinkKind's stored `deletion-cancellation` is a row value, the link kind on the
  wire is LinkKind `deletion-cancel`; StepUpOutcome, RecoveryPurpose, BackupChoice,
  SubjectEventKind are spelled by their own chapters, not section 5. The assessments body
  member `organisationalSecurityMeasures` is named by no chapter; left with question 65.

#### Section G (the ledger)

- d9a8ecb5: the "Revised by entry" lines G owes and no code commit carries: 144 (315), 175
  (194 and 205), 190 (411), 280 (356), 284 (318), 367 (400, beside 402). 283 (343 and 349)
  was present as "Revised by entries 343 and 349".
- Every "Superseded by D-166" line G names is present except these, each waiting on its
  parked item: 115 (question 31), 118 (question 27), 119 (question 38), 129 (question 42), 133 and 147 (question 29, question 30), 136
  (question 34), 143 (question 23), 144 (question 25), 152 (question 40), 227 and 322 (with 119, question 38), 235 and 335 (question 27),
  246 (with 242 (3) and (4), question 41, question 36), 265 (question 22), 306 (question 31), 317 (question 26), 318 (question 33), 328
  (question 46), 382 (question 50, question 51), 389 (question 48), 419 (question 32).
- The lines already present stand: 139, 151, 262, 186, 328 (399), 367 (402), 400, 402.

#### X9, `part/rollback-sessions`, merged as `738ef362` (`1b9a5e9b`, `e3f97ab2`, `141a473f`, `fb1e3ee0`)

- `SessionService.ResolveAsync`, `RestoreAsync`, `DeriveAsync` and the private `BeginAsync`: the return where the concurrent-sessions condition is not raised left the unit of work open; rolled back. Tests `SessionServiceTests.CONV_DESIGN_003_AC5_AUseWhoseConditionIsNotRaisedIsRolledBackAsync`, `..._ARestorationWhoseConditionIsNotRaisedIsRolledBackAsync`, `..._ADerivationWhoseConditionIsNotRaisedIsRolledBackAsync`, `..._ABeginningWhoseConditionIsNotRaisedIsRolledBackAsync`.
- `SessionService`, the private `BeginAsync`: `auth.factor.rejected` for an account found suspended, deleting or deleted under its lock ended with a commit; rolled back. Test `IDN_LIFE_013_AnAccountSuspendedMeanwhileBeginsNoSessionAsync`.
- Reviewed, left: `SessionService.PresentAsync`, `RotateAsync`, `EndAsync`, `RevokeEveryAsync`, `EndAccountAsync` and the seven sites of `PreAuthenticationService` (no return between the beginning and the commit); `ConcurrentSessions` begins none.
- `AuthenticationService.RaiseAsync`: the challenge gone under its lock and a failure of the presentation ended with a commit; rolled back. Test `CONV_DESIGN_003_AC5_AStepUpWhoseChallengeIsGoneIsRolledBackAsync`.
- `AuthenticationService`, the private `CompleteAsync`: the challenge gone under its lock and every failure of the issue ended with a commit; rolled back, so a session begun before device trust failed is no longer saved. The refusals inside the issue stay after the challenge's lock. Tests `CONV_DESIGN_003_AC5_ASignInCompletedMeanwhileIsRolledBackAsync`, `AUTH_FACT_017_AC1_WithNoGraceARaiseHoldsTheSignInAtEnrolmentAsync`, `CONV_DESIGN_003_AC8_ASessionRefusedInsideACompletionRollsTheCompletionBackAsync`.
- Reviewed, left: `AuthenticationService.BeginAsync` and the units of `PresentAsync` and `LandAsync` (no return between); `CountedAsync` and `StepUpRefusedAsync` keep the record of a failed authentication (CONV-LOG-005), begin the outermost unit of work and are called before any other begins.
- `SignInLinks.SpendCodeAsync`: `auth.code.expired` for a code gone or lapsed under its lock ended with a commit; rolled back. A wrong try keeps its count and commits. Tests `CONV_DESIGN_003_AC5_ASignInCodeGoneUnderItsLockIsRolledBackAsync`, `CONV_DESIGN_003_AC5_AWrongSignInCodeKeepsItsCountAsync`. The try that reaches the limit is question 71.
- Reviewed, left: `SignInLinks.SendSecondStepAsync`, `AbandonAsync` and the private `IssueAsync` (no return between).
- `NonExistenceNotice`: a refused send of the notice and a probe alert not raised returned with the unit open; rolled back. Tests `AUTH_ABUSE_002_AC3_TheNoticeAnswersToTheRestrictionsOfTheAskAsync`, `CONV_DESIGN_003_AC5_ANoticeWhoseAlertIsNotRaisedIsRolledBackAsync`.
- `RestrictionAdministration.EditAsync`: the set unreadable under its lock ended with a commit; the refused configuration change, the event not written and the alert not raised returned with the unit open; all rolled back. `GrantAsync`: the event not written and the alert not raised; rolled back. Tests `CONV_DESIGN_003_AC5_AnEditWhoseEventIsNotWrittenIsRolledBackAsync`, `CONV_DESIGN_003_AC5_AGrantWhoseEventIsNotWrittenIsRolledBackAsync`.
- `SmsBalance.PollAsync`: the alert not raised; rolled back. Test `CONV_DESIGN_003_AC5_APollWhoseAlertIsNotRaisedIsRolledBackAsync`.
- `ThrottleService.FailedAsync`: the alert not raised returned with the unit open; rolled back. The counted failure commits (AUTH-ABUSE-001). Tests `CONV_DESIGN_003_AC5_AFailureWhoseAlertIsNotRaisedIsRolledBackAsync`, `CONV_DESIGN_003_AC5_ACountedFailureIsCommittedAsync`.
- Reviewed, left: `ThrottleService.SucceededAsync`, `PhoneSignals.AllowsAsync` and `ConsiderAsync` (no return between).
- `SigningKeys.ChangeAsync`: where another process made the change first it answered success with the unit left open; rolled back, then success. Test `CONV_DESIGN_003_AC5_AChangeAnotherProcessMadeFirstIsRolledBackAsync`.
- Reviewed, left: `SigningKeys.LengthenAsync`, `ClientRegistry.RegisterAsync`, `OidcService.ReuseAsync` (no return between).
- Parked: `DeliveryReports.ReportAsync` (question 69), `BotDefence` (question 70), the limit of `SignInLinks.SpendCodeAsync` (question 71), `RegisteredSecrets.RotatedAsync` (question 72).

#### X9, `part/rollback-factors`, merged as `c0c6d6bc` (`6a91c392`, `297df282`, `19efea40`, `168f4d10`, `853d036b`)

The rollback is written inline at each return. `CredentialService.RefusedAsync`, which committed, is removed.

- `TotpService.PresentAsync`: a code refused on the row under its lock ended with a commit; rolled back. Test `TotpServiceTests.CONV_DESIGN_003_AC5_ACodeSpentMeanwhileRollsBackAsync`. Reviewed, left: `BeginAsync`, `ConfirmAsync`, `AbandonAsync` (every refusal before the beginning).
- `RecoveryCodeService.SpendAsync`: no set, or a code not of the set or spent, ended with a commit; rolled back. Test `AUTH_FACT_008_AC1_AUsedCodeIsRejectedOnSecondPresentationAsync`. Reviewed, left: `GenerateAsync`, `ShownAsync`.
- `WebAuthnService.PresentAsync`: a credential gone or unusable under its lock ended with a commit; rolled back. Test `WebAuthnServiceTests.CONV_DESIGN_003_AC5_ACredentialInvalidatedMeanwhileRollsBackAsync`. The counter mismatch is question 74; `AUTH_FACT_014_AC3_ACounterMovingBackwardsIsRejectedAndAuditedAsync` holds what it does today. Reviewed, left: `CompleteAsync`, `UpgradeAsync`.
- Reviewed, left: `VerificationCodes.IssueAsync`; `DeviceService.FailedAsync`, `RemoveAsync`, `RevokeTrustAsync`, `KnownAsync` (refusals before the beginning); `StepUpGuard` begins none.
- `PasswordService.SetAsync`: the event not written returned with the unit open; rolled back. Test `PasswordServiceTests.CONV_DESIGN_003_AC5_APasswordThatCannotBeAnnouncedRollsBackAsync`. Reviewed, left: `VerifyAsync`.
- `LossReports.SuspendAsync`, `CancelAsync` and `InvalidateAsync`: the announcement not written returned with the unit open; rolled back. `CancelAsync`: a report no longer running under the credential's lock ended with a commit; rolled back. Tests `LossReportsTests.CONV_DESIGN_003_AC5_ASuspensionThatCannotBeAnnouncedRollsBackAsync`, `CONV_DESIGN_003_AC5_ACancellationOfAReportEndedMeanwhileRollsBackAsync`, `CONV_DESIGN_003_AC5_ACancellationThatCannotBeAnnouncedRollsBackAsync`, `AUTH_RECOV_007_ASweepWhoseAnnouncementIsRefusedAnswersWithTheRefusalAsync`. Reviewed, left: `CarryAsync`, `RepeatAsync`.
- `RecoveryService.CompleteAsync` and `BeginEnrolmentAsync`: a link refused under its lock, and a refused password, ended with a commit; rolled back. `StandAsync`: the day limit reached under the approvals' hold ended with a commit, and the alert not raised returned with the unit open; rolled back. Tests `AUTH_RECOV_002_AC1_TheLinkExpiresAndIsNotReusedAsync`, `AUTH_RECOV_002_TheEnrolmentLinkIsTheOnlyOneThatOpensASessionAsync`, `BFF_ABUSE_001_AC2_TwoCapsReachedLiftAtTheLaterOfThemAsync`, `CONV_DESIGN_002_AnApprovalWhoseAlertCannotBeWrittenCommitsNothingAsync`. Reviewed, left: `IssueAsync`, `SendAsync`, `EnrolmentSessions.EndAsync`.
- `CredentialService.SetPasswordAsync`, `CompleteKeyAsync`, `ConfirmGeneratorAsync`: the enrolment session not open under its lock and the refused password, ceremony or confirmation ended with a commit; rolled back. The tail both enrolments share: recovery codes failed or the event not written returned with the unit open; rolled back. `RemoveAsync`, `LinkAsync`, `UnlinkAsync`: the refusals under the locks ended with a commit, and the event not written in `LinkAsync` returned with the unit open; rolled back. Tests `CredentialServiceTests.CONV_DESIGN_003_AC5_ARefusedPasswordRollsBackAsync`, `AUTH_FACT_002b_AC3_AFailedUpgradeChangesNothingAsync`, `CONV_DESIGN_003_AC5_AGeneratorConfirmedWithAWrongCodeRollsBackAsync`, `CONV_DESIGN_003_AC5_AnEnrolmentThatCannotBeAnnouncedRollsBackAsync`, `CONV_DESIGN_003_AC5_ARemovalOfAnUnknownCredentialRollsBackAsync`, `AUTH_RECOV_007_AC5_RemovingTheLastSecondStepRunsTheWindowAsync`, `CONV_DESIGN_003_AC5_ALinkOfAProviderLinkedMeanwhileRollsBackAsync`, `CONV_DESIGN_003_AC5_ALinkThatCannotBeAnnouncedRollsBackAsync`, `CONV_DESIGN_003_AC5_AnUnlinkOfAnIdentityGoneMeanwhileRollsBackAsync`. Reviewed, left: the opening of a key ceremony, `ProviderAttempts.BindAsync` and `TakeAsync`; `ProviderEvents` begins none.
- `BreakGlassService.GenerateAsync`: the alert not raised returned with the unit open; rolled back. The unit that spends the credential: the conditional record answering false, the exempt session not begun and the use alert not raised returned with the unit open; rolled back. Tests `BreakGlassServiceTests.CONV_DESIGN_003_AC5_AGenerationThatCannotBeAlertedRollsBackAsync`, `CONV_DESIGN_003_AC5_AUseThatCannotBeAlertedRollsBackAsync`, `CONV_DESIGN_003_AC5_ARefusedCodeCommitsItsAttemptAndItsRecordAsync`. The attempt count and the failed authentication's record keep their commits; question 76.
- No test reaches these changed returns, each sharing its statement or its operation with one that is tested: `BreakGlassService`, the conditional record answering false and the exempt session not begun; `CredentialService.CompleteKeyAsync` and `ConfirmGeneratorAsync`, the enrolment session found closed under its lock; the shared tail, recovery codes failing; `RemoveAsync`, the last linked way in; `RecoveryService.CompleteAsync`, the password refused by the nested set.
- Parked: `VerificationCodes.PresentAsync` (question 73), the counter mismatch of `WebAuthnService.PresentAsync` (question 74), `RecoveryCodeReminders.RemindedAsync` (question 75), the refusals of `BreakGlassService.PresentAsync` (question 76), `DeviceService`'s standing check and the cancelled report of `LossReports.InvalidateAsync` (question 77).
- Observed, outside the sweep: `CredentialService.SetPasswordAsync` ends the account's other sessions after its commit, in no unit of work; `DeviceService.VerifiedAsync` publishes `DeviceVerified` after the commit that remembered the browser (X1).

#### X9, `part/rollback-accounts`, merged as `699ab691` (`4582a7ce`, `1a58339a`, `fe7a5cdc`, `a64c93e2`, `7e096fc4`, `9c84477a`, `b78a6241`, `10c5e7c9`, `ccfec930`)

The rollback is written inline at each return. The helpers that committed a refusal (`SettledAsync` in `AccountAdministration`, `AccountLifecycle` and `IdentifierService`; `EndedAsync` in `OrganizationService` and `ConfigurationAdministration`) are removed. Where the decision under the lock finds the operation already done, the return is a success with nothing to write and still commits.

- `AccountAdministration.SuspendAsync`, `ReactivateAsync`, `LiftRestrictionAsync`, `CancelDeletionAsync`: a refusal under the account's lock ended with a commit, and the event not written returned with the unit open; rolled back. Tests `AccountAdministrationTests.CONV_DESIGN_003_AC5_ASuspensionRefusedUnderTheLockIsRolledBackAsync`, `..._AReactivationRefusedUnderTheLockIsRolledBackAsync`, `..._ALiftRefusedUnderTheLockIsRolledBackAsync`, `..._ACancellationRefusedUnderTheLockIsRolledBackAsync`.
- `AccountLifecycle.DeactivateAsync`, `ReactivateAsync`, `DeleteAsync`, `CancelDeletionAsync`: the same two returns; rolled back. Tests `IDN_ACCT_007_ASuspensionCommittedMeanwhileIsNamedToADeactivationAsync`, `CONV_DESIGN_005_AC1_AnEventThatIsNotTakenFailsTheOperationAsync`, `IDN_LIFE_013_ASuspensionCommittedMeanwhileIsNotReversedByALinkAsync`, `CONV_DESIGN_003_AC5_ADeletionRefusedUnderTheLockIsRolledBackAsync`, `CONV_DESIGN_003_AC5_ACancellationRefusedUnderTheLockIsRolledBackAsync`.
- `AccountService.EditProfileAsync`: the three refusals of a username on its own text (mixed script, invalid, reserved), the first after the beginning, are judged before it; every refusal inside the choice rolls back. `SetPreferencesAsync`: a refused preference returned with the unit open; rolled back. Tests `REG_IDENT_009_AC2_ASecondChangeInsideTheWindowIsRefusedAsync`, `CONV_DESIGN_003_AC5_APreferenceRefusedAfterTheWorkBeganIsRolledBackAsync`. Reviewed, left: `LabelCredentialAsync`, `PreferSecondStepAsync`, `ProfilePhotos.SetAsync` and `RemoveAsync` (no return between).
- `IdentifierService.AddAsync` (the maximum under the lock, a refused stage), `LandAsync`, `MakePrimaryAsync`, `SetBackupAsync`, `RemoveAsync`, `UndoAsync` and the two replacements: refusals under the lock ended with a commit, and a refused send or an event not written returned with the unit open; rolled back. The verification: no pending record, an expired code and a failed settle roll back; a wrong code writes its count and commits, read as the wrong try of AUTH-FACT-004, the operation beginning the outermost unit of work. Tests `CONV_DESIGN_003_AC5_AnAdditionWhoseSendIsRefusedIsRolledBackAsync`, `CONV_DESIGN_003_AC5_AWrongCodeCommitsItsCountAloneAsync`, `CONV_DESIGN_003_AC5_AnExpiredCodeIsRolledBackAsync`, `REG_IDENT_007_AC2_AChangeAbandonedMeanwhileIsNotAppliedAsync`, `CONV_DESIGN_003_AC5_APressOnAVerificationGoneMeanwhileIsRolledBackAsync`, `..._APromotionRefusedUnderTheLockIsRolledBackAsync`, `..._ABackupRefusedUnderTheLockIsRolledBackAsync`, `REG_IDENT_006_AC1_AnAddressMadePrimaryMeanwhileIsSparedAsync`, `..._AnUndoSpentMeanwhileIsRolledBackAsync`, `..._AReplacementWhoseSendIsRefusedIsRolledBackAsync`. Reviewed, left: `AbandonAsync`.
- `RegistrationService.BeginAsync`: an invitation no longer opening under its lock ended with a commit; rolled back. The unit `RecordAgeAsync` and `VerifyAsync` share committed whatever was decided; it now rolls back but where the refusal counts (a code spent, never outstanding, expired or wrong, with the throttle's failure counted). The stage, the addition, the resend, the change and the supplied address: a refused send rolls back. The completion: a session not issued, a browser not remembered, a refused consent and an event not written roll back. Tests `RegistrationServiceTests.CONV_DESIGN_003_AC5_ALinkRefusedUnderTheInvitationsLockIsRolledBackAsync`, `..._AWrongCodeCommitsItsCountsAsync`, `..._ACodeForNoStagedIdentifierIsRolledBackAsync`, `..._AnAgeAnsweredOutOfStepIsRolledBackAsync`, `..._AStagedIdentifierWhoseSendIsRefusedIsRolledBackAsync`, `..._ABoundIdentifierWhoseSendIsRefusedIsRolledBackAsync`, `..._AChangedIdentifierWhoseSendIsRefusedIsRolledBackAsync`, `..._ASuppliedAddressWhoseSendIsRefusedIsRolledBackAsync`, `PRIV_CONS_001_AC1_AControlForAPurposeTakingNoConsentIsRefusedAsync`. Reviewed, left: `SkipPhoneAsync`, `DiscardAsync`, `LandAsync`, `AbandonAsync`, `SweepAsync`.
- `InvitationService.IssueAsync` and `RevokeAsync`, `InvitationAcknowledgement.AcknowledgeAsync`, `InvitationOpening.OpenAsync`, `MembershipEnd.EndAsync`: refusals under the lock ended with a commit, and a failed attachment or an event not written returned with the unit open; rolled back. Tests `IDN_ORG_003_AC12_ADeletionRequestedMeanwhileTakesNoInvitationAsync`, `CONV_DESIGN_003_AC5_ARevocationRefusedUnderTheLockIsRolledBackAsync`, `REG_INV_001_AnInvitationRevokedMeanwhileAttachesNothingAsync`, `CONV_DESIGN_003_AC5_APressRefusedUnderTheLockIsRolledBackAsync`, `CONV_DESIGN_003_AC5_AnEndThatFindsNoMembershipIsRolledBackAsync`. Reviewed, left: `InvitationService.SweepAsync`.
- `ConfigurationAdministration.ChangeAsync` and `ChangeMemberAsync`, `ProtectedConfiguration.ChangeAsync`: refusals under the lock ended with a commit, and a refused write or an alert not raised returned with the unit open; rolled back. Tests `OPS_CFG_002_AC2_LengtheningOneRequiresStepUpAsync`, `ChangeAsync_AWarningThatIsNotTaken_IsRefusedAndNotWrittenDownAsync`, `OPS_ALERT_001_AnExportStepUpTurnedOffWithoutItsAlertIsNotMadeAsync`, `CONV_DESIGN_003_AC5_AMemberTheStoreRefusesIsRolledBackAsync`, `OPS_ALERT_001_AProtectedChangeWhoseAlertIsNotRaisedIsNotMadeAsync`.
- `OrganizationService.CreateAsync`, `RequestDeletionAsync`, `CancelDeletionAsync`, `ReplacePolicyAsync`; `OrganizationDomainService`, the holding read, `AddDomainAsync`, `RemoveDomainAsync`; `DomainReverification.SweepAsync`: refusals under the lock ended with a commit, and a write refused or an alert not raised returned with the unit open; rolled back. Tests `OrganizationServiceTests.CONV_DESIGN_003_AC5_ADeletionRequestRefusedUnderTheLockIsRolledBackAsync`, `..._ACancellationRefusedUnderTheLockIsRolledBackAsync`, `..._APolicyRefusedUnderTheLocksIsRolledBackAsync`, `OrganizationDomainServiceTests.CONV_DESIGN_003_AC5_ADomainAddedWithNoResolverIsRolledBackAsync`, `..._ADomainAddedWithoutStepUpIsRolledBackAsync`, `..._ADomainRemovedWithoutStepUpIsRolledBackAsync`, `DomainReverificationTests.CONV_DESIGN_003_AC5_AFailedCheckWhoseAlertIsNotRaisedIsRolledBackAsync`, and, in the host's tests, `OrganizationPolicyEndpointTests.OPS_CFG_002_AC6_APolicyChangeIsDecidedUnderItsRowsLocksAsync` and `OrganizationDomainEndpointTests.OPS_CFG_002_AC6_AListChangeIsDecidedUnderItsRowLockAsync`, which asserted the commit. Reviewed, left: `VerifyDomainAsync`.
- `AlertChannels.RaiseAsync`: the event not written; rolled back (`CONV_DESIGN_002_AnEventWhoseRowCannotBeWrittenFailsTheRaiseAsync`). `MailboxPublisher`, the record of a failed push: the alert not raised; rolled back (`CONV_DESIGN_003_AC5_AFailedPushWhoseAlertIsNotRaisedIsRolledBackAsync`). `DeploymentBootstrap.RunAsync`: a deployment already administered, an under-age date, a failed join and the alert not raised; rolled back (`DeploymentBootstrapTests.CONV_DESIGN_003_AC5_ADeploymentStoodUpAlreadyIsRolledBackAsync`, `..._AnUnderAgeAdministratorIsRolledBackAsync`).
- Reviewed, left (no return between): `AppPasswords.RecordAsync`, `MaintenanceRecords` (two sites), `EventOutbox`, `CallbackReferences`.
- Reviewed, left after the beginning though no row decides them, each for the order of refusals: the resolver missing in `OrganizationDomainService` (it would be answered before "already listed", which succeeds); the reads of configuration in the identifier's verification and in the registration's settle; "already administered" and the under-age date at bootstrap, which read what the transaction wrote or must see; the reason of a configuration change, judged with the direction under the lock.
- `AlertChannels.RaiseAsync` and `ConfigurationAdministration.ChangeMemberAsync` begin a level inside their callers' unit of work and roll it back on failure; every caller returns that failure and rolls back.
- No test reaches these changed returns, the fakes giving no such failure: the registration's settle (a policy read or the recovery codes failing), `OrganizationService.CreateAsync`, the configuration failure of the holding read, and the event not written in `AccountAdministration`, `IdentifierService`, `InvitationAcknowledgement` and `MembershipEnd`.
- Parked: the under-age refusal of `RegistrationService.RecordAgeAsync` (question 78); the taken identifier at `RegistrationService.CompleteAsync` (question 79); the operations that discard a send's refusal inside their unit of work (question 80); the throttle's count inside the verification's unit of work (question 81).
- Observed, outside the sweep: the holding read of `OrganizationDomainService` throws where `BeginAsync` fails although its operations return a result (CONV-DESIGN-003 criterion 7).

#### Question 57, `part/registration`, merged as `2f4bdeaf` (`dae18972`, `66771b55`, `52cf4197`, `89e6fdc2`, `59b89a67`, `a8dc5501`, `58847dde`)

- `Janus.Core`, `AddCoreArea`: the key ring and the mail server in use (4 singletons). `Janus.Storage`, `AddStorageArea`: gains the schema check. `Janus.Authorization`, `AddAuthorizationArea(declaration)`: 16 registrations. `Janus.Privacy`, `AddPrivacyArea`: 20. `Janus.Authentication`, `AddAuthenticationArea`: 81. `Janus.Identity`, `Janus.Cli` and `Janus.Conformance` expose none.
- `AddJanus` calls the five methods and registers the types `Janus.Hosting` defines (the gates of the request, the alerts, the concealed refusals, the gated settings, the notices, the word list and the leaked-password corpus, the location database, the mail-server tokens, the configuration service, the sending service, the restore test, the audit retention, the browser boundary, the hosted services, the provider's setup). `AddKeyRing` is removed.
- Each command's composition calls `AddCoreArea` and `AddStorageArea`, registers the ring its key document filled after them, and bootstrap and `configure` call `AddAuthenticationArea`.
- `Microsoft.AspNetCore.App` is a framework reference of Core, Authentication, Authorization, Privacy, Storage, Cli and Conformance; the lockfiles of Authentication, Cli, Conformance and Storage lose their `Microsoft.Extensions.*` entries.
- The descriptors `AddJanus` makes (903) were compared before and after: for each service type the sequence of lifetime, kind and made type is the same, and the order of the hosted services is the same.
- Tests: `PublicSurfaceTests.CONV_DESIGN_007_AC7_EachProjectExposesItsOneRegistrationMethodAndNoOtherExposesOne`, `PublicSurfaceTests.CONV_DESIGN_007_AC7_TheEntryPointCallsEveryMethodAndEachRegistersItsOwnProjectsTypesAlone`, `PublicSurfaceTests.CONV_DESIGN_007_AC8_OnlyTheHostingProjectUsesTheFrameworkAndTheOthersItsContainerAlone`. The integration tests of `Janus.Cli.Tests` (72) and of `Janus.Hosting.Tests` (272) passed on the part.
- Not built: criterion 7's clause that every type of such a project `AddJanus` registers is registered by that project's own method has no test; nine registrations stay in Hosting on questions 82 to 84. Questions 85 and 86 park the commands' own registrations and two inner compositions.

#### X9, `part/rollback-privacy`, merged as `0f4fed1a` (`d399bdf2`, `96cf4458`, `d29ae7a9`, `e67f234d`, `c896d24b`, `4c8b33d8`, `1b363b33`, `976231f9`, `124cd764`, `284a0c2e`, `42659492`, `946567cd`, `e01c8c06`, `c5514735`, `f784515f`, `79a480c3`, `0c7962b2`, `2b42bf9f`)

The rollback is written inline. `EndedAsync` (`RoleService`, `ConfigurationService`) and `RefusedAsync` (`GroupService`), which committed, are removed. A return that answers success having found nothing to do commits (question 77).

- `ConsentService.GrantAsync`, `WithdrawAsync`, `ObjectAsync`, `WithdrawObjectionAsync`: the event not written returned with the unit open; rolled back. Test `ConsentTests.CONV_DESIGN_003_AC5_AChangeRefusedAfterItBeganRollsBackAsync`.
- `LegalDocumentService`, the publication: a failed supersession; rolled back (`LegalDocumentTests.CONV_DESIGN_003_AC5_APublicationRefusedAfterItBeganRollsBackAsync`). Reviewed, left: `TranslateAsync`.
- `ErasureService.CompleteAsync`: an erasure not failed under its lock ended with a commit; rolled back (`ErasureServiceTests.IDN_LIFE_003a_AnErasureClosedMeanwhileIsNotClosedTwiceAsync`). `OrganizationErasureSweep`: two event rows not written; rolled back (`IDN_ORG_003_AnErasureWhoseEventRowFailsErasesNothingAsync`, `CONV_DESIGN_003_AC5_AnErasureThatCannotBeAnnouncedRollsBackAsync`). Reviewed, left: `DeletionSweep`, `ErasureReplay`, `OutboxPublisher`, `ProcessingRecordsService.DeclareAsync` (no return between; failures throw).
- `ExportService.AssembleAsync`: the limit under the hold ended with a commit; rolled back (`ExportServiceTests.PRIV_RIGHT_003_AnExportCountedMeanwhileIsCountedAsync`).
- `PrivacyRequestService.FulfilAsync`, `RefuseAsync` and the queueing: a request undecidable under its lock and a duplicate under the hold ended with a commit, and a failed fulfilment returned with the unit open; rolled back. Tests `PrivacyRequestTests.PRIV_RIGHT_001_AC1_ARequestQueuedMeanwhileMakesADuplicateAsync`, `PRIV_RIGHT_001_IDN_LIFE_003_AnErasureThatCannotBeginFailsTheFulfilmentAsync`, `PRIV_RIGHT_002_AC5_AnErasureRefusedMeanwhileBeginsNoDeletionAsync`, `CONV_DESIGN_003_AC5_ARefusalOfARequestDecidedMeanwhileRollsBackAsync`.
- `KeyRotation.ReWrapAsync` and `FingerprintKeyRotation.RecomputeAsync`: the refusal for a ring without its keys, which reads nothing, is judged before the beginning; the refusal on the progress and versions returned with the unit open; rolled back. `RetireAsync` of each: "not standing" under the hold, and for the fingerprint key what still stands, ended with a commit; rolled back. Tests `KeyRotationTests.CONV_DESIGN_003_ARotationWithoutItsKeysBeginsNoUnitOfWorkAsync`, `CONV_DESIGN_003_AC5_ARotationRefusedWithItsProgressHeldRollsBackAsync`, `CONV_DESIGN_003_AC5_ARetirementRefusedWithItsProgressHeldRollsBackAsync`, the same three in `FingerprintKeyRotationTests`, and `FingerprintKeyRotationTests.CONV_DESIGN_003_AC5_ARetirementRefusedForWhatStillStandsRollsBackAsync`.
- `TakedownService.ExecuteAsync` and `ReverseAsync`: the refusal under the account's lock and the event not written returned with the unit open; rolled back. Tests `TakedownServiceTests.IDN_LIFE_003_AStateCommittedMeanwhileIsTheOneAnsweredAsync`, `IDN_LIFE_003_ARefusedAnnouncementLeavesNothingAsync`, `IDN_LIFE_003_ARefusedReversalAnnouncementLeavesNothingAsync`, `CONV_DESIGN_003_AC5_AReversalRefusedUnderTheLockRollsBackAsync`.
- `ExportOperations`, the gate's admission of an export: the limit under the hold ended with a commit; rolled back (`ExportOperationsTests.OPS_ALERT_006_AnExportAdmittedMeanwhileIsCountedAsync`). Where the gate is asked inside an operation's unit of work, a limit now marks that unit.
- `GrantService`, the grant and `RevokeAsync`; `GroupService.RemoveAsync`, `AddMemberAsync`, `RemoveMemberAsync`; `RoleService.DefineAsync` and `RemoveAsync`: refusals under the locks ended with a commit, and `authz.group.inuse` returned with the unit open; rolled back, the step-up still last. A member not held answered success with the unit open; it commits. Tests `GrantEndpointTests.CONV_DESIGN_003_AC5_AGrantOrARevocationRefusedAfterItBeganRollsBackAsync`, `GroupEndpointTests.CONV_DESIGN_003_AC5_AChangeRefusedAfterItBeganRollsBackAsync`, `GroupEndpointTests.AUTHZ_GROUP_001_AChangeThatChangesNothingRecordsNothingAsync`, `RoleEndpointTests.CONV_DESIGN_003_AC5_ADefinitionOrARemovalRefusedAfterItBeganRollsBackAsync`. Reviewed, left: `GroupService.CreateAsync`, `DerivationMaterialiser.RefreshAsync`, `ReadVolume.RebaselineAsync`, `ResourceService.RegisterAsync` and `MoveAsync`.
- `AlertDestinationChange.ChangeAsync`: the joined change refused and the event not written; rolled back (`AlertDestinationChangeTests.CONV_DESIGN_003_AC5_AChangeThatCannotBeAnnouncedRollsBackAsync`). `BackgroundWorker`, the lapse check: no lapse answered success with the unit open, and commits; a raise that fails rolls back (`BackgroundWorkerTests.CONV_DESIGN_003_AC5_ALapseThatCannotBeRaisedRollsBackAsync`). `RestoreTest`, the record: the alert refused; rolled back (`RestoreTestRecordTests.CONV_DESIGN_003_AC5_ARunWhoseAlertCannotBeRaisedRollsBackAsync`, `RestoreTestRecordTests.DR_007_AC3_AFailedRunIsRecordedAndRaisedInOneUnitOfWorkAsync`). Reviewed, left: `AlertRouter.RaiseAsync`, `AlertDispatch`, `AuditRetention`.
- `CallbackIntake`: an answer other than `integration.callback.rejected` was left to the scope's disposal; rolled back (`HostCallbackTests.CONV_DESIGN_003_AC5_ARefusalWhoseAlertCannotBeRaisedRollsBackAsync`). The rejected answer commits as before (question 87). `ProviderEventIntake.TakeAsync`: a failed take; rolled back (`ProviderEventTests.CONV_DESIGN_003_AC5_AnEventThatCannotBeAnnouncedRollsBackAsync`).
- `ConfigurationService`, the retention of a category: two refusals under the hold ended with a commit, and a refused member write returned with the unit open; rolled back (`ConfigurationEndpointTests.PRIV_RET_001_ACategorysRetentionIsChangedThroughTheRouteAsync`).
- `EventPublisher.PublishAsync` and `SendingService`, the settling of an attempt: the alert for a spent budget refused; rolled back (`EventPublisherTests.CONV_DESIGN_003_AC5_ASpentBudgetWhoseAlertCannotBeRaisedRollsBackAsync`, `SendingServiceTests.CONV_DESIGN_003_AC5_ASpentBudgetWhoseAlertCannotBeRaisedRollsBackAsync`). Reviewed, left: the draw and the send of `SendingService` (every refusal before the beginning); `TruthTable` of the conformance suite (failures throw).
- The integration classes `FingerprintRotationTests`, `ConfigurationLockTests` and `RestoreTestTests` passed on the part.
- Parked: the rejected callback at `CallbackIntake` (question 87); the sends joined to `DeadlineSweep` and to the provider event's intake (question 88); the returns that answer success having written nothing (question 77).
- Observed, outside the sweep: where `SendingService.SendAsync` or `AlertRouter.RaiseAsync` joins a caller's unit of work (the deadline sweep's lapse, the provider event's notice, the worker's lapse of the alert dispatch), the transport is called with the caller's transaction open (CONV-DESIGN-002); it goes with the governed send.

#### D-183, `part/gates`, merged as `b9aa3038` (`9bac4a40`, `64b1a154`, `893d7258`, `bebf5dbd`, `367b7496`, `dd009fb7`)

After the merge: build 0 warnings 0 errors, format clean, 2925 unit tests, 139 contract tests. No migration. No permission logic changed.

- **Question 25** (`9bac4a40`, `dd009fb7`; IDN-ATTR-002, OPS-CFG-003, REF-001). The family `photo.enabled.<organization>` is removed; `Policy` and `PolicyOverride` gain `Photos`, resolved as every field; the profile photo reads the resolved policy; bootstrap writes the administrative organization's photos false; the organization's policy and `policy.default` refuse photos true without a codec with `config.value.notallowed`; the start reads `policy.default` and every `policy.<organization>`. The two REF-001 tests, `BFF_ERR_001_AC3` and `OPS_CFG_004` read chapter 10 alone. Tests:
  - `ProfilePhotosTests.IDN_ATTR_002_AC1_AnAccountInNoOrganizationShowsAPhotoExactlyWhenTheSystemPolicyDoesAsync`, `ProfilePhotosTests.IDN_ATTR_002_AC2_AnOrganizationIsGivenPhotosByItsPolicyAloneAsync`
  - `BootstrapTests.IDN_ATTR_002_AC3_BootstrapWritesTheAdministrativeOrganizationsPhotosOffAsync`
  - `StartupValidationTests.IDN_ATTR_002_AC3_ADeploymentThatShowsNoPhotosStartsWithNoCodecAsync`, `OPS_CFG_003_AC4_ADeploymentWhoseSystemPolicyShowsPhotosWithNoCodecIsRefusedAsync`, `IDN_ATTR_002_ADeploymentThatShowsPhotosWithNoCodecIsRefusedAsync`, `IDN_ATTR_002_ADeploymentThatShowsPhotosAndDeclaredACodecStartsAsync`
  - `OrganizationPolicyEndpointTests.IDN_ATTR_002_AC4_PhotosAreNotTurnedOnWithoutACodecAsync`
  - `AccountAdministrationEndpointTests.IDN_ATTR_002_AC5_AnAccountOfTwoOrganizationsShowsAPhotoOnlyWhereBothEnableThemAsync`, `IDN_ATTR_003_AC3_AnAccountWithoutAPhotoIsAnsweredWithTheCodeAsync`
  - `AccountAdministrationTests.IDN_ATTR_002_APhotoThePolicyWithholdsIsNotReadAsync`
  - `SettingsCatalogueTests.REF_001_AC1_EveryKeyInTheSourceIsARowOfTheReference`, `OPS_CFG_004_AKeyMarkedProtectedIsOnTheOneList`, `LIB_API_001_AC2_TheKeysAreTheContract`
  - `ErrorCodesTests.REF_001_AC1_EveryCodeInTheSourceIsARowOfTheReference`, `BFF_ERR_001_AC3_EveryCodeTheBoundaryCanAnswerIsInTheReference`
  - Ledger: entry 144 "Superseded by D-166"; entry 403 "Superseded by D-183".
  - Parked: the reverse direction of the REF-001 tests (question 89).
- **Question 49** (`64b1a154`; CONV-DESIGN-004). The scan reads every project under `src` and `tools`, with no file-level exclusion; a match is exempt only as a member a package's interface fixes, or as the wrapped value in the declaration of the typed value itself. `BrowserProfileLog.Concealed` and `ConcealedTooLate` take `AuditRecordId`. Tests: `PublicSurfaceTests.CONV_DESIGN_004_AC2_NoMethodTakesAValueAsItsUnderlyingType`, `PublicSurfaceTests.CONV_DESIGN_004_AC2_OnlyAMemberAPackagesInterfaceFixesIsExempt`.
- **Question 52** (`893d7258`; INT-MAIL-001, INT-MAIL-007). The JMAP adapter lists an account whose `emailAddress` is absent or not text with no address, where it failed the listing; an unreadable permissions member still fails it. Tests: `MailboxReconciliationTests.INT_MAIL_007_AC9_AnAccountWhoseAddressDoesNotReadIsCountedOrIsADifferenceAsync`, `JmapMailServerTests.INT_MAIL_007_AC9_AnAccountWhoseAddressDoesNotReadIsListedWithNoneAsync`. Question 91.
- **Question 55** (`bebf5dbd`; LIB-API-005, CONV-DESIGN-002). The signature already took no access context. Test: `PublicSurfaceTests.CONV_DESIGN_002_AC3_TheReadOfThePublishedKeySetTakesNoAccessContextAndAnswersPublicKeysAlone`.
- **Question 56** (`367b7496`; IDN-LIFE-012a, LIB-API-003). The intake reads the token first, then verifies; the Google route answers 400 with `err` and `description` failure by failure in the chapter's order; the Apple route answers 422 `integration.callback.rejected`; a provider document that cannot be read rolls back and answers 500 `system.fault`, claiming nothing. The audience is compared exactly. Tests, all `ProviderEventTests`:
  - `IDN_LIFE_012a_AC8_EachFailureOnTheGoogleRouteIsAnsweredWithItsErrAsync` (12 cases), `IDN_LIFE_012a_AC8_AnEventWhoseLifetimeHasNotPassedIsCarriedAsync`, `IDN_LIFE_012a_AC8_AnEventOfAProviderNotDeclaredIsAnsweredInvalidIssuerAsync`, `IDN_LIFE_012a_AC8_AProviderDocumentThatCannotBeReadIsAFaultAndClaimsNothingAsync`
  - `IDN_LIFE_012a_AC7_AnEventCarryingNoJtiChangesNothingAndIsRefusedAsync`
  - `IDN_LIFE_012a_AC3_TheAppleRouteRefusesAnUnsignedEventAsARejectedCallbackAsync`, `IDN_LIFE_012a_AC3_AnEventOfAProviderNotDeclaredIsRefusedOnTheAppleRouteAsync`
  - `IDN_LIFE_012a_AC1_AnUnsignedEventChangesNothingAndIsAuditedAsRejectedAsync`
  - Not decided by a test: "the provider may deliver again", beyond the redelivery the fault test makes.
  - The parked site of question 87 was touched: `CallbackIntake.RefusedAsync` is split so that the Google route can answer in its own shape; what a rejected callback commits is unchanged.
  - A token whose `nbf` is in the future: question 90.
- For audit: `OrganizationService` and `ConfigurationService` take the codec as a constructor parameter, registered by a factory that asks the container for it; `Policy` and `PolicyOverride` gain a trailing positional member.

#### D-183, `part/sessions`, merged as `0b598993` (`a4a00c82`, `fdef37a9`, `d52cacfb`, `bd858a96`, `0c3746d8`, `f805640f`, `a2025e00`)

After the merge: build 0 warnings 0 errors, format clean, 2946 unit tests, 139 contract tests; the truth-table check passes over the range. One migration, `RecordWhenASessionWasDowngraded` (one nullable column, no hand-written SQL); the working branch held no other, so the snapshot merged without conflict and nothing was regenerated.

- **Question 40** (`a4a00c82`; AUTHZ-IMP-001, AUTH-RECOV-007). `CredentialSuspended` carries the effective identity of the reporting context, as given. Test: `LossReportsTests.AUTHZ_IMP_001_ASuspensionCarriesBothIdentitiesAsTheContextGivesThemAsync`. Ledger: 152 "Superseded by D-166". The other events that name who acted: question 95.
- **Question 41** (`fdef37a9`; REG-DOM-001 criterion 10). An account holding no verified email is refused `identity.identifier.domainnotallowed` at acknowledgement where the organization's lock is on, before the unit of work. This brings `2e8174c1` to the answer. Test: `InvitationServiceTests.REG_DOM_001_AC10_AnAccountHoldingNoVerifiedEmailCannotAcknowledgeIntoALockedOrganizationAsync`.
- **Question 46, and the failure rules of `669bac7b`** (`d52cacfb`; LIB-HOST-004 criterion 4, AUTH-STEP-002, AUTHZ-TEST-001). A report whose instant is after now, or whose level or reachable assurance is not a level of `10` section 5.4, and a provider that fails, are `auth.stepup.required` with `outcome` `present`, empty `options` and a null `pendingUntil`; no provider stays `auth.stepup.unavailable`. Truth table: the table `StepUps`, seven rows, in a second container that registers a provider. Tests: `StepUpGatesTests.LIB_HOST_004_AC4_AReportThatDoesNotReadMeetsNoGateAsync`, `StepUpGatesTests.LIB_HOST_004_AC4_TheActingPersonsOwnSessionIsJudgedInPlaceOfTheProviderAsync`, `TruthTableTests.AUTHZ_TEST_001_AC1_EveryStepUpCaseDecidesTheWayTheTableSaysAsync` (seven cases). Ledger: 328 "Superseded by D-166". Not built: the filter half of the six unmet rows (question 92) and the seven scenarios of the conformance suite (question 93).
- **Question 36** (`bd858a96`; AUTH-SESS-009, AUTH-SESS-001, AUTH-STEP-002, IDN-LIFE-009b, AUTHZ-GATE-005). `sessions.downgraded_at`; the acknowledgement downgrades every standing session of the account in its transaction; a proof counts only where attained after the last downgrade; a session derived from a downgraded record is downgraded from its first instant; the capability page answers `reauthenticate` where the gate is unmet only for the downgrade; at a step-up a factor outside the policy's `loginFactors` is refused `auth.factor.notpermitted` before it is verified, recorded and counted. Truth table: three rows in the operations table. Tests:
  - `StepUpTests.AUTH_SESS_009_AC6_ADowngradedSessionIsAskedAPresentationThatLiftsIt`, `StepUpTests.AUTH_STEP_002_AC3_ProofAttainedUpToTheLastDowngradeIsNotCounted`, `StepUpTests.AUTH_SESS_009_AC5_ASessionDerivedFromADowngradedRecordPassesNoGate`
  - `AuthenticationServiceTests.IDN_LIFE_009b_ASessionHeldBeforeTheMembershipIsDowngradedAsync` (also AUTH-STEP-002 criterion 9)
  - `InvitationServiceTests.AUTH_SESS_009_AC5_TheAcknowledgementDowngradesEverySessionTheAccountHoldsAsync`
  - `SessionStoreTests.AUTH_SESS_009_AC5_ADowngradeIsWrittenOnEveryStandingSessionOfTheAccountAsync`
  - `TruthTableTests.AUTHZ_TEST_001_AC1_EveryOperationCaseDecidesTheWayTheTableSaysAsync` (three new cases)
  - Ledger: 246 "Superseded by D-166".
  - For audit: a presentation below the level a session attained before its downgrade lifts the downgrade, and the earlier, higher level then counts again, as it already does for the maximum age.
- **The correction of `52482ed5`, the sign-in half** (`0c3746d8`; AUTH-FACT-002 criterion 7, AUTH-FACT-002b criterion 6). A `phoneCode` ask after a first factor whose number answers `risk` issues, sends and counts nothing, records the consideration and answers 200 `factorRequired` without the entry, or 422 `auth.factor.rejected` where none is left. Tests: `AuthenticationServiceTests.AUTH_FACT_002_AC7_AReportedChangeAtTheAskLeavingNoFactorIsRefusedAsync`, `AuthenticationServiceTests.AUTH_FACT_002_AC7_AReportedChangeAtTheAskIsAnsweredWithTheFactorsLeftAsync`, `SignInFlowTests.AUTH_FACT_002_AC7_ATextCodeAskedForAReportedNumberIsAnsweredWithWhatIsLeftAsync`. The step-up half is parked (question 94); there the ask still answers 202.
- **Questions 47 and 48, D-166 389** (`f805640f`; BFF-ORDER-001 criteria 3, 6, 7, AUTH-ABUSE-001 criterion 13, AUTH-SESS-013 criterion 6, REG-SESS-007 criterion 6). A source is the IPv4 address, an IPv4-mapped address read as IPv4, or the /64 of an IPv6 address; stage 4 counts the source and its /48 under the new key `abuse.source.sitelimit` (3000) and writes one line where a hold begins; a session records the whole address. Tests: `SourceRateLimitingTests.BFF_ORDER_001_AC3_AddressesInOneIpv6SubnetAreOneSourceAsync`, `BFF_ORDER_001_AC3_AnotherSubnetOfTheSiteIsAdmittedUntilTheSiteLimitAsync`, `BFF_ORDER_001_AC3_AnIpv4MappedAddressIsItsIpv4SourceAsync`, `BFF_ORDER_001_AC3_AHeldSourceWritesNoLineForEachRefusalAsync`, `AUTH_SESS_013_TheSessionRecordsTheWholeAddressAsync`; `ThrottlingTests.AUTH_ABUSE_001_TwoAddressesOfOneIpv6SubnetShareTheSourceDelayAsync`; `RegistrationServiceTests.REG_SESS_007_AC6_TheFirstSessionRecordsTheWholeAddressOfTheCompletingRequestAsync`. Ledger: 389 "Superseded by D-166". Not decided by a test: "each instance counts in its own memory"; the counts are fields of a singleton.
- **Question 32, and the counting half of 48** (`a2025e00`; REG-SESS-003 criterion 6, AUTH-ABUSE-001, REG-SESS-001). A pressed token that opens nothing asks its source's delay, is counted against the source alone and answered `auth.code.expired`; every delay, count and send of a registration uses the source of the request in hand. Tests: `RegistrationServiceTests.REG_SESS_003_AC6_APressedTokenThatOpensNothingIsCountedAgainstItsSourceAsync`, `RegistrationServiceTests.AUTH_ABUSE_001_ARegistrationCountsAgainstTheSourceOfTheRequestInHandAsync`, `RegistrationFlowTests.REG_SESS_003_AC6_APressedTokenThatOpensNothingIsCountedOverTheWireAsync`. Ledger: 419 "Superseded by D-166". The public surface it changed: question 96.
- **Not built in the part:** question 31 with 306 (the registration and identifier codes through the verification-code record, the record for a held or reserved value, the expiry sweep; ledger lines 115 and 306) and question 42 (the registration session's credential authority, the staged ceremony and unconfirmed generator; ledger line 129). Neither was started. Both are taken up on the working branch after `part/sending` is merged, since each rebuilds code the governed send changes.
- The integration classes `TruthTableTests`, `GateBehaviourTests`, `SignInFlowTests`, `SourceRateLimitingTests`, `ThrottlingTests`, `RegistrationFlowTests`, `RegistrationWizardTests`, `SessionStoreTests`, `ModelTests` and `SchemaContractTests` passed on the part.

#### D-183, `part/privacy`, merged as `965a8201` (`3963b184`, `0b11699d`, `67b5c544`, `2247202a`, `f2ef4d42`, `46968c21`, `ea174da9`, `c972731e`)

After the merge: build 0 warnings 0 errors, format clean, 2991 unit tests, 139 contract tests. Three migrations: `AllowAbsentRequestDetail` (the column nullable; hand-written, an empty detail made null and back), `NarrowWhatAFingerprintRetirementDeletes` (hand-written alone: eight `REVOKE DELETE` from the maintenance credential, and their grants back), `AddLawfulBases` (the table; hand-written, its grant to the runtime credential). The snapshot merged without conflict beside the sessions part's migration, and the model has no change a migration does not carry (`dotnet ef migrations has-pending-model-changes`), so nothing was regenerated.

- **Question 21** (`3963b184`; API-CONV-002, CONV-CODE-006, `09` section 8a). This brings `e747407f` to the answer: `detail` is nullable from the endpoint to the column, null records none, a given value that is blank or past 1024 characters after trimming is refused, and the empty string is never stored. Tests: `PrivacyRequestTests.API_CONV_002_AnEntryWithNoDetailRecordsNoneAsync`, `PrivacyRequestTests.API_CONV_002_AnEntryWithABlankOrOverlongDetailIsMalformedAsync`, `PrivacyRequestEndpointTests.API_CONV_002_AnEntryWithNoDetailIsTakenAndRecordsNoneAsync`, `PrivacyRequestEndpointTests.API_CONV_002_AnEntryWithADetailOutsideTheBoundIsRefusedBeforeTheServiceAsync`, `PrivacyRequestStoreTests.API_CONV_002_AnEntryWithNoDetailIsStoredAsNoneAsync`, `SchemaContractTests.LIB_API_001_AC2_TheLibraryOwnedSchemaIsTheContractAsync`.
- **Question 44** (`0b11699d`; IDN-LIFE-003 criterion 5, PRIV-RIGHT-001 criterion 4). `b1960a2e` stands; no code changed. One test for the case none held: `AccountTests.IDN_LIFE_003_AC5_AReversalToAnOutOfBandDeletionKeepsTheSuspensionItHolds`.
- **Question 45** (`67b5c544`; PRIV-RIGHT-001, IDN-LIFE-003, OPS-BOOT-002, `09` section 8a). The entry refuses a subject no account bears with 422 `api.request.invalid` naming `subject`; the fulfilment no longer answers `identity.account.notfound`: a subject with no account is a fault, an erasure of the reserved emergency account is `authz.denied` before the step-up, and a deletion that does not begin is recorded fulfilled only where the account is found deleting or deleted under its lock. Tests: `PrivacyRequestTests.PRIV_RIGHT_001_AnEntryForASubjectNoAccountBearsIsInvalidAsync`, `PrivacyRequestTests.OPS_BOOT_002_AnErasureOfTheReservedAccountIsDeniedBeforeTheStepUpAsync`, `PrivacyRequestTests.PRIV_RIGHT_001_IDN_LIFE_003_AnErasureOfASubjectNoAccountBearsIsAFaultAsync`, `PrivacyRequestTests.PRIV_RIGHT_001_IDN_LIFE_003_ADeletionThatWillNotBeginIsAFaultAsync`, `PrivacyRequestEndpointTests.PRIV_RIGHT_001_AnEntryForASubjectNoAccountBearsIsRefusedAsync`, `AccountStatesTests.OPS_BOOT_002_TheReservedAccountIsNeverTakenDownAsync`.
- **Question 24** (`2247202a`; INT-SMS-003, `09` sections 7 and 8a). The read, the publication and the translation routes refuse a document name outside the rule with 400 `api.request.malformed` naming `document`, as the handler's first act. Tests: `PublicationEndpointTests.INT_SMS_003_APublicationUnderANameOutsideTheRuleIsMalformedAsync`, `PublicationEndpointTests.INT_SMS_003_ATranslationUnderANameOutsideTheRuleIsMalformedAsync`, `LegalDocumentEndpointTests.INT_SMS_003_AReadOfANameOutsideTheRuleIsMalformedAsync`. Not decided by a test: "answered before the body is read" (question 97). A caller in process: question 98.
- **Question 65** (`f2ef4d42`; PRIV-ROPA-001, API-CONV-002, CONV-CODE-006). `dataOwner` and `organizationalSecurityMeasures` are trimmed, refused 400 `api.request.malformed` naming the member where blank or past 1024 characters, at the endpoint and in the service, kept trimmed, and cleared when omitted. Tests: `ProcessingRecordsEndpointTests.API_CONV_002_AStatementOutsideTheBoundIsRefusedBeforeTheServiceAsync`, `ProcessingRecordsEndpointTests.API_CONV_002_AStatementIsKeptTrimmedAndAnOmittedOneIsClearedAsync`, `ProcessingRecordsTests.API_CONV_002_AStatementOutsideTheBoundIsMalformedAsync`, `ProcessingRecordsTests.API_CONV_002_AStatementIsRecordedTrimmedAndAnOmittedOneIsClearedAsync`. The spelling elsewhere: question 99.
- **Question 26** (`46968c21`; OPS-SEC-003 criterion 3, OPS-SEC-001 criterion 2, CONV-CODE-007). An unwrap under a version the ring does not hold reads the erased marker first, then throws a fault that carries the ring's own answer (`model.startup.secretunavailable`, the key and the version); the fault log writes its code and details; a command that meets one ends with exit 1 and the code; the start refuses where a live subject key stands under a version the source lacks. Tests: `PersonalFieldCipherTests.OPS_SEC_003_AC3_AKeyUnderARetiredVersionFailsWithANamedError`, `PersonalFieldCipherTests.OPS_SEC_003_AC3_AnErasedKeyUnderARetiredVersionReadsAsErased`, `FaultLogTests.OPS_SEC_003_AC3_AFaultThatCarriesACodeIsLoggedWithItsCodeAndDetails`, `FaultLogTests.BFF_ERR_002_AC2_AFaultThatCarriesNoCodeIsLoggedByItsTypeAlone`, `KeyMaterialTests.OPS_SEC_001_AC2_StartupFailsNamedWhereALiveSubjectKeyStandsUnderAVersionTheSourceLacksAsync`, `SubjectKeyStoreTests.OPS_SEC_001_AC2_TheVersionsReadAreThoseOfTheKeysThatAreNotErasedAsync`, `RegisterClientTests.OPS_SEC_003_AC3_AValueUnderAVersionTheDocumentLacksEndsTheCommandWithTheCodeAsync`. Not decided by a new test: "a request is answered `system.fault`" (the translation answers every exception so, held by the BFF-ERR-002 tests) and "a job fails its run" (the worker records a thrown fault through the fault log). Ledger: 317 "Superseded by D-166".
- **Question 33** (`ea174da9`; OPS-SEC-003, OPS-MIG-003a criterion 4, PRIV-RIGHT-005c). A sign-in in progress under a previous version counts as pending; the retirement deletes only unspent credit under a previous version and released username holds; the maintenance credential loses `DELETE` on the eight ledgers. Tests: `FingerprintRotationTests.OPS_SEC_003_AC6_RetirementWaitsWhileASignInInProgressCarriesThePreviousVersionAsync`, `FingerprintRotationTests.OPS_SEC_003_AC6_RetirementDeletesOnlyUnspentCreditAndReleasedHoldsAsync`, `DatabaseRoleTests.OPS_MIG_003a_AC4_TheMaintenanceRoleReachesTheFingerprintsAndNoOtherColumnAsync`. Ledger: 318 "Superseded by D-166".
- **Question 28** (`c972731e`; PRIV-BASIS-001, CONV-ENUM-001, `10` section 5.7). `LawfulBasisDeclaration` gains `Label`; the table `identity.lawful_bases`, written whole by a leading hosted service at the start, under a table lock; the model refuses a key named twice or an empty key or label with `model.startup.declarationinvalid`; the records of processing emit the label. Tests: `AuthorizationModelTests.PRIV_BASIS_001_AListNamingAKeyTwiceOrAnEmptyKeyOrLabelFailsStartup`, `DeclaredProcessingTests.PRIV_BASIS_001_TheShippedDeclarationCarriesTheDeclaredProperties`, `ProcessingRecordsTests.PRIV_BASIS_001_AC2_TheBasisColumnCarriesTheDeclaredLabelAsync`, `LawfulBasisSeedTests.PRIV_BASIS_001_AC6_TheStartWritesTheDeclaredListInOneTransactionAsync`, `LawfulBasisSeedTests.CONV_DESIGN_003_AC7_ATransactionThatFailsIsAFaultNamingItsCodeAsync`, `LawfulBasisStoreTests.PRIV_BASIS_001_AC6_AfterAStartTheTableHoldsExactlyTheDeclaredBasesAsync`, `LawfulBasisStoreTests.PRIV_BASIS_001_AC6_TwoStartsWithDifferentListsLeaveOneWholeListAsync`, `LawfulBasisStoreTests.PRIV_BASIS_001_OnlyTheRuntimeCredentialWritesTheTableAsync`, `StartupValidationTests.AUTHZ_MODEL_004_AC2_TheChecksStartBeforeEverythingElseRegistered`, `ModelTests.REG_ACCT_001_AC2_NoFieldExistsOutsideTheGroupsTheTableNames`.
- **Not built in the part:** question 30 (the consent and objection rows re-keyed to a row per grant, the grant while a live record stands) and question 29 (the consented resources: the view, the third source of the filter, the consent condition, its truth-table case), which depends on 30 and on question 22 of `part/authorization`. Neither was started; ledger lines 133 and 147 are not written. Both are taken up after `part/authorization` is merged.
- `ea174da9` and `c972731e` change `AuthorizationModel.cs` and no truth-table case (question 100). The check passes over the branch's range.
- The integration classes `PrivacyRequestStoreTests`, `SchemaContractTests`, `AccountStatesTests`, `SubjectKeyStoreTests`, `FingerprintRotationTests`, `DatabaseRoleTests`, `LawfulBasisStoreTests`, `MigrationRunTests`, `RegisterClientTests`, `KeyRotationTests`, `FingerprintKeyRotationTests`, `StartupValidationTests` and `BackgroundJobsTests` passed on the part. No parked site of questions 69 to 88 was touched.

#### D-183, `part/authorization`, merged as `330cb853` (`6cd14e2f`, `be757f8d`, `e467284d`, `1d80d9d1`, `10e7acb6`, `6e496a64`)

After the merge: build 0 warnings 0 errors, format clean, 3000 unit tests, 139 contract tests; `TruthTableTests` (84), `StartupValidationTests` (67), `SchemaContractTests` and `MigrationRunTests` passed on the merged tree. Two conflicts, each keeping both sides: `DeclarationCoverage.cs` (the relationship sources beside the photo check) and the usings of `TruthTableTests.cs`. One migration, `NameBothIdentitiesOnEveryAuditRecord`, hand-written throughout since `audit_records` is outside the model: it fails where a row holds a null identity, drops `ck_audit_records_identities` and sets both identity columns not null; the model has no change a migration does not carry, so nothing was regenerated.

- **Question 35** (`6cd14e2f`; AUTHZ-IMP-001, IDN-AUD-001, PRIV-BREACH-002). `AuditEntry` gains `Subject`, carried as `subject` by `GET /admin/audit`. Tests: `AuditStoreTests.IDN_AUD_001_AC4_TheTrailOfEitherReturnsTheSuspensionWithItsSubjectAsync`, `AuditTrailEndpointTests.PRIV_BREACH_002_AnEntryCarriesTheDataSubjectItConcernsAsync`.
- **Question 34** (`be757f8d`; AUTHZ-CONCEAL-004, AUTHZ-GATE-004, IDN-AUD-001, OPS-ALERT-001). A refusal of background work is recorded under the nil subject in both identities with the principal's name and reason; both identity columns are not null; refusals are counted by the principal's name where one acted; `ExplainedPrincipal` gains `Name` and `Reason`, carried on both explanation routes. Truth table: one row (a check by background work, which holds no grant). Tests: `ExplanationTests.AUTHZ_CONCEAL_004_AC1_ARefusalOfBackgroundWorkNamesThePrincipalAndItsReasonAsync`, `AuditStoreTests.IDN_AUD_001_AC1_ARefusalNamingNobodyIsRefusedByTheDatabaseAsync`, `ExplanationEndpointTests.AUTHZ_CONCEAL_004_AC3_ARefusalOfBackgroundWorkResolvesToItsNameAndReasonAsync`, `ExplanationEndpointTests.AUTHZ_GATE_004_AC3_TheCallerResolvesTheirOwnRefusalOverTheEndpointAsync`, `GateBehaviourTests.OPS_ALERT_001_AC1_APrincipalsRefusalsAreCountedByItsNameAsync`, `GateBehaviourTests.OPS_ALERT_001_AC1_ARunOfRefusalsNamingNoOneIsRaisedAsync`, `SchemaContractTests.LIB_API_001_AC2_TheLibraryOwnedSchemaIsTheContractAsync`. Ledger: 136 "Superseded by D-166".
  - For audit: the explanation of a principal's refusal shows null acting and effective identities, as `ExplainedPrincipal` documented before, while the trail's row holds the nil subject. The version before this one writes null identities for a principal's refusal, which the migrated table refuses (OPS-MIG-005 reads against that); D-166 136 orders the migration.
- **Question 59** (`e467284d`; AUTHZ-CONCEAL-004, CONV-DESIGN-002, CONV-DESIGN-003, OPS-ALERT-001). The refusal's record, its count and the denial-spike alert are one unit of work in a scope of the gate's own, under a hold on the actor's refusals; a raise that fails rolls back and is a fault. Truth table: one row (a check refused inside work the caller rolls back). Tests: `DenialRecordingTests.AUTHZ_CONCEAL_004_ARefusalIsRecordedCountedAndRaisedInOneUnitOfWorkAsync`, `DenialRecordingTests.AUTHZ_CONCEAL_004_ASpikeThatCannotBeRaisedFailsTheRecordAsync`, `DenialRecordingTests.CONV_DESIGN_003_AC7_AUnitOfWorkThatFailsIsAFaultNamingItsCodeAsync`, `GateBehaviourTests.AUTHZ_CONCEAL_004_AC4_ASpikeRaisedInsideATransactionThatRollsBackStandsAsync`, `GateBehaviourTests.CONV_DESIGN_003_AC6_TwoRefusalsAtOnceAreCountedOneAfterTheOtherAsync`.
- **Question 37** (`1d80d9d1`; OPS-MIG-003a, AUTHZ-MODEL-005). Tests alone; the migrations and the listing held as they stand. Tests: `DatabaseRoleTests.OPS_MIG_003a_AC5_TheLibrarysSchemaHoldsExactlyTheListedGrantsAsync`, `DatabaseRoleTests.OPS_MIG_003a_AC5_OutsideTheLibrarysSchemaTheRoleHoldsWhatARoleGrantedNothingHoldsAsync`.
- **Question 22, the second half of 265** (`10e7acb6`, `6e496a64`; LIB-HOST-001, AUTHZ-DERIVE-005, AUTHZ-DERIVE-007, AUTHZ-GRANT-003, INF-BG-001).
  - `RelationshipSource` and `RelationshipSource.Of<TContext, TRow>` are public; the start refuses a derivation without its source (`model.startup.declarationmissing`) and a malformed source (`model.startup.declarationinvalid`), building the context's model and running no query; `GET /admin/access` answers derived grants through the declared sources inside the budget.
  - The job `derivation-driftcheck` evaluates each materialised derivation in one statement, refreshes each record that differs under its principal in one unit of work, and raises the degradation there where a refresh changed something.
  - Truth table: three rows (a lookup of a record a fact in the host's data reaches; a fact no grant was materialised for, after the drift check; a materialised grant the data no longer supports, after it).
  - Tests: `RelationshipSourceTests.LIB_HOST_001_ASourceHandsTheRowsOfOneContextInstanceToItsReader`, `RelationshipSourceTests.LIB_HOST_001_ASourceNamingNoRelationshipOrNoRowsIsNotMade`, `StartupValidationTests.AUTHZ_DERIVE_005_AC5_ADerivationWithoutItsRelationshipSourceIsRefusedAsync`, `StartupValidationTests.AUTHZ_DERIVE_005_AC5_AMalformedRelationshipSourceIsRefusedNamingItAsync`, `ReverseLookupTests.AUTHZ_DERIVE_007_AC1_StoredAndDerivedGrantsAreReportedDistinctlyAsync`, `ReverseLookupTests.AUTHZ_DERIVE_007_AC2_PastTheBudgetTheAnswerIsPartialAndNamesWhatWentUnevaluatedAsync`, `ReverseLookupTests.AUTHZ_DERIVE_005_AC6_TheViewEvaluatesADerivationInOneStatementAsync`, `ReverseLookupTests.AUTHZ_DERIVE_007_ADerivedGrantWhoseRoleAllowsNothingIsNotReportedAsync`, `ReverseLookupTests.AUTHZ_DERIVE_007_WithoutTheHostsRowsARecordADerivationReachesIsRefusedAsync`, `AccessEndpointTests.AUTHZ_DERIVE_007_AC1_ADerivedGrantIsAnsweredWithNoIdentifierAsync`, `MaterialisationTests.AUTHZ_DERIVE_005_AC4_TheDriftCheckCorrectsWhatTheHostsRowsNoLongerSupportAsync`, `MaterialisationTests.AUTHZ_DERIVE_005_AC4_ADriftCheckThatFindsNoDifferenceRaisesNothingAsync`, `MaterialisationTests.AUTHZ_DERIVE_005_AC6_TheDriftCheckEvaluatesADerivationInOneStatementAsync`, `MaterialisationTests.AUTHZ_DERIVE_005_TheRefreshTakesTheDriftChecksPrincipalAndNoOtherAsync`, `GrantStoreTests.AUTHZ_DERIVE_005_AC4_TheLiveMaterialisedGrantsAreReadInEveryOrganizationAsync`.
  - Ledger: 265 "Superseded by D-166".
  - Not built: the audit record of a grant the drift check writes (question 101); the consented resources in the source's start check, which arrive with question 29.
  - Observed: `IAccessGate.WhoCanAccessAsync` with the rows handed in still stands beside the declared-source path; no chapter says whether it stays.
- No parked site of questions 69 to 88 was touched; `AccessGate`'s registration (question 83) is where it was.

#### D-183, `part/consent`, merged as `6ad5c978` (`c3191b69`)

After the merge: build 0 warnings 0 errors, format clean, 3012 unit tests, 139 contract tests; `TruthTableTests` (84) passed on the merged tree. One migration, `KeepARecordForEachGrant`; the snapshot merged without conflict and the model has no change a migration does not carry, so nothing was regenerated. Hand-written in it: `id` added nullable, filled for the existing rows in SQL (a version 7 value from the row's instant), then set not null before the keys are added. Its `Down` restores the key on subject and purpose and deletes nothing, so it fails where a subject holds two records of one purpose (question 104).

- **Question 30** (`c3191b69`; PRIV-CONS-001 criteria 4 and 6, PRIV-RIGHT-001a criterion 6; D-166 133 and 147 (5)). `consents` and `objections` are keyed on an identifier, a record for each grant and objection, with a unique index on the live consent and on the standing objection of a subject and purpose. The store's port adds and stamps and never overwrites. A grant over a live record the purpose admits writes and raises nothing; over one it no longer admits, the live record is stamped superseded and the new one added in the same transaction, `ConsentChanged` superseded then granted; an objection while one stands records nothing. The supersession announces only a consent it stamped; the gate reads the standing record (the live one, else the latest). No public signature changed.
  - `ConsentTests`: `PRIV_CONS_001_AC4_AGrantAfterAWithdrawalAddsARecordAndLeavesTheEarlierAsItWasAsync`, `PRIV_CONS_001_AC4_AGrantAfterASupersessionAddsARecordAndLeavesTheEarlierAsItWasAsync`, `PRIV_CONS_001_AC6_AGrantOverALiveRecordThePurposeAdmitsChangesNothingAsync`, `PRIV_CONS_001_AC6_AGrantMeetingARecordWrittenMeanwhileAddsNoneAsync`, `PRIV_CONS_001_AC6_AGrantFindingARecordWrittenWhileItWaitedAddsNoneAsync`, `PRIV_CONS_001_AC6_AGrantOverALiveRecordAgainstAnotherDocumentSupersedesItAndAddsOneAsync`, `PRIV_CONS_001_AC6_AReplacingGrantWhoseEventCannotBeWrittenRollsBackAsync`, `PRIV_CONS_001_AnAdministratorGrantOverALiveRecordNoLongerAdmittedIsRecordedAsNamedAsync`, `PRIV_CONS_001_AGrantOverALiveRecordOfAKindThePurposeDoesNotAdmitSupersedesItAsync`, `PRIV_RIGHT_001a_AC6_ObjectingAgainWhileAnObjectionStandsRecordsNothingAsync`, `PRIV_RIGHT_001a_AC6_AnObjectionMeetingOneRecordedMeanwhileRecordsNothingAsync`, `PRIV_RIGHT_001a_AC2_AnObjectionAfterAWithdrawalIsARecordOfItsOwnAsync`.
  - `ConsentStoreTests`: `PRIV_CONS_001_AC1_AConsentReadsBackEveryFieldItWasWrittenWithAsync`, `PRIV_CONS_001_AC1_AConsentAddedWithItsAccountInOneUnitOfWorkIsHeldAsync`, `PRIV_CONS_008_AC4_WithdrawalKeepsTheRecordAndTimestampsItAsync`, `PRIV_CONS_001_AC4_AGrantAfterAWithdrawalKeepsTheWithdrawnRecordAsync`, `PRIV_CONS_001_AC4_ASupersessionAndAWithdrawalEachStampTheLiveRecordAsync`, `WithdrawConsent_OverASupersededRecord_StampsItOnceAsync`, `PRIV_CONS_001_AC4_AtMostOneLiveRecordASubjectAndPurposeExistsAsync`, `PRIV_CONS_001_AC6_TwoGrantsAtOnceAddOneRecordAsync`, `PRIV_CONS_007_AC1_OnlyLiveConsentsAgainstAnEarlierVersionAreFoundAsync`, `PRIV_CONS_007_AC1_ALiveConsentAgainstAnotherDocumentIsFoundAsync`, `PRIV_RIGHT_001a_AC1_AnObjectionReadsBackAndItsWithdrawalIsTimestampedAsync`, `PRIV_RIGHT_001a_AC6_AnObjectionWhileOneStandsIsNotAddedAsync`, `PRIV_CONS_008_AC5_TwoWithdrawalsAtOnceWithdrawOnceAsync`, `PRIV_CONS_001_AC4_ARecordWrittenBeforeTheKeyStaysUnderAnIdentifierOfItsOwnAsync`.
  - `ConsentGateTests`: `PRIV_CONS_001_AC4_AConsentGivenAgainAfterAWithdrawalAdmitsTheActionAsync`, `PRIV_CONS_001_AC4_WhereNoRecordIsLiveTheLatestDecidesTheRefusalAsync`.
  - The 204 for a grant over an admitted live record is asserted at the service; the endpoint maps success as before.
  - Ledger: no line for 133 or 147, which also cover question 29.
  - Two more sites of questions 77 and 88, left committing: `ConsentService.GrantAsync` where it finds an admitted live record written meanwhile, and `ConsentService.ObjectAsync` where it meets an objection recorded meanwhile.
  - The integration classes `ConsentStoreTests`, `MigrationRunTests`, `SchemaContractTests`, `ModelTests`, `ConsentGateTests` and `TruthTableTests` passed on the part.

#### D-183, `part/sending`, merged as `9be2801f` (`c43b3c2c`, `1874f506`, `79dfdb2a`, `af618c88`, `c813a1b4`, `e8f32416`)

After the merge: build 0 warnings 0 errors, format clean, 3035 unit tests, 139 contract tests; `SchemaContractTests`, `MigrationRunTests`, `ModelTests`, `SubjectEraserTests`, `SignInFlowTests`, `RegistrationFlowTests`, `RegistrationWizardTests`, `ThrottlingTests`, `InvitationServiceTests` and `BackgroundJobsTests` passed on the merged tree. Three conflicts: the changelog (both sides kept), the constructor of `InvitationAcknowledgement` (the session store of question 36 beside the governed send) and `SignInLinks.SendSecondStepAsync` (the answer of the `52482ed5` correction with the send inside the unit of work). Three migrations: `GovernSendsFromAdmission`, `KeepTheReferenceHashOfAnOutboxRow` (hand-written: the reference hash of the rows that stand), `ClaimARaisedAlertBeforeItIsCarried`. The snapshot merged without conflict and the model has no change a migration does not carry, so nothing was regenerated.

- **Question 60** (`c43b3c2c`; REG-IDENT-007, AUTH-ABUSE-004). The confirmation asked of the displaced address goes under `verification`. Tests: `IdentifierServiceTests.REG_IDENT_007_AC5_TheConfirmationIsAskedUnderTheVerificationPurposeAsync`, `SendingGovernanceTests.AUTH_ABUSE_004_AC15_ALinkAPersonAskedForAnswersToNoNotificationRestrictionAsync`.
- **Question 23** (`1874f506`; OPS-OBS-002, REG-SESS-003). `RegistrationSignals` raises `degradation` naming `registration-channel` where a wait begins on a channel it had opened and no longer listens on, and reopens it. Test: `RegistrationSignalsTests.OPS_OBS_002_ALostChannelIsRaisedAsADegradationAsync`. Ledger: 143 "Superseded by D-166".
- **Question 64** (`79dfdb2a`; OPS-ALERT-004a). A destination change holds the setting's row after its notice, reads the value in force again and refuses `config.change.superseded` (409) where another change overtook it, rolling back. Test: `AlertDestinationChangeTests.OPS_ALERT_004a_AC7_AChangeOvertakenAfterItsNoticeIsRefusedAsSupersededAsync`. Criterion 7, "two changes at once", is held by a hook that changes the value at the hold, not by two transactions.
- **Questions 27, 38 and 63, with D-166 118, 119 (1) to (4), 227, 322, 235 and 335** (`af618c88`; AUTH-ABUSE-004, AUTH-ABUSE-003, CONV-DESIGN-002, CONV-DESIGN-003, CONV-LAYOUT-002, LIB-API-001, LIB-EXT-001, IDN-ATTR-001, IDN-LIFE-009a, AUTH-FACT-008, INF-BG-001).
  - `IGovernedSend.UndertakeAsync` judges and admits a message inside the caller's unit of work and begins none; a send outside one is a fault. The attempt is registered on the unit of work and runs after the outermost commit (`IUnitOfWork.AfterCommit`); the handler is called outside any transaction, under a claim, and bounded by the claim's timeout. `INotificationHandler` takes the admitted message. `SendingService` is removed.
  - The ledger holds every candidate counter in one order for the judgement; a retried send is judged with its own count set aside; a send that fails for good gives its count and its credit back.
  - A text owed in every declared language is one row and one reference per language, admitted all or none.
  - The invitation's issue, the recovery links and notices, the sign-in link and codes, the credential notices, the no-account draw and the privacy notices send inside their unit of work. `NotificationRequested` is emitted by nothing.
  - `SendingGovernanceTests`: `AUTH_ABUSE_004_AReplacedHandlerIsStillGovernedAsync`, `AUTH_ABUSE_004_AC8_AMessageUndertakenInARolledBackOperationIsNeverCarriedAsync`, `AUTH_ABUSE_004_AC8_AMessageIsCarriedOnlyAfterTheOutermostCommitAsync`, `AUTH_ABUSE_004_AC16_ASendCountsFromItsAdmissionAndIsReleasedWhereItFailsForGoodAsync`, `AUTH_ABUSE_004_AC1_ASendInEveryDeclaredLanguageIsJudgedOnceAsync`, `AUTH_ABUSE_004_AC15_ALinkAPersonAskedForAnswersToNoNotificationRestrictionAsync`, `AUTH_ABUSE_003_AC7_AnAskIsAnsweredBeforeAnyTransportIsCalledAsync`, `AUTH_ABUSE_004_AC9_ARetryIsJudgedByTheRestrictionsAgainAsync`, `AUTH_ABUSE_002_AC3_ADrawCountsAsTheMessageWouldAndCarriesNothingAsync`, `D_022_TheMessageIsWrittenToTheOutboxAndRemovedOnceTakenAsync`, `D_022_ATransportRefusalLeavesTheMessageRecordedAsync`, `D_022_ARefusedMessageIsCarriedOnceItsRetryIsDueAsync`, `D_022_AMessageWhoseBudgetIsSpentIsRemovedAndRaisesDegradationAsync`, `CONV_DESIGN_003_AC5_ASpentBudgetWhoseAlertCannotBeRaisedRollsBackAsync`, `IDN_ATTR_001_AC3_NoKnownLanguageGoesOutInEveryDeclaredOneAsync`, `IDN_ATTR_001_ALanguageTheTransportRefusedLeavesTheMessageRecordedAsync`, `IDN_ATTR_001_ARetryCarriesOnlyTheLanguagesStillOwedAsync`.
  - `SendLedgerTests`: `AUTH_ABUSE_004_AC16_SendsJudgedAtOnceAreJudgedOneAfterTheOtherAsync`, `AUTH_ABUSE_004_AC16_ARetriedSendIsJudgedWithItsOwnCountSetAsideAsync`, `AUTH_ABUSE_004_AC16_AReleasedSendGivesBackTheCreditItSpentAsync`, `AUTH_ABUSE_004_AC6_ACounterThatCountedNothingGoesWithTheNextSweepAsync`.
  - `SendOutboxTests`: `IDN_PRIN_003_AMessageTakenLeavesNoRowAsync`, `D_022_AnAttemptReadsBackAsItWasRecordedAsync`, `D_022_OnlyAMessageWhoseAttemptIsDueIsReadAsync`.
  - `UnitOfWorkTests`: `CONV_DESIGN_002_AC5_WhatIsRegisteredRunsAfterTheOutermostCommitAsync`, `CONV_DESIGN_002_AC5_ARollbackDiscardsWhatWasRegisteredAsync`, `CONV_DESIGN_002_ARegistrationOutsideAUnitOfWorkIsAFaultAsync`.
  - `InvitationServiceTests`: `IDN_LIFE_009a_ALinkThatCouldNotBeSentIssuesNothingAsync`, `IDN_LIFE_009a_AnIssueThatDoesNotCommitSendsNothingAsync`, `IDN_LIFE_009a_TheLinkIsCarriedOnceTheIssueHasCommittedAsync`.
  - `RecoveryCodeRemindersTests.AUTH_FACT_008_AC5_TwentySetsDueTogetherAreEachRemindedUnderTheShippedRestrictionsAsync`, `RecoveryCodeRemindersTests.AUTH_FACT_008_AC5_ASetWhoseEveryNoticeIsRefusedStaysOwedAsync`; `ThrottlingTests.AUTH_ABUSE_003_AC7_AnAskIsAnsweredBeforeTheTransportIsCalledAsync`.
  - Names D-166 gives that differ: the `SendingServiceTests.D_022_AMessage...` names are the two `AUTH_ABUSE_004_AC8` tests; `ThrottlingTests.AUTH_ABUSE_003_AC2_ALinkAskIs...` is the `AC7` test; `IDN_LIFE_009a_ALinkTheTransportRefusesIsCarriedLaterAsync` is `IDN_LIFE_009a_TheLinkIsCarriedOnceTheIssueHasCommittedAsync` with `D_022_ARefusedMessageIsCarriedOnceItsRetryIsDueAsync`.
  - Not decided as written: AUTH-ABUSE-004 criterion 16, "several sends judged at once", is held against PostgreSQL at the ledger's hold and over fakes above it; no test runs the whole governed send concurrently over a database. CONV-DESIGN-003 criterion 9, "two processes", is held by concurrent claims on one database.
  - Ledger: 118, 119, 227, 235, 322 and 335 "Superseded by D-166".
  - Questions 107 to 112 follow from it.
- **Question 39, with D-166 119 (6)** (`c813a1b4`; PRIV-RIGHT-005a, PRIV-RIGHT-005, AUTH-ABUSE-004). The erased value is 32 zero bytes, refused before the unwrap; the mailbox's release writes it; the erasure overwrites the wrapped key of the subject's outbox rows in its transaction; the publisher removes an erased row uncarried and releases its count. Tests: `SubjectEraserTests.PRIV_RIGHT_005_AC1_AnOutstandingMessageIsUnreadableAndUncarriedAfterErasureAsync`, `PersonalFieldCipherTests.PRIV_RIGHT_005a_TheErasedValueIsThirtyTwoZeroBytesAndIsRefusedBeforeTheUnwrap`, `SendingGovernanceTests.PRIV_RIGHT_005a_AMessageWhoseKeyWasErasedIsRemovedUncarriedAndItsCountReleasedAsync`, `SendingGovernanceTests.AUTH_ABUSE_004_AC9_ASendThatHoldsNoCountIsJudgedBeforeItIsCarriedAsync`, `SendOutboxTests.AUTH_ABUSE_004_ARowWrittenBeforeItCarriedAReferenceReadsWithOneOfItsOwnAsync`. The invitation's erased key: question 113.
- **Question 61** (`af618c88`, `e8f32416`; CONV-DESIGN-003, INF-BG-001, OPS-ALERT-001). `outbox.claim.timeout` (PT2M, floor PT30S, ceiling PT10M). Built for two carriers of five: the send outbox and the raised alerts, each claimed by one conditional update, its outcome written under the claim. Tests: `SendOutboxTests.CONV_DESIGN_003_AC9_ARowIsClaimedByOneAttemptUntilItsClaimTimesOutAsync`, `SendOutboxTests.CONV_DESIGN_003_AC9_AnOutcomeWhoseClaimWasTakenOverChangesNothingAsync`, `SendingGovernanceTests.CONV_DESIGN_003_AC9_AnAttemptThatDoesNotHoldTheClaimRecordsAndCarriesNothingAsync`, `RaisedAlertsTests.CONV_DESIGN_003_AC9_ARaisedConditionIsClaimedByOnePassAsync`, `RaisedAlertsTests.OPS_ALERT_001_AC1_ACarriedConditionIsRemovedAsync`, `AlertDispatchTests.CONV_DESIGN_003_AC9_AConditionAnotherPassHoldsIsNotCarriedAsync`.
  - **Not built in the part:** the claim of the event rows (`EventPublisher`), of the erasure outbox's deliveries (`OutboxPublisher`) and of the mailbox pushes (`MailboxPublisher`). CONV-DESIGN-003 criterion 9 and INF-BG-001 criterion 4 hold for messages and raised alerts alone until a further part builds them.
- Parked sites touched: question 75 (`RecoveryCodeReminders`), question 80 (`AccountLifecycle`, `IdentifierService`, `InvitationAcknowledgement`, `MembershipEnd`, `AppPasswords`) and question 88 (`ProviderEvents`): the send's type alone. The send begins no unit of work now, so it marks no caller's; what each caller does with a refused send is as it was. `RecoveryCodeReminders.RemindedAsync`, where every channel refuses, commits having written counters alone (question 77's matter). `PhoneSignals.ConsiderAsync` still begins a level of its own inside the caller's unit and throws on failure.
- For audit: `Deployment`, the fixture of `Janus.Hosting.Tests`, runs the publisher's pass after each request by default, as the worker does, so that the flow tests find the mail of an ask; a test of what the request itself did turns it off. A caller of the governed send outside a unit of work on a path no test reaches would fault at run time; the full gate has not run yet.

#### D-183, `part/carriers`, merged as `17583e8f` (`0be8db67`)

After the merge: build 0 warnings 0 errors, format clean, 3039 unit tests, 139 contract tests. One migration, `ClaimAMailboxPushBeforeItIsCarried` (one nullable column, no hand-written SQL); nothing was regenerated.

- **Question 61, the mailbox pushes** (`0be8db67`; CONV-DESIGN-003, INF-BG-001, INT-MAIL-007). The publisher claims only a mailbox it has something to do for, in a unit of work of its own; reads the row again as it stands under the claim and decides on that; writes the counted attempt under the claim; calls the server outside any transaction, bounded by `outbox.claim.timeout`; writes the outcome and its alert under the claim and gives the claim up. An outcome whose claim was taken over writes and raises nothing. The claimed writes set the push's columns alone, where the pass wrote the whole aggregate from its earlier read.
  - `MailboxStoreTests`: `CONV_DESIGN_003_AC9_AMailboxPushIsClaimedByOnePassAsync`, `CONV_DESIGN_003_AC9_AnOutcomeWritesThePushAndNothingElseAsync`, `PRIV_RIGHT_005a_AC19_ARemovalConfirmedUnderAClaimForgetsTheAddressAsync`.
  - `MailboxPublisherTests`: `CONV_DESIGN_003_AC9_AMailboxAnotherPassHoldsIsNotPushedAsync`, `CONV_DESIGN_003_AC9_AnOutcomeWhoseClaimWasTakenOverIsNotRecordedAsync`, `CONV_DESIGN_003_APushAbandonedAtItsClaimsTimeoutIsAFailedAttemptAsync`, `CONV_DESIGN_003_AMailboxWithNothingDueIsNotClaimedAsync`.
  - Four existing tests of `MailboxPublisherTests` expect one commit more, the claim's own: `INT_MAIL_007_AC1_APushIsWrittenDownBeforeItLeavesAsync`, `INT_MAIL_007_AC1_EachAttemptIsCountedBeforeItIsMadeAsync`, `INT_MAIL_001_AC4_AConflictIsMarkedFailedAndRaisedOnItsFirstAttemptAsync`, `CONV_DESIGN_003_AC5_AFailedPushWhoseAlertIsNotRaisedIsRolledBackAsync`.
  - Not decided by a test: the timer that abandons a push at the claim's timeout (read; the test holds what the publisher does with a call that ends cancelled). INF-BG-001 criterion 4 with two processes is held by the storage test over separate connections.
- **Not built:** the claim of the event rows and of the erasure outbox, parked on question 114. Both still read due rows and carry them unclaimed.
- Question 77's matter: where a claim was taken over, or a claimed row has nothing to write, the publisher commits a unit of work that wrote nothing, as `AlertDispatch` and `SendPublisher` do.
- Observed in `af618c88`, not changed: `SendDeliveryStore.ClaimAsync` is conditional on the claim alone, not on the row still being due, and `SendPublisher` does not read the schedule again after it claims. A pass that read a row as due and claims it after another pass failed an attempt and released it carries it again at once: each attempt is counted once, and one interval of the backoff is skipped. It is stated under question 114.

#### D-183, `part/consented-resources`, merged as `4d9a3275` (`ffa77d18`, `e20b2543`, `7903e534`, `b5fd0791`)

After the merge: build 0 warnings 0 errors, format clean, 3054 unit tests, 139 contract tests; `TruthTableTests`, `StartupValidationTests`, `ConsentGateTests`, `SchemaContractTests`, `MigrationRunTests` and `DatabaseRoleTests` passed on the merged tree. One conflict, the changelog, both sides kept. One migration, `AddConsentedResources`, hand-written throughout (the view and its grant to the runtime credential; the model does not change); nothing was regenerated.

- **Question 29, whole** (D-166 133 and 147 (1) to (4); AUTHZ-GATE-002 criterion 4, PRIV-SENS-002 criterion 5, PRIV-CONS-007 criterion 5, AUTHZ-TEST-001, LIB-HOST-001, LIB-HOST-002, LIB-API-001).
  - `ffa77d18`: the view `identity.consented_resources` (resource type, resource, purpose, document, kind) over the live consents.
  - `e20b2543`: `ConsentedResource`, public, mapped by `MapAuthorizationTables`; `FilterSources<TResource>` takes it as a third required source; `PermissionRule` adds the one consent condition to the expression and to the fragment from one required consent; the single check and the capability page answer `privacy.consent.superseded` for a live consent against another document than the purpose names; the conformance suite counts the record as a contract table. Truth table: the table `Consents`, ten cases, each through the check, the expression and the fragment.
  - `7903e534`: the start refuses a relationship source whose context does not map the consented resources.
  - `b5fd0791`: at the start, one statement stamps every live consent recorded against another document than its purpose names, and each is announced, in one unit of work (`DocumentSupersessionStartService`, a leading hosted service).
  - Tests: `ConsentedResourcesTests.AUTHZ_GATE_002_AC4_ARecordOfAConsentingSubjectIsListedWithTheDocumentAndKindAsync`, `ConsentedResourcesTests.AUTHZ_GATE_002_AC4_ARecordWithoutAConsentingSubjectIsNotListedAsync`, `ConsentedResourcesTests.PRIV_SENS_002a_AC2_AWithdrawalTakesTheRecordOutOfTheViewAsync`, `ConsentedResourcesTests.PRIV_CONS_007_AC5_ASupersededConsentTakesTheRecordOutUntilTheNextGrantAsync`, `TruthTableTests.AUTHZ_GATE_002_AC4_EveryConsentCaseDecidesTheSameWayThroughBothPathsAsync` (ten cases), `PermissionRuleTests.AUTHZ_GATE_002_AC4_TheFragmentAsksTheConsentByParameter`, `PermissionRuleTests.AUTHZ_GATE_002_AC4_AnOrdinaryConsentAsksNoKind`, `PermissionRuleTests.AUTHZ_GATE_002_AC4_ARuleBoundToNoConsentReadsNoConsentedResource`, `PermissionRuleTests.AUTHZ_GATE_002_AC4_TheExpressionAdmitsOnlyAConsentedRecord`, `GateBehaviourTests.PRIV_SENS_002_AC1_AListAdmitsOnlyTheRecordsWhoseSubjectsConsentedAsync`, `GateBehaviourTests.PRIV_SENS_002a_AC2_AWithdrawalRemovesTheRecordFromTheNextListAsync`, `ConsentGateTests.PRIV_CONS_007_APurposeGivenAnotherDocumentAsksItsSubjectsAgainAsync`, `StartupValidationTests.AUTHZ_DERIVE_005_AC5_AMalformedRelationshipSourceIsRefusedNamingItAsync` (a case more), `StartupValidationTests.AUTHZ_MODEL_004_AC2_TheChecksStartBeforeEverythingElseRegistered`, `StartupValidationTests.PRIV_CONS_007_AC5_AStartStampsAConsentAgainstAnotherDocumentOnceAsync`, `DocumentSupersessionTests.PRIV_CONS_007_AC5_AStartStampsAConsentAgainstAnotherDocumentAndAnnouncesItAsync`, `DocumentSupersessionTests.PRIV_CONS_007_AC5_ASecondStartStampsAndAnnouncesNothingAsync`, `DocumentSupersessionTests.PRIV_CONS_007_AC5_AStartTouchesNoConsentAgainstTheDocumentItsPurposeNamesAsync`, `DocumentSupersessionTests.PRIV_CONS_007_AC5_AConsentThatIsNotAnnouncedRollsTheStartBackAsync`, `DocumentSupersessionTests.CONV_DESIGN_003_AC7_ATransactionThatFailsIsAFaultNamingItsCodeAsync`, `ConsentStoreTests.PRIV_CONS_007_AC5_OneStatementStampsEveryLiveConsentAgainstAnotherDocumentAsync`, `ConsentStoreTests.PRIV_CONS_007_AC5_TwoStartsAtOnceStampEachConsentOnceAsync`.
  - Not decided by a test: AUTHZ-GATE-002 criterion 1 for the consent condition, "neither rendering written separately" (read: both renderings take the one required consent the rule was built with). PRIV-CONS-007 criterion 5, "however many processes start": held by two transactions running the statement at once and by two starts of a host one after the other; two processes starting at one instant were not run. D-166's "`PublicSurfaceTests` lists the new record": that class holds no list of types and reads every public type.
  - Ledger: 133 and 147 "Superseded by D-166".
  - Question 77's matter: the start's stamping commits where it stamped nothing, every start after the first.
  - For audit: the start stamps by document alone; a live ordinary consent where the purpose now asks a written one is refused by the gate and the lists with `privacy.consent.writtenrequired` and is not stamped.

#### D-183, `part/registration-codes`, merged as `3e59cdeb` (`7a64685a`, `bae7b728`, `02ff4f0e`)

After the merge: build 0 warnings 0 errors, format clean, 3075 unit tests, 139 contract tests; `RegistrationFlowTests`, `RegistrationWizardTests`, `CredentialFlowTests` and `AccountFlowTests` passed on the merged tree. One conflict, the changelog, both sides kept. No migration.

- **Question 42, whole** (`7a64685a`; D-166 129 (1); REG-SESS-006, REG-SESS-001 criterion 5). `CredentialAuthority.Of(RegistrationSessionId)`; the open ceremony and the unconfirmed generator are held in the session's encrypted document; the four enrolment operations go to the registration where the authority is a registration session; the terms step writes a confirmed generator alone; the four enrolment endpoints resolve the authority from the registration cookie where no session asks.
  - `RegistrationServiceTests`: `REG_SESS_006_AC1_APasskeyCreatedAgainstTheSessionCompletesTheStepAsync`, `REG_SESS_006_AC4_AGeneratorConfirmedBesideAPasswordShowsRecoveryCodesAsync`, `REG_SESS_006_TheSessionEnrolsOnlyAtTheSecurityStepAsync`, `REG_SESS_001_AC5_ACeremonyAndAGeneratorBegunAreHeldOnTheSessionAloneAsync`, `REG_SESS_001_AC5_TheTermsStepWritesOnlyAConfirmedGeneratorAsync`, `REG_SESS_001_AC5_AnAbandonedOrExpiredSessionLeavesNeitherBehindAsync`, `REG_SESS_001_ACeremonyIsSpentOrReplacedUnderTheSessionAsync`, `REG_SESS_001_AGeneratorIsSpentByTheCodeThatConfirmsItAsync`.
  - `RegistrationFlowTests`: `REG_SESS_006_AC1_APasskeyAloneCompletesTheSecurityStepOverTheWireAsync`, `REG_SESS_006_AC4_AGeneratorBesideAPasswordShowsRecoveryCodesOverTheWireAsync`, `REG_SESS_006_TheRegistrationSessionEnrolsAtNoEarlierStepAsync`.
  - `RegistrationSessionStoreTests.REG_SESS_001_AC5_AnOpenCeremonyAndABegunGeneratorAreKeptEncryptedOnTheSessionAsync`.
  - Not decided by a test: criterion 5, "under the new account's subject key" (read: the terms step writes through the authenticator store as before; the unit test asserts the subject).
  - Ledger: 129 "Superseded by D-166".
  - For audit: D-166 129 has the authority "resolved in Asking ... while the session's step is security or terms"; a credential endpoint may not call `IRegistration` (LIB-API-005), so the endpoint resolves it from the cookie and the step is judged in the service, which answers `auth.session.expired` outside those steps. The staged generator carries an identifier beside its label and secret, which the confirming endpoint names. The ceremony's lifetime is `code.verification.lifetime`, as the account's is; the key's display name is empty.
- **Question 31, in part** (`bae7b728`, `02ff4f0e`; D-166 115 (2); AUTH-FACT-004, REG-SESS-005, AUTH-ABUSE-004, REG-SESS-003, REG-IDENT-004, REG-IDENT-007).
  - Registration: the codes are issued, presented and ended through the verification-code record; a held or reserved value takes a record no code answers, and its ask and resend are counted against the restrictions as the message would be and refused alike.
  - Account identifiers: the code of an add or a replace is issued, presented, shown and ended through the record; the staged identity carries no code of its own. A code outstanding at deployment stops verifying and a resend replaces it.
  - Changed answer: after the attempt cap, and where no code is outstanding, `auth.code.expired` where it was `auth.code.invalid`.
  - Tests: `RegistrationServiceTests.REG_SESS_005_AC5_ACodeForAHeldOrReservedAddressIsAnsweredAsAWrongOneForAFreshAddressAsync`, `RegistrationServiceTests.REG_SESS_005_AC5_NoCodeVerifiesAHeldAddressAndItsRecordRunsOutAsACodeDoesAsync`, `RegistrationServiceTests.AUTH_ABUSE_004_AC14_AnAskForAHeldOrReservedAddressIsCountedAsItsMessageWouldBeAsync`, `RegistrationServiceTests.AUTH_ABUSE_004_AC14_AnAskForAHeldAddressIsRefusedByTheRestrictionsAlikeAsync`, `VerificationCodesTests.AUTH_FACT_004_ARecordNoCodeMatchesIsAnsweredAndSweptAsACodeIsAsync`, `RegistrationFlowTests.REG_SESS_005_AC5_ACodeForAHeldAddressIsAnsweredInTheBytesOfAWrongOneAsync`, `IdentifierServiceTests.AUTH_FACT_004_AC3_TheCapEndsTheCodeOfAnAddedIdentifierAsync`, `IdentifierServiceTests.AUTH_FACT_004_TheCodeThatVerifiesAnIdentifierIsSpentAsync`.
  - Existing tests changed, for audit: the two cap tests expect five invalid answers and then expired; the wrong-code tests read the count from the code record; seven tests of a refused send assert no outermost commit where they asserted no commit, since the code's issue and presentation commit a nested level inside the unit that rolls back; `ProviderSignInTests.REG_IDENT_008_AC4_AnAddressAnotherAccountHoldsAnswersAsAFreshOneAsync` advances the clock before the second ask, which now draws on the restrictions; two storage tests of wrong tries counted at once on the session's and the verification's rows, whose counter is gone, are renamed and keep the row's lock (`PendingVerificationStoreTests.REG_IDENT_004_AValueProvedAtOnceIsProvedOnceAsync`, `RegistrationSessionStoreTests.REG_SESS_003_AnIdentifierVerifiedAtOnceIsVerifiedOnceAsync`); the count at once is held by `VerificationCodesTests.AUTH_FACT_004_AC3_ConcurrentWrongTriesAreAllCountedAsync`.
  - Not decided by a test: REG-SESS-005 criterion 1's timing (one path through the presentation, a fixed-time comparison; the wire test asserts identical bytes).
  - **Not built:** the record for a held or reserved value at an account identifier's add and replace (question 115); the whole of D-166 306, the sweep of pending verifications, its schedule and its three tests (question 116), so REG-IDENT-004 criterion 4 and REG-IDENT-007 criterion 4 are not met. No ledger line for 115 or 306.
- Parked sites: question 73 (`VerificationCodes.PresentAsync`) is unchanged and is now also called, nested, from the registration's and the identifier's verification; question 81 keeps its shape, the try's count now on the code record; questions 78 and 79 are untouched.

#### D-183, `part/endpoints`, merged as `88174fd5` (`d88c1c72`, `0c20ff61`, `28c53a12`)

After the merge: build 0 warnings 0 errors, format clean, 3085 unit tests, 139 contract tests. No migration. No permission logic changed.

`28c53a12` fails `ProductNameTests.CONV_NAME_001_AC2_TheProductNameAppearsOnlyInNamespacesIdentifiersAndTheEntryPoint` on its own: one line of `EndpointDeclarationTests` wrote the product name as text. The part was built in a worktree, where that test fails for another reason, and the failure was not seen. The merge commit corrects the line (the name is read from a namespace, as `KeyMaterialTests` reads it), so the merge is green and that one commit is not.

- **The codes** (`d88c1c72`; AUTH-FACT-002b, AUTH-FACT-008). `auth.factor.passwordrequired` and `auth.factor.notenrolled`, each 409. A second step, a code generator and a recovery-code set asked for without a password answer the first, where they answered `auth.factor.notpermitted`; the record of a shown set with no set answers the second, where it answered `auth.factor.rejected`. Tests: `CredentialServiceTests.AUTH_RECOV_006_AC4_APasskeyOnlyAccountIsOfferedNoCodesAsync`, `CredentialServiceTests.AUTH_FACT_002b_ASecondStepIsRefusedWithoutAPasswordAsync`, `TotpServiceTests.AUTH_FACT_002b_AC2_ACodeGeneratorIsRefusedWithoutAPasswordAsync`, `WebAuthnServiceTests.AUTH_FACT_002b_AC2_ASecondStepIsRefusedWithoutAPasswordAsync`, `RecoveryCodeServiceTests.ShownAsync_AnAccountHoldingNoSet_IsRefusedAsNotEnrolledAsync`, `ErrorCodesTests.CONV_NAME_003_AC2_ChangingACodeFailsTheContractTest`, `ErrorCodesTests.LIB_API_001_AC2_TheStatusesAreTheContract`.
  - The named throttle codes: `auth.throttled` and `auth.restriction.exceeded` exist and every such refusal already carries one; no bare 429 was found, and nothing was added.
  - The route `POST /account/recoverycodes/exported` is not mounted (question 117).
- **The retirement of `NotificationRequested`** (`0c20ff61`; LIB-API-001). The type, its 15 lines of `PublicAPI.Unshipped.txt` (it was never shipped), and its entries in `EventConsumers`, `EventJson` and `PendingEvents` are removed. `PendingEventsTests` and `EventPublisherTests` pass.
- **Question 53** (`28c53a12`; CONV-DESIGN-006 criterion 5, API-CONV-003). An endpoint declares its typed route and query values (`Declares`), and the error translation names the first declared value, in declared order, that does not parse, then the body's member. `RoleName`, `ResourceType`, `ResourceId` and `ConfigurationKey` read themselves from the text of a route. 55 declarations. Beyond the four routes the question names, the handlers of `/admin/access`, `/admin/grants`, `/admin/groups` and `/admin/audit` bind typed values, and every `:guid` route constraint (41) is removed, so an identifier that does not read is 400 naming it where it was 404 (question 120). The four `/admin/restrictions/{name}` handlers refuse a name outside the rule as their first check (question 118).
  - Tests: `EndpointDeclarationTests.CONV_DESIGN_006_AC5_EachHandlersTypedValuesAreTheOnesItsEndpointDeclares`, `EndpointDeclarationTests.CONV_DESIGN_006_AC5_AValueThatDoesNotParseIsRefusedNamingItAsync`, `EndpointDeclarationTests.CONV_DESIGN_006_TheFirstDeclaredValueThatDoesNotParseIsTheOneNamedAsync`, `EndpointDeclarationTests.CONV_DESIGN_006_ABodyIsNamedWhereEveryDeclaredValueReadsAsync`, `RestrictionEndpointTests.AUTH_ABUSE_004_ANameOutsideItsRuleIsMalformedOnEveryRouteAsync`, `RoleNameTests.CONV_DESIGN_006_TheValueIsReadFromTheTextOfARoute`, `ResourceTypeTests.CONV_DESIGN_006_TheValueIsReadFromTheTextOfARoute`, `ResourceIdTests.CONV_DESIGN_006_TheValueIsReadFromTheTextOfARoute`, `ConfigurationKeyTests.CONV_DESIGN_006_TheValueIsReadFromTheTextOfARoute`.
  - Changed tests: `SessionRequirementTests.BFF_STEP_001_TheEndpointsThatNeedASessionAreTheOnesListed` (route patterns without the constraint); `BrowserProfileTests.AUTH_SESS_007_AC2_NoEndpointCanOptOut` and `BrowserProfileTests.BFF_CSRF_001_AC2_NoEndpointCanBeExcludedByConfigurationOrAttribute` (question 121).
- **Not built: questions 50 and 51, the whole of them** (CONV-DESIGN-006 criteria 3 and 4; D-166 359 and 382 (3)), parked on question 119. Left: the codes declared on the 154 endpoints (99 hold no declaration); the produced and accepted types as metadata; `endpoints.txt` with its `pipeline` heading and `EndpointContractTests.LIB_API_001_AC2_TheEndpointsAreTheContract`; the test host's check; the lines of `release.sh` and the two scenarios of D-166 382; ledger line 382.

#### D-183, `part/held-gate`, merged as `42fe0861` (`c269fb1a`, `ca559838`, `be484f95`, `804b666e`, `94e4ad4e`)

After the merge: build 0 warnings 0 errors, format clean, 3112 unit tests, 139 contract tests; `TruthTableTests`, `GateBehaviourTests`, `CredentialFlowTests`, `RegistrationFlowTests`, `ConfigurationLockTests` and `SubjectRestrictionsTests` passed on the merged tree. Two conflicts: the changelog (both sides kept) and `CredentialService.BeginGeneratorAsync` (the unit of work of question 62 around the generator's write, then the answer as question 42 left it). No migration.

- **Question 62** (AUTHZ-GATE-006 criterion 3, IDN-ACCT-007, CONV-DESIGN-002, CONV-DESIGN-003).
  - The gate (`c269fb1a`): for a modifying permission asked inside an open transaction, the restriction is read under a shared hold on the acting account's row. Outside a transaction, for a reading permission and for a principal with no account, nothing is held. Where an operation locks the acting account's row itself, that lock is taken first; no second way to hold the row was added.
  - The sites (`ca559838`, `be484f95`, `804b666e`, `94e4ad4e`): after a successful beginning, the gate's ask again, a refusal rolling back.
  - Truth table: five cases (a modifying check asked again inside the caller's unit of work; the same by a caller restricted since the gate step; a settings change asked again by a caller restricted since; a group created by a caller managing groups; the same by a caller restricted since), in `c269fb1a` and `ca559838`, the two commits that change counted files.
  - Tests of the gate: `SubjectSetsTests.AUTHZ_GATE_006_AC3_InsideATransactionTheRestrictionIsReadUnderTheHoldAsync`, `SubjectSetsTests.AUTHZ_GATE_006_APrincipalWithNoAccountHoldsNoRowAsync`; `SubjectRestrictionsTests` over PostgreSQL with two connections: `AUTHZ_GATE_006_AC3_ARestrictionWaitsForTheActionThatHoldsTheRowAsync`, `AUTHZ_GATE_006_AC3_AnActionWaitsForARestrictionUnderWayAndReadsItAsync`, `AUTHZ_GATE_006_TwoActionsOfOneAccountHoldTheRowTogetherAsync`, `AUTHZ_GATE_006_ARowTheOperationLockedItselfIsJudgedWithoutWaitingAsync`, `AUTHZ_GATE_006_OutsideATransactionNothingIsHeldAsync`, `AUTHZ_GATE_006_ASubjectWithNoAccountIsNotRestrictedAsync`; `GateBehaviourTests.AUTHZ_GATE_006_AC3_ARestrictionCommittedAfterTheGateStepRefusesInsideTheUnitOfWorkAsync`, `GateBehaviourTests.AUTHZ_GATE_006_AC3_ARestrictionBegunWhileAnAdmittedActionHoldsTheRowWaitsForItAsync`, `GateBehaviourTests.AUTHZ_GATE_006_AReadingActionInsideAUnitOfWorkHoldsNoRowAsync`.
  - Tests of the sites, each named `AUTHZ_GATE_006_AC3_ARestrictionCommittedSinceTheGateStep...`: `GroupEndpointTests...RefusesEachGroupChangeAsync`, `GrantEndpointTests...RefusesAGrantAndARevocationAsync`, `RoleEndpointTests...RefusesARoleChangeAsync`, `AccountAdministrationEndpointTests...RefusesEachTransitionAsync`, `MaintenanceEndpointTests...RefusesBothRecordsAsync`, `OrganizationEndpointTests...RefusesEachLifecycleChangeAsync`, `OrganizationPolicyEndpointTests...RefusesAPolicyChangeAsync`, `OrganizationDomainEndpointTests...RefusesEachDomainChangeAsync`, `InvitationEndpointTests...RefusesEachMembershipChangeAsync`, `RestrictionEndpointTests...RefusesAnEditADeletionAndAGrantAsync`, `SessionRevocationEndpointTests...RefusesBothRevocationsAsync`, `BreakGlassEndpointTests...RefusesAGenerationAsync`, `InvitationAcknowledgementFlowTests...RefusesAnAcknowledgementAsync`, `AccountLifecycleFlowTests...RefusesADeactivationAsync`, `PhotoFlowTests...RefusesAPhotoChangeAsync`, `AccountApplicationTests...RefusesEachSettingAsync`, `CredentialFlowTests...RefusesEachCredentialChangeAsync`, `CredentialServiceTests...RefusesEachCredentialChangeAsync`, `AccountServiceTests...RefusesAUsernameAndAPreferredStepAsync`, `RecoveryServiceTests...RefusesAnApprovalAsync`, `PublicationEndpointTests...RefusesAPublicationAndATranslationAsync`, `ProcessingRecordsEndpointTests...RefusesTheStatementsAsync`, `PrivacyRequestEndpointTests...RefusesEachDecisionAsync`, `TakedownEndpointTests...RefusesATakedownAndItsReversalAsync`, `ConfigurationEndpointTests...RefusesAKeyAndARetentionAsync`.
  - Not decided by a test: the order of the locks at each site (the acting account's row before any other) is held by a test at the gate alone; at each site it was read: the ask is the first statement after the beginning, or follows only the lock of the acting account's own row. The tests of the sites run over fakes and hold the refusal, the rollback and that nothing is written, not the waiting.
- **Place by place.**
  - Changed: `GrantService` (the grant, the revocation); `GroupService` (create, remove, add and remove a member); `RoleService` (define, remove); `AccountAdministration` (suspend, reactivate, lift a restriction, cancel a deletion; the acting account's own row first where it is the target); `BreakGlassService.GenerateAsync`; `MaintenanceRecords` (the licences, the record); `OrganizationService` (create, request and cancel a deletion, replace the policy); `OrganizationDomainService` (add, remove, verify); `InvitationService` (issue, revoke); `MembershipEnd.EndAsync`; `SessionService.RevokeAccountAsync`, which now begins a unit of work of its own, and `RevokeEveryAsync`; `RecoveryService`, the approval's first unit of work; `RestrictionAdministration` (edit, grant); `AccountLifecycle.DeactivateAsync`; `AccountService` (the profile, the preferences, a credential's label, the preferred second step); `ProfilePhotos` (set, remove); `InvitationAcknowledgement.AcknowledgeAsync`; `CredentialService` (set a password, remove, unlink, begin and upgrade a key, complete a key, confirm a generator, link; begin a generator and generate recovery codes inside a unit of work begun for the ask); `LegalDocumentService` (publish, translate); `ProcessingRecordsService.DeclareAsync`; `PrivacyRequestService` (fulfil, refuse, the entry on a subject's behalf); `TakedownService` (execute, reverse; `IAccountStates.HoldAsync` is new); `ConfigurationService.ChangeAsync` (the retention of a category; a plain key, which now begins an outer unit of work that the change joins).
  - Parked: `AppPasswords.CreateAsync` (question 122), `RecoveryService.SendAsync` (question 123), `ErasureService.CompleteAsync` (question 124), the destination keys through `AlertDestinationChange.ChangeAsync` (question 125).
  - Not yet done: `IdentifierService` (add, verify, make primary, set the backup, remove, replace), `RegistrationService` and `VerificationCodes`, which another part held while this one ran; any endpoint that asks the gate itself and then writes was not reviewed. A further part takes them.
  - Reviewed, left: `ConfigurationAdministration.ChangeAsync` and `ChangeMemberAsync` (an internal seam; its own ask for a loosening is already inside the unit of work); `AccountLifecycle` reactivate, delete and cancel a deletion (borne by a link, or kept available under restriction by IDN-ACCT-007); `SessionService`'s ends of one's own sessions, `LossReports`, `DeviceService` (no ask of the gate); `ConsentService`, `PrivacyRequestService.SubmitAsync`, `ExportService` (no permission asked); reads; the background sweeps, callbacks, intakes, publishers, the alert router and dispatch (a system principal, no account row); `ExportOperations`, `DerivationMaterialiser`, `ResourceService`.
  - Parked sites of questions 69 to 88: none settled. `GroupService.AddMemberAsync` and `RemoveMemberAsync` (77), `AccountLifecycle.DeactivateAsync`, `InvitationAcknowledgement` and `MembershipEnd` (80) gained the ask before what they do.
- For audit:
  - `InvitationAcknowledgement` asks the gate for the inviter inside the unit of work, so the inviter's row is held shared after other locks, not before; the same holds for the asks for a loosening inside `ConfigurationAdministration` on a path the outer ask has not held.
  - Two administrators who restrict or suspend each other at once each hold their own row shared and want the other's exclusively; PostgreSQL ends one as a deadlock. No chapter speaks to it.

## 2. Items not implemented

| Item | Reason | Waits on |
|---|---|---|
| CONV-CODE-007 criterion 4 with D-171, the signing credential: a credential a rotation replaces leaves the server's options when its overlap ends | Open question 20, settled by D-181 | Nothing: applied in `211b1d42` |
| `details.member` of an unreadable body member, named as API-CONV-002 criterion 4 now states (D-179) | Part of the section C sweep, X4 and X5 | Nothing: applied in `9fcc85ec` |
| D-166 362, but for its point (5) | Not reached at the first stop | Nothing: applied in `6a586266`, and point (5) in `0bab2cda` with the test of 263 |
| D-166 D.8, the paragraphs after 121 and 336: 317, 318, 341, 303 (and the audit's subject), 304 and 334 parts (1) and (2), 323, the audit action rows | Not reached at the first stop | Applied (section 1), but for 317 (2), 318 (3) and `Subject` on `AuditEntry`, below |
| D-166 section D.2, the entries after 114 (115, 129, 146, 152, 208, 328, 401, 402 and 422, 417, 419, 421, 326, and the preferred second step) | Not reached at the first stop | Applied on `part/sessions`, but for 115 (2), 129 (1), `Effective` of 152 (3), the rows of 328 and the link-token case of 419, below |
| D-166 section C, rules X1 and X3 to X9 as sweeps (X2 is applied, under 116; X3 on the configuration routes, under 178) | Not reached at the first stop | Applied on the working branch after the merges (section 1), but for the points below |
| D-166 sections D.1 to D.7 and D.9 to D.11 | Not reached at the first stop | Applied on the parts and the working branch, but for the items below |
| D-166 section E, every item other than E.6 | Not reached at the first stop | E.8 in `7d9477bc`, E.9 in `58f11da4`, E.10 in `30a8c32c`; E.1 to E.3, E.7 and E.11 ask nothing of the code; E.4 and E.5 below |
| D-166 section F, the rows of chapter 10 other than those applied under D.8 | Not reached at the first stop | Applied with each entry and checked in the section F sweep (`402e6a1c`), but for `derivation-driftcheck`, `registration-channel` and `abuse.source.sitelimit`, below |
| D-166 section G, the ledger lines of the entries not yet applied | Each goes in the commit that applies its entry | Written with each entry and in `d9a8ecb5`, but for the 23 entries below |
| Truth-table rows for D-166 entries 396 and 265 | They state the D-166 outcomes, so they belong with those fixes | Nothing: written in `cb73c32a` and `ef62ecdc` |
| The full gate, the pull request for `corrections-4` | The push of `corrections-4` after `d5a7fc0e` was refused in the session's environment | The full gate: section 5. The pull request: the push |
| The detail of an entered request: absent and blank told apart (D-166 414 under X4); built while parked, in `e747407f` (slips, below) | Open question 21 | Question 21 |
| 265, the second half: the relationship sources, their start check, the full answer of `GET /admin/access`, the drift-check job and its principal `derivation-driftcheck`, `RefreshAsync`, their AUTHZ-DERIVE-005 and AUTHZ-DERIVE-007 tests; ledger line 265 | Open question 22 | Question 22 |
| 143, the stream of `GET /register/events` raising the degradation `registration-channel`; ledger line 143 | Open question 23 | Question 23 |
| The removal of the `photo.enabled` family of 144 and 315, and the photos half of X6; ledger line 144 | Open question 25 | Question 25 |
| 317 (2), the coded refusal of a retired version's unwrap; ledger line 317 | Open question 26 | Question 26 |
| 118, the governed send contract; 235; the twenty-sets test of 335; X1 at `SendingService.CarryAsync`; ledger lines 118, 235 and 335 | Open question 27 | Question 27 |
| 156, the lawful basis table | Open question 28 | Question 28 |
| 133 and 147 (1) to (3), the consented resources against the document a purpose now names, with `ConsentGateTests.PRIV_CONS_007_APurposeGivenAnotherDocumentAsksItsSubjectsAgainAsync`; ledger lines 133 and 147 | Open question 29 | Question 29 |
| 133 and 147 (5), a grant or objection while a live record stands | Open question 30 | Question 30 |
| 115 (2), registration and identifier codes in the verification-code record; 306 whole, which needs that record; ledger lines 115 and 306 | Open question 31 | Question 31 |
| 419, a registration link token that opens nothing; ledger line 419 | Open question 32 | Question 32 |
| 318 (3), what retirement forgets; ledger line 318 | Open question 33 | Question 33 |
| 136 whole: the record, its migration, the explanation's principal and reason, its two tests; ledger line 136 | Open question 34 | Question 34 |
| `Subject` on `AuditEntry`, its view and its test (303) | Open question 35 | Question 35 |
| 242 (4), the downgrade and `auth.factor.notpermitted` halves, with `AuthenticationServiceTests.IDN_LIFE_009b_ASessionHeldBeforeTheMembershipIsDowngradedAsync`; ledger line 246 | Open question 36 | Question 36 |
| 135, the grants of the maintenance role in every schema | Open question 37 | Question 37 |
| 119 (1) to (4) and what builds on them; 227 and 322; ledger lines 119, 227 and 322 | Open question 38 | Question 38 |
| 119 (6), the erased value in `send_outbox.wrapped_key`, with `SubjectEraserTests.PRIV_RIGHT_005_AC1_AnOutstandingMessageIsUnreadableAndUncarriedAfterErasureAsync` | Open question 39 | Question 39 |
| `Effective` on `CredentialSuspended` (152 (3)); ledger line 152 | Open question 40 | Question 40 |
| 242 (3), an accepting account holding no verified email | Open question 41 | Question 41 |
| 129 (1) whole: the registration session's credential authority, the key and generator paths, their tests; ledger line 129 | Open question 42 | Question 42 |
| The truth-table rows of 328; ledger line 328 | Open question 46 | Question 46 |
| The section of the changelog line of 389 (6) | Open question 47 | Question 47 |
| 389 whole, with the row `abuse.source.sitelimit` of section F; ledger line 389 | Open question 48 | Question 48 |
| The exemption rule of CONV-DESIGN-004 criterion 2 and its test; `BrowserProfileLog` and `Concealment` | Open question 49 | Question 49 |
| 359 and 382 (3), the endpoint lines of (4) and the endpoint and response-member scenarios of (5); ledger line 382 | Open questions 50 and 51 | Questions 50 and 51 |
| The four routes that bind a string: `RoleEndpoints` `name`, `ConfigurationEndpoints` `key`, `RestrictionEndpoints` `name`, `AppPasswordEndpoints` `id` | Open question 53 | Question 53 |
| 160, `IOidc.KeysAsync`, its two callers and its `PublicAPI` line; ledger line 160 | Open question 55 | Question 55 |
| 282, the Google 400 writer of `ProviderEventIntake`, its tests on both routes; E.5, a token without `jti`; ledger line 282 | Open question 56 | Question 56 |
| E.4, CONV-DESIGN-007, the area registration methods and their test | Open question 57 | Question 57 |
| X9, every return after `BeginAsync`, and its test | Open question 58 | Question 58 |
| X1 at `DenialSpikes.WatchAsync`, the transaction its alert is written in | Open question 59 | Question 59 |
| The purpose of the identifier-change-confirm link (`IdentifierService.AskOldAsync`) | Open question 60 | Question 60 |
| X3 at V7 (`OutboxPublisher`) and S6 (`SendingService`, retry and settle) | Open question 61 | Question 61 |
| X3 at C9, the settings restriction decided before the transaction | Open question 62 | Question 62 |
| X3 at S1, the admission half | Open question 63 | Question 63 |
| X3 at S5, `AlertDestinationChange` | Open question 64 | Question 64 |
| X4 at `PUT /admin/compliance/assessments` (`dataOwner`, `organisationalSecurityMeasures`) | Open question 65 | Question 65 |
| 209 (2), the IDNA mapping | The download is approved and made; the parameters of the processing are not stated | Question 68 |

**Slips.** None is rewritten; each commit is green on the fast checks unless said.
- `30b1c4bc` carries only the changelog line of 323; its code and tests are in `5db004f4`. The two are one change split in two commits.
- `863883c1` uses the commit type `style`, which CONV-VCS-003's list does not hold. The commit-message check was red on the push of `06ec9ba0` and is red on the pull request (question 43).
- `c25b633a` has a body line of 76 characters, past the 72 of CONV-VCS-003 (question 43).
- `a9964c20` has a footer line of 84 characters.
- `782d471a` and `98eebc5c` left the integration suites red (the permissive provider's count of findings; three rotation cases of `FingerprintKeyRotationTests`) until `f3fe8dc1` and `99f7140d`.
- On `part/organizations`: from `61b3df11` until `351e2326`, `PublicSurfaceTests.CONV_DESIGN_002_AC3_EveryOperationMeetsTheGateBeforeItReadsOrWrites` was red; from `acb0b8f2` until `adfe7490`, `IdentityEndpointsTests.LIB_API_005_AC1_NoEndpointCarriesLogicItsServiceDoesNotAsync` was red; `8510d664` left two documentation tests of `ErrorCodesTests` red until `d7ad2e63`; `2e8174c1` was committed with the format check red, put right by `863883c1`.
- `fe839f64` left the schema contract test red until `3d136d1e` recorded `send_key_counters`.
- `e747407f`, in the X4 sweep, built the detail rule of an entered request while question 21 parks it: a detail given is trimmed and held to 1 to 1024 characters, and none is held as empty. It stands until the answer.
- The push of `corrections-4` after `d5a7fc0e` was refused in the session's environment. The branch is unpushed from `d5a7fc0e` on.

## 3. Resolved by rule

| Place | What was out of step | Governing item | Rule applied |
|---|---|---|---|
| `.github/gates/changelog.sh`, `truth-table-change.sh`, `release.sh`, `dependency-vulnerabilities.sh` (`551d6ee`) | Each piped `git` output into a search that stops at its first match. Under `pipefail`, the closed pipe failed `git` whenever the output was larger than the pipe buffer, so the gate failed on something its chapter does not say | CONV-VCS-005, CONV-VCS-004, LIB-VER-001, CONV-DEP-002; the working guide's section 3, a gate that mis-implements its own rule | Each gate reads the output whole before searching it, and checks exactly what its chapter states |
| `tests/Janus.Conformance.Tests` (`b9fbbbf`) | The test ran the command from the test project's output, which the SDK's conflict resolution leaves without an assembly the command loads | LIB-TEST-001; the working guide's section 3, test infrastructure | The test runs the command from the command's own build output. No runtime code and no shipped project changed |
| `.gitleaks.toml` (`9e6f4d3`) | The scan of the full history flagged `PRIV-BREACH-002` in `docs/decision-log.md` line 11505 (commit `6866d9c`), under the `generic-api-key` rule. It is D-167's own quotation of the comment its first entry exempts | OPS-DEP-004, D-167 item 2; the working guide's section 3, an allow-list entry for specification text | One entry: the file `docs/decision-log.md` and the exact value `PRIV-BREACH-002`, `condition = "AND"`, reason "an item identifier the decision log quotes from a documentation comment". It is an item identifier, not a credential |
| `src/Janus.Hosting/Background/RestoreTest.cs` (`021060f`) | A run the deadline cut short was judged by a stopwatch that can read short of the objective at the instant the deadline's timer fires, and was then recorded `unrestored` | DR-007: the job "is abandoned at `backup.restoretest.objective`", and a run that exceeds it fails as `overrun` | A run is `overrun` when its deadline fired or its measured time passed the objective |
| `src/Janus.Hosting/Passwords/3esl.txt` and `.gitattributes` (`539a4dc`) | `9b77185` committed the list with its line endings changed from those the 12dicts package publishes, and the repository's line-ending conversion would change them again | D-169: third-party data "is kept exactly as its source publishes it" | The file is the published bytes, and one git attribute keeps git from converting that one file |
| `BackgroundJobs.PollBalanceAsync` (`f64e31b`) | D-166 305 has the balance poll fail where no `ISmsTransport` is registered and names no code | LIB-HOST-001; `10` section 1, `model.startup.declarationmissing` | A missing host declaration is `model.startup.declarationmissing` with `details.key` naming the declaration (`smsTransport`), as `imageCodec` and `dnsResolver` are named |
| `ProtectedConfiguration.CompleteAsync` (`aa76682`) | D-166 319 (1) refuses "with the code each gives", and the algorithm check of `SigningKeys` gives none (it throws) | OPS-CFG-003; `10` section 1.5 | A value its key does not admit is `config.value.notallowed` naming the key (`token.signing.algorithm`) |
| `Program.RunAsync` (`82fa429`) | D-166 308 (4) names the command in the refusal; an invocation with no argument names none | `10` section 1, `api.request.malformed` | A request refused before any member is read carries the code alone |
| `ConformanceSuite.ProviderAsync` and `ConformanceSuite.Validated` (`0dc0ae0`) | Neither is given a container, and each drew from the static `RandomNumberGenerator` | CONV-DESIGN-007 AC2 | Randomness comes from a generator instance made where the work is composed and handed to what draws, as the command compositions and `StorageRegistration` make theirs |
| `tests/Janus.Storage.Tests` `Deployment` and `DatabaseFixture` (`ced2208`) | Each test drew its own key-encryption key while the class shares one database, and the deployment key of 316 is one row of that database, so a second test could not unwrap it | OPS-SEC-003; the working guide's section 3, test infrastructure | The key-encryption key is the fixture's, one per database, and the tests build the deployment key's store from it; no runtime code changed |
| `tests/Janus.Storage.Tests` `DatabaseFixture` (`c656253`) | A migration test needs a database of its own, migrated to a named migration, beside the class's shared one | PRIV-RIGHT-005a AC18; the working guide's section 3, test infrastructure | `DatabaseFixture` gains the creation of a named database and a context over a connection string; no runtime code changed |
| `docs/reports/decisions-pending-review.md`, entry 234 (`c656253`) | The work order asked for 234's ledger line with its commit; section G lists 234 neither as superseded nor as revised, D-166 keeps it "with a fix", and the ledger takes only the lines section G gives | The working guide's section 3 (the closed ledger); D-166 section G | No line is written for a kept entry that section G does not list |
| `src/Janus.Core/Janus.Core.csproj` and `LibraryStructureTests` (`2aa2733`) | The criterion that the ring's clearing leaves every array zero needs its test to see the ring's internal arrays | CONV-CODE-007 AC3, D-171; CONV-LAYOUT-002 AC1; the working guide's section 3, test infrastructure | `Janus.Core` grants its internals to `Janus.Core.Tests` alone, and the structure test permits that grant; no runtime code changed |
| `docs/reports/decisions-pending-review.md`, entry 283 (`2aa2733`) | Section G revises 283 by two entries at once, and the ledger's form names one | D-166 section G | The line names both entries: "Revised by entries 343 and 349" |
| `MailboxPush` and `HostedMailbox` (`3cef29a`) | D-177 (a) gives the push the mailbox's identifier and names no type for it | CONV-DESIGN-004 AC2; `07`, the Operations contract row | The identifier is `MailboxId`, moved to `Janus.Core` and made public |
| `KeyRingService` (`d5a2b03`) | The chapters say the mail server in use is decided once, at the start, and not when the host's registration is resolved | D-176 item 1; CONV-DESIGN-007 | The host's `IMailServer` is resolved once, when the ring's service is built, and held for the process |
| `JmapMailServer`, finding a domain and an account (`9e7605d`) | D-166 215 point 4 finds each by name; the mail server's object reference lists `name` as a text filter | D-166 215 point 4 | The query is followed in the same request by a `get` of `name`, and only the exact name counts; no domain is a failure, two accounts do not read |
| `JmapMailServer`, the `disabled` and `enabled` patches (`9e7605d`) | RFC 8620 section 5.3 names a member in a patch path only where every earlier part exists | D-176 item 2; D-166 215 point 4 | A set the account lacks is written whole, an `Inherit` account as the `Merge` shape of point 4; `enabled` patches `authenticate` out of `disabledPermissions` and, under `Replace`, into `enabledPermissions`; nothing is sent where the account already holds the state |
| `JmapMailServer`, the listing (`9e7605d`) | D-166 215 point 5 lists accounts of `@type` `User` only, and INT-MAIL-007 compares every account the server lists; a query answers one page | D-166 215 point 5; INT-MAIL-007 | An account whose `@type` is present and not `User` is skipped; pages are read by `position` until one is empty, and an answer at another position does not read |
| `ISecretSource.ReadMailServerSecretAsync` (`9e7605d`) | D-166 336 names the member and not its answer | `07` LIB-HOST-001; D-166 336 | It answers a result, as the provider credential's member does |
| `IntegrationBoundaryTests` (`9e7605d`, `e717980`) | The mail server naming test read all of `src`, while INT-MAIL-008 criterion 1 reads a core namespace and INT-MAIL-001 has the adapter in `Janus.Hosting` name the capability `urn:stalwart:jmap`; `9e7605d` narrowed it to `Janus.Core`, less than a core namespace is | `07`, the definition of a core namespace; INT-MAIL-008 AC1; LIB-EXT-001 AC3 | Both naming tests read `Janus.Core` and the four area projects, exactly what `07` names; test only |
| `mailboxes.attempted` (`78b1141`) | D-177 confirms unsent a removal of a mailbox no push of which was ever attempted, which needs that fact kept across keys; a row written before cannot tell | INT-MAIL-007; D-177 | A column of the table, by the existing migration pattern; a row written before counts as attempted, which can only send a removal the rule would have skipped |
| `Mailbox`, the day before a failed push begins again (`78b1141`) | INT-MAIL-007 fixes the interval at a day and names no key | INT-MAIL-007; D-177 | A constant of `Mailbox`, not a setting |
| `AlertRaised.Scope` (`e4a2c75`) | Keys already carried discriminators that `10` section 5.23 does not name as scopes (`event:<type>`, `location.database.*`, `licence:<id>`) | OPS-ALERT-002; `10` section 5.23, Scopes | Only a scope the Scopes table names is carried as `Scope`; the others stay in the key as before, so no key changes |
| `mailboxes.removal_owed_at` (`e64141d`) | D-178 marks both a replaced mailbox and a released reservation and gives the mark no name; the owner asked for one fitting both | INT-MAIL-006 AC7 (owed `removed` "from the instant its row records"); D-178 | The column is named for what the instant is, `removal_owed_at`, and `released_at`, which marked the same for a released reservation, is renamed rather than joined by a second column |
| A released mailbox's wrapped key, once its removal is confirmed (`e64141d`) | PRIV-RIGHT-005a says the wrapped key is overwritten and not with what, and `ck_mailboxes_key` holds a key on every row nobody holds | PRIV-RIGHT-005a; the erasure of a subject key (`SubjectKey.Erase`) | It is overwritten in place with zeros of its own length, as a subject key's erasure is |
| The wait behind an unconfirmed removal (`e64141d`) | INT-MAIL-007 criterion 5 releases the push at the first run after the confirmation | INT-MAIL-007 AC5 | The wait is read from the mailboxes as each pass reads them, so a removal confirmed in one pass releases the push in the next |
| `docs/reports/decisions-pending-review.md`, entry 219 (`e64141d`) | D-166 kept 219, so section G gave it no line; D-177 reverses 219's rule that a failed push stays failed until the state owed changes | The working guide's section 3 (the closed ledger takes "Superseded by D-NNN" under an entry a decision-log entry reverses); D-177 | 219 takes "Superseded by D-177"; 222, which D-178 keeps, takes none |
| A `reason` with no `formerMailbox` (`1566815`) | `09` section 8a gives `reason` only beside `formerMailbox` | API-CONV-002; `10`, the `api.request.malformed` row | A `reason` without `formerMailbox`, and a `formerMailbox` with no, a blank or an over-long reason, is `api.request.malformed` naming `reason` |
| The issue's record (`1566815`) | The `10` row of `identity.invitation.issued` says "`details.formerMailbox` with its reason" | `10`: every other row carries a reason as `details.reason` | The record carries `details.formerMailbox` and `details.reason` |
| `PrivacyContractTests.PRIV_CONS_010_AC1_NoLibrarySourceNamesATransferPurpose` (`1566815`) | The test flags any `"transfer"` literal, and so the wire name of `FormerMailbox.Transfer` that `10` section 5.44 gives | PRIV-CONS-010 AC1 (no consent-based purpose named for the hosting or its transfer); PRIV-CONS-002 AC1 (a purpose is a string) | A `[JsonStringEnumMemberName(...)]` line names no purpose and is skipped; every other literal is still read; test only |
| The mail server row's condition "or the library's default mail transport is in use" (`de42f15`) | The library ships no transport, so the condition can never hold | D-166 270 ("Until the shipped transports exist"); INT-MAIL-008 | The condition is not coded, and no setting stands in for it |
| `AlertsTests` (`72f0e85`) | `breakglass-generated` takes the next free value, 31, but is declared after `breakglass-used` in the table's order, so the test that read the order from the values failed | OPS-ALERT-001; the working guide's section 3, test infrastructure | The test reads the declaration order from the enumeration's fields |
| `src/Janus.Authentication/Janus.Authentication.csproj` (`211b1d42`) | D-181 places the credential source in `Janus.Authentication`, and the `SigningCredentials` and `JsonWebKey` objects it holds have their types only through `OpenIddict.Server`, which the project did not reference | AUTH-KEY-001 and CONV-CODE-007 with D-181; CONV-DESIGN-008 | The project references `OpenIddict.Server`, a package CONV-DESIGN-008 lists and `Janus.Hosting` already references; every OpenIddict and IdentityModel type stays internal, no `PublicAPI` line is added, and no project references `Microsoft.IdentityModel.JsonWebTokens` directly For audit: a reference added to a shipped project may fail the condition of no new dependency. |
| `src/Janus.Hosting/Oidc/SigningAlgorithms.cs` and `TokenDigests.cs` (`211b1d42`) | Two steps of the provider that D-181 does not name read a signing credential from the options (the discovery's list of signing algorithms, and the token hashes for the algorithm) | AUTH-KEY-001 with D-181: no step of the provider takes a signing credential from its options, and such steps are replaced through its event model | Both are removed and replaced at the same order by steps that read the source; `SigningKeyRotationTests.AUTH_KEY_001_AC3_TheDocumentAndTheTokenHashesAreUnchangedAfterTheStartKeyRetiresAsync` |
| Migration `HoldSigningKeysThroughTheirKeeping`, column `signing_keys.longest_lifetime` (`211b1d42`) | D-181 gives no value for a key stored before the column existed, whose longest lifetime was recorded nowhere | AUTH-KEY-001 criterion 2 with D-181: the overlap covers the longest lifetime the key signed under | The migration writes the ceiling of `oidc.accesstoken.lifetime` (1 hour), the one value known to cover every access token such a key could have signed |
| `AuditAction`, eight remarks (`f407060d`) | Eight remarks cited items no chapter holds (AUTH-ABUSE-009, AUTH-REC-004 four times, AUTH-REC-006, AUTH-TOK-004, REG-PM-002) | `10` section 5 names the governing item of each action | Each remark cites the item of its row in `10` section 5 (AUTH-ABUSE-008 and LIB-HOST-001, AUTH-RECOV-007, AUTH-RECOV-007 criterion 3, AUTH-FACT-001, AUTH-RECOV-002 and AUTH-RECOV-002a, AUTH-OIDC-003) |
| `.gitleaks.toml` (`814d8901`) | Secret scanning on the pushes of `a8b1f104` and `06ec9ba0` flagged `PRIV-BREACH-002` under `generic-api-key`, in a comment of `src/Janus.Storage/Migrations/20260930224333_NameTheSubjectOfEachAuditRecord.cs` (line 16, from `9cfccf3a`) | OPS-DEP-004; the working guide's section 3, an allow-list entry for specification text | One entry: that file and the exact value `^PRIV-BREACH-002$`, `condition = "AND"`, reason "an item identifier in a migration's comment". It is an item identifier, not a credential. The pinned scanner, run locally over 996 commits: no finding |
| `BackgroundJobsTests` (`a940181b`) | D-166 304 names `BackgroundWorkerTests`, a unit class over a hand-built container that holds none of the job services | IDN-PRIN-001 criterion 3; the working guide's section 3, test infrastructure | The test needs the deployment's own container, which `BackgroundJobsTests` builds, so it is there under the name the settlement gives; test only |
| `PrivacyRequestTests` (`01c23b22`, `9c9983cf`) | D-166 gives the tests under `PrivacyRequestServiceTests`, a class that does not exist | CONV-TEST-001; the working guide's section 3, test infrastructure | The tests are in `PrivacyRequestTests`, the class of the service's tests, under the names D-166 gives |
| `RoleEndpoints`, a role a standing invitation names (`650c6c7d`) | D-166 189 says an invitation "not past expiry"; `03`, `09` and `10` say "standing" | AUTHZ-GRANT-004; `10`, the definition of a standing invitation | A role counts as referenced by a standing invitation as the chapters and `10` define it |
| `tests/Janus.Hosting.Tests`, `Organizations/InvitationServiceTests` and `HostFixture` (`cbd0bb43`) | The test of 252 needs the gate's memberships on PostgreSQL, while the class D-166 names is a unit class over a fake gate; ending a membership needs a mail and an SMS transport, which the fixture did not register | IDN-LIFE-009a, IDN-MEM-001; CONV-TEST-001; the working guide's section 3, test infrastructure | The test is in the integration class over `HostFixture`, which registers `MailTransportInMemory` and `SmsTransportInMemory`; no runtime code changed |
| `tests/Janus.Hosting.Tests`, `Authorization/ConcealmentTests` (`05689c65`) | The criterion of 339 needs the statements traced on PostgreSQL; the `ConcealmentTests` of the BFF is a unit class | AUTHZ-CONCEAL-002 criterion 2; the working guide's section 3, test infrastructure | The test is in an integration class of that name in `Janus.Hosting.Tests`; no runtime code changed |
| The Hosting `Deployment` fixture, `OrganizationDomainService` and `DomainReverification` registrations (`db60ab71`) | A deployment that lists a domain now needs `dnsResolver`, and the fixture declared none | LIB-HOST-001 (the resolver is optional); the working guide's section 3, test infrastructure | The fixture takes a resolver parameter, true by default, and the two services are registered by factories passing the optional resolver, as the other optional declarations are |
| `ConfigurationInMemory` and the three configuration fakes (`050d46be`) | The fakes' `Set` accepted a value its setting refuses, so a test could hold what the real store cannot | OPS-CFG-003; the working guide's section 3, test infrastructure | Each fake's `Set` validates through `setting.Accept`, and `ConfigurationInMemory` gains `Forget<TValue>(Setting<TValue>)`; test only |
| `InvitationService.IssueAsync`, `RevokeAsync` and `MembershipEnd.EndAsync` (`351e2326`) | Each read the path's organization before the gate, which the gate test refuses | CONV-DESIGN-002 criterion 3 (the gate test's existing rule for `ScopeOfAsync`); D-166 X5 | The path's organization is resolved by a private `ScopeOfAsync` as the gate step, and the standing is read after the gate; an organization the deployment does not hold is 404 `identity.organization.notfound` |
| `OrganizationEndpoints`, the reason of a policy or domain change (`adfe7490`) | D-166 200 puts the check at the endpoint, and LIB-API-005 criterion 1 forbids an endpoint to reach an area type | D-166 200; LIB-API-005 criterion 1 | The endpoint checks with `Janus.Core` types alone: a blank reason is 422 `config.change.reasonrequired` naming `policy.<id>`, a reason past 1024 characters 400 `api.request.malformed` naming `reason` |
| `OrganizationDirectoryTests` (`65c03ac2`) | The fixture placed two current memberships in one organization, which the constraint of 154 now refuses | IDN-MEM-002; the working guide's section 3, test infrastructure | The fixture places one current membership per organization; test only |
| The scope of a send alert of 119 (3) (no commit) | 119 (3) writes the scope `send:invitation-link`; `10` section 5.23 writes `send:<channel>` | OPS-ALERT-002; `10` section 5.23 governs scopes | The scope is written as `10` writes it. Nothing is built: 119 (3) waits on question 38 |
| The refusals of the name rule (`ef19b85f`) | 120 (2) names no member for a refusal at the start | LIB-HOST-001; `10`, `details.declaration` and `details.field` | A purpose's document is named by `details.declaration` (the purpose's name) with `details.field` `document`, a subscriber by its name with `details.field` `name` |
| `RestrictionEndpointTests` (`61204cc0`) | D-166 names `RestrictionEndpointsTests` | CONV-TEST-001 | The tests are in the existing class, `RestrictionEndpointTests` |
| Test names of 120 and 123 (`25d5dc9c`, `908f5817`) | Some names D-166 gives say AC1 where the criterion is AC3 | CONV-TEST-007 | D-166's names are kept, as the settlement writes them |
| The name rule's lower-case letters (`50016b49`) | 120 (1) says "lower-case letters" | INT-SMS-003; `10` section 5.26 | Read as the ASCII letters a to z, as every name `10` gives is |
| `ExpirySweepTests` and `BackgroundJobsTests.Deployed` (`fe839f64`) | The test of 122 needs the sweep run over the deployment's container | PRIV-RET-005 criterion 2; the working guide's section 3, test infrastructure | `ExpirySweepTests` is created in `Janus.Hosting.Tests/Background`, and `BackgroundJobsTests.Deployed` is internal static; test only |
| The width of `{link}` (`908f5817`) | D-166 123 (3) measures `{link}` at its own application's origin; `10` section 5.26, which governs widths, measures it at the longer declared origin and the widest kind | INT-SMS-003; `10` section 5.26 | `{link}` is measured as `10` measures it |
| `AlertDispatch` (`01a5a2c9`) | 119 (5) commits the claim before the send, and the dispatch's delivery ran inside an open transaction | OPS-ALERT-002, INF-BG-001 | The router runs in a transaction of its own, the row is removed in a transaction after the router returns, and a row an early stop leaves is folded by the deduplication |
| `INT_SMS_003_AC1_APlaceTheLibraryDoesNotFillIsMeasuredAsWrittenAsync` (`b9c5243e`) | The test asserted the behaviour the message kinds (6) replace | INT-SMS-003 with D-166 D.3, the message kinds (6) | The test is retired; the new behaviour has its own tests |
| `Deployment`, the hand-built Hosting hosts, the Conformance `SampleHost`, `Landing.cs`, `MailTransportInMemory` and `AlertRouterTests` (`56312065`, `908f5817`, `01a5a2c9`) | Every host must now declare its landing origins, a test must read a token through its link, and the claim's commit must be observed | LIB-HOST-001; the working guide's section 3, test infrastructure | The fixtures declare `LandingOrigins`; `Authentication.Tests/Sending/Landing.cs` gives `SendRequest.Token()`; `MailTransportInMemory` gains a `Handed` hook; `AlertRouterTests` has a `TransactionWitness` fake; no runtime code beyond the items |
| `DeadlineSweep`, the spelling of a request's type and status (`cc804177`) | `WrittenName` is internal to `Janus.Authentication` | INT-SMS-003; CONV-NAME-003 | The sweep uses spelled `JsonSerializerOptions`, as `TakedownService` does |
| `Error.Throttled` (`7d9477bc`) | E.8 moves the builder and names no place for the check that no one else builds the code | AUTH-ABUSE-002 criterion 2; D-166 E.8 | The throttled refusal is built by `Error.Throttled` alone, and the structure test admits only the builder and the status map |
| The sign-in code (`6b79266c`) | 115 names the code a sign-in shows and not its key | AUTH-FACT-004 criterion 6 | The sign-in code is held to the mail's own keys (`MailAlone`) |
| Five wrong tries (`6b79266c`) | AUTH-FACT-004 criterion 3 says five wrong tries invalidate | AUTH-FACT-004 criterion 3 | Five wrong tries answer invalid, and the sixth answers expired |
| 146 (4), an ask with a risk signal (`52482ed5`) | 146 (4) answers 202 and then considers the signal | D-166 146 (4) with 146 (3) and `/recovery/begin` | The ask answers 202, issues and sends nothing, records the consideration and counts nothing For audit: it decides a step-up ask, which may be security semantics and Tier 3. |
| The send path of 146 (`52482ed5`) | The governed send of 118 is parked | D-166 146; AUTH-FACT-002 | The second-step code goes by the path `SignInLinks` takes today; question 27 moves both |
| The Hosting `Deployment`, a `signals` parameter (`52482ed5`) | The tests of 146 need the phone signals | The working guide's section 3, test infrastructure | The fixture takes the signals; test only |
| 152 under X9 (`50cd2637`) | A refusal of `keys.EnrolAsync` or `generators.ConfirmAsync` after 152 (1)'s transaction opens left it open | CONV-DESIGN-003, as 115 does | The empty transaction is committed before the refusal returns |
| `UnitOfWorkInMemory.OutermostCommitted` and `PendingEventsUnwritable` (`50cd2637`) | The X1 tests must observe the outermost commit and an event row that cannot be written | The working guide's section 3, test infrastructure | Two fake members; test only |
| `StepUpGates`, the values of 328 (`669bac7b`) | 328 names the three values the gate reads | LIB-HOST-004, AUTH-STEP-002 | The gate reads them through `ISessionGates.CostAsync`; no view leaves the gate met where it is not For audit: it decides how the step-up gate fails, which may be security semantics and Tier 3. |
| The X1 tests of 152 (`50cd2637`) | That nothing stands after a failed event row is not observed by the tests themselves | CONV-TEST-007; `UnitOfWorkTests.CONV_DESIGN_003_AC3` | The tests assert that the outermost transaction never commits and that no notice goes out; that nothing stands follows from the rollback on disposal, which `UnitOfWorkTests` proves, as `b359fa5c` |
| `DeploymentDataKeyTests` (`60dd9cb9`) | The test wrote its rows through the current model, whose columns its migration does not have | PRIV-RIGHT-005a criterion 18; the working guide's section 3, test infrastructure | The rows are written in raw SQL in the columns of `20260929142007`; test only |
| `AccountStatesInMemory` (`6a586266`) | The end-to-end test of IDN-ACCT-007 criterion 2 needs the Privacy fake to know the restriction | The working guide's section 3, test infrastructure | The Hosting `Deployment` sets the fake's `Restricted` hook; test only |
| 141 at the terms step (`4459da5b`) | D-166 141 names no code for an address reserved since it was staged | REG-SESS-005 criterion 3; D-166 141 | The session ends as `SessionExpired`, as REG-SESS-005 criterion 3 answers |
| The erasure of an account's grants (`f7757aff`) | The grants at deletion name no cache step | AUTHZ-CACHE-001 (any change of a grant) | The erasure raises the grant version counter |
| 362 (5), the app-password half (`0bab2cda`) | D-166 names a test of its own | REG-MAIL-002 criterion 4 | `AppPasswordsTests.REG_MAIL_002_AC4_ARestrictedAccountListsAndRevokesAndCreatesNoneAsync`, from 263, decides it; no second test is written |
| `SuspendedBy` through a takedown's window (`b1960a2e`) | 169 names no change to who suspended | IDN-LIFE-003 criterion 4 | `SuspendedBy` stays `Administrator` through the window, as the existing test of criterion 4 has it |
| `CanonicalValueTests` (`fcd21991`) | The test that every canonical value with rules is read from text took the identifiers' `IParsable<T>` members for values with rules | CONV-DESIGN-004 criterion 3 | A `Parse` or `TryParse` taking an `IFormatProvider` is not counted; the assertion is unchanged; test only |
| `JmapMailServer.ProvisionAsync` (`ae35b431`) | The check that the address holds an `@` is unreachable once the push carries an `EmailAddress` | CONV-DESIGN-004 | The check is dropped |
| `UndoIdentifierAsync` and `AbandonIdentifierAsync` (`fcd21991`) | Each took a `Guid` while its route binds an `IdentifierId` | CONV-DESIGN-004 | Each takes an `IdentifierId`, as the route does |
| `StepUpAction` at the merges (`06ec9ba0`, `5caf97e4`) | `part/organizations` took 30 for `MembershipEnd`, which `part/privacy` had taken for `PrivacyRequestFulfil`; `part/registration-accounts` took values the merged branch held | `10` section 5a names the actions and no value | Each member takes the next free value in the order merged: `MembershipEnd` 31, `AccountRestrictionLift` 32, `AccountDeletionCancel` 33, `AccountSessionsRevoke` 34, `SessionRevokeAll` 35, with their `PublicAPI` lines |
| Tests at the merges (`06ec9ba0`, `057973e4`, `304254fe`, `5caf97e4`) | Tests one part wrote met a change of another: the end of a membership now stepped up, a token now in a link, the subject of 303, the `StepUpGuard` construction, the session the fulfilment takes, the registrations `BrowserProfileTests` needs, `organizations.canonical_name` now required | The working guide's section 3, test infrastructure; each behaviour is the merged part's | Each test is brought to the merged behaviour (a stepped-up session, `Token()` through the link, the 303 subject, the new constructor arguments, the browser session, `IIdentifierDirectory` and `PhoneSignals` registered, `canonical_name` written; `HostFixture` registers `LandingOrigins`); no assertion is weakened |
| `error-statuses.txt` at the merge of `part/gates` (`d5a7fc0e`) | The file `4d1552d8` made lacked 14 codes the working branch had declared since | LIB-API-001 criterion 2; `09` and `10` | The 14 codes are added with the statuses `09` and `10` give, `identity.registration.incomplete` 409 (`3ff77d5d`) |
| `configuration-keys.txt` at the merge of `part/gates` (`d5a7fc0e`) | `part/gates` changed the file's form, and the working branch had added `code.signin.attempts` and `code.signin.lifetime` | LIB-API-001 criterion 2; `10` section 4 | The new form, with both keys written in it |
| `FingerprintKeyTests` at the merge of `part/gates` (`d5a7fc0e`) | `RestrictionKey` takes a kind since 122 | INF-HOST-003 criterion 4 | The test passes `RestrictionKeyKind.Destination`; test only |
| `.gitleaks.toml` (`1a5a2af6`) | The scan of the full history flagged `PRIV-BREACH-002` under `generic-api-key` in `docs/reports/corrections-4.md` line 313 (from `a22c76f7`), where the row of D-166 D.8 cites the item beside its tests | OPS-DEP-004; the working guide's section 3, an allow-list entry for specification text | One entry: that file and the exact value `^PRIV-BREACH-002$`, `condition = "AND"`, reason "an item identifier a report cites beside the tests that carry it" |
| Fakes and tests of the unit tests, at the X9 sweep (`a64c93e2`, `ccfec930`, `9c84477a`) | No fake let a membership end between the find and the end; no fake of `IDeploymentSeed` stood in the unit tests; two tests of the host asserted a commit on a refusal under the lock | CONV-DESIGN-003 criterion 5, CONV-TEST-007; the working guide's section 3, test infrastructure | `MembershipEndingInMemory` takes a hook as `InvitationStoreInMemory` has; `DeploymentSeedInMemory` is added; the two tests assert the rollback. No runtime code |
| `Janus.Core.csproj` and the fixtures of `Janus.Hosting.Tests` (`66771b55`, `a8dc5501`) | The fixtures stood the key ring up through `AddKeyRing` of Hosting, which question 57 removes, and relied on the provider's setup to register `SigningCredentialSource`, now registered by `AddAuthenticationArea` | CONV-DESIGN-007; the working guide's section 3, a grant to a test project and a fixture arrangement | `Janus.Core` grants `InternalsVisibleTo` to `Janus.Hosting.Tests`, and `LibraryStructureTests` permits it; the fixtures call `AddCoreArea` and register `SigningCredentialSource` themselves. No runtime code. CONV-LAYOUT-002 criterion 1's list in `08` does not name the grant |
| Fakes of the unit tests, at the X9 sweep (`124cd764`, `1b363b33`, `4c8b33d8`, `e01c8c06`, `946567cd`, `0c7962b2`) | No test could reach a refusal under the lock of a grant or of a reversed takedown; the two rotations had no unit test; the worker's tests kept no unit of work to assert on | CONV-DESIGN-003 criterion 5, CONV-TEST-007; the working guide's section 3, test infrastructure | `GrantsInMemory` takes a locking hook; `AccountStatesInMemory.ReverseTakedownAsync` calls the holding hook; `KeyRingInMemory`, `KeyRotationStoreInMemory` and `FingerprintRotationStoreInMemory` are added; the fixtures of `BackgroundWorkerTests`, `AlertDestinationChangeTests` and `EventPublisherTests` expose what the assertions read. No runtime code, no grant |
| `tests/Janus.Hosting.Tests/PublicSurfaceTests.cs` (`64b1a154`) | D-166 Tier 1 correction (1) places the scan of CONV-DESIGN-004 criterion 2 in `LibraryStructureTests`, whose project references `Janus.Core` alone, and the exempt members now sit in `Janus.Storage` and `Janus.Hosting` | CONV-TEST-001; the working guide's section 3, test infrastructure | A test that reads the shipped assemblies lives in the test project that references every shipped project; no reference or grant was added |
| `PublicSurfaceTests.CONV_DESIGN_004_AC2_OnlyAMemberAPackagesInterfaceFixesIsExempt` (`64b1a154`) | Criterion 2 admits "an assembly of a package CONV-DESIGN-008 lists, or names as one a listed package brings"; the provider's store interfaces are declared in `OpenIddict.Abstractions`, which the listed packages bring and the table does not name | D-166 Tier 1 correction (1), which fixes "an assembly named OpenIddict.*" for those members | The test admits an assembly named as a listed or named package, or named `OpenIddict.*` |
| `DeclarationCoverage.Origin` (`64b1a154`) | The scan reads a text parameter named `address` as an email address taken untyped; this one is a client's return address or the sign-in address, for which no typed value exists | CONV-DESIGN-004 criterion 2, "where a typed identifier or value exists" | The parameter is renamed `location`; behaviour unchanged |
| 146 (4), an ask with a risk signal (`52482ed5`), the row above | The row was recorded as Tier 1 | D-183, the audit of the Tier 1 records | It was not Tier 1: it decided a step-up's outcome from rules for anonymous asks. The sign-in half is corrected in `0c3746d8`; the step-up half waits on question 94 |
| `StepUpGates`, the values of 328 (`669bac7b`), the row above | The row was recorded as Tier 1 | D-183, the audit of the Tier 1 records | The main line stands; its failure rules were not Tier 1. They are built as LIB-HOST-004 criterion 4 now states them in `d52cacfb` |
| `AssessmentsRequest`, the body of `PUT /admin/compliance/assessments` (`f2ef4d42`) | The member was read from the wire as `organisationalSecurityMeasures` | `09` section 8a, the route's row; D-183 question 65 | The request member is spelled `organizationalSecurityMeasures` as `09` spells it. For audit: the response of `GET /admin/ropa`, two public members and the column keep the other spelling (question 99) |
| Fakes of the unit tests (`46968c21`, `c972731e`) | The unit compositions start every hosted service with no database, and the start now reads the subject keys' versions and writes the lawful bases | CONV-TEST-007; the working guide's section 3, test infrastructure | A fake of the subject-key store and one of the lawful-basis store are registered in the fixtures; no runtime code |
| `BreakGlassEndpointTests` and `PrivacyRequestEndpointTests` (`67b5c544`) | Two fixtures entered a request for a subject the account-states fake did not hold, which the entry now refuses | `09` section 8a; D-183 question 45; the working guide's section 3, test infrastructure | The fixture holds the subject's account, one line in each place |
| `IAuditTrail.OfSubjectAsync`, its summary, and the fake of the trail's store (`6cd14e2f`) | The summary said the trail names the subject "as the acting or the effective identity", and the fake filtered on the acting identity alone | `09` `GET /admin/audit`; IDN-AUD-001; PRIV-BREACH-002 | The trail of a subject is the records it acted in or is the data subject of; documentation and a test fake |
| `DerivationMaterialiser`, the fault for rows not supplied (`10e7acb6`) | It was thrown after the unit of work began | CONV-DESIGN-003 | What needs no write is judged before the beginning; same exception, same condition |
| `DeclarationCoverage`, `details.key` of a missing relationship source (`10e7acb6`) | D-183 question 22 names the code and not the key's form | `10` section 1.5, "a relationship source is named by its relationship" | The key is the relationship's name, with no prefix |
| Fixtures and fakes for questions 22, 34 and 59 (`be757f8d`, `e467284d`, `10e7acb6`) | The test host and the conformance sample declared no relationship source and no longer started; no fake held the alerts of the gate or the order the audit is asked in | The working guide's section 3, test infrastructure | The fixtures register the test host's context and its sources; fakes alone, no runtime code |
| `ConsentStoreTests.PRIV_CONS_001_AC4_AGrantAfterAWithdrawalKeepsTheWithdrawnRecordAsync` (`c3191b69`) | D-166 names this test with criterion 3; the criterion that states it is PRIV-CONS-001 criterion 4 | `04` PRIV-CONS-001; CONV-TEST-007 | The test carries the number of the criterion the chapter states |
| `IntegrationBoundaryTests`, the list `Mapping` (`af618c88`) | It named `SendingService.cs` as the one file that builds an outbound payload | INT-GEN-005 criterion 2 | The payload is built in `NotificationHandler.cs` now, and the list names that file |
| `SendReference` (`af618c88`) | A default instance gave no failure when read, which the contract test requires of every type that reads itself from text | CONV-DESIGN-004 criterion 3 | `Value` and `ToString` of a default instance throw |
| Fakes and fixtures for the governed send (`1874f506`, `af618c88`) | No fake held the alert channels in `Janus.Storage.Tests`; the fakes of the unit of work lacked the registration after a commit; the Hosting fixture carried nothing after a request | The working guide's section 3, test infrastructure | The fakes implement the new member and run what is registered at the outermost commit; the fixture runs the publisher's pass after each request unless a test turns it off; no runtime code |
| `StoreContextModelSnapshot.cs`, the product version (`af618c88` and the migrations of the other parts) | The file said 10.0.4 | D-184 | The tool at the version of EF Core writes 10.0.12 |
| `AccessGate`, the private constant `privacy-notice` (`e20b2543`) | The rule passes "the document the purpose now names", and a purpose naming none is governed by the privacy notice, whose name no type the authorization area can reach carries | PRIV-CONS-007, "the privacy notice where it names none"; D-166 147 (4), which spells it | The gate names the default document by the private constant the three other readers hold; no public type. For audit: the name is now written in four places |
| The columns of `identity.consented_resources` (`ffa77d18`) | D-166 147 (1) lists the view without the document | AUTHZ-GATE-002 as D-183 question 29 leaves it | The view carries `document`, as the chapter reads |
| The place of a ledger line (`b5fd0791` and the lines before it) | The working guide puts the line "under the heading"; every line the ledger holds sits at the end of its entry | D-166 section G | The lines follow the form the ledger has |
| `RoleName`, `ResourceType`, `ResourceId`, `ConfigurationKey` (`28c53a12`) | A public `Parse(string, IFormatProvider)` beside `Parse(string)` makes every existing call an analyser error | CONV-DESIGN-006, "binds through `IParsable<T>`" | The four implement the interface explicitly, which the framework binds and which adds no line to the public surface |
| `BrowserProfileLog.BodyUnreadable`, its message (`28c53a12`) | It said "request body" where the stage now also answers a route or query value | CONV-DESIGN-006 | The message reads "A request could not be bound at {Member}"; the event's identifier and level are unchanged |
| `EndpointDeclarationTests` (the merge of `part/endpoints`) | One line wrote the product name as text, which CONV-NAME-001 criterion 2 refuses | CONV-NAME-001 | The name is read from a namespace, as `KeyMaterialTests` reads it; test only |
| Fakes and fixtures for question 62 (`c269fb1a`, `ca559838`, `be484f95`, `804b666e`, `94e4ad4e`) | No fixture could commit a restriction between the gate step and the first write; two fixtures called a service with no grant, which the ask inside the unit of work now refuses; one hook of `InvitationServiceTests` ran twice now that the row is held twice | AUTHZ-GATE-006 criterion 3; CONV-TEST-007; the working guide's section 3, test infrastructure | The fakes of the unit of work and of the gate let a restriction be committed meanwhile; the fixtures grant what the route's gate step already requires; the hook fires once. No assertion weakened, no runtime code |

## 4. Open questions

**1. Tier 2. INT-PWD-001 AC3 and D-166 item 114: where the library's version is read from.**

- **Item.** D-166 item 114: "The range requests of INT-PWD-001 carry a `User-Agent` naming
  the library and its version ... the `LeakedPasswordCorpus` client sets it once where it
  is registered." INT-PWD-001 AC3 asks for a test of it.
- **What the code needs.** The library's version at run time.
  - MinVer stamps it from the release tag into the assembly's attributes. By LIB-VER-001 and CONV-VCS-005, nothing else carries it: it appears in no project file, and nothing else sets a version number.
  - The product name is read from the root namespace, as the product-name rule already requires.
- **What the specification says.** CONV-CODE-004: "Reflection SHALL NOT be used where a type,
  a generic or a source generator serves", and AC2: "No use of `System.Reflection` outside
  the model builder and tests". This is enforced by `PublicSurfaceTests.CONV_CODE_004_AC2_NoShippedFileUsesReflection`.
  - Reading an assembly attribute is `System.Reflection`.
  - No type or generic carries the version.
  - `08` names no source generator or build step that writes a value into source.
- **Reading A.** Reading the stamped version is a use of reflection that no type,
  generic or generator serves, so CONV-CODE-004 admits it. AC2 is read with it.
  - Smallest fix: one private method in `LeakedPasswordCorpus` reads `AssemblyInformationalVersionAttribute`, and the AC2 test's exemption list gains that one file beside the model builder.
  - This is the patch in hand; its test passes.
  - Under this reading, one more point is open. The informational version carries the source revision after `+` (for example `0.0.0-alpha.0.34+52e4b29...`), so the commit identifier would go to the provider. Trimming at `+` sends the package version alone.
- **Reading B.** AC2 stands as written, and the build writes the version into source.
  - Smallest fix: one MSBuild target in `Janus.Hosting`, run after MinVer computes the version, writes a generated file holding an `internal const string` of the package version, marked as generated code (`08`, generated code).
  - `08` would need to name the pattern.
- **The patch** is kept out of history until the answer arrives.

- **Settled by D-168**, applied in `0f4c5e1` and `ad7acd9`.

**2. Tier 3. The "PRIV-RESTRICT-005c AC6 tension" (D-166 E.2).**

- No item of that name exists. No report, ledger entry or working note holds the original statement of the tension; only the label was carried forward. The nearest item is PRIV-RIGHT-005c, whose AC6 is the one criterion the label fits. The text puts it in tension as follows.
- **PRIV-RIGHT-005c AC6:** "No requirement obliges an application to maintain a per-schema redaction routine."
- **PRIV-RIGHT-005b** in `04` says the same in prose: "Applications no longer maintain a redaction routine. D-082 removed that requirement".
- **Against that, the same item's table** gives the host application, on `ErasureRequested`, the work: it "Redacts personal fields wherever it holds them".
- **Chapter 10's `ErasureRequested` row** reads "host-side redaction is due (PRIV-RIGHT-005b)", with "Every registered subject-event handler, **required**".
- **The code enforces the table:** a deployment with a sensitive type and no registered handler does not start (`HandlerCoverageTests` and `StartupValidationTests`, under PRIV-RIGHT-005b AC3).
- **The question.** Is a host that holds personal fields in its own tables obliged to redact them on `ErasureRequested`, or not? AC6 says no requirement obliges it; the table, chapter 10 and AC3 say one does.
- No code was changed.

- **Settled by D-168**, applied in `0961899`.

**3. Tier 2. D-166 R3 and R1 against the working guide's section 9: third-party lists that hold the names of AI tools and their vendors.**

- **Items.**
  - D-166 R3: "Ship the list unmodified, with its header, as an embedded dated resource". AUTH-FACT-010, Values (D-166): "The list ships unmodified, with its header".
  - D-166 R1 and AUTH-PASS-004: the English list is Alan Beale's 3esl list from the 12dicts 6.0.2 package, filtered at build time.
- **What the code needs.** Both lists as files of the repository, from which the build embeds them.
- **What the working guide says.**
  - Section 8: no "file that names an AI tool, model, agent, assistant or vendor".
  - Section 9: "Never let a tool, model or vendor name into the repository or its history."
- **What the lists hold.**
  - **The Public Suffix List** (version 2026-09-24_13-26-36_UTC). Its private section holds the entries of seven vendors of AI models, assistants and coding tools: a comment naming each vendor, its address and the person who submitted the entry, then the suffixes of its products. They are at lines 12331 to 12339, 14098 to 14099, 15164 to 15167, 15264 to 15265, 15482 to 15514 and 16373 to 16376.
    In the ICANN section, the comment at line 10853 names a registry company that shares a model's name.
  - **The 3esl list.** Six ordinary English words that are also the names of AI tools or models, at lines 4017, 4451, 7909, 11274 and 21547; line 14210 holds a vendor's name inside a longer word. The build keeps five of them as words the dictionary source refuses; line 7909 is capitalised and dropped.
- **Reading A.** Sections 8 and 9 concern traces of the tooling that produced the work. A third-party list carried as it is published, holding these names as data, is not such a trace.
  - Smallest fix: none. `9b77185` is pushed as it is, and the held R3 patch is committed as it is.
- **Reading B.** The rule applies to every file in the repository, whatever its origin.
  - **For the Public Suffix List**, one of two fixes:
    - The build downloads the list from publicsuffix.org into the intermediate output, at most once a day, and embeds it; the repository never holds it. Every build then needs the network, and "refreshed at every release" becomes "refreshed at every build".
    - The list is carried with those entries removed. It is then no longer unmodified, so AUTH-FACT-010 and D-166 R3 would need to change.
  - **For the 3esl list:** the vendored copy without those six lines, stated in `NOTICE`. The build still counts well over 10,000 words.
  - Under reading B, `9b77185` leaves the local history before anything is pushed.
- **Settled by D-169** (reading A). `9b77185` stands, and the R3 work is committed as it was staged, in `bb59be1`.

**4. Tier 2, with one Tier 3 point. D-166 D.8 break-glass part (7), settled 302 option 1: where the reason given at use is kept and carried.**

- **Item.** OPS-BOOT-002; `09` `POST /auth/break-glass`; `10` section 5.24: "Every record a
  break-glass session writes carries the reason given at its use". The request shape
  `{credential, reason}` and its 400 (the reason trimmed, 1 to 1024 characters) are
  determined.
- **What the code needs, and what no chapter settles.**
  - **(a) Where the session keeps the reason.** `identity.sessions` has no column for it.
  - **(a2) Tier 3: whether the text is sealed.** OPS-BOOT-002 says "what a break-glass session records under it is sealed as any account's is". No chapter says whether the reason, kept by the session and written into the trail, is such a record, sealed under the reserved account's subject key, or plain text. This touches keys and erasure; no proposal is made.
  - **(b) Where an audit record carries it.** `audit_records.principal_reason` is held by a check constraint to rows with a principal, and `09` has `GET /admin/audit` return `principalReason` for background work only. Every other stated reason (grant, role, takedown, recovery approval, configuration) is written as `details.reason`, which an action of the session that carries its own reason already fills.
  - **(c) How a writer knows it writes for a break-glass session.** The seventeen audit writers take explicit identities; none takes the session or the access context, and `08` names no request-scoped carrier.
- **Readings.** Each uses one request-scoped holder, internal to `Janus.Identity`, set where the request's session is resolved and read by `AuditStore.AppendAsync`:
  1. The reason under a new details key. Smallest fix: the holder, one key name (which `10` does not give), and the session column.
  2. The reason in a new nullable `audit_records` column. Smallest fix: the holder, the column and the session column; the trail read does not return it unless `09` adds it.
  3. The reason in `principal_reason`, its constraint relaxed to hold it beside a person's identity, and read back as `principalReason`. Smallest fix: the holder, the constraint change, the session column and the `09` description of the field.
- Under every reading, `auth.breakglass.used` carries the reason too.
- **What settles it.** The reading, the details key or column name, and point (a2).
- Nothing of part (7) is committed; parts (1) to (6) are (section 1).
- **Settled by D-170**, applied in `a1f64a1`.

**5. Tier 2. D-166 D.8, 319 fix (2): whether it governs bootstrap's seed.**

- **Item.** D-166 319 (2): "`before` is the written form of the value in force, the default
  where no row stood, and null only for a required key with no row." It stands among the
  fixes of `configure`; entry 315 (revised by entry 319) had bootstrap's seed record the
  row's text, null where no row stood.
- **What the code does.** `configure` records the value in force (`c9f901f`). Bootstrap's
  seed still records `before` as null for every value it writes, since it writes each
  where no row stands.
- **Readings.**
  - A. Fix (2) governs `configure`, whose paragraph it is. Smallest fix: none.
  - B. Fix (2) governs every configuration record, bootstrap's seed included. Smallest fix: the seed records the written form of each key's default as `before`, null only for a required key; one test carrying OPS-CFG-005.
- **Settled by D-170** (reading B), applied in `aec590e`.

**6. Tier 3. D-166 D.8 break-glass part (2): the limit-reached alert where no reserved account exists.**

- **Item.** D-166 D.8 (2): the first arrival the global limit refuses raises
  `auth-failures-sustained` "scoped to the reserved account".
- **What the code does** (`44dfdc5`). The scope is the reserved account's subject as
  `IEmergencyAccount` finds it; where none is found (a deployment never bootstrapped) the
  alert is raised with no scope. No chapter states what the alert is, or whether it is
  raised, where no reserved account exists. This is an alert on the break-glass gate; no
  proposal is made. The committed behaviour stands until the owner decides.
- **Settled by D-170**, applied in `5ad27b4`.

**7. Tier 2, with one Tier 3 point. D-166 D.8, 121 and 336: the secret path.**

- **Item.** D-166 121 and 336; LIB-HOST-001 and LIB-EXT-001 in `07`; CONV-DESIGN-007 in
  `08`: every secret is read once through `ISecretSource`, asynchronously, in the startup
  hosted service, before the server serves; no secret is an argument of `AddJanus`; every
  member of the source, and `IUnitOfWork.BeginAsync` and `CommitAsync`, return a `Result`.
  Nothing of the paragraph is committed.
- **(a) Tier 2. What the paragraph applies before 340, 343 and 215.** It lists the source's
  members as they stand after 340 (`ReadSignOnSecretAsync` removed; `SignOn` needs the
  sign-on secret until 340 replaces it), 343 (`ReadProviderCredentialAsync`, answering the
  `ProviderCredential` 343 introduces) and 215 (`ReadMailServerSecretAsync`, asked only
  where the shipped mail server adapter, which 215 builds, is used). 340 and 343 are in D.9
  and 215 in D.6; none is applied.
  - Reading 1: apply 121 and 336 now with the members that exist, keep
    `ReadSignOnSecretAsync` (read at startup like the others) until 340 removes it, declare
    `ReadMailServerSecretAsync` now, asked by nothing until 215, and leave
    `ReadProviderCredentialAsync` to 343. Smallest fix: the owner confirms the order.
  - Reading 2: apply 340, 343 and 215 first, then 121 and 336 whole. Smallest fix: the
    owner confirms the order.
- **(b) Tier 3. Where the secrets are held between the startup read and their use.** Today
  `AddJanus` takes the key-encryption keys, the fingerprint keys, the sign-on secret and
  the maintenance credential as arguments: the factories of about 30 store registrations
  close over the first two, the provider's token protection derives its keys from the
  key-encryption keys when OpenIddict is configured, and `AuditRetention` and
  `RestoreTest` build a second storage area with them. A read in the startup hosted
  service gives none of these the values when services are registered, so the values
  must be held for the process's lifetime from the read to each use. CONV-CODE-007 has
  secrets live in `byte[]` inside a method and be cleared after use; no chapter says where
  key material read at startup is held, for how long, or what a use before the read
  answers, and `08` names no holder. This concerns keys; no proposal is made.
- **(c) Tier 2. What a caller does with the `Result` of `BeginAsync` and `CommitAsync`.**
  `10` names no failure either returns (a transaction's failure is a fault and throws,
  CONV-ERR-001), so the result is always success. Of the 437 calls in `src`, 350 are in
  methods that return a `Result` and pass a failure on. 87 calls in 40 methods return
  none: middleware `InvokeAsync`, sweeps and rotation passes returning counts, and store,
  alert and delivery helpers returning `bool`, `Error?` or a value.
  - Reading 1: each such method returns a `Result` (or its `Error?` carries the failure) up
    to a caller that returns one; middleware answers it as any failure is answered.
    Smallest fix: internal signatures only.
  - Reading 2: a caller with no `Result` to return treats a failure as a fault and throws,
    the idiom the configuration reads use under X2. Smallest fix: one `Match` per call.
- **Settled by D-171**: (a) 340, 343 and 215 first; (b) the key ring; (c) passed up or thrown as a fault. Being applied (section 2).

**8. Tier 3. D-166 340 against LIB-TEST-001 AC4 and AUTH-OIDC-006 AC1: how the conformance suite authenticates as a registered client.**

- D-166 340: no person chooses, supplies, carries or is asked for a client secret; the
  library draws it at the first registration, holds it wrapped, reads it only for its own
  client half, and rotates it at `token.signing.rotation`. `register-client` loses the key
  document member `clientSecret`.
- LIB-TEST-001 AC4 and AUTH-OIDC-006 AC1: the suite, which the host runs, asks the
  deployment's provider "as a registered client" for each retired form. The shipped
  surface is `ConformanceSuite.ProviderAsync(HttpClient, Uri, ConformanceClient,
  CancellationToken)`, and `ConformanceClient(ClientId, Secret, Destination)` takes the
  secret from the host; the probe sends it as `client_secret` on every probe but the one
  that asks whether a client that does not authenticate is refused.
- After 340 no host holds a registered client's secret, and a secret read once goes stale
  at the cadence. No chapter, D-166 paragraph or D-171 item says how the suite
  authenticates. The sample host's conformance test registers the relying party with a
  `clientSecret` and hands the same bytes to `ConformanceClient`; under 340 its provider
  probe cannot authenticate.
- This decides who outside the library may hold a client secret, and changes the shipped
  `Janus.Conformance` surface. No proposal is made.
- **Settled by D-172**: the probes are made by the sign-on's client half through a contract in `Janus.Core`. Not yet applied (section 2).

**9. Tier 3. D-166 316: the reserved identifier of the deployment key.**

- 316 holds the deployment key "as a row of `subject_keys` under a reserved identifier no
  subject is issued and erasure never touches", and names no identifier.
- `ced2208` uses the nil subject, the one identifier `10` reserves; it is also the acting
  identity of every system principal's work (IDN-AUD-001). `SubjectEraser` refuses it.
- Which identifier holds the key, and whether the nil subject may, concerns keys and
  erasure. No proposal is made; the committed choice stands until the owner decides.
- **Settled by D-172** (the max UUID of RFC 9562), applied in `c656253` and `ae04c76`.

**10. Tier 3. D-172 item 2: re-binding the values under the deployment key.**

- D-172 item 2: the migration that moves the deployment key's row to the max UUID also
  re-encrypts, in the same transaction, every value bound to the key's identifier, so
  every value still decrypts; and every value under the deployment key is bound in its
  additional data, in the subject identifier's place, to its own row's identifier.
- **The migration holds no key.** The values bound to the nil subject are AES-256-GCM
  ciphertexts with the nil subject in their additional data: `invitations.encrypted_identifiers`,
  `mailboxes.encrypted_canonical` where no holder stands, and `send_outbox.message` where
  the row names no subject. Each is under a data key of its own row, wrapped under the
  deployment key, which is wrapped under the key-encryption key. Changing the additional
  data means decrypting and encrypting again. Migrations run in the pipeline step under
  `identity_migrate` (OPS-MIG-001, OPS-MIG-003), which holds no key-encryption key, and
  the database has no AES-GCM or RFC 5649 of its own. Moving the key's row alone is
  possible in SQL, since the `subject_keys` wrap binds no subject; the re-encryption is not.
- **Some values have no additional data.** The values held directly under the deployment
  key are RFC 5649 key wraps, which take none: `signing_keys.private_key`,
  `preauthentication_sessions.signon_verifier`, `provider_attempts.verifier`, and, with
  340, `oidc_clients.secret` and `previous_secret`. The rule that such a value is bound to
  its own row cannot hold for them without a change to how they are written, and those
  already stored would need the same re-encryption.
- **The invitation's binding.** D-172 cites the invitation as the example ("as an
  invitation is", D-166 234). 234 is in D.6 and not yet applied: an invitation's
  identifiers are bound today to the nil subject, as `ced2208` found them.
- Where the re-encryption runs and with which key material, or whether the migration
  refuses where such values stand (as `MoveValuesUnderTheDeploymentKey` refuses where a
  value is still wrapped under the key-encryption key), is a decision on keys. No proposal
  is made. Nothing of item 2 is committed; the 340 work stays uncommitted in the working
  tree, as before.
- **Settled by D-173**: the field cipher's values bound to their row, RFC 5649 wraps unbound, the migration refusing instead of re-encrypting. Applied in `c656253`.

**11. Tier 3. PRIV-RIGHT-005a and D-172 item 2: which tables are "tables of subjects".**

- PRIV-RIGHT-005a and D-172 item 2: "a check constraint keeps it out of every table of
  subjects other than the subject-key table"; criterion 18: "a row of any other table of
  subjects carrying the max UUID is refused by the database". No chapter, `10` row or
  log entry defines "table of subjects", and the phrase appears nowhere else.
- The schema as it stands:
  - tables whose primary key is the subject: `accounts`, `account_preferences`,
    `erasures`, `grant_versions`, `key_ceremonies`, `passwords`, `profile_photos`,
    `profiles`, `recovery_code_sets`;
  - tables whose primary key begins with the subject: `consents`, `objections`,
    `identifier_backup_settings`, `recovery_codes`, `recovery_approvals`;
  - columns naming a subject in other tables, most under a foreign key to
    `accounts(subject)`, and some under none: the audit records' acting and effective
    identities, `grants.subject_id` and `granted_by`, `devices.subject_id`,
    `registration_sessions.provisional_subject`, `resources.subject`,
    `send_outbox.subject`, `verification_codes.holder`.
- The scope decides where the database refuses the deployment key's identifier. No
  proposal is made. The constraint and its test wait for the answer; the rest of the item
  is in `c656253`.
- **Settled by D-174** (every column that can hold a subject identifier), applied in `ae04c76`.

**12. Tier 2. D-166 343 against LIB-HOST-001 AC2 and `10`: how a malformed social provider declaration is named.**

- **What the code needs.** The details of the startup refusal for a social provider
  declared twice, naming a factor that is not a social provider, with an address that is
  not absolute `https`, a `return` whose path does not end as it must, or no client or an
  empty one. Today these are refused with `model.startup.declarationmissing`, and
  `ErrorCodes` has no member for `model.startup.declarationinvalid` yet.
- **What the specification says.** D-166 343: `model.startup.declarationinvalid`,
  `details.key` naming the member. `07` LIB-HOST-001 AC2 and the `10` row: a malformed
  declaration is named by `details.declaration` and `details.field`, and the row lists
  "a social provider declared twice or with a member outside its rule" among them
  (`details.key` is kept for the landing origins alone). `10` gives the value of
  `details.declaration` for a template (the message kind) and an encrypted field (the
  type), not for a social provider, nor what `details.field` names for one declared twice.
- **Readings.**
  1. `details.key` `socialProvider.<member>`, as the log has it. Smallest fix: `10` and
     AC2 name `details.key` for social providers, as AC6 does for the landing origins.
  2. `details.declaration` `socialProvider`, `details.field` the member (`provider`,
     `metadata`, `configuration`, `return`, `clientIds`). Smallest fix: the `10` row
     states that value.
  3. `details.declaration` the provider's name as its route names it (`apple`),
     `details.field` the member, as the template and the encrypted field are named.
     Smallest fix: as 2.
- The rest of 343 reads as settled; nothing of it is committed.
- **Settled by D-175** (`details.declaration` `socialProvider.<provider>`, `details.field` the member or `provider`), applied in `2aa2733`.

**13. Tier 2. D-166 215 point 2 against LIB-HOST-001: when the mail server adapter is registered.**

- **What the specification says.** `07` LIB-HOST-001's **Mail server** row and the
  defaults table of section 4, the `10` row of `integration.mailserver.endpoint` and
  D-166 215 point 2: the library registers the JMAP adapter "while" the key is set and
  the host registered no `IMailServer`; absent, nothing is pushed or compared and every
  app-password operation answers `identity.mailbox.notfound`. The start reads the mail
  server's secret only while the key is set. The key is protected (`10` section 4.8,
  the list of OPS-CFG-004), and `configure` writes it to the settings table.
- **What the code needs.** `AddJanus` makes every registration before anything can be
  read, and the key lives in the settings table. Every consumer (the mailbox publisher,
  the reconciliation, app passwords, invitations, the declaration check of the mail
  server's client) reads an `IMailServer` not registered as no mail server. The key
  cannot decide a registration when registrations are made.
- **Readings.**
  1. Decided once, at the start: the service that fills the key ring reads the key, and
     where it is set and the host registered none, the adapter is the mail server until
     the process stops; a change by `configure` takes effect at the next start, as the
     secret it needs is read only then. Smallest fix: `07` and the `10` row say "set when
     the application starts", and `08` names how a registration decided at the start is
     resolved (a factory answering none, or a holder the consumers ask), since neither
     exists.
  2. Decided at each use: the adapter is always registered where the host registered
     none, and each consumer reads the key before it acts, so a change by `configure`
     takes effect at once. Smallest fix: `07` states that rule and what an endpoint set
     after the start answers while the ring holds no secret for it.
- **Also in 215, points 4 and 5.** `enabled` is written as `authenticate` not in
  `disabledPermissions`, while the listing reads an account under `Replace` without
  `authenticate` in `enabledPermissions` as disabled. An `enabled` push to such an
  account succeeds and is found as drift at every reconciliation. Readings: (a) as
  written, the drift is the signal; (b) under `Replace`, `enabled` also adds
  `authenticate` to `enabledPermissions`. Smallest fix for either: one sentence in 215
  point 4 or INT-MAIL-001's paragraph of values.
- The shapes of point 6 were read from the mail server's object reference and match
  D-166 215: a set is an object mapping each member to `true`. Nothing of 215 is
  committed.
- **Settled by D-176** (reading 1, the mail server in use chosen once at the start; and
  reading (b)), applied in `d5a2b03` and `9e7605d`.

**14. Tier 2. D-166 215 and INT-MAIL-001 against the `07` Mail server row: two things the adapter is not given.**

- **(a) The mailbox's identifier.** INT-MAIL-001's paragraph of values and D-166 215
  point 4: every create carries the library's identifier of the mailbox as its
  `description`, an account the server already holds is adopted only where its
  `description` carries it, and the refusal names the mailbox by it. The `07` Mail server
  row gives a push three things: a key stable until the server confirms it, the
  canonical address and the state. `MailboxPush` is those three; its key is the push's
  own (INT-MAIL-006), a new one for each change, not the mailbox's.
  1. A push also carries the mailbox's identifier. Smallest fix: the `07` row names it;
     `MailboxPush` gains it (PublicAPI.Unshipped).
  2. The adapter looks the mailbox up by address in the library's store. Smallest fix:
     `07` says so. Under a `replace` of the former mailbox two mailboxes stand at one
     address, so the lookup is not unique.
- **(b) How a refusal to adopt crosses `IMailServer`.** INT-MAIL-001: such a push fails
  and raises `degradation` at once, without waiting for `outbox.retry.maxattempts`.
  `ProvisionAsync` answers a result, and the publisher counts every failure as one
  attempt and alerts when the attempts are spent (the `10` row of scope
  `mailbox.push:<mailbox id>`). To fail at once it has to tell this refusal from a
  failure a retry may cure, and `10` names no code for it.
  1. A code of its own, which any `IMailServer` may answer; the publisher marks the push
     failed at once and raises the push's existing `degradation`. Smallest fix: a `10`
     section 1 row, a sentence in the `07` Mail server row, and the `10` alert row
     naming this case beside the spent attempts.
  2. As 1, with an alert of its own scope and details. Smallest fix: as 1, and a new
     row in `10` section 5.23.
- **(c) A `replace` of the former mailbox.** The old mailbox's removal and the new one's
  create name one address. The publisher runs in reservation order, so the removal goes
  first; where it fails and the create is sent in the same pass, the create meets the
  old account and, under (b), fails for good.
  1. A create waits while another mailbox at its address is owed `removed` and not yet
     confirmed. Smallest fix: one sentence in INT-MAIL-007.
  2. As written: the alert is the signal. No change.
- Also read before stopping, from the mail server's documentation: the management
  objects are reached at `<endpoint>/jmap`, as D-166 215 says, and a request carries no
  `accountId`. Nothing of 215 is committed.

- **Settled by D-177** (a) reading 1, (b) reading 2, (c) reading 1 narrowed. Applied in
  `3cef29a`, `9e7605d`, `78b1141` and `e4a2c75`, and (c) in `e64141d`.

**15. Tier 3. REG-MAIL-003 and PRIV-RIGHT-005: an erased holder's mailbox and `identity.invitation.mailboxheld`.**

- REG-MAIL-003, the `10` row of `identity.invitation.mailboxheld`, `10` section 5.44 and
  `13` R-M27: an invitation of a corporate address whose mailbox has been held before, by
  anyone, an erased holder included, is refused unless it names `formerMailbox`. The
  issue has to recognise, by the address, a mailbox whose last holder was erased.
- `04` PRIV-RIGHT-005, its text and criterion 8: erasure neutralises the fingerprint of
  every mailbox the subject holds or last held, and the address is a personal field under
  that account's key, which erasure destroys. Nothing left identifies the address. The
  same paragraph of `04` says no invitation gives the mailbox out again without
  `formerMailbox`. Kept entry 222 says the neutralised fingerprint leaves the address free
  for a later invitation, which reserves it as a row of its own, and INT-MAIL-007 counts
  the account left at the server as unknown. D-166 221, REG-MAIL-003 criterion 6 and
  INT-MAIL-006 criterion 6 do not name an erased holder.
- The refusal for an erased holder needs something that identifies the address to
  survive erasure, which criterion 8 does not leave. This is erasure. No proposal is made.

**16. Tier 2. REG-MAIL-003 and INT-MAIL-007: how two mailboxes stand at one address under `replace`.**

- **What the specification says.** Under `replace` the old mailbox is owed `removed` and
  a new one is reserved at the same canonical address; the new one's push waits until the
  server confirms the removal (REG-MAIL-003, INT-MAIL-007 criteria 5 and 7). No chapter
  says how the old row is marked or how the two rows stand together.
- **What the code needs.** `ux_mailboxes_fingerprint` is unique over every fingerprint
  that is not neutral (its reason was entry 221, now superseded), and the store finds one
  mailbox by address. A mailbox someone has held has no way to be owed `removed`: a
  release is for a reservation nobody took, and `ck_mailboxes_released` holds it there.
- **Readings.**
  1. A nullable `mailboxes.replaced_at`: a `replace` sets it, a mailbox with it set is
     owed `removed`, the unique index admits only rows without it, and the store finds by
     address the mailbox not replaced. Smallest fix: one sentence in REG-MAIL-003 or
     INT-MAIL-006 naming the mark.
  2. A `replace` neutralises the old row's fingerprint, so the index and the lookup are
     unchanged. Against it: the neutral fingerprint is erasure's mark, which would gain a
     second meaning for a living holder's mailbox, and a held mailbox still needs a way to
     be owed `removed`. Smallest fix: as 1, naming the neutral value.
- **Settled by D-178** (15: erasure stands, an erased holder's address is one never held;
  16: reading 1, the mark fitting a replaced and a released mailbox). Applied in `e64141d`
  and `1566815`.

**17. Tier 3. D-166 263 and REG-MAIL-002 against OPS-BOOT-002: an app password asked for from the break-glass session.**

- The request: `POST /account/mail/apppasswords` from the break-glass session. The
  reserved account holds no mailbox (OPS-BOOT-002).
- D-166 263, REG-MAIL-002 criterion 3 and `09` section 6: the app-password operations are
  present only where the account is `active` or `restricted` and holds a mailbox the mail
  server is told to enable; otherwise each answers 404 `identity.mailbox.notfound`. The
  route's 403 answers are `auth.stepup.required` and `authz.restricted`.
- OPS-BOOT-002 and criterion 9, and D-166 D.8 part (3): from the break-glass session
  `mailcredential:create` is refused with 403 `authz.denied`, whatever the reserved
  account's policy lists. D.8 part (3) names
  `BreakGlassEndpointTests.OPS_BOOT_002_NoSignInMethodIsGivenToTheReservedAccountAsync`,
  which sends this request and expects 403 `authz.denied`.
- Before 263 both held, since a missing mailbox also answered `authz.denied`. With 263
  the request answers 404, which fails OPS-BOOT-002 criterion 9; answering 403 fails
  REG-MAIL-002 criterion 3 for the reserved account. Which refusal the break-glass session
  meets is a question of its gate. No proposal is made.
- **Settled by D-179** (403 `authz.denied` at the gate step, for the nine actions, from
  both sessions). Applied in `8e570fd` and `a5a295e`.

**18. Tier 2. D-166 121 and 336 against LIB-HOST-001 and `10`: how an absent secret source is named.**

- **What the specification says.** The `07` Secret source row: absent, startup fails with
  `model.startup.declarationmissing`. LIB-HOST-001 criterion 2: `details.key`,
  `details.handler` or `details.supplier` names the omission. The `10` row lists the
  declarations' `details.key` spellings (`passkeyAddresses`,
  `authenticationAddresses.signIn`, `authenticationAddresses.provider`,
  `signOnClient.clientId`, `landingOrigins.authentication`, `landingOrigins.account`,
  `mailServerClient.clientId`, `mailTransport`, `smsTransport`, `imageCodec`,
  `dnsResolver`). None is the secret source's, and no chapter or D-entry spells it.
- **What the code needs.** Once every key comes only from the source, a start without one
  is refused, first, in the ring's start. Today an absent source is refused only where a
  social provider is declared, as that provider's credential unavailable.
- **Readings.**
  1. `secretSource`, as the `07` row's name is written in the other spellings
     (`imageCodec`, `dnsResolver`, `mailTransport`, `smsTransport`). Smallest fix: the
     `10` row lists it.
  2. `ISecretSource`, the contract's own name. Smallest fix: the `10` row lists that.
- The rest of 121 and 336 reads as settled; nothing of it is committed.
- **Settled by D-180** (`secretSource`). Applied in `d129edc`.

**19. Tier 2. CONV-DESIGN-007 and the gate of INT-MAIL-009 criterion 2: how an optional declaration reaches `KeyRingService` and `SendingValidation`.**

- **What the code does.** An optional host declaration reaches the service that takes it
  through a factory registration passing `GetService<T>()` (the clock reference, the
  certificate renewer, the restore-test instance, the location source, the image codec,
  the erasure ledger). `KeyRingService` (`2aa2733`, for `IMailServer` and
  `ISecretSource`) and `SendingValidation` (`de42f15`, for `IMailTransport` and
  `ISmsTransport`) instead take them as constructor parameters defaulting to `null`,
  resolved by the container's activation: a second way of doing one thing (the working
  guide's section 4).
- **Why each took it.** `SendingValidation`'s factory would name `IMailTransport` in
  `HostingRegistration.cs`, which already names `IMailServerInUse` and
  `IMailServerTokens`; the gate of INT-MAIL-009 criterion 2 searches every source for
  the text `IMailServer` and the text `IMailTransport` and fails on a file holding both.
  `KeyRingService` is placed among the hosted services by type, and
  `StartupValidationTests.AUTHZ_MODEL_004_AC2_TheChecksStartBeforeEverythingElseRegistered`
  reads each hosted service's implementation type, which a factory registration does
  not carry. We tried the factory form on this branch; each fails where said, and it was
  not committed.
- **Readings.**
  1. The null defaults stand as the way for these two, recorded. Smallest fix: none in
     the code; `08` CONV-DESIGN-007 names it beside the factory form.
  2. The factory form everywhere: the INT-MAIL-009 gate reads the two contracts' names as
     whole words, and the ordering test reads a factory-registered hosted service by the
     type it answers. Smallest fix: the two tests, and the two registrations.
  3. The factory form, with the two registrations in a file of their own that names one
     side, as `OidcRegistration` and `KeyRingRegistration` are. Smallest fix: the
     registrations, and the ordering test as in 2.
- **Settled by D-180** (reading 3). Applied in `38933fe`.

**20. Tier 3. CONV-CODE-007 criterion 4 and D-171 against AUTH-KEY-001: how a replaced signing credential leaves the server's options.**

- **What the specification says.** CONV-CODE-007 and D-171 point 1: the only key
  material outside the ring is the OIDC provider's credentials in its own server
  options; a signing credential a rotation replaces stays for the overlap of AUTH-KEY-001
  (`oidc.accesstoken.lifetime` plus 5 minutes) and leaves the options when the overlap
  ends. D-162 items 58 and 59: the signing credentials come from the library's key store
  through "a credential source the rotation job updates in process".
- **What the code does.** `OidcRegistration` adds `SigningCredentialSource.Current` to
  `OpenIddictServerOptions.SigningCredentials` once, when the options are first built,
  and the options keep that credential for the life of the process. Each token is signed
  with the credential `TokenSigning` reads from the source per request; validation and
  the key set read the store. The source holds the replaced key until the next rotation,
  not until the overlap ends. A rotation happens at the first read of the signing key
  past the cadence (`SigningKeys`), not in a job, and the replaced key's end
  (`RetiresAt`) is on the stored key and does not reach the source. The private key's
  bytes are zero once each credential is made.
- **What no chapter settles.** What makes the replaced credential leave when the overlap
  ends (a later read of the key, the expiry sweep, or a timer, which nothing in the
  library runs outside the worker); how the server's options are changed while requests
  read them (the options are cached by the options system, `08` names only
  `ValidateOnStart` for them, and a rebuild would also make the encryption credential
  again, which D-171 says changes only at a restart); and when the replaced key object is
  disposed while a signature begun with it may still run. Each touches the key
  material's lifetime, so no proposal is made. Nothing of it is committed.

- **Settled by D-181**, applied in `211b1d42`.

**21. Tier 2. D-166 414 under X4: the detail of an entered request.**

- **Item.** D-166 414 and X4: a request's detail is 1 to 1024 characters after trimming.
- **What the code needs.** `PrivacyRequestEntry.Detail` is a non-nullable string, and the
  endpoint passes `body.Detail ?? ""`, so an absent detail and a blank one cannot be told
  apart in the service.
- **What the specification says.** `09` section 8a gives the detail of an entered request
  as optional; X4 holds every free-text member to 1 to 1024 characters after trimming.
- **Readings.**
  1. `Detail` becomes `string?`; an absent detail is stored as the empty string. Smallest
     fix: the record's member and its `PublicAPI` line.
  2. The detail is nullable through the queue. Smallest fix: as 1, and a migration drops
     `NOT NULL` from the column.
  3. No surface change: the empty string is read as absent. Smallest fix: the rule in the
     service and at the endpoint.
- **Parked.** The detail rule of `EnterAsync` alone. `e747407f` built it while parked, as
  reading 3 (section 2).
- **Answer:** D-183. Built in `3963b184` (`part/privacy`), which brings `e747407f` to the answer.

**22. Tier 2. D-166 265 and LIB-HOST-001: the shape of a relationship source.**

- **Item.** D-166 265, the half it settles: the host declares its relationship sources,
  and the library builds its filter sources from them.
- **What the code needs.** A declaration with a shape. "Builds its `FilterSources` from it"
  also needs the ancestry, the grants and the resource type of each source.
- **What the specification says.** LIB-HOST-001 lists relationship sources among the
  declarations and gives them no shape; no chapter names the type.
- **Readings.**
  1. `RelationshipSource.Of<TRow>(name, Func<IServiceProvider, IQueryable<TRow>>)`,
     registered as a singleton. Smallest fix: one public type.
  2. An `IRelationshipSource` interface the host implements, registered scoped. Smallest
     fix: one public interface.
  3. A member of the model builder, `Relationship<TRow>(...)`. Smallest fix: one builder
     method.
- **Parked.** The declaration, its start check, the full answer of `GET /admin/access`, the
  drift-check job and its principal `derivation-driftcheck`, `RefreshAsync`, the
  AUTHZ-DERIVE-005 and AUTHZ-DERIVE-007 tests, ledger line 265.
- **Answer:** D-183. Built in `10e7acb6` and `6e496a64` (`part/authorization`), but for the audit record of question 101.

**23. Tier 2. D-166 143 and OPS-OBS-002 against LIB-API-005: who raises the loss of the registration channel.**

- **Item.** D-166 143: the stream that serves `GET /register/events` raises the
  degradation `registration-channel` through `IAlertChannels`.
- **What the code needs.** The endpoint, or something it holds, to reach `IAlertChannels`.
- **What the specification says.** `07` LIB-API-005 limits what an endpoint takes beside its
  contract to the pre-authentication binding, the session rotation and the registration
  signal; the gate of LIB-API-005 criterion 3 enforces that list.
- **Readings.**
  1. "Registration signal" covers raising its loss. Smallest fix: the gate's list widens.
  2. `RegistrationSignals`, a singleton, raises it itself. Smallest fix: the signals reach
     `IAlertChannels`, which is scoped, through a scope of their own.
  3. `07` adds `IAlertChannels` for this endpoint. Smallest fix: the chapter and the gate's
     list.
- **Parked.** 143 and its ledger line, with the `registration-channel` member of section F.
  The patch is held aside.
- **Answer:** D-183. Built in `1874f506` (`part/sending`).

**24. Tier 2. D-166 120 (2), INT-SMS-003 and `10` section 5.26: a document name on the publish route.**

- **Item.** D-166 120 (2): a governing document named outside the name rule is refused at
  the start.
- **What the code needs.** `POST /admin/documents/{document}/versions` and its translation
  route take any route segment, and the alert `governing-text-missing` carries that name
  into the document place, whose width is 64.
- **What the specification says.** Nothing on what the publish route does with a name
  outside the rule or one no deployment declares.
- **Readings.**
  1. The route refuses a name outside the rule. Smallest fix: a code and status the `09`
     row does not yet name.
  2. The route refuses any name that is neither `privacy-notice` nor a document a purpose
     declares. Smallest fix: as 1.
  3. As is: the width covers declared names only.
- **Parked.** Nothing; 120 is otherwise built.
- **Answer:** D-183. Built in `2247202a` (`part/privacy`); questions 97 and 98 follow from it.

**25. Tier 2. D-166 144 and 315 against entry 403 and the closed ledger: the retired `photo.enabled` family.**

- **Item.** D-166 144 and 315 retire `photo.enabled.<organization>`.
- **What the code needs.** `SettingsCatalogueTests.REF_001_AC1` (entry 403, kept) counts the
  ledger's owed rows of chapter 10 in both directions, and the ledger still owes
  `photo.enabled`. The ledger is closed.
- **What the specification says.** D-166 section F gathered the owed rows into `10`; section
  G gives no line that strikes the appendix row.
- **Readings.**
  1. Strike the appendix row. Smallest fix: one ledger change that section G does not give.
  2. The gate counts `10` alone now (entry 403, reading 1). Smallest fix: the test, and 403
     superseded.
  3. Drop the reverse assertion. This weakens the gate.
- **Parked.** The removal of the `photo.enabled` family, the photos half of X6, ledger line
  144. The patch is held aside.
- **Answer:** D-183. Built in `9bac4a40` and `dd009fb7` (`part/gates`).

**26. Tier 3. D-166 317 (2) and OPS-SEC-003 criterion 3: a retired version's unwrap as a coded refusal.**

- **Item.** D-166 317 (2): a failure of `PersonalFieldCipher.Unwrap` under a retired version
  carries `model.startup.secretunavailable`, `details.key` `keyEncryptionKeys` and
  `details.version`, "as a coded refusal".
- **What the code needs.** `Unwrap` returns `byte[]` and throws `CryptographicException`;
  about 24 callers in `Janus.Storage` return plain values; the only coded exception is
  `StartupException`, for faults at the start.
- **What the specification says.** The item names the code and its details, and not the
  path by which a refusal reaches a request from a value read in a store.
- **Parked.** 317 (2), ledger line 317.
- **Answer:** D-183. Built in `46968c21` (`part/privacy`).

**27. Tier 2. D-166 118: the governed send contract.**

- **Item.** D-166 118: a governed send contract in `Janus.Core`.
- **What the code needs.** An interface and an input type; neither is named.
- **What the specification says.** `07` pairs `INotificationHandler` with `SendRequest`,
  which 118 makes the admitted message.
- **Readings.**
  1. `IGovernedSend` with an `OutboundMessage` input; `SendRequest` becomes the admitted
     message (it loses `Purpose` and `Source` and gains `Reference`), and `DrawAsync` for
     119 (4). Smallest fix: one public interface, one public record, the changed record.
  2. The same interface; the input keeps the name `SendRequest`, and the admitted message
     takes a new name (`AdmittedMessage`). Smallest fix: as 1; `07`'s pairing is out of
     step.
- **Parked.** 118, 235, the twenty-sets test of 335, X1 at `SendingService.CarryAsync`,
  ledger lines 118, 235 and 335.
- **Answer:** D-183. Built in `af618c88` (`part/sending`); questions 107, 108 and 111 follow from it.

**28. Tier 2. D-166 156, PRIV-BASIS-001 and CONV-ENUM-001: the lawful basis table.**

- **Item.** D-166 156: the lawful basis table is seeded at the start.
- **What the code needs.** The table's and columns' names, its grants, its writer, what
  becomes of a row no declaration names, the rule for two starts at once, and the default
  labels.
- **What the specification says.** `10` gives none of these, and section 5.7 holds no label
  (the Basis column of PRIV-BASIS-001). `08` also lists the sensitive-data categories as a
  seeded table.
- **Readings.**
  1. A hosted service at the start upserts the declared bases and deletes the undeclared,
     in `identity.lawful_bases`, the label on the declaration, no table of sensitive
     categories. Smallest fix: one migration, one service.
  2. As 1, the undeclared rows kept.
  3. As 1, with a `sensitive_categories` table.
  4. The command or a migration writes the rows.
- **Parked.** 156.
- **Answer:** D-183. Built in `c972731e` (`part/privacy`).

**29. Tier 3. D-166 133 and 147 (1) to (4), AUTHZ-GATE-002 and PRIV-CONS-007: the consented resources against the document a purpose now names.**

- **Item.** D-166 133 and 147: the view `identity.consented_resources` admits only consents
  given against the document the purpose now names.
- **What the code needs.** That document lives only in the declaration held in memory; the
  view has no document column, and nothing stamps `superseded_at` when a declaration
  moves a purpose to another document.
- **What the specification says.** The values of AUTHZ-GATE-002 define the view without the
  document. A query through the view would admit what `AccessGate.Unconsented` refuses
  (AUTHZ-GATE-002 criterion 4, PRIV-CONS-007).
- **Parked.** 133 and 147 (1) to (3), the gate's reading of another document,
  `ConsentGateTests.PRIV_CONS_007_APurposeGivenAnotherDocumentAsksItsSubjectsAgainAsync`,
  ledger lines 133 and 147. Point (4) is in `c573f655`.
- **Answer:** D-183. Built in `ffa77d18`, `e20b2543`, `7903e534` and `b5fd0791` (`part/consented-resources`).

**30. Tier 3. D-166 133 and 147 (5) and PRIV-CONS-001: a grant while a live record stands.**

- **Item.** D-166 133 and 147 (5).
- **What the code needs.** What a grant, or an objection, does while a live record for the
  purpose stands. A unique partial index refuses a second live row.
- **What the specification says.** No chapter says.
- **Parked.** 133 and 147 (5).
- **Answer:** D-183. Built in `c3191b69` (`part/consent`); questions 102 to 106 follow from it.

**31. Tier 3. D-166 115 (2), AUTH-FACT-004 criterion 3, REG-SESS-005 criterion 1 and AUTH-ABUSE-003: verifying a code for a held identifier.**

- **Item.** D-166 115 (2): registration and identifier codes live in the verification-code
  record.
- **What the code needs.** A held identifier has no code row there, so a verify answers
  expired at once, where a fresh one answers invalid five times: the answer tells whether
  the identifier is held.
- **What the specification says.** No chapter says what a verify answers for a held
  identifier.
- **Parked.** 115 (2), and 306 whole, whose sweep needs that record's expiry
  (`identifier_verifications` holds `CodeExpiresAt` only inside `enc_staged`); the two
  `IdentifierServiceTests` and the storage test of 306; ledger lines 115 and 306. 115 (1)
  and (3) are in `6b79266c`.
- **Answer:** D-183. Built in `bae7b728` and `02ff4f0e` (`part/registration-codes`), but for what questions 115 and 116 park.

**32. Tier 2. D-166 419 and REG-SESS-003 criterion 6: a registration link token that opens nothing.**

- **Item.** D-166 419: such a token is counted through `ThrottleService.FailedAsync` against
  the source and the identifier's hash.
- **What the code needs.** `IRegistration.LandAsync` takes no source, and a token that opens
  nothing finds no session, so there is neither `session.Source` nor an identifier.
- **What the specification says.** Nothing on where the source comes from on that path.
- **Readings.**
  1. `IRegistration.LandAsync` takes a `string source`, as `IAuthentication.LandAsync` and
     `IIdentifiers.LandAsync` do. Smallest fix: one parameter and its `PublicAPI` line.
  2. Every registration operation takes the source. Smallest fix: the request address on
     every call.
  3. Count against the source of the landing browser's session, and nothing where there is
     none. Smallest fix: no surface change.
- **Parked.** That case alone, and ledger line 419.
- **Answer:** D-183. Built in `a2025e00` (`part/sessions`); question 96 follows from it.

**33. Tier 3. D-166 318 (3) against OPS-SEC-003: what a fingerprint key's retirement forgets.**

- **Item.** D-166 318 (3): `ForgetAsync` deletes only `send_grants` lines under a previous
  version and released username holds.
- **What the specification says.** `06` OPS-SEC-003: a keyed hash kept with no plaintext (a
  sign-in in progress, a throttle ledger line) is forgotten when its version retires.
- **What the code needs.** One rule. The log and the chapter differ on what retirement
  forgets.
- **Parked.** 318 (3), ledger line 318. 318 (1) and (2) are in `98eebc5c`.
- **Answer:** D-183. Built in `ea174da9` (`part/privacy`).

**34. Tier 2. D-166 136 against `10` `alerting.denials.threshold`: the actor of a principal's refusal.**

- **Item.** D-166 136 counts the actor of a system principal's refusal by the principal's
  name.
- **What the code needs.** After 136 a principal records the nil subject (the column is
  `NOT NULL`).
- **What the specification says.** `10` `alerting.denials.threshold`: the actor is the acting
  subject recorded, and refusals naming none count as one actor. Read with 136, every
  principal shares one actor, and the clause on refusals naming none never applies.
- **Readings.**
  1. By the principal's name where there is one, else by the acting subject (D-166).
     Smallest fix: the row of `10`.
  2. By the acting subject alone, the nil subject standing for every principal (`10`).
     Smallest fix: D-166 136's count.
- **Parked.** All of 136: the record, its migration, the explanation's principal and
  reason, `ExplanationTests.AUTHZ_CONCEAL_004_AC1_ARefusalOfBackgroundWorkNamesThePrincipalAndItsReasonAsync`,
  the audit store's test of IDN-AUD-001 criterion 1, ledger line 136.
  `OPS_ALERT_001_AC1_ARunOfRefusalsNamingNoOneIsRaisedAsync` turns on the answer.
- **Answer:** D-183. Built in `be757f8d` (`part/authorization`).

**35. Tier 2. D-166 303 (1) against `09` `GET /admin/audit`: the subject of an audit entry.**

- **Item.** D-166 303 (1) appends only `Principal` and `PrincipalReason` to the public
  `AuditEntry`.
- **What the code needs.** After 303 (2) the data subject lives only in
  `audit_records.subject`.
- **What the specification says.** `09` `GET /admin/audit`: each entry carries the data
  subject.
- **Readings.**
  1. `AuditEntry` and its view gain `SubjectId? Subject`. Smallest fix: one member and its
     `PublicAPI` line.
  2. D-166's shape stands, and `09` changes.
- **Parked.** `Subject` on `AuditEntry`, the view, its test. The rest of 303 is in
  `9cfccf3a`.
- **Answer:** D-183. Built in `6cd14e2f` (`part/authorization`).

**36. Tier 3. D-166 242 (4), AUTH-SESS-009 and IDN-LIFE-009b: a downgraded session, and where `auth.factor.notpermitted` is judged.**

- **Item.** D-166 242 (4).
- **What the code needs.** (A) A representation of a downgraded session: `Session` has no
  such state, and `StepUp.Proved` reads `Attained` and `AttainedAt`. (B) A place to judge
  `auth.factor.notpermitted` "in `AuthenticationService.AcceptsAsync` and
  `SessionService.PresentAsync` alike, as a sign-in does".
- **What the specification says.** (A) Nothing on how a downgrade is held. (B)
  `AcceptsAsync` is shared with sign-in and runs before the factor is verified, so a
  refusal there tells a caller not yet authenticated which factors the organization
  permits; a sign-in today refuses in `SessionService.BeginAsync`, after the factor
  succeeds.
- **Parked.** 242 (4), the downgrade and `auth.factor.notpermitted` halves, with
  `AuthenticationServiceTests.IDN_LIFE_009b_ASessionHeldBeforeTheMembershipIsDowngradedAsync`
  and ledger line 246. The `policyRequirement` half is in `e5b53dbd`.
- **Answer:** D-183. Built in `bd858a96` (`part/sessions`).

**37. Tier 2. D-166 135 and OPS-MIG-003a criterion 4: the `public` schema.**

- **Item.** D-166 135: `DatabaseRoleTests` reads every schema.
- **What the code needs.** Widened so, the test finds `USAGE` on `SCHEMA public` held through
  `PUBLIC`, PostgreSQL's default since version 15, which no migration grants.
- **What the specification says.** "Every schema", with no word on the default grant.
- **Readings.**
  1. Every schema means the library's schemas; `USAGE` on `public` through `PUBLIC` is left
     out. Smallest fix: the test alone.
  2. `(SCHEMA public, USAGE)` joins `AuthorizationModel.MaintenanceGrants`. Smallest fix:
     the serialized model and the truth table.
  3. A migration revokes `USAGE` on `public` from `PUBLIC`. Smallest fix: one migration,
     which changes the host's database.
- **Parked.** 135.
- **Answer:** D-183. Held by tests in `1d80d9d1` (`part/authorization`); nothing else changed.

**38. Tier 2. D-166 119 (1): an immediate attempt after the caller's commit.**

- **Item.** D-166 119 (1): an after-commit registration on `IUnitOfWork` makes the
  immediate attempt of `SendAsync` wait for the caller's outermost commit.
- **What the code needs.** `SendAsync` returns `Result<SendReference>`, and the reference is
  drawn when the message is carried, so inside a caller's transaction there is no outcome
  to return.
- **What the specification says.** 118 (question 27) moves the reference into the admitted
  message; 119 (5) says callers that act on the outcome send outside transactions.
- **Readings.**
  1. Build (1) now; `SendAsync` inside an open transaction faults
     (`InvalidOperationException`) until (2) and (3) move the callers to `Undertake`.
  2. Build only the `IUnitOfWork` registration now, and wire `SendingService` with 118, (2)
     and (3).
  3. Park all of (1) with 118.
- **Parked.** 119 (1) and what builds on it, (2) to (4); 227 and 322 with 119; ledger lines
  119, 227 and 322.
- **Answer:** D-183. Built in `af618c88` (`part/sending`); question 112 follows from it.

**39. Tier 3. D-166 119 (6) and PRIV-RIGHT-005a: the erased value in `send_outbox.wrapped_key`.**

- **Item.** D-166 119 (6).
- **What the code needs.** The layout of the erased value. The outbox has no
  `format_marker` column: `wrapped_key` is a bare 40-byte RFC 5649 wrap.
- **What the specification says.** `04`: an erased wrapped key is 32 zero bytes under marker
  `0x00`, and the outbox holds "the erased value (marker `0x00`, Values above)".
  `subject_keys` keeps the marker in a column of its own, and `MailboxStore`'s release
  zeroes the stored length without a marker. Whether the value is 33 bytes (`0x00` and 32
  zeros) or 32 zeros with the marker implied is not settled.
- **Parked.** 119 (6) and
  `SubjectEraserTests.PRIV_RIGHT_005_AC1_AnOutstandingMessageIsUnreadableAndUncarriedAfterErasureAsync`.
- **Answer:** D-183. Built in `c813a1b4` (`part/sending`); question 113 follows from it.

**40. Tier 2. D-166 152 (3) against AUTHZ-IMP-001 criterion 3: `Effective` on `CredentialSuspended`.**

- **Item.** D-166 152 (3): `CredentialSuspended` carries `Actor`, the reporting context's
  acting subject, "and `Effective` where it differs".
- **What the code needs.** To compare the acting and the effective identity.
- **What the specification says.** AUTHZ-IMP-001 criterion 3: "No feature reads them as
  differing", enforced by
  `AccessSeamTests.LIB_SEAM_002_AC2_NoFeatureReadsActingAndEffectiveAsDiffering`, which
  refuses `Effective ==` and `!=` in `src`. The documentation of `DomainEvent.Effective`
  says "where it was not their own".
- **Readings.**
  1. `Effective` is never set on `CredentialSuspended`. Every path today has the acting
     equal to the effective; only the reserved seam `AccessContext.Of(acting, effective)`
     loses it.
  2. `Effective` is always the context's effective identity. This departs from "where it
     differs" and from the documentation.
  3. Compare, and relax the gate. This weakens a gate.
- **Parked.** `Effective` on `CredentialSuspended`, ledger line 152. The rest of 152 is in
  `50cd2637`.
- **Answer:** D-183. Built in `a4a00c82` (`part/sessions`); question 95 follows from it.

**41. Tier 3. D-166 242 (3) and REG-DOM-001: an accepting account that holds no verified email.**

- **Item.** D-166 242 (3): at acknowledgement the lock is judged on the corporate address
  where one is taken on, else the bound email, else at least one verified email of the
  accepting account.
- **What the code needs.** The answer for an account signed in by phone, holding no
  verified email.
- **What the specification says.** REG-DOM-001's second paragraph says a phone sign-in uses
  no sign-in email, so the lock does not reach it; "at least one verified email" finds
  none, so it refuses.
- `2e8174c1` admits that case (the loop over no email returns no refusal); no test asserts
  it. A follow-up changes it if the answer is otherwise.
- **Parked.** That case.
- **Answer:** D-183. Built in `fdef37a9` (`part/sessions`).

**42. Tier 3. D-166 129 (1), `09` section 4 and REG-SESS-006: where a registration's open ceremony and unconfirmed generator are held.**

- **Item.** D-166 129 (1): `BeginKeyAsync` and the generator's begin under a registration
  session's `CredentialAuthority`.
- **What the code needs.** A place to hold, between begin and complete or confirm, the open
  WebAuthn ceremony (kind, challenge, expiry) and the unconfirmed generator (identifier,
  label, secret).
- **What the specification says.** `identity.key_ceremonies` has a foreign key to
  `identity.accounts`, and an unconfirmed generator is a row of `identity.authenticators`
  with the same key: both need an account row before the terms step, which 129 forbids.
  No chapter names where they go, and the answer decides where an unconfirmed generator's
  secret is stored.
- **Parked.** 129 (1) whole: the registration form of `CredentialAuthority`, `Asking`, the
  key and generator paths, the `RegistrationFlowTests` of REG-SESS-006 criteria 1 and 4
  and their counterparts, ledger line 129. 129 (2) is in `6e0c13e8`.
- **Answer:** D-183. Built in `7a64685a` (`part/registration-codes`).

**43. Process. CONV-VCS-003: two commit messages out of the rule.**

- **Item.** CONV-VCS-003 and its commit-message check.
- **What the code needs.** `863883c1` (on the pushed `corrections-4`) uses the type `style`,
  outside the chapter's list of types; the check was red on the push of `06ec9ba0` and
  will be red on the pull request. `c25b633a` has a body line of 76 characters, past 72.
- **What the specification says.** The history is not rewritten without the owner's word,
  and the default branch takes no force push.
- **Readings.**
  1. Reword both: a rebase of `corrections-4` from the parent of `06ec9ba0` and a force
     push of that branch, which is not the default one.
  2. Leave both; the pull request's commit-message check stays red.
- **Parked.** Nothing. The local message check now reads the type list as well.
- **Answer:** pending.

**44. Tier 3. D-166 169, 170, 254, 255 and 257 (3) against PRIV-RIGHT-004 and `10` section 5.12b: what a takedown's reversal keeps.**

- **Item.** D-166 169 and its group, point (3): `Account.ReverseTakedown` restores a held
  deletion, else a held suspension, else restricted where a restriction is held, else
  active, "and clears every held value".
- **What the specification says.** `04` PRIV-RIGHT-004 and `10` section 5.12b hold a
  restriction requested while the account is suspended or deleting, in force when the
  account returns.
- **What the code does.** `b1960a2e` keeps `RestrictionHeld`, and an out-of-band deletion's
  held suspension, on a reversal to a held deletion or suspension, as the chapter says.
- **Parked.** Nothing more; the reversal is committed as said, under the open question.
- **Answer:** D-183. `b1960a2e` stands; a test added in `0b11699d` (`part/privacy`).

**45. Tier 2. An out-of-band erasure by the account's state (`0149b6d6`): the fulfilment's codes against `09`.**

- **Item.** D-166 D.5, an out-of-band erasure by the account's state.
- **What the code does.** The fulfilment answers 404 `identity.account.notfound` where the
  subject has no account, and 409 `identity.account.stateconflict` where the window cannot
  begin.
- **What the specification says.** `09`'s fulfil row lists only 403, 404 for the request,
  and 409 for a request already decided.
- **Readings.**
  1. The `09` row gains both.
  2. Both are answered as a code the row already has.
  3. The fulfilment never refuses on the account; it records the request fulfilled.
- **Parked.** Nothing more; `0149b6d6` is committed with the codes of reading 1, and the
  ledger lines of the D.5 items are written.
- **Answer:** D-183. Built in `67b5c544` (`part/privacy`).

**46. Tier 2. D-166 328 and CONV-VCS-004: truth-table rows for the step-up gate.**

- **Item.** D-166 328 changes `StepUpGates.cs` and `ISessionGates.cs` in
  `src/Janus.Authorization/Gate`, a path the truth-table gate counts as permission logic.
- **What the code needs.** The table has no step-up outcome, and `HostFixture` registers no
  `IAssuranceProvider`. The step-up commits `d12ea4a2`, `44f99f4b` and `b7704d1b` touched
  no row.
- **What the specification says.** CONV-VCS-004 and AUTHZ-TEST-001 name relationship,
  permission and condition logic.
- **Readings.**
  1. The step-up gate is not that logic; no rows.
  2. `Decided.StepUpRequired` and two rows (a report that meets the gate, Allowed; an aged
     report, StepUpRequired) in a second fixture that registers a provider.
- **Parked.** Ledger line 328, and the rows under reading 2. The code of 328 is in
  `669bac7b`.
- **Answer:** D-183. Built in `d52cacfb` (`part/sessions`), but for the filter half of the unmet rows (question 92) and the conformance scenarios (question 93).

**47. Tier 2. D-166 389 (6) against CONV-VCS-005: the section of a changelog line.**

- **Item.** D-166 389 (6): a changelog line under `Unreleased` (Security).
- **What the specification says.** CONV-VCS-005: the first version's section records under
  Added alone.
- **Readings.**
  1. A `### Security` heading under `Unreleased`.
  2. The line under `### Added`.
- **Parked.** The line, with 389 (question 48).
- **Answer:** D-183. Built in `f805640f` (`part/sessions`).

**48. Tier 2. D-166 389 (2): the address a registration's first session records.**

- **Item.** D-166 389 (2): `RegistrationEndpoints` passes the source in its counting form
  (an IPv6 address cut to its /64) at begin.
- **What the code needs.** `RegistrationSession.Source` is one string, and
  `RegistrationService.CompleteAsync` opens the first session with
  `new SessionOrigin(live.Source, device)`, so that session would record the /64 and not
  the whole address.
- **What the specification says.** Nothing on the first session's address.
- **Readings.**
  1. `CompleteAsync` takes the completing request's `SessionOrigin` (the whole address); the
     contract path of `AcceptTermsAsync` keeps `live.Source`.
  2. Begin stores both. Smallest fix: a new column, which 389 (6) says is not needed.
  3. The /64 is accepted for a registration's first session.
- **Parked.** 389 whole, with the row `abuse.source.sitelimit` and the /48 count of section
  F, ledger line 389.
- **Answer:** D-183. Built in `f805640f` and `a2025e00` (`part/sessions`).

**49. Tier 2. D-166 Tier 1 correction (1) and CONV-DESIGN-004 criterion 2: what the exemption rule clears.**

- **Item.** D-166 Tier 1 correction (1): the whole-file exclusion of `Foreign` removed and
  every project scanned.
- **What the code needs.** Matches the literal rule (only `ForeignMembers` pairs and
  `OpenIddict.*` interfaces exempt) cannot clear: `SubjectId(Guid value)`, the value type's
  own constructor; `ProviderDocuments.GetDocumentAsync(string address)` and
  `ProviderMetadataReading.GetConfigurationAsync(string address)`, which implement
  `IDocumentRetriever` and `IConfigurationRetriever<T>` of
  `Microsoft.IdentityModel.Protocols`. `BrowserProfileLog.Concealed` and
  `ConcealedTooLate` take a `Guid` correlation (an `AuditRecordId` value); a
  `[LoggerMessage]` method logs its declared parameters, so logging `.Value` keeps a `Guid`
  parameter the scan reports, and taking `AuditRecordId` keeps the text but changes the
  structured state.
- **What the specification says.** `08` criterion 2 exempts a member that implements an
  interface of a CONV-DESIGN-008 package; D-166's reflection test requires `OpenIddict.*`.
- **Readings.**
  1. Literal: the test fails, and nothing clears the constructor of `SubjectId`.
  2. A match inside the declaration of the value type it names is exempt;
     `ForeignMembers` gains the two IdentityModel pairs; the reflection test admits an
     interface from an assembly of a CONV-DESIGN-008 package (`OpenIddict.*` or
     `Microsoft.IdentityModel.*`).
  For the log: (i) take `AuditRecordId`; (ii) keep `Guid` and exempt it under the rule;
  (iii) another the owner names.
- **Parked.** The exemption rule and its test; `BrowserProfileLog` and `Concealment`
  unchanged. `LeakedPasswordCorpus.Range` takes a `Uri` (`afc9d0cb`).
- **Answer:** D-183. Built in `64b1a154` (`part/gates`).

**50. Tier 2. D-166 359 and 382 (3): which statuses `endpoints.txt` lists.**

- **Item.** D-166 359 and 382 (3): `endpoints.txt` holds each status the endpoint answers
  and the codes each status carries.
- **What the code needs.** Which statuses. 154 endpoints carry no `Produces` metadata.
- **Readings.**
  1. Only those `09`'s heading lists.
  2. Those, and the statuses that cut across (401 `auth.session.expired` on session routes,
     403 `auth.session.csrfinvalid` on state-changing routes, 400 `api.request.malformed` on
     bodies, 404 `authz.resource.notfound` for a method the path does not take, 500
     `system.fault`), derived from each endpoint's markers in the generator.
- **Parked.** With question 51.
- **Answer:** D-183. Not built: parked on question 119.

**51. Tier 2. D-166 359 and 382 (3): where the statuses and codes come from, and what carries them.**

- **Item.** As question 50.
- **What the code needs.** Nothing in the code yields an endpoint's codes, so they would be
  copied from `09` (the file recording `09` where a handler differs); ASP.NET Core has no
  metadata type for an error code.
- **Readings.**
  1. A new internal metadata record in `Janus.Hosting` (a status with its codes, through a
     route builder extension), with `09` as the source. `08` names none.
  2. The owner names another source or carrier.
- **Parked.** 359 and 382 (3), the endpoint lines of (4), the endpoint and response-member
  scenarios of (5), ledger line 382.
- **Answer:** D-183. Not built: parked on question 119.

**52. Tier 2. D-166 Tier 1 correction (1): `HostedMailbox.Address`.**

- **Item.** D-166 Tier 1 correction (1) says `HostedMailbox.Address` takes `EmailAddress`.
- **What the code does.** `ae35b431` makes it `EmailAddress?`: the reconciliation reads a
  listed address that does not parse as no mailbox's address (IDN-ACCT-004), rather than
  failing the listing.
- **Readings.**
  1. Keep `EmailAddress?`, null for an address that does not parse.
  2. `EmailAddress`; a listing holding such an address faults.
- **Parked.** Nothing more; `ae35b431` is committed under reading 1.
- **Answer:** D-183. Built in `893d7258` (`part/gates`); question 91 follows from it.

**53. Tier 2. D-166 Tier 1 correction (1) and `08`: four routes that bind a string.**

- **Item.** `08`: no handler takes a value with a type as a bare string.
- **What the code needs.** `RoleEndpoints` `name` (`RoleName`), `ConfigurationEndpoints`
  `key` (`ConfigurationKey`), `RestrictionEndpoints` `name` and `AppPasswordEndpoints` `id`
  bind strings and check them in the handler, naming `details.member` (API-CONV-002
  criterion 4). Bound through `IParsable<T>`, they answer 400 malformed with no member,
  since `MalformedRequest` names none for a failure that is not JSON.
- **Readings.**
  1. Bind through `IParsable<T>`; those routes lose `details.member`.
  2. Keep the string binding where the refusal names its member; `08` is out of step.
  3. Bind through `IParsable<T>`, with a refusal of the path that names its member.
- **Parked.** Those four routes.
- **Answer:** D-183. Built in `28c53a12` (`part/endpoints`); questions 118, 120 and 121 follow from it.

**54. Owner action. D-166 378 and OPS-DEP-002: the repository variable `DESTRUCTIVE_DDL_GATE`.**

- **Item.** D-166 378.
- **What the code needs.** `destructive-operations.sh` now fails where the variable is
  unset, so the pull request's destructive-operations job needs it, with the value
  `disabled`.
- **What the specification says.** Repository settings are the owner's.
- **Parked.** Nothing; the variable is not created.
- **Answer:** pending.

**55. Tier 2. D-166 160: the access context of `IOidc.KeysAsync`.**

- **Item.** D-166 160: `KeysAsync` takes an `AccessContext` and admits any, the anonymous one
  included.
- **What the code needs.** `AccessContext` has no anonymous form (its factories are
  `Of(subject)`, `Of(acting, effective)`, `Of(SystemPrincipal)` and break-glass). The
  callers are `KeySetAnswer` (the key set's `GET`, by nobody) and `SignOn.RecordAsync`.
- **Readings.**
  1. A public `AccessContext.Anonymous`, all null, for both callers, with its `PublicAPI`
     line.
  2. `AccessContext.Of(SystemPrincipal.ForDeployment("key-set", "AUTH-KEY-001", ...))` for
     both, with a new principal name and an operation to choose.
  3. `KeySetAnswer` as 1, `SignOn` as 2.
- **Parked.** The signature of `IOidc.KeysAsync`, its two callers, its `PublicAPI` line,
  ledger line 160. The `ClaimsAsync` half is in `d7a913fe`.
- **Answer:** D-183. Held by a test in `bebf5dbd` (`part/gates`); no code path changed.

**56. Tier 3. D-166 282 against `07` LIB-API-003 and `09` section 10: a provider event's refusal.**

- Written as Tier 2; raised to Tier 3 when asked: point 1 is two texts that contradict
  each other on behaviour, and point 2 rests on RFC 8935 section 2.4, which the repository
  does not hold.
- **Item.** D-166 282, and E.5 (a Security Event Token without `jti`).
- **What the specification says.** (1) `07` LIB-API-003: the description carries the `err`
  code again, never a sentence; D-166 282 and Tier 1 item 5: the description carries
  `integration.callback.rejected`. (2) `09` section 10 groups a token that cannot be read,
  one without `jti` and one the provider's keys do not verify as failing validation;
  D-166 fixes `invalid_request` only for the token without `jti`.
- **What the code needs.** `ProviderKeys.VerifiesAsync` returns a `bool` over the signature,
  the issuer, the audience, the lifetime and unreadable documents; no item maps these to
  an `err`.
- **Parked.** The Google 400 writer in `ProviderEventIntake`, the Google 422 expectation of
  `ProviderEventTests`, the IDN-LIFE-012a criterion 3 tests on both routes, E.5, ledger
  line 282.
- **Answer:** D-183. Built in `367b7496` (`part/gates`); question 90 follows from it.

**57. Tier 2. D-166 E.4 and CONV-DESIGN-007: the area registration methods.**

- **Item.** D-166 E.4: each area registers its own types through an `IServiceCollection`
  method.
- **What the code needs.** Such a method in `Janus.Core`, `Janus.Identity`,
  `Janus.Authorization` and `Janus.Privacy` needs
  `Microsoft.Extensions.DependencyInjection.Abstractions`, which none references and
  CONV-DESIGN-008 does not list. `Janus.Identity` defines no registered type.
- **What the specification says.** `08`: the key ring and the mail server in use are
  registered where the ring is (`Janus.Hosting`, `Janus.Cli`: `AddKeyRing`).
- **Readings.**
  1. A `FrameworkReference` to `Microsoft.AspNetCore.App` in the four; `AddIdentityArea`
     empty; `AddCoreArea` holds the key ring, the mail server in use and `Janus.Core`'s
     defaults.
  2. The owner adds the abstractions package to CONV-DESIGN-008; then as 1.
  3. Only projects that define registered types and already reference the container:
     `AddAuthenticationArea` beside `AddStorageArea`, the others as they are, the test over
     two projects.
- **Parked.** All of E.4.
- **Answer:** pending.

**58. Tier 2. D-166 X9 and CONV-DESIGN-003 criterion 5: a unit of work left clean.**

- **Item.** D-166 X9.
- **What the code needs.** About 136 returns between `BeginAsync` and `CommitAsync`, in about
  50 services, none of which rolls back. The scoped `UnitOfWork` keeps the transaction, so
  the next operation joins at depth 1 and commits nothing. `IUnitOfWork` has `Begin`,
  `Commit` and `Dispose` (a dispose rolls back and resets the depth); the idiom of
  `CredentialService.RefusedAsync` commits the empty transaction. A commit or rollback
  never clears the change tracker, so a refused operation's tracked writes are saved by
  the next commit.
- **What the specification says.** Neither the member nor what an inner failure does to the
  outer transaction.
- **Readings.**
  1. `DisposeAsync` before a failure returns, the tracker cleared in `DisposeAsync`, an
     inner failure rolling back the outermost; no surface change.
  2. A public `RollbackAsync` on `IUnitOfWork`, with its `PublicAPI` line and three fakes.
  3. A savepoint per nested `BeginAsync`.
- **Parked.** All of X9, its sites and its test.
- **Answer:** pending.

**59. Tier 3. D-166 321 and AUTHZ-GATE-004 criterion 4 against X1: the transaction of a denial-spike alert.**

- Written as Tier 2; raised to Tier 3 when asked: the gate's denial alert is security
  semantics.
- **Item.** D-166 321 and X1.
- **What the code does.** `AccessGate` writes `authz.access.denied` outside any transaction
  (321); `DenialSpikes.WatchAsync` then raises through `IAccessAlerts` and
  `AlertChannels.RaiseAsync`, which joins the caller's open transaction, so a rollback
  loses the alert row and `AlertRaised` while the record and the count stay.
- **What the specification says.** 321 names "no record, no count and no alert" as the
  defect; its fix and its test cover the record and the count.
- **Parked.** The transaction of the raise of `DenialSpikes`. Related: question 34.
- **Answer:** D-183. Built in `e467284d` (`part/authorization`).

**60. Tier 3. D-166 119 (7), AUTH-ABUSE-004, `10` section 5.15, REG-IDENT-002 and REG-IDENT-007, `10` section 5.25: the purpose of the identifier-change-confirm link.**

- **Item.** D-166 119 (7).
- **What the code does.** The identifier-change-confirm link (`IdentifierService.AskOldAsync`,
  sent to the displaced address to confirm a replacement) goes under `notification`.
- **What the specification says.** As a link the person asked for, it may not carry
  `notification`, and no chapter names its purpose (`signin` and `verification` both fit);
  as a notice to an existing holder it answers to `notification` alone.
- **Parked.** Its purpose alone. The enrolment link of an assisted recovery goes under
  `signin` (`049f31a6`).
- **Answer:** D-183. Built in `c43b3c2c` (`part/sending`).

**61. Tier 2. D-166 X3, CONV-DESIGN-003 criterion 6 and INF-BG-001: passes that overlap across processes.**

- **Item.** D-166 X3 at V7 and S6.
- **What the code needs.** `JobRunStore.Claim` is a claim per interval, not a lease for the
  pass. The outbox, the sends and the alert dispatch run every `PT5S`, with network calls in
  batches of 100, so passes overlap across processes. `OutboxPublisher.DueAsync` and
  `SendingService.RetryAsync` read due rows with no claim: an attempt count lost, a failure
  or raise made twice, a duplicate confirmation insert that throws, an erasure ledger line
  appended twice. An inline attempt past `outbox.retry.initial` meets the retry pass.
- **Readings.**
  1. A claim per row, by a conditional update, before anyone is called (`OutboxPublisher`,
     the retry and settle of `SendingService`, `AlertDispatch`).
  2. A lease for the pass on `job_runs`, with an expiry; a migration.
  3. The overlap accepted under at-least-once delivery: the lost update fixed (an increment
     in SQL), `ON CONFLICT DO NOTHING`, an idempotent ledger append.
- **Parked.** V7 (`OutboxPublisher`) and S6 (the retry and settle of `SendingService`).
- **Answer:** D-183. Built for the send outbox and the raised alerts in `af618c88` and `e8f32416` (`part/sending`) and for the mailbox pushes in `0be8db67` (`part/carriers`); the event rows and the erasure outbox wait on question 114.

**62. Tier 3. D-166 X3 at C9, IDN-ACCT-007 criterion 2 and AUTHZ-GATE-006 criterion 2 against CONV-DESIGN-003 criterion 6: a restriction committed after the gate reads.**

- **Item.** D-166 X3, place C9.
- **What the code does.** `ISettingsRestriction.RefusedAsync` (`GatedSettings`, then
  `AccessGate.RequireSettingsChangeAsync`) reads `SubjectSets.Restricted` before
  `BeginAsync`, at 16 sites; `AccountStates.RestrictAsync` writes under the account row lock
  (C2). A restriction committed between the gate's read and the change's commit does not
  stop the change; every modifying action the gate refuses under a restriction has the same
  window. `AccessGate` resolves `SubjectSets` once per operation, and `GatedSettings`
  enforces the restriction in no second place.
- **Parked.** C9.
- **Answer:** D-183. Built in `c269fb1a`, `ca559838`, `be484f95`, `804b666e` and `94e4ad4e` (`part/held-gate`), but for the four sites questions 122 to 125 park and the sites of the identifiers and the registration, not yet done.

**63. Tier 3. D-166 X3 at S1, AUTH-ABUSE-004 criterion 1 against CONV-DESIGN-003 criterion 6: the admission of a send.**

- Written as Tier 2; raised to Tier 3 when asked: sending restrictions are restriction
  semantics.
- **Item.** D-166 X3, place S1.
- **What the code does.** `SendingService` judges a send on the counters read in
  `PlanAsync`, outside any transaction, carries it, and counts it in `SettleAsync` after
  the transport took it. Sends made at once while the count stands one below the limit
  each pass the check and are all sent, and one credit is judged available to each. A lock cannot span the transport call
  without the after-commit carrying of 119 (1) (questions 27 and 38).
- **Parked.** The admission half of S1. The lost updates, which every way of settling it
  needs, are in `2835ce2f` and `e664d897`.
- **Answer:** D-183. Built in `af618c88` (`part/sending`); questions 109 and 110 follow from it.

**64. Tier 3. D-166 X3 at S5, OPS-ALERT-004a against CONV-DESIGN-003 criterion 6 and X7: whom a destination change notifies.**

- Written as Tier 2; raised to Tier 3 when asked: the notice to a replaced alert
  destination is a security control (OPS-ALERT-004a, D-083).
- **Item.** D-166 X3, place S5.
- **What the code does.** `AlertDestinationChange` reads the destinations in force and
  notifies them before its transaction, then writes the replacement under the settings row
  lock (178). Two changes at once (A to B, A to C) both notify A, and B is replaced
  without B being told. Deciding under the lock whom to notify means carrying the notice
  inside the transaction (a send in the caller's transaction, which X7 and the parked
  119 (1) move after the commit) or refusing a change whose value in force moved.
- **Parked.** S5.
- **Answer:** D-183. Built in `79dfdb2a` (`part/sending`).

**65. Tier 2. D-166 X4, API-CONV-002 criterion 3 and CONV-CODE-006 criterion 3: the free text of `PUT /admin/compliance/assessments`.**

- **Item.** D-166 X4 at `PUT /admin/compliance/assessments` (PRIV-ROPA-001).
- **What the code needs.** `dataOwner` and `organisationalSecurityMeasures` reach
  `IProcessingRecords.DeclareAsync` untrimmed and unbounded.
- **What the specification says.** X4 says every free-text member; API-CONV-002 names four;
  `09` section 8a says an omitted field is cleared. Document text is free text no bound
  of 1024 can hold, so "every other" is not literal.
- **Readings.**
  1. Both are free text: trimmed, blank or past 1024 characters malformed naming the
     member, absent cleared.
  2. Blank clears as omitted; only the upper bound applies.
  3. Record content, outside X4; nothing changes.
- **Parked.** The free text of that route.
- **Answer:** D-183. Built in `f2ef4d42` (`part/privacy`); question 99 follows from it.

**66. Withdrawn.** It asked after the row `abuse.source.sitelimit` of section F, which is
part of 389 (3) and waits with 389 on question 48.

**67. Tier 2. CONV-DESIGN-008 and CONV-SETUP-002: `Microsoft.EntityFrameworkCore` at 10.0.12 beside `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3.**

- **Item.** The move of `Microsoft.EntityFrameworkCore` and
  `Microsoft.EntityFrameworkCore.Design` to 10.0.12 (`3cb3fe5a`).
- **What the code needs.** One version of `Microsoft.EntityFrameworkCore.Relational` in
  every project. `Janus.Storage` resolves 10.0.12, through `Design`, whose assets are
  private. Every project that references `Janus.Storage` resolves 10.0.4, the floor the
  provider 10.0.3 declares, beside `Microsoft.EntityFrameworkCore` 10.0.12. The build
  reports MSB3277 in seven projects and the output of `Janus.Storage.Tests` holds
  `Relational` 10.0.4.0 beside `EntityFrameworkCore` 10.0.12.0. The locked restore passes.
  At 10.0.4 all three were one version.
- **What the specification says.** CONV-DESIGN-008 lists the three relational packages
  and not `Microsoft.EntityFrameworkCore.Relational`; CONV-SETUP-002 holds every version
  in `Directory.Packages.props`, whose entries are exactly that list. No stable provider
  newer than 10.0.3 exists.
- **Readings.**
  1. `Relational` is pinned at the version of `Microsoft.EntityFrameworkCore`: an entry
     in `Directory.Packages.props` with central transitive pinning, or a reference in
     `Janus.Storage`. Either adds a package the table does not name.
  2. The two packages stay at 10.0.4 until a provider declares a later floor.
- **Parked.** The build and the fast checks of the version step, and so every item after
  it.
- **Answer:** D-184: `Janus.Storage` references `Microsoft.EntityFrameworkCore.Relational` directly, at the version of `Microsoft.EntityFrameworkCore` (`ff057e7b`, `d2b9ffac`).

**68. Tier 3. D-166 209 (2), REG-DOM-001 and IDN-ACCT-004: which checks of UTS #46 the library's own processing applies.**

- **Item.** 209 (2), the ASCII form of a domain from the library's own UTS #46 tables.
- **What the code needs.** The value of each parameter of UTS #46 ToASCII: CheckHyphens,
  CheckBidi, CheckJoiners, VerifyDnsLength and Transitional_Processing, beside
  UseSTD3ASCIIRules.
- **What the specification says.** REG-DOM-001 and `01` name the STD3 rules, the mapping
  table of the pinned version and RFC 3492, and no other parameter. D-166 209 (2) asks
  for a test that the form is the same whatever ICU the machine holds.
- **What the present code does.** `IdnMapping` with `UseStd3AsciiRules`, on this machine
  (Windows, .NET 10.0.401): a label with a hyphen at both its third and fourth place
  (`ab--c.example`) is accepted; a leading or trailing hyphen is refused; a label mixing
  a right-to-left letter with a left-to-right one (U+05D0 `b1`) is accepted; a digit
  before a right-to-left letter is accepted; U+200C and U+200D between Latin letters are
  refused; a label of 64 octets, an empty label, a leading combining mark, an underscore
  and `xn--a` are refused; `xn--bcher-KVA` is answered in the case it came. So the
  present answers are not one set of parameters the chapters could be read to keep.
- **Parked.** 209 (2), with its tests and its ledger line. `IdnaMappingTable.txt` of
  Unicode 17.0.0 is downloaded (SHA-256
  `87f05505dc026fdb2bff16132bdc68a8014675836882a9a2b1844540ad3be382`) and kept outside
  the repository until the item is built.
- **Answer:** pending.

**69. Tier 3. CONV-DESIGN-003 and INT-GEN-003: whether the counts of a rejected callback stand.**

- **Item.** X9 at `DeliveryReports.ReportAsync` (question 58).
- **What the code does.** It begins, then `CallbackAdmission` counts the callback and, on a rejection, counts the rejection and past the threshold writes the alert's event row, all in that unit of work. On `integration.callback.rejected` it commits; on any other failure it returns with the unit open.
- **What the specification says.** CONV-DESIGN-003 lists the refusals whose count or record stands (AUTH-FACT-004, AUTH-ABUSE-001, AUTH-STEP-002, OPS-BOOT-004, CONV-LOG-005). The callback counts of INT-GEN-003 are not among them, and a rejection past the threshold writes more than a count.
- **Parked.** The whole of `DeliveryReports.ReportAsync`, its other-failure return included.
- **Answer:** pending.

**70. Tier 2. CONV-DESIGN-003: a refusal returned after the commit of a record the list does not name (`BotDefence`).**

- **Item.** X9 at `BotDefence` (question 58).
- **What the code does.** It begins, writes the signal's audit record, commits, then asks the verifier outside the unit of work and may answer `bff.challenge.required`. No unit is left open.
- **What the specification says.** A refusal that needs no write is returned before the unit of work begins; a refusal that keeps a record is one of the five the item lists, and the signal's record is not among them.
- **Readings.**
  1. The record is the operation's own success and the challenge is a later answer: nothing changes.
  2. The record is written only where the verifier's answer is known, in one unit of work after it.
- **Parked.** `BotDefence`.
- **Answer:** pending.

**71. Tier 3. AUTH-FACT-004 and CONV-DESIGN-003: the wrong try that reaches `code.signin.attempts`.**

- **Item.** X9 at `SignInLinks.SpendCodeAsync` (question 58).
- **What the code does.** A wrong try below the limit writes its count and commits. The wrong try that reaches the limit writes no count: it removes the pending sign-in, and commits.
- **What the specification says.** CONV-DESIGN-003: such a refusal commits "that count or record and nothing else". AUTH-FACT-004: the code is invalidated after that many wrong tries.
- **Parked.** The limit path of `SpendCodeAsync`, left committing as it was. The expired path of the same method is rolled back (`e3f97ab2`).
- **Answer:** pending.

**72. Tier 2. CONV-DESIGN-003: a lost race that commits nothing (`RegisteredSecrets.RotatedAsync`).**

- **Item.** X9 at `RegisteredSecrets.RotatedAsync` (question 58).
- **What the code does.** The conditional replace is followed by a commit whether or not it replaced anything. Where another process won, the method reads the standing secret outside the unit of work and may answer `system.fault`.
- **What the specification says.** Every return but success rolls back. The lost race normally answers success, as `SigningKeys.ChangeAsync` does, which now rolls back before it.
- **Readings.**
  1. Where nothing was replaced the unit of work is rolled back, then the standing secret is read: as `SigningKeys.ChangeAsync`.
  2. The commit of nothing stays, since the operation succeeds.
- **Parked.** `RegisteredSecrets.RotatedAsync`.
- **Answer:** pending.

**73. Tier 3. AUTH-FACT-004 and CONV-DESIGN-003: what a refused verification code may write (`VerificationCodes.PresentAsync`).**

- **Item.** X9 at `VerificationCodes.PresentAsync` (question 58). It begins the outermost unit of work and always commits.
- **What the code does.** Four refusals: nothing outstanding (`auth.code.expired`, nothing written); a code past its life (`auth.code.expired`, the row removed); a wrong try (the count written); the wrong try that reaches the limit (the row removed).
- **What the specification says.** Such a refusal commits "that count or record and nothing else". The removal of a lapsed row and the removal at the limit are not a count; the first refusal writes nothing and by the letter rolls back. This is the same matter as question 71.
- **Parked.** The whole of `PresentAsync`, left as it was. `VerificationCodesTests.CONV_DESIGN_003_AC5_AWrongTryCommitsItsCountAsync` holds the wrong try.
- **Answer:** pending.

**74. Tier 3. AUTH-FACT-014 criterion 3 and CONV-DESIGN-003: the audit record of a counter mismatch.**

- **Item.** X9 at `WebAuthnService.PresentAsync` (question 58).
- **What the code does.** `auth.webauthn.countermismatch` writes the audit record `auth.credential.countermismatch`, and nothing else, and commits. The operation begins the outermost unit of work.
- **What the specification says.** AUTH-FACT-014 criterion 3 requires the event audited. CONV-DESIGN-003's list of refusals that commit does not name it, and a rollback discards the record.
- **Parked.** That return, left committing.
- **Answer:** pending.

**75. Tier 2. CONV-DESIGN-003: sends inside a unit of work that goes on after one is refused (`RecoveryCodeReminders.RemindedAsync`).**

- **Item.** X9 at `RecoveryCodeReminders.RemindedAsync` (question 58).
- **What the code does.** It begins, calls `INotificationHandler.SendAsync` for each channel inside the unit of work, counts the sends that succeeded, discards each failure and commits. The send begins a unit of work of its own. A send that fails after its own beginning now marks the whole, and the reminder's commit would then throw. Where every channel refuses, the reminder commits having written nothing.
- **What the specification says.** A call an operation must be able to survive being refused begins no unit of work of its own; a sweep site that cannot meet this is a question (D-183 question 58). The send is rebuilt by questions 27, 38 and 63.
- **Readings.**
  1. It waits for the governed send, where the admission decides in the caller's unit of work.
  2. The reminder's sends move outside its unit of work.
- **Parked.** `RemindedAsync`.
- **Answer:** pending.

**76. Tier 3. OPS-BOOT-004, CONV-LOG-005 and CONV-DESIGN-003: the units of a refused break-glass credential.**

- **Item.** X9 at `BreakGlassService.PresentAsync` (question 58).
- **What the code does.** (1) For a wrong or used code, the unit that takes the hold and compares commits having written nothing beyond the attempt, and the failed authentication's record is written in a second unit, with the throttle's failure after it: the refusal is not decided and recorded in one unit. (2) `auth.breakglass.consumed`, answered where the conditional record of the use finds it taken, now rolls back, and writes no record of a failed authentication and counts no failure.
- **What the specification says.** CONV-DESIGN-003: a break-glass attempt and its alert stand, committed alone. CONV-LOG-005 names a refused break-glass credential a failed authentication.
- **Parked.** Both paths, as they are after `853d036b`.
- **Answer:** pending.

**77. Tier 2. CONV-DESIGN-003: an answer that is not a failure and writes nothing.**

- **Item.** X9 at `DeviceService`'s standing check (behind `TrustsAsync` and `RemembersAsync`) and at `LossReports.InvalidateAsync` (question 58).
- **What the code does.** The standing check answers `false` under its lock, and the sweep answers success with a count of zero where the report was cancelled meanwhile; each commits an empty unit of work.
- **What the specification says.** "With `CommitAsync` where it succeeds ... with `RollbackAsync` on every other return". Neither answer is a failure of the operation.
- **Readings.**
  1. Each is the operation's success and commits.
  2. An answer that changed nothing rolls back.
- **Parked.** Those two returns, left committing.
- **Answer:** pending.

**78. Tier 3. REG-PROF-002 and CONV-DESIGN-003: the lock an under-age answer writes.**

- **Item.** X9 at `RegistrationService.RecordAgeAsync` (question 58).
- **What the code does.** An under-age date locks the age screen on the registration session, commits, and answers the refusal.
- **What the specification says.** A refusal rolls back but where it keeps one of the counts or records CONV-DESIGN-003 lists; the locked screen of REG-PROF-002 is not among them, and a rollback loses the lock.
- **Parked.** That return, left committing.
- **Answer:** pending.

**79. Tier 3. REG-SESS-005 criterion 4 and CONV-DESIGN-003: the session removed where a staged identifier was taken since.**

- **Item.** X9 at `RegistrationService.CompleteAsync` (question 58).
- **What the code does.** Where a staged identifier is taken or reserved since it was staged, the registration session is removed, the unit of work committed, and the session answered expired.
- **What the specification says.** As for question 78: the removal is a write the list does not name, and a rollback leaves the session alive.
- **Parked.** That return, left committing.
- **Answer:** pending.

**80. Tier 2. CONV-DESIGN-003: operations that discard a send's refusal inside their unit of work and commit.**

- **Item.** X9 (question 58), the same matter as question 75.
- **What the code does.** `INotificationHandler.SendAsync` begins a unit of work of its own, and the phone signals it consults another. These callers call it inside their unit of work, discard its result and commit: `AccountLifecycle.DeactivateAsync` and `DeleteAsync`; `IdentifierService`, the promotion, the backup, the undo, the stage, the notice to a holder, the swap and the surrender; `InvitationAcknowledgement`, the corporate address; `MembershipEnd`, the retirement; `AppPasswords.RecordAsync`. As the send stands it refuses only before its own beginning, so nothing marks the outer unit.
- **What the specification says.** A call an operation must be able to survive being refused begins no unit of work of its own. The send is rebuilt by questions 27, 38 and 63, where it is judged in the caller's transaction.
- **Readings.**
  1. These sites wait for the governed send.
  2. Each discards nothing: a refused send fails the operation.
- **Parked.** Nothing is changed at these sites.
- **Answer:** pending.

**81. Tier 3. AUTH-ABUSE-001 and CONV-DESIGN-003: the throttle's count inside the registration's verification.**

- **Item.** X9 at `RegistrationService.VerifyAsync` (question 58).
- **What the code does.** A refused code is counted through `ThrottleService.FailedAsync`, which begins a level of its own inside the verification's outermost unit of work. The verification commits where the count succeeded and rolls back where it failed. The unit also holds the wrong try on the code's record.
- **What the specification says.** "An operation whose refusal keeps such a count begins the outermost unit of work and is never called inside another's."
- **Parked.** The site is left as `fe7a5cdc` made it.
- **Answer:** pending.

**82. Tier 2. CONV-DESIGN-007 criterion 7 and D-183 question 57: the types of `Janus.Core` that `AddJanus` registers beside the ring and the mail server in use.**

- **Item.** Question 57.
- **What the code needs.** `AddJanus` registers `RestrictionKeySuppliers.None`, `PreferenceDeclarations.None`, `ReservedUsernames.Default` and `DictionaryWords.Default` (each where the host registered none), the host's declaration, and `DeclaredProcessing` by a factory that reads `AuthorizationModel`, which `Janus.Core` cannot see. `AddCoreArea` is also called by every command, which has no declaration.
- **What the specification says.** Criterion 7: every type of such a project that `AddJanus` registers is registered by that project's own method. D-183 question 57: Core's method registers the key ring and the mail server in use.
- **Readings.**
  1. These are the host's declarations and their defaults, outside the rule; they stay in Hosting.
  2. The four defaults move to `AddCoreArea`, the commands then registering them too; the declaration and `DeclaredProcessing` take a parameter or stay.
- **Parked.** Those six registrations, left in Hosting, and with them the test of criterion 7's third clause.
- **Answer:** pending.

**83. Tier 3. CONV-DESIGN-007 criterion 7 and LIB-SEAM-001 criterion 1: where `AccessGate` is registered.**

- **Item.** Question 57.
- **The contradiction.** Criterion 7 puts `AccessGate` and its `IAccessGate` forward in `AddAuthorizationArea`. `AccessSeamTests.LIB_SEAM_001_AC1_ReplacingWhatEvaluatesIsOneChange` holds `AccessGate` to two files, its own and `HostingRegistration.cs`. Hosting keeps naming it for the factories of `GatedSettings` and `GatedUnscopedRefusal`, which are Hosting's types, so the move makes three.
- **Parked.** The two registrations, left in Hosting.
- **Answer:** pending.

**84. Tier 2. CONV-DESIGN-007 criterion 7 and CONV-LAYOUT-001: the factory of `SendingValidation`.**

- **Item.** Question 57.
- **What the code needs.** `SendingValidation` is a type of `Janus.Authentication` made by a factory in `Janus.Hosting` that builds its placeholders from `Janus.Privacy`'s `ErasureLedgerSubscriber.Joined`, which Authentication cannot reference.
- **What the specification says.** A factory registration belongs with the type it builds; no area depends on another.
- **Readings.**
  1. It stays in Hosting, the one project that sees both.
  2. The placeholders reach the type another way, and the factory moves; this changes a class.
- **Parked.** That registration, left in Hosting.
- **Answer:** pending.

**85. Tier 2. CONV-DESIGN-007: the types only a command registers.**

- **Item.** Question 57.
- **What the code needs.** The commands' compositions register `DeploymentBootstrap`, `ClientRegistry` and `ProtectedConfiguration` (Authentication), `ErasureReplay`, `KeyRotation` and `FingerprintKeyRotation` (Privacy), `KeyRotationStore` and `FingerprintRotationStore` (Storage). `LibraryStructureTests` holds the files that may name `ProtectedConfiguration`, `KeyRotation` and the rotation stores (OPS-CFG-004 criterion 2, OPS-SEC-003 criterion 1, DR-009a criterion 5). The replay, the rotations and the client registration call no area method but Core's and Storage's, using none of the others' registered types.
- **What the specification says.** A project's method holds "the registrations of the types that project defines and no other"; criterion 7 speaks of what `AddJanus` registers; a command's composition "calls the methods of the projects it uses".
- **Readings.**
  1. A type only a command resolves is registered by that command; nothing changes.
  2. They move into the area methods, and the three criteria's file lists change with them.
- **Parked.** Those registrations, left in the commands.
- **Answer:** pending.

**86. Tier 2. CONV-DESIGN-007: the inner compositions of `AuditRetention` and `RestoreTest`.**

- **Item.** Question 57.
- **What the code needs.** Each builds a container of its own over another credential, registering the outer key ring's instance and calling `AddStorageArea`, not `AddCoreArea`.
- **What the specification says.** Core's method registers the key ring; these compositions need the ring already filled.
- **Readings.**
  1. An inner composition over the filled ring registers that instance, as a command does after `AddCoreArea`; nothing changes.
  2. Each calls `AddCoreArea` and then registers the instance.
- **Parked.** Both, left as they are.
- **Answer:** pending.

**87. Tier 3. CONV-DESIGN-003, INT-GEN-003, BFF-MACH-002, BFF-MACH-003 and IDN-LIFE-012a: what a rejected callback keeps (`CallbackIntake`).**

- **Item.** X9 at `CallbackIntake`, reached from `SignedCallbackGuard`, `UnsignedCallbackGuard` and `ProviderEventIntake.TakeAsync` (question 58). The same matter as question 69.
- **What the code does.** A refused callback (rate limit, source range, signature, window, reference, confirmation, event) commits. The commit keeps the admission count of the source, the rejection count, the alert once the threshold is passed and, for an unsigned provider event, the audit record of its rejection (IDN-LIFE-012a criterion 1).
- **What the specification says.** CONV-DESIGN-003's list does not name these counts, and the refusal writes more than one thing.
- **Parked.** The rejected answer, left committing. The other failures of the intake roll back (`c5514735`, `79a480c3`).
- **Answer:** pending.

**88. Tier 2. CONV-DESIGN-003: a send joined to its caller's unit of work now marks it.**

- **Item.** X9 (question 58), the same matter as questions 75 and 80.
- **What the code does.** `DeadlineSweep`, at a lapse, and `ProviderEvents.TakeAsync`, inside the provider event's intake, call the send inside their unit of work, discard its answer and commit. The send's one failure after its own beginning is the settling of the immediate attempt where the budget is spent and its alert is refused. That return left its level open before; it now rolls back (`2b42bf9f`), which is right where the settling is outermost (the retry, a send after a commit), and joined it marks the caller's unit, whose commit then throws.
- **What the specification says.** A call an operation must be able to survive being refused begins no unit of work of its own.
- **Readings.**
  1. These sites wait for the governed send (questions 27, 38 and 63), where nothing is carried inside the caller's transaction.
  2. The callers roll back and fail where the send fails.
- **Parked.** Both callers, unchanged. The same reading decides the further sites the sweep counted under question 77: `ConsentService` (a withdrawal made meanwhile, twice), `OrganizationErasureSweep` (cancelled meanwhile), `DeadlineSweep` (decided meanwhile), the rotations' re-read at completion and their passes with nothing left, `GroupService.AddMemberAsync` (already a member), `GroupService.RemoveMemberAsync` and the worker's lapse check, each answering success with nothing written and committing.
- **Answer:** pending.

**89. Tier 2. REF-001 criterion 1 and D-183 question 25: the directions the reference tests read.**

- **Item.** Question 25.
- **What the code needs.** Whether the two tests also assert that every live row of chapter 10 sections 1 and 4 is in the source.
- **What the specification says.** REF-001 criterion 1 states one direction (in the source, and no live row fails) and "the test reads this document alone". D-183 question 25 says the tests read chapter 10 alone "in each direction they read now"; before, they read the source into the chapter or the ledger, and what the ledger owed into the source.
- **Readings.**
  1. One direction, as the criterion states. Built.
  2. Both: one assertion more in each of the two tests. It can hold only on the working branch after every part is merged and questions 50 and 51 are built, since rows such as `abuse.source.sitelimit`, `outbox.claim.timeout`, `auth.factor.notenrolled`, `auth.factor.passwordrequired` and `config.change.superseded` are built elsewhere.
- **Parked.** The reverse assertion.
- **Answer:** pending.

**90. Tier 3. IDN-LIFE-012a criterion 8 and `09` section 10: a Security Event Token whose `nbf` is in the future.**

- **Item.** Question 56.
- **What the code does.** A token not yet valid is refused, as before the change, and on the Google route it is answered with the lifetime's code, `invalid_request`.
- **What the specification says.** `09` section 10 fixes the failures and their order and names `exp` alone for the lifetime; it says nothing of `nbf`.
- **Parked.** Nothing further. The refusal is left standing.
- **Answer:** pending.

**91. Tier 2. INT-MAIL-001: an account whose `emailAddress` member is absent or not text.**

- **Item.** Question 52.
- **What the specification says.** INT-MAIL-001: "its address (none where emailAddress does not read as an email address)", and "an answer that does not read" is a failure. INT-MAIL-007: "it never fails the listing".
- **Readings.**
  1. Absent or not text is none. Built (`893d7258`).
  2. Only text that does not parse is none, and a missing member is an answer that does not read: four lines of `JmapMailServer.MailboxesAsync` and one account of the adapter's test go back.
- **Parked.** Nothing.
- **Answer:** pending.

**92. Tier 3. AUTHZ-TEST-001 criterion 2 against D-166 328 and AUTHZ-GATE-005: the filter of an unmet step-up row.**

- **Item.** Question 46.
- **The contradiction.** Criterion 2 says a step-up case agrees "when the filter lists the record and the check answers the gate's outcome". `IAccessGate.FilterAsync` and `FragmentAsync` ask the bound gate as the check does and are refused with the gate's code where it is unmet (D-166 328, held by `GateBehaviourTests.AUTH_STEP_001_AListUnderABoundActionAsksForStepUpAsync`), so for the six unmet rows no filter is rendered and no record is listed. AUTHZ-GATE-005 says the per-row grant query does not evaluate step-up.
- **Parked.** The filter assertions of the six unmet step-up rows. The rows assert the check for all seven, and both filter renderings for the met row.
- **Answer:** pending.

**93. Tier 2. `10` section 5.30 and LIB-TEST-001 criterion 2: the seven step-up scenarios of the conformance suite.**

- **Item.** Question 46.
- **What the code needs.** The members of `TruthTableScenario` for the met case and the six unmet ones, `details.gate` on a finding, what a `TruthTableCase` states for a step-up case, and a way for a suite run against a host's deployment to produce each outcome (a report that meets, each kind that does not, a provider that fails, no provider).
- **What the specification says.** The scenario names, and when a step-up scenario agrees. Nothing on how the suite arranges the provider's report in a deployment that registers its own provider or none.
- **Readings.**
  1. The suite sets the host's provider aside and builds the gate over one of its own for these cases.
  2. The suite runs only the scenarios the deployment can produce.
  3. The host supplies the reports through `IConformanceRows`.
- **Parked.** The seven members, `details.gate`, their running. It also waits on question 92.
- **Answer:** pending.

**94. Tier 2. `09` `POST /auth/step-up` and AUTH-FACT-002 criterion 7: the gate of a step-up `phoneCode` ask whose number answers `risk`.**

- **Item.** The correction of `52482ed5`, the step-up half.
- **What the code needs.** The 403 `auth.stepup.required` "computed without it" needs a gate's three values. The ask carries `challengeId` and `factor`; the challenge names no action.
- **What the specification says.** Nothing on which gate.
- **Readings.**
  1. The strictest of the gates of the policy in force, field by field, as a host-named gate costs: `StepUpGuard` judged with no action.
  2. The gate of the action the step-up was opened for, which the challenge would carry from `/auth/begin`: a member on the begin request and on the challenge's row.
- **Parked.** The step-up half; the ask on `risk` at a step-up still answers 202 (`AuthenticationServiceTests.AUTH_FACT_002b_AC6_AReportedChangeWithholdsTheTextCodeFromAStepUpAsync`).
- **Answer:** pending.

**95. Tier 2. AUTHZ-IMP-001: `Effective` on the other events that name who acted.**

- **Item.** Question 40.
- **What the specification says.** An event that names who acted carries both identities as the context gives them.
- **What the code does.** `CredentialSuspended` and `CredentialRestored` carry both. These set `Actor` and no `Effective`: `AccountAdministration` (three events), `AccountLifecycle` (four, two of them from a link, with no context), `CredentialService` (two), `PasswordService` (one), `RestrictionAdministration` (two), `AlertDestinationChange` (one), `TakedownService` (two).
- **Readings.**
  1. Question 40's answer is the one event; the others stand.
  2. Every event that sets `Actor` from a context sets `Effective` from it: one member at each of the thirteen sites that have a context, with a test each.
- **Parked.** The thirteen sites, unchanged.
- **Answer:** pending.

**96. Tier 2. D-183 question 32 and LIB-API: the registration operations that take the request's source.**

- **Item.** Question 32.
- **What the specification says.** D-183 names the new parameter for `IRegistration.LandAsync`. It also says every count and delay of a registration session uses the source of the request in hand, never one stored at its begin.
- **What the code does.** `StageAsync`, `AddAsync`, `ChangeAsync` and `VerifyAsync` take the source too (`a2025e00`), since a caller in process could not otherwise meet the rule; five lines of `PublicAPI.Unshipped.txt` changed. It is built, and stated here because it changes the public surface beyond the one method the answer names.
- **Readings.**
  1. As built.
  2. Only `LandAsync` takes it, and the four others count against a source the session carries, which the rule forbids for a request arriving from elsewhere.
- **Parked.** Nothing.
- **Answer:** pending.

**97. Tier 2. `09` section 8a and question 24: a document name refused "before the body is read".**

- **Item.** Question 24.
- **What the code needs.** The publication and translation handlers take their body as a bound parameter; the framework reads it before the handler runs. A name outside the rule with an unreadable body is answered for the body.
- **What the specification says.** `09` section 8a: 400 naming `document`, "answered before the body is read". CONV-DESIGN-006: a typed route value binds through `IParsable` and the error translation names the first declared value that does not parse (question 53); `{document}` binds as text today.
- **Readings.**
  1. The handler's first check, as built: every readable body is answered naming `document`.
  2. The document name becomes a typed value declared with question 53's metadata, so the stage names it whatever the body: a public value type in `Janus.Core` and the three handlers.
- **Parked.** Nothing; reading 1 is built.
- **Answer:** pending.

**98. Tier 2. INT-SMS-003 and question 24: a document name outside the rule from a caller in process.**

- **Item.** Question 24.
- **What the code needs.** `ILegalDocuments.PublishAsync`, `TranslateAsync` and `ReadAsync` take the name as text and refuse none; a host in process can publish under a name the read route then refuses. The rule (`PlaceName`) is internal to `Janus.Core`, which grants its internals to Hosting and the command alone, so `Janus.Privacy` cannot apply it.
- **What the specification says.** D-183 question 24 and `09` name the routes; INT-SMS-003 bounds the name at declaration and at the routes.
- **Readings.**
  1. The routes alone, as built.
  2. The service refuses too: the rule made public in `Janus.Core`, a change of the public surface.
- **Parked.** The refusal in process.
- **Answer:** pending.

**99. Tier 2. `09` section 8a and PRIV-ROPA-001: the spelling of the organizational measures outside the request.**

- **Item.** Question 65.
- **What the code does.** The request member is `organizationalSecurityMeasures`. The response of `GET /admin/ropa` writes `organisationalSecurityMeasures`; `ComplianceRecord.OrganisationalSecurityMeasures`, `ProcessingRegister.OrganisationalSecurityMeasures` and the column `organisational_measures` keep that spelling.
- **What the specification says.** `09` spells the request member alone; PRIV-ROPA-001 writes "Organisational" in the template's field name and `organizational-measures-missing` for the finding.
- **Readings.**
  1. Only the request member follows `09`, as built.
  2. The response member, the two public members and the column follow: a rename of each, the `PublicAPI` lines and a migration.
- **Parked.** Nothing.
- **Answer:** pending.

**100. Tier 2. CONV-VCS-004: two commits that change `AuthorizationModel.cs` and no truth-table case.**

- **Item.** Questions 33 and 28.
- **What the code does.** `ea174da9` removes eight lines of the maintenance credential's listing from the serialized model (OPS-MIG-003a criterion 4); `c972731e` gives the lawful basis list its coded refusal at the start. Neither changes an outcome a truth-table case states, and neither commit touches the table. The check reads a range: it passes over the branch, and fails over a range that holds these two alone.
- **What the specification says.** A change to permission logic comes with a change to the truth table; the check counts every file of the authorization area.
- **Readings.**
  1. They are not changes of permission logic; they stand.
  2. Every change under the area's paths carries a table change: the two changes move to files outside the paths, or each gains a case.
- **Parked.** Nothing.
- **Answer:** pending.

**101. Tier 2. AUTHZ-GRANT-003 and AUTHZ-DERIVE-005: the audit record of a grant the drift check writes.**

- **Item.** Question 22.
- **What the code needs.** An audit action under which a record naming the principal `derivation-driftcheck` is written where the drift check writes or takes back a materialised grant.
- **What the specification says.** AUTHZ-GRANT-003: "a materialised grant the drift check writes carries the reason AUTHZ-DERIVE-005, its audit record naming the system principal `derivation-driftcheck`". AUTHZ-DERIVE-005: "and its audit record names the principal". `10` section 5.24 lists no action for a grant written or revoked by anyone, the grants table has no principal column, and neither item's criteria mention the record.
- **Readings.**
  1. A new audit action, written in the correcting transaction with the principal and the reason: one or two rows of `10` section 5.24 and the write in `DerivationMaterialiser`.
  2. The grant's own row is the record (nil granter, the reason), and the job names the principal: the two sentences of `03` change.
  3. A principal column on grants: a migration.
- **Parked.** That audit record alone.
- **Answer:** pending.

**102. Tier 2. CONV-VCS-004: the gate's read of the standing consent and the truth table.**

- **Item.** Question 30.
- **What the code does.** `RecordedConsents`, under a path the check counts as permission logic, reads the standing record (the live one, else the latest) where it read the one row. No outcome a truth-table case states changed; the table's file changed in a fixture call alone, which satisfies the check.
- **Readings.**
  1. That suffices.
  2. The table gains two rows (a live record beside an ended one; no live record, the latest decides), which `ConsentGateTests` hold today.
- **Parked.** Nothing.
- **Answer:** pending.

**103. Tier 2. CONV-DESIGN-004 and PRIV-CONS-001: the identifiers the migration gives the existing records.**

- **Item.** Question 30.
- **What the code does.** The migration fills `id` in SQL: the 48-bit millisecond instant of the row, the version 7 nibble, the rest from `gen_random_uuid()`. The values are valid version 7; a test asserts version and instant.
- **Readings.**
  1. The store's rule applied in SQL, as built.
  2. The identifiers are minted in code, in a data step outside the migration.
- **Parked.** Nothing.
- **Answer:** pending.

**104. Tier 2. PRIV-CONS-001 and OPS-DEP-002: the `Down` of `KeepARecordForEachGrant`.**

- **Item.** Question 30.
- **What the code does.** `Down` restores the key on subject and purpose and deletes nothing, so it fails where a subject holds two records of one purpose. `Up` drops both primary keys and adds keys and unique indexes to existing tables; the destructive-operations report will list it.
- **Readings.**
  1. A revert removes no evidence, as built.
  2. `Down` keeps the latest record and deletes the rest.
- **Parked.** Nothing.
- **Answer:** pending.

**105. Tier 2. PRIV-CONS-008: a withdrawal where no record is live.**

- **Item.** Question 30.
- **What the code does.** A withdrawal stamps the live record, else the latest unwithdrawn one, so a superseded, unwithdrawn consent can still be withdrawn, as before the change (D-166 148).
- **Readings.**
  1. As built.
  2. A withdrawal touches a live record alone: one statement of the store and one test.
- **Parked.** Nothing.
- **Answer:** pending.

**106. Tier 2. CONV-DESIGN-003 and PRIV-CONS-002: the store saves tracked changes before its conditional insert.**

- **Item.** Question 30.
- **What the code does.** The insert conditional on the live index is hand-written SQL on the ambient transaction. The unit of work saves tracked changes at the outermost commit alone, so a consent given at registration would name an account not yet written. `ConsentStore` saves the tracked changes before its insert, inside the same transaction (`ConsentStoreTests.PRIV_CONS_001_AC1_AConsentAddedWithItsAccountInOneUnitOfWorkIsHeldAsync`).
- **Readings.**
  1. As built.
  2. The record is inserted through the model, which cannot be conditional on the partial index: a second grant at once then fails on the index and is read again.
- **Parked.** Nothing.
- **Answer:** pending.

**107. Tier 2. AUTH-ABUSE-004, CONV-DESIGN-002 and LIB-API-001: the public members of the governed send.**

- **Item.** Questions 27 and 38.
- **What the specification says.** The attempt is "registered on the unit of work to run after that commit" (D-166 119 (1): "Give `IUnitOfWork` an after-commit registration"); the admitted message is "kind, destination, subject, language, values and its reference"; every contract method answers a result (CONV-DESIGN-005 criterion 1). The chapters name no member.
- **What the code does.** `Result IUnitOfWork.AfterCommit(Func<CancellationToken, ValueTask> work)`, a fault with no unit of work in progress; `OutboundMessage` (destination, message, purpose, source, language; subject, values, kind, whether it is an alert, context) and `SendRequest` as the admitted message (destination, message, language, reference; subject, values, kind); `INotificationHandler.SendAsync` answering `Result`; `SendReference.TryParse`, since `Janus.Storage` reads the reference back from the row.
- **Readings.**
  1. As built.
  2. The registration is an internal port the unit of work implements, with no public member.
- **Parked.** Nothing.
- **Answer:** pending.

**108. Tier 2. IDN-ATTR-001 and AUTH-ABUSE-004: what stands between the languages of one mail.**

- **Item.** Question 27.
- **What the specification says.** "the subject lines joined", and the mail "composes it from each language's rendered template"; no separator is named.
- **What the code does.** " | " between the subject lines, one blank line between the texts.
- **Parked.** Nothing.
- **Answer:** pending.

**109. Tier 2. AUTH-ABUSE-004: a retry the restrictions refuse.**

- **Item.** Question 63.
- **What the specification says.** "refused, it holds none", against "released where it fails for good: ... or the restrictions refuse its retry".
- **Readings.**
  1. A retry the restrictions or the floor refuse releases the count and waits as a failed attempt (the attempt counted, rescheduled, `degradation` where the attempts are spent). Built; it is what the code did before.
  2. It fails for good at that refusal and its row is removed: one branch of `SendPublisher`.
- **Parked.** Nothing.
- **Answer:** pending.

**110. Tier 3. AUTH-ABUSE-006 against CONV-DESIGN-002: the gateway's balance asked inside the caller's transaction.**

- **Item.** Question 63.
- **The contradiction.** No transport is called while a transaction is open. The floor is judged inside the caller's transaction now, and `SmsBalance.BelowFloorAsync`, where no reading stands inside `abuse.sms.pollinterval`, asks the gateway and records the reading in a unit of work of its own.
- **Parked.** That call, as it is.
- **Answer:** pending.

**111. Tier 2. AUTH-RECOV-007 and OPS-ALERT-003 against the governed send's answer.**

- **Item.** Question 27.
- **What the specification says.** The governed send answers "the admission or the refusal, never the delivery". AUTH-RECOV-007: a loss report "SHALL NOT complete if none of the notifications delivered". OPS-ALERT-003: mail that carries nothing falls to the text message, and the gateway that carries nothing is recorded.
- **What the code does.** An internal port of the area, `IFollowedSend`: the caller undertakes in a unit of work of its own, commits, the attempt runs, and the caller asks whether its rows are gone. `AlertRouter` and `LossReports` use it; both counted the first attempt before.
- **Readings.**
  1. As built.
  2. Both count the admission alone, which changes OPS-ALERT-003 criteria 1 and 4 and AUTH-RECOV-007 criterion 3.
- **Parked.** Nothing.
- **Answer:** pending.

**112. Tier 2. CONV-DESIGN-002 against CONV-ERR-003 criterion 2: a fault of the library's own in the attempt after the commit.**

- **Item.** Question 38.
- **What the code does.** A handler that refuses or throws is a failed attempt and stays with the publisher. A fault of the library's own inside the attempt (a setting that does not read, the database failing at the claim or at the outcome) leaves `CommitAsync` as an exception, so the operation answers `system.fault` although it committed. A catch that logs and carries on is what CONV-ERR-003 criterion 2 forbids.
- **Readings.**
  1. No catch, as built.
  2. The attempt's own faults are held and left to the publisher's pass, with the exemption stated in CONV-ERR-003.
- **Parked.** Nothing.
- **Answer:** pending.

**113. Tier 3. PRIV-RIGHT-005a: the erased key of an invitation.**

- **Item.** Question 39.
- **The contradiction.** PRIV-RIGHT-005a: "a wrapped key held with no marker (an outbox row's, an invitation's, a mailbox's) is the 32 zero bytes alone". `SubjectEraser` sets an attached invitation's wrapped key and its encrypted document to null, and `InvitationStore` nulls both at use and at expiry. D-183 question 39 names the mailbox's release and the subject key as what is brought to the value, and not the invitation.
- **Parked.** The invitation's erased key, as it is.
- **Answer:** pending.

**114. Tier 2. CONV-DESIGN-003 (the rule and criterion 9) and INF-BG-001 criterion 4: how a row that tracks several deliveries is claimed "each delivery apart".**

- **Item.** Question 61, for the event rows (`EventPublisher`) and the erasure outbox (`OutboxPublisher`, its ledger pass included).
- **What the code needs.** Both rows track several deliveries: `outbox` has a row of `outbox_confirmations` for each subscriber, written only once that subscriber confirms; `events` keeps the consumers that took the event in `taken_by`. In both, the attempts, the schedule, the status or failure, the alert at exhaustion and, for an erasure, the `erasures` row are kept for the row. To claim each delivery apart the code needs somewhere to hold a claim for a row and one subscriber or consumer, which no table has before confirmation, and a rule for who writes the row's attempt count, schedule, completion, failure and alert where two passes hold different deliveries of one row, and what an attempt then is.
- **What the specification says.** CONV-DESIGN-003: a row that background work carries out of the database, "each delivery apart where a row tracks several", is claimed by one conditional update, and the attempt's outcome is written by one update conditional on that claim. IDN-LIFE-003a counts attempts and fails for the record. LIB-API-001 retries an event for the row under `outbox.retry.*`. No chapter names where a delivery's claim is held or how the row's outcome is guarded.
- **Readings.**
  1. One claim for the row; its subscribers or consumers are called in turn under it, bounded by the claim's timeout as a whole; the outcome is one write conditional on the row's claim. `claimed_until` on `events` and `outbox`, the shape of the three carriers built. It does not match "each delivery apart".
  2. One claim for each delivery: a row for a delivery and its subscriber written at the claim (in `outbox_confirmations` with a nullable `confirmed_at`, or a new table), and for events a table of event and consumer; the row's own outcome then needs a rule of its own (a lock at settling, or a claim of the row as well).
  3. Both: a claim of the row for its outcome and a claim of each delivery for each call.
- **Also open.** Whether a claim is conditional on the row still being due. The mailbox publisher decides on the row as read under the claim; the send publisher does not, and can skip one interval of the backoff after another pass released the row.
- **Parked.** The event rows and the erasure outbox, unclaimed.
- **Answer:** pending.

**115. Tier 2. REG-SESS-005, AUTH-FACT-004, REG-IDENT-004, REG-IDENT-007 and AUTH-ABUSE-004 criterion 14: a held or reserved value at an account identifier's add or replace.**

- **Item.** Question 31.
- **What the code needs.** Somewhere to hold the staged value, so that a code record can be held against it, the list and the verifying endpoint can name it, and the sweep can remove it when one of a fresh value would be.
- **What the specification says.** D-183 question 31 gives such a value "a verification-code record like any other", swept on the same expiry. `IdentifierService` stages nothing for a held or reserved value today. `ux_identifiers_fingerprint` is unique on kind and fingerprint for every identifier row, verified or not, so an unverified row carrying a held value cannot be written; no chapter says how the staged value is stored or whether the list shows it.
- **Readings.**
  1. An unverified identifier row with the neutralised fingerprint, outside the index, with a pending verification and a record no code answers: no change of schema; the row's fingerprint no longer names its value.
  2. A pending verification with no identifier row: the list and the verifying endpoint need a way to name it.
  3. The index over verified rows alone: a migration, and a change to who may stage a value another holds.
- **Parked.** The record for a held or reserved value at add and replace, and its ask counted there. The registration's side is built.
- **Answer:** pending.

**116. Tier 2. D-166 306, REG-IDENT-007 criterion 4 and REG-IDENT-004 criterion 4: what the sweep of pending verifications waits for.**

- **Item.** Question 31.
- **What the code needs.** A rule the store can evaluate for "every code it sent is past its expiry".
- **What the specification says.** 306: every code it sent, "the new address's and, for a replace whose old address must confirm, that one as well", as the verification-code record holds them. The displaced address is sent a link alone, with no code record and no expiry anywhere; a replace whose new address has verified and waits on the old one has no live code record.
- **Readings.**
  1. The old address's confirmation takes a lifetime (which setting is unstated), held as a record or a column, and a press past it is refused; the sweep waits for both.
  2. The sweep takes only verifications whose new value is unverified and whose code record is gone or expired; a verified replace waiting on the old address stands until confirmed or abandoned, and still blocks a later replace.
  3. The sweep takes every verification with no live code record, which sweeps a replace the old address has not answered.
- **Also to settle with it.** The holder of an identifier's code is the SHA-256 of the identifier's bytes in the layout `Guid.TryWriteBytes` gives; a sweep that joins in SQL on `sha256(uuid_send(...))` needs the other byte order, one line of `IdentifierService` to change before any deployment holds records.
- **Parked.** The whole of 306: the sweep's rule, its schedule and its three tests.
- **Answer:** pending.

**117. Tier 2. AUTH-FACT-008 criterion 4 and `09` section 6: `POST /account/recoverycodes/exported` is not mounted.**

- **Item.** The codes D-183 adds.
- **What the code needs.** An endpoint and a contract operation that records the export. `RecoveryCodeService.ShownAsync` has no caller outside tests, so neither `viewedAt` nor `exportedAt` is set.
- **What the specification says.** `09` gives the route and its 409 `auth.factor.notenrolled`; no chapter names the operation of `ICredentials` it maps to.
- **Readings.**
  1. A new public operation on `ICredentials`, its name the owner's to give, and the mapping.
  2. The endpoint maps to the internal service as one of the exceptions of LIB-API-005, which then names it.
- **Parked.** The endpoint. The code's row and the service's refusal exist.
- **Answer:** pending.

**118. Tier 2. CONV-DESIGN-006 and CONV-DESIGN-004 criterion 2: a restriction's `{name}` and an app password's `{id}` have no typed value.**

- **Item.** Question 53.
- **What the specification says.** D-183 question 53: a restriction's name in the path is named because it binds through `IParsable<T>`. `09`: `{id}` is 400 where it "does not read as an app-password identifier". No type exists for either, and no chapter gives the rule of an app-password identifier.
- **Readings.**
  1. Public value types in `Janus.Core`, changing the text parameters of `IRestrictionSet` and `IAppPasswords`.
  2. Internal types in `Janus.Hosting` alone, the contracts keeping text, which CONV-DESIGN-004 criterion 2 reads against.
  3. Text, checked in the handler.
- **What the code does.** Reading 3 for the restriction's name alone, whose rule and answer are fixed. A request with an unreadable body and a name outside the rule names the body's member, not `name`.
- **Parked.** The refusal of an app password's `{id}`, whole; the type of a restriction's name.
- **Answer:** pending.

**119. Tier 2. CONV-DESIGN-006 criteria 3 and 4: what "the codes its `09` row gives" covers.**

- **Item.** Questions 50 and 51.
- **What the code needs.** One list of codes for each endpoint, such that no response a test receives carries a code outside it.
- **What the specification says.** An endpoint declares "the error codes its `09` row gives". The preamble of `09` section 6 gives `authz.restricted` and `authz.denied` for many account routes and says the routes below do not each list it; the preamble of section 8 and section 1 do the same for the administrative routes; section 8a is tables for a permission, not for a route.
- **Readings.**
  1. An endpoint declares its row and every preamble that governs it.
  2. The row alone, the preambles' answers derived as the mounting's are: a sentence of the `09` preambles names them as derived.
- **Measured.** The unit tests of `Janus.Hosting.Tests` receive 338 distinct answers (endpoint, status, code). Beyond the preambles' codes, these are answered and not in their rows: `POST /account/recoverycodes` 422 `auth.enrolment.tokeninvalid`; `POST /recovery/begin` 429 `auth.restriction.exceeded` (its row says 202 always); `POST /auth/webauthn/register/begin` and `/complete` 403 `authz.denied` (section 4, outside section 6's preamble). `GET /register/events` and a path outside the mount answer 404 with no code; the `/oidc/*` routes and the Google provider-event route answer bodies with no `code`. The cause of each was not traced.
- **Parked.** Questions 50 and 51, whole.
- **Answer:** pending.

**120. Tier 2. CONV-DESIGN-006 and API-CONV-003: the routes beyond the four question 53 names.**

- **Item.** Question 53.
- **What the code does.** `28c53a12` binds typed values on `/admin/access` (`resourceType`, `resourceId`), `/admin/grants` and `/admin/groups` (`organization`) and `/admin/audit` (`subject`), which bound text, and removes every `:guid` route constraint (41). An identifier that does not read is now 400 `api.request.malformed` naming it, where the route did not match and the answer was 404. It was handed back as resolved by rule; it changes an answer, so it is stated here.
- **What the specification says.** Every route or query value with a typed value binds through `IParsable<T>`, and one that does not read is 400 naming it. Question 53 lists four routes.
- **Readings.**
  1. The rule covers every such route. Built.
  2. The four routes alone: the constraints and the four other bindings go back.
- **Parked.** Nothing.
- **Answer:** pending.

**121. Tier 2. BFF-CSRF-001 criterion 2 and AUTH-SESS-007 criterion 2 against CONV-DESIGN-006: the files that may read endpoint metadata.**

- **Item.** Question 53.
- **What the code does.** `BrowserProfileTests.AUTH_SESS_007_AC2_NoEndpointCanOptOut` and `BrowserProfileTests.BFF_CSRF_001_AC2_NoEndpointCanBeExcludedByConfigurationOrAttribute` hold a fixed list of the files that read endpoint metadata. The error translation now reads an endpoint's declared values, so `EndpointDeclaration.cs`, `EndpointDeclarations.cs` and `MalformedRequest.cs` joined the list (`28c53a12`). The reader runs after the endpoint was reached and enforces no token. The tests guard the exclusion of an endpoint from the token's check, so the change is stated here and not as resolved by rule.
- **Readings.**
  1. The three files join the list. Built.
  2. The declared values reach the stage another way, and the list stands.
- **Parked.** Nothing.
- **Answer:** pending.

**122. Tier 3. AUTHZ-GATE-006: `AppPasswords.CreateAsync`, whose first write is outside any unit of work.**

- **Item.** Question 62.
- **The matter.** The gate is asked again "inside its unit of work before its first write". The operation's first write is the mail server's creation of the password, outside any unit of work; the unit of work that follows records the audit entry and the notice of what the server already did. The chapters do not state how such a site is built.
- **Parked.** The site, unchanged.
- **Answer:** pending.

**123. Tier 3. AUTHZ-GATE-006: `RecoveryService.SendAsync`, the approval's second unit of work.**

- **Item.** Question 62.
- **The matter.** The approval is two units of work: the first commits the approval and asks the gate again; the second writes the send. The chapters do not state whether the second asks again, nor what a restriction committed between the two leaves (an approval standing with nothing sent).
- **Parked.** `SendAsync`, unchanged.
- **Answer:** pending.

**124. Tier 3. AUTHZ-GATE-006: `ErasureService.CompleteAsync`, whose ledger line precedes its unit of work.**

- **Item.** Question 62.
- **The matter.** The ledger line is appended before the unit of work begins, so the first write precedes it.
- **Parked.** The site, unchanged.
- **Answer:** pending.

**125. Tier 3. AUTHZ-GATE-006 against OPS-ALERT-004a: the destination keys.**

- **Item.** Question 62.
- **The matter.** OPS-ALERT-004a sends the notice to the destinations being replaced before the change's unit of work begins; that delivery is the first effect and a rollback does not undo it. A refusal found by asking again inside the unit of work would follow the notice of a change that then did not happen.
- **Parked.** `ConfigurationService.DestinationsAsync` and `AlertDestinationChange.ChangeAsync`, unchanged; the gate step and the ask for a loosening inside the unit of work are as they were.
- **Answer:** pending.

## 5. Gate result

**`corrections-4`, at the stop at question 20.** Not run. The run stopped at question 20, before step 4 of the work
order, so the full gate was not run and no pull request was opened. The branch is pushed so
its commits can be read.
- At `7b45d2b`, the head of the code, in the repository's own checkout: build with
  warnings as errors, format, and the unit and contract tests, Analyzers 20,
  Authentication 832, Authorization 126, Cli 20, Conformance 2, Core 484, Hosting 763,
  Identity 89, Privacy 223, Storage 33, no failure; and the integration suites,
  `Janus.Storage.Tests` 425, `Janus.Cli.Tests` 66, `Janus.Conformance.Tests` 10,
  `Janus.Hosting.Tests` 233, no failure. The migration applied twice and the pipeline's
  own jobs were not run. `1cb2827` changes the ledger only.
- Secret scanning: the pinned scanner, run locally as the pipeline runs it, over the 873
  commits of the history at `1cb2827` before the push: no finding.
- Push runs on the reports of the earlier stops: 36558943751 on `40bd871`, 36566244433 on
  `927de12`, 36588064683 on `3e6b810`, 36597616186 on `f24e546`, 36610818396 on
  `870b325`, 36648263740 on `f0e149e`, 36651799340 on `87a6eb7`, 36658057913 on
  `60f5917`, 36672744133 on `575e0f7`, 36680791826 on `9a0c482` and 36702161811 on `c339c40`, all green.
- Fast checks at `ced2208` (build with warnings as errors, format, the unit and contract
  tests), in a scratch checkout of that commit so the uncommitted 340 work took no part:
  Analyzers 20, Authentication 813, Authorization 126, Cli 19, Core 468, Hosting 723,
  Identity 89, Privacy 222, Storage 33. One failure, of the scratch checkout and not of the
  code: `ProductNameTests.CONV_NAME_001_AC2_...` read the checkout's `.git` file, whose
  `gitdir` path names the product; in the repository `.git` is a directory.
- The integration suite of `Janus.Storage.Tests` at `ced2208`: 407, no failure. The other
  suites of the full gate were not run.
- Secret scanning: the pinned scanner, run locally as the pipeline runs it, over the 840
  commits of the history at `c656253` before the push: no finding.

**Pull request #6 (`phase-10-release`), merged as `b6d14fe`.**
- Pull request run 36217259244 on `cdc185a`: every required check green. `Secret scanning` runs on push only, and push run 36217256269 was green, with job 108335588796.
- Earlier runs:
  - 36203591546 on `f22becb`, failed: the conformance and pipe defects above.
  - 36215901211 on `ce19b71`, failed: REG_SESS_003, not re-run.
  - 36215899821, push on `ce19b71`, green.

**Pull request #4 (`corrections-3`) with D-167:**
- Push run 36201534531 failed the contract tests on `7a4d4dd`.
- Push run 36201936626 on `3d1b5d8` was green.
- Pull request run 36201939031 on `3d1b5d8` was green on attempt 2. Attempt 1 failed REG_SESS_003 alone, as above.

**`corrections-4`, at the end of the run.** Full gate on corrections-4 at `d9a8ecb5`, run once and locally, job by job as the gates workflow runs it (range base `b6d14fe`, the merge base with `main`). The branch is unpushed from `d5a7fc0e`, so no pipeline run exists for it yet.

| Job | Result |
|---|---|
| Locked restore | passed |
| Public surface files up to date (`release.sh`) | passed |
| Format | passed |
| Unit tests | passed, 2808 |
| Contract tests | passed, 132 |
| Unicode tables regenerate without a diff | passed |
| Integration tests | passed, 859 |
| Policy coverage test | passed, 3 |
| Truth-table suite (change check and suite) | passed, 69 |
| Double migration run | passed (against a `postgres:17-alpine` container; no release is tagged, so run two starts from the empty schema) |
| Janus.Analyzers rules, permitted outcome, forbidden log values | passed, 20 |
| Dependency allow-list | passed |
| InternalsVisibleTo allow-list | passed |
| Forbidden markers and commented-out code | passed |
| Acceptance-criterion test names | passed |
| Commit message format | failed: `863883c1` (type `style`) and `c25b633a` (a body line of 76 characters), question 43 |
| Changelog line present | passed |
| Destructive-operation detection report | passed with `DESTRUCTIVE_DDL_GATE` set to `disabled` for the run; the repository variable does not exist yet (question 54) |
| Dependency vulnerability alerting | passed |
| Secret scanning | not run locally: it downloads the pinned scanner, which waits on the owner's approval; the last full-history scan, at `814d8901`, found nothing |
