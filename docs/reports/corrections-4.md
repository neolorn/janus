# Corrections 4: D-166 and D-167

Status: stopped at open questions 4 (Tier 2, with a Tier 3 point), 5 (Tier 2) and 6
(Tier 3). D-168 settled questions 1 and 2 of the first stop, and D-169 settled question 3;
all three are applied. Of the rest of D-166, section D.8 is applied up to break-glass
part (7), where question 4 stops the run (section 2).

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

**Criteria no test decides.**
- D-166 319 (1), the signing algorithm: `token.signing.algorithm` admits only `ES256` at its reading, so `configure` refuses any other value before the check of `SigningKeys` is reached. The check stands in `CompleteAsync`; no value reaches its refusal. Verified by review.
- D-166 116 and X2, the sweep's completeness: every call of `IConfigurationStore` in the code was listed by a script and each one that turned a failure into a value is among the places above. Verified by review.

**Commit type of `c440f0a`.** It adds the key `integration.mailserver.endpoint` and its startup rule, with its changelog line, under the type `test`; the type is `feat`. The history is not rewritten.

## 2. Items not implemented

| Item | Reason | Waits on |
|---|---|---|
| D-166 D.8, break-glass part (7): the reason given at use, kept by the session and carried by every record it writes | Open question 4 | Question 4 |
| D-166 D.8, break-glass part (8): `GET /admin/break-glass` (`IBreakGlass.StandingAsync`, which it reads through, is in place) | Not reached: it follows part (7) | Question 4 |
| D-166 D.8, the ledger lines of the break-glass paragraph (291, 295, 297, 302, 331) | They go in the commit that completes the paragraph | Question 4 |
| D-166 D.8, the paragraphs after break-glass: 121 and 336 (the secret path), 316, 317, 318, 341, 303 (and the audit's subject), 304 and 334 parts (1) and (2), 323, the audit action rows | Not reached | Question 4 |
| D-166 section D.2, the entries after 114 (115, 129, 146, 152, 208, 328, 401, 402 and 422, 417, 419, 421, 326, and the preferred second step) | Not reached | Question 4 |
| D-166 section C, rules X1 and X3 to X9 as sweeps (X2 is applied, under 116; X3 on the configuration routes, under 178) | Not reached | Question 4 |
| D-166 sections D.1 to D.7 and D.9 to D.11 | Not reached | Question 4 |
| D-166 section E, every item other than E.6 | Not reached | Question 4 |
| D-166 section F, the rows of chapter 10 other than those applied under D.8 (the retired step-up and device verification codes, `model.startup.secretunavailable`, `config.change.reasonrequired`, the retired switches, `integration.mailserver.endpoint`, `breakglass-generated`) | Not reached. The three contract tests that failed at `aa7c5e9` now pass at `0dc0ae0` | Question 4 |
| D-166 section G, the ledger lines of the entries not yet applied | Each goes in the commit that applies its entry | Question 4 |
| Truth-table rows for D-166 entries 396 and 265 | They state the D-166 outcomes, so they belong with those fixes | Question 4 |
| The full gate, the pull request for `corrections-4` | The run stopped before step 4 of the work order (section 5) | Questions 4 to 6 |

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

**6. Tier 3. D-166 D.8 break-glass part (2): the limit-reached alert where no reserved account exists.**

- **Item.** D-166 D.8 (2): the first arrival the global limit refuses raises
  `auth-failures-sustained` "scoped to the reserved account".
- **What the code does** (`44dfdc5`). The scope is the reserved account's subject as
  `IEmergencyAccount` finds it; where none is found (a deployment never bootstrapped) the
  alert is raised with no scope. No chapter states what the alert is, or whether it is
  raised, where no reserved account exists. This is an alert on the break-glass gate; no
  proposal is made. The committed behaviour stands until the owner decides.

## 5. Gate result

**`corrections-4`.** Not run. The run stopped at questions 4 to 6, before step 4 of the work
order, so the full gate was not run and no pull request was opened. The branch is not
pushed: `origin/corrections-4` stands at `4d12ba1`, and every later commit is local.
- Fast checks at `0dc0ae0` (build with warnings as errors, format, the unit and contract
  tests): green. Analyzers 20, Authentication 812, Authorization 126, Cli 19, Core 468,
  Hosting 712, Identity 89, Privacy 222, Storage 33; no failure.
- The integration, migration, contract and conformance suites of the full gate were not run.
- Secret scanning: the pinned scanner, run locally as the pipeline runs it, over the 825
  commits of the history at `0dc0ae0` before the push: no finding.

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
