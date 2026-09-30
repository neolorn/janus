# Corrections 4: D-166 and D-167

Status: stopped at open question 20 (Tier 3). D-168 settled questions 1 and 2 of the
first stop, D-169 question 3, D-170 questions 4 to 6, D-171 question 7, D-172 questions
8 and 9, D-173 question 10, D-174 question 11, D-175 question 12, D-176 question 13,
D-177 question 14, D-178 questions 15 and 16, D-179 question 17 and D-180 questions 18
and 19; all are applied. Of the rest of D-166, section D.8 is applied up to 340 and 121
and 336, with D-171 item 3, D-166 343 and 349 with D-175, 215, 221, 263 and 270 with
D-176 to D-179; the signing credential of CONV-CODE-007 criterion 4, which completes the
key ring of D-171, stops the run at question 20 (section 2).

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

## 2. Items not implemented

| Item | Reason | Waits on |
|---|---|---|
| CONV-CODE-007 criterion 4 with D-171, the signing credential: a credential a rotation replaces leaves the server's options when its overlap ends | Open question 20 | Question 20 |
| `details.member` of an unreadable body member, named as API-CONV-002 criterion 4 now states (D-179) | Part of the section C sweep, X4 and X5 | Question 20 |
| D-166 362, but for its point (5) | Not reached | Question 20 |
| D-166 D.8, the paragraphs after 121 and 336: 317, 318, 341, 303 (and the audit's subject), 304 and 334 parts (1) and (2), 323, the audit action rows | Not reached: they follow the key ring in the log's order | Question 20 |
| D-166 section D.2, the entries after 114 (115, 129, 146, 152, 208, 328, 401, 402 and 422, 417, 419, 421, 326, and the preferred second step) | Not reached | Question 20 |
| D-166 section C, rules X1 and X3 to X9 as sweeps (X2 is applied, under 116; X3 on the configuration routes, under 178) | Not reached | Question 20 |
| D-166 sections D.1 to D.7 and D.9 to D.11 | Not reached | Question 20 |
| D-166 section E, every item other than E.6 | Not reached | Question 20 |
| D-166 section F, the rows of chapter 10 other than those applied under D.8 (the retired step-up and device verification codes, `model.startup.secretunavailable`, `config.change.reasonrequired`, the retired switches, `integration.mailserver.endpoint`, `breakglass-generated`) | Not reached. The three contract tests that failed at `aa7c5e9` now pass at `0dc0ae0` | Question 20 |
| D-166 section G, the ledger lines of the entries not yet applied | Each goes in the commit that applies its entry | Question 20 |
| Truth-table rows for D-166 entries 396 and 265 | They state the D-166 outcomes, so they belong with those fixes | Question 20 |
| The full gate, the pull request for `corrections-4` | The run stopped before step 4 of the work order (section 5) | Question 20 |

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

## 5. Gate result

**`corrections-4`.** Not run. The run stopped at question 20, before step 4 of the work
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
